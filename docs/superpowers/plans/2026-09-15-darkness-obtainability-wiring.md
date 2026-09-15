# Darkness Rework Part B (obtainability wiring) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the approved darkness rework (one nightly roll, one event per strike, the Darkness dial, blight by level, the guaranteed Winter tamper, the stack limits) and make reversion and tampering consult the obtainability model against the real save, by Darkness level.

**Architecture:** Every rule is pure C# in `src/TheLongestYear.Core/Sabotage/` and tested against a fake `ObtainabilityModel` built from hand-made `ObtainSource` records. The glue (`src/TheLongestYear/Loop/SabotageService.cs`, `SpoilagePass.cs`, a new `SaveSnapshotReader.cs`) reads the game and calls the rules. The obtainability model is read only through `ObtainabilityModel.Sources`, `ObtainSource.Lands`, `Conditions` and `Setup`; nothing under `Core/Obtainability/` changes.

**Tech Stack:** .NET 6, C# 10, xUnit, SMAPI 4, Stardew Valley 1.6 (PC decompile at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley`).

**Spec:** `docs/superpowers/specs/2026-09-15-darkness-obtainability-wiring-design.md` (Part B). Read it first; also `docs/superpowers/specs/2026-09-14-darkness-rework-design.md` and `docs/superpowers/specs/2026-09-09-darkness-pushback-design.md` for the effects this keeps.

## Global Constraints

- Branch `story` only. `git checkout story` before anything. Never merge to master, never release, never touch Nexus.
- **Do not change `manifest.json`'s `Version`** (feature branch; the release line owns bumps).
- **Push to `origin story` after every commit:** `git push origin story`.
- **No em dash characters (U+2014) anywhere:** code, comments, strings, docs, commit messages. Use a comma, a colon or "to". Check with `python -c "import sys;print(open(sys.argv[1],encoding='utf-8').read().count('\u2014'))" <file>` before committing.
- **Android paths**, if one is ever needed (none should be), always start with `/storage/emulated/0/`; the legacy symlink form is banned everywhere.
- **The blind guard stays.** `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs` lists the blind files. Do not edit any file under `src/TheLongestYear.Core/Obtainability/`, nor `src/TheLongestYear/Loop/GameObtainabilityData.cs` or `GameObtainabilityParsing.cs`. Darkness code reads the model through its public API only. `ObtainabilityComparison.cs` stays the only file naming both `ObtainabilityModel` and `ItemAvailabilityModel` for comparison; `SabotageService.cs` may reference both (it reads effort from the old model and fairness from the new one) but never compares them.
- **Board generation, gates, goals and pacing must not read the model.** Do not touch `ItemPoolBuilder`, `BundleSlotFiller`, `BoardRequirements`, `GateEvaluator`, `GoalObtainability`, `BundleDeadlines`, `QuantityAskPass`, `AuthoredBundleComposer`, `BundleGenerationTuning`, `PacingWeek`, `BonusItemSampler`, `BonusSlotSampler`.
- **Test command (run from the repo root, must stay green after every task):**
  `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false`
  2285 passing at the start.
- **If a test's expected value looks wrong, stop and report** the task, the test and your reasoning. Never change an assertion to match your code. (Rewriting an OLD test because the SPEC changed the number, as Task 2 does for blight counts, is allowed and is called out explicitly there.)
- **The PC 1.6 decompile wins over any plan text.** If a game fact here is wrong, follow the decompile and say so in the commit message.
- **Live checks** are automated runs on a throwaway `None_*` save via `tly_loadsave` (runbook `docs/HEADLESS_DRIVING.md`). Never `PuffPuff_*` or `Cheatside_*` saves, never `tly_skipscene`, never the mouse or keyboard. Say plainly in reports that the launch was the agent's.
- **Player-facing strings** (i18n) go through the game-writing skill. The two this plan adds are already drafted in Task 8 in that register; do not add others without it.
- Commit message trailer on every commit:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_014nCkXCiwy6ympotfpBwy41
  ```
- Repo root for every path below: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`.

## File Structure

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/DifficultySettings.cs` (modify) | The eleventh dial `Darkness` (nullable in JSON so an old config is detectable), `LowestDial()`, `MigrateDarkness()`, `DarknessOrLowest`. |
| `src/TheLongestYear.Core/DifficultyProfile.cs` (modify) | Stamped `DarknessStep` (nullable) and `Darkness` accessor deriving the lowest dial for an old stamp. |
| `src/TheLongestYear.Core/DifficultyResolver.cs` (modify) | Resolves `DarknessStep`. |
| `src/TheLongestYear.Core/Sabotage/DarknessLevels.cs` (create) | Per-level numbers: blight share and caps, unmoderated chance, storage reach. |
| `src/TheLongestYear.Core/Sabotage/SabotageSchedule.cs` (modify) | `SabotageTuning` gains the night roll numbers, the gap tables and loses the per-front chances; `Rng` gains a night overload. |
| `src/TheLongestYear.Core/Sabotage/SabotageRules.cs` (modify) | `BlightRule.Count`/`SpoilCount` by level; `ReversionRule.Pick` with a fairness test; `TamperRule.MaxCount` (legendary and basis limits). |
| `src/TheLongestYear.Core/Sabotage/NightRoll.cs` (create) | The one nightly roll: season chance, decay and weekly reset, even split with fall-through, the guaranteed Winter tamper, the unmoderated roll. |
| `src/TheLongestYear.Core/Sabotage/SaveSnapshot.cs` (create) | The real-save snapshot record. |
| `src/TheLongestYear.Core/Sabotage/FairnessRule.cs` (create) | "Does this item count at this level from this day to this deadline", route by route, with an explanation. |
| `src/TheLongestYear.Core/RunState.cs` (modify) | `DarknessChanceWeek`, `DarknessChance`, `UnmoderatedReversionSpent`, `UnmoderatedTamperSpent`, `GuaranteedTamperDone`, reset. |
| `src/TheLongestYear.Core/MetaState.cs` (modify) | `FirstWinterTamperSeen` (per save). |
| `src/TheLongestYear/Loop/SaveSnapshotReader.cs` (create) | Builds a `SaveSnapshot` from `Game1`. |
| `src/TheLongestYear/Loop/SabotageService.cs` (modify) | One night roll, the fairness picker, the guaranteed tamper, the catalog-based tamper pool, the stack limits, `Status`, `Explain`. |
| `src/TheLongestYear/Loop/SpoilagePass.cs` (modify) | The Extreme pool (all chest items, placed farm machines, 3 units each). |
| `src/TheLongestYear/ModEntry.cs` (modify) | Config migration, GMCM dial, `tly_difficulty` row, `tly_sabotage fair`, service wiring. |
| `src/TheLongestYear/i18n/default.json` (modify) | Two GMCM strings. |
| `tests/TheLongestYear.Tests/DarknessDialTests.cs` (create) | Dial, migration, profile. |
| `tests/TheLongestYear.Tests/DarknessLevelsTests.cs` (create) | Blight by level, stack limits. |
| `tests/TheLongestYear.Tests/NightRollTests.cs` (create) | Roll, decay, split, guaranteed tamper, unmoderated roll. |
| `tests/TheLongestYear.Tests/FairnessRuleTests.cs` (create) | Every cell of the spec's 1.2 table. |
| `tests/TheLongestYear.Tests/SabotageTests.cs` (modify) | Blight count tests rewritten to the new numbers; reversion fairness pick. |
| `tests/TheLongestYear.Tests/BoardPathsNeverReadTheModelTests.cs` (create) | Static guard: the board files never name the model or the darkness. |

---

### Task 0: Baseline board output for the byte-identical proof

**Files:**
- Create: `.superpowers/sdd/2026-09-15-darkness-obtainability-wiring/baseline-genbundles.txt`
- Create: `.superpowers/sdd/2026-09-15-darkness-obtainability-wiring/progress.md`

The game is currently running minimized on the throwaway save `None_449077472` from an automated run (STATUS.md). The build it runs is story HEAD before this plan (bf6a116 or the spec commit 5516e36, which changed no code). Record what the board commands print now, so Task 9 can prove nothing moved.

- [ ] **Step 1: Confirm the game is up and on the throwaway save**

Run (PowerShell, repo root):
```
pwsh -NoProfile -File tools/bridge.ps1 -Action count
Select-String -Path "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" -Pattern "Run \d+ ready|loaded save|None_449077472" | Select-Object -Last 3
```
Expected: a line count, and a recent line naming `None_449077472`. If the game is not running, launch it yourself with `pwsh -NoProfile -File tools/deploy.ps1 -Minimized -NoBuild` if that switch exists, otherwise `tools/deploy.ps1 -Minimized` (it rebuilds the current HEAD, which is fine: no code has changed yet), wait for the bridge banner, then send `tly_loadsave None_449077472` and wait for `Run \d+ ready` plus 45 seconds. This is the agent's launch, not Jeff's.

- [ ] **Step 2: Capture the board output for three seed loops and the gate audit**

```
$n = (pwsh -NoProfile -File tools/bridge.ps1 -Action count)
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_genbundles 1|tly_genbundles 2|tly_genbundles 3|tly_gatecheck"
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "gatecheck|IMPOSSIBLE|FREE|audit" -TimeoutSec 120 -FromLine $n
```
Then copy every log line after line `$n` that contains `tly_genbundles`, `genbundles`, `Room`, `bundle`, `slot`, `determinism`, `gatecheck` or `gate` into the baseline file, stripping the timestamp prefix (`[HH:MM:SS LEVEL The Longest Year] `) so a later diff compares content only:
```
Get-Content "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" | Select-Object -Skip $n |
  Where-Object { $_ -match "genbundles|Room|bundle|slot|determinism|gatecheck|gate" } |
  ForEach-Object { $_ -replace '^\[\d\d:\d\d:\d\d [A-Z]+\s+The Longest Year\] ', '' } |
  Set-Content .superpowers/sdd/2026-09-15-darkness-obtainability-wiring/baseline-genbundles.txt
```
Expected: a non-empty file with three generated boards and the gate audit.

- [ ] **Step 3: Start the ledger**

Write `progress.md` with one line: `Task 0: baseline captured (<line count> lines) from the running game on None_449077472, build <git rev-parse --short HEAD>`.

- [ ] **Step 4: Commit and push**

```
git add .superpowers/sdd/2026-09-15-darkness-obtainability-wiring/
git commit -m "Part B: baseline board output for the byte-identical proof"
git push origin story
```

---

### Task 1: The Darkness dial in Core

**Files:**
- Modify: `src/TheLongestYear.Core/DifficultySettings.cs`
- Modify: `src/TheLongestYear.Core/DifficultyProfile.cs`
- Modify: `src/TheLongestYear.Core/DifficultyResolver.cs`
- Test: `tests/TheLongestYear.Tests/DarknessDialTests.cs`

**Interfaces:**
- Produces: `DifficultySettings.Darkness` (`DifficultyStep?`), `DifficultySettings.DarknessOrLowest` (`DifficultyStep`), `DifficultySettings.LowestDial()`, `DifficultySettings.MigrateDarkness()` (`bool`, true when it changed something), `DifficultyProfile.DarknessStep` (`DifficultyStep?`), `DifficultyProfile.Darkness` (`DifficultyStep`).

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/DarknessDialTests.cs`:

```csharp
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The eleventh dial, Darkness (spec 2026-09-15 Part B, section 2.3): set by the overall
/// lever with the other ten, stamped like the rest, and migrated to the LOWEST of the ten existing
/// dials when a config or a stamp predates it.</summary>
public class DarknessDialTests
{
    [Fact]
    public void A_fresh_config_reads_darkness_as_normal_until_it_is_set()
    {
        // The JSON key is absent on a fresh config too, so the property is null and the
        // accessor derives Normal from nine Normal dials; the first migration pins it.
        var settings = new DifficultySettings();
        Assert.Null(settings.Darkness);
        Assert.Equal(DifficultyStep.Normal, settings.DarknessOrLowest);
        Assert.True(settings.MigrateDarkness());
        Assert.Equal(DifficultyStep.Normal, settings.Darkness);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Extreme)]
    public void SetAll_sets_darkness_too(DifficultyStep step)
    {
        var settings = new DifficultySettings();
        settings.SetAll(step);
        Assert.Equal(step, settings.Darkness);
    }

    [Fact]
    public void Clone_carries_darkness()
        => Assert.Equal(DifficultyStep.Hard, new DifficultySettings { Darkness = DifficultyStep.Hard }.Clone().Darkness);

    [Fact]
    public void Lowest_dial_is_the_minimum_of_the_ten_and_ignores_the_lever_and_darkness()
    {
        var settings = new DifficultySettings { Overall = DifficultyStep.Extreme, Darkness = DifficultyStep.Extreme };
        settings.SetAll(DifficultyStep.Hard);
        settings.CartSlots = DifficultyStep.Easy;
        settings.Darkness = DifficultyStep.Extreme;
        Assert.Equal(DifficultyStep.Easy, settings.LowestDial());
    }

    [Fact]
    public void An_old_config_without_the_dial_migrates_to_the_lowest_dial_and_moves_the_lever()
    {
        var settings = JsonSerializer.Deserialize<DifficultySettings>(
            "{\"Overall\":3,\"StackSize\":3,\"QualityAsks\":3,\"RequiredSlots\":1,\"ItemRarity\":3}")!;
        Assert.Null(settings.Darkness);

        Assert.True(settings.MigrateDarkness());

        Assert.Equal(DifficultyStep.Normal, settings.Darkness);
        Assert.Equal(DifficultyStep.Normal, settings.Overall);
        Assert.False(settings.MigrateDarkness());
    }

    [Fact]
    public void A_config_that_already_has_the_dial_is_left_alone()
    {
        var settings = new DifficultySettings { Overall = DifficultyStep.Hard, Darkness = DifficultyStep.Extreme };
        Assert.False(settings.MigrateDarkness());
        Assert.Equal(DifficultyStep.Extreme, settings.Darkness);
        Assert.Equal(DifficultyStep.Hard, settings.Overall);
    }

    [Fact]
    public void The_profile_stamps_the_darkness_step()
    {
        var settings = new DifficultySettings { Darkness = DifficultyStep.Hard };
        DifficultyProfile profile = DifficultyResolver.Resolve(settings, new GameplayConfig());
        Assert.Equal(DifficultyStep.Hard, profile.DarknessStep);
        Assert.Equal(DifficultyStep.Hard, profile.Darkness);
    }

    [Fact]
    public void An_old_stamp_without_the_step_derives_the_lowest_dial()
    {
        var profile = JsonSerializer.Deserialize<DifficultyProfile>(
            "{\"StackFactor\":1.5,\"Steps\":{\"StackSize\":2,\"QualityAsks\":2,\"RequiredSlots\":2,\"ItemRarity\":2,\"JpEarned\":2,\"ShrinePrices\":2,\"StartingGold\":0,\"CartSlots\":2,\"HoldPrices\":2}}")!;
        Assert.Null(profile.DarknessStep);
        Assert.Equal(DifficultyStep.Easy, profile.Darkness);
    }

    [Fact]
    public void Darkness_off_normal_counts_as_a_changed_difficulty()
        => Assert.False(new DifficultySettings { Darkness = DifficultyStep.Hard }.IsAllNormal());
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false --filter "FullyQualifiedName~DarknessDialTests"`
Expected: build errors (`Darkness`, `LowestDial`, `MigrateDarkness`, `DarknessStep` not defined).

- [ ] **Step 3: Add the dial to DifficultySettings**

In `src/TheLongestYear.Core/DifficultySettings.cs`:

Add `using System;` and `using System.Linq;` at the top. Inside `SetAll`, after `HoldPrices = step;` add `Darkness = step;`. After the `HoldPrices` property add:

```csharp
    // ---- The darkness (spec 2026-09-15 darkness-obtainability-wiring, section 2.3) ----

    /// <summary>How hard the darkness hits from Summer on: blight share and caps, the fairness
    /// picker's level, the unmoderated roll. NULLABLE in the JSON on purpose: a config written
    /// before the dial existed has no key, and <see cref="MigrateDarkness"/> must be able to tell
    /// that apart from a player who chose Normal. NO initializer: a C# initializer would fill the
    /// missing key with Normal and hide the old config. Read through <see cref="DarknessOrLowest"/>.</summary>
    public DifficultyStep? Darkness { get; set; }

    /// <summary>The dial as gameplay reads it: the value, or the lowest of the ten when unset.</summary>
    public DifficultyStep DarknessOrLowest => Darkness ?? LowestDial();

    /// <summary>The lowest of the TEN original dials (never the lever, never Darkness itself).</summary>
    public DifficultyStep LowestDial()
        => new[]
        {
            StackSize, QualityAsks, RequiredSlots, ItemRarity, JpEarned, ShrinePrices, StartingGold,
            CartSlots, HoldPrices,
        }.Min();

    /// <summary>Migration (Jeff, 2026-09-14): a config from before the dial sets Darkness AND the
    /// overall lever to the lowest of the ten existing dials. True when something changed.</summary>
    public bool MigrateDarkness()
    {
        if (Darkness != null) return false;
        DifficultyStep lowest = LowestDial();
        Darkness = lowest;
        Overall = lowest;
        return true;
    }
```

Note: `DifficultySettings` names nine dials plus StackSize in `SetAll`; count them: StackSize, QualityAsks, RequiredSlots, ItemRarity, JpEarned, ShrinePrices, StartingGold, CartSlots, HoldPrices is nine. The docs say "ten" because SeasonPity was retired (see the comment in the file). Keep the word "ten" in comments where it already exists; the `LowestDial` array lists the nine that exist. Do not invent a tenth.

In `IsAllNormal()` add `&& DarknessOrLowest == DifficultyStep.Normal` at the end of the chain. In `Clone()` add `Darkness = Darkness,`.

- [ ] **Step 4: Stamp it in DifficultyProfile and DifficultyResolver**

In `src/TheLongestYear.Core/DifficultyProfile.cs`, after `HoldPriceFactor` add:

```csharp
    /// <summary>The Darkness dial this loop runs under (spec 2026-09-15 Part B). The step itself is
    /// the resolved value: every darkness number is keyed by it in <c>DarknessLevels</c>. Nullable so
    /// a stamp written before the dial existed reads through <see cref="Darkness"/> as the lowest of
    /// its ten dials, the same migration a config gets.</summary>
    public DifficultyStep? DarknessStep { get; set; }

    /// <summary>The level gameplay reads.</summary>
    public DifficultyStep Darkness => DarknessStep ?? Steps.LowestDial();
```

In `DifficultyResolver.Resolve`, after `HoldPriceFactor = ...,` add `DarknessStep = settings.DarknessOrLowest,`.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false`
Expected: all pass (2285 + 10 new). `DifficultyResolver.Resolve` always writes an explicit `DarknessStep`, so a resolved profile is never null there; if `DifficultyResolverTests` compares a resolved profile against a hand-built `new DifficultyProfile()` field by field, that hand-built one now has a null `DarknessStep`: report it before editing, do not paper over it.

- [ ] **Step 6: Commit and push**

```
git add src/TheLongestYear.Core/DifficultySettings.cs src/TheLongestYear.Core/DifficultyProfile.cs src/TheLongestYear.Core/DifficultyResolver.cs tests/TheLongestYear.Tests/DarknessDialTests.cs
git commit -m "Darkness: the eleventh difficulty dial, stamped and migrated to the lowest dial"
git push origin story
```

---

### Task 2: Darkness numbers by level, blight by level, stack limits

**Files:**
- Create: `src/TheLongestYear.Core/Sabotage/DarknessLevels.cs`
- Modify: `src/TheLongestYear.Core/Sabotage/SabotageSchedule.cs` (the `SabotageTuning` class inside it)
- Modify: `src/TheLongestYear.Core/Sabotage/SabotageRules.cs`
- Test: `tests/TheLongestYear.Tests/DarknessLevelsTests.cs`, `tests/TheLongestYear.Tests/SabotageTests.cs`

**Interfaces:**
- Produces: `DarknessLevels.BlightShare(DifficultyStep)`, `DarknessLevels.BlightCap(DifficultyStep, Season)`, `DarknessLevels.UnmoderatedChance(DifficultyStep)`, `DarknessLevels.StorageReachesEverything(DifficultyStep)`, `DarknessLevels.BigCraftableUnits` (const 3); `BlightRule.Count(int liveCrops, Season season, DifficultyStep level)`, `BlightRule.SpoilCount(int storedUnits, Season season, DifficultyStep level)`; `TamperRule.MaxCount(string itemId, double? basisByWinter)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/DarknessLevelsTests.cs`:

```csharp
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-15 Part B, sections 2.4 to 2.6: the numbers keyed by the Darkness dial.</summary>
public class DarknessLevelsTests
{
    [Theory]
    [InlineData(DifficultyStep.Easy, 0.04, 8, 12)]
    [InlineData(DifficultyStep.Normal, 0.05, 10, 15)]
    [InlineData(DifficultyStep.Hard, 0.06, 12, 18)]
    [InlineData(DifficultyStep.Extreme, 0.07, 14, 21)]
    public void Blight_share_and_caps_by_level(DifficultyStep level, double share, int summerCap, int otherCap)
    {
        Assert.Equal(share, DarknessLevels.BlightShare(level));
        Assert.Equal(summerCap, DarknessLevels.BlightCap(level, Season.Summer));
        Assert.Equal(otherCap, DarknessLevels.BlightCap(level, Season.Fall));
        Assert.Equal(otherCap, DarknessLevels.BlightCap(level, Season.Winter));
    }

    [Theory]
    [InlineData(0, Season.Summer, DifficultyStep.Normal, 0)]
    [InlineData(1, Season.Summer, DifficultyStep.Normal, 1)]
    [InlineData(10, Season.Summer, DifficultyStep.Normal, 1)]     // 5% of 10 rounds up to 1
    [InlineData(100, Season.Summer, DifficultyStep.Normal, 5)]
    [InlineData(1000, Season.Summer, DifficultyStep.Normal, 10)]  // Summer cap
    [InlineData(1000, Season.Fall, DifficultyStep.Normal, 15)]    // Fall cap
    [InlineData(100, Season.Winter, DifficultyStep.Easy, 4)]
    [InlineData(1000, Season.Winter, DifficultyStep.Extreme, 21)]
    [InlineData(100, Season.Summer, DifficultyStep.Extreme, 7)]
    public void Crop_count_is_the_level_share_clamped(int crops, Season season, DifficultyStep level, int expected)
        => Assert.Equal(expected, BlightRule.Count(crops, season, level));

    [Theory]
    [InlineData(0, Season.Summer, DifficultyStep.Normal, 0)]
    [InlineData(1, Season.Summer, DifficultyStep.Normal, 1)]
    [InlineData(100, Season.Summer, DifficultyStep.Normal, 5)]
    [InlineData(10000, Season.Summer, DifficultyStep.Normal, 10)]
    [InlineData(10000, Season.Fall, DifficultyStep.Hard, 18)]
    public void Storage_count_uses_the_same_share_and_caps(int units, Season season, DifficultyStep level, int expected)
        => Assert.Equal(expected, BlightRule.SpoilCount(units, season, level));

    [Theory]
    [InlineData(DifficultyStep.Easy, 0.0)]
    [InlineData(DifficultyStep.Normal, 0.0)]
    [InlineData(DifficultyStep.Hard, 0.10)]
    [InlineData(DifficultyStep.Extreme, 0.30)]
    public void Unmoderated_chance_by_level(DifficultyStep level, double chance)
        => Assert.Equal(chance, DarknessLevels.UnmoderatedChance(level));

    [Fact]
    public void Only_extreme_reaches_tools_and_machines()
    {
        Assert.False(DarknessLevels.StorageReachesEverything(DifficultyStep.Hard));
        Assert.True(DarknessLevels.StorageReachesEverything(DifficultyStep.Extreme));
        Assert.Equal(3, DarknessLevels.BigCraftableUnits);
    }

    [Fact]
    public void A_legendary_fish_is_always_one()
        => Assert.Equal(1, TamperRule.MaxCount("(O)163", 40.0));   // Legend

    [Fact]
    public void No_basis_means_no_max_so_the_stack_rule_asks_for_one()
    {
        Assert.Equal(0, TamperRule.MaxCount("(O)24", null));
        Assert.Equal(1, TamperRule.Stack(0, 2, DifficultyStep.Extreme, new System.Random(1)));
    }

    [Fact]
    public void Max_count_is_the_boards_own_ceiling_of_the_basis()
        => Assert.Equal(8, TamperRule.MaxCount("(O)24", 10.0));     // ceil(10 * 0.8)
}
```

- [ ] **Step 2: Rewrite the two old blight-count theories in SabotageTests.cs**

The spec changed these numbers (share 5% Normal, caps 10/15, chest blight on the same table). This is a SPEC change, not an assertion looking wrong. In `tests/TheLongestYear.Tests/SabotageTests.cs`, replace the theory `Count_is_a_share_of_the_field_clamped_to_a_nibble` and the theory `Storage_loss_is_a_share_of_stored_units_clamped` (lines 120 to 138) with:

```csharp
    [Fact]
    public void Count_and_spoil_count_are_now_by_level_and_pinned_in_DarknessLevelsTests()
    {
        Assert.Equal(5, BlightRule.Count(100, Season.Summer, DifficultyStep.Normal));
        Assert.Equal(5, BlightRule.SpoilCount(100, Season.Summer, DifficultyStep.Normal));
    }
```

- [ ] **Step 3: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false --filter "FullyQualifiedName~DarknessLevelsTests|FullyQualifiedName~BlightRuleTests"`
Expected: build errors (`DarknessLevels` missing; `Count` has no three-argument overload).

- [ ] **Step 4: Create DarknessLevels**

Create `src/TheLongestYear.Core/Sabotage/DarknessLevels.cs`:

```csharp
namespace TheLongestYear.Core.Sabotage;

/// <summary>Every number the Darkness dial changes (spec 2026-09-15 darkness-obtainability-wiring,
/// sections 1.5, 2.4 and 2.5). Jeff's numbers, 2026-09-14 and 2026-09-15; retune here.</summary>
public static class DarknessLevels
{
    private const double ShareEasy = 0.04, ShareNormal = 0.05, ShareHard = 0.06, ShareExtreme = 0.07;
    private const int SummerCapEasy = 8, SummerCapNormal = 10, SummerCapHard = 12, SummerCapExtreme = 14;
    private const int OtherCapEasy = 12, OtherCapNormal = 15, OtherCapHard = 18, OtherCapExtreme = 21;
    private const double UnmoderatedHard = 0.10, UnmoderatedExtreme = 0.30;

    /// <summary>A placed or stored machine counts as this many units when chest blight picks it
    /// (Jeff, 2026-09-15: "so they can't be decimated in one night").</summary>
    public const int BigCraftableUnits = 3;

    /// <summary>Share of live crops (crop blight) or stored units (chest blight) a strike takes.</summary>
    public static double BlightShare(DifficultyStep level) => level switch
    {
        DifficultyStep.Easy => ShareEasy,
        DifficultyStep.Hard => ShareHard,
        DifficultyStep.Extreme => ShareExtreme,
        _ => ShareNormal,
    };

    /// <summary>The most a strike takes; Summer is gentler than Fall and Winter.</summary>
    public static int BlightCap(DifficultyStep level, Season season)
        => season == Season.Summer
            ? level switch
            {
                DifficultyStep.Easy => SummerCapEasy,
                DifficultyStep.Hard => SummerCapHard,
                DifficultyStep.Extreme => SummerCapExtreme,
                _ => SummerCapNormal,
            }
            : level switch
            {
                DifficultyStep.Easy => OtherCapEasy,
                DifficultyStep.Hard => OtherCapHard,
                DifficultyStep.Extreme => OtherCapExtreme,
                _ => OtherCapNormal,
            };

    /// <summary>The chance a reversion or tamper ignores the fairness picker (once per loop each).
    /// Jeff, 2026-09-15: "10% chance for hard, 30% on extreme".</summary>
    public static double UnmoderatedChance(DifficultyStep level) => level switch
    {
        DifficultyStep.Hard => UnmoderatedHard,
        DifficultyStep.Extreme => UnmoderatedExtreme,
        _ => 0.0,
    };

    /// <summary>On Extreme, chest blight can take tools, weapons, rings, boots, hats, stored machines
    /// and machines placed on the farm (spec section 2.5).</summary>
    public static bool StorageReachesEverything(DifficultyStep level) => level == DifficultyStep.Extreme;
}
```

- [ ] **Step 5: Retire the old blight numbers in SabotageTuning and add the new ones**

In `src/TheLongestYear.Core/Sabotage/SabotageSchedule.cs`, inside `SabotageTuning`, delete these constants: `BlightChanceSummer`, `BlightChanceFall`, `BlightChanceWinter`, `BlightShareSummer`, `BlightShareFall`, `BlightShareWinter`, `BlightMaxSummer`, `BlightMaxFall`, `BlightMaxWinter`, `SpoilShare`, `SpoilMinPerNight`, `SpoilMaxPerNight`, `ReversionChanceFall`, `ReversionChanceWinter`, `TamperChance`. Keep `BlightMinPerNight`, `BlightNightsPerWeek`, `ReversionQuietFromDay`, `TamperPerSeason`, `TamperMinDaysApart`, `TamperQuietFromDay`, `TamperCandidatePool`, `TamperQuality`, `TamperSliceWidth`. Add, in their place:

```csharp
    // The one nightly roll (spec 2026-09-15 Part B, section 2.1; Jeff's numbers 2026-09-14).
    public const double NightChanceSummer = 0.25;
    public const double NightChanceFall = 0.35;
    public const double NightChanceWinter = 0.35;
    /// <summary>Each strike lowers the week's chance by this much until the week resets.</summary>
    public const double NightChanceDecay = 0.05;

    // The gap tables the fairness picker adds on Normal and Hard (spec section 1.3; Jeff's first
    // draft, 2026-09-15). Index = skill level; days for a player deliberately working the skill.
    public static readonly int[] SkillDaysToLevel = { 0, 1, 2, 3, 5, 7, 10, 14, 19, 25, 32 };
    /// <summary>Regular mine: one day per this many floors below the deepest reached ("call it 10
    /// floors per day, we want it to be a stretch").</summary>
    public const int MineFloorsPerDay = 10;
    /// <summary>A missing machine the player can craft costs this many days on Normal.</summary>
    public const int MachineCraftDays = 1;
    /// <summary>Skull Cavern is a condition, not a wait: Staircases need this Mining level.</summary>
    public const int StaircaseMiningLevel = 2;
```

Then `SabotageSchedule.NightlyChance` no longer compiles (it read the deleted constants). Delete `NightlyChance` and `StrikesTonight` from `SabotageSchedule` entirely (Task 3's `NightRoll` replaces them), and delete the tests `A_closed_season_never_strikes_even_with_a_willing_die`, `Reversion_and_tampering_keep_quiet_at_the_end_of_a_season` and `The_nights_die_is_fixed_by_seed_day_and_front` from `SabotageScheduleTests` ONLY IF they call `StrikesTonight`; keep any that only call `IsOpen`, `IsQuietDay`, `WithinCaps`, `RecordStrike`, `Rng`. Rewrite `Reversion_and_tampering_keep_quiet_at_the_end_of_a_season` to call `IsQuietDay` directly:

```csharp
    [Fact]
    public void Reversion_and_tampering_keep_quiet_at_the_end_of_a_season()
    {
        Assert.False(SabotageSchedule.IsQuietDay(SabotageKind.Reversion, 24));
        Assert.True(SabotageSchedule.IsQuietDay(SabotageKind.Reversion, 25));
        Assert.False(SabotageSchedule.IsQuietDay(SabotageKind.Tampering, 20));
        Assert.True(SabotageSchedule.IsQuietDay(SabotageKind.Tampering, 21));
    }
```

and `A_closed_season_never_strikes_even_with_a_willing_die` to:

```csharp
    [Fact]
    public void A_closed_season_is_closed()
    {
        Assert.False(SabotageSchedule.IsOpen(SabotageKind.Blight, Season.Spring));
        Assert.False(SabotageSchedule.IsOpen(SabotageKind.Tampering, Season.Fall));
    }
```

If `The_nights_die_is_fixed_by_seed_day_and_front` only calls `Rng`, keep it. The glue (`SabotageService.RunNight`) also calls `StrikesTonight`; it will not compile until Task 6 rewrites it. To keep the build green in THIS task, leave a minimal shim in `SabotageSchedule`:

```csharp
    /// <summary>Transitional: the three-dice roll the rework replaces. Task 6 removes the last
    /// caller; NightRoll owns the real decision.</summary>
    [System.Obsolete("Replaced by NightRoll; removed with the SabotageService rewrite.")]
    public static bool StrikesTonight(SabotageKind kind, RunState run, Season season, int dayOfMonth, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (!IsOpen(kind, season) || IsQuietDay(kind, dayOfMonth)) return false;
        int week = Calendar.WeekOfYear((int)season, dayOfMonth);
        int day = Calendar.DayOfYear((int)season, dayOfMonth);
        return WithinCaps(kind, run, week, day) && rng.NextDouble() < NightRoll.SeasonChance(season);
    }
```

That shim references `NightRoll.SeasonChance`, which Task 3 creates. So in THIS task write the shim against a temporary private helper instead: replace `NightRoll.SeasonChance(season)` with `SabotageTuning.NightChanceFall` (any constant; the shim is dead within two tasks). Suppress the obsolete warning at the call site in `SabotageService.RunNight` with `#pragma warning disable CS0618` / `restore` around the three calls, since the project may treat warnings as errors (check `Directory.Build.props` / the csproj for `TreatWarningsAsErrors`; if it is not set, the pragma is still harmless).

- [ ] **Step 6: Blight by level and the tamper max count in SabotageRules**

In `src/TheLongestYear.Core/Sabotage/SabotageRules.cs`, replace `BlightRule.Count` and `BlightRule.SpoilCount` with:

```csharp
    /// <summary>How many crops die on a strike: the level's share of the live crops, at least one,
    /// capped by season and level (spec 2026-09-15 Part B, section 2.4).</summary>
    public static int Count(int liveCrops, Season season, DifficultyStep level)
        => Take(liveCrops, season, level);

    /// <summary>How many stored units go on a chest strike, from the total in unwarded chests (the
    /// Junimo Stash excluded by the caller). Same share and caps as crops.</summary>
    public static int SpoilCount(int storedUnits, Season season, DifficultyStep level)
        => Take(storedUnits, season, level);

    private static int Take(int have, Season season, DifficultyStep level)
    {
        if (have <= 0) return 0;
        int n = (int)Math.Ceiling(have * DarknessLevels.BlightShare(level));
        n = Math.Max(SabotageTuning.BlightMinPerNight, Math.Min(DarknessLevels.BlightCap(level, season), n));
        return Math.Min(n, have);
    }
```

In `TamperRule`, add above `Stack`:

```csharp
    /// <summary>The most the hall may ask for: legendary fish are always one (LegendaryFishRules,
    /// the rule QuantityAskPass already applies and the old tamper path skipped); otherwise the
    /// board's own ceiling, <see cref="AskBands.Ceiling"/> of the item's basis by Winter 28; 0 when
    /// the item has no basis, which <see cref="Stack"/> turns into an ask of one.</summary>
    public static int MaxCount(string itemId, double? basisByWinter)
    {
        if (LegendaryFishRules.IsLegendary(itemId)) return 1;
        if (basisByWinter is not double basis || basis <= 0) return 0;
        return (int)Math.Ceiling(basis * AskBands.Ceiling);
    }
```

Then `Stack(1, ...)` for a legendary: `remainder = 1 / weeksPassed`, fraction up to 0.4, rounds to 0, floored to 1. Good: legendary stays 1 at every level and week. Add this test to `DarknessLevelsTests`:

```csharp
    [Theory]
    [InlineData(1, DifficultyStep.Extreme)]
    [InlineData(4, DifficultyStep.Easy)]
    public void A_legendary_stays_one_through_the_stack_roll(int week, DifficultyStep level)
    {
        for (int seed = 0; seed < 20; seed++)
            Assert.Equal(1, TamperRule.Stack(TamperRule.MaxCount("(O)775", 30.0), week, level, new System.Random(seed)));
    }
```

- [ ] **Step 7: Fix the glue call sites so the solution builds**

`src/TheLongestYear/Loop/BlightPass.cs` `CountFor(CoreSeason season)` calls `BlightRule.Count(count, season)`: change its signature to `CountFor(CoreSeason season, DifficultyStep level)` and pass `level`. `SabotageService.RunNight` calls `BlightPass.CountFor(season)` and `BlightRule.SpoilCount(SpoilagePass.StoredUnits())`; `ModEntry.CmdSabotage` case `blight` calls both too. In both, read the level as `Meta.EffectiveDifficulty(_config).Darkness` (SabotageService) and `_meta.State.EffectiveDifficulty(_config).Darkness` (ModEntry) and pass `(season, level)`. Add `using TheLongestYear.Core;` where `DifficultyStep` is not in scope.

- [ ] **Step 8: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false`
Expected: all pass. Count should be the previous total minus the two removed theories' rows plus the new tests.

- [ ] **Step 9: Commit and push**

```
git add src/TheLongestYear.Core/Sabotage/ src/TheLongestYear/Loop/BlightPass.cs src/TheLongestYear/Loop/SabotageService.cs src/TheLongestYear/ModEntry.cs tests/TheLongestYear.Tests/DarknessLevelsTests.cs tests/TheLongestYear.Tests/SabotageTests.cs
git commit -m "Darkness: blight share and caps by level, legendary and basis limits on the tampered stack"
git push origin story
```

---

### Task 3: The night roll (Core) and the run-state counters

**Files:**
- Create: `src/TheLongestYear.Core/Sabotage/NightRoll.cs`
- Modify: `src/TheLongestYear.Core/Sabotage/SabotageSchedule.cs` (remove the shim, add the night `Rng` overload)
- Modify: `src/TheLongestYear.Core/RunState.cs`
- Modify: `src/TheLongestYear.Core/MetaState.cs`
- Test: `tests/TheLongestYear.Tests/NightRollTests.cs`, `tests/TheLongestYear.Tests/SabotageTests.cs` (`A_new_run_forgets_every_counter_and_report`)

**Interfaces:**
- Produces: `enum DarknessEvent { CropBlight, ChestBlight, Reversion, Tampering }`; `NightRoll.SeasonChance(Season)`, `NightRoll.ChanceTonight(RunState, int weekOfYear, Season)`, `NightRoll.RecordStrike(RunState, int weekOfYear, Season)`, `NightRoll.Options(Season)`, `NightRoll.Pick(IReadOnlyList<DarknessEvent>, Func<DarknessEvent,bool> canAct, Random)`, `NightRoll.GuaranteedTamperDay(int runSeed, bool firstWinterEver)`, `NightRoll.IsGuaranteedTamperNight(RunState, bool firstWinterEver, Season, int dayOfMonth)`, `NightRoll.UnmoderatedFires(DifficultyStep, bool spent, Random)`, `SabotageSchedule.Rng(int runSeed, int dayOfYear)` (the night stream); `RunState.DarknessChanceWeek`, `RunState.DarknessChance`, `RunState.UnmoderatedReversionSpent`, `RunState.UnmoderatedTamperSpent`, `RunState.GuaranteedTamperDone`; `MetaState.FirstWinterTamperSeen`.
- Removes: `SabotageSchedule.StrikesTonight` shim (Task 6 rewrites the caller; until then keep the shim but point it at `NightRoll.SeasonChance`).

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/NightRollTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-15 Part B, sections 2.1, 2.2, 2.6 and 1.5: one roll a night with a
/// decaying weekly chance, one event per strike split evenly with fall-through, the guaranteed
/// Winter tamper, and the once-per-loop unmoderated roll.</summary>
public class NightRollTests
{
    private sealed class FixedRng : Random
    {
        private readonly double _next;
        public FixedRng(double next) => _next = next;
        protected override double Sample() => _next;
        public override double NextDouble() => _next;
        public override int Next(int maxValue) => (int)(_next * maxValue);
    }

    [Theory]
    [InlineData(Season.Spring, 0.0)]
    [InlineData(Season.Summer, 0.25)]
    [InlineData(Season.Fall, 0.35)]
    [InlineData(Season.Winter, 0.35)]
    public void The_chance_starts_each_week_at_the_seasons_value(Season season, double chance)
        => Assert.Equal(chance, NightRoll.ChanceTonight(new RunState(), 7, season));

    [Fact]
    public void Each_strike_drops_the_chance_five_points_until_the_week_resets()
    {
        var run = new RunState();
        Assert.Equal(0.35, NightRoll.ChanceTonight(run, 10, Season.Fall), 3);
        NightRoll.RecordStrike(run, 10, Season.Fall);
        Assert.Equal(0.30, NightRoll.ChanceTonight(run, 10, Season.Fall), 3);
        NightRoll.RecordStrike(run, 10, Season.Fall);
        Assert.Equal(0.25, NightRoll.ChanceTonight(run, 10, Season.Fall), 3);
        Assert.Equal(0.35, NightRoll.ChanceTonight(run, 11, Season.Fall), 3);
    }

    [Fact]
    public void The_chance_never_goes_below_zero()
    {
        var run = new RunState();
        for (int i = 0; i < 10; i++) NightRoll.RecordStrike(run, 5, Season.Summer);
        Assert.Equal(0.0, NightRoll.ChanceTonight(run, 5, Season.Summer));
    }

    [Fact]
    public void Options_grow_by_season()
    {
        Assert.Empty(NightRoll.Options(Season.Spring));
        Assert.Equal(new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight }, NightRoll.Options(Season.Summer));
        Assert.Equal(new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight, DarknessEvent.Reversion }, NightRoll.Options(Season.Fall));
        Assert.Equal(new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight, DarknessEvent.Reversion, DarknessEvent.Tampering }, NightRoll.Options(Season.Winter));
    }

    [Fact]
    public void The_pick_is_even_among_the_options_that_can_act()
    {
        var counts = new Dictionary<DarknessEvent, int>();
        for (int seed = 0; seed < 4000; seed++)
        {
            DarknessEvent? pick = NightRoll.Pick(NightRoll.Options(Season.Winter), e => e != DarknessEvent.Reversion, new Random(seed));
            Assert.NotNull(pick);
            counts[pick.Value] = counts.GetValueOrDefault(pick.Value) + 1;
        }
        Assert.DoesNotContain(DarknessEvent.Reversion, counts.Keys);
        Assert.All(counts.Values, n => Assert.InRange(n, 1150, 1520));   // about a third each
    }

    [Fact]
    public void No_option_that_can_act_means_no_strike()
        => Assert.Null(NightRoll.Pick(NightRoll.Options(Season.Fall), _ => false, new Random(1)));

    [Fact]
    public void The_first_winter_ever_tampers_on_winter_1_and_later_winters_on_a_week_1_night()
    {
        Assert.Equal(1, NightRoll.GuaranteedTamperDay(12345, firstWinterEver: true));
        for (int seed = 0; seed < 50; seed++)
            Assert.InRange(NightRoll.GuaranteedTamperDay(seed, firstWinterEver: false), 1, 7);
        Assert.Equal(NightRoll.GuaranteedTamperDay(99, false), NightRoll.GuaranteedTamperDay(99, false));
    }

    [Fact]
    public void The_guaranteed_night_retries_until_it_lands_and_never_repeats()
    {
        var run = new RunState { Seed = 7 };
        int day = NightRoll.GuaranteedTamperDay(7, false);
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, false, Season.Fall, day));
        if (day > 1) Assert.False(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, day - 1));
        Assert.True(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, day));
        Assert.True(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, 7));    // skipped nights retry
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, 8));   // week 1 only
        run.GuaranteedTamperDone = true;
        Assert.False(NightRoll.IsGuaranteedTamperNight(run, false, Season.Winter, day));
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 0.01, false)]
    [InlineData(DifficultyStep.Normal, 0.01, false)]
    [InlineData(DifficultyStep.Hard, 0.05, true)]
    [InlineData(DifficultyStep.Hard, 0.15, false)]
    [InlineData(DifficultyStep.Extreme, 0.25, true)]
    [InlineData(DifficultyStep.Extreme, 0.35, false)]
    public void The_unmoderated_roll_by_level(DifficultyStep level, double die, bool fires)
        => Assert.Equal(fires, NightRoll.UnmoderatedFires(level, spent: false, new FixedRng(die)));

    [Fact]
    public void The_unmoderated_roll_is_once_per_loop()
        => Assert.False(NightRoll.UnmoderatedFires(DifficultyStep.Extreme, spent: true, new FixedRng(0.0)));

    [Fact]
    public void The_night_stream_is_fixed_by_seed_and_day()
    {
        Assert.Equal(SabotageSchedule.Rng(5, 40).Next(), SabotageSchedule.Rng(5, 40).Next());
        Assert.NotEqual(SabotageSchedule.Rng(5, 40).Next(), SabotageSchedule.Rng(5, 41).Next());
        Assert.NotEqual(SabotageSchedule.Rng(5, 40).Next(), SabotageSchedule.Rng(5, 40, SabotageKind.Blight).Next());
    }

    [Fact]
    public void A_new_run_forgets_the_darkness_counters()
    {
        var run = new RunState
        {
            DarknessChanceWeek = 9, DarknessChance = 0.1, UnmoderatedReversionSpent = true,
            UnmoderatedTamperSpent = true, GuaranteedTamperDone = true,
        };
        run.BeginNewRun(1, 1);
        Assert.Equal(-1, run.DarknessChanceWeek);
        Assert.Equal(0.0, run.DarknessChance);
        Assert.False(run.UnmoderatedReversionSpent);
        Assert.False(run.UnmoderatedTamperSpent);
        Assert.False(run.GuaranteedTamperDone);
    }
}
```

Check `RunState.BeginNewRun`'s real signature first (`grep -n "public void BeginNewRun" src/TheLongestYear.Core/RunState.cs`) and call it the way `SabotageTests.A_new_run_forgets_every_counter_and_report` does; copy that call exactly.

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false --filter "FullyQualifiedName~NightRollTests"`
Expected: build errors (`NightRoll`, `DarknessEvent`, the new `RunState` fields missing).

- [ ] **Step 3: Add the run and meta fields**

In `src/TheLongestYear.Core/RunState.cs`, after `PendingSabotageReports` add:

```csharp
    // ---- Darkness rework (spec 2026-09-15 Part B) ----
    /// <summary>Week of the year <see cref="DarknessChance"/> belongs to; -1 until the first roll.</summary>
    public int DarknessChanceWeek { get; set; } = -1;
    /// <summary>The week's current strike chance, after this week's decay. Meaningless when
    /// <see cref="DarknessChanceWeek"/> is not the current week.</summary>
    public double DarknessChance { get; set; }
    /// <summary>This loop's one unmoderated reversion has fired (Hard and Extreme).</summary>
    public bool UnmoderatedReversionSpent { get; set; }
    /// <summary>This loop's one unmoderated tamper has fired (Hard and Extreme).</summary>
    public bool UnmoderatedTamperSpent { get; set; }
    /// <summary>This Winter's guaranteed week-1 tamper has landed.</summary>
    public bool GuaranteedTamperDone { get; set; }
```

In `BeginNewRun` (the reset block that clears `BlightWeek` etc.) add:

```csharp
        DarknessChanceWeek = -1;
        DarknessChance = 0.0;
        UnmoderatedReversionSpent = false;
        UnmoderatedTamperSpent = false;
        GuaranteedTamperDone = false;
```

In `src/TheLongestYear.Core/MetaState.cs`, next to `SabotageLettersSent` add:

```csharp
    /// <summary>The save has had its first-ever Winter 1 tamper (spec 2026-09-15 Part B, 2.6): the
    /// first Winter a save reaches strikes on Winter 1, every later Winter on a random night of
    /// week 1. Per save, never reset by a loop.</summary>
    public bool FirstWinterTamperSeen { get; set; }
```

- [ ] **Step 4: Create NightRoll and the night Rng**

Create `src/TheLongestYear.Core/Sabotage/NightRoll.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>What a strike does. Blight is two options so the even split is over four in Winter
/// (spec 2026-09-15 Part B, section 2.2).</summary>
public enum DarknessEvent { CropBlight, ChestBlight, Reversion, Tampering }

/// <summary>The one nightly roll (spec 2026-09-15 Part B, sections 2.1, 2.2, 2.6 and 1.5). Pure:
/// the glue supplies the run, the calendar and "can this option act tonight"; this decides.</summary>
public static class NightRoll
{
    private const int NoWeek = -1;
    private const int FirstWinterDay = 1;
    private const int Week1Nights = 7;

    public static double SeasonChance(Season season) => season switch
    {
        Season.Summer => SabotageTuning.NightChanceSummer,
        Season.Fall => SabotageTuning.NightChanceFall,
        Season.Winter => SabotageTuning.NightChanceWinter,
        _ => 0.0,
    };

    /// <summary>The chance tonight: the season's value on a week's first roll, less five points per
    /// strike already taken this week.</summary>
    public static double ChanceTonight(RunState run, int weekOfYear, Season season)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        return run.DarknessChanceWeek == weekOfYear ? run.DarknessChance : SeasonChance(season);
    }

    /// <summary>A strike landed: drop the week's chance, starting the week if this is its first.</summary>
    public static void RecordStrike(RunState run, int weekOfYear, Season season)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        double current = ChanceTonight(run, weekOfYear, season);
        run.DarknessChanceWeek = weekOfYear;
        run.DarknessChance = Math.Max(0.0, current - SabotageTuning.NightChanceDecay);
    }

    /// <summary>What the season offers, in a fixed order.</summary>
    public static IReadOnlyList<DarknessEvent> Options(Season season) => season switch
    {
        Season.Summer => new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight },
        Season.Fall => new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight, DarknessEvent.Reversion },
        Season.Winter => new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight, DarknessEvent.Reversion, DarknessEvent.Tampering },
        _ => Array.Empty<DarknessEvent>(),
    };

    /// <summary>One event, evenly among the options that can act. An option that is capped, off,
    /// warded or has nothing fair to act on hands its share to the rest; none left means no strike.</summary>
    public static DarknessEvent? Pick(IReadOnlyList<DarknessEvent> options, Func<DarknessEvent, bool> canAct, Random rng)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (canAct is null) throw new ArgumentNullException(nameof(canAct));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        List<DarknessEvent> able = options.Where(canAct).ToList();
        return able.Count == 0 ? null : able[rng.Next(able.Count)];
    }

    /// <summary>The first Winter a save ever reaches tampers on Winter 1; every later Winter on a
    /// random night of week 1, fixed by the run seed.</summary>
    public static int GuaranteedTamperDay(int runSeed, bool firstWinterEver)
        => firstWinterEver ? FirstWinterDay : FirstWinterDay + SabotageSchedule.Rng(runSeed, 0, SabotageKind.Tampering).Next(Week1Nights);

    /// <summary>True on the guaranteed night and on every later week-1 night until it lands (a night
    /// with no fair replacement is skipped and retried, spec 2.6).</summary>
    public static bool IsGuaranteedTamperNight(RunState run, bool firstWinterEver, Season season, int dayOfMonth)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (season != Season.Winter || run.GuaranteedTamperDone) return false;
        if (dayOfMonth > Week1Nights) return false;
        return dayOfMonth >= GuaranteedTamperDay(run.Seed, firstWinterEver);
    }

    /// <summary>Does this hit ignore the fairness picker? Hard 10%, Extreme 30%, never on Easy or
    /// Normal, and never once this loop's roll is spent (Jeff, 2026-09-15).</summary>
    public static bool UnmoderatedFires(DifficultyStep level, bool spent, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        double chance = DarknessLevels.UnmoderatedChance(level);
        if (spent || chance <= 0.0) return false;
        return rng.NextDouble() < chance;
    }
}
```

In `SabotageSchedule`, add the night overload next to the existing `Rng`:

```csharp
    /// <summary>The single night roll's stream (spec 2026-09-15 Part B): seed and day only, so it is
    /// distinct from every front's own stream.</summary>
    public static Random Rng(int runSeed, int dayOfYear)
    {
        unchecked
        {
            int hash = runSeed;
            hash = hash * 397 ^ dayOfYear * 7919;
            hash = hash * 397 ^ 15485863;
            return new Random(hash);
        }
    }
```

Point the Task 2 shim at `NightRoll.SeasonChance(season)`. Check `RunState.Seed` exists (`grep -n "public int Seed" src/TheLongestYear.Core/RunState.cs`); `SabotageService` already reads `Run.Seed`, so it does.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false`
Expected: all pass. The even-split range (1150 to 1520 of 4000 over three options) is loose on purpose; if it fails, report the counts rather than widening it.

- [ ] **Step 6: Commit and push**

```
git add src/TheLongestYear.Core/Sabotage/NightRoll.cs src/TheLongestYear.Core/Sabotage/SabotageSchedule.cs src/TheLongestYear.Core/RunState.cs src/TheLongestYear.Core/MetaState.cs tests/TheLongestYear.Tests/NightRollTests.cs
git commit -m "Darkness: one nightly roll with a decaying chance, the even split, the guaranteed Winter tamper"
git push origin story
```

---

### Task 4: The fairness rule (Core)

**Files:**
- Create: `src/TheLongestYear.Core/Sabotage/SaveSnapshot.cs`
- Create: `src/TheLongestYear.Core/Sabotage/FairnessRule.cs`
- Test: `tests/TheLongestYear.Tests/FairnessRuleTests.cs`

**Interfaces:**
- Consumes: `ObtainabilityModel(IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>>)`, `ObtainSource(SourceKind, DayTable, Reliability, ObtainConditions, string Detail) { Setup }`, `ObtainConditions { Skill, SkillLevel, Requires, YearTwo, GingerIsland, Unresolved }`, `SetupStep(Name, Days)`, `DayTable.Available(Func<int,bool>)`, `DayTable.Lands(int)`; `SabotageTuning.SkillDaysToLevel`, `MineFloorsPerDay`, `MachineCraftDays`, `StaircaseMiningLevel` (Task 2).
- Produces: `SaveSnapshot` record; `FairnessRule.Judge(string itemId, int hitDay, int deadlineDay, DifficultyStep level, SaveSnapshot save, ObtainabilityModel? model)` returning `FairnessVerdict(bool Counts, IReadOnlyList<RouteVerdict> Routes, string Summary)`; `FairnessRule.Counts(...)` (bool shortcut); `RouteVerdict(ObtainSource Source, bool Counts, int AddedDays, int? LandingDay, string Reason)`; `FairnessRule.Explain(FairnessVerdict)` (string for the debug command); `FairnessRule.ReversionDeadline(int hitDay, DifficultyStep level)`; `FairnessRule.TamperDeadline` (const 112).

The model's condition strings, read from the blind code (do not edit those files; these are the formats they write today):
- `recipe:<Name>` plus optionally one of `unlock:shop`, `unlock:Queen of Sauce episode N (...)`, `unlock:none (taught some other way)` (Unresolved), `unlock:l N`, `unlock:f NPC N`; a skill unlock is in `Conditions.Skill`/`SkillLevel` instead.
- `machine:(BC)12`
- `building:Coop`, `building:Fish Pond`
- `animal:Chicken`, `animal:Ostrich (not sold)`
- `mail:ccPantry`, `mail:Farm_Eternal`, `mail:lostBookFound`
- `mines:floor 40` (nodes, mine fish, monster drops)
- `location:SkullCave`, `location:Desert`, other `location:<Name>`
- `item:(O)472` (a crop's seed; the seed's own wait is already in the table)
- `guild:<quest> N kills (...)`, `pond population N`, `tapper on tree ...`
- Setup steps: `building:Coop 3`, `animal:Chicken 1`, `friendship:Chicken 200 <days>`, `sapling 28`, `tea bush 20`. The blind model ALREADY adds sapling and tea bush days into the landing; it does NOT add building, animal or friendship days (spec decision 3).

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/FairnessRuleTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-15 Part B, section 1: every cell of the "which routes count" table,
/// against a fake model built from hand-made sources.</summary>
public class FairnessRuleTests
{
    private const string Item = "(O)999";
    private const int Hit = 60;          // Fall 4
    private const int Deadline = 112;    // Winter 28

    private static ObtainabilityModel Model(params ObtainSource[] sources)
        => new(new Dictionary<string, IReadOnlyList<ObtainSource>> { [Item] = sources });

    private static ObtainSource Route(
        SourceKind kind = SourceKind.Forage, Reliability reliability = Reliability.Dependable,
        Func<int, bool>? available = null, string[]? requires = null, string? skill = null, int skillLevel = 0,
        bool yearTwo = false, bool island = false, bool unresolved = false, SetupStep[]? setup = null)
        => new(kind, DayTable.Available(available ?? (_ => true)), reliability,
            ObtainConditions.None with
            {
                Requires = requires ?? Array.Empty<string>(), Skill = skill, SkillLevel = skillLevel,
                YearTwo = yearTwo, GingerIsland = island, Unresolved = unresolved,
            }, "test") { Setup = setup ?? Array.Empty<SetupStep>() };

    private static SaveSnapshot Save(
        string[]? recipes = null, string[]? buildings = null, string[]? machines = null, string[]? craftable = null,
        string[]? animals = null, string[]? mail = null, int floor = 0, int mining = 0, int fishing = 0,
        Dictionary<string, int>? friendship = null)
        => new(
            new HashSet<string>(recipes ?? Array.Empty<string>()), new HashSet<string>(buildings ?? Array.Empty<string>()),
            new HashSet<string>(machines ?? Array.Empty<string>()), new HashSet<string>(craftable ?? Array.Empty<string>()),
            new HashSet<string>(animals ?? Array.Empty<string>()), friendship ?? new Dictionary<string, int>(),
            new HashSet<string>(mail ?? Array.Empty<string>()), floor,
            new Dictionary<string, int> { ["Mining"] = mining, ["Fishing"] = fishing });

    private static bool Counts(DifficultyStep level, SaveSnapshot save, params ObtainSource[] sources)
        => FairnessRule.Counts(Item, Hit, Deadline, level, save, Model(sources));

    [Fact]
    public void No_model_means_everything_counts()
        => Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Easy, Save(), null));

    [Fact]
    public void No_source_at_all_never_counts()
        => Assert.False(FairnessRule.Counts("(O)1", Hit, Deadline, DifficultyStep.Extreme, Save(), Model()));

    [Theory]
    [InlineData(DifficultyStep.Easy, false)]
    [InlineData(DifficultyStep.Normal, false)]
    [InlineData(DifficultyStep.Hard, false)]
    [InlineData(DifficultyStep.Extreme, true)]
    public void A_chance_route_counts_only_on_extreme(DifficultyStep level, bool counts)
        => Assert.Equal(counts, Counts(level, Save(), Route(SourceKind.Cart, Reliability.Chance)));

    [Theory]
    [InlineData(DifficultyStep.Normal, false)]
    [InlineData(DifficultyStep.Extreme, true)]
    public void An_unresolved_route_counts_only_on_extreme(DifficultyStep level, bool counts)
        => Assert.Equal(counts, Counts(level, Save(), Route(unresolved: true)));

    [Fact]
    public void An_island_route_never_counts()
        => Assert.False(Counts(DifficultyStep.Extreme, Save(), Route(island: true)));

    [Fact]
    public void A_year_two_route_that_is_not_tv_never_counts()
        => Assert.False(Counts(DifficultyStep.Extreme, Save(), Route(SourceKind.Shop, yearTwo: true)));

    [Theory]
    [InlineData(DifficultyStep.Easy, false)]
    [InlineData(DifficultyStep.Normal, false)]
    [InlineData(DifficultyStep.Hard, true)]
    [InlineData(DifficultyStep.Extreme, true)]
    public void A_year_two_queen_of_sauce_route_counts_on_hard_and_extreme(DifficultyStep level, bool counts)
        => Assert.Equal(counts, Counts(level, Save(), Route(SourceKind.Cooking, yearTwo: true,
            requires: new[] { "recipe:Bruschetta", "unlock:Queen of Sauce episode 31 (Sunday of week 31)" })));

    [Fact]
    public void A_route_landing_after_the_deadline_does_not_count()
        => Assert.False(Counts(DifficultyStep.Extreme, Save(), Route(available: d => d >= 113)));

    [Fact]
    public void The_route_starts_the_day_after_the_hit()
    {
        Assert.False(FairnessRule.Counts(Item, Hit, Hit, DifficultyStep.Normal, Save(), Model(Route(available: d => d == Hit))));
        Assert.True(FairnessRule.Counts(Item, Hit, Hit + 1, DifficultyStep.Normal, Save(), Model(Route(available: d => d == Hit + 1))));
    }

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Normal)]
    [InlineData(DifficultyStep.Hard)]
    public void A_missing_recipe_with_a_friendship_unlock_rules_the_route_out(DifficultyStep level)
    {
        ObtainSource route = Route(SourceKind.Cooking, requires: new[] { "recipe:Cheese Cauliflower", "unlock:f Pam 3" });
        Assert.False(Counts(level, Save(), route));
        Assert.True(Counts(level, Save(recipes: new[] { "Cheese Cauliflower" }), route));
    }

    [Fact]
    public void A_missing_recipe_the_shop_or_the_tv_teaches_is_priced_by_the_table_not_ruled_out()
    {
        Assert.True(Counts(DifficultyStep.Easy, Save(), Route(SourceKind.Cooking, requires: new[] { "recipe:Omelet", "unlock:shop" })));
        Assert.True(Counts(DifficultyStep.Easy, Save(), Route(SourceKind.Cooking, requires: new[] { "recipe:Omelet", "unlock:Queen of Sauce episode 4 (Sunday of week 4)" })));
    }

    [Fact]
    public void Extreme_ignores_conditions()
        => Assert.True(Counts(DifficultyStep.Extreme, Save(), Route(SourceKind.Cooking, requires: new[] { "recipe:X", "unlock:f Pam 3", "machine:(BC)12", "mail:ccPantry" })));

    [Fact]
    public void A_missing_machine_rules_out_on_easy_and_costs_a_day_on_normal_when_craftable()
    {
        ObtainSource route = Route(SourceKind.Machine, requires: new[] { "machine:(BC)12" }, available: d => d >= Hit + 1);
        Assert.False(Counts(DifficultyStep.Easy, Save(), route));
        Assert.False(Counts(DifficultyStep.Normal, Save(), route));
        Assert.True(Counts(DifficultyStep.Normal, Save(craftable: new[] { "(BC)12" }), route));
        Assert.True(Counts(DifficultyStep.Easy, Save(machines: new[] { "(BC)12" }), route));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(craftable: new[] { "(BC)12" }), Model(route));
        Assert.Equal(SabotageTuning.MachineCraftDays, verdict.Routes[0].AddedDays);
    }

    [Fact]
    public void A_missing_building_rules_out_on_easy_and_adds_its_days_on_normal()
    {
        ObtainSource route = Route(SourceKind.Animal, requires: new[] { "building:Coop", "animal:Chicken" },
            setup: new[] { new SetupStep("building:Coop", 3), new SetupStep("animal:Chicken", 1) }, available: d => d >= 110);
        Assert.False(Counts(DifficultyStep.Easy, Save(), route));
        Assert.True(Counts(DifficultyStep.Easy, Save(buildings: new[] { "Coop" }, animals: new[] { "Chicken" }), route));
        // Normal: lands 110 + 3 + 1 = 114, past Winter 28.
        Assert.False(Counts(DifficultyStep.Normal, Save(), route));
        // With the coop but no chicken: 110 + 1 = 111.
        Assert.True(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Coop" }), route));
    }

    [Fact]
    public void An_animal_that_is_not_sold_rules_out_unless_owned()
    {
        ObtainSource route = Route(SourceKind.Animal, requires: new[] { "building:Barn", "animal:Ostrich (not sold)" },
            setup: new[] { new SetupStep("building:Barn", 3), new SetupStep("animal:Ostrich", 1) });
        Assert.False(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Barn" }), route));
        Assert.True(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Barn" }, animals: new[] { "Ostrich" }), route));
    }

    [Fact]
    public void Friendship_days_are_added_on_normal_when_the_animal_is_not_there_yet()
    {
        ObtainSource route = Route(SourceKind.Animal, requires: new[] { "building:Coop", "animal:Chicken" },
            setup: new[] { new SetupStep("building:Coop", 3), new SetupStep("animal:Chicken", 1), new SetupStep("friendship:Chicken 200", 14) },
            available: d => d >= 100);
        Assert.True(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Coop" }, animals: new[] { "Chicken" },
            friendship: new Dictionary<string, int> { ["Chicken"] = 500 }), route));               // 100
        Assert.False(Counts(DifficultyStep.Normal, Save(buildings: new[] { "Coop" }, animals: new[] { "Chicken" },
            friendship: new Dictionary<string, int> { ["Chicken"] = 0 }), route));                 // 100 + 14 = 114
        Assert.False(Counts(DifficultyStep.Easy, Save(buildings: new[] { "Coop" }, animals: new[] { "Chicken" },
            friendship: new Dictionary<string, int> { ["Chicken"] = 0 }), route));
    }

    [Fact]
    public void A_missing_skill_rules_out_on_easy_and_adds_the_table_days_on_normal()
    {
        ObtainSource route = Route(SourceKind.Fish, skill: "Fishing", skillLevel: 6, available: d => d >= 100);
        Assert.False(Counts(DifficultyStep.Easy, Save(fishing: 3), route));
        Assert.True(Counts(DifficultyStep.Easy, Save(fishing: 6), route));
        // Normal: 100 + (10 - 3) = 107, in time.
        Assert.True(Counts(DifficultyStep.Normal, Save(fishing: 3), route));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(fishing: 3), Model(route));
        Assert.Equal(7, verdict.Routes[0].AddedDays);
        // Normal from level 0: 100 + 10 = 110, in time; from level 0 with a later landing, out.
        Assert.False(Counts(DifficultyStep.Normal, Save(fishing: 0), Route(SourceKind.Fish, skill: "Fishing", skillLevel: 6, available: d => d >= 105)));
    }

    [Fact]
    public void A_mine_floor_not_reached_rules_out_on_easy_and_costs_a_day_per_ten_floors_on_normal()
    {
        ObtainSource route = Route(SourceKind.MineNode, requires: new[] { "mines:floor 80" }, available: d => d >= 100);
        Assert.False(Counts(DifficultyStep.Easy, Save(floor: 40), route));
        Assert.True(Counts(DifficultyStep.Easy, Save(floor: 80), route));
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 40), Model(route));
        Assert.Equal(4, verdict.Routes[0].AddedDays);
        Assert.True(verdict.Counts);
        Assert.Equal(12, FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 0),
            Model(Route(SourceKind.MineNode, requires: new[] { "mines:floor 120" }))).Routes[0].AddedDays);
        Assert.Equal(1, FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 39),
            Model(Route(SourceKind.MineNode, requires: new[] { "mines:floor 40" }))).Routes[0].AddedDays);
    }

    [Fact]
    public void Skull_cavern_is_a_condition_never_a_wait()
    {
        ObtainSource route = Route(SourceKind.MonsterDrop, requires: new[] { "location:SkullCave" });
        Assert.False(Counts(DifficultyStep.Easy, Save(mining: 5), route));                                   // desert shut
        Assert.False(Counts(DifficultyStep.Easy, Save(mail: new[] { "ccVault" }, mining: 1), route));         // no staircase
        Assert.True(Counts(DifficultyStep.Easy, Save(mail: new[] { "ccVault" }, mining: 2), route));
        Assert.False(Counts(DifficultyStep.Normal, Save(mining: 10), route));                                 // the bus is never priced
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(mail: new[] { "ccVault" }, mining: 0), Model(route));
        Assert.True(verdict.Counts);
        Assert.Equal(2, verdict.Routes[0].AddedDays);   // Mining 2 from 0 = 2 days
    }

    [Fact]
    public void The_desert_needs_the_bus()
    {
        ObtainSource route = Route(SourceKind.Shop, requires: new[] { "location:Desert" });
        Assert.False(Counts(DifficultyStep.Normal, Save(), route));
        Assert.True(Counts(DifficultyStep.Normal, Save(mail: new[] { "ccVault" }), route));
    }

    [Fact]
    public void A_mail_flag_is_met_or_not()
    {
        ObtainSource route = Route(SourceKind.GreenhouseCrop, requires: new[] { "item:(O)472", "mail:ccPantry" });
        Assert.False(Counts(DifficultyStep.Normal, Save(), route));
        Assert.True(Counts(DifficultyStep.Normal, Save(mail: new[] { "ccPantry" }), route));
    }

    [Fact]
    public void Conditions_the_table_does_not_name_count_as_met()
        => Assert.True(Counts(DifficultyStep.Easy, Save(), Route(requires: new[] { "item:(O)472", "guild:Slimes 1000 kills", "pond population 3", "location:Beach", "tapper on tree 1, 7 days" })));

    [Fact]
    public void One_counting_route_is_enough()
        => Assert.True(Counts(DifficultyStep.Easy, Save(), Route(SourceKind.Cart, Reliability.Chance), Route(SourceKind.Forage)));

    [Theory]
    [InlineData(60, DifficultyStep.Easy, 84)]      // Fall 4: the end of Fall
    [InlineData(60, DifficultyStep.Normal, 112)]
    [InlineData(90, DifficultyStep.Easy, 112)]     // Winter 6: the end of Winter
    [InlineData(90, DifficultyStep.Hard, 112)]
    public void Reversion_deadline_by_level(int hitDay, DifficultyStep level, int deadline)
        => Assert.Equal(deadline, FairnessRule.ReversionDeadline(hitDay, level));

    [Fact]
    public void Tamper_deadline_is_winter_28()
        => Assert.Equal(112, FairnessRule.TamperDeadline);

    [Fact]
    public void Explain_names_every_route_and_the_verdict()
    {
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 40),
            Model(Route(SourceKind.MineNode, requires: new[] { "mines:floor 80" }), Route(SourceKind.Cart, Reliability.Chance)));
        string text = FairnessRule.Explain(verdict);
        Assert.Contains("counts", text);
        Assert.Contains("+4 day", text);
        Assert.Contains("chance route", text);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false --filter "FullyQualifiedName~FairnessRuleTests"`
Expected: build errors (`SaveSnapshot`, `FairnessRule`, `FairnessVerdict` missing).

- [ ] **Step 3: Create SaveSnapshot**

Create `src/TheLongestYear.Core/Sabotage/SaveSnapshot.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>What the real save has, read by the glue just before a darkness pick (spec 2026-09-15
/// Part B, section 1.1). The fairness rule checks the model's conditions against this: what the
/// player already has costs nothing, what they lack adds days or rules a route out, by level.</summary>
/// <param name="RecipesKnown">Cooking and crafting recipe names the player knows.</param>
/// <param name="Buildings">Building type names on the farm, plus every type each one upgraded from.</param>
/// <param name="MachinesOwned">Qualified ids of big craftables placed anywhere or held in a chest or the bag.</param>
/// <param name="CraftableMachines">Qualified ids of the items the player's known crafting recipes make.</param>
/// <param name="AnimalsOwned">Animal type names the player owns.</param>
/// <param name="AnimalFriendship">Best friendship per owned animal type.</param>
/// <param name="MailFlags">Mail flags received (ccPantry, ccVault and the rest).</param>
/// <param name="DeepestMineFloor">Deepest regular mine floor reached.</param>
/// <param name="Skills">Level per skill name (Farming, Fishing, Foraging, Mining, Combat, Luck).</param>
public sealed record SaveSnapshot(
    IReadOnlySet<string> RecipesKnown,
    IReadOnlySet<string> Buildings,
    IReadOnlySet<string> MachinesOwned,
    IReadOnlySet<string> CraftableMachines,
    IReadOnlySet<string> AnimalsOwned,
    IReadOnlyDictionary<string, int> AnimalFriendship,
    IReadOnlySet<string> MailFlags,
    int DeepestMineFloor,
    IReadOnlyDictionary<string, int> Skills)
{
    public static readonly SaveSnapshot Empty = new(
        new HashSet<string>(), new HashSet<string>(), new HashSet<string>(), new HashSet<string>(),
        new HashSet<string>(), new Dictionary<string, int>(), new HashSet<string>(), 0, new Dictionary<string, int>());

    public int SkillLevel(string skill) => Skills.TryGetValue(skill, out int level) ? level : 0;
}
```

- [ ] **Step 4: Create FairnessRule**

Create `src/TheLongestYear.Core/Sabotage/FairnessRule.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Sabotage;

/// <summary>One route's verdict for the debug readout.</summary>
public sealed record RouteVerdict(ObtainSource Source, bool Counts, int AddedDays, int? LandingDay, string Reason);

/// <summary>Whether an item counts as obtainable for a darkness hit, with every route's reason.</summary>
public sealed record FairnessVerdict(bool Counts, IReadOnlyList<RouteVerdict> Routes, string Summary);

/// <summary>"Start from nothing the day after the hit; can this player get the item by the deadline
/// through what this Darkness level allows?" (spec 2026-09-15 Part B, section 1). Reads the
/// obtainability model through its public API only and prices nothing the game does not price:
/// a missing building adds the game's build days, a missing skill or mine floor adds Jeff's gap
/// table, everything else is met or rules the route out.</summary>
public static class FairnessRule
{
    /// <summary>Tampering only happens in Winter and every unfinished bundle is judged on Winter 28.</summary>
    public const int TamperDeadline = DayTable.Days;

    private const string RecipePrefix = "recipe:";
    private const string UnlockPrefix = "unlock:";
    private const string UnlockShop = "unlock:shop";
    private const string UnlockTv = "unlock:Queen of Sauce";
    private const string MachinePrefix = "machine:";
    private const string BuildingPrefix = "building:";
    private const string AnimalPrefix = "animal:";
    private const string NotSoldSuffix = " (not sold)";
    private const string MailPrefix = "mail:";
    private const string MineFloorPrefix = "mines:floor ";
    private const string SkullCavern = "location:SkullCave";
    private const string Desert = "location:Desert";
    private const string BusMail = "ccVault";
    private const string FriendshipPrefix = "friendship:";
    private const string MiningSkill = "Mining";
    private const int NoDays = 0;

    /// <summary>What a level allows (the columns of the spec's table 1.2).</summary>
    private sealed record Policy(bool AllowChance, bool AllowUnresolved, bool YearTwoTv, bool IgnoreConditions, bool AddDays)
    {
        public static Policy For(DifficultyStep level) => level switch
        {
            DifficultyStep.Easy => new(false, false, false, false, false),
            DifficultyStep.Hard => new(false, false, true, false, true),
            DifficultyStep.Extreme => new(true, true, true, true, true),
            _ => new(false, false, false, false, true),   // Normal
        };
    }

    /// <summary>Reversion's deadline: the end of the current season on Easy, Winter 28 otherwise.</summary>
    public static int ReversionDeadline(int hitDay, DifficultyStep level)
        => level == DifficultyStep.Easy ? Calendar.LastDayOfSeason(hitDay) : DayTable.Days;

    public static bool Counts(string itemId, int hitDay, int deadlineDay, DifficultyStep level, SaveSnapshot save, ObtainabilityModel? model)
        => Judge(itemId, hitDay, deadlineDay, level, save, model).Counts;

    public static FairnessVerdict Judge(string itemId, int hitDay, int deadlineDay, DifficultyStep level, SaveSnapshot save, ObtainabilityModel? model)
    {
        if (save is null) throw new ArgumentNullException(nameof(save));
        if (model is null)
            return new FairnessVerdict(true, Array.Empty<RouteVerdict>(), "no obtainability model: no fairness filter, everything counts");
        Policy policy = Policy.For(level);
        int startDay = hitDay + 1;
        var routes = new List<RouteVerdict>();
        foreach (ObtainSource source in model.Sources(itemId))
            routes.Add(JudgeRoute(source, startDay, deadlineDay, policy, save));
        bool counts = routes.Any(r => r.Counts);
        string summary = routes.Count == 0
            ? "no source in the obtainability model"
            : counts ? $"counts ({routes.Count(r => r.Counts)} of {routes.Count} route(s))" : $"does not count (0 of {routes.Count} route(s))";
        return new FairnessVerdict(counts, routes, summary);
    }

    private static RouteVerdict JudgeRoute(ObtainSource source, int startDay, int deadlineDay, Policy policy, SaveSnapshot save)
    {
        ObtainConditions c = source.Conditions;
        if (c.GingerIsland) return Out(source, "Ginger Island route");
        if (c.YearTwo)
        {
            bool tv = source.Kind == SourceKind.Cooking && c.Requires.Any(r => r.StartsWith(UnlockTv, StringComparison.Ordinal));
            if (!tv) return Out(source, "year 2 route");
            if (!policy.YearTwoTv) return Out(source, "year 2 Queen of Sauce episode (Hard and Extreme only)");
        }
        if (source.Reliability == Reliability.Chance && !policy.AllowChance) return Out(source, "chance route");
        if (c.Unresolved && !policy.AllowUnresolved) return Out(source, "unresolved route (a guess)");
        int? landing = source.Lands.Lands(startDay);
        if (landing is null) return Out(source, $"never lands from day {startDay}");

        int added = NoDays;
        if (!policy.IgnoreConditions)
        {
            string? blocked = Conditions(source, policy, save, ref added);
            if (blocked != null) return Out(source, blocked);
        }
        int lands = landing.Value + added;
        bool counts = lands <= deadlineDay;
        string reason = counts
            ? (added > NoDays ? $"counts, lands day {lands} (+{added} day(s) of setup)" : $"counts, lands day {lands}")
            : $"lands day {lands}, after the deadline (day {deadlineDay})" + (added > NoDays ? $" with +{added} day(s) of setup" : "");
        return new RouteVerdict(source, counts, added, lands, reason);
    }

    /// <summary>Checks every condition against the save. Returns the reason the route is out, or null
    /// with <paramref name="added"/> holding the days the lacking setup costs.</summary>
    private static string? Conditions(ObtainSource source, Policy policy, SaveSnapshot save, ref int added)
    {
        ObtainConditions c = source.Conditions;
        IReadOnlyList<string> requires = c.Requires;

        if (c.Skill != null && save.SkillLevel(c.Skill) < c.SkillLevel)
        {
            if (!policy.AddDays) return $"needs {c.Skill} {c.SkillLevel}, has {save.SkillLevel(c.Skill)}";
            added += SkillGapDays(save.SkillLevel(c.Skill), c.SkillLevel);
        }

        foreach (string r in requires)
        {
            if (r.StartsWith(RecipePrefix, StringComparison.Ordinal))
            {
                string name = r.Substring(RecipePrefix.Length);
                if (save.RecipesKnown.Contains(name)) continue;
                // A shop sale or a TV episode is a wait the game itself prices (the table already
                // holds the Sunday); any other unlock is a judgement, so a missing recipe rules out.
                bool priced = requires.Any(u => u == UnlockShop || u.StartsWith(UnlockTv, StringComparison.Ordinal));
                bool skillTaught = c.Skill != null;
                if (priced || skillTaught) continue;
                return $"recipe {name} not known";
            }
            if (r.StartsWith(MachinePrefix, StringComparison.Ordinal))
            {
                string id = r.Substring(MachinePrefix.Length);
                if (save.MachinesOwned.Contains(id)) continue;
                if (policy.AddDays && save.CraftableMachines.Contains(id)) { added += SabotageTuning.MachineCraftDays; continue; }
                return $"machine {id} not owned" + (policy.AddDays ? " and not craftable" : "");
            }
            if (r.StartsWith(BuildingPrefix, StringComparison.Ordinal))
            {
                string name = r.Substring(BuildingPrefix.Length);
                if (save.Buildings.Contains(name)) continue;
                if (!policy.AddDays) return $"building {name} not on the farm";
                SetupStep? step = source.Setup.FirstOrDefault(s => s.Name == r);
                if (step is null) return $"building {name} not on the farm and no build time recorded";
                added += step.Days;
                continue;
            }
            if (r.StartsWith(AnimalPrefix, StringComparison.Ordinal))
            {
                bool notSold = r.EndsWith(NotSoldSuffix, StringComparison.Ordinal);
                string name = notSold ? r.Substring(AnimalPrefix.Length, r.Length - AnimalPrefix.Length - NotSoldSuffix.Length) : r.Substring(AnimalPrefix.Length);
                if (save.AnimalsOwned.Contains(name)) continue;
                if (notSold) return $"animal {name} not owned and not sold";
                if (!policy.AddDays) return $"animal {name} not owned";
                SetupStep? step = source.Setup.FirstOrDefault(s => s.Name == AnimalPrefix + name);
                added += step?.Days ?? 1;
                continue;
            }
            if (r.StartsWith(MailPrefix, StringComparison.Ordinal))
            {
                string flag = r.Substring(MailPrefix.Length);
                if (save.MailFlags.Contains(flag)) continue;
                return $"needs {flag}";
            }
            if (r.StartsWith(MineFloorPrefix, StringComparison.Ordinal))
            {
                if (!int.TryParse(r.Substring(MineFloorPrefix.Length), out int floor)) continue;
                if (save.DeepestMineFloor >= floor) continue;
                if (!policy.AddDays) return $"mine floor {floor} not reached (deepest {save.DeepestMineFloor})";
                added += MineGapDays(save.DeepestMineFloor, floor);
                continue;
            }
            if (r == SkullCavern)
            {
                if (!save.MailFlags.Contains(BusMail)) return "Skull Cavern: the desert is not open";
                int mining = save.SkillLevel(MiningSkill);
                if (mining >= SabotageTuning.StaircaseMiningLevel) continue;
                if (!policy.AddDays) return $"Skull Cavern: Staircases need Mining {SabotageTuning.StaircaseMiningLevel}, has {mining}";
                added += SkillGapDays(mining, SabotageTuning.StaircaseMiningLevel);
                continue;
            }
            if (r == Desert)
            {
                if (!save.MailFlags.Contains(BusMail)) return "the desert is not open";
                continue;
            }
            // item:, guild:, pond population, tapper on tree, other location: and unlock: notes count as met.
        }

        // Friendship setup (deluxe animal produce): the days of petting the game needs, when the
        // animal is not there yet or not friendly enough.
        foreach (SetupStep step in source.Setup)
        {
            if (!step.Name.StartsWith(FriendshipPrefix, StringComparison.Ordinal)) continue;
            string[] parts = step.Name.Substring(FriendshipPrefix.Length).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2 || !int.TryParse(parts[1], out int needed)) continue;
            int have = save.AnimalFriendship.TryGetValue(parts[0], out int f) ? f : 0;
            if (have >= needed) continue;
            if (!policy.AddDays) return $"{parts[0]} friendship {needed} needed, has {have}";
            added += step.Days;
        }
        return null;
    }

    /// <summary>Jeff's skill table: the target level's days minus the current level's days.</summary>
    public static int SkillGapDays(int currentLevel, int targetLevel)
    {
        int[] table = SabotageTuning.SkillDaysToLevel;
        int Days(int level) => table[Math.Clamp(level, 0, table.Length - 1)];
        return Math.Max(0, Days(targetLevel) - Days(currentLevel));
    }

    /// <summary>One day per ten floors below the deepest reached, rounded up.</summary>
    public static int MineGapDays(int deepest, int target)
        => target <= deepest ? 0 : (target - deepest + SabotageTuning.MineFloorsPerDay - 1) / SabotageTuning.MineFloorsPerDay;

    private static RouteVerdict Out(ObtainSource source, string reason) => new(source, false, NoDays, null, reason);

    /// <summary>The readout for tly_sabotage fair: the verdict, then one line per route.</summary>
    public static string Explain(FairnessVerdict verdict)
    {
        if (verdict is null) throw new ArgumentNullException(nameof(verdict));
        var sb = new StringBuilder(verdict.Summary);
        foreach (RouteVerdict r in verdict.Routes)
            sb.AppendLine().Append("  - ").Append(r.Counts ? "counts: " : "out: ").Append(r.Reason)
              .Append(" | ").Append(ObtainabilityText.SourceLine(r.Source));
        return sb.ToString();
    }
}
```

Note on `Calendar.LastDayOfSeason(int dayOfYear)`: it exists (`Calendar.cs` line 47) and returns the season's day 28 as a day of year. `DayTable.Days` is 112.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false`
Expected: all pass, including `ObtainabilityBlindGuardTests` (this task adds no file to the blind folder and edits none).

- [ ] **Step 6: Commit and push**

```
git add src/TheLongestYear.Core/Sabotage/SaveSnapshot.cs src/TheLongestYear.Core/Sabotage/FairnessRule.cs tests/TheLongestYear.Tests/FairnessRuleTests.cs
git commit -m "Darkness: the fairness rule, route by route against the real save, by level"
git push origin story
```

---

### Task 5: Reversion and tampering read the fairness test (Core)

**Files:**
- Modify: `src/TheLongestYear.Core/Sabotage/SabotageRules.cs`
- Test: `tests/TheLongestYear.Tests/SabotageTests.cs` (`ReversionRuleTests`)

**Interfaces:**
- Produces: `ReversionRule.Pick(SlotLedger, IReadOnlyList<BundleRequirement>, Func<string,bool> fair, Random)`; the two-argument-plus-rng overload stays and means "everything is fair". `TamperRule` is unchanged here: the glue filters its candidate list with the rule before calling `PickReplacement`.

- [ ] **Step 1: Write the failing test**

In `tests/TheLongestYear.Tests/SabotageTests.cs`, inside `ReversionRuleTests`, look at how `Only_unfinished_item_room_bundles_are_candidates` builds its ledger and requirements (a `SlotLedger` with `DonatedSlot` entries and `BundleRequirement`s). Add, using the same builders:

```csharp
    [Fact]
    public void A_slot_whose_item_fails_the_fairness_test_is_never_picked()
    {
        // Build the same two-slot unfinished bundle the candidate test uses; call the ids A and B.
        (SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements) = TwoFilledSlotsInOneUnfinishedBundle();
        string a = ledger.Entries[0].ItemId, b = ledger.Entries[1].ItemId;

        for (int seed = 0; seed < 20; seed++)
        {
            DonatedSlot? pick = ReversionRule.Pick(ledger, requirements, id => id == b, new Random(seed));
            Assert.NotNull(pick);
            Assert.Equal(b, pick!.ItemId);
        }
        Assert.Null(ReversionRule.Pick(ledger, requirements, _ => false, new Random(1)));
        Assert.NotNull(ReversionRule.Pick(ledger, requirements, new Random(1)));
    }
```

Write `TwoFilledSlotsInOneUnfinishedBundle()` as a private static helper in the same test class, extracted from the setup of `Only_unfinished_item_room_bundles_are_candidates` so both tests share it (do not duplicate the builder; refactor the existing test to call the helper too).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false --filter "FullyQualifiedName~ReversionRuleTests"`
Expected: build error (no four-argument `Pick`).

- [ ] **Step 3: Add the fairness-aware Pick**

In `SabotageRules.cs`, `ReversionRule`:

```csharp
    /// <summary>Uniform among the candidates whose item passes <paramref name="fair"/> (the
    /// FairnessRule, spec 2026-09-15 Part B, 1.6). Null when none passes.</summary>
    public static DonatedSlot? Pick(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, Func<string, bool> fair, Random rng)
    {
        if (fair is null) throw new ArgumentNullException(nameof(fair));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        List<DonatedSlot> candidates = Candidates(ledger, requirements).Where(s => fair(s.ItemId)).ToList();
        return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
    }
```

and make the existing three-argument `Pick` call it with `_ => true`.

- [ ] **Step 4: Run the whole suite, commit, push**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false`
Expected: all pass.

```
git add src/TheLongestYear.Core/Sabotage/SabotageRules.cs tests/TheLongestYear.Tests/SabotageTests.cs
git commit -m "Darkness: reversion picks only among slots the fairness rule passes"
git push origin story
```

---

### Task 6: The glue: snapshot reader, one night roll, the fairness picker, the tamper pool

**Files:**
- Create: `src/TheLongestYear/Loop/SaveSnapshotReader.cs`
- Modify: `src/TheLongestYear/Loop/SabotageService.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (service construction near line 698; `CmdSabotage`)
- Modify: `src/TheLongestYear.Core/Sabotage/SabotageSchedule.cs` (delete the `StrikesTonight` shim)

**Interfaces:**
- Consumes: everything from Tasks 2 to 5; `ObtainabilityModel` via `Func<ObtainabilityModel>` (ModEntry's `_obtainability`, may be null); `CcItemCatalog.Items` (`CcItem { Id, Theme }`) for the tamper pool; `ItemAvailabilityModel.For(id).Effort` for closeness; `QuantityAskPass.BasisByDeadline(id, Season.Winter)`; `MetaState.EffectiveDifficulty(config).Darkness`.
- Produces: `SabotageService.Explain(string itemId, DifficultyStep? level)` (string), `SabotageService.Status()` extended, `SabotageService.RunNight()` rewritten, `SabotageService.Arm(...)` unchanged in signature.

No unit tests here (SMAPI types); the proof is the build, the existing suite, and Task 9's live run. Keep every public debug entry point (`Blight`, `Revert`, `Tamper`, `Arm`, `ShowMorning`, `Status`) working.

- [ ] **Step 1: Create the snapshot reader**

Create `src/TheLongestYear/Loop/SaveSnapshotReader.cs`:

```csharp
using System;
using System.Collections.Generic;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Buildings;
using StardewValley.Locations;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>Reads what the real save has into a <see cref="SaveSnapshot"/> for the fairness
    /// rule (spec 2026-09-15 Part B, 1.1). Host only; read once per night pass.</summary>
    internal static class SaveSnapshotReader
    {
        private static readonly string[] SkillNames = { "Farming", "Fishing", "Foraging", "Mining", "Combat", "Luck" };

        public static SaveSnapshot Read()
        {
            Farmer player = Game1.player;
            if (player == null) return SaveSnapshot.Empty;

            var recipes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in player.cookingRecipes.Keys) recipes.Add(name);
            foreach (string name in player.craftingRecipes.Keys) recipes.Add(name);

            // What the known crafting recipes make, by qualified id, so a missing keg can cost a day
            // of crafting on Normal rather than rule the route out.
            var craftable = new HashSet<string>(StringComparer.Ordinal);
            foreach (string name in player.craftingRecipes.Keys)
            {
                try
                {
                    Item made = new CraftingRecipe(name, false).createItem();
                    if (made != null) craftable.Add(made.QualifiedItemId);
                }
                catch (Exception ex) when (ex is KeyNotFoundException or NullReferenceException or ArgumentException)
                {
                    // A content mod's recipe with no item data: skip it, it cannot be a machine we need.
                }
            }

            var buildings = new HashSet<string>(StringComparer.Ordinal);
            var animals = new HashSet<string>(StringComparer.Ordinal);
            var friendship = new Dictionary<string, int>(StringComparer.Ordinal);
            Farm farm = Game1.getFarm();
            if (farm != null)
            {
                foreach (Building b in farm.buildings)
                {
                    // A Big Coop stands in for a Coop: walk the upgrade chain down (BuildingData.BuildingToUpgrade).
                    string type = b.buildingType.Value;
                    int guard = 0;
                    while (!string.IsNullOrEmpty(type) && buildings.Add(type) && guard++ < 8)
                        type = Game1.buildingData != null && Game1.buildingData.TryGetValue(type, out BuildingData data) ? data.BuildingToUpgrade : null;
                }
                foreach (FarmAnimal animal in farm.getAllFarmAnimals())
                {
                    string type = animal.type.Value;
                    animals.Add(type);
                    int f = animal.friendshipTowardFarmer.Value;
                    if (!friendship.TryGetValue(type, out int best) || f > best) friendship[type] = f;
                }
            }

            var machines = new HashSet<string>(StringComparer.Ordinal);
            Utility.ForEachLocation(loc =>
            {
                foreach (StardewValley.Object obj in loc.objects.Values)
                {
                    if (obj.bigCraftable.Value) machines.Add(obj.QualifiedItemId);
                    if (obj is Chest chest)
                        foreach (Item item in chest.Items)
                            if (item is StardewValley.Object o && o.bigCraftable.Value) machines.Add(o.QualifiedItemId);
                }
                return true;
            });
            foreach (Item item in player.Items)
                if (item is StardewValley.Object o && o.bigCraftable.Value) machines.Add(o.QualifiedItemId);

            var mail = new HashSet<string>(StringComparer.Ordinal);
            foreach (string flag in player.mailReceived) mail.Add(flag);
            foreach (string flag in Game1.MasterPlayer.mailReceived) mail.Add(flag);

            var skills = new Dictionary<string, int>(StringComparer.Ordinal);
            for (int i = 0; i < SkillNames.Length; i++) skills[SkillNames[i]] = player.GetSkillLevel(i);

            int floor = Game1.netWorldState?.Value == null ? 0 : MineShaft.lowestLevelReached;
            return new SaveSnapshot(recipes, buildings, machines, craftable, animals, friendship, mail, floor, skills);
        }
    }
}
```

Check against the decompile before trusting these names: `Farm.getAllFarmAnimals()`, `FarmAnimal.type`, `FarmAnimal.friendshipTowardFarmer`, `Building.buildingType`, `Game1.buildingData`, `Farmer.GetSkillLevel(int)` (skill indices 0 Farming, 1 Fishing, 2 Foraging, 3 Mining, 4 Combat, 5 Luck), `MineShaft.lowestLevelReached` (static, `StardewValley.Locations`). Fix any name the decompile disagrees with and say so in the commit.

- [ ] **Step 2: Rewrite SabotageService**

Replace the whole of `src/TheLongestYear/Loop/SabotageService.cs` with the version below. It keeps the class shape (constructor gains one `Func<ObtainabilityModel>` parameter), the debug entry points, the morning code and `WriteTamper` unchanged. Only the night pass, the picks and `Status` change; those are given in full.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Loop
{
    /// <summary>The darkness (spec 2026-09-09 pushback; rework 2026-09-14; Part B wiring
    /// 2026-09-15): ONE roll a night with a decaying weekly chance, one event per strike split
    /// evenly among what the season offers, and reversion and tampering that consult the
    /// obtainability model against the real save by Darkness level. Blight kills crops (BlightPass)
    /// or takes stored units (SpoilagePass); reversion opens a filled slot; tampering rewrites an
    /// unfilled slot's item and asks ModEntry to rebuild the catalog and requirements. Host only,
    /// single player or master, never on a day 28 or the win night (RunController decides that).</summary>
    internal sealed class SabotageService
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly Func<IReadOnlyList<BundleRequirement>> _requirements;
        private readonly Func<ItemAvailabilityModel> _availability;
        private readonly Func<ObtainabilityModel> _obtainability;
        private readonly Action<string> _rebuildBoard;
        private readonly SabotageMailService _mail;
        public Action<string, string, Action> StartTamperScene { get; set; }

        private RunState Run => _store.Run;
        private MetaState Meta => _store.State;
        private DifficultyStep Level => Meta.EffectiveDifficulty(_config).Darkness;

        public SabotageService(
            IMonitor monitor, MetaStore store, GameplayConfig config,
            Func<IReadOnlyList<BundleRequirement>> requirements,
            Func<ItemAvailabilityModel> availability,
            Func<ObtainabilityModel> obtainability,
            Action<string> rebuildBoard,
            SabotageMailService mail)
        {
            _mail = mail;
            _monitor = monitor ?? throw new ArgumentNullException(nameof(monitor));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
            _availability = availability ?? throw new ArgumentNullException(nameof(availability));
            _obtainability = obtainability ?? throw new ArgumentNullException(nameof(obtainability));
            _rebuildBoard = rebuildBoard ?? throw new ArgumentNullException(nameof(rebuildBoard));
        }

        private static bool HostCanAct() => Game1.IsMasterGame && !Game1.IsMultiplayer;

        public bool Enabled(SabotageKind kind) => kind switch
        {
            SabotageKind.Blight => _config.EnableBlight,
            SabotageKind.Reversion => _config.EnableBundleReversion,
            SabotageKind.Tampering => _config.EnableRequirementTampering,
            _ => false,
        };

        private static SabotageKind KindOf(DarknessEvent e) => e switch
        {
            DarknessEvent.Reversion => SabotageKind.Reversion,
            DarknessEvent.Tampering => SabotageKind.Tampering,
            _ => SabotageKind.Blight,
        };

        // ------------------------------------------------------------------ the night pass

        /// <summary>One roll for tonight. Effects land before the save; reports queue for the morning.</summary>
        public void RunNight()
        {
            if (!RunActivation.IsActive || !HostCanAct()) return;
            CoreSeason season = Run.Season;
            int day = Run.DayOfMonth;
            int dayOfYear = Calendar.DayOfYear((int)season, day);
            int week = Run.WeekOfYear;
            DifficultyStep level = Level;
            if (season == CoreSeason.Spring) { _armed.Clear(); _armedBlightTarget = null; return; }

            Random rng = SabotageSchedule.Rng(Run.Seed, dayOfYear);
            SaveSnapshot save = SaveSnapshotReader.Read();
            ObtainabilityModel model = _obtainability();
            var night = new NightPlan(this, season, day, week, dayOfYear, level, save, model, rng);

            // The guaranteed Winter tamper (spec 2.6) comes first and is the night's whole strike.
            if (Enabled(SabotageKind.Tampering) && NightRoll.IsGuaranteedTamperNight(Run, Meta.FirstWinterTamperSeen, season, day))
            {
                if (night.CanAct(DarknessEvent.Tampering) && night.Execute(DarknessEvent.Tampering))
                {
                    Run.GuaranteedTamperDone = true;
                    Meta.FirstWinterTamperSeen = true;
                    NightRoll.RecordStrike(Run, week, season);
                    SabotageSchedule.RecordStrike(SabotageKind.Tampering, Run, week, dayOfYear);
                    _monitor.Log($"Darkness: the guaranteed Winter tamper struck on Winter {day}.", LogLevel.Info);
                    _armed.Clear(); _armedBlightTarget = null;
                    return;
                }
                _monitor.Log($"Darkness: guaranteed Winter tamper had no fair target on Winter {day}; retrying tomorrow.", LogLevel.Info);
            }

            double chance = NightRoll.ChanceTonight(Run, week, season);
            bool dice = rng.NextDouble() < chance;
            DarknessEvent? forced = TakeArmed(night);
            _monitor.Log($"Darkness: night roll {season} {day} at {chance:P0}: {(dice ? "strike" : "quiet")}{(forced != null ? $", armed {forced}" : "")}; level {level}.", LogLevel.Trace);
            if (!dice && forced == null) return;

            DarknessEvent? pick = forced ?? NightRoll.Pick(NightRoll.Options(season), night.CanAct, rng);
            if (pick == null)
            {
                _monitor.Log("Darkness: nothing could act tonight; no strike and the chance does not drop.", LogLevel.Trace);
                return;
            }
            if (!night.Execute(pick.Value))
            {
                _monitor.Log($"Darkness: {pick} was picked but took nothing.", LogLevel.Trace);
                return;
            }
            NightRoll.RecordStrike(Run, week, season);
            SabotageSchedule.RecordStrike(KindOf(pick.Value), Run, week, dayOfYear);
        }

        /// <summary>Everything one night needs, computed lazily so a plan is built once and reused
        /// by the "can it act" test and the strike.</summary>
        private sealed class NightPlan
        {
            private readonly SabotageService _s;
            private readonly CoreSeason _season;
            private readonly int _day, _week, _dayOfYear;
            private readonly DifficultyStep _level;
            private readonly SaveSnapshot _save;
            private readonly ObtainabilityModel _model;
            private readonly Random _rng;
            private int? _crops, _stored;
            private DonatedSlot _reversion; private bool _reversionPlanned, _reversionUnmoderated;
            private TamperPlan _tamper; private bool _tamperPlanned, _tamperUnmoderated;

            public NightPlan(SabotageService s, CoreSeason season, int day, int week, int dayOfYear, DifficultyStep level, SaveSnapshot save, ObtainabilityModel model, Random rng)
            { _s = s; _season = season; _day = day; _week = week; _dayOfYear = dayOfYear; _level = level; _save = save; _model = model; _rng = rng; }

            private RunState Run => _s.Run;

            public bool CanAct(DarknessEvent e)
            {
                switch (e)
                {
                    case DarknessEvent.CropBlight:
                        if (!_s.Enabled(SabotageKind.Blight) || _s.CropsWarded(_season)) return false;
                        if (!SabotageSchedule.WithinCaps(SabotageKind.Blight, Run, _week, _dayOfYear)) return false;
                        return (_crops ??= BlightPass.LiveCropTiles().Count) > 0;
                    case DarknessEvent.ChestBlight:
                        if (!_s.Enabled(SabotageKind.Blight)) return false;
                        if (!SabotageSchedule.WithinCaps(SabotageKind.Blight, Run, _week, _dayOfYear)) return false;
                        return (_stored ??= SpoilagePass.StoredUnits(DarknessLevels.StorageReachesEverything(_level))) > 0;
                    case DarknessEvent.Reversion:
                        if (!_s.Enabled(SabotageKind.Reversion) || !SabotageSchedule.IsOpen(SabotageKind.Reversion, _season)) return false;
                        if (SabotageSchedule.IsQuietDay(SabotageKind.Reversion, _day) || !SabotageSchedule.WithinCaps(SabotageKind.Reversion, Run, _week, _dayOfYear)) return false;
                        return PlanReversion() != null;
                    case DarknessEvent.Tampering:
                        if (!_s.Enabled(SabotageKind.Tampering) || !SabotageSchedule.IsOpen(SabotageKind.Tampering, _season)) return false;
                        if (SabotageSchedule.IsQuietDay(SabotageKind.Tampering, _day) || !SabotageSchedule.WithinCaps(SabotageKind.Tampering, Run, _week, _dayOfYear)) return false;
                        return PlanTamper() != null;
                    default:
                        return false;
                }
            }

            public bool Execute(DarknessEvent e)
            {
                switch (e)
                {
                    case DarknessEvent.CropBlight:
                        return _s.Blight(BlightRule.Count(_crops ?? BlightPass.LiveCropTiles().Count, _season, _level), 0, _rng) > 0;
                    case DarknessEvent.ChestBlight:
                    {
                        bool everything = DarknessLevels.StorageReachesEverything(_level);
                        int units = _stored ?? SpoilagePass.StoredUnits(everything);
                        return _s.Blight(0, BlightRule.SpoilCount(units, _season, _level), _rng) > 0;
                    }
                    case DarknessEvent.Reversion:
                    {
                        DonatedSlot pick = PlanReversion();
                        if (pick == null || !_s.RevertSlot(pick)) return false;
                        if (_reversionUnmoderated) Run.UnmoderatedReversionSpent = true;
                        return true;
                    }
                    case DarknessEvent.Tampering:
                    {
                        TamperPlan plan = PlanTamper();
                        if (plan == null || !_s.WriteTamper(Game1.netWorldState.Value, plan.Target, plan.ItemId, plan.Stack, _dayOfYear)) return false;
                        if (_tamperUnmoderated) Run.UnmoderatedTamperSpent = true;
                        return true;
                    }
                    default:
                        return false;
                }
            }

            private DonatedSlot PlanReversion()
            {
                if (_reversionPlanned) return _reversion;
                _reversionPlanned = true;
                _reversionUnmoderated = NightRoll.UnmoderatedFires(_level, Run.UnmoderatedReversionSpent, _rng);
                int deadline = FairnessRule.ReversionDeadline(_dayOfYear, _level);
                _reversion = _s.PickReversion(_rng, _reversionUnmoderated ? null : (Func<string, bool>)(id => FairnessRule.Counts(id, _dayOfYear, deadline, _level, _save, _model)));
                if (_reversionUnmoderated) _s._monitor.Log("Darkness: this reversion is the loop's unmoderated one.", LogLevel.Info);
                return _reversion;
            }

            private TamperPlan PlanTamper()
            {
                if (_tamperPlanned) return _tamper;
                _tamperPlanned = true;
                _tamperUnmoderated = NightRoll.UnmoderatedFires(_level, Run.UnmoderatedTamperSpent, _rng);
                _tamper = _s.PlanTamper(_rng, _tamperUnmoderated ? null : (Func<string, bool>)(id => FairnessRule.Counts(id, _dayOfYear, FairnessRule.TamperDeadline, _level, _save, _model)));
                if (_tamperUnmoderated) _s._monitor.Log("Darkness: this tamper is the loop's unmoderated one.", LogLevel.Info);
                return _tamper;
            }
        }

        private sealed class TamperPlan
        {
            public TamperTarget Target;
            public string ItemId;
            public int Stack;
        }

        // ------------------------------------------------------------------ arming (debug)

        private readonly HashSet<SabotageKind> _armed = new HashSet<SabotageKind>();
        private BlightTarget? _armedBlightTarget;

        /// <summary>Debug: make <paramref name="kind"/> strike on tonight's real roll, so a playtest
        /// sleeps into it exactly as a player would (Jeff, 2026-09-14). Clears any waiting report.
        /// In memory only: a relaunch disarms.</summary>
        public string Arm(SabotageKind kind, BlightTarget? blightTarget = null)
        {
            int cleared = Run.PendingSabotageReports?.Count ?? 0;
            Run.PendingSabotageReports?.Clear();
            _armed.Add(kind);
            if (kind == SabotageKind.Blight) _armedBlightTarget = blightTarget;
            string target = kind == SabotageKind.Blight ? $", target {(blightTarget?.ToString() ?? "either (coin flip)")}" : "";
            string closed = SabotageSchedule.IsOpen(kind, Run.Season) ? "" : $" WARNING: {kind} is not open in {Run.Season}, so tonight will not strike.";
            string off = Enabled(kind) ? "" : $" WARNING: {kind} is switched off in the config.";
            return $"Darkness: {kind} armed for tonight's roll ({Run.Season} {Run.DayOfMonth}{target}, level {Level}); cleared {cleared} waiting report(s).{closed}{off}";
        }

        /// <summary>The armed event for tonight, if any and if it can act; consumes every arm.</summary>
        private DarknessEvent? TakeArmed(NightPlan night)
        {
            if (_armed.Count == 0) return null;
            DarknessEvent? result = null;
            foreach (SabotageKind kind in _armed.ToList())
            {
                DarknessEvent[] candidates = kind switch
                {
                    SabotageKind.Reversion => new[] { DarknessEvent.Reversion },
                    SabotageKind.Tampering => new[] { DarknessEvent.Tampering },
                    _ => _armedBlightTarget switch
                    {
                        BlightTarget.Crops => new[] { DarknessEvent.CropBlight },
                        BlightTarget.Chests => new[] { DarknessEvent.ChestBlight },
                        _ => new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight },
                    },
                };
                DarknessEvent? able = candidates.Cast<DarknessEvent?>().FirstOrDefault(e => night.CanAct(e.Value));
                _monitor.Log(able != null
                    ? $"Darkness: {kind} was armed; striking tonight as {able}."
                    : $"Darkness: {kind} was armed but cannot act tonight (closed, quiet, capped, warded or nothing fair).", LogLevel.Info);
                result ??= able;
            }
            _armed.Clear();
            _armedBlightTarget = null;
            return result;
        }

        private bool CropsWarded(CoreSeason season)
        {
            string ward = WardIds.CropWardFor(season);
            return ward != null && Meta.HasUpgrade(ward);
        }

        // ------------------------------------------------------------------ blight

        /// <summary>Kill <paramref name="crops"/> crops and take <paramref name="spoil"/> stored units
        /// now, then queue the report. Returns how many things were taken in all. Also the debug
        /// entry point (<c>tly_sabotage blight [crops] [spoil]</c>).</summary>
        public int Blight(int crops, int spoil, Random rng)
        {
            int killed = BlightPass.Strike(crops, rng);
            SpoilagePass.Taken taken = SpoilagePass.Strike(spoil, rng, DarknessLevels.StorageReachesEverything(Level));
            if (killed <= 0 && taken.Total <= 0)
            {
                _monitor.Log("Darkness: blight rolled but found nothing to strike or take.", LogLevel.Trace);
                return 0;
            }
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Blight, Count = killed, Spoiled = taken.Spoiled, Missing = taken.Missing,
            });
            _monitor.Log($"Darkness: {killed} crop(s) struck down, {taken.Spoiled} stored unit(s) spoiled, {taken.Missing} gone missing on {Run.Season} {Run.DayOfMonth}.", LogLevel.Info);
            return killed + taken.Total;
        }

        // ------------------------------------------------------------------ reversion

        /// <summary>Debug entry point (<c>tly_sabotage revert</c>): a fair pick at the current level, opened now.</summary>
        public bool Revert(Random rng)
        {
            int dayOfYear = Calendar.DayOfYear((int)Run.Season, Run.DayOfMonth);
            int deadline = FairnessRule.ReversionDeadline(dayOfYear, Level);
            SaveSnapshot save = SaveSnapshotReader.Read();
            ObtainabilityModel model = _obtainability();
            DonatedSlot pick = PickReversion(rng, id => FairnessRule.Counts(id, dayOfYear, deadline, Level, save, model));
            return pick != null && RevertSlot(pick);
        }

        private DonatedSlot PickReversion(Random rng, Func<string, bool> fair)
        {
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            SlotLedger ledger = Run.DonatedLedger();
            DonatedSlot pick = fair == null
                ? ReversionRule.Pick(ledger, _requirements(), rng)
                : ReversionRule.Pick(ledger, _requirements(), fair, rng);
            if (pick == null)
                _monitor.Log("Darkness: reversion found no slot it may fairly empty.", LogLevel.Trace);
            return pick;
        }

        private bool RevertSlot(DonatedSlot pick)
        {
            if (!TheLongestYear.Integration.CcSlotWriter.TryUnfill(pick.BundleIndex, pick.IngredientIndex))
            {
                _monitor.Log($"Darkness: reversion could not open slot {pick.BundleIndex}/{pick.IngredientIndex} on the board.", LogLevel.Warn);
                return false;
            }
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            string bundleName = _requirements().FirstOrDefault(r => r.BundleIndex == pick.BundleIndex)?.Name ?? "";
            Run.PendingSabotageReports.Add(new SabotageReport
            {
                Kind = SabotageKind.Reversion, Count = 1, BundleName = bundleName, ItemId = pick.ItemId,
            });
            _monitor.Log($"Darkness: {Strings.ItemName(pick.ItemId)} came undone from {bundleName} (slot {pick.BundleIndex}/{pick.IngredientIndex}) on {Run.Season} {Run.DayOfMonth}.", LogLevel.Info);
            return true;
        }

        // ------------------------------------------------------------------ tampering

        /// <summary>Debug entry point (<c>tly_sabotage tamper</c>): a fair pick at the current level, written now.</summary>
        public bool Tamper(Random rng, int dayOfYear)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return false;
            SaveSnapshot save = SaveSnapshotReader.Read();
            ObtainabilityModel model = _obtainability();
            TamperPlan plan = PlanTamper(rng, id => FairnessRule.Counts(id, dayOfYear, FairnessRule.TamperDeadline, Level, save, model));
            return plan != null && WriteTamper(worldState, plan.Target, plan.ItemId, plan.Stack, dayOfYear);
        }

        /// <summary>The target and replacement a tamper would write, or null when no unfilled slot has
        /// a fair replacement. <paramref name="fair"/> null means the unmoderated roll: the whole
        /// catalog, no check.</summary>
        private TamperPlan PlanTamper(Random rng, Func<string, bool> fair)
        {
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            SlotLedger ledger = Run.DonatedLedger();
            IReadOnlyList<BundleRequirement> requirements = _requirements();
            List<TamperTarget> targets = TamperRule.Targets(ledger, requirements).ToList();
            if (targets.Count == 0)
            {
                _monitor.Log("Darkness: tampering has no unfilled slot in an unfinished bundle.", LogLevel.Trace);
                return null;
            }
            IReadOnlyList<TamperCandidate> candidates = Candidates(fair);
            HashSet<string> held = HeldItemIds();
            var ordered = new List<TamperTarget>();
            while (targets.Count > 0)
            {
                TamperTarget next = TamperRule.PickTarget(targets, held.Contains, rng);
                if (next == null) break;
                ordered.Add(next);
                targets.Remove(next);
            }
            ItemAvailabilityModel availability = _availability();
            int weekOfWinter = Run.WeekInMonth;
            foreach (TamperTarget target in ordered)
            {
                int effort = availability.For(target.ItemId).Effort;
                TamperCandidate replacement = TamperRule.PickReplacement(target, effort, candidates, rng);
                if (replacement == null) continue;
                int maxCount = TamperRule.MaxCount(replacement.ItemId, QuantityAskPass.BasisByDeadline(replacement.ItemId, CoreSeason.Winter));
                int stack = TamperRule.Stack(maxCount, weekOfWinter, Level, rng);
                return new TamperPlan { Target = target, ItemId = replacement.ItemId, Stack = stack };
            }
            _monitor.Log("Darkness: tampering found no fair replacement for any open slot.", LogLevel.Info);
            return null;
        }

        /// <summary>The replacement pool: every catalog item (the board's own universe), with its room
        /// theme and the existing model's effort for closeness, filtered by <paramref name="fair"/>.</summary>
        private IReadOnlyList<TamperCandidate> Candidates(Func<string, bool> fair)
        {
            ItemAvailabilityModel availability = _availability();
            var result = new List<TamperCandidate>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (CcItem item in CcItemCatalog.Items)
            {
                string id = BundleParsing.NormalizeItemId(item.Id);
                if (!seen.Add(id)) continue;
                if (fair != null && !fair(id)) continue;
                int effort = availability.IsPlaced(id) ? availability.For(id).Effort : 0;
                result.Add(new TamperCandidate(id, item.Theme, effort));
            }
            return result;
        }

        private static HashSet<string> HeldItemIds()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            Farmer player = Game1.player;
            if (player != null)
                foreach (Item item in player.Items)
                    if (item != null) ids.Add(item.QualifiedItemId);
            Utility.ForEachLocation(loc =>
            {
                foreach (StardewValley.Object obj in loc.objects.Values)
                    if (obj is Chest chest)
                        foreach (Item item in chest.Items)
                            if (item != null) ids.Add(item.QualifiedItemId);
                return true;
            });
            return ids;
        }

        // WriteTamper: unchanged from the current file (copy it verbatim).

        // ShowMorning, ShowMorningReports, Hud: unchanged from the current file (copy verbatim).

        /// <summary>tly_sabotage fair: the fairness readout for one item at the current (or a named) level.</summary>
        public string Explain(string itemId, DifficultyStep? level)
        {
            DifficultyStep at = level ?? Level;
            int dayOfYear = Calendar.DayOfYear((int)Run.Season, Run.DayOfMonth);
            SaveSnapshot save = SaveSnapshotReader.Read();
            ObtainabilityModel model = _obtainability();
            int reversion = FairnessRule.ReversionDeadline(dayOfYear, at);
            FairnessVerdict asReversion = FairnessRule.Judge(itemId, dayOfYear, reversion, at, save, model);
            FairnessVerdict asTamper = FairnessRule.Judge(itemId, dayOfYear, FairnessRule.TamperDeadline, at, save, model);
            return $"{Strings.ItemName(itemId)} at {at}, hit day {dayOfYear}:\n"
                 + $"reversion (deadline day {reversion}): {FairnessRule.Explain(asReversion)}\n"
                 + $"tampering (deadline day {FairnessRule.TamperDeadline}): {asTamper.Summary}";
        }

        /// <summary>One-screen status for tly_sabotage.</summary>
        public string Status()
        {
            int week = Run.WeekOfYear;
            var lines = new List<string>
            {
                $"Darkness: level {Level}; blight={(Enabled(SabotageKind.Blight) ? "on" : "off")}, reversion={(Enabled(SabotageKind.Reversion) ? "on" : "off")}, tampering={(Enabled(SabotageKind.Tampering) ? "on" : "off")}; model {(_obtainability() == null ? "MISSING (no fairness filter)" : "published")}",
                $"  season {Run.Season} day {Run.DayOfMonth}: options = {string.Join(", ", NightRoll.Options(Run.Season))}; chance tonight {NightRoll.ChanceTonight(Run, week, Run.Season):P0}",
                $"  unmoderated this loop: reversion {(Run.UnmoderatedReversionSpent ? "spent" : "available")}, tamper {(Run.UnmoderatedTamperSpent ? "spent" : "available")}; guaranteed Winter tamper {(Run.GuaranteedTamperDone ? "done" : "pending")} (first Winter ever: {(!Meta.FirstWinterTamperSeen)})",
                $"  wards owned: {string.Join(", ", WardIds.All.Where(Meta.HasUpgrade).DefaultIfEmpty("none"))}",
                $"  blight week {Run.BlightWeek} nights {Run.BlightNightsThisWeek}; last reversion week {Run.LastReversionWeek}; tamper days [{string.Join(",", Run.TamperDays)}]",
                $"  live crops on the farm: {BlightPass.LiveCropTiles().Count}; units in chests (stash excluded): {SpoilagePass.StoredUnits(DarknessLevels.StorageReachesEverything(Level))}",
            };
            foreach (TamperRecord t in Run.Tampers)
                lines.Add($"  tampered: {t.BundleName} slot {t.IngredientIndex}: {Strings.ItemName(t.OldItemId)} -> {t.Stack} {Strings.ItemName(t.NewItemId)} (day {t.DayOfYear})");
            if (Run.PendingSabotageReports.Count > 0)
                lines.Add($"  pending morning reports: {Run.PendingSabotageReports.Count}");
            return string.Join("\n", lines);
        }
    }
}
```

Where the plan says "copy verbatim", take the method bodies from the current file (`WriteTamper`, `ShowMorning`, `ShowMorningReports`, `Hud`) without change. `WriteTamper` must stay `private bool WriteTamper(StardewValley.Network.NetWorldState worldState, TamperTarget target, string newItemId, int stack, int dayOfYear)`.

`SpoilagePass.StoredUnits(bool)` and `SpoilagePass.Strike(int, Random, bool)` are Task 7's signatures. To keep this task building on its own, add them to `SpoilagePass` NOW as overloads that ignore the flag and call the existing methods; Task 7 fills them in. Delete the `StrikesTonight` shim from `SabotageSchedule` in this task (its last caller is gone).

- [ ] **Step 3: Wire ModEntry**

Near line 698, add the model accessor as the sixth constructor argument:

```csharp
            _sabotage = new TheLongestYear.Loop.SabotageService(
                this.Monitor, _meta, _config,
                () => _runController?.Requirements ?? _requirements,
                () => _availability,
                () => _obtainability,
                RebuildBoardDerivedState,
                _sabotageMail);
```

In `CmdSabotage`, add a `fair` case and a level argument:

```csharp
                case "fair":
                {
                    if (args.Length < 2) { this.Monitor.Log("Usage: tly_sabotage fair <itemId> [easy|normal|hard|extreme]", LogLevel.Warn); break; }
                    TheLongestYear.Core.DifficultyStep? at = args.Length > 2 && Enum.TryParse(args[2], true, out TheLongestYear.Core.DifficultyStep parsed) ? parsed : null;
                    this.Monitor.Log(_sabotage.Explain(args[1], at), LogLevel.Info);
                    break;
                }
```

Update both usage strings (the `ConsoleCommands.Add` text at line 323 and the `default:` case) to include `fair <itemId> [level]`. In the `blight` case, pass the level to `BlightPass.CountFor` and `BlightRule.SpoilCount` as Task 2 step 7 already did.

- [ ] **Step 4: Build and run the suite**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` then the test command.
Expected: 0 errors, all tests pass. Warnings about unused `using` are fine; fix any error by checking the decompile for the game API names (say so in the commit).

- [ ] **Step 5: Commit and push**

```
git add src/TheLongestYear/Loop/SaveSnapshotReader.cs src/TheLongestYear/Loop/SabotageService.cs src/TheLongestYear/Loop/SpoilagePass.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear.Core/Sabotage/SabotageSchedule.cs
git commit -m "Darkness: one night roll in the glue, the fairness picker on reversion and tampering, tly_sabotage fair"
git push origin story
```

---

### Task 7: Extreme chest blight reaches tools and placed machines

**Files:**
- Modify: `src/TheLongestYear/Loop/SpoilagePass.cs`
- Modify: `src/TheLongestYear.Core/Sabotage/SabotageRules.cs` (`BlightRule.UnitsOf`)
- Test: `tests/TheLongestYear.Tests/DarknessLevelsTests.cs`

**Interfaces:**
- Produces: `BlightRule.UnitsOf(int stack, bool bigCraftable)` (Core, pure: a big craftable weighs `DarknessLevels.BigCraftableUnits`); `SpoilagePass.StoredUnits(bool everything)`, `SpoilagePass.Strike(int count, Random rng, bool everything)`.

- [ ] **Step 1: Write the failing test (the pure part)**

Add to `DarknessLevelsTests`:

```csharp
    [Theory]
    [InlineData(1, false, 1)]
    [InlineData(40, false, 40)]
    [InlineData(1, true, 3)]
    [InlineData(2, true, 6)]
    public void A_big_craftable_weighs_three_units(int stack, bool bigCraftable, int units)
        => Assert.Equal(units, BlightRule.UnitsOf(stack, bigCraftable));
```

Run the filter; expected: build error.

- [ ] **Step 2: Add UnitsOf**

In `BlightRule`:

```csharp
    /// <summary>How many of the night's take one item costs: a stack counts its size, a machine
    /// (placed or stored) counts <see cref="DarknessLevels.BigCraftableUnits"/> per copy (Jeff, 2026-09-15).</summary>
    public static int UnitsOf(int stack, bool bigCraftable)
        => Math.Max(0, stack) * (bigCraftable ? DarknessLevels.BigCraftableUnits : 1);
```

- [ ] **Step 3: Rewrite SpoilagePass**

Replace `src/TheLongestYear/Loop/SpoilagePass.cs` with:

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>The blight front reaching into storage (Jeff, 2026-09-09): units vanish from the
    /// player's chests in the night, one at a time off random stacks. Food spoils; anything else
    /// goes missing. Every chest on every map counts except the Junimo Stash and chests on a Circle
    /// of Warding. Below Extreme only plain objects are taken (never tools, weapons or big
    /// craftables). On Extreme (<paramref name="everything"/>, spec 2026-09-15 Part B, 2.5) anything
    /// in an unwarded chest can go, and machines placed on the FARM map join the pool at three
    /// units each; a machine on a circle's tiles is protected like a chest there.</summary>
    internal static class SpoilagePass
    {
        private sealed class Entry
        {
            public Chest Chest;          // null for a placed machine
            public int Slot;
            public Item Item;
            public GameLocation Location; // placed machine only
            public Vector2 Tile;          // placed machine only
            public bool BigCraftable;
            public int Units => BlightRule.UnitsOf(Item.Stack, BigCraftable);
        }

        public readonly struct Taken
        {
            public readonly int Spoiled;
            public readonly int Missing;
            public Taken(int spoiled, int missing) { Spoiled = spoiled; Missing = missing; }
            public int Total => Spoiled + Missing;
        }

        private static List<Entry> Entries(bool everything)
        {
            var entries = new List<Entry>();
            var circles = CircleOfWardingService.ProtectedTiles();
            Farm farm = Game1.getFarm();
            Utility.ForEachLocation(loc =>
            {
                foreach (KeyValuePair<Vector2, StardewValley.Object> pair in loc.objects.Pairs)
                {
                    StardewValley.Object obj = pair.Value;
                    if (obj is Chest chest)
                    {
                        if (chest.modData.ContainsKey(JunimoStashService.StashModDataKey)) continue;
                        if (CircleOfWardingService.Covers(circles, loc, chest.TileLocation)) continue;
                        var items = chest.Items;
                        for (int i = 0; i < items.Count; i++)
                        {
                            Item item = items[i];
                            if (item == null || item.Stack <= 0) continue;
                            bool big = item is StardewValley.Object o && o.bigCraftable.Value;
                            bool plain = item is StardewValley.Object && !big;
                            if (!everything && !plain) continue;
                            entries.Add(new Entry { Chest = chest, Slot = i, Item = item, BigCraftable = big });
                        }
                        continue;
                    }
                    if (!everything || loc != farm) continue;
                    if (!obj.bigCraftable.Value) continue;
                    if (CircleOfWardingService.Covers(circles, loc, pair.Key)) continue;
                    entries.Add(new Entry { Item = obj, Location = loc, Tile = pair.Key, BigCraftable = true });
                }
                return true;
            });
            return entries;
        }

        /// <summary>Total units at stake (stash excluded), for the roll.</summary>
        public static int StoredUnits(bool everything)
        {
            int units = 0;
            foreach (Entry e in Entries(everything)) units += e.Units;
            return units;
        }

        /// <summary>Take up to <paramref name="count"/> units, each off an entry picked by
        /// <paramref name="rng"/> weighted by its units. A machine costs three of the count.</summary>
        public static Taken Strike(int count, Random rng, bool everything)
        {
            if (count <= 0) return new Taken(0, 0);
            List<Entry> entries = Entries(everything);
            int spoiled = 0, missing = 0, taken = 0;
            while (taken < count && entries.Count > 0)
            {
                int total = 0;
                foreach (Entry e in entries) total += e.Units;
                if (total <= 0) break;
                int roll = rng.Next(total);
                Entry hit = entries[0];
                foreach (Entry e in entries)
                {
                    roll -= e.Units;
                    if (roll < 0) { hit = e; break; }
                }
                int cost = BlightRule.UnitsOf(1, hit.BigCraftable);
                taken += cost;
                if (hit.BigCraftable || !BlightRule.IsPerishableCategory(hit.Item.Category)) missing += cost; else spoiled += cost;
                if (hit.Chest != null)
                {
                    hit.Item.Stack -= 1;
                    if (hit.Item.Stack <= 0) { hit.Chest.Items[hit.Slot] = null; entries.Remove(hit); }
                }
                else
                {
                    // A placed machine vanishes with whatever it held (spec 2.5).
                    hit.Location.objects.Remove(hit.Tile);
                    entries.Remove(hit);
                }
            }
            return new Taken(spoiled, missing);
        }
    }
}
```

Remove the Task 6 placeholder overloads (this is the real version). `loc.objects.Pairs` is the `OverlaidDictionary` pair enumerator; check the decompile (`StardewValley.Network.OverlaidDictionary`) for the exact member name (`Pairs`) and use `loc.objects.Keys` plus indexing if it differs.

- [ ] **Step 4: Build, run the suite, commit, push**

Run the build and test commands. Expected: 0 errors, all pass.

```
git add src/TheLongestYear/Loop/SpoilagePass.cs src/TheLongestYear.Core/Sabotage/SabotageRules.cs tests/TheLongestYear.Tests/DarknessLevelsTests.cs
git commit -m "Darkness: on Extreme chest blight reaches tools, weapons and placed farm machines at three units each"
git push origin story
```

---

### Task 8: Config migration, the GMCM dial, tly_difficulty, the reset log, i18n

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (config load near line 127; GMCM near line 2440; `CmdDifficulty` near line 4276)
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs` (the reset log near line 156)
- Modify: `src/TheLongestYear/i18n/default.json`
- Test: `tests/TheLongestYear.Tests/I18nGuardTests.cs` runs as-is (it scans literal keys)

- [ ] **Step 1: Migrate the config on load**

In `ModEntry.Entry`, in the "One-shot config migration" block after the stash-tile migration, add:

```csharp
            // Darkness dial (spec 2026-09-15 Part B, 2.3): a config from before the dial sets it,
            // and the overall lever, to the lowest of the ten existing dials (Jeff, 2026-09-14).
            if (_config.Difficulty.MigrateDarkness())
            {
                migrated = true;
                this.Monitor.Log($"Migrated config.json: Darkness dial set to {_config.Difficulty.Darkness} (the lowest existing dial).", LogLevel.Info);
            }
```

Check the block writes the config back when `migrated` is true (`helper.WriteConfig(_config)`); if it only logs, add the write.

- [ ] **Step 2: Add the i18n strings**

In `src/TheLongestYear/i18n/default.json`, after `"gmcm.difficulty.hold-prices.tooltip"` add (these were drafted in the game-writing register; keep them verbatim):

```json
    "gmcm.difficulty.darkness.name": "Darkness",
    "gmcm.difficulty.darkness.tooltip": "How hard the darkness hits from Summer on. Sets how many crops or stored items a blight night takes, and whether the darkness checks that you can still get an item back before it undoes a donation or changes what a bundle asks for. Easy and Normal only take what you can replace in time. Hard and Extreme can take anything once a loop, and on Extreme a blight night can also reach tools and machines. The three switches under Features still turn each front off.",
```

Mind the trailing comma rules of the surrounding JSON.

- [ ] **Step 3: Add the GMCM option**

After the `HoldPrices` `AddDifficultyOption(...)` call add:

```csharp
            AddDifficultyOption(
                () => _config.Difficulty.DarknessOrLowest, v => _config.Difficulty.Darkness = v,
                () => Strings.Get("gmcm.difficulty.darkness.name"),
                () => Strings.Get("gmcm.difficulty.darkness.tooltip"));
```

- [ ] **Step 4: The probe and the reset log**

In `CmdDifficulty` after `LogStep("hold prices", ...)` add `LogStep("darkness", configured.DarknessOrLowest, live.Darkness);`. In `WorldResetService` the "difficulty modifiers active" log string: append `$", darkness {_meta.Difficulty.Darkness}"` before the closing of that message.

- [ ] **Step 5: Build, run the suite (I18nGuardTests included), commit, push**

Expected: 0 errors, all pass.

```
git add src/TheLongestYear/ModEntry.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/i18n/default.json
git commit -m "Darkness: the dial in GMCM, config migration to the lowest dial, tly_difficulty row"
git push origin story
```

---

### Task 9: Guards, live check, docs

**Files:**
- Create: `tests/TheLongestYear.Tests/BoardPathsNeverReadTheModelTests.cs`
- Modify: `STATUS.md`, `TODO.md`, `docs/superpowers/specs/2026-09-15-darkness-obtainability-wiring-design.md` (Status line), `docs/superpowers/specs/2026-09-14-darkness-rework-design.md` (Status line)
- Append: `.superpowers/sdd/2026-09-15-darkness-obtainability-wiring/progress.md`

- [ ] **Step 1: The static guard**

Create `tests/TheLongestYear.Tests/BoardPathsNeverReadTheModelTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Part B (spec 2026-09-15) owes a proof that board generation, gates, goals and pacing
/// never read the obtainability model or the darkness: byte-identical tly_genbundles and
/// tly_gatecheck across seeds. This is the static half; the live diff is in the plan's Task 9.</summary>
public class BoardPathsNeverReadTheModelTests
{
    private static readonly string[] BoardFiles =
    {
        "TheLongestYear.Core/ItemPoolBuilder.cs", "TheLongestYear.Core/BundleSlotFiller.cs",
        "TheLongestYear.Core/BoardRequirements.cs", "TheLongestYear.Core/GateEvaluator.cs",
        "TheLongestYear.Core/GoalObtainability.cs", "TheLongestYear.Core/BundleDeadlines.cs",
        "TheLongestYear.Core/QuantityAskPass.cs", "TheLongestYear.Core/AuthoredBundleComposer.cs",
        "TheLongestYear.Core/BundleGenerationTuning.cs", "TheLongestYear.Core/BonusItemSampler.cs",
        "TheLongestYear.Core/BonusSlotSampler.cs",
    };

    private static readonly string[] Forbidden = { "Obtainability", "FairnessRule", "SaveSnapshot", "NightRoll", "Sabotage" };

    private static string SrcRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    [Fact]
    public void Board_files_never_name_the_model_or_the_darkness()
    {
        var hits = new List<string>();
        foreach (string relative in BoardFiles)
        {
            string path = Path.Combine(SrcRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { hits.Add($"{relative}: missing (update the list)"); continue; }
            string text = File.ReadAllText(path);
            foreach (string word in Forbidden)
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b"))
                    hits.Add($"{relative}: {word}");
        }
        Assert.Empty(hits);
    }
}
```

Run the suite. If a listed file does not exist under that name, fix the list to the real file names (grep for the class) rather than deleting the entry.

- [ ] **Step 2: Live check (the agent's own automated launch, throwaway save)**

Follow `docs/HEADLESS_DRIVING.md`. Deploy the final build minimized, load `None_449077472`, wait for `Run \d+ ready` plus 45 seconds, then:

```
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_difficulty|tly_sabotage status|tly_sabotage fair (O)24|tly_sabotage fair (O)163 extreme|tly_sabotage fair (O)426 normal|tly_genbundles 1|tly_genbundles 2|tly_genbundles 3|tly_gatecheck"
```

Then wait for the gatecheck output and capture the same filtered lines as Task 0 into `.superpowers/sdd/2026-09-15-darkness-obtainability-wiring/after-genbundles.txt`, and diff:

```
git diff --no-index .superpowers/sdd/2026-09-15-darkness-obtainability-wiring/baseline-genbundles.txt .superpowers/sdd/2026-09-15-darkness-obtainability-wiring/after-genbundles.txt
```

Expected: no diff. Record in the ledger: the `Obtainability model:` log line, the `tly_difficulty` darkness row (configured and in force), the `status` lines (level, chance, options, unmoderated flags), and the three `fair` readouts. Expected shapes: Parsnip counts on Normal in any season with a Pierre row; Legend (`(O)163`) at extreme reads its chance and Skill routes; Goat Cheese (`(O)426`) on Normal shows `building:Barn` days added or the barn met. If the save is in Spring the `fair` readouts still work (the hit day is today). Do not sleep the game and do not run the played sequence: that is Jeff's.

Then run `git checkout -- test-output/log-archive` if deploy pruned tracked archives (runbook note).

- [ ] **Step 3: Docs**

- `docs/superpowers/specs/2026-09-15-darkness-obtainability-wiring-design.md`: Status line to `built (commits <first>..<last>), live-checked 2026-09-15 on the throwaway save; Jeff's played run pending`.
- `docs/superpowers/specs/2026-09-14-darkness-rework-design.md`: Status line to `built in Part B, see 2026-09-15-darkness-obtainability-wiring-design.md`.
- `STATUS.md`: a new top section "2026-09-15: darkness rework Part B built" with the commit range, the test count, the live log lines from Step 2 (verbatim, in a fenced block), the byte-identical result, and the plain statement that the game was launched by the agent minimized on `None_449077472` and left running. Update the header lines (Last updated, Tests, Build).
- `TODO.md`: in "Darkness pushback: Jeff's live test before it ships", add a paragraph on what changed (one roll, the dial, the fairness picker, Extreme storage reach, the `fair` command for reading a verdict) and the staging recipe: `tly_sabotage arm revert` then sleep; `tly_sabotage arm tamper` on a Winter save then sleep; set the Darkness dial in GMCM and rewind for it to take. In "Obtainability phase 2", replace the "Still open from phase 1 (Part B, the first consumer)" bullets with a CLOSED line pointing at the spec.
- Every edit: no em dashes.

- [ ] **Step 4: Ledger, commit, push**

Append the task lines to `progress.md`. Then:

```
git add tests/TheLongestYear.Tests/BoardPathsNeverReadTheModelTests.cs STATUS.md TODO.md docs/superpowers/specs/2026-09-15-darkness-obtainability-wiring-design.md docs/superpowers/specs/2026-09-14-darkness-rework-design.md .superpowers/sdd/2026-09-15-darkness-obtainability-wiring/
git commit -m "Docs: darkness rework Part B built, live-checked, board output byte-identical"
git push origin story
```

---

## Self-review notes (done while writing)

- **Spec coverage.** 1.1 snapshot: Task 4 and 6. 1.2 table: Task 4 (every row has a test). 1.3 gap tables: Task 2 constants, Task 4 maths. 1.4 deadlines: Task 4. 1.5 unmoderated roll: Task 3 (roll), Task 6 (spent flags, whole catalog). 1.6 picks: Tasks 5 and 6. 2.1 to 2.2: Task 3 and 6. 2.3 dial and migration: Tasks 1 and 8. 2.4 blight by level: Task 2. 2.5 Extreme storage: Task 7. 2.6 guaranteed tamper and stack limits: Tasks 2, 3, 6. 2.7 debug: Task 6 and 8. 3.3 guards: Task 9 plus the blind guard untouched. 3.4 tests: listed per task. 3.5 live: Task 9.
- **Interpretation to flag to Jeff (not in the spec's words):** a missing recipe rules a route out on Easy, Normal and Hard EXCEPT when the same route's unlock is a shop sale or a Queen of Sauce episode, because those are waits the game itself prices and the table already holds the Sunday. Without this, the year 2 TV rule on Hard could never fire (no year 1 player knows a year 2 recipe). Task 4 tests pin it.
- **Type consistency.** `BlightRule.Count(int, Season, DifficultyStep)`, `SpoilCount(int, Season, DifficultyStep)`, `TamperRule.MaxCount(string, double?)`, `TamperRule.Stack(int, int, DifficultyStep, Random)` (unchanged), `ReversionRule.Pick(ledger, requirements, Func<string,bool>, Random)`, `NightRoll.*` as listed in Task 3, `FairnessRule.Judge/Counts/Explain/ReversionDeadline/TamperDeadline` as in Task 4, `SpoilagePass.StoredUnits(bool)`/`Strike(int, Random, bool)` as in Task 7, `SabotageService(monitor, store, config, requirements, availability, obtainability, rebuildBoard, mail)` as in Task 6.
- **Old tests rewritten by spec change:** the two blight-count theories (Task 2) and the three `StrikesTonight` tests (Task 2). Everything else is additive.
