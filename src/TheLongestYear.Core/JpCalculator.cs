using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One line of a donation batch: a quantity of items at a given rarity.</summary>
public readonly record struct DonationLine(Rarity Rarity, int Count);

/// <summary>
/// Computes Junimo Points. Per-item JP scales by rarity and the season multiplier
/// (spec 2026-05-26: Spring 1.0×, Summer 1.5×, Fall 2.5×, Winter 4.0×). Bundle, room,
/// and championed-contract completion bonuses scale by the same multiplier.
/// Completion bonuses attach to vanilla bundles/rooms only — never to contracts/gates,
/// except the explicit CompletedContractBonus path for a championed contract.
/// </summary>
public sealed class JpCalculator
{
    private readonly JpSettings _s;

    public JpCalculator(JpSettings settings) => _s = settings;

    /// <summary>Season multiplier for a 1-based week-of-year (1..16).</summary>
    public double Multiplier(int weekOfYear)
    {
        int seasonIdx = (weekOfYear - 1) / Calendar.WeeksPerMonth;
        if (seasonIdx < 0) seasonIdx = 0;
        if (seasonIdx >= _s.SeasonMultipliers.Length) seasonIdx = _s.SeasonMultipliers.Length - 1;
        return _s.SeasonMultipliers[seasonIdx];
    }

    public long PerItem(Rarity rarity, int weekOfYear) => Scale(_s.BaseFor(rarity), weekOfYear);

    public long BundleBonus(int weekOfYear) => Scale(_s.BundleCompletionBonus, weekOfYear);

    public long RoomBonus(int weekOfYear) => Scale(_s.RoomCompletionBonus, weekOfYear);

    public long CompletedContractBonus(int weekOfYear) => Scale(_s.CompletedContractBonus, weekOfYear);

    public long ForDonationBatch(
        IEnumerable<DonationLine> lines,
        int weekOfYear,
        int bundlesCompleted,
        int roomsCompleted)
    {
        long total = 0;
        foreach (var line in lines)
            total += PerItem(line.Rarity, weekOfYear) * line.Count;
        total += (long)bundlesCompleted * BundleBonus(weekOfYear);
        total += (long)roomsCompleted * RoomBonus(weekOfYear);
        return total;
    }

    private long Scale(int baseValue, int weekOfYear)
        => (long)Math.Round(baseValue * Multiplier(weekOfYear), MidpointRounding.AwayFromZero);
}
