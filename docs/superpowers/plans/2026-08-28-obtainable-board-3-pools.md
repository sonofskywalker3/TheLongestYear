# Obtainable Board, Plan 3 of 5: full pools, no fixed lists Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every TLY Custom bundle keeps its name, room and pick count but rolls its slots from the full pool of its kind (or a named recipe for the mixed-kind bundles); string-id vanilla items weigh like vanilla; Stonefish, Ice Pip, Lava Eel, the five legendaries and the year-2 crops enter the pools at weight 1; the rewind lets a legendary be caught again.

**Architecture:** `ItemPoolBuilder` builds one pool per `ItemKind` from Data/Objects plus the special sets (resources, totems and essences, trophies, colour tags) into `ItemPools.ByKind` and `ItemPools.Special`; a new `BundlePoolRecipes` table maps bundle names to a `PoolRecipe` (ordered parts, each a source and a count); `PoolDomainClassifier` returns `PoolDomain.Recipe` for any non-money bundle it cannot place in a legacy domain; `BundleSlotFiller.Candidates` serves recipe parts. Pool additions are a small table read by the builder; the rewind change lives in `FarmerReset`.

**Tech Stack:** C# / .NET 6, xunit, SMAPI. Depends on plan 1 (weeks, absolute bands) and plan 2 (`StretchRule`, hard-item rule in the filler).

**Spec:** `docs/superpowers/specs/2026-08-28-obtainable-board-design.md` section 3, "Rulings on the mixed-kind bundles", Easy.

## Global Constraints

- No em dashes. Patch bump per commit, local commits only, stage only the task's files.
- Board determinism: pools and recipes are pure functions of game data, tuning and the difficulty step.
- Weights: `VanillaItemWeight` 3, `ModdedItemWeight` 1. Vanilla is any id without a `.` in it (SMAPI mod items are `Author.Mod_Item`); string-id vanilla items (Goby, the jellies, Broccoli, Moss, Mystery Box, Book_*) therefore weigh 3. Additions at weight 1: Stonefish `(O)158`, Ice Pip `(O)161`, Lava Eel `(O)162`, Legend `(O)163`, Crimsonfish `(O)159`, Angler `(O)160`, Glacierfish `(O)775`, Mutant Carp `(O)682`, Garlic `(O)248`, Red Cabbage `(O)266`, Artichoke `(O)274`.
- Easy keeps `YearTwoCrops` exclusion; Normal, Hard, Extreme drop it.
- A legendary drawn into a 4-of-4 fish bundle is mandatory for it (Jeff: a hard roll is a challenge).
- Money (Vault) bundles are never rolled. Season-named bundles keep their seasonal pools.
- Existing vet stays for everything else: no quest items, no `ExcludeFromRandomSale` except the listed additions and the Night Market fish, no unknown-to-Data/Objects ids, `BuiltInExcludedItemIds` (Banana, Mango from plan 1).

---

### Task 1: Weights: vanilla by id shape

**Files:**
- Modify: `src/TheLongestYear.Core/ItemPoolBuilder.cs:641-643`
- Test: `tests/TheLongestYear.Tests/ItemPoolBuilderTests.cs` (grep `VanillaItemWeight` for the existing test)

- [ ] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData("(O)Goby", 3)] [InlineData("(O)SeaJelly", 3)] [InlineData("(O)24", 3)]
[InlineData("(O)sonofskywalker3.CartCatalog_Book", 1)] [InlineData("(O)Author.Mod_Fish", 1)]
public void Vanilla_is_any_id_without_a_dot(string id, int weight)
    => Assert.Equal(weight, ItemPoolBuilder.WeightFor(id, new BundleGenerationTuning()));
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

```csharp
private const char ModIdSeparator = '.';

/// <summary>Draw weight: a named override, else vanilla for any id without a mod prefix
/// (SMAPI mod items are Author.Mod_Item), else modded. 1.6's own string ids (Goby, the jellies,
/// Broccoli, Moss, Mystery Box, the books) are vanilla (Jeff, 2026-08-28).</summary>
public static int WeightFor(string qualifiedId, BundleGenerationTuning tuning)
{
    if (tuning.RareRollWeights.TryGetValue(qualifiedId, out int over)) return over;
    return Unqualify(qualifiedId).Contains(ModIdSeparator) ? tuning.ModdedItemWeight : tuning.VanillaItemWeight;
}
```

and have `MakeItem` call it. Fix the doc comment at the top of the file.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump patch)

---

### Task 2: Pool additions: mine fish, legendaries, year-2 crops

**Files:**
- Create: `src/TheLongestYear.Core/PoolAdditions.cs`
- Modify: `src/TheLongestYear.Core/ItemPoolBuilder.cs` (vet exception for the addition ids; the Fish pool gains the mine fish and legendaries with their seasons and locations; `fish_legendary` vet removed), `src/TheLongestYear.Core/YearTwoCrops.cs` (`ExcludedFor(hasUpgrade, DifficultyStep step)`), `src/TheLongestYear.Core/Availability/FishAvailability.cs` (`LegendaryPacingWeeks` table in `AvailabilityWeeks`), `src/TheLongestYear.Core/AvailabilityWeeks.cs`, `src/TheLongestYear/Loop/GameDataPools.cs` (pass the step into `ExcludedFor`)
- Test: `tests/TheLongestYear.Tests/PoolAdditionsTests.cs`

**Interfaces:**
- Produces: `PoolAdditions.Fish : IReadOnlyList<PoolAddition>` where `PoolAddition(string ItemId, IReadOnlyList<Season> Seasons, IReadOnlyList<string> Locations, int Weight)`; `PoolAdditions.YearTwoCropIds`; `AvailabilityWeeks.LegendaryPacingWeeks` (`(O)163` 4, `(O)159` 5, `(O)160` 9, `(O)775` 13, `(O)682` 7); `YearTwoCrops.ExcludedFor(Func<string,bool> hasUpgrade, DifficultyStep step)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Mine_fish_and_legendaries_join_the_fish_pool_at_weight_1()
{
    ItemPools pools = BuildPoolsWithObjects(
        ("158", "Stonefish", excludeFromRandomSale: true, tags: new string[0]),
        ("163", "Legend", excludeFromRandomSale: false, tags: new[] { "fish_legendary" }),
        ("128", "Pufferfish", excludeFromRandomSale: false, tags: new string[0]));
    Assert.Equal(1, pools.Fish.Single(p => p.ItemId == "(O)158").Weight);
    Assert.Equal(1, pools.Fish.Single(p => p.ItemId == "(O)163").Weight);
    Assert.Equal(new[] { "UndergroundMine" }, pools.Fish.Single(p => p.ItemId == "(O)158").Locations);
    Assert.Equal(new[] { Season.Spring }, pools.Fish.Single(p => p.ItemId == "(O)163").Seasons);
}

[Fact]
public void Year_two_crops_are_excluded_only_on_easy()
{
    Assert.Contains("(O)266", YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Easy));
    Assert.Empty(YearTwoCrops.ExcludedFor(_ => false, DifficultyStep.Normal));
}

[Theory]
[InlineData("(O)163", 4)] [InlineData("(O)682", 7)] [InlineData("(O)775", 13)]
public void Legendaries_have_pacing_weeks(string id, int week)
{
    var item = new PoolItem(id, 5000, 1, PoolAdditions.Fish.Single(a => a.ItemId == id).Seasons, PoolAdditions.Fish.Single(a => a.ItemId == id).Locations);
    Assert.Equal(week, FishAvailability.Derive(item, null).Week);
}
```

(`BuildPoolsWithObjects` is a test helper calling `ItemPoolBuilder.Build` with a minimal `RawObjectEntry` dictionary and empty spawn lists; copy the shape from the existing pool-builder tests.)

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

```csharp
namespace TheLongestYear.Core;

public sealed record PoolAddition(string ItemId, IReadOnlyList<Season> Seasons, IReadOnlyList<string> Locations, int Weight);

/// <summary>Items the game data does not put in a pool but Jeff wants on the board at the jelly
/// rate (weight 1): the three mine fish MineShaft.getFish hard-codes by area, the five legendaries
/// (fish_legendary, CatchLimit 1; the rewind clears the catch), and the year-2 crops (a Boost or a
/// permanent buy is their route). Spec 2026-08-28-obtainable-board, section 3.</summary>
public static class PoolAdditions
{
    private static readonly string[] Mine = { "UndergroundMine" };
    private static readonly Season[] Any = System.Array.Empty<Season>();

    public static readonly IReadOnlyList<PoolAddition> Fish = new[]
    {
        new PoolAddition("(O)158", Any, Mine, 1),                              // Stonefish, floors 1 to 39
        new PoolAddition("(O)161", Any, Mine, 1),                              // Ice Pip, floors 40 to 79
        new PoolAddition("(O)162", Any, Mine, 1),                              // Lava Eel, floors 80 to 119
        new PoolAddition("(O)163", new[] { Season.Spring }, new[] { "Mountain" }, 1),   // Legend, rain, Fishing 10
        new PoolAddition("(O)159", new[] { Season.Summer }, new[] { "Beach" }, 1),      // Crimsonfish, Fishing 5
        new PoolAddition("(O)160", new[] { Season.Fall }, new[] { "Town" }, 1),         // Angler, Fishing 3
        new PoolAddition("(O)775", new[] { Season.Winter }, new[] { "Forest" }, 1),     // Glacierfish, Fishing 6
        new PoolAddition("(O)682", Any, new[] { "Sewer" }, 1),                          // Mutant Carp
    };

    public static readonly IReadOnlySet<string> YearTwoCropIds =
        new HashSet<string>(System.StringComparer.Ordinal) { "(O)248", "(O)266", "(O)274" };

    public static readonly IReadOnlySet<string> VetExceptions =
        new HashSet<string>(System.StringComparer.Ordinal) { "(O)158", "(O)161", "(O)162", "(O)163", "(O)159", "(O)160", "(O)775", "(O)682" };
}
```

`AvailabilityWeeks.LegendaryPacingWeeks` as listed; in `FishAvailability.Derive`, after the mine-fish check: `if (AvailabilityWeeks.LegendaryPacingWeeks.TryGetValue(item.ItemId, out int legendary)) week = Math.Max(week, legendary);` (hard week stays the season and location week). `ItemPoolBuilder`: `Vets` skips the `ExcludeFromRandomSale` and `fish_legendary` checks for `PoolAdditions.VetExceptions`; remove the `fish_legendary` vet entirely (legendaries are now wanted); after the Fish pool is built, append each addition whose id exists in `objects` and is not already present, with `Weight` from the addition. `YearTwoCrops.ExcludedFor` gains the step parameter: `if (step != DifficultyStep.Easy) return empty;`. `GameDataPools` passes the live step (it already resolves the difficulty for other purposes; if not, thread it from `ModEntry` the same way the model's step is).

- [ ] **Step 4: Run all tests, expect green** (update the tests that asserted the legendary vet)

- [ ] **Step 5: Commit** (bump patch)

---

### Task 3: The rewind lets a legendary be caught again

**Files:**
- Modify: `src/TheLongestYear/Loop/FarmerReset.cs`, `src/TheLongestYear/Loop/GameDataPools.cs` or a new small reader for `CatchLimit` ids (`CatchLimitedFishIds`)
- Test: `tests/TheLongestYear.Tests/FarmerResetTests.cs` if the reset has pure helpers; otherwise a Core helper `CaughtFishReset.IdsToClear(IEnumerable<string> catchLimited, IEnumerable<string> caught)` with a unit test

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Only_catch_limited_fish_are_cleared()
{
    var limited = new[] { "(O)163", "(O)682" };
    var caught = new[] { "(O)163", "(O)128", "(O)682" };
    Assert.Equal(new[] { "(O)163", "(O)682" }, CaughtFishReset.IdsToClear(limited, caught).OrderBy(x => x));
}
```

- [ ] **Step 2: Run, expect a compile failure**

- [ ] **Step 3: Implement**

`src/TheLongestYear.Core/CaughtFishReset.cs`:

```csharp
/// <summary>The rewind must let a legendary be caught again: the game blocks a repeat through
/// CatchLimit against player.fishCaught (GameLocation.cs:13831) and the reset never touched that
/// record. Only catch-limited ids are cleared; the collection tab keeps everything else.</summary>
public static class CaughtFishReset
{
    public static IReadOnlyList<string> IdsToClear(IEnumerable<string> catchLimitedIds, IEnumerable<string> caughtIds)
    {
        var limited = new HashSet<string>(catchLimitedIds, StringComparer.Ordinal);
        return caughtIds.Where(limited.Contains).ToList();
    }
}
```

Glue: at SaveLoaded, read `Data/Locations` (`LocationData.Fish` rows with `CatchLimit > 0`) into a set of qualified ids (`ItemRegistry.QualifyItemId`), store it beside the pools; in `FarmerReset` where `p.fishCaught` would be, call `foreach (string id in CaughtFishReset.IdsToClear(catchLimited, p.fishCaught.Keys.ToList())) p.fishCaught.Remove(id);` and log `"Reset: cleared {n} catch-limited fish so they can be caught again."`.

- [ ] **Step 4: Run all tests, build the mod, expect green and clean**

- [ ] **Step 5: Commit** (bump patch)

---

### Task 4: Pools by kind and the special sets

**Files:**
- Modify: `src/TheLongestYear.Core/ItemPoolModel.cs` (`ItemPools.ByKind`, `ItemPools.Special`), `src/TheLongestYear.Core/ItemPoolBuilder.cs` (build them), `src/TheLongestYear.Core/ItemKind.cs` (add `Resource`, `Seed`, `Sapling`, `Book`, `Trophy`, `Totem`, `Essence` kinds where Data/Objects can tell: category -16 building resources, -74 seeds, `Type == "Book"` or `book_item` tag, id in `AuthoredBundleCatalog.GilTrophies`, name ends with " Totem", name ends with " Essence")
- Test: `tests/TheLongestYear.Tests/ItemPoolBuilderTests.cs`

**Interfaces:**
- Produces: `ItemPools.ByKind : IReadOnlyDictionary<ItemKind, IReadOnlyList<PoolItem>>` (every vetted Data/Objects item under its kind; `Other` included); `ItemPools.ColourTags : IReadOnlyDictionary<string, IReadOnlyList<PoolItem>>` (tag `color_red` and so on -> items carrying it); `ItemPools.WinterOnly : IReadOnlyList<PoolItem>` (vetted items whose catalog seasons are exactly Winter); `ItemKindClassifier.From(bareId, RawObjectEntry)` overload.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Every_vetted_object_lands_in_its_kind_pool()
{
    ItemPools pools = BuildPoolsWithObjects(
        ("388", "Wood", cat: -16), ("176", "Egg", cat: -5), ("184", "Milk", cat: -6),
        ("72", "Diamond", cat: -2), ("681", "Rain Totem", cat: 0), ("768", "Solar Essence", cat: -28));
    Assert.Contains(pools.ByKind[ItemKind.Resource], p => p.ItemId == "(O)388");
    Assert.Contains(pools.ByKind[ItemKind.Egg], p => p.ItemId == "(O)176");
    Assert.Contains(pools.ByKind[ItemKind.Gem], p => p.ItemId == "(O)72");
    Assert.Contains(pools.ByKind[ItemKind.Totem], p => p.ItemId == "(O)681");
    Assert.Contains(pools.ByKind[ItemKind.MonsterLoot], p => p.ItemId == "(O)768");
}

[Fact]
public void Colour_tags_index_items_by_colour()
{
    ItemPools pools = BuildPoolsWithObjects(("420", "Red Mushroom", cat: -81, tags: new[] { "color_red" }));
    Assert.Contains(pools.ColourTags["color_red"], p => p.ItemId == "(O)420");
}
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

Extend `ItemKind` with `Resource, Seed, Sapling, Book, Trophy, Totem, Essence` and `ItemKindClassifier.From(string bareId, RawObjectEntry obj)` that checks, in order: Arch type -> Artifact; `Type == "Book"` or tag `book_item` -> Book; `AuthoredBundleCatalog.GilTrophies` contains the qualified id -> Trophy; name ends with " Totem" -> Totem; name ends with " Essence" -> Essence; category -16 -> Resource; -74 with "Sapling" in the name -> Sapling, else -74 -> Seed; then the existing category switch. Keep the old `From(category, type)` for the theme classifier (it maps the new kinds to `Other` for themes: Mixed only). In `ItemPoolBuilder.Build`, after the existing pools, build `ByKind` by walking every `objects` entry that passes `Vets`, with `MakeItem` (seasons from the catalog where known, else empty), and `ColourTags` from each object's `ContextTags` starting with `color_`, and `WinterOnly`. Weapons and hats for Trophy are not in Data/Objects: `ByKind[Trophy]` is built from `AuthoredBundleCatalog.GilTrophies` directly with weight 3.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump patch)

---

### Task 5: `BundlePoolRecipes` and `PoolDomain.Recipe`

**Files:**
- Create: `src/TheLongestYear.Core/BundlePoolRecipes.cs`
- Modify: `src/TheLongestYear.Core/ItemPoolModel.cs` (`PoolDomain.Recipe`), `src/TheLongestYear.Core/PoolDomainClassifier.cs` (non-money, non-seasonal, non-legacy bundles return `Recipe`), `src/TheLongestYear.Core/BundleSlotFiller.cs` (`Candidates` for Recipe; per-part sampling)
- Test: `tests/TheLongestYear.Tests/BundlePoolRecipesTests.cs`, `PoolDomainClassifierTests.cs`

**Interfaces:**
- Produces: `PoolPart(Func<ItemPools, ItemAvailabilityModel?, IReadOnlyList<PoolItem>> Source, int Count, string Label)`; `PoolRecipe(string Name, IReadOnlyList<PoolPart> Parts)` where a `Count` of 0 means "the rest of the slots"; `BundlePoolRecipes.For(string bundleName, IReadOnlyList<string> vanillaIds, ItemPools pools, ItemAvailabilityModel? model) : PoolRecipe` (named recipe, else the majority `ItemKind` of the vanilla ids, else `Other`).

Named recipes, from the spec's rulings (parts listed in order; every part also keeps the bundle's own vanilla ids as candidates):

| Bundle | Parts |
|---|---|
| Treasure Hunter's | Gem (rest) |
| Construction | Resource (rest) |
| Fodder | Hay `(O)178` plus crops with category -75 or -79 whose name is Wheat, Corn, Apple, or any fruit (rest): `ByKind[Other]` filtered to `(O)178` + `Crops` fruit and grain |
| Dye | one item per colour: `color_red`, `color_purple`, `color_yellow`, `color_white`, `color_blue`, `color_green` (one each; Count 1 per part) |
| Field Research | Forage 1, (Artifact or shell forage) 1, Fish 1, (Mineral or geode) 1 |
| Wild Medicine | Forage filtered to `edible_mushroom` tag or category -81 (rest) |
| Chef's | Cooking half (`Count = NumberOfSlots / 2`), then ingredients of the Cooking pool's recipes (rest) |
| Winter Star | WinterOnly (rest) |
| The Missing | Extreme band: any vetted item with `model.For(id).Effort >= 9` (rest) |
| Children's | sweets: Cooking items tagged `food_sweet`, berries (`(O)296`, `(O)410`), dolls (`(O)103`, `(O)126`, `(O)127`) (rest) |
| Enchanter's | vanilla four plus `ByKind[Totem]` plus `ByKind[Essence]` (rest) |
| Fish Farmer's | pond goods: `(O)812` Roe, `(O)447` Aged Roe, `(O)814` Squid Ink, `(O)445` Caviar (rest) |
| Animal | Egg, Milk, AnimalProduct union (rest) |
| Artisan | ArtisanGoods (rest) |
| Adventurer's | MonsterLoot (rest) |
| Forager's | Forage (rest) |
| Gil's Trophies | Trophy (rest) |
| Recycler's | trash `(O)168` to `(O)172` plus `(O)338`, `(O)428` (rest) |
| Book | Book (rest, already limited by plan 1's `BookWeeks`) |

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void An_unnamed_bundle_rolls_from_its_majority_kind()
{
    PoolRecipe r = BundlePoolRecipes.For("Some Gem Bundle", new[] { "(O)72", "(O)64", "(O)80" }, pools, null);
    Assert.Single(r.Parts);
    Assert.Equal("Gem", r.Parts[0].Label);
}

[Fact]
public void Dye_has_one_part_per_colour()
{
    PoolRecipe r = BundlePoolRecipes.For("Dye", new string[0], pools, null);
    Assert.Equal(6, r.Parts.Count);
    Assert.All(r.Parts, p => Assert.Equal(1, p.Count));
}

[Fact]
public void A_vanilla_list_bundle_is_classified_as_a_recipe()
{
    BundleSpec dye = SpecWithSlots("Dye", "(O)420", "(O)397", "(O)421", "(O)444", "(O)62", "(O)266");
    Assert.Equal(PoolDomain.Recipe, PoolDomainClassifier.Classify(dye, pools).Domain);
}

[Fact]
public void Money_bundles_are_never_rolled()
    => Assert.Equal(PoolDomain.None, PoolDomainClassifier.Classify(SpecWithSlots("2,500g", "-1"), pools).Domain);
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`PoolDomainClassifier.Classify`: keep the seasonal and Quality Crops fast paths and the legacy majority claim; when the claim is `None` and the bundle is not a money bundle, return `new DomainMatch(PoolDomain.Recipe, null)`. `BundleSlotFiller.Fill`: for `Recipe`, look up `BundlePoolRecipes.For(spec.Name, vanillaIds, pools, availability)`, then sample part by part (each part's candidates minus `avoid` and minus already chosen, `Count` slots or the rest), concatenating into `chosen`; the pity trim, the stretch swap and the hard-item swap run on the union of all parts' candidates. `Candidates(spec, match, pools)` for `Recipe` returns the union (used by `CandidateCount`). When a part cannot fill its count, log `"'{name}': part {label} short by {n}; falling back to the vanilla items"` and take the bundle's own vanilla ids for the shortfall.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump patch)

---

### Task 6: Engine wiring, `tly_genbundles` on 20 boards, no fixed lists left

**Files:**
- Modify: `src/TheLongestYear/Loop/BundleEngine.cs` (recipes get the availability model; the "vanilla slots" path is gone for every non-money bundle), `src/TheLongestYear/ModEntry.cs` (`tly_genbundles` prints `re-rolled from recipe <name>`)
- Test: build the mod; then, with the game up, `tly_genbundles 0` to `19` and count `vanilla slots` lines in the log: expected 0 outside the Vault

- [ ] **Step 1: Implement** the wiring: where the engine calls `BundleSlotFiller.Fill`, pass `availability`; where it decides "keep vanilla slots" for `PoolDomain.None`, only money bundles reach it now. The determinism self-check in `tly_genbundles` must still pass (log line `determinism OK`).

- [ ] **Step 2: Build the mod, deploy with `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`, then `git checkout -- test-output/log-archive`; load the Rodger save (`tly_loadsave`), run `tly_genbundles 0` to `19` over the bridge; grep the log for `vanilla slots` and `determinism`.

- [ ] **Step 3: Commit** (bump patch) with the counts in the message.

---

## Self-review

Spec section 3: no fixed lists (Tasks 5, 6); kind pools (Task 4); weights (Task 1); additions and vet (Task 2); rewind (Task 3); Easy exclusion (Task 2); hard-item rule and stretch apply through the filler (plan 2). Rulings table: every named bundle has a recipe row in Task 5. Types: `PoolRecipe` and `PoolPart` are defined once in Task 5 and consumed by the filler in the same task; `ItemPools.ByKind` keyed by the extended `ItemKind` from Task 4.
