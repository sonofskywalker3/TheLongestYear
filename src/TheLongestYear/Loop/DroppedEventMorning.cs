using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Clears what an event the night dropped left behind (<see cref="DroppedEventRule"/>):
    /// notes an event running on the farmer's map while the day ends, and on the next morning, when
    /// that event is gone, lowers the event flag and shows the HUD again. Runs before the day-start
    /// flow so the planning screens find a clear morning.</summary>
    internal static class DroppedEventMorning
    {
        private static IMonitor _monitor;
        // Per screen: the event flag and the HUD belong to each split-screen player.
        private static readonly StardewModdingAPI.Utilities.PerScreen<string> _startedAsDayEnded = new();

        public static void Init(IMonitor monitor) => _monitor = monitor;

        public static void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!Context.IsWorldReady || !RunActivation.IsActive) return;
            if (Game1.newDay && Game1.eventUp && Game1.CurrentEvent != null)
                _startedAsDayEnded.Value = $"{Game1.CurrentEvent.id} in {Game1.currentLocation?.Name ?? "an unknown map"}";
        }

        public static void OnReturnedToTitle(object sender, ReturnedToTitleEventArgs e) => _startedAsDayEnded.Value = null;

        public static void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            string dropped = _startedAsDayEnded.Value;
            _startedAsDayEnded.Value = null;
            if (dropped == null || !RunActivation.IsActive || Game1.currentLocation == null) return;

            DroppedEventRepair repair = DroppedEventRule.Decide(
                startedAsDayEnded: true,
                eventNow: Game1.CurrentEvent != null,
                busy: Game1.farmEvent != null || Game1.locationRequest != null || Game1.eventOver || Game1.newDay,
                eventFlagUp: Game1.eventUp,
                hudShown: Game1.displayHUD);
            if (!repair.Any) return;
            if (repair.LowerEventFlag) Game1.eventUp = false;
            if (repair.ShowHud) Game1.displayHUD = true;
            _monitor?.Log(
                $"Morning: event {dropped} started as the day ended and the night dropped it; " +
                $"{(repair.LowerEventFlag ? "lowered its event flag" : "its event flag was already down")}" +
                $"{(repair.ShowHud ? " and showed the HUD again" : "")}.", LogLevel.Info);
        }
    }
}
