using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Pathfinding;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>Townsfolk walking the town's real paths backwards under the rewind pan: one more
    /// reading that time is running the wrong way, on top of the seasons and the light (Jeff,
    /// 2026-09-11).
    ///
    /// WHAT "BACKWARDS" MEANS HERE. Reversed footage does not show people walking in reverse; it
    /// shows them facing the way they are going and translating the other way. So each extra plays
    /// its ordinary walk cycle for the direction the route is heading, while its position runs the
    /// route from its far end back to its start. The walk animation has to be driven by hand: the
    /// pan sets <c>Game1.freezeControls</c>, so <c>Game1.shouldTimePass</c> is false and
    /// <c>GameLocation.updateCharacters</c> never calls these NPCs' own update.
    ///
    /// REAL ROUTES, NOT A LINE. The first build of this walked each extra along the straight line
    /// between the camera's two endpoints. The camera's route is a diagonal across the whole map and
    /// nothing walks it: "I just saw evelyn walking diagonal over the river forwards" (2026-09-11).
    /// Every route here now comes from <see cref="PathFindController.findPathForNPCSchedules"/>,
    /// which is the same pathfinder that builds villagers' actual schedule routes, so an extra walks
    /// roads and bridges exactly as it would on its way to work. An extra whose route cannot be
    /// found is simply not placed, rather than sliding through the river.
    ///
    /// Everything borrowed is given back by <see cref="Teardown"/>, which the pan calls on both its
    /// normal and its abnormal end.</summary>
    internal static class RewindReversedExtras
    {
        /// <summary>How many villagers a rewind puts on the road, inclusive. Three or four: enough to
        /// read as traffic, few enough that none of them becomes the thing you watch.</summary>
        private const int MinCast = 3, MaxCast = 4;

        /// <summary>The search limit vanilla gives a schedule route (NPC.pathfindToNextScheduleLocation).</summary>
        private const int PathfinderLimit = 30000;

        /// <summary>The map whose schedule legs the pan plays.</summary>
        private const string TownName = "Town";

        /// <summary>How close, in tiles, a villager's real route has to come to the camera's line to be
        /// used: about half the view's height, so the walker is in shot as the camera passes.</summary>
        private const double CameraReachTiles = 6.0;

        /// <summary>Tiles of route kept beyond the point nearest the camera's line. The backwards walk
        /// starts there, just out of shot, so the walker is already moving when the camera arrives.</summary>
        private const int LeadTiles = 10;

        /// <summary>Shorter routes are not used: a walker should be seen walking, not appear and vanish.</summary>
        private const int MinRouteTiles = 6;

        /// <summary>An ordinary walking pace, in tiles per second: vanilla's own.</summary>
        private const double WalkTilesPerSecond = 3.0;

        private sealed class Extra
        {
            public NPC Npc;
            public GameLocation HomeLocation;
            public Vector2 HomePosition;
            public int HomeFacing;
            public int HomeForceUpdateTimer;
            public Point[] Route;      // forward order: Route[0] is the door or map edge the villager came in by
            public WalkState State;
            public double Along = 1.0; // 1 is the far end the backwards walk starts from
        }

        private enum WalkState { Waiting, Walking, Gone }

        /// <summary>How close, in tiles, the camera's view comes to a waiting walker's start before it
        /// sets off. Far enough that it is already walking when it comes into shot.</summary>
        private const int StartMarginTiles = 4;

        /// <summary>Where a walker who has gone through its door is kept until <see cref="Teardown"/>
        /// puts it back: nowhere any camera is pointed.</summary>
        private static readonly Vector2 ParkedPosition = new Vector2(-100000f, -100000f);

        private static readonly List<Extra> Extras = new List<Extra>();
        private static IMonitor _monitor;
        private static GameLocation _town;

        /// <summary>Borrows a few villagers and walks each one backwards along a real part of today's
        /// schedule that crosses the camera's line. Safe to call when there are none to borrow, or
        /// when no route qualifies: the pan runs on its other dials alone.
        ///
        /// REAL SCHEDULE ROUTES. Earlier builds made routes up: two walkable tiles near the camera's
        /// line and the pathfinder between them, then a straight walk off past the end while still in
        /// shot, which is how Emily walked backwards up a cliff (Jeff, 2026-09-16: "can you not just
        /// use their normal paths they're assigned by the game?"). Each route now is one of the
        /// villager's own Town legs for the day (<see cref="ScheduleLegs"/>), pathed exactly as
        /// vanilla paths it, and only legs that start at a door or the map's edge are used. Played
        /// backwards, every walk ends there, and the walker goes through it the way villagers do.
        ///
        /// A DIFFERENT FEW EVERY TIME. The eligible cast is shuffled and walked in that order until
        /// enough of them have a route (Jeff, 2026-09-11: the same people every reset is tedious).
        /// Shuffling first keeps the pathfinder work on the handover frame to the villagers used.</summary>
        public static void Spawn(IMonitor monitor, GameLocation town, Point from, Point to)
        {
            _monitor = monitor;
            _town = town;
            Extras.Clear();
            if (town == null) return;

            var timer = System.Diagnostics.Stopwatch.StartNew();
            List<NPC> cast = Shuffle(Cast());
            int wanted = MinCast + Game1.random.Next(MaxCast - MinCast + 1);
            var placed = new List<string>();
            int searched = 0;
            for (int i = 0; i < cast.Count && Extras.Count < wanted; i++)
            {
                NPC npc = cast[i];
                searched++;
                Point[] route = RealRoute(npc, town, from, to);
                if (route == null) continue;

                var extra = new Extra
                {
                    Npc = npc,
                    HomeLocation = npc.currentLocation,
                    HomePosition = npc.Position,
                    HomeFacing = npc.FacingDirection,
                    HomeForceUpdateTimer = npc.forceUpdateTimer,
                    Route = route,
                };

                try
                {
                    npc.controller = null;
                    npc.Halt();
                    if (extra.HomeLocation != town)
                    {
                        extra.HomeLocation?.characters.Remove(npc);
                        if (!town.characters.Contains(npc)) town.characters.Add(npc);
                    }
                    npc.currentLocation = town;
                    // Same reason as the Junimos in the bedroom: a character whose forceUpdateTimer
                    // is above zero gets its own update() run even though time is not passing, which
                    // would put this one back on its schedule mid-shot.
                    npc.forceUpdateTimer = 0;
                    Extras.Add(extra);
                    Place(extra, extra.Along);
                    Point back = route[0], start = route[route.Length - 1];
                    placed.Add($"{npc.Name} ({route.Length} tiles, ({start.X},{start.Y}) back to ({back.X},{back.Y}))");
                }
                catch (Exception ex)
                {
                    _monitor?.Log($"RewindReversedExtras: could not place {npc.Name}: {ex.Message}", LogLevel.Warn);
                }
            }

            _monitor?.Log(
                (placed.Count > 0
                    ? $"RewindReversedExtras: {string.Join("; ", placed)}, all walking their schedule routes backwards"
                    : "RewindReversedExtras: no villager's schedule crosses the camera line; the pan runs without extras")
                + $" ({searched} searched in {timer.ElapsedMilliseconds} ms).",
                LogLevel.Info);
        }

        /// <summary>One of <paramref name="npc"/>'s real Town legs for today, cut to the stretch the
        /// camera sees, in forward order; or null when none qualifies.</summary>
        private static Point[] RealRoute(NPC npc, GameLocation town, Point from, Point to)
        {
            List<ScheduleLegs.Leg> legs;
            try
            {
                var stops = new List<ScheduleLegs.Stop>();
                foreach (KeyValuePair<int, SchedulePathDescription> entry in npc.Schedule.OrderBy(e => e.Key))
                    stops.Add(new ScheduleLegs.Stop(entry.Value.targetLocationName, (entry.Value.targetTile.X, entry.Value.targetTile.Y)));
                Vector2 home = npc.DefaultPosition / 64f;
                legs = ScheduleLegs.In(
                    TownName, npc.DefaultMap, ((int)home.X, (int)home.Y), stops,
                    (a, b) => WarpPathfindingCache.GetLocationRoute(a, b, npc.Gender),
                    (map, next) => WarpTo(map, next, npc),
                    (map, warp) => WarpTarget(map, warp, npc));
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindReversedExtras: could not read {npc.Name}'s schedule: {ex.Message}", LogLevel.Trace);
                return null;
            }

            foreach (ScheduleLegs.Leg leg in Shuffle(legs.Where(l => l.EntersMap).ToList()))
            {
                try
                {
                    Stack<Point> path = PathFindController.findPathForNPCSchedules(
                        new Point(leg.From.X, leg.From.Y), new Point(leg.To.X, leg.To.Y), town, PathfinderLimit);
                    if (path == null || path.Count < MinRouteTiles) continue;
                    List<(int X, int Y)> kept = ScheduleLegs.ForCamera(
                        path.Select(p => (p.X, p.Y)).ToList(), (from.X, from.Y), (to.X, to.Y),
                        CameraReachTiles, LeadTiles, MinRouteTiles);
                    if (kept != null)
                        return kept.Select(t => new Point(t.X, t.Y)).ToArray();
                }
                catch (Exception ex)
                {
                    _monitor?.Log($"RewindReversedExtras: pathfinder refused {npc.Name}'s route: {ex.Message}", LogLevel.Trace);
                }
            }
            return null;
        }

        private static (int X, int Y)? WarpTo(string map, string next, NPC npc)
        {
            Point warp = Game1.getLocationFromName(map)?.getWarpPointTo(next, npc) ?? Point.Zero;
            return warp == Point.Zero ? null : (warp.X, warp.Y);
        }

        private static (int X, int Y) WarpTarget(string map, (int X, int Y) warp, NPC npc)
        {
            Point target = Game1.getLocationFromName(map)?.getWarpPointTarget(new Point(warp.X, warp.Y), npc) ?? Point.Zero;
            return (target.X, target.Y);
        }

        /// <summary>A copy of <paramref name="cast"/> in a random order (Fisher-Yates, on the game's
        /// own random so a run is still reproducible from its seed).</summary>
        private static List<T> Shuffle<T>(List<T> cast)
        {
            for (int i = cast.Count - 1; i > 0; i--)
            {
                int j = Game1.random.Next(i + 1);
                (cast[i], cast[j]) = (cast[j], cast[i]);
            }
            return cast;
        }

        /// <summary>Villagers that can be borrowed without the scene reading as wrong: real
        /// townsfolk, not children, not the player's spouse (who should be at home in the bed the
        /// farmer is asleep in), and not anyone already committed to an event.</summary>
        private static List<NPC> Cast()
        {
            var cast = new List<NPC>();
            var rejected = new List<string>();
            string spouse = Game1.player?.spouse;
            Utility.ForEachVillager(npc =>
            {
                if (npc == null || npc is Child || npc is Horse || npc is Pet) return true;
                if (!InTheWorld(npc)) { rejected.Add(npc.Name + "(not here yet)"); return true; }
                if (!WalksTheTown(npc)) { rejected.Add(npc.Name); return true; }
                if (!CanWalk(npc)) { rejected.Add($"{npc.Name}({FrameCount(npc)} frames)"); return true; }
                if (!npc.IsVillager || npc.IsInvisible) return true;
                if (spouse != null && npc.Name == spouse) return true;
                if (npc.EventActor) return true;
                cast.Add(npc);
                return true;
            });
            if (rejected.Count > 0)
                _monitor?.Log(
                    $"RewindReversedExtras: not borrowing {string.Join(", ", rejected)}.",
                    LogLevel.Trace);
            return cast;
        }

        /// <summary>True when this villager's sprite sheet actually holds a walk cycle.
        ///
        /// <c>AnimatedSprite.framesPerAnimation</c> is 4 and the walk helpers this scene calls index
        /// by direction off that: down is frames 0-3, right 4-7, up 8-11, left 12-15. A villager who
        /// never walks in vanilla can ship a much shorter sheet, and asking for frame 12 of a sheet
        /// that holds four runs the source rect clean off the texture. On screen that is a character
        /// flickering and then drawing as a white block, which is what the weapon shop's Marlon did
        /// during a pan (Jeff, 2026-09-11: "the weapon shop guy was blinking in and out, and was a
        /// white box at some point"). Cheaper and more honest to not borrow them than to clamp the
        /// animation and have them slide along on a single standing frame.</summary>
        /// <summary>Every appearance entry this villager has in <c>Data/Characters</c>, with the
        /// season each one is for. Logged next to the one that actually got picked, because the
        /// clothes not changing across a rewind has two very different causes and they look the same
        /// on screen: either vanilla has no seasonal outfit for them at all, or it has one and this
        /// scene is failing to select it.</summary>
        private static string AppearanceOptions(NPC npc)
        {
            try
            {
                var data = npc?.GetData();
                if (data?.Appearance == null || data.Appearance.Count == 0) return "none";
                var parts = new List<string>();
                foreach (var option in data.Appearance)
                    parts.Add($"{option.Id}:{option.Season?.ToString() ?? "any"}");
                return string.Join("|", parts);
            }
            catch (Exception) { return "?"; }
        }

        /// <summary>True when this villager is actually in the valley right now.
        ///
        /// Villagers who have not arrived yet still exist to enumerate, and one of them turned up on
        /// the road: "you had kent there... kent shouldn't be there unless it's a year 2 rewind"
        /// (Jeff, 2026-09-11). An NPC the game has not placed has no current location, which is the
        /// cheapest honest test for whether they are someone the player could have passed in the
        /// street that year.</summary>
        private static bool InTheWorld(NPC npc)
            => npc?.currentLocation != null;

        /// <summary>True when this villager is someone who actually walks Pelican Town, which is
        /// what a schedule means: it is the list of places vanilla sends them during a day, and an
        /// NPC without one stands where they were put and never goes anywhere.
        ///
        /// This is the check that catches the weapon shop's Marlon, and the sprite-sheet one below
        /// does not: his sheet holds exactly sixteen frames, which passes. Logged counts on the whole
        /// cast put the line in the right place, though. Schedule-less: Marlon, Gunther, Mister Qi,
        /// the Dwarf, Krobus, Birdie and the Wizard, none of whom belong on the road in a shot about
        /// the town's ordinary traffic running backwards. Sandy has a schedule of one entry and never
        /// leaves the desert, so two is the floor rather than one.</summary>
        private static bool WalksTheTown(NPC npc)
            => npc?.Schedule != null && npc.Schedule.Count >= MinScheduleEntries;

        /// <summary>Schedule entries a villager needs before this scene will borrow them. See
        /// <see cref="WalksTheTown"/>.</summary>
        private const int MinScheduleEntries = 2;

        /// <summary>Frames this villager's sprite sheet holds, or 0 when it has no usable texture.
        /// Logged with a rejection so a villager who draws wrong can be checked against
        /// <see cref="WalkCycleFrames"/> instead of guessed at.</summary>
        private static int FrameCount(NPC npc)
        {
            StardewValley.AnimatedSprite sprite = npc?.Sprite;
            Microsoft.Xna.Framework.Graphics.Texture2D texture = sprite?.Texture;
            if (texture == null || sprite.SpriteWidth <= 0 || sprite.SpriteHeight <= 0) return 0;
            return (texture.Width / sprite.SpriteWidth) * (texture.Height / sprite.SpriteHeight);
        }

        private static bool CanWalk(NPC npc)
        {
            StardewValley.AnimatedSprite sprite = npc?.Sprite;
            Microsoft.Xna.Framework.Graphics.Texture2D texture = sprite?.Texture;
            if (texture == null || sprite.SpriteWidth <= 0 || sprite.SpriteHeight <= 0) return false;
            int frames = (texture.Width / sprite.SpriteWidth) * (texture.Height / sprite.SpriteHeight);
            return frames >= WalkCycleFrames;
        }

        /// <summary>Frames a full four-direction walk cycle occupies: four directions at
        /// <c>AnimatedSprite.framesPerAnimation</c> each.</summary>
        private const int WalkCycleFrames = 16;

        /// <summary>Moves every extra along for this frame. <paramref name="elapsedMs"/> is the
        /// pan's own clock, so they walk at a steady pace regardless of what the camera is doing.</summary>
        public static void Tick(float elapsedMs, double cameraFraction, GameTime time)
        {
            // NEVER STILL IN SHOT. The walk used to be a band of the camera's progress, parked at a
            // route end before and after it, and "off screen by then" was a hope, not a check: "people
            // are stopping while still visible sometimes, or standing still until they're visible for
            // a second or more ... make sure they start moving off screen and continue moving until
            // they're no longer visible" (Jeff, 2026-09-14). So each walker now waits at its far end
            // until the view comes within StartMarginTiles of it, walks at an ordinary pace from
            // there, and leaves the road when it reaches the door or map edge its route came in by
            // (2026-09-16: walking on in a straight line past the route's end took Emily up a cliff).
            double stepTiles = WalkTilesPerSecond * time.ElapsedGameTime.TotalMilliseconds / 1000.0;
            foreach (Extra extra in Extras)
            {
                if (extra.Npc == null || extra.Route == null || extra.State == WalkState.Gone) continue;

                if (extra.State == WalkState.Waiting)
                {
                    Point start = extra.Route[extra.Route.Length - 1];
                    if (!Utility.isOnScreen(new Vector2(start.X * 64f, start.Y * 64f), StartMarginTiles * 64)) continue;
                    extra.State = WalkState.Walking;
                }

                extra.Along = Math.Max(0.0, extra.Along - stepTiles / (extra.Route.Length - 1));
                if (extra.Along <= 0.0)
                {
                    // Back at the door or map edge it came in by: through it, as villagers go.
                    extra.State = WalkState.Gone;
                    extra.Npc.Position = ParkedPosition;
                    continue;
                }
                Place(extra, extra.Along);
                Animate(extra, extra.Along, time);
            }
        }

        /// <summary>Puts the extra at <paramref name="along"/> of the way through its route, between
        /// tiles rather than snapped to them, so the walk is smooth.</summary>
        private static void Place(Extra extra, double along)
        {
            double exact = Math.Clamp(along, 0.0, 1.0) * (extra.Route.Length - 1);
            int index = (int)exact;
            int next = Math.Min(index + 1, extra.Route.Length - 1);
            float t = (float)(exact - index);
            extra.Npc.Position = new Vector2(
                MathHelper.Lerp(extra.Route[index].X, extra.Route[next].X, t) * 64f,
                MathHelper.Lerp(extra.Route[index].Y, extra.Route[next].Y, t) * 64f);
        }

        /// <summary>The walk cycle for the way the ROUTE runs, not the way the extra is sliding:
        /// that mismatch is the whole effect.</summary>
        private static void Animate(Extra extra, double along, GameTime time)
        {
            int index = Math.Clamp((int)(along * (extra.Route.Length - 1)), 0, extra.Route.Length - 2);
            Point step = new Point(
                extra.Route[index + 1].X - extra.Route[index].X,
                extra.Route[index + 1].Y - extra.Route[index].Y);

            if (Math.Abs(step.X) >= Math.Abs(step.Y))
            {
                if (step.X >= 0) { extra.Npc.faceDirection(Game1.right); extra.Npc.Sprite?.AnimateRight(time); }
                else { extra.Npc.faceDirection(Game1.left); extra.Npc.Sprite?.AnimateLeft(time); }
                return;
            }
            if (step.Y >= 0) { extra.Npc.faceDirection(Game1.down); extra.Npc.Sprite?.AnimateDown(time); }
            else { extra.Npc.faceDirection(Game1.up); extra.Npc.Sprite?.AnimateUp(time); }
        }

        /// <summary>Where every extra is against where the camera is looking, in tiles. The whole
        /// point of the camera-driven walk is that these stay close while each extra is in its band,
        /// and it is far easier to read off a log line than off a screenshot of a moving shot.</summary>
        public static string Positions()
        {
            var parts = new List<string>();
            Vector2 centre = new Vector2(
                (Game1.viewport.X + Game1.viewport.Width / 2f) / 64f,
                (Game1.viewport.Y + Game1.viewport.Height / 2f) / 64f);
            foreach (Extra extra in Extras)
            {
                if (extra.Npc == null) continue;
                Vector2 at = extra.Npc.Tile;
                parts.Add($"{extra.Npc.Name}@({at.X:0},{at.Y:0}) d={Vector2.Distance(at, centre):0}");
            }
            return $"camera({centre.X:0},{centre.Y:0}) " + string.Join(" ", parts);
        }

        /// <summary>Re-picks every borrowed villager's clothes for the season that is on screen now,
        /// without moving them.
        ///
        /// The pan rewinds the year under the camera, and the extras kept wearing whatever they had
        /// on when the rewind started: "the villagers are in their clothes from the last season (fall
        /// in this case) through the whole rewind, so it scans weird" (Jeff, 2026-09-11). Vanilla
        /// picks an NPC's sprite from the <c>Appearance</c> list in <c>Data/Characters</c>, matching
        /// on <c>location.GetSeason()</c> among other things, and <c>NPC.ChooseAppearance</c> does
        /// that every time it is called with no caching, so calling it right after the season swap is
        /// the whole change of clothes.
        ///
        /// The frame and position are put back around the call because choosing an appearance
        /// replaces the sprite's texture, and an extra that blinked back to a standing frame in the
        /// middle of its walk would undo the one thing this scene is for.</summary>
        public static void RefreshAppearance()
        {
            var dressed = new List<string>();
            foreach (Extra extra in Extras)
            {
                NPC npc = extra.Npc;
                if (npc == null) continue;
                try
                {
                    Vector2 position = npc.Position;
                    int facing = npc.FacingDirection;
                    int frame = npc.Sprite?.CurrentFrame ?? 0;
                    npc.ChooseAppearance();
                    npc.Position = position;
                    npc.faceDirection(facing);
                    if (npc.Sprite != null) npc.Sprite.CurrentFrame = frame;
                    dressed.Add($"{npc.Name}={npc.Sprite?.Texture?.Name ?? "?"} picked={npc.LastAppearanceId ?? "none"} options=[{AppearanceOptions(npc)}]");
                }
                catch (Exception ex)
                {
                    _monitor?.Log($"RewindReversedExtras: could not redress {npc.Name}: {ex.Message}", LogLevel.Trace);
                }
            }
            if (dressed.Count > 0)
                _monitor?.Log(
                    $"RewindReversedExtras: dressed for {Game1.season}: {string.Join(", ", dressed)}.",
                    LogLevel.Trace);
        }

        /// <summary>Gives every borrowed villager back exactly where it was found. Idempotent and
        /// safe when nothing was borrowed.</summary>
        public static void Teardown()
        {
            foreach (Extra extra in Extras)
            {
                NPC npc = extra.Npc;
                if (npc == null) continue;
                try
                {
                    npc.controller = null;
                    npc.Halt();
                    if (extra.HomeLocation != null && extra.HomeLocation != _town)
                    {
                        _town?.characters.Remove(npc);
                        if (!extra.HomeLocation.characters.Contains(npc))
                            extra.HomeLocation.characters.Add(npc);
                    }
                    npc.currentLocation = extra.HomeLocation ?? npc.currentLocation;
                    npc.Position = extra.HomePosition;
                    npc.faceDirection(extra.HomeFacing);
                    npc.forceUpdateTimer = extra.HomeForceUpdateTimer;
                }
                catch (Exception ex)
                {
                    _monitor?.Log($"RewindReversedExtras: {ex.GetType().Name} returning {npc.Name}: {ex.Message}", LogLevel.Warn);
                }
            }
            Extras.Clear();
        }
    }
}
