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
            ["(O)420"] = "Summer",   // Red Mushroom
            ["(O)397"] = "Spring",   // Sea Urchin
            ["(O)421"] = "Summer",   // Sunflower
            ["(O)444"] = "Summer",   // Duck Feather
            ["(O)62"]  = "Summer",   // Aquamarine
            ["(O)266"] = "Summer",   // Red Cabbage

            // --- Field Research (Bulletin, X=Y) ---
            ["(O)422"] = "Winter",   // Purple Mushroom
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

    /// <summary>QA/debug: show the "Re-roll Themes" button on the planning hub, which regenerates
    /// the week's theme offer in place. Off by default — the week's offer is meant to be fixed —
    /// but the re-roll code is retained behind this switch. Toggle via config.json / GMCM.</summary>
    public bool EnableThemeReroll { get; set; } = false;

    /// <summary>Windowed width the mod forces on launch. SDV doesn't persist a windowed width/height
    /// (it always boots at 1280×720 in windowed mode), and the dev redeploy loop force-kills the game
    /// so it never saves one on exit — so the mod nudges the window to this size once the game is up.
    /// Ignored in fullscreen. Set to 0 (either dimension) to leave the window untouched.</summary>
    public int WindowWidth { get; set; } = 1920;

    /// <summary>Windowed height the mod forces on launch. See <see cref="WindowWidth"/>.</summary>
    public int WindowHeight { get; set; } = 1080;
}
