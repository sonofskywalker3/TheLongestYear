# Obtainable Board, Plan 2 of 5: stretch gates and the hard-item rule Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Spring foothold with the stretch rule (a bundle that gains nothing in a season reaches two weeks past it, swapping in a stretch item if it holds none), require one genuinely hard item per rolled bundle of 4 or more slots, exempt Easy, and surface both on the board, in `tly_gatecheck` and on the weekly cards.

**Architecture:** A pure `StretchRule` in Core decides, from a bundle's final ingredient list and the availability model, which (season, item) lines are stretch lines; `BundleClassifier.RampFromItems` and `BundleDeadlines` read those lines so the gate demands them; `BundleSlotFiller` runs the stretch swap and the hard-item swap in place of the foothold swap; `BundleRequirement` carries `StretchLines` for the goal sampler, the gate audit and the hub. The difficulty step travels on the availability model (`ItemAvailabilityModel.Step`) so Core never reads game state.

**Tech Stack:** C# / .NET 6, xunit (`dotnet test tests/TheLongestYear.Tests`), SMAPI mod (`dotnet build src/TheLongestYear -c Release`). Depends on plan 1 (WeekMode, HardWeek, `ItemAvailability.HardWeekOrPacing`, `EffortTiers.Tier(int)` absolute bands).

**Spec:** `docs/superpowers/specs/2026-08-28-obtainable-board-design.md` sections 2, 3 (the hard-item paragraph), 9 (gatecheck tags) and Easy.

## Global Constraints

- No em dashes anywhere. Bump `src/TheLongestYear/manifest.json` by one patch per commit (continue from where plan 1 ended). Commit locally only, never push. Stage only the task's files.
- Board determinism: a board is compared byte for byte at save load, so every decision here must be a pure function of (seed, pools, availability model, difficulty step). No `Game1` reads in Core.
- Stretch window is 2 weeks (`StretchRule.WindowWeeks`); it never reaches into Winter (Winter demands everything). Season-named bundles are exempt. Easy is exempt from the stretch rule, from the hard-item rule, and gets no Spring foothold either.
- Hard item means `EffortTiers.Tier(effort) >= EffortTier.Hard` (effort 6 and up). Rule applies to rolled bundles with `NumberOfSlots >= 4` on Normal, Hard and Extreme.
- A stretch item is never a true in-season item; a swap for a stretch item never swaps in a true in-season item.

---

### Task 1: `ItemAvailabilityModel.Step` and `StretchRule`

**Files:**
- Create: `src/TheLongestYear.Core/StretchRule.cs`
- Modify: `src/TheLongestYear.Core/ItemAvailability.cs` (model gains `DifficultyStep Step` constructor parameter, default Normal), `src/TheLongestYear.Core/Availability/ItemAvailabilityBuilder.cs` (pass it), `src/TheLongestYear/ModEntry.cs` (pass the live step where the model is built, next to the mode from plan 1)
- Test: `tests/TheLongestYear.Tests/StretchRuleTests.cs`

**Interfaces:**
- Produces: `ItemAvailabilityModel.Step : DifficultyStep`; `StretchRule.WindowWeeks = 2`; `StretchRule.Applies(DifficultyStep step)` (false for Easy); `StretchRule.IsReachable(ItemAvailability a, Season s)` (`a.Week <= LastWeekOf(s)`); `StretchRule.IsStretchFor(ItemAvailability a, Season s)` (`s != Winter && !IsReachable(a, s) && a.HardWeekOrPacing <= LastWeekOf(s) && a.Week <= LastWeekOf(s) + WindowWeeks`); `StretchRule.Lines(IReadOnlyList<string> ingredients, ItemAvailabilityModel model) : IReadOnlyDictionary<string, Season>` (for each season Spring to Fall in order: if no ingredient is newly reachable in s, that is `IsReachable(a, s) && !IsReachable(a, s - 1)` (Spring compares against nothing), pick the stretch candidates for s and return the first by ordinal id as the stretch line for s; an item is never a stretch line for two seasons; empty when `!Applies(model.Step)`).

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StretchRuleTests
{
    private static ItemAvailability Item(int week, int hard) =>
        new(AvailabilityWeeks.SeasonOf(week), 3, "test", EffortSource.Derived, week, AvailabilityWeeks.SeasonOf(week), hard);

    private static ItemAvailabilityModel Model(DifficultyStep step, params (string Id, int Week, int Hard)[] items)
    {
        var derived = new Dictionary<string, ItemAvailability>();
        foreach ((string id, int week, int hard) in items) derived[id] = Item(week, hard);
        return new ItemAvailabilityModel(derived, step: step);
    }

    [Fact]
    public void A_summer_week_6_item_with_a_spring_hard_week_is_a_spring_stretch()
    {
        Assert.True(StretchRule.IsStretchFor(Item(6, 1), Season.Spring));
        Assert.False(StretchRule.IsStretchFor(Item(7, 1), Season.Spring));    // past the window
        Assert.False(StretchRule.IsStretchFor(Item(6, 5), Season.Spring));    // hard week is Summer: a real fact
        Assert.False(StretchRule.IsStretchFor(Item(3, 1), Season.Spring));    // already reachable
        Assert.False(StretchRule.IsStretchFor(Item(14, 13), Season.Winter));  // Winter never stretches
    }

    [Fact]
    public void A_bundle_with_nothing_new_in_spring_gets_one_stretch_line()
    {
        ItemAvailabilityModel model = Model(DifficultyStep.Normal, ("(O)b", 6, 1), ("(O)a", 5, 2), ("(O)c", 9, 9), ("(O)d", 13, 13));
        IReadOnlyDictionary<string, Season> lines = StretchRule.Lines(new[] { "(O)b", "(O)a", "(O)c", "(O)d" }, model);
        Assert.Equal(Season.Spring, lines["(O)a"]);   // ordinal first of the two candidates
        Assert.False(lines.ContainsKey("(O)b"));
        // Summer gains (O)a and (O)b (reachable by week 8, not by week 4): no Summer stretch.
        // Fall gains (O)c: no Fall stretch.
        Assert.Single(lines);
    }

    [Fact]
    public void A_bundle_that_gains_something_every_season_has_no_stretch_lines()
    {
        ItemAvailabilityModel model = Model(DifficultyStep.Normal, ("(O)a", 1, 1), ("(O)b", 5, 5), ("(O)c", 9, 9), ("(O)d", 13, 13));
        Assert.Empty(StretchRule.Lines(new[] { "(O)a", "(O)b", "(O)c", "(O)d" }, model));
    }

    [Fact]
    public void Easy_never_stretches()
    {
        ItemAvailabilityModel model = Model(DifficultyStep.Easy, ("(O)a", 5, 2), ("(O)d", 13, 13));
        Assert.Empty(StretchRule.Lines(new[] { "(O)a", "(O)d" }, model));
        Assert.False(StretchRule.Applies(DifficultyStep.Easy));
    }
}
```

- [ ] **Step 2: Run, expect compile failures**

- [ ] **Step 3: Implement**

`ItemAvailabilityModel`: add `DifficultyStep step = DifficultyStep.Normal` as the last constructor parameter, store as `public DifficultyStep Step { get; }`. `ItemAvailabilityBuilder.Build` gains `DifficultyStep step = DifficultyStep.Normal` and passes it. `ModEntry` passes the same live step it resolves for the mode.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The stretch rule (spec 2026-08-28-obtainable-board, section 2). A bundle that gains
/// no reachable item in a season may have that season's gate reach an item whose hard week is
/// inside the season and whose pacing week is at most WindowWeeks past it. Never on Easy, never
/// into Winter. Pure: the same ingredients and model always give the same lines.</summary>
public static class StretchRule
{
    public const int WindowWeeks = 2;

    public static bool Applies(DifficultyStep step) => step != DifficultyStep.Easy;

    public static bool IsReachable(ItemAvailability a, Season season)
        => a.Week <= AvailabilityWeeks.LastWeekOf(season);

    public static bool IsStretchFor(ItemAvailability a, Season season)
        => season != Season.Winter
           && !IsReachable(a, season)
           && a.HardWeekOrPacing <= AvailabilityWeeks.LastWeekOf(season)
           && a.Week <= AvailabilityWeeks.LastWeekOf(season) + WindowWeeks;

    public static IReadOnlyDictionary<string, Season> Lines(IReadOnlyList<string> ingredients, ItemAvailabilityModel model)
    {
        if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));
        if (model == null) throw new ArgumentNullException(nameof(model));
        var lines = new Dictionary<string, Season>(StringComparer.Ordinal);
        if (!Applies(model.Step)) return lines;
        foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Fall })
        {
            bool gainsSomething = ingredients.Any(id =>
            {
                ItemAvailability a = model.For(id);
                bool now = IsReachable(a, season);
                bool before = season != Season.Spring && IsReachable(a, season - 1);
                return now && !before;
            });
            if (gainsSomething) continue;
            string? pick = ingredients
                .Where(id => !lines.ContainsKey(id) && IsStretchFor(model.For(id), season))
                .OrderBy(id => id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (pick != null) lines[pick] = season;
        }
        return lines;
    }
}
```

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump patch)

```bash
git commit -m "vX: StretchRule and the difficulty step on the availability model"
```

---

### Task 2: The ramp and the deadlines read the stretch lines; `BundleRequirement.StretchLines`

**Files:**
- Modify: `src/TheLongestYear.Core/BundleRequirement.cs` (new `IReadOnlyDictionary<string, Season> StretchLines`, empty by default, on both factory methods as an optional parameter), `src/TheLongestYear.Core/BundleClassifier.cs` (`RampFromItems` takes the lines; classifier computes lines once and passes them to `RampFromItems`, `BundleDeadlines.For` and the requirement), `src/TheLongestYear.Core/BundleDeadlines.cs` (a stretch line pins the item to its stretch season instead of clamping up to the gate)
- Test: `tests/TheLongestYear.Tests/BundleClassifierTests.cs`, `BundleDeadlinesTests.cs`

**Interfaces:**
- Produces: `BundleClassifier.RampFromItems(int numberOfSlots, IReadOnlyList<string> ingredients, ItemAvailabilityModel model, IReadOnlyDictionary<string, Season>? stretchLines = null)`; `BundleDeadlines.For(ingredients, model, stretchLines = null)`; `BundleRequirement.StretchLines`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void The_spring_ramp_counts_a_spring_stretch_line_as_reachable()
{
    // 4 of 6, nothing reachable in Spring, one Summer week 6 item with a Spring hard week.
    var model = ModelWith(("(O)a", 6, 1), ("(O)b", 6, 5), ("(O)c", 9, 9), ("(O)d", 9, 9), ("(O)e", 13, 13), ("(O)f", 13, 13));
    string[] ids = { "(O)a", "(O)b", "(O)c", "(O)d", "(O)e", "(O)f" };
    Assert.Equal(new[] { 0, 2, 3, 4 }, BundleClassifier.RampFromItems(4, ids, model));
    var lines = new Dictionary<string, Season> { ["(O)a"] = Season.Spring };
    Assert.Equal(new[] { 1, 2, 3, 4 }, BundleClassifier.RampFromItems(4, ids, model, lines));
}

[Fact]
public void A_stretch_line_pins_a_per_item_ingredient_to_its_stretch_season()
{
    var model = ModelWith(("(O)a", 6, 1), ("(O)b", 6, 5), ("(O)c", 13, 13));
    var lines = new Dictionary<string, Season> { ["(O)a"] = Season.Spring };
    IReadOnlyDictionary<string, Season> pins = BundleDeadlines.For(new[] { "(O)a", "(O)b", "(O)c" }, model, lines);
    Assert.Equal(Season.Spring, pins["(O)a"]);
    Assert.True(pins["(O)b"] >= Season.Summer);
}
```

(`ModelWith` is a small test helper building an `ItemAvailabilityModel` from `(id, week, hard)` tuples; add it to a shared test helper file if one exists, else to each test class.)

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`RampFromItems`: `int reachable = ingredients.Count(id => model.For(id).Gate <= season || (stretchLines != null && stretchLines.TryGetValue(id, out Season st) && st <= season));`. `BundleDeadlines.For`: after computing `deadline`, `if (stretchLines != null && stretchLines.TryGetValue(id, out Season stretch)) deadline = stretch; else if (availability.Gate > deadline) deadline = availability.Gate;`. Classifier: `var stretch = StretchRule.Lines(ingredients, availability);` computed once per bundle when `availability != null`, passed to both, stored on the requirement (`CreatePercentage(..., stretchLines: stretch)`, `CreatePerItem(..., stretchLines: stretch)`), and logged per bundle at board build (`"'{name}': stretch {id} for {season}"`).

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump patch)

---

### Task 3: The filler swaps in a stretch item, then a hard item; the foothold is retired

**Files:**
- Modify: `src/TheLongestYear.Core/BundleSlotFiller.cs` (replace the `springReady` block), `src/TheLongestYear.Core/SpringFoothold.cs` (delete), `src/TheLongestYear/Loop/BundleEngine.cs:243` (pass the model instead of the foothold predicate), `src/TheLongestYear.Core/EffortTiers.cs` (helper `IsHard(int effort)`)
- Test: `tests/TheLongestYear.Tests/BundleSlotFillerTests.cs` (replace the foothold tests)

**Interfaces:**
- Consumes: `StretchRule` (Task 1), `EffortTiers.Tier(int)` (plan 1).
- Produces: `BundleSlotFiller.Fill(..., ItemAvailabilityModel? availability = null)` replacing `Func<string, bool>? springReady`; `EffortTiers.IsHard(int effort) => Tier(effort) >= EffortTier.Hard`; `BundleSlotFiller.MinSlotsForHardItem = 4`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void A_bundle_with_nothing_for_spring_swaps_in_a_stretch_item_not_a_spring_item()
{
    // Pool: two Winter items, one Summer-week-6-with-Spring-hard item (stretch), one true Spring item.
    // Seed chosen so the raw roll takes the two Winter items and one other; assert the result holds the stretch item.
    ...build ItemPools with a single Fish domain, a BundleSpec of 3 slots, a model where "(O)s" is (6, hard 1), "(O)p" is (1, 1), "(O)w1" and "(O)w2" are (13, 13)...
    BundleSpec filled = BundleSlotFiller.Fill(spec, match, pools, tuning, new Random(1), availability: model);
    Assert.Contains(filled.Slots, s => s.ItemId == "(O)s");
}

[Fact]
public void Easy_gets_no_swap_at_all() { ...same pools, model built with DifficultyStep.Easy; assert the raw roll is unchanged... }

[Fact]
public void A_four_slot_bundle_without_a_hard_item_swaps_one_in()
{
    ...pool of five effort-2 items and one effort-7 item; 4 slots; seed whose raw roll misses the hard item...
    Assert.Contains(filled.Slots, s => model.For(s.ItemId).Effort >= 6);
}

[Fact]
public void A_three_slot_bundle_is_exempt_from_the_hard_item_rule() { ... }
```

Write these with real `ItemPools`/`PoolItem` construction copied from the existing `BundleSlotFillerTests` foothold tests, choosing seeds by running the raw roll once and asserting the pre-condition (`Assert.DoesNotContain` before the swap logic exists is the RED step).

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

Replace the foothold block in `Fill` with:

```csharp
if (availability != null && match.Season == null && StretchRule.Applies(availability.Step))
{
    // Stretch swap (spec section 2): for each season the chosen list gains nothing in, hold a
    // stretch item; swap the last non-reachable slot for one from the pool when it holds none.
    var chosenIds = new HashSet<string>(chosen.Select(c => c.ItemId), StringComparer.Ordinal);
    foreach (Season season in new[] { Season.Spring, Season.Summer, Season.Fall })
    {
        bool gains = chosen.Any(c => Gains(availability.For(c.ItemId), season));
        bool holdsStretch = chosen.Any(c => StretchRule.IsStretchFor(availability.For(c.ItemId), season));
        if (gains || holdsStretch) continue;
        List<PoolItem> stretchPool = candidates
            .Where(c => !chosenIds.Contains(c.ItemId) && StretchRule.IsStretchFor(availability.For(c.ItemId), season))
            .ToList();
        if (stretchPool.Count == 0) { log?.Invoke($"'{spec.Name}': no stretch item for {season} in its pool."); continue; }
        int victim = chosen.FindLastIndex(c => !StretchRule.IsReachable(availability.For(c.ItemId), season));
        if (victim < 0) continue;
        PoolItem pick = WeightedSampler.Sample(stretchPool, 1, rng)[0];
        chosenIds.Remove(chosen[victim].ItemId);
        chosen[victim] = pick;
        chosenIds.Add(pick.ItemId);
        log?.Invoke($"'{spec.Name}': swapped in {pick.ItemId} as a {season} stretch.");
    }
    // Hard-item rule (spec section 3): one effort-6-or-more item per bundle of 4 or more slots.
    if (targetCount >= MinSlotsForHardItem && !chosen.Any(c => EffortTiers.IsHard(availability.For(c.ItemId).Effort)))
    {
        List<PoolItem> hardPool = candidates.Where(c => !chosenIds.Contains(c.ItemId) && EffortTiers.IsHard(availability.For(c.ItemId).Effort)).ToList();
        if (hardPool.Count == 0) log?.Invoke($"'{spec.Name}': no hard item in its pool.");
        else
        {
            // Swap the easiest slot that is not a stretch line, so the stretch swap above survives.
            int victim = chosen.Select((c, i) => (c, i))
                .Where(p => !Enumerable.Range(0, 3).Any(s => StretchRule.IsStretchFor(availability.For(p.c.ItemId), (Season)s)))
                .OrderBy(p => availability.For(p.c.ItemId).Effort).Select(p => p.i).DefaultIfEmpty(-1).First();
            if (victim >= 0)
            {
                PoolItem pick = WeightedSampler.Sample(hardPool, 1, rng)[0];
                chosen[victim] = pick;
                log?.Invoke($"'{spec.Name}': swapped in {pick.ItemId} as the hard item (effort {availability.For(pick.ItemId).Effort}).");
            }
        }
    }
}
```

with `private static bool Gains(ItemAvailability a, Season s) => StretchRule.IsReachable(a, s) && (s == Season.Spring || !StretchRule.IsReachable(a, s - 1));`. Delete `SpringFoothold.cs` and every reference; `BundleEngine` passes `availability: _availability` (find the field the engine already holds for the model, it builds `SpringReady` from it today). Delete the foothold tests and `SpringFootholdTests.cs` if present.

Note on rng consumption: the stretch and hard swaps draw from `rng` after the main sample, exactly as the foothold did; boards regenerate identically because the model and step are fixed per loop.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump patch)

---

### Task 4: `tly_gatecheck` and the board dump report stretch lines, missing hard items and Spring-tight bundles

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (`LogGateAudit` near line 2251; `tly_dumpavailability` Due column shows `stretch` for a stretch line)
- Test: none in Core; verify by building the mod and reading the audit line format in the report

- [ ] **Step 1: Implement**

In `LogGateAudit`: replace the `[no spring foothold]` tag and its counter with three tags per bundle: `[stretch: {id} {season}]` for each entry in `req.StretchLines`; `[no hard item]` when `req.NumberOfSlots >= BundleSlotFiller.MinSlotsForHardItem`, the bundle is rolled (not season-named) and no ingredient has `EffortTiers.IsHard(_availability.For(id).Effort)`, on a step where the rule applies; `[spring tight]` when the Spring demand equals the Spring reachable count and both are above zero. The RESULT line counts each: `"{n} stretch line(s), {m} without a hard item, {k} Spring tight"`. In the dump, the Due column prints `stretch ({season})` for a stretch line.

- [ ] **Step 2: Build the mod** (`dotnet build src/TheLongestYear -c Release`), expect clean

- [ ] **Step 3: Commit** (bump patch)

---

### Task 5: Stretch items on the weekly cards from week 4, tagged

**Files:**
- Modify: `src/TheLongestYear.Core/GoalObtainability.cs` (overload taking the stretch season), `src/TheLongestYear.Core/SlotPoolBuilder.cs` (a stretch line is in play and due from `LastWeekOf(stretchSeason)`; `BonusSlot.Stretch = true`), `src/TheLongestYear.Core/BonusSlot.cs` (new `bool Stretch`), the HUD or quest text that lists goals (grep `WeeklyThemeQuestService` for where goal names are rendered) to append " (stretch)"
- Test: `tests/TheLongestYear.Tests/SlotPoolBuilderTests.cs` (find by grepping `OpenSlotsForTheme`)

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void A_spring_stretch_line_is_a_goal_from_spring_week_4()
{
    // requirement with StretchLines { "(O)s" -> Spring }, model where (O)s is week 6 hard 1
    IReadOnlyList<BonusSlot> week3 = SlotPoolBuilder.OpenSlotsForTheme(bundleData, _ => null, reqs, Theme.Mixed, Season.Spring, id => obtainable(id, 3), kindOf, weekOfYear: 3);
    IReadOnlyList<BonusSlot> week4 = SlotPoolBuilder.OpenSlotsForTheme(bundleData, _ => null, reqs, Theme.Mixed, Season.Spring, id => obtainable(id, 4), kindOf, weekOfYear: 4);
    Assert.DoesNotContain(week3, s => s.ItemId == "(O)s");
    BonusSlot slot = Assert.Single(week4, s => s.ItemId == "(O)s");
    Assert.True(slot.Stretch);
    Assert.True(slot.Due);
}
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`SlotPoolBuilder.OpenSlotsForTheme` gains `int weekOfYear` (callers in `RunController` pass the current week). For an ingredient in `req.StretchLines` with season `st`: it is in play and due when `weekOfYear >= AvailabilityWeeks.LastWeekOf(st)` regardless of the obtainability predicate (the predicate would say no until its pacing week), and the emitted `BonusSlot` has `Stretch = true`. Everything else is unchanged. Where the weekly quest lists goal names (`WeeklyThemeQuestService` or the HUD goal list), append `Strings.Get("goal.stretch-tag")` (" (stretch)") for `Stretch` slots; add the i18n key to `i18n/default.json`.

- [ ] **Step 4: Run all tests, build the mod, expect green and clean**

- [ ] **Step 5: Commit** (bump patch)

---

## Self-review

Spec section 2: cases 1 to 3 of the rule are Task 1 (lines) + Task 3 (swap); goals from week 4 tagged is Task 5; season-named exempt is the `match.Season == null` guard (Task 3) and `StretchRule.Lines` is only called for rolled bundles with a model (Task 2 computes for every classified bundle, which includes season-named ones: their lines will be empty because they gain something in their own season, and Winter never stretches; acceptable). Section 3 hard-item paragraph: Task 3 and Task 4's report. Easy: `StretchRule.Applies` gates Task 1 and Task 3; the foothold is deleted so Easy gets nothing. Section 9 tags: Task 4. Types: `StretchLines` is `IReadOnlyDictionary<string, Season>` everywhere; `Fill`'s new parameter is `ItemAvailabilityModel? availability`.
