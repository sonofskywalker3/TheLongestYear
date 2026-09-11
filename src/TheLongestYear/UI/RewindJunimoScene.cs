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
        // Six directions to spread the actors around, one palette colour each, matching the six the
        // ending seats in the Community Center hall (Jeff, 2026-09-11).
        //
        // WHY THE TILES ARE SEARCHED FOR AND NOT COMPUTED. Three playtests running, only two of them
        // were ever on screen. A tight ring one tile out put two inside the bed's own sprite, which
        // draws in front of anything with a smaller Y. A wider fixed ring cleared the bed and walked
        // the outer ones into the walls, where the Front layer draws over them ("I only see 2
        // junimos, I guess they're behind the wall instead of on-top of it?"). Walking each
        // direction outward-in fixed the ones that fit and dumped the rest back onto the bed through
        // its own fallback: measured live, the farmer is at (9, 9) in the starter FarmHouse with the
        // bed in the top-RIGHT corner, so half the compass is wall within two tiles.
        //
        // No fixed shape survives that, because the room's size, the bed's corner and the amount of
        // open floor all change with the farmhouse upgrade level and the farm type. So
        // <see cref="ChooseStations"/> collects the real standable floor around the sleeper and
        // picks the six tiles that spread best across it: a ring where the room allows one, an arc
        // into the open half where it does not, and never a tile the bed or a wall is drawn over.
        private static readonly Vector2[] JunimoDirections =
        {
            new Vector2(1f, 0f), new Vector2(0.5f, 0.87f), new Vector2(-0.5f, 0.87f),
            new Vector2(-1f, 0f), new Vector2(-0.5f, -0.87f), new Vector2(0.5f, -0.87f),
        };

        /// <summary>How far from the sleeper a station may sit. The near edge keeps them off the bed
        /// and out of the farmer's own sprite; the far edge keeps the circle readable in one shot.</summary>
        private const int JunimoNearestTile = 2, JunimoFurthestTile = 6;

        /// <summary>How much a candidate is penalised for sitting next to one already chosen, so six
        /// actors spread out instead of bunching in whichever corner has the most floor.</summary>
        private const float JunimoCrowdingPenalty = 2.5f;

        private const string JunimoDisplayName = "Junimo";

        // Idle-bob constants for StardewValley.Characters.Junimo's own Sprite (frame 8, 4 frames,
        // 100ms each): the same animation vanilla plays for a standing-still, non-temporary Junimo
        // (its update()'s final "motion is zero" branch, and its updateSlaveAnimation's matching idle
        // branch), the Community Center ending look the user asked for ("moving normally", not static).
        private const int JunimoIdleFrame = 8;
        private const int JunimoIdleFrameCount = 4;
        private const float JunimoIdleFrameMs = 100f;

        // The lying-down farmer frame, the one vanilla shows for a collapsed player
        // (Farmer.performPassoutWarp and MineShaft's own faint both use showFrame(5)). Its art has
        // the eyes shut, and FarmerRenderer skips its separate eye pass entirely while
        // PauseForSingleAnimation is set, so this alone is the whole "asleep" look.
        private const int FarmerSleepingFrame = 5;

        // How many palette entries (and generated Portraits/Junimo<i> assets) exist. JunimoPortrait
        // serves 0..5 and JunimoPalette holds six colours; a speaker index is taken modulo this.
        private const int JunimoCastSize = 6;

        private static readonly FieldInfo JunimoColourField = typeof(Junimo).GetField(
            "color", BindingFlags.Instance | BindingFlags.NonPublic);

        // Set once from ModEntry.Entry. Null until then, in which case the steal watch is simply
        // inert; a scene still runs correctly end to end on its own, it just has no way to notice a
        // steal without SMAPI's own event pump, which needs a helper it cannot get through
        // IClickableMenu's fixed constructor.
        private static IModHelper _helper;
        private static IMonitor _monitor;

        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            _helper = helper;
        }

        private readonly Action _onComplete;
        private readonly List<Junimo> _junimos = new List<Junimo>();
        private readonly Texture2D[] _portraits = new Texture2D[JunimoCastSize];
        private bool _tornDown;
        private bool _menuWatchSubscribed;
        private bool _farmerAsleep;
        private bool _farmerWasInBed;
        private StardewValley.Objects.Hat _stashedHat;

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

            for (int i = 0; i < _portraits.Length; i++)
            {
                try { _portraits[i] = Game1.content.Load<Texture2D>("Portraits/Junimo" + i); }
                catch (Exception) { _portraits[i] = null; }
            }

            SleepFarmer();
            SubscribeMenuWatch();
        }

        /// <summary>The generated portrait for palette entry <paramref name="index"/>, tinted to the
        /// same colour <see cref="SpawnJunimos"/> paints that actor's sprite (see JunimoPortrait), or
        /// null if it would not load. Rotating the index per line is what makes each line read as a
        /// different Junimo speaking, the way the ending's hall scene does.</summary>
        protected Texture2D PortraitFor(int index)
        {
            int i = ((index % JunimoCastSize) + JunimoCastSize) % JunimoCastSize;
            return _portraits[i];
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

        /// <summary>Spawns the six actors in a ring around the sleeping farmer. Real
        /// <see cref="Junimo"/> instances added to the current location's own character list, so the
        /// game's normal world-space draw pass positions them under the camera transform; these
        /// scenes never draw them. <see cref="OnJunimoSpawned"/> is where a scene adds anything of
        /// its own (the bedroom gives each one a light; the morning beat deliberately gives none,
        /// that being its point).
        ///
        /// Every station is a real standable tile found by <see cref="PickStation"/>, and the six it
        /// chose are logged, so a scene that comes out wrong can be read off the log rather than
        /// guessed at from a screenshot.</summary>
        protected void SpawnJunimos()
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null || Game1.player == null) return;
            Point playerTile = Game1.player.TilePoint;
            List<Point> stations = ChooseStations(loc, playerTile);

            _monitor?.Log(
                $"Rewind Junimos: farmer at ({playerTile.X}, {playerTile.Y}) in '{loc.Name}'; stations " +
                string.Join(", ", stations.ConvertAll(p => $"({p.X}, {p.Y})")) + ".",
                LogLevel.Info);

            for (int i = 0; i < stations.Count; i++)
            {
                Vector2 worldPos = new Vector2(stations[i].X, stations[i].Y) * 64f;
                Color colour = JunimoPalette.Get(i);

                var junimo = new Junimo(worldPos, -1, temporary: true)
                {
                    Name = JunimoNamePrefix + i,
                    displayName = JunimoDisplayName,
                    EventActor = true,
                    currentLocation = loc,
                };
                junimo.stayPut.Value = true;
                // THE JUMPING (playtest 2026-09-11). Junimo's constructor sets forceUpdateTimer to
                // 9999, and GameLocation.updateCharacters runs a character's own update() when
                // EITHER time is passing OR that timer is above zero. So despite Game1.shouldTimePass
                // being false for this scene's whole run, these Junimos' update() was running every
                // tick, and re-arming the timer to 99999 on the way through (Junimo.cs:536), forever.
                // Its temporaryJunimo branch plays a different animation AND rolls for a
                // jumpWithoutSound every tick, which is what the player was watching: the actors
                // hopping, over the top of the idle this scene drives by hand. Zeroing the timer
                // stops update() ever being called, which leaves AnimateJunimos below as the only
                // thing animating them and the standing idle intact.
                junimo.forceUpdateTimer = 0;
                if (JunimoColourField?.GetValue(junimo) is NetColor net)
                    net.Value = colour;
                loc.characters.Add(junimo);
                _junimos.Add(junimo);

                OnJunimoSpawned(i, junimo, worldPos, colour);
            }
        }

        /// <summary>Per-scene extras for one freshly spawned actor. Does nothing by default.</summary>
        protected virtual void OnJunimoSpawned(int index, Junimo junimo, Vector2 worldPos, Color colour) { }

        /// <summary>The six tiles the actors stand on. Collects every standable floor tile in the
        /// band around the sleeper, then hands one to each of <see cref="JunimoDirections"/>: the
        /// candidate that lies most nearly in that direction, furthest out, and least crowded by the
        /// ones already chosen. Where the room is open on all sides that reproduces a ring; where it
        /// is not, the circle bends into the floor that exists instead of walking into the wall.
        ///
        /// If the room has nothing standable at all (no candidates), the sleeper's own tile stands
        /// in and the caller logs it: six actors always spawn, so no beat is ever short.</summary>
        private static List<Point> ChooseStations(GameLocation loc, Point centre)
        {
            var candidates = new List<Point>();
            for (int dx = -JunimoFurthestTile; dx <= JunimoFurthestTile; dx++)
            {
                for (int dy = -JunimoFurthestTile; dy <= JunimoFurthestTile; dy++)
                {
                    var tile = new Point(centre.X + dx, centre.Y + dy);
                    float distance = Distance(centre, tile);
                    if (distance < JunimoNearestTile || distance > JunimoFurthestTile) continue;
                    if (IsStandable(loc, tile)) candidates.Add(tile);
                }
            }

            var chosen = new List<Point>();
            foreach (Vector2 direction in JunimoDirections)
            {
                Point? best = null;
                float bestScore = float.MinValue;
                foreach (Point tile in candidates)
                {
                    if (chosen.Contains(tile)) continue;
                    float score = Score(centre, tile, direction, chosen);
                    if (score <= bestScore) continue;
                    bestScore = score;
                    best = tile;
                }
                chosen.Add(best ?? centre);
            }
            return chosen;
        }

        /// <summary>How well one tile serves one direction: mostly how nearly it lies along it,
        /// then how far out it is, less a penalty for every station already standing beside it.</summary>
        private static float Score(Point centre, Point tile, Vector2 direction, List<Point> chosen)
        {
            var offset = new Vector2(tile.X - centre.X, tile.Y - centre.Y);
            float distance = offset.Length();
            if (distance <= 0f) return float.MinValue;

            float alignment = Vector2.Dot(offset / distance, Vector2.Normalize(direction));
            float score = alignment * 3f + distance / JunimoFurthestTile;
            foreach (Point other in chosen)
            {
                float gap = Distance(other, tile);
                if (gap < JunimoNearestTile) score -= JunimoCrowdingPenalty * (JunimoNearestTile - gap);
            }
            return score;
        }

        private static float Distance(Point a, Point b)
            => new Vector2(a.X - b.X, a.Y - b.Y).Length();

        private static bool IsStandable(GameLocation loc, Point tile)
        {
            if (!loc.isTileOnMap(tile.X, tile.Y)) return false;
            try
            {
                if (!loc.isTilePassable(new xTile.Dimensions.Location(tile.X, tile.Y), Game1.viewport))
                    return false;
            }
            catch (Exception)
            {
                return false;   // a map without the layers the check reads; treat as unusable
            }
            // Any furniture, not just the bed. The bed is the one that hid the first ring (its
            // sprite draws in front of anything with a smaller Y), but a Junimo standing inside the
            // table or the bookcase reads no better.
            if (loc.GetFurnitureAt(new Vector2(tile.X, tile.Y)) != null) return false;
            return !loc.objects.ContainsKey(new Vector2(tile.X, tile.Y));
        }

        /// <summary>Beat 1: the farmer reads as asleep rather than standing at the top edge of the
        /// bed facing the wall with a hat on, as though they had walked into it (playtest
        /// 2026-09-11; the designer's word for the scene is "a dream"). Three pieces:
        /// <c>showFrame</c> holds vanilla's own lying-down frame, whose art has the eyes shut and
        /// whose PauseForSingleAnimation suppresses FarmerRenderer's separate eye pass; the hat comes
        /// off for the duration; and isInBed is set so anything reading it agrees with the picture.
        /// <see cref="WakeFarmer"/> puts all three back.</summary>
        private void SleepFarmer()
        {
            Farmer player = Game1.player;
            if (player == null || _farmerAsleep) return;
            _farmerAsleep = true;
            _farmerWasInBed = player.isInBed.Value;
            _stashedHat = player.hat.Value;
            player.hat.Value = null;
            player.isInBed.Value = true;
            player.showFrame(FarmerSleepingFrame);
        }

        /// <summary>Re-asserts the sleeping pose if anything has knocked the farmer out of it.
        /// <c>Farmer.Update</c> runs every tick regardless of this menu (Game1.UpdateCharacters is
        /// not gated on shouldTimePass), so the frame is not simply set once and left.</summary>
        private void HoldFarmerAsleep()
        {
            if (!_farmerAsleep) return;
            Farmer player = Game1.player;
            if (player?.FarmerSprite == null) return;
            if (player.FarmerSprite.CurrentFrame != FarmerSleepingFrame || !player.FarmerSprite.PauseForSingleAnimation)
                player.showFrame(FarmerSleepingFrame);
        }

        /// <summary>Gives the farmer back their pose, their hat and their real isInBed. Runs inside
        /// <see cref="TeardownWorldState"/>'s once-only guard, so every exit path reaches it exactly
        /// once: normal completion, and the menu-steal watch.</summary>
        private void WakeFarmer()
        {
            if (!_farmerAsleep) return;
            _farmerAsleep = false;
            Farmer player = Game1.player;
            if (player == null) return;
            player.stopShowingFrame();
            player.isInBed.Value = _farmerWasInBed;
            if (_stashedHat != null)
            {
                player.hat.Value = _stashedHat;
                _stashedHat = null;
            }
        }

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

            WakeFarmer();
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
            HoldFarmerAsleep();
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
