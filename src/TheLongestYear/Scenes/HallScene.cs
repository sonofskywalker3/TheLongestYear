using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>The reversion scene (spec 2026-09-21). About seven seconds, no text: the Community
    /// Center from OUTSIDE, in Town, at night. Its windows glow like firelight and the silhouettes of
    /// men cross them. Shane, walking past on his way home from the Saloon to clear his head, stops
    /// dead on the path, gives a small jump of shock, backs away two tiles still facing the hall,
    /// then turns and hurries off toward Marnie's (Jeff, 2026-09-23: he is not going to the hall).
    /// The slot comes undone at the jump.
    ///
    /// THE PLAYER LEARNS NOTHING ABOUT WHICH ROOM OR SLOT WAS HIT, and that is the point of shooting
    /// it from the street. This scene never opens, enters or draws the Community Center's interior,
    /// and nothing it paints is tied to the slot the strike takes.
    ///
    /// WHAT IS REAL. The building, the street, the lamps and Town's own night are the game's, and so
    /// are Shane's routes (<see cref="HallWalker"/>). The firelight and the figures behind the glass
    /// are painted (<see cref="SceneWindowGlow"/>), and Shane is drawn from his sheet, never the
    /// real NPC: the real one has a schedule that would put him back on the map at 6am wherever the
    /// scene left him.
    ///
    /// NOTHING HERE MAY STRAND THE NIGHT. No route for Shane means he simply stands in view, a sheet
    /// that will not load means the scene plays without him at all, and only a Town with no map
    /// loaded calls the scene off. The reversion lands either way.</summary>
    internal sealed class HallScene : StrikeSceneBase
    {
        // ---------------------------------------------------------------- the timeline, in ms

        private const int FadeInMs = 700;
        /// <summary>The latest he starts walking in. A long way in starts earlier, under the fade,
        /// rather than being hurried. The glow and the figures are already running.</summary>
        internal const int WalkInAtMs = 1500;
        /// <summary>When he stops dead on the path and faces the hall.</summary>
        internal const int StopAtMs = 3000;
        /// <summary>The jump of shock, and the beat the slot comes undone on.</summary>
        internal const int JumpAtMs = 3300;
        internal const int JumpLengthMs = 300;
        /// <summary>How far off the ground the jump takes him, in SCREEN pixels. The brief asked for
        /// sixteen, which is four pixels of his sheet at the game's 4x draw scale, and on the first
        /// overnight frames that was not readable at all against a figure sixteen sheet pixels wide.
        /// Twenty eight is seven sheet pixels, which reads as a start without reading as a leap.</summary>
        private const float JumpHeightPixels = 28f;
        /// <summary>When he starts backing away, how long it takes, and how far he goes.</summary>
        internal const int BackAwayAtMs = 3800;
        internal const int BackAwayMs = 800;
        internal const int BackAwayTiles = 2;
        /// <summary>When he turns and hurries off home.</summary>
        internal const int RunOutAtMs = 4600;
        /// <summary>The hold on the windows with nobody in the shot.</summary>
        internal const int HoldAtMs = 5400;
        private const int FadeOutAtMs = 6200;
        private const int FadeOutLengthMs = 800;
        private const int SceneEndMs = 7000;

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
                Monitor.Log("Darkness: Town has no map loaded, so the reversion lands with no scene.", LogLevel.Info);
                return false;
            }

            SceneCamera.CutTo(_town, HallFacade.CameraTile);
            // The panes were measured off the abandoned facade. On a restored or Joja building they
            // would land on the wrong art, so nothing is lit there and the scene plays on the dark
            // front instead.
            bool restored = HallFacade.IsRestored;
            _windows = new SceneWindowGlow(
                HallFacade.OriginTile,
                restored ? Array.Empty<Rectangle>() : HallFacade.FrontWindows,
                warning => Monitor.Log($"Darkness: {warning}.", LogLevel.Warn));
            _windows.AddLights();

            _shane = new HallWalker(_town, Monitor);
            _shane.Stage();

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
            _shane?.Move(elapsed);
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
