using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Netcode;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>The first half of the rewind cutscene: the Junimos appear around the sleeping farmer,
    /// the room's own lights go out, the darkness closes in, and their light flares white as they
    /// spend everything they have. Drawn entirely by us, like <see cref="Day28CutsceneMenu"/> and for
    /// the same reason (see that class's comment): a menu draws the already-rendered world, our
    /// screen-space darkness/white overlay, and the dialogue box in one ordered pass, where a vanilla
    /// Event's <c>fade</c>/<c>globalFade</c>/<c>RenderedWorld</c> trio fought each other on this exact
    /// frame in playtest. Non-skippable: <see cref="readyToClose"/> is always false and cancel/ESC are
    /// ignored (forwarding a cancel press to the open dialogue box only advances its page, not the
    /// scene). The Junimos themselves are real <see cref="Junimo"/> actors added to the current
    /// location's own character list, so the game's normal per-location update and world-space draw
    /// pass animates and positions them correctly under the camera transform; this menu never draws
    /// them itself, only the overlay and the dialogue on top.
    ///
    /// This class only builds the scene. Nothing opens it yet (a later task wires it into the day-28
    /// driver in place of <see cref="Day28CutsceneMenu"/> for the fail branch), and it exposes nothing
    /// for a driver to read afterward beyond <paramref name="onComplete"/>: <c>RunController.ShowHoldChoice</c>
    /// already runs from <c>OnCutsceneEnded</c>, which this scene's completion precedes, so there is no
    /// hold-or-reshuffle question here to answer or store.</summary>
    internal sealed class RewindBedroomScene : IClickableMenu
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

        // Junimo seating: four stations around the bed, one palette colour each (JunimoPalette has
        // six; this scene only needs four).
        private static readonly Point[] JunimoOffsets =
        {
            new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1),
        };
        private const string JunimoNamePrefix = "TlyRewindJunimo";
        private const string JunimoDisplayName = "Junimo";

        // Beat 2/6/9's light dials. sconceLight is a small round light, the same texture index used
        // for ordinary room lights elsewhere in the game.
        private const int JunimoLightTexture = StardewValley.LightSource.sconceLight;
        private const float JunimoLightRadiusStart = 2f;
        private const float JunimoLightRadiusFloor = 0.5f;
        // "Past the screen size" per the brief: large enough that the light's own falloff blows out
        // every pixel long before the geometric radius is reached.
        private const float JunimoLightRadiusFlash = 40f;

        // Beat 6/9's screen-space overlay: darkness eases in to DarknessMaxAlpha (not fully opaque —
        // the Junimos' own light should still be visible poking through it), then beat 9 carries both
        // the alpha and the colour the rest of the way to an opaque white flash.
        private const float DarknessMaxAlpha = 0.9f;

        private static readonly FieldInfo JunimoColourField = typeof(Junimo).GetField(
            "color", BindingFlags.Instance | BindingFlags.NonPublic);

        private const string JunimoLightIdPrefix = "TlyRewindJunimoLight";

        private readonly Action _onComplete;
        private readonly Texture2D _portrait;
        private readonly List<Junimo> _junimos = new List<Junimo>();
        private readonly List<string> _junimoLightIds = new List<string>();
        private readonly List<LightSource> _junimoLights = new List<LightSource>();
        private readonly List<Color> _junimoBaseColours = new List<Color>();

        private Phase _phase;
        private float _phaseElapsed;
        private EndingSpeechBox _activeBox;
        private Color _overlayColor = Color.Black;
        private float _overlayAlpha;
        private bool _done;

        public RewindBedroomScene(Action onComplete)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, showUpperRightCloseButton: false)
        {
            _onComplete = onComplete;

            try { _portrait = Game1.content.Load<Texture2D>("Portraits/Junimo0"); }
            catch (Exception) { _portrait = null; }

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
                    // 2026-09-11). Nothing is re-added afterward — StripForeignLights keeps it that way
                    // every tick from here on.
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
                    // The Junimos' last two lines run as one box's two pages: the reset, then the
                    // send-off, both spoken in the same breath as they spend the last of their power.
                    OpenBox(Strings.Get("cutscene.rewind.junimo-4"), Strings.Get("cutscene.rewind.morning"));
                    break;
                case Phase.DarknessIn:
                case Phase.White:
                case Phase.Done:
                    break;
            }
        }

        private void SpawnJunimos()
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null) return;
            Point playerTile = Game1.player.TilePoint;

            for (int i = 0; i < JunimoOffsets.Length; i++)
            {
                Vector2 tilePos = new Vector2(playerTile.X + JunimoOffsets[i].X, playerTile.Y + JunimoOffsets[i].Y);
                Vector2 worldPos = tilePos * 64f;

                var junimo = new Junimo(worldPos, -1, temporary: true)
                {
                    Name = JunimoNamePrefix + i,
                    displayName = JunimoDisplayName,
                    EventActor = true,
                    currentLocation = loc,
                };
                junimo.stayPut.Value = true;
                Color colour = JunimoPalette.Get(i);
                if (JunimoColourField?.GetValue(junimo) is NetColor net)
                    net.Value = colour;
                loc.characters.Add(junimo);
                _junimos.Add(junimo);

                string lightId = JunimoLightIdPrefix + i;
                var light = new LightSource(lightId, JunimoLightTexture, worldPos, JunimoLightRadiusStart, colour);
                Game1.currentLightSources[lightId] = light;
                _junimoLightIds.Add(lightId);
                _junimoLights.Add(light);
                _junimoBaseColours.Add(colour);
            }
        }

        private void OpenBox(params string[] lines)
        {
            string playerName = Game1.player?.Name ?? string.Empty;
            var pages = new List<string>();
            foreach (string line in lines)
                pages.Add(line.Replace("@", playerName));
            _activeBox = new EndingSpeechBox(_portrait, pages);
        }

        /// <summary>Forwards player input to the open dialogue box, which is a plain object here (not
        /// the active menu) so this scene keeps ticking behind it. <see cref="EndingSpeechBox"/> ends
        /// itself by calling <c>Game1.exitActiveMenu()</c> on its last page, which — since it isn't
        /// actually the active menu, we are — just clears the static field; we notice that happen in
        /// the same call and put ourselves straight back, all before the game gets another frame.</summary>
        private void ForwardToBox(Action<EndingSpeechBox> invoke)
        {
            if (_activeBox == null) return;
            bool wasActive = ReferenceEquals(Game1.activeClickableMenu, this);
            invoke(_activeBox);
            if (!wasActive || ReferenceEquals(Game1.activeClickableMenu, this)) return;

            Game1.activeClickableMenu = this;
            _activeBox = null;
            AdvanceAfterBox();
        }

        private void AdvanceAfterBox()
        {
            switch (_phase)
            {
                case Phase.Say1: EnterPhase(Phase.Say2); break;
                case Phase.Say2: EnterPhase(Phase.DarknessIn); break;
                case Phase.Say3: EnterPhase(Phase.Say4); break;
                case Phase.Say4: EnterPhase(Phase.White); break;
            }
        }

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
            base.update(time);
            if (_done) return;

            if (_activeBox != null)
            {
                _activeBox.update(time);
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

        private void Finish()
        {
            if (_done) return;
            _done = true;
            _phase = Phase.Done;

            GameLocation loc = Game1.currentLocation;
            foreach (Junimo j in _junimos)
                loc?.characters.Remove(j);
            foreach (string lightId in _junimoLightIds)
                Game1.currentLightSources.Remove(lightId);
            _junimos.Clear();
            _junimoLightIds.Clear();
            _junimoLights.Clear();
            _junimoBaseColours.Clear();

            if (ReferenceEquals(Game1.activeClickableMenu, this))
                Game1.activeClickableMenu = null;
            _onComplete?.Invoke();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
            => ForwardToBox(b => b.receiveLeftClick(x, y, playSound));

        public override void receiveRightClick(int x, int y, bool playSound = true)
            => ForwardToBox(b => b.receiveRightClick(x, y, playSound));

        public override void receiveKeyPress(Keys key)
            => ForwardToBox(b => b.receiveKeyPress(key));

        public override void receiveGamePadButton(Buttons b)
            => ForwardToBox(box => box.receiveGamePadButton(b));

        // Forced scene: never satisfy the engine's close paths (ESC / controller-B). Forwarding those
        // presses to the open dialogue box (above) only advances its page, never closes this scene.
        public override bool readyToClose() => false;

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            width = Game1.uiViewport.Width;
            height = Game1.uiViewport.Height;
        }

        public override void draw(SpriteBatch b)
        {
            if (_overlayAlpha > 0f)
            {
                int w = Game1.uiViewport.Width, h = Game1.uiViewport.Height;
                b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, w, h), _overlayColor * _overlayAlpha);
            }
            _activeBox?.draw(b);
        }
    }
}
