using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Rewind;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Integration
{
    /// <summary>The bedroom beats' half of paint versus state, and the exact mirror of
    /// <see cref="RewindSpringPaint"/> at the other end of the sequence.
    ///
    /// The rewind opens on the dark wake frame of the morning AFTER the failed day, so the HUD is
    /// already showing the next date: a Spring 28 failure read "Summer 1, 6:00am" over a scene that
    /// is meant to be the night the year ran out (playtest 2026-09-11). This paints the failed day's
    /// night over it (<see cref="NightPaint.Values"/>) and HOLDS it every tick, for the same reason
    /// the Spring paint holds: the beats span several frames and a menu, and the location's own
    /// per-tick updates would otherwise put the real values back.
    ///
    /// It does NOT touch weather or lighting. The room's look belongs to
    /// <see cref="RewindNightLight"/>; this is only what the clock and calendar read.
    ///
    /// The pan (beat 10) takes the clock and the calendar over from here and drives them itself, so
    /// <see cref="Release"/> runs at the end of the bedroom rather than at the end of the
    /// sequence.</summary>
    internal static class RewindNightPaint
    {
        private static IMonitor _monitor;
        private static bool _registered;
        private static bool _holding;
        private static NightPaintValues _values;

        /// <summary>Wires the per-tick hold and the return-to-title safety net. Safe to call more
        /// than once; the subscriptions only happen on the first call.</summary>
        public static void Register(IMonitor monitor, IModHelper helper)
        {
            _monitor = monitor;
            if (_registered) return;
            _registered = true;
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => Release();
        }

        /// <summary>Paints the night of <paramref name="failed"/> day 28 now and holds it until
        /// <see cref="Release"/>. Idempotent.</summary>
        public static void Apply(CoreSeason failed)
        {
            _values = NightPaint.Values(failed);
            _holding = true;
            Paint();
        }

        /// <summary>Stops holding. Does not revert: whoever calls this is taking the clock and the
        /// calendar over (the pan does, immediately). Idempotent.</summary>
        public static void Release()
        {
            if (!_holding) return;
            _holding = false;
            _monitor?.Log("RewindNightPaint: released the failed night; the next beat owns the clock.", LogLevel.Info);
        }

        private static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_holding) return;
            // Dormancy (project rule): _holding is static and outlives the save it was set on.
            if (!RunActivation.IsActive) { Release(); return; }
            Paint();
        }

        private static void Paint()
        {
            // Core.Season and StardewValley.Season share Spring=0..Winter=3 (see GameEffortData.cs;
            // RewindSpringPaint uses the same cast).
            Game1.season = (StardewValley.Season)(int)_values.Season;
            Game1.dayOfMonth = _values.DayOfMonth;
            Game1.timeOfDay = _values.TimeOfDay;
        }
    }
}
