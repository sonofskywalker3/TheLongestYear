namespace TheLongestYear.Core
{
    /// <summary>Single source of truth for the win payoff's loop-count line, shared by the
    /// <c>VictoryMenu</c> screen and the post-win "keep playing?" prompt so the two (shown seconds
    /// apart in the same win sequence) can never drift. Both call sites supply their own surrounding
    /// lead-in text; this is just the one-loop-vs-many sentence.</summary>
    public static class WinSummary
    {
        /// <summary><paramref name="runNumber"/> is the attempt counter (incremented only on a full
        /// loop reset), so it reads as "which loop you won on". Loop 1 (or any non-positive defensive
        /// value) reads as a first-loop brag; later loops count the attempts.</summary>
        public static string LoopLine(int runNumber) =>
            runNumber <= 1
                ? "You restored it on your very first loop!"
                : $"It took {runNumber} loops.";
    }
}
