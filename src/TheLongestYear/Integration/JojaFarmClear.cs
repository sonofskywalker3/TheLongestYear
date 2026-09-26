using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Characters;
using StardewValley.TerrainFeatures;
using SObject = StardewValley.Object;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's bulldozer: empties the whole farm and paves it, so the scene's real
    /// coops, barns and animals stand on concrete whatever the player built or planted there.
    /// <see cref="ClearAll"/> removes objects, terrain features (crops, trees, grass, flooring),
    /// resource clumps, large terrain features (bushes), every building except the main farmhouse
    /// (the scene hides that instead), the farm animals, outdoor furniture, pets and horses. The mod's
    /// own tagged objects (modData keys starting "tly", the Junimo Stash) are lifted off for the scene
    /// and put back by <see cref="RestoreHeld"/>.
    ///
    /// IN MEMORY ONLY. This is safe only because the bad ending always ends at the title without
    /// saving: JojaBadEndingCommands fails closed to the title if the scene ends any other way, and
    /// its tlyClearFarm / tlyPaveFarm commands refuse to run outside the scene.</summary>
    internal static class JojaFarmClear
    {
        private const string ModDataPrefix = "tly";
        // The mod's own farm objects (the Junimo Stash chest) are never deleted, even in memory: the
        // whole-farm clear lifts them off the farm for the scene and Restore puts them back.
        private static Farm _heldOn;
        private static readonly List<(Vector2 Tile, SObject Obj)> Held = new();

        /// <summary>The whole farm (Jeff, 2026-09-25: "the whole farm needs to be cleared"): everything
        /// <see cref="Clear"/> takes over the entire map, plus the player's farm animals outside, any
        /// outdoor furniture, and the pets and horses (not drawn in an event, but they would stand in
        /// the new animals' way). Returns a count summary for the log.</summary>
        public static string ClearAll(Farm farm)
        {
            var map = new Rectangle(0, 0, farm.Map.Layers[0].LayerWidth, farm.Map.Layers[0].LayerHeight);
            string cleared = Clear(farm, map);
            _heldOn = farm;
            foreach (Vector2 tile in farm.objects.Keys.ToList())
            {
                if (!farm.objects.TryGetValue(tile, out SObject obj)) continue;
                Held.Add((tile, obj));
                farm.objects.Remove(tile);
            }
            int animals = farm.animals.Length;
            farm.animals.Clear();
            int furniture = farm.furniture.Count;
            farm.furniture.Clear();
            int critters = 0;
            foreach (NPC npc in farm.characters.ToList())
            {
                if (npc is not Pet && npc is not Horse) continue;
                farm.characters.Remove(npc);
                critters++;
            }
            return $"{cleared}, {animals} farm animals, {furniture} furniture, {critters} pets/horses, {Held.Count} mod objects set aside";
        }

        public static bool HasHeld => Held.Count > 0;

        /// <summary>Puts the mod's own objects back where they were.</summary>
        public static void RestoreHeld()
        {
            foreach (var (tile, obj) in Held)
                _heldOn.objects[tile] = obj;
            Held.Clear();
            _heldOn = null;
        }

        /// <summary>Lays the flooring on every tile the player could lay it on (the flooring item's own
        /// canBePlacedHere, Object.cs 5768: buildings ignored, so flooring may go under the new
        /// ones), plus the hidden farmhouse's own lot: the house is gone for the scene and
        /// Data/Buildings lets flooring lie under a Farmhouse (AllowsFlooringUnderneath), and left
        /// out it read as a lawn in the middle of the concrete (live 2026-09-25; Jeff: "concrete
        /// across the whole map"). Returns the number of tiles paved.</summary>
        public static int Pave(Farm farm, string floorId)
        {
            if (!Game1.floorPathData.TryGetValue(floorId, out var data) || data?.ItemId == null)
                throw new System.ArgumentException($"no flooring '{floorId}' in Data/FloorsAndPaths");
            if (ItemRegistry.Create(data.ItemId) is not SObject floorItem)
                throw new System.ArgumentException($"flooring '{floorId}' item {data.ItemId} is not an object");
            Building house = farm.GetMainFarmHouse();
            int width = farm.Map.Layers[0].LayerWidth, height = farm.Map.Layers[0].LayerHeight;
            int paved = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var tile = new Vector2(x, y);
                    if (farm.terrainFeatures.ContainsKey(tile)) continue;
                    bool lot = house != null && house.occupiesTile(tile) && farm.getTileIndexAt(x, y, "Back") >= 0 && !farm.isWaterTile(x, y);
                    if (!lot && !floorItem.canBePlacedHere(farm, tile, CollisionMask.All, showError: false)) continue;
                    farm.terrainFeatures.Add(tile, new Flooring(floorId));
                    paved++;
                }
            return paved;
        }

        /// <summary>Clears the rectangle (tiles) and returns a count summary for the log.</summary>
        public static string Clear(Farm farm, Rectangle tiles)
        {
            Rectangle pixels = new(tiles.X * 64, tiles.Y * 64, tiles.Width * 64, tiles.Height * 64);

            int objects = 0;
            foreach (Vector2 tile in farm.objects.Keys.ToList())
            {
                if (!tiles.Contains((int)tile.X, (int)tile.Y)) continue;
                if (farm.objects.TryGetValue(tile, out SObject obj) && obj.modData.Keys.Any(k => k.StartsWith(ModDataPrefix))) continue;
                farm.objects.Remove(tile);
                objects++;
            }

            int features = 0;
            foreach (Vector2 tile in farm.terrainFeatures.Keys.ToList())
            {
                if (!tiles.Contains((int)tile.X, (int)tile.Y)) continue;
                farm.terrainFeatures.Remove(tile);
                features++;
            }

            int clumps = 0;
            foreach (ResourceClump clump in farm.resourceClumps.ToList())
            {
                if (!clump.getBoundingBox().Intersects(pixels)) continue;
                farm.resourceClumps.Remove(clump);
                clumps++;
            }

            int large = 0;
            foreach (LargeTerrainFeature feature in farm.largeTerrainFeatures.ToList())
            {
                if (!feature.getBoundingBox().Intersects(pixels)) continue;
                farm.largeTerrainFeatures.Remove(feature);
                large++;
            }

            int buildings = 0;
            Building house = farm.GetMainFarmHouse();
            var gone = new List<string>();
            foreach (Building building in farm.buildings.ToList())
            {
                if (building == house) continue;
                var footprint = new Rectangle(building.tileX.Value, building.tileY.Value, building.tilesWide.Value, building.tilesHigh.Value);
                if (!footprint.Intersects(tiles)) continue;
                farm.buildings.Remove(building);
                gone.Add(building.buildingType.Value);
                buildings++;
            }

            string names = gone.Count > 0 ? $" [{string.Join(", ", gone)}]" : "";
            return $"{objects} objects, {features} terrain features, {clumps} clumps, {large} large features, {buildings} buildings{names}";
        }
    }
}
