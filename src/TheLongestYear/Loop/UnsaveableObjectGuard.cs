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
    /// hold anyway.</summary>
    internal static class UnsaveableObjectGuard
    {
        private static readonly Assembly GameAssembly = typeof(SObject).Assembly;

        private static IMonitor _monitor;
        private static XmlSerializer _objectSerializer;
        private static readonly List<(GameLocation Location, Vector2 Tile, SObject Obj)> _setAside = new();

        public static void Init(IMonitor monitor) => _monitor = monitor;

        [EventPriority(EventPriority.Low)]
        public static void OnSaving(object sender, SavingEventArgs e)
        {
            if (!RunActivation.IsActive || !Context.IsMainPlayer) return;
            PutBack();   // a write that threw never raised Saved

            var verdicts = new Dictionary<Type, bool>();
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
                return true;
            }, includeInteriors: true, includeGenerated: false);

            if (_setAside.Count == 0) return;
            string summary = string.Join(", ", _setAside
                .GroupBy(x => (x.Obj.GetType().FullName, x.Location.NameOrUniqueName))
                .Select(g => $"{g.Count()}x {g.Key.FullName} in {g.Key.NameOrUniqueName}"));
            _monitor?.Log(
                $"Save guard: set aside {_setAside.Count} object(s) the save cannot write ({summary}). Another mod left them " +
                "in the world at save time; they go back after the save so nothing changes in play.", LogLevel.Warn);
        }

        [EventPriority(EventPriority.High)]
        public static void OnSaved(object sender, SavedEventArgs e) => PutBack();

        public static void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e) => _setAside.Clear();

        private static void PutBack()
        {
            if (_setAside.Count == 0) return;
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
