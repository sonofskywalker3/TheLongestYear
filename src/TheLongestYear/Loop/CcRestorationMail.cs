using System.Collections.Generic;
using StardewValley;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// The CC/Joja room-completion mail that, sitting in <c>mailForTomorrow</c> overnight, makes
    /// vanilla play that room's restoration <c>WorldChangeEvent</c> the next morning — the Junimos
    /// fixing the bus, greenhouse, minecarts, etc. (decompile <c>Utility.pickFarmEvent</c>,
    /// Utility.cs:4369-4416, each gated only on the mail flag, never on the date).
    ///
    /// Two consumers need this list, so it lives here (not duplicated):
    ///  - <see cref="TheLongestYear.Loop.RunController"/> strips it at day-end on a FAIL loop so the
    ///    rewind-doomed scene never plays (you don't watch the bus get fixed seconds before the loop
    ///    breaks it again).
    ///  - <see cref="WorldResetService.PerformReset"/> purges it from the post-reset world so a
    ///    <c>ccVault</c> queued just before the rewind can't survive in <c>mailForTomorrow</c> and
    ///    fire on the fresh run (the "bus fixed with 0 bundles done" carryover, 2026-06-08).
    ///
    /// Each entry is stored as the bare key OR key + the NO_LETTER_MAIL suffix (a flag with no
    /// letter); <c>pickFarmEvent</c> matches both, so both forms are removed.
    /// </summary>
    internal static class CcRestorationMail
    {
        public const string NoLetterSuffix = "%&NL&%";

        public static readonly string[] Keys =
        {
            "ccPantry", "ccVault", "ccBoilerRoom", "ccCraftsRoom", "ccFishTank", "ccMovieTheater",
            "jojaPantry", "jojaVault", "jojaBoilerRoom", "jojaCraftsRoom", "jojaFishTank",
            "jojaMovieTheater", "ccMovieTheaterJoja",
        };

        /// <summary>Remove every CC-restoration entry (both forms) from the farmer's
        /// <c>mailForTomorrow</c>. Returns the entries actually removed (for logging); empty when
        /// there was nothing pending.</summary>
        public static List<string> PurgeFromMailForTomorrow(Farmer who)
        {
            var stripped = new List<string>();
            if (who == null)
                return stripped;

            foreach (string key in Keys)
            {
                foreach (string form in new[] { key, key + NoLetterSuffix })
                {
                    if (who.mailForTomorrow.Contains(form))
                    {
                        who.mailForTomorrow.Remove(form);
                        stripped.Add(form);
                    }
                }
            }
            return stripped;
        }
    }
}
