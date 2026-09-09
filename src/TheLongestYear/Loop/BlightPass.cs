using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TerrainFeatures;
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
            var circles = CircleOfWardingService.ProtectedTiles();
            foreach (KeyValuePair<Vector2, TerrainFeature> pair in farm.terrainFeatures.Pairs)
                if (pair.Value is HoeDirt dirt && dirt.crop != null && !dirt.crop.dead.Value
                    && !CircleOfWardingService.Covers(circles, farm, pair.Key))
                    tiles.Add(pair.Key);
            tiles.Sort((a, b) => a.Y != b.Y ? a.Y.CompareTo(b.Y) : a.X.CompareTo(b.X));
            return tiles;
        }

        /// <summary>Kill up to <paramref name="count"/> crops chosen by <paramref name="rng"/>.
        /// Returns how many actually died.</summary>
        public static int Strike(int count, Random rng)
        {
            List<Vector2> tiles = LiveCropTiles();
            if (tiles.Count == 0 || count <= 0) return 0;
            Farm farm = Game1.getFarm();
            int killed = 0;
            foreach (int index in BlightRule.PickIndexes(tiles.Count, count, rng))
            {
                if (farm.terrainFeatures.TryGetValue(tiles[index], out TerrainFeature tf)
                    && tf is HoeDirt dirt && dirt.crop != null && !dirt.crop.dead.Value)
                {
                    dirt.crop.Kill();
                    killed++;
                }
            }
            return killed;
        }

        /// <summary>The nightly roll for one season: how many to kill, or 0 when nothing dies.</summary>
        public static int CountFor(CoreSeason season) => BlightRule.Count(LiveCropTiles().Count, season);
    }
}
