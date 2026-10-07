using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage
{
    /// <summary>The pure arithmetic behind the tamper scene's cloud (spec 2026-09-21, Scene 4): where
    /// each soft blob of it starts, where it settles, when it moves and how dark it is at any
    /// instant, and how a rectangle on the world map lands on the screen.
    ///
    /// Every position here is in MAP PIXELS AT THE MAP TAB'S OWN SCALE: the world map's art times
    /// four, with (0,0) at the map's top left corner. That is the space <c>MapRegion</c> hands back
    /// from <c>GetPixelArea</c> and <c>MapPixelArea</c>, so the farm's rectangle comes straight from
    /// the game's own map data and the painter only applies the screen fit (<see cref="Fit"/>).
    ///
    /// It is here, and not beside the painter, because it is the only part of the cloud that can be
    /// checked without a graphics device.</summary>
    public static class SceneCloud
    {
        /// <summary>How many blobs make the cloud.</summary>
        public const int BlobCount = 40;

        /// <summary>The share of the cloud that settles over the farm. The rest scatters over the
        /// whole map, so the cloud covers the valley and lies thickest on the farm. A quarter, not
        /// the first cut's two fifths: Jeff found the farm piled too high and the valley too bare
        /// (2026-10-07).</summary>
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

        /// <summary>How long a scattered blob takes to cross to its rest, at least and at most. A
        /// drift, not a dart (Jeff, 2026-10-07: the first cut's two seconds "darted in").</summary>
        public const int TravelMinMs = 3000;
        public const int TravelMaxMs = 4500;

        /// <summary>A blob's alpha once it has arrived.</summary>
        public const float BlobAlpha = 0.7f;

        /// <summary>The full-map dim's alpha once it has eased in.</summary>
        public const float DimAlpha = 0.35f;

        /// <summary>The share of a blob's journey its alpha takes to reach <see cref="BlobAlpha"/>,
        /// so a blob is already dark when it is halfway across and does not fade in on arrival.</summary>
        private const double AlphaRampShare = 0.6;

        /// <summary>A scattered blob's diameter, as a share of the map's width. Big enough that the
        /// thirty of them overlap into a veil over the whole valley rather than standing as dark
        /// spots of their own.</summary>
        private const double ScatterSizeMin = 0.22;
        private const double ScatterSizeMax = 0.32;

        /// <summary>A farm blob's diameter, as a share of the farm rectangle's shorter side. Most of
        /// the side, so the ten of them overlap over the farm and, with the veil already on it, make
        /// it the darkest place on the map without blacking it out (1.2 to 1.6 did, 2026-10-07).</summary>
        private const double FarmSizeMin = 0.7;
        private const double FarmSizeMax = 1.0;

        /// <summary>The scattered rests are laid on a grid this many cells across and down, one blob
        /// a cell at a random spot inside it, so the veil covers the whole valley evenly. Plain
        /// random rests left one side of the map black and the other bare (2026-10-07).</summary>
        private const int ScatterColumns = 6;

        /// <summary>The farm's own blobs are laid the same way on a smaller grid over the farm.</summary>
        private const int FarmColumns = 5;

        /// <summary>How far from its cell's middle a rest may land, as a share of the cell. Half the
        /// cell either way would let two neighbours meet on a shared edge and make a dark knot;
        /// a quarter keeps the veil even (2026-10-07 frames).</summary>
        private const double CellJitter = 0.25;

        /// <summary>How far in from the farm's edges a farm blob's centre may settle, as a share of
        /// the farm's size, so the packed blobs sit ON the farm rather than straddling its border.</summary>
        private const double FarmInset = 0.2;

        /// <summary>The spawn line runs along the map's top right, the mountain and mines side:
        /// from above the top edge at this share of the width to past the right edge at this share
        /// of the height.</summary>
        private const double SpawnFromX = 0.55;
        private const double SpawnToY = 0.45;

        /// <summary>How far off the map the spawn line sits, as a share of the map's height.</summary>
        private const double SpawnOffMap = 0.15;

        /// <summary>The most a blob's path bows sideways, as a share of the map's height, so the
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

            /// <summary>Where it comes from, on the spawn line, in map pixels.</summary>
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

        /// <summary>How many of <paramref name="count"/> blobs settle over the farm.</summary>
        public static int FarmCount(int count) => (int)Math.Round(count * FarmShare);

        /// <summary>The whole cloud, for a map <paramref name="mapWidth"/> by
        /// <paramref name="mapHeight"/> map pixels with the farm at the given rectangle (also map
        /// pixels), sorted by start. Scattered blobs start from <see cref="PourAtMs"/> to
        /// <see cref="ScatterLastStartMs"/> and farm blobs from <see cref="FarmFirstStartMs"/> to
        /// <see cref="FarmLastStartMs"/>, so the farm's share drifts in among the last of the rest;
        /// every farm blob is at rest at <see cref="SettledAtMs"/>.</summary>
        public static IReadOnlyList<Blob> Plan(int count, int mapWidth, int mapHeight, int farmX, int farmY, int farmWidth, int farmHeight, Random rng)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            if (mapWidth <= 0 || mapHeight <= 0) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            int farm = FarmCount(count);
            int scatter = count - farm;
            var blobs = new List<Blob>(count);
            double farmShort = Math.Max(1, Math.Min(farmWidth, farmHeight));

            for (int i = 0; i < scatter; i++)
            {
                // Scattered starts are spread from the pour to ScatterLastStartMs, and each one
                // drifts for its own few seconds, never past the farm settling.
                int start = PourAtMs + Spread(i, scatter, ScatterLastStartMs - PourAtMs);
                int travel = (int)Math.Round(Between(rng, TravelMinMs, TravelMaxMs));
                double diameter = mapWidth * Between(rng, ScatterSizeMin, ScatterSizeMax);
                (double restX, double restY) = GridRest(i, scatter, ScatterColumns, 0, 0, mapWidth, mapHeight, rng);
                blobs.Add(Make(rng, mapWidth, mapHeight, restX, restY,
                    diameter, start, Math.Min(SettledAtMs, start + travel), onFarm: false));
            }
            for (int i = 0; i < farm; i++)
            {
                // Farm starts come late and all arrive together, a 3000 to 4500 ms drift each.
                int start = FarmFirstStartMs + Spread(i, farm, FarmLastStartMs - FarmFirstStartMs);
                (double restX, double restY) = GridRest(i, farm, FarmColumns,
                    farmX + farmWidth * FarmInset, farmY + farmHeight * FarmInset,
                    farmWidth * (1 - 2 * FarmInset), farmHeight * (1 - 2 * FarmInset), rng);
                double diameter = farmShort * Between(rng, FarmSizeMin, FarmSizeMax);
                blobs.Add(Make(rng, mapWidth, mapHeight, restX, restY, diameter, start, SettledAtMs, onFarm: true));
            }
            blobs.Sort((a, b) => a.StartMs.CompareTo(b.StartMs));
            return blobs;
        }

        /// <summary>Blob <paramref name="index"/>'s rest: a spot near the middle of its own cell of a
        /// grid <paramref name="columns"/> wide over the given rectangle, with as many rows as the
        /// count needs. The cells are taken in a scrambled order, so the start times do not sweep
        /// the grid row by row.</summary>
        private static (double X, double Y) GridRest(int index, int count, int columns, double left, double top, double width, double height, Random rng)
        {
            int rows = Math.Max(1, (count + columns - 1) / columns);
            int cell = Shuffled(count, index);
            double cellWidth = width / columns, cellHeight = height / rows;
            int column = cell % columns, row = cell / columns;
            return (left + (column + Between(rng, 0.5 - CellJitter, 0.5 + CellJitter)) * cellWidth,
                    top + (row + Between(rng, 0.5 - CellJitter, 0.5 + CellJitter)) * cellHeight);
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

        private static Blob Make(Random rng, int mapWidth, int mapHeight, double restX, double restY, double diameter, int start, int arrive, bool onFarm)
        {
            // A point on the spawn line, from above the top edge right of centre round to past the
            // right edge above the middle.
            double along = rng.NextDouble();
            double off = mapHeight * SpawnOffMap;
            double fromX = mapWidth * SpawnFromX, fromY = -off;
            double toX = mapWidth + off, toY = mapHeight * SpawnToY;
            double spawnX = fromX + (toX - fromX) * along;
            double spawnY = fromY + (toY - fromY) * along;
            double bow = mapHeight * BowMax * (rng.NextDouble() * 2 - 1);
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
            double rest = 1 - t;
            return 1 - rest * rest * rest;
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

        /// <summary>A blob's alpha this instant: 0 before it starts, rising to
        /// <see cref="BlobAlpha"/> over the first part of its journey, and held there.</summary>
        public static float Alpha(Blob blob, int elapsedMs)
        {
            if (elapsedMs <= blob.StartMs) return 0f;
            double ramp = (blob.ArriveMs - blob.StartMs) * AlphaRampShare;
            double t = ramp <= 0 ? 1 : Math.Min(1, (elapsedMs - blob.StartMs) / ramp);
            return BlobAlpha * (float)SmoothStep(t);
        }

        /// <summary>The full-map dim this instant: eases in with the cloud, from the first blob to
        /// the farm settling, and holds.</summary>
        public static float Dim(int elapsedMs)
        {
            double t = (elapsedMs - PourAtMs) / (double)(SettledAtMs - PourAtMs);
            return DimAlpha * (float)SmoothStep(t);
        }

        /// <summary>A rectangle in the world map's own art pixels, as <c>Data/WorldMap</c> writes
        /// one, scaled to map pixels (times four, as <c>MapRegion</c> does).</summary>
        public static (int X, int Y, int Width, int Height) ArtToMap(int x, int y, int width, int height, int artScale)
            => (x * artScale, y * artScale, width * artScale, height * artScale);

        /// <summary>A whole-number scale is kept when it fills at least this share of the fractional
        /// fit, so the art stays on a clean pixel grid; below it the fractional fit is used so the
        /// map still fills the screen.</summary>
        public const double IntegerFillShare = 0.9;

        /// <summary>How the map fills a screen <paramref name="viewWidth"/> by
        /// <paramref name="viewHeight"/> paint pixels (Jeff, 2026-10-07: the map tab's own fit left it
        /// tiny on a large screen). The largest scale at which the whole map fits, aspect kept and
        /// centred on whole pixels; a whole number of screen pixels per map ART pixel when that still
        /// fills well. <paramref name="mapWidth"/> and <paramref name="mapHeight"/> are in map pixels
        /// (art times <paramref name="artScale"/>). Returns the scale from map pixels to paint
        /// pixels and the map's top left corner on screen.</summary>
        public static (double Scale, int OriginX, int OriginY) Fit(int viewWidth, int viewHeight, int mapWidth, int mapHeight, int artScale)
        {
            if (mapWidth <= 0 || mapHeight <= 0 || artScale <= 0) throw new ArgumentOutOfRangeException(nameof(mapWidth));
            double artWidth = mapWidth / (double)artScale, artHeight = mapHeight / (double)artScale;
            double fit = Math.Min(viewWidth / artWidth, viewHeight / artHeight);
            double whole = Math.Floor(fit);
            double perArt = whole >= 1 && whole / fit >= IntegerFillShare ? whole : fit;
            int originX = (int)Math.Floor((viewWidth - artWidth * perArt) / 2);
            int originY = (int)Math.Floor((viewHeight - artHeight * perArt) / 2);
            return (perArt / artScale, originX, originY);
        }

        /// <summary>A rectangle in map pixels to the screen space the scene paints in: scaled by the
        /// fit's <paramref name="scale"/> and offset by the map's top left corner on screen, with
        /// both edges rounded so neighbouring rectangles share an edge.</summary>
        public static (int X, int Y, int Width, int Height) MapToPaint(int x, int y, int width, int height, int originX, int originY, double scale)
        {
            int left = originX + (int)Math.Round(x * scale);
            int top = originY + (int)Math.Round(y * scale);
            int right = originX + (int)Math.Round((x + width) * scale);
            int bottom = originY + (int)Math.Round((y + height) * scale);
            return (left, top, right - left, bottom - top);
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
