using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Characters;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The first half of the rewind cutscene, beats 1-9: the Junimos appear around the
    /// sleeping farmer, the room's own lights go out, the darkness closes in, and their light flares
    /// white as they spend everything they have.
    ///
    /// Drawn entirely by us, like <see cref="Day28CutsceneMenu"/> and for the same reason (see that
    /// class's comment): a menu draws the already-rendered world, our screen-space darkness/white
    /// overlay, and the dialogue box in one ordered pass, where a vanilla Event's
    /// <c>fade</c>/<c>globalFade</c>/<c>RenderedWorld</c> trio fought each other on this exact frame in
    /// playtest.
    ///
    /// The Junimo actors, their hand-driven idle animation, their teardown, the
    /// <c>Display.MenuChanged</c> steal watch, the speech-box input forwarding and the single-shot
    /// finish all live in <see cref="RewindJunimoScene"/>, shared with
    /// <see cref="RewindMorningScene"/>. What is this scene's own is the three dials it adds on top:
    /// the room's lights going out and staying out, each Junimo's light radius, and the screen-space
    /// overlay that carries black to white.
    ///
    /// Non-skippable for the player: <see cref="RewindJunimoScene.readyToClose"/> is always false and
    /// cancel/ESC are ignored (forwarding a cancel press to the open dialogue box only advances its
    /// page). <see cref="RewindJunimoScene.SkipToEnd"/> is a debug/automation entry point, not a
    /// player one.
    ///
    /// This scene exposes nothing for a driver to read afterward beyond its completion callback:
    /// <c>RunController.ShowHoldChoice</c> already runs from <c>OnCutsceneEnded</c>, which this
    /// scene's completion precedes, so there is no hold-or-reshuffle question here to answer or
    /// store.</summary>
    internal sealed class RewindBedroomScene : RewindJunimoScene
    {
        /// <summary>Phase order. Each advances on a timer except a Say phase, which waits for its
        /// dialogue box to close.</summary>
        private enum Phase { LightsOut, JunimosIn, Say1, Say2, DarknessIn, Say3, Say4, White, Done }

        // Beat timings. Phases without a listed constant (the four Say phases) advance when their
        // dialogue box closes instead of on a clock.
        private const float LightsOutHoldMs = 600f;
        private const float JunimosInHoldMs = 1000f;
        private const float DarknessInMs = 2500f;
        private const float WhiteMs = 2200f;

        // Beat 2/6/9's light dials. sconceLight is a small round light, the same texture index used
        // for ordinary room lights elsewhere in the game.
        private const int JunimoLightTexture = StardewValley.LightSource.sconceLight;
        private const float JunimoLightRadiusStart = 2f;
        private const float JunimoLightRadiusFloor = 0.5f;
        // "Past the screen size" per the brief: large enough that the light's own falloff blows out
        // every pixel long before the geometric radius is reached.
        private const float JunimoLightRadiusFlash = 40f;

        // Beat 6/9's screen-space overlay: darkness eases in to DarknessMaxAlpha (not fully opaque,
        // so the Junimos' own light should still be visible poking through it), then beat 9 carries
        // both the alpha and the colour the rest of the way to an opaque white flash.
        private const float DarknessMaxAlpha = 0.9f;

        private const string JunimoLightIdPrefix = "TlyRewindJunimoLight";

        protected override string JunimoNamePrefix => "TlyRewindJunimo";

        private readonly List<string> _junimoLightIds = new List<string>();
        private readonly List<LightSource> _junimoLights = new List<LightSource>();
        private readonly List<Color> _junimoBaseColours = new List<Color>();

        private Phase _phase;
        private float _phaseElapsed;
        private Color _overlayColor = Color.Black;
        private float _overlayAlpha;

        public RewindBedroomScene(Action onComplete)
            : base(onComplete)
        {
            EnterPhase(Phase.LightsOut);
        }

        private static float Ease(float t) => t * t * (3f - 2f * t);

        private void EnterPhase(Phase next)
        {
            _phase = next;
            _phaseElapsed = 0f;
            switch (next)
            {
                case Phase.LightsOut:
                    // Beat 2: the room is still daylit on Spring 1, only the glowing auras go (Jeff,
                    // 2026-09-11). Nothing is re-added afterward, StripForeignLights keeps it that
                    // way every tick from here on.
                    Game1.currentLightSources.Clear();
                    break;
                case Phase.JunimosIn:
                    SpawnJunimos();
                    break;
                case Phase.Say1:
                    OpenBox(Strings.Get("cutscene.rewind.junimo-1"));
                    break;
                case Phase.Say2:
                    OpenBox(Strings.Get("cutscene.rewind.junimo-2"));
                    break;
                case Phase.Say3:
                    OpenBox(Strings.Get("cutscene.rewind.junimo-3"));
                    break;
                case Phase.Say4:
                    OpenBox(Strings.Get("cutscene.rewind.junimo-4"));
                    break;
                case Phase.DarknessIn:
                case Phase.White:
                case Phase.Done:
                    break;
            }
        }

        /// <summary>This scene's addition to a spawned actor: its own small light aura, which beats 6
        /// and 9 then shrink and flare. The morning beat deliberately adds none.</summary>
        protected override void OnJunimoSpawned(int index, Junimo junimo, Vector2 worldPos, Color colour)
        {
            string lightId = JunimoLightIdPrefix + index;
            var light = new LightSource(lightId, JunimoLightTexture, worldPos, JunimoLightRadiusStart, colour);
            Game1.currentLightSources[lightId] = light;
            _junimoLightIds.Add(lightId);
            _junimoLights.Add(light);
            _junimoBaseColours.Add(colour);
        }

        /// <summary>The lights go with the actors. Runs inside the base teardown's once-only guard.</summary>
        protected override void TeardownSceneExtras()
        {
            foreach (string lightId in _junimoLightIds)
                Game1.currentLightSources.Remove(lightId);
            _junimoLightIds.Clear();
            _junimoLights.Clear();
            _junimoBaseColours.Clear();
        }

        private void OpenBox(params string[] lines)
        {
            string playerName = Game1.player?.Name ?? string.Empty;
            var pages = new List<string>();
            foreach (string line in lines)
                pages.Add(line.Replace("@", playerName));
            ActiveBox = new EndingSpeechBox(Portrait, pages);
        }

        protected override void OnBoxClosed()
        {
            switch (_phase)
            {
                case Phase.Say1: EnterPhase(Phase.Say2); break;
                case Phase.Say2: EnterPhase(Phase.DarknessIn); break;
                case Phase.Say3: EnterPhase(Phase.Say4); break;
                case Phase.Say4: EnterPhase(Phase.White); break;
            }
        }

        protected override void OnFinishing() => _phase = Phase.Done;

        private void StripForeignLights()
        {
            List<string> foreign = null;
            foreach (string key in Game1.currentLightSources.Keys)
            {
                if (_junimoLightIds.Contains(key)) continue;
                (foreign ??= new List<string>()).Add(key);
            }
            if (foreign == null) return;
            foreach (string key in foreign)
                Game1.currentLightSources.Remove(key);
        }

        private void ApplyDarkness(float t)
        {
            float eased = Ease(MathHelper.Clamp(t, 0f, 1f));
            _overlayColor = Color.Black;
            _overlayAlpha = eased * DarknessMaxAlpha;
            float radius = MathHelper.Lerp(JunimoLightRadiusStart, JunimoLightRadiusFloor, eased);
            foreach (LightSource light in _junimoLights)
                light.radius.Value = radius;
        }

        private void ApplyWhite(float t)
        {
            float eased = Ease(MathHelper.Clamp(t, 0f, 1f));
            _overlayColor = Color.Lerp(Color.Black, Color.White, eased);
            _overlayAlpha = MathHelper.Lerp(DarknessMaxAlpha, 1f, eased);
            float radius = MathHelper.Lerp(JunimoLightRadiusFloor, JunimoLightRadiusFlash, eased);
            for (int i = 0; i < _junimoLights.Count; i++)
            {
                _junimoLights[i].radius.Value = radius;
                _junimoLights[i].color.Value = Color.Lerp(_junimoBaseColours[i], Color.White, eased);
            }
        }

        public override void update(GameTime time)
        {
            base.update(time);   // keeps the Junimos bobbing, including once the scene is done
            if (Completed) return;

            if (ActiveBox != null)
            {
                ActiveBox.update(time);
                return;
            }

            _phaseElapsed += (float)time.ElapsedGameTime.TotalMilliseconds;

            switch (_phase)
            {
                case Phase.LightsOut:
                    StripForeignLights();
                    if (_phaseElapsed >= LightsOutHoldMs) EnterPhase(Phase.JunimosIn);
                    break;
                case Phase.JunimosIn:
                    StripForeignLights();
                    if (_phaseElapsed >= JunimosInHoldMs) EnterPhase(Phase.Say1);
                    break;
                case Phase.DarknessIn:
                    StripForeignLights();
                    ApplyDarkness(_phaseElapsed / DarknessInMs);
                    if (_phaseElapsed >= DarknessInMs) EnterPhase(Phase.Say3);
                    break;
                case Phase.White:
                    StripForeignLights();
                    ApplyWhite(_phaseElapsed / WhiteMs);
                    if (_phaseElapsed >= WhiteMs) Finish();
                    break;
                case Phase.Say1:
                case Phase.Say2:
                case Phase.Say3:
                case Phase.Say4:
                case Phase.Done:
                    break;
            }
        }

        /// <summary>Skipping the bedroom lands on the beat-9 white rather than on a half-dark room,
        /// so the hand-off into the Town pan looks the same as a watched run. The overlay is left
        /// opaque white; the pan takes the frame from here.</summary>
        public override void SkipToEnd()
        {
            ApplyWhite(1f);
            base.SkipToEnd();
        }

        public override void draw(SpriteBatch b)
        {
            if (_overlayAlpha > 0f)
            {
                int w = Game1.uiViewport.Width, h = Game1.uiViewport.Height;
                b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, w, h), _overlayColor * _overlayAlpha);
            }
            base.draw(b);   // the speech box on top
        }
    }
}
