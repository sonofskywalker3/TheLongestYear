using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>Blight and reversion: the effects and their reports.</summary>
    internal sealed partial class SabotageService
    {
        // ------------------------------------------------------------------ blight

        /// <summary>Kill <paramref name="crops"/> crops and take <paramref name="spoil"/> stored units
        /// now, then queue the report. Returns how many things were taken in all. The debug
        /// entry point (<c>tly_sabotage blight [crops] [spoil]</c>), which strikes at once and never
        /// goes through <see cref="Pending"/>.</summary>
        public int Blight(int crops, int spoil, Random rng)
            => ReportBlight(BlightPass.Strike(crops, rng), SpoilagePass.Strike(spoil, rng, DarknessLevels.StorageReachesEverything(Level)));

        /// <summary>Queue the morning report for what a blight took, and log it. Returns how many
        /// things were taken in all, 0 when nothing was.</summary>
        private int ReportBlight(int killed, SpoilagePass.Taken taken)
        {
            if (killed <= 0 && taken.Total <= 0)
            {
                _monitor.Log("Darkness: blight rolled but found nothing to strike or take.", LogLevel.Trace);
                return 0;
            }
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Blight, Count = killed,
                Stolen = taken.Stolen == null ? new List<StolenStack>() : MorningLines.Merge(taken.Stolen),
            });
            _monitor.Log($"Darkness: {killed} crop(s) struck down, {taken.Total} stored thing(s) stolen on {Run.Season} {Run.DayOfMonth}.", LogLevel.Info);
            return killed + taken.Total;
        }

        // ------------------------------------------------------------------ reversion

        /// <summary>Debug entry point (<c>tly_sabotage revert</c>): a fair pick at the current level, opened now.</summary>
        public bool Revert(Random rng)
        {
            DonatedSlot pick = PickFairReversion(rng);
            return pick != null && RevertSlot(pick);
        }

        /// <summary>Debug entry point (<c>tly_sabotage scene hall</c>): a fair pick at the current
        /// level, parked rather than opened, so the scene itself lands it at its own beat exactly as
        /// the overnight path would. Null when nothing may fairly be taken tonight, and then the
        /// caller decides whether to watch the scene against a no-op instead.</summary>
        public PendingStrike PrepareRevert(Random rng)
        {
            DonatedSlot pick = PickFairReversion(rng);
            if (pick == null) return null;
            _monitor.Log($"Darkness: {Strings.ItemName(pick.ItemId)} is the slot the hall scene will open (slot {pick.BundleIndex}/{pick.IngredientIndex}).", LogLevel.Info);
            return new PendingStrike(DarknessEvent.Reversion, () => RevertSlot(pick));
        }

        /// <summary>The fair reversion pick both debug entry points share: today's day of the year,
        /// the reversion deadline at the current level, and the save and obtainability model the
        /// fairness rule reads. Null when nothing may fairly come undone.</summary>
        private DonatedSlot PickFairReversion(Random rng)
        {
            int dayOfYear = Calendar.DayOfYear((int)Run.Season, Run.DayOfMonth);
            int deadline = FairnessRule.ReversionDeadline(dayOfYear, Level);
            SaveSnapshot save = SaveSnapshotReader.Read(msg => _monitor.Log(msg, LogLevel.Trace));
            ObtainabilityModel model = _obtainability();
            return PickReversion(rng, id => FairnessRule.Counts(id, dayOfYear, deadline, Level, save, model));
        }

        private DonatedSlot PickReversion(Random rng, Func<string, bool> fair)
        {
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            SlotLedger ledger = Run.DonatedLedger();
            DonatedSlot pick = fair == null
                ? ReversionRule.Pick(ledger, _requirements(), rng)
                : ReversionRule.Pick(ledger, _requirements(), fair, rng);
            if (pick == null)
                _monitor.Log("Darkness: reversion found no slot it may fairly empty.", LogLevel.Trace);
            return pick;
        }

        private bool RevertSlot(DonatedSlot pick)
        {
            if (!TheLongestYear.Integration.CcSlotWriter.TryUnfill(pick.BundleIndex, pick.IngredientIndex))
            {
                _monitor.Log($"Darkness: reversion could not open slot {pick.BundleIndex}/{pick.IngredientIndex} on the board.", LogLevel.Warn);
                return false;
            }
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            string bundleName = _requirements().FirstOrDefault(r => r.BundleIndex == pick.BundleIndex)?.Name ?? "";
            // What the morning callout names (designer, 2026-10-08): the slot's stack and the
            // bundle's name as its menu shows it, read off the live board.
            Dictionary<string, string> board = Game1.netWorldState?.Value?.BundleData;
            string key = board != null ? BundleDataTamper.KeyForIndex(board, pick.BundleIndex) : null;
            string value = key != null ? board[key] : null;
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Reversion, Count = 1, BundleName = bundleName, ItemId = pick.ItemId,
                Stack = value != null ? BundleDataTamper.StackAt(value, pick.IngredientIndex) : 0,
                BundleLabel = value != null ? BundleDataTamper.LabelOf(value) : "",
                OldFlavor = BoardFlavor(pick.BundleIndex, pick.IngredientIndex),
            });
            _monitor.Log($"Darkness: {Strings.ItemName(pick.ItemId)} came undone from {bundleName} (slot {pick.BundleIndex}/{pick.IngredientIndex}) on {Run.Season} {Run.DayOfMonth}.", LogLevel.Info);
            return true;
        }
    }
}
