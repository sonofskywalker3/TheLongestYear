using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Root config object read by the mod via SMAPI. All tuning dials hang off this.</summary>
public sealed class GameplayConfig
{
    /// <summary>
    /// Per-item theme pins. Key is the qualified item id, value is a theme name
    /// ("Foraging"/"Farming"/"Fishing"/"Mining"/"Mixed"). This re-themes the item from its bundle-
    /// derived room theme — useful when the vanilla room placement is thematically wrong
    /// (e.g. Cave Carrot is bundled as Foraging but is thematically Mining).
    /// Loaded config is merged with <see cref="DefaultThemeOverrides"/>; user values win.
    /// </summary>
    public Dictionary<string, string> ThemeOverrides { get; set; } = new();

    public static IReadOnlyDictionary<string, string> DefaultThemeOverrides { get; } =
        new Dictionary<string, string>
        {
            // Cave Carrot — vanilla bundles it as Foraging (Exotic Foraging Bundle) but it's mined
            // out of mine-level dirt patches. Re-theme to Mining so its bonus/liability match how
            // the player actually obtains it.
            ["(O)78"]      = "Mining",
            ["CaveCarrot"] = "Mining",
        };

    /// <summary>
    /// User-configurable per-item bundle pins. Keyed by qualified item id ("(O)24"); value is a
    /// season name. Used by KIND 2 PerItem bundles only — see <see cref="BundleKind.PerItem"/>.
    /// User entries OVERRIDE <see cref="DefaultItemSeasonPins"/> on conflict.
    /// </summary>
    public Dictionary<string, string> ItemSeasonPins { get; set; } = new();

    /// <summary>
    /// User-configurable per-bundle cumulative quotas. Keyed by bundle name (vanilla
    /// Data/Bundles "name" field, e.g. "Crab Pot", "Artisan"); value is a 4-int array of
    /// cumulative donations required by [Spring, Summer, Fall, Winter] day 28. Used by KIND 3
    /// Percentage bundles only — see <see cref="BundleKind.Percentage"/>. User entries OVERRIDE
    /// <see cref="DefaultBundleQuotas"/> on conflict.
    /// </summary>
    public Dictionary<string, int[]> BundleQuotas { get; set; } = new();

    /// <summary>Engine slot-generation tuning (pool weights, stack/quality rolls,
    /// forage additions, exclude-list). See BundleGenerationTuning.</summary>
    public BundleGenerationTuning PoolTuning { get; set; } = new();

    /// <summary>JP price of holding the bundle board across a Fail-night reset, indexed by how
    /// many holds the player has taken in a row (index 0 = first hold). The last value repeats.
    /// Reshuffling resets the counter. Spec 2026-08-24 keep-bundles hold.</summary>
    public List<long> BundleHoldCosts { get; set; } = new() { 0, 50, 100, 200, 300 };

    /// <summary>Season pity (spec 2026-08-25): after <see cref="PityThreshold"/> fails at the SAME
    /// season, each further fail eases that season's gate. Counting always runs; this switch only
    /// zeroes the effect so it can be turned on later without losing history.</summary>
    public bool PityEnabled { get; set; } = true;

    /// <summary>Fails at one season before easing starts (the first N are standard difficulty).</summary>
    public int PityThreshold { get; set; } = 5;

    /// <summary>Quota reduction per ease step when the player KEEPS the board (0.10 = -10%).</summary>
    public double PityQuotaStep { get; set; } = 0.10;

    /// <summary>Lowest quota factor the keep-path easing can reach.</summary>
    public double PityQuotaFloor { get; set; } = 0.50;

    /// <summary>Hardest items removed from that season's slot pools per ease step when the player RESHUFFLES.</summary>
    public int PityTrimPerStep { get; set; } = 2;

    /// <summary>JP price of accepting the Junimos' pity offer on a Fail night, indexed by how
    /// many offers the player has accepted in a row (index 0 = first accept, free). Same shape
    /// and default as <see cref="BundleHoldCosts"/>; declining resets the counter.</summary>
    public List<long> PityCosts { get; set; } = new() { 0, 50, 100, 200, 300 };

    /// <summary>
    /// The override layer for rulings the derivation rules cannot see. Everything that USED to
    /// live here (fish, ores, bars, crops, forage) is now placed by its own rule in
    /// ItemAvailabilityBuilder / FishAvailability / MetalsAvailability / etc, which reads real
    /// game data instead of a hand curated guess; a pin surviving here would only be able to move
    /// a rule's week LATER (see ItemAvailabilityModel), never earlier, so an entry that agreed
    /// with its rule did nothing and one that disagreed was silently ignored.
    ///
    /// What is left are the only two ids no rule places at all: Red Mushroom ((O)420) and Sea
    /// Urchin ((O)397). Both are commented below with why no rule can see them.
    ///
    /// User <see cref="ItemSeasonPins"/> entries win on conflict: the caller merges this table
    /// first and the user's on top, so a user pin for either id replaces the default outright.
    /// </summary>
    public static IReadOnlyDictionary<string, string> DefaultItemSeasonPins { get; } =
        new Dictionary<string, string>
        {
            // Red Mushroom is a Spring item on this mod (Jeff, 2026-08-28: "red mushroom in
            // spring is perfectly fine"): the Spring forage pool adds it (SeasonalForageAdditions)
            // and the mines grow it on mushroom floors from level 41 in any season (MineShaft
            // line 1434), but neither of those is a rule this model reads, so nothing derives it.
            ["(O)420"] = "Spring",   // Red Mushroom
            // Sea Urchin: bridge repair, Jeff's ruling. No derivation rule reads bridge repair
            // state, so this floor exists only here.
            ["(O)397"] = "Spring",   // Sea Urchin
        };

    /// <summary>
    /// Design-default cumulative quotas for KIND 3 Percentage bundles. Keyed by vanilla bundle
    /// name; value is [Spring, Summer, Fall, Winter] day-28 cumulative donation thresholds.
    /// Each value must be ≤ that bundle's X (numberOfSlots). User <see cref="BundleQuotas"/>
    /// entries win on conflict.
    /// </summary>
    /// <summary>Empty since the even-year spec (2026-08-28): a pick-X-of-Y bundle's ramp now follows
    /// its own items (BundleClassifier.RampFromItems). The user's <see cref="BundleQuotas"/> is the
    /// only hand override left. The 2026-08-21 curated table is in git history.</summary>
    public static IReadOnlyDictionary<string, int[]> DefaultBundleQuotas { get; } =
        new Dictionary<string, int[]>();

    /// <summary>The ten difficulty modifiers (spec 2026-08-26). Each is an independent
    /// Easy/Normal/Hard/Extreme step; there is no overall tier. Every one defaults to Normal,
    /// which is the mod's shipping balance, so leaving this alone changes nothing. A change takes
    /// effect at the NEXT reset, not mid-run: the resolved profile is stamped into
    /// <see cref="MetaState.Difficulty"/> when a loop begins and consumers read that stamp.</summary>
    public DifficultySettings Difficulty { get; set; } = new();

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

    /// <summary>Master switch for The Longest Year. When false, TLY does no setup at SaveLoaded
    /// and no game effects fire. Use the in-game GMCM (if installed) to toggle, or edit
    /// config.json directly. Toggling takes effect on next save load.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>X tile coordinate on the Farm where the Junimo Stash chest is placed.
    /// Sentinel <c>(0, 0)</c> means "auto-pick relative to the FarmHouse entry tile" — the
    /// stash service places the chest two tiles east + one tile south of where the player
    /// spawns when exiting the farmhouse, which is always visible on Standard farm.
    /// Use tly_setstash in-game to anchor to a specific tile.</summary>
    public int StashTileX { get; set; } = 0;

    /// <summary>Y tile coordinate on the Farm where the Junimo Stash chest is placed.
    /// See <see cref="StashTileX"/>. <c>(0, 0)</c> = auto-pick.</summary>
    public int StashTileY { get; set; } = 0;

    /// <summary>JP multiplier applied to bonus-list items donated during their selected week.</summary>
    public double SelectionBonusMultiplier { get; set; } = 1.5;

    /// <summary>Always-on top-right corner HUD showing banked JP + the current week's theme +
    /// the 1.5× / drawback-lifted state. Toggle off to hide.</summary>
    public bool ShowJpHud { get; set; } = true;

    /// <summary>When true (default), TLY scans the live save's Data/Events at load and auto-flags any
    /// cutscene that grants a run-wipe-able unlock (recipe / mail flag / quest) as "replayable" so it
    /// re-fires each loop — covering mod unlock cutscenes, not just the vanilla furnace/cave scenes.
    /// Set false to fall back to only the hardcoded vanilla ids (today's behavior). Takes effect on the
    /// next save load.</summary>
    public bool AutoDetectReplayableUnlockCutscenes { get; set; } = true;

    /// <summary>QA/debug: show the "Re-roll Themes" button on the planning hub, which regenerates
    /// the week's theme offer in place. Off by default — the week's offer is meant to be fixed —
    /// but the re-roll code is retained behind this switch. Toggle via config.json / GMCM.</summary>
    public bool EnableThemeReroll { get; set; } = false;

    /// <summary>Better Start (Nexus 32131) mails Robin's starter gift (chests, wood, stone, coal,
    /// seeds, 1,500g) through a one-shot trigger action. The reset now clears the "already fired"
    /// record so the gift returns every loop, like a fresh save would get it. Off keeps that record,
    /// so the gift arrives once per save. See <see cref="TriggerActionResetRules"/>.</summary>
    public bool ResendBetterStartGift { get; set; } = true;

    /// <summary>Rule B (activity-themes spec 2026-08-28): how many weekly goals may be FILLER
    /// (open lines the day-28 gate does not demand this season) per season, indexed
    /// Spring..Winter. Since the even-year build (Jeff, 2026-08-28: the floor only stops an item
    /// showing too early, nothing holds one back) the default is unlimited everywhere; how far
    /// the goals may run ahead of the gate is bounded per bundle by SeasonNeed instead.</summary>
    public List<int> ThemeFillerBySeason { get; set; } = new() { GoalSamplingRules.UnlimitedFiller, GoalSamplingRules.UnlimitedFiller, GoalSamplingRules.UnlimitedFiller, GoalSamplingRules.UnlimitedFiller };

    /// <summary>Spec 2026-08-28-even-year: move one item's first week (1 to 16), by qualified id.
    /// Later only; an override earlier than the derived floor is rejected and listed by
    /// tly_itemmodel and tly_dumpavailability.</summary>
    public Dictionary<string, int> AvailabilityWeekOverrides { get; set; } = new();

    public int FillerAllowanceFor(Season season)
    {
        int index = (int)season;
        return ThemeFillerBySeason != null && index >= 0 && index < ThemeFillerBySeason.Count
            ? ThemeFillerBySeason[index]
            : GoalSamplingRules.UnlimitedFiller;
    }

    /// <summary>Developer-only: poll the <c>tly_commands.txt</c> file in the mod folder and execute
    /// the queued tly_ debug commands (including destructive ones like reset / wipe). Off by default
    /// so a shipped build never watches the filesystem or runs commands the player didn't initiate;
    /// the in-game SMAPI console commands stay available regardless. Toggle via config.json.</summary>
    public bool EnableDebugCommandBridge { get; set; } = false;

    /// <summary>Windowed width the mod forces on launch. SDV doesn't persist a windowed width/height
    /// (it always boots at 1280×720 in windowed mode), and the dev redeploy loop force-kills the game
    /// so it never saves one on exit — so the mod nudges the window to this size once the game is up.
    /// Ignored in fullscreen. Set to 0 (either dimension) to leave the window untouched.</summary>
    public int WindowWidth { get; set; } = 1920;

    /// <summary>Windowed height the mod forces on launch. See <see cref="WindowWidth"/>.</summary>
    public int WindowHeight { get; set; } = 1080;

    /// <summary>Where the Community Center board comes from. "Engine" (default): The Longest Year
    /// builds its own board every loop. "Vanilla": keep the game's own Standard/Remixed board (or
    /// another bundle mod's) and re-roll it the same way on every reset. Takes effect at the next
    /// reset; the new-game Advanced Options dropdown sets it per save. See
    /// <see cref="BundleSourceNames"/>.</summary>
    public string BundleSource { get; set; } = BundleSourceNames.Engine;

    /// <summary>Kill-switch for the weapon/hat donation patches. When false, Gil's Trophies
    /// composes rings-only (no weapon/hat slots offered), for compatibility with mods that
    /// conflict with those patches. Governs the NEXT generated board: a board already composed
    /// with weapon/hat slots keeps its donation patches live until the next reset regenerates
    /// rings-only (spec 2026-08-21).</summary>
    public bool EnableNonObjectDonations { get; set; } = true;

    /// <summary>When true (default), the Traveling Cart is capped to the number of stalls unlocked
    /// by the Cart Stall shrine upgrades (one item until Cart Stall II is bought). Set false to
    /// leave the cart's full vanilla (and other-mod) stock untouched — the Cart Stall upgrades then
    /// do nothing. Takes effect the next time the cart menu opens.</summary>
    public bool LimitTravelingCartStock { get; set; } = true;

    /// <summary>Festival time-flow: hours pass normally inside a festival and leaving does not end
    /// the day (see FestivalTimeFlow). Off = vanilla, where walking into a festival costs the whole
    /// day. On by default: a time-loop cannot afford to hand away a day.</summary>
    public bool FestivalTimeFlows { get; set; } = true;

    /// <summary>Run each festival's main event (Egg Hunt, Luau soup, ice fishing, Flower Dance)
    /// at most once per day. Only meaningful while <see cref="FestivalTimeFlows"/> is on, which is
    /// what makes the festival map re-enterable in the first place. A new loop always starts
    /// clean - the rewind means the festival has not happened yet.</summary>
    public bool FestivalMainEventOncePerDay { get; set; } = true;

    /// <summary>Deja-vu dialogue (spec 2026-08-27): villagers you have dealt with a lot across loops
    /// occasionally half-remember you. No mechanical effect. GMCM "Features".</summary>
    public bool EnableDejaVuDialogue { get; set; } = true;

    /// <summary>Familiarity points (talk 1, gift 3, heart event 10, summed over every loop) a
    /// villager needs before a deja-vu line can play. Tier 2 lines start at three times this.</summary>
    public int DejaVuThreshold { get; set; } = 60;

    /// <summary>Percent chance per eligible conversation. Capped to one line per villager per loop
    /// and one line per week across the whole town regardless of this value.</summary>
    public int DejaVuChancePercent { get; set; } = 6;
}
