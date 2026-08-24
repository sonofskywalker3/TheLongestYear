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
    /// ExcludeFromRandomSale, fish_legendary tag). Defaults exclude Banana Sapling and
    /// Mango Sapling — tropical fruit tree saplings that fall out of the Saplings pool's
    /// island-only obtainability without needing special-case derivation code.</summary>
    public List<string> ExcludedItemIds { get; set; } = new()
    {
        // NOTE: this list is a config EXTENSION point only. Structural exclusions
        // (Ginger Island / Qi-gated content, the two island saplings) live in
        // ItemPoolBuilder.BuiltInExcludedItemIds — an existing config.json overrides
        // serialized list defaults wholesale, so anything that MUST be excluded cannot
        // live here (Nexus 1122358: a saved config wiped the old sapling defaults).
    };

    /// <summary>Spawn locations whose key contains any of these markers (case-insensitive)
    /// are excluded from pool derivation — post-CC / late-game areas whose items aren't
    /// year-1 obtainable (Ginger Island; SVE's Fable Reef and Crimson Badlands).
    /// Config-extensible for other mods' late-game maps.</summary>
    public List<string> ExcludedLocationMarkers { get; set; } = new()
    {
        "Island", "FableReef", "CrimsonBadlands",
        // BugLand (Mutant Bug Lair) is a BUILT-IN marker in ItemPoolBuilder — see
        // ExcludedItemIds note for why mandatory exclusions can't live in this list.
    };

    /// <summary>Config extension point: additional qualified item ids that can never carry
    /// a quality star, so slot re-rolls must never put a silver/gold ask on them. The
    /// built-in set (Seaweed/Green Algae/White Algae — Nexus 1122358: "silver and gold
    /// quality algaes") lives in BundleSlotFiller for the same config-override reason as
    /// <see cref="ExcludedItemIds"/>.</summary>
    public List<string> QualityIneligibleItemIds { get; set; } = new();

    /// <summary>Curated harder additions to the seasonal forage pools (spec seasonal-forage
    /// ruling 2026-07-14). Keys are Season names; values are qualified item ids.</summary>
    public Dictionary<string, List<string>> SeasonalForageAdditions { get; set; } = new()
    {
        ["Spring"] = new() { "(O)404", "(O)420" },          // Common + Red Mushroom
        ["Summer"] = new() { "(O)88", "(O)90" },            // desert forage (rare via weights)
        ["Fall"] = new() { "(O)422", "(O)88", "(O)90" },    // Purple Mushroom + desert
        ["Winter"] = new() { "(O)422" },                    // Purple Mushroom
    };

    /// <summary>Curated additions to the seasonal crop pools (mirrors
    /// <see cref="SeasonalForageAdditions"/>'s shape/loop): Tea Leaves aren't a
    /// Data/Crops entry (grown from a bush, not a seed), so they join the crop pool via
    /// this curated list instead of crop-table derivation. Keys are Season names; values
    /// are qualified item ids.</summary>
    public Dictionary<string, List<string>> CropPoolAdditions { get; set; } = new()
    {
        ["Spring"] = new() { "(O)815" }, // Tea Leaves
        ["Summer"] = new() { "(O)815" }, // Tea Leaves
        ["Fall"] = new() { "(O)815" },   // Tea Leaves
    };

    /// <summary>Multiplier applied to the Junimo Vault's base donation-value ask (spec
    /// Plan-3 tuning knob).</summary>
    public double VaultAmountMultiplier { get; set; } = 1.25;

    /// <summary>Number of trophy slots shown on the Gil's Trophies bundle composition.</summary>
    public int TrophyShownCount { get; set; } = 4;

    /// <summary>Number of trophy slots that must be donated to complete Gil's Trophies.</summary>
    public int TrophyRequiredCount { get; set; } = 2;

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
