using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>The speech-box face that goes with <see cref="MorrisDarkSprite"/>: Morris's own
    /// portrait, blacked down and given glowing red eyes, for his last line of the ending (Jeff,
    /// 2026-09-10). Built at load from the game's own Portraits/Morris, so no copyrighted art ships
    /// in the mod, and served as "Portraits/Morris_Dark" for tlySay's portrait override.
    ///
    /// The sprite sheet's iris colour is a known constant; the portrait is drawn at a different
    /// scale and may not use the same one, so this matches a small set of candidates and logs how
    /// many pixels it hit. A zero there means the eyes stayed dark and the constant needs the
    /// portrait's real colour.</summary>
    internal sealed class MorrisDarkPortrait
    {
        public const string AssetName = "Portraits/Morris_Dark";
        private const string SourceAsset = "Portraits/Morris";

        // The sprite sheet's iris colour, plus the two neighbouring shades a 64x64 portrait
        // typically uses for the same feature. Anything that matches becomes the glow.
        private static readonly Color[] EyeColours =
        {
            new Color(147, 202, 219),
            new Color(114, 171, 197),
            new Color(88, 141, 173),
        };
        private static readonly Color RedEye = new Color(255, 40, 30);
        private const float Shadow = 0.22f;   // darker than the sprite: the portrait is big enough to take it

        private readonly IMonitor _monitor;

        public MorrisDarkPortrait(IMonitor monitor) => _monitor = monitor;

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(AssetName)) return;
            e.LoadFrom(Build, AssetLoadPriority.Medium);
        }

        /// <summary>Never throws: this runs mid-cutscene, and a load failure would surface as a
        /// broken ending rather than a missing recolour. Mirrors MorrisDarkSprite.Build's guard.</summary>
        private Texture2D Build()
        {
            Texture2D source = null;
            try
            {
                source = Game1.content.Load<Texture2D>(SourceAsset);
                var pixels = new Color[source.Width * source.Height];
                source.GetData(pixels);
                int eyes = 0;
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color c = pixels[i];
                    if (c.A == 0) continue;
                    if (IsEye(c)) { pixels[i] = RedEye; eyes++; continue; }
                    pixels[i] = new Color((byte)(c.R * Shadow), (byte)(c.G * Shadow), (byte)(c.B * Shadow), c.A);
                }
                var result = new Texture2D(Game1.graphics.GraphicsDevice, source.Width, source.Height);
                result.SetData(pixels);
                _monitor.Log($"Morris_Dark portrait: {eyes} eye pixel(s) lit.", eyes == 0 ? LogLevel.Warn : LogLevel.Trace);
                return result;
            }
            catch (System.Exception ex)
            {
                _monitor.Log(
                    $"Morris_Dark portrait: could not build from {SourceAsset} ({ex.GetType().Name}: {ex.Message}); " +
                    "serving the ordinary portrait.", LogLevel.Warn);
                if (source != null) return source;
                var blank = new Texture2D(Game1.graphics.GraphicsDevice, 1, 1);
                blank.SetData(new[] { Color.Transparent });
                return blank;
            }
        }

        private static bool IsEye(Color c)
        {
            foreach (Color eye in EyeColours)
                if (c == eye) return true;
            return false;
        }
    }
}
