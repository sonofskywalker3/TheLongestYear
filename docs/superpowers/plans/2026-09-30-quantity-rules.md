# Quantity Rules for Reused Bundles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every item a board can ask for gets a data-backed amount, Mystic Syrup leaves the pools, and Prismatic Shard and Mystery Box are capped at 0/1/2/3 per board by Stack size.

**Architecture:** New hand rows go in one per-season table (`SeasonalAskBasis`, split out of `QuantityBasisTables`). Dishes get a generated per-season table built beside the availability model and read through it. The two capped items reuse the legendary-fish pattern: a per-board allowance spent across bundles in fill order, a ban once spent, and every slot clamped to 1. Two availability dates (Night Market fish, Moss) and the Prismatic Shard geode route are corrected so the new rows read true weeks.

**Tech Stack:** C# (.NET 6), SMAPI mod, xUnit. Build and test: `dotnet test TheLongestYear.sln -c Release` from `TheLongestYear/`.

**Spec:** `docs/superpowers/specs/2026-09-30-quantity-rules-design.md`

## Global Constraints

- Branch: `master` (the release line). Every commit bumps the PATCH of `src/TheLongestYear/manifest.json` `Version` (0.18.101 is current; the first task makes 0.18.102). Push after every commit.
- Commit message trailer (every commit):
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_011e4uL561rmjEX7skpfDT5V
  ```
- No em dashes in any code comment, string, doc or commit message (Jeff's rule).
- Determinism: a board is regenerated and compared byte for byte at save load. Nothing may read `System.Random` without a seed, `string.GetHashCode`, dictionary enumeration order, or the `keep_kitchen` upgrade.
- Files CRLF; edit with the Edit tool, not `sed -i` or `perl -pi` on multi-line patterns.
- Never use `/sdcard/`. Never use Playwright.
- Cap values, verbatim from the spec: Prismatic Shard `(O)74` and Mystery Box `(O)MysteryBox`, at most **0 Easy, 1 Normal, 2 Hard, 3 Extreme** per board each, counted as total items, following the **Stack size** step.

## Review Focus

1. A Helper's bundle on a board whose Mystery Box allowance is spent must not fall back to vanilla's "5 Mystery Box" (the filler returns the spec unchanged when its pool is short). Pinned in Task 6.
2. A capped item a vanilla-kept or banded slot carries (QuantityAskPass or FlavoredSlotPass sets stacks after StackScaling) must still end at 1. Pinned in Task 6 (clamp runs last in Pass 3).
3. A dish whose ingredient is a category (any egg, any milk) or another dish (Tortilla, Bread) must not throw or return 0 in a season it exists. Pinned in Task 8.
4. The Remixed/Normal bundle source (vanilla board path, `VanillaBoardDifficultyPass`) still asks "5 Mystery Box" unless its clamp is added. Pinned in Task 6.
5. Board repair (`BoardRepairService`) can swap a capped item into a live board past the cap. Pinned in Task 6 (repair excludes capped ids, as it already excludes legendaries).

---

### Task 1: Mystic Syrup leaves every pool

**Files:**
- Modify: `src/TheLongestYear.Core/ItemPoolBuilder.cs:730` (`BuiltInExcludedItemIds`)
- Modify: `src/TheLongestYear.Core/AvailabilityWeeks.cs:86-101` (delete the `(O)MysticSyrup` LateFloors row and its comment; keep the doc line about Winter dig-spot forage, drop the Mystic Syrup bullet)
- Test: `tests/TheLongestYear.Tests/ItemPoolBuilderTests.cs`

**Interfaces:** Produces nothing new. `ItemPools.ExcludedIds` contains `(O)MysticSyrup`.

- [ ] **Step 1: Write the failing test** (add to `ItemPoolBuilderTests`; reuse that file's existing builder helper for a minimal `Build` call, following the nearest existing `BuiltInExcludedItemIds` test)

```csharp
[Fact]
public void Mystic_Syrup_is_excluded_from_every_pool()
{
    // Its seed is the Foraging Mastery crafting recipe (all five skills at 10, then 10,000 XP);
    // the syrup only comes from a tapper on that tree (Jeff, 2026-09-30: remove it).
    Assert.Contains("(O)MysticSyrup", ItemPoolBuilder.BuiltInExcludedItemIds);
}
```

- [ ] **Step 2: Run it, expect FAIL.** `dotnet test TheLongestYear.sln -c Release --filter Mystic_Syrup_is_excluded`
- [ ] **Step 3: Implement.** Add to `BuiltInExcludedItemIds`:

```csharp
"(O)MysticSyrup", // Mystic Tree only; its seed is the Foraging Mastery recipe (Jeff, 2026-09-30)
```
Delete the LateFloors row `["(O)MysticSyrup"] = ...` and its comment block. If a test asserts that row, update it to assert the exclusion instead.
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Bump version, commit** `Mystic Syrup excluded from every pool (0.18.102)`, push.

---

### Task 2: One per-season hand table with the gathered goods and the fish no simulation covers

**Files:**
- Create: `src/TheLongestYear.Core/SeasonalAskBasis.cs`
- Modify: `src/TheLongestYear.Core/QuantityBasisTables.cs` (remove `Seasonal`, moved)
- Modify: `src/TheLongestYear.Core/QuantityAskPass.cs` (read `SeasonalAskBasis` instead of `QuantityBasisTables.Seasonal`)
- Test: `tests/TheLongestYear.Tests/SeasonalAskBasisTests.cs` (new); update `StickyBundleTests.cs` only if a moved name breaks it

**Interfaces:**
- Produces: `public static class SeasonalAskBasis` with `public static readonly IReadOnlyDictionary<string, double[]> Rows` (order Spring, Summer, Fall, Winter; 0 = cannot exist yet) and `public static double? BasisByDeadline(string itemId, Season? deadline)` (best nonzero season from Spring up to the deadline, Winter when null; null when no row or all zero so far).
- `QuantityAskPass.Covers` and `QuantityAskPass.BasisByDeadline` consult `SeasonalAskBasis` (replacing the private `SeasonalBasis` helper).

- [ ] **Step 1: Write the failing tests**

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Hand rows from the 2026-09-30 quantity spec: gathered goods, pantry staples, Prize
/// Tickets, and the fish tools/fish-sim cannot simulate (mines, Night Market, bobber rows).</summary>
public class SeasonalAskBasisTests
{
    [Theory]
    [InlineData("(O)309", Season.Spring, 20.0)]   // Acorn
    [InlineData("(O)310", Season.Fall, 20.0)]     // Maple Seed: best so far, Summer 20 beats Fall 17
    [InlineData("(O)Moss", Season.Spring, 5.0)]
    [InlineData("(O)Moss", Season.Summer, 99.0)]  // green rain
    [InlineData("(O)635", Season.Summer, 14.0)]   // Orange
    [InlineData("(O)613", Season.Fall, 14.0)]     // Apple
    [InlineData("(O)815", Season.Spring, 7.0)]    // Tea Leaves: Caroline's sunroom bush
    [InlineData("(O)815", Season.Summer, 17.0)]
    [InlineData("(O)78", Season.Spring, 15.0)]    // Cave Carrot
    [InlineData("(O)399", Season.Winter, 35.0)]   // Spring Onion keeps
    [InlineData("(O)296", Season.Spring, 80.0)]   // Salmonberry
    [InlineData("(O)178", Season.Spring, 60.0)]   // Hay
    [InlineData("(O)168", Season.Summer, 8.0)]    // Trash
    [InlineData("(O)246", Season.Spring, 20.0)]   // Wheat Flour
    [InlineData("(O)419", Season.Summer, 20.0)]   // Vinegar
    [InlineData("(O)PrizeTicket", Season.Fall, 3.0)]
    [InlineData("(O)Goby", Season.Winter, 21.0)]
    [InlineData("(O)158", Season.Spring, 10.5)]   // Stonefish, floor 20
    [InlineData("(O)161", Season.Spring, 9.0)]    // Ice Pip, floor 60
    [InlineData("(O)162", Season.Spring, 7.6)]    // Lava Eel, floor 100
    [InlineData("(O)796", Season.Summer, 36.5)]   // Slimejack
    [InlineData("(O)798", Season.Winter, 5.0)]    // Midnight Squid, fishing hours only
    [InlineData("(O)799", Season.Winter, 4.0)]    // Spook Fish
    [InlineData("(O)800", Season.Winter, 2.0)]    // Blobfish
    public void Rows_read_the_best_season_up_to_the_deadline(string id, Season deadline, double expected)
        => Assert.Equal(expected, SeasonalAskBasis.BasisByDeadline(id, deadline));

    [Theory]
    [InlineData("(O)635", Season.Spring)]   // Orange: no fruit before Summer
    [InlineData("(O)613", Season.Summer)]   // Apple: Fall fruit
    [InlineData("(O)798", Season.Fall)]     // Night Market is Winter 15-17
    public void A_season_before_the_item_exists_has_no_basis(string id, Season deadline)
        => Assert.Null(SeasonalAskBasis.BasisByDeadline(id, deadline));

    [Theory]
    [InlineData("(O)634")]   // Apricot: single on purpose (Spring fruit, year-two tree)
    [InlineData("(O)638")]   // Cherry
    [InlineData("(O)MysticSyrup")]
    public void Single_on_purpose_items_have_no_row(string id)
        => Assert.False(SeasonalAskBasis.Rows.ContainsKey(id));

    [Fact]
    public void Every_row_has_four_seasons_and_no_negative_values()
        => Assert.All(SeasonalAskBasis.Rows, kv =>
        {
            Assert.Equal(4, kv.Value.Length);
            Assert.All(kv.Value, v => Assert.True(v >= 0, kv.Key));
        });

    [Fact]
    public void The_pass_reads_the_new_rows()
    {
        Assert.True(QuantityAskPass.Covers("(O)309"));
        Assert.Equal(21.0, QuantityAskPass.BasisByDeadline("(O)Goby", Season.Spring));
    }
}
```

- [ ] **Step 2: Run, expect FAIL** (`SeasonalAskBasis` does not exist).
- [ ] **Step 3: Implement.** Create `SeasonalAskBasis.cs`. Move the five existing rows (Sugar, Ice Cream, Maple Bar, Cranberry Sauce, Miner's Treat) with their comments verbatim from `QuantityBasisTables.Seasonal`, then add, each with a one-line comment carrying its arithmetic from the spec's section 3 and 4 tables:

```csharp
// Tree seeds: 8 trees chopped a week (75% seed, 1-2 each) plus shaking about 30 trees (5%).
["(O)309"] = new double[] { 20, 20, 20, 20 },   // Acorn
["(O)310"] = new double[] { 20, 20, 17, 20 },   // Maple Seed: from Fall 14 a shake drops a Hazelnut
["(O)311"] = new double[] { 20, 20, 20, 20 },   // Pine Cone
// Trees restart young each loop and moss needs growth stage 14; green rain (one Summer day) floods it.
["(O)Moss"] = new double[] { 5, 99, 45, 45 },
// Fruit: 2 trees a species, one fruit a day in season, 28 days to mature.
["(O)635"] = new double[] { 0, 14, 14, 14 },    // Orange
["(O)636"] = new double[] { 0, 14, 14, 14 },    // Peach
["(O)613"] = new double[] { 0, 0, 14, 14 },     // Apple
["(O)637"] = new double[] { 0, 0, 14, 14 },     // Pomegranate
// Spring: Caroline's sunroom tea bush (2 hearts), a leaf a day on days 22-28. Summer on: 10 own bushes.
["(O)815"] = new double[] { 7, 17, 17, 17 },    // Tea Leaves
["(O)78"] = new double[] { 15, 15, 15, 15 },    // Cave Carrot: mine barrels, about 30 a mine day, 4 days
["(O)399"] = new double[] { 35, 0, 0, 0 },      // Spring Onion: Spring forage, south-east Forest
["(O)296"] = new double[] { 80, 0, 0, 0 },      // Salmonberry: Spring 15-18, 20-25 bushes a day
["(O)178"] = new double[] { 60, 99, 99, 99 },   // Hay: Marnie 50g; Spring money-limited
// Crab-pot junk (10 pots, about 26 a week) split five ways, plus fishing junk. Pots need Fishing 3.
["(O)168"] = new double[] { 6, 8, 8, 8 },       // Trash
["(O)169"] = new double[] { 6, 8, 8, 8 },       // Driftwood
["(O)170"] = new double[] { 6, 8, 8, 8 },       // Broken Glasses
["(O)171"] = new double[] { 6, 8, 8, 8 },       // Broken CD
["(O)172"] = new double[] { 6, 8, 8, 8 },       // Soggy Newspaper
["(O)246"] = new double[] { 20, 40, 40, 40 },   // Wheat Flour: Pierre 100g, like Sugar
["(O)247"] = new double[] { 10, 20, 20, 20 },   // Oil: Pierre 200g
["(O)419"] = new double[] { 10, 20, 20, 20 },   // Vinegar: Pierre 200g
["(O)423"] = new double[] { 10, 20, 20, 20 },   // Rice: Pierre 200g
["(O)PrizeTicket"] = new double[] { 2, 2, 3, 3 },  // every 3rd Help Wanted; town Special Orders from Fall
// Fish tools/fish-sim cannot simulate. Fishing 10, bait, 2 catches a game hour, best 10 hours x 7.
["(O)Goby"] = new double[] { 21, 21, 21, 21 },  // Forest waterfall pool, 0.15 a cast, any season
["(O)158"] = new double[] { 10.5, 10.5, 10.5, 10.5 },  // Stonefish, floor 20, 0.075 a cast
["(O)161"] = new double[] { 9, 9, 9, 9 },       // Ice Pip, floor 60, 0.0645 a cast
["(O)162"] = new double[] { 7.6, 7.6, 7.6, 7.6 },  // Lava Eel, floor 100, 0.054 a cast
["(O)796"] = new double[] { 36.5, 36.5, 36.5, 36.5 },  // Slimejack, Mutant Bug Lair, 0.26 a cast
// Night Market (Jeff, 2026-09-30: "only open for a few hours at night"): one 1,000g submarine ride a
// night, about 4 game hours of fishing, 3 nights = 24 casts.
["(O)798"] = new double[] { 0, 0, 0, 5 },       // Midnight Squid, 0.207 a cast
["(O)799"] = new double[] { 0, 0, 0, 4 },       // Spook Fish, 0.162 a cast
["(O)800"] = new double[] { 0, 0, 0, 2 },       // Blobfish, 0.10 a cast
```
Class doc: the per-season rule, "0 = cannot exist yet", and a pointer to the spec. Move the season-walk into `SeasonalAskBasis.BasisByDeadline` (index `(int)season`, Season.Spring = 0). In `QuantityAskPass`, replace `QuantityBasisTables.Seasonal.ContainsKey(id)` with `SeasonalAskBasis.Rows.ContainsKey(id)` and the private `SeasonalBasis` call with `SeasonalAskBasis.BasisByDeadline(id, deadline)`; delete the private helper.
- [ ] **Step 4: Run the whole suite, expect PASS.** Some existing tests may assert an item was NOT covered (e.g. a forage or fish that now has a row); update each such assertion to the new row and say so in the commit message.
- [ ] **Step 5: Bump, commit** `Seasonal ask rows: tree seeds, fruit, forage, trash, pantry, Prize Tickets, unsimulated fish (0.18.103)`, push.

---

### Task 3: Night Market fish and Moss get their true weeks

**Files:**
- Modify: `src/TheLongestYear.Core/AvailabilityWeeks.cs` (add `NightMarketFishWeeks`; Moss row in `OtherPlacements`)
- Modify: `src/TheLongestYear.Core/Availability/FishAvailability.cs:36-43` (apply the new floor like `MineFishWeeks`)
- Test: `tests/TheLongestYear.Tests/FishAvailabilityTests.cs` (or the file holding `FishAvailability.Derive` tests; find with `grep -rln "FishAvailability.Derive" tests`)

**Interfaces:** Produces `AvailabilityWeeks.NightMarketFishWeeks : IReadOnlyDictionary<string, int>` (`(O)798`, `(O)799`, `(O)800` -> 15).

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData("(O)798")]
[InlineData("(O)799")]
[InlineData("(O)800")]
public void Night_Market_fish_wait_for_Winter_15(string id)
{
    var item = new PoolItem(id, 50, 3, new[] { Season.Winter }, Array.Empty<string>());
    ItemAvailability a = FishAvailability.Derive(item, null);
    Assert.Equal(15, a.EarliestWeek);   // Winter 1 is week 13; the market opens Winter 15
    Assert.Equal(15, a.HardWeek);
}

[Fact]
public void Moss_waits_for_summer()
    => Assert.Equal(6, AvailabilityWeeks.OtherPlacements["(O)Moss"].Week);
```
(Match the `ItemAvailability` property names the file already uses; `EarliestWeek`/`HardWeek` per `ItemAvailability.cs:17-33`.)
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement.** In `AvailabilityWeeks`:

```csharp
/// <summary>The Night Market submarine fish. The pool files them under Winter through the
/// festival mapping, which reads as Winter 1 (week 13); the market opens Winter 15.</summary>
public static readonly IReadOnlyDictionary<string, int> NightMarketFishWeeks =
    new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["(O)798"] = 15, ["(O)799"] = 15, ["(O)800"] = 15,
    };
```
Change the Moss row to `(6, "Moss, trees restart young each loop; Summer's green rain is the first real crop")`. In `FishAvailability.Derive`, after the `MineFishWeeks` block:

```csharp
if (AvailabilityWeeks.NightMarketFishWeeks.TryGetValue(item.ItemId, out int market))
{
    week = Math.Max(week, market);
    hardWeek = Math.Max(hardWeek, market);
    floor = AvailabilityWeeks.SeasonOf(week);
}
```
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Bump, commit** `Night Market fish from Winter 15, Moss from Summer (0.18.104)`, push.

---

### Task 4: Prismatic Shard's geode route reads true odds

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/GeodeAvailability.cs`
- Test: `tests/TheLongestYear.Tests/GeodeAvailabilityTests.cs` (find with `grep -rln "GeodeAvailability" tests`)

**Interfaces:** `GeodeAvailability.Derive(string, IReadOnlyList<RawGeodeDrop>)` unchanged in signature. New constants `GeodeTableShare = 0.5` and `NegligibleChance = 0.01`.

Background: `Utility.getTreasureFromGeode` reads a geode's GeodeDrops list on only half of cracks when the geode also has default items (`GeodeDropsDefaultItems`), and the Omni Geode's Prismatic Shard row needs 16 geodes cracked. At 0.008 x 0.5 = 0.4% a crack it is not a route a year can plan on, yet `ChanceStep` treats it like any "rare" 2%-5% drop and places the shard at Spring week 2.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void A_drop_under_one_percent_after_the_table_split_is_not_a_route()
{
    var drops = new List<RawGeodeDrop>
    {
        new("(O)749", "(O)74", 0.008),   // Omni Geode -> Prismatic Shard, halved to 0.4%
    };
    Assert.Null(GeodeAvailability.Derive("(O)74", drops));
}

[Fact]
public void A_listed_drop_still_counts_after_the_split()
{
    var drops = new List<RawGeodeDrop> { new("(O)535", "(O)86", 0.3) };   // Geode -> Earth Crystal
    Assert.NotNull(GeodeAvailability.Derive("(O)86", drops));
}
```
- [ ] **Step 2: Run, expect FAIL** (first test returns a week-2 route).
- [ ] **Step 3: Implement.** In `Derive`, before `ChanceStep`:

```csharp
// Data GeodeDrops rows are read on half of cracks for these four geodes (they all carry
// GeodeDropsDefaultItems); the code-only default-table rows already carry that half in their
// chance (DefaultTableDrops). A drop under 1% after the split is not a route a year can plan
// on: Prismatic Shard from an Omni Geode is 0.4% a crack and needs 16 geodes cracked first.
double chance = IsDefaultTableRow(drop) ? drop.Chance : drop.Chance * GeodeTableShare;
if (chance < NegligibleChance)
    continue;
```
`IsDefaultTableRow(drop)` is true when the drop's item id is in `DefaultTable[drop.GeodeItemId]` AND its chance equals `DefaultTableShare / ids.Length` (the value `DefaultTableDrops` produces); use `chance` everywhere `drop.Chance` was used below it.
- [ ] **Step 4: Run the whole suite.** Some geode tests may shift a chance step; update them only where the halving is the reason, and list each in the commit message.
- [ ] **Step 5: In-game check (headless, `docs/HEADLESS_DRIVING.md`):** `tools/deploy.ps1 -Minimized`, load `None_450288305` with `tly_loadsave`, run `tly_itemmodel (O)74`. Expect a non-geode basis (mine-bottom node or monster), week 9 or later, effort 6 or more. Also run `tly_itemmodel` for every item `GeodeAvailability` used to place (the Omni Geode's GeodeDrops ids) and list any whose week moved in the report. Close with `close-smapi.ps1`; `git checkout -- test-output/log-archive`.
- [ ] **Step 6: Bump, commit** `Geode routes: table rows read on half of cracks, drops under 1% are not a route; Prismatic Shard no longer week 2 (0.18.105)`, push.

---

### Task 5: CappedAsks core rule

**Files:**
- Create: `src/TheLongestYear.Core/CappedAsks.cs`
- Test: `tests/TheLongestYear.Tests/CappedAsksTests.cs`

**Interfaces (produced):**

```csharp
public static class CappedAsks
{
    public const string PrismaticShard = "(O)74";
    public const string MysteryBox = "(O)MysteryBox";
    public static readonly IReadOnlySet<string> Ids;                 // the two above
    public static bool IsCapped(string? itemId);
    public static int BoardAllowance(DifficultyStep stackSize);     // Easy 0, Normal 1, Hard 2, Extreme 3
    public static int ClampStack(string? itemId, int stack);         // 1 for a capped id, stack otherwise
    public static BundleSpec ClampBundle(BundleSpec spec);           // every capped slot to stack 1; same reference when unchanged
    public static int CountOnBoard(string itemId, IEnumerable<BundleSpec> board); // sum of stacks of that id
    public static void Enforce(List<PoolItem> chosen, IReadOnlyList<PoolItem> candidates,
        IReadOnlyDictionary<string, int> remaining, Random rng, Action<string>? log = null, string? bundleName = null);
        // keeps at most remaining[id] of each capped id (rolled order), swaps each surplus for a
        // weighted pick from candidates that is neither capped nor already chosen, drops it if none
}
```

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CappedAsksTests
{
    private static PoolItem Item(string id) => new(id, 50, 3, Array.Empty<Season>(), Array.Empty<string>());

    [Theory]
    [InlineData(DifficultyStep.Easy, 0)]
    [InlineData(DifficultyStep.Normal, 1)]
    [InlineData(DifficultyStep.Hard, 2)]
    [InlineData(DifficultyStep.Extreme, 3)]
    public void Allowance_follows_stack_size(DifficultyStep step, int expected)
        => Assert.Equal(expected, CappedAsks.BoardAllowance(step));

    [Fact]
    public void A_capped_slot_asks_for_one()
    {
        Assert.Equal(1, CappedAsks.ClampStack("(O)MysteryBox", 5));
        Assert.Equal(1, CappedAsks.ClampStack("74", 3));   // bare id normalises
        Assert.Equal(7, CappedAsks.ClampStack("(O)60", 7));
    }

    [Fact]
    public void ClampBundle_lowers_only_capped_slots()
    {
        var spec = new BundleSpec("Bulletin Board", 3, "Helper's", "Helper's", "O 1 1", 0, 2,
            new List<BundleSlotSpec> { new("(O)PrizeTicket", 1, 0), new("(O)MysteryBox", 5, 0) });
        BundleSpec clamped = CappedAsks.ClampBundle(spec);
        Assert.Equal(new[] { 1, 1 }, clamped.Slots.Select(s => s.Stack));
        Assert.Same(clamped, CappedAsks.ClampBundle(clamped));
    }

    [Fact]
    public void Enforce_keeps_what_the_board_has_left_and_swaps_the_rest()
    {
        var chosen = new List<PoolItem> { Item("(O)74"), Item("(O)60"), Item("(O)MysteryBox") };
        var candidates = new List<PoolItem> { Item("(O)74"), Item("(O)60"), Item("(O)62"), Item("(O)MysteryBox"), Item("(O)64") };
        var remaining = new Dictionary<string, int> { ["(O)74"] = 1, ["(O)MysteryBox"] = 0 };
        CappedAsks.Enforce(chosen, candidates, remaining, new Random(1));
        Assert.Equal(3, chosen.Count);
        Assert.Contains(chosen, c => c.ItemId == "(O)74");
        Assert.DoesNotContain(chosen, c => c.ItemId == "(O)MysteryBox");
    }

    [Fact]
    public void CountOnBoard_sums_stacks()
    {
        var a = new BundleSpec("R", 1, "A", "A", "O 1 1", 0, 1, new List<BundleSlotSpec> { new("(O)74", 1, 0) });
        var b = new BundleSpec("R", 2, "B", "B", "O 1 1", 0, 1, new List<BundleSlotSpec> { new("(O)74", 2, 0) });
        Assert.Equal(3, CappedAsks.CountOnBoard("(O)74", new[] { a, b }));
    }
}
```
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement** `CappedAsks.cs` to the interface above. Model `Enforce` on `LegendaryFishRules.Enforce` (walk in roll order, keep while under `remaining[id]`, swap via `WeightedSampler.Sample(replacements, 1, rng)[0]`, drop when no replacement). Class doc cites Jeff, 2026-09-30, and the spec. Named constants for the four allowances.
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Bump, commit** `CappedAsks: Prismatic Shard and Mystery Box allowance, clamp and enforce (0.18.106)`, push.

---

### Task 6: Wire the cap through every path that writes a board

**Files:**
- Modify: `src/TheLongestYear.Core/BundleSlotFiller.cs` (`Fill`: new optional parameter `IReadOnlyDictionary<string, int>? cappedBudget = null`; call `CappedAsks.Enforce` right after `LegendaryFishRules.Enforce`; `ReplacementFor`: exclude capped ids next to the legendary exclusion at ~line 287)
- Modify: `src/TheLongestYear/Loop/BundleEngine.cs` (allowance, Helper's handling, Pass 2 budget, authored ban, Pass 3 clamp and guard)
- Modify: `src/TheLongestYear.Core/VanillaBoardDifficultyPass.cs:105` (wrap the stack in `CappedAsks.ClampStack`)
- Modify: `src/TheLongestYear/Loop/BoardRepairService.cs:175` (wrap in `CappedAsks.ClampStack`)
- Test: `tests/TheLongestYear.Tests/BundleSlotFillerTests.cs`, `tests/TheLongestYear.Tests/VanillaBoardDifficultyPassTests.cs` (find with grep)

**Interfaces:**
- Consumes: `CappedAsks` (Task 5). `DifficultyProfile.Steps.StackSize` (verify `DifficultyResolver.Resolve` copies the settings into `Steps`; if it does not, add `Steps = settings` there with a test).
- Produces: `BundleSlotFiller.Fill(..., int legendaryBudget = int.MaxValue, IReadOnlyDictionary<string, int>? cappedBudget = null)`.

Engine behaviour, in order:
1. After the legendary allowance (BundleEngine.cs ~223): `int cappedAllowance = CappedAsks.BoardAllowance(_difficulty.Steps.StackSize);` Log it at Trace.
2. `WidenWithAuthoredBundles`: when `cappedAllowance == 0`, add `CappedAsks.Ids` to `bannedIds` (next to the legendary ban at ~558).
3. Pass 1, before `RemixSelector.PickForRoom`: when `cappedAllowance == 0`, remove any candidate named `Helper's` from each position list that has at least one other candidate. When Helper's IS picked, record a reservation of 1 Mystery Box: the Pass 2 budget for Mystery Box starts at `cappedAllowance - 1` for every other bundle, and Helper's itself is filled with `cappedBudget[MysteryBox] = 1`.
4. Pass 2: before each `Fill`, compute `remaining[id] = allowance - CappedAsks.CountOnBoard(id, composed picks so far) - reservation (for Mystery Box, when Helper's is picked and not yet filled)`, add ids with `remaining <= 0` to `banned`, pass `remaining` as `cappedBudget`.
5. Pass 3: after `FlavoredSlotPass.Apply`, `composed = CappedAsks.ClampBundle(composed);` then, after the loop, count each capped id over `allPicks` and log at Error if the count exceeds `cappedAllowance` (a guard; the fill rules enforce it).

- [ ] **Step 1: Write the failing filler tests** (in `BundleSlotFillerTests`, reusing its `Item`, `Spec`, `Tuning` helpers)

```csharp
[Fact]
public void Fill_never_passes_the_capped_budget()
{
    var pools = new ItemPools
    {
        ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
        {
            [ItemKind.Gem] = new[] { Item("(O)74", weight: 1000), Item("(O)60"), Item("(O)62"), Item("(O)64"), Item("(O)66") },
        },
    };
    var spec = Spec("Treasure Hunter's", 4);
    var budget = new Dictionary<string, int> { ["(O)74"] = 0, ["(O)MysteryBox"] = 0 };
    for (int seed = 0; seed < 50; seed++)
    {
        BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Gems, null), pools, Tuning,
            new Random(seed), cappedBudget: budget);
        Assert.DoesNotContain(filled.Slots, s => s.ItemId == "(O)74");
    }
}

[Fact]
public void Replacement_draws_never_offer_a_capped_item()
{
    // Build as the file's existing ReplacementFor tests do (BundleSlotFillerReplacementTests):
    // a Gem bundle whose pool holds only (O)74 besides the slot being replaced must return null.
}
```
(Use the file's actual domain enum for gems; `grep -n "PoolDomain\." src/TheLongestYear.Core/ItemPoolModel.cs`. Write the second test fully in `BundleSlotFillerReplacementTests.cs` following its first test's setup, asserting `Assert.Null(pick)` when the only other candidate is `(O)74`.)

And in the vanilla-pass test file:

```csharp
[Fact]
public void The_vanilla_board_asks_for_one_Mystery_Box()
{
    // Follow the file's existing fixture for a Remixed board; give Helper's "5 Mystery Box" and
    // assert the pass writes stack 1 for (O)MysteryBox and leaves the Prize Ticket alone.
}
```
(Write it out against the file's real fixture helper; the assertion is `Assert.Equal(1, stackOf("(O)MysteryBox"))`.)
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement** the filler parameter, the Enforce call, the repair exclusion, the two `ClampStack` wraps, and engine steps 1-5 above. Keep every new rng draw on an existing per-pick stream or a new salted one (`seed ^ CappedSalt`, a new constant); never `new Random()`.
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: In-game check (headless):** deploy minimized, load the Rodger save, `tly_genbundles 1` to `tly_genbundles 20` on Normal. Grep the log: no `(O)MysticSyrup`; `Prismatic Shard` and `Mystery Box` at most 1 each per board, each x1; no Helper's asking x5. Close the game.
- [ ] **Step 6: Bump, commit** `Prismatic Shard and Mystery Box: 0/1/2/3 per board by Stack size, one each, every board path (0.18.107)`, push.

---

### Task 7: Cooking recipes keep their ingredient counts

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/EffortData.cs:32-33` (`RawCookingRecipe`)
- Modify: `src/TheLongestYear/Loop/GameEffortData.cs:173-185`
- Test: `tests/TheLongestYear.Tests/` file that builds `RawCookingRecipe` fixtures (grep `new RawCookingRecipe(`)

**Interfaces (produced):**

```csharp
public sealed record RawCookingRecipe(
    string Name, IReadOnlyList<string> IngredientIds, string OutputItemId, string UnlockCondition,
    IReadOnlyList<int>? IngredientCounts = null)
{
    /// <summary>How many of ingredient i the recipe takes; 1 when the data gave no count.</summary>
    public int CountOf(int i) => IngredientCounts != null && i < IngredientCounts.Count ? Math.Max(1, IngredientCounts[i]) : 1;
}
```

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void A_recipe_reports_its_ingredient_counts()
{
    var plum = new RawCookingRecipe("Plum Pudding", new[] { "(O)406", "(O)246", "(O)245" }, "(O)604", "l 13",
        new[] { 2, 1, 1 });
    Assert.Equal(2, plum.CountOf(0));
    Assert.Equal(1, plum.CountOf(2));
    Assert.Equal(1, new RawCookingRecipe("X", new[] { "(O)1" }, "(O)2", "default").CountOf(0));
}
```
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement** the record change and, in the glue loop, collect `counts.Add(int.TryParse(ingredientPairs[i + 1], out int c) ? c : 1)` beside each id and pass `counts`.
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Bump, commit** `Cooking recipes keep ingredient counts (0.18.108)`, push.

---

### Task 8: Generated dish amounts

**Files:**
- Create: `src/TheLongestYear.Core/Availability/DishAskBasis.cs`
- Modify: `src/TheLongestYear.Core/ItemAvailability.cs` (`ItemAvailabilityModel`: hold an optional `IReadOnlyDictionary<string, double[]> DishBases`, default empty)
- Modify: `src/TheLongestYear.Core/Availability/ItemAvailabilityBuilder.cs` (build it when `effortData` is given)
- Modify: `src/TheLongestYear.Core/Availability/CookedDishAvailability.cs` (expose `public static int EffortWithoutKitchen(RawCookingRecipe recipe, EffortData data, Func<string, int?> effortOf)`: hardest ingredient + unlock effort, the same arithmetic `Derive` uses minus the kitchen term)
- Modify: `src/TheLongestYear.Core/QuantityAskPass.cs` (`Apply`, `Covers`, `BasisByDeadline` take an optional `ItemAvailabilityModel? model`; hand rows first, then `model.DishBases`)
- Modify: `src/TheLongestYear/Loop/BundleEngine.cs:385` and `src/TheLongestYear.Core/FlavoredSlotPass.cs:69` (pass the model)
- Modify: `tests/TheLongestYear.Tests/QuantityBasisTablesTests.cs:160` (Fried Egg is covered once a model with dish bases is passed; without a model it stays uncovered)
- Test: `tests/TheLongestYear.Tests/DishAskBasisTests.cs`

**Interfaces (produced):**

```csharp
public static class DishAskBasis
{
    public const double IngredientShare = 0.25;
    public const int CapEasyDish = 12, CapMidDish = 8, CapHardDish = 4;   // effort (no kitchen) <=3, 4-6, 7+
    public const double ShopBudget = 6000;                               // gold; basis = budget / price
    public const int ShopCap = 25;
    /// <summary>Year-round shop dishes and their shelf price (Data/Shops, price x2 markup).</summary>
    public static readonly IReadOnlyDictionary<string, int> ShopDishPrices;   // Salad 220, Bread 120, Spaghetti 240, Pizza 600, Trout Soup 250
    public static IReadOnlyDictionary<string, double[]> Build(
        EffortData data, Func<string, ItemAvailability?> availabilityOf,
        Func<string, Season, double?> ingredientBasis, Func<string, int?> effortOf);
}
```
Ids: Salad `(O)196`, Bread `(O)216`, Spaghetti `(O)224`, Pizza `(O)206`, Trout Soup `(O)219` (verify each against `patch export/Data_Objects.json` before writing).

Rule for dish d and season s (index 0-3):
- `gate`: the season of `availabilityOf(d).EarliestWeek`; `s < gate` gives 0. An unplaced dish gets no row.
- `cooked`: for each ingredient i, `b = basis(i, s) / recipe.CountOf(i)`, where `basis` is: a category ref (negative id) takes the max `ingredientBasis` over `data.Objects` in that category; a dish ingredient takes its own computed row (memoised, cycle-guarded to 0); anything else `ingredientBasis(id, s)`. A missing basis counts as 1. `cooked = max(1, round(min(b) * IngredientShare))`, then capped by `EffortWithoutKitchen`: <=3 to 12, 4-6 to 8, 7+ to 4.
- `shop`: `min(ShopCap, ShopBudget / price)` when the dish is in `ShopDishPrices`, all four seasons.
- `row[s] = max(cooked, shop)` for `s >= gate`.
- `SeasonalAskBasis` hand rows beat this table (QuantityAskPass checks them first and only falls back to `model.DishBases` when there is no hand row).

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class DishAskBasisTests
{
    // Fixture: Fried Egg (1 egg, default, week 6), Plum Pudding (2 Wild Plum + Flour + Sugar,
    // week 13), Salad (shop 220g, week 1), Tortilla dish used by Fish Taco (dish ingredient).
    private static EffortData Data() => /* build with the file's EffortData fixture helper, the four recipes above,
        and Data/Objects rows for (O)176 Egg category -5, (O)406 Wild Plum, (O)246, (O)245, (O)270 Corn,
        (O)130 Tuna, (O)229 Tortilla */;

    private static ItemAvailability At(int week) => new(AvailabilityWeeks.SeasonOf(week), 3, "t", EarliestWeek: week, HardWeek: week);

    private static IReadOnlyDictionary<string, double[]> Build()
        => DishAskBasis.Build(Data(),
            id => id switch { "(O)194" => At(6), "(O)604" => At(13), "(O)196" => At(1), "(O)213" => At(8), "(O)229" => At(6), _ => null },
            (id, s) => id switch { "(O)176" => 28, "(O)406" => 40, "(O)246" => 40, "(O)245" => 40, "(O)270" => 99, "(O)130" => 16.9, _ => null },
            _ => 2);

    [Fact] public void Nothing_before_the_dish_exists() => Assert.Equal(0, Build()["(O)194"][0]);   // Fried Egg in Spring
    [Fact] public void Scarcest_ingredient_times_share() => Assert.Equal(7, Build()["(O)194"][1]);   // Egg 28 x 0.25
    [Fact] public void Counts_divide() => Assert.Equal(5, Build()["(O)604"][3]);                    // Wild Plum 40 / 2 x 0.25
    [Fact] public void Shop_dishes_use_the_price() => Assert.Equal(25, Build()["(O)196"][0]);       // 6000/220 = 27, cap 25
    [Fact] public void A_dish_ingredient_uses_its_own_row() => Assert.True(Build()["(O)213"][1] >= 1);
    [Fact] public void Unplaced_dish_has_no_row() => Assert.False(Build().ContainsKey("(O)999"));

    [Fact]
    public void Hand_rows_beat_the_generated_table()
    {
        // Ice Cream hand row 0/25/25/25 must win over any generated value.
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>())
            { DishBases = new Dictionary<string, double[]> { ["(O)233"] = new double[] { 9, 9, 9, 9 } } };
        Assert.Null(QuantityAskPass.BasisByDeadline("(O)233", Season.Spring, model));
        Assert.Equal(25, QuantityAskPass.BasisByDeadline("(O)233", Season.Summer, model));
    }
}
```
(Replace the `Data()` comment with real construction using whatever helper existing `CookedDishAvailability` tests use; grep `CookedDishAvailability.Derive` in tests. The expected values above are the contract.)
- [ ] **Step 2: Run, expect FAIL.**
- [ ] **Step 3: Implement** `DishAskBasis.Build`, `EffortWithoutKitchen`, the model property, the builder call (ingredient basis = `(id, s) => QuantityAskPass.BasisByDeadline(id, s)` without a model, to avoid recursion; dishes-as-ingredients are handled inside `Build`), and the `QuantityAskPass` / `FlavoredSlotPass` / `BundleEngine` wiring. The effort cap must use `EffortWithoutKitchen`, never `model.For(d).Effort` (the kitchen point moves with `keep_kitchen` and would change a stored board).
- [ ] **Step 4: Run the whole suite, expect PASS.**
- [ ] **Step 5: Bump, commit** `Dish amounts from ingredients, shop prices and effort (0.18.109)`, push.

---

### Task 9: Verify on real boards, write it up

**Files:**
- Modify: `CHANGELOG.md` (the `## Unreleased` section), `TODO.md` (the Sticky/quantity item), `docs/superpowers/notes/2026-09-30-quantity-audit.md` (append the after-numbers)
- No code changes unless verification finds a defect (fix it in its owning task's files, with a test, and its own commit).

- [ ] **Step 1: Deploy and load (headless).** `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`, wait for the bridge line, `tly_loadsave None_450288305`, wait for `Run \d+ ready`, wait 40 s.
- [ ] **Step 2: For each Stack size step (Easy, Normal, Hard, Extreme):** set it through the config the save reads (see `docs/HEADLESS_DRIVING.md`; the Difficulty settings live in the save's MetaState / GMCM; use the existing `tly_*` difficulty command if one exists: `grep -n "ConsoleCommands.Add(\"tly_" src/TheLongestYear/ModEntry.cs`). Run `tly_genbundles 1` to `tly_genbundles 60`, wait until 60 `generated for loop` lines, and copy the log out per step.
- [ ] **Step 3: Re-run the audit.** Temporary xUnit fact (as on 2026-09-30, deleted after): parse each log's `slots:` lines, list every slot where `QuantityAskPass.Covers(id, model: null)` is false and the id is not a dish, an artifact, a book, a trophy, a sapling, a gem, a legendary, Apricot, Cherry or Jack-O-Lantern. Expected: none. Also per board: `(O)MysticSyrup` count 0; `(O)74` and `(O)MysteryBox` totals at most 0/1/2/3 for the step; no Helper's slot above x1.
- [ ] **Step 4: `tly_itemmodel`** for `(O)74`, `(O)Moss`, `(O)798`, `(O)799`, `(O)800`: weeks 9+, 6, 15, 15, 15.
- [ ] **Step 5: Close the game** (`close-smapi.ps1`), `git checkout -- test-output/log-archive`.
- [ ] **Step 6: CHANGELOG** entry under Unreleased, player words, no file names: amounts follow what a year can produce (tree seeds, fruit, forage, trash, pantry goods, dishes, the rare fish); Mystic Syrup no longer asked; Prismatic Shard and Mystery Box at most one per board on Normal, two on Hard, three on Extreme, none on Easy, one at a time; Night Market fish no earlier than Winter 15; Moss from Summer. Credit elaineofshalott (the Acorn report started it).
- [ ] **Step 7: TODO and audit note** updated with the after-numbers. Bump, commit `Quantity rules: verification on 240 boards, changelog (0.18.110)`, push.

---

## Self-review notes

- Spec sections 1-6 map to Tasks 1, 5+6, 2, 2, 7+8, 3; the Prismatic geode fix is Task 4; testing is Task 9.
- `Covers`/`BasisByDeadline` gain an optional `model` parameter in Task 8 only; Tasks 2-6 call the existing signatures.
- `CappedAsks.BoardAllowance` takes the Stack size step; Task 6 reads it from `DifficultyProfile.Steps.StackSize` and verifies the resolver fills it.
