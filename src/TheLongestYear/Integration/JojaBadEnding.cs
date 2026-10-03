// Bad ending tiles, read live 2026-09-25 (tly_newgame standard skipintro, debug warp to each spot,
// PrintWindow frames with a 64 px tile grid, and the maps via patch export).
//
// Farm: the anchor is Farm.GetMainFarmHouseEntry() (Standard: 64,15; Farmhouse at 59,12, size 9x5,
// HumanDoor 5,2). The farmhouse sprite (160x144 px, DrawOffset -16,2) covers cols door-6..door+3,
// rows door-7..door+1, and its mailbox draw layer the column after: that 11 x 9 at door-6, door-7
// is HouseRect. The kept house (the default; Jeff, 2026-10-02: "try keeping the farmhouse"): one
// explosion's dust on each side of it (4 x 8 at door-10 and door+5, rows door-6..door+1; the
// puffs grow right and down, so they lap its edges), the farm-wide dust skips puffs centred on
// HouseRect, and the house's own footprint stays unpaved (it is under the sprite). The Joja sign
// (JojaBadEndingVisuals, Cursors' warehouse sign, 52 x 20 px at 4x) sits centred on the door
// tile with its bottom 136 px above the entry tile's top: just over the door frame on all three
// upgrade levels (Buildings/houses: door frame tops at sprite rows 77, 77, 75). SignDust is its
// puff. The razed house (tly_joja badending [floorId] razehouse, or RazeHouseByDefault): dust on
// HouseRect, then the house hidden, as before.
// Nothing else on the farm is a fixed tile: the rows of coops and barns come from the farm's own
// isBuildable after the clear (JojaFactoryFarm), so every farm type lays out its own.
// Town: the river south-east of town runs down cols 76..82 under the bridge on rows 93..96; west
//   bank sand 74,89..93, east bank sand 83,90..92. Dead fish on the sand at 74,90, 74,92 and 83,91.
//   The farmer is parked at 74,93 (sand). Camera: 78,93 (river, bridge, both banks), then a pan to
//   44,57 (Pierre's front, boarded up by JojaPierreBoards). Floating litter only on the river the
//   camera on 78,93 sees (74..85 x 84..101).
// Community Center (Maps/Town patch export and the PC 1.6 decompile, 2026-10-02): the building is
//   47..58 x 11..20 (Town.refurbishCommunityCenter's ccBounds), its door 52..53 x 19 (the
//   WarpCommunityCenter tiles), straight up the square from Pierre's (door 43..44 x 55). The Joja
//   facade vanilla draws over it (Town.ccFacadePosition, 3044,940 px, 174 x 101 px at 4x) covers
//   about 47..58 x 14..21. Camera: 53,16 (the whole front and roof), panned up to from 44,57.
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
    /// Yes. No dialogue, sad music throughout: dust goes up around the farmhouse and everything
    /// else on the farm is bulldozed and paved, a Joja sign goes up over the farmhouse door (or, the
    /// older version, the farmhouse itself goes up in dust), rows of real coops and barns go up all
    /// over the farm, and the camera pans along the first row while its animals go in; the town river runs green with dead fish on the bank and
    /// floating in it, Pierre's is boarded up like the closed JojaMart, and up the square the
    /// Community Center has become a Joja warehouse; the camera pans along a beach
    /// strewn with driftwood, trash and dead fish. Then Game Over. All of it in daylight, whatever
    /// the hour of the Yes (tlyDaylight).</summary>
    internal static class JojaBadEnding
    {
        // Farm offsets from the farmhouse entry (see the tile notes above).
        private static readonly Rectangle HouseRect = new(-6, -7, 11, 9);   // the house sprite and mailbox
        private static readonly Rectangle LeftDust = new(-10, -6, 4, 8), RightDust = new(5, -6, 4, 8);
        private static readonly Rectangle SignDust = new(-1, -4, 3, 2);
        private const int SignDelayMs = 450, SignDustMs = 900, SignHoldMs = 1600;
        // The ending's default: false keeps the farmhouse and puts the Joja sign on it; true brings
        // back the house torn down and hidden (tly_joja badending [floorId] razehouse picks it too).
        internal const bool RazeHouseByDefault = false;
        internal const string RazeHouseArg = "razehouse";
        private const int FarmerArriveDy = 3;
        internal const string DefaultFloor = "1";         // Flooring.stone: flat grey slabs, the closest to concrete (vs 5 gravel, 12 town cobbles; task 6b report)
        private const string Music = "grandpas_theme";    // see the task 6b report

        // The Standard farm's entry: vanilla offsets farm event tiles by entry - (64, 15).
        private static readonly Point VanillaEntry = new(64, 15);

        // Town and Beach tiles.
        private static readonly Point TownPark = new(74, 93), RiverView = new(78, 93), PierreView = new(44, 57), WarehouseView = new(53, 16);
        private const int PierreHoldMs = 1000, WarehousePanMs = 4000, WarehouseHoldMs = 2500;
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

        internal static string Build(Point farmer, int facing, Point door, string floor, bool razeHouse = RazeHouseByDefault)
        {
            Point off = new(door.X - VanillaEntry.X, door.Y - VanillaEntry.Y);
            // Vanilla commands on the farm (viewport, warp) add the farm's event offset themselves;
            // the tly* commands take absolute tiles.
            string V(int x, int y) => $"{x - off.X} {y - off.Y}";
            string At(Rectangle r) => $"{door.X + r.X} {door.Y + r.Y} {r.Width} {r.Height}";
            string Dust(Rectangle r, int ms) => $"{JojaBadEndingCommands.DustName} {At(r)} {ms}";
            // The kept house stays out of the farm-wide dust; the razed one is gone by then.
            string DustView(int ms) => $"{JojaBadEndingCommands.DustViewName} {ms}" + (razeHouse ? "" : $" {At(HouseRect)}");

            var s = new List<string>
            {
                "none", "-1000 -1000", $"farmer {farmer.X} {farmer.Y} {facing}",

                // ---- JojaMart: the answer given, the store goes dark, the music turns ----
                $"{JojaBadEndingCommands.MusicName} {Music}",
                $"{EndingEventCommands.FadeOutName} 1200",
                JojaBadEndingCommands.DaylightName,   // under black, before the first warp: every place after it is day

                // ---- The farm: the house comes down ----
                $"{EndingEventCommands.ChangeLocationName} Farm {door.X} {door.Y + FarmerArriveDy}",
                "warp farmer -100 -100",
                $"viewport {V(door.X, door.Y)} clamp",
                $"{EndingEventCommands.FadeInName} 1200",
                "pause 800",
            };
            if (razeHouse)
            {
                // ---- The farm: the house comes down (the older version) ----
                s.AddRange(new[]
                {
                    "playSound explosion", Dust(HouseRect, 1000),
                    "playSound explosion", Dust(HouseRect, 1000),
                    JojaBadEndingCommands.HideFarmhouseName,
                });
            }
            else
            {
                // ---- The farm: the ground goes up on both sides of the house ----
                s.AddRange(new[]
                {
                    "playSound explosion", Dust(LeftDust, 1000),
                    "playSound explosion", Dust(RightDust, 1000),
                });
            }
            s.AddRange(new[]
            {
                "pause 1000",

                // ---- ...then everything else goes, and the concrete goes down ----
                "playSound boulderBreak",
                DustView(2600),
                "pause 900",
                JojaBadEndingCommands.ClearFarmName,
                $"{JojaBadEndingCommands.PaveFarmName} {floor}",   // paves the house's lot only once it is hidden
                "pause 2600",
            });
            if (!razeHouse)
            {
                // ---- ...and Joja's sign goes up over the door, under its own puff ----
                s.AddRange(new[]
                {
                    "playSound hammer",
                    $"{JojaBadEndingCommands.JojaSignName} {door.X} {door.Y} {SignDelayMs}",
                    Dust(SignDust, SignDustMs),
                    $"pause {SignHoldMs}",
                });
            }
            s.AddRange(new[]
            {
                // ---- ...then the coops and barns go up, full of animals ----
                "playSound hammer",
                DustView(2600),
                "pause 900",
                JojaBadEndingCommands.JojaFarmName,   // puts every row up, then adds the pan along the first one

                // ---- Town: the river runs green, Pierre's is boarded up ----
                $"{EndingEventCommands.ChangeLocationName} Town {TownPark.X} {TownPark.Y}",
                "warp farmer -100 -100",
                $"viewport {RiverView.X} {RiverView.Y} clamp",
                $"{JojaBadEndingCommands.WaterTintName} {RiverTintR} {RiverTintG} {RiverTintB}",
            });
            foreach (var f in DeadFish)
                s.Add($"{JojaBadEndingCommands.ItemSpriteName} {f.Id} {f.X} {f.Y} {f.Deg}");
            s.AddRange(new[]
            {
                Litter(RiverSeed, "water", RiverArea, RiverCount, RiverSpacing, FloatingLitter),
                JojaBadEndingCommands.BoardPierreName,
                JojaBadEndingCommands.JojaWarehouseName,
                $"{EndingEventCommands.FadeInName} 1200",
                "pause 2500",
                $"{EndingEventCommands.PanToName} {PierreView.X} {PierreView.Y} 4000",
                $"pause {PierreHoldMs}",

                // ---- ...and up the square, the Community Center is a Joja warehouse ----
                $"{EndingEventCommands.PanToName} {WarehouseView.X} {WarehouseView.Y} {WarehousePanMs}",
                $"pause {WarehouseHoldMs}",

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
        public static void Start(IMonitor monitor, string floor = DefaultFloor, bool razeHouse = RazeHouseByDefault)
            => TryStart(monitor, StartRetries, floor, razeHouse);

        private static void TryStart(IMonitor monitor, int triesLeft, string floor, bool razeHouse)
        {
            GameLocation loc = Game1.currentLocation;
            bool busy = !Context.IsWorldReady || loc == null || Game1.eventUp || loc.currentEvent != null
                        || Game1.activeClickableMenu != null || Game1.dialogueUp;
            if (!busy)
            {
                Farm farm = Game1.getFarm();
                Point door = farm.GetMainFarmHouseEntry();
                loc.startEvent(new Event(Build(Game1.player.TilePoint, Game1.player.FacingDirection, door, floor, razeHouse), null, JojaEventKeys.BadEndingId));
                if (loc.currentEvent?.id == JojaEventKeys.BadEndingId)
                {
                    JojaBadEndingCommands.MarkRunning();
                    monitor.Log($"Joja: bad ending (door={door.X},{door.Y}, floor {floor}, farmhouse {(razeHouse ? "razed" : "kept, Joja sign")}, {farm.Map.Layers[0].LayerWidth}x{farm.Map.Layers[0].LayerHeight} farm, from {loc.Name}).", LogLevel.Info);
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
            DelayedAction.functionAfterDelay(() => TryStart(monitor, triesLeft - 1, floor, razeHouse), StartRetryMs);
        }
    }
}
