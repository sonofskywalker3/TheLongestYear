// Bad ending tiles, read live 2026-09-25 (tly_newgame standard skipintro, debug warp to each spot,
// PrintWindow frames with a 64 px tile grid anchored on the farmer's tile and checked against the
// Shipping Bin at 71,14 / Pierre's door objects in Maps/Town / Data/Buildings via patch export).
//
// Farm (Standard). The anchor is Farm.GetMainFarmHouseEntry() = 64,15 (Farmhouse at 59,12, size
// 9x5, HumanDoor 5,2). Every farm tile below is an offset from it, so other farm types follow their
// own house (not checked live on them).
//   - The farmhouse sprite (160x144 px, DrawOffset -16,2) covers cols 58..67, rows 8..16; its
//     mailbox draw layer is col 68, rows 14..16. Dust: door-6, door-7, 11 x 9.
//   - Barn (7x4, sprite 7x7 tiles, AnimalDoor 3,3 w2): bottom-left 54,14 (door-10, door-1),
//     footprint cols 54..60 rows 11..14, sprite rows 8..14. West of it the pine at 52,13 (canopy to
//     col 53); the rest of the spot is the old house lot and small debris the barn covers.
//     Animal door 57..58,14; the queue stands on row 15 (the old porch row) at 57, 59 .. 71.
//   - Coop (6x3, sprite 6x7 tiles, AnimalDoor 2,2): bottom-left 73,14 (door+9, door-1), footprint
//     cols 73..78 rows 12..14, sprite rows 8..14: grass, two bushes and the fence along col 78
//     all behind it. The bin (71..72,14) is just west of it. Animal door 75,14; queue 75..78, row 15.
//   - Row 15 is open from col 57 to the map edge once the house and mailbox are hidden (the
//     fences at col 78 end at row 14 and at col 77 start at row 18; the path leaves east on
//     rows 15..17). Each line waits in file past the east edge (the map is 80 wide), its head on col 81.
// Town: Pierre's door is 43..44, 55..56 (LockedDoorWarp tiles 43,56 and 44,56). The river south-
//   east of town runs down cols 76..82 under the bridge on rows 93..96; west bank sand 74,89..93,
//   east bank sand 83,90..92. Dead fish at 74,90, 74,92 and 83,91. The farmer is parked at 74,93
//   (sand). Camera: 78,93 (river, bridge, both banks), then a pan to 44,57 (Pierre's front).
// Beach: the south shore east of the tide pools: dry sand row 24 and wet sand row 25, clean from
//   col 65 to col 85 (the dock starts at col 86). Camera 74,24.
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending of Morris's offer (spec 2026-09-25-joja-offer-design): the player said
    /// Yes. No dialogue: the farmhouse goes up in dust, a barn and a coop rise in its place, the farm
    /// animals file in; the town river runs green with dead fish on the bank, Pierre's is boarded up;
    /// driftwood and trash wash up on the beach. Then Game Over.</summary>
    internal static class JojaBadEnding
    {
        // Farm offsets from the farmhouse entry (see the tile notes above).
        private static readonly Point HouseDust = new(-6, -7);
        private const int HouseDustW = 11, HouseDustH = 9;
        // Cleared ground (Controller ruling 2026-09-25, in memory only): the house footprint plus
        // its sprite's west column and the mailbox column (rows door-3..door+1; not the fence
        // behind it), the barn and coop footprints, and the route rows door-1..door+1 from the barn
        // door to the east edge (a big animal on row door draws over the row above it).
        private static readonly Point HouseClear = new(-6, -3);
        private const int HouseClearW = 11, HouseClearH = 5;
        private const int BarnFootW = 7, BarnFootH = 4, CoopFootW = 6, CoopFootH = 3;
        private const int RouteClearDy = -1, RouteClearH = 3;
        private static readonly Point Barn = new(-10, -1);
        private const int BarnW = 7, BarnDoorDx = 3;
        private static readonly Point Coop = new(9, -1);
        private const int CoopW = 6, CoopDoorDx = 2;
        private const int BuildingSpriteRows = 7;
        private const int LineRowDy = 0;          // the queue row: the old porch row, door row + 0
        private const int FarmerArriveDy = 3;
        private const int EdgeStartPastMap = 1;   // lines start one tile past the east edge

        // The Standard farm's entry: vanilla offsets farm event tiles by entry - (64, 15).
        private static readonly Point VanillaEntry = new(64, 15);

        private static readonly string[] BarnLine = { "White_Cow:Cow1", "White_Cow:Cow2", "Sheep:Sheep1", "Sheep:Sheep2", "Goat:Goat1", "Goat:Goat2", "Pig:Pig1", "Pig:Pig2" };
        private static readonly string[] CoopLine = { "White_Chicken:Chicken1", "White_Chicken:Chicken2", "Duck:Duck1", "Duck:Duck2" };
        private const int BigSprite = 32, SmallSprite = 16;
        private const int BarnGap = 2, CoopGap = 1;           // tiles between animals in the queue
        private const int BarnSpeed = 3, CoopSpeed = 2;
        private const int WalkTimeoutMs = 20000;
        private const int FaceLeft = 3, FaceDown = 2;

        // Town and Beach tiles.
        private static readonly Point TownPark = new(74, 93), RiverView = new(78, 93), PierreView = new(44, 57);
        private static readonly Point PierreDoor = new(43, 56);
        private static readonly (string Id, int X, int Y, int Deg)[] DeadFish =
        {
            ("(O)145", 74, 90, 200), ("(O)132", 74, 92, 165), ("(O)145", 83, 91, 190),
        };
        private static readonly Point BeachPark = new(74, 23), BeachView = new(74, 24);
        private const string Driftwood = "(O)169", Trash = "(O)168";
        private static readonly (string Id, int X, int Y, int Deg)[] Washup =
        {
            (Driftwood, 65, 24, 0), (Driftwood, 67, 25, 25), (Trash, 68, 24, 0), (Driftwood, 70, 25, 340),
            (Driftwood, 71, 24, 15), (Trash, 73, 25, 0), (Driftwood, 75, 24, 350), (Driftwood, 76, 25, 30),
            (Trash, 78, 24, 0), (Driftwood, 80, 25, 10), (Driftwood, 81, 24, 335), (Trash, 83, 25, 0),
        };
        private const int RiverTintR = 70, RiverTintG = 110, RiverTintB = 40;

        private const int StartRetries = 20, StartRetryMs = 250;

        internal static string Build(Point farmer, int facing, Point door, int farmWidth)
        {
            Point off = new(door.X - VanillaEntry.X, door.Y - VanillaEntry.Y);
            // Vanilla commands on the farm (addTemporaryActor, viewport, warp) add the farm's event
            // offset themselves; the tly* commands take absolute tiles.
            string V(int x, int y) => $"{x - off.X} {y - off.Y}";

            int lineY = door.Y + LineRowDy;
            int startX = farmWidth + EdgeStartPastMap;
            int barnX = door.X + Barn.X, barnY = door.Y + Barn.Y;
            int coopX = door.X + Coop.X, coopY = door.Y + Coop.Y;

            var s = new List<string>
            {
                "none", "-1000 -1000", $"farmer {farmer.X} {farmer.Y} {facing}",

                // ---- JojaMart: the answer given, the store goes dark ----
                $"{EndingEventCommands.FadeOutName} 1200",

                // ---- The farm ----
                $"{EndingEventCommands.ChangeLocationName} Farm {door.X} {door.Y + FarmerArriveDy}",
                "warp farmer -100 -100",
                $"viewport {V(door.X, door.Y)} clamp",
            };
            AddLine(s, BarnLine, BigSprite, BarnGap, startX, lineY, V);
            AddLine(s, CoopLine, SmallSprite, CoopGap, startX, lineY, V);
            s.AddRange(new[]
            {
                $"{EndingEventCommands.FadeInName} 1200",
                "pause 800",
                "playSound explosion",
                $"{JojaBadEndingCommands.ClearName} {door.X + HouseClear.X} {door.Y + HouseClear.Y} {HouseClearW} {HouseClearH}",
                $"{JojaBadEndingCommands.DustName} {door.X + HouseDust.X} {door.Y + HouseDust.Y} {HouseDustW} {HouseDustH} 1000",
                "playSound explosion",
                $"{JojaBadEndingCommands.DustName} {door.X + HouseDust.X} {door.Y + HouseDust.Y} {HouseDustW} {HouseDustH} 1000",
                JojaBadEndingCommands.HideFarmhouseName,
                "pause 1000",
                $"{JojaBadEndingCommands.ClearName} {barnX} {barnY - BarnFootH + 1} {BarnFootW} {BarnFootH}",
                $"{JojaBadEndingCommands.ClearName} {barnX + BarnDoorDx} {lineY + RouteClearDy} {farmWidth - (barnX + BarnDoorDx)} {RouteClearH}",
                $"{JojaBadEndingCommands.DustName} {barnX} {barnY - BuildingSpriteRows + 1} {BarnW} {BuildingSpriteRows} 1000",
                $"{JojaBadEndingCommands.BuildingSpriteName} Barn {barnX} {barnY}",
                "pause 300",
                $"{JojaBadEndingCommands.ClearName} {coopX} {coopY - CoopFootH + 1} {CoopFootW} {CoopFootH}",
                $"{JojaBadEndingCommands.DustName} {coopX} {coopY - BuildingSpriteRows + 1} {CoopW} {BuildingSpriteRows} 1000",
                $"{JojaBadEndingCommands.BuildingSpriteName} Coop {coopX} {coopY}",
                "pause 800",
            });
            WalkLine(s, BarnLine, BarnSpeed, startX, barnX + BarnDoorDx);
            WalkLine(s, CoopLine, CoopSpeed, startX, coopX + CoopDoorDx);
            s.Add("pause 4000");

            // ---- Town: the river runs green, Pierre's is boarded up ----
            s.AddRange(new[]
            {
                $"{EndingEventCommands.ChangeLocationName} Town {TownPark.X} {TownPark.Y}",
                "warp farmer -100 -100",
                $"viewport {RiverView.X} {RiverView.Y} clamp",
                $"{JojaBadEndingCommands.WaterTintName} {RiverTintR} {RiverTintG} {RiverTintB}",
            });
            foreach (var f in DeadFish)
                s.Add($"{JojaBadEndingCommands.ItemSpriteName} {f.Id} {f.X} {f.Y} {f.Deg}");
            s.AddRange(new[]
            {
                $"{JojaBadEndingCommands.ClosedSignName} {PierreDoor.X} {PierreDoor.Y}",
                $"{EndingEventCommands.FadeInName} 1200",
                "pause 1500",
                $"{EndingEventCommands.PanToName} {PierreView.X} {PierreView.Y} 4000",
                "pause 2500",

                // ---- The beach: the tide brings in the rest ----
                $"{EndingEventCommands.ChangeLocationName} Beach {BeachPark.X} {BeachPark.Y}",
                "warp farmer -100 -100",
                $"viewport {BeachView.X} {BeachView.Y} clamp",
            });
            foreach (var w in Washup)
                s.Add($"{JojaBadEndingCommands.ItemSpriteName} {w.Id} {w.X} {w.Y} {w.Deg}");
            s.AddRange(new[]
            {
                $"{EndingEventCommands.FadeInName} 1200",
                "pause 2500",
                $"{EndingEventCommands.FadeOutName} 1500",
                JojaBadEndingCommands.GameOverName,
            });
            return string.Join("/", s);
        }

        /// <summary>The animals already in single file past the east edge, facing west: the first one
        /// tile off the map, each next one a gap further out.</summary>
        private static void AddLine(List<string> s, string[] line, int size, int gap, int startX, int y, Func<int, int, string> v)
        {
            for (int k = 0; k < line.Length; k++)
            {
                string[] parts = line[k].Split(':');
                s.Add($"addTemporaryActor {parts[0]} {size} {size} {v(startX + k * gap, y)} {FaceLeft} false Animal {parts[1]}");
            }
        }

        /// <summary>Single file: every animal walks the same distance west at the same speed, all at
        /// once, so the file keeps its gaps and stops with the first at the door. (Staggered starts
        /// from one tile do not work: NPCController halts a waiting controller, pause countdown too,
        /// while an earlier one in the list overlaps it, so they set off one by one, 2026-09-25.)
        /// Then heads down.</summary>
        private static void WalkLine(List<string> s, string[] line, int speed, int startX, int doorX)
        {
            var names = new List<string>();
            int tiles = startX - doorX;
            foreach (string entry in line)
            {
                string name = entry.Split(':')[1];
                names.Add(name);
                s.Add($"speed {name} {speed}");
                s.Add($"advancedMove {name} false -{tiles} 0");
            }
            foreach (string name in names)
                s.Add($"{OpeningEventCommands.WaitWalkName} {name} {WalkTimeoutMs}");
            foreach (string name in names)
                s.Add($"faceDirection {name} {FaceDown}");
        }

        /// <summary>Plays the bad ending from wherever the player stands (JojaMart after the Yes).
        /// Waits out a closing dialogue box or an ending event, then gives up after a few seconds.</summary>
        public static void Start(IMonitor monitor) => TryStart(monitor, StartRetries);

        private static void TryStart(IMonitor monitor, int triesLeft)
        {
            GameLocation loc = Game1.currentLocation;
            bool busy = !Context.IsWorldReady || loc == null || Game1.eventUp || loc.currentEvent != null
                        || Game1.activeClickableMenu != null || Game1.dialogueUp;
            if (!busy)
            {
                Farm farm = Game1.getFarm();
                Point door = farm.GetMainFarmHouseEntry();
                int width = farm.Map.Layers[0].LayerWidth;
                loc.startEvent(new Event(Build(Game1.player.TilePoint, Game1.player.FacingDirection, door, width), null, JojaEventKeys.BadEndingId));
                if (loc.currentEvent?.id == JojaEventKeys.BadEndingId)
                {
                    JojaBadEndingCommands.MarkRunning();
                    monitor.Log($"Joja: bad ending (door={door.X},{door.Y}, farm width {width}, from {loc.Name}).", LogLevel.Info);
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
            DelayedAction.functionAfterDelay(() => TryStart(monitor, triesLeft - 1), StartRetryMs);
        }
    }
}
