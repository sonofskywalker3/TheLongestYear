using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One line of a donation batch: a quantity of items at a given rarity.</summary>
public readonly record struct DonationLine(Rarity Rarity, int Count);

/// <summary>
/// Computes Junimo Points. Per-item JP scales by rarity and the season multiplier
/// (spec 2026-05-26: Spring 1.0×, Summer 1.5×, Fall 2.5×, Winter 4.0×). Bundle and room
/// completion bonuses scale by the same multiplier and attach to vanilla bundles/rooms.
/// </summary>
public sealed class JpCalculator
{
    private readonly JpSettings _s;
    private readonly double _earned;

    /// <param name="settings">Base JP values and the season ramp.</param>
    /// <param name="earnedMultiplier">The difficulty profile's JP-earned factor (spec 2026-08-26).
    /// Applied to EVERY award: per-item, completion bonuses, and vault payments. 1.0 is the
    /// default and changes nothing. Callers pass the run's STAMPED profile value, so a GMCM
    /// change does not alter the season the player is already in.</param>
    public JpCalculator(JpSettings settings, double earnedMultiplier = 1.0)
    {
        _s = settings;
        _earned = earnedMultiplier;
    }

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

    /// <summary>Bonus JP awarded on weekly theme quest completion.</summary>
    public long WeeklyQuestBonus(int weekOfYear) => Scale(_s.WeeklyQuestCompletionBonus, weekOfYear);

    /// <summary>Season-checkpoint completion bonus, scaled at the ENTERING week
    /// (pass Run.WeekOfYear + 1 from the day-28 boundary).</summary>
    public long CheckpointBonus(int enteringWeekOfYear) =>
        Scale(_s.CheckpointCompletionBonus, enteringWeekOfYear);

    /// <summary>JP for paying a CC Vault money bundle, proportional to the gold spent
    /// (gold / VaultGoldPerJp, rounded, minimum 1). NOT season-multiplied.</summary>
    public long VaultPayment(int gold)
    {
        if (_s.VaultGoldPerJp <= 0) return 1;
        long jp = (long)Math.Round(
            (double)gold / _s.VaultGoldPerJp * _earned, MidpointRounding.AwayFromZero);
        return jp < 1 ? 1 : jp;   // paying the vault always pays SOMETHING, at every difficulty
    }

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

    /// <summary>The difficulty factor multiplies the season-scaled value rather than the base, so
    /// the season ramp's SHAPE (1x / 1.5x / 2.5x / 4x) is identical at every step and only its
    /// height moves.</summary>
    private long Scale(int baseValue, int weekOfYear)
        => (long)Math.Round(
            baseValue * Multiplier(weekOfYear) * _earned, MidpointRounding.AwayFromZero);
}
