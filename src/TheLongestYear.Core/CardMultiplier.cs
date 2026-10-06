using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Spec sections 4 and 5: a per-card JP multiplier on the theme's goal pay, and about one week
/// in four a face-down card that shows only its (always generous) multiplier.</summary>
public static class CardMultiplier
{
    private const int StepHundredths = 5;
    private const int RandomLowHundredths = 50, RandomSteps = 21;   // 0.50 .. 1.50
    private const int MysteryLowHundredths = 125, MysterySteps = 11; // 1.25 .. 1.75
    private const int MysteryOneIn = 4;
    private const int SaltMultiplier = 0x51A7, SaltMysteryWeek = 0x6C3B, SaltMysteryValue = 0x7D4F;

    /// <summary>Every roll here seeds through <see cref="RollSeed"/>, so the two cards of a week (and the
    /// sealed value as rerolls change the sealed theme) roll independently (final review I1).</summary>
    private static Random Rng(int seed, int week, int salt, int theme = NoTheme) => RollSeed.Rng(seed, week, salt, theme);
    private const int NoTheme = -1;

    public static double For(int seed, int weekOfYear, Theme theme, bool random)
    {
        if (!random) return 1.0;
        int step = Rng(seed, weekOfYear, SaltMultiplier, (int)theme).Next(RandomSteps);
        return (RandomLowHundredths + step * StepHundredths) / 100.0;
    }

    public static bool IsMysteryWeek(int seed, int weekOfYear, bool enabled)
        => enabled && Rng(seed, weekOfYear, SaltMysteryWeek).Next(MysteryOneIn) == 0;

    /// <summary>The face-down card's position (0 left, 1 right). Depends on seed and week only, so a
    /// reroll of the themes keeps the same slot sealed. It is the SECOND draw of the mystery-week
    /// Random: a separate Random on a neighbouring seed (the old salt ^ 1) correlated with the
    /// mystery roll and sealed the right card about 95% of mystery weeks.</summary>
    public static int SealedSlot(int seed, int weekOfYear)
    {
        Random rng = Rng(seed, weekOfYear, SaltMysteryWeek);
        rng.Next(MysteryOneIn); // the IsMysteryWeek draw
        return rng.Next(2);
    }

    /// <summary>True when the card in <paramref name="slot"/> is face down this week.</summary>
    public static bool IsSealed(int seed, int weekOfYear, int slot, RandomizerSettings r, bool doubleWeek = false)
        => !doubleWeek && r.MysteryCard && IsMysteryWeek(seed, weekOfYear, true) && slot == SealedSlot(seed, weekOfYear);

    public static double Mystery(int seed, int weekOfYear, Theme theme)
    {
        int step = Rng(seed, weekOfYear, SaltMysteryValue, (int)theme).Next(MysterySteps);
        return (MysteryLowHundredths + step * StepHundredths) / 100.0;
    }

    public static double ForCard(int seed, int week, Theme theme, int slot, RandomizerSettings r, bool doubleWeek = false)
        => IsSealed(seed, week, slot, r, doubleWeek)
            ? Mystery(seed, week, theme)
            : For(seed, week, theme, r.RandomMultiplier);

    /// <summary>The label a log line may show for the face-down card (final review I2).</summary>
    public const string SealedLabel = "?";

    /// <summary>The offer in card order for an Info log line, with <see cref="SealedLabel"/> in place
    /// of the face-down theme so the SMAPI console never gives the mystery card away.</summary>
    public static IReadOnlyList<string> OfferLabels(IReadOnlyList<Theme> offer, int seed, int week, RandomizerSettings r, bool doubleWeek = false)
        => offer.Select((t, slot) => IsSealed(seed, week, slot, r, doubleWeek) ? SealedLabel : t.ToString()).ToList();

    /// <summary>True when any of the first <paramref name="cards"/> slots is face down this week.</summary>
    public static bool AnySealed(int cards, int seed, int week, RandomizerSettings r, bool doubleWeek = false)
        => Enumerable.Range(0, cards).Any(slot => IsSealed(seed, week, slot, r, doubleWeek));

    /// <summary>Display form: "1.25x", "0.5x", "1x".</summary>
    public static string Format(double multiplier)
        => multiplier.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "x";
}
