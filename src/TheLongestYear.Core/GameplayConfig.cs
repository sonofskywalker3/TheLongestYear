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
    /// Design-default per-item season pins for KIND 2 PerItem bundles. Sourced from the
    /// bundle-gate handoff doc (2026-05-26) — each pin reflects a realistic obtainability
    /// expectation that an early-run player can hit without late-game investment.
    /// User <see cref="ItemSeasonPins"/> entries win on conflict.
    /// </summary>
    public static IReadOnlyDictionary<string, string> DefaultItemSeasonPins { get; } =
        new Dictionary<string, string>
        {
            // --- Construction (Crafts Room, X=Y) ---
            ["(O)388"] = "Spring",   // Wood
            ["(O)390"] = "Spring",   // Stone
            ["(O)709"] = "Summer",   // Hardwood — need an axe upgrade or secret-woods access

            // --- Blacksmith's (Boiler Room, X=Y) ---
            ["(O)334"] = "Spring",   // Copper Bar
            ["(O)335"] = "Summer",   // Iron Bar
            ["(O)336"] = "Fall",     // Gold Bar  — aligns with smelter progression

            // --- Geologist's (Boiler Room, X=Y) ---
            ["(O)80"]  = "Spring",   // Quartz
            ["(O)86"]  = "Spring",   // Earth Crystal
            ["(O)84"]  = "Summer",   // Frozen Tear
            ["(O)82"]  = "Fall",     // Fire Quartz

            // --- River Fish (Fish Tank, X=Y) ---
            ["(O)145"] = "Spring",   // Sunfish
            ["(O)706"] = "Summer",   // Shad
            ["(O)699"] = "Fall",     // Tiger Trout
            ["(O)143"] = "Spring",   // Catfish

            // --- Lake Fish (Fish Tank, X=Y) ---
            ["(O)136"] = "Spring",   // Largemouth Bass
            ["(O)142"] = "Summer",   // Carp
            ["(O)700"] = "Fall",     // Bullhead
            ["(O)698"] = "Summer",   // Sturgeon

            // --- Ocean Fish (Fish Tank, X=Y) ---
            ["(O)131"] = "Spring",   // Sardine
            ["(O)130"] = "Summer",   // Tuna
            ["(O)150"] = "Summer",   // Red Snapper
            ["(O)701"] = "Fall",     // Tilapia

            // --- Night Fishing (Fish Tank, X=Y) ---
            ["(O)132"] = "Summer",   // Bream
            ["(O)140"] = "Fall",     // Walleye
            ["(O)148"] = "Fall",     // Eel

            // --- Specialty Fish (Fish Tank, X=Y) ---
            ["(O)128"] = "Summer",   // Pufferfish
            ["(O)156"] = "Summer",   // Ghostfish — caught in Mines L20+; Spring is tight w/o JP
            ["(O)164"] = "Fall",     // Sandfish
            ["(O)734"] = "Summer",   // Woodskip

            // --- Dye (Bulletin, X=Y) ---
            // Red Mushroom is a Spring item on this mod (Jeff, 2026-08-28: "red mushroom in
            // spring is perfectly fine"): the Spring forage pool adds it (SeasonalForageAdditions)
            // and the mines grow it on mushroom floors from level 41 in any season (MineShaft
            // line 1434). The old Summer pin made loop 16's Spring Foraging audit IMPOSSIBLE.
            ["(O)420"] = "Spring",   // Red Mushroom
            ["(O)397"] = "Spring",   // Sea Urchin
            ["(O)421"] = "Summer",   // Sunflower
            ["(O)444"] = "Summer",   // Duck Feather
            ["(O)62"]  = "Summer",   // Aquamarine
            ["(O)266"] = "Summer",   // Red Cabbage

            // --- Field Research (Bulletin, X=Y) ---
            // Purple Mushroom is NOT winter-gated (Jeff, 2026-08-27): the Mushroom Cave farm
            // choice produces it, the mines drop it from floor 80, and a mushroom log grows it.
            // The old Winter pin also contradicted this mod's own Fall forage pool, which adds
            // it, and that disagreement made a Fall Foraging bundle that drew it unsatisfiable
            // at its own Fall gate (found by tly_gatecheck). Fall keeps the two tables agreeing
            // and is comfortably reachable by any of the three routes.
            ["(O)422"] = "Fall",     // Purple Mushroom
            ["(O)392"] = "Winter",   // Nautilus Shell
            ["(O)702"] = "Spring",   // Chub
            ["(O)536"] = "Summer",   // Frozen Geode

            // --- Fodder (Bulletin, X=Y) ---
            ["(O)262"] = "Summer",   // Wheat
            ["(O)178"] = "Spring",   // Hay
            ["(O)613"] = "Fall",     // Apple

            // --- Enchanter's (Bulletin, X=Y) ---
            ["(O)725"] = "Summer",   // Oak Resin
            ["(O)348"] = "Fall",     // Wine
            ["(O)446"] = "Fall",     // Rabbit's Foot
            ["(O)637"] = "Fall",     // Pomegranate
        };

    /// <summary>
    /// Design-default cumulative quotas for KIND 3 Percentage bundles. Keyed by vanilla bundle
    /// name; value is [Spring, Summer, Fall, Winter] day-28 cumulative donation thresholds.
    /// Each value must be ≤ that bundle's X (numberOfSlots). User <see cref="BundleQuotas"/>
    /// entries win on conflict.
    /// </summary>
    public static IReadOnlyDictionary<string, int[]> DefaultBundleQuotas { get; } =
        new Dictionary<string, int[]>
        {
            // Crafts Room
            ["Exotic Foraging"] = new[] { 1, 3, 5, 5 },   // X=5 of Y=9
            // Pantry
            ["Quality Crops"]   = new[] { 1, 2, 3, 3 },   // X=3 of Y=4
            ["Animal"]          = new[] { 1, 3, 5, 5 },   // X=5 of Y=6
            ["Artisan"]         = new[] { 1, 2, 4, 6 },   // X=6 of Y=12
            // Fish Tank
            ["Crab Pot"]        = new[] { 1, 3, 5, 5 },   // X=5 of Y=10
            // Boiler Room
            ["Adventurer's"]    = new[] { 0, 1, 2, 2 },   // X=2 of Y=5
            // Bulletin Board
            ["Chef's"]          = new[] { 0, 1, 2, 3 },   // X=3 of Y=6  — lean-late ramp (user)

            // Curated 2026-08-21 (user ruling: "do it for all") — remix-pool + authored pick-X-of-Y
            // bundles whose derived floor(X*[.25,.5,.75,1]) ramp demanded a donation before any of
            // the bundle's items can exist (run-bricking) or was plainly harsh/lax. Obtainability
            // reasoning per bundle: docs/superpowers/specs/2026-08-21-curated-quota-ramps-design.md.
            // Run-bricking set:
            ["Winter Star"]          = new[] { 0, 0, 0, 2 },   // X=2 of 4 — Holly/Plum Pudding/Stuffing/Powdermelon are all Fall-Winter
            ["Forager's"]            = new[] { 0, 0, 2, 2 },   // X=2 of 3 — Salmonberry (Sp 15-18), Blackberry + Wild Plum (Fall)
            ["Gil's Trophies"]       = new[] { 0, 0, 1, 2 },   // X=2 of 4 — Spring/Summer trophies are a coin flip; rest are Fall+
            ["Brewer's"]             = new[] { 0, 1, 2, 4 },   // X=4 of 5 — random artisan goods; keg/press are not a Spring thing
            ["Preserver's"]          = new[] { 0, 1, 2, 4 },   // X=4 of 6 — same pool as Brewer's
            ["Mineral"]              = new[] { 0, 1, 3, 4 },   // X=4 of 6 — ~9%/loop no Spring-obtainable geode mineral among 6
            // Harsh/lax set:
            ["Home Cook's Feast"]    = new[] { 0, 1, 2, 4 },   // X=4 of 6 — no kitchen by Spring 28 on 500g; matches Chef's shape
            ["Fish Farmer's"]        = new[] { 0, 0, 1, 2 },   // X=2 of 3 — Roe needs a 5,000g Fish Pond; first ask in Fall
            ["Artifact"]             = new[] { 0, 1, 2, 4 },   // X=4 of 6 — 1 specific artifact by Spring 28 is a coin flip
            ["Four Seasons Sampler"] = new[] { 1, 3, 4, 5 },   // X=5 of 6 — forage expires with its season; front-load instead
            ["Rare Crops"]           = new[] { 0, 0, 1, 1 },   // X=1 of 2 — force the one donation by Fall 28, not Winter 28
            ["Garden"]               = new[] { 1, 2, 4, 4 },   // X=4 of 5 — complete by Fall 28 while crops still grow
        };

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
