namespace TheLongestYear.Core;

/// <summary>The ten configured difficulty modifiers, serialized into
/// <see cref="GameplayConfig.Difficulty"/>. Each one is independent. <see cref="Overall"/> is a
/// setup shortcut, not a tier (Jeff, 2026-09-14): picking it sets all ten, and a dial edited
/// afterwards simply keeps its own value. Nothing reads Overall to decide gameplay.
///
/// Every property defaults to <see cref="DifficultyStep.Normal"/>, and Normal is the mod's
/// shipping balance, so an untouched config changes nothing.
///
/// A change here takes effect at the NEXT reset, never mid-run: the resolved
/// <see cref="DifficultyProfile"/> is stamped into <see cref="MetaState.Difficulty"/> when a loop
/// begins and every consumer reads that stamp. Spec 2026-08-26 difficulty-modifiers.
///
/// The two "all normal" checks are METHODS, not properties, on purpose: SMAPI's JSON layer
/// serializes get-only properties, and a computed flag written into config.json would read as a
/// setting the player could change.</summary>
public sealed class DifficultySettings
{
    /// <summary>The overall lever: the level last picked for every dial at once. Display and
    /// setup only; see <see cref="SetAll"/>. Excluded from <see cref="IsAllNormal"/>.</summary>
    public DifficultyStep Overall { get; set; } = DifficultyStep.Normal;

    /// <summary>Set the lever and all ten dials to one level.</summary>
    public void SetAll(DifficultyStep step)
    {
        Overall = step;
        StackSize = step;
        QualityAsks = step;
        RequiredSlots = step;
        ItemRarity = step;
        JpEarned = step;
        ShrinePrices = step;
        StartingGold = step;
        CartSlots = step;
        HoldPrices = step;
        SeasonPity = step;
    }

    // ---- Ask-side: baked into the board when it is generated ----

    /// <summary>Scales how much of an item a slot asks for.</summary>
    public DifficultyStep StackSize { get; set; } = DifficultyStep.Normal;

    /// <summary>Scales how often a slot asks for a silver or gold star. Never overrides
    /// eligibility: an item the game cannot give a star to still never carries one.</summary>
    public DifficultyStep QualityAsks { get; set; } = DifficultyStep.Normal;

    /// <summary>How many of a bundle's shown slots must be donated. The only ask-side modifier
    /// that raises the real total rather than redistributing it.</summary>
    public DifficultyStep RequiredSlots { get; set; } = DifficultyStep.Normal;

    /// <summary>Weights slot composition toward harder items. TLY Custom (Engine) bundles only:
    /// changing which item a vanilla bundle asks for would be changing the bundle.</summary>
    public DifficultyStep ItemRarity { get; set; } = DifficultyStep.Normal;

    // ---- Economy: read live from the run's stamp ----

    /// <summary>Scales every Junimo Point award.</summary>
    public DifficultyStep JpEarned { get; set; } = DifficultyStep.Normal;

    /// <summary>Scales what shrine upgrades cost.</summary>
    public DifficultyStep ShrinePrices { get; set; } = DifficultyStep.Normal;

    /// <summary>Scales <see cref="GameplayConfig.StartingMoney"/>.</summary>
    public DifficultyStep StartingGold { get; set; } = DifficultyStep.Normal;

    /// <summary>How many items the Traveling Cart shows before any Cart Stall upgrade.</summary>
    public DifficultyStep CartSlots { get; set; } = DifficultyStep.Normal;

    /// <summary>Scales the JP price of holding the board on a Fail night, and of accepting the
    /// Junimos' pity offer. The first of each stays free at every step, because the curves start
    /// at 0: the step makes REPEATED holds expensive, it does not tax the first mistake.</summary>
    public DifficultyStep HoldPrices { get; set; } = DifficultyStep.Normal;

    // ---- Mercy ----

    /// <summary>How readily the Junimos ease a season you keep failing. Extreme turns season
    /// pity off entirely; the fail counting still runs, so dropping back to Normal resumes where
    /// easing would have been.</summary>
    public DifficultyStep SeasonPity { get; set; } = DifficultyStep.Normal;

    /// <summary>True when every modifier is Normal, i.e. this build behaves exactly as a
    /// pre-difficulty build.</summary>
    public bool IsAllNormal()
        => AsksAllNormal()
           && JpEarned == DifficultyStep.Normal
           && ShrinePrices == DifficultyStep.Normal
           && StartingGold == DifficultyStep.Normal
           && CartSlots == DifficultyStep.Normal
           && HoldPrices == DifficultyStep.Normal
           && SeasonPity == DifficultyStep.Normal;

    /// <summary>True when the three modifiers a Vanilla board can honour are all Normal. Gates
    /// the Vanilla post-pass, so the default Vanilla path keeps its current zero-write behaviour.
    /// <see cref="ItemRarity"/> is deliberately NOT part of this check: it cannot apply to a
    /// vanilla board at all, so it must not force the pass to run.</summary>
    public bool AsksAllNormal()
        => StackSize == DifficultyStep.Normal
           && QualityAsks == DifficultyStep.Normal
           && RequiredSlots == DifficultyStep.Normal;

    /// <summary>A field-by-field copy, so a stamped profile can never alias the live config
    /// object and drift when the player edits GMCM mid-run.</summary>
    public DifficultySettings Clone() => new()
    {
        Overall = Overall,
        StackSize = StackSize,
        QualityAsks = QualityAsks,
        RequiredSlots = RequiredSlots,
        ItemRarity = ItemRarity,
        JpEarned = JpEarned,
        ShrinePrices = ShrinePrices,
        StartingGold = StartingGold,
        CartSlots = CartSlots,
        HoldPrices = HoldPrices,
        SeasonPity = SeasonPity,
    };
}
