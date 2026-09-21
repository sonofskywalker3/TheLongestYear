using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>The blight front's hands (spec 2026-09-09): live crops on the Farm die in the night,
    /// the way vanilla kills an out-of-season crop (<c>Crop.Kill</c>), so they draw withered and
    /// the player sees exactly what was taken. Farm only: the greenhouse, indoor pots and every
    /// other map are exempt.</summary>
    internal static class BlightPass
    {
        /// <summary>Tiles on the farm holding a live, non-dead crop, in a stable order so the
        /// seeded roll picks the same crops on a re-sleep.</summary>
        public static List<Vector2> LiveCropTiles()
        {
            var tiles = new List<Vector2>();
            Farm farm = Game1.getFarm();
            if (farm == null) return tiles;
            // No Circle of Warding exemption here any more: the circle wards CHESTS, not crops. A
            // rug and a crop cannot share a tile in any way that plays (Jeff, 2026-09-11): the rug
            // draws under tilled soil, the watering can picks the rug up instead of watering, and
            // once a seed is in the ground the rug will not go back down on it or beside it.
            foreach (KeyValuePair<Vector2, TerrainFeature> pair in farm.terrainFeatures.Pairs)
                if (pair.Value is HoeDirt dirt && dirt.crop != null && !dirt.crop.dead.Value)
                    tiles.Add(pair.Key);
            tiles.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return tiles;
        }

        /// <summary>Which crops die tonight, chosen by <paramref name="rng"/>. Reads only, so the
        /// pick can be made at day end and the killing left to the scene (spec 2026-09-21).</summary>
        public static List<Vector2> Pick(int count, Random rng)
        {
            List<Vector2> tiles = LiveCropTiles();
            var picked = new List<Vector2>();
            if (tiles.Count == 0 || count <= 0) return picked;
            foreach (int index in BlightRule.PickIndexes(tiles.Count, count, rng))
                picked.Add(tiles[index]);
            return picked;
        }

        /// <summary>Kill the crops on these tiles. A tile whose crop is gone or already dead is
        /// passed over, so calling it twice is harmless. Returns how many died.</summary>
        public static int Kill(IEnumerable<Vector2> tiles)
        {
            Farm farm = Game1.getFarm();
            if (farm == null) return 0;
            int killed = 0;
            foreach (Vector2 tile in tiles)
            {
                if (farm.terrainFeatures.TryGetValue(tile, out TerrainFeature tf)
                    && tf is HoeDirt dirt && dirt.crop != null && !dirt.crop.dead.Value)
                {
                    dirt.crop.Kill();
                    // Kill only flips the flag. The withered sprite comes from Crop.sourceRect,
                    // which is cached and only rebuilt by updateDrawMath. Vanilla gets away with it
                    // because it always kills inside Crop.newDay, which calls updateDrawMath a few
                    // lines later (Crop.cs:922). The overnight scene kills while the player is
                    // LOOKING at the row, so without this the crops stay green until the next time
                    // something else touches them (screenshots, 2026-09-21).
                    dirt.crop.updateDrawMath(tile);
                    killed++;
                }
            }
            return killed;
        }

        /// <summary>Pick and kill in one call: the debug entry point, and the shape the night pass
        /// had before the pick and the apply were split.</summary>
        public static int Strike(int count, Random rng) => Kill(Pick(count, rng));

        /// <summary>The nightly roll for one season: how many to kill, or 0 when nothing dies.</summary>
        public static int CountFor(CoreSeason season, DifficultyStep level) => BlightRule.Count(LiveCropTiles().Count, season, level);
    }
}
