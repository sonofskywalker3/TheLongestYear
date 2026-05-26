using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Root config object read by the mod via SMAPI. All tuning dials hang off this.</summary>
public sealed class GameplayConfig
{
    /// <summary>
    /// Per-item season pins. Key is the qualified item id ("(O)24"), value is a season name
    /// ("Spring"/"Summer"/"Fall"/"Winter"). When the generator places an item, an override here
    /// wins over the algorithm IF the pinned season is one of the item's obtainable seasons.
    /// Use this to dial specific placements after reviewing the dumped assignment table.
    /// </summary>
    public Dictionary<string, string> SeasonOverrides { get; set; } = new();

    public JpSettings Jp { get; set; } = new JpSettings();

    /// <summary>Gold the farmer starts each run with after a reset.</summary>
    public int StartingMoney { get; set; } = 500;

    /// <summary>Price cutoffs used to derive an item's rarity (and thus its JP value).</summary>
    public RarityThresholds RarityThresholds { get; set; } = new RarityThresholds();

    /// <summary>Number of weather preview rows shown on the planning hub. Hidden by default
    /// (count = 0); Plan 06 will compute this dynamically from owned Weather Sage upgrades.</summary>
    public int DefaultWeatherPreviewSlots { get; set; } = 0;

    /// <summary>Number of Traveling Cart preview rows shown on the planning hub. Hidden by default
    /// (count = 0); Plan 06 will compute this dynamically from owned Cart Whisperer tiers.</summary>
    public int DefaultCartPreviewSlots { get; set; } = 0;

    /// <summary>SButton name (parsed mod-side) for the hotkey that reopens the weekly planning hub. Default: 'P'.
    /// (unused in v1; Plan 06 will re-enable the hotkey)</summary>
    public string WeeklyHubHotkey { get; set; } = "P";

    /// <summary>
    /// Per-season maximum items per (season, theme) contract slot. Indices match Season enum:
    /// [0]=Spring, [1]=Summer, [2]=Fall, [3]=Winter. The cap creates a roguelite difficulty curve —
    /// early weeks have fewer required items, later weeks have more. Multi-season items overflow to
    /// later seasons preferentially; single-season items override the cap (we never drop CC items).
    /// </summary>
    public int[] ContractItemCapBySeason { get; set; } = new[] { 4, 5, 6, 9 };
}
