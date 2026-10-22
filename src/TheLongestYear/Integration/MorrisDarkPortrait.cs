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
    /// Morris's eyes sit behind his glasses, so the glow goes on the lenses: the two cyan shades
    /// vanilla's portrait fills them with (read from Portraits/Morris, all four frames). The log
    /// says how many pixels it lit; a zero means another mod repainted the portrait.</summary>
    internal sealed class MorrisDarkPortrait
    {
        public const string AssetName = "Portraits/" + PortraitName;
        /// <summary>The name an event script passes to tlySay: a "/" would split the command.</summary>
        public const string PortraitName = "Morris_Dark";
        private const string SourceAsset = "Portraits/Morris";

        // The lens fill and its highlight in vanilla's Portraits/Morris. Anything that matches becomes the glow.
        private static readonly Color[] EyeColours =
        {
            new Color(115, 169, 188),
            new Color(103, 225, 219),
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
