using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;

namespace TheLongestYear.Integration
{
    /// <summary>The bad ending's draw-side pieces: the Joja sign over the kept farmhouse's door, and
    /// the Harmony patches that hide the farmhouse and its mail flag while the scene (the razed-house
    /// version) has torn the house down.</summary>
    internal static partial class JojaBadEndingCommands
    {
        public const string JojaSignName = "tlyJojaSign";

        // Vanilla's own Joja sign: the blue "Joja" board hanging on the warehouse facade it draws over
        // the Community Center on the Joja route (Town.jojaFacadeTop, Cursors 424,1275 174x50; the
        // board is its 52 x 20 at 544,1292). Drawn in three pieces so the facade wall behind the
        // board's rounded corners stays out: the board less its side columns, then each side column
        // less its corner pixels. Offsets are source pixels from the board's top-left.
        private const string SignTexture = "LooseSprites\\Cursors";
        private static readonly (Rectangle Source, Point Offset)[] SignPieces =
        {
            (new Rectangle(545, 1292, 50, 20), new Point(1, 0)),
            (new Rectangle(544, 1293, 1, 18), new Point(0, 1)),
            (new Rectangle(595, 1293, 1, 18), new Point(51, 1)),
        };
        // From the entry tile's top-left (world px): centred on the door tile, its bottom 136 px up,
        // just over the door frame on every upgrade level (tile notes in JojaBadEnding.cs).
        private static readonly Vector2 SignOffset = new(-72f, -216f);
        private const float SignAboveHouse = 0.0001f;   // vanilla's step for the Gold Clock's hands over its building

        /// <summary>tlyJojaSign &lt;x&gt; &lt;y&gt; [delayMs]: the sign as temporary sprites on the current
        /// location (the farm), just in front of the farmhouse; in memory only, removed by Cleanup.</summary>
        private static void JojaSign(Event evt, string[] args, EventContext context)
        {
            if (!ArgUtility.TryGetInt(args, 1, out int x, out string error)
                || !ArgUtility.TryGetInt(args, 2, out int y, out error)
                || !ArgUtility.TryGetOptionalInt(args, 3, out int delay, out error))
            {
                Skip(evt, JojaSignName, error);
                return;
            }
            Guarded(evt, JojaSignName, () =>
            {
                Building house = (Game1.currentLocation as Farm)?.GetMainFarmHouse();
                // Building.draw's sort depth for the house, so the sign is drawn on its front.
                float houseDepth = house == null
                    ? (y + 2) * 64f / 10000f
                    : ((house.tileY.Value + house.tilesHigh.Value) * 64f - (house.GetData()?.SortTileOffset ?? 0f) * 64f) / 10000f;
                Vector2 topLeft = new Vector2(x * 64f, y * 64f) + SignOffset;
                foreach (var (source, offset) in SignPieces)
                {
                    var piece = new TemporaryAnimatedSprite(SignTexture, source, LongLife, 1, 0,
                        topLeft + new Vector2(offset.X, offset.Y) * SpriteScale, flicker: false, flipped: false,
                        houseDepth + SignAboveHouse, 0f, Color.White, SpriteScale, 0f, 0f, 0f)
                    {
                        delayBeforeAnimationStart = System.Math.Max(0, delay),
                    };
                    Add(piece);
                }
                _monitor.Log($"{JojaSignName}: Joja sign over the farmhouse door at {topLeft.X},{topLeft.Y} px (entry {x},{y}, shown after {delay} ms, "
                             + $"{(house == null ? "no farmhouse found" : $"farmhouse at {house.tileX.Value},{house.tileY.Value}")}; in memory).", LogLevel.Info);
            });
        }

        private static bool IsHiddenFarmhouse(Building building)
            => _farmhouseHidden && building != null && building == Game1.getFarm()?.GetMainFarmHouse();

        [HarmonyPatch(typeof(Building), nameof(Building.draw))]
        internal static class HideFarmhouseDraw
        {
            private static bool Prefix(Building __instance) => !IsHiddenFarmhouse(__instance);
        }

        /// <summary>The farm's new-mail flag floats over the mailbox; with the house gone it would
        /// hang in the air. While hidden, the mailbox is off the map.</summary>
        [HarmonyPatch(typeof(Farmer), nameof(Farmer.getMailboxPosition))]
        internal static class HideMailFlag
        {
            private static readonly Point OffMap = new(-100, -100);

            private static void Postfix(ref Point __result)
            {
                if (_farmhouseHidden) __result = OffMap;
            }
        }
    }
}
