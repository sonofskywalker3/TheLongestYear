using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;

namespace TheLongestYear.DebugCommands
{
    /// <summary>Debug bridge only: checks whether a blocking garbage collection on the game thread
    /// would hang (Nexus posts, Celjhyn 2026-11-05: a mod that calls
    /// <c>GC.WaitForPendingFinalizers()</c> in its Saving handler froze the first save after a
    /// rewind). A <see cref="RenderTarget2D"/> that is collected without being disposed has its
    /// finalizer wait for the game thread (<c>Threading.BlockOnUIThread</c>), so if the game thread
    /// is itself waiting for finalizers, neither side ever moves.
    ///
    /// <c>tly_finalizerprobe</c> forces a full collection, then waits for the finalizers on a
    /// worker thread with a time limit, so the probe reports the deadlock instead of entering it.
    /// On a failure it lists the render targets queued for the game thread.
    /// <c>tly_finalizerprobe leak</c> instead makes one render target and drops it undisposed,
    /// standing in for a mod that leaves one behind, so the hang can be set up on purpose.</summary>
    internal static class FinalizerProbeCommand
    {
        public const string Name = "tly_finalizerprobe";
        public const string Usage = "Usage: tly_finalizerprobe [leak]";

        private const string LeakArg = "leak";
        private const int LeakTargetSize = 64;

        private const int ProbeTimeoutMs = 5000;
        private const string ThreadingTypeName = "Microsoft.Xna.Framework.Threading";
        private const string StateHelperTypeName = "StateActionHelper`1";
        private const string QueueFieldName = "Queue";
        private const string StateFieldName = "State";

        public static void Run(IMonitor monitor, string[] args)
        {
            if (args.Length > 0 && string.Equals(args[0], LeakArg, StringComparison.OrdinalIgnoreCase))
            {
                LeakRenderTarget();
                monitor.Log("tly_finalizerprobe: left one undisposed render target for the garbage collector.", LogLevel.Info);
                return;
            }

            GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true);
            Task wait = Task.Run(GC.WaitForPendingFinalizers);
            if (wait.Wait(ProbeTimeoutMs))
            {
                monitor.Log("tly_finalizerprobe: PASS. No finalizer is waiting on the game thread.", LogLevel.Info);
                return;
            }

            List<string> waiting = DescribeQueuedRenderTargets();
            string detail = waiting.Count == 0
                ? "nothing readable in the game thread's render target queue"
                : $"{waiting.Count} render target(s) waiting: {string.Join(", ", waiting)}";
            monitor.Log(
                $"tly_finalizerprobe: FAIL. Finalizers did not finish in {ProbeTimeoutMs} ms; {detail}. " +
                "A blocking collection on the game thread would hang here.",
                LogLevel.Warn);
        }

        /// <summary>Held only long enough to age the leaked target into the oldest generation.</summary>
        private static RenderTarget2D _aging;

        /// <summary>Makes a render target, lets it age into the oldest generation as a mod's
        /// long-lived one would, then drops it undisposed. Only a full collection finds it after
        /// that, which is what a blocking collection at save time runs.</summary>
        private static void LeakRenderTarget()
        {
            _aging = new RenderTarget2D(StardewValley.Game1.graphics.GraphicsDevice, LeakTargetSize, LeakTargetSize)
            {
                Name = "tly_finalizerprobe leak",
            };
            for (int i = 0; i < GC.MaxGeneration; i++)
                GC.Collect();
            _aging = null;
        }

        /// <summary>Reads the render targets whose disposal is queued for the game thread. The
        /// queue is internal to the framework, so this is reflection and best effort.</summary>
        private static List<string> DescribeQueuedRenderTargets()
        {
            var found = new List<string>();
            try
            {
                Type threading = typeof(GraphicsDevice).Assembly.GetType(ThreadingTypeName);
                Type helper = threading?.GetNestedType(StateHelperTypeName, BindingFlags.NonPublic | BindingFlags.Public);
                Type closed = helper?.MakeGenericType(typeof(RenderTarget2D));
                FieldInfo queueField = closed?.GetField(QueueFieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
                if (queueField?.GetValue(null) is not IEnumerable queue)
                    return found;

                foreach (object item in queue)
                {
                    FieldInfo stateField = item.GetType().GetField(StateFieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (stateField?.GetValue(item) is RenderTarget2D target)
                        found.Add($"'{target.Name ?? "unnamed"}' {target.Width}x{target.Height}");
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException or MemberAccessException or TargetException)
            {
                found.Add($"(queue unreadable: {ex.GetType().Name})");
            }
            return found;
        }
    }
}
