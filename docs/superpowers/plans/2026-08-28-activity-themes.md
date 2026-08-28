# Activity Themes (Spelunking, Artisan, Kitchen) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Three activity themes whose weekly goals match by item kind anywhere on the board, one plain bonus and one cross-over liability each, plus the theme-week economy fix (rules A to E: goals follow the gate, filler follows the ramp, the offer counts what a week can ask, the bonus is paid per goal, easier items earlier) driven by an effort model derived from the game's own data.

**Architecture:** Phase 1 adds effort-only derivation rules (`Core/Availability/*Availability.cs`) composed by `ItemAvailabilityBuilder` from a new `EffortData` snapshot the mod reads at SaveLoaded (`Loop/GameEffortData.cs`); effort feeds goal weighting and a review document, never a season floor, so no day-28 gate changes. Phase 2 changes the goal pipeline on the existing themes: `SlotPoolBuilder` marks lines Due, `BonusSlotSampler` draws due lines first and filler under the season allowance with tier weights, `SelectionService` offers only themes with 2+ askable goals weighted by count, `WeeklyThemeQuestService` pays the weekly bonus per goal. Phase 3 appends the three enum members and their five Harmony effects.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony, xunit in `tests/TheLongestYear.Tests`. Run tests with `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`. Build with `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` (auto-deploys to the PC Mods folder; close the game first with `tools/deploy.ps1 -NoLaunch`).

**Spec:** `docs/superpowers/specs/2026-08-27-activity-themes-design.md`. Audit that measured the problem: `docs/superpowers/AUDIT-2026-08-29-bundle-loops.md`.

## Global Constraints

- Work on `master`. Bump `src/TheLongestYear/manifest.json` `Version` by one patch on EVERY code commit, starting at **0.16.42** (0.16.41 is the last commit). Docs-only commits do not bump.
- Commit locally only. Never push, never release, never `gh release`.
- No em dashes anywhere (code comments, i18n, docs, commit messages). Never a `/sdcard/` path.
- Named constants for every number in a condition; type-annotated signatures; one file per responsibility, keep files under ~400 lines.
- Every player-visible string goes through `Strings.Get` and `src/TheLongestYear/i18n/default.json` (plain ASCII in `modifier.*`); `I18nGuardTests` fails on a missing or orphan key.
- Enum values are appended: `Theme.Spelunking = 5, Artisan = 6, Kitchen = 7` (existing values persist in `RunState`).
- Modifier ids exactly: `monster_drops_double`, `machines_slow`, `machines_fast`, `cooked_food_weak`, `animal_double_product`, `monster_damage_up`.
- Filler allowance default `[0, 1, 2, 99]` (`GameplayConfig.ThemeFillerBySeason`); rule E weights per spec table; offer floor `askable >= 2`.
- `CHANGELOG.md` `## Unreleased` gets a line for every user-visible change (per-goal bonus called out).
- Decision recorded here (report it to Jeff): **Phase 1 rules produce effort only.** A Phase 2 rule never sets a season floor; an effort-only id still floors at Winter for gates exactly as today, so no day-28 deadline moves. Reason: a floor set too early bricks a loop, and the spec's Phase 2 text defines effort, not floors.
- Decision: the enum members, `ThemeModifiers` ids and i18n names land at the START of Phase 2 (Task 15) because `I18nGuardTests` iterates every `Theme` value through `ThemeModifiers.For`; the five Harmony effects stay in Phase 3.
- Decision: `RunState.DoubleProduceToday` is cleared on DayEnding (before the night's `FarmAnimal.dayUpdate` writes it), not DayStarted, which would wipe the record before the player could collect.
- Decision: `cooked_food_weak` is three postfixes on `Object.staminaRecoveredOnConsumption`, `Object.healthRecoveredOnConsumption` and `Object.GetFoodOrDrinkBuffs` (category -7 only), which halves the numbers the HUD reports too; same observable effect as the spec's eatObject pair, fewer moving parts.

---

## Phase 1: Availability model Phase 2 (effort rules) + `tly_dumpeffort`

### Task 1: Effort-only derivations in `ItemAvailabilityModel`

**Files:**
- Modify: `src/TheLongestYear.Core/ItemAvailability.cs`
- Test: `tests/TheLongestYear.Tests/ItemAvailabilityTests.cs`

**Interfaces:**
- Produces: `public enum EffortSource { Derived, Price, Override }`; `public sealed record ItemEffort(int Effort, string Basis)`; `ItemAvailability` gains `EffortSource Source = EffortSource.Derived`; `ItemAvailabilityModel(derived, seasonOverrides, effortOverrides, IReadOnlyDictionary<string, ItemEffort>? effortDerived = null)`; `bool HasDerivedEffort(string id)`; `int DerivedEffortCount`.

- [ ] **Step 1: Write the failing tests** (append to `ItemAvailabilityTests`)

```csharp
    [Fact]
    public void An_Effort_Only_Derivation_Keeps_The_Winter_Floor_But_Carries_Its_Effort()
    {
        var model = new ItemAvailabilityModel(
            new Dictionary<string, ItemAvailability>(),
            effortDerived: new Dictionary<string, ItemEffort>
            {
                ["(O)348"] = new ItemEffort(6, "artisan, keg from grapes"),
            });

        ItemAvailability result = model.For("(O)348");

        Assert.Equal(Season.Winter, result.EarliestSeason);
        Assert.Equal(6, result.Effort);
        Assert.Equal(EffortSource.Derived, result.Source);
        Assert.Contains("keg from grapes", result.Basis);
        Assert.False(model.IsDerived("(O)348"));
        Assert.True(model.HasDerivedEffort("(O)348"));
        Assert.Empty(model.UnrecognisedIds);
    }

    [Fact]
    public void An_Unknown_Item_Reports_The_Price_Source()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>());
        Assert.Equal(EffortSource.Price, model.For("(O)999").Source);
        Assert.False(model.HasDerivedEffort("(O)999"));
    }

    [Fact]
    public void An_Effort_Override_Reports_The_Override_Source()
    {
        var model = new ItemAvailabilityModel(
            new Dictionary<string, ItemAvailability>(),
            effortOverrides: new Dictionary<string, int> { ["(O)5"] = 2 });
        Assert.Equal(EffortSource.Override, model.For("(O)5").Source);
    }
```

- [ ] **Step 2: Run to verify they fail**: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter ItemAvailabilityTests` (compile error: `ItemEffort` undefined).

- [ ] **Step 3: Implement**

In `ItemAvailability.cs`, above the record:

```csharp
/// <summary>Where an item's effort number came from: a derivation rule, the price bucket
/// fallback (no rule claimed the id), or the curated effort override table.</summary>
public enum EffortSource { Derived, Price, Override }

/// <summary>Effort without a season floor. Phase 2 rules (gems, geodes, monster drops,
/// artifacts, artisan goods, animal products, dishes, crops, forage) produce these: they say how
/// much work an item is, never when it first exists, so a gate is never moved by them.</summary>
public sealed record ItemEffort(int Effort, string Basis);
```

Change the record to `public sealed record ItemAvailability(Season EarliestSeason, int Effort, string Basis, EffortSource Source = EffortSource.Derived);`

In the model: add field `private readonly IReadOnlyDictionary<string, ItemEffort> _effortDerived;`, constructor parameter `IReadOnlyDictionary<string, ItemEffort>? effortDerived = null` assigned with `?? new Dictionary<string, ItemEffort>(StringComparer.Ordinal)`. Add:

```csharp
    private const string EffortOnlyFloorNote = "floor not derived (Winter)";

    /// <summary>True when either a season rule (fish, crab-pot, metals) or an effort-only rule
    /// placed this id. The goal sampler tiers such ids by effort; the rest use the price bucket.</summary>
    public bool HasDerivedEffort(string qualifiedItemId)
        => qualifiedItemId != null
           && (_derived.ContainsKey(qualifiedItemId) || _effortDerived.ContainsKey(qualifiedItemId));

    public int DerivedEffortCount => _effortDerived.Count;
```

Rewrite `For`:

```csharp
    public ItemAvailability For(string qualifiedItemId)
    {
        if (qualifiedItemId == null) throw new ArgumentNullException(nameof(qualifiedItemId));

        bool known = _derived.TryGetValue(qualifiedItemId, out ItemAvailability? derived);
        bool effortKnown = _effortDerived.TryGetValue(qualifiedItemId, out ItemEffort? effortOnly);
        bool hasSeasonOverride = _seasonOverrides.TryGetValue(qualifiedItemId, out Season overrideSeason);
        bool hasEffortOverride = _effortOverrides.TryGetValue(qualifiedItemId, out int overrideEffort);

        if (!known && !effortKnown && !hasSeasonOverride && !hasEffortOverride)
        {
            _unrecognised.Add(qualifiedItemId);
            return new ItemAvailability(Season.Winter, UnrecognisedEffort, UnrecognisedBasis, EffortSource.Price);
        }

        Season season = derived?.EarliestSeason ?? Season.Winter;
        int effort = derived?.Effort ?? effortOnly?.Effort ?? UnrecognisedEffort;
        string basis = derived?.Basis
            ?? (effortOnly != null ? $"{effortOnly.Basis}; {EffortOnlyFloorNote}" : UnrecognisedBasis);
        EffortSource source = known || effortKnown ? EffortSource.Derived : EffortSource.Price;

        if (hasSeasonOverride) { /* unchanged block */ }
        if (hasEffortOverride)
        {
            basis = $"{basis}; effort override to {overrideEffort}";
            effort = overrideEffort;
            source = EffortSource.Override;
        }

        return new ItemAvailability(season, effort, basis, source);
    }
```

- [ ] **Step 4: Run the whole suite**: expect 1202 + 3 passing.
- [ ] **Step 5: Commit** (bump to 0.16.42): `git add src/TheLongestYear.Core/ItemAvailability.cs tests/TheLongestYear.Tests/ItemAvailabilityTests.cs src/TheLongestYear/manifest.json && git commit -m "v0.16.42: availability model carries effort-only derivations (Phase 2 groundwork, no floor changes)"`

---

### Task 2: `EffortData` raw records and `ContextTagMatcher`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/EffortData.cs`
- Create: `src/TheLongestYear.Core/Availability/ContextTagMatcher.cs`
- Modify: `src/TheLongestYear.Core/ItemPoolModel.cs` (`RawObjectEntry` gains `string Name = ""`)
- Test: `tests/TheLongestYear.Tests/ContextTagMatcherTests.cs`

**Interfaces:**
- Produces (all in `TheLongestYear.Core.Availability`):

```csharp
public sealed record RawGeodeDrop(string GeodeItemId, string ItemId, double Chance);
public sealed record RawMonsterDrop(string MonsterName, string ItemId, double Chance);
public sealed record RawArtifactSpot(string Location, string ItemId, double Chance);
public sealed record RawMachineRule(string MachineItemId, string? RequiredItemId, IReadOnlyList<string> RequiredTags, IReadOnlyList<string> OutputItemIds, int MinutesUntilReady, int DaysUntilReady);
public sealed record RawFarmAnimal(string Name, string Building, int PurchasePrice, int DaysToProduce, IReadOnlyList<string> ProduceIds, IReadOnlyList<string> DeluxeProduceIds);
public sealed record RawBuilding(string Name, string? BuildingToUpgrade);
public sealed record RawCookingRecipe(string Name, IReadOnlyList<string> IngredientIds, string OutputItemId, string UnlockCondition);
public sealed record RawFishPondProduct(string ItemId, int RequiredPopulation);
public sealed record RawFishPondRule(IReadOnlyList<string> RequiredTags, IReadOnlyList<RawFishPondProduct> Products);
public sealed record RawCropGrowth(string HarvestItemId, int GrowthDays, bool Regrows, bool Trellis);
public sealed class EffortData { Objects, GeodeDrops, MonsterDrops, ArtifactSpots, MachineRules, MachineUnlocks (IReadOnlyDictionary<string,string> machine id -> unlock condition), Animals, Buildings, CookingRecipes, FishPonds, Crops, ForageSpawns (IReadOnlyList<RawSpawnEntry>) ; all init-only with empty defaults }
public static class ContextTagMatcher { bool Matches(string bareId, RawObjectEntry obj, string tag); string ItemTag(string name); IReadOnlyList<string> IdsMatchingAll(IReadOnlyDictionary<string, RawObjectEntry> objects, IReadOnlyList<string> tags) /* qualified ids, ordinal order */ }
```
- Item ids inside these records are QUALIFIED (`(O)24`); the glue normalises with `BundleParsing.NormalizeItemId`. `Objects` is keyed by bare id like the pools.

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ContextTagMatcherTests
{
    private static RawObjectEntry Obj(int category, string name, params string[] tags)
        => new("Basic", category, 10, false, tags, name);

    [Fact]
    public void Category_tags_match_by_category_number()
    {
        Assert.True(ContextTagMatcher.Matches("398", Obj(-79, "Grape"), "category_fruit"));
        Assert.False(ContextTagMatcher.Matches("24", Obj(-75, "Parsnip"), "category_fruit"));
        Assert.True(ContextTagMatcher.Matches("184", Obj(-6, "Milk"), "category_milk"));
    }

    [Fact]
    public void Item_and_id_tags_match_by_name_and_id()
    {
        Assert.True(ContextTagMatcher.Matches("698", Obj(-4, "Sturgeon"), "item_sturgeon"));
        Assert.True(ContextTagMatcher.Matches("795", Obj(-4, "Void Salmon"), "item_void_salmon"));
        Assert.True(ContextTagMatcher.Matches("262", Obj(-75, "Wheat"), "id_o_262"));
        Assert.False(ContextTagMatcher.Matches("262", Obj(-75, "Wheat"), "id_o_304"));
    }

    [Fact]
    public void Other_tags_fall_back_to_the_objects_own_tag_list()
    {
        Assert.True(ContextTagMatcher.Matches("92", Obj(-16, "Sap", "sap_item"), "sap_item"));
        Assert.False(ContextTagMatcher.Matches("92", Obj(-16, "Sap"), "sap_item"));
    }

    [Fact]
    public void IdsMatchingAll_returns_qualified_ids_in_ordinal_order()
    {
        var objects = new Dictionary<string, RawObjectEntry>
        {
            ["398"] = Obj(-79, "Grape"), ["24"] = Obj(-75, "Parsnip"), ["613"] = Obj(-79, "Apple"),
        };
        Assert.Equal(new[] { "(O)398", "(O)613" },
            ContextTagMatcher.IdsMatchingAll(objects, new[] { "category_fruit" }));
    }
}
```

- [ ] **Step 2: Run, expect compile failure.**
- [ ] **Step 3: Implement**

`ItemPoolModel.cs`: `public sealed record RawObjectEntry(string Type, int Category, int Price, bool ExcludeFromRandomSale, IReadOnlyList<string> ContextTags, string Name = "");`

`EffortData.cs`: the records above plus

```csharp
/// <summary>Everything the effort rules read, snapshotted from the game's own data tables by the
/// mod at SaveLoaded (Loop/GameEffortData). Core never touches Game1; tests hand-build these.</summary>
public sealed class EffortData
{
    public IReadOnlyDictionary<string, RawObjectEntry> Objects { get; init; } = new Dictionary<string, RawObjectEntry>();
    public IReadOnlyList<RawGeodeDrop> GeodeDrops { get; init; } = new List<RawGeodeDrop>();
    public IReadOnlyList<RawMonsterDrop> MonsterDrops { get; init; } = new List<RawMonsterDrop>();
    public IReadOnlyList<RawArtifactSpot> ArtifactSpots { get; init; } = new List<RawArtifactSpot>();
    public IReadOnlyList<RawMachineRule> MachineRules { get; init; } = new List<RawMachineRule>();
    public IReadOnlyDictionary<string, string> MachineUnlocks { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<RawFarmAnimal> Animals { get; init; } = new List<RawFarmAnimal>();
    public IReadOnlyList<RawBuilding> Buildings { get; init; } = new List<RawBuilding>();
    public IReadOnlyList<RawCookingRecipe> CookingRecipes { get; init; } = new List<RawCookingRecipe>();
    public IReadOnlyList<RawFishPondRule> FishPonds { get; init; } = new List<RawFishPondRule>();
    public IReadOnlyList<RawCropGrowth> Crops { get; init; } = new List<RawCropGrowth>();
    public IReadOnlyList<RawSpawnEntry> ForageSpawns { get; init; } = new List<RawSpawnEntry>();
}
```

`ContextTagMatcher.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Mirrors the tags the game synthesises at runtime (ItemContextTagManager): the
/// category_* family maps to Data/Objects Category, item_* to the sanitised internal name, id_o_*
/// to the bare id. Anything else is looked up in the object's own ContextTags list.</summary>
public static class ContextTagMatcher
{
    private const string ItemTagPrefix = "item_";
    private const string ObjectIdTagPrefix = "id_o_";

    private static readonly IReadOnlyDictionary<string, int> CategoryTags =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["category_gem"] = -2, ["category_fish"] = -4, ["category_egg"] = -5,
            ["category_milk"] = -6, ["category_cooking"] = -7, ["category_minerals"] = -12,
            ["category_metal_resources"] = -15, ["category_animal_product"] = -18,
            ["category_artisan_goods"] = -26, ["category_syrup"] = -27,
            ["category_monster_loot"] = -28, ["category_seeds"] = -74,
            ["category_vegetable"] = -75, ["category_fruit"] = -79,
            ["category_flower"] = -80, ["category_greens"] = -81,
        };

    public static bool Matches(string bareId, RawObjectEntry obj, string tag)
    {
        if (obj == null || string.IsNullOrEmpty(tag)) return false;
        if (CategoryTags.TryGetValue(tag, out int category)) return obj.Category == category;
        if (tag.StartsWith(ObjectIdTagPrefix, StringComparison.Ordinal))
            return string.Equals(tag.Substring(ObjectIdTagPrefix.Length), bareId, StringComparison.OrdinalIgnoreCase);
        if (tag.StartsWith(ItemTagPrefix, StringComparison.Ordinal))
            return tag == ItemTag(obj.Name);
        return obj.ContextTags != null && obj.ContextTags.Contains(tag);
    }

    public static string ItemTag(string name)
        => ItemTagPrefix + (name ?? "").ToLowerInvariant().Replace(' ', '_').Replace("'", "");

    public static IReadOnlyList<string> IdsMatchingAll(
        IReadOnlyDictionary<string, RawObjectEntry> objects, IReadOnlyList<string> tags)
        => objects
            .Where(kv => tags.All(t => Matches(kv.Key, kv.Value, t)))
            .Select(kv => BundleParsing.NormalizeItemId(kv.Key))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
}
```

- [ ] **Step 4: Run tests, expect pass.**
- [ ] **Step 5: Commit** (0.16.43): "v0.16.43: EffortData raw records and ContextTagMatcher for the Phase 2 effort rules"

---

### Task 3: `MineAreas` and `MineralNodeAvailability` (gems and minerals from nodes)

**Files:**
- Create: `src/TheLongestYear.Core/Availability/MineAreas.cs`
- Create: `src/TheLongestYear.Core/Availability/MineralNodeAvailability.cs`
- Test: `tests/TheLongestYear.Tests/MineralNodeAvailabilityTests.cs`

**Interfaces:**
- `public static class MineAreas { const int Area0 = 0; Area40 = 40; Area80 = 80; SkullCavern = 121; int Effort(int area); string Label(int area) }`
- `public static class MineralNodeAvailability { ItemEffort? Derive(string qualifiedId) }`

- [ ] **Step 1: Failing tests**

```csharp
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class MineralNodeAvailabilityTests
{
    [Theory]
    [InlineData("(O)80", 1)]   // Quartz, any floor
    [InlineData("(O)66", 1)]   // Amethyst node, floors 1 to 39
    [InlineData("(O)70", 3)]   // Jade node, floors 41 to 79
    [InlineData("(O)64", 5)]   // Ruby node, floors 81 to 119 (spec: "a gem node at area 80 scores 5")
    [InlineData("(O)72", 5)]   // Diamond, floor 80 node
    [InlineData("(O)74", 7)]   // Prismatic Shard, Skull Cavern iridium nodes and mystic stones
    public void Node_gems_and_minerals_score_their_shallowest_area(string id, int expected)
    {
        ItemEffort? result = MineralNodeAvailability.Derive(id);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Effort);
    }

    [Fact]
    public void An_id_the_node_table_does_not_know_yields_null()
        => Assert.Null(MineralNodeAvailability.Derive("(O)24"));

    [Theory]
    [InlineData(0, 1)] [InlineData(10, 1)] [InlineData(40, 3)] [InlineData(80, 5)] [InlineData(121, 7)]
    public void Area_effort_matches_the_metals_scale(int area, int effort)
        => Assert.Equal(effort, MineAreas.Effort(area));
}
```

- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement**

`MineAreas.cs`:

```csharp
namespace TheLongestYear.Core.Availability;

/// <summary>Mine areas as the game numbers them (MineShaft.getMineArea): 0 and 10 are floors 1
/// to 39, 40 is 41 to 79, 80 is 81 to 119, 121 is the Skull Cavern. Effort per area is the scale
/// MetalsAvailability already uses (copper 1, iron 3, gold 5, iridium 7).</summary>
public static class MineAreas
{
    public const int Area0 = 0;
    public const int Area10 = 10;
    public const int Area40 = 40;
    public const int Area80 = 80;
    public const int SkullCavern = 121;

    private const int ShallowEffort = 1;
    private const int MidEffort = 3;
    private const int DeepEffort = 5;
    private const int SkullEffort = 7;

    public static int Effort(int area) => area switch
    {
        Area0 or Area10 => ShallowEffort,
        Area40 => MidEffort,
        Area80 => DeepEffort,
        _ => SkullEffort,
    };

    public static string Label(int area) => area switch
    {
        Area0 or Area10 => "mine floors 1 to 39",
        Area40 => "mine floors 41 to 79",
        Area80 => "mine floors 81 to 119",
        _ => "Skull Cavern",
    };
}
```

`MineralNodeAvailability.cs`: a `Dictionary<string, (int Area, string Note)>` table with a doc comment citing `MineShaft.getRandomGemRichStoneForThisLevel` (amethyst 66 / topaz 68 before floor 40; jade 70 / aquamarine 62 from area 40; ruby 64 / emerald 60 from area 80), `MineShaft.getRandomItemForThisLevel` (Quartz 80 any area, Earth Crystal 86 area 0, Frozen Tear 84 area 40, Fire Quartz 82 area 80) and the iridium node / mystic stone for Prismatic Shard 74 (Skull Cavern). Entries: `(O)80`,`(O)86`,`(O)66`,`(O)68` area 0; `(O)84`,`(O)70`,`(O)62` area 40; `(O)82`,`(O)64`,`(O)60`,`(O)72` area 80; `(O)74` area 121. `Derive` returns `new ItemEffort(MineAreas.Effort(area), $"node, {note}, {MineAreas.Label(area)}, effort {effort}")` or null.

- [ ] **Step 4: Run tests.**
- [ ] **Step 5: Commit** (0.16.44): "v0.16.44: effort rule for gems and minerals from mine nodes (MineShaft code facts)"

---

### Task 4: `GeodeAvailability`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/GeodeAvailability.cs`
- Test: `tests/TheLongestYear.Tests/GeodeAvailabilityTests.cs`

**Interfaces:**
- `public static class GeodeAvailability { ItemEffort? Derive(string qualifiedId, IReadOnlyList<RawGeodeDrop> drops); int ChanceStep(double chance); IReadOnlyList<RawGeodeDrop> DefaultTableDrops(string geodeQualifiedId) }`
- Geode efforts: `(O)535` 1, `(O)536` 3, `(O)537` 5, `(O)749` 4; other geodes (Trove 275, Golden Coconut 791, Mystery Box) ignored.

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class GeodeAvailabilityTests
{
    [Fact]
    public void A_rare_drop_from_a_frozen_geode_scores_geode_plus_two()
    {
        var drops = new List<RawGeodeDrop> { new("(O)536", "(O)541", 1.0 / 32) };
        ItemEffort? result = GeodeAvailability.Derive("(O)541", drops);
        Assert.NotNull(result);
        Assert.Equal(3 + 2, result!.Effort);
    }

    [Fact]
    public void The_easiest_geode_wins()
    {
        var drops = new List<RawGeodeDrop> { new("(O)537", "(O)541", 0.5), new("(O)535", "(O)541", 0.5) };
        Assert.Equal(1, GeodeAvailability.Derive("(O)541", drops)!.Effort);
    }

    [Theory]
    [InlineData(0.5, 0)] [InlineData(0.125, 0)] [InlineData(0.1, 1)] [InlineData(0.05, 1)] [InlineData(0.01, 2)]
    public void Chance_steps(double chance, int step) => Assert.Equal(step, GeodeAvailability.ChanceStep(chance));

    [Fact]
    public void Unknown_geodes_and_unknown_items_yield_null()
    {
        Assert.Null(GeodeAvailability.Derive("(O)541", new List<RawGeodeDrop> { new("(O)275", "(O)541", 1) }));
        Assert.Null(GeodeAvailability.Derive("(O)24", new List<RawGeodeDrop>()));
    }

    [Fact]
    public void Default_table_covers_the_code_only_ore_and_stone_rows()
    {
        var rows = GeodeAvailability.DefaultTableDrops("(O)535");
        Assert.Contains(rows, r => r.ItemId == "(O)390");   // Stone
        Assert.Contains(rows, r => r.ItemId == "(O)378");   // Copper Ore
        Assert.Contains(rows, r => r.ItemId == "(O)86");    // Earth Crystal
        Assert.Empty(GeodeAvailability.DefaultTableDrops("(O)275"));
    }
}
```

- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort for anything a geode can yield: the easiest geode that can drop it, plus a
/// rarity step from that geode's per-drop chance (Data/Objects GeodeDrops). The code-only half of
/// the table (Utility.getTreasureFromGeode, decompile Utility.cs:6368: stone, clay, coal, the ores
/// and the area crystal) is exposed by <see cref="DefaultTableDrops"/> so the glue can add it for
/// geodes flagged GeodeDropsDefaultItems.</summary>
public static class GeodeAvailability
{
    private const double CommonChance = 1.0 / 8;
    private const double UncommonChance = 1.0 / 20;
    private const double DefaultTableShare = 0.5;

    private sealed record GeodeRule(int Effort, string Label);

    private static readonly IReadOnlyDictionary<string, GeodeRule> Geodes =
        new Dictionary<string, GeodeRule>(StringComparer.Ordinal)
        {
            ["(O)535"] = new(1, "Geode, floors 1 to 39"),
            ["(O)536"] = new(3, "Frozen Geode, floors 41 to 79"),
            ["(O)537"] = new(5, "Magma Geode, floors 81 to 119"),
            ["(O)749"] = new(4, "Omni Geode, any floor at low odds, Skull Cavern reliably"),
        };

    private static readonly IReadOnlyDictionary<string, string[]> DefaultTable =
        new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["(O)535"] = new[] { "(O)390", "(O)330", "(O)382", "(O)378", "(O)380", "(O)86" },
            ["(O)536"] = new[] { "(O)390", "(O)330", "(O)382", "(O)378", "(O)380", "(O)384", "(O)84" },
            ["(O)537"] = new[] { "(O)390", "(O)330", "(O)382", "(O)378", "(O)380", "(O)384", "(O)386", "(O)82" },
            ["(O)749"] = new[] { "(O)390", "(O)330", "(O)382", "(O)378", "(O)380", "(O)384", "(O)386", "(O)82", "(O)84", "(O)86" },
        };

    public static int ChanceStep(double chance)
        => chance >= CommonChance ? 0 : chance >= UncommonChance ? 1 : 2;

    public static IReadOnlyList<RawGeodeDrop> DefaultTableDrops(string geodeQualifiedId)
        => DefaultTable.TryGetValue(geodeQualifiedId, out string[]? ids)
            ? ids.Select(id => new RawGeodeDrop(geodeQualifiedId, id, DefaultTableShare / ids.Length)).ToList()
            : Array.Empty<RawGeodeDrop>();

    public static ItemEffort? Derive(string qualifiedId, IReadOnlyList<RawGeodeDrop> drops)
    {
        if (drops == null) throw new ArgumentNullException(nameof(drops));
        ItemEffort? best = null;
        foreach (RawGeodeDrop drop in drops)
        {
            if (drop.ItemId != qualifiedId || !Geodes.TryGetValue(drop.GeodeItemId, out GeodeRule? geode))
                continue;
            int effort = geode.Effort + ChanceStep(drop.Chance);
            if (best == null || effort < best.Effort)
                best = new ItemEffort(effort,
                    $"geode, {geode.Label}, chance {drop.Chance:0.###} (+{ChanceStep(drop.Chance)}), effort {effort}");
        }
        return best;
    }
}
```

- [ ] **Step 4: Run tests.**
- [ ] **Step 5: Commit** (0.16.45): "v0.16.45: effort rule for geode contents (Data/Objects GeodeDrops plus the code table)"

---

### Task 5: `MonsterDropAvailability`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/MonsterDropAvailability.cs`
- Test: `tests/TheLongestYear.Tests/MonsterDropAvailabilityTests.cs`

**Interfaces:** `public static class MonsterDropAvailability { ItemEffort? Derive(string qualifiedId, IReadOnlyList<RawMonsterDrop> drops); int ChanceStep(double chance); int? SpawnArea(string monsterName) }`

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class MonsterDropAvailabilityTests
{
    [Fact]
    public void Bat_wing_from_a_shallow_bat_at_ninety_percent_scores_one()
    {
        var drops = new List<RawMonsterDrop> { new("Bat", "(O)767", 0.9) };
        Assert.Equal(1, MonsterDropAvailability.Derive("(O)767", drops)!.Effort);
    }

    [Fact]
    public void Minimum_over_every_monster_that_drops_it()
    {
        var drops = new List<RawMonsterDrop> { new("Serpent", "(O)769", 0.9), new("Dust Spirit", "(O)769", 0.05) };
        // Serpent: Skull Cavern 7 + 0; Dust Spirit: area 40 3 + 2 = 5.
        Assert.Equal(5, MonsterDropAvailability.Derive("(O)769", drops)!.Effort);
    }

    [Fact]
    public void Unknown_monsters_are_skipped_and_an_unclaimed_item_is_null()
    {
        var drops = new List<RawMonsterDrop> { new("SVE Wyvern", "(O)769", 1.0) };
        Assert.Null(MonsterDropAvailability.Derive("(O)769", drops));
    }

    [Theory]
    [InlineData(0.9, 0)] [InlineData(0.5, 0)] [InlineData(0.25, 1)] [InlineData(0.1, 1)] [InlineData(0.05, 2)]
    public void Chance_steps(double chance, int step) => Assert.Equal(step, MonsterDropAvailability.ChanceStep(chance));
}
```

- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement.** Constants `FrequentChance = 0.5`, `OccasionalChance = 0.1`. Spawn table (doc comment cites `MineShaft.getMonsterForThisLevel`, decompile MineShaft.cs:4033, and the constructors that pick a name by floor: GreenSlime, Bat, RockCrab), name -> area:

  - Area 0: `Green Slime`, `Duggy`, `Rock Crab`, `Bug`, `Grub`, `Fly`, `Stone Golem`, `Bat`, `Big Slime`
  - Area 40: `Dust Spirit`, `Frost Bat`, `Frost Jelly`, `Ghost`, `Skeleton`, `Blue Squid`
  - Area 80: `Lava Bat`, `Sludge`, `Shadow Brute`, `Shadow Shaman`, `Metal Head`, `Lava Crab`, `Squid Kid`, `Haunted Skull`
  - Skull Cavern 121: `Serpent`, `Royal Serpent`, `Mummy`, `Carbon Ghost`, `Putrid Ghost`, `Iridium Bat`, `Iridium Crab`, `Pepper Rex`, `Armored Bug`, `Assassin Bug`, `Stick Bug`, `Skeleton Mage`, `Shadow Sniper`, `Spider`, `Tiger Slime`, `Lava Lurk`, `Hot Head`, `Magma Sprite`, `Magma Sparker`, `Magma Duggy`, `False Magma Cap`, `Dwarvish Sentry`, `Fireball`, `Spiker`

  `Derive`: for each drop with matching id and known area: `MineAreas.Effort(area) + ChanceStep(chance)`, keep the minimum; basis `$"monster drop, {name} ({MineAreas.Label(area)}) at {chance:0.##} (+{step}), effort {e}"`. Null when nothing matched.

- [ ] **Step 4: Run tests.**
- [ ] **Step 5: Commit** (0.16.46): "v0.16.46: effort rule for monster drops (Data/Monsters chances, MineShaft spawn floors)"

---

### Task 6: `ArtifactAvailability` and `ItemQueryIds`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/ItemQueryIds.cs`
- Create: `src/TheLongestYear.Core/Availability/ArtifactAvailability.cs`
- Test: `tests/TheLongestYear.Tests/ArtifactAvailabilityTests.cs`

**Interfaces:**
- `public static class ItemQueryIds { IReadOnlyList<string> Expand(string? itemIdOrQuery) }`: `"(O)107"` -> `["(O)107"]`; bare `"107"` -> `["(O)107"]`; `"RANDOM_ITEMS (O) 96 127"` -> `(O)96 .. (O)127`; `"FLAVORED_ITEM Wine DROP_IN_ID"` -> `["(O)348"]` (map: Wine 348, Juice 350, Jelly 344, Pickles 342, Roe 812, AgedRoe 447, Honey 340, DriedFruit `(O)DriedFruit`, DriedMushrooms `(O)DriedMushrooms`, SmokedFish `(O)SmokedFish`, Bait `(O)SpecificBait`); any other query -> empty.
- `public static class ArtifactAvailability { ItemEffort? Derive(string qualifiedId, IReadOnlyList<RawArtifactSpot> spots); int ReachEffort(string location); int ChanceStep(double chance) }`

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ArtifactAvailabilityTests
{
    [Fact]
    public void Dinosaur_egg_is_the_minimum_over_its_spots()
    {
        var spots = new List<RawArtifactSpot>
        {
            new("Mountain", "(O)107", 0.005),   // 1 + 2
            new("SkullCave", "(O)107", 0.02),   // 7 + 1
        };
        ItemEffort? result = ArtifactAvailability.Derive("(O)107", spots);
        Assert.Equal(3, result!.Effort);
    }

    [Theory]
    [InlineData("Farm", 1)] [InlineData("Default", 1)] [InlineData("BusStop", 1)]
    [InlineData("UndergroundMine", 2)] [InlineData("Desert", 4)] [InlineData("SkullCave", 7)] [InlineData("IslandNorth", 7)]
    public void Reach_effort_by_location(string location, int effort)
        => Assert.Equal(effort, ArtifactAvailability.ReachEffort(location));

    [Theory]
    [InlineData(0.5, 0)] [InlineData(0.1, 0)] [InlineData(0.05, 1)] [InlineData(0.01, 2)] [InlineData(0.001, 3)]
    public void Chance_steps(double chance, int step) => Assert.Equal(step, ArtifactAvailability.ChanceStep(chance));

    [Fact]
    public void Unclaimed_item_is_null()
        => Assert.Null(ArtifactAvailability.Derive("(O)24", new List<RawArtifactSpot>()));
}

public class ItemQueryIdsTests
{
    [Fact]
    public void Plain_and_bare_ids_are_qualified()
    {
        Assert.Equal(new[] { "(O)107" }, ItemQueryIds.Expand("(O)107"));
        Assert.Equal(new[] { "(O)107" }, ItemQueryIds.Expand("107"));
    }

    [Fact]
    public void Random_items_ranges_expand()
        => Assert.Equal(new[] { "(O)96", "(O)97", "(O)98" }, ItemQueryIds.Expand("RANDOM_ITEMS (O) 96 98"));

    [Fact]
    public void Flavored_items_map_to_their_base_item()
    {
        Assert.Equal(new[] { "(O)348" }, ItemQueryIds.Expand("FLAVORED_ITEM Wine DROP_IN_ID"));
        Assert.Equal(new[] { "(O)DriedFruit" }, ItemQueryIds.Expand("FLAVORED_ITEM DriedFruit DROP_IN_ID"));
    }

    [Fact]
    public void Unknown_queries_and_blanks_expand_to_nothing()
    {
        Assert.Empty(ItemQueryIds.Expand("LOST_BOOK_OR_ITEM (O)770"));
        Assert.Empty(ItemQueryIds.Expand(null));
        Assert.Empty(ItemQueryIds.Expand("(W)4"));
    }
}
```

- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement.** `ItemQueryIds.Expand`: trim; empty -> empty; starts with `RANDOM_ITEMS ` -> split by space, require `(O)` in position 1 and two ints, return the range (cap the range at 500 entries); starts with `FLAVORED_ITEM ` -> look up token 1 in the map; starts with `(` -> return it only if it starts with `(O)`; else if all digits or a plain object id (no space) -> `BundleParsing.NormalizeItemId(text)`; else empty.

  `ArtifactAvailability`: reach table `(Marker, Effort)`: `("SkullCave", 7), ("Island", 7), ("Desert", 4), ("UndergroundMine", 2), ("Mine", 2)`, default 1 (matched as ordinal substrings, first hit wins; empty or "Default" -> 1). Constants `CommonChance = 0.1`, `UncommonChance = 0.02`, `RareChance = 0.005`; steps 0/1/2/3. `Derive`: min over spots with matching id of `ReachEffort(loc) + ChanceStep(chance)`; basis `$"artifact spot, {location} at {chance:0.####} (+{step}), effort {e}"`.

- [ ] **Step 4: Run tests.**
- [ ] **Step 5: Commit** (0.16.47): "v0.16.47: effort rule for artifacts (Data/Locations ArtifactSpots, reach by location) and item-query id expansion"

---

### Task 7: `AnimalProductAvailability`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/AnimalProductAvailability.cs`
- Test: `tests/TheLongestYear.Tests/AnimalProductAvailabilityTests.cs`

**Interfaces:** `public static class AnimalProductAvailability { ItemEffort? Derive(string qualifiedId, IReadOnlyList<RawFarmAnimal> animals, IReadOnlyList<RawBuilding> buildings); int HousingEffort(string building, IReadOnlyList<RawBuilding> buildings); int PriceStep(int purchasePrice) }`

- [ ] **Step 1: Failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class AnimalProductAvailabilityTests
{
    private static readonly List<RawBuilding> Buildings = new()
    {
        new("Coop", null), new("Big Coop", "Coop"), new("Deluxe Coop", "Big Coop"),
        new("Barn", null), new("Big Barn", "Barn"), new("Deluxe Barn", "Big Barn"),
    };

    private static readonly List<RawFarmAnimal> Animals = new()
    {
        new("White Chicken", "Coop", 800, 1, new[] { "(O)176" }, new[] { "(O)174" }),
        new("Pig", "Deluxe Barn", 16000, 1, new[] { "(O)430" }, new string[0]),
        new("Ostrich", "Barn", -1, 7, new[] { "(O)289" }, new string[0]),
    };

    [Fact]
    public void Egg_is_one() => Assert.Equal(1, AnimalProductAvailability.Derive("(O)176", Animals, Buildings)!.Effort);

    [Fact]
    public void Large_egg_adds_the_deluxe_step() => Assert.Equal(2, AnimalProductAvailability.Derive("(O)174", Animals, Buildings)!.Effort);

    [Fact]
    public void Truffle_is_deluxe_barn_three_plus_pig_price_two()
        => Assert.Equal(5, AnimalProductAvailability.Derive("(O)430", Animals, Buildings)!.Effort);

    [Fact]
    public void An_animal_that_is_not_for_sale_adds_the_incubator_step()
        // Barn 1 + not for sale 3 + DaysToProduce > 1 = 5
        => Assert.Equal(5, AnimalProductAvailability.Derive("(O)289", Animals, Buildings)!.Effort);

    [Fact]
    public void Unclaimed_item_is_null() => Assert.Null(AnimalProductAvailability.Derive("(O)24", Animals, Buildings));

    [Theory]
    [InlineData(800, 0)] [InlineData(999, 0)] [InlineData(1000, 1)] [InlineData(3999, 1)] [InlineData(4000, 2)]
    public void Price_steps(int price, int step) => Assert.Equal(step, AnimalProductAvailability.PriceStep(price));
}
```

- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement.** Constants: `BaseHousingEffort = 1`, `CheapPrice = 1000`, `MidPrice = 4000`, `NotForSaleStep = 3`, `DeluxeStep = 1`, `SlowProduceStep = 1`, `MaxChainDepth = 8`. `HousingEffort`: walk `BuildingToUpgrade` from the animal's building to the root, counting links (unknown building = 0 links) -> `BaseHousingEffort + links`. `PriceStep`: `< CheapPrice` 0, `< MidPrice` 1, else 2; a negative price is handled in `Derive` as `NotForSaleStep` instead. `Derive`: for each animal listing the id in `ProduceIds` or `DeluxeProduceIds`: `housing + (price < 0 ? NotForSaleStep : PriceStep(price)) + (deluxe ? DeluxeStep : 0) + (DaysToProduce > 1 ? SlowProduceStep : 0)`, minimum; basis `$"animal product, {name} in {building} (housing {h}), price {price} (+{p}){deluxeNote}{slowNote}, effort {e}"`.

- [ ] **Step 4: Run tests.**
- [ ] **Step 5: Commit** (0.16.48): "v0.16.48: effort rule for animal products (Data/FarmAnimals, Data/Buildings upgrade chain)"

---

### Task 8: `ArtisanAvailability` (machines) and `MachineUnlock`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/ArtisanAvailability.cs`
- Test: `tests/TheLongestYear.Tests/ArtisanAvailabilityTests.cs`

**Interfaces:**
- `public static class ArtisanAvailability { ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf); int MachineUnlockEffort(string? unlockCondition); int TimeStep(int minutesUntilReady, int daysUntilReady) }`
- `effortOf(id)` is the composer's memoised resolver (Task 13); returns null when nothing can place the id.
- Constants: `MinutesPerDay = 1440`, `LongDays = 4` (a run of 4 days or more is +2; under a day 0; between 1), `DefaultUnlockEffort = 1`, `MidSkillLevel = 4`, `HighSkillLevel = 8`, `QuestUnlockEffort = 3`, `NoInputEffort = 0`, `UnresolvedInputEffort = ItemAvailabilityModel.UnrecognisedEffort`.

- [ ] **Step 1: Failing tests**

```csharp
using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ArtisanAvailabilityTests
{
    private static RawObjectEntry Obj(int category, string name) => new("Basic", category, 10, false, new string[0], name);

    private static EffortData Data() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry>
        {
            ["398"] = Obj(-79, "Grape"), ["613"] = Obj(-79, "Apple"), ["262"] = Obj(-75, "Wheat"), ["184"] = Obj(-6, "Milk"),
        },
        MachineRules = new List<RawMachineRule>
        {
            new("(BC)12", null, new[] { "category_fruit" }, new[] { "(O)348" }, 10000, -1),   // Keg: wine, 6.9 days
            new("(BC)12", "(O)262", new string[0], new[] { "(O)346" }, 1750, -1),            // Keg: beer
            new("(BC)16", "(O)184", new string[0], new[] { "(O)424" }, 200, -1),             // Cheese Press
            new("(BC)10", null, new string[0], new[] { "(O)340" }, -1, 4),                   // Bee House: honey, 4 days, no input
        },
        MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "Farming 2", ["(BC)16"] = "Farming 6", ["(BC)10"] = "Farming 3" },
    };

    private static int? Effort(string id) => id switch { "(O)398" => 3, "(O)613" => 4, "(O)262" => 2, "(O)184" => 1, _ => null };

    [Fact]
    public void Wine_is_the_cheapest_fruit_plus_machine_plus_two_for_a_week_in_the_keg()
        => Assert.Equal(3 + 1 + 2, ArtisanAvailability.Derive("(O)348", Data(), Effort)!.Effort);

    [Fact]
    public void Cheese_is_milk_plus_a_level_six_press_and_no_time_step()
        => Assert.Equal(1 + 2 + 0, ArtisanAvailability.Derive("(O)424", Data(), Effort)!.Effort);

    [Fact]
    public void Honey_has_no_input_and_a_four_day_wait()
        => Assert.Equal(0 + 1 + 2, ArtisanAvailability.Derive("(O)340", Data(), Effort)!.Effort);

    [Fact]
    public void Unclaimed_output_is_null() => Assert.Null(ArtisanAvailability.Derive("(O)24", Data(), Effort));

    [Theory]
    [InlineData(null, 3)] [InlineData("null", 3)] [InlineData("default", 1)] [InlineData("Farming 2", 1)]
    [InlineData("s Farming 3", 1)] [InlineData("Farming 4", 2)] [InlineData("Farming 7", 2)] [InlineData("Farming 8", 3)] [InlineData("f Robin 6", 3)]
    public void Machine_unlock_effort(string? unlock, int effort) => Assert.Equal(effort, ArtisanAvailability.MachineUnlockEffort(unlock));

    [Theory]
    [InlineData(200, -1, 0)] [InlineData(1750, -1, 1)] [InlineData(10000, -1, 2)] [InlineData(-1, 4, 2)] [InlineData(-1, 1, 1)] [InlineData(-1, 14, 2)]
    public void Time_step(int minutes, int days, int step) => Assert.Equal(step, ArtisanAvailability.TimeStep(minutes, days));
}
```

- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement.**

```csharp
public static ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf)
{
    ItemEffort? best = null;
    foreach (RawMachineRule rule in data.MachineRules)
    {
        if (!rule.OutputItemIds.Contains(qualifiedId)) continue;
        (int inputEffort, string inputNote) = InputEffort(rule, data, effortOf);
        int machine = MachineUnlockEffort(data.MachineUnlocks.TryGetValue(rule.MachineItemId, out string? unlock) ? unlock : null);
        int time = TimeStep(rule.MinutesUntilReady, rule.DaysUntilReady);
        int effort = inputEffort + machine + time;
        if (best == null || effort < best.Effort)
            best = new ItemEffort(effort, $"artisan, {rule.MachineItemId}: input {inputNote} ({inputEffort}) + machine {machine} + time {time}, effort {effort}");
    }
    return best;
}

private static (int Effort, string Note) InputEffort(RawMachineRule rule, EffortData data, Func<string, int?> effortOf)
{
    if (!string.IsNullOrEmpty(rule.RequiredItemId))
    {
        string id = BundleParsing.NormalizeItemId(rule.RequiredItemId);
        return (effortOf(id) ?? UnresolvedInputEffort, id);
    }
    if (rule.RequiredTags.Count > 0)
    {
        int? cheapest = null; string cheapestId = "";
        foreach (string id in ContextTagMatcher.IdsMatchingAll(data.Objects, rule.RequiredTags))
        {
            int? e = effortOf(id);
            if (e != null && (cheapest == null || e < cheapest)) { cheapest = e; cheapestId = id; }
        }
        return cheapest == null
            ? (UnresolvedInputEffort, $"tags {string.Join("+", rule.RequiredTags)} (no member derived)")
            : (cheapest.Value, $"cheapest {cheapestId} of {string.Join("+", rule.RequiredTags)}");
    }
    return (NoInputEffort, "none");
}
```

`MachineUnlockEffort`: null/blank/`null`/`none` -> `QuestUnlockEffort`; `default` -> `DefaultUnlockEffort`; split on spaces, drop a leading `s`; if the last token parses as an int and the first is a skill name -> `< MidSkillLevel` 1, `< HighSkillLevel` 2, else 3; a leading `f` (friendship) or anything unparsed -> 3. `TimeStep`: `minutes = days >= 0 ? days * MinutesPerDay : minutes`; `< MinutesPerDay` -> 0; `< LongDays * MinutesPerDay` -> 1; else 2.

- [ ] **Step 4: Run tests.**
- [ ] **Step 5: Commit** (0.16.49): "v0.16.49: effort rule for artisan goods (Data/Machines rules, Data/CraftingRecipes unlocks, ready time)"

---

### Task 9: `FishPondAvailability`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/FishPondAvailability.cs`
- Test: `tests/TheLongestYear.Tests/FishPondAvailabilityTests.cs`

**Interfaces:** `public static class FishPondAvailability { ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf); int PopulationSteps(int requiredPopulation) }`; constants `PondCost = 2`, `PopulationStepSize = 3`, `FishType = "Fish"`.

- [ ] **Step 1: Failing tests**

```csharp
public class FishPondAvailabilityTests
{
    private static RawObjectEntry Fish(string name) => new("Fish", -4, 10, false, new string[0], name);

    private static EffortData Data() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry> { ["145"] = Fish("Sunfish"), ["698"] = Fish("Sturgeon") },
        FishPonds = new List<RawFishPondRule>
        {
            new(new[] { "item_sturgeon" }, new[] { new RawFishPondProduct("(O)812", 1), new RawFishPondProduct("(O)814", 7) }),
            new(new[] { "category_fish" }, new[] { new RawFishPondProduct("(O)812", 1) }),
        },
    };

    private static int? Effort(string id) => id switch { "(O)145" => 2, "(O)698" => 9, _ => null };

    [Fact]
    public void Roe_takes_the_cheapest_fish_any_pond_accepts() => Assert.Equal(2 + 2, FishPondAvailability.Derive("(O)812", Data(), Effort)!.Effort);

    [Fact]
    public void A_population_gate_adds_a_step_per_three_fish() => Assert.Equal(9 + 2 + 2, FishPondAvailability.Derive("(O)814", Data(), Effort)!.Effort);

    [Theory] [InlineData(1, 0)] [InlineData(2, 1)] [InlineData(4, 1)] [InlineData(5, 2)] [InlineData(7, 2)] [InlineData(10, 3)]
    public void Population_steps(int population, int steps) => Assert.Equal(steps, FishPondAvailability.PopulationSteps(population));

    [Fact]
    public void Unclaimed_is_null() => Assert.Null(FishPondAvailability.Derive("(O)24", Data(), Effort));
}
```

- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement.** For each rule with a product matching the id: fish ids = `ContextTagMatcher.IdsMatchingAll(data.Objects, rule.RequiredTags)` filtered to `Type == FishType`; cheapest `effortOf` (skip rule when none resolves); `effort = fish + PondCost + PopulationSteps(product.RequiredPopulation)`; `PopulationSteps(p) = p <= 1 ? 0 : (p - 2) / PopulationStepSize + 1`; minimum; basis `$"fish pond, {fishId} ({fishEffort}) + pond {PondCost} + population {p} (+{steps}), effort {e}"`.

- [ ] **Step 4: Run tests.** **Step 5: Commit** (0.16.50): "v0.16.50: effort rule for fish pond outputs (Data/FishPondData)"

---

### Task 10: `CookedDishAvailability`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/CookedDishAvailability.cs`
- Test: `tests/TheLongestYear.Tests/CookedDishAvailabilityTests.cs`

**Interfaces:** `public static class CookedDishAvailability { ItemEffort? Derive(string qualifiedId, EffortData data, Func<string, int?> effortOf, bool hasKitchen); int UnlockEffort(string? unlock); const int ExtremeEffort = 12; const int KitchenCost = 1 }`

- [ ] **Step 1: Failing tests**

```csharp
public class CookedDishAvailabilityTests
{
    private static RawObjectEntry Obj(int category, string name) => new("Basic", category, 10, false, new string[0], name);

    private static EffortData Data() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry> { ["184"] = Obj(-6, "Milk"), ["186"] = Obj(-6, "Large Milk"), ["24"] = Obj(-75, "Parsnip") },
        CookingRecipes = new List<RawCookingRecipe>
        {
            new("Fried Egg", new[] { "(O)176" }, "(O)194", "default"),
            new("Omelet", new[] { "(O)176", "-6" }, "(O)195", "null"),
            new("Parsnip Soup", new[] { "(O)24", "-6" }, "(O)199", "f Caroline 3"),
            new("Mystery", new[] { "(O)9999" }, "(O)200", "default"),
        },
    };

    private static int? Effort(string id) => id switch { "(O)176" => 1, "(O)184" => 1, "(O)186" => 2, "(O)24" => 2, _ => null };

    [Fact]
    public void Default_recipe_is_its_hardest_ingredient_plus_the_kitchen()
        => Assert.Equal(1 + 0 + 1, CookedDishAvailability.Derive("(O)194", Data(), Effort, hasKitchen: false)!.Effort);

    [Fact]
    public void A_kept_kitchen_drops_the_kitchen_cost()
        => Assert.Equal(1, CookedDishAvailability.Derive("(O)194", Data(), Effort, hasKitchen: true)!.Effort);

    [Fact]
    public void Category_ingredients_use_the_cheapest_member_and_tv_recipes_add_one()
        => Assert.Equal(1 + 1 + 1, CookedDishAvailability.Derive("(O)195", Data(), Effort, false)!.Effort);

    [Fact]
    public void Friendship_recipes_add_two()
        => Assert.Equal(2 + 2 + 1, CookedDishAvailability.Derive("(O)199", Data(), Effort, false)!.Effort);

    [Fact]
    public void An_unrecognised_ingredient_makes_the_dish_extreme()
    {
        ItemEffort? r = CookedDishAvailability.Derive("(O)200", Data(), Effort, false);
        Assert.Equal(CookedDishAvailability.ExtremeEffort, r!.Effort);
        Assert.Contains("(O)9999", r.Basis);
    }

    [Theory] [InlineData("default", 0)] [InlineData("null", 1)] [InlineData("s Cooking 3", 1)] [InlineData("s Farming 6", 2)] [InlineData("f Gus 7", 2)] [InlineData("e 1", 3)]
    public void Unlock_effort(string unlock, int effort) => Assert.Equal(effort, CookedDishAvailability.UnlockEffort(unlock));
}
```

- [ ] **Step 2: Run, expect failure.**
- [ ] **Step 3: Implement.** Constants `TvUnlockEffort = 1`, `LowSkillMax = 5`, `SkillUnlockLow = 1`, `SkillUnlockHigh = 2`, `FriendshipUnlockEffort = 2`, `SpecialUnlockEffort = 3`. Ingredient effort: an id that parses as a negative int is a category ref: cheapest `effortOf` over `data.Objects` entries with that Category (null if none); otherwise `effortOf(NormalizeItemId(id))`. Any null -> return `new ItemEffort(ExtremeEffort, $"dish {name}: ingredient {id} unrecognised, extreme")`. Else `max(ingredients) + UnlockEffort(unlock) + (hasKitchen ? 0 : KitchenCost)`; basis names the hardest ingredient and the unlock. Minimum over recipes producing the id.

- [ ] **Step 4: Run tests.** **Step 5: Commit** (0.16.51): "v0.16.51: effort rule for cooked dishes (Data/CookingRecipes, unlock and kitchen)"

---

### Task 11: `CropForageAvailability`

**Files:**
- Create: `src/TheLongestYear.Core/Availability/CropForageAvailability.cs`
- Test: `tests/TheLongestYear.Tests/CropForageAvailabilityTests.cs`

**Interfaces:** `public static class CropForageAvailability { ItemEffort? DeriveCrop(string qualifiedId, IReadOnlyList<RawCropGrowth> crops); ItemEffort? DeriveForage(string qualifiedId, IReadOnlyList<RawSpawnEntry> spawns) }`; constants `BaseEffort = 1`, `QuickGrowthDays = 6`, `MediumGrowthDays = 12`, `RegrowStep = 1`, `SingleLocationStep = 1`, `RemoteLocationStep = 1`, remote markers `Woods`, `Desert`, `Island`.

- [ ] **Step 1: Failing tests**

```csharp
public class CropForageAvailabilityTests
{
    [Theory]
    [InlineData("(O)24", 4, false, false, 1)]    // Parsnip
    [InlineData("(O)400", 8, true, false, 3)]    // Strawberry regrows
    [InlineData("(O)304", 11, true, true, 3)]    // Hops, trellis
    [InlineData("(O)254", 13, false, false, 3)]  // Melon
    public void Crops_score_growth_and_regrowth(string id, int days, bool regrows, bool trellis, int effort)
    {
        var crops = new List<RawCropGrowth> { new(id, days, regrows, trellis) };
        Assert.Equal(effort, CropForageAvailability.DeriveCrop(id, crops)!.Effort);
    }

    [Fact]
    public void Forage_in_many_places_is_one_and_secret_woods_only_is_three()
    {
        var spawns = new List<RawSpawnEntry>
        {
            new("(O)16", Season.Spring, null, "Forest"), new("(O)16", Season.Spring, null, "Mountain"),
            new("(O)257", Season.Spring, null, "Woods"),
        };
        Assert.Equal(1, CropForageAvailability.DeriveForage("(O)16", spawns)!.Effort);
        Assert.Equal(3, CropForageAvailability.DeriveForage("(O)257", spawns)!.Effort);
        Assert.Null(CropForageAvailability.DeriveForage("(O)24", spawns));
    }
}
```

- [ ] **Step 2: Run, expect failure.** **Step 3: Implement** per the constants (forage: `+RemoteLocationStep` when every location key contains a remote marker). **Step 4: Run.** **Step 5: Commit** (0.16.52): "v0.16.52: effort rule for crops and forage (growth days, regrowth, spawn spread)"

---

### Task 12: `EffortTiers` and `EffortWeights` (rule E tables)

**Files:**
- Create: `src/TheLongestYear.Core/EffortTiers.cs`
- Create: `src/TheLongestYear.Core/EffortWeights.cs`
- Test: `tests/TheLongestYear.Tests/EffortTiersTests.cs`

**Interfaces:**
- `public enum EffortTier { Easy, Medium, Hard, Extreme }`
- `public sealed record TierCutoffs(int Easy, int Medium, int Hard)` (inclusive upper bounds)
- `public static class EffortTiers { TierCutoffs Cutoffs(IReadOnlyCollection<int> efforts); EffortTier Tier(int effort, TierCutoffs cutoffs); EffortTier FromRarity(Rarity rarity) }`
- `public static class EffortWeights { int For(Season season, EffortTier tier) }` with the spec table `{8,3,1,0},{6,4,2,1},{3,4,4,2},{1,2,4,8}`.

- [ ] **Step 1: Failing tests**

```csharp
public class EffortTiersTests
{
    [Fact]
    public void A_pool_of_eight_tiers_two_per_quartile()
    {
        int[] efforts = { 1, 1, 2, 2, 3, 3, 5, 7 };
        TierCutoffs c = EffortTiers.Cutoffs(efforts);
        var tiers = efforts.Select(e => EffortTiers.Tier(e, c)).ToArray();
        Assert.Equal(new[] { EffortTier.Easy, EffortTier.Easy, EffortTier.Medium, EffortTier.Medium,
            EffortTier.Hard, EffortTier.Hard, EffortTier.Extreme, EffortTier.Extreme }, tiers);
    }

    [Fact]
    public void A_pool_of_one_is_easy()
        => Assert.Equal(EffortTier.Easy, EffortTiers.Tier(9, EffortTiers.Cutoffs(new[] { 9 })));

    [Fact]
    public void An_empty_pool_has_no_extreme_ids()
        => Assert.Equal(EffortTier.Easy, EffortTiers.Tier(3, EffortTiers.Cutoffs(Array.Empty<int>())));

    [Theory] [InlineData(Rarity.Common, EffortTier.Easy)] [InlineData(Rarity.Uncommon, EffortTier.Medium)] [InlineData(Rarity.Rare, EffortTier.Hard)] [InlineData(Rarity.VeryRare, EffortTier.Extreme)]
    public void Price_buckets_map_to_tiers(Rarity r, EffortTier t) => Assert.Equal(t, EffortTiers.FromRarity(r));

    [Theory]
    [InlineData(Season.Spring, EffortTier.Easy, 8)] [InlineData(Season.Spring, EffortTier.Extreme, 0)]
    [InlineData(Season.Summer, EffortTier.Hard, 2)] [InlineData(Season.Fall, EffortTier.Medium, 4)]
    [InlineData(Season.Winter, EffortTier.Extreme, 8)] [InlineData(Season.Winter, EffortTier.Easy, 1)]
    public void Weights_follow_the_spec_table(Season s, EffortTier t, int w) => Assert.Equal(w, EffortWeights.For(s, t));
}
```

- [ ] **Step 2: Run, expect failure.** **Step 3: Implement.** `Cutoffs`: sort ascending; `n == 0` -> `(int.MaxValue, int.MaxValue, int.MaxValue)`; `Quartile(k) = sorted[Math.Max(0, k * n / QuartileCount - 1)]` with `QuartileCount = 4` for k = 1..3. `Tier`: `<= Easy` Easy, `<= Medium` Medium, `<= Hard` Hard, else Extreme. **Step 4: Run.** **Step 5: Commit** (0.16.53): "v0.16.53: effort quartile tiers and the season-by-tier goal weights (rule E tables)"

---

### Task 13: `EffortComposer` and the builder composition

**Files:**
- Create: `src/TheLongestYear.Core/Availability/EffortComposer.cs`
- Modify: `src/TheLongestYear.Core/Availability/ItemAvailabilityBuilder.cs`
- Test: `tests/TheLongestYear.Tests/EffortComposerTests.cs`

**Interfaces:**
- `public sealed class EffortComposer(EffortData data, IReadOnlyDictionary<string, ItemAvailability> seasonDerived, bool hasKitchen)`; `ItemEffort? Derive(string qualifiedId)`; `int? EffortOf(string qualifiedId)` (season-derived effort, else memoised `Derive`, null while the id is being resolved to break recipe cycles); `IReadOnlyDictionary<string, ItemEffort> DeriveAll()` (every `data.Objects` id not season-derived, ordinal order).
- Rule order in `Derive`: MineralNode, Geode, MonsterDrop, Artifact, AnimalProduct, Artisan, FishPond, CookedDish, Crop, Forage; first non-null wins.
- `ItemAvailabilityBuilder.Build(ItemPools pools, IReadOnlyDictionary<string, Season>? seasonOverrides = null, IReadOnlyDictionary<string, int>? effortOverrides = null, EffortData? effortData = null, bool hasKitchen = false)`.

- [ ] **Step 1: Failing tests**

```csharp
public class EffortComposerTests
{
    private static RawObjectEntry Obj(int category, string name, string type = "Basic") => new(type, category, 10, false, new string[0], name);

    [Fact]
    public void Composer_tries_domains_in_order_and_recurses_through_inputs()
    {
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["398"] = Obj(-79, "Grape"), ["348"] = Obj(-26, "Wine") },
            Crops = new List<RawCropGrowth> { new("(O)398", 10, true, true) },                       // 1 + 1 + 1 = 3
            MachineRules = new List<RawMachineRule> { new("(BC)12", null, new[] { "category_fruit" }, new[] { "(O)348" }, 10000, -1) },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "Farming 8" },
        };
        var composer = new EffortComposer(data, new Dictionary<string, ItemAvailability>(), hasKitchen: false);
        Assert.Equal(3, composer.Derive("(O)398")!.Effort);
        Assert.Equal(3 + 3 + 2, composer.Derive("(O)348")!.Effort);
    }

    [Fact]
    public void Season_derived_effort_wins_and_unclaimed_ids_are_null()
    {
        var seasonDerived = new Dictionary<string, ItemAvailability> { ["(O)128"] = new(Season.Summer, 4, "fish") };
        var composer = new EffortComposer(new EffortData(), seasonDerived, false);
        Assert.Equal(4, composer.EffortOf("(O)128"));
        Assert.Null(composer.Derive("(O)999"));
    }

    [Fact]
    public void Builder_reports_effort_only_ids_and_keeps_the_price_bucket_for_unclaimed_ones()
    {
        var pools = new ItemPools();
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["767"] = Obj(-28, "Bat Wing"), ["999"] = Obj(0, "Modded Thing") },
            MonsterDrops = new List<RawMonsterDrop> { new("Bat", "(O)767", 0.9) },
        };
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(pools, effortData: data);
        Assert.Equal(1, model.DerivedEffortCount);
        Assert.Equal(EffortSource.Derived, model.For("(O)767").Source);
        Assert.Equal(EffortSource.Price, model.For("(O)999").Source);
        Assert.Contains("(O)999", model.UnrecognisedIds);
    }
}
```

- [ ] **Step 2: Run, expect failure.** **Step 3: Implement.** `EffortOf`: `seasonDerived[id].Effort` if present; memo hit; if `_visiting.Contains(id)` return null; add, `Derive`, remove, memoise (including null). `Derive` calls the rules in order with `EffortOf` as the resolver. `DeriveAll` iterates `data.Objects.Keys` ordinal, normalises, skips season-derived, stores non-null results. Builder: after the Phase 1 loops, `if (effortData != null) effortDerived = new EffortComposer(effortData, derived, hasKitchen).DeriveAll();` and pass it to the model. **Step 4: Run whole suite.** **Step 5: Commit** (0.16.54): "v0.16.54: EffortComposer composes the Phase 2 rules into the availability model"

---

### Task 14: Mod glue: `GameEffortData`, `tly_itemmodel` source/tier, `tly_dumpeffort`

**Files:**
- Create: `src/TheLongestYear/Loop/GameEffortData.cs`
- Create: `src/TheLongestYear/Debug/EffortDocWriter.cs`
- Create: `src/TheLongestYear.Core/ThemeEffortPools.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (SaveLoaded build at ~line 472; `CmdItemModel`; command registration next to `tly_itemmodel`; a `case "tly_dumpeffort"` in the bridge switch near line 1717)
- Modify: `.gitignore` (add `item-effort-model.md`)
- Modify: `CHANGELOG.md`

**Interfaces:**
- `internal sealed class GameEffortData(IMonitor monitor) { EffortData Build() }` reads: `Data/Objects` (Name, GeodeDrops with Chance and the default table via `GeodeAvailability.DefaultTableDrops` when `GeodeDropsDefaultItems`), `Data/Monsters` (name, id, chance pairs from field 6), `Data/Locations` (`ArtifactSpots` per location incl. `Default`, ids via `ItemQueryIds.Expand(ItemId)` plus `RandomItemId`, `Chance`), `Data/Machines` (`OutputRules[].Triggers[]` -> one `RawMachineRule` per trigger with `OutputItem[].ItemId` expanded via `ItemQueryIds.Expand`, `MinutesUntilReady`, `DaysUntilReady`), `Data/CraftingRecipes` (field 2 output id -> machine `(BC)id` when field 3 is `true`; unlock = field 4), `Data/FarmAnimals` (`RequiredBuilding ?? House`, `PurchasePrice`, `DaysToProduce`, `ProduceItemIds[].ItemId`, `DeluxeProduceItemIds[].ItemId`), `Data/Buildings` (`BuildingToUpgrade`), `Data/CookingRecipes` (field 0 pairs -> ids, field 2 first token -> output, field 3 unlock), `Data/FishPondData` (List), `Data/Crops` (sum of `DaysInPhase`, `RegrowDays > 0`, `IsRaised`), forage spawns as `GameDataPools` reads them (same exclusions). Every read in one try/catch that logs Warn and returns the partial snapshot.
- `public static class ThemeEffortPools { IReadOnlyList<string> IdsFor(Theme theme, ItemPools pools, IReadOnlyDictionary<string, RawObjectEntry> objects) }`: Foraging = `pools.Forage`; Farming = `pools.Crops`; Fishing = `Fish` + `CrabPot`; Mining = `Metals` + `GeodeMinerals`; Spelunking = `GeodeMinerals` + `MonsterDrops` + `Artifacts` + objects with Category -2 or -12; Artisan = `ArtisanGoods`; Kitchen = `Cooking` + objects with Category -5, -6, -18; Mixed = union of all. (Spelunking/Artisan/Kitchen enum members land in Task 15; until then the switch default returns the Mixed union. Re-touch it in Task 15.)
- `tly_dumpeffort [fileName]`: writes `item-effort-model.md` in the mod folder (same as `tly_dumpbundles`): header explaining the scale, then one section per theme listing every pool item as `| id | name | effort | tier | source | basis |` sorted by effort then id, tier from `EffortTiers.Cutoffs` over that theme's pool. Copy to `docs/item-effort-model.md` for Jeff's review (gitignored, like the engine catalogue).
- `tly_itemmodel` line becomes `"{id}: earliest {season}, effort {effort} ({source}), tier {tier in its first matching theme pool or n/a} [{basis}]"`.

- [ ] **Step 1: Write `GameEffortData.Build`** following `GameDataPools` (one `Game1.content.Load<...>` per table; `MapSeason` helpers copied; `Data/Monsters` chance parse `double.TryParse(pairs[i + 1], NumberStyles.Float, CultureInfo.InvariantCulture, ...)`).
- [ ] **Step 2: Wire in `ModEntry`** right after `enginePools`:

```csharp
TheLongestYear.Core.Availability.EffortData effortData = new TheLongestYear.Loop.GameEffortData(this.Monitor).Build();
_effortData = effortData;
_enginePools = enginePools;
_availability = TheLongestYear.Core.Availability.ItemAvailabilityBuilder.Build(
    enginePools, seasonOverrides: itemSeasonPins, effortData: effortData,
    hasKitchen: _meta.State.HasUpgrade("keep_kitchen"));
```
and add `{_availability.DerivedEffortCount} effort-only id(s)` to the Trace line.
- [ ] **Step 3: `EffortDocWriter.Write(path, pools, objects, availability)`** and the two command changes.
- [ ] **Step 4: Build** `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` (close the game first: `pwsh -NoProfile -File tools/deploy.ps1 -NoLaunch`); run the suite.
- [ ] **Step 5: CHANGELOG** under `### Added`: "**`tly_dumpeffort` console command** writes `item-effort-model.md`: every pool item with its derived effort, tier and the game-data basis (gems and minerals, geodes, monster drops, artifacts, artisan goods, fish ponds, animal products, cooked dishes, crops, forage). `tly_itemmodel` now prints the effort source and tier."
- [ ] **Step 6: Commit** (0.16.55): "v0.16.55: GameEffortData reads the effort tables at SaveLoaded; tly_dumpeffort writes item-effort-model.md; tly_itemmodel prints source and tier"

Phase 1 review point: after Task 28's deploy, run `tly_dumpeffort` and copy the document to `docs/item-effort-model.md` for Jeff. Phases 2 and 3 continue in this session by Jeff's instruction (the plan was asked for end to end); a tier that looks wrong is fixed by fixing its rule.

---

## Phase 2: Theme economy (rules A to E) on the board, with the classifier and per-goal bonus

### Task 15: Theme enum members, `ThemeModifiers`, i18n, `ThemeDomains`, `ItemKind` classifier

**Files:**
- Modify: `src/TheLongestYear.Core/Theme.cs`, `ThemeModifiers.cs`, `RunState.cs` (comment only), `ThemeEffortPools.cs`
- Create: `src/TheLongestYear.Core/ItemKind.cs` (enum + `ItemKindClassifier`), `src/TheLongestYear.Core/ThemeDomains.cs`
- Modify: `src/TheLongestYear/i18n/default.json`
- Test: `tests/TheLongestYear.Tests/ThemeModifiersTests.cs`, `ThemeDisplayTests.cs`, new `ThemeDomainsTests.cs`; update `SelectionServiceTests.Over_a_month_exactly_four_distinct_themes_can_be_selected` (the final `Assert.Single` becomes `Assert.Equal(4, ThemeDomains.RoomThemes.Except(selected).Count() + 0)` replaced by: assert every selected theme is distinct and none repeats; the "5 themes" sanity line is deleted).

**Interfaces:**
- `enum Theme { Foraging, Farming, Fishing, Mining, Mixed, Spelunking, Artisan, Kitchen }`
- `ThemeModifiers.For`: Spelunking `("monster_drops_double", "machines_slow")`, Artisan `("machines_fast", "cooked_food_weak")`, Kitchen `("animal_double_product", "monster_damage_up")`.
- `public enum ItemKind { Other, Gem, Mineral, MonsterLoot, Artifact, ArtisanGood, Cooking, Egg, Milk, AnimalProduct }`
- `public static class ItemKindClassifier { ItemKind From(int category, string? type) }` (constants `GemCategory = -2`, `MineralCategory = -12`, `MonsterLootCategory = -28`, `ArtisanCategory = -26`, `CookingCategory = -7`, `EggCategory = -5`, `MilkCategory = -6`, `AnimalProductCategory = -18`, `ArchType = "Arch"`).
- `public static class ThemeDomains { IReadOnlyList<Theme> RoomThemes; IReadOnlyList<Theme> ActivityThemes; bool MatchesPerLine(Theme t); bool Matches(Theme t, ItemKind k) }`.
- i18n keys: `theme.spelunking` "Spelunking", `theme.artisan` "Artisan", `theme.kitchen` "Kitchen", and the six `modifier.*` strings from the spec (plain ASCII).

- [ ] **Step 1: Failing tests.** Extend `ThemeModifiersTests` theories with the three themes and six display strings, plus:

```csharp
    [Fact]
    public void Every_liability_lands_on_a_different_activity_than_its_bonus_and_each_new_activity_is_bitten_once()
    {
        // bonus id prefix names the activity it helps; liability id names the activity it bites
        var pairs = new[] { Theme.Spelunking, Theme.Artisan, Theme.Kitchen }.Select(ThemeModifiers.For).ToList();
        Assert.Equal(new[] { "machines_slow", "cooked_food_weak", "monster_damage_up" }, pairs.Select(p => p.LiabilityId));
        Assert.Equal(3, pairs.Select(p => p.LiabilityId).Distinct().Count());
        Assert.Equal(3, pairs.Select(p => p.BonusId).Distinct().Count());
    }
```

`ThemeDomainsTests`:

```csharp
public class ThemeDomainsTests
{
    [Theory]
    [InlineData(-2, "Minerals", ItemKind.Gem)] [InlineData(-12, "Minerals", ItemKind.Mineral)] [InlineData(-28, "Basic", ItemKind.MonsterLoot)]
    [InlineData(0, "Arch", ItemKind.Artifact)] [InlineData(-26, "Basic", ItemKind.ArtisanGood)] [InlineData(-7, "Cooking", ItemKind.Cooking)]
    [InlineData(-5, "Basic", ItemKind.Egg)] [InlineData(-6, "Basic", ItemKind.Milk)] [InlineData(-18, "Basic", ItemKind.AnimalProduct)] [InlineData(-75, "Basic", ItemKind.Other)]
    public void Classifier_reads_the_games_category_and_type(int category, string type, ItemKind kind)
        => Assert.Equal(kind, ItemKindClassifier.From(category, type));

    [Theory]
    [InlineData(Theme.Spelunking, ItemKind.Gem, true)] [InlineData(Theme.Spelunking, ItemKind.Mineral, true)]     // Quartz, Ruby
    [InlineData(Theme.Spelunking, ItemKind.MonsterLoot, true)] [InlineData(Theme.Spelunking, ItemKind.Artifact, true)]  // Bat Wing, Ancient Doll
    [InlineData(Theme.Artisan, ItemKind.ArtisanGood, true)]                                                        // Wine
    [InlineData(Theme.Kitchen, ItemKind.Cooking, true)] [InlineData(Theme.Kitchen, ItemKind.Egg, true)]           // Pizza, Egg
    [InlineData(Theme.Kitchen, ItemKind.Milk, true)] [InlineData(Theme.Kitchen, ItemKind.AnimalProduct, true)]    // Milk, Wool
    [InlineData(Theme.Spelunking, ItemKind.Other, false)] [InlineData(Theme.Artisan, ItemKind.Other, false)] [InlineData(Theme.Kitchen, ItemKind.Other, false)]  // Parsnip
    [InlineData(Theme.Mixed, ItemKind.Other, true)] [InlineData(Theme.Mixed, ItemKind.Gem, true)]
    [InlineData(Theme.Farming, ItemKind.Other, false)]
    public void Themes_match_kinds(Theme theme, ItemKind kind, bool expected)
        => Assert.Equal(expected, ThemeDomains.Matches(theme, kind));

    [Fact]
    public void Room_and_activity_theme_lists()
    {
        Assert.Equal(new[] { Theme.Foraging, Theme.Farming, Theme.Fishing, Theme.Mining, Theme.Mixed }, ThemeDomains.RoomThemes);
        Assert.Equal(new[] { Theme.Spelunking, Theme.Artisan, Theme.Kitchen }, ThemeDomains.ActivityThemes);
        Assert.True(ThemeDomains.MatchesPerLine(Theme.Mixed));
        Assert.False(ThemeDomains.MatchesPerLine(Theme.Fishing));
    }
}
```

- [ ] **Step 2: Run, expect failures.** **Step 3: Implement** the enum, the switch arms, the two new classes, the i18n keys (next to the existing `theme.*` and `modifier.*` lines), and update `ThemeEffortPools` for the three themes. Update the `SelectionServiceTests` month test as described. **Step 4: Run the whole suite** (I18nGuardTests must pass: the new keys are reached through `ThemeDisplay.Name` / `DisplayNameFor`). **Step 5: Commit** (0.16.56): "v0.16.56: Theme gains Spelunking, Artisan, Kitchen (ids, names, modifier text); ItemKind classifier and ThemeDomains"

---

### Task 16: Rule A groundwork: `BundleRequirement.DueItemsFor` and folding the 0.16.41 stopgap

**Files:**
- Modify: `src/TheLongestYear.Core/BundleRequirement.cs`
- Test: `tests/TheLongestYear.Tests/BundleRequirementTests.cs`

**Interfaces:** `public IEnumerable<string> DueItemsFor(Season season, Func<string, bool> obtainablePredicate)`: Seasonal -> all ingredients when `SeasonalSeason == season`; PerItem -> pins whose value is `season` (obtainable); Percentage -> every obtainable ingredient when the cumulative quota RISES this season (`Cum[s] > (s == 0 ? 0 : Cum[s - 1])`), else empty. `InPlayItemsFor` is unchanged in behaviour; its PerItem comment is rewritten as the spec rule: in-play = every obtainable undonated ingredient (rule A's filler tier), due = `DueItemsFor` (rule A's first tier). One rule, two tiers.

- [ ] **Step 1: Failing tests**

```csharp
    [Fact]
    public void Due_items_follow_the_gate_per_kind()
    {
        var perItem = BundleRequirement.CreatePerItem("Blacksmiths", Theme.Mining,
            new Dictionary<string, Season> { ["Copper"] = Season.Spring, ["Iron"] = Season.Summer });
        Assert.Equal(new[] { "Copper" }, perItem.DueItemsFor(Season.Spring, _ => true).ToArray());
        Assert.Equal(new[] { "Iron" }, perItem.DueItemsFor(Season.Summer, _ => true).ToArray());
        Assert.Empty(perItem.DueItemsFor(Season.Fall, _ => true));

        var seasonal = BundleRequirement.CreateSeasonal("Spring Crops", Theme.Farming, new[] { "A", "B" }, Season.Spring);
        Assert.Equal(new[] { "A", "B" }, seasonal.DueItemsFor(Season.Spring, _ => true).ToArray());
        Assert.Empty(seasonal.DueItemsFor(Season.Summer, _ => true));

        var pct = BundleRequirement.CreatePercentage("Crab Pot", Theme.Fishing, new[] { "X", "Y", "Z" }, 3, new[] { 1, 1, 2, 3 });
        Assert.Equal(new[] { "X", "Y", "Z" }, pct.DueItemsFor(Season.Spring, _ => true).ToArray());
        Assert.Empty(pct.DueItemsFor(Season.Summer, _ => true));   // quota did not rise
        Assert.Equal(new[] { "Y" }, pct.DueItemsFor(Season.Fall, id => id == "Y").ToArray());
    }
```

- [ ] **Step 2: Run, expect compile failure.** **Step 3: Implement** with a private `QuotaRisesIn(Season)` helper. Rewrite the PerItem comment in `InPlayItemsFor`:

```csharp
            case BundleKind.PerItem:
                // Rule A (activity-themes spec, 2026-08-28): in-play is every obtainable undonated
                // ingredient, whatever season it is DUE; DueItemsFor picks out the ones the day-28
                // gate demands this season and the sampler draws those first. The earlier
                // due-only rule left the Mixed theme with one goal all Spring (0.16.41 stopgap,
                // now folded into this two-tier rule). Obtainability stays (Sturgeon in Fall).
                return Ingredients.Where(obtainablePredicate);
```
**Step 4: Run.** **Step 5: Commit** (0.16.57): "v0.16.57: BundleRequirement.DueItemsFor (rule A tier 1); the 0.16.41 stopgap becomes rule A's filler tier"

---

### Task 17: `BonusSlot.Due` / `Paid`, per-line theme matching in `SlotPoolBuilder`

**Files:**
- Modify: `src/TheLongestYear.Core/BonusSlot.cs`, `src/TheLongestYear.Core/SlotPoolBuilder.cs`
- Test: `tests/TheLongestYear.Tests/SlotPoolBuilderTests.cs`

**Interfaces:**
- `BonusSlot` gains `public bool Due { get; set; }` (the day-28 gate wants this line this season) and `public bool Paid { get; set; }` (its share of the weekly bonus has been paid; rule D).
- `SlotPoolBuilder.OpenSlotsForTheme(bundleData, slotStateForBundle, requirements, theme, season, isObtainableInSeason, Func<string, ItemKind>? kindOf = null)`: when `kindOf != null && ThemeDomains.MatchesPerLine(theme)` a bundle of ANY theme contributes the lines whose kind matches; otherwise the bundle-level `req.Theme == theme` check as today. Every emitted slot has `Due` set from `req.DueItemsFor`.

- [ ] **Step 1: Failing tests** (use the existing test helpers in `SlotPoolBuilderTests` for bundle data strings; look at how the file builds `bundleData` and copy that shape):

```csharp
    [Fact]
    public void Activity_themes_match_lines_by_kind_across_every_room()
    {
        // Two bundles: a Boiler Room (Mining) bundle with Quartz + Copper Bar, a Pantry (Farming) bundle with Egg + Parsnip.
        var data = new Dictionary<string, string>
        {
            ["Boiler Room/20"] = "Blacksmith's/O 334 1/80 1 0 334 1 0/3/2/20",
            ["Pantry/0"] = "Animal/O 176 1/176 1 0 24 1 0/2/2/0",
        };
        var reqs = new List<BundleRequirement>
        {
            BundleRequirement.CreatePerItem("Blacksmith's", Theme.Mining, new Dictionary<string, Season> { ["(O)80"] = Season.Spring, ["(O)334"] = Season.Summer }),
            BundleRequirement.CreatePerItem("Animal", Theme.Farming, new Dictionary<string, Season> { ["(O)176"] = Season.Spring, ["(O)24"] = Season.Spring }),
        };
        ItemKind Kind(string id) => id switch { "(O)80" => ItemKind.Gem, "(O)176" => ItemKind.Egg, _ => ItemKind.Other };

        var spelunking = SlotPoolBuilder.OpenSlotsForTheme(data, _ => null, reqs, Theme.Spelunking, Season.Spring, _ => true, Kind);
        Assert.Equal(new[] { "(O)80" }, spelunking.Select(s => s.ItemId));
        Assert.True(spelunking[0].Due);

        var kitchen = SlotPoolBuilder.OpenSlotsForTheme(data, _ => null, reqs, Theme.Kitchen, Season.Summer, _ => true, Kind);
        Assert.Equal(new[] { "(O)176" }, kitchen.Select(s => s.ItemId));
        Assert.False(kitchen[0].Due);    // pinned Spring, now Summer: filler

        var mixed = SlotPoolBuilder.OpenSlotsForTheme(data, _ => null, reqs, Theme.Mixed, Season.Spring, _ => true, Kind);
        Assert.Equal(4, mixed.Count);    // Mixed means anything on the board

        var mining = SlotPoolBuilder.OpenSlotsForTheme(data, _ => null, reqs, Theme.Mining, Season.Spring, _ => true, Kind);
        Assert.Equal(2, mining.Count);   // room themes stay bundle-level
        Assert.Equal(new[] { true, false }, mining.Select(s => s.Due));
    }

    [Fact]
    public void Without_a_classifier_mixed_stays_the_bulletin_board_room()
    {
        // same data as above, kindOf null: Mixed matches no bundle (neither is Theme.Mixed)
        ...
        Assert.Empty(SlotPoolBuilder.OpenSlotsForTheme(data, _ => null, reqs, Theme.Mixed, Season.Spring, _ => true));
    }
```
(Check the bundle-data string format against `BundleParsing.Parse` before finalising the literals.)

- [ ] **Step 2: Run, expect failure.** **Step 3: Implement**: compute `bool perLine = kindOf != null && ThemeDomains.MatchesPerLine(theme);` then `if (!perLine && req.Theme != theme) continue;`, `var due = new HashSet<string>(req.DueItemsFor(season, isObtainableInSeason), StringComparer.Ordinal);`, per line `if (perLine && !ThemeDomains.Matches(theme, kindOf!(id))) continue;` and `Due = due.Contains(id)`. Update the class doc comment. **Step 4: Run.** **Step 5: Commit** (0.16.58): "v0.16.58: goal pool matches activity themes and Mixed per line by item kind; every slot knows whether the gate is due"

---

### Task 18: Rules A, B and E in `BonusSlotSampler`

**Files:**
- Create: `src/TheLongestYear.Core/GoalSamplingRules.cs` (record + `GoalWeighting`)
- Modify: `src/TheLongestYear.Core/BonusSlotSampler.cs`
- Test: `tests/TheLongestYear.Tests/BonusSlotSamplerRulesTests.cs`

**Interfaces:**
- `public sealed record GoalSamplingRules(Season Season, int FillerAllowance, Func<string, int?> EffortOf)`; `public const int UnlimitedFiller = 99` on `GoalSamplingRules`.
- `public sealed record GoalWeight(string ItemId, int? Effort, EffortTier Tier, int Weight)`
- `public static class GoalWeighting { IReadOnlyList<GoalWeight> For(IEnumerable<string> ids, GoalSamplingRules rules, Func<string, Rarity> rarityOf) }`: cutoffs from `EffortTiers.Cutoffs` over the recognised efforts of the given ids; tier = `EffortTiers.Tier(effort, cutoffs)` when `EffortOf(id)` is non-null, else `EffortTiers.FromRarity(rarityOf(id))`; weight = `EffortWeights.For(rules.Season, tier)`. Ordinal id order.
- `BonusSlotSampler.SampleSlots(runSeed, weekOfYear, theme, openSlots, rarityOf, maxCount, remainingNeedForBundle = null, caps = null, GoalSamplingRules? rules = null)`. With `rules == null` the draw is byte-for-byte today's (one pass, rarity weights, every line eligible). With rules: pass 1 draws ids that have a `Due` slot (weights from `GoalWeighting`, zero-weight ids removed, each drawn id resolves to one of its Due slots); pass 2 draws the remaining ids up to `min(maxCount - taken, FillerAllowance)`, with an extra per-slot rule: a bundle may hold at most ONE filler goal per week. Both passes share the rng in sequence; the per-bundle remaining-need cap and the group caps apply in both.
- Constants: `MaxFillerPerBundle = 1`.

- [ ] **Step 1: Failing tests**

```csharp
public class BonusSlotSamplerRulesTests
{
    private static BonusSlot Slot(string id, int bundle, int line, bool due)
        => new() { ItemId = id, BundleIndex = bundle, IngredientIndex = line, BundleName = $"B{bundle}", Due = due };
    private static Rarity Common(string _) => Rarity.Common;
    private static GoalSamplingRules Rules(Season s, int filler, Func<string, int?>? effort = null) => new(s, filler, effort ?? (_ => null));

    private static List<BonusSlot> OneBundle() => new()
    {
        Slot("(O)1", 0, 0, true), Slot("(O)2", 0, 1, true),
        Slot("(O)3", 0, 2, false), Slot("(O)4", 0, 3, false), Slot("(O)5", 0, 4, false),
        Slot("(O)6", 0, 5, false), Slot("(O)7", 0, 6, false), Slot("(O)8", 0, 7, false),
    };

    [Fact]
    public void Spring_takes_the_due_lines_only()
    {
        var sample = BonusSlotSampler.SampleSlots(1, 1, Theme.Mining, OneBundle(), Common, 4, rules: Rules(Season.Spring, 0));
        Assert.Equal(2, sample.Count);
        Assert.All(sample, s => Assert.True(s.Due));
    }

    [Fact]
    public void Summer_adds_one_filler()
    {
        var sample = BonusSlotSampler.SampleSlots(1, 5, Theme.Mining, OneBundle(), Common, 5, rules: Rules(Season.Summer, 1));
        Assert.Equal(3, sample.Count);
        Assert.Equal(1, sample.Count(s => !s.Due));
    }

    [Fact]
    public void Winter_still_takes_one_filler_per_bundle()
    {
        var sample = BonusSlotSampler.SampleSlots(1, 13, Theme.Mining, OneBundle(), Common, 7, rules: Rules(Season.Winter, GoalSamplingRules.UnlimitedFiller));
        Assert.Equal(3, sample.Count);
    }

    [Fact]
    public void Fall_takes_two_fillers_when_they_are_spread_over_bundles()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)1", 0, 0, true), Slot("(O)2", 0, 1, true),
            Slot("(O)3", 0, 2, false), Slot("(O)4", 1, 0, false), Slot("(O)5", 2, 0, false), Slot("(O)6", 2, 1, false),
        };
        var sample = BonusSlotSampler.SampleSlots(1, 9, Theme.Mining, pool, Common, 6, rules: Rules(Season.Fall, 2));
        Assert.Equal(4, sample.Count);
        Assert.Equal(2, sample.Count(s => !s.Due));
        Assert.True(sample.Where(s => !s.Due).Select(s => s.BundleIndex).Distinct().Count() == 2);
    }

    [Fact]
    public void Spring_never_samples_an_extreme_id()
    {
        // efforts 1,1,2,2,3,3,5,8: (O)7 and (O)8 are Extreme
        int? Effort(string id) => id switch { "(O)1" => 1, "(O)2" => 1, "(O)3" => 2, "(O)4" => 2, "(O)5" => 3, "(O)6" => 3, "(O)7" => 5, "(O)8" => 8, _ => null };
        var pool = OneBundle(); pool.ForEach(s => s.Due = true);
        for (int seed = 1; seed <= 50; seed++)
        {
            var sample = BonusSlotSampler.SampleSlots(seed, 1, Theme.Mining, pool, Common, 4, rules: Rules(Season.Spring, 0, Effort));
            Assert.DoesNotContain(sample, s => s.ItemId == "(O)7" || s.ItemId == "(O)8");
        }
    }

    [Fact]
    public void Winter_prefers_the_extreme_id_eight_to_one()
    {
        int? Effort(string id) => id switch { "(O)1" => 1, "(O)2" => 1, "(O)3" => 2, "(O)4" => 2, "(O)5" => 3, "(O)6" => 3, "(O)7" => 5, "(O)8" => 8, _ => null };
        var pool = new List<BonusSlot> { Slot("(O)1", 0, 0, true), Slot("(O)8", 1, 0, true) };
        // Cutoffs come from the pool: with only {1, 8} the quartiles put 8 in Hard. Use the eight-item pool to fix the tiers, then sample the two.
        var eight = OneBundle(); eight.ForEach(s => s.Due = true);
        int extremeFirst = 0;
        for (int seed = 1; seed <= 100; seed++)
        {
            var sample = BonusSlotSampler.SampleSlots(seed, 13, Theme.Mining, eight, Common, 1, rules: Rules(Season.Winter, 0, Effort));
            if (sample[0].ItemId is "(O)7" or "(O)8") extremeFirst++;
        }
        // Weights: Easy 1 x2, Medium 2 x2, Hard 4 x2, Extreme 8 x2 = 30; Extreme share 16/30.
        Assert.InRange(extremeFirst, 40, 70);
    }

    [Fact]
    public void GoalWeighting_uses_the_price_bucket_when_no_effort_is_known()
    {
        var weights = GoalWeighting.For(new[] { "(O)1", "(O)2" }, Rules(Season.Spring, 0), id => id == "(O)2" ? Rarity.VeryRare : Rarity.Common);
        Assert.Equal(EffortTier.Easy, weights[0].Tier);
        Assert.Equal(EffortTier.Extreme, weights[1].Tier);
        Assert.Equal(0, weights[1].Weight);
    }

    [Fact]
    public void Legacy_call_without_rules_is_unchanged()
    {
        var pool = OneBundle();
        var a = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, Common, 3);
        Assert.Equal(3, a.Count);
    }
}
```

- [ ] **Step 2: Run, expect failure.** **Step 3: Implement.** Refactor the body of `SampleSlots` into a private `Draw(...)` pass that takes the candidate `(Id, Weight)` list, the number to draw, the rng, `slotsById`, `takenPerBundle`, `fillerPerBundle`, a `bool filler` flag, `remainingNeedForBundle`, `caps`, and appends to `result`; the rng calls (one `Next(totalWeight)` then one `Next(candidates.Count)` per pick) stay exactly as today. In `SampleSlots`: build `slotsById`, apply the week 1-2 filter, then `if (rules == null)` -> one `Draw` with rarity weights over all ids (today's path); else `weights = GoalWeighting.For(idPool, rules, rarityOf)` minus zero weights, `dueIds = ids with any Due slot`, `Draw(due, maxCount, filler: false)`, then `Draw(filler ids, Math.Min(maxCount - result.Count, rules.FillerAllowance), filler: true)`. In the filler pass a candidate slot is disallowed when `fillerPerBundle[bundle] >= MaxFillerPerBundle`. In the due pass a drawn id resolves among its `Due` slots only. **Step 4: Run whole suite** (existing sampler tests must still pass). **Step 5: Commit** (0.16.59): "v0.16.59: weekly goals follow the gate first, filler under the season allowance (one per bundle), weighted by effort tier and season (rules A, B, E)"

---

### Task 19: Rule C in `SelectionService`

**Files:**
- Modify: `src/TheLongestYear.Core/SelectionService.cs`
- Test: `tests/TheLongestYear.Tests/SelectionServiceTests.cs`

**Interfaces:**
- `public const int MinAskableToOffer = 2;`
- `OfferForWeek(int seed, int weekOfYear, IReadOnlyCollection<Theme> alreadySelectedThisMonth, Func<Theme, int>? askableFor = null)`: `askableFor == null` -> today's shuffle over every `Theme` value (legacy). Otherwise: `qualified` = themes not selected with `askableFor(t) >= MinAskableToOffer`, enum order; weighted draw without replacement (weight = askable count, `rng.Next(totalWeight)` cumulative walk) up to `OfferSize`; if short, `fallback` = `ThemeDomains.RoomThemes` not selected and not already offered, shuffled with the SAME `rng` by the legacy Fisher-Yates, appended until `OfferSize`.
- `public static IReadOnlyList<Theme> Candidates(IReadOnlyCollection<Theme> alreadySelectedThisMonth, Func<Theme, int> askableFor)`: the qualified list, or the not-selected room themes when fewer than `OfferSize` qualify (the hub reroll shuffles this).

- [ ] **Step 1: Failing tests**

```csharp
    private static int Askable(Theme t) => t switch { Theme.Spelunking => 0, Theme.Artisan => 5, Theme.Kitchen => 1, Theme.Farming => 3, _ => 0 };

    [Fact]
    public void Themes_with_fewer_than_two_askable_goals_are_never_offered()
    {
        for (int seed = 1; seed <= 100; seed++)
        {
            var offer = SelectionService.OfferForWeek(seed, 5, Array.Empty<Theme>(), Askable);
            Assert.Equal(2, offer.Count);
            Assert.DoesNotContain(Theme.Spelunking, offer);
            Assert.DoesNotContain(Theme.Kitchen, offer);
            Assert.Contains(Theme.Artisan, offer);
            Assert.Contains(Theme.Farming, offer);
        }
    }

    [Fact]
    public void Zero_askable_everywhere_reproduces_the_room_theme_offer()
    {
        var activity = new HashSet<Theme>(ThemeDomains.ActivityThemes);
        for (int seed = 1; seed <= 30; seed++)
        {
            var withRules = SelectionService.OfferForWeek(seed, 3, Array.Empty<Theme>(), _ => 0);
            var legacyRooms = SelectionService.OfferForWeek(seed, 3, activity);   // legacy path, activity themes excluded
            Assert.Equal(legacyRooms, withRules);
        }
    }

    [Fact]
    public void Weighted_draw_is_deterministic_for_a_seed()
    {
        int Ask(Theme t) => t switch { Theme.Farming => 3, Theme.Artisan => 5, Theme.Fishing => 2, _ => 0 };
        var a = SelectionService.OfferForWeek(7, 2, Array.Empty<Theme>(), Ask);
        var b = SelectionService.OfferForWeek(7, 2, Array.Empty<Theme>(), Ask);
        Assert.Equal(a, b);
        Assert.Equal(2, a.Distinct().Count());
    }

    [Fact]
    public void A_single_qualified_theme_is_padded_from_room_themes()
    {
        var offer = SelectionService.OfferForWeek(3, 6, new[] { Theme.Farming }, t => t == Theme.Artisan ? 4 : 0);
        Assert.Equal(2, offer.Count);
        Assert.Contains(Theme.Artisan, offer);
        Assert.DoesNotContain(Theme.Farming, offer);
        Assert.Contains(offer.First(t => t != Theme.Artisan), ThemeDomains.RoomThemes);
    }
```

- [ ] **Step 2: Run, expect failure.** **Step 3: Implement.** Keep `WeekSaltPrime`; legacy branch untouched. Update the `RunState.SelectedThemesThisMonth` doc comment ("eight themes, four picks a month"). **Step 4: Run.** **Step 5: Commit** (0.16.60): "v0.16.60: the weekly offer counts what a week can ask: only themes with 2+ askable goals, weighted by count, room themes as the floor (rule C)"

---

### Task 20: Rule D: the weekly bonus is paid per goal

**Files:**
- Create: `src/TheLongestYear.Core/WeeklyGoalPayout.cs`
- Modify: `src/TheLongestYear/Loop/WeeklyThemeQuestService.cs`, `src/TheLongestYear/i18n/default.json`, `CHANGELOG.md`
- Test: `tests/TheLongestYear.Tests/WeeklyGoalPayoutTests.cs`; grep tests for `hud.theme-complete` / `quest.weekly.tip` text and update.

**Interfaces:**
- `public static class WeeklyGoalPayout { long PerGoal(long weekBonus, int goalCount); int MarkPaid(IReadOnlyList<BonusSlot> slots, Func<BonusSlot, bool> isComplete) }` (`MarkPaid` sets `Paid` on every complete, unpaid slot and returns how many it marked).
- i18n: `hud.goal-paid` = "Goal done! +{{jp}} JP ({{done}}/{{total}})"; `hud.theme-complete` = "Weekly theme complete! Drawback lifted."; `quest.weekly.tip` = "Tip: hold matching donations for their theme week - each goal pays 1.5x JP plus its share of the weekly bonus, and finishing every goal lifts the drawback."

- [ ] **Step 1: Failing tests**

```csharp
public class WeeklyGoalPayoutTests
{
    [Fact]
    public void A_three_goal_summer_week_pays_forty_five_in_three_fifteens()
    {
        var jp = new JpCalculator(new JpSettings());
        long week = jp.WeeklyQuestBonus(5);           // Summer week 1: 30 x 1.5
        Assert.Equal(45, week);
        Assert.Equal(15, WeeklyGoalPayout.PerGoal(week, 3));
        Assert.Equal(17, WeeklyGoalPayout.PerGoal(120, 7));
        Assert.Equal(0, WeeklyGoalPayout.PerGoal(120, 0));
    }

    [Fact]
    public void Completing_the_same_slot_twice_pays_once()
    {
        var slots = new List<BonusSlot> { new() { ItemId = "(O)1", Deposited = true }, new() { ItemId = "(O)2" } };
        Assert.Equal(1, WeeklyGoalPayout.MarkPaid(slots, s => s.Deposited));
        Assert.Equal(0, WeeklyGoalPayout.MarkPaid(slots, s => s.Deposited));
        slots[1].Deposited = true;
        Assert.Equal(1, WeeklyGoalPayout.MarkPaid(slots, s => s.Deposited));
        Assert.All(slots, s => Assert.True(s.Paid));
    }
}
```

- [ ] **Step 2: Run, expect failure.** **Step 3: Implement** `WeeklyGoalPayout` (`Math.Round(week / (double)count, MidpointRounding.AwayFromZero)`). In `WeeklyThemeQuestService.RefreshObjective`, before the auto-complete block:

```csharp
            int newlyPaid = WeeklyGoalPayout.MarkPaid(slots, IsSlotComplete);
            if (newlyPaid > 0)
                PayGoalShares(newlyPaid, doneCount, slots.Count);
```
with

```csharp
        /// <summary>Rule D (activity-themes spec): the weekly bonus is split evenly across the
        /// week's goals and paid as each lands. BonusSlot.Paid is the idempotency guard, so a
        /// save+reload never pays a goal twice and a one-goal Winter week pays 120 / 7, not 120.</summary>
        private void PayGoalShares(int newlyPaid, int doneCount, int total)
        {
            long perGoal = WeeklyGoalPayout.PerGoal(Jp.WeeklyQuestBonus(Run.WeekOfYear), total);
            long paid = JpBoostHelper.Apply(_store.State, perGoal * newlyPaid);
            _store.State.JunimoPoints += paid;
            Game1.addHUDMessage(new HUDMessage(Strings.Get("hud.goal-paid", new Dictionary<string, string>
            {
                ["jp"] = paid.ToString(), ["done"] = doneCount.ToString(), ["total"] = total.ToString(),
            }), HUDMessage.achievement_type));
            _monitor.Log($"WeeklyThemeQuest: {newlyPaid} goal(s) done, +{paid} JP ({doneCount}/{total}, now {_store.State.JunimoPoints}).", LogLevel.Info);
        }
```
`AwardCompletionRewards` becomes `LiftLiability()`: keeps the `LiabilitySuppressedThisWeek` guard, sets it, calls `ActiveEffectsProvider.SuppressLiability()`, HUD `hud.theme-complete` with no token, log. Update `RunState.LiabilitySuppressedThisWeek` doc comment (no longer the JP guard). **Step 4: Run whole suite** (i18n token guard). **Step 5: CHANGELOG** `### Changed`: "**The weekly theme bonus is paid per goal.** The 30 JP (times the season multiplier) that used to land only when every goal was done is now split evenly across the week's goals and paid as each one lands. The drawback still lifts only when every goal is done. A one-goal Winter week pays its share, not the full 120." **Step 6: Commit** (0.16.61): "v0.16.61: weekly bonus paid per goal as each lands; drawback still lifts on the full set (rule D)"

---

### Task 21: Wiring: config, `RunController`, `MenuLauncher`, hub reroll, `tly_themepool`

**Files:**
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs`, `src/TheLongestYear/Loop/RunController.cs`, `src/TheLongestYear/UI/MenuLauncher.cs`, `src/TheLongestYear/UI/WeeklyHubMenu.cs`, `src/TheLongestYear/ModEntry.cs`, `CHANGELOG.md`
- Test: `tests/TheLongestYear.Tests/GameplayConfigTests.cs` (create if absent: default `ThemeFillerBySeason` is `[0,1,2,99]`)

**Interfaces:**
- `GameplayConfig.ThemeFillerBySeason : List<int> = new() { 0, 1, 2, GoalSamplingRules.UnlimitedFiller }` with a doc comment (rule B). `public int FillerAllowanceFor(Season season)` on `GameplayConfig` (index clamp; missing -> unlimited).
- `RunController`:
  - `public Func<string, ItemKind> ItemKindOf { get; set; }` (default `_ => ItemKind.Other`).
  - `SampleSlotsForTheme(theme, season, week)` passes `kindOf: ItemKindOf` and `rules: new GoalSamplingRules(season, _config.FillerAllowanceFor(season), EffortOf)` where `EffortOf(id) => Availability != null && Availability.HasDerivedEffort(id) ? Availability.For(id).Effort : (int?)null`.
  - `public int AskableCount(Theme theme, CoreSeason season, int week) => SampleSlotsForTheme(theme, season, week).Count`.
  - `public IReadOnlyList<Theme> OfferFor(int week, CoreSeason season, IReadOnlyCollection<Theme> selections) => SelectionService.OfferForWeek(Run.Seed, week, selections, t => AskableCount(t, season, week))`.
  - `public IReadOnlyList<Theme> OfferCandidates(int week, CoreSeason season, IReadOnlyCollection<Theme> selections) => SelectionService.Candidates(selections, t => AskableCount(t, season, week))`.
  - `PresentOffer` and `SelectByName` use `OfferFor` (season = `seasonOverride ?? Run.Season`; for `SelectByName`, `Run.Season`).
  - `public IReadOnlyList<GoalWeight> DescribeGoalPool(Theme theme, CoreSeason season, int week, out IReadOnlyList<BonusSlot> pool)` for the debug command.
- `MenuLauncher.OpenWeeklyHub`: `var offer = _runController.OfferFor(week, offerSeason, selectionsForOffer);` (log line unchanged: "Opened planning hub (week N, offer: A,B)").
- `WeeklyHubMenu.RerollOffer`: `candidates = _runController.OfferCandidates(week, _offerSeason, _run.SelectedThemesThisMonth).ToList()` then the existing salted shuffle.
- `ModEntry`: after constructing `_runController`: `_runController.ItemKindOf = id => { string bare = BundleParsing.StripQualifier(id); return Game1.objectData != null && Game1.objectData.TryGetValue(bare, out var o) ? ItemKindClassifier.From(o.Category, o.Type) : ItemKind.Other; };`
- `tly_themepool [theme]`: no arg -> one line per theme `"{theme}: askable {n}"` for `run.Season` / `run.WeekOfYear`; with a theme -> each open line: `"{Due|filler} {name} ({id}) effort {e or 'price'} tier {tier} weight {w} [{bundle}#{index}/{line}]"` plus the allowance for the season. Register next to `tly_goals` and add the bridge `case`.

- [ ] **Step 1: Config test** (failing): `Assert.Equal(new[] { 0, 1, 2, 99 }, new GameplayConfig().ThemeFillerBySeason); Assert.Equal(99, new GameplayConfig().FillerAllowanceFor(Season.Winter));`
- [ ] **Step 2: Implement all wiring**, then `dotnet build` Release (game closed) and the suite.
- [ ] **Step 3: CHANGELOG** `### Changed`: "**Weekly goals follow the season gate first.** Goals are drawn from the lines the day-28 gate demands this season; other open lines are filler, at most one per bundle per week and capped per season (Spring 0, Summer 1, Fall 2, Winter unlimited; `ThemeFillerBySeason` in config.json). Easier items are weighted earlier in the year and harder ones later, using effort derived from the game's own data." and "**The weekly offer only shows themes that can ask for two or more goals**, weighted by how much they can ask; the Bulletin Board's Mixed theme now draws from anything on the board." `### Added`: "**`tly_themepool [theme]`** prints each theme's askable goal count for the current week and, with a theme, every candidate line with its tier and weight (debug)."
- [ ] **Step 4: Commit** (0.16.62): "v0.16.62: rules A to E wired into the hub offer, the goal sampler and the reroll; ThemeFillerBySeason config; tly_themepool"

---

## Phase 3: The three activity themes' five effects

### Task 22: `RunState.DoubleProduceToday`

**Files:**
- Modify: `src/TheLongestYear.Core/RunState.cs`
- Test: `tests/TheLongestYear.Tests/RunStateTests.cs`

**Interfaces:**
- `public sealed class DoubleProduceRecord { public long AnimalId { get; set; } public string ProduceId { get; set; } = ""; }` (own file `DoubleProduceRecord.cs`, plain POCO for JSON).
- `RunState.DoubleProduceToday : List<DoubleProduceRecord>`; `void RecordDoubleProduce(long animalId, string produceId)` (idempotent per animal); `bool TryTakeDoubleProduce(long animalId, out string produceId)` (removes the record); cleared in `BeginNewRun`.

- [ ] **Step 1: Failing test**

```csharp
    [Fact]
    public void Double_produce_records_are_taken_once_and_wiped_by_a_new_run()
    {
        var run = new RunState();
        run.RecordDoubleProduce(7, "184");
        run.RecordDoubleProduce(7, "184");
        Assert.Single(run.DoubleProduceToday);
        Assert.True(run.TryTakeDoubleProduce(7, out string produce));
        Assert.Equal("184", produce);
        Assert.False(run.TryTakeDoubleProduce(7, out _));
        run.RecordDoubleProduce(8, "440");
        run.BeginNewRun(5);
        Assert.Empty(run.DoubleProduceToday);
    }
```
- [ ] **Step 2: Run, expect failure.** **Step 3: Implement.** **Step 4: Run.** **Step 5: Commit** (0.16.63): "v0.16.63: RunState.DoubleProduceToday for the Kitchen bonus"

---

### Task 23: Spelunking bonus and Kitchen liability: `MonsterDropsDoublePatch`, `MonsterDamageUpPatch`

**Files:**
- Create: `src/TheLongestYear/Loop/MonsterThemePatches.cs`
- Modify: `CHANGELOG.md`

Both classes are free static Harmony classes (auto-patched by `harmony.PatchAll`, same as `FishBiteRatePatch`); log through `PatchLog`.

- [ ] **Step 1: Write the patches**

```csharp
using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Monsters;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Spelunking bonus (monster_drops_double): 10% chance a slain monster drops
    /// everything twice. Postfix on GameLocation.monsterDrop (decompile GameLocation.cs:4360):
    /// snapshot the debris count before, clone every item-bearing Debris the call added, the same
    /// shape as vanilla's own Book_Void 3% clone at the end of that method. Trinkets are skipped.</summary>
    [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.monsterDrop),
        new Type[] { typeof(Monster), typeof(int), typeof(int), typeof(Farmer) })]
    internal static class MonsterDropsDoublePatch
    {
        public const string BonusId = "monster_drops_double";
        private const double Chance = 0.10;

        private static void Prefix(GameLocation __instance, out int __state)
            => __state = __instance?.debris?.Count ?? -1;

        private static void Postfix(GameLocation __instance, Monster monster, int x, int y, Farmer who, int __state)
        {
            if (!ActiveEffectsProvider.ActiveBonus(BonusId)) return;
            if (__state < 0 || __instance?.debris == null || monster == null || who == null) return;
            int total = __instance.debris.Count;
            if (total <= __state) return;

            double roll = Game1.random.NextDouble();
            if (roll >= Chance) { PatchLog.Trace($"{BonusId}: roll={roll:F3} >= {Chance:F2}, no double."); return; }

            try
            {
                Vector2 playerPos = Utility.PointToVector2(who.StandingPixel);
                var clones = new List<Debris>();
                for (int i = __state; i < total; i++)
                {
                    Debris d = __instance.debris[i];
                    Item clone = null;
                    if (d?.item != null)
                    {
                        if (d.item is StardewValley.Objects.Trinkets.Trinket) continue;
                        clone = d.item.getOne();
                        if (clone != null) { clone.Stack = d.item.Stack; clone.HasBeenInInventory = false; }
                    }
                    else if (!string.IsNullOrEmpty(d?.itemId?.Value))
                    {
                        if (d.itemId.Value.StartsWith("(TR)", StringComparison.Ordinal)) continue;
                        clone = ItemRegistry.Create(d.itemId.Value, 1, 0, allowNull: true);
                        if (clone != null) clone.HasBeenInInventory = false;
                    }
                    if (clone != null)
                        clones.Add(monster.ModifyMonsterLoot(new Debris(clone, new Vector2(x, y), playerPos)));
                }
                foreach (Debris c in clones) __instance.debris.Add(c);
                PatchLog.Info($"{BonusId}: {monster.Name} dropped everything twice ({clones.Count} extra drop(s), roll {roll:F3}).");
            }
            catch (Exception ex)
            {
                PatchLog.Trace($"{BonusId}: clone path threw: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    /// <summary>Kitchen liability (monster_damage_up): monsters deal 25% more damage. Prefix on
    /// Farmer.takeDamage (decompile Farmer.cs:7331); only when a monster is the damager.</summary>
    [HarmonyPatch(typeof(Farmer), nameof(Farmer.takeDamage), new Type[] { typeof(int), typeof(bool), typeof(Monster) })]
    internal static class MonsterDamageUpPatch
    {
        public const string LiabilityId = "monster_damage_up";
        private const double Factor = 1.25;

        private static void Prefix(ref int damage, Monster damager)
        {
            if (damager == null || !ActiveEffectsProvider.ActiveLiability(LiabilityId)) return;
            int boosted = (int)Math.Ceiling(damage * Factor);
            PatchLog.Trace($"{LiabilityId}: {damager.Name} damage {damage} -> {boosted}.");
            damage = boosted;
        }
    }
}
```
- [ ] **Step 2: Build** Release (game closed). **Step 3: CHANGELOG** `### Added`: "**Three new weekly themes: Spelunking, Artisan and Kitchen.** Their goals match by item kind anywhere on the board (gems, minerals, monster loot and artifacts; artisan goods; cooked dishes and animal products). Spelunking: 10% chance a slain monster drops everything twice, but machines run 25% slower. Artisan: machines finish 25% sooner, but cooked food restores half its energy and health and gives no buffs. Kitchen: 20% chance an animal gives a second product each day, but monsters deal 25% more damage. Eight themes, still two cards a week." (one line, written once here; later tasks do not repeat it). **Step 4: Commit** (0.16.64): "v0.16.64: Spelunking bonus (monster drops doubled 10%) and Kitchen liability (monster damage +25%)"

---

### Task 24: Machine speed: `MachineReadyTime` (Core) + `MachineSpeedPatch`

**Files:**
- Create: `src/TheLongestYear.Core/MachineReadyTime.cs`
- Create: `src/TheLongestYear/Loop/MachineSpeedPatch.cs`
- Test: `tests/TheLongestYear.Tests/MachineReadyTimeTests.cs`

**Interfaces:** `public static class MachineReadyTime { const double FastFactor = 0.75; const double SlowFactor = 1.25; const int RoundTo = 10; const int Floor = 10; int Scale(int minutes, double factor) }`

- [ ] **Step 1: Failing tests**

```csharp
public class MachineReadyTimeTests
{
    [Theory]
    [InlineData(200, 0.75, 150)] [InlineData(200, 1.25, 250)] [InlineData(1750, 0.75, 1310)] [InlineData(1750, 1.25, 2190)]
    [InlineData(10, 0.75, 10)] [InlineData(4, 1.25, 10)] [InlineData(0, 1.25, 0)] [InlineData(-1, 0.75, -1)]
    public void Scales_and_rounds_to_ten_minutes_with_a_ten_minute_floor(int minutes, double factor, int expected)
        => Assert.Equal(expected, MachineReadyTime.Scale(minutes, factor));
}
```
- [ ] **Step 2: Run, expect failure.** **Step 3: Implement** (`minutes <= 0` returned unchanged; `Math.Max(Floor, (int)Math.Round(minutes * factor / RoundTo, MidpointRounding.AwayFromZero) * RoundTo)`), and the patch:

```csharp
    /// <summary>Artisan bonus (machines_fast) / Spelunking liability (machines_slow). Postfix on
    /// Object.OutputMachine (decompile Object.cs:2481), which is where MinutesUntilReady is set
    /// for every data-driven machine (kegs, jars, casks, bee houses, tappers, smokers). Scales the
    /// queued time by 0.75 or 1.25, rounded to 10 minutes, floor 10.</summary>
    [HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.OutputMachine))]
    internal static class MachineSpeedPatch
    {
        public const string BonusId = "machines_fast";
        public const string LiabilityId = "machines_slow";

        private static void Postfix(StardewValley.Object __instance, bool probe, bool heldObjectOnly, bool __result)
        {
            if (!__result || probe || heldObjectOnly || __instance == null) return;
            int before = __instance.MinutesUntilReady;
            if (before <= 0) return;
            double factor;
            string effect;
            if (ActiveEffectsProvider.ActiveBonus(BonusId)) { factor = MachineReadyTime.FastFactor; effect = BonusId; }
            else if (ActiveEffectsProvider.ActiveLiability(LiabilityId)) { factor = MachineReadyTime.SlowFactor; effect = LiabilityId; }
            else return;
            __instance.MinutesUntilReady = MachineReadyTime.Scale(before, factor);
            PatchLog.Info($"{effect}: {__instance.Name} ready in {__instance.MinutesUntilReady} min (was {before}).");
        }
    }
```
- [ ] **Step 4: Build + suite.** **Step 5: Commit** (0.16.65): "v0.16.65: Artisan bonus (machines 25% sooner) and Spelunking liability (machines 25% slower)"

---

### Task 25: Artisan liability: `CookedFoodWeakPatch`

**Files:**
- Create: `src/TheLongestYear/Loop/CookedFoodWeakPatch.cs`

- [ ] **Step 1: Write three postfix classes** in one file, all guarded by `ActiveEffectsProvider.ActiveLiability("cooked_food_weak") && __instance.Category == StardewValley.Object.CookingCategory`:
  - `[HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.staminaRecoveredOnConsumption))]` Postfix `(StardewValley.Object __instance, ref int __result)`: `__result /= HalfDivisor` (const 2) when `__result > 0`.
  - Same for `healthRecoveredOnConsumption`.
  - `[HarmonyPatch(typeof(StardewValley.Object), nameof(StardewValley.Object.GetFoodOrDrinkBuffs))]` Postfix `(StardewValley.Object __instance, ref IEnumerable<Buff> __result)`: `__result = System.Linq.Enumerable.Empty<Buff>()`.
  - One `PatchLog.Trace` per fire, mentioning the item name. Doc comment: week-scoped, items untouched; halves the numbers the eat HUD and tooltips show because those read the same methods.
- [ ] **Step 2: Build.** **Step 3: Commit** (0.16.66): "v0.16.66: Artisan liability (cooked food restores half, no buffs)"

---

### Task 26: Kitchen bonus: `AnimalDoubleProductPatch`

**Files:**
- Create: `src/TheLongestYear/Loop/AnimalDoubleProductPatch.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (`AnimalDoubleProductPatch.Connect(() => _meta.Run)` next to `DejaVuDialoguePatch.Connect`), `src/TheLongestYear/Loop/RunController.cs` (`Run.DoubleProduceToday.Clear()` at the top of `OnDayEnding`)

- [ ] **Step 1: Write the patches**

```csharp
    /// <summary>Kitchen bonus (animal_double_product): 20% chance an animal gives a second product
    /// that day. Two shapes, because the game has two produce paths (FarmAnimal.dayUpdate,
    /// decompile FarmAnimal.cs:929):
    ///  - animals with currentProduce (cows, goats, sheep, pigs): when dayUpdate sets a NEW
    ///    currentProduce and the roll passes, the animal id + produce id are recorded in
    ///    RunState.DoubleProduceToday; when the pail, shears or truffle dig clears currentProduce
    ///    the record puts it back once (MilkPail.DoFunction, Shears.DoFunction, FarmAnimal.DigUpProduce).
    ///  - overnight droppers (chickens, ducks, rabbits, dinosaurs): the produce object lands in
    ///    the coop during dayUpdate; a second copy is spawned beside it.
    /// Records are cleared on DayEnding, before the night's dayUpdate writes new ones.</summary>
    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.dayUpdate))]
    internal static class AnimalDoubleProductPatch
    {
        public const string BonusId = "animal_double_product";
        private const double Chance = 0.20;
        private static Func<RunState> _run;
        public static void Connect(Func<RunState> run) => _run = run;

        internal sealed class State { public string ProduceBefore; public HashSet<Vector2> Tiles; public GameLocation Indoors; }

        private static void Prefix(FarmAnimal __instance, out State __state)
        {
            GameLocation indoors = __instance?.home?.GetIndoors();
            __state = new State
            {
                ProduceBefore = __instance?.currentProduce?.Value,
                Indoors = indoors,
                Tiles = indoors?.objects != null ? new HashSet<Vector2>(indoors.objects.Keys) : null,
            };
        }

        private static void Postfix(FarmAnimal __instance, State __state)
        {
            if (!ActiveEffectsProvider.ActiveBonus(BonusId) || __instance == null || __state == null) return;
            RunState run = _run?.Invoke();
            if (run == null) return;
            try
            {
                string produce = __instance.currentProduce?.Value;
                if (produce != null && produce != __state.ProduceBefore)
                {
                    if (Game1.random.NextDouble() >= Chance) return;
                    run.RecordDoubleProduce(__instance.myID.Value, produce);
                    PatchLog.Info($"{BonusId}: {__instance.displayName} will give a second {produce} today.");
                    return;
                }
                if (__state.Indoors?.objects == null || __state.Tiles == null) return;
                foreach (Vector2 tile in __state.Indoors.objects.Keys)
                {
                    if (__state.Tiles.Contains(tile)) continue;
                    StardewValley.Object dropped = __state.Indoors.objects[tile];
                    if (dropped == null) continue;
                    if (Game1.random.NextDouble() >= Chance) return;
                    var copy = (StardewValley.Object)dropped.getOne();
                    Utility.spawnObjectAround(__instance.Tile, copy, __state.Indoors);
                    PatchLog.Info($"{BonusId}: {__instance.displayName} left a second {dropped.Name}.");
                    return;
                }
            }
            catch (Exception ex) { PatchLog.Trace($"{BonusId}: threw {ex.GetType().Name}: {ex.Message}"); }
        }

        /// <summary>Shared by the three collect postfixes.</summary>
        internal static void RestoreIfRecorded(FarmAnimal animal)
        {
            if (animal == null || animal.currentProduce?.Value != null) return;
            RunState run = _run?.Invoke();
            if (run == null || !run.TryTakeDoubleProduce(animal.myID.Value, out string produce)) return;
            animal.currentProduce.Value = produce;
            animal.ReloadTextureIfNeeded();
            PatchLog.Info($"{BonusId}: {animal.displayName} has a second {produce} ready.");
        }
    }

    [HarmonyPatch(typeof(StardewValley.Tools.MilkPail), nameof(StardewValley.Tools.MilkPail.DoFunction))]
    internal static class MilkPailDoublePatch { private static void Postfix(StardewValley.Tools.MilkPail __instance) => AnimalDoubleProductPatch.RestoreIfRecorded(__instance.animal); }

    [HarmonyPatch(typeof(StardewValley.Tools.Shears), nameof(StardewValley.Tools.Shears.DoFunction))]
    internal static class ShearsDoublePatch { private static void Postfix(StardewValley.Tools.Shears __instance) => AnimalDoubleProductPatch.RestoreIfRecorded(__instance.animal); }

    [HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.DigUpProduce))]
    internal static class DigUpDoublePatch { private static void Postfix(FarmAnimal __instance) => AnimalDoubleProductPatch.RestoreIfRecorded(__instance); }
```
(Verify `Building.GetIndoors()` exists in the decompile; fall back to `__instance.homeInterior` if not. `animal` is a public field on both tools.)
- [ ] **Step 2: Wire Connect and the DayEnding clear; build; suite.** **Step 3: Commit** (0.16.67): "v0.16.67: Kitchen bonus (20% chance of a second animal product each day)"

---

### Task 27: Docs: README, Nexus description, catalogue theme-pools section, STATUS/TODO

**Files:**
- Modify: `README.md` (Features line 35 and How-it-works line 61), `docs/nexus-description.bbcode` (line 32 and its How-it-works twin), `src/TheLongestYear/ModEntry.cs` (`tly_dumpbundles`: new `AppendThemePools(sb)` after `AppendPools`), `STATUS.md` (top), `TODO.md` (the "SPEC APPROVED, NOT PLANNED" heading becomes "BUILT 0.16.42 to 0.16.68, not yet real-play tested")

- [ ] **Step 1: README + Nexus, identical wording**: Features bullet: "**Weekly themes.** Each week, pick one of two themes (Foraging, Farming, Fishing, Mining, Mixed, Spelunking, Artisan, Kitchen) for a bonus and a paired liability. Goals follow the season gate and the weekly bonus is paid per goal."; How-it-works bullet: "**Weekly themes.** Each week you choose one of two offered themes. Room themes (Foraging, Farming, Fishing, Mining) take goals from their Community Center room; Spelunking, Artisan and Kitchen take goals by item kind from anywhere on the board, and Mixed takes anything. Goals follow what the season gate demands first; the weekly JP bonus is paid goal by goal and the drawback lifts when every goal is done. The planning hub opens at the start of each week."
- [ ] **Step 2: `AppendThemePools`**: a "## Theme pools" section: the spec's simulated line counts table (monster drops 2.4 avg, absent 27%; artifacts 3.4 / 23%; animal products 3.5 / 5%; minerals + gems 7.4; cooked dishes 7.4; artisan goods 13.5; Spelunking merged 13.1, under 4 on 1.6%; Kitchen merged 24.5) with a note that they are from the 100,000-board simulation in the spec, then "### Effort overrides" listing `_config`'s effort overrides (if a config field exists) or "none (the override table ships empty)".
- [ ] **Step 3: STATUS.md** top block: what shipped (0.16.42 to 0.16.68), decisions from Global Constraints, what is committed / deployed / not pushed, the open items (Dinosaur Egg vs Diamond tiering claim in the spec could not be reproduced under the spec's own formula: Dino 3, Diamond 5; the review doc shows the real numbers). TODO.md heading update.
- [ ] **Step 4: Commit** (docs only, no bump): "docs: eight weekly themes in README and Nexus description; theme pools section in the catalogue; STATUS/TODO for the activity themes build"

---

### Task 28: Deploy and live check (ask Jeff before driving)

- [ ] **Step 1:** Ask Jeff: "OK to drive the game now?" (memory ask-before-driving-desktop). Wait for yes.
- [ ] **Step 2:** `pwsh -NoProfile -File tools/deploy.ps1` (archives the log, closes the game, builds, relaunches). Then `git checkout -- test-output/log-archive/SMAPI-playtest-2026-05-26-*.txt` if the pull pruned tracked archives.
- [ ] **Step 3:** After the title screen: `pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_loadsave <current Rodger folder>"`, wait ~45 s (`-Action wait -Pattern "Run \d+ ready"`).
- [ ] **Step 4:** `bridge.ps1 -Action send -Lines "tly_dumpeffort|tly_themepool|tly_goals spring|tly_goals summer|tly_goals fall|tly_goals winter"`; copy `item-effort-model.md` from the mod folder to `docs/`.
- [ ] **Step 5:** Several `tly_reset` cycles (wait for `In-place reset: complete`, then the hub log line `Opened planning hub (week 1, offer: A,B)`); pick a card each time (left (707,530) / right (1210,530) via `tools/game.ps1 -Click`) or let the next reset replace it. Confirm from the SMAPI log that Spelunking / Artisan / Kitchen appear in offers (expect them mostly from Summer on: also run `tly_goals summer|fall|winter` and read their goal lists), that Spring offers are room themes, and that `tly_themepool` shows askable counts.
- [ ] **Step 6:** Report: committed range, deployed build, nothing pushed, the log evidence, and anything left out. Do NOT run `tools/sim-season.sh` or `tly_playseason`.

---

## Self-review notes

- Spec coverage: Rulings 1 to 6 -> Tasks 15 to 21 and 22 to 26; themes table -> 15; effects -> 23 to 26; goal domains -> 15, 17; rules A/B -> 16, 18, 21; rule C -> 19, 21; rule D -> 20; rule E -> 12, 18; effort rules per domain -> 3 to 11; composer, overrides, unrecognised logging -> 13, 14; hub/HUD -> 21 (no layout change); persistence -> 17 (Paid), 22; debug commands -> 14, 21; testing list -> the tests in each task; docs -> 27; phasing -> sections; live checks -> 28.
- Not built (say so in the report): the spec's live checklist item "pick Kitchen with animals, debug sleep, confirm a doubled product" and the other in-game effect confirmations are Jeff's real-play test, not this session's; Task 28 verifies the offer and goal lists from the log only.
