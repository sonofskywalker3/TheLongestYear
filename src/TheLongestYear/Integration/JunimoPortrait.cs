using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>Serves a portrait for the Junimo speakers the ending and intro events place as
    /// temporary actors (Junimo, Junimo0..Junimo5).
    ///
    /// Live run 2026-09-06: with no Portraits/&lt;Name&gt; asset the game logged
    /// "NPC Junimo0 can't load portraits from 'Portraits/Junimo0'" on EVERY frame a Junimo speak box
    /// was open (3,951 lines in one run). DialogueBox.isPortraitBox reads NPC.Portrait each draw, and
    /// that getter re-runs ChooseAppearance -> TryLoadPortraits while the portrait is null, so the
    /// failed load is retried forever. Supplying the asset ends the retry loop.
    ///
    /// The portrait is generated from the game's own Characters/Junimo sheet, so no art ships in the
    /// mod: frame 0 scaled 3x and centred in the first 64x64 cell of a 128x128 portrait sheet, tinted the
    /// classic Junimo green (the vanilla sheet is a white silhouette that takes a tint).</summary>
    internal sealed class JunimoPortrait
    {
        private const string Prefix = "Portraits/Junimo";
        private const string SourceAsset = "Characters/Junimo";
        private const int PortraitSheet = 128, Cell = 64, Frame = 16, Scale = 3;
        // Six cells (2 wide, 3 high), every one the same face. A line tagged $h or $s asks for cell 1
        // or 2; with only cell 0 drawn those pages showed an empty portrait box (opening playthrough,
        // Jeff 2026-09-25: blank on every page that ended a $h or $s line).
        private const int SheetHeight = Cell * 3;
        // The Junimo is 16 px of real art, so filling the whole 64 px cell the way a hand-drawn
        // villager portrait does made it read as a wall of giant pixels in the speech box (Jeff,
        // 2026-09-10). It is drawn at 3x and centred in the cell instead, leaving a margin.
        private const int Drawn = Frame * Scale, Inset = (Cell - Drawn) / 2;
        // "Portraits/Junimo" (the intro's single Junimo) keeps the classic green; "Portraits/Junimo<i>"
        // takes the ending's palette entry i, the same colour tlyJunimo paints that actor's sprite.
        private static readonly Color JunimoGreen = new Color(110, 200, 74);

        private readonly IMonitor _monitor;
        private bool _logged;

        public JunimoPortrait(IMonitor monitor) => _monitor = monitor;

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!Matches(e.NameWithoutLocale.Name)) return;
            Color tint = TintFor(e.NameWithoutLocale.Name);
            e.LoadFrom(() => Build(tint), AssetLoadPriority.Medium);
        }

        private static Color TintFor(string name)
        {
            name = name.Replace('\\', '/');
            string suffix = name.Substring(Prefix.Length);
            return suffix.Length == 1 ? JunimoPalette.Get(suffix[0] - '0') : JunimoGreen;
        }

        /// <summary>"Portraits/Junimo" and "Portraits/Junimo0".."Portraits/Junimo5" only: exactly the
        /// actor names the intro and ending injectors place (the ending uses Junimo0..Junimo5 for the
        /// six in the hall, the intro a single "Junimo"). Anything else, including a real NPC whose
        /// name merely starts with "Junimo", keeps its own portrait asset.</summary>
        private static bool Matches(string name)
        {
            name = name.Replace('\\', '/');
            if (!name.StartsWith(Prefix, System.StringComparison.OrdinalIgnoreCase)) return false;
            string suffix = name.Substring(Prefix.Length);
            return suffix.Length == 0
                || (suffix.Length == 1 && suffix[0] >= '0' && suffix[0] <= '5');
        }

        private Texture2D Build(Color tint)
        {
            var pixels = new Color[PortraitSheet * SheetHeight];
            try
            {
                Texture2D source = Game1.content.Load<Texture2D>(SourceAsset);
                var src = new Color[source.Width * source.Height];
                source.GetData(src);
                for (int y = 0; y < Drawn; y++)
                {
                    for (int x = 0; x < Drawn; x++)
                    {
                        int sx = x / Scale, sy = y / Scale;
                        if (sx >= source.Width || sy >= source.Height || sx >= Frame || sy >= Frame) continue;
                        Color c = src[sy * source.Width + sx];
                        if (c.A == 0) continue;
                        var tinted = new Color(
                            (byte)(c.R * tint.R / 255),
                            (byte)(c.G * tint.G / 255),
                            (byte)(c.B * tint.B / 255),
                            c.A);
                        for (int cy = 0; cy < SheetHeight; cy += Cell)
                            for (int cx = 0; cx < PortraitSheet; cx += Cell)
                                pixels[(cy + y + Inset) * PortraitSheet + (cx + x + Inset)] = tinted;
                    }
                }
                if (!_logged)
                {
                    _monitor.Log("Junimo portrait: generated from Characters/Junimo frame 0.", LogLevel.Trace);
                    _logged = true;
                }
            }
            catch (System.Exception ex)
            {
                // A transparent sheet still ends the per-frame retry loop, which is the point.
                _monitor.Log($"Junimo portrait: could not build from {SourceAsset} ({ex.Message}); serving a blank portrait.", LogLevel.Warn);
                pixels = new Color[PortraitSheet * SheetHeight];
            }
            var result = new Texture2D(Game1.graphics.GraphicsDevice, PortraitSheet, SheetHeight);
            result.SetData(pixels);
            return result;
        }
    }
}
