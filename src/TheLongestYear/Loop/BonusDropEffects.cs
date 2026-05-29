using Microsoft.Xna.Framework;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Plays the audio + particle cue that fires when a bonus drop (forage_yield_up,
    /// mine_drops_up, all_drops_up) actually grants an extra +1 item. Added 2026-05-29
    /// per playtest: user could not tell whether the bonus rolls were firing — they need
    /// a visible/audible confirmation each time, not just a log line.
    ///
    /// Effect choice:
    ///   - Sound: <c>"yoba"</c> — the same mystical chime vanilla uses for Junimo / Yoba
    ///     bonus moments. Reads as "Junimo blessing fired" without sounding like a UI ping.
    ///   - Sprite: <see cref="Utility.sparkleWithinArea"/> over the drop tile, 5 particles,
    ///     white. Sits on top of the spawned drop for a moment, fades naturally.
    /// </summary>
    internal static class BonusDropEffects
    {
        /// <summary>Spark + chime at a tile-coord drop site on the current location.</summary>
        public static void Play(GameLocation location, int tileX, int tileY)
        {
            if (location == null) return;

            // 32×32 burst centred on the tile (offset 16 to centre the 32-wide region inside
            // the 64-wide tile). 5 sparkles, white, default lifetime / fade.
            var area = new Microsoft.Xna.Framework.Rectangle(
                tileX * 64 + 16, tileY * 64 + 16, 32, 32);
            location.temporarySprites.AddRange(
                Utility.sparkleWithinArea(area, 5, Color.White));

            Game1.playSound("yoba");
        }
    }
}
