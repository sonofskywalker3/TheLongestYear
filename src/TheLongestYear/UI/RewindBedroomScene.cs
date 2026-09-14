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
        private enum Phase { LightsOut, JunimosIn, Say1, Say2, Say3, Say4, White, Done }

        // Beat timings. Phases without a listed constant (the four Say phases) advance when their
        // dialogue box closes instead of on a clock.
        private const float LightsOutHoldMs = 600f;
        private const float JunimosInHoldMs = 1000f;
        // Beat 6, which runs on its own clock underneath lines 2 to 4 (see EnterPhase). "Make it take
        // like 5 seconds to go from normal bright to the darkened effect" (Jeff, 2026-09-14).
        private const float DarknessInMs = 5000f;
        private const float WhiteMs = 2200f;
        // Beat 9's lead-in: the Junimo light swells, breaking the tentacles up as it goes, before the
        // white starts ("maybe 1-2 seconds of their light expanding before starting the fade to
        // white", Jeff, 2026-09-14).
        private const float ExpandLeadMs = 1500f;
        // How far past the tentacles' roots the lead-in pushes the light, so none survive into the white.
        private const float LeadClearOvershoot = 1.05f;

        // Beat 2/6/9's light dials. sconceLight is a small round light, the same texture index used
        // for ordinary room lights elsewhere in the game.
        private const int JunimoLightTexture = StardewValley.LightSource.sconceLight;
        // Sized for the room the scene is actually in, and for SIX of them. The starter farmhouse is
        // about ten tiles across, so a radius that would read as one lamp in a mine lights the whole
        // house: at 4, and then again at 1.5, the six pools overlapped into plain daylight ("dark for
        // a second, then light", screenshots 2026-09-11). Overlap is why the count matters so much
        // more than it looks: the lights are alpha-blended into the lightmap one after another, so
        // six faint tails at the same pixel compound into a strong one. These still overlap into one
        // unbroken lit area around the bed, which is what was asked for, but the falloff dies before
        // it reaches the far side of the room.
        private const float JunimoLightRadiusStart = 1.0f;
        // Where the six pools end up once the darkness has closed: 20% smaller, same brightness
        // ("lower the junimo light output by an additional 20% in size not brightness", Jeff,
        // 2026-09-14). This reverses the 2026-09-11 call that the pools never shrink, at his word.
        private const float JunimoLightRadiusClosed = 0.8f;

        // THE HEARTH. One big light on the bed, and the only thing that moves when the darkness
        // closes in.
        //
        // Beat 6 used to deepen the ambient AND shrink all six Junimo pools together, which is not
        // what closing in looks like: every pool dimmed in place, so the room got evenly darker
        // rather than the dark advancing on the bed. Jeff, 2026-09-11: "the darkness pushing in needs
        // to push in from the outside but not diminish the Junimo's light. If that means you need to
        // make the farmer the center of the bed be the true light source that's fine, but it needs to
        // have a pushing in effect, not a reducing the range thing like it is now."
        //
        // So the six Junimo pools are now fixed for the whole beat and this one is the dial. It
        // starts wide enough to reach the room's corners and shrinks to the bed, which reads as the
        // lit area contracting from the walls inward while the Junimos go on burning exactly as
        // brightly as they were. It is also what answers "it's leaving pockets of darkness, I want
        // them to be enough to light up everything around the player": at its opening radius there
        // is one continuous lit area around the bed, not six separate pools with gaps between them.
        // Sized like the Junimo pools, not like a floodlight: a sconce light's own falloff reaches
        // roughly three tiles for every one of radius, so anything much past this lights the far wall
        // and the scene is an afternoon again. Dimmer than a Junimo pool too, because its job is to
        // fill the gaps BETWEEN them rather than to be a seventh light anyone looks at.
        private const float HearthRadiusStart = 1.6f;
        private const float HearthRadiusFloor = 0.35f;
        private const float HearthTintStrength = 0.0f;   // vanilla's lantern, untinted: this one is not a Junimo
        private const float HearthBrightness = 0.6f;
        private const string HearthLightId = "TlyRewindHearthLight";
        // "Past the screen size" per the brief: large enough that the light's own falloff covers
        // every pixel long before the geometric radius is reached.
        private const float JunimoLightRadiusFlash = 40f;

        // How far a pool leans toward its Junimo's palette colour. Gentle on purpose: at 1 a pool is
        // a flat colour wash rather than a lit patch of floor. See RewindNightLight.PoolTint.
        private const float JunimoPoolTintStrength = 0.35f;

        // How far a pool travels from the surrounding dark toward a full lantern. Near 1, because
        // the thing it is travelling toward is now vanilla's own lantern colour rather than "subtract
        // nothing": a pool at 1 is a lit patch of cave floor, not daylight. See
        // RewindNightLight.PoolColour and LanternPool.
        private const float JunimoPoolBrightness = 0.9f;

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
        private LightSource _hearthLight;
        private Color _hearthBaseColour;

        private Phase _phase;
        private float _phaseElapsed;
        private float _flashAlpha;

        // Beat 6's own clock. It starts with line 2 and runs whether or not a box is open.
        private bool _darknessRunning;
        private float _darknessElapsed;

        // Where beat 9 starts from: wherever beat 6 had reached, which is short of its end when the
        // last lines are clicked through faster than the darkness closes.
        private Color _whiteFromAmbient = RewindNightLight.NightAmbient;
        private float _whiteFromHearthRadius = HearthRadiusStart;
        private float _whiteFromJunimoRadius = JunimoLightRadiusStart;
        // Where the lead-in takes the light before the white starts, and the lit edge the tentacles
        // were drawn against when it began. Frozen so they stay put and break up as the light passes
        // them, rather than being stretched outward with it ("it was weird seeing the drawing grow as
        // it moved outward with the expanding light", Jeff, 2026-09-14).
        private float _leadJunimoRadius = JunimoLightRadiusStart;
        private float _leadHearthRadius = HearthRadiusStart;
        private float _tentacleEdge;

        private readonly RewindTentacles _tentacles = new RewindTentacles();

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
                    SpawnHearth();
                    SpawnJunimos();
                    break;
                case Phase.Say1:
                    OpenBox(0, Strings.Get("cutscene.rewind.junimo-1"));
                    break;
                case Phase.Say2:
                    OpenBox(1, Strings.Get("cutscene.rewind.junimo-2"));
                    // Beat 6 closes in WHILE this line is on screen. It used to be a phase of its
                    // own between lines 2 and 3, with no box up: "I also don't like that the dialog
                    // pauses while the fade to black happens, can it fade with the second message on
                    // screen?" (Jeff, 2026-09-14).
                    _darknessRunning = true;
                    _darknessElapsed = 0f;
                    break;
                case Phase.Say3:
                    OpenBox(2, Strings.Get("cutscene.rewind.junimo-3"));
                    break;
                case Phase.Say4:
                    OpenBox(3, Strings.Get("cutscene.rewind.junimo-4"));
                    break;
                case Phase.White:
                    _darknessRunning = false;
                    _whiteFromAmbient = RewindNightLight.Ambient;
                    _whiteFromHearthRadius = _hearthLight?.radius.Value ?? HearthRadiusFloor;
                    _whiteFromJunimoRadius = _junimoLights.Count > 0 ? _junimoLights[0].radius.Value : JunimoLightRadiusClosed;
                    PlanLeadIn();
                    break;
                case Phase.Done:
                    break;
            }
        }

        /// <summary>The one light that is not a Junimo: a wide, soft pool centred on the bed, which
        /// beat 6 then closes in. See HearthRadiusStart for why the closing-in lives here rather than
        /// on the six actors.
        ///
        /// Centred on the BED, not the farmer, for the same reason the ring is: the sleeper lies at
        /// one end of it, and a light hung off the farmer's own tile lights the pillow and leaves the
        /// foot of the bed in the dark.</summary>
        private void SpawnHearth()
        {
            Vector2 centre = BedCentre();
            _hearthBaseColour = RewindNightLight.PoolColour(
                RewindNightLight.LanternPool, HearthTintStrength, HearthBrightness);
            _hearthLight = new LightSource(
                HearthLightId, JunimoLightTexture, centre, HearthRadiusStart, _hearthBaseColour);
            Game1.currentLightSources[HearthLightId] = _hearthLight;
            _junimoLightIds.Add(HearthLightId);
        }

        /// <summary>The middle of the bed in world pixels, or the farmer if this room has no bed to
        /// find.</summary>
        private static Vector2 BedCentre()
        {
            if (Game1.currentLocation is StardewValley.Locations.FarmHouse house)
            {
                try
                {
                    StardewValley.Objects.BedFurniture bed = house.GetPlayerBed();
                    if (bed != null)
                    {
                        Rectangle box = bed.GetBoundingBox();
                        return new Vector2(box.X + box.Width / 2f, box.Y + box.Height / 2f);
                    }
                }
                catch (Exception) { /* fall through to the farmer */ }
            }
            return Game1.player?.Position ?? Vector2.Zero;
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
            Color lightColour = RewindNightLight.PoolColour(colour, JunimoPoolTintStrength, JunimoPoolBrightness);
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
            _hearthLight = null;
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
                case Phase.Say2: EnterPhase(Phase.Say3); break;
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
            // The hearth carries the closing-in (see HearthRadiusStart); the six Junimo pools lose a
            // fifth of their size alongside it but none of their brightness (JunimoLightRadiusClosed).
            if (_hearthLight != null)
                _hearthLight.radius.Value = MathHelper.Lerp(HearthRadiusStart, HearthRadiusFloor, eased);
            float junimoRadius = MathHelper.Lerp(JunimoLightRadiusStart, JunimoLightRadiusClosed, eased);
            foreach (LightSource light in _junimoLights)
                light.radius.Value = junimoRadius;
            _tentacles.Strength = eased;
        }

        /// <summary>Works out, as beat 9 begins, how big the lights must swell during the lead-in for
        /// their lit edge to pass every tentacle's root, and freezes the edge the tentacles are drawn
        /// against.</summary>
        private void PlanLeadIn()
        {
            if (_hearthLight == null) return;
            _tentacleEdge = LitEdgeRadius();
            float clearTo = RewindTentacles.RootDistance(_tentacleEdge) * LeadClearOvershoot;
            float reachPerRadius = RewindTentacles.GlowReach(1f);
            float ringDistance = _junimoLights.Count > 0
                ? Vector2.Distance(_junimoLights[0].position.Value, _hearthLight.position.Value)
                : 0f;
            _leadHearthRadius = Math.Max(_whiteFromHearthRadius, clearTo / reachPerRadius);
            _leadJunimoRadius = Math.Max(_whiteFromJunimoRadius, (clearTo - ringDistance) / reachPerRadius);
        }

        /// <summary>Beat 9, <paramref name="elapsedMs"/> in. First the lead-in: the lights swell out
        /// past the tentacles' roots and break them up. Then the white: the radii grow past the size
        /// of the screen and every light goes fully neutral (black subtracts nothing), which opens the
        /// room back to full brightness; the ambient follows them down to black so no corner is left
        /// dark; and the overlay carries the last stretch to white, which subtractive lighting cannot
        /// do on its own.</summary>
        private void ApplyWhite(float elapsedMs)
        {
            float grow = Ease(MathHelper.Clamp(elapsedMs / ExpandLeadMs, 0f, 1f));
            float flash = Ease(MathHelper.Clamp((elapsedMs - ExpandLeadMs) / WhiteMs, 0f, 1f));
            RewindNightLight.Ambient = Color.Lerp(_whiteFromAmbient, Color.Black, flash);
            float radius = MathHelper.Lerp(
                MathHelper.Lerp(_whiteFromJunimoRadius, _leadJunimoRadius, grow), JunimoLightRadiusFlash, flash);
            for (int i = 0; i < _junimoLights.Count; i++)
            {
                _junimoLights[i].radius.Value = radius;
                _junimoLights[i].color.Value = Color.Lerp(_junimoBaseColours[i], Color.Black, flash);
            }
            if (_hearthLight != null)
            {
                _hearthLight.radius.Value = MathHelper.Lerp(
                    MathHelper.Lerp(_whiteFromHearthRadius, _leadHearthRadius, grow), JunimoLightRadiusFlash, flash);
                _hearthLight.color.Value = Color.Lerp(_hearthBaseColour, Color.Black, flash);
            }
            _tentacles.Opacity = 1f - flash;
            _flashAlpha = flash;
        }

        public override void update(GameTime time)
        {
            base.update(time);   // keeps the Junimos bobbing, including once the scene is done
            if (Completed) return;

            float elapsedMs = (float)time.ElapsedGameTime.TotalMilliseconds;
            _tentacles.Update(elapsedMs);
            if (_darknessRunning)
            {
                _darknessElapsed += elapsedMs;
                ApplyDarkness(_darknessElapsed / DarknessInMs);
                if (_darknessElapsed >= DarknessInMs) _darknessRunning = false;
            }

            if (ActiveBox != null)
            {
                ActiveBox.update(time);
                return;
            }

            _phaseElapsed += elapsedMs;

            switch (_phase)
            {
                case Phase.LightsOut:
                    if (_phaseElapsed >= LightsOutHoldMs) EnterPhase(Phase.JunimosIn);
                    break;
                case Phase.JunimosIn:
                    if (_phaseElapsed >= JunimosInHoldMs) EnterPhase(Phase.Say1);
                    break;
                case Phase.White:
                    ApplyWhite(_phaseElapsed);
                    if (_phaseElapsed >= ExpandLeadMs + WhiteMs) Finish();
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
            ApplyWhite(ExpandLeadMs + WhiteMs);
            base.SkipToEnd();
        }

        /// <summary>How far from the bed the lit area reaches, in world pixels: the farthest any light's
        /// glow gets, the hearth's own or a Junimo's plus its distance from the bed. Read off the live
        /// radii every frame, so the tentacles follow the light in as the darkness closes.</summary>
        private float LitEdgeRadius()
        {
            Vector2 centre = _hearthLight.position.Value;
            float edge = RewindTentacles.GlowReach(_hearthLight.radius.Value);
            foreach (LightSource light in _junimoLights)
                edge = Math.Max(edge,
                    Vector2.Distance(light.position.Value, centre) + RewindTentacles.GlowReach(light.radius.Value));
            return edge;
        }

        public override void draw(SpriteBatch b)
        {
            // Under the flash and the speech box, over the lit world. See RewindTentacles. While the
            // light swells they stay against the edge they had, and the live edge breaks them up.
            if (_hearthLight != null)
            {
                bool swelling = _phase == Phase.White;
                _tentacles.Draw(b, _hearthLight.position.Value,
                    swelling ? _tentacleEdge : LitEdgeRadius(),
                    swelling ? LitEdgeRadius() : 0f);
            }
            if (_flashAlpha > 0f)
            {
                int w = Game1.uiViewport.Width, h = Game1.uiViewport.Height;
                b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, w, h), Color.White * _flashAlpha);
            }
            base.draw(b);   // the speech box on top
        }
    }
}
