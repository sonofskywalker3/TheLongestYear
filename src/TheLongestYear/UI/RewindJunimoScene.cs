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
using TheLongestYear.Integration;

namespace TheLongestYear.UI
{
    /// <summary>REQUIRED WIRING: ModEntry.Entry must call <see cref="Register"/> once, or the
    /// menu-steal safety net below is dead code for every scene that derives from this.
    ///
    /// Everything the rewind cutscene's two farmhouse beats do identically: the four Junimo actors
    /// around the sleeping farmer, their hand-driven idle animation, their teardown, the
    /// <c>Display.MenuChanged</c> steal watch that tears them down when something else takes the
    /// frame, the forwarding of player input to an <see cref="EndingSpeechBox"/> that is not itself
    /// the active menu, and the single-shot <see cref="Finish"/>.
    ///
    /// Extracted 2026-09-11 from <see cref="RewindBedroomScene"/> (beats 1-9) and
    /// <see cref="RewindMorningScene"/> (beats 11-12), which were near line-for-line copies of each
    /// other except that the morning copy was made before the steal watch was added to the bedroom
    /// and never got one. That gap was the live defect this extraction closes: a menu stealing the
    /// frame during beat 12 left four Junimos standing in the farmhouse, and the driver then re-armed
    /// and replayed from beat 1, spawning four more. Sharing the net rather than pasting it a second
    /// time is what stops the next scene inheriting the same omission.
    ///
    /// Why these scenes are drawn menus rather than vanilla Events, why the Junimos' idle animation
    /// has to be driven by hand, and why the speech box is a plain object instead of the active menu,
    /// are all documented on the members below and in <see cref="Day28CutsceneMenu"/>.</summary>
    internal abstract class RewindJunimoScene : IClickableMenu
    {
        // Four stations around the bed, one palette colour each (JunimoPalette has six; these scenes
        // only need four).
        private static readonly Point[] JunimoOffsets =
        {
            new Point(-1, -1), new Point(1, -1), new Point(-1, 1), new Point(1, 1),
        };
        private const string JunimoDisplayName = "Junimo";

        // Idle-bob constants for StardewValley.Characters.Junimo's own Sprite (frame 8, 4 frames,
        // 100ms each): the same animation vanilla plays for a standing-still, non-temporary Junimo
        // (its update()'s final "motion is zero" branch, and its updateSlaveAnimation's matching idle
        // branch), the Community Center ending look the user asked for ("moving normally", not static).
        private const int JunimoIdleFrame = 8;
        private const int JunimoIdleFrameCount = 4;
        private const float JunimoIdleFrameMs = 100f;

        private static readonly FieldInfo JunimoColourField = typeof(Junimo).GetField(
            "color", BindingFlags.Instance | BindingFlags.NonPublic);

        // Set once from ModEntry.Entry. Null until then, in which case the steal watch is simply
        // inert; a scene still runs correctly end to end on its own, it just has no way to notice a
        // steal without SMAPI's own event pump, which needs a helper it cannot get through
        // IClickableMenu's fixed constructor.
        private static IModHelper _helper;

        public static void Register(IModHelper helper) => _helper = helper;

        private readonly Action _onComplete;
        private readonly List<Junimo> _junimos = new List<Junimo>();
        private bool _tornDown;
        private bool _menuWatchSubscribed;

        /// <summary>The Junimo portrait for this scene's speech box, or null if it would not load.</summary>
        protected Texture2D Portrait { get; }

        /// <summary>The open speech box, or null when no line is playing. A plain object, never the
        /// active menu (this scene is), which is what lets the scene keep ticking behind it.</summary>
        protected EndingSpeechBox ActiveBox { get; set; }

        /// <summary>True once <see cref="Finish"/> has run. Subclasses check it to stop advancing.</summary>
        protected bool Completed { get; private set; }

        /// <summary>Distinct per scene so two scenes' actors can never be confused for each other in
        /// a location's character list or a log.</summary>
        protected abstract string JunimoNamePrefix { get; }

        protected RewindJunimoScene(Action onComplete)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, showUpperRightCloseButton: false)
        {
            _onComplete = onComplete;

            try { Portrait = Game1.content.Load<Texture2D>("Portraits/Junimo0"); }
            catch (Exception) { Portrait = null; }

            SubscribeMenuWatch();
        }

        /// <summary>Watches for something else replacing this scene as the active menu (a steal, not
        /// this scene's own normal completion) and tears down the world state if that happens, since
        /// nothing else will: a menu's own <c>update</c>/<c>draw</c> only run while it IS the active
        /// menu, so it cannot notice or react to losing that slot on its own. SMAPI's event pump runs
        /// regardless, which is the whole reason this needs a helper reference at all.</summary>
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
            // leak: whatever replaced us, our Junimos do not belong in the save any more.
            TeardownWorldState();
            UnsubscribeMenuWatch();
        }

        /// <summary>Spawns the four actors around the sleeping farmer. Real <see cref="Junimo"/>
        /// instances added to the current location's own character list, so the game's normal
        /// world-space draw pass positions them under the camera transform; these scenes never draw
        /// them. <see cref="OnJunimoSpawned"/> is where a scene adds anything of its own (the bedroom
        /// gives each one a light; the morning beat deliberately gives none, that being its point).</summary>
        protected void SpawnJunimos()
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null || Game1.player == null) return;
            Point playerTile = Game1.player.TilePoint;

            for (int i = 0; i < JunimoOffsets.Length; i++)
            {
                Vector2 worldPos = new Vector2(playerTile.X + JunimoOffsets[i].X, playerTile.Y + JunimoOffsets[i].Y) * 64f;
                Color colour = JunimoPalette.Get(i);

                var junimo = new Junimo(worldPos, -1, temporary: true)
                {
                    Name = JunimoNamePrefix + i,
                    displayName = JunimoDisplayName,
                    EventActor = true,
                    currentLocation = loc,
                };
                junimo.stayPut.Value = true;
                if (JunimoColourField?.GetValue(junimo) is NetColor net)
                    net.Value = colour;
                loc.characters.Add(junimo);
                _junimos.Add(junimo);

                OnJunimoSpawned(i, junimo, worldPos, colour);
            }
        }

        /// <summary>Per-scene extras for one freshly spawned actor. Does nothing by default.</summary>
        protected virtual void OnJunimoSpawned(int index, Junimo junimo, Vector2 worldPos, Color colour) { }

        /// <summary>Drives the Junimos' idle animation directly, every tick the scene is active.
        /// <see cref="Game1.shouldTimePass"/> is false for a scene's whole run (any non-BobberBar
        /// activeClickableMenu forces it false), so <c>GameLocation.updateCharacters</c> never calls
        /// these Junimos' own <c>update(time, location)</c>, and their idle animation would otherwise
        /// never advance despite <see cref="Junimo.stayPut"/> being set to hold them in place, not
        /// freeze them. Their own <c>update</c> also cannot simply be called here instead: its
        /// <c>temporaryJunimo</c> branch plays a different animation (frame 12), and its
        /// otherwise-idle branch depends on <c>Game1.IsMasterGame</c> and other world-state checks
        /// these scenes do not want to reason about. Calling <c>Sprite.Animate</c> directly is exactly
        /// what <c>Junimo.updateSlaveAnimation</c>'s own idle branch does, and is the only piece of
        /// the vanilla animation logic these scenes actually need.</summary>
        private void AnimateJunimos(GameTime time)
        {
            foreach (Junimo j in _junimos)
                j.Sprite?.Animate(time, JunimoIdleFrame, JunimoIdleFrameCount, JunimoIdleFrameMs);
        }

        /// <summary>Removes the Junimo actors (and, through <see cref="TeardownSceneExtras"/>,
        /// anything else the scene put in the world), idempotently: safe to call more than once, since
        /// the normal completion path and the menu-steal watch can both reach it, and safe when some
        /// or all of them are already gone (<c>Dictionary.Remove</c> and <c>NetCollection.Remove</c>
        /// both no-op on a missing entry rather than throwing).</summary>
        protected void TeardownWorldState()
        {
            if (_tornDown) return;
            _tornDown = true;

            GameLocation loc = Game1.currentLocation;
            foreach (Junimo j in _junimos)
                loc?.characters.Remove(j);
            _junimos.Clear();

            TeardownSceneExtras();
        }

        /// <summary>Per-scene world state to remove alongside the actors. Does nothing by default.
        /// Runs inside <see cref="TeardownWorldState"/>'s once-only guard, so it never runs twice.</summary>
        protected virtual void TeardownSceneExtras() { }

        /// <summary>Forwards player input to the open dialogue box. <see cref="EndingSpeechBox"/> ends
        /// itself by calling <c>Game1.exitActiveMenu()</c> on its last page. Since it is not actually
        /// the active menu (this scene is), that call just clears the static field; noticing that in
        /// the same call and putting ourselves straight back, before the game gets another frame, is
        /// what lets a scene tell "the line finished" from "something else stole the frame" without a
        /// callback on EndingSpeechBox itself.</summary>
        protected void ForwardToBox(Action<EndingSpeechBox> invoke)
        {
            if (ActiveBox == null) return;
            bool wasActive = ReferenceEquals(Game1.activeClickableMenu, this);
            invoke(ActiveBox);
            if (!wasActive || ReferenceEquals(Game1.activeClickableMenu, this)) return;

            Game1.activeClickableMenu = this;
            ActiveBox = null;
            OnBoxClosed();
        }

        /// <summary>What this scene does when its open line finishes: advance to the next beat, or
        /// finish.</summary>
        protected abstract void OnBoxClosed();

        /// <summary>Ends the scene once: tears the world state down, stops watching for a steal,
        /// releases the menu slot if we still hold it, and runs the completion callback.</summary>
        protected void Finish()
        {
            if (Completed) return;
            Completed = true;

            OnFinishing();
            TeardownWorldState();
            UnsubscribeMenuWatch();

            if (ReferenceEquals(Game1.activeClickableMenu, this))
                Game1.activeClickableMenu = null;
            _onComplete?.Invoke();
        }

        /// <summary>Runs at the top of <see cref="Finish"/>, before any teardown. Does nothing by
        /// default.</summary>
        protected virtual void OnFinishing() { }

        /// <summary>Jumps straight to this scene's completion, exactly as if the player had clicked
        /// through every remaining beat. The per-scene entry point for the <c>tly_skipscene</c>
        /// console command, and the seam a player-facing per-scene skip would sit on later: each
        /// scene owns its own skip, so one can be made skippable without the others.</summary>
        public virtual void SkipToEnd()
        {
            ActiveBox = null;
            Finish();
        }

        public override void update(GameTime time)
        {
            base.update(time);
            AnimateJunimos(time);
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
        // presses to the open dialogue box (above) only advances its page, never closes the scene.
        public override bool readyToClose() => false;

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            width = Game1.uiViewport.Width;
            height = Game1.uiViewport.Height;
        }

        public override void draw(SpriteBatch b)
        {
            ActiveBox?.draw(b);
        }
    }
}
