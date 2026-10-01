using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Loop
{
    internal sealed partial class JunimoStashService
    {
        /// <summary>
        /// Resolve the tile to place the stash chest at. Returns Vector2.Zero only if we
        /// cannot find any valid tile (extremely degenerate Farm state).
        /// </summary>
        private Vector2 ResolveTile(Farm farm)
        {
            bool isAuto = _config.StashTileX == 0 && _config.StashTileY == 0;
            Vector2 desired = isAuto ? AutoTile(farm) : new Vector2(_config.StashTileX, _config.StashTileY);

            // Empty Farm somehow, auto-pick gave Zero. Bail.
            if (isAuto && desired == Vector2.Zero)
                return Vector2.Zero;

            if (IsTilePlaceable(farm, desired))
                return desired;

            // Configured tile (or first auto candidate) is blocked. Walk the auto candidate
            // ladder looking for a clear tile near the farmhouse before falling back.
            Point? entryPoint = TryGetFarmHouseEntry(farm);
            if (entryPoint.HasValue)
            {
                foreach (Vector2 candidate in AutoCandidates(entryPoint.Value))
                {
                    if (candidate == desired) continue;  // already tried
                    if (!IsTilePlaceable(farm, candidate)) continue;

                    string source = isAuto
                        ? $"first auto candidate ({desired.X}, {desired.Y}) blocked by " +
                          $"{DescribeBlocker(farm, desired)}"
                        : $"configured tile ({desired.X}, {desired.Y}) blocked by " +
                          $"{DescribeBlocker(farm, desired)}";
                    _monitor.Log(
                        $"JunimoStashService: {source}; using fallback tile ({candidate.X}, {candidate.Y}). " +
                        "Run tly_setstash to anchor a different tile.",
                        LogLevel.Info);
                    return candidate;
                }
            }

            // Last resort: place at the desired tile anyway and hope the overlay is visible.
            // (Better than no chest at all, the player can use tly_setstash to relocate.)
            _monitor.Log(
                $"JunimoStashService: no clear tile near the farmhouse, placing at ({desired.X}, {desired.Y}) " +
                $"despite blocker ({DescribeBlocker(farm, desired)}). Use tly_setstash to relocate.",
                LogLevel.Warn);
            return desired;
        }

        /// <summary>Pick a tile three east + one south of the farmhouse entry. Path through
        /// the 2026-05-28 / 2026-05-29 playtests:
        ///   - (entry+2,+1)  : original, landed on the porch (blocked by Farmhouse building)
        ///   - (entry+2,+2)  : 2026-05-28 ladder fallback, "directly in front of the exit"
        ///   - (entry+4,+2)  : 2026-05-29 first retry, "in front of the mailbox" (which sits
        ///                     at (68, 16) on the Standard farm per Farm.cs:1483, so the
        ///                     chest at (68, 17) was the mail-reading tile)
        ///   - (entry+3,+2)  : 2026-05-29 second retry, "one space too low"
        ///   - (entry+3,+1)  : current, same column as before, one tile north. On Standard
        ///                     farm that's (67, 16): one tile west of the mailbox column,
        ///                     just clear of the porch's bottom edge. Returns Vector2.Zero
        ///                     if the entry is unavailable.</summary>
        private static Vector2 AutoTile(Farm farm)
        {
            Point? entry = TryGetFarmHouseEntry(farm);
            return entry.HasValue
                ? new Vector2(entry.Value.X + 3, entry.Value.Y + 1)
                : Vector2.Zero;
        }

        /// <summary>
        /// Ordered list of fallback candidate offsets relative to the farmhouse entry tile.
        /// The first candidate is the original "+2, +1" choice; subsequent entries are biased
        /// SOUTH and AWAY (-/+ X) of the entry so they clear the Farmhouse building footprint
        /// because the 2026-05-28 playtest showed (+2, +1) lands on the porch which intersects the
        /// Building.intersects rect. Tiles are tried in order; first one that <c>IsTilePlaceable</c>
        /// wins.
        /// </summary>
        private static System.Collections.Generic.IEnumerable<Vector2> AutoCandidates(Point entry)
        {
            // (dx, dy) offsets: 2026-05-29 v3: lead with (+3, +1), fall through south then
            // east past the mailbox column then west. Avoids both the porch (dx <=2 at +1) and
            // the mailbox column (dx=4) on Standard farm.
            (int dx, int dy)[] offsets =
            {
                ( 3, 1),  // new default, west-of-mailbox, just clear of porch
                ( 3, 2),
                ( 3, 3),
                ( 5, 1),  // jump past the mailbox column
                ( 5, 2),
                ( 2, 3),  // SE, deeper south than the old default
                ( 0, 3),  // straight south, far enough to clear porch
                (-2, 3),
                (-3, 2),  // wider west fallback
                (-4, 2),  // widest west fallback
            };

            foreach (var (dx, dy) in offsets)
                yield return new Vector2(entry.X + dx, entry.Y + dy);
        }

        /// <summary>Safe wrapper around <c>Farm.GetMainFarmHouseEntry</c>, returns null if the
        /// farmhouse isn't resolvable yet (very early load, degenerate state).</summary>
        private static Point? TryGetFarmHouseEntry(Farm farm)
        {
            try { return farm.GetMainFarmHouseEntry(); }
            catch { return null; }
        }

        /// <summary>
        /// True when the tile is clear enough that a chest placed there will be visible and
        /// reachable. Checks: no building, no resource clump, no existing object, no furniture
        /// (Keep Farm Decor can put outdoor furniture back first), no terrain feature other than
        /// a path or floor (trees and grass obscure the chest, which is exactly what burned the
        /// user on (72, 12)) and no large terrain feature (bush).
        /// </summary>
        private static bool IsTilePlaceable(Farm farm, Vector2 tile)
        {
            if (!farm.isTileOpenBesidesTerrainFeatures(tile))
                return false;
            // A path or floor under the chest is fine (Keep Farm Decor can restore one there).
            if (farm.terrainFeatures.TryGetValue(tile, out StardewValley.TerrainFeatures.TerrainFeature feature)
                && feature is not StardewValley.TerrainFeatures.Flooring)
                return false;
            var rect = new Microsoft.Xna.Framework.Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64);
            foreach (var ltf in farm.largeTerrainFeatures)
                if (ltf.getBoundingBox().Intersects(rect))
                    return false;
            foreach (var furniture in farm.furniture)
                if (furniture.boundingBox.Value.Intersects(rect))
                    return false;
            return true;
        }

        /// <summary>Best-effort human-readable blocker description for the log.</summary>
        private static string DescribeBlocker(Farm farm, Vector2 tile)
        {
            var rect = new Microsoft.Xna.Framework.Rectangle((int)tile.X * 64, (int)tile.Y * 64, 64, 64);
            foreach (var b in farm.buildings)
                if (b.intersects(rect))
                    return $"building '{b.buildingType.Value}'";
            foreach (var c in farm.resourceClumps)
                if (c.getBoundingBox().Intersects(rect))
                    return "resource clump";
            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj))
                return $"object '{obj?.QualifiedItemId ?? "?"}'";
            if (farm.terrainFeatures.ContainsKey(tile))
                return $"terrain feature '{farm.terrainFeatures[tile]?.GetType().Name ?? "?"}'";
            foreach (var ltf in farm.largeTerrainFeatures)
                if (ltf.getBoundingBox().Intersects(rect))
                    return $"large terrain feature '{ltf.GetType().Name}'";
            return "tile not passable";
        }
    }
}
