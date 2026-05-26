using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Root config object read by the mod via SMAPI. All tuning dials hang off this.</summary>
public sealed class GameplayConfig
{
    /// <summary>
    /// Per-item season pins. Key is the qualified item id ("(O)24") or hand-authored catalog id
    /// ("Diamond"). Value is a season name ("Spring"/"Summer"/"Fall"/"Winter"). When the generator
    /// places an item, an override here wins over the algorithm IF the pinned season is one of
    /// the item's obtainable seasons. Loaded config is merged with <see cref="DefaultSeasonOverrides"/>;
    /// user values win on conflict.
    /// </summary>
    public Dictionary<string, string> SeasonOverrides { get; set; } = new();

    /// <summary>
    /// Per-item theme pins. Key is the qualified item id, value is a theme name
    /// ("Foraging"/"Farming"/"Fishing"/"Mining"/"Mixed"). This re-themes the item from its bundle-
    /// derived room theme — useful when the vanilla room placement is thematically wrong
    /// (e.g. Cave Carrot is bundled as Foraging but is thematically Mining).
    /// Loaded config is merged with <see cref="DefaultThemeOverrides"/>; user values win.
    /// </summary>
    public Dictionary<string, string> ThemeOverrides { get; set; } = new();

    /// <summary>
    /// Design-default season pins for items where the pure algorithm picked the "wrong" feel.
    /// Each entry is keyed by both forms (qualified id "(O)72" + hand-authored name "Diamond") so
    /// the same set works against the runtime bundle catalog and the dev sample catalog.
    /// User config entries OVERRIDE these defaults — to "unpin," set the value to the algorithm's
    /// natural pick (or any obtainable season).
    /// </summary>
    public static IReadOnlyDictionary<string, string> DefaultSeasonOverrides { get; } =
        new Dictionary<string, string>
        {
            // --- Mining bars: Copper Spring → Iron Summer → Gold Fall (user, 2026-05-26). ---
            // Aligns the gating with smelter progression so the player can't bypass bar tiers.
            ["(O)334"] = "Spring",   ["CopperBar"] = "Spring",
            ["(O)335"] = "Summer",   ["IronBar"]   = "Summer",
            ["(O)336"] = "Fall",     ["GoldBar"]   = "Fall",

            // --- Mining gems / essences ---
            // Quartz — common, abundant in early levels; easy spring gate.
            ["(O)80"]  = "Spring",   ["Quartz"]     = "Spring",
            // Frozen Tear — swap to Spring per user (the mine has plenty by then if you've made it).
            ["(O)84"]  = "Spring",   ["FrozenTear"] = "Spring",
            // Fire Quartz ("magma" interp) — Summer per user, replacing the Spring slot.
            ["(O)82"]  = "Summer",   ["FireQuartz"] = "Summer",
            // Diamond — held in Winter for the rarest tier (not in vanilla CC ingredients but
            // pinned for mod-extended catalogs).
            ["(O)72"]  = "Winter",   ["Diamond"]    = "Winter",

            // --- Foraging ---
            // Morel — Rare [Spring, Fall]; push to Fall to un-crowd Spring.
            ["(O)257"] = "Fall",     ["Morel"]      = "Fall",
            // Stone / Wood — Construction Bundle staples, trivial Spring gates.
            ["(O)388"] = "Spring",   ["Wood"]       = "Spring",
            ["(O)390"] = "Spring",   ["Stone"]      = "Spring",

            // --- Fishing ---
            // Catfish & Sardine — confirmed Spring (easy, iconic).
            ["(O)143"] = "Spring",   ["Catfish"]    = "Spring",
            ["(O)131"] = "Spring",   ["Sardine"]    = "Spring",

            // --- Farming: animal products ---
            // Regular eggs Summer, large eggs Fall — gives the player a season to upgrade
            // the coop after Spring, then another to get a Large Egg by Fall.
            ["(O)176"] = "Summer",   ["Egg"]            = "Summer",
            ["(O)180"] = "Summer",   ["BrownEgg"]       = "Summer",
            ["(O)174"] = "Fall",     ["LargeEgg"]       = "Fall",
            ["(O)182"] = "Fall",     ["LargeBrownEgg"]  = "Fall",
            // Milks same pattern as eggs.
            ["(O)184"] = "Summer",   ["Milk"]           = "Summer",
            ["(O)436"] = "Summer",   ["GoatMilk"]       = "Summer",
            ["(O)186"] = "Fall",     ["LargeMilk"]      = "Fall",
            ["(O)438"] = "Fall",     ["LargeGoatMilk"]  = "Fall",
            // Wool — Fall (sheep production ramps by then).
            ["(O)440"] = "Fall",     ["Wool"]           = "Fall",
            // Honey — Summer (needs Beekeeper recipe + flowers up).
            ["(O)340"] = "Summer",   ["Honey"]          = "Summer",

            // --- Mixed ---
            // Truffle — Fall (pigs don't surface in Winter).
            ["(O)430"] = "Fall",     ["Truffle"]        = "Fall",
        };

    public static IReadOnlyDictionary<string, string> DefaultThemeOverrides { get; } =
        new Dictionary<string, string>
        {
            // Cave Carrot — vanilla bundles it as Foraging (Exotic Foraging Bundle) but it's mined
            // out of mine-level dirt patches. Re-theme to Mining so its bonus/liability match how
            // the player actually obtains it.
            ["(O)78"]      = "Mining",
            ["CaveCarrot"] = "Mining",
        };

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
    /// Per-(season, theme) cap kept as a structural safety net rather than a difficulty knob —
    /// JP upgrades carry the difficulty curve now (spec 2026-05-26 round 3). Defaults are
    /// generous enough that no realistic vanilla / SVE catalog hits them; the cap mostly
    /// prevents pathological "all 50 items in Spring Foraging" placements when the algorithm's
    /// least-loaded fallback hits an edge case. User SeasonOverrides ignore the cap by design.
    /// </summary>
    public int[] ContractItemCapBySeason { get; set; } = new[] { 15, 15, 15, 15 };

    /// <summary>
    /// How many items from a (season, theme) contract pool the player must donate to clear that
    /// contract's gate. Indices match Season enum. Spec 2026-05-26 round 5: gate moved from
    /// "all pool items required" to "N from pool"; harder seasons demand more. Each contract
    /// caps its effective N at the pool size, so tiny pools (e.g. Winter Mining with 2 items)
    /// stay satisfiable even if the configured N is larger.
    /// </summary>
    public int[] GateRequirementBySeason { get; set; } = new[] { 2, 3, 4, 4 };

    /// <summary>
    /// How many bonus items per (season, theme) contract are shown on the planning hub. These
    /// items pay the championship JP multiplier when donated during their championed week.
    /// Defaults scale +1 per season so the player sees more bonus options later in the year.
    /// Each contract caps its effective sample at the pool size.
    /// </summary>
    public int[] BonusListSizeBySeason { get; set; } = new[] { 4, 5, 6, 7 };

    /// <summary>JP multiplier applied to bonus-list items donated during their championed week.</summary>
    public double ChampionBonusMultiplier { get; set; } = 1.5;
}
