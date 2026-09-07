using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>Year One Ending (spec 2026-09-06 §4 scene 4): Morris's sheet with the eyes recoloured
    /// red, built at load from the game's own Characters/Morris so no copyrighted sheet ships in the
    /// mod. The event command <c>changeSprite Morris Dark</c> loads Characters/Morris_Dark.</summary>
    internal sealed class MorrisDarkSprite
    {
        public const string AssetName = "Characters/Morris_Dark";
        private const string SourceAsset = "Characters/Morris";
        // Morris's iris colour on the vanilla sheet, confirmed offline against the game's own
        // Characters/Morris.xnb (frame 0, offsets (7,9) and (9,9) within the 16x32 down-facing idle
        // frame). Checked against every frame on the sheet: this colour appears only at eye-height
        // offsets across every facing/row, never in the outline, hair or suit, so a global replace
        // does not bleed.
        private static readonly Color EyeColour = new Color(147, 202, 219);
        private static readonly Color RedEye = new Color(220, 20, 20);
        private readonly IMonitor _monitor;

        public MorrisDarkSprite(IMonitor monitor) => _monitor = monitor;

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(AssetName)) return;
            e.LoadFrom(Build, AssetLoadPriority.Medium);
        }

        private Texture2D Build()
        {
            Texture2D source = Game1.content.Load<Texture2D>(SourceAsset);
            var pixels = new Color[source.Width * source.Height];
            source.GetData(pixels);
            int changed = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] == EyeColour) { pixels[i] = RedEye; changed++; }
            }
            var result = new Texture2D(Game1.graphics.GraphicsDevice, source.Width, source.Height);
            result.SetData(pixels);
            _monitor.Log($"Morris_Dark: recoloured {changed} pixel(s).", changed == 0 ? LogLevel.Warn : LogLevel.Trace);
            return result;
        }
    }
}
