using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace TheLongestYear.Loop
{
    /// <summary>Clears out pending finalizers before the post-reset save, without the game thread
    /// ever blocking on them.
    ///
    /// The reset rebuilds the whole world, so anything another mod kept per location or per
    /// character becomes garbage at once. A <see cref="RenderTarget2D"/> collected undisposed has
    /// its finalizer wait for the game thread (<c>Threading.BlockOnUIThread</c>). A mod whose
    /// Saving handler calls <c>GC.WaitForPendingFinalizers()</c> on the game thread then waits for
    /// that finalizer, which is waiting for the game thread, and the save never returns (Nexus
    /// posts, Celjhyn 2026-11-05, Ultra Smooth's save-time memory cleanup on a 435 mod list).
    ///
    /// Here the wait happens on a worker thread while the game thread keeps running the
    /// framework's queued game-thread work, so those finalizers finish and the later handler finds
    /// nothing left to wait on. Best effort: something released after this runs, or a finalizer
    /// stuck on anything other than the game thread, is out of its reach. Reproduce with <c>tly_finalizerprobe leak</c> before the reset's
    /// last focus pick, with such a mod installed.</summary>
    internal static class FinalizerDrain
    {
        /// <summary>Longest the drain may hold the game thread. A finalizer stuck on something
        /// other than the game thread cannot be helped from here, so give up and carry on.</summary>
        private const int MaxDrainMs = 3000;
        private const int PollMs = 1;
        private const string ThreadingTypeName = "Microsoft.Xna.Framework.Threading";
        private const string RunMethodName = "Run";

        private static bool _resolved;
        private static Action _runQueuedWork;

        public static void Run(IMonitor monitor)
        {
            Action runQueuedWork = ResolveRunQueuedWork(monitor);
            if (runQueuedWork == null)
                return;

            var clock = Stopwatch.StartNew();
            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            Task wait = Task.Run(GC.WaitForPendingFinalizers);
            while (!wait.IsCompleted && clock.ElapsedMilliseconds < MaxDrainMs)
            {
                try
                {
                    runQueuedWork();
                }
                catch (Exception ex)
                {
                    // The queue holds other code's work, so anything can come out of it. The game
                    // would have met the same failure on its next frame; the save must still open.
                    monitor?.Log($"Day-28 save: queued game-thread work failed during the finalizer drain ({ex.GetType().Name}: {ex.Message}); saving anyway.", LogLevel.Warn);
                    return;
                }
                wait.Wait(PollMs);
            }

            monitor?.Log(
                wait.IsCompleted
                    ? $"Day-28 save: pending finalizers cleared in {clock.ElapsedMilliseconds} ms before the save."
                    : $"Day-28 save: finalizers still pending after {MaxDrainMs} ms; saving anyway.",
                wait.IsCompleted ? LogLevel.Trace : LogLevel.Warn);
        }

        /// <summary>The framework's "run everything queued for the game thread" call. It is
        /// internal, so it is found once by reflection; without it the drain is skipped, since
        /// waiting without it is the very hang this avoids.</summary>
        private static Action ResolveRunQueuedWork(IMonitor monitor)
        {
            if (_resolved)
                return _runQueuedWork;
            _resolved = true;
            try
            {
                MethodInfo run = typeof(GraphicsDevice).Assembly
                    .GetType(ThreadingTypeName)
                    ?.GetMethod(RunMethodName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
                if (run != null)
                    _runQueuedWork = (Action)Delegate.CreateDelegate(typeof(Action), run);
            }
            catch (Exception ex) when (ex is ArgumentException or AmbiguousMatchException or MemberAccessException)
            {
                monitor?.Log($"Day-28 save: finalizer drain unavailable ({ex.GetType().Name}: {ex.Message}).", LogLevel.Trace);
            }
            if (_runQueuedWork == null)
                monitor?.Log("Day-28 save: finalizer drain unavailable (the framework's game-thread queue was not found).", LogLevel.Trace);
            return _runQueuedWork;
        }
    }
}
