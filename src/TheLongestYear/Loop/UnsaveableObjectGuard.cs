using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Serialization;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.SaveSerialization;
using TheLongestYear.Core;
using SObject = StardewValley.Object;

namespace TheLongestYear.Loop
{
    /// <summary>One placed object whose type the save cannot write must never cost the player the
    /// save. Mods that place their own Object subclasses (Farm Type Manager's buried items,
    /// breakable containers) take them out of the world before the game saves and put them back
    /// after; if one is left behind for any reason, the save throws "The type ... was not expected"
    /// and hangs on the saving screen. Just before the write (after every other mod's Saving
    /// handler), probe each non-game object type with the save's own serializer, set aside the ones
    /// it rejects, and put them back after the save, so the save succeeds and the world in memory
    /// is unchanged. Only those objects are missing from the file, which is all the save could ever
    /// hold anyway.
    ///
    /// Characters are never set aside. A mod character the save cannot write fails the save the
    /// same way (Farm Type Manager's MummyFTM after a duplicate day start, Celjhyn 1.2.0), but
    /// deciding that safely for another mod's NPC is not possible from here, so the guard only
    /// names any mod character it finds, so a failed save points at its cause in the log.</summary>
    internal static class UnsaveableObjectGuard
    {
        private static readonly Assembly GameAssembly = typeof(SObject).Assembly;

        /// <summary>How far from its own tile a set-aside object looks for a free one when its tile
        /// was taken while it was out.</summary>
        private const int PutBackRadius = 8;

        /// <summary>SpaceCore registers mod types with the save serializer (best-effort read, for the log only).</summary>
        private const string SpaceCoreId = "spacechase0.SpaceCore";
        private const string SpaceCoreTypeName = "SpaceCore.SpaceCore";
        private const string SpaceCoreModTypesField = "ModTypes";

        private static IMonitor _monitor;
        private static IModRegistry _registry;
        private static XmlSerializer _objectSerializer;
        private static readonly List<SetAsideObject> _setAside = new();

        /// <summary>One object out of the world for a save, and how many times putting it back failed.</summary>
        private sealed class SetAsideObject
        {
            public GameLocation Location { get; init; }
            public Vector2 Tile { get; init; }
            public SObject Obj { get; init; }
            public int Attempts { get; set; }
        }
        /// <summary>Mod character types already named at WARN this session.</summary>
        private static readonly HashSet<Type> _namedCharacterTypes = new();

        public static void Init(IMonitor monitor, IModRegistry registry)
        {
            _monitor = monitor;
            _registry = registry;
        }

        [EventPriority(EventPriority.Low)]
        public static void OnSaving(object sender, SavingEventArgs e)
        {
            if (!RunActivation.IsActive || !Context.IsMainPlayer) return;
            PutBack("Saving");   // a write that threw never raised Saved

            var verdicts = new Dictionary<Type, bool>();
            var characters = new List<(Type Type, string Location)>();
            Utility.ForEachLocation(location =>
            {
                List<Vector2> tiles = UnsaveableObjects.Pick(
                    location.objects.Pairs, type => type.Assembly == GameAssembly, CanWrite, verdicts);
                foreach (Vector2 tile in tiles)
                {
                    SObject obj = location.objects[tile];
                    // Recorded before it leaves the world, so nothing taken out can go unrecorded.
                    _setAside.Add(new SetAsideObject { Location = location, Tile = tile, Obj = obj });
                    location.objects.Remove(tile);
                }
                foreach (NPC npc in location.characters)
                    if (npc != null) characters.Add((npc.GetType(), location.NameOrUniqueName));
                return true;
            }, includeInteriors: true, includeGenerated: false);

            if (_setAside.Count > 0)
            {
                string summary = string.Join(", ", _setAside
                    .GroupBy(x => (x.Obj.GetType().FullName, x.Location.NameOrUniqueName))
                    .Select(g => $"{g.Count()}x {g.Key.FullName} in {g.Key.NameOrUniqueName}"));
                _monitor?.Log(
                    $"Save guard: set aside {_setAside.Count} object(s) the save cannot write ({summary}). Another mod left them " +
                    "in the world at save time; they go back after the save so nothing changes in play.", LogLevel.Warn);
            }
            NameModCharacters(characters);
        }

        [EventPriority(EventPriority.High)]
        public static void OnSaved(object sender, SavedEventArgs e) => PutBack("Saved");

        /// <summary>SMAPI raises no UpdateTicked while a save is running, so anything still set
        /// aside at a tick belongs to a save that ended without Saved (the write threw).</summary>
        public static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (_setAside.Count > 0)
                PutBack("the tick after a save that raised no Saved");
        }

        /// <summary>Leaving to the title discards the world, so nothing is put back: the objects
        /// were never in the save on disk, which is unaffected.</summary>
        public static void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            if (_setAside.Count == 0) return;
            _monitor?.Log(
                $"Save guard: returned to title with {_setAside.Count} object(s) still set aside; the world is discarded, so they " +
                "are let go (they were never in the save on disk).", LogLevel.Info);
            _setAside.Clear();
        }

        /// <summary>Put every set-aside object back. Each is tried on its own: one that throws stays
        /// listed for the next hook (up to <see cref="SetAsideRestore.MaxAttempts"/> tries, then it
        /// is given up with a warning), and the rest still go back. An object already in the world
        /// (its mod put it back, or the player has it) is left alone; one whose tile was taken goes
        /// to the nearest free tile, or onto the ground as a dropped item; nothing is thrown away.</summary>
        private static void PutBack(string when)
        {
            if (_setAside.Count == 0) return;
            int already = 0, back = 0, moved = 0, dropped = 0, failed = 0, total = _setAside.Count;
            var pending = new List<SetAsideObject>();
            try
            {
                foreach (SetAsideObject entry in _setAside)
                {
                    GameLocation location = entry.Location;
                    Vector2 tile = entry.Tile;
                    SObject obj = entry.Obj;
                    try
                    {
                        if (obj == null || location == null) continue;
                        bool ownTileFree = !location.objects.ContainsKey(tile);
                        DecorTile? free = null;
                        bool inWorld = IsInWorld(location, obj);
                        if (!inWorld && !ownTileFree)
                            free = DropSpot.Nearest(
                                new DecorTile((int)tile.X, (int)tile.Y),
                                (x, y) => IsFreeTile(location, new Vector2(x, y)),
                                PutBackRadius);

                        switch (SetAsideRestore.Decide(inWorld, ownTileFree, free.HasValue))
                        {
                            case SetAsideRestore.Placement.AlreadyBack:
                                already++;
                                break;
                            case SetAsideRestore.Placement.OwnTile:
                                location.objects[tile] = obj;
                                back++;
                                break;
                            case SetAsideRestore.Placement.NearbyTile:
                                var spot = new Vector2(free.Value.X, free.Value.Y);
                                obj.TileLocation = spot;
                                location.objects[spot] = obj;
                                moved++;
                                _monitor?.Log(
                                    $"Save guard: {obj.GetType().FullName} ({obj.Name}) in {location.NameOrUniqueName}: its tile {tile} was taken " +
                                    $"while it was out for the save, so it went to {spot}.", LogLevel.Info);
                                break;
                            default:
                                Game1.createItemDebris(obj, tile * Game1.tileSize, -1, location);
                                dropped++;
                                _monitor?.Log(
                                    $"Save guard: {obj.GetType().FullName} ({obj.Name}) in {location.NameOrUniqueName}: its tile {tile} was taken " +
                                    "and no free tile was near, so it was dropped on the ground there.", LogLevel.Info);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        entry.Attempts++;
                        bool keep = SetAsideRestore.KeepAfterFailure(entry.Attempts);
                        if (keep) pending.Add(entry);
                        _monitor?.Log(
                            $"Save guard: could not put back {obj?.GetType().FullName} at {tile} in {location?.NameOrUniqueName} " +
                            $"(try {entry.Attempts} of {SetAsideRestore.MaxAttempts}; {ex.GetType().Name}: {ex.Message}). " +
                            (keep ? "It is tried again at the next save hook." : "Giving up on it; it stays out of the world."),
                            LogLevel.Warn);
                    }
                }
            }
            finally
            {
                _setAside.Clear();
                _setAside.AddRange(pending);
                _monitor?.Log(
                    $"Save guard: put back {back + moved + dropped} of {total} object(s) set aside for the save ({when}; " +
                    $"{back} on their own tile, {moved} moved, {dropped} dropped, {already} already back, {failed} failed, " +
                    $"{pending.Count} still waiting).", LogLevel.Trace);
            }
        }

        /// <summary>The same instance is already in the world: in this location at any tile, or in
        /// the player's inventory.</summary>
        private static bool IsInWorld(GameLocation location, SObject obj)
        {
            foreach (SObject placed in location.objects.Values)
                if (ReferenceEquals(placed, obj)) return true;
            foreach (Item item in Game1.player?.Items ?? Enumerable.Empty<Item>())
                if (ReferenceEquals(item, obj)) return true;
            return false;
        }
        private static bool IsFreeTile(GameLocation location, Vector2 tile)
            => location.isTileOnMap(tile)
               && !location.objects.ContainsKey(tile)
               && !location.terrainFeatures.ContainsKey(tile)
               && location.CanItemBePlacedHere(tile);

        /// <summary>Detection only: name, once a session per type, every character from outside the
        /// game that is not registered with SpaceCore. If the save then fails with "The type ... was
        /// not expected", the line right before it says which mod left the character. Nothing is
        /// removed. Registered types are skipped when SpaceCore's list can be read; when it cannot,
        /// every mod character is named.</summary>
        private static void NameModCharacters(List<(Type Type, string Location)> characters)
        {
            ISet<Type> registered = RegisteredSerializerTypes();
            List<Type> unregistered = ForeignCharacters.Unregistered(
                characters.Select(c => c.Type), type => type.Assembly == GameAssembly, registered);
            foreach (Type type in unregistered)
            {
                bool first = _namedCharacterTypes.Add(type);
                string where = string.Join(", ", characters.Where(c => c.Type == type)
                    .GroupBy(c => c.Location).Select(g => $"{g.Count()}x in {g.Key}"));
                _monitor?.Log(
                    $"Save guard: a mod character the save may not be able to write is in the world: {type.FullName} " +
                    $"from {type.Assembly.GetName().Name} ({where}). If this save fails with \"The type ... was not expected\", " +
                    "that mod left it behind. Nothing is removed.",
                    first ? LogLevel.Warn : LogLevel.Trace);
            }
        }

        /// <summary>SpaceCore's registered serializer types, or an empty set when SpaceCore is not
        /// installed or its list cannot be read.</summary>
        private static ISet<Type> RegisteredSerializerTypes()
        {
            if (_registry == null || !_registry.IsLoaded(SpaceCoreId))
                return new HashSet<Type>();
            try
            {
                Type spaceCore = AccessTools.TypeByName(SpaceCoreTypeName);
                if (spaceCore != null && AccessTools.Field(spaceCore, SpaceCoreModTypesField)?.GetValue(null) is IEnumerable<Type> types)
                    return new HashSet<Type>(types.Where(t => t != null));
            }
            catch (Exception ex) when (ex is TargetInvocationException or FieldAccessException or InvalidOperationException)
            {
                _monitor?.Log($"Save guard: reading SpaceCore's serializer types failed ({ex.GetType().Name}: {ex.Message}).", LogLevel.Trace);
            }
            return new HashSet<Type>();
        }

        /// <summary>True if the serializer the save uses for a location's objects can write this
        /// one. That is the static value serializer of SerializableDictionary&lt;Vector2, Object&gt;
        /// (where the reported exceptions came from), so a type another mod registered with it
        /// (SpaceCore) passes.</summary>
        private static bool CanWrite(SObject obj)
        {
            try
            {
                using var writer = XmlWriter.Create(TextWriter.Null);
                ObjectSerializer().SerializeFast(writer, obj);
                return true;
            }
            catch (InvalidOperationException ex)
            {
                _monitor?.Log($"Save guard: the save cannot write {obj.GetType().FullName} ({ex.InnerException?.Message ?? ex.Message}).", LogLevel.Trace);
                return false;
            }
        }

        private static XmlSerializer ObjectSerializer()
        {
            if (_objectSerializer != null) return _objectSerializer;
            Type dict = typeof(SerializableDictionary<Vector2, SObject>);
            RuntimeHelpers.RunClassConstructor(dict.TypeHandle);
            _objectSerializer = dict.GetField("_valueSerializer", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) as XmlSerializer
                ?? SaveSerializer.GetSerializer(typeof(SObject));
            return _objectSerializer;
        }
    }
}
