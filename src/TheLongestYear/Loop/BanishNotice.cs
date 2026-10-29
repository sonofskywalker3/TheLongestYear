using System;
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The morning message for banishes a reset refunded (<see cref="BanishRules"/>). The
    /// notice stays in MetaState until the corner box actually shows it, and is cleared then; the
    /// game's next save stores the cleared state. A quit before the box shows keeps it for the next
    /// load, and the same message is never queued twice.</summary>
    internal static class BanishNotice
    {
        private const string Tag = "tly.banish-refund";

        /// <param name="state">Read when the box shows, not now: a deferred reload can replace the
        /// MetaState instance in between.</param>
        public static void QueueIfPending(Func<MetaState> state, IMonitor monitor)
        {
            if (MorningBox.IsQueued(Tag)) return;
            if (BanishRules.PeekNotice(state()) is not { } refunded) return;
            MorningBox.Enqueue(BanishText.RefundNotice(refunded), tag: Tag, onShown: () =>
            {
                BanishRules.TakeNotice(state());
                monitor.Log("Banish refund notice shown and cleared (stored with the next save).", LogLevel.Trace);
            });
            monitor.Log($"Banish refund notice queued for {refunded.Count} banish(es).", LogLevel.Info);
        }
    }
}
