using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Characters;
using StardewValley.Menus;
using TheLongestYear.Core;
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>REQUIRED WIRING: ModEntry.Entry must call <see cref="Register"/> once, or the
    /// teardown safety net is dead code. See the remarks below for why.
    ///
    /// The first half of the rewind cutscene: the Junimos appear around the sleeping farmer,
    /// the room's own lights go out, the darkness closes in, and their light flares white as they
    /// spend everything they have. Drawn entirely by us, like <see cref="Day28CutsceneMenu"/> and for
    /// the same reason (see that class's comment): a menu draws the already-rendered world, our
    /// screen-space darkness/white overlay, and the dialogue box in one ordered pass, where a vanilla
    /// Event's <c>fade</c>/<c>globalFade</c>/<c>RenderedWorld</c> trio fought each other on this exact
    /// frame in playtest. Non-skippable: <see cref="readyToClose"/> is always false and cancel/ESC are
    /// ignored (forwarding a cancel press to the open dialogue box only advances its page, not the
    /// scene). The Junimos themselves are real <see cref="Junimo"/> actors added to the current
    /// location's own character list, so the game's normal world-space draw pass positions them
    /// correctly under the camera transform; this menu never draws them itself, only the overlay and
    /// the dialogue on top. Their own idle animation is NOT free from that, though: this scene's own
    /// <c>update</c> drives it directly every tick (see <see cref="AnimateJunimos"/>), because
    /// <see cref="Game1.shouldTimePass"/> is false for this scene's whole run and the game's own
    /// per-character update (where that animation would otherwise come from) never runs while that's
    /// false.
    ///
    /// This class only builds the scene. Nothing opens it yet (a later task wires it into the day-28
    /// driver in place of <see cref="Day28CutsceneMenu"/> for the fail branch), and it exposes nothing
    /// for a driver to read afterward beyond <paramref name="onComplete"/>: <c>RunController.ShowHoldChoice</c>
    /// already runs from <c>OnCutsceneEnded</c>, which this scene's completion precedes, so there is no
    /// hold-or-reshuffle question here to answer or store.
    ///
    /// Call <see cref="Register"/> once from ModEntry.Entry, the same way
    /// <c>EndingEventCommands.Register</c> and <c>TownRouteProbe.Register</c> are already called.
    /// This scene's own completion path (<see cref="Finish"/>) always cleans up its Junimos and their
    /// lights, but something else can steal <see cref="Game1.activeClickableMenu"/> out from under it
    /// before that runs (vanilla's own end-of-night menus after an overnight FarmEvent are the
    /// documented case, see <c>Day28CutsceneDriver</c>'s watchdog comment). <see cref="Register"/>
    /// wires a <c>Display.MenuChanged</c> watch that notices that and tears the world state down
    /// anyway. Without it, a stolen scene leaks its actors and lights into the save.</summary>
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

        // Beat 6/9's screen-space overlay: darkness eases in to DarknessMaxAlpha (not fully opaque,
        // so the Junimos' own light should still be visible poking through it), then beat 9 carries
        // both the alpha and the colour the rest of the way to an opaque white flash.
        private const float DarknessMaxAlpha = 0.9f;

        private static readonly FieldInfo JunimoColourField = typeof(Junimo).GetField(
            "color", BindingFlags.Instance | BindingFlags.NonPublic);

        private const string JunimoLightIdPrefix = "TlyRewindJunimoLight";

        // Set once from ModEntry.Entry (see the class comment). Null until then, in which case the
        // menu-steal safety net below is simply inert; the scene still runs correctly end to end on
        // its own, it just has no way to notice a steal without SMAPI's own event pump, which needs
        // a helper it cannot get through this class's fixed constructor.
        private static IModHelper _helper;

        public static void Register(IModHelper helper) => _helper = helper;

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
        // _completed guards the normal Finish() path (world teardown + onComplete, once).
        // _tornDown guards just the world teardown (Junimos + lights), which can also run on its own
        // if a steal is caught, without _completed ever becoming true or onComplete ever firing.
        private bool _completed;
        private bool _tornDown;
        private bool _menuWatchSubscribed;

        public RewindBedroomScene(Action onComplete)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, showUpperRightCloseButton: false)
        {
            _onComplete = onComplete;

            try { _portrait = Game1.content.Load<Texture2D>("Portraits/Junimo0"); }
            catch (Exception) { _portrait = null; }

            SubscribeMenuWatch();
            EnterPhase(Phase.LightsOut);
        }

        /// <summary>Watches for something else replacing this scene as the active menu (a steal, not
        /// this scene's own normal completion) and tears down the world state if that happens, since
        /// nothing else will: this scene's own <c>update</c>/<c>draw</c> only run while it IS the
        /// active menu, so it cannot notice or react to losing that slot on its own. SMAPI's event
        /// pump runs regardless, which is the whole reason this needs a helper reference at all.</summary>
        private void SubscribeMenuWatch()
        {
            if (_helper == null || _menuWatchSubscribed) return;
            _helper.Events.Display.MenuChanged += OnMenuChanged;
            _menuWatchSubscribed = true;
        }

        private void UnsubscribeMenuWatch()
        {
            if (!_menuWatchSubscribed) return;
            _helper.Events.Display.MenuChanged -= OnMenuChanged;
            _menuWatchSubscribed = false;
        }

        private void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (ReferenceEquals(Game1.activeClickableMenu, this)) return;
            // Not stranding (Day28CutsceneDriver's watchdog already covers that survivably), just the
            // leak: whatever replaced us, our Junimos and lights do not belong in the save any more.
            TeardownWorldState();
            UnsubscribeMenuWatch();
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

        private void SpawnJunimos()
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null || Game1.player == null) return;
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
        /// itself by calling <c>Game1.exitActiveMenu()</c> on its last page. Since it isn't actually
        /// the active menu (we are), that call just clears the static field; we notice that happen in
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

        // Idle-bob constants for StardewValley.Characters.Junimo's own Sprite (frame 8, 4 frames,
        // 100ms each): the same animation vanilla plays for a standing-still, non-temporary Junimo
        // (its update()'s final "motion is zero" branch, and its updateSlaveAnimation's matching idle
        // branch), the Community Center ending look the user asked for ("moving normally", not static).
        private const int JunimoIdleFrame = 8;
        private const int JunimoIdleFrameCount = 4;
        private const float JunimoIdleFrameMs = 100f;

        /// <summary>Drives the Junimos' idle animation ourselves, every tick this scene is active.
        /// <see cref="Game1.shouldTimePass"/> is false for this scene's whole run (any non-BobberBar
        /// activeClickableMenu forces it false), so <c>GameLocation.updateCharacters</c> never calls
        /// these Junimos' own <c>update(time, location)</c>, and their idle animation would otherwise
        /// never advance despite <see cref="StardewValley.Characters.Junimo.stayPut"/> being set to
        /// hold them in place, not freeze them. Their own <c>update</c> also can't simply be called
        /// here instead: its <c>temporaryJunimo</c> branch plays a different animation (frame 12), and
        /// its otherwise-idle branch depends on <c>Game1.IsMasterGame</c> and other world-state checks
        /// this scene doesn't want to reason about. Calling <c>Sprite.Animate</c> directly is exactly
        /// what <c>Junimo.updateSlaveAnimation</c>'s own idle branch does, and is the only piece of the
        /// vanilla animation logic this scene actually needs.</summary>
        private void AnimateJunimos(GameTime time)
        {
            foreach (Junimo j in _junimos)
                j.Sprite?.Animate(time, JunimoIdleFrame, JunimoIdleFrameCount, JunimoIdleFrameMs);
        }

        public override void update(GameTime time)
        {
            base.update(time);
            AnimateJunimos(time);
            if (_completed) return;

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

        /// <summary>Removes the Junimo actors and their light sources, idempotently: safe to call
        /// more than once (the normal completion path and the menu-steal watch can both reach it),
        /// and safe to call when some or all of them are already gone (<c>Dictionary.Remove</c> and
        /// <c>NetCollection.Remove</c> both no-op on a missing entry rather than throwing, so nothing
        /// extra is needed to tolerate that here).</summary>
        private void TeardownWorldState()
        {
            if (_tornDown) return;
            _tornDown = true;

            GameLocation loc = Game1.currentLocation;
            foreach (Junimo j in _junimos)
                loc?.characters.Remove(j);
            foreach (string lightId in _junimoLightIds)
                Game1.currentLightSources.Remove(lightId);
            _junimos.Clear();
            _junimoLightIds.Clear();
            _junimoLights.Clear();
            _junimoBaseColours.Clear();
        }

        private void Finish()
        {
            if (_completed) return;
            _completed = true;
            _phase = Phase.Done;

            TeardownWorldState();
            UnsubscribeMenuWatch();

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
