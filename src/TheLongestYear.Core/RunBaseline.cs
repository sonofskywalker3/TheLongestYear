using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// The full description of the player + world's starting state at the top of a new run,
/// derived from <see cref="MetaState"/> (banked upgrades) and <see cref="RunState"/>
/// (in-run peaks). The mod-side reset code translates this into game-state writes.
///
/// Pure data — no game refs. Defaults represent a brand-new save with zero keep-upgrades
/// purchased: 500g, 12 inventory slots, all tools at base tier, no skill levels, no kept
/// buildings, no animals, no horse, no kitchen, no bus, no mine elevator.
/// </summary>
public sealed class RunBaseline
{
    public int StartingGold { get; init; } = 500;
    public int MaxItems { get; init; } = 12;

    /// <summary>Tool kind slug → <c>Tool.UpgradeLevel</c> the player starts the run holding.
    /// Slug list: <c>hoe</c>, <c>pickaxe</c>, <c>axe</c>, <c>watering_can</c> (0..4 each),
    /// and <c>fishing_rod</c> (2 = Fiberglass, 3 = Iridium — bamboo at UpgradeLevel 1 is
    /// vanilla Willy day-2 grant, not represented). Missing keys mean "leave at the vanilla
    /// baseline that Game1.loadForNewGame produced" (rusty hoe/pickaxe/axe/watering can; no rod).</summary>
    public IReadOnlyDictionary<string, int> ToolTiers { get; init; }
        = new Dictionary<string, int>();

    /// <summary>Vanilla skill-index (0=Farming, 1=Fishing, 2=Foraging, 3=Mining, 4=Combat)
    /// → level 1..10 to restore at run start. XP is floored to that level's threshold via
    /// <c>Farmer.getBaseExperienceForLevel</c>.</summary>
    public IReadOnlyDictionary<int, int> SkillLevels { get; init; }
        = new Dictionary<int, int>();

    /// <summary>Skill indexes (subset of <see cref="SkillLevels"/>) whose restored level
    /// is 5 or 10 — the reset code queues a <c>LevelUpMenu</c> for each so the player can
    /// re-pick their profession.</summary>
    public IReadOnlyList<int> ProfessionPickerSkillsToRequeue { get; init; }
        = new List<int>();

    /// <summary>Mine elevator floor (10..120 in steps of 10) accessible at run start.
    /// 0 means "no elevator" (vanilla baseline: -1 sentinel).</summary>
    public int MineElevatorFloor { get; init; }

    /// <summary>True if the player gets the Kitchen house upgrade on day 1.</summary>
    public bool KitchenOnDay1 { get; init; }

    /// <summary>True if the player gets the full L3 farmhouse (kitchen + kids' room + cellar)
    /// on day 1. Implies <see cref="KitchenOnDay1"/> — L3 can't exist without L1 + L2 in
    /// vanilla data, and the catalog enforces this via the keep_kitchen prerequisite.</summary>
    public bool BasementOnDay1 { get; init; }

    /// <summary>True if the four Vault bundles should be marked paid (bus restored).</summary>
    public bool BusUnlocked { get; init; }

    /// <summary>True if Robin's map-shortcut community upgrade should be pre-applied on day 1
    /// (single mail flag <c>communityUpgradeShortcuts</c> covers all five shortcuts).</summary>
    public bool ShortcutsUnlocked { get; init; }

    /// <summary>True if the early-horse upgrade should spawn a horse + stable on day 1.</summary>
    public bool EarlyHorse { get; init; }

    /// <summary>Building blueprint names to pre-build on the farm (e.g. "Coop", "Deluxe Barn").
    /// Each ends up at a deterministic tile per <see cref="BuildingPreplacement"/>.</summary>
    public IReadOnlyList<string> KeptBuildings { get; init; } = new List<string>();

    /// <summary>Animal species + count to place into the matching housing on day 1.
    /// Tuple = (vanilla animal type string, building blueprint required).</summary>
    public IReadOnlyList<StartingAnimal> StartingAnimals { get; init; } = new List<StartingAnimal>();

    /// <summary>Mastery level to restore at run start (0 = none). Permanent floor — owning the
    /// keep_mastery_N tiers always restores level N, not capped at in-run reach.</summary>
    public int MasteryLevel { get; init; }

    /// <summary>Grant the Golden Scythe instead of the basic scythe each run (Keep Golden Scythe).</summary>
    public bool GrantGoldenScythe { get; init; }
}

/// <summary>One starting-animal entry. The reset code finds a building of <c>HousingType</c>
/// on the farm and adds an animal of <c>VanillaType</c>.</summary>
public sealed record StartingAnimal(string VanillaType, string HousingType);
