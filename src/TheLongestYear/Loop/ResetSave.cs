using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Network;

namespace TheLongestYear.Loop
{
    /// <summary>The game's own save menu, run for the save right after a loop reset. SMAPI raises
    /// GameLoop.Saving while a <see cref="SaveGameMenu"/> (or a subclass) is the active menu and
    /// GameLoop.Saved once it closes, so every mod gets its save hooks, and closing it runs
    /// vanilla's Game1.OnDayStarted like after any night save. The one change: a write that throws
    /// is caught and the menu closes, instead of hanging on "Saving..." forever.</summary>
    internal sealed class ResetSaveMenu : SaveGameMenu
    {
        /// <summary>Why the write failed, or null.</summary>
        public Exception Error { get; private set; }

        /// <summary>The write ran (SaveGame.IsProcessing was seen true).</summary>
        public bool Wrote { get; private set; }

        /// <summary>The write finished and the menu reached its closing pause.</summary>
        public bool Succeeded => Error == null && Wrote && quit;

        public override void update(GameTime time)
        {
            if (Error != null) return;
            try
            {
                base.update(time);
                if (SaveGame.IsProcessing) Wrote = true;
            }
            catch (Exception ex)
            {
                Error = ex;
                SaveGame.IsProcessing = false;
                Game1.game1.IsSaving = false;
                if (ReferenceEquals(Game1.activeClickableMenu, this))
                    Game1.exitActiveMenu();
            }
        }
    }

    /// <summary>The full save after a loop reset (batch-B notes §4: the on-disk save must hold the
    /// Spring 1 world before the player can quit). It runs through <see cref="ResetSaveMenu"/>, not
    /// a direct SaveGame.Save() drained in one tick: that skipped SMAPI's Saving and Saved, so mods
    /// that write their data on Saving (SpaceCore's custom data) did not, and the save failed for
    /// players with Unlockable Bundles (SMAPI log 2026-10-14).
    ///
    /// Static, not on the RunController: the deferred SaveLoaded that follows an in-place reset
    /// rebuilds the controller and the reset service, and a save still queued then must survive it.
    /// The old save folder is deleted only after a confirmed write; a failed or skipped save leaves
    /// the inner-file rename as the fallback (the save stays loadable, the next sleep rewrites it).</summary>
    internal static class ResetSave
    {
        /// <summary>A save menu that closed before it wrote anything is reopened this many times.</summary>
        private const int MaxOpenAttempts = 3;

        private static IMonitor _monitor;
        private static ResetSaveMenu _menu;
        private static bool _queued;
        private static int _attempts;
        private static Action _onWritten;
        private static Action _after;

        /// <summary>LocationWeather entries set aside for the write (see <see cref="DetachUnsaveableWeather"/>).</summary>
        private static readonly List<KeyValuePair<string, LocationWeather>> _detached = new();

        public static bool Pending => _menu != null || _queued;

        /// <summary>Save now if nothing is on screen, otherwise as soon as the screen is clear.
        /// <paramref name="onWritten"/> runs only after a confirmed write; <paramref name="after"/>
        /// runs once when the save is settled (written, failed or given up), or right away if the
        /// save has to wait for a busy screen.</summary>
        public static void Start(IMonitor monitor, Action onWritten, Action after)
        {
            if (Pending)
                monitor.Log("Day-28 save: a previous reset save was still pending; it is replaced by this one.", LogLevel.Warn);
            Clear();
            _monitor = monitor;
            _onWritten = onWritten;
            _after = after;
            _queued = true;
            if (!TryOpen())
            {
                monitor.Log(
                    $"Day-28 save: waiting for a clear screen (activeClickableMenu={Game1.activeClickableMenu?.GetType().Name ?? "none"}, " +
                    $"eventUp={Game1.eventUp}); the reset carries on and the save follows.", LogLevel.Info);
                RunAfter();
            }
        }

        /// <summary>Every tick: open a queued save, and settle one whose menu has gone without a
        /// Saved event (normally <see cref="OnSaved"/> settles it first, in the same tick).</summary>
        public static void Tick()
        {
            if (_queued) { TryOpen(); return; }
            if (_menu != null && !ReferenceEquals(Game1.activeClickableMenu, _menu))
                Settle("tick");
        }

        /// <summary>SMAPI's GameLoop.Saved. Runs before the DayStarted SMAPI raises with it, and
        /// before the deferred SaveLoaded, so the reset's tail (the planning hub, the meta save)
        /// lands before either reads the run state.</summary>
        public static void OnSaved()
        {
            if (_menu != null && !ReferenceEquals(Game1.activeClickableMenu, _menu))
                Settle("Saved");
        }

        /// <summary>Leaving the save: drop everything, putting any set-aside weather back first.</summary>
        public static void Clear()
        {
            ReattachWeather();
            _menu = null;
            _queued = false;
            _attempts = 0;
            _onWritten = null;
            _after = null;
        }

        private static bool TryOpen()
        {
            if (Game1.activeClickableMenu != null || Game1.eventUp || Game1.currentMinigame != null)
                return false;
            _queued = false;
            _attempts++;
            DetachUnsaveableWeather();
            _menu = new ResetSaveMenu();
            Game1.activeClickableMenu = _menu;
            _monitor?.Log(
                $"Day-28 save: opened the game's save menu (attempt {_attempts}) so every mod's Saving and Saved handlers run.",
                LogLevel.Info);
            return true;
        }

        private static void Settle(string via)
        {
            ResetSaveMenu menu = _menu;
            _menu = null;
            ReattachWeather();

            if (menu.Succeeded)
            {
                _monitor?.Log($"Day-28 save: full save written post-reset ({via}); the save folder is now consistent at Spring 1.", LogLevel.Info);
                Action written = _onWritten;
                _onWritten = null;
                written?.Invoke();
            }
            else if (menu.Error != null)
            {
                _monitor?.Log(
                    $"Day-28 save: the post-reset save failed ({menu.Error.GetType().Name}: {menu.Error.Message}). The save remains " +
                    "loadable via the inner-file rename; the next natural sleep will rewrite it.", LogLevel.Warn);
                _monitor?.Log($"Day-28 save: failure detail: {menu.Error}", LogLevel.Trace);
                _onWritten = null;
            }
            else if (!menu.Wrote && _attempts < MaxOpenAttempts)
            {
                _monitor?.Log(
                    $"Day-28 save: the save menu closed before it wrote (replaced by {Game1.activeClickableMenu?.GetType().Name ?? "nothing"}); " +
                    "it reopens once the screen is clear.", LogLevel.Warn);
                _queued = true;
                RunAfter();
                return;
            }
            else
            {
                _monitor?.Log(
                    "Day-28 save: the save menu closed without writing. The save remains loadable via the inner-file rename; " +
                    "the next natural sleep will rewrite it.", LogLevel.Warn);
                _onWritten = null;
            }
            _attempts = 0;
            RunAfter();
        }

        private static void RunAfter()
        {
            Action after = _after;
            _after = null;
            after?.Invoke();
        }

        /// <summary>XmlSerializer cannot write a type that is not public, so a mod that keeps one in
        /// netWorldState.LocationWeather breaks any save that runs while it is there (Unlockable
        /// Bundles keeps its bundle list there between DayStarted and DayEnding and strips it on
        /// DayEnding, so the night save is fine but a save mid-day throws). Set such entries aside
        /// for the write and put them back after, so the mod keeps working and the save succeeds.</summary>
        private static void DetachUnsaveableWeather()
        {
            var weather = Game1.netWorldState?.Value?.LocationWeather;
            if (weather == null) return;
            foreach (var pair in new List<KeyValuePair<string, LocationWeather>>(weather.Pairs))
            {
                if (pair.Value == null || pair.Value.GetType().IsVisible) continue;
                _detached.Add(pair);
                weather.Remove(pair.Key);
                _monitor?.Log(
                    $"Day-28 save: set aside location weather '{pair.Key}' ({pair.Value.GetType().FullName}) for the write; " +
                    "its type is not public, so the save cannot hold it. It goes back after the save.", LogLevel.Info);
            }
        }

        private static void ReattachWeather()
        {
            if (_detached.Count == 0) return;
            var weather = Game1.netWorldState?.Value?.LocationWeather;
            foreach (var pair in _detached)
            {
                if (weather == null || weather.ContainsKey(pair.Key)) continue;   // the mod already put its own back
                weather[pair.Key] = pair.Value;
            }
            _monitor?.Log($"Day-28 save: put back {_detached.Count} location weather entr{(_detached.Count == 1 ? "y" : "ies")} set aside for the write.", LogLevel.Trace);
            _detached.Clear();
        }
    }
}
