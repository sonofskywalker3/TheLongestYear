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
    /// it rejects, and put them back on Saved, so the save succeeds and the world in memory is
    /// unchanged. Only those objects are missing from the file, which is all the save could ever
    /// hold anyway.
    ///
    /// Characters get the same treatment: a mod's monster or NPC subclass left in a location
    /// (Farm Type Manager's MummyFTM after a duplicate day start, Celjhyn 1.2.0) fails the save
    /// with the same exception. The game's own characters, and every pet, horse and child, are
    /// never touched.</summary>
    internal static class UnsaveableObjectGuard
    {
        private static readonly Assembly GameAssembly = typeof(SObject).Assembly;

        /// <summary>SpaceCore registers mod types with the save serializer and rebuilds it with them.</summary>
        private const string SpaceCoreId = "spacechase0.SpaceCore";
        private const string SpaceCoreTypeName = "SpaceCore.SpaceCore";
        private const string SpaceCoreModTypesField = "ModTypes";

        private static IMonitor _monitor;
        private static IModRegistry _registry;
        private static XmlSerializer _objectSerializer;
        private static bool _warnedUnknownSerializerTypes;
        private static readonly List<(GameLocation Location, Vector2 Tile, SObject Obj)> _setAside = new();
        private static readonly List<(GameLocation Location, NPC Npc)> _setAsideCharacters = new();

        public static void Init(IMonitor monitor, IModRegistry registry)
        {
            _monitor = monitor;
            _registry = registry;
        }

        [EventPriority(EventPriority.Low)]
        public static void OnSaving(object sender, SavingEventArgs e)
        {
            if (!RunActivation.IsActive || !Context.IsMainPlayer) return;
            PutBack();   // a write that threw never raised Saved

            var verdicts = new Dictionary<Type, bool>();
            HashSet<Type> knownCharacterTypes = KnownForeignCharacterTypes();
            var characterVerdicts = new Dictionary<Type, bool>();
            Utility.ForEachLocation(location =>
            {
                List<Vector2> tiles = UnsaveableObjects.Pick(
                    location.objects.Pairs, type => type.Assembly == GameAssembly, CanWrite, verdicts);
                foreach (Vector2 tile in tiles)
                {
                    SObject obj = location.objects[tile];
                    location.objects.Remove(tile);
                    _setAside.Add((location, tile, obj));
                }
                if (knownCharacterTypes != null)
                    SetAsideCharacters(location, knownCharacterTypes, characterVerdicts);
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
            if (_setAsideCharacters.Count > 0)
            {
                string summary = string.Join(", ", _setAsideCharacters
                    .GroupBy(x => (x.Npc.GetType().FullName, x.Location.NameOrUniqueName))
                    .Select(g => $"{g.Count()}x {g.Key.FullName} in {g.Key.NameOrUniqueName}"));
                _monitor?.Log(
                    $"Save guard: set aside {_setAsideCharacters.Count} character(s) the save cannot write ({summary}). Another mod " +
                    "left them in the world at save time; they go back after the save so nothing changes in play.", LogLevel.Warn);
            }
        }

        [EventPriority(EventPriority.High)]
        public static void OnSaved(object sender, SavedEventArgs e) => PutBack();

        public static void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            _setAside.Clear();
            _setAsideCharacters.Clear();
        }

        private static void PutBack()
        {
            if (_setAside.Count > 0)
            {
                int back = 0;
                foreach (var (location, tile, obj) in _setAside)
                {
                    if (location.objects.ContainsKey(tile)) continue;   // something else took the tile meanwhile
                    location.objects[tile] = obj;
                    back++;
                }
                _monitor?.Log($"Save guard: put back {back} of {_setAside.Count} object(s) set aside for the save.", LogLevel.Trace);
                _setAside.Clear();
            }
            if (_setAsideCharacters.Count > 0)
            {
                int back = 0;
                foreach (var (location, npc) in _setAsideCharacters)
                {
                    if (location.characters.Contains(npc)) continue;   // its mod already put it back
                    location.characters.Add(npc);
                    back++;
                }
                _monitor?.Log($"Save guard: put back {back} of {_setAsideCharacters.Count} character(s) set aside for the save.", LogLevel.Trace);
                _setAsideCharacters.Clear();
            }
        }

        /// <summary>Set aside the characters in a location whose type the save cannot write.</summary>
        private static void SetAsideCharacters(GameLocation location, HashSet<Type> known, Dictionary<Type, bool> verdicts)
        {
            if (location.characters.Count == 0) return;
            var entries = new List<KeyValuePair<int, NPC>>(location.characters.Count);
            for (int i = 0; i < location.characters.Count; i++)
                entries.Add(new KeyValuePair<int, NPC>(i, location.characters[i]));

            List<int> picked = UnsaveableObjects.Pick(entries, IsProtectedCharacterType, npc => CanWriteCharacter(npc, known), verdicts);
            if (picked.Count == 0) return;
            var remove = picked.Select(i => location.characters[i]).ToList();
            foreach (NPC npc in remove)
            {
                location.characters.Remove(npc);
                _setAsideCharacters.Add((location, npc));
            }
        }

        /// <summary>Never set aside: the game's own characters (villagers, monsters, junimos), and
        /// any pet, horse or child, even a mod's subclass of one.</summary>
        private static bool IsProtectedCharacterType(Type type)
            => type.Assembly == GameAssembly
               || typeof(StardewValley.Characters.Pet).IsAssignableFrom(type)
               || typeof(StardewValley.Characters.Horse).IsAssignableFrom(type)
               || typeof(StardewValley.Characters.Child).IsAssignableFrom(type);

        private static bool CanWriteCharacter(NPC npc, HashSet<Type> known)
        {
            bool writable = known.Contains(npc.GetType());
            if (!writable)
                _monitor?.Log($"Save guard: the save cannot write character type {npc.GetType().FullName} (not registered with the save serializer).", LogLevel.Trace);
            return writable;
        }

        /// <summary>The mod types the save serializer was taught (see <see cref="SerializerKnownTypes"/>),
        /// or null if SpaceCore is installed but its list cannot be read: then nothing is known
        /// for sure, and no character is set aside.</summary>
        private static HashSet<Type> KnownForeignCharacterTypes()
        {
            if (_registry == null || !_registry.IsLoaded(SpaceCoreId))
                return SerializerKnownTypes.Closure(Array.Empty<Type>());
            try
            {
                Type spaceCore = AccessTools.TypeByName(SpaceCoreTypeName);
                if (spaceCore != null && AccessTools.Field(spaceCore, SpaceCoreModTypesField)?.GetValue(null) is IEnumerable<Type> registered)
                    return SerializerKnownTypes.Closure(registered.ToList());
            }
            catch (Exception ex) when (ex is TargetInvocationException or FieldAccessException or InvalidOperationException)
            {
                _monitor?.Log($"Save guard: reading SpaceCore's serializer types failed ({ex.GetType().Name}: {ex.Message}).", LogLevel.Trace);
            }
            if (!_warnedUnknownSerializerTypes)
            {
                _warnedUnknownSerializerTypes = true;
                _monitor?.Log(
                    "Save guard: could not read the mod types SpaceCore adds to the save, so mod characters are not checked before saving.",
                    LogLevel.Warn);
            }
            return null;
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
