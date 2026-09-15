using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>What a strike does. Blight is two options so the even split is over four in Winter
/// (spec 2026-09-15 Part B, section 2.2).</summary>
public enum DarknessEvent { CropBlight, ChestBlight, Reversion, Tampering }

/// <summary>The one nightly roll (spec 2026-09-15 Part B, sections 2.1, 2.2, 2.6 and 1.5). Pure:
/// the glue supplies the run, the calendar and "can this option act tonight"; this decides.</summary>
public static class NightRoll
{
    private const int FirstWinterDay = 1;

    /// <summary>How many nights week 1 of a season has: the window the guaranteed Winter tamper
    /// lives in, and the day from which a Winter counts as reached (spec 2.6).</summary>
    public const int Week1Nights = 7;

    public static double SeasonChance(Season season) => season switch
    {
        Season.Summer => SabotageTuning.NightChanceSummer,
        Season.Fall => SabotageTuning.NightChanceFall,
        Season.Winter => SabotageTuning.NightChanceWinter,
        _ => 0.0,
    };

    /// <summary>The chance tonight: the season's value on a week's first roll, less five points per
    /// strike already taken this week.</summary>
    public static double ChanceTonight(RunState run, int weekOfYear, Season season)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        return run.DarknessChanceWeek == weekOfYear ? run.DarknessChance : SeasonChance(season);
    }

    /// <summary>A strike landed: drop the week's chance, starting the week if this is its first.</summary>
    public static void RecordStrike(RunState run, int weekOfYear, Season season)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        double current = ChanceTonight(run, weekOfYear, season);
        run.DarknessChanceWeek = weekOfYear;
        run.DarknessChance = Math.Max(0.0, current - SabotageTuning.NightChanceDecay);
    }

    /// <summary>What the season offers, in a fixed order.</summary>
    public static IReadOnlyList<DarknessEvent> Options(Season season) => season switch
    {
        Season.Summer => new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight },
        Season.Fall => new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight, DarknessEvent.Reversion },
        Season.Winter => new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight, DarknessEvent.Reversion, DarknessEvent.Tampering },
        _ => Array.Empty<DarknessEvent>(),
    };

    /// <summary>One event, evenly among the options that can act. An option that is capped, off,
    /// warded or has nothing fair to act on hands its share to the rest; none left means no strike.</summary>
    public static DarknessEvent? Pick(IReadOnlyList<DarknessEvent> options, Func<DarknessEvent, bool> canAct, Random rng)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (canAct is null) throw new ArgumentNullException(nameof(canAct));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        List<DarknessEvent> able = options.Where(canAct).ToList();
        return able.Count == 0 ? null : able[rng.Next(able.Count)];
    }

    /// <summary>The first Winter a save ever reaches tampers on Winter 1; every later Winter on a
    /// random night of week 1, fixed by the run seed.</summary>
    public static int GuaranteedTamperDay(int runSeed, bool firstWinterEver)
        => firstWinterEver ? FirstWinterDay : FirstWinterDay + SabotageSchedule.Rng(runSeed, 0, SabotageKind.Tampering).Next(Week1Nights);

    /// <summary>True on the guaranteed night and on every later week-1 night until it lands (a night
    /// with no fair replacement is skipped and retried, spec 2.6).</summary>
    public static bool IsGuaranteedTamperNight(RunState run, bool firstWinterEver, Season season, int dayOfMonth)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (season != Season.Winter || run.GuaranteedTamperDone) return false;
        if (dayOfMonth > Week1Nights) return false;
        return dayOfMonth >= GuaranteedTamperDay(run.Seed, firstWinterEver);
    }

    /// <summary>Does this hit ignore the fairness picker? Hard 10%, Extreme 30%, never on Easy or
    /// Normal, and never once this loop's roll is spent (Jeff, 2026-09-15).</summary>
    public static bool UnmoderatedFires(DifficultyStep level, bool spent, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        double chance = DarknessLevels.UnmoderatedChance(level);
        if (spent || chance <= 0.0) return false;
        return rng.NextDouble() < chance;
    }
}
