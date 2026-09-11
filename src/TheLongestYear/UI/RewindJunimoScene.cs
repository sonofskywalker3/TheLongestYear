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
        // A tight hexagonal ring two tiles out, one palette colour each, matching the six the ending
        // seats in the Community Center hall.
        //
        // WALL TILES ARE FINE. This went round three times. A fixed ring one tile out buried two of
        // them in the bed sprite; a fixed ring two to three tiles out walked the outer ones into the
        // walls; searching for standable floor instead put them wherever the room happened to have
        // space, which is what Jeff saw and rejected: "I specifically said that I was ok with the
        // junimos on the wall, it look super weird for them to be where they are" (2026-09-11). So
        // the shape wins over the floor plan: a symmetric ring round the sleeper, clamped only so
        // nothing lands off the map, standing in the wall where the wall is where the ring goes.
        // What made the first ring fail was the BED, not the walls, and two tiles out clears it.
        // Six points on a ring, at 60 degrees apart, measured in TILES from the ring's centre and
        // applied as world positions rather than tile indices, so the shape stays a ring instead of
        // being rounded onto the tile grid into a lopsided blob.
        //
        // A CIRCLE, not an ellipse. This was briefly squashed wide and shallow to fit the room,
        // and it showed: "the light is better now because it doesn't go black in the middle, but
        // it's still weird because it's like an oval instead of a circle" (Jeff, 2026-09-11). The
        // room is made to fit the ring instead, by lifting the camera (see CameraLift) so the two
        // tiles below the bed are not behind the dialogue box.
        private const float RingRadius = 1.75f;
        private const int RingPoints = 6;

        /// <summary>How far up the room is nudged for these beats, in pixels.
        ///
        /// The farmhouse is small enough to fit on screen whole, so the camera normally centres it
        /// with black above and below and cannot scroll. That put the room's bottom wall at almost
        /// exactly the top edge of the dialogue box (212px tall on a 64px margin, so its top lands at
        /// 804 on a 1080 screen against a room that ends at about 830), and anything standing on or
        /// below that wall was behind the UI for the whole of every line. Lifting the world into the
        /// black above it costs nothing and buys the two tiles the ring needs, which is what lets the
        /// ring be a circle centred on the bed instead of an oval squashed up off the floor.</summary>
        private const int CameraLift = 160;

        /// <summary>How far outside the clear floor a station may sit. Wall tiles are explicitly fine
        /// ("I specifically said that I was ok with the junimos on the wall", 2026-09-11) and
        /// <see cref="OnRenderedWorld"/> now draws the actors over the wall art, so the ring is
        /// allowed one tile of wall on every side. What it is NOT allowed is the black past the
        /// building, which is what two of them were standing in.</summary>
        private const float RingWallAllowance = 1.5f;

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
        private bool _cameraLifted;
        private bool _priorViewportFreeze;
        private int _priorViewportY;

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
            LiftCamera();
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
            _helper.Events.Display.RenderedWorld += OnRenderedWorld;
            _menuWatchSubscribed = true;
        }

        private void UnsubscribeMenuWatch()
        {
            if (!_menuWatchSubscribed) return;
            _helper.Events.Display.MenuChanged -= OnMenuChanged;
            _helper.Events.Display.RenderedWorld -= OnRenderedWorld;
            _menuWatchSubscribed = false;
        }

        /// <summary>Draws the actors a second time, above the map's Front layer.
        ///
        /// WHY THIS EXISTS, and why it is not a workaround. The actors are real characters in the
        /// location, so the world pass positions and lights them correctly, but that pass draws
        /// characters BEFORE the Front layer, which is the layer the walls are on. Three of the six
        /// stations on the starter farmhouse sit on Front tiles (the room grid logged by
        /// <see cref="RoomGrid"/> reads F at x 11 and on rows 10 and 11), so three Junimos were being
        /// drawn and then painted over by the wall: "I only see 2 junimos, I guess they're behind the
        /// wall instead of on-top of it" (2026-09-11). The designer's answer to that was to keep them
        /// on the wall and put them in front of it, not to move the ring off the wall: "I specifically
        /// said that I was ok with the junimos on the wall".
        ///
        /// SMAPI's RenderedWorld runs after the map's own layers and still in world space, so the
        /// same <c>draw</c> the world pass would have used lands in the same place, just later. The
        /// ones that were already visible are drawn twice at identical coordinates; Junimo art is
        /// pixel art with no partial alpha, so the second pass is pixel-for-pixel what is already
        /// there and nothing about them changes.</summary>
        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            if (!ReferenceEquals(Game1.activeClickableMenu, this)) return;
            foreach (Junimo j in _junimos)
            {
                try { j.draw(e.SpriteBatch); }
                catch (Exception) { /* one bad actor must not take the frame down */ }
            }
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
        /// The six stations are logged, so a scene that comes out wrong can be read off the log
        /// rather than guessed at from a screenshot.</summary>
        protected void SpawnJunimos()
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null || Game1.player == null) return;
            Point playerTile = Game1.player.TilePoint;
            Vector2 centre = RingCentre(loc, playerTile);
            List<Vector2> stations = RingStations(loc, centre);

            _monitor?.Log(
                $"Rewind Junimos: farmer at ({playerTile.X}, {playerTile.Y}) in '{loc.Name}'; ring centred on " +
                $"({centre.X:0.00}, {centre.Y:0.00}); stations " +
                string.Join(", ", stations.ConvertAll(p => $"({p.X:0.00}, {p.Y:0.00})")) +
                $"; walkable floor {WalkableBox(loc, playerTile)}.",
                LogLevel.Info);
            _monitor?.Log($"Rewind Junimos: room grid ({RoomGrid(loc, playerTile)}", LogLevel.Info);

            for (int i = 0; i < stations.Count; i++)
            {
                Vector2 worldPos = stations[i] * 64f;
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


        /// <summary>Where the ring goes. THE BED, not the farmer (Jeff, 2026-09-11: "Junimos are
        /// centered around the farmer and not the bed, so it looks wrong"). The farmer lies at one
        /// end of a bed two tiles wide and three deep, so a ring hung off the farmer's own tile sits
        /// visibly off to one side of the thing it is supposed to be encircling.
        ///
        /// Then clamped so the whole ring lands on the building. The bed is in the corner of the
        /// room, so a ring centred honestly on it runs off the floor to the right and below; one
        /// tile of that is fine and wanted (the wall), but two of the six were standing in the black
        /// past the house. Clamping the CENTRE rather than each station individually is what keeps
        /// the shape a ring: every point moves together.</summary>
        private static Vector2 RingCentre(GameLocation loc, Point playerTile)
        {
            Vector2 centre = new Vector2(playerTile.X + 0.5f, playerTile.Y + 0.5f);
            if (loc is StardewValley.Locations.FarmHouse house)
            {
                try
                {
                    StardewValley.Objects.BedFurniture bed = house.GetPlayerBed();
                    if (bed != null)
                    {
                        Rectangle box = bed.GetBoundingBox();
                        centre = new Vector2((box.X + box.Width / 2f) / 64f, (box.Y + box.Height / 2f) / 64f);
                    }
                }
                catch (Exception) { /* no bed, or a modded house: the farmer is a fine fallback */ }
            }

            Rectangle floor = FloorBox(loc, playerTile);
            if (floor.Width <= 0 || floor.Height <= 0) return centre;
            float minX = floor.Left - RingWallAllowance + RingRadius;
            float maxX = floor.Right + RingWallAllowance - RingRadius;
            float minY = floor.Top - RingWallAllowance + RingRadius;
            float maxY = floor.Bottom + RingWallAllowance - RingRadius;
            return new Vector2(
                maxX < minX ? (minX + maxX) / 2f : MathHelper.Clamp(centre.X, minX, maxX),
                maxY < minY ? (minY + maxY) / 2f : MathHelper.Clamp(centre.Y, minY, maxY));
        }

        /// <summary>The six points of the ring, in tiles, starting at the right and going round.
        /// Offset half a turn of a step so no station lands squarely on the sleeper's own head.</summary>
        private static List<Vector2> RingStations(GameLocation loc, Vector2 centre)
        {
            var stations = new List<Vector2>(RingPoints);
            for (int i = 0; i < RingPoints; i++)
            {
                double angle = Math.PI * 2.0 * i / RingPoints;
                stations.Add(new Vector2(
                    centre.X + RingRadius * (float)Math.Cos(angle) - 0.5f,
                    centre.Y + RingRadius * (float)Math.Sin(angle) - 0.5f));
            }
            return stations;
        }

        /// <summary>The box of CLEAR FLOOR around the sleeper: measured by walking out from the
        /// sleeper's own tile in each of the four directions and stopping at the first tile that is
        /// not clear (no Back tile, or a Front tile over it).
        ///
        /// RAYS, NOT A BOUNDING BOX OVER EVERY CLEAR TILE, which is what this did first and got
        /// wrong: the farmhouse's front door sits in an alcove one row BELOW the bottom wall, so a
        /// single clear tile down there stretched the box a whole row past the room and the ring
        /// clamped to a centre that was still half off the floor. Walking out from the sleeper stops
        /// at the wall, which is the room the sleeper is actually in.</summary>
        private static Rectangle FloorBox(GameLocation loc, Point centre, int reach = 12)
        {
            xTile.Layers.Layer back = loc.map?.GetLayer("Back");
            if (back == null) return Rectangle.Empty;
            xTile.Layers.Layer front = loc.map?.GetLayer("Front");

            bool Clear(int x, int y)
                => loc.isTileOnMap(x, y) && Tile(back, x, y) != null && Tile(front, x, y) == null;

            if (!Clear(centre.X, centre.Y)) return Rectangle.Empty;
            int minX = centre.X, maxX = centre.X, minY = centre.Y, maxY = centre.Y;
            while (minX - 1 >= centre.X - reach && Clear(minX - 1, centre.Y)) minX--;
            while (maxX + 1 <= centre.X + reach && Clear(maxX + 1, centre.Y)) maxX++;
            while (minY - 1 >= centre.Y - reach && Clear(centre.X, minY - 1)) minY--;
            while (maxY + 1 <= centre.Y + reach && Clear(centre.X, maxY + 1)) maxY++;
            return new Rectangle(minX, minY, maxX - minX, maxY - minY);
        }

        /// <summary>The room's walkable extent around the sleeper, logged with the stations so the
        /// ring can be judged against the floor plan it has to fit rather than guessed at.
        ///
        /// Passability alone is NOT enough to site an actor by, which cost a round: it said the
        /// starter farmhouse was walkable down to y 11 when the floor visibly stops at y 9, because
        /// the rows past the bottom wall are outside the room but still inside the map and carry no
        /// collision. <see cref="RoomGrid"/> is the one to read.</summary>
        private static string WalkableBox(GameLocation loc, Point centre)
        {
            int minX = int.MaxValue, maxX = int.MinValue, minY = int.MaxValue, maxY = int.MinValue;
            for (int dx = -10; dx <= 10; dx++)
            {
                for (int dy = -10; dy <= 10; dy++)
                {
                    int x = centre.X + dx, y = centre.Y + dy;
                    if (!loc.isTileOnMap(x, y)) continue;
                    bool passable;
                    try { passable = loc.isTilePassable(new xTile.Dimensions.Location(x, y), Game1.viewport); }
                    catch (Exception) { continue; }
                    if (!passable) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            return minX > maxX ? "none found" : $"x {minX}..{maxX}, y {minY}..{maxY}";
        }

        /// <summary>The room as the PLAYER sees it, one character per tile, logged around the
        /// sleeper so a station that comes out invisible can be explained instead of guessed at.
        ///
        /// Three things decide whether an actor standing on a tile is legible, and only the first is
        /// what <see cref="WalkableBox"/> measures:
        /// <list type="bullet">
        /// <item>a Back tile, or the tile is off the room entirely and the actor stands on black;</item>
        /// <item>no Front tile, which is the layer the game draws AFTER characters, so an actor under
        /// one is hidden behind the wall art (the designer's "I guess they're behind the wall instead
        /// of on-top of it");</item>
        /// <item>passability, which only decides whether it looks like floor or like furniture.</item>
        /// </list>
        ///
        /// Legend: <c>.</c> clear floor, <c>o</c> floor with something impassable on it, <c>F</c> a
        /// Front tile covers it, <c>#</c> no Back tile (off the room), <c>P</c> the sleeper.</summary>
        private const char NewLine = '\n';

        private static string RoomGrid(GameLocation loc, Point centre, int reach = 6)
        {
            xTile.Layers.Layer back = loc.map?.GetLayer("Back");
            xTile.Layers.Layer front = loc.map?.GetLayer("Front");
            if (back == null) return "no Back layer";
            var sb = new System.Text.StringBuilder();
            for (int y = centre.Y - reach; y <= centre.Y + reach; y++)
            {
                sb.Append(NewLine).Append("  y").Append(y.ToString().PadLeft(3)).Append(' ');
                for (int x = centre.X - reach; x <= centre.X + reach; x++)
                {
                    if (x == centre.X && y == centre.Y) { sb.Append('P'); continue; }
                    if (!loc.isTileOnMap(x, y) || Tile(back, x, y) == null) { sb.Append('#'); continue; }
                    if (Tile(front, x, y) != null) { sb.Append('F'); continue; }
                    bool passable;
                    try { passable = loc.isTilePassable(new xTile.Dimensions.Location(x, y), Game1.viewport); }
                    catch (Exception) { passable = false; }
                    sb.Append(passable ? '.' : 'o');
                }
            }
            return $"x {centre.X - reach}..{centre.X + reach}" + sb;
        }

        private static xTile.Tiles.Tile Tile(xTile.Layers.Layer layer, int x, int y)
        {
            if (layer == null) return null;
            if (x < 0 || y < 0 || x >= layer.LayerWidth || y >= layer.LayerHeight) return null;
            try { return layer.Tiles[x, y]; }
            catch (Exception) { return null; }
        }

        /// <summary>Beat 1: the farmer reads as asleep rather than standing at the top edge of the
        /// bed facing the wall with a hat on, as though they had walked into it (playtest
        /// 2026-09-11; the designer's word for the scene is "a dream"). Three pieces:
        /// <c>showFrame</c> holds vanilla's own lying-down frame, whose art has the eyes shut and
        /// whose PauseForSingleAnimation suppresses FarmerRenderer's separate eye pass; the hat comes
        /// off for the duration; and isInBed is set so anything reading it agrees with the picture.
        /// <see cref="WakeFarmer"/> puts all three back.</summary>
        /// <summary>Nudges the room up the screen, clear of the dialogue box. See
        /// <see cref="CameraLift"/>. <c>viewportFreeze</c> is what makes it stick: without it the
        /// engine re-centres a map this small on the very next tick.</summary>
        private void LiftCamera()
        {
            if (_cameraLifted) return;
            _cameraLifted = true;
            _priorViewportFreeze = Game1.viewportFreeze;
            _priorViewportY = Game1.viewport.Y;
            Game1.viewportFreeze = true;
            Game1.viewport.Y += CameraLift;
        }

        /// <summary>Puts the camera back. Runs inside <see cref="TeardownWorldState"/>'s once-only
        /// guard, so every exit path reaches it exactly once.</summary>
        private void DropCamera()
        {
            if (!_cameraLifted) return;
            _cameraLifted = false;
            Game1.viewport.Y = _priorViewportY;
            Game1.viewportFreeze = _priorViewportFreeze;
        }

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
            DropCamera();
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
