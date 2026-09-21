using System;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace TheLongestYear.Scenes
{
    /// <summary>Plays one strike scene in the middle of an ordinary day, for
    /// <c>tly_sabotage scene crows</c> (spec 2026-09-21 Task 7). Debug only.
    ///
    /// WHY NOT JUST SET Game1.farmEvent. The engine does tick a farm event outside the new-day flow
    /// (the branch at Game1.cs:3799 only wants <c>gameMode == 3</c>), so the scene WOULD play. The
    /// problem is how it ends: the same branch then sets <c>timeOfDay = 600</c>, warps the farmer to
    /// his bed, and calls <c>showEndOfNightStuff()</c>, which puts up the save menu and rolls the day
    /// over (Game1.cs:3801 to 3841). Handing the scene to that slot at noon would end the day every
    /// time it was watched. So the preview drives the same <c>FarmEvent</c> methods itself, off
    /// SMAPI's update and render events, and stops at the scene's own ending.
    ///
    /// The scene cannot tell the difference except where it must: <see cref="CrowsScene.tickUpdate"/>
    /// skips its hand-pumping of the clock and the location here, because the engine's own update is
    /// still running and would do all of it twice.</summary>
    internal sealed class ScenePreview
    {
        private readonly IModHelper _helper;
        private readonly IMonitor _monitor;
        private StrikeSceneBase _playing;

        public ScenePreview(IModHelper helper, IMonitor monitor)
        {
            _helper = helper ?? throw new ArgumentNullException(nameof(helper));
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
        }

        /// <summary>Is a preview on screen right now?</summary>
        public bool Playing => _playing != null;

        /// <summary>Start one. False when another is already running or the scene called itself
        /// off, in which case nothing was shown and nothing was left behind.</summary>
        public bool Play(StrikeSceneBase scene)
        {
            if (scene == null) throw new ArgumentNullException(nameof(scene));
            if (_playing != null)
            {
                _monitor.Log("A strike scene is already playing. Let it finish.", LogLevel.Warn);
                return false;
            }
            if (scene.setUp())
            {
                _monitor.Log("The scene called itself off, so there is nothing to watch.", LogLevel.Warn);
                return false;
            }
            _playing = scene;
            _helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            _helper.Events.Display.RenderedWorld += OnRenderedWorld;
            _helper.Events.Display.Rendered += OnRendered;
            return true;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            StrikeSceneBase scene = _playing;
            if (scene == null) return;
            if (!Context.IsWorldReady) { Stop("the world went away"); return; }
            bool done;
            try
            {
                done = scene.tickUpdate(Game1.currentGameTime ?? new GameTime());
            }
            catch (Exception ex)
            {
                _monitor.Log($"The strike scene preview threw. The scene is being stopped properly so the camera comes back. {ex}", LogLevel.Error);
                done = true;
            }
            if (done) Stop("the scene finished");
        }

        private void OnRenderedWorld(object sender, RenderedWorldEventArgs e)
        {
            try { _playing?.draw(e.SpriteBatch); }
            catch (Exception ex) { _monitor.Log($"The strike scene preview could not draw. {ex}", LogLevel.Error); }
        }

        private void OnRendered(object sender, RenderedEventArgs e)
        {
            try { _playing?.drawAboveEverything(e.SpriteBatch); }
            catch (Exception ex) { _monitor.Log($"The strike scene preview could not draw over the frame. {ex}", LogLevel.Error); }
        }

        /// <summary>Unhook, and make sure the scene itself has ENDED. A preview that simply stopped
        /// calling tickUpdate would leave the scene mid-run with the night camera held, the HUD off
        /// and the controls frozen, because Cleanup only runs from the scene's own ending. Abort is
        /// a no-op when the scene ended on its own.</summary>
        private void Stop(string why)
        {
            StrikeSceneBase scene = _playing;
            _playing = null;
            scene?.Abort(why);
            _helper.Events.GameLoop.UpdateTicked -= OnUpdateTicked;
            _helper.Events.Display.RenderedWorld -= OnRenderedWorld;
            _helper.Events.Display.Rendered -= OnRendered;
            _monitor.Log("Darkness: the strike scene preview is over.", LogLevel.Info);
        }
    }
}
