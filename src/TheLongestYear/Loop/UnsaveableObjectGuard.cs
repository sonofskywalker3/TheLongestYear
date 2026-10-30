using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml;
using System.Xml.Serialization;
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
    /// with the same exception. Each mod character type is tried once with the save's own NPC
    /// serializer, and only the ones it rejects as an unknown type are set aside. The game's own
    /// characters, and every pet, horse and child, are never touched.</summary>
    internal static class UnsaveableObjectGuard
    {
        private static readonly Assembly GameAssembly = typeof(SObject).Assembly;

        private static IMonitor _monitor;
        private static XmlSerializer _objectSerializer;
        private static XmlSerializer _characterSerializer;
        private static readonly List<(GameLocation Location, Vector2 Tile, SObject Obj)> _setAside = new();
        private static readonly List<(GameLocation Location, NPC Npc)> _setAsideCharacters = new();

        public static void Init(IMonitor monitor) => _monitor = monitor;

        [EventPriority(EventPriority.Low)]
        public static void OnSaving(object sender, SavingEventArgs e)
        {
            if (!RunActivation.IsActive || !Context.IsMainPlayer) return;
            PutBack("Saving");   // a write that threw never raised Saved

            var verdicts = new Dictionary<Type, bool>();
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
                SetAsideCharacters(location, characterVerdicts);
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
        public static void OnSaved(object sender, SavedEventArgs e) => PutBack("Saved");

        public static void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e)
        {
            _setAside.Clear();
            _setAsideCharacters.Clear();
        }

        private static void PutBack(string when)
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
        private static void SetAsideCharacters(GameLocation location, Dictionary<Type, bool> verdicts)
        {
            if (location.characters.Count == 0) return;
            var entries = new List<KeyValuePair<int, NPC>>(location.characters.Count);
            for (int i = 0; i < location.characters.Count; i++)
                entries.Add(new KeyValuePair<int, NPC>(i, location.characters[i]));

            List<int> picked = UnsaveableObjects.Pick(entries, IsProtectedCharacterType, CanWriteCharacter, verdicts);
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

        /// <summary>Ask the save's own NPC serializer to write this character to a discarded
        /// stream. <c>SaveSerializer.GetSerializer(typeof(NPC))</c> is the game's serializer for
        /// the type, and SpaceCore replaces it with one built with every type registered with it,
        /// the same list it rebuilds the save's location serializer with; so this answers exactly
        /// as the save would. Only a "type was not expected" failure sets the character aside. Any
        /// other failure says nothing about the type: the character stays and a WARN names it.</summary>
        private static bool CanWriteCharacter(NPC npc)
        {
            XmlSerializer serializer;
            try
            {
                serializer = _characterSerializer ??= SaveSerializer.GetSerializer(typeof(NPC));
            }
            catch (Exception ex)
            {
                _monitor?.Log($"Save guard: could not get the save's character serializer ({ex.GetType().Name}: {ex.Message}); {npc.GetType().FullName} is left in place.", LogLevel.Warn);
                return true;
            }

            SerializerProbe.Verdict verdict = SerializerProbe.Probe(serializer, npc, out Exception error);
            switch (verdict)
            {
                case SerializerProbe.Verdict.TypeRejected:
                    _monitor?.Log($"Save guard: the save cannot write character type {npc.GetType().FullName} ({error?.InnerException?.Message ?? error?.Message}).", LogLevel.Trace);
                    return false;
                case SerializerProbe.Verdict.ProbeFailed:
                    _monitor?.Log(
                        $"Save guard: checking {npc.GetType().FullName} ({npc.Name}) failed for a reason other than its type " +
                        $"({error?.GetType().Name}: {error?.InnerException?.Message ?? error?.Message}); it is left in place.", LogLevel.Warn);
                    return true;
                default:
                    return true;
            }
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
