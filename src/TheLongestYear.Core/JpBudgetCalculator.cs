using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One concrete ingredient slot on the board, reduced to what the JP economy sees:
/// rarity + the earliest season index (0..3) the item can realistically be obtained.</summary>
public sealed record BudgetSlot(string ItemId, Rarity Rarity, int EarliestSeasonIndex);

/// <summary>One bundle on the board. <paramref name="VaultGold"/> &gt; 0 marks a Vault money
/// bundle (no item slots, no completion bonus, gold-scaled JP instead).</summary>
public sealed record BudgetBundle(string Room, string Name, int NumberOfSlots, IReadOnlyList<BudgetSlot> Slots, int VaultGold);

/// <summary>Per-season breakdown (index 0..3 = Spring..Winter) of the maximum JP one loop can
/// pay out under the "donate each slot as soon as it is obtainable" model, plus a theoretical
/// hoard ceiling. Diagnostics only — see <see cref="JpBudgetCalculator"/>.</summary>
public sealed class JpBudgetReport
{
    public long[] Donation { get; } = new long[Calendar.MonthsPerYear];
    public long[] SelectionBonus { get; } = new long[Calendar.MonthsPerYear];
    public long[] BundleBonus { get; } = new long[Calendar.MonthsPerYear];
    public long[] RoomBonus { get; } = new long[Calendar.MonthsPerYear];
    public long[] WeeklyQuest { get; } = new long[Calendar.MonthsPerYear];
    public long[] Checkpoint { get; } = new long[Calendar.MonthsPerYear];
    public long[] Vault { get; } = new long[Calendar.MonthsPerYear];
    public int[] SlotsBySeason { get; } = new int[Calendar.MonthsPerYear];

    public long SeasonTotal(int season) =>
        Donation[season] + SelectionBonus[season] + BundleBonus[season] + RoomBonus[season]
        + WeeklyQuest[season] + Checkpoint[season] + Vault[season];

    public long Total => Enumerable.Range(0, Calendar.MonthsPerYear).Sum(SeasonTotal);

    /// <summary>Everything donated in Winter at 4× (ignores checkpoint quotas — an upper bound
    /// on what any hoarding strategy could bank in one loop).</summary>
    public long HoardCeiling { get; set; }
}

/// <summary>
/// Computes the maximum JP attainable in one loop for a given board. Pure — the mod-side
/// <c>tly_jpbudget</c> command reduces the live BundleData + CcItem catalog to
/// <see cref="BudgetBundle"/>s and logs the result. Model (spec 2026-08-21 B5):
/// <list type="bullet">
///   <item>Each slot pays once (DonationObserver passes count=1) at the multiplier of its
///   EARLIEST obtainable season.</item>
///   <item>Selection (weekly-theme) bonus "at the cap": per season, the top
///   <c>WeeksPerMonth × goalSlotsPerWeek[season]</c> slot payouts get the extra from
///   <c>selectionBonusMultiplier</c>, rounded the way DonationService rounds.</item>
///   <item>A bundle completes in the season its NumberOfSlots-th earliest slot becomes
///   obtainable; a room completes when its last bundle does. Vault bundles pay gold/VaultGoldPerJp
///   (unscaled, counted in Spring) and have no completion bonus.</item>
///   <item>Weekly quest bonus every week; the three season checkpoints at the entering week.</item>
///   <item>No JP-boost upgrades applied (baseline economy).</item>
/// </list>
/// </summary>
public static class JpBudgetCalculator
{
    public static JpBudgetReport Compute(
        IReadOnlyList<BudgetBundle> bundles,
        JpSettings settings,
        double selectionBonusMultiplier,
        IReadOnlyList<int> goalSlotsPerWeekBySeason)
    {
        if (bundles == null) throw new ArgumentNullException(nameof(bundles));
        if (settings == null) throw new ArgumentNullException(nameof(settings));
        if (goalSlotsPerWeekBySeason == null) throw new ArgumentNullException(nameof(goalSlotsPerWeekBySeason));

        var calc = new JpCalculator(settings);
        var report = new JpBudgetReport();
        var slotJpBySeason = new List<long>[Calendar.MonthsPerYear];
        for (int s = 0; s < Calendar.MonthsPerYear; s++) slotJpBySeason[s] = new List<long>();
        var roomCompletionSeason = new Dictionary<string, int>(StringComparer.Ordinal);
        var allSlotJpAtWinter = new List<long>();
        long bundleBonusAtWinter = 0;

        foreach (BudgetBundle bundle in bundles)
        {
            if (bundle.VaultGold > 0)
            {
                report.Vault[0] += calc.VaultPayment(bundle.VaultGold);
                TrackRoom(roomCompletionSeason, bundle.Room, 0);
                continue;
            }
            if (bundle.Slots.Count == 0) continue;

            var seasons = new List<int>();
            foreach (BudgetSlot slot in bundle.Slots)
            {
                int s = Clamp(slot.EarliestSeasonIndex);
                long jp = calc.PerItem(slot.Rarity, WeekFor(s));
                report.Donation[s] += jp;
                report.SlotsBySeason[s]++;
                slotJpBySeason[s].Add(jp);
                allSlotJpAtWinter.Add(calc.PerItem(slot.Rarity, WeekFor(Calendar.MonthsPerYear - 1)));
                seasons.Add(s);
            }

            seasons.Sort();
            int need = Math.Min(Math.Max(bundle.NumberOfSlots, 1), seasons.Count);
            int completion = seasons[need - 1];
            report.BundleBonus[completion] += calc.BundleBonus(WeekFor(completion));
            bundleBonusAtWinter += calc.BundleBonus(WeekFor(Calendar.MonthsPerYear - 1));
            TrackRoom(roomCompletionSeason, bundle.Room, completion);
        }

        foreach (KeyValuePair<string, int> room in roomCompletionSeason)
            report.RoomBonus[room.Value] += calc.RoomBonus(WeekFor(room.Value));

        for (int s = 0; s < Calendar.MonthsPerYear; s++)
        {
            int cap = Calendar.WeeksPerMonth * GoalSlots(goalSlotsPerWeekBySeason, s);
            report.SelectionBonus[s] = SelectionExtra(slotJpBySeason[s], cap, selectionBonusMultiplier);
            report.WeeklyQuest[s] = Calendar.WeeksPerMonth * calc.WeeklyQuestBonus(WeekFor(s));
            if (s > 0)
                report.Checkpoint[s] = calc.CheckpointBonus(WeekFor(s));
        }

        int winter = Calendar.MonthsPerYear - 1;
        long winterSelectionCap = Calendar.WeeksPerMonth * GoalSlots(goalSlotsPerWeekBySeason, winter);
        report.HoardCeiling =
            allSlotJpAtWinter.Sum()
            + SelectionExtra(allSlotJpAtWinter, (int)winterSelectionCap, selectionBonusMultiplier)
            + bundleBonusAtWinter
            + roomCompletionSeason.Count * calc.RoomBonus(WeekFor(winter))
            + report.WeeklyQuest.Sum()
            + report.Checkpoint.Sum()
            + report.Vault.Sum();

        return report;
    }

    /// <summary>1-based week-of-year for the first week of a season index (0..3).</summary>
    public static int WeekFor(int seasonIndex) => seasonIndex * Calendar.WeeksPerMonth + 1;

    private static int Clamp(int seasonIndex) =>
        Math.Min(Math.Max(seasonIndex, 0), Calendar.MonthsPerYear - 1);

    private static int GoalSlots(IReadOnlyList<int> perSeason, int season) =>
        perSeason.Count == 0 ? 0 : perSeason[Math.Min(season, perSeason.Count - 1)];

    private static void TrackRoom(Dictionary<string, int> rooms, string room, int season)
    {
        if (!rooms.TryGetValue(room, out int existing) || season > existing)
            rooms[room] = season;
    }

    /// <summary>Extra JP from the selection multiplier on the <paramref name="cap"/> richest
    /// slots, using DonationService's rounding (round(base × mult) − base per slot).</summary>
    private static long SelectionExtra(List<long> slotJps, int cap, double multiplier)
    {
        if (cap <= 0 || multiplier <= 1.0) return 0;
        long extra = 0;
        foreach (long jp in slotJps.OrderByDescending(v => v).Take(cap))
            extra += (long)Math.Round(jp * multiplier, MidpointRounding.AwayFromZero) - jp;
        return extra;
    }
}
