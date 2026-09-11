using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using StardewValley.Characters;
using TheLongestYear.Core;
using TheLongestYear.Integration;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.UI
{
    /// <summary>The first half of the rewind cutscene, beats 1-9: the Junimos appear around the
    /// sleeping farmer, the room's own lights go out, the darkness closes in, and their light flares
    /// white as they spend everything they have.
    ///
    /// Drawn entirely by us, like <see cref="Day28CutsceneMenu"/> and for the same reason (see that
    /// class's comment): a menu draws the already-rendered world and the dialogue box in one ordered
    /// pass, where a vanilla Event's <c>fade</c>/<c>globalFade</c>/<c>RenderedWorld</c> trio fought
    /// each other on this exact frame in playtest.
    ///
    /// The Junimo actors, their hand-driven idle animation, the sleeping farmer, their teardown, the
    /// <c>Display.MenuChanged</c> steal watch, the speech-box input forwarding and the single-shot
    /// finish all live in <see cref="RewindJunimoScene"/>, shared with
    /// <see cref="RewindMorningScene"/>. What is this scene's own is the three dials it adds on top:
    /// the failed night painted over the HUD, the room's lighting, and the white flash that ends it.
    ///
    /// THE DARKNESS IS THE ENGINE'S OWN LIGHTING, not an overlay (reworked 2026-09-11 after the first
    /// playtest: a flat translucent sheet over the finished frame dimmed the HUD along with the
    /// world, produced no light pools, and read as someone turning the brightness down).
    /// <see cref="RewindNightLight"/> owns the mechanism and documents it; this scene owns the two
    /// dials that move along it, the ambient colour and the Junimos' light radii, and the light
    /// colours that make each pool read as that Junimo's palette entry.
    ///
    /// THE WHITE FLASH keeps a screen-space overlay, and only the white flash does. The engine's
    /// lighting pass is subtractive: it can take the room down to black and give it back, but it can
    /// never push a pixel past the brightness the world was already drawn at, so the radii growing
    /// past the screen open the room to full brightness and the overlay carries it the rest of the
    /// way to white. The overlay is opaque by the last frame, which is what the Town pan takes the
    /// frame from.
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
        private const float JunimoLightRadiusStart = 2.5f;
        private const float JunimoLightRadiusFloor = 0.75f;
        // "Past the screen size" per the brief: large enough that the light's own falloff covers
        // every pixel long before the geometric radius is reached.
        private const float JunimoLightRadiusFlash = 40f;

        // How far a pool leans toward its Junimo's palette colour. Gentle on purpose: at 1 a pool is
        // a flat colour wash rather than a lit patch of floor. See RewindNightLight.PoolTint.
        private const float JunimoPoolTintStrength = 0.35f;

        private const string JunimoLightIdPrefix = "TlyRewindJunimoLight";

        // Which of the six Junimos speaks each line, in order, the way the ending's hall scene passes
        // its lines around the circle. Index i is both the palette colour of that actor's sprite and
        // the tint of the Portraits/Junimo<i> asset the speech box shows, so the portrait always
        // matches the Junimo the line came from.
        private static readonly int[] SpeakerOrder = { 0, 1, 2, 3 };

        protected override string JunimoNamePrefix => "TlyRewindJunimo";

        private readonly List<string> _junimoLightIds = new List<string>();
        private readonly List<LightSource> _junimoLights = new List<LightSource>();
        private readonly List<Color> _junimoBaseColours = new List<Color>();

        private Phase _phase;
        private float _phaseElapsed;
        private float _flashAlpha;

        public RewindBedroomScene(CoreSeason failed, Action onComplete)
            : base(onComplete)
        {
            // Beat 1: the HUD is still on the morning after the failed day (a Spring 28 failure
            // reads "Summer 1, 6:00am" over a scene that is the night the year ran out). Paint the
            // failed night over it before anything else is on screen.
            RewindNightPaint.Apply(failed);
            RewindNightLight.Begin(_junimoLightIds);
            // The mod's JP box is not part of a dream. Vanilla's clock and date ARE, for this beat
            // (the failed night is what the paint above is for), and they hide themselves for the
            // pan, which freezes controls. RewindBlackout owns both from the morning beat on.
            RewindBlackout.SuppressHud();
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
                    // Beat 2: the farmhouse's own lamps and fireplace go, and RewindNightLight keeps
                    // them (and anything the farmer carries) out every tick from here on.
                    Game1.currentLightSources.Clear();
                    RewindNightLight.Ambient = RewindNightLight.NightAmbient;
                    break;
                case Phase.JunimosIn:
                    SpawnJunimos();
                    break;
                case Phase.Say1:
                    OpenBox(0, Strings.Get("cutscene.rewind.junimo-1"));
                    break;
                case Phase.Say2:
                    OpenBox(1, Strings.Get("cutscene.rewind.junimo-2"));
                    break;
                case Phase.Say3:
                    OpenBox(2, Strings.Get("cutscene.rewind.junimo-3"));
                    break;
                case Phase.Say4:
                    OpenBox(3, Strings.Get("cutscene.rewind.junimo-4"));
                    break;
                case Phase.DarknessIn:
                case Phase.White:
                case Phase.Done:
                    break;
            }
        }

        /// <summary>This scene's addition to a spawned actor: its own small light aura, which beats 6
        /// and 9 then shrink and flare. The morning beat deliberately adds none.
        ///
        /// The light's colour is NOT the palette colour. The lightmap is subtracted from the world,
        /// so a light's colour is the colour it removes, and handing a Junimo's green straight to its
        /// light would carve a magenta hole rather than a green pool. <c>PoolTint</c> converts it.</summary>
        protected override void OnJunimoSpawned(int index, Junimo junimo, Vector2 worldPos, Color colour)
        {
            string lightId = JunimoLightIdPrefix + index;
            Color lightColour = RewindNightLight.PoolTint(colour, JunimoPoolTintStrength);
            var light = new LightSource(lightId, JunimoLightTexture, worldPos, JunimoLightRadiusStart, lightColour);
            Game1.currentLightSources[lightId] = light;
            _junimoLightIds.Add(lightId);
            _junimoLights.Add(light);
            _junimoBaseColours.Add(lightColour);
        }

        /// <summary>The lights and the two per-tick holds go with the actors. Runs inside the base
        /// teardown's once-only guard, so it never runs twice, and it is reached by the menu-steal
        /// path as well as by normal completion: without that, a stolen frame would leave the room
        /// black and the calendar stuck on the failed night.</summary>
        protected override void TeardownSceneExtras()
        {
            foreach (string lightId in _junimoLightIds)
                Game1.currentLightSources.Remove(lightId);
            _junimoLightIds.Clear();
            _junimoLights.Clear();
            _junimoBaseColours.Clear();
            RewindNightLight.Release();
            // The pan takes the clock and the calendar over from here (Day28CutsceneDriver chains
            // them), so this hands them on rather than restoring anything.
            RewindNightPaint.Release();
        }

        private void OpenBox(int speaker, params string[] lines)
        {
            string playerName = Game1.player?.Name ?? string.Empty;
            var pages = new List<string>();
            foreach (string line in lines)
                pages.Add(line.Replace("@", playerName));
            ActiveBox = new EndingSpeechBox(PortraitFor(SpeakerOrder[speaker % SpeakerOrder.Length]), pages);
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

        /// <summary>Beat 6: the ambient deepens toward black and every pool shrinks, so the visible
        /// world closes to a few small circles around the bed.</summary>
        private void ApplyDarkness(float t)
        {
            float eased = Ease(MathHelper.Clamp(t, 0f, 1f));
            RewindNightLight.Ambient = Color.Lerp(
                RewindNightLight.NightAmbient, RewindNightLight.DeepAmbient, eased);
            float radius = MathHelper.Lerp(JunimoLightRadiusStart, JunimoLightRadiusFloor, eased);
            foreach (LightSource light in _junimoLights)
                light.radius.Value = radius;
        }

        /// <summary>Beat 9: the same dials reversed. The radii grow past the size of the screen and
        /// every light goes fully neutral (black subtracts nothing), which opens the room back to
        /// full brightness; the ambient follows them down to black so no corner is left dark; and the
        /// overlay carries the last stretch to white, which subtractive lighting cannot do on its
        /// own.</summary>
        private void ApplyWhite(float t)
        {
            float eased = Ease(MathHelper.Clamp(t, 0f, 1f));
            RewindNightLight.Ambient = Color.Lerp(RewindNightLight.DeepAmbient, Color.Black, eased);
            float radius = MathHelper.Lerp(JunimoLightRadiusFloor, JunimoLightRadiusFlash, eased);
            for (int i = 0; i < _junimoLights.Count; i++)
            {
                _junimoLights[i].radius.Value = radius;
                _junimoLights[i].color.Value = Color.Lerp(_junimoBaseColours[i], Color.Black, eased);
            }
            _flashAlpha = eased;
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
                    if (_phaseElapsed >= LightsOutHoldMs) EnterPhase(Phase.JunimosIn);
                    break;
                case Phase.JunimosIn:
                    if (_phaseElapsed >= JunimosInHoldMs) EnterPhase(Phase.Say1);
                    break;
                case Phase.DarknessIn:
                    ApplyDarkness(_phaseElapsed / DarknessInMs);
                    if (_phaseElapsed >= DarknessInMs) EnterPhase(Phase.Say3);
                    break;
                case Phase.White:
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
            if (_flashAlpha > 0f)
            {
                int w = Game1.uiViewport.Width, h = Game1.uiViewport.Height;
                b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, w, h), Color.White * _flashAlpha);
            }
            base.draw(b);   // the speech box on top
        }
    }
}
