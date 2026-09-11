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
        private const int Count = 5;

        /// <summary>Tiles a second. Vanilla walking pace is about three; this is deliberately just
        /// under, because the thing that killed the first attempt at a walking villager was speed.</summary>
        private const float TilesPerSecond = 2.5f;

        /// <summary>How long a route to ask the pathfinder for, in tiles, and how far it may search
        /// before giving up. Short routes: these are people crossing the frame the camera happens to
        /// be on, not travellers.</summary>
        private const int RouteTiles = 14, PathfinderLimit = 400;

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
            public double Phase;       // where on the route this one begins, 0..1
        }

        private static readonly List<Extra> Extras = new List<Extra>();
        private static IMonitor _monitor;
        private static GameLocation _town;

        /// <summary>Borrows up to <see cref="Count"/> villagers and puts them on routes through the
        /// square. Safe to call when there are none to borrow, or when no route can be found: the
        /// pan runs on its other dials alone.</summary>
        public static void Spawn(IMonitor monitor, GameLocation town, Point from, Point to)
        {
            _monitor = monitor;
            _town = town;
            Extras.Clear();
            if (town == null) return;

            List<NPC> cast = Cast();
            var placed = new List<string>();
            for (int i = 0; i < cast.Count; i++)
            {
                Point[] route = FindRoute(town, from, to, i, cast.Count);
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
            int dx = Math.Sign(to.X - from.X) * RouteTiles;
            int dy = Math.Sign(to.Y - from.Y) * RouteTiles;
            Point? end = NearestWalkable(town, new Point(start.Value.X + dx, start.Value.Y + dy))
                         ?? NearestWalkable(town, new Point(start.Value.X - dx, start.Value.Y - dy));
            if (end == null || end.Value == start.Value) return null;

            try
            {
                Stack<Point> path = PathFindController.findPathForNPCSchedules(
                    start.Value, end.Value, town, PathfinderLimit);
                if (path == null || path.Count < 4) return null;
                return path.ToArray();
            }
            catch (Exception ex)
            {
                _monitor?.Log($"RewindReversedExtras: pathfinder refused a route: {ex.Message}", LogLevel.Trace);
                return null;
            }
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

        /// <summary>Villagers that can be borrowed without the scene reading as wrong: real
        /// townsfolk, not children, not the player's spouse (who should be at home in the bed the
        /// farmer is asleep in), and not anyone already committed to an event.</summary>
        private static List<NPC> Cast()
        {
            var cast = new List<NPC>();
            string spouse = Game1.player?.spouse;
            Utility.ForEachVillager(npc =>
            {
                if (cast.Count >= Count) return false;
                if (npc == null || npc is Child || npc is Horse || npc is Pet) return true;
                if (!npc.IsVillager || npc.IsInvisible) return true;
                if (spouse != null && npc.Name == spouse) return true;
                if (npc.EventActor) return true;
                cast.Add(npc);
                return true;
            });
            return cast;
        }

        /// <summary>Moves every extra along for this frame. <paramref name="elapsedMs"/> is the
        /// pan's own clock, so they walk at a steady pace regardless of what the camera is doing.</summary>
        public static void Tick(float elapsedMs, GameTime time)
        {
            foreach (Extra extra in Extras)
            {
                if (extra.Npc == null || extra.Route == null) continue;

                // A triangle along the route: back to its start, then forward again, so an extra
                // never runs off the end and never has to be respawned. The BACKWARD half is the
                // one the scene is about; the forward half reads as the same footage rewinding on.
                double tilesWalked = elapsedMs / 1000.0 * TilesPerSecond;
                double laps = tilesWalked / Math.Max(1, extra.Route.Length - 1) + extra.Phase;
                double within = laps % 2.0;
                double along = within <= 1.0 ? 1.0 - within : within - 1.0;
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
