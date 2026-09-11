using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Pathfinding;

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
        /// <summary>How many to put on the road. Enough to read as traffic, few enough that the shot
        /// is still a shot of the town rather than of a crowd.</summary>
        /// <summary>How many villagers a rewind puts on the road, inclusive. Three or four: enough to
        /// read as traffic, few enough that none of them becomes the thing you watch.</summary>
        private const int MinCast = 3, MaxCast = 4;

        /// <summary>How long a route to ask the pathfinder for, in tiles, and how far it may search
        /// before giving up. The pace is not set here any more: each extra covers its own route once
        /// across the pan's whole duration (see Tick), so the route length IS the speed, and asking
        /// for roughly this many tiles is what keeps that speed an ordinary walk.</summary>
        // Long enough that a person walking at an ordinary pace is still walking when the pan ends.
        // At the old fourteen the pathfinder returned routes of about thirty to forty-five tiles,
        // which is a little over fifteen seconds of walking in a shot of twenty-five, and the extras
        // spent the back half of every pan walking the way they had come.
        private const int PathfinderLimit = 900;

        /// <summary>Route lengths to try, in tiles, longest first. The long ones keep an extra
        /// walking backwards for the whole pan at an ordinary pace; the short ones are
        /// the fallback for a start tile with nothing that far away.</summary>
        private static readonly int[] RouteTileChoices = { 34, 26, 20, 14 };

        /// <summary>How far off the camera's line an extra starts, so they are scattered around the
        /// square rather than queued along one path.</summary>
        private static readonly Point[] Scatter =
        {
            new Point(0, -4), new Point(3, 3), new Point(-4, 2), new Point(2, -5), new Point(-2, 5),
        };

        private sealed class Extra
        {
            public NPC Npc;
            public GameLocation HomeLocation;
            public Vector2 HomePosition;
            public int HomeFacing;
            public int HomeForceUpdateTimer;
            public Point[] Route;      // forward order: Route[0] is where a normal walk would start
            public double Phase;       // kept for the placement scatter; the walk itself is paced off DurationMs
            public double DurationMs;  // how long this extra has to cover its route, once, backwards
        }

        private static readonly List<Extra> Extras = new List<Extra>();
        private static IMonitor _monitor;
        private static GameLocation _town;

        /// <summary>Borrows a few villagers and puts them on routes through the square. Safe to call
        /// when there are none to borrow, or when no route can be found: the pan runs on its other
        /// dials alone.
        ///
        /// A DIFFERENT FEW EVERY TIME. This used to take the first five villagers the game happened
        /// to enumerate, which is a stable order, so every rewind in a run showed Evelyn, George,
        /// Alex, Emily and Haley walking the same ground: "watching the same exact people run through
        /// their paths backwards every reset is going to get tedious" (Jeff, 2026-09-11). The whole
        /// eligible cast is shuffled now and walked in that order until enough of them have a route,
        /// so anyone in town can turn up and the ones who do are different each rewind.
        ///
        /// Shuffling first, rather than pathfinding for all thirty-odd villagers and then choosing,
        /// is deliberate: it gives the same variety for the cost of the three or four routes that are
        /// actually used, and this runs on the frame the white flash hands over to the pan, which is
        /// not a frame to spend a hundred pathfinder searches on.</summary>
        public static void Spawn(IMonitor monitor, GameLocation town, Point from, Point to, float durationMs)
        {
            _monitor = monitor;
            _town = town;
            Extras.Clear();
            if (town == null) return;

            List<NPC> cast = Shuffle(Cast());
            int wanted = MinCast + Game1.random.Next(MaxCast - MinCast + 1);
            var placed = new List<string>();
            for (int i = 0; i < cast.Count && Extras.Count < wanted; i++)
            {
                // The anchor spreads the walks along the camera's line, so it counts placements
                // rather than candidates: a villager who could not be routed must not leave a gap in
                // the shot where the next one should have been.
                Point[] route = FindRoute(town, from, to, Extras.Count, wanted);
                if (route == null || route.Length < 4) continue;

                NPC npc = cast[i];
                var extra = new Extra
                {
                    Npc = npc,
                    HomeLocation = npc.currentLocation,
                    HomePosition = npc.Position,
                    HomeFacing = npc.FacingDirection,
                    HomeForceUpdateTimer = npc.forceUpdateTimer,
                    Route = route,
                    Phase = i / (double)Math.Max(1, cast.Count),
                    DurationMs = durationMs,
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
                    Place(extra, 0.0);
                    placed.Add($"{npc.Name} ({route.Length} tiles from ({route[0].X},{route[0].Y}))");
                }
                catch (Exception ex)
                {
                    _monitor?.Log($"RewindReversedExtras: could not place {npc.Name}: {ex.Message}", LogLevel.Warn);
                }
            }

            _monitor?.Log(
                placed.Count > 0
                    ? $"RewindReversedExtras: {string.Join("; ", placed)}, all walking their routes backwards."
                    : "RewindReversedExtras: no walkable route found near the camera line; the pan runs without extras.",
                LogLevel.Info);
        }

        /// <summary>A real walkable route through the square for extra <paramref name="index"/>.
        /// Both ends are sampled off the camera's line, scattered, and then snapped to somewhere the
        /// pathfinder will actually accept, so the result follows the roads and bridges the town has
        /// rather than the straight line the camera takes.</summary>
        private static Point[] FindRoute(GameLocation town, Point from, Point to, int index, int count)
        {
            double alongLine = (index + 0.5) / Math.Max(1, count);
            Point scatter = Scatter[index % Scatter.Length];
            var anchor = new Point(
                (int)Math.Round(from.X + (to.X - from.X) * alongLine) + scatter.X,
                (int)Math.Round(from.Y + (to.Y - from.Y) * alongLine) + scatter.Y);

            Point? start = NearestWalkable(town, anchor);
            if (start == null) return null;

            // Aim along the camera's line so the walks read as traffic heading the way the shot is
            // going, then let the pathfinder work out how a person actually gets there.
            // Longest first, then settle for less. Asking only for the long route dropped the cast
            // from five to two, because most start tiles have nothing walkable that far along the
            // camera's line; a shorter route walked slower is much better than an extra that never
            // appears.
            foreach (int reach in RouteTileChoices)
            {
                int dx = Math.Sign(to.X - from.X) * reach;
                int dy = Math.Sign(to.Y - from.Y) * reach;
                Point? end = NearestWalkable(town, new Point(start.Value.X + dx, start.Value.Y + dy))
                             ?? NearestWalkable(town, new Point(start.Value.X - dx, start.Value.Y - dy));
                if (end == null || end.Value == start.Value) continue;

                try
                {
                    Stack<Point> path = PathFindController.findPathForNPCSchedules(
                        start.Value, end.Value, town, PathfinderLimit);
                    if (path == null || path.Count < 4) continue;
                    return path.ToArray();
                }
                catch (Exception ex)
                {
                    _monitor?.Log($"RewindReversedExtras: pathfinder refused a route: {ex.Message}", LogLevel.Trace);
                }
            }
            return null;
        }

        /// <summary>The nearest tile to <paramref name="wanted"/> a villager could stand on, searched
        /// outward in rings. Null when there is nothing walkable nearby at all.</summary>
        private static Point? NearestWalkable(GameLocation town, Point wanted)
        {
            for (int ring = 0; ring <= 6; ring++)
            {
                for (int dx = -ring; dx <= ring; dx++)
                {
                    for (int dy = -ring; dy <= ring; dy++)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != ring) continue;
                        var tile = new Point(wanted.X + dx, wanted.Y + dy);
                        if (!town.isTileOnMap(tile.X, tile.Y)) continue;
                        try
                        {
                            if (town.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport)
                                && !town.isWaterTile(tile.X, tile.Y))
                                return tile;
                        }
                        catch (Exception) { }
                    }
                }
            }
            return null;
        }

        /// <summary>A copy of <paramref name="cast"/> in a random order (Fisher-Yates, on the game's
        /// own random so a run is still reproducible from its seed).</summary>
        private static List<NPC> Shuffle(List<NPC> cast)
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
        public static void Tick(float elapsedMs, GameTime time)
        {
            foreach (Extra extra in Extras)
            {
                if (extra.Npc == null || extra.Route == null) continue;

                // BACKWARDS, ONCE, FOR THE WHOLE PAN. This used to be a triangle along the route,
                // walking back to the start and then forward again, because the routes were about
                // fourteen tiles and the walk ran out long before the shot did. The turn
                // was plainly visible: "the people are walking backwards and forwards across the
                // same path, that's not what I want. Just backwards, on a path long enough that they
                // can go backwards the whole time they're on screen" (Jeff, 2026-09-11).
                //
                // So the route is now asked to be long enough (RouteTiles) and the walk is paced off
                // the pan's own duration rather than a fixed tiles-per-second: every extra leaves the
                // far end of its route at the first frame and arrives at the near end on the last
                // one, so nobody turns round and nobody stands still waiting. The pace that falls out
                // of that is the route's length over the pan's duration, which for the lengths the
                // pathfinder returns here is an ordinary walking speed.
                double progress = extra.DurationMs > 0.0
                    ? Math.Clamp(elapsedMs / extra.DurationMs, 0.0, 1.0)
                    : 0.0;
                double along = 1.0 - progress;
                Place(extra, along);
                Animate(extra, along, time);
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
                    dressed.Add($"{npc.Name}={npc.Sprite?.Texture?.Name ?? "?"}");
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
