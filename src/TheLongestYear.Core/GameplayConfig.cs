namespace TheLongestYear.Core;

/// <summary>Root config object read by the mod via SMAPI. All tuning dials hang off this.</summary>
public sealed class GameplayConfig
{
    public JpSettings Jp { get; set; } = new JpSettings();

    /// <summary>Gold the farmer starts each run with after a reset.</summary>
    public int StartingMoney { get; set; } = 500;

    /// <summary>Price cutoffs used to derive an item's rarity (and thus its JP value).</summary>
    public RarityThresholds RarityThresholds { get; set; } = new RarityThresholds();

    /// <summary>Number of weather preview rows shown on the planning hub (Plan 05 default; actual upgrade-tier gating is Plan 06).</summary>
    public int DefaultWeatherPreviewSlots { get; set; } = 1;

    /// <summary>Number of Traveling Cart preview rows shown on the planning hub (Plan 05 default; Plan 06 gates this on Cart Whisperer tiers).</summary>
    public int DefaultCartPreviewSlots { get; set; } = 2;

    /// <summary>SButton name (parsed mod-side) for the hotkey that reopens the weekly planning hub. Default: 'P'.</summary>
    public string WeeklyHubHotkey { get; set; } = "P";
}
