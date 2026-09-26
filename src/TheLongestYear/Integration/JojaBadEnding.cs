// Bad ending tiles, read live 2026-09-25 (tly_newgame standard skipintro, debug warp to each spot,
// PrintWindow frames with a 64 px tile grid, and the maps via patch export).
//
// Farm: the anchor is Farm.GetMainFarmHouseEntry() (Standard: 64,15; Farmhouse at 59,12, size 9x5,
// HumanDoor 5,2). The farmhouse sprite (160x144 px, DrawOffset -16,2) covers cols door-6..door+3,
// rows door-7..door+1, and its mailbox draw layer the column after: dust door-6, door-7, 11 x 9.
// Nothing else on the farm is a fixed tile: the rows of coops and barns come from the farm's own
// isBuildable after the clear (JojaFactoryFarm), so every farm type lays out its own.
// Town: the river south-east of town runs down cols 76..82 under the bridge on rows 93..96; west
//   bank sand 74,89..93, east bank sand 83,90..92. Dead fish on the sand at 74,90, 74,92 and 83,91.
//   The farmer is parked at 74,93 (sand). Camera: 78,93 (river, bridge, both banks), then a pan to
//   44,57 (Pierre's front, boarded up by JojaPierreBoards). Floating litter only on the river the
//   camera on 78,93 sees (74..85 x 84..101).
// Beach (Maps/Beach is 104 x 50, patch export 2026-09-25): the sand runs from the west sea (x 0..8)
//   past Elliott's cabin and the river mouth (x 57..62) to the tide pools and the east pier (x 86).
//   The camera pans the shore on rows 18..20 from x 18 to x 86; litter is picked from the whole map
//   by the shore rule in JojaLitter (dry open sand within 4 tiles of water).
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending of Morris's offer (spec 2026-09-25-joja-offer-design): the player said
    /// Yes. No dialogue, sad music throughout: the farmhouse goes up in dust, the whole farm is
    /// bulldozed and paved, rows of real coops and barns go up and the animals go in for the night
    /// while the camera pans along them; the town river runs green with dead fish on the bank and
    /// floating in it, Pierre's is boarded up like the closed JojaMart; the camera pans along a beach
    /// strewn with driftwood, trash and dead fish. Then Game Over.</summary>
    internal static class JojaBadEnding
    {
        // Farm offsets from the farmhouse entry (see the tile notes above).
        private static readonly Point HouseDust = new(-6, -7);
        private const int HouseDustW = 11, HouseDustH = 9;
        private const int FarmerArriveDy = 3;
        internal const string DefaultFloor = "1";         // Flooring.stone: flat grey slabs, the closest to concrete (vs 5 gravel, 12 town cobbles; task 6b report)
        private const string Music = "grandpas_theme";    // see the task 6b report

        // The Standard farm's entry: vanilla offsets farm event tiles by entry - (64, 15).
        private static readonly Point VanillaEntry = new(64, 15);

        // Town and Beach tiles.
        private static readonly Point TownPark = new(74, 93), RiverView = new(78, 93), PierreView = new(44, 57);
        private static readonly (string Id, int X, int Y, int Deg)[] DeadFish =
        {
            ("(O)145", 74, 90, 200), ("(O)132", 74, 92, 165), ("(O)145", 83, 91, 190),
        };
        private const int RiverTintR = 70, RiverTintG = 110, RiverTintB = 40;
        private const string FloatingLitter = "(O)145,(O)132,(O)131,(O)142,(O)168,(O)172,(O)131";
        private static readonly Rectangle RiverArea = new(74, 84, 12, 18);   // the river as the camera on 78,93 sees it
        private const int RiverSeed = 4101, RiverCount = 5;
        private const float RiverSpacing = 3f;
        private const string ShoreLitter = "(O)169,(O)169,(O)169,(O)168,(O)172,(O)170,(O)145,(O)132,(O)131,(O)142";
        private static readonly Rectangle BeachArea = new(0, 0, 104, 50);
        private const int BeachSeed = 7303, BeachCount = 22;
        private const float BeachSpacing = 5f;
        private static readonly Point BeachPark = new(40, 14), BeachFrom = new(18, 18), BeachTo = new(86, 20);
        private const int BeachPanMs = 12000;

        private const int StartRetries = 20, StartRetryMs = 250;

        internal static string Build(Point farmer, int facing, Point door, string floor)
        {
            Point off = new(door.X - VanillaEntry.X, door.Y - VanillaEntry.Y);
            // Vanilla commands on the farm (viewport, warp) add the farm's event offset themselves;
            // the tly* commands take absolute tiles.
            string V(int x, int y) => $"{x - off.X} {y - off.Y}";
            string houseDust = $"{JojaBadEndingCommands.DustName} {door.X + HouseDust.X} {door.Y + HouseDust.Y} {HouseDustW} {HouseDustH} 1000";

            var s = new List<string>
            {
                "none", "-1000 -1000", $"farmer {farmer.X} {farmer.Y} {facing}",

                // ---- JojaMart: the answer given, the store goes dark, the music turns ----
                $"{JojaBadEndingCommands.MusicName} {Music}",
                $"{EndingEventCommands.FadeOutName} 1200",

                // ---- The farm: the house comes down ----
                $"{EndingEventCommands.ChangeLocationName} Farm {door.X} {door.Y + FarmerArriveDy}",
                "warp farmer -100 -100",
                $"viewport {V(door.X, door.Y)} clamp",
                $"{EndingEventCommands.FadeInName} 1200",
                "pause 800",
                "playSound explosion",
                houseDust,
                "playSound explosion",
                houseDust,
                JojaBadEndingCommands.HideFarmhouseName,
                "pause 1000",

                // ---- ...then everything else goes, and the concrete goes down ----
                "playSound boulderBreak",
                $"{JojaBadEndingCommands.DustViewName} 2600",
                "pause 900",
                JojaBadEndingCommands.ClearFarmName,
                $"{JojaBadEndingCommands.PaveFarmName} {floor}",
                "pause 2600",

                // ---- ...then the coops and barns go up, full of animals ----
                "playSound hammer",
                $"{JojaBadEndingCommands.DustViewName} 2600",
                "pause 900",
                JojaBadEndingCommands.JojaFarmName,   // puts them up, then adds the pan across every row

                // ---- Town: the river runs green, Pierre's is boarded up ----
                $"{EndingEventCommands.ChangeLocationName} Town {TownPark.X} {TownPark.Y}",
                "warp farmer -100 -100",
                $"viewport {RiverView.X} {RiverView.Y} clamp",
                $"{JojaBadEndingCommands.WaterTintName} {RiverTintR} {RiverTintG} {RiverTintB}",
            };
            foreach (var f in DeadFish)
                s.Add($"{JojaBadEndingCommands.ItemSpriteName} {f.Id} {f.X} {f.Y} {f.Deg}");
            s.AddRange(new[]
            {
                Litter(RiverSeed, "water", RiverArea, RiverCount, RiverSpacing, FloatingLitter),
                JojaBadEndingCommands.BoardPierreName,
                $"{EndingEventCommands.FadeInName} 1200",
                "pause 2500",
                $"{EndingEventCommands.PanToName} {PierreView.X} {PierreView.Y} 4000",
                "pause 3000",

                // ---- The beach: the tide brings in the rest ----
                $"{EndingEventCommands.ChangeLocationName} Beach {BeachPark.X} {BeachPark.Y}",
                "warp farmer -100 -100",
                $"viewport {BeachFrom.X} {BeachFrom.Y} clamp",
                Litter(BeachSeed, "shore", BeachArea, BeachCount, BeachSpacing, ShoreLitter),
                $"{EndingEventCommands.FadeInName} 1200",
                "pause 800",
                $"{EndingEventCommands.PanToName} {BeachTo.X} {BeachTo.Y} {BeachPanMs}",
                "pause 1500",
                $"{EndingEventCommands.FadeOutName} 1500",
                JojaBadEndingCommands.GameOverName,
            });
            return string.Join("/", s);
        }

        private static string Litter(int seed, string mode, Rectangle area, int count, float spacing, string ids)
            => $"{JojaBadEndingCommands.LitterName} {seed} {mode} {area.X} {area.Y} {area.Width} {area.Height} {count} {spacing} {ids}";

        /// <summary>Plays the bad ending from wherever the player stands (JojaMart after the Yes).
        /// Waits out a closing dialogue box or an ending event, then gives up after a few seconds.</summary>
        public static void Start(IMonitor monitor, string floor = DefaultFloor) => TryStart(monitor, StartRetries, floor);

        private static void TryStart(IMonitor monitor, int triesLeft, string floor)
        {
            GameLocation loc = Game1.currentLocation;
            bool busy = !Context.IsWorldReady || loc == null || Game1.eventUp || loc.currentEvent != null
                        || Game1.activeClickableMenu != null || Game1.dialogueUp;
            if (!busy)
            {
                Farm farm = Game1.getFarm();
                Point door = farm.GetMainFarmHouseEntry();
                loc.startEvent(new Event(Build(Game1.player.TilePoint, Game1.player.FacingDirection, door, floor), null, JojaEventKeys.BadEndingId));
                if (loc.currentEvent?.id == JojaEventKeys.BadEndingId)
                {
                    JojaBadEndingCommands.MarkRunning();
                    monitor.Log($"Joja: bad ending (door={door.X},{door.Y}, floor {floor}, {farm.Map.Layers[0].LayerWidth}x{farm.Map.Layers[0].LayerHeight} farm, from {loc.Name}).", LogLevel.Info);
                    return;
                }
            }
            if (triesLeft <= 0)
            {
                // Never leave the player in the store after a Yes. Task 7 routes this through
                // JojaGameOverMenu instead of going straight to the title.
                JojaBadEndingCommands.FailClosed("the bad ending could not start (the game stayed busy)");
                return;
            }
            DelayedAction.functionAfterDelay(() => TryStart(monitor, triesLeft - 1, floor), StartRetryMs);
        }
    }
}
