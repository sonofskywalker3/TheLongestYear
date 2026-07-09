# Slot-Based Weekly Theme Checklist Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Weekly theme goals point at specific still-open CC bundle slots (id+stack+quality+bundle) and tick only when that exact slot completes.

**Architecture:** Pure Core additions (`BonusSlot` POCO, `SlotPoolBuilder`, `BonusSlotSampler`) feed a mod-side rewiring of RunController (sampling/persist/migration/empty-pool lift), WeeklyThemeQuestService (render + live-state tick), DonationObserver/DonationService (slot identity for the 1.5× bonus), and WeeklyHubMenu (per-slot icons + banking tip). Spec: `docs/superpowers/specs/2026-07-09-slot-based-weekly-theme-checklist-design.md`.

**Tech Stack:** C# / .NET 6, SMAPI mod + pure Core assembly, xUnit.

**Version bumps:** bump `src/TheLongestYear/manifest.json` `"Version"` by PATCH in every commit (starting 0.11.12 — adjust if master moved).

**Working directory:** `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`

---

### Task 1: Core `BonusSlot` + RunState slot list (and retire `DonatedThisWeekIds`)

**Files:**
- Create: `src/TheLongestYear.Core/BonusSlot.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs`
- Test: `tests/TheLongestYear.Tests/RunStateTests.cs`

- [ ] **Step 1: Write the failing tests** (append to `RunStateTests.cs`)

```csharp
    [Fact]
    public void BeginNewMonth_clears_current_week_bonus_slots()
    {
        var run = new RunState();
        run.CurrentWeekBonusSlots.Add(new BonusSlot { BundleIndex = 3, IngredientIndex = 1, ItemId = "(O)24", Stack = 5, Quality = 2, BundleName = "Quality Crops" });
        run.BeginNewMonth(Season.Summer);
        Assert.Empty(run.CurrentWeekBonusSlots);
    }

    [Fact]
    public void BeginNewRun_clears_current_week_bonus_slots()
    {
        var run = new RunState();
        run.CurrentWeekBonusSlots.Add(new BonusSlot { BundleIndex = 3, IngredientIndex = 1, ItemId = "(O)24" });
        run.BeginNewRun(seed: 42);
        Assert.Empty(run.CurrentWeekBonusSlots);
    }

    [Fact]
    public void Select_clears_current_week_bonus_slots()
    {
        var run = new RunState();
        run.CurrentWeekBonusSlots.Add(new BonusSlot { BundleIndex = 3, IngredientIndex = 1, ItemId = "(O)24" });
        run.Select(Theme.Farming);
        Assert.Empty(run.CurrentWeekBonusSlots);
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests --filter "FullyQualifiedName~RunStateTests" --nologo`
Expected: compile error `BonusSlot` not found.

- [ ] **Step 3: Create `src/TheLongestYear.Core/BonusSlot.cs`**

```csharp
namespace TheLongestYear.Core;

/// <summary>
/// One sampled Community Center bundle slot — a weekly theme goal. Identified by
/// (BundleIndex, IngredientIndex) against the live CC slot state; ItemId/Stack/Quality/BundleName
/// are display copies captured at sampling time. Plain get/set POCO so MetaStore's JSON
/// round-trips it (same pattern as StashItemRecord).
/// </summary>
public sealed class BonusSlot
{
    public int BundleIndex { get; set; }
    public int IngredientIndex { get; set; }
    public string ItemId { get; set; } = "";
    public int Stack { get; set; } = 1;
    public int Quality { get; set; }
    public string BundleName { get; set; } = "";
}
```

- [ ] **Step 4: Modify `RunState.cs`**

Add after the `CurrentWeekBonusItems` property (keep that property — legacy JSON read for migration; document it):

```csharp
    /// <summary>
    /// LEGACY (pre-slot redesign, 2026-07-09): the old id-only weekly bonus sample. Kept ONLY so
    /// mid-week saves from older versions deserialize and RunController can detect + migrate them
    /// (non-empty here + empty CurrentWeekBonusSlots → one-time re-sample). Never written to by
    /// new code except clears.
    /// </summary>
    // (the existing CurrentWeekBonusItems property — update its doc comment to the text above)

    /// <summary>
    /// The active week's sampled goal slots. Populated at selection time by
    /// RunController.PopulateBonusSlotsForCurrentSelection; a goal ticks when its exact CC slot
    /// flips complete (live state is the source of truth). Completing a sampled slot earns the
    /// 1.5× SelectionBonusMultiplier. Cleared on Select/BeginNewMonth/BeginNewRun.
    /// </summary>
    public List<BonusSlot> CurrentWeekBonusSlots { get; set; } = new();
```

Then:
- In `Select(Theme)`: replace `DonatedThisWeekIds.Clear();` with `CurrentWeekBonusSlots.Clear();`
- In `BeginNewMonth`: remove `DonatedThisWeekIds.Clear();`, add `CurrentWeekBonusSlots.Clear();` next to `CurrentWeekBonusItems.Clear();`
- In `BeginNewRun`: remove `DonatedThisWeekIds.Clear();`, add `CurrentWeekBonusSlots.Clear();` next to `CurrentWeekBonusItems.Clear();`
- Delete the `DonatedThisWeekIds` property and its doc comment.
- In `RecordDonation`: delete the two `DonatedThisWeekIds` lines (body becomes identical to `RecordCumulativeDonation`; keep both methods — callers differ semantically and the doc comments explain).

- [ ] **Step 5: Fix compile fallout in tests**

`grep -rn "DonatedThisWeekIds" tests/ src/` — update/delete any test asserting on `DonatedThisWeekIds` (RunStateTests has Select/BeginNewMonth clearing tests for it; retarget them at `CurrentWeekBonusSlots` or delete if now duplicates of Step 1 tests). `WeeklyThemeQuestService.cs` still references it — that's Task 4; to keep this commit green, do NOT touch the mod project here; the mod project compiles because `DonatedThisWeekIds` removal breaks it — so instead of deleting the property outright now, mark it `[System.Obsolete]`? **No — keep it simple: in THIS task delete the property and immediately patch the one mod-side reference:** in `WeeklyThemeQuestService.RefreshObjective`, replace `List<string> donated = Run.DonatedThisWeekIds;` with `List<string> donated = new();` (temporarily ticks nothing; fully rewritten in Task 4). Add `// TEMP: rewritten to slot-based in the same plan (Task 4).`

- [ ] **Step 6: Run full suite + build**

Run: `dotnet test tests/TheLongestYear.Tests --nologo && dotnet build TheLongestYear.sln --nologo`
Expected: all tests pass, 0 warnings/errors.

- [ ] **Step 7: Bump manifest + commit**

```bash
git add src/TheLongestYear.Core/BonusSlot.cs src/TheLongestYear.Core/RunState.cs src/TheLongestYear/Loop/WeeklyThemeQuestService.cs src/TheLongestYear/manifest.json tests/TheLongestYear.Tests/RunStateTests.cs
git commit -m "v0.11.12: Add BonusSlot + RunState.CurrentWeekBonusSlots; retire DonatedThisWeekIds"
```

---

### Task 2: Core `SlotPoolBuilder` (pure open-slot pool)

**Files:**
- Create: `src/TheLongestYear.Core/SlotPoolBuilder.cs`
- Test: `tests/TheLongestYear.Tests/SlotPoolBuilderTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SlotPoolBuilderTests
{
    // Pantry room maps to Theme.Farming via RoomThemeMap.
    private static Dictionary<string, string> BundleData(params (int index, string name, string ingredients, int slots)[] bundles)
    {
        var d = new Dictionary<string, string>();
        foreach (var (index, name, ingredients, slots) in bundles)
            d[$"Pantry/{index}"] = $"{name}/O 465 1/{ingredients}/0/{slots}/0/{name}";
        return d;
    }

    private static IReadOnlyList<BundleRequirement> Reqs(params BundleRequirement[] reqs) => reqs;

    private static BundleRequirement SeasonalReq(string name, params string[] ids)
        => BundleRequirement.CreateSeasonal(name, Theme.Farming, ids, Season.Spring,
            new Dictionary<string, int>(), new Dictionary<string, int>());

    [Fact]
    public void Open_slots_of_an_in_play_bundle_are_pooled_with_stack_and_quality()
    {
        var data = BundleData((3, "Spring Crops", "24 1 0 188 5 2", 2));
        var reqs = Reqs(SeasonalReq("Spring Crops", "(O)24", "(O)188"));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => new[] { false, false }, reqs,
            Theme.Farming, Season.Spring, _ => true);

        Assert.Equal(2, pool.Count);
        var green = pool.Single(s => s.ItemId == "(O)188");
        Assert.Equal(3, green.BundleIndex);
        Assert.Equal(1, green.IngredientIndex);
        Assert.Equal(5, green.Stack);
        Assert.Equal(2, green.Quality);
        Assert.Equal("Spring Crops", green.BundleName);
    }

    [Fact]
    public void Completed_slots_are_excluded()
    {
        var data = BundleData((3, "Spring Crops", "24 1 0 188 5 2", 2));
        var reqs = Reqs(SeasonalReq("Spring Crops", "(O)24", "(O)188"));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => new[] { true, false }, reqs,
            Theme.Farming, Season.Spring, _ => true);

        Assert.Single(pool);
        Assert.Equal("(O)188", pool[0].ItemId);
    }

    [Fact]
    public void Bundle_with_enough_completed_slots_is_fully_excluded()
    {
        // Pick-1-of-2: one slot done ⇒ the bundle is complete; its remaining line is dead.
        var data = BundleData((3, "Rare Crops", "24 1 0 188 5 2", 1));
        var reqs = Reqs(BundleRequirement.CreatePercentage(
            "Rare Crops", Theme.Farming, new[] { "(O)24", "(O)188" },
            numberOfSlots: 1, cumulativeRequiredBySeason: new[] { 0, 0, 0, 1 }));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => new[] { true, false }, reqs,
            Theme.Farming, Season.Spring, _ => true);

        Assert.Empty(pool);
    }

    [Fact]
    public void Other_theme_and_off_season_and_category_slots_are_excluded()
    {
        var data = BundleData(
            (3, "Spring Crops", "24 1 0 -5 1 0", 2),     // -5 = category ref
            (4, "Summer Crops", "256 1 0", 1));
        var reqs = Reqs(
            SeasonalReq("Spring Crops", "(O)24"),
            BundleRequirement.CreateSeasonal("Summer Crops", Theme.Farming, new[] { "(O)256" }, Season.Summer,
                new Dictionary<string, int>(), new Dictionary<string, int>()));

        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => null, reqs,
            Theme.Farming, Season.Spring, _ => true);

        // Summer Crops not in play in Spring; category ref skipped; only (O)24 remains.
        Assert.Single(pool);
        Assert.Equal("(O)24", pool[0].ItemId);
    }

    [Fact]
    public void Null_slot_state_means_all_open()
    {
        var data = BundleData((3, "Spring Crops", "24 1 0", 1));
        var reqs = Reqs(SeasonalReq("Spring Crops", "(O)24"));
        var pool = SlotPoolBuilder.OpenSlotsForTheme(
            data, _ => null, reqs, Theme.Farming, Season.Spring, _ => true);
        Assert.Single(pool);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests --filter "FullyQualifiedName~SlotPoolBuilder" --nologo`
Expected: compile error `SlotPoolBuilder` not found.

- [ ] **Step 3: Create `src/TheLongestYear.Core/SlotPoolBuilder.cs`**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Builds the weekly-theme goal pool: every OPEN, in-play, concrete ingredient slot of the
/// theme's bundles. Pure — live game data comes in as plain inputs (bundle data dict + a
/// per-bundle slot-state accessor), so it unit-tests without Game1.
///
/// Rules (spec 2026-07-09):
///   - Bundle must classify to a BundleRequirement (matched by bundle name) with the requested
///     theme, and have in-play items this season (same gating as BundleRequirement.InPlayItemsFor:
///     Seasonal in its season / PerItem pins / Percentage non-zero quota + obtainability).
///   - A bundle that already has NumberOfSlots completed ingredient lines is complete — its
///     remaining lines can no longer be donated and are excluded.
///   - Category refs and completed slots are excluded. Null slot state ⇒ all lines open.
/// </summary>
public static class SlotPoolBuilder
{
    public static IReadOnlyList<BonusSlot> OpenSlotsForTheme(
        IReadOnlyDictionary<string, string> bundleData,
        Func<int, bool[]?> slotStateForBundle,
        IReadOnlyList<BundleRequirement> requirements,
        Theme theme, Season season,
        Func<string, bool> isObtainableInSeason)
    {
        if (bundleData == null) throw new ArgumentNullException(nameof(bundleData));
        if (slotStateForBundle == null) throw new ArgumentNullException(nameof(slotStateForBundle));
        if (requirements == null) throw new ArgumentNullException(nameof(requirements));
        if (isObtainableInSeason == null) throw new ArgumentNullException(nameof(isObtainableInSeason));

        // Requirements by bundle name (first wins — names are unique per save in practice).
        var reqByName = new Dictionary<string, BundleRequirement>(StringComparer.Ordinal);
        foreach (BundleRequirement r in requirements)
            if (!reqByName.ContainsKey(r.Name))
                reqByName[r.Name] = r;

        var pool = new List<BonusSlot>();
        foreach (KeyValuePair<string, string> kvp in bundleData)
        {
            ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
            if (!reqByName.TryGetValue(bundle.Name, out BundleRequirement req)) continue;
            if (req.Theme != theme) continue;

            var inPlay = new HashSet<string>(
                req.InPlayItemsFor(season, isObtainableInSeason), StringComparer.Ordinal);
            if (inPlay.Count == 0) continue;

            bool[]? state = slotStateForBundle(bundle.Index);

            // Bundle already complete (enough lines filled)? Remaining lines are dead.
            if (state != null)
            {
                int completed = 0;
                int lineCount = Math.Min(bundle.Ingredients.Count, state.Length);
                for (int i = 0; i < lineCount; i++)
                    if (state[i]) completed++;
                if (completed >= bundle.NumberOfSlots) continue;
            }

            for (int i = 0; i < bundle.Ingredients.Count; i++)
            {
                BundleIngredient ing = bundle.Ingredients[i];
                if (BundleParsing.IsCategoryRef(ing.ItemRef)) continue;
                string id = BundleParsing.NormalizeItemId(ing.ItemRef);
                if (!inPlay.Contains(id)) continue;
                if (state != null && i < state.Length && state[i]) continue;   // already donated

                pool.Add(new BonusSlot
                {
                    BundleIndex = bundle.Index,
                    IngredientIndex = i,
                    ItemId = id,
                    Stack = ing.Stack > 0 ? ing.Stack : 1,
                    Quality = ing.Quality,
                    BundleName = bundle.Name,
                });
            }
        }
        return pool;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests --filter "FullyQualifiedName~SlotPoolBuilder" --nologo`
Expected: PASS (5 tests).

- [ ] **Step 5: Full suite + build, bump manifest, commit**

```bash
dotnet test tests/TheLongestYear.Tests --nologo && dotnet build TheLongestYear.sln --nologo
git add src/TheLongestYear.Core/SlotPoolBuilder.cs tests/TheLongestYear.Tests/SlotPoolBuilderTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.11.13: Add SlotPoolBuilder (pure open-slot goal pool)"
```

---

### Task 3: Core `BonusSlotSampler` (seeded draw over slots)

**Files:**
- Create: `src/TheLongestYear.Core/BonusSlotSampler.cs`
- Test: `tests/TheLongestYear.Tests/BonusSlotSamplerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BonusSlotSamplerTests
{
    private static BonusSlot Slot(string id, int bundleIndex = 0, int ingredientIndex = 0,
        int stack = 1, int quality = 0, string bundleName = "B")
        => new() { ItemId = id, BundleIndex = bundleIndex, IngredientIndex = ingredientIndex,
                   Stack = stack, Quality = quality, BundleName = bundleName };

    private static Rarity CommonRarity(string _) => Rarity.Common;

    [Fact]
    public void Sample_is_deterministic_for_same_inputs()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)1", 0, 0), Slot("(O)2", 0, 1), Slot("(O)3", 1, 0),
            Slot("(O)4", 1, 1), Slot("(O)5", 2, 0),
        };
        var a = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 3);
        var b = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 3);
        Assert.Equal(
            a.Select(s => (s.BundleIndex, s.IngredientIndex)),
            b.Select(s => (s.BundleIndex, s.IngredientIndex)));
        Assert.Equal(3, a.Count);
    }

    [Fact]
    public void One_goal_per_item_id_even_when_multiple_open_slots_share_it()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)24", 0, 0, stack: 1),          // Spring Crops parsnip
            Slot("(O)24", 3, 1, stack: 5, quality: 2), // Quality Crops parsnip
        };
        var sample = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 4);
        Assert.Single(sample);
        Assert.Equal("(O)24", sample[0].ItemId);
    }

    [Fact]
    public void Slot_choice_among_duplicates_is_seeded_deterministic()
    {
        var pool = new List<BonusSlot>
        {
            Slot("(O)24", 0, 0, stack: 1),
            Slot("(O)24", 3, 1, stack: 5, quality: 2),
        };
        var a = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 4).Single();
        var b = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 4).Single();
        Assert.Equal((a.BundleIndex, a.IngredientIndex), (b.BundleIndex, b.IngredientIndex));

        // Across many (seed, week) combos BOTH slots must occur — i.e. the pick is random,
        // not a fixed min/max rule.
        var seen = new HashSet<(int, int)>();
        for (int seed = 0; seed < 40; seed++)
            foreach (var s in BonusSlotSampler.SampleSlots(seed, 5, Theme.Farming, pool, CommonRarity, 4))
                seen.Add((s.BundleIndex, s.IngredientIndex));
        Assert.Contains((0, 0), seen);
        Assert.Contains((3, 1), seen);
    }

    [Fact]
    public void Pool_smaller_than_max_returns_whole_pool()
    {
        var pool = new List<BonusSlot> { Slot("(O)1"), Slot("(O)2", 1) };
        var sample = BonusSlotSampler.SampleSlots(42, 5, Theme.Farming, pool, CommonRarity, 7);
        Assert.Equal(2, sample.Count);
    }

    [Fact]
    public void Empty_pool_returns_empty()
    {
        var sample = BonusSlotSampler.SampleSlots(
            42, 5, Theme.Farming, new List<BonusSlot>(), CommonRarity, 4);
        Assert.Empty(sample);
    }

    [Fact]
    public void Weeks_1_and_2_avoid_early_game_infrastructure_items()
    {
        // (O)426 Goat Cheese is in CcItemCatalog.EarlyGameAvoid; (O)24 Parsnip is not.
        var pool = new List<BonusSlot> { Slot("(O)426", 0, 0), Slot("(O)24", 1, 0) };
        for (int seed = 0; seed < 20; seed++)
        {
            var sample = BonusSlotSampler.SampleSlots(seed, 2, Theme.Farming, pool, CommonRarity, 1);
            Assert.Equal("(O)24", sample.Single().ItemId);
        }
        // Week 3+ unlocks the full pool: with maxCount 2, both appear.
        var late = BonusSlotSampler.SampleSlots(42, 3, Theme.Farming, pool, CommonRarity, 2);
        Assert.Equal(2, late.Count);
    }
}
```

(Before finalizing the early-game test, verify `"(O)426"` is in `CcItemCatalog.EarlyGameAvoid` — `grep -n "426" src/TheLongestYear.Core/CcItemCatalog.cs`. If not, substitute any id that IS listed there, e.g. cheese/wine, and a non-listed id.)

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests --filter "FullyQualifiedName~BonusSlotSampler" --nologo`
Expected: compile error `BonusSlotSampler` not found.

- [ ] **Step 3: Create `src/TheLongestYear.Core/BonusSlotSampler.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Per-(run, week, theme) seeded sample of goal SLOTS from the open-slot pool (see
/// <see cref="SlotPoolBuilder"/>). Successor to <see cref="BonusItemSampler"/> (2026-07-09
/// slot redesign): the draw is still per item id with inverse-rarity weighting and the week-1/2
/// early-game filter, but each drawn id resolves to ONE concrete slot — seeded-random among the
/// id's open slots — so the checklist entry names an exact (bundle, line, stack, quality).
/// Deterministic: same (seed, week, theme, pool) → same slots.
/// </summary>
public static class BonusSlotSampler
{
    private const int WeekSaltPrime = 7919;
    private const int ThemeSaltPrime = 1031;
    private const int EarlyGameMaxWeek = 2;

    public static IReadOnlyList<BonusSlot> SampleSlots(
        int runSeed, int weekOfYear, Theme theme,
        IReadOnlyList<BonusSlot> openSlots,
        Func<string, Rarity> rarityOf,
        int maxCount)
    {
        if (openSlots is null) throw new ArgumentNullException(nameof(openSlots));
        if (rarityOf is null) throw new ArgumentNullException(nameof(rarityOf));
        if (maxCount <= 0 || openSlots.Count == 0) return Array.Empty<BonusSlot>();

        // Group open slots by item id; per-id weight = inverse rarity (unchanged from
        // BonusItemSampler.WeightFor).
        Dictionary<string, List<BonusSlot>> slotsById = new(StringComparer.Ordinal);
        foreach (BonusSlot slot in openSlots)
        {
            if (!slotsById.TryGetValue(slot.ItemId, out List<BonusSlot> list))
                slotsById[slot.ItemId] = list = new List<BonusSlot>();
            list.Add(slot);
        }

        // Week 1-2: drop late-game-infrastructure ids unless that empties the pool.
        IEnumerable<string> idPool = slotsById.Keys;
        if (weekOfYear <= EarlyGameMaxWeek)
        {
            var filtered = slotsById.Keys.Where(id => !CcItemCatalog.EarlyGameAvoid.Contains(id)).ToList();
            if (filtered.Count > 0)
                idPool = filtered;
        }

        // Stable input order keyed by id so the seeded weighted draws are reproducible.
        List<(string Id, int Weight)> remaining = idPool
            .OrderBy(id => id, StringComparer.Ordinal)
            .Select(id => (id, BonusItemSampler.WeightFor(rarityOf(id))))
            .ToList();

        Random rng = new Random(runSeed ^ (weekOfYear * WeekSaltPrime) ^ ((int)theme * ThemeSaltPrime));
        int take = Math.Min(maxCount, remaining.Count);
        List<BonusSlot> result = new(take);
        for (int n = 0; n < take; n++)
        {
            int totalWeight = 0;
            for (int i = 0; i < remaining.Count; i++) totalWeight += remaining[i].Weight;

            int draw = rng.Next(totalWeight);
            int cum = 0;
            for (int i = 0; i < remaining.Count; i++)
            {
                cum += remaining[i].Weight;
                if (draw < cum)
                {
                    // Resolve the drawn id to one concrete slot: seeded-random among its open
                    // slots (deterministic order first so the rng pick reproduces).
                    List<BonusSlot> candidates = slotsById[remaining[i].Id]
                        .OrderBy(s => s.BundleIndex).ThenBy(s => s.IngredientIndex)
                        .ToList();
                    result.Add(candidates[rng.Next(candidates.Count)]);
                    remaining.RemoveAt(i);
                    break;
                }
            }
        }

        return result;
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests --filter "FullyQualifiedName~BonusSlotSampler" --nologo`
Expected: PASS (6 tests).

- [ ] **Step 5: Full suite + build, bump manifest, commit**

```bash
dotnet test tests/TheLongestYear.Tests --nologo && dotnet build TheLongestYear.sln --nologo
git add src/TheLongestYear.Core/BonusSlotSampler.cs tests/TheLongestYear.Tests/BonusSlotSamplerTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.11.14: Add BonusSlotSampler (seeded slot draw, per-id weighting kept)"
```

---

### Task 4: Mod-side rewiring — RunController, WeeklyThemeQuestService, DonationService/Observer, ModEntry

This task converts the runtime to slots end-to-end. It compiles+runs coherently only as a whole, so it is one commit; steps below are still bite-sized. No unit tests (all Game1-coupled); the suite must stay green and the build clean.

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs`
- Modify: `src/TheLongestYear/Loop/WeeklyThemeQuestService.cs`
- Modify: `src/TheLongestYear/Donations/DonationService.cs`
- Modify: `src/TheLongestYear/Donations/DonationObserver.cs`
- Modify: `src/TheLongestYear/ModEntry.cs`

- [ ] **Step 1: RunController — slot state accessor + public sampling API**

Add near `RarityForItem` (RunController.cs ~line 699). `CoreSeason` is the existing alias for `TheLongestYear.Core.Season` in this file:

```csharp
        /// <summary>Live per-slot completion state for a bundle (vanilla source of truth), or
        /// null when absent. Same NetBundles access pattern as ItemDonationSync/VaultPaymentSync:
        /// FieldDict.ContainsKey is the safe presence check.</summary>
        internal static bool[] SlotStateForBundle(int bundleIndex)
        {
            var bundles = Game1.netWorldState?.Value?.Bundles;
            if (bundles?.FieldDict == null) return null;
            return bundles.FieldDict.ContainsKey(bundleIndex) ? bundles[bundleIndex] : null;
        }

        /// <summary>Sample this week's goal slots for a theme+season — shared by the hub preview
        /// and the selection-time commit so both show the same goals. Pool = open, in-play slots
        /// (already-donated slots are never sampled; a complete bundle's leftover lines are dead).</summary>
        public System.Collections.Generic.IReadOnlyList<BonusSlot> SampleSlotsForTheme(
            Theme theme, CoreSeason season, int weekOfYear)
        {
            var bundleData = Game1.netWorldState?.Value?.BundleData;
            if (bundleData == null) return System.Array.Empty<BonusSlot>();
            var pool = SlotPoolBuilder.OpenSlotsForTheme(
                bundleData, SlotStateForBundle, _requirements,
                theme, season, id => IsObtainableInSeason(id, season));
            return BonusSlotSampler.SampleSlots(
                Run.Seed, weekOfYear, theme, pool, RarityForItem, BonusListSizeFor(season));
        }
```

Find `BonusListSizeForCurrentSeason()` (grep it in RunController.cs). If it indexes `BonusItemSampler.DefaultMaxCountBySeason` by `Run.Season`, refactor to:

```csharp
        public int BonusListSizeFor(CoreSeason season)
            => BonusItemSampler.DefaultMaxCountBySeason[(int)season];

        public int BonusListSizeForCurrentSeason() => BonusListSizeFor(Run.Season);
```

(Adapt to whatever the existing body actually computes — keep its exact semantics, parameterized by season.)

- [ ] **Step 2: RunController — replace `PopulateBonusItemsForCurrentSelection`**

Replace the method body (keep the old name's callers in sync — rename to `PopulateBonusSlotsForCurrentSelection` and update the 3 call sites at lines ~121, ~510, ~662):

```csharp
        /// <summary>Sample the per-week goal slots for the current selection and store them on
        /// RunState. Clears the legacy id list so post-migration saves stop carrying it.</summary>
        private void PopulateBonusSlotsForCurrentSelection()
        {
            Run.CurrentWeekBonusSlots.Clear();
            Run.CurrentWeekBonusItems.Clear();
            if (!Run.CurrentSelection.HasValue) return;
            var sample = SampleSlotsForTheme(Run.CurrentSelection.Value, Run.Season, Run.WeekOfYear);
            Run.CurrentWeekBonusSlots.AddRange(sample);
        }

        /// <summary>Empty goal pool (everything for this theme already donated): no quest this
        /// week, drawback auto-lifted, no weekly JP bonus (spec 2026-07-09 §3). The
        /// LiabilitySuppressedThisWeek flag doubles as the completion-reward idempotency guard,
        /// so setting it here also prevents a later JP payout.</summary>
        private void ApplyEmptyPoolLiftIfNeeded()
        {
            if (!Run.CurrentSelection.HasValue) return;
            if (Run.CurrentWeekBonusSlots.Count > 0) return;
            if (Run.LiabilitySuppressedThisWeek) return;

            Run.LiabilitySuppressedThisWeek = true;
            ActiveEffectsProvider.SuppressLiability();
            Game1.addHUDMessage(new StardewValley.HUDMessage(
                "Nothing left to donate for this theme - drawback lifted.",
                StardewValley.HUDMessage.newQuest_type));
            _monitor.Log(
                $"Weekly goal pool for {Run.CurrentSelection} is empty (all in-play slots donated) - " +
                "no quest this week; drawback auto-lifted, no weekly JP bonus.",
                LogLevel.Info);
        }
```

In `SelectByName` (after `PopulateBonusSlotsForCurrentSelection(); ... ActiveEffectsProvider.Set(bonus, liability);`) add `ApplyEmptyPoolLiftIfNeeded();` BEFORE `_questService?.OnThemeSelected();`, and update the log line:

```csharp
            _monitor.Log(
                $"Selected {theme} (bonus {bonus}, liability {liability}). " +
                $"Goal slots this week: [{string.Join(", ", Run.CurrentWeekBonusSlots.Select(s => $"{s.ItemId}@{s.BundleName}#{s.IngredientIndex}"))}].",
                LogLevel.Info);
```

(add `using System.Linq;` if not present). Same `ApplyEmptyPoolLiftIfNeeded();` insertion in `DoDayStartSeasonAndHub`'s pre-pick branch (after `ActiveEffectsProvider.Set`, before `_questService?.OnThemeSelected();`).

- [ ] **Step 3: RunController — one-time migration in `OnRunLoaded`**

Insert right after the month-rollover block (after `Run.DayOfMonth = Game1.dayOfMonth;`, ~line 123):

```csharp
            // 2026-07-09 slot redesign migration: a mid-week save from an older version has the
            // legacy id-only bonus list but no slot goals. Re-sample once (the week's goals
            // re-roll — one-time, beta-acceptable) and rebuild the quest from slots.
            if (Run.CurrentSelection.HasValue
                && Run.CurrentWeekBonusSlots.Count == 0
                && Run.CurrentWeekBonusItems.Count > 0)
            {
                _monitor.Log(
                    "Migrating this week's bonus list to slot-based goals (one-time re-sample).",
                    LogLevel.Info);
                PopulateBonusSlotsForCurrentSelection();
                ApplyEmptyPoolLiftIfNeeded();
                _questService?.OnThemeSelected();
            }
```

- [ ] **Step 4: WeeklyThemeQuestService — render from slots, tick from live CC state**

Full replacement of the changed members (ctor, OnThemeSelected guard, RefreshObjective, ResolveDisplayName → DescribeSlot):

```csharp
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly JpCalculator _jp;
        private readonly Func<int, bool[]> _slotStateForBundle;

        public WeeklyThemeQuestService(IMonitor monitor, MetaStore store,
            GameplayConfig config, Func<int, bool[]> slotStateForBundle)
        {
            _monitor = monitor;
            _store = store;
            _jp = new JpCalculator(config.Jp);
            _slotStateForBundle = slotStateForBundle ?? (_ => null);
        }
```

In `OnThemeSelected`: replace `if (Run.CurrentWeekBonusItems.Count == 0) return;` with `if (Run.CurrentWeekBonusSlots.Count == 0) return;` and the count in its log line with `Run.CurrentWeekBonusSlots.Count`. Replace the quest tip:

```csharp
            q.questDescription =
                $"Bonus: {ThemeModifiers.DisplayNameFor(bonusId)}\n" +
                $"Drawback: {ThemeModifiers.DisplayNameFor(liabilityId)}\n\n" +
                "Tip: hold matching donations for their theme week - completing a goal slot pays " +
                "1.5x JP, and finishing every goal lifts the drawback. Each goal names the exact " +
                "bundle slot it wants (full quantity and quality).";
```

In `OnRunLoaded`: replace `Run.CurrentWeekBonusItems.Count > 0` with `Run.CurrentWeekBonusSlots.Count > 0`.

Replace `RefreshObjective`:

```csharp
        private void RefreshObjective(Quest q)
        {
            IList<BonusSlot> slots = Run.CurrentWeekBonusSlots;
            int doneCount = 0;
            var lines = new List<string>();

            foreach (BonusSlot slot in slots)
            {
                // Live CC slot state is the source of truth: every sampled slot was open at
                // selection time, so "complete now" means "completed this week". Self-reconciling
                // (no observer-miss drift), and vanilla only completes a slot when the full
                // stack at the required quality is deposited — multi-item goals need all items.
                bool isDone = IsSlotComplete(slot);
                if (isDone) doneCount++;
                // ASCII checkbox glyphs — Stardew's smallFont doesn't include U+2611/U+2610.
                lines.Add(isDone ? $"  [X] {DescribeSlot(slot)}" : $"  [ ] {DescribeSlot(slot)}");
            }

            q.currentObjective = $"Donated {doneCount}/{slots.Count}:\n" + string.Join("\n", lines);

            if (slots.Count > 0 && doneCount == slots.Count && !q.completed.Value)
            {
                q.questComplete();
                AwardCompletionRewards();
            }
        }

        private bool IsSlotComplete(BonusSlot slot)
        {
            bool[] state = _slotStateForBundle(slot.BundleIndex);
            return state != null
                && slot.IngredientIndex >= 0
                && slot.IngredientIndex < state.Length
                && state[slot.IngredientIndex];
        }
```

Replace `ResolveDisplayName` with `DescribeSlot` (keep `AmbiguousEggColors` + `BareItemId` unchanged):

```csharp
        /// <summary>"DisplayName (Brown) x5 (gold) - Bundle Name" — names the exact slot
        /// requirement. Quality tags: 1=silver, 2/3=gold, 4=iridium.</summary>
        private string DescribeSlot(BonusSlot slot)
        {
            string name = slot.ItemId;
            try
            {
                Item item = ItemRegistry.Create(slot.ItemId, 1, 0, allowNull: true);
                if (item != null) name = item.DisplayName;
            }
            catch (Exception)
            {
                // ItemRegistry may throw for malformed ids; fall back to the raw id.
            }

            string colorTag = AmbiguousEggColors.TryGetValue(BareItemId(slot.ItemId), out string color)
                ? $" ({color})" : "";
            string qty = slot.Stack > 1 ? $" x{slot.Stack}" : "";
            string quality = slot.Quality >= 4 ? " (iridium)"
                : slot.Quality >= 2 ? " (gold)"
                : slot.Quality >= 1 ? " (silver)"
                : "";
            return $"{name}{colorTag}{qty}{quality} - {slot.BundleName}";
        }
```

Delete the now-unused `_stackForIngredient` field and the temporary `List<string> donated = new();` line from Task 1.

- [ ] **Step 5: DonationService + DonationObserver — slot identity for the 1.5×**

`DonationService.OnItemDonated` signature + bonus check:

```csharp
        /// <summary>A successful donation of <paramref name="count"/> of an item to the CC.
        /// Pays base rarity JP; the SelectionBonusMultiplier applies when the completed slot
        /// (bundleIndex, ingredientIndex) is one of this week's sampled goal slots (2026-07-09
        /// slot redesign — an id-only match no longer qualifies).</summary>
        public void OnItemDonated(string qualifiedItemId, int count, int bundleIndex = -1, int ingredientIndex = -1)
```

and replace `bool bonusApplies = IsSelectedBonusItem(qualifiedItemId);` with `bool bonusApplies = IsSelectedBonusSlot(bundleIndex, ingredientIndex);`, replacing the old private method:

```csharp
        /// <summary>True if the just-completed CC slot is one of this week's sampled goal slots
        /// (see <see cref="BonusSlotSampler"/>; persisted in <see cref="RunState.CurrentWeekBonusSlots"/>).</summary>
        private bool IsSelectedBonusSlot(int bundleIndex, int ingredientIndex)
        {
            if (bundleIndex < 0 || ingredientIndex < 0) return false;
            if (!Run.CurrentSelection.HasValue) return false;
            foreach (BonusSlot s in Run.CurrentWeekBonusSlots)
                if (s.BundleIndex == bundleIndex && s.IngredientIndex == ingredientIndex)
                    return true;
            return false;
        }
```

`DonationObserver.DiffAndAward`: change `DonationService.Active.OnItemDonated(qualifiedId, 1);` to `DonationService.Active.OnItemDonated(qualifiedId, 1, b.bundleIndex, i);`.

Check other `OnItemDonated` callers: `grep -rn "OnItemDonated(" src/` — any caller without slot info (e.g. debug commands) keeps working via the defaults (no bonus).

- [ ] **Step 6: ModEntry wiring**

Replace the quest-service construction (~line 331):

```csharp
            _questService = new WeeklyThemeQuestService(
                this.Monitor, _meta, _config,
                slotStateForBundle: Loop.RunController.SlotStateForBundle);
```

(`_ingredientStacks` is still used by RunController/others — leave line 327 alone.)

- [ ] **Step 7: Full suite + build**

Run: `dotnet test tests/TheLongestYear.Tests --nologo && dotnet build TheLongestYear.sln --nologo`
Expected: green, 0 warnings. Also `grep -rn "CurrentWeekBonusItems" src/` — remaining references must be ONLY: RunState property, the two RunState clears, RunController's populate-clear + migration check.

- [ ] **Step 8: Bump manifest + commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/Loop/WeeklyThemeQuestService.cs src/TheLongestYear/Donations/DonationService.cs src/TheLongestYear/Donations/DonationObserver.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.11.15: Weekly theme goals are exact CC slots (live-state tick, slot-strict 1.5x, empty-pool lift, migration)"
```

---

### Task 5: WeeklyHubMenu — slot-accurate preview icons + banking tip line

**Files:**
- Modify: `src/TheLongestYear/UI/WeeklyHubMenu.cs`

- [ ] **Step 1: Preview uses the same slot sampler as the commit**

Replace the body of `PopulateBonusList` (lines ~265–293). The season-size lookup moves inside `SampleSlotsForTheme`, and each icon renders its OWN slot's stack/quality (the global MAX maps are no longer consulted here):

```csharp
            dest.Clear();
            if (theme == null) return;

            // Sample for the OFFER's season (which is next-season on day 28's Sunday-night hub).
            int week = _isPreSelectForNextMonth ? _run.WeekOfYear + 1 : _run.WeekOfYear;
            var sample = _runController.SampleSlotsForTheme(theme.Value, _offerSeason, week);

            foreach (BonusSlot slot in sample)
            {
                // Each icon shows ITS slot's true requirement (stack badge + quality star) —
                // 2026-07-09 slot redesign; replaces the global per-id MAX maps here.
                Item item = null;
                try { item = ItemRegistry.Create(slot.ItemId, slot.Stack, slot.Quality, allowNull: true); }
                catch (Exception) { item = null; }
                dest.Add(item);
            }
```

Add `using TheLongestYear.Core;` if not already imported (it is — the file references Theme).

- [ ] **Step 2: Banking tip line under "Pick a theme"**

At line ~315 change the title block height (add 32 for the tip line):

```csharp
            int titleBlock = 24 + (_junimoTexture != null ? JunimoSpriteSize + 12 : 0) + 48 + 32 + 20;
```

In `draw` after `SpriteText.drawStringHorizontallyCenteredAt(b, "Pick a theme", panelCenterX, drawY);` (line ~582) add:

```csharp
            drawY += 48;
            const string bankingTip = "Banking items for a matching theme week pays 1.5x JP.";
            Microsoft.Xna.Framework.Vector2 tipSize = Game1.smallFont.MeasureString(bankingTip);
            Utility.drawTextWithShadow(b, bankingTip, Game1.smallFont,
                new Microsoft.Xna.Framework.Vector2(panelCenterX - tipSize.X / 2f, drawY),
                Game1.textColor);
```

- [ ] **Step 3: Build + suite, visual sanity note**

Run: `dotnet test tests/TheLongestYear.Tests --nologo && dotnet build TheLongestYear.sln --nologo`
Expected: green. (Layout is verified in the Task 6 playtest.)

- [ ] **Step 4: Bump manifest + commit**

```bash
git add src/TheLongestYear/UI/WeeklyHubMenu.cs src/TheLongestYear/manifest.json
git commit -m "v0.11.16: Hub preview renders per-slot goal requirements + banking-tip line"
```

---

### Task 6: Cleanup — retire `BonusItemSampler.SampleForTheme`, PC smoke test

**Files:**
- Modify: `src/TheLongestYear.Core/BonusItemSampler.cs`
- Modify: `tests/TheLongestYear.Tests/BonusItemSamplerTests.cs`

- [ ] **Step 1: Verify `SampleForTheme` is now unused**

Run: `grep -rn "SampleForTheme" src/ tests/`
Expected: only `BonusItemSampler.cs` itself + its tests. If anything else still calls it, STOP — that caller was missed in Task 4/5; fix it first.

- [ ] **Step 2: Delete `SampleForTheme` + `EarlyGameMaxWeek` from `BonusItemSampler`**

Keep `WeightFor` and `DefaultMaxCountBySeason` (consumed by `BonusSlotSampler` / `RunController`). Update the class doc comment: sampling now lives in `BonusSlotSampler`; this class retains the shared weighting table + per-season counts.

- [ ] **Step 3: Trim `BonusItemSamplerTests.cs`**

Delete tests that call `SampleForTheme`; keep/retarget any `WeightFor` / `DefaultMaxCountBySeason` assertions. If nothing remains, delete the file.

- [ ] **Step 4: Full suite + build**

Run: `dotnet test tests/TheLongestYear.Tests --nologo && dotnet build TheLongestYear.sln --nologo`
Expected: green, 0 warnings.

- [ ] **Step 5: Bump manifest + commit**

```bash
git add src/TheLongestYear.Core/BonusItemSampler.cs tests/TheLongestYear.Tests/BonusItemSamplerTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.11.17: Retire BonusItemSampler.SampleForTheme (slot sampler owns the draw)"
```

- [ ] **Step 6: PC smoke test (deploy is automatic via build)**

Launch SMAPI on the PC test save (see MEMORY: `launch-smapi.ps1` pattern / TLY `close-smapi.ps1`). On a TLY save:
1. `tly_reset` → pick a theme → quest log shows slot-named goals (`Item xN (gold) - Bundle`).
2. Donate a PARTIAL stack of a multi-item goal (e.g. 1 parsnip into a different slot) → goal does NOT tick; SMAPI log Trace shows the donation without `(bonus x1.5)`.
3. Complete the exact goal slot → tick + `(bonus x1.5)` in the log.
4. `tly_addjp` + donate everything for a theme, re-pick it → "Nothing left to donate for this theme - drawback lifted." HUD + no quest.
5. Hub: banking-tip line renders, bonus icons show per-slot stack/quality.
Log check: no ERROR/WARN from TLY. Pull the log per the standard flow and read it before claiming success.

---

## Self-review notes (already applied)

- Spec §1 pool rules → Task 2; §1 seeded slot pick → Task 3; §2 display+tick+1.5× → Task 4; §3 shrink/empty → Task 4 (`ApplyEmptyPoolLiftIfNeeded`, shorter list falls out of pool size); §4 persistence/migration → Tasks 1+4; §5 messaging → Task 4 (quest tip) + Task 5 (hub line); testing → Tasks 2/3 units + Task 6 playtest.
- Type consistency: `BonusSlot` fields (BundleIndex/IngredientIndex/ItemId/Stack/Quality/BundleName) used identically in Tasks 1–5; `SampleSlotsForTheme(Theme, CoreSeason, int)` defined Task 4 Step 1, consumed Task 4 Step 2 + Task 5 Step 1; `SlotStateForBundle` defined Task 4 Step 1, consumed Task 4 Step 6.
- Known judgment call: `RecordDonation` keeps its name/dual with `RecordCumulativeDonation` (semantic callers) even though bodies converge after Task 1.
