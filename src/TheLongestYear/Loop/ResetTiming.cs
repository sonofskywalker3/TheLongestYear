using System.Diagnostics;
using StardewModdingAPI;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Stage stopwatch for the loop reset path. Added for the "closing the post-loop shrine freezes
    /// the game for about 3 seconds" investigation (TODO, Jeff 2026-09-14): every stage of
    /// <c>WorldResetService.PerformReset</c> and <c>RunController.FinalizeReset</c> logs how long it
    /// took, so the freeze can be attributed from the SMAPI log instead of guessed at.
    /// </summary>
    internal sealed class ResetTiming
    {
        private readonly IMonitor _monitor;
        private readonly string _scope;
        private readonly Stopwatch _total = Stopwatch.StartNew();
        private readonly Stopwatch _stage = Stopwatch.StartNew();

        public ResetTiming(IMonitor monitor, string scope)
        {
            _monitor = monitor;
            _scope = scope;
        }

        /// <summary>Logs the time since the previous mark (or construction) under <paramref name="stage"/>.</summary>
        public void Mark(string stage)
        {
            _monitor.Log($"Reset timing [{_scope}] {stage}: {_stage.ElapsedMilliseconds} ms", LogLevel.Info);
            _stage.Restart();
        }

        /// <summary>Logs the total elapsed time since construction.</summary>
        public void Total()
        {
            _monitor.Log($"Reset timing [{_scope}] total: {_total.ElapsedMilliseconds} ms", LogLevel.Info);
        }
    }
}
