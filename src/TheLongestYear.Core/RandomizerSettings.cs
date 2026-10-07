namespace TheLongestYear.Core;

/// <summary>How the weekly theme reroll button behaves. Spec 2026-09-28-randomizer-design, section 1.</summary>
public enum RerollMode { Off, CostsJp, Free }

/// <summary>The Randomizer section of the settings. Every option trades the shipped balance for variety,
/// and every option is off by default, so a player who never opens the section plays the balanced game.
/// Weekly options are read through <see cref="RunState.RandomizerFor"/>, never live, so a flip mid-week
/// waits for the next weekly offer.</summary>
public sealed class RandomizerSettings
{
    public RerollMode Rerolls { get; set; } = RerollMode.Off;
    public bool RandomThemeItems { get; set; }
    public bool RandomPairings { get; set; }
    public bool RandomMultiplier { get; set; }
    public bool MysteryCard { get; set; }
    public bool RandomBundleRewards { get; set; }
    public bool RandomCartDays { get; set; }
    public bool DoubleThemeWeek { get; set; }
    public bool WildcardDays { get; set; }
    public bool RandomShrineDonations { get; set; }

    public RandomizerSettings Clone() => (RandomizerSettings)MemberwiseClone();
}

public static class RandomizerMigration
{
    /// <summary>The old on/off reroll switch becomes Free; it is cleared so this runs once.</summary>
    public static bool Apply(GameplayConfig config)
    {
        if (!config.EnableThemeReroll) return false;
        config.Randomizer ??= new RandomizerSettings();
        config.Randomizer.Rerolls = RerollMode.Free;
        config.EnableThemeReroll = false;
        return true;
    }
}
