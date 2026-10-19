using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    internal sealed partial class RunController
    {
        /// <summary>A short or empty offer (fewer than two cards even after the refill from this
        /// month's picks): log at Warn every unmet season-gate line no dealt theme can ask for this
        /// week, with the reason. Player report (0.19.97): the themes ran dry and the gate failed
        /// with nothing in the log saying which lines were stranded or why.</summary>
        private void LogUnaskableGateLines(
            int week, CoreSeason season, IReadOnlyList<Theme> offer, IReadOnlyCollection<Theme> picked)
        {
            var bundleData = Game1.netWorldState?.Value?.BundleData;
            if (bundleData == null || _requirements == null) return;

            var pools = new Dictionary<Theme, IReadOnlyList<BonusSlot>>();
            var samples = new Dictionary<Theme, IReadOnlyList<BonusSlot>>();
            foreach (Theme theme in Enum.GetValues(typeof(Theme)))
            {
                pools[theme] = SlotPoolBuilder.OpenSlotsForTheme(
                    bundleData, SlotStateForBundle, _requirements,
                    theme, season, id => IsObtainableInWeek(id, week), week, ItemKindOf, RouteBasisOf);
                samples[theme] = SampleSlotsForTheme(theme, season, week);
            }

            _monitor.Log(
                $"Week {week} offer is short ({offer.Count} card(s): [{string.Join(",", offer)}], " +
                $"picked this month: [{string.Join(",", picked)}]). Checking the {season} gate for lines no theme can ask this week.",
                LogLevel.Warn);

            SlotLedger ledger = Run.DonatedLedger();
            int unmet = 0, stranded = 0;
            foreach (BundleRequirement req in _requirements)
            {
                var (count, ids) = req.MissingForSeason(season, ledger);
                if (count == 0) continue;
                unmet++;
                foreach (string id in ids.Distinct(StringComparer.Ordinal))
                {
                    GateLineVerdict verdict = GateLineDiagnosis.Classify(
                        BlockerForWeek(id, week),
                        ThemesHolding(pools, req.BundleIndex, id),
                        ThemesHolding(samples, req.BundleIndex, id),
                        offer, picked);
                    if (verdict.Reason == UnaskableReason.Askable) continue;
                    stranded++;
                    _monitor.Log(
                        $"  {req.Name} (needs {count} more by {season} 28): {ItemLogName(id)} ({id}) - {GateLineDiagnosis.Describe(verdict)}",
                        LogLevel.Warn);
                }
            }
            string summary = unmet == 0
                ? $"Week {week}: the {season} gate has nothing left to fill."
                : stranded == 0
                    ? $"Week {week}: every unmet {season} gate line is still asked by a dealt theme."
                    : $"Week {week}: {stranded} unmet {season} gate line(s) in {unmet} bundle(s) no dealt theme can ask this week.";
            _monitor.Log(summary, stranded == 0 ? LogLevel.Info : LogLevel.Warn);
        }

        /// <summary>The empty-offer week: tell the player there is no theme, and what the season
        /// still needs (the same count the season gate reads).</summary>
        private void ShowNoThemeWeekMessage()
        {
            int due = GateLineDiagnosis.ItemsDue(_requirements ?? Array.Empty<BundleRequirement>(), Run.Season, Run.DonatedLedger());
            string season = Utility.getSeasonNameFromNumber((int)Run.Season);
            string text = GateLineDiagnosis.NoThemeWeekMessageFor(due) switch
            {
                NoThemeWeekMessage.GateMet => Strings.Get("hud.no-theme-week-met", new Dictionary<string, string> { ["season"] = season }),
                NoThemeWeekMessage.OneItemDue => Strings.Get("hud.no-theme-week-one", new Dictionary<string, string> { ["season"] = season }),
                _ => Strings.Get("hud.no-theme-week", new Dictionary<string, string> { ["count"] = due.ToString(), ["season"] = season }),
            };
            Game1.addHUDMessage(new HUDMessage(text, HUDMessage.newQuest_type));
            _monitor.Log($"No theme this week; HUD: \"{text}\" ({due} item(s) still due by {Run.Season} 28).", LogLevel.Warn);
        }

        /// <summary>The picks a stored re-roll may still show: this month's picks minus any the week's
        /// refill can deal again (offer or re-roll candidates). Without this, a re-rolled pair that
        /// holds a refilled theme would be thrown away on reopening the hub.</summary>
        internal IReadOnlyCollection<Theme> RerollExclusions(
            int week, CoreSeason season, IReadOnlyCollection<Theme> selections)
        {
            if (selections == null || selections.Count == 0 || Run.RerollWeek != week) return selections;
            var reusable = new HashSet<Theme>(OfferFor(week, season, selections)
                .Concat(OfferCandidates(week, season, selections)));
            return selections.Where(t => !reusable.Contains(t)).ToList();
        }

        private static List<Theme> ThemesHolding(
            Dictionary<Theme, IReadOnlyList<BonusSlot>> lists, int bundleIndex, string itemId)
            => lists.Where(kv => kv.Value.Any(s => s.BundleIndex == bundleIndex && s.ItemId == itemId))
                .Select(kv => kv.Key)
                .ToList();

        private static string ItemLogName(string itemId)
        {
            try { return ItemRegistry.GetData(BundleParsing.NormalizeItemId(itemId))?.DisplayName ?? itemId; }
            catch (Exception ex)
            {
                PatchLog.TraceOnce("offer-name:" + itemId, $"No display name for '{itemId}' ({ex.GetType().Name}); using the id.");
                return itemId;
            }
        }
    }
}
