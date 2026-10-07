using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>The pure arithmetic behind the tamper scene's cloud (spec 2026-09-21, Scene 4): where
    /// each soft blob of it starts, where it settles, when it moves and how dark it is at any
    /// instant. Where the map and its frame land on the screen is <see cref="SceneMapFit"/>.
    ///
    /// Every position here is in MAP PIXELS: the world map's art times four, with (0,0) at the
    /// map's top left corner. That is the space <c>MapRegion</c> hands back from
    /// <c>GetPixelArea</c> and <c>MapPixelArea</c>, so the farm's rectangle comes straight from the
    /// game's own map data. The SCREEN is a rectangle in the same space; with the map framed in the
    /// middle it starts left of and above the map.
    ///
    /// The cloud covers the whole screen, not just the map (Jeff, 2026-10-07, the framed map): the
    /// scattered share rests on an even grid over the screen, the farm's share packs over the farm
    /// so the farm ends the darkest place, and every blob comes in from above the screen's top edge.
    ///
    /// It is here, and not beside the painter, because it is the only part of the cloud that can be
    /// checked without a graphics device.</summary>
    public static class SceneCloud
    {
        /// <summary>How many scattered blobs cover a screen the size of the map. A bigger screen
        /// gets more, in proportion to its area, so the cover looks the same, up to
        /// <see cref="MaxScatterCount"/>; past that the blobs grow instead.</summary>
        public const int BaseScatterCount = 30;

        /// <summary>The most scattered blobs any screen asks for (the grid rounds to whole rows, so
        /// the real count can sit a few either side of it).</summary>
        public const int MaxScatterCount = 60;

        /// <summary>The share of the cloud that settles over the farm on a map-sized screen: one farm
        /// blob for every three scattered ones, a quarter of the whole (see <see cref="FarmCount"/>). Not the first cut's two fifths: Jeff found the
        /// farm piled too high and the valley too bare (2026-10-07).</summary>
        public const double FarmShare = 0.25;

        /// <summary>When the first blob starts to drift in.</summary>
        public const int PourAtMs = 1500;

        /// <summary>The last scattered blob's start. Scattered starts are spread from
        /// <see cref="PourAtMs"/> to here.</summary>
        public const int ScatterLastStartMs = 5500;

        /// <summary>The farm blobs' starts are spread over this window, late, so they drift in
        /// behind the veil and settle together.</summary>
        public const int FarmFirstStartMs = 4500;
        public const int FarmLastStartMs = 6000;

        /// <summary>When every farm blob is at rest. The dim is at full by then as well, and nothing
        /// is still moving after it.</summary>
        public const int SettledAtMs = 9000;

        /// <summary>How long a blob takes to drift to its rest, at least and at most. A drift, not a
        /// dart (Jeff, 2026-10-07: the first cut's two seconds "darted in"). A scattered blob's time
        /// grows with how far down the screen it rests, so the far ones are not hurried, and the far
        /// ones start first so they still arrive before the farm settles.</summary>
        public const int TravelMinMs = 3000;
        public const int TravelMaxMs = 4500;

        /// <summary>A blob's alpha from the moment it sets out.</summary>
        public const float BlobAlpha = 0.7f;

        /// <summary>The full dim's alpha once it has eased in.</summary>
        public const float DimAlpha = 0.35f;

        /// <summary>A scattered blob's diameter, as a share of the map's width (grown on a screen
        /// that wanted more blobs than <see cref="MaxScatterCount"/>). Big enough that they overlap
        /// into a veil rather than standing as dark spots of their own.</summary>
        private const double ScatterSizeMin = 0.22;
        private const double ScatterSizeMax = 0.32;

        /// <summary>A farm blob's diameter, as a share of the farm rectangle's shorter side. Most of
        /// the side, so they overlap over the farm and, with the veil already on it, make it the
        /// darkest place without blacking it out (1.2 to 1.6 did, 2026-10-07).</summary>
        private const double FarmSizeMin = 0.7;
        private const double FarmSizeMax = 1.0;

        /// <summary>The farm's own blobs are laid on a small grid over the farm, this many across.</summary>
        private const int FarmColumns = 5;

        /// <summary>How far from its cell's middle a rest may land, as a share of the cell. Half the
        /// cell either way would let two neighbours meet on a shared edge and make a dark knot;
        /// a quarter keeps the veil even (2026-10-07 frames).</summary>
        private const double CellJitter = 0.25;

        /// <summary>How far in from the farm's edges a farm blob's centre may settle, as a share of
        /// the farm's size, so the packed blobs sit ON the farm rather than straddling its border.</summary>
        private const double FarmInset = 0.2;

        /// <summary>Every blob comes in from the north (Jeff, 2026-10-07: "all the clouds come in
        /// from the north, not start on the map and spread outward"). It starts wholly above the
        /// screen's top edge, its whole disc off the screen, this much higher again as a share of
        /// the screen's height, and drifts south to its rest.</summary>
        private const double SpawnAboveScreen = 0.02;

        /// <summary>How far sideways a blob may start from above its own rest, as a share of the
        /// screen's width, so the cloud drifts in a little on the slant rather than in plumb lines.</summary>
        private const double SpawnSideways = 0.04;

        /// <summary>The most a blob's path bows sideways, as a share of the screen's height, so the
        /// cloud pours rather than marches in straight lines.</summary>
        private const double BowMax = 0.06;

        /// <summary>A settled blob's slow drift: its swing in map pixels (it can travel twice this from
        /// where it arrived) and its period.</summary>
        private const double DriftPixels = 6.0;
        private const double DriftPeriodMs = 1100.0;

        /// <summary>One soft blob of the cloud.</summary>
        public readonly struct Blob
        {
            public Blob(double spawnX, double spawnY, double restX, double restY, double diameter, double bow, double phase, int startMs, int arriveMs, bool onFarm)
            {
                SpawnX = spawnX; SpawnY = spawnY; RestX = restX; RestY = restY;
                Diameter = diameter; Bow = bow; Phase = phase;
                StartMs = startMs; ArriveMs = arriveMs; OnFarm = onFarm;
            }

            /// <summary>Where it comes from, wholly above the screen's top edge, in map pixels.</summary>
            public double SpawnX { get; }
            public double SpawnY { get; }
            /// <summary>Where it settles, in map pixels.</summary>
            public double RestX { get; }
            public double RestY { get; }
            /// <summary>How wide it is drawn, in map pixels.</summary>
            public double Diameter { get; }
            /// <summary>How far its path bows sideways at the middle, in map pixels (either sign).</summary>
            public double Bow { get; }
            /// <summary>Its own offset for the slow drift once settled, in radians.</summary>
            public double Phase { get; }
            public int StartMs { get; }
            public int ArriveMs { get; }
            /// <summary>True for a blob that packs over the farm.</summary>
            public bool OnFarm { get; }
        }

        /// <summary>The scattered blobs' grid for a screen of the given size in map pixels: about
        /// square cells, one blob each, as many as the screen's area asks for against
        /// <see cref="BaseScatterCount"/> on a map-sized screen, capped near
        /// <see cref="MaxScatterCount"/>.</summary>
        public static (int Columns, int Rows) ScatterGrid(double screenWidth, double screenHeight, double mapWidth, double mapHeight)
        {
            double ratio = screenWidth * screenHeight / (mapWidth * mapHeight);
            double target = Math.Max(BaseScatterCount, Math.Min(MaxScatterCount, BaseScatterCount * ratio));
            int columns = Math.Max(1, (int)Math.Round(Math.Sqrt(target * screenWidth / screenHeight)));
            int rows = Math.Max(1, (int)Math.Round(target / columns));
            return (columns, rows);
        }

        /// <summary>How many blobs pack over the farm: a quarter of the cloud on a map-sized screen
        /// (one for every three of <see cref="BaseScatterCount"/>), and the same number on any
        /// bigger screen. The farm is the same size on the map whatever the screen, and the extra
        /// scattered blobs a big screen gets fall on the extra screen round the map, so scaling
        /// the farm's share with them only piled the farm black (the first native frames,
        /// 2026-10-07, with twenty).</summary>
        public static int FarmCount => (int)Math.Round(BaseScatterCount * FarmShare / (1 - FarmShare));

        /// <summary>The whole cloud, sorted by start. The map is <paramref name="mapWidth"/> by
        /// <paramref name="mapHeight"/> map pixels with the farm at the given rectangle; the screen
        /// is the given rectangle in the same space. Scattered blobs rest on an even grid over the
        /// whole screen and start from <see cref="PourAtMs"/> to <see cref="ScatterLastStartMs"/>;
        /// farm blobs pack over the farm and start from <see cref="FarmFirstStartMs"/> to
        /// <see cref="FarmLastStartMs"/>; every farm blob is at rest at <see cref="SettledAtMs"/>.
        /// Every blob starts wholly above the screen.</summary>
        public static IReadOnlyList<Blob> Plan(
            int mapWidth, int mapHeight, int farmX, int farmY, int farmWidth, int farmHeight,
            double screenX, double screenY, double screenWidth, double screenHeight, Random rng)
        {
            if (mapWidth <= 0 || mapHeight <= 0) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            if (screenWidth <= 0 || screenHeight <= 0) throw new ArgumentOutOfRangeException(nameof(screenWidth));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            (int columns, int rows) = ScatterGrid(screenWidth, screenHeight, mapWidth, mapHeight);
            int scatter = columns * rows;
            int farm = FarmCount;
            // A screen that wanted more blobs than the cap gets bigger ones, so the cover is the same.
            double wanted = BaseScatterCount * screenWidth * screenHeight / (mapWidth * (double)mapHeight);
            double grow = Math.Max(1, Math.Sqrt(wanted / scatter));
            var screen = new Area(screenX, screenY, screenWidth, screenHeight);
            var blobs = new List<Blob>(scatter + farm);
            double farmShort = Math.Max(1, Math.Min(farmWidth, farmHeight));

            // Scattered blobs: rests first, then the drift time from how far down each rests, then
            // the starts, longest drift first, so a far-south blob sets out early and a blob that
            // rests near the top comes in last.
            var rests = new List<Rest>(scatter);
            for (int i = 0; i < scatter; i++)
            {
                double diameter = mapWidth * grow * Between(rng, ScatterSizeMin, ScatterSizeMax);
                (double restX, double restY) = GridRest(i, scatter, columns, screen, rng);
                double south = Math.Max(0, Math.Min(1, (restY - screenY) / screenHeight));
                int travel = (int)Math.Round(TravelMinMs + (TravelMaxMs - TravelMinMs) * south);
                rests.Add(new Rest(restX, restY, diameter, travel));
            }
            rests.Sort(LongestTravelFirst);
            for (int i = 0; i < rests.Count; i++)
            {
                Rest r = rests[i];
                int start = PourAtMs + Spread(i, scatter, ScatterLastStartMs - PourAtMs);
                blobs.Add(Make(rng, screen, r.X, r.Y, r.Diameter, start, Math.Min(SettledAtMs, start + r.Travel), onFarm: false));
            }
            var farmArea = new Area(
                farmX + farmWidth * FarmInset, farmY + farmHeight * FarmInset,
                farmWidth * (1 - 2 * FarmInset), farmHeight * (1 - 2 * FarmInset));
            for (int i = 0; i < farm; i++)
            {
                // Farm starts come late and all arrive together, a 3000 to 4500 ms drift each.
                int start = FarmFirstStartMs + Spread(i, farm, FarmLastStartMs - FarmFirstStartMs);
                (double restX, double restY) = GridRest(i, farm, FarmColumns, farmArea, rng);
                double diameter = farmShort * Between(rng, FarmSizeMin, FarmSizeMax);
                blobs.Add(Make(rng, screen, restX, restY, diameter, start, SettledAtMs, onFarm: true));
            }
            blobs.Sort(EarliestStartFirst);
            return blobs;
        }

        private readonly struct Area
        {
            public Area(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
            public double X { get; }
            public double Y { get; }
            public double Width { get; }
            public double Height { get; }
        }

        private readonly struct Rest
        {
            public Rest(double x, double y, double diameter, int travel) { X = x; Y = y; Diameter = diameter; Travel = travel; }
            public double X { get; }
            public double Y { get; }
            public double Diameter { get; }
            public int Travel { get; }
        }

        private static int LongestTravelFirst(Rest a, Rest b) => b.Travel.CompareTo(a.Travel);

        private static int EarliestStartFirst(Blob a, Blob b) => a.StartMs.CompareTo(b.StartMs);

        /// <summary>Blob <paramref name="index"/>'s rest: a spot near the middle of its own cell of a
        /// grid <paramref name="columns"/> wide over the given area, with as many rows as the count
        /// needs. The cells are taken in a scrambled order, so the start times do not sweep the grid
        /// row by row.</summary>
        private static (double X, double Y) GridRest(int index, int count, int columns, Area area, Random rng)
        {
            int rows = Math.Max(1, (count + columns - 1) / columns);
            int cell = Shuffled(count, index);
            double cellWidth = area.Width / columns, cellHeight = area.Height / rows;
            int column = cell % columns, row = cell / columns;
            return (area.X + (column + Between(rng, 0.5 - CellJitter, 0.5 + CellJitter)) * cellWidth,
                    area.Y + (row + Between(rng, 0.5 - CellJitter, 0.5 + CellJitter)) * cellHeight);
        }

        /// <summary>A fixed scramble of 0 to count-1: a stride coprime with the count walks every
        /// cell once without sweeping them in order.</summary>
        private static int Shuffled(int count, int index)
        {
            int stride = 7;
            while (Gcd(stride, count) != 1) stride++;
            return (int)((long)index * stride % count);
        }

        private static int Gcd(int a, int b) => b == 0 ? a : Gcd(b, a % b);

        private static Blob Make(Random rng, Area screen, double restX, double restY, double diameter, int start, int arrive, bool onFarm)
        {
            // Above its own rest, a little to one side, with its whole disc off the top of the screen.
            double spawnX = restX + screen.Width * SpawnSideways * (rng.NextDouble() * 2 - 1);
            double spawnY = screen.Y - (diameter / 2) - screen.Height * SpawnAboveScreen;
            double bow = screen.Height * BowMax * (rng.NextDouble() * 2 - 1);
            double phase = rng.NextDouble() * Math.PI * 2;
            return new Blob(spawnX, spawnY, restX, restY, diameter, bow, phase, start, Math.Max(start + 1, arrive), onFarm);
        }

        /// <summary>How far through its journey a blob is, eased out so it slows as it settles. 0
        /// before it starts, 1 from its arrival on.</summary>
        public static double Progress(Blob blob, int elapsedMs)
        {
            if (elapsedMs <= blob.StartMs) return 0;
            if (elapsedMs >= blob.ArriveMs) return 1;
            double t = (elapsedMs - blob.StartMs) / (double)(blob.ArriveMs - blob.StartMs);
            // A sine ease-out: it slows into its rest without the cubic's rush off the start,
            // which carried a blob most of the way down the map in its first third.
            return Math.Sin(t * Math.PI / 2);
        }

        /// <summary>Where a blob's centre is this instant, in map pixels: on its bowed path while it
        /// travels, and drifting a few pixels round its rest once settled.</summary>
        public static (double X, double Y) Position(Blob blob, int elapsedMs)
        {
            double p = Progress(blob, elapsedMs);
            double dx = blob.RestX - blob.SpawnX, dy = blob.RestY - blob.SpawnY;
            double length = Math.Sqrt(dx * dx + dy * dy);
            double bow = length > 0 ? blob.Bow * Math.Sin(Math.PI * p) : 0;
            double nx = length > 0 ? -dy / length : 0, ny = length > 0 ? dx / length : 0;
            double x = blob.SpawnX + dx * p + nx * bow;
            double y = blob.SpawnY + dy * p + ny * bow;
            if (p >= 1)
            {
                // Measured from where it arrived, so the drift starts at nothing and never jumps.
                double since = (elapsedMs - blob.ArriveMs) / DriftPeriodMs + blob.Phase;
                x += DriftPixels * (Math.Sin(since) - Math.Sin(blob.Phase));
                y += DriftPixels * 0.5 * (Math.Cos(since) - Math.Cos(blob.Phase));
            }
            return (x, y);
        }

        /// <summary>A blob's alpha this instant: nothing before it sets out, then
        /// <see cref="BlobAlpha"/> at once, held all the way in. It sets out wholly above the
        /// screen, so it is already at full darkness when it comes into view and the cloud is seen
        /// rolling in dark from the north (Jeff, 2026-10-07: a ramp over the journey looked like
        /// "blowing in transparent and then darkened already halfway down the screen"). The valley
        /// darkens from more cloud arriving and overlapping, and from the dim, never from a blob
        /// fading up mid-screen.</summary>
        public static float Alpha(Blob blob, int elapsedMs)
            => elapsedMs <= blob.StartMs ? 0f : BlobAlpha;

        /// <summary>The dim this instant: eases in with the cloud, from the first blob to the farm
        /// settling, and holds.</summary>
        public static float Dim(int elapsedMs)
        {
            double t = (elapsedMs - PourAtMs) / (double)(SettledAtMs - PourAtMs);
            return DimAlpha * (float)SmoothStep(t);
        }

        private static int Spread(int index, int count, int spreadMs)
            => count <= 1 ? 0 : (int)Math.Round(spreadMs * index / (double)(count - 1));

        private static double Between(Random rng, double min, double max) => min + (max - min) * rng.NextDouble();

        private static double SmoothStep(double t)
        {
            t = Math.Max(0, Math.Min(1, t));
            return t * t * (3 - 2 * t);
        }
    }
}
