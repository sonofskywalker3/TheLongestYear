using System;

namespace TheLongestYear.Core;

/// <summary>The one definition of how a quantity ask rolls off a basis. Jeff's ruling, 2026-09-04,
/// generalising the forage bands of 2026-08-30: the basis is what a season realistically yields;
/// 80% of it is the hard ceiling nothing rolls above; each difficulty step rolls inside its own
/// band of the basis. Easy 10-30%, Normal 20-50%, Hard 50-65%, Extreme 65-80%.
///
/// A gold ask takes three quarters of the roll. Fish quality is cast distance and level, not
/// luck (BobberBar: size = distance/5 x level roll, gold from level 6 on a full cast), so the cut
/// is a hedge for the early loop rather than a per-fish statistic.
///
/// The band edges are stamped on the <see cref="DifficultyProfile"/> as values, like every other
/// modifier, so a later retune of what "Hard" means cannot move an in-flight board.</summary>
public static class AskBands
{
    public const double EasyLow = 0.10, EasyHigh = 0.30;
    public const double NormalLow = 0.20, NormalHigh = 0.50;
    public const double HardLow = 0.50, HardHigh = 0.65;
    public const double ExtremeLow = 0.65, ExtremeHigh = 0.80;

    /// <summary>Nothing rolls above this fraction of the basis, whatever the step.</summary>
    public const double Ceiling = 0.80;

    /// <summary>Fraction of the rolled stack a gold-quality ask keeps.</summary>
    public const double GoldFactor = 0.75;

    public static (double Low, double High) For(DifficultyStep step)
        => step switch
        {
            DifficultyStep.Easy => (EasyLow, EasyHigh),
            DifficultyStep.Hard => (HardLow, HardHigh),
            DifficultyStep.Extreme => (ExtremeLow, ExtremeHigh),
            _ => (NormalLow, NormalHigh),
        };

    /// <summary>A uniform integer roll between the band's edges of the basis. The basis is rounded
    /// to a whole number first and each edge rounds UP, so a basis of 8 on Extreme rolls 6 to 7 and
    /// on Easy 1 to 3; the high edge never exceeds <see cref="Ceiling"/> of the basis.</summary>
    public static int Roll(double basis, DifficultyProfile profile, Random rng)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (rng == null) throw new ArgumentNullException(nameof(rng));
        int whole = Math.Max(1, (int)Math.Round(basis, MidpointRounding.AwayFromZero));
        double highFraction = Math.Min(profile.AskBandHigh, Ceiling);
        int low = Math.Max(1, (int)Math.Ceiling(whole * profile.AskBandLow));
        int high = Math.Max(low, (int)Math.Ceiling(whole * highFraction));
        return rng.Next(low, high + 1);
    }

    /// <summary>The stack a gold-quality ask keeps: three quarters, rounded, never below one.</summary>
    public static int ForGold(int stack)
        => Math.Max(1, (int)Math.Round(stack * GoldFactor, MidpointRounding.AwayFromZero));
}
