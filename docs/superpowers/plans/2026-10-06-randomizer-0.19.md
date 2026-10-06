# Randomizer 0.19.0 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the first seven Randomizer options (spec build steps 1 to 5) as 0.19.0: theme rerolls Off / Costs JP / Free, random theme items, random buff/debuff pairings, random weekly JP multiplier, mystery card, random bundle rewards, random cart days.

**Architecture:** Every rule is a pure static in `TheLongestYear.Core` (testable, seeded from `RunState.Seed` and the week with its own salt). The game layer reads a per-week snapshot of the Randomizer settings stored on `RunState`, so flipping a setting mid-week only changes the next weekly offer. Board-level options (bundle rewards) snapshot onto `MetaState` when a board is built.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony, xUnit 2.4.1, GMCM.

**Spec:** `docs/superpowers/specs/2026-09-28-randomizer-design.md` (sections 1 to 5, 9, 10; sections 6, 7, 8 are OUT of this plan and ship later).

## Global Constraints

- Branch `randomizer`, worktree `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear-randomizer`. Do NOT change `manifest.json` `Version` on this branch; master owns versions (0.19.0 is set at merge).
- Every option is OFF by default. With every option off the game must behave exactly as 0.18.145 (same offer, same goals, same drawbacks, same JP, same cart days, same rewards).
- Only existing switch migrates: `EnableThemeReroll = true` becomes `Rerolls = Free`; false becomes `Off`.
- Weekly options take effect from the next weekly offer; bundle rewards take effect at the next board build (next loop, or the new game's first board).
- All rolls deterministic per save: `new Random(seed ^ (weekOfYear * 7919) ^ salt)` with a distinct salt per rule (existing salts in use: theme `*1031`, reroll counter `*1399`, flavor `0x5C2F`).
- Testable logic lives in Core (the test project references Core only).
- Player-facing strings (GMCM names/tooltips, hub labels, HUD lines) go through the `game-writing` skill before they are written. No em dashes in any string or doc.
- i18n: every key in `src/TheLongestYear/i18n/default.json` must be used by a literal `Strings.Get("...")` (I18nGuardTests); enum display names use one literal call per value, like `FormatDifficultyStep` (`ModEntry.cs:5483`).
- Test command: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`. Build: `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo` (Debug; Release auto-deploys to the game, an IOException there means the game is running).
- Commit after each task (small commits, push `origin randomizer` after each commit). End commit messages with the session attribution lines.

## Review Focus

1. **Everything off = 0.18.145.** A save with all Randomizer options off must roll the identical offer, goals, drawback, payouts, cart days and rewards. Each task adds a "default off is unchanged" test for its rule.
2. **Mid-week setting flip.** Turning an option on or off mid-week must not change the current week's goals, drawback, multiplier or cart days. Task 1's snapshot test pins this; tasks 4, 5, 7 read only the snapshot.
3. **Old saves.** A save from 0.18.x (no new RunState/MetaState fields) loads with every new field at its default and behaves as off. RunState JSON round-trip tests per new field.
4. **Reload of an Engine board with shuffled rewards.** `ResolveRequirements` compares the reward field (`EngineManifestCheck`), so the reload re-derivation must reproduce the same rewards. Task 6 makes the shuffle a function of the board seed plus the stored MetaState flag, never of live config.
5. **Cart week with no legal day.** A week whose every day is a festival must not loop forever or force a cart onto a festival; forcing picks the first non-festival day, and a week with none simply has no cart. Task 7 tests it.

---

### Task 1: Randomizer settings block, migration, weekly snapshot, GMCM section

**Files:**
- Create: `src/TheLongestYear.Core/RandomizerSettings.cs`
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs:155` (keep `EnableThemeReroll` for migration only; add `Randomizer`)
- Modify: `src/TheLongestYear.Core/RunState.cs` (snapshot fields; clear in `BeginNewRun`)
- Modify: `src/TheLongestYear/ModEntry.cs:115-139` (migration call), `:2305-2309` (remove old reroll bool option), new section after the difficulty block (~2405, before `"Registered GMCM options."`)
- Modify: `src/TheLongestYear/UI/WeeklyHubMenu.cs:337,352,359,415` (read `Rerolls != Off` from the snapshot instead of `_config.EnableThemeReroll`)
- Modify: `src/TheLongestYear/i18n/default.json` (remove `gmcm.theme-reroll.*`, add `gmcm.randomizer.*`)
- Test: `tests/TheLongestYear.Tests/RandomizerSettingsTests.cs`

**Interfaces:**
- Produces:
  - `public enum RerollMode { Off, CostsJp, Free }`
  - `public sealed class RandomizerSettings { RerollMode Rerolls; bool RandomThemeItems; bool RandomPairings; bool RandomMultiplier; bool MysteryCard; bool RandomBundleRewards; bool RandomCartDays; RandomizerSettings Clone(); }`
  - `GameplayConfig.Randomizer` (`RandomizerSettings`, `= new()`)
  - `public static bool RandomizerMigration.Apply(GameplayConfig config)` returns true if it changed anything
  - `RunState.RandomizerWeek` (`int`, default -1), `RunState.RandomizerSnapshot` (`RandomizerSettings?`)
  - `public static RandomizerSettings RunState.RandomizerFor(int weekOfYear, RandomizerSettings live)`: returns the stored snapshot when `RandomizerWeek == weekOfYear`, otherwise stores `live.Clone()` for that week and returns it.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RandomizerSettingsTests
{
    [Fact]
    public void Every_option_is_off_by_default()
    {
        var r = new GameplayConfig().Randomizer;
        Assert.Equal(RerollMode.Off, r.Rerolls);
        Assert.False(r.RandomThemeItems || r.RandomPairings || r.RandomMultiplier
            || r.MysteryCard || r.RandomBundleRewards || r.RandomCartDays);
    }

    [Fact]
    public void An_old_reroll_switch_that_was_on_becomes_free()
    {
        var c = new GameplayConfig { EnableThemeReroll = true };
        Assert.True(RandomizerMigration.Apply(c));
        Assert.Equal(RerollMode.Free, c.Randomizer.Rerolls);
        Assert.False(c.EnableThemeReroll);
        Assert.False(RandomizerMigration.Apply(c)); // one shot
    }

    [Fact]
    public void An_old_reroll_switch_that_was_off_changes_nothing()
    {
        var c = new GameplayConfig();
        Assert.False(RandomizerMigration.Apply(c));
        Assert.Equal(RerollMode.Off, c.Randomizer.Rerolls);
    }

    [Fact]
    public void A_setting_flipped_mid_week_waits_for_the_next_week()
    {
        var run = new RunState();
        var live = new RandomizerSettings();
        Assert.False(run.RandomizerFor(5, live).RandomPairings);
        live.RandomPairings = true;
        Assert.False(run.RandomizerFor(5, live).RandomPairings); // same week: snapshot holds
        Assert.True(run.RandomizerFor(6, live).RandomPairings);  // next week: new snapshot
    }

    [Fact]
    public void The_snapshot_survives_a_save_round_trip()
    {
        var run = new RunState();
        run.RandomizerFor(3, new RandomizerSettings { RandomMultiplier = true, Rerolls = RerollMode.CostsJp });
        var back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(run))!;
        Assert.Equal(3, back.RandomizerWeek);
        Assert.True(back.RandomizerFor(3, new RandomizerSettings()).RandomMultiplier);
        Assert.Equal(RerollMode.CostsJp, back.RandomizerSnapshot!.Rerolls);
    }

    [Fact]
    public void An_old_save_without_a_snapshot_takes_the_live_settings()
    {
        var back = JsonSerializer.Deserialize<RunState>("{}")!;
        Assert.True(back.RandomizerFor(1, new RandomizerSettings { RandomCartDays = true }).RandomCartDays);
    }

    [Fact]
    public void A_new_run_forgets_the_snapshot()
    {
        var run = new RunState();
        run.RandomizerFor(9, new RandomizerSettings { MysteryCard = true });
        run.BeginNewRun(123);
        Assert.Equal(-1, run.RandomizerWeek);
        Assert.Null(run.RandomizerSnapshot);
    }
}
```

- [ ] **Step 2: Run to verify they fail** (compile errors: `RandomizerSettings` not defined).

- [ ] **Step 3: Implement**

`src/TheLongestYear.Core/RandomizerSettings.cs`:
```csharp
namespace TheLongestYear.Core;

/// <summary>How the weekly theme reroll button behaves. Spec 2026-09-28-randomizer-design, section 1.</summary>
public enum RerollMode { Off, CostsJp, Free }

/// <summary>The Randomizer section of the settings. Every option trades the shipped balance for variety,
/// and every option is off by default, so a player who never opens the section plays the balanced game.
/// Weekly options are read through <see cref="RunState.RandomizerFor"/>, never live, so a flip mid-week
/// waits for the next weekly offer.</summary>
public sealed class RandomizerSettings
{
    public RerollMode Rerolls { get; set; } = RerollMode.Off;
    public bool RandomThemeItems { get; set; }
    public bool RandomPairings { get; set; }
    public bool RandomMultiplier { get; set; }
    public bool MysteryCard { get; set; }
    public bool RandomBundleRewards { get; set; }
    public bool RandomCartDays { get; set; }

    public RandomizerSettings Clone() => (RandomizerSettings)MemberwiseClone();
}

public static class RandomizerMigration
{
    /// <summary>The old on/off reroll switch becomes Free; it is cleared so this runs once.</summary>
    public static bool Apply(GameplayConfig config)
    {
        if (!config.EnableThemeReroll) return false;
        config.Randomizer ??= new RandomizerSettings();
        config.Randomizer.Rerolls = RerollMode.Free;
        config.EnableThemeReroll = false;
        return true;
    }
}
```

`GameplayConfig.cs`: change the doc comment on `EnableThemeReroll` to "Legacy: read once by RandomizerMigration, then always false. Kept so an old config.json's value can still be read." Add:
```csharp
/// <summary>Randomizer section: all off by default.</summary>
public RandomizerSettings Randomizer { get; set; } = new();
```

`RunState.cs` (next to the reroll fields, same style):
```csharp
/// <summary>The week <see cref="RandomizerSnapshot"/> was taken for; -1 when none.</summary>
public int RandomizerWeek { get; set; } = -1;
/// <summary>The Randomizer settings as they stood when this week's offer was first shown.</summary>
public RandomizerSettings? RandomizerSnapshot { get; set; }

/// <summary>This week's Randomizer settings. The first call in a week stores a copy of
/// <paramref name="live"/>; later calls that week return the copy, so a setting changed mid-week
/// waits for the next weekly offer.</summary>
public RandomizerSettings RandomizerFor(int weekOfYear, RandomizerSettings live)
{
    if (RandomizerWeek == weekOfYear && RandomizerSnapshot != null) return RandomizerSnapshot;
    RandomizerWeek = weekOfYear;
    RandomizerSnapshot = (live ?? new RandomizerSettings()).Clone();
    return RandomizerSnapshot;
}
```
In `BeginNewRun`, add `RandomizerWeek = -1; RandomizerSnapshot = null;`.

- [ ] **Step 4: Run tests, verify pass.**

- [ ] **Step 5: Game glue**
  - `ModEntry.Entry` (after the stash-tile block, `:131`): `if (RandomizerMigration.Apply(_config)) { migrated = true; this.Monitor.Log("Migrated config.json: theme reroll switch moved to Randomizer > Rerolls = Free.", LogLevel.Info); }` (the existing `migrated` log stays).
  - Add a convenience on `RunController`: `internal RandomizerSettings Randomizer => Run.RandomizerFor(Run.WeekOfYear, _config.Randomizer);` and `internal RandomizerSettings RandomizerForWeek(int week) => Run.RandomizerFor(week, _config.Randomizer);`. For the day-28 pre-pick hub (offer week = WeekOfYear + 1) the hub must read `_config.Randomizer` live (do NOT store a snapshot for a future week, it would be overwritten by the current week's next call); add `RandomizerSettings Live => _config.Randomizer` and in `WeeklyHubMenu` use `_isPreSelectForNextMonth ? config.Randomizer : runController.Randomizer`, held in a field `_rand` set in the constructor.
  - `WeeklyHubMenu`: replace the four `_config.EnableThemeReroll` reads with `_rand.Rerolls != RerollMode.Off`.
  - GMCM: delete the `gmcm.theme-reroll` bool option. After the difficulty block add `AddSectionTitle(gmcm.randomizer.section)`, `AddParagraph(gmcm.randomizer.blurb)`, then:
    - Rerolls: `AddTextOption` with `allowedValues: new[] { "Off", "CostsJp", "Free" }`, `getValue: () => _config.Randomizer.Rerolls.ToString()`, `setValue: v => _config.Randomizer.Rerolls = Enum.TryParse(v, out RerollMode m) ? m : RerollMode.Off`, `formatAllowedValue: FormatRerollMode` (new static next to `FormatDifficultyStep`, one literal `Strings.Get` per value: `gmcm.randomizer.rerolls.off|costs-jp|free`).
    - Six `AddBoolOption`s: `random-theme-items`, `random-pairings`, `random-multiplier`, `mystery-card`, `random-bundle-rewards`, `random-cart-days`, each `gmcm.randomizer.<x>.name` / `.tooltip`.
  - i18n: draft every new string with the `game-writing` skill. Tooltips must say when the option takes effect (weekly ones: "from next week's offer"; bundle rewards: "from the next loop"). The Rerolls tooltip states the cost: 50 JP, doubling each reroll, back to 50 each week. Remove `gmcm.theme-reroll.name` / `.tooltip`.

- [ ] **Step 6: Build + full test run** (I18nGuardTests must pass).

- [ ] **Step 7: Commit + push**
```bash
git add src/TheLongestYear.Core/RandomizerSettings.cs src/TheLongestYear.Core/GameplayConfig.cs src/TheLongestYear.Core/RunState.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/UI/WeeklyHubMenu.cs src/TheLongestYear/i18n/default.json tests/TheLongestYear.Tests/RandomizerSettingsTests.cs
git commit -m "Randomizer settings section, weekly snapshot, reroll switch migrates to Free"
git push origin randomizer
```

---

### Task 2: Rerolls that cost JP

**Files:**
- Create: `src/TheLongestYear.Core/RerollPricing.cs`
- Modify: `src/TheLongestYear/UI/WeeklyHubMenu.cs` (`RerollOffer` ~557, `DrawRerollButton` ~678, `receiveLeftClick` ~528, constructor gets a JP accessor)
- Modify: `src/TheLongestYear/UI/MenuLauncher.cs:39-64` (pass the accessor)
- Modify: `src/TheLongestYear/i18n/default.json`
- Test: `tests/TheLongestYear.Tests/RerollPricingTests.cs`

**Interfaces:**
- Consumes: `RerollMode`, `RunState.RerollCount` / `RerollWeek` (existing; `RerollCount` is the number of rerolls already made this week).
- Produces: `public static long RerollPricing.CostOf(RerollMode mode, int rerollsAlreadyThisWeek)`; `public const long RerollPricing.BaseCost = 50`.

- [ ] **Step 1: Failing tests**
```csharp
public class RerollPricingTests
{
    [Theory]
    [InlineData(0, 50)] [InlineData(1, 100)] [InlineData(2, 200)] [InlineData(5, 1600)]
    public void Costs_jp_doubles_each_reroll_from_fifty(int already, long cost)
        => Assert.Equal(cost, RerollPricing.CostOf(RerollMode.CostsJp, already));

    [Fact]
    public void Free_rerolls_cost_nothing() => Assert.Equal(0, RerollPricing.CostOf(RerollMode.Free, 7));

    [Fact]
    public void A_huge_count_never_overflows()
        => Assert.Equal(RerollPricing.MaxCost, RerollPricing.CostOf(RerollMode.CostsJp, 80));
}
```

- [ ] **Step 2: Verify fail.**

- [ ] **Step 3: Implement**
```csharp
namespace TheLongestYear.Core;

/// <summary>Spec section 1. Flat on purpose, not season-scaled (Jeff, 2026-09-28): in Spring one reroll
/// costs more than the week's whole bonus, so paid rerolls are a real choice.</summary>
public static class RerollPricing
{
    public const long BaseCost = 50;
    /// <summary>Doubling stops here so a long reroll streak cannot overflow.</summary>
    public const long MaxCost = 1_000_000;
    private const int MaxDoublings = 40;

    public static long CostOf(RerollMode mode, int rerollsAlreadyThisWeek)
    {
        if (mode != RerollMode.CostsJp) return 0;
        int n = System.Math.Clamp(rerollsAlreadyThisWeek, 0, MaxDoublings);
        return System.Math.Min(MaxCost, BaseCost << n);
    }
}
```

- [ ] **Step 4: Verify pass.**

- [ ] **Step 5: Hub glue**
  - The hub needs the player's JP. Add constructor parameter `Func<long>? getJp = null, Action<long>? spendJp = null` (MenuLauncher passes `() => store.State.JunimoPoints` and `n => store.State.JunimoPoints -= n` from the `MetaStore` it already reaches through ModEntry; find how `WeeklyThemeQuestService` gets `_store`).
  - Cost this reroll: `long cost = RerollPricing.CostOf(_rand.Rerolls, _run.RerollWeek == OfferWeek ? _run.RerollCount : 0);`
  - `receiveLeftClick` on the button: if `cost > getJp()` play `Game1.playSound("cancel")` and return; else `spendJp(cost)` then `RerollOffer()`. `RerollForDebug()` (`tly_reroll`) stays free.
  - `DrawRerollButton`: label `menu.hub.reroll-cost` with `{{cost}}` when `cost > 0`, otherwise the existing labels; draw the label and box at 50% alpha (`Color.White * 0.5f`) when unaffordable.
  - New strings through `game-writing`.

- [ ] **Step 6: Build + full tests. Step 7: Commit + push** (`"Rerolls can cost JP: 50, doubling each reroll, reset weekly"`).

---

### Task 3: Random theme items

**Files:**
- Modify: `src/TheLongestYear.Core/GoalSamplingRules.cs` (record gains `bool Even = false`)
- Modify: `src/TheLongestYear.Core/BonusSlotSampler.cs:87-103`
- Modify: `src/TheLongestYear/Loop/RunController.cs:1177` (`RulesFor`)
- Test: `tests/TheLongestYear.Tests/BonusSlotSamplerEvenTests.cs`

**Interfaces:**
- Produces: `GoalSamplingRules(Season Season, int FillerAllowance, Func<string,int?> EffortOf, bool Even = false)`.

Rule: in even mode every open id has weight 1 (no effort table, so Spring can draw an Extreme id), there is no due-first tier and no filler allowance/one-filler-per-bundle split: one `Take` over all ids with `filler: false, dueOnly: false`. Kept: obtainability (already applied to the pool), `remainingNeedForBundle`, group caps, `maxCount`, and the early-week `EarlyGameAvoid` drop.

- [ ] **Step 1: Failing tests** (reuse the helper shapes `Slot(id, bundle, line, due)` from `BonusSlotSamplerRulesTests.cs`; copy them into the new file)
```csharp
public class BonusSlotSamplerEvenTests
{
    private static BonusSlot Slot(string id, int bundle, int line, bool due = false)
        => new() { ItemId = id, BundleIndex = bundle, IngredientIndex = line, Due = due };
    private static Rarity Common(string _) => Rarity.Common;
    private static GoalSamplingRules Even(Season s) => new(s, 0, id => id.StartsWith("X") ? 10 : 1, Even: true);

    [Fact]
    public void Even_mode_can_draw_an_extreme_item_in_spring()
    {
        var pool = new[] { Slot("X1", 1, 0), Slot("E1", 2, 0) };
        bool sawExtreme = Enumerable.Range(1, 200).Any(seed =>
            BonusSlotSampler.SampleSlots(seed, 3, Theme.Mining, pool, Common, 1, rules: Even(Season.Spring))
                .Any(s => s.ItemId == "X1"));
        Assert.True(sawExtreme);
    }

    [Fact]
    public void Even_mode_does_not_put_due_lines_first()
    {
        var pool = new[] { Slot("D1", 1, 0, due: true), Slot("F1", 2, 0), Slot("F2", 3, 0), Slot("F3", 4, 0) };
        bool sawNoDue = Enumerable.Range(1, 200).Any(seed =>
            BonusSlotSampler.SampleSlots(seed, 3, Theme.Mining, pool, Common, 1, rules: Even(Season.Summer))
                .All(s => !s.Due));
        Assert.True(sawNoDue);
    }

    [Fact]
    public void Even_mode_keeps_the_bundle_need_cap()
    {
        var pool = new[] { Slot("A", 1, 0), Slot("B", 1, 1), Slot("C", 1, 2) };
        var got = BonusSlotSampler.SampleSlots(1, 3, Theme.Mining, pool, Common, 3,
            remainingNeedForBundle: _ => 1, rules: Even(Season.Summer));
        Assert.Single(got);
    }

    [Fact]
    public void Even_mode_keeps_group_caps()
    {
        var pool = new[] { Slot("T1", 1, 0), Slot("T2", 2, 0), Slot("Z", 3, 0) };
        var caps = new[] { new GoalGroupCap(new HashSet<string> { "T1", "T2" }, 1) };
        var got = BonusSlotSampler.SampleSlots(1, 3, Theme.Mining, pool, Common, 3, caps: caps, rules: Even(Season.Summer));
        Assert.True(got.Count(s => s.ItemId.StartsWith("T")) <= 1);
    }

    [Fact]
    public void Default_rules_are_unchanged()
    {
        var pool = new[] { Slot("D1", 1, 0, due: true), Slot("F1", 2, 0) };
        var a = BonusSlotSampler.SampleSlots(7, 3, Theme.Mining, pool, Common, 1, rules: new(Season.Spring, 0, _ => 1));
        Assert.All(a, s => Assert.True(s.Due));
    }
}
```
(Check `BonusSlot`'s real property names and `Rarity`/`Theme` namespaces against `BonusSlotSamplerRulesTests.cs` and adjust the helper only.)

- [ ] **Step 2: Verify fail. Step 3: Implement** in the rules branch of `SampleSlots`:
```csharp
if (rules.Even)
{
    var all = idPool.ToList(); // ids after the early-week avoid filter, before weighting
    draw.TakeEven(all, maxCount);
    return draw.Result;
}
```
Add to `DrawState` a `TakeEven(IReadOnlyList<string> ids, int max)` that runs the existing weighted walk with every weight 1, `filler: false`, `dueOnly: false`, honouring `Allowed` and caps (reuse `Take` by passing a weight map of 1s if `Take` takes weights from a dictionary; read `DrawState` first and use whichever is smaller). Keep the rng consumption of the non-even path untouched.

- [ ] **Step 4: Verify pass.**

- [ ] **Step 5: Glue.** `RunController.RulesFor(season)` becomes `RulesFor(season, int weekOfYear)` and passes `Even: RandomizerForWeek(weekOfYear).RandomThemeItems`; update its callers (`SampleSlotsForTheme` passes its `weekOfYear`). For the pre-pick week (WeekOfYear + 1) read `_config.Randomizer` live, same rule as Task 1: add `private RandomizerSettings RandomizerForWeekNoStore(int week) => week == Run.WeekOfYear ? Randomizer : _config.Randomizer;` and use that everywhere outside the hub.

- [ ] **Step 6: Build + full tests. Step 7: Commit + push** (`"Random theme items: weekly goals drawn evenly when the option is on"`).

---

### Task 4: Random buff/debuff pairings

**Files:**
- Create: `src/TheLongestYear.Core/RandomPairing.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs` (`CurrentLiabilityId`)
- Modify call sites of `ThemeModifiers.For(...)`: `RunController.cs:217-218, 878-879, 1069-1070`; `WeeklyThemeQuestService.cs:86, 250`; `WeeklyHubMenu.cs:761`
- Test: `tests/TheLongestYear.Tests/RandomPairingTests.cs`

**Interfaces:**
- Produces:
  - `public static IReadOnlyList<string> RandomPairing.AllLiabilities` (the eight ids, in `Theme` order of `ThemeModifiers.For`)
  - `public static IReadOnlySet<string> RandomPairing.ExcludedFor(Theme t)`
  - `public static string RandomPairing.LiabilityFor(int seed, int weekOfYear, Theme theme, bool random)` (returns `ThemeModifiers.For(theme).LiabilityId` when `random` is false)
  - `RunState.CurrentLiabilityId` (`string?`): set at selection, null means "theme's own".
  - `public static (string BonusId, string LiabilityId) RandomPairing.EffectsFor(RunState run, Theme theme)` returns `(ThemeModifiers.For(theme).BonusId, run.CurrentLiabilityId ?? ThemeModifiers.For(theme).LiabilityId)`

Exclusions (spec table): Foraging `forage_off`; Farming `crop_growth_down`; Fishing `fish_bite_down`; Mining and Spelunking `mines_closed`; Artisan `machines_slow`; Kitchen, Mixed none. Salt: `0x2B9D`.

- [ ] **Step 1: Failing tests**
```csharp
public class RandomPairingTests
{
    [Fact]
    public void There_are_eight_drawbacks()
        => Assert.Equal(8, RandomPairing.AllLiabilities.Distinct().Count());

    [Theory]
    [InlineData(Theme.Foraging, "forage_off")]
    [InlineData(Theme.Farming, "crop_growth_down")]
    [InlineData(Theme.Fishing, "fish_bite_down")]
    [InlineData(Theme.Mining, "mines_closed")]
    [InlineData(Theme.Spelunking, "mines_closed")]
    [InlineData(Theme.Artisan, "machines_slow")]
    public void A_theme_never_draws_the_drawback_that_blocks_its_own_goals(Theme t, string blocked)
    {
        for (int seed = 0; seed < 500; seed++)
            Assert.NotEqual(blocked, RandomPairing.LiabilityFor(seed, 4, t, random: true));
    }

    [Fact]
    public void Random_pairings_reach_more_than_one_drawback()
        => Assert.True(Enumerable.Range(0, 200).Select(s => RandomPairing.LiabilityFor(s, 4, Theme.Kitchen, true)).Distinct().Count() > 3);

    [Fact]
    public void Off_returns_the_themes_own_drawback()
    {
        foreach (Theme t in Enum.GetValues<Theme>())
            Assert.Equal(ThemeModifiers.For(t).LiabilityId, RandomPairing.LiabilityFor(99, 4, t, random: false));
    }

    [Fact]
    public void Same_seed_week_and_theme_give_the_same_drawback()
        => Assert.Equal(RandomPairing.LiabilityFor(5, 7, Theme.Mixed, true), RandomPairing.LiabilityFor(5, 7, Theme.Mixed, true));

    [Fact]
    public void Effects_use_the_stored_drawback_and_fall_back_to_the_theme()
    {
        var run = new RunState();
        Assert.Equal(ThemeModifiers.For(Theme.Fishing), RandomPairing.EffectsFor(run, Theme.Fishing));
        run.CurrentLiabilityId = "machines_slow";
        Assert.Equal((ThemeModifiers.For(Theme.Fishing).BonusId, "machines_slow"), RandomPairing.EffectsFor(run, Theme.Fishing));
    }

    [Fact]
    public void An_old_save_has_no_stored_drawback()
        => Assert.Null(System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!.CurrentLiabilityId);
}
```
(If `Theme` has members beyond the eight, `Off_returns...` must only iterate themes `ThemeModifiers.For` supports; check `Theme.cs`.)

- [ ] **Step 2: Verify fail. Step 3: Implement**
```csharp
namespace TheLongestYear.Core;

/// <summary>Spec section 3: a card keeps its theme's buff and draws a random drawback, never one that
/// blocks the theme's own goals.</summary>
public static class RandomPairing
{
    private const int Salt = 0x2B9D;

    public static readonly IReadOnlyList<string> AllLiabilities = new[]
    {
        "mines_closed", "fish_bite_down", "crop_growth_down", "forage_off",
        "all_sell_prices_down", "machines_slow", "cooked_food_weak", "monster_damage_up",
    };

    private static readonly IReadOnlySet<string> None = new HashSet<string>();

    public static IReadOnlySet<string> ExcludedFor(Theme t) => t switch
    {
        Theme.Foraging => new HashSet<string> { "forage_off" },
        Theme.Farming => new HashSet<string> { "crop_growth_down" },
        Theme.Fishing => new HashSet<string> { "fish_bite_down" },
        Theme.Mining or Theme.Spelunking => new HashSet<string> { "mines_closed" },
        Theme.Artisan => new HashSet<string> { "machines_slow" },
        _ => None,
    };

    public static string LiabilityFor(int seed, int weekOfYear, Theme theme, bool random)
    {
        string own = ThemeModifiers.For(theme).LiabilityId;
        if (!random) return own;
        var allowed = AllLiabilities.Where(id => !ExcludedFor(theme).Contains(id)).ToList();
        var rng = new Random(seed ^ (weekOfYear * 7919) ^ ((int)theme * 1031) ^ Salt);
        return allowed[rng.Next(allowed.Count)];
    }

    public static (string BonusId, string LiabilityId) EffectsFor(RunState run, Theme theme)
    {
        var own = ThemeModifiers.For(theme);
        return (own.BonusId, run.CurrentLiabilityId ?? own.LiabilityId);
    }
}
```
`RunState`: `public string? CurrentLiabilityId { get; set; }`; set to null in `BeginNewRun` and at the start of `Select` (the caller sets it right after `Select`).

- [ ] **Step 4: Verify pass.**

- [ ] **Step 5: Glue**
  - `RunController.SelectByName` (:1069) and the day-28 pre-pick application (:878): after `Run.Select(theme)`, set `Run.CurrentLiabilityId = RandomPairing.LiabilityFor(Run.Seed, Run.WeekOfYear, theme, RandomizerForWeekNoStore(Run.WeekOfYear).RandomPairings);` then use `RandomPairing.EffectsFor(Run, theme)` for `ActiveEffectsProvider.Set`. For the pre-pick, note the card was shown with week WeekOfYear+1 and the hub pre-pick reads live settings; the application at :878 happens in the new month's first week, so compute with that week (it equals the week the card was shown for).
  - Load restore (:217): `ActiveEffectsProvider.Set(...)` from `RandomPairing.EffectsFor(Run, Run.CurrentSelection.Value)`.
  - `WeeklyThemeQuestService.cs:86, 250`: use `RandomPairing.EffectsFor(Run, theme)`.
  - `WeeklyHubMenu.DrawCard` (:761): the card shows `RandomPairing.LiabilityFor(_run.Seed, OfferWeek, theme, _rand.RandomPairings)` (the stored value does not exist yet when the card is drawn; the formula gives the same answer).
  - `SweepExistingForage()` keys on the id, no change.

- [ ] **Step 6: Build + full tests. Step 7: Commit + push** (`"Random buff/debuff pairings, never a drawback that blocks the theme"`).

---

### Task 5: Random weekly JP multiplier and the mystery card

**Files:**
- Create: `src/TheLongestYear.Core/CardMultiplier.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs` (`CurrentGoalMultiplier`)
- Modify: `src/TheLongestYear/Loop/WeeklyThemeQuestService.cs:204-220` (`PayGoalShares`)
- Modify: `src/TheLongestYear/Donations/DonationService.cs:50-87` (`bonusApplies` branch)
- Modify: `src/TheLongestYear/Loop/RunController.cs` (`SelectByName`, pre-pick application)
- Modify: `src/TheLongestYear/UI/WeeklyHubMenu.cs` (`DrawCard`, `ResolvePerCardData`, reroll keeps a sealed card)
- Modify: `src/TheLongestYear/i18n/default.json`
- Create: `src/TheLongestYear/assets/card-back.png` ONLY if no vanilla texture fits; prefer drawing the card box with a vanilla texture (e.g. `Game1.mouseCursors` question-mark or the Junimo note) so no new asset is needed.
- Test: `tests/TheLongestYear.Tests/CardMultiplierTests.cs`

**Interfaces:**
- Produces:
  - `public static double CardMultiplier.For(int seed, int weekOfYear, Theme theme, bool random)`: 1.0 when off; otherwise 0.50 to 1.50 in 0.05 steps (21 values). Salt `0x51A7`.
  - `public static bool CardMultiplier.IsMysteryWeek(int seed, int weekOfYear, bool enabled)`: about 1 week in 4. Salt `0x6C3B`.
  - `public static int CardMultiplier.SealedSlot(int seed, int weekOfYear)`: 0 or 1.
  - `public static double CardMultiplier.Mystery(int seed, int weekOfYear, Theme theme)`: 1.25 to 1.75 in 0.05 steps (11 values). Salt `0x7D4F`.
  - `public static double CardMultiplier.ForCard(int seed, int week, Theme theme, int slot, RandomizerSettings r)`: the mystery value when `r.MysteryCard && IsMysteryWeek && slot == SealedSlot`, else `For(..., r.RandomMultiplier)`.
  - `RunState.CurrentGoalMultiplier` (`double`, default 1.0).

Pay rule (spec section 4): the multiplier scales only the theme's goal JP: the weekly bonus shares and the goal-slot donation bonus. Ordinary donations, bundle and room bonuses are untouched.

- [ ] **Step 1: Failing tests**
```csharp
public class CardMultiplierTests
{
    [Fact]
    public void Off_is_always_one()
        => Assert.All(Enumerable.Range(0, 50), s => Assert.Equal(1.0, CardMultiplier.For(s, 3, Theme.Mining, false)));

    [Fact]
    public void Random_multiplier_stays_in_half_to_one_and_a_half_in_steps_of_five_hundredths()
    {
        var seen = Enumerable.Range(0, 2000).Select(s => CardMultiplier.For(s, 3, Theme.Mining, true)).ToList();
        Assert.All(seen, m => { Assert.InRange(m, 0.5, 1.5); Assert.Equal(0, Math.Round(m * 100) % 5); });
        Assert.Contains(0.5, seen); Assert.Contains(1.5, seen);
    }

    [Fact]
    public void Mystery_weeks_are_about_one_in_four()
    {
        int n = Enumerable.Range(0, 4000).Count(s => CardMultiplier.IsMysteryWeek(s, 6, true));
        Assert.InRange(n, 800, 1200);
    }

    [Fact]
    public void No_mystery_when_the_option_is_off()
        => Assert.DoesNotContain(true, Enumerable.Range(0, 500).Select(s => CardMultiplier.IsMysteryWeek(s, 6, false)));

    [Fact]
    public void The_mystery_card_always_pays_one_and_a_quarter_to_one_and_three_quarters()
        => Assert.All(Enumerable.Range(0, 1000), s => Assert.InRange(CardMultiplier.Mystery(s, 2, Theme.Farming), 1.25, 1.75));

    [Fact]
    public void ForCard_gives_the_sealed_slot_the_mystery_value()
    {
        var r = new RandomizerSettings { MysteryCard = true };
        int seed = Enumerable.Range(0, 500).First(s => CardMultiplier.IsMysteryWeek(s, 5, true));
        int sealedSlot = CardMultiplier.SealedSlot(seed, 5);
        Assert.Equal(CardMultiplier.Mystery(seed, 5, Theme.Fishing), CardMultiplier.ForCard(seed, 5, Theme.Fishing, sealedSlot, r));
        Assert.Equal(1.0, CardMultiplier.ForCard(seed, 5, Theme.Fishing, 1 - sealedSlot, r));
    }

    [Fact]
    public void An_old_save_pays_one_times()
        => Assert.Equal(1.0, System.Text.Json.JsonSerializer.Deserialize<RunState>("{}")!.CurrentGoalMultiplier);
}
```

- [ ] **Step 2: Verify fail. Step 3: Implement**
```csharp
namespace TheLongestYear.Core;

/// <summary>Spec sections 4 and 5: a per-card JP multiplier on the theme's goal pay, and about one week
/// in four a face-down card that shows only its (always generous) multiplier.</summary>
public static class CardMultiplier
{
    private const int StepHundredths = 5;
    private const int RandomLowHundredths = 50, RandomSteps = 21;   // 0.50 .. 1.50
    private const int MysteryLowHundredths = 125, MysterySteps = 11; // 1.25 .. 1.75
    private const int MysteryOneIn = 4;
    private const int SaltMultiplier = 0x51A7, SaltMysteryWeek = 0x6C3B, SaltMysteryValue = 0x7D4F;

    private static Random Rng(int seed, int week, int salt) => new(seed ^ (week * 7919) ^ salt);

    public static double For(int seed, int weekOfYear, Theme theme, bool random)
    {
        if (!random) return 1.0;
        int step = Rng(seed, weekOfYear, SaltMultiplier ^ ((int)theme * 1031)).Next(RandomSteps);
        return (RandomLowHundredths + step * StepHundredths) / 100.0;
    }

    public static bool IsMysteryWeek(int seed, int weekOfYear, bool enabled)
        => enabled && Rng(seed, weekOfYear, SaltMysteryWeek).Next(MysteryOneIn) == 0;

    public static int SealedSlot(int seed, int weekOfYear) => Rng(seed, weekOfYear, SaltMysteryWeek ^ 1).Next(2);

    public static double Mystery(int seed, int weekOfYear, Theme theme)
    {
        int step = Rng(seed, weekOfYear, SaltMysteryValue ^ ((int)theme * 1031)).Next(MysterySteps);
        return (MysteryLowHundredths + step * StepHundredths) / 100.0;
    }

    public static double ForCard(int seed, int week, Theme theme, int slot, RandomizerSettings r)
        => r.MysteryCard && IsMysteryWeek(seed, week, true) && slot == SealedSlot(seed, week)
            ? Mystery(seed, week, theme)
            : For(seed, week, theme, r.RandomMultiplier);
}
```
`RunState`: `public double CurrentGoalMultiplier { get; set; } = 1.0;`; reset to 1.0 in `BeginNewRun` and at the start of `Select`.

- [ ] **Step 4: Verify pass.**

- [ ] **Step 5: Glue**
  - Selection: the hub knows which slot (0 left, 1 right) was clicked. Pass it to `SelectByName(..., int slot = -1)` (and `PreSelectForNextMonth`, storing the multiplier alongside `NextMonthSelection` in a new `RunState.NextMonthGoalMultiplier`, default 1.0, consumed in `BeginNewMonth`). After `Run.Select(theme)`: `Run.CurrentGoalMultiplier = slot < 0 ? 1.0 : CardMultiplier.ForCard(Run.Seed, week, theme, slot, settings);`. `tly_*` debug selection passes -1 (1x).
  - `PayGoalShares`: `long perGoal = WeeklyGoalPayout.PerGoal((long)Math.Round(Jp.WeeklyQuestBonus(Run.WeekOfYear) * Run.CurrentGoalMultiplier, MidpointRounding.AwayFromZero), total);`
  - `DonationService.OnItemDonated`: in the `bonusApplies` branch multiply by `_config.SelectionBonusMultiplier * Run.CurrentGoalMultiplier`.
  - Hub `DrawCard(b, card, theme, bonus, bounds, int slot)`:
    - Face-down: when `_rand.MysteryCard && CardMultiplier.IsMysteryWeek(_run.Seed, OfferWeek, true) && slot == CardMultiplier.SealedSlot(_run.Seed, OfferWeek)`, draw the card box, a large "?" (dialogueFont, centred) and the line `menu.hub.mystery-mult` (`{{mult}}`), and nothing else (skip theme name, buff, drawback, icons; `ResolvePerCardData` skips the preview for it).
    - Face-up with `_rand.RandomMultiplier` or a mystery week on offer: one line `menu.hub.card-mult` (`{{mult}}`, formatted `0.##` + "x") after the drawback line. Hidden otherwise.
    - Reroll on a mystery week: the sealed slot stays sealed (the rule is per week and slot, so rerolling the themes keeps the same slot face down automatically; verify).
    - Hover on the sealed card shows no item tooltips.
  - Hard-coded "1.5x" strings (`menu.hub.banking-tip`, `bonus-week`, `bonus-hover`, `quest.weekly.tip`): leave as they are (they describe the goal-slot bonus, which still applies).
  - Quest log: when `Run.CurrentGoalMultiplier != 1.0`, append `quest.weekly.mult` (`{{mult}}`) to the quest description in `WeeklyThemeQuestService`.
  - All new strings through `game-writing`.

- [ ] **Step 6: Build + full tests. Step 7: Commit + push** (`"Random weekly JP multiplier and the face-down mystery card"`).

---

### Task 6: Random bundle rewards

**Files:**
- Create: `src/TheLongestYear.Core/BundleRewardShuffle.cs`
- Modify: `src/TheLongestYear.Core/MetaState.cs` (find it: the class `MetaStore` persists; add `RandomBundleRewardsBoard`)
- Modify: `src/TheLongestYear/Loop/BundleEngine.cs:222-455` (`Generate` end), `src/TheLongestYear/Loop/WorldResetService.cs:662-710` (both sources), `src/TheLongestYear/ModEntry.cs:5285-5300` (fresh run)
- Test: `tests/TheLongestYear.Tests/BundleRewardShuffleTests.cs`

**Interfaces:**
- Produces:
  - `public static string BundleRewardShuffle.RewardFor(int boardSeed, string bundleKey, IReadOnlyList<string> pool)` (salt `0x3E71`, stable per key via a deterministic string hash; copy `VanillaBoardDifficultyPass.StableHash` into the new class rather than calling a private).
  - `public static IReadOnlyList<BundleSpec> BundleRewardShuffle.Apply(IReadOnlyList<BundleSpec> specs, int boardSeed, IReadOnlyList<string> pool, Func<string,bool> skipRoom)` (key = `$"{Room}/{Index}"`).
  - `public static IDictionary<string,string> BundleRewardShuffle.ApplyToData(IReadOnlyDictionary<string,string> board, int boardSeed, IReadOnlyList<string> pool, Func<string,bool> skipRoom)` (key format `Room/Index`; field 1 replaced; all other fields byte-identical).
  - `public static IReadOnlyList<string> BundleRewardShuffle.CleanPool(IEnumerable<string> rewards)`: distinct, non-empty, no '/', ordinal-sorted (so the pool is the same on every machine).
  - `MetaState.RandomBundleRewardsBoard` (`bool`): the option's value when the current board was built.

Pool source (game layer): `VanillaBundlePool.BuildRoomPools()` rewards from every room except pass-through rooms (`BundleEngine.IsPassThroughRoom`: Vault, Abandoned Joja Mart), through `CleanPool`. Skipped rooms on the board: the same pass-through rooms (Vault "rewards" are money bundles).

- [ ] **Step 1: Failing tests**
```csharp
public class BundleRewardShuffleTests
{
    private static readonly string[] Pool = BundleRewardShuffle.CleanPool(new[] { "O 495 30", "BO 21 1", "O 472 10", "", "O 495 30" }).ToArray();
    private static BundleSpec Spec(string room, int i, string reward)
        => new(room, i, "N" + i, "N" + i, reward, 0, 4, new List<BundleSlotSpec>());

    [Fact]
    public void The_pool_is_cleaned_and_sorted()
        => Assert.Equal(new[] { "BO 21 1", "O 472 10", "O 495 30" }, Pool);

    [Fact]
    public void Every_reward_comes_from_the_pool_and_vault_is_untouched()
    {
        var specs = new[] { Spec("Pantry", 0, "x"), Spec("Crafts Room", 13, "y"), Spec("Vault", 23, "2500") };
        var after = BundleRewardShuffle.Apply(specs, 42, Pool, room => room == "Vault");
        Assert.All(after.Take(2), s => Assert.Contains(s.RewardField, Pool));
        Assert.Equal("2500", after[2].RewardField);
    }

    [Fact]
    public void The_same_board_seed_gives_the_same_rewards()
    {
        var specs = new[] { Spec("Pantry", 0, "x"), Spec("Pantry", 1, "x") };
        Assert.Equal(BundleRewardShuffle.Apply(specs, 7, Pool, _ => false).Select(s => s.RewardField),
                     BundleRewardShuffle.Apply(specs, 7, Pool, _ => false).Select(s => s.RewardField));
    }

    [Fact]
    public void Data_form_replaces_only_the_reward_field()
    {
        var board = new Dictionary<string, string> { ["Pantry/0"] = "Spring Crops/O 465 20/24 1 0 188 1 0/0/4//Spring Crops" };
        var after = BundleRewardShuffle.ApplyToData(board, 3, Pool, _ => false);
        var a = after["Pantry/0"].Split('/'); var b = board["Pantry/0"].Split('/');
        Assert.Contains(a[1], Pool);
        Assert.Equal(b.Where((_, i) => i != 1), a.Where((_, i) => i != 1));
    }

    [Fact]
    public void Spec_and_data_forms_agree_for_the_same_key()
    {
        var spec = BundleRewardShuffle.Apply(new[] { Spec("Pantry", 0, "x") }, 11, Pool, _ => false)[0];
        var data = BundleRewardShuffle.ApplyToData(new Dictionary<string, string> { ["Pantry/0"] = "n/x/1 1 0/0/1//n" }, 11, Pool, _ => false);
        Assert.Equal(spec.RewardField, data["Pantry/0"].Split('/')[1]);
    }

    [Fact]
    public void An_empty_pool_leaves_rewards_alone()
        => Assert.Equal("x", BundleRewardShuffle.Apply(new[] { Spec("Pantry", 0, "x") }, 1, Array.Empty<string>(), _ => false)[0].RewardField);
}
```

- [ ] **Step 2: Verify fail. Step 3: Implement** the static class (per key: `new Random(unchecked(boardSeed ^ StableHash(key) ^ Salt))`, pick `pool[rng.Next(pool.Count)]`; empty pool returns input unchanged; `Apply` uses `spec with { RewardField = ... }`).

- [ ] **Step 4: Verify pass.**

- [ ] **Step 5: Glue**
  - `MetaState.RandomBundleRewardsBoard`: set from `_config.Randomizer.RandomBundleRewards` at the moment a NEW board is built (reset path in `WorldResetService` before generating, and the fresh-run path `ModEntry.cs:5285`); NOT changed when a held board is restored, NOT read from live config anywhere else.
  - Engine: `BundleEngine.Generate(int seed)` already builds room pools (:247). At the end, if `_meta.RandomBundleRewardsBoard` (pass the flag in as a parameter `bool randomRewards` from every caller, including the reload re-derivation in `ResolveRequirements` and the debug re-generation at `ModEntry.cs:4393-4420`, all reading the MetaState flag), run `BundleRewardShuffle.Apply(specs, seed, pool, IsPassThroughRoom)` before returning. The reward pool comes from the same `BuildRoomPools()` result.
  - Vanilla / Remixed: in `WorldResetService` after `ApplyVanillaBoardDifficulty()` / `ClampVanillaCappedAsks()` and NOT on the held-board restore path, if the flag is set: read the live board (`Game1.netWorldState.Value.BundleData` or the helper the pass already uses), `ApplyToData(board, seed, pool, room => skip)`, `SetBundleData(result)`. Seed is the same `BundleEngineSeed.For(..., _meta.EffectiveBundleSeedLoop)` the difficulty pass uses.
  - Loop 1 of a vanilla save is written by the game, so the option starts at the first rewind there; the Engine's fresh board honours it from day 1. That matches "takes effect at the next board build".
  - Log one Info line when the shuffle runs: `Randomizer: bundle rewards shuffled (N bundles, pool M).`

- [ ] **Step 6: Build + full tests (EngineManifestCheckTests and VanillaBoardDifficultyPassTests must stay green). Step 7: Commit + push** (`"Random bundle rewards, fixed per board"`).

---

### Task 7: Random cart days

**Files:**
- Create: `src/TheLongestYear.Core/CartSchedule.cs`
- Modify: `src/TheLongestYear.Core/WeatherScheduler.cs:49-52,217` (make the festival table reachable: `public static IReadOnlyList<int> FestivalDays(int seasonIndex)`; keep the private arrays)
- Modify: `src/TheLongestYear.Core/CartStockPreview.cs:16` (delegate to `CartSchedule.VanillaDaysInWeek`)
- Modify: `src/TheLongestYear.Core/RunState.cs` (`CartDaysWeek`, `CartDays`)
- Create: `src/TheLongestYear/Loop/CartDaysPatch.cs`
- Modify: `src/TheLongestYear/UI/ShrinePreviewMenu.cs:162-220`
- Modify: `src/TheLongestYear/ModEntry.cs` (wire `CartDaysPatch.RunProvider` / settings next to `CartSlotLimitPatch` at :527 and :811)
- Test: `tests/TheLongestYear.Tests/CartScheduleTests.cs`

**Interfaces:**
- Produces:
  - `public static IReadOnlyList<int> CartSchedule.VanillaDaysInWeek(int weekStartDay)` (existing `% 7 % 5 == 0` rule).
  - `public static IReadOnlyList<int> CartSchedule.RandomDaysInWeek(int seed, int weekOfYear, int weekStartDay, IReadOnlyCollection<int> blockedDays)`: each of the 7 days independently with chance 2/7 (`rng.Next(7) < 2`), blocked days skipped; if none came up, force the first-by-rng unblocked day; if every day is blocked, return empty. Salt `0x4A2D`.
  - `public static IReadOnlyList<int> CartSchedule.BlockedDays(int seasonIndex)`: `WeatherScheduler.FestivalDays(seasonIndex)` plus passive festivals: Spring (0) 15, 16, 17 (Desert Festival); Winter (3) 15, 16, 17 (Night Market, which keeps its own boat cart).
  - `RunState.CartDaysWeek` (`int`, -1) and `RunState.CartDays` (`List<int>`): this week's rolled days (day-of-month), stored the first time they are asked for in a week.
  - `public static IReadOnlyList<int> CartSchedule.ForWeek(RunState run, int weekOfYear, int seasonIndex, int weekStartDay, bool random)`: vanilla days when `random` is false; otherwise the stored list for that week, rolling and storing it if missing.

- [ ] **Step 1: Failing tests**
```csharp
public class CartScheduleTests
{
    [Fact]
    public void Vanilla_days_are_friday_and_sunday()
        => Assert.Equal(new[] { 5, 7 }, CartSchedule.VanillaDaysInWeek(1));

    [Fact]
    public void Random_weeks_average_about_two_visits_and_never_zero()
    {
        var counts = Enumerable.Range(0, 2000).Select(s => CartSchedule.RandomDaysInWeek(s, 3, 15, Array.Empty<int>()).Count).ToList();
        Assert.DoesNotContain(0, counts);
        Assert.InRange(counts.Average(), 1.8, 2.6);
    }

    [Fact]
    public void Festival_days_never_get_a_cart()
    {
        var blocked = CartSchedule.BlockedDays(0);
        for (int s = 0; s < 500; s++)
            Assert.DoesNotContain(CartSchedule.RandomDaysInWeek(s, 2, 8, blocked), d => blocked.Contains(d));
    }

    [Fact]
    public void A_week_with_every_day_blocked_has_no_cart()
        => Assert.Empty(CartSchedule.RandomDaysInWeek(1, 2, 8, Enumerable.Range(8, 7).ToList()));

    [Fact]
    public void Days_stay_inside_the_week()
        => Assert.All(CartSchedule.RandomDaysInWeek(9, 4, 22, Array.Empty<int>()), d => Assert.InRange(d, 22, 28));

    [Fact]
    public void Off_gives_vanilla_days()
        => Assert.Equal(new[] { 12, 14 }, CartSchedule.ForWeek(new RunState(), 2, 0, 8, random: false));

    [Fact]
    public void A_rolled_week_is_stored_and_reused()
    {
        var run = new RunState();
        var first = CartSchedule.ForWeek(run, 6, 1, 15, random: true);
        Assert.Equal(6, run.CartDaysWeek);
        run.CartDays = new List<int> { 20 };
        Assert.Equal(new[] { 20 }, CartSchedule.ForWeek(run, 6, 1, 15, random: true));
        Assert.NotNull(first);
    }

    [Fact]
    public void Night_market_and_desert_festival_are_blocked()
    {
        Assert.Contains(15, CartSchedule.BlockedDays(3));
        Assert.Contains(16, CartSchedule.BlockedDays(0));
    }
}
```

- [ ] **Step 2: Verify fail. Step 3: Implement** `CartSchedule` as specified (weekStartDay is 1, 8, 15 or 22; ordinal-sorted results). `CartStockPreview.CartVisitDaysInWeek` returns `CartSchedule.VanillaDaysInWeek(weekStartDay).ToArray()` so its tests stay green. `CartDays` default `new()`, cleared (`CartDaysWeek = -1`) in `BeginNewRun`.

- [ ] **Step 4: Verify pass.**

- [ ] **Step 5: Glue**
  - `CartDaysPatch`:
```csharp
[HarmonyPatch(typeof(Forest), nameof(Forest.ShouldTravelingMerchantVisitToday))]
internal static class CartDaysPatch
{
    internal static Func<RunState>? RunProvider;
    internal static Func<RandomizerSettings>? Settings; // the week's snapshot via RunController.Randomizer
    private static void Postfix(ref bool __result)
    {
        if (!RunActivation.IsActive || RunProvider == null || Settings == null) return;
        if (!Settings().RandomCartDays) return;
        RunState run = RunProvider();
        int weekStart = ((Game1.dayOfMonth - 1) / 7) * 7 + 1;
        __result = CartSchedule.ForWeek(run, run.WeekOfYear, Game1.seasonIndex, weekStart, random: true).Contains(Game1.dayOfMonth);
    }
}
```
    Wire `RunProvider` and `Settings` where `CartSlotLimitPatch.RunProvider` is set (`ModEntry.cs:527`) and null them at unload (:811).
  - `ShrinePreviewMenu`: replace `TravelingCartVisitsToday` and `NextCartVisitDay` with calls through one helper that uses `CartSchedule.ForWeek` for the current week (random or vanilla per the snapshot). For "next visit": search the rest of this week's days, then later weeks of this season with `CartSchedule.RandomDaysInWeek` computed (not stored) for those weeks when random, vanilla otherwise; if none are left this season, show the existing "away" header without a day (add `menu.shrine-preview.cart-away-season` through `game-writing` if the current string needs a day).
  - Keep the Cart Stall preview (`WeeklyHubMenu` cart rows) as it is; it shows stock, not days.

- [ ] **Step 6: Build + full tests. Step 7: Commit + push** (`"Random cart days: rolled per week, never on a festival"`).

---

### Task 8: Headless smoke, docs, release prep

**Files:**
- Modify: `CHANGELOG.md`, `README.md` (What's New), `release-notes/` (new `0.19.0` note + Nexus BBCode description), `TODO.md` (Randomizer entry: built options; wildcard days, double week and shrine donations remain)

- [ ] **Step 1: Headless smoke** per `docs/HEADLESS_DRIVING.md` on the throwaway Rodger save (load clones via `tly_loadsave`), every launch labelled as Claude's. Turn each option on in config.json, then: open the hub (rerolls: costs 50 then 100, greyed when broke; Free costs nothing); pick a card and check the quest log goals, drawback id (`ActiveEffectsProvider.LiabilityId` via a `tly_*` dump), multiplier; force a mystery week by trying seeds with a scratch `tly_` command if needed; `tly_failreset` and confirm rewards changed and the reload keeps them; advance to a rolled cart day and confirm the cart is in the Forest. Also run once with every option off and compare offer, goals and cart days against master.
- [ ] **Step 2: Docs.** README + Nexus description (content-identical, house style): collapse the 0.18.x What's New stack into one short "0.18 in brief" paragraph, move the detail to `CHANGELOG.md`, add "What's New in 0.19.0: the Randomizer" at the top (seven options, all off by default; wildcard days, double theme weeks and shrine donations coming next; credit Nijah for the idea thread). No em dashes. Do NOT bump the manifest here.
- [ ] **Step 3: Commit + push.** Merging to master, setting 0.19.0 and publishing are Jeff's call.
