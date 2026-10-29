using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>The Herd Book's place in the rewind night (spec 2026-09-25, section 4): after the
    /// Cookbook and Craftbook, open the book on its Herd Book tab when an owned slot is empty and an
    /// animal on the farm fits it, then continue to the reset once it closes. Same watchdog as the
    /// other tabs and the shrine.</summary>
    internal sealed partial class RunController
    {
        private void OfferHerdBook(System.Action onContinue)
        {
            MetaState meta = _store.State;
            if (Game1.player == null || meta == null || _launcher == null) { onContinue(); return; }

            IReadOnlyList<HerdSlotKind> slots = HerdBookRules.SlotsFor(meta);
            List<HerdAnimal> live = HerdBookService.LiveHerdAnimals();
            int registered = HerdBookRules.Used(meta.HerdBook, slots.Count);
            if (!HerdBookRules.ShouldOfferAtReset(slots, meta.HerdBook, live))
            {
                _monitor.Log($"Herd Book not offered before the reset: slots={slots.Count}, registered={registered}, animals={live.Count}.", LogLevel.Trace);
                onContinue();
                return;
            }

            OfferBookTab(TheLongestYear.Core.Interactables.BookTab.HerdBook, "Herd Book",
                $"slots={slots.Count}, registered={registered}, animals={live.Count}", onContinue);
        }
    }
}
