# Obtainable Board, Plan 4 of 5: the Garden Pot keep and two Boosts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A permanent Garden Pot recipe keep (750 JP, Obtainability), and two in-loop Boosts bought at the farm's planning shrine: Year-Two Seeds (75 JP, this week: Mixed Seeds roll the season's year-2 crop at 5 percent) and Sneak Peek (100 JP, this season: the Queen of Sauce airs the year-2 episode for the week).

**Architecture:** The keep follows the Power-book pattern (`RunBaselineBuilder` collects it when owned, `FarmerReset` grants the crafting recipe after the rewind). Boosts are a small `BoostCatalog` in Core with `BoostPurchase.TryBuy(MetaState, RunState, id, weekOfYear)`; their state lives on `RunState` (`YearTwoSeedsWeek`, `SneakPeekSeason`), cleared by `BeginNewRun`; they are sold from the existing `ShrinePreviewMenu` (the in-world planning shrine), which gains a Boosts section with a Buy button; `MixedSeedsPatch` reads the week flag and a new `QueenOfSaucePatch` (Harmony postfix on `TV.getWeeklyRecipe`) reads the season flag. The full shrine-tabs redesign in TODO stays a later spec; this plan adds only what the two Boosts need.

**Tech Stack:** C#, Harmony (per-class discovery in `ModEntry.cs:183-205`), xunit, SMAPI.

**Spec:** `docs/superpowers/specs/2026-08-28-obtainable-board-design.md` section 8.

## Global Constraints

- No em dashes. Patch bump per commit, local commits only, stage only the task's files.
- Prices: Garden Pot keep 750 JP (`UpgradeCategory.Obtainability`); Year-Two Seeds 75 JP for the current week (`weekOfYear`); Sneak Peek 100 JP for the current season. Year-Two Seeds chance 5 percent (`0.05`). Year-2 seed ids: Garlic `(O)476` Spring, Red Cabbage `(O)485` Summer, Artichoke `(O)489` Fall; nothing in Winter (the boost cannot be bought in Winter).
- Spending JP in-loop uses `MetaState.JunimoPoints` through `BoostPurchase` only; `UpgradePurchase` stays for permanents.
- i18n keys: `upgrade.keep_garden_pot.name/desc`, `boost.year_two_seeds.name/desc`, `boost.sneak_peek.name/desc`, `shrine.boosts.header`, `shrine.boosts.buy`, `shrine.boosts.active`, in `i18n/default.json`.
- Sneak Peek only changes Sunday's episode (the year-2 episode is `num + 16` for `num` in 1 to 16); Wednesday reruns are untouched.

---

### Task 1: Garden Pot recipe keep

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs` (row after `pierre_year2_seeds`), `src/TheLongestYear.Core/RunBaseline.cs` (`KeptCraftingRecipes : List<string>`), `src/TheLongestYear.Core/RunBaselineBuilder.cs` (add "Garden Pot" when `state.HasUpgrade("keep_garden_pot")`), `src/TheLongestYear/Loop/FarmerReset.cs` (grant after `LearnDefaultRecipes` and the banked grants: `foreach (string r in baseline.KeptCraftingRecipes) if (!p.craftingRecipes.ContainsKey(r)) p.craftingRecipes[r] = 0;`), `src/TheLongestYear/i18n/default.json`
- Test: `tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs` (find by grepping `KeptBookStats`)

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void The_garden_pot_keep_puts_the_recipe_in_the_baseline()
{
    MetaState state = NewState();
    state.OwnedUpgrades.Add("keep_garden_pot");
    RunBaseline baseline = RunBaselineBuilder.Build(state, new RunState(), EmptyPeaks(), 500);
    Assert.Contains("Garden Pot", baseline.KeptCraftingRecipes);
    Assert.Empty(RunBaselineBuilder.Build(NewState(), new RunState(), EmptyPeaks(), 500).KeptCraftingRecipes);
}
```

- [ ] **Step 2: Run, expect failures**
- [ ] **Step 3: Implement** as listed; i18n: name "Keep: Garden Pot recipe", desc "The Garden Pot recipe is in your crafting book from day 1 of every loop (10 Stone, 1 Clay, 1 Refined Quartz). Any crop grows in a pot indoors in any season."
- [ ] **Step 4: Run all tests, build the mod, expect green and clean**
- [ ] **Step 5: Commit** (bump patch)

---

### Task 2: `BoostCatalog`, `BoostPurchase`, RunState flags

**Files:**
- Create: `src/TheLongestYear.Core/Boosts.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs` (`public int YearTwoSeedsWeek { get; set; } = -1; public int SneakPeekSeason { get; set; } = -1;`, both reset in `BeginNewRun`)
- Test: `tests/TheLongestYear.Tests/BoostsTests.cs`

**Interfaces:**
- Produces: `enum BoostId { YearTwoSeeds, SneakPeek }`; `BoostDefinition(BoostId Id, long Cost, string NameKey, string DescKey)`; `BoostCatalog.All`; `BoostPurchase.Result { Success, NotEnoughJp, AlreadyActive, NotAvailable }`; `BoostPurchase.TryBuy(MetaState meta, RunState run, BoostId id, int weekOfYear)`; `BoostState.YearTwoSeedsActive(RunState run, int weekOfYear)`; `BoostState.SneakPeekActive(RunState run, Season season)`; `YearTwoSeeds.SeedIdFor(Season season)` returns `"476"`, `"485"`, `"489"` or null for Winter; `YearTwoSeeds.Chance = 0.05`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Buying_year_two_seeds_marks_this_week_and_spends_75()
{
    var meta = new MetaState { JunimoPoints = 100 };
    var run = new RunState();
    Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.YearTwoSeeds, weekOfYear: 6));
    Assert.Equal(25, meta.JunimoPoints);
    Assert.True(BoostState.YearTwoSeedsActive(run, 6));
    Assert.False(BoostState.YearTwoSeedsActive(run, 7));
    Assert.Equal(BoostPurchase.Result.AlreadyActive, BoostPurchase.TryBuy(meta, run, BoostId.YearTwoSeeds, 6));
}

[Fact]
public void Year_two_seeds_cannot_be_bought_in_winter()
{
    var meta = new MetaState { JunimoPoints = 1000 };
    Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(meta, new RunState(), BoostId.YearTwoSeeds, weekOfYear: 14));
}

[Fact]
public void Sneak_peek_lasts_the_season_and_costs_100()
{
    var meta = new MetaState { JunimoPoints = 100 };
    var run = new RunState();
    Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.SneakPeek, weekOfYear: 10));
    Assert.True(BoostState.SneakPeekActive(run, Season.Fall));
    Assert.False(BoostState.SneakPeekActive(run, Season.Winter));
    Assert.Equal(0, meta.JunimoPoints);
}

[Fact]
public void A_new_run_clears_boosts()
{
    var run = new RunState { YearTwoSeedsWeek = 3, SneakPeekSeason = 2 };
    run.BeginNewRun(1);
    Assert.Equal(-1, run.YearTwoSeedsWeek);
    Assert.Equal(-1, run.SneakPeekSeason);
}

[Theory]
[InlineData(Season.Spring, "476")] [InlineData(Season.Summer, "485")] [InlineData(Season.Fall, "489")] [InlineData(Season.Winter, null)]
public void Seed_per_season(Season season, string? id) => Assert.Equal(id, YearTwoSeeds.SeedIdFor(season));
```

- [ ] **Step 2: Run, expect compile failures**
- [ ] **Step 3: Implement** `Boosts.cs` with the types above; `TryBuy` checks availability (Winter for seeds), active, JP, then spends and sets the flag (`YearTwoSeedsWeek = weekOfYear`, `SneakPeekSeason = (int)AvailabilityWeeks.SeasonOf(weekOfYear)`).
- [ ] **Step 4: Run all tests, expect green**
- [ ] **Step 5: Commit** (bump patch)

---

### Task 3: Buying Boosts at the planning shrine

**Files:**
- Modify: `src/TheLongestYear/UI/ShrinePreviewMenu.cs` (Boosts section: one row per `BoostCatalog.All` with name, description, cost, and a Buy button or "Active until ..." label; clicking calls a `Func<BoostId, BoostPurchase.Result>` the menu receives), `src/TheLongestYear/UI/PlanningShrineService.cs` (pass a purchase callback that calls `BoostPurchase.TryBuy(_store.State, _store.Run, id, _store.Run.WeekOfYear)`, saves, plays the purchase sound, and shows a HUD message on failure), `src/TheLongestYear/i18n/default.json`
- Test: build the mod; smoke in game (open the shrine on the Rodger save with JP set by `tly_jp` or the existing debug JP command, buy both, confirm the HUD and the `RunState` flags via `tly_runstate`)

- [ ] **Step 1: Implement** following the menu's existing row layout. Keep the read-only rows as they are; the Boosts section sits above them.
- [ ] **Step 2: Build, deploy, smoke; note the log lines.**
- [ ] **Step 3: Commit** (bump patch)

---

### Task 4: Year-Two Seeds in `MixedSeedsPatch`

**Files:**
- Modify: `src/TheLongestYear/Loop/MixedSeedsPatch.cs` (a static hook `BoostChecker.YearTwoSeedsActive : Func<bool>` set by ModEntry to `() => BoostState.YearTwoSeedsActive(_meta.Run, _meta.Run.WeekOfYear)`; in the postfix, after the existing cult check: `if (BoostChecker.YearTwoSeedsActive?.Invoke() == true) { string? seed = YearTwoSeeds.SeedIdFor(ToCoreSeason(location.GetSeason())); if (seed != null && Game1.random.NextDouble() < YearTwoSeeds.Chance) __result = seed; }`), `src/TheLongestYear/ModEntry.cs` (set and clear the hook beside `UpgradeChecker.HasUpgrade`)
- Test: `tests/TheLongestYear.Tests/BoostsTests.cs` covers `SeedIdFor`; the patch itself is smoke-tested: with the boost active in Summer, `debug season summer`, plant 40 Mixed Seeds, `debug growcrops 30`, count Red Cabbage (expect about 2).

- [ ] **Step 1: Implement**; **Step 2: build, deploy, smoke; Step 3: commit** (bump patch)

---

### Task 5: Sneak Peek: `QueenOfSaucePatch`

**Files:**
- Create: `src/TheLongestYear/Loop/QueenOfSaucePatch.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (hook `BoostChecker.SneakPeekActive : Func<bool>`)

- [ ] **Step 1: Implement**

```csharp
/// <summary>Sneak Peek Boost (spec 2026-08-28-obtainable-board, section 8): for the season it was
/// bought in, the Sunday Queen of Sauce airs the year-2 episode for the week (episode + 16), so
/// every year-2 dish has a year-1 route. Wednesday reruns are untouched. Patches
/// TV.getWeeklyRecipe (protected virtual): DaysPlayed % 224 / 7 is the week, 1 to 16 in year 1.</summary>
[HarmonyPatch(typeof(StardewValley.Objects.TV), "getWeeklyRecipe", new System.Type[0])]
internal static class QueenOfSaucePatch
{
    private const int YearOneEpisodes = 16;
    private const int CycleDays = 224;

    private static void Postfix(StardewValley.Objects.TV __instance, ref string[] __result)
    {
        if (BoostChecker.SneakPeekActive?.Invoke() != true) return;
        if (Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth) == "Wed") return;
        int week = (int)(Game1.stats.DaysPlayed % CycleDays / 7);
        if (week < 1 || week > YearOneEpisodes) return;
        var channel = DataLoader.Tv_CookingChannel(Game1.temporaryContent);
        string key = (week + YearOneEpisodes).ToString();
        if (!channel.ContainsKey(key)) return;
        string[] replaced = AccessTools.Method(typeof(StardewValley.Objects.TV), "getWeeklyRecipe", new[] { typeof(Dictionary<string, string>), typeof(string) })
            .Invoke(__instance, new object[] { channel, key }) as string[];
        if (replaced != null) __result = replaced;
    }
}
```

(The private two-argument `getWeeklyRecipe(channelData, id)` overload does the dialogue and recipe grant; calling it through `AccessTools` reuses vanilla's own logic.)

- [ ] **Step 2: Build, deploy, smoke**: buy Sneak Peek on the Rodger save in Spring, `debug day 7` (a Sunday), watch TV (`tly` has no TV command: use `debug ...` to warp home and interact is mouse work, so instead verify through the log by adding a `Monitor.Log` in the postfix at Trace level naming the episode swapped), confirm the year-2 recipe (Pizza for week 1) lands in `Game1.player.cookingRecipes` via `tly_runstate` or a `debug` dump.
- [ ] **Step 3: Commit** (bump patch)

---

### Task 6: Dishes and crops know the Boost routes

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/CookedDishAvailability.cs` (`RecipeWeek`: a year-2 episode is placed at its week `episode - 16` with basis "year-2 episode, Sneak Peek Boost" instead of null, on Normal and above; still null on Easy), `src/TheLongestYear.Core/AvailabilityWeeks.cs` (the year-2 crop pacing rows Garlic 3, Red Cabbage 7, Artichoke 11 in `SeedSourceWeeks` with the Boost note), the goal card tag: `BonusSlot.RouteTag` ("Boost: Year-Two Seeds" / "Boost: Sneak Peek") appended like the stretch tag
- Test: extend `RecipeTimingTests` (Blackberry Cobbler week 10 on Normal, null on Easy) and `CropForageWeekTests`

- [ ] **Step 1: Write the failing tests; Step 2: run; Step 3: implement; Step 4: green; Step 5: commit** (bump patch)

---

## Self-review

Section 8: keep (Task 1), Year-Two Seeds (Tasks 2, 3, 4), Sneak Peek (Tasks 2, 3, 5), the availability side of both routes (Task 6). The step (Easy vs Normal) reaches `RecipeWeek` through `EffortData`? No: add a `DifficultyStep step` parameter to `CookedDishAvailability.Derive` and `RecipeWeek`, passed from `EffortComposer` (which gets it from the builder's `step`). Types: `BoostId`, `BoostPurchase.Result`, `BoostState`, `YearTwoSeeds` defined once in Task 2 and used by Tasks 3 to 5.
