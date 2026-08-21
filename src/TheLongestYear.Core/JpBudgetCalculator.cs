using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One concrete ingredient slot on the board, reduced to what the JP economy sees:
/// rarity, the earliest season index (0..3) the item can realistically be obtained, and —
/// for PerItem bundles — the season it is pinned to (must be donated by that season's end).</summary>
public sealed record BudgetSlot(string ItemId, Rarity Rarity, int EarliestSeasonIndex, int? PinnedSeasonIndex = null);

/// <summary>One bundle on the board. <paramref name="VaultGold"/> &gt; 0 marks a Vault money
/// bundle (no item slots, no completion bonus, gold-scaled JP instead). Gate shape, when the
/// bundle has a classified requirement: <paramref name="SeasonalSeasonIndex"/> for Seasonal
/// bundles (every slot due that season), <paramref name="CumulativeQuota"/> for Percentage
/// bundles ([Sp,Su,Fa,Wi] cumulative minimums); PerItem bundles carry pins on their slots.
/// Null/none = no checkpoint pressure (e.g. The Missing).</summary>
public sealed record BudgetBundle(
    string Room, string Name, int NumberOfSlots, IReadOnlyList<BudgetSlot> Slots, int VaultGold,
    int? SeasonalSeasonIndex = null, IReadOnlyList<int>? CumulativeQuota = null);

/// <summary>Per-season breakdown (index 0..3 = Spring..Winter) of one donation model's payout.</summary>
public sealed class JpBudgetModel
{
    public long[] Donation { get; } = new long[Calendar.MonthsPerYear];
    public long[] SelectionBonus { get; } = new long[Calendar.MonthsPerYear];
    public long[] BundleBonus { get; } = new long[Calendar.MonthsPerYear];
    public long[] RoomBonus { get; } = new long[Calendar.MonthsPerYear];
    public int[] Slots { get; } = new int[Calendar.MonthsPerYear];
    public long Total => Donation.Sum() + SelectionBonus.Sum() + BundleBonus.Sum() + RoomBonus.Sum();
}

/// <summary>Maximum-JP report for one loop's board. Two donation models share the fixed awards
/// (weekly quests, checkpoints, vault). Diagnostics only — see <see cref="JpBudgetCalculator"/>.</summary>
public sealed class JpBudgetReport
{
    /// <summary>"Donate as soon as obtainable": every slot at its earliest season's multiplier.</summary>
    public JpBudgetModel Earliest { get; } = new();

    /// <summary>"Strong player": donate only what each checkpoint demands (cheapest obtainable
    /// slots first), hoard everything else for Winter's 4×.</summary>
    public JpBudgetModel Strong { get; } = new();

    public long[] WeeklyQuest { get; } = new long[Calendar.MonthsPerYear];
    public long[] Checkpoint { get; } = new long[Calendar.MonthsPerYear];
    public long[] Vault { get; } = new long[Calendar.MonthsPerYear];

    public long FixedAwards => WeeklyQuest.Sum() + Checkpoint.Sum() + Vault.Sum();
    public long FixedAwardsFor(int season) => WeeklyQuest[season] + Checkpoint[season] + Vault[season];

    public long EarliestTotal => Earliest.Total + FixedAwards;
    public long StrongTotal => Strong.Total + FixedAwards;

    /// <summary>Every payable slot at Winter's 4× plus all bonuses at 4× — ignores checkpoints
    /// entirely; an upper bound no real strategy can exceed.</summary>
    public long HoardCeiling { get; set; }

    /// <summary>Bundles whose checkpoint minimum could not be met from obtainable slots in
    /// some season under the strong model (a structurally impossible gate on this board).</summary>
    public List<string> ImpossibleGates { get; } = new();
}

/// <summary>
/// Computes the maximum JP attainable in one loop for a given board. Pure — the mod-side
/// <c>tly_jpbudget</c> command reduces the live BundleData + CcItem catalog + requirements
/// to <see cref="BudgetBundle"/>s and logs the result. Shared rules (spec 2026-08-21 B5):
/// <list type="bullet">
///   <item>A pick-X-of-Y bundle pays for at most X slots (the rest die on completion); each
///   paid slot pays once (DonationObserver passes count=1) at the multiplier of the season it
///   is donated in.</item>
///   <item>Selection (weekly-theme) bonus "at the cap": per season, the top
///   <c>WeeksPerMonth × goalSlotsPerWeek[season]</c> payouts donated that season get the extra
///   from <c>selectionBonusMultiplier</c>, rounded the way DonationService rounds.</item>
///   <item>A bundle completes in the season of its X-th donation; a room completes when its
///   last bundle does. Vault bundles pay gold/VaultGoldPerJp (unscaled, counted in Spring) and
///   have no completion bonus.</item>
///   <item>Weekly quest bonus every week; the three season checkpoints at the entering week.
///   No JP-boost upgrades applied (baseline economy).</item>
/// </list>
/// </summary>
public static class JpBudgetCalculator
{
    private const int Winter = Calendar.MonthsPerYear - 1;

    private readonly record struct Paid(long Jp, int Season);

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

        var earliestPaid = new List<Paid>();
        var strongPaid = new List<Paid>();
        var earliestRooms = new Dictionary<string, int?>(StringComparer.Ordinal);
        var strongRooms = new Dictionary<string, int?>(StringComparer.Ordinal);
        long ceilingSlots = 0, ceilingBundles = 0;
        var ceilingSlotJps = new List<long>();

        foreach (BudgetBundle bundle in bundles)
        {
            if (bundle.VaultGold > 0)
            {
                report.Vault[0] += calc.VaultPayment(bundle.VaultGold);
                TrackRoom(earliestRooms, bundle.Room, 0);
                TrackRoom(strongRooms, bundle.Room, 0);
                continue;
            }
            if (bundle.Slots.Count == 0) continue;
            int payable = Math.Min(Math.Max(bundle.NumberOfSlots, 1), bundle.Slots.Count);

            // --- earliest model: the first X slots to become obtainable (richer first on ties),
            // each at its earliest season ---
            List<Paid> e = bundle.Slots
                .Select(s => new Paid(calc.PerItem(s.Rarity, WeekFor(Clamp(s.EarliestSeasonIndex))), Clamp(s.EarliestSeasonIndex)))
                .OrderBy(p => p.Season).ThenByDescending(p => p.Jp)
                .Take(payable).ToList();
            earliestPaid.AddRange(e);
            Complete(report.Earliest, calc, earliestRooms, bundle.Room, e, payable);

            // --- strong model: gate minimums first (cheapest obtainable), hoard the rest ---
            List<Paid> s = AssignStrong(bundle, payable, calc, report.ImpossibleGates);
            strongPaid.AddRange(s);
            Complete(report.Strong, calc, strongRooms, bundle.Room, s, payable);

            // --- ceiling: the X richest slots at Winter ---
            var winterJps = bundle.Slots.Select(x => calc.PerItem(x.Rarity, WeekFor(Winter)))
                .OrderByDescending(v => v).Take(payable).ToList();
            ceilingSlots += winterJps.Sum();
            ceilingSlotJps.AddRange(winterJps);
            ceilingBundles += calc.BundleBonus(WeekFor(Winter));
        }

        Finish(report.Earliest, earliestPaid, earliestRooms, calc, selectionBonusMultiplier, goalSlotsPerWeekBySeason);
        Finish(report.Strong, strongPaid, strongRooms, calc, selectionBonusMultiplier, goalSlotsPerWeekBySeason);

        for (int season = 0; season < Calendar.MonthsPerYear; season++)
        {
            report.WeeklyQuest[season] = Calendar.WeeksPerMonth * calc.WeeklyQuestBonus(WeekFor(season));
            if (season > 0)
                report.Checkpoint[season] = calc.CheckpointBonus(WeekFor(season));
        }

        int roomCount = earliestRooms.Count;
        report.HoardCeiling =
            ceilingSlots
            + SelectionExtra(ceilingSlotJps, Calendar.WeeksPerMonth * GoalSlots(goalSlotsPerWeekBySeason, Winter), selectionBonusMultiplier)
            + ceilingBundles
            + roomCount * calc.RoomBonus(WeekFor(Winter))
            + report.FixedAwards;

        return report;
    }

    /// <summary>1-based week-of-year for the first week of a season index (0..3).</summary>
    public static int WeekFor(int seasonIndex) => seasonIndex * Calendar.WeeksPerMonth + 1;

    private static List<Paid> AssignStrong(BudgetBundle bundle, int payable, JpCalculator calc, List<string> impossible)
    {
        var paid = new List<Paid>();

        if (bundle.SeasonalSeasonIndex.HasValue)
        {
            // Every slot is due by that season's end; a later-obtainable slot is simply late.
            int due = Clamp(bundle.SeasonalSeasonIndex.Value);
            foreach (BudgetSlot slot in bundle.Slots.Take(payable))
            {
                int season = Math.Max(due, Clamp(slot.EarliestSeasonIndex));
                if (season > due) impossible.Add($"{bundle.Name}: {slot.ItemId} not obtainable by {(Season)due}");
                paid.Add(new Paid(calc.PerItem(slot.Rarity, WeekFor(season)), season));
            }
            return paid;
        }

        if (bundle.Slots.Any(s => s.PinnedSeasonIndex.HasValue) && bundle.CumulativeQuota == null)
        {
            // PerItem: pinned slots at their pin (or when first obtainable), the rest hoarded.
            foreach (BudgetSlot slot in bundle.Slots.Take(payable))
            {
                int season = slot.PinnedSeasonIndex.HasValue
                    ? Math.Max(Clamp(slot.PinnedSeasonIndex.Value), Clamp(slot.EarliestSeasonIndex))
                    : Winter;
                if (slot.PinnedSeasonIndex.HasValue && season > Clamp(slot.PinnedSeasonIndex.Value))
                    impossible.Add($"{bundle.Name}: {slot.ItemId} not obtainable by its pin {(Season)Clamp(slot.PinnedSeasonIndex.Value)}");
                paid.Add(new Paid(calc.PerItem(slot.Rarity, WeekFor(season)), season));
            }
            return paid;
        }

        // Percentage (or ungated): meet each cumulative minimum with the cheapest obtainable
        // slots, then fill the remaining payable slots in Winter with the richest leftovers.
        IReadOnlyList<int> quota = bundle.CumulativeQuota ?? new int[Calendar.MonthsPerYear];
        var remaining = bundle.Slots.ToList();
        for (int season = 0; season < Winter; season++)
        {
            int need = Math.Min(QuotaAt(quota, season), payable) - paid.Count;
            if (need <= 0) continue;
            var picks = remaining
                .Where(s => Clamp(s.EarliestSeasonIndex) <= season)
                .OrderBy(s => calc.PerItem(s.Rarity, WeekFor(season)))
                .Take(need).ToList();
            if (picks.Count < need)
                impossible.Add($"{bundle.Name}: {(Season)season} minimum {QuotaAt(quota, season)} exceeds the {paid.Count + picks.Count} obtainable slot(s)");
            foreach (BudgetSlot pick in picks)
            {
                remaining.Remove(pick);
                paid.Add(new Paid(calc.PerItem(pick.Rarity, WeekFor(season)), season));
            }
        }
        foreach (BudgetSlot pick in remaining
                     .OrderByDescending(s => calc.PerItem(s.Rarity, WeekFor(Winter)))
                     .Take(payable - paid.Count))
            paid.Add(new Paid(calc.PerItem(pick.Rarity, WeekFor(Winter)), Winter));
        return paid;
    }

    private static void Complete(JpBudgetModel model, JpCalculator calc, Dictionary<string, int?> rooms,
        string room, List<Paid> paid, int payable)
    {
        foreach (Paid p in paid)
        {
            model.Donation[p.Season] += p.Jp;
            model.Slots[p.Season]++;
        }
        if (paid.Count >= payable)
        {
            int completion = paid.Max(p => p.Season);
            model.BundleBonus[completion] += calc.BundleBonus(WeekFor(completion));
            TrackRoom(rooms, room, completion);
        }
        else
        {
            rooms[room] = null; // a bundle that never completes keeps its room incomplete
        }
    }

    private static void Finish(JpBudgetModel model, List<Paid> paid, Dictionary<string, int?> rooms,
        JpCalculator calc, double multiplier, IReadOnlyList<int> goalSlots)
    {
        foreach (KeyValuePair<string, int?> room in rooms)
            if (room.Value.HasValue)
                model.RoomBonus[room.Value.Value] += calc.RoomBonus(WeekFor(room.Value.Value));

        for (int season = 0; season < Calendar.MonthsPerYear; season++)
        {
            var jps = paid.Where(p => p.Season == season).Select(p => p.Jp).ToList();
            model.SelectionBonus[season] = SelectionExtra(jps, Calendar.WeeksPerMonth * GoalSlots(goalSlots, season), multiplier);
        }
    }

    private static int Clamp(int seasonIndex) => Math.Min(Math.Max(seasonIndex, 0), Winter);

    private static int QuotaAt(IReadOnlyList<int> quota, int season) =>
        quota.Count == 0 ? 0 : Math.Max(0, quota[Math.Min(season, quota.Count - 1)]);

    private static int GoalSlots(IReadOnlyList<int> perSeason, int season) =>
        perSeason.Count == 0 ? 0 : perSeason[Math.Min(season, perSeason.Count - 1)];

    /// <summary>Track a room's completion season; a null entry (never completes) is sticky.</summary>
    private static void TrackRoom(Dictionary<string, int?> rooms, string room, int season)
    {
        if (rooms.TryGetValue(room, out int? existing))
        {
            if (existing.HasValue && season > existing.Value) rooms[room] = season;
            return;
        }
        rooms[room] = season;
    }

    /// <summary>Extra JP from the selection multiplier on the <paramref name="cap"/> richest
    /// payouts, using DonationService's rounding (round(base × mult) − base per slot).</summary>
    private static long SelectionExtra(IEnumerable<long> slotJps, int cap, double multiplier)
    {
        if (cap <= 0 || multiplier <= 1.0) return 0;
        long extra = 0;
        foreach (long jp in slotJps.OrderByDescending(v => v).Take(cap))
            extra += (long)Math.Round(jp * multiplier, MidpointRounding.AwayFromZero) - jp;
        return extra;
    }
}
