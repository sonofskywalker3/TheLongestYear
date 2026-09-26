using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Locations;
using TheLongestYear.Core.Joja;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's factory farm (Jeff, 2026-09-25: "multiple barns and coops side by
    /// side across the whole farm and pan across all of them ... do the floors and buildings for
    /// real"). Runs on the cleared, paved farm: lays out rows with <see cref="JojaFarmLayout"/> over
    /// the farm's own <c>isBuildable</c> (so every farm type gets its own rows), puts up real
    /// buildings in <c>farm.buildings</c>, fully built with their interiors, and real animals housed in
    /// them, then returns the commands that pan across every row (tlyPanTo) while the animals go in.
    /// IN MEMORY ONLY, like the rest of the bad ending's farm.</summary>
    internal static class JojaFactoryFarm
    {
        internal const string BarnType = "Deluxe Barn", CoopType = "Deluxe Coop";
        private static readonly string[] Kinds = { BarnType, CoopType };
        private static readonly string[][] Residents =
        {
            new[] { "White Cow", "Brown Cow", "Goat", "Sheep", "Pig" },
            new[] { "White Chicken", "Brown Chicken", "Duck" },
        };
        private const int Lane = 4;             // tiles under a row where its animals stand
        private const int SpriteRows = 7;       // coop and barn sprites are 7 tiles tall
        private const int Gap = 1;              // tiles between buildings in a row
        private const int MinPerRow = 2, MaxRows = 5;
        private const int AnimalsPerBuilding = 3;
        private const double AnimalSpacing = 2.0;
        private const int Seed = 20260925;

        private const int PanMsPerTile = 170, MinPanMs = 1200;
        private const int SettleMs = 1500, RowStartPauseMs = 500, RowEndPauseMs = 900, AfterMs = 1500;
        private const int WaitTimeoutMs = 8000;
        private const int RowCentreDy = -1;     // a row reads from its sprite tops to its lane: centre one above the bottom

        /// <summary>Puts up the rows and their animals; returns a log summary and the pan script.</summary>
        internal static (string Summary, List<string> Script) Build(Farm farm)
        {
            var sizes = Kinds.Select(k => Game1.buildingData.TryGetValue(k, out BuildingData d) ? (d.Size.X, d.Size.Y) : throw new InvalidOperationException($"no building data for {k}")).ToList();
            int width = farm.Map.Layers[0].LayerWidth, height = farm.Map.Layers[0].LayerHeight;
            IReadOnlyList<FarmBuildingRow> rows = JojaFarmLayout.Rows((x, y) => farm.isBuildable(new Vector2(x, y)),
                width, height, sizes, Lane, SpriteRows, Gap, MinPerRow, MaxRows);

            int seed = Seed, buildings = 0, animals = 0, skipped = 0;
            var rowNotes = new List<string>();
            foreach (FarmBuildingRow row in rows)
            {
                foreach (FarmBuildingSpot spot in row.Spots)
                {
                    Building building = Place(farm, Kinds[spot.Kind], spot.X, spot.Y, sizes[spot.Kind]);
                    if (building == null) { skipped++; continue; }
                    buildings++;
                    var rng = new Random(seed + 1);
                    foreach (var (x, y) in JojaFarmLayout.LaneSpots(spot.X, sizes[spot.Kind].Item1, row.Bottom, Lane, AnimalsPerBuilding, AnimalSpacing, seed))
                    {
                        string[] pool = Residents[spot.Kind];
                        AddAnimal(farm, building, pool[rng.Next(pool.Length)], x, y);
                        animals++;
                    }
                    seed += 2;
                }
                rowNotes.Add($"bottom {row.Bottom}: {row.Spots.Count} from x {row.Left}");
            }
            string summary = $"{rows.Count} rows ({string.Join("; ", rowNotes)}), {buildings} buildings, {animals} animals"
                             + (skipped > 0 ? $", {skipped} spots refused" : "");
            return (summary, PanScript(rows, sizes));
        }

        /// <summary>A real building, fully built: the same steps Building.Update takes for a new
        /// building on its first tick (Building.cs 1360: ReloadBuildingData for construction, then
        /// load), so it has its interior. The door starts shut; the animals' evening opens it.
        /// Refuses a footprint the farm no longer calls buildable.</summary>
        private static Building Place(Farm farm, string type, int x, int y, (int W, int H) size)
        {
            for (int ty = y; ty < y + size.H; ty++)
                for (int tx = x; tx < x + size.W; tx++)
                    if (!farm.isBuildable(new Vector2(tx, ty))) return null;
            Building building = Building.CreateInstanceFromId(type, new Vector2(x, y));
            building.daysOfConstructionLeft.Value = 0;
            building.owner.Value = Game1.player.UniqueMultiplayerID;
            farm.buildings.Add(building);
            building.ReloadBuildingData(forUpgrade: false, forConstruction: true);
            building.load();
            building.animalDoorOpen.Value = false;
            building.animalDoorOpenAmount.Value = 0f;
            return building.GetIndoors() is AnimalHouse ? building : Remove(farm, building);
        }

        private static Building Remove(Farm farm, Building building)
        {
            farm.buildings.Remove(building);
            return null;
        }

        /// <summary>A grown animal of <paramref name="type"/> standing on (x, y), living in
        /// <paramref name="home"/> (what AnimalHouse.adoptAnimal sets, without its dialogue events).</summary>
        private static void AddAnimal(Farm farm, Building home, string type, int x, int y)
        {
            var animal = new FarmAnimal(type, Game1.Multiplayer.getNewID(), Game1.player.UniqueMultiplayerID);
            animal.growFully();
            animal.currentProduce.Value = null;             // no truffles dug up mid-scene
            animal.ReloadTextureIfNeeded(forceReload: true);
            animal.home = home;
            ((AnimalHouse)home.GetIndoors()).animalsThatLiveHere.Add(animal.myID.Value);
            animal.Position = new Vector2(x * Game1.tileSize, y * Game1.tileSize);
            animal.currentLocation = farm;
            farm.animals.Add(animal.myID.Value, animal);
            JojaFarmAnimals.Add(animal, home);
        }

        /// <summary>Settle, arm the go-home signal, then pan along every row in turn (left to right,
        /// then back the other way), each leg paced by its length; then wait for the last animal in.</summary>
        private static List<string> PanScript(IReadOnlyList<FarmBuildingRow> rows, IReadOnlyList<(int W, int H)> sizes)
        {
            var s = new List<string> { $"pause {SettleMs}", JojaBadEndingCommands.AnimalsHomeName };
            float halfView = Game1.viewport.Width / (2f * Game1.tileSize);
            var at = new Vector2((Game1.viewport.X + Game1.viewport.Width / 2f) / Game1.tileSize, (Game1.viewport.Y + Game1.viewport.Height / 2f) / Game1.tileSize);
            for (int i = 0; i < rows.Count; i++)
            {
                FarmBuildingRow row = rows[i];
                FarmBuildingSpot last = row.Spots[row.Spots.Count - 1];
                int right = last.X + sizes[last.Kind].W - 1;
                int from = (int)Math.Round(row.Left + halfView - 1), to = (int)Math.Round(right - halfView + 1);
                if (to < from) from = to = (row.Left + right) / 2;
                if (i % 2 == 1) (from, to) = (to, from);
                int y = row.Bottom + RowCentreDy;
                s.Add(Pan(ref at, from, y));
                s.Add($"pause {RowStartPauseMs}");
                if (to != from) s.Add(Pan(ref at, to, y));
                s.Add($"pause {RowEndPauseMs}");
            }
            s.Add($"{JojaBadEndingCommands.AnimalsWaitName} {WaitTimeoutMs}");
            s.Add($"pause {AfterMs}");
            return s;
        }

        private static string Pan(ref Vector2 at, int x, int y)
        {
            float tiles = Vector2.Distance(at, new Vector2(x, y));
            at = new Vector2(x, y);
            return $"{EndingEventCommands.PanToName} {x} {y} {Math.Max(MinPanMs, (int)(tiles * PanMsPerTile))}";
        }
    }
}
