using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The single tunable knob block for engine slot generation (spec "Stacks &
/// qualities": one config block, baseline ~ vanilla feel; Plan 3 tunes these to the
/// Normal difficulty bar). Serialized inside GameplayConfig -> config.json, so every
/// dial is user-overridable without a rebuild. Item ids are QUALIFIED ("(O)337").</summary>
public sealed class BundleGenerationTuning
{
    /// <summary>Sampling weight for items with numeric (vanilla) ids vs non-numeric
    /// (modded) ids — the spec's "unknown items get conservative default weights".</summary>
    public int VanillaItemWeight { get; set; } = 3;
    public int ModdedItemWeight { get; set; } = 1;

    /// <summary>Per-item weight overrides (win over the vanilla/modded default).
    /// Defaults: iridium bar + desert forage are RARE rolls (spec).</summary>
    public Dictionary<string, int> RareRollWeights { get; set; } = new()
    {
        ["(O)337"] = 1, // Iridium Bar — joins the bar pool as a rare roll
        ["(O)88"] = 1,  // Coconut — desert forage, rare where season-appropriate
        ["(O)90"] = 1,  // Cactus Fruit
    };

    /// <summary>Config-extensible exclude-list (spec modded-content rule): qualified item
    /// ids never offered by any pool, merged with the structural vetting (Quest type,
    /// ExcludeFromRandomSale, fish_legendary tag).</summary>
    public List<string> ExcludedItemIds { get; set; } = new();

    /// <summary>Curated harder additions to the seasonal forage pools (spec seasonal-forage
    /// ruling 2026-07-14). Keys are Season names; values are qualified item ids.</summary>
    public Dictionary<string, List<string>> SeasonalForageAdditions { get; set; } = new()
    {
        ["Spring"] = new() { "(O)404", "(O)420" },          // Common + Red Mushroom
        ["Summer"] = new() { "(O)88", "(O)90" },            // desert forage (rare via weights)
        ["Fall"] = new() { "(O)422", "(O)88", "(O)90" },    // Purple Mushroom + desert
        ["Winter"] = new() { "(O)422" },                    // Purple Mushroom
    };

    /// <summary>Quality-ask chances per re-rolled slot (crops/forage/fish domains only).</summary>
    public double SilverQualityChance { get; set; } = 0.10;
    public double GoldQualityChance { get; set; } = 0.05;
    /// <summary>Quality Crops slots always ask gold at this stack (vanilla: 5 gold).</summary>
    public int QualityCropStack { get; set; } = 5;

    /// <summary>Large-quantity forage roll (spec: "Salmonberry x99"-style asks): chance
    /// that ONE slot of a seasonal-foraging bundle becomes a big-stack ask.</summary>
    public double LargeQuantityForageChance { get; set; } = 0.20;
    public int LargeQuantityMinStack { get; set; } = 40;
    public int LargeQuantityMaxStack { get; set; } = 99;

    /// <summary>Price-banded stack asks for monster drops (data-driven so modded drops
    /// scale sensibly): cheap items ask big stacks, dear items small ones.</summary>
    public int CheapPriceCeiling { get; set; } = 15;
    public int MidPriceCeiling { get; set; } = 50;
    public int CheapMinStack { get; set; } = 20;
    public int CheapMaxStack { get; set; } = 99;
    public int MidMinStack { get; set; } = 5;
    public int MidMaxStack { get; set; } = 20;
    public int DearMinStack { get; set; } = 1;
    public int DearMaxStack { get; set; } = 3;
}
