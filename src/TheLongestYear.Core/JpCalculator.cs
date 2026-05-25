using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One line of a donation batch: a quantity of items at a given rarity.</summary>
public readonly record struct DonationLine(Rarity Rarity, int Count);

/// <summary>
/// Computes Junimo Points. Per-item JP scales by rarity and how deep into the year you are.
/// Completion bonuses attach to vanilla bundles/rooms only — never to contracts/gates (no double-dip).
/// </summary>
public sealed class JpCalculator
{
    private readonly JpSettings _s;

    public JpCalculator(JpSettings settings) => _s = settings;

    public double Multiplier(int weekOfYear) => 1.0 + (weekOfYear - 1) * _s.WeekDepthStep;

    public long PerItem(Rarity rarity, int weekOfYear)
        => (long)Math.Round(_s.BaseFor(rarity) * Multiplier(weekOfYear), MidpointRounding.AwayFromZero);

    public long ForDonationBatch(
        IEnumerable<DonationLine> lines,
        int weekOfYear,
        int bundlesCompleted,
        int roomsCompleted)
    {
        long total = 0;
        foreach (var line in lines)
            total += PerItem(line.Rarity, weekOfYear) * line.Count;
        total += (long)bundlesCompleted * _s.BundleCompletionBonus;
        total += (long)roomsCompleted * _s.RoomCompletionBonus;
        return total;
    }
}
