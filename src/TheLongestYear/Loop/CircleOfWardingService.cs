using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>Circle of Warding (Jeff, 2026-09-09): a 3x3 rug the Junimos charge for the player,
    /// bought at the shrine up to three times on an escalating ladder. Whatever stands on its nine
    /// tiles is beyond the darkness's reach: crops there never blight, chests there never spoil or
    /// lose a thing. Placeable anywhere, indoors or out, so where the circles go is the player's
    /// storage plan. Owned circles are re-granted every loop (the reset wipes the world and the
    /// inventory), and the count is kept exact the way the books are.</summary>
    internal sealed class CircleOfWardingService
    {
        internal const string CircleId = "sonofskywalker3.TheLongestYear_CircleOfWarding";
        internal const string QualifiedId = "(F)" + CircleId;
        internal const string TextureAsset = "Mods/sonofskywalker3.TheLongestYear/CircleOfWarding";
        private const int Size = 3;

        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;

        public CircleOfWardingService(IMonitor monitor, MetaStore meta, IModHelper helper)
        {
            _monitor = monitor;
            _meta = meta;
            helper.Events.Content.AssetRequested += OnAssetRequested;
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (e.NameWithoutLocale.IsEquivalentTo(TextureAsset))
            {
                e.LoadFromModFile<Texture2D>("assets/circle_of_warding.png", AssetLoadPriority.Medium);
                return;
            }
            if (e.NameWithoutLocale.IsEquivalentTo("Data/Furniture"))
            {
                e.Edit(asset =>
                {
                    var data = asset.AsDictionary<string, string>().Data;
                    // rug, 3x3 tiles, 3x3 bounding box, one rotation, no price, restriction 2 =
                    // indoors or outdoors. Backslash texture path: see PlanningShrineService.
                    string tex = TextureAsset.Replace('/', '\\');
                    data[CircleId] = $"Circle of Warding/rug/{Size} {Size}/{Size} {Size}/1/0/2/{Strings.Get("furniture.circle-of-warding")}/0/{tex}";
                }, AssetEditPriority.Default);
            }
        }

        /// <summary>How many circles the player owns (the shrine ladder).</summary>
        public int OwnedCount() => WardIds.CircleCount(_meta.State.HasUpgrade);

        /// <summary>Per-loop invariant: exactly as many circles exist (placed anywhere, carried, or
        /// in a chest) as the player owns. Missing ones go to the inventory or the ground; extras
        /// are removed from the inventory first, then from chests, never from the world.</summary>
        public void Reconcile()
        {
            Farmer p = Game1.player;
            if (p == null) return;
            int owned = OwnedCount();
            int placed = 0;
            var carried = new List<int>();
            var stored = new List<(Chest Chest, int Slot)>();
            Utility.ForEachLocation(loc =>
            {
                foreach (Furniture f in loc.furniture)
                    if (f.ItemId == CircleId) placed++;
                foreach (StardewValley.Object obj in loc.objects.Values)
                    if (obj is Chest chest)
                        for (int i = 0; i < chest.Items.Count; i++)
                            if (chest.Items[i]?.ItemId == CircleId) stored.Add((chest, i));
                return true;
            });
            for (int i = 0; i < p.Items.Count; i++)
                if (p.Items[i]?.ItemId == CircleId) carried.Add(i);

            int have = placed + carried.Count + stored.Count;
            if (have == owned) return;
            if (have < owned)
            {
                for (int n = have; n < owned; n++)
                {
                    Furniture circle = Furniture.GetFurnitureInstance(CircleId);
                    if (!p.addItemToInventoryBool(circle))
                        Game1.createItemDebris(circle, new Vector2(p.StandingPixel.X, p.StandingPixel.Y), p.FacingDirection, p.currentLocation);
                }
                _monitor.Log($"Circle of Warding: granted {owned - have} (owned {owned}, had {have}).", LogLevel.Info);
                return;
            }
            int extra = have - owned;
            for (int i = carried.Count - 1; i >= 0 && extra > 0; i--, extra--) p.Items[carried[i]] = null;
            for (int i = stored.Count - 1; i >= 0 && extra > 0; i--, extra--) stored[i].Chest.Items[stored[i].Slot] = null;
            _monitor.Log($"Circle of Warding: removed {have - owned - extra} extra copies (owned {owned}).", LogLevel.Info);
        }

        /// <summary>Every tile under a placed circle, per location name.</summary>
        public static Dictionary<string, HashSet<Vector2>> ProtectedTiles()
        {
            var result = new Dictionary<string, HashSet<Vector2>>();
            Utility.ForEachLocation(loc =>
            {
                foreach (Furniture f in loc.furniture)
                {
                    if (f.ItemId != CircleId) continue;
                    if (!result.TryGetValue(loc.NameOrUniqueName, out HashSet<Vector2> tiles))
                        result[loc.NameOrUniqueName] = tiles = new HashSet<Vector2>();
                    Vector2 at = f.TileLocation;
                    for (int x = 0; x < Size; x++)
                        for (int y = 0; y < Size; y++)
                            tiles.Add(new Vector2(at.X + x, at.Y + y));
                }
                return true;
            });
            return result;
        }

        public static bool Covers(Dictionary<string, HashSet<Vector2>> protectedTiles, GameLocation loc, Vector2 tile)
            => protectedTiles != null && loc != null
               && protectedTiles.TryGetValue(loc.NameOrUniqueName, out HashSet<Vector2> tiles) && tiles.Contains(tile);
    }
}
