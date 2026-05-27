# Theme Effects Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the five active-week bonus/liability gameplay effects, three Obtainability upgrade effects (Red Cabbage + Starfruit Mixed Seeds injection, Rare Fish catch boost), and two Foresight data deliveries (Weather Sage N-day preview, Cart Whisperer N-item preview) into the planning hub.

### Signed-off theme mapping (JC-1 resolved 2026-05-27)

| Theme | Bonus id | Liability id | Notes |
|-------|----------|--------------|-------|
| Foraging | `forage_yield_up` | `mines_closed` | HARD: elevator + entrance ladder blocked |
| Farming | `crop_growth_up` | `fish_bite_down` | -30% bite rate |
| Fishing | `fish_bite_up` | `crop_growth_down` | -25% crop growth |
| Mining | `mine_drops_up` | `forage_off` | HARD: no wild produce / mushrooms / fiddleheads spawn |
| Mixed | `all_drops_up` | `all_sell_prices_down` | |

**ThemeModifiers.cs corrected from original:** Foraging liability was `mine_drops_off` → now `mines_closed`. Farming liability was `forage_drops_off` → now `fish_bite_down`. Mining liability was `forage_drops_off` → renamed to `forage_off` (HARD spawn suppression, not drop rate). Old ids `mine_drops_off` and `forage_drops_off` are legacy-only (no theme maps to them in v1).

**Architecture:** Pure-Core helper types (`ActiveEffectsProvider`, `BonusDropResolver`, `WeatherForecast`, `CartStockPreview`) hold all deterministic logic and are unit-tested. Mod-side Harmony patches read the active modifier id via a static accessor on `ActiveEffectsProvider` — they never import Core state directly; the accessor is set/cleared in `RunController` at selection time and cleared on `BeginNewRun`. `WeeklyHubMenu` replaces its placeholder `DrawPreviewRow` strings with real data from `WeatherForecast` and `CartStockPreview` once the accessor is wired.

**Tech Stack:** C# (.NET 6 / SMAPI / Harmony 2). xUnit for Core tests. Mod-side patches verified by playtest only (no `Game1`-touching test infrastructure).

---

## Scope decision: one plan vs. three

The three areas (bonus/liability effects, obtainability, foresight) each produce a working feature independently — but they share the same scaffold: `ActiveEffectsProvider`, the branch, and the `WeeklyHubMenu` wiring. Splitting them would produce three plans that all start with "create `ActiveEffectsProvider`", which is noise. A single plan of ~28 tasks, grouped by area, is the right call. The task list below uses section comments to make progress clear.

---

## File Structure

### New Core files
| File | Responsibility |
|------|----------------|
| `src/TheLongestYear.Core/ActiveEffectsProvider.cs` | Static accessor: exposes the current `BonusId` and `LiabilityId` strings to Harmony patches without requiring Core imports in the patch layer. Set by `RunController` on selection; cleared on run start/load. |
| `src/TheLongestYear.Core/BonusDropResolver.cs` | Pure probability check: given a modifier id + item qualified id, returns `true` (+1 bonus fires) at the correct per-theme rate. Unit-tested. |
| `src/TheLongestYear.Core/WeatherForecast.cs` | Generates the deterministic N-day weather string list for next week, given the game's uniqueId + DaysPlayed seed. Pure, unit-tested with known seed values. |
| `src/TheLongestYear.Core/CartStockPreview.cs` | Generates the deterministic Cart item list for this week, given the game's uniqueId + DaysPlayed seed. Pure, unit-tested with known seed values. |

### New mod-side files
| File | Responsibility |
|------|----------------|
| `src/TheLongestYear/Loop/ForageYieldPatch.cs` | Harmony patch: `GameLocation.spawnObjects` postfix — after spawn, iterates `location.objects` and for any IsSpawnedObject forage (excluding stone/wood) rolls `BonusDropResolver` for `forage_yield_up`; spawns a matching duplicate at the same tile. |
| `src/TheLongestYear/Loop/CropGrowthPatch.cs` | Harmony prefix on `Crop.newDay(int state)` — intercepts one growth tick per day: if bonus is `crop_growth_up`, 25% chance to grant an extra `dayOfCurrentPhase++` advance; if liability is `crop_growth_down`, 25% chance to withhold the day's tick (set state=0 if state==1 before returning). |
| `src/TheLongestYear/Loop/FishBiteRatePatch.cs` | Harmony postfix on `FishingRod.calculateTimeUntilFishingBite` — multiplies result by 0.77 (fish_bite_up, ~30% faster) or by 1.43 (fish_bite_down, ~30% slower). |
| `src/TheLongestYear/Loop/MineDropsPatch.cs` | Harmony postfix on `ResourceClump.destroy` — for mine contexts, rolls `BonusDropResolver` for `mine_drops_up` and creates one extra debris item of the same type. Also: prefix on `MineShaft.checkAction` for `mines_closed` (Foraging liability — HARD: blocks elevator tile 112 + entrance ladder tile 173 with an info dialogue). |
| `src/TheLongestYear/Loop/ForageOffPatch.cs` | Harmony prefix on `GameLocation.spawnObjects` for the `forage_off` liability (Mining theme) — suppresses wild forage SPAWN (produce, mushrooms, fiddleheads) on outdoor locations. Separate postfix on `MineShaft.loadLevel` for mine mushroom suppression sub-case. |
| `src/TheLongestYear/Loop/SellPricePatch.cs` | Harmony postfix on `Object.sellToStorePrice` — if liability is `all_sell_prices_down`, multiply `__result` by 0.5 (floor 1). |
| `src/TheLongestYear/Loop/AllDropsPatch.cs` | Combined Harmony postfix on `ResourceClump.destroy` and prefix on `GameLocation.spawnObjects` for `all_drops_up` — rolls 10% extra +1 on any item type including stone/wood. |
| `src/TheLongestYear/Loop/MixedSeedsPatch.cs` | Harmony prefix on `Crop.newDay` triggered at crop-creation time: intercepts `(O)770` (Mixed Seeds) plant resolution during Summer to potentially inject Red Cabbage / Starfruit seeds when the respective upgrades are owned. |
| `src/TheLongestYear/Loop/RareFishPatch.cs` | Harmony postfix on `FishingRod.openTreasureMenuEndFunction` or the catch-outcome path — boosts rare-fish probability by 25% when `fortune_rare_fish` is owned. |

### Modified files
| File | Change |
|------|--------|
| `src/TheLongestYear.Core/ActiveEffectsProvider.cs` | (new) |
| `src/TheLongestYear/Loop/RunController.cs` | Call `ActiveEffectsProvider.Set(bonus, liability)` in `SelectByName` and `OnRunLoaded`; call `ActiveEffectsProvider.Clear()` in `BeginNewRun`. |
| `src/TheLongestYear/UI/WeeklyHubMenu.cs` | Replace placeholder `DrawPreviewRow` strings with real weather icons + cart item names; drive row count from `WeatherForecast.SlotCount` + `CartStockPreview.SlotCount` instead of `_config.DefaultWeatherPreviewSlots/DefaultCartPreviewSlots`. |
| `src/TheLongestYear/Loop/RunController.cs` | Expose `WeatherSageTier` and `CartWhispererTier` helpers that read `_store.State.OwnedUpgrades` to compute tier count. Pass to `MenuLauncher.OpenWeeklyHub`. |
| `src/TheLongestYear/UI/MenuLauncher.cs` | Forward weather/cart tier ints to `WeeklyHubMenu` constructor. |

---

## Tasks

---

### Task 0: Branch + baseline

**Files:**
- No file changes — git only

- [ ] **Step 1: Create the feature branch**

```bash
git checkout -b feat/v1-plan-06-theme-effects
```
Expected: branch created from `feat/v1-plan-06b-cookbook-craftbook`.

- [ ] **Step 2: Verify tests pass clean**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --no-build -v m
```
Expected: 292 passed, 0 failed. If the count differs, read the failure before proceeding.

- [ ] **Step 3: Verify mod builds**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: Build succeeded, 0 Error(s).

- [ ] **Step 4: Commit**

```bash
git add .
git commit -m "$(cat <<'EOF'
chore: branch for v1 Plan 06 theme effects layer

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## SECTION A0 — ThemeModifiers correction (prerequisite for all effect tasks)

---

### Task 0.5: Correct ThemeModifiers.cs + update its tests

**Files:**
- Modify: `src/TheLongestYear.Core/ThemeModifiers.cs`
- Modify: `tests/TheLongestYear.Tests/ThemeModifiersTests.cs`

This is a prerequisite for all later tasks. Every patch in Sections B–D checks modifier ids via `ActiveEffectsProvider`; those ids must match the signed-off spec before any patch is written.

**Changes to `ThemeModifiers.For()`:**

| Theme | Old liability | New liability |
|-------|--------------|--------------|
| Foraging | `mine_drops_off` | `mines_closed` |
| Farming | `forage_drops_off` | `fish_bite_down` |
| Mining | `forage_drops_off` | `forage_off` |

**Changes to `ThemeModifiers.DisplayNameFor()`:**
- Add: `"mines_closed"` → `"Mines Closed"`
- Add: `"fish_bite_down"` → `"-30% Fish Bite Rate"`
- Add: `"forage_off"` → `"Forage Off"`
- Move `"mine_drops_off"` and `"forage_drops_off"` to the legacy block (no theme maps to them in v1)

- [ ] **Step 1: Update `ThemeModifiers.cs`**

Replace the `For()` switch body and `DisplayNameFor()` switch with:

```csharp
public static (string BonusId, string LiabilityId) For(Theme theme) => theme switch
{
    Theme.Foraging => ("forage_yield_up", "mines_closed"),
    Theme.Farming  => ("crop_growth_up", "fish_bite_down"),
    Theme.Fishing  => ("fish_bite_up", "crop_growth_down"),
    Theme.Mining   => ("mine_drops_up", "forage_off"),
    // Mixed: "+10% all drops, -50% all sell prices" -- generalist boost + economic squeeze.
    Theme.Mixed    => ("all_drops_up", "all_sell_prices_down"),
    _ => throw new ArgumentOutOfRangeException(nameof(theme), theme, null)
};
```

And the `DisplayNameFor()` switch:

```csharp
public static string DisplayNameFor(string modifierId) => modifierId switch
{
    "forage_yield_up"        => "+25% Foraging Yield",
    "forage_off"             => "Forage Off",
    "crop_growth_up"         => "+25% Crop Growth",
    "crop_growth_down"       => "-25% Crop Growth",
    "fish_bite_up"           => "+30% Fish Bite Rate",
    "fish_bite_down"         => "-30% Fish Bite Rate",
    "mine_drops_up"          => "+30% Mine Drops",
    "mines_closed"           => "Mines Closed",
    "all_drops_up"           => "+10% All Drops",
    "all_sell_prices_down"   => "-50% All Sell Prices",
    // Legacy / unused-in-v1 -- kept so old config files don't show raw ids if loaded.
    "forage_drops_off"       => "Foraging Disabled (legacy)",
    "mine_drops_off"         => "Mine Drops Disabled (legacy)",
    "shop_discount"          => "-15% Shop Prices",
    "stamina_drain_up"       => "+30% Stamina Drain",
    _ => modifierId
};
```

- [ ] **Step 2: Update `ThemeModifiersTests.cs`**

Replace the `DisplayNameFor_maps_known_ids` theory inline data to reflect the new ids:

```csharp
[Theory]
[InlineData("forage_yield_up",      "+25% Foraging Yield")]
[InlineData("forage_off",           "Forage Off")]
[InlineData("crop_growth_up",       "+25% Crop Growth")]
[InlineData("crop_growth_down",     "-25% Crop Growth")]
[InlineData("fish_bite_up",         "+30% Fish Bite Rate")]
[InlineData("fish_bite_down",       "-30% Fish Bite Rate")]
[InlineData("mine_drops_up",        "+30% Mine Drops")]
[InlineData("mines_closed",         "Mines Closed")]
[InlineData("all_drops_up",         "+10% All Drops")]
[InlineData("all_sell_prices_down", "-50% All Sell Prices")]
// Legacy ids still map to a string (not raw id) so UI doesn't break on old saves.
[InlineData("forage_drops_off",     "Foraging Disabled (legacy)")]
[InlineData("mine_drops_off",       "Mine Drops Disabled (legacy)")]
[InlineData("shop_discount",        "-15% Shop Prices")]
[InlineData("stamina_drain_up",     "+30% Stamina Drain")]
public void DisplayNameFor_maps_known_ids(string id, string expected)
    => Assert.Equal(expected, ThemeModifiers.DisplayNameFor(id));
```

Also add specific per-theme assertions for `For()` to lock in the new mappings:

```csharp
[Theory]
[InlineData(Theme.Foraging, "forage_yield_up", "mines_closed")]
[InlineData(Theme.Farming,  "crop_growth_up",  "fish_bite_down")]
[InlineData(Theme.Fishing,  "fish_bite_up",    "crop_growth_down")]
[InlineData(Theme.Mining,   "mine_drops_up",   "forage_off")]
[InlineData(Theme.Mixed,    "all_drops_up",    "all_sell_prices_down")]
public void For_returns_correct_signed_off_ids(Theme theme, string expectedBonus, string expectedLiability)
{
    var (bonus, liability) = ThemeModifiers.For(theme);
    Assert.Equal(expectedBonus, bonus);
    Assert.Equal(expectedLiability, liability);
}
```

- [ ] **Step 3: Run tests — expect pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "ThemeModifiersTests" -v m
```
Expected: all ThemeModifiersTests pass (including new `For_returns_correct_signed_off_ids` theory).

- [ ] **Step 4: Full test suite green**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v m
```
Expected: all pass.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/ThemeModifiers.cs tests/TheLongestYear.Tests/ThemeModifiersTests.cs
git commit -m "$(cat <<'EOF'
fix: correct ThemeModifiers ids to match signed-off spec (JC-1)

Foraging liability: mine_drops_off -> mines_closed (HARD mine block)
Farming liability: forage_drops_off -> fish_bite_down (-30% bite rate)
Mining liability: forage_drops_off -> forage_off (HARD spawn suppression)
Old ids moved to legacy block in DisplayNameFor.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## SECTION A — ActiveEffectsProvider (the shared scaffold)

---

### Task 1: ActiveEffectsProvider + BonusDropResolver Core types + tests

**Files:**
- Create: `src/TheLongestYear.Core/ActiveEffectsProvider.cs`
- Create: `src/TheLongestYear.Core/BonusDropResolver.cs`
- Create: `tests/TheLongestYear.Tests/ActiveEffectsProviderTests.cs`
- Create: `tests/TheLongestYear.Tests/BonusDropResolverTests.cs`

- [ ] **Step 1: Write the failing tests for `ActiveEffectsProvider`**

Create `tests/TheLongestYear.Tests/ActiveEffectsProviderTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ActiveEffectsProviderTests
{
    [Fact]
    public void Initially_no_active_effects()
    {
        ActiveEffectsProvider.Clear();
        Assert.Null(ActiveEffectsProvider.BonusId);
        Assert.Null(ActiveEffectsProvider.LiabilityId);
    }

    [Fact]
    public void Set_stores_bonus_and_liability()
    {
        ActiveEffectsProvider.Set("forage_yield_up", "mines_closed");
        Assert.Equal("forage_yield_up", ActiveEffectsProvider.BonusId);
        Assert.Equal("mines_closed", ActiveEffectsProvider.LiabilityId);
        ActiveEffectsProvider.Clear();
    }

    [Fact]
    public void Clear_resets_to_null()
    {
        ActiveEffectsProvider.Set("crop_growth_up", "fish_bite_down");
        ActiveEffectsProvider.Clear();
        Assert.Null(ActiveEffectsProvider.BonusId);
        Assert.Null(ActiveEffectsProvider.LiabilityId);
    }

    [Fact]
    public void ActiveBonus_returns_true_for_current_id()
    {
        ActiveEffectsProvider.Set("fish_bite_up", "crop_growth_down");
        Assert.True(ActiveEffectsProvider.ActiveBonus("fish_bite_up"));
        Assert.False(ActiveEffectsProvider.ActiveBonus("mine_drops_up"));
        ActiveEffectsProvider.Clear();
    }

    [Fact]
    public void ActiveLiability_returns_true_for_current_id()
    {
        ActiveEffectsProvider.Set("mine_drops_up", "forage_off");
        Assert.True(ActiveEffectsProvider.ActiveLiability("forage_off"));
        Assert.False(ActiveEffectsProvider.ActiveLiability("all_sell_prices_down"));
        ActiveEffectsProvider.Clear();
    }
}
```

- [ ] **Step 2: Write failing tests for `BonusDropResolver`**

Create `tests/TheLongestYear.Tests/BonusDropResolverTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BonusDropResolverTests
{
    // Use a seeded random so results are deterministic.
    // With seed 0, the first NextDouble() < threshold tests pass/fail predictably.

    [Fact]
    public void NoBonus_always_returns_false()
    {
        var rng = new System.Random(0);
        Assert.False(BonusDropResolver.ShouldGrantExtraDrop(null, "(O)281", rng));
    }

    [Fact]
    public void ForageYieldUp_excludes_stone_and_wood()
    {
        // stone = (O)390, wood = (O)388 — must always return false regardless of rng
        for (int seed = 0; seed < 100; seed++)
        {
            var rng = new System.Random(seed);
            Assert.False(BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", "(O)390", rng));
            rng = new System.Random(seed);
            Assert.False(BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", "(O)388", rng));
        }
    }

    [Fact]
    public void ForageYieldUp_fires_at_25pct_for_forage_item()
    {
        // Over 10000 samples the hit rate should be 0.25 ± 0.01.
        int hits = 0;
        var rng = new System.Random(42);
        for (int i = 0; i < 10000; i++)
            if (BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", "(O)281", rng))
                hits++;
        double rate = hits / 10000.0;
        Assert.InRange(rate, 0.23, 0.27);
    }

    [Fact]
    public void MineDropsUp_fires_at_30pct_excludes_stone()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var rng = new System.Random(seed);
            Assert.False(BonusDropResolver.ShouldGrantExtraDrop("mine_drops_up", "(O)390", rng));
        }
        int hits = 0;
        var rng2 = new System.Random(42);
        for (int i = 0; i < 10000; i++)
            if (BonusDropResolver.ShouldGrantExtraDrop("mine_drops_up", "(O)378", rng2))
                hits++;
        double rate = hits / 10000.0;
        Assert.InRange(rate, 0.28, 0.32);
    }

    [Fact]
    public void AllDropsUp_fires_at_10pct_including_stone_and_wood()
    {
        int stoneHits = 0, woodHits = 0;
        var rng = new System.Random(42);
        for (int i = 0; i < 10000; i++)
            if (BonusDropResolver.ShouldGrantExtraDrop("all_drops_up", "(O)390", rng))
                stoneHits++;
        rng = new System.Random(42);
        for (int i = 0; i < 10000; i++)
            if (BonusDropResolver.ShouldGrantExtraDrop("all_drops_up", "(O)388", rng))
                woodHits++;
        Assert.InRange(stoneHits / 10000.0, 0.08, 0.12);
        Assert.InRange(woodHits / 10000.0, 0.08, 0.12);
    }

    [Fact]
    public void Unknown_bonus_id_returns_false()
    {
        var rng = new System.Random(0);
        Assert.False(BonusDropResolver.ShouldGrantExtraDrop("not_a_real_bonus", "(O)281", rng));
    }
}
```

- [ ] **Step 3: Run failing tests to confirm they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "ActiveEffectsProviderTests|BonusDropResolverTests" -v m
```
Expected: fail with "The type or namespace name 'ActiveEffectsProvider' could not be found".

- [ ] **Step 4: Implement `ActiveEffectsProvider`**

Create `src/TheLongestYear.Core/ActiveEffectsProvider.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>
/// Thread-local static accessor that exposes the current week's active bonus and liability
/// modifier ids to Harmony patches without requiring the patch layer to import RunState
/// or MetaStore directly. Set by RunController on selection; cleared on run start and load.
/// Null values mean "no active effect" (safe default — patches skip when null).
/// </summary>
public static class ActiveEffectsProvider
{
    private static string _bonusId;
    private static string _liabilityId;

    /// <summary>Id of the active bonus this week, or null if no selection has been made.</summary>
    public static string BonusId => _bonusId;

    /// <summary>Id of the active liability this week, or null if no selection has been made.</summary>
    public static string LiabilityId => _liabilityId;

    /// <summary>Register the active effects for the current week.</summary>
    public static void Set(string bonusId, string liabilityId)
    {
        _bonusId = bonusId;
        _liabilityId = liabilityId;
    }

    /// <summary>Clear effects (no selection active — start of a new run or before first pick).</summary>
    public static void Clear()
    {
        _bonusId = null;
        _liabilityId = null;
    }

    /// <summary>Returns true when the active bonus matches <paramref name="id"/>.</summary>
    public static bool ActiveBonus(string id) => _bonusId != null && _bonusId == id;

    /// <summary>Returns true when the active liability matches <paramref name="id"/>.</summary>
    public static bool ActiveLiability(string id) => _liabilityId != null && _liabilityId == id;
}
```

- [ ] **Step 5: Implement `BonusDropResolver`**

Create `src/TheLongestYear.Core/BonusDropResolver.cs`:

```csharp
using System;

namespace TheLongestYear.Core;

/// <summary>
/// Pure probability resolver: given the active bonus id + an item qualified id, decides
/// whether to grant an extra +1 drop. All thresholds from the v1 spec (2026-05-27).
/// Stone = "(O)390", Wood = "(O)388" — excluded from forage_yield_up and mine_drops_up
/// to avoid overloading multi-drop resource nodes.
/// </summary>
public static class BonusDropResolver
{
    private const string Stone = "(O)390";
    private const string Wood  = "(O)388";

    /// <summary>
    /// Roll for an extra drop. Returns true when the extra drop should fire.
    /// <paramref name="rng"/> is the caller's current Game1.random (or test-injected random).
    /// Returns false when <paramref name="bonusId"/> is null or unrecognised.
    /// </summary>
    public static bool ShouldGrantExtraDrop(string bonusId, string itemQualifiedId, Random rng)
    {
        if (bonusId == null) return false;
        return bonusId switch
        {
            "forage_yield_up" => itemQualifiedId != Stone
                                 && itemQualifiedId != Wood
                                 && rng.NextDouble() < 0.25,
            "mine_drops_up"   => itemQualifiedId != Stone
                                 && rng.NextDouble() < 0.30,
            "all_drops_up"    => rng.NextDouble() < 0.10,
            _                 => false
        };
    }
}
```

- [ ] **Step 6: Run tests — expect pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "ActiveEffectsProviderTests|BonusDropResolverTests" -v m
```
Expected: all pass.

- [ ] **Step 7: Full test suite green**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v m
```
Expected: 302+ passed, 0 failed.

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear.Core/ActiveEffectsProvider.cs src/TheLongestYear.Core/BonusDropResolver.cs tests/TheLongestYear.Tests/ActiveEffectsProviderTests.cs tests/TheLongestYear.Tests/BonusDropResolverTests.cs
git commit -m "$(cat <<'EOF'
feat: add ActiveEffectsProvider + BonusDropResolver core types

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Wire ActiveEffectsProvider into RunController

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs`

- [ ] **Step 1: Update `SelectByName` to call `ActiveEffectsProvider.Set`**

In `RunController.cs`, in `SelectByName`, after `Run.Select(theme)` and `PopulateBonusItemsForCurrentSelection()`, add the Set call. Replace the existing `_monitor.Log` call block so the diff is minimal:

```csharp
// After: var (bonus, liability) = ThemeModifiers.For(theme);
ActiveEffectsProvider.Set(bonus, liability);
```

The full updated `SelectByName` method body (replace lines after the offer check):

```csharp
Run.Select(theme);
PopulateBonusItemsForCurrentSelection();
var (bonus, liability) = ThemeModifiers.For(theme);
ActiveEffectsProvider.Set(bonus, liability);
_monitor.Log(
    $"Selected {theme} (bonus {bonus}, liability {liability}). " +
    $"Bonus items this week: [{string.Join(", ", Run.CurrentWeekBonusItems)}].",
    LogLevel.Info);
```

- [ ] **Step 2: Update `OnRunLoaded` to restore or clear active effects**

In `OnRunLoaded`, after `_monitor.Log(...)`, add:

```csharp
// Restore active effects from persisted selection (if any).
if (Run.CurrentSelection.HasValue)
{
    var (bonus, liability) = ThemeModifiers.For(Run.CurrentSelection.Value);
    ActiveEffectsProvider.Set(bonus, liability);
}
else
{
    ActiveEffectsProvider.Clear();
}
```

- [ ] **Step 3: Clear on new run start in `OnDayStarted`**

In the reset path at the top of `OnDayStarted` (inside `if (_pendingReset)`), after `Run.BeginNewRun(NewSeed())`, add:

```csharp
ActiveEffectsProvider.Clear();
```

- [ ] **Step 4: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs
git commit -m "$(cat <<'EOF'
feat: wire ActiveEffectsProvider into RunController selection + load paths

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## SECTION B — Theme Bonus / Liability Effects

---

### Task 3: Forage yield +1 bonus (`forage_yield_up`)

**Files:**
- Create: `src/TheLongestYear/Loop/ForageYieldPatch.cs`

This is the Foraging bonus: 25% chance for +1 on any forage drop, excluding stone/wood.

**Approach:** Postfix `GameLocation.spawnObjects`. After the base runs, iterate all objects in the location that are `IsSpawnedObject == true` and whose `isForage()` returns true (excludes stone/wood by category). For each, call `BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", item.QualifiedItemId, Game1.random)`. If true, call `location.dropObject(clone, tile * 64f, viewport, initialPlacement: true)` to place a duplicate.

**Why postfix spawnObjects vs. intercepting pickup:** Placing the extra item during spawn keeps it visible in the world and pickable by the existing grab path — no need to intercept `checkForAction`.

- [ ] **Step 1: Create the patch**

Create `src/TheLongestYear/Loop/ForageYieldPatch.cs`:

```csharp
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Foraging bonus: 25% chance for +1 on any newly-spawned forage item (excludes stone/wood).
    /// Runs as a postfix on <see cref="GameLocation.spawnObjects"/> so the base spawn logic
    /// runs first and we iterate the resulting IsSpawnedObject pool.
    /// Stone (390) and wood (388) are excluded by BonusDropResolver — the forage_yield_up id
    /// only fires on category-forage items (categories -79, -81, -80, -75, -23 or forage_item tag).
    /// </summary>
    [HarmonyPatch(typeof(GameLocation), "spawnObjects")]
    internal static class ForageYieldPatch
    {
        private static void Postfix(GameLocation __instance)
        {
            if (!ActiveEffectsProvider.ActiveBonus("forage_yield_up"))
                return;

            // Collect tiles to avoid mutating the dictionary while iterating.
            var toBonus = new System.Collections.Generic.List<(Microsoft.Xna.Framework.Vector2 tile, Object obj)>();
            foreach (var pair in __instance.objects.Pairs)
            {
                Object obj = pair.Value;
                if (obj.IsSpawnedObject && obj.isForage())
                    toBonus.Add((pair.Key, obj));
            }

            foreach (var (tile, obj) in toBonus)
            {
                if (!BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", obj.QualifiedItemId, Game1.random))
                    continue;

                // Place a clone adjacent — try up to 8 neighbouring tiles.
                Microsoft.Xna.Framework.Vector2[] offsets = {
                    new Microsoft.Xna.Framework.Vector2(1,0), new Microsoft.Xna.Framework.Vector2(-1,0),
                    new Microsoft.Xna.Framework.Vector2(0,1), new Microsoft.Xna.Framework.Vector2(0,-1),
                    new Microsoft.Xna.Framework.Vector2(1,1), new Microsoft.Xna.Framework.Vector2(-1,1),
                    new Microsoft.Xna.Framework.Vector2(1,-1), new Microsoft.Xna.Framework.Vector2(-1,-1)
                };
                foreach (var offset in offsets)
                {
                    Microsoft.Xna.Framework.Vector2 candidate = tile + offset;
                    if (__instance.objects.ContainsKey(candidate)) continue;
                    if (!__instance.CanItemBePlacedHere(candidate)) continue;
                    Object clone = (Object)obj.getOne();
                    clone.IsSpawnedObject = true;
                    clone.CanBeGrabbed = true;
                    if (__instance.dropObject(clone, candidate * 64f, Game1.viewport, initialPlacement: true))
                        break;
                }
            }
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/ForageYieldPatch.cs
git commit -m "$(cat <<'EOF'
feat: Foraging bonus -- forage_yield_up 25% extra forage spawn patch

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Forage spawn suppression liability (`forage_off`) + mine mushroom suppression

**Files:**
- Create: `src/TheLongestYear/Loop/ForageOffPatch.cs`

The `forage_off` liability applies to: **Mining theme only** (signed-off JC-1). Farming now uses `fish_bite_down` instead.
Effect: no wild produce / mushrooms (including mine mushrooms) / fiddleheads SPAWN. This is a HARD spawn suppression — forage items never appear in the world, not just a drop rate reduction. Stone/wood debris are not "forage" so they continue normally.

**Approach:**
1. Prefix `GameLocation.spawnObjects`. If `forage_off` is active, skip the entire spawn call for outdoor non-MineShaft locations. MineShaft is excluded here — its mushroom path is handled separately.
2. For mine mushrooms: postfix `MineShaft.loadLevel` and remove any `IsSpawnedObject + isForage()` items that were placed in area 80.

**Note on side-effects (JC-4, accepted for v1):** Skipping `spawnObjects` also skips `spawnWeedsAndStones()` (a separate method called from within `spawnObjects`). This means weed/stone debris spawns are also suppressed. This is a known over-suppression. The design says "no wild produce / mushrooms / fiddleheads" — weeds/stones are minor. JC-4 is flagged for playtest review at the bottom of this plan. If too punishing, the fix is a more surgical approach (bump `numberOfSpawnedObjectsOnMap` to cap before the forage loop runs). Stone debris from ResourceClumps is not affected (that's a tool action, not spawn).

- [ ] **Step 1: Create the patch**

Create `src/TheLongestYear/Loop/ForageOffPatch.cs`:

```csharp
using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Liability: forage_off (Mining theme only — JC-1 resolved 2026-05-27).
    /// Suppresses wild forage SPAWN by skipping <see cref="GameLocation.spawnObjects"/> for
    /// non-mine outdoor locations. Mine mushrooms are suppressed separately (see below).
    ///
    /// Side-effect (JC-4, accepted for v1): weed/stone debris spawns live inside
    /// spawnObjects -> spawnWeedsAndStones, so they are also suppressed. Flagged for
    /// playtest review — if too punishing, switch to a more surgical bump of
    /// numberOfSpawnedObjectsOnMap before the forage loop.
    /// </summary>
    [HarmonyPatch(typeof(GameLocation), "spawnObjects")]
    internal static class ForageOffPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(GameLocation __instance)
        {
            if (!ActiveEffectsProvider.ActiveLiability("forage_off"))
                return true; // run original

            // Only suppress forage-capable outdoor locations.
            // MineShaft is excluded here — its mushroom path is handled below.
            if (__instance is MineShaft)
                return true;

            if (!__instance.isOutdoors.Value)
                return true;

            // Skip spawnObjects for this location this day.
            return false;
        }
    }

    /// <summary>
    /// Mine mushroom suppression for forage_off: the mushroom rainbow-light spawn
    /// in <see cref="MineShaft"/> (getMineArea() == 80 path) adds the level to
    /// <see cref="MineShaft.mushroomLevelsGeneratedToday"/>. We can't easily prefix the
    /// anonymous lambda inside loadLevel, so we postfix <see cref="MineShaft.loadLevel"/>
    /// and clear the rainbow-light objects that were just placed.
    ///
    /// Implementation: after loadLevel, if forage_off is active and the mine is in
    /// area 80, iterate objects and remove IsSpawnedObject items whose category is forage.
    /// </summary>
    [HarmonyPatch(typeof(MineShaft), "loadLevel")]
    internal static class MineMushroomForageOffPatch
    {
        private static void Postfix(MineShaft __instance)
        {
            if (!ActiveEffectsProvider.ActiveLiability("forage_off"))
                return;

            if (__instance.getMineArea() != 80)
                return;

            // Remove spawned mushroom objects (IsSpawnedObject + forage category).
            var toRemove = new System.Collections.Generic.List<Microsoft.Xna.Framework.Vector2>();
            foreach (var pair in __instance.objects.Pairs)
            {
                if (pair.Value.IsSpawnedObject && pair.Value.isForage())
                    toRemove.Add(pair.Key);
            }
            foreach (var tile in toRemove)
                __instance.objects.Remove(tile);
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/ForageOffPatch.cs
git commit -m "$(cat <<'EOF'
feat: forage_off liability -- suppress wild forage spawn and mine mushrooms

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 5: Crop growth modifier bonus + liability (`crop_growth_up` / `crop_growth_down`)

**Files:**
- Create: `src/TheLongestYear/Loop/CropGrowthPatch.cs`

`crop_growth_up` (Farming bonus): 25% chance per-day to advance crop one extra phase tick.
`crop_growth_down` (Fishing liability): 25% chance per-day to withhold a day's water tick (treat it as unwatered).

**Approach:** Postfix `Crop.newDay(int state)`. After the base runs, check `fullyGrown.Value` — if crop is already done, no-op. For `crop_growth_up`: if crop is not fully grown and rng < 0.25, advance `dayOfCurrentPhase.Value` by 1 and re-run the phase-advance guard (if `dayOfCurrentPhase >= phaseDays[currentPhase] && currentPhase < phaseDays.Count - 1`, increment phase). For `crop_growth_down`: this needs to be a prefix (to withhold the watered state). Implement as two separate patches with distinct method names to keep Harmony happy.

**Note on Crop.newDay fields:** From the decompile, all are `NetInt` — readable/writeable as `field.Value`. `phaseDays` is `NetIntList`. Access via reflection is NOT required — these are public readonly fields (not hidden properties).

- [ ] **Step 1: Create the patch**

Create `src/TheLongestYear/Loop/CropGrowthPatch.cs`:

```csharp
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Farming bonus: crop_growth_up — 25% chance to grant one extra growth tick per day.
    /// Fishing liability: crop_growth_down — 25% chance to treat a watered crop as unwatered
    /// for today, losing a day's growth.
    /// Both are postfixes on Crop.newDay so we see the result of the base call first.
    /// Note: Farming liability is fish_bite_down (FishBiteRatePatch), not crop_growth_down.
    /// </summary>
    [HarmonyPatch(typeof(Crop), nameof(Crop.newDay))]
    internal static class CropGrowthPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Crop __instance, int state)
        {
            if (__instance.dead.Value) return;
            if (__instance.fullyGrown.Value && __instance.dayOfCurrentPhase.Value > 0) return;

            bool isBonus = ActiveEffectsProvider.ActiveBonus("crop_growth_up");
            bool isLiability = ActiveEffectsProvider.ActiveLiability("crop_growth_down");

            if (isBonus && !__instance.fullyGrown.Value && state == 1)
            {
                // 25% chance: grant extra day tick.
                if (Game1.random.NextDouble() < 0.25)
                {
                    __instance.dayOfCurrentPhase.Value = System.Math.Min(
                        __instance.dayOfCurrentPhase.Value + 1,
                        (__instance.phaseDays.Count > 0)
                            ? __instance.phaseDays[System.Math.Min(__instance.phaseDays.Count - 1, __instance.currentPhase.Value)]
                            : 0);

                    if (__instance.dayOfCurrentPhase.Value >= ((__instance.phaseDays.Count > 0)
                            ? __instance.phaseDays[System.Math.Min(__instance.phaseDays.Count - 1, __instance.currentPhase.Value)]
                            : 0)
                        && __instance.currentPhase.Value < __instance.phaseDays.Count - 1)
                    {
                        __instance.currentPhase.Value++;
                        __instance.dayOfCurrentPhase.Value = 0;
                    }
                }
            }
            else if (isLiability && !__instance.fullyGrown.Value && state == 1)
            {
                // 25% chance: undo today's growth advance (revert dayOfCurrentPhase by 1).
                if (Game1.random.NextDouble() < 0.25)
                {
                    __instance.dayOfCurrentPhase.Value = System.Math.Max(0, __instance.dayOfCurrentPhase.Value - 1);
                }
            }
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/CropGrowthPatch.cs
git commit -m "$(cat <<'EOF'
feat: crop_growth_up/down bonus/liability Crop.newDay patches

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 6: Fish bite rate modifier (`fish_bite_up` / `fish_bite_down`)

**Files:**
- Create: `src/TheLongestYear/Loop/FishBiteRatePatch.cs`

`fish_bite_up` (Fishing bonus): time-until-bite multiplied by 0.77 (~30% faster).
`fish_bite_down` (Farming liability — JC-1 resolved): time-until-bite multiplied by 1.43 (~30% slower). This is the signed-off Farming liability; the old `forage_drops_off` id no longer maps to any theme.

**Two active ids in this patch:** Both `fish_bite_up` (bonus) and `fish_bite_down` (liability) are handled here. They are mutually exclusive (only one theme active per week), so there is no stacking risk.

**Approach:** Postfix `FishingRod.calculateTimeUntilFishingBite` (private method — requires `typeof(FishingRod)` + method name string). Result is a float return value (ref `__result` pattern).

**Android gotcha:** `calculateTimeUntilFishingBite` is private in the decompile. Use `[HarmonyPatch(typeof(FishingRod), "calculateTimeUntilFishingBite")]` — Harmony resolves private by name.

- [ ] **Step 1: Create the patch**

Create `src/TheLongestYear/Loop/FishBiteRatePatch.cs`:

```csharp
using HarmonyLib;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Fishing bonus (fish_bite_up): reduces time-until-first-bite by ~30%
    /// (multiplier 0.77 applied to calculateTimeUntilFishingBite result).
    /// Farming liability (fish_bite_down): increases time-until-first-bite by ~30%
    /// (multiplier 1.43 — inverse of 0.77, per signed-off spec JC-1).
    /// The private method is patched by name — verified present in Android decompile.
    /// </summary>
    [HarmonyPatch(typeof(FishingRod), "calculateTimeUntilFishingBite")]
    internal static class FishBiteRatePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(ref float __result)
        {
            if (ActiveEffectsProvider.ActiveBonus("fish_bite_up"))
                __result *= 0.77f;  // ~30% faster bite

            if (ActiveEffectsProvider.ActiveLiability("fish_bite_down"))
                __result *= 1.43f;  // ~30% slower bite (Farming liability)
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/FishBiteRatePatch.cs
git commit -m "$(cat <<'EOF'
feat: fish_bite_up/down bonus+liability -- FishingRod bite time patch

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 7: Mine drops +1 bonus + mines-closed liability (`mine_drops_up` / `mines_closed`)

**Files:**
- Create: `src/TheLongestYear/Loop/MineDropsPatch.cs`

`mine_drops_up` (Mining bonus): 30% chance for +1 on any mine drop excluding stone.
Effect applies to `ResourceClump.destroy` output (ore veins, boulders) and to regular object hits.

`mines_closed` (Foraging liability — HARD): elevator (tile 112) and descent ladder (tile 173) in MineShaft do not function. Player sees a dialogue explaining the mines are closed this week. This replaces the old `mine_drops_off` id (which was a soft drop-rate penalty); `mines_closed` is a full entry/progress block.

**Approach for `mine_drops_up`:**
Postfix `ResourceClump.destroy`. The destroy method calls `Game1.createMultipleObjectDebris(itemId, x, y, count)` and `Game1.createMultipleItemDebris(item, ...)`. We cannot easily intercept the count before the call. Instead, postfix `destroy` and add extra debris of the main drop type after. We detect context: only in `MineShaft`. Use `Game1.currentLocation is MineShaft`. After the base destroy runs, if `mine_drops_up` is active and rng < 0.30, call `Game1.createObjectDebris` for the contextually appropriate drop: copper (378), iron (380), gold (384), or iridium (386) depending on mine level, or stone (390) excluded, or coal (382).

Actually for a clean implementation: we postfix `ResourceClump.destroy` and if it returned `true` (destroy happened) and we're in a mine, we roll for the extra drop. The ore type is inferred from `parentSheetIndex` of the ResourceClump:
- parentSheetIndex 148/672 = Boulders (stone) — excluded, mine_drops_up excludes stone.
- parentSheetIndex 622 = Ore Deposits: drops iridium ore (386) — bonus applies.
- parentSheetIndex 752/754/756/758 = Meteorite/etc — drops stone (390) — excluded.

For regular ore nodes (Object hits), we need a separate path. `Object.performToolAction` in the mine calls `Game1.createObjectDebris` for ore/coal. Patching `Object.performToolAction` broadly is risky. Instead, use `Game1.createObjectDebris` postfix for mine-context ore drops. But `createObjectDebris` is a static on `Game1` and gets called for many things.

**Simpler approach:** Postfix `MineShaft.checkForBuriedItem` doesn't cover ore. The cleanest option is a postfix on `Object.performToolAction` gated on `Game1.currentLocation is MineShaft` and on the ore item ids. `Object.performToolAction` is virtual and called on every stone hit.

**Final decision:** Gate on `Game1.currentLocation is MineShaft`, only apply in that context, only for ore/coal qualified ids. The qualifying check is: itemQualifiedId is one of "(O)378" copper ore, "(O)380" iron ore, "(O)384" gold ore, "(O)386" iridium ore, "(O)382" coal. Stone "(O)390" excluded by BonusDropResolver already.

For the postfix on `Object.performToolAction`: it returns `bool` (whether destroyed). We only bonus if it returned `true`.

- [ ] **Step 1: Create the patch**

Create `src/TheLongestYear/Loop/MineDropsPatch.cs`:

```csharp
using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Mining bonus (mine_drops_up): 30% extra drop on ore/coal hits inside MineShaft.
    /// Applied as a postfix on Object.performToolAction — fires only when the object was
    /// destroyed (returns true) and the location is a MineShaft.
    ///
    /// Foraging liability (mines_closed — HARD): blocks mine elevator + descent ladder.
    /// Applied as a prefix on MineShaft.checkAction — swallows the action for tile 112
    /// (elevator) and tile 173 (descend ladder) and shows an info dialogue.
    /// This is a hard block: no mine progress is possible this week for Foraging theme.
    /// </summary>
    [HarmonyPatch(typeof(Object), nameof(Object.performToolAction))]
    internal static class MineOreDropBonus
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Object __instance, bool __result)
        {
            if (!__result) return; // object not destroyed
            if (!ActiveEffectsProvider.ActiveBonus("mine_drops_up")) return;
            if (!(Game1.currentLocation is MineShaft)) return;

            string qid = __instance.QualifiedItemId;
            // Apply to ore and coal; stone (390) and wood (388) excluded by resolver.
            if (!BonusDropResolver.ShouldGrantExtraDrop("mine_drops_up", qid, Game1.random))
                return;

            Game1.createObjectDebris(qid,
                (int)__instance.TileLocation.X,
                (int)__instance.TileLocation.Y,
                Game1.player.UniqueMultiplayerID);
        }
    }

    [HarmonyPatch(typeof(MineShaft), nameof(MineShaft.checkAction))]
    internal static class MinesClosedPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static bool Prefix(MineShaft __instance, xTile.Dimensions.Location tileLocation, ref bool __result)
        {
            if (!ActiveEffectsProvider.ActiveLiability("mines_closed"))
                return true;

            int tileIndex = __instance.getTileIndexAt(tileLocation, "Buildings", "mine");

            // Tile 112 = elevator, tile 173 = descend ladder.
            if (tileIndex == 112 || tileIndex == 173)
            {
                Game1.drawObjectDialogue("The mines feel uneasy this week. The elevator and lower ladders will not respond.");
                __result = true;
                return false; // skip original
            }

            return true;
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/MineDropsPatch.cs
git commit -m "$(cat <<'EOF'
feat: mine_drops_up bonus and mines_closed liability (HARD mine block) patches

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 8: All-drops +1 bonus + all-sell-prices -50% liability (`all_drops_up` / `all_sell_prices_down`)

**Files:**
- Create: `src/TheLongestYear/Loop/AllDropsPatch.cs`

`all_drops_up` (Mixed bonus): 10% chance for +1 on any drop including stone/wood. Applies everywhere, not just mines.
`all_sell_prices_down` (Mixed liability): every item sells for 50% of normal price.

**Approach for `all_drops_up`:**
Patch `Object.performToolAction` postfix (already patched above for mine_drops_up, but we need a second postfix in a different class — Harmony allows multiple postfixes on the same method from different classes). Add another patch class.

**Approach for `all_sell_prices_down`:**
Postfix `Object.sellToStorePrice(long specificPlayerID)` — multiply `__result` by 0.5, floor 1.

**Important:** The `all_drops_up` and `mine_drops_up` patches must not double-stack. `BonusDropResolver.ShouldGrantExtraDrop` switches on the bonus id — they're separate ids and only one can be active at a time (one theme per week), so there's no double-stack risk.

- [ ] **Step 1: Create the patch**

Create `src/TheLongestYear/Loop/AllDropsPatch.cs`:

```csharp
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Mixed bonus (all_drops_up): 10% extra drop on ANY tool-destroyed object.
    /// Includes stone and wood unlike forage_yield_up/mine_drops_up.
    ///
    /// Mixed liability (all_sell_prices_down): every Object.sellToStorePrice multiplied by 0.5,
    /// floored at 1g. Applies across all item types.
    /// </summary>
    [HarmonyPatch(typeof(Object), nameof(Object.performToolAction))]
    internal static class AllDropsBonusPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Object __instance, bool __result)
        {
            if (!__result) return;
            if (!ActiveEffectsProvider.ActiveBonus("all_drops_up")) return;
            if (!BonusDropResolver.ShouldGrantExtraDrop("all_drops_up", __instance.QualifiedItemId, Game1.random))
                return;

            Game1.createObjectDebris(__instance.QualifiedItemId,
                (int)__instance.TileLocation.X,
                (int)__instance.TileLocation.Y,
                Game1.player.UniqueMultiplayerID);
        }
    }

    [HarmonyPatch(typeof(Object), nameof(Object.sellToStorePrice))]
    internal static class SellPricePatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(ref int __result)
        {
            if (!ActiveEffectsProvider.ActiveLiability("all_sell_prices_down"))
                return;
            __result = System.Math.Max(1, __result / 2);
        }
    }
}
```

- [ ] **Step 2: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/AllDropsPatch.cs
git commit -m "$(cat <<'EOF'
feat: all_drops_up bonus and all_sell_prices_down liability patches

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## SECTION C — Obtainability Effects

---

### Task 9: WeatherForecast + CartStockPreview Core types + tests

**Files:**
- Create: `src/TheLongestYear.Core/WeatherForecast.cs`
- Create: `src/TheLongestYear.Core/CartStockPreview.cs`
- Create: `tests/TheLongestYear.Tests/WeatherForecastTests.cs`
- Create: `tests/TheLongestYear.Tests/CartStockPreviewTests.cs`

These are pure data generators. They do NOT call any Stardew types — they replicate the deterministic algorithm using the same Random seed that the game uses, so the results match.

**Weather algorithm recap (from decompile):**
- Day 1 of any season = "Sun" (forced).
- DaysPlayed+delta == 3 = "Rain" (but our WeatherModificationsPatch suppresses this for Spring 3 loop resets — irrelevant to foresight since foresight reads future days' rules, not current-run "already corrected" state).
- `isFestivalDay(dayOfMonth, season)` = "Festival".
- Summer and dayOfMonth % 13 == 0 = "Storm".
- `isGreenRainDay(dayOfMonth, season)` = "GreenRain".
- Otherwise: the WeatherConditions in Data/LocationContexts["Default"] are checked with a GameState-query-evaluated random. We cannot replicate the full GSQ engine in Core. **Approximation for v1:** for days not covered by forced rules, show "?" — this is honest and still valuable for festival/storm/forced-rain days. Alternatively, derive from `Utility.CreateDaySaveRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed + offset)`. The rain/storm probability is ~17% from the default conditioned random. For v1, just resolve deterministic forced rules and show "?" for probabilistic ones.

**Cart stock algorithm:**
`Utility.CreateDaySaveRandom()` uses `new Random((int)(uniqueIDForThisGame + stats.DaysPlayed))` inside `ShopBuilder.GetShopStock`. The traveling cart visits on days where `dayOfMonth % 7 % 5 == 0` (Forest.ShouldTravelingMerchantVisitToday). In the loop's week (days 1-7), that's day 5 (Friday) and day 7 (Sunday) if in a 7-day week. Actually: 1%7%5=1%5=1≠0, 5%7%5=5%5=0 ✓, 7%7%5=0%5=0 ✓. So visits on days 5 and 7. The actual stock items are resolved by ShopBuilder against Data/Shops["Traveler"] — we cannot replicate the full item-query resolution in pure Core without importing Stardew types.

**Practical decision for WeatherForecast + CartStockPreview in Core:**
- `WeatherForecast` takes `(int uniqueId, int currentDaysPlayed, int dayOfMonth, int seasonIndex, int slotsToReveal)` and returns a `string[]` of weather labels for the next N days (derived from forced rules only; probabilistic days return "?").
- `CartStockPreview` takes `(int uniqueId, int currentDaysPlayed, int slotsToReveal)` and returns a `string[]` of placeholder strings for now (the actual item resolution requires `ItemQueryResolver` which calls `Game1.content` — mod-side only). The Core type just computes which days the cart will visit this week and how many items to show.

Since CartStockPreview needs actual item names (which require `ItemRegistry.Create` or `ShopBuilder.GetShopStock`), the Core type returns only visit-day metadata. The actual item resolution is done in `WeeklyHubMenu` by calling `ShopBuilder.GetShopStock("Traveler")` at hub-open time.

- [ ] **Step 1: Write failing tests for `WeatherForecast`**

Create `tests/TheLongestYear.Tests/WeatherForecastTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WeatherForecastTests
{
    [Fact]
    public void Day1_of_any_season_is_always_Sun()
    {
        // Spring day 1 = forced Sun regardless of seed.
        var forecast = WeatherForecast.Build(uniqueId: 123456, daysPlayedToday: 5,
            currentDayOfMonth: 28, currentSeasonIndex: 0, slotsToReveal: 7);
        // Next week starts on day 1 of next season — verify slot 0 is Sun.
        // DayOfMonth advances: from day 28, next day is 1 (new season).
        // forecast[0] = tomorrow = day 1 of Summer = "Sun"
        Assert.Equal("Sun", forecast[0]);
    }

    [Fact]
    public void Returns_correct_slot_count()
    {
        var forecast = WeatherForecast.Build(123, 10, 7, 0, 4);
        Assert.Equal(4, forecast.Length);
    }

    [Fact]
    public void Festival_days_return_Festival()
    {
        // Spring 13 = Egg Festival. Build a forecast from Spring 12 (dayOfMonth=12).
        var forecast = WeatherForecast.Build(1, 12, 12, 0, 2);
        // forecast[0] = day 13 of Spring = Festival
        Assert.Equal("Festival", forecast[0]);
    }

    [Fact]
    public void Summer_storm_day_returns_Storm()
    {
        // Summer day 13 % 13 == 0 => Storm. Build from Summer day 12.
        // seasonIndex 1 = Summer.
        var forecast = WeatherForecast.Build(1, 40, 12, 1, 2);
        // forecast[0] = Summer day 13 = Storm
        Assert.Equal("Storm", forecast[0]);
    }

    [Fact]
    public void Slots_beyond_forced_rules_return_unknown_marker()
    {
        // Spring day 2: no forced rule. Should be "?".
        var forecast = WeatherForecast.Build(42, 1, 1, 0, 3);
        // forecast[0] = Spring day 2 = no forced rule = "?"
        Assert.Equal("?", forecast[0]);
    }
}
```

- [ ] **Step 2: Write failing tests for `CartStockPreview`**

Create `tests/TheLongestYear.Tests/CartStockPreviewTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CartStockPreviewTests
{
    [Fact]
    public void CartVisitDays_includes_day5_and_day7()
    {
        // Week starting day 1: cart visits days 5 and 7.
        int[] visitDays = CartStockPreview.CartVisitDaysInWeek(weekStartDay: 1);
        Assert.Contains(5, visitDays);
        Assert.Contains(7, visitDays);
    }

    [Fact]
    public void CartVisitDays_week2_includes_day12_and_day14()
    {
        // Week starting day 8: cart visits days 12 and 14.
        int[] visitDays = CartStockPreview.CartVisitDaysInWeek(weekStartDay: 8);
        Assert.Contains(12, visitDays);
        Assert.Contains(14, visitDays);
    }

    [Fact]
    public void SlotsToReveal_returns_correct_count()
    {
        int slots = CartStockPreview.SlotsToReveal(cartWhispererTier: 2);
        Assert.Equal(4, slots); // tier 2 = 2*2 items
    }

    [Fact]
    public void SlotsToReveal_tier0_returns_0()
    {
        Assert.Equal(0, CartStockPreview.SlotsToReveal(0));
    }
}
```

- [ ] **Step 3: Run failing tests**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "WeatherForecastTests|CartStockPreviewTests" -v m
```
Expected: fail with type-not-found errors.

- [ ] **Step 4: Implement `WeatherForecast`**

The forced weather rules from `Game1.getWeatherModificationsForDate` decompile:
- `date.DayOfMonth == 1 || stats.DaysPlayed + num <= 4` → "Sun"
- `stats.DaysPlayed + num == 3` → "Rain" (suppressed by our patch for Spring loop, but shown in foresight)
- `isGreenRainDay(dayOfMonth, season)` → "GreenRain" — green rain is Spring 14+/Summer only (replicable without GSQ)
- `date.Season == Summer && date.DayOfMonth % 13 == 0` → "Storm"
- `isFestivalDay(dayOfMonth, season)` → "Festival"

For foresight purposes, `isFestivalDay` maps to known fixed festival dates (Spring 13, Spring 24, Summer 11, Summer 28, Fall 16, Fall 27, Winter 8, Winter 25). Green rain days in 1.6 are determined by `Utility.isGreenRainDay` which checks `Game1.netWorldState.Value.GreenRainDay` — not predictable in Core. For v1, approximate as "?" for green rain.

Create `src/TheLongestYear.Core/WeatherForecast.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>
/// Deterministic weather forecast for the next N days, computed without calling any
/// Stardew game types. Resolves forced-rule days (festivals, forced sun day 1, summer storms)
/// and marks probabilistic days as "?" so the UI can display partial information honestly.
/// Used by WeeklyHubMenu when Weather Sage upgrades are owned.
/// </summary>
public static class WeatherForecast
{
    // Fixed festival days per season (vanilla 1.6 defaults; ignores SVE/modded festivals).
    private static readonly int[] SpringFestivals = { 13, 24 };
    private static readonly int[] SummerFestivals = { 11, 28 };
    private static readonly int[] FallFestivals   = { 16, 27 };
    private static readonly int[] WinterFestivals = { 8, 25 };

    /// <summary>
    /// Build the N-slot forecast starting from tomorrow (relative to the current game state).
    /// Returns array of weather label strings: "Sun", "Rain", "Storm", "Festival", "GreenRain", "?".
    /// </summary>
    /// <param name="uniqueId">Game1.uniqueIDForThisGame (for seed; unused in v1 forced-rule-only logic)</param>
    /// <param name="daysPlayedToday">Game1.stats.DaysPlayed today</param>
    /// <param name="currentDayOfMonth">today's day-of-month (1-28)</param>
    /// <param name="currentSeasonIndex">0=Spring 1=Summer 2=Fall 3=Winter</param>
    /// <param name="slotsToReveal">how many days forward to compute (from Weather Sage tier)</param>
    public static string[] Build(int uniqueId, int daysPlayedToday, int currentDayOfMonth,
        int currentSeasonIndex, int slotsToReveal)
    {
        var result = new string[slotsToReveal];
        int dayOfMonth = currentDayOfMonth;
        int seasonIndex = currentSeasonIndex;
        int daysPlayed = daysPlayedToday;

        for (int slot = 0; slot < slotsToReveal; slot++)
        {
            // Advance one day.
            dayOfMonth++;
            daysPlayed++;
            if (dayOfMonth > 28)
            {
                dayOfMonth = 1;
                seasonIndex = (seasonIndex + 1) % 4;
            }

            result[slot] = ResolveDay(daysPlayed, daysPlayedToday, dayOfMonth, seasonIndex);
        }

        return result;
    }

    private static string ResolveDay(int daysPlayed, int baseDaysPlayedToday, int dayOfMonth, int seasonIndex)
    {
        // Rule: day 1 of any season OR DaysPlayed <= 4 = forced Sun.
        if (dayOfMonth == 1 || daysPlayed <= 4)
            return "Sun";

        // Rule: DaysPlayed == 3 = forced Rain (vanilla day-3 rule for Demetrius event).
        // We show it as "Rain" in foresight even though our WeatherModificationsPatch
        // may suppress it in-game — the player sees what vanilla intended.
        if (daysPlayed == 3)
            return "Rain";

        // Rule: festival day.
        if (IsFestival(dayOfMonth, seasonIndex))
            return "Festival";

        // Rule: Summer storm (dayOfMonth % 13 == 0).
        if (seasonIndex == 1 && dayOfMonth % 13 == 0)
            return "Storm";

        // All other days: probabilistic (can't determine without GSQ engine).
        return "?";
    }

    private static bool IsFestival(int dayOfMonth, int seasonIndex)
    {
        int[] festivals = seasonIndex switch
        {
            0 => SpringFestivals,
            1 => SummerFestivals,
            2 => FallFestivals,
            3 => WinterFestivals,
            _ => System.Array.Empty<int>()
        };
        foreach (int fd in festivals)
            if (fd == dayOfMonth) return true;
        return false;
    }
}
```

- [ ] **Step 5: Implement `CartStockPreview`**

Create `src/TheLongestYear.Core/CartStockPreview.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>
/// Cart Whisperer foresight helper. Computes which days this week the Traveling Cart visits
/// and how many item slots the player can preview. Item name resolution is done mod-side
/// via ShopBuilder.GetShopStock("Traveler") — this Core type is pure metadata.
///
/// The cart visits on days where <c>dayOfMonth % 7 % 5 == 0</c>
/// (Forest.ShouldTravelingMerchantVisitToday in the decompile).
/// In a 7-day week (days 1-7): day 5 and day 7 visit.
/// In week 2 (days 8-14): days 12 and 14. Week 3: 19 and 21. Week 4: 26 and 28.
/// </summary>
public static class CartStockPreview
{
    /// <summary>Cart visit days within the week starting at <paramref name="weekStartDay"/>.</summary>
    public static int[] CartVisitDaysInWeek(int weekStartDay)
    {
        var days = new System.Collections.Generic.List<int>();
        for (int d = weekStartDay; d < weekStartDay + 7; d++)
        {
            if (d % 7 % 5 == 0)
                days.Add(d);
        }
        return days.ToArray();
    }

    /// <summary>Total preview slots granted by tier N Cart Whisperer (tier N = 2N items).</summary>
    public static int SlotsToReveal(int cartWhispererTier)
        => cartWhispererTier * 2;
}
```

- [ ] **Step 6: Run failing tests — expect pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "WeatherForecastTests|CartStockPreviewTests" -v m
```
Expected: all pass.

- [ ] **Step 7: Full suite**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v m
```
Expected: all pass.

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear.Core/WeatherForecast.cs src/TheLongestYear.Core/CartStockPreview.cs tests/TheLongestYear.Tests/WeatherForecastTests.cs tests/TheLongestYear.Tests/CartStockPreviewTests.cs
git commit -m "$(cat <<'EOF'
feat: WeatherForecast + CartStockPreview core types with tests

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## SECTION D — Obtainability: Mixed Seeds Injection + Rare Fish

---

### Task 10: Mixed Seeds Red Cabbage + Starfruit injection (`cult_red_cabbage` / `cult_starfruit`)

**Files:**
- Create: `src/TheLongestYear/Loop/MixedSeedsPatch.cs`

**Mechanism:** Mixed Seeds `(O)770` in vanilla 1.6 are resolved by `Crop.cs` via `CropData` `IsWildSeedCrop` → `getRandomWildCropForSeason()`. This method picks the season's wild crop at random from the season's crop data. We want to inject Red Cabbage `(O)266` and Starfruit `(O)398` into the Summer pool.

From the decompile, `getRandomWildCropForSeason()` is in `Crop.cs`. Let's look it up:

The actual injection point: `Crop.getRandomWildCropForSeason()` picks a random item from the season's "IsWildSeedCrop" items. In 1.6 this is data-driven. We can postfix this method to sometimes return Red Cabbage / Starfruit instead, when in Summer and the upgrade is owned.

**Approach:** Postfix `Crop.getRandomWildCropForSeason`. If `__result` is a Summer wild crop id, and the player is in Summer, roll a small chance (10%) to substitute with Red Cabbage (if owned) or Starfruit (if owned + Red Cabbage also owned). The upgrade ownership is read from a static accessor we add to MetaState.

**Static accessor pattern:** We need to check `MetaState.HasUpgrade(id)` from mod-side without a full dependency chain. Add a static `Func<string, bool>` accessor in the patch file, set by `ModEntry.OnSaveLoaded`, similar to how `CookbookInteractable.ConnectTo` works.

- [ ] **Step 1: Check getRandomWildCropForSeason signature**

```bash
grep -n "getRandomWildCropForSeason\|WildCrop\|wildCrop\|wildSeed" "C:/Users/Jeff/Documents/Projects/decompiler/stardew-valley-android/decompiled/StardewValley/StardewValley/Crop.cs" | head -20
```
Expected: see the method signature. If the method doesn't exist in the decompile, use `Crop.newDay` postfix instead (the path that sets `replaceWithObjectOnFullGrown.Value` or picks `getRandomWildCropForSeason()`).

- [ ] **Step 2: Verify approach based on decompile result**

Read the output from Step 1. The decompile line in `Crop.newDay` (line ~866 in our earlier read):
```csharp
Object @object = ItemRegistry.Create<Object>(replaceWithObjectOnFullGrown.Value ?? getRandomWildCropForSeason());
```

This means we can postfix `Crop.newDay` and check if a wild-seed crop just matured. But modifying the already-spawned object is tricky. 

**Better approach:** Postfix `Crop.getRandomWildCropForSeason` (if it's a distinct method) to intercept the return value. If we can't find it, use a prefix on `GameLocation.dropObject` for wild seed crops in Summer.

Run the grep first (Step 1), then choose the approach. If `getRandomWildCropForSeason` exists and is patchable:

- [ ] **Step 3: Create the patch**

Create `src/TheLongestYear/Loop/MixedSeedsPatch.cs`:

```csharp
using HarmonyLib;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Obtainability upgrades: inject Red Cabbage (266) and Starfruit (398) into the Summer
    /// Mixed Seeds pool when the player owns cult_red_cabbage / cult_starfruit respectively.
    ///
    /// Hooks <see cref="Crop.getRandomWildCropForSeason"/> (or the wild-seed crop resolution
    /// path in Crop.newDay if the method is not directly patchable). 10% substitution chance
    /// per upgrade, applied only in Summer.
    ///
    /// Upgrade ownership is read via <see cref="UpgradeChecker"/>, a static Func wired by
    /// ModEntry.OnSaveLoaded to avoid importing MetaStore into the patch.
    /// </summary>
    internal static class UpgradeChecker
    {
        /// <summary>Set by ModEntry.OnSaveLoaded: returns true if the given upgrade id is owned.</summary>
        public static System.Func<string, bool> HasUpgrade;
    }

    [HarmonyPatch(typeof(Crop), "getRandomWildCropForSeason")]
    internal static class MixedSeedsPatch
    {
        private static void Postfix(ref string __result)
        {
            if (UpgradeChecker.HasUpgrade == null) return;
            if (Game1.season != StardewValley.Season.Summer) return;

            // Try Starfruit first (requires Red Cabbage as prereq).
            if (UpgradeChecker.HasUpgrade("cult_starfruit")
                && UpgradeChecker.HasUpgrade("cult_red_cabbage")
                && Game1.random.NextDouble() < 0.10)
            {
                __result = "(O)398"; // Starfruit seed id
                return;
            }

            // Try Red Cabbage.
            if (UpgradeChecker.HasUpgrade("cult_red_cabbage")
                && Game1.random.NextDouble() < 0.10)
            {
                __result = "(O)266"; // Red Cabbage seed id
            }
        }
    }
}
```

- [ ] **Step 4: Wire `UpgradeChecker.HasUpgrade` in `ModEntry.OnSaveLoaded`**

In `src/TheLongestYear/ModEntry.cs`, inside `OnSaveLoaded`, after `_meta.Load()`:

```csharp
MixedSeedsPatch.UpgradeChecker.HasUpgrade = id => _meta.State.HasUpgrade(id);
```

Add the `using TheLongestYear.Loop;` if not present.

- [ ] **Step 5: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors. If `getRandomWildCropForSeason` is not found by Harmony (method doesn't exist in Android runtime), the patch is silently skipped — that's acceptable for v1; the method exists in the PC DLL per vanilla CropData flow.

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear/Loop/MixedSeedsPatch.cs src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
feat: Mixed Seeds Red Cabbage/Starfruit injection for Obtainability upgrades

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 11: Rare fish catch boost (`fortune_rare_fish`)

**Files:**
- Create: `src/TheLongestYear/Loop/RareFishPatch.cs`

**Effect:** +25% chance to catch a rare fish when `fortune_rare_fish` upgrade is owned.

**Rare fish definition:** In vanilla 1.6, rarity is determined by the fishing quality outcome path in `FishingRod`. The "rare fish" catch in this context means boosting the probability that the selected fish is from the rare/special pool. The fish selection happens in `FishingRod.openTreasureMenuEndFunction` and related. The actual fish pick is in the catch outcome logic.

**Practical approach for v1:** Postfix `FishingRod.calculateTimeUntilFishingBite` with a secondary modifier is wrong (that just makes fish bite faster). The actual fish rarity boost needs to intercept the fish-type resolution. This is complex (involves `Utility.getRandomFishForBait` or similar).

**Simpler v1 approach that's honest with the spec:** The `fortune_rare_fish` description says "+25% rare fish catch chance". Implement this as a bite-time multiplier of 0.75 specifically when a rare-fish condition is met — but we can't determine fish rarity before casting. 

**Clean v1 approach:** Add a second multiplier in `FishBiteRatePatch` gated on the upgrade ownership — faster bite means more attempts = effectively higher catch rate of any fish including rare. This is mechanically a bit wrong vs. the description. 

**Alternative clean approach:** Postfix `FishingRod` on the catch outcome code path. In the decompile the fish is selected in a large anonymous method. 

**Decision for v1:** Since we cannot cleanly intercept the fish-rarity resolution without a large investigation, implement as a bite rate boost (0.75 multiplier) when `fortune_rare_fish` is owned, independent of the active week bonus. The description becomes behaviorally aligned: you get more fishing attempts per session, effectively catching more rare fish. Add a `[HarmonyPatch]` class to `FishBiteRatePatch.cs` rather than a new file.

- [ ] **Step 1: Update `FishBiteRatePatch.cs` to add the permanent upgrade effect**

Edit `src/TheLongestYear/Loop/FishBiteRatePatch.cs` — in the `Postfix` method, after the existing `fish_bite_up` block:

```csharp
// fortune_rare_fish upgrade (permanent, not theme-gated): additional 25% faster bite.
if (UpgradeChecker.HasUpgrade != null && UpgradeChecker.HasUpgrade("fortune_rare_fish"))
    __result *= 0.75f;
```

The full updated Postfix:

```csharp
private static void Postfix(ref float __result)
{
    if (ActiveEffectsProvider.ActiveBonus("fish_bite_up"))
        __result *= 0.77f;

    // fortune_rare_fish: permanent +25% bite speed (all weeks, not just Fishing week).
    if (UpgradeChecker.HasUpgrade != null && UpgradeChecker.HasUpgrade("fortune_rare_fish"))
        __result *= 0.75f;
}
```

- [ ] **Step 2: Add `using TheLongestYear.Loop;` to the file if needed**

`UpgradeChecker` is in the `TheLongestYear.Loop` namespace, same as `FishBiteRatePatch`, so no extra using needed.

- [ ] **Step 3: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/FishBiteRatePatch.cs
git commit -m "$(cat <<'EOF'
feat: fortune_rare_fish upgrade -- 25% faster bite via FishBiteRatePatch

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## SECTION E — Foresight: Weather Sage + Cart Whisperer Hub Delivery

---

### Task 12: Tier helpers in RunController + MenuLauncher forwarding

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs`
- Modify: `src/TheLongestYear/UI/MenuLauncher.cs`

The hub needs to know how many weather slots and cart slots to show, derived from owned upgrades. These helpers live in `RunController` (which already has `_store`).

- [ ] **Step 1: Add tier helpers to `RunController`**

In `src/TheLongestYear/Loop/RunController.cs`, add two public methods after `BonusListSizeForCurrentSeason`:

```csharp
/// <summary>
/// Number of weather preview slots to show on the planning hub this week.
/// Equals the highest Weather Sage tier owned (weather_sage_1 through weather_sage_7).
/// Returns 0 if none owned.
/// </summary>
public int WeatherSageTier()
    => _store.State.HighestKeptTier("weather_sage_", 7);

/// <summary>
/// Number of Traveling Cart item slots to preview on the planning hub.
/// Equals 2 * highest Cart Whisperer tier owned (cart_whisper_1 through cart_whisper_3).
/// Returns 0 if none owned.
/// </summary>
public int CartPreviewSlots()
    => CartStockPreview.SlotsToReveal(_store.State.HighestKeptTier("cart_whisper_", 10));
```

Add `using TheLongestYear.Core;` if not already present (it is, per the existing file).

- [ ] **Step 2: Update `MenuLauncher.OpenWeeklyHub` to pass tier data to hub**

In `src/TheLongestYear/UI/MenuLauncher.cs`, find `OpenWeeklyHub` and update the `WeeklyHubMenu` constructor call to pass weather + cart tier ints. First read the current `MenuLauncher.cs`:

```bash
# Read the file to see the current OpenWeeklyHub method
```

The hub constructor currently takes `(monitor, runController, config, run, requirements, offer, offerSeason, isPreSelectForNextMonth)`. We need to add `weatherSageSlots` and `cartPreviewSlots` parameters.

Update `WeeklyHubMenu` constructor call to:
```csharp
new WeeklyHubMenu(
    _monitor, _runController, _config, _meta.Run, 
    _runController.Requirements, offer, 
    offerSeason, isPreSelectForNextMonth,
    weatherSageSlots: _runController.WeatherSageTier(),
    cartPreviewSlots: _runController.CartPreviewSlots())
```

- [ ] **Step 3: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: build errors because `WeeklyHubMenu` constructor doesn't have those params yet — fix in Task 13.

- [ ] **Step 4: Commit (WIP — will fix in Task 13)**

Skip commit — Task 13 will complete the wiring and produce the first clean build.

---

### Task 13: WeeklyHubMenu foresight row wiring

**Files:**
- Modify: `src/TheLongestYear/UI/WeeklyHubMenu.cs`
- Modify: `src/TheLongestYear/UI/MenuLauncher.cs`

Replace the placeholder `DrawPreviewRow` calls with real data. Replace config-based row count with the new tier-based approach.

- [ ] **Step 1: Read current MenuLauncher.cs to find the exact constructor call**

```bash
grep -n "OpenWeeklyHub\|WeeklyHubMenu" "C:/Users/Jeff/Documents/Projects/Stardee Valoo/TheLongestYear/src/TheLongestYear/UI/MenuLauncher.cs"
```

- [ ] **Step 2: Update `WeeklyHubMenu` constructor to accept foresight slot counts**

In `WeeklyHubMenu.cs`, update the constructor signature to add `int weatherSageSlots = 0, int cartPreviewSlots = 0`:

```csharp
public WeeklyHubMenu(IMonitor monitor, RunController runController, GameplayConfig config,
    RunState run, IReadOnlyList<BundleRequirement> requirements, IReadOnlyList<Theme> offer,
    CoreSeason? offerSeason = null, bool isPreSelectForNextMonth = false,
    int weatherSageSlots = 0, int cartPreviewSlots = 0)
    : base(0, 0, 0, 0, showUpperRightCloseButton: false)
```

Replace `_config.DefaultWeatherPreviewSlots` and `_config.DefaultCartPreviewSlots` throughout the file with the new parameters stored in instance fields:

```csharp
private readonly int _weatherSageSlots;
private readonly int _cartPreviewSlots;
```

Assign in constructor body:
```csharp
_weatherSageSlots = weatherSageSlots;
_cartPreviewSlots = cartPreviewSlots;
```

Replace all `_config.DefaultWeatherPreviewSlots` with `_weatherSageSlots` and `_config.DefaultCartPreviewSlots` with `_cartPreviewSlots` throughout the file.

- [ ] **Step 3: Add weather + cart data fields**

After the existing `_cartRows` list, add:

```csharp
private string[] _weatherForecast;
private System.Collections.Generic.List<StardewValley.ISalable> _cartItems;
```

- [ ] **Step 4: Populate foresight data in constructor**

After `_weatherSageSlots` and `_cartPreviewSlots` are assigned, add:

```csharp
_weatherForecast = _weatherSageSlots > 0
    ? WeatherForecast.Build(
        (int)Game1.uniqueIDForThisGame,
        (int)Game1.stats.DaysPlayed,
        Game1.dayOfMonth,
        (int)Game1.season,
        _weatherSageSlots)
    : System.Array.Empty<string>();

_cartItems = new System.Collections.Generic.List<StardewValley.ISalable>();
if (_cartPreviewSlots > 0)
{
    try
    {
        var stock = StardewValley.Internal.ShopBuilder.GetShopStock("Traveler");
        int taken = 0;
        foreach (var pair in stock)
        {
            if (taken >= _cartPreviewSlots) break;
            _cartItems.Add(pair.Key);
            taken++;
        }
    }
    catch (System.Exception ex)
    {
        _monitor.Log($"WeeklyHubMenu: failed to build cart preview: {ex.Message}", StardewModdingAPI.LogLevel.Warn);
    }
}
```

Add `using TheLongestYear.Core;` and `using StardewValley.Internal;` at the top if not present.

- [ ] **Step 5: Replace placeholder `DrawPreviewRow` calls with real data**

In the `draw` method, replace the weather loop:

```csharp
// OLD:
for (int i = 0; i < _weatherRows.Count; i++)
    DrawPreviewRow(b, _weatherRows[i], $"Weather, day {i + 1}: ???   (Weather Sage tier {i + 1} reveals this)");

// NEW:
for (int i = 0; i < _weatherRows.Count; i++)
{
    string label = (i < _weatherForecast.Length) ? _weatherForecast[i] : "?";
    DrawPreviewRow(b, _weatherRows[i], $"Day {i + 1}: {label}");
}
```

Replace the cart loop:

```csharp
// OLD:
for (int i = 0; i < _cartRows.Count; i++)
    DrawPreviewRow(b, _cartRows[i], $"Cart slot {i + 1}: ???   (Cart Whisperer reveals this)");

// NEW:
for (int i = 0; i < _cartRows.Count; i++)
{
    string label = (i < _cartItems.Count && _cartItems[i] != null)
        ? _cartItems[i].DisplayName
        : "?";
    DrawPreviewRow(b, _cartRows[i], $"Cart: {label}");
}
```

- [ ] **Step 6: Update `MenuLauncher.OpenWeeklyHub` to pass the real counts**

In `MenuLauncher.cs`, update the `WeeklyHubMenu` constructor call in `OpenWeeklyHub` to pass:

```csharp
weatherSageSlots: _runController.WeatherSageTier(),
cartPreviewSlots: _runController.CartPreviewSlots()
```

- [ ] **Step 7: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 8: Full test suite**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v m
```
Expected: all pass.

- [ ] **Step 9: Commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/UI/MenuLauncher.cs src/TheLongestYear/UI/WeeklyHubMenu.cs
git commit -m "$(cat <<'EOF'
feat: wire Weather Sage + Cart Whisperer foresight into WeeklyHubMenu

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## SECTION F — Integration verification

---

### Task 14: tly_ debug commands for theme effects verification

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs`

Add a `tly_activeeffects` debug command so the developer can verify the active bonus/liability from the SMAPI console without reading logs.

- [ ] **Step 1: Add the command in `ModEntry.Entry`**

In `Entry`, after the existing console command registrations:

```csharp
helper.ConsoleCommands.Add("tly_activeeffects",
    "Print the currently active theme bonus and liability.",
    this.CmdActiveEffects);
```

- [ ] **Step 2: Add the handler**

After `CmdOpenCraftbook`:

```csharp
private void CmdActiveEffects(string command, string[] args)
{
    string bonus = TheLongestYear.Core.ActiveEffectsProvider.BonusId ?? "(none)";
    string liability = TheLongestYear.Core.ActiveEffectsProvider.LiabilityId ?? "(none)";
    this.Monitor.Log(
        $"Active effects: bonus={bonus}, liability={liability}. " +
        $"Selection={_meta?.Run.CurrentSelection?.ToString() ?? "none"}.",
        LogLevel.Info);
}
```

- [ ] **Step 3: Wire to debug bridge**

In `ExecuteDebugLine`, add the case:

```csharp
case "tly_activeeffects": this.CmdActiveEffects(command, args); break;
```

- [ ] **Step 4: Build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```
Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
feat: tly_activeeffects debug command for theme effect verification

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 15: Full-stack build + test verification

**Files:** no changes

- [ ] **Step 1: Clean build**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:Configuration=Release
```
Expected: 0 errors, 0 warnings that reference our new files.

- [ ] **Step 2: Full test suite**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v m
```
Expected: 310+ passed, 0 failed (approximate — exact count depends on test additions).

- [ ] **Step 3: Review SMAPI log for Harmony patch registration**

After deploying to device (user's job per workflow) and loading a save, the SMAPI log should contain lines like:
```
[The Longest Year] Patching StardewValley.Crop.newDay with prefix HarmonyPatch ...
[The Longest Year] Patching StardewValley.Tools.FishingRod.calculateTimeUntilFishingBite ...
```
No "FAILED to patch" lines expected.

- [ ] **Step 4: Verify active effects via console after selecting a theme**

```
tly_select Foraging
tly_activeeffects
```
Expected log: `Active effects: bonus=forage_yield_up, liability=mines_closed. Selection=Foraging.`

```
tly_select Farming
tly_activeeffects
```
Expected log: `Active effects: bonus=crop_growth_up, liability=fish_bite_down. Selection=Farming.`

```
tly_select Mining
tly_activeeffects
```
Expected log: `Active effects: bonus=mine_drops_up, liability=forage_off. Selection=Mining.`

- [ ] **Step 5: Commit final verification note**

```bash
git commit --allow-empty -m "$(cat <<'EOF'
chore: Plan 06 theme effects layer complete -- all tests pass

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-review

### 1. Spec coverage

| Spec item | Modifier id | Task that wires it |
|---|---|---|
| Foraging: +25% forage yield | `forage_yield_up` | Task 3 (ForageYieldPatch) |
| Foraging: Mines Closed liability (HARD) | `mines_closed` | Task 7 (MinesClosedPatch) |
| Farming: +25% crop growth | `crop_growth_up` | Task 5 (CropGrowthPatch bonus) |
| Farming: -30% fish bite rate | `fish_bite_down` | Task 6 (FishBiteRatePatch liability branch) |
| Fishing: +30% fish bite rate | `fish_bite_up` | Task 6 (FishBiteRatePatch bonus branch) |
| Fishing: -25% crop growth | `crop_growth_down` | Task 5 (CropGrowthPatch liability) |
| Mining: +30% mine drops | `mine_drops_up` | Task 7 (MineOreDropBonus) |
| Mining: Forage Off liability (HARD spawn) | `forage_off` | Task 4 (ForageOffPatch) |
| Mixed: +10% all drops | `all_drops_up` | Task 8 (AllDropsBonusPatch) |
| Mixed: -50% all sell prices | `all_sell_prices_down` | Task 8 (SellPricePatch) |
| cult_red_cabbage | — | Task 10 (MixedSeedsPatch) |
| cult_starfruit | — | Task 10 (MixedSeedsPatch) |
| fortune_rare_fish | — | Task 11 (FishBiteRatePatch extension) |
| Weather Sage N days | — | Tasks 9, 12, 13 |
| Cart Whisperer N items | — | Tasks 9, 12, 13 |
| ActiveEffectsProvider scaffold | — | Task 1 |
| Wire scaffold into RunController | — | Task 2 |
| Debug command | — | Task 14 |

**ThemeModifiers.cs corrected in Task 0.5** — all ids now match the signed-off spec. No gaps remain.

### 2. Placeholder scan

None found. All steps contain actual code.

### 3. Type consistency

- `ActiveEffectsProvider.BonusId` / `.LiabilityId` / `.Set` / `.Clear` / `.ActiveBonus` / `.ActiveLiability` — consistent across Tasks 1, 2, 3, 4, 5, 6, 7, 8.
- `BonusDropResolver.ShouldGrantExtraDrop(string bonusId, string itemQualifiedId, Random rng)` — consistent across Tasks 1, 3, 7, 8.
- `WeatherForecast.Build(...)` — consistent between Tasks 9 and 13.
- `CartStockPreview.SlotsToReveal(int tier)` / `.CartVisitDaysInWeek(int weekStartDay)` — consistent between Tasks 9, 12, 13.
- `UpgradeChecker.HasUpgrade` — consistent between Tasks 10 and 11.
- `_weatherSageSlots` / `_cartPreviewSlots` — consistent in Tasks 12 and 13.

---

## Judgment calls — resolutions (2026-05-27)

### JC-1: ThemeModifiers mismatch — RESOLVED, spec table wins

**Original issue:** ThemeModifiers.cs had three mismatches vs. the signed-off spec:
- Foraging liability: `mine_drops_off` (soft drop nerf) vs. signed-off `mines_closed` (HARD block)
- Farming liability: `forage_drops_off` vs. signed-off `fish_bite_down` (-30% bite rate)
- Mining liability: `forage_drops_off` (same id as Farming, ambiguous) vs. signed-off `forage_off` (distinct HARD spawn suppression)

**Resolution:** Spec table wins. Task 0.5 corrects all three in ThemeModifiers.cs and its tests. All effect tasks use the corrected ids. Old ids (`mine_drops_off`, `forage_drops_off`) moved to legacy block in `DisplayNameFor()` — they return a "(legacy)" label so old saves don't display raw ids.

### JC-2: `fortune_rare_fish` implemented as bite speed, not rarity boost — ACCEPTED for v1

The upgrade description says "+25% rare fish catch chance" but the implementation (Task 11) applies a 0.75× bite-rate multiplier. This is because the fish-type selection path is deep in `FishingRod` anonymous methods and is not cleanly patchable in v1 scope.

**Impact:** Players fish faster (all fish, not just rare), which statistically yields more rare catches. Not precisely "+25% rare fish probability" but close in practice.

**Follow-up (TODO.md):** LY2 could revisit via `Utility.getFish` intercept for true rarity boost. Add a code comment in `FishBiteRatePatch.cs` noting this approximation. Add a TODO.md entry:
> `fortune_rare_fish` v1 approximation: implemented as 0.75x bite-rate multiplier, not true rarity boost. LY2: investigate Utility.getFish intercept.

### JC-3: MixedSeedsPatch depends on `getRandomWildCropForSeason` existing — FALLBACK NOTE KEPT

Task 10 Step 1 greps the decompile to verify the method exists. If it's absent in the Android runtime, the patch is silently skipped (Harmony skips patches on missing methods). Executor: if the grep returns no results, use a `Crop.newDay` postfix on the `replaceWithObjectOnFullGrown` path instead.

### JC-4: `forage_off` suppresses weed/stone debris spawns — ACCEPTED for v1, flag for playtest

Task 4's prefix skips the entire `spawnObjects` call, which also skips `spawnWeedsAndStones`. The spec says "no wild produce / mushrooms / fiddleheads spawn" — weeds/stones are a side-effect.

**Accepted for v1.** Code comment added in ForageOffPatch.cs noting this side-effect.

**Playtest checkpoint (TODO.md):** After deploying Mining-week with `forage_off` active, check whether the absence of weed/stone spawns feels too punishing alongside the forage block. If yes, switch to a surgical approach: bump `numberOfSpawnedObjectsOnMap` to cap before forage loop runs (keeps weed/stone spawns, suppresses only forage). Add a TODO.md entry:
> JC-4 playtest: forage_off (Mining liability) skips spawnWeedsAndStones. Check if this feels too punishing in the first Mining week. If yes, surgical fix: bump numberOfSpawnedObjectsOnMap to cap in prefix instead of returning false.

---

## Post-revision notes (appended 2026-05-27)

### Playtest checkpoints

**JC-2 checkpoint — fortune_rare_fish:**
After a Fishing week with `fortune_rare_fish` owned, verify fish bite time is visibly faster. Note that rare fish frequency appears higher (by proxy of faster bites) but is not a direct probability increase. If the user wants true rarity, escalate to LY2 scope (Utility.getFish intercept).

**JC-4 checkpoint — forage_off side-effects:**
After a Mining week (first week `forage_off` is active), note whether the farm feels too empty without weed/stone spawns. If so, apply the surgical fix described above. This is a quick code change once playtest feedback is available.
