using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The reversion scene (spec 2026-09-21). Ten seconds, no text: the Community Center
    /// from OUTSIDE, in Town, at night. Its windows glow like firelight, and a Shadow Brute, solid
    /// black, steps into the right hand one and works there side-on, busy at something and turned
    /// from the street: the reversion is what it is doing. The camera opens on the facade and
    /// tilts down to the dirt road below it. Shane, taking the long way home from the Saloon to
    /// clear his head, walks along that road past the hall. Level with it he stops dead, gives a
    /// small jump of shock, backs off two tiles still facing it, then hurries off home toward the
    /// square and Marnie's (Jeff, 2026-09-23 and 2026-09-25: he is not going to the hall, and he
    /// walks only the town's real paths). The slot comes undone at the jump, and the shot holds on
    /// the windows after he has gone.
    ///
    /// THE PLAYER LEARNS NOTHING ABOUT WHICH ROOM OR SLOT WAS HIT, and that is the point of shooting
    /// it from the street. This scene never opens, enters or draws the Community Center's interior,
    /// and nothing it paints is tied to the slot the strike takes.
    ///
    /// WHAT IS REAL. The building, the street, the lamps and Town's own night are the game's, and so
    /// are Shane's routes (<see cref="HallWalker"/>). The firelight and the figure behind the glass
    /// are painted (<see cref="SceneWindowGlow"/>), and Shane is drawn from his sheet, never the
    /// real NPC: the real one has a schedule that would put him back on the map at 6am wherever the
    /// scene left him.
    ///
    /// NOTHING HERE MAY STRAND THE NIGHT. No path route for Shane, or a sheet that will not load,
    /// means the scene plays without him at all, and only a Town with no map loaded calls the scene
    /// off. The reversion lands either way.</summary>
    internal sealed class HallScene : StrikeSceneBase
    {
        // ---------------------------------------------------------------- the timeline, in ms
        //
        // Ten seconds, the controller's cap (2026-09-25): Shane walks a real stretch of road at a
        // real walking pace, and the windows get a real hold after he has gone.

        private const int FadeInMs = 700;
        /// <summary>The camera opens on the whole facade and tilts down to the road below it,
        /// keeping the windows in the top of the frame.</summary>
        internal const int PanDownAtMs = 700;
        internal const int PanDownMs = 1400;
        /// <summary>When he stops dead on the road, level with the hall, and faces it. He walks in at
        /// a walking pace and starts early enough to get here, under the fade if need be.</summary>
        internal const int StopAtMs = 5000;
        /// <summary>The jump of shock, and the beat the slot comes undone on.</summary>
        internal const int JumpAtMs = 5300;
        internal const int JumpLengthMs = 300;
        /// <summary>How far off the ground the jump takes him, in SCREEN pixels. The brief asked for
        /// sixteen, which is four pixels of his sheet at the game's 4x draw scale, and on the first
        /// overnight frames that was not readable at all against a figure sixteen sheet pixels wide.
        /// Twenty eight is seven sheet pixels, which reads as a start without reading as a leap.</summary>
        private const float JumpHeightPixels = 28f;
        /// <summary>When he starts backing off down the road, and how long it takes.</summary>
        internal const int BackAwayAtMs = 5800;
        internal const int BackAwayMs = 800;
        /// <summary>When he turns and hurries off home. He is out of the shot about 700 ms later.</summary>
        internal const int RunOutAtMs = 6600;
        /// <summary>The hold on the windows with nobody in the shot.</summary>
        internal const int HoldAtMs = 7600;
        private const int FadeOutAtMs = 9200;
        private const int FadeOutLengthMs = 800;
        private const int SceneEndMs = 10000;

        // ---------------------------------------------------------------- the camera, in tiles

        /// <summary>The opening framing: the whole facade, roof to steps.</summary>
        private static readonly Vector2 FacadeCentre = new Vector2(50.5f, 18f);
        /// <summary>The road framing: the windows at the top of the frame, the dirt road below the
        /// hall at the bottom. Same column as the opening, so the move is a plain tilt.</summary>
        private static readonly Vector2 RoadCentre = new Vector2(50.5f, 24f);

        // ---------------------------------------------------------------- state

        private GameLocation _town;
        private SceneWindowGlow _windows;
        private HallWalker _shane;

        public HallScene(PendingStrike strike, bool skippable, IMonitor monitor, Action<bool> onFinished)
            : base(strike, skippable, monitor, onFinished) { }

        // ---------------------------------------------------------------- staging

        /// <inheritdoc />
        protected override bool Stage()
        {
            _town = Game1.getLocationFromName("Town");
            if (_town?.map == null)
            {
                Monitor.Log("Darkness: Town has no map loaded, so the strike waits for a later night, effect and scene both.", LogLevel.Info);
                return false;
            }

            // Where the frame will be once the camera is down on the road, which is where Shane's
            // routes are cut to. Then the opening framing on the facade.
            SceneCamera.CutTo(_town, HallFacade.DoorTile);
            SceneCamera.CenterOnPixel(_town, RoadCentre * SceneCamera.TileSize);
            Rectangle roadFrame = SceneCamera.FrameInTiles();
            SceneCamera.CenterOnPixel(_town, FacadeCentre * SceneCamera.TileSize);
            // The panes were measured off the abandoned facade. On a restored or Joja building they
            // would land on the wrong art, so nothing is lit there and the scene plays on the dark
            // front instead.
            bool restored = HallFacade.IsRestored;
            _windows = new SceneWindowGlow(
                HallFacade.OriginTile,
                restored ? Array.Empty<Rectangle>() : HallFacade.FrontWindows,
                HallFacade.FigureWindow,
                warning => Monitor.Log($"Darkness: {warning}.", LogLevel.Warn));
            _windows.AddLights();

            _shane = new HallWalker(_town, Monitor);
            _shane.Stage(roadFrame);

            Monitor.Log(
                $"Darkness: the hall is staged on the Community Center front at ({HallFacade.Tiles.X},{HallFacade.Tiles.Y}) to ({HallFacade.Tiles.Right - 1},{HallFacade.Tiles.Bottom - 1}) in Town, "
                + $"facade {(restored ? "RESTORED, so no window is lit (the panes were measured off the abandoned art)" : "abandoned")}, "
                + $"{_windows.Describe()}, {_shane.Describe()}. {HallFacade.DescribeMapWindowLights(_town)}",
                LogLevel.Trace);
            return true;
        }

        // ---------------------------------------------------------------- the beats

        /// <inheritdoc />
        protected override void Build(Timeline t)
        {
            Fade(FadeInMs, FadeOutAtMs, FadeOutLengthMs);
            t.At(JumpAtMs, () =>
            {
                Game1.playSound("dwop", -400);
                ApplyStrike();
            });
            t.At(HoldAtMs, () => Game1.playSound("shadowDie", -900));
            t.EndAt(SceneEndMs);
        }

        /// <summary>The base pumps Town for the scene on the real overnight path.</summary>
        protected override GameLocation SceneLocation => _town;

        /// <inheritdoc />
        protected override void Advance(int elapsed)
        {
            float tilt = SmoothStep((elapsed - PanDownAtMs) / (float)PanDownMs);
            SceneCamera.CenterOnPixel(_town, Vector2.Lerp(FacadeCentre, RoadCentre, tilt) * SceneCamera.TileSize);
            _shane?.Move(elapsed);
        }

        /// <summary>Eased 0 to 1, clamped, so the tilt starts and settles gently.</summary>
        private static float SmoothStep(float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return t * t * (3f - 2f * t);
        }

        /// <summary>The jump arc: up and back down again over <see cref="JumpLengthMs"/>, the way
        /// vanilla's own jump reads, nothing at either end.</summary>
        internal static float JumpLift(int intoJumpMs)
        {
            float across = Math.Max(0f, Math.Min(1f, intoJumpMs / (float)JumpLengthMs));
            return JumpHeightPixels * (float)Math.Sin(across * Math.PI);
        }

        // ---------------------------------------------------------------- painting

        /// <inheritdoc />
        protected override void Paint(SpriteBatch b)
        {
            _windows?.Paint(b, ElapsedMs);
            _shane?.Draw(b, ElapsedMs);
        }

        // ---------------------------------------------------------------- putting it back

        /// <inheritdoc />
        protected override void Cleanup()
        {
            _windows?.RemoveLights();
            _windows?.Dispose();
            SceneCamera.Restore();
        }
    }
}
