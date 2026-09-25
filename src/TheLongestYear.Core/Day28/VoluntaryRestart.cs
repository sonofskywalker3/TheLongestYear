namespace TheLongestYear.Core.Day28
{
    /// <summary>Why the Junimo Shrine's Restart the year button is hidden right now. None means it
    /// shows. <see cref="VoluntaryRestart.BlockedBy"/> checks in declaration order, so a log line
    /// names the most basic reason first.</summary>
    public enum RestartBlock
    {
        None,
        /// <summary>A Fail, Win or Restart chain is already queued or mid-way, or the game is already ending the day.</summary>
        ResetRunning,
        /// <summary>An event, cutscene, festival in progress or overnight farm event is playing.</summary>
        EventUp,
        /// <summary>Day 28: the real gate owns tonight (Fail, Continue or Win).</summary>
        SeasonEndDay,
    }

    /// <summary>The world facts the button depends on, read by the mod when the shrine opens and
    /// again when the player confirms.</summary>
    public readonly record struct RestartSituation(
        int DayOfMonth,
        bool EventUp,
        bool ResetRunning);

    /// <summary>Voluntary restart at the Junimo Shrine (spec 2026-09-24-voluntary-restart-design).
    /// A restart is a normal loop reset that skips the Junimo scene and happens tonight.</summary>
    public static class VoluntaryRestart
    {
        public static RestartBlock BlockedBy(RestartSituation s)
        {
            if (s.ResetRunning) return RestartBlock.ResetRunning;
            if (s.EventUp) return RestartBlock.EventUp;
            if (Calendar.IsMonthEnd(s.DayOfMonth)) return RestartBlock.SeasonEndDay;
            return RestartBlock.None;
        }

        public static bool IsOffered(RestartSituation s) => BlockedBy(s) == RestartBlock.None;

        /// <summary>After "Keep playing" the won-run flag silences later wins. A restart starts a
        /// loop that can be won again, like "Start a new loop" on the win screen. Returns whether
        /// the flag was set (and is now cleared). The Year 2 wall is disarmed with it, as the wall's
        /// own Loop again does: the restarted loop's Spring 1 is not the start of year 2.</summary>
        public static bool ClearWonRun(MetaState meta)
        {
            if (!meta.VictoryAcknowledged) return false;
            meta.VictoryAcknowledged = false;
            meta.Year2WallArmed = false;
            return true;
        }

        /// <summary>Branches whose morning rewinds the world: tonight's farm event is pointless and
        /// its end warp can orphan the chain.</summary>
        public static bool IsRewind(Day28Branch branch)
            => branch == Day28Branch.Fail || branch == Day28Branch.Restart;
    }
}
