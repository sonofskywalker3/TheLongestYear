using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The stash nesting rule shared by both refusal hooks (the Chest.addItem prefix and
    /// the stash inventory's slot watcher): which nested items block a deposit, and the HUD line,
    /// shown at most once per HudIntervalMs so two hooks or repeated clicks never stack messages.</summary>
    internal static class StashNestingRefusal
    {
        private const long HudIntervalMs = 2000;
        private static readonly HudThrottle Hud = new(HudIntervalMs);
        private static readonly IReadOnlyList<string> NothingBlocked = new string[0];

        /// <summary>The non-cosmetic items nested in <paramref name="item"/>, empty when it may go
        /// in. Fails open: a mod item whose contents cannot be read is let through, logged.</summary>
        internal static IReadOnlyList<string> Blocked(Item item)
        {
            if (item == null)
                return NothingBlocked;
            try
            {
                return StashNesting.NonCosmeticNested(StashItemCodec.ToRecord(item));
            }
            catch (System.Exception ex)
            {
                // Exception on purpose: ToRecord reads mod item types by reflection. Failing closed
                // would trap a mod container outside the stash for good; letting it in loses nothing.
                StashItemCodec.Monitor?.Log($"StashNestingRefusal: could not read '{item.QualifiedItemId}' ({ex.GetType().Name}: {ex.Message}); letting it in.", LogLevel.Warn);
                return NothingBlocked;
            }
        }

        internal static void ShowHud()
        {
            if (Hud.TryFire(System.Environment.TickCount64))
                Game1.showRedMessage(Strings.Get("hud.stash-nesting-refused"));
        }
    }
}
