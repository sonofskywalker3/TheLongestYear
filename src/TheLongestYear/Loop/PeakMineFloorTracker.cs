using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Updates <see cref="RunState.PeakMineFloor"/> when the local player enters a deeper
    /// MineShaft. Feeds the cap-not-grant calculation for keep_mine_elevator_N on the
    /// next reset (Plan 06A persistence design §B).
    /// Only responds to the local player — farmhand warps in multiplayer/split-screen are
    /// ignored via the <see cref="WarpedEventArgs.IsLocalPlayer"/> guard.
    /// </summary>
    internal sealed class PeakMineFloorTracker
    {
        private readonly IMonitor _monitor;
        private readonly RunState _run;

        public PeakMineFloorTracker(IMonitor monitor, RunState run)
        {
            _monitor = monitor;
            _run = run;
        }

        public void OnWarped(object sender, WarpedEventArgs e)
        {
            if (!e.IsLocalPlayer)
                return;
            if (e.NewLocation is not MineShaft mine)
                return;
            int before = _run.PeakMineFloor;
            _run.RecordMineFloor(mine.mineLevel);
            if (_run.PeakMineFloor != before)
                _monitor.Log(
                    $"PeakMineFloor advanced to {_run.PeakMineFloor} (entered MineShaft level {mine.mineLevel}).",
                    LogLevel.Trace);
        }
    }
}
