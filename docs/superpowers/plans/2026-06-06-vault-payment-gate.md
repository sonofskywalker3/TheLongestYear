# Vault payment + count-based vault gate — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the CC vault payable in normal play, gate each season on a tier-agnostic cumulative count of paid vault bundles, reward the payment with gold-scaled JP, and show per-season vault status in the green journal.

**Architecture:** Vanilla CC bundle-completion state is the source of truth. A live `DonationObserver` hook records in-game vault payments immediately; an additive `VaultPaymentSync.Reconcile` backstop (run at day-end, journal-open, shrine-open) backfills anything the observer couldn't see (e.g. a mid-run mod upgrade where the bundle was already complete). Both funnel through one idempotent sink, `DonationService.OnVaultBundlePaid`. The gate becomes `VaultBundlesPaid.Count ≥ season ordinal` (or `keep_bus_unlocked`), and `keep_bus_unlocked` now needs run-reach `bus:4`.

**Tech Stack:** C# / .NET 6, SMAPI 4.x, Harmony, xUnit. Two projects: `TheLongestYear.Core` (pure, unit-tested) and `TheLongestYear` (SMAPI glue). Solution `TheLongestYear.sln`.

**Conventions for every commit:**
- Master is the release line → bump `src/TheLongestYear/manifest.json` `Version` by PATCH each commit (start: 0.9.7 → first commit 0.9.8, then 0.9.9, …).
- Stage only the files the task touches (never `test-output/`, `media/`, `marketing/`).
- Commit message footer:
  ```
  Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
  ```
- Build check (game may be running → DLL locked): `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
- Test: `dotnet test TheLongestYear.sln -c Release` (filter with `--filter "FullyQualifiedName~<ClassName>"`).

---

## File Structure

- **Modify** `src/TheLongestYear.Core/VaultRules.cs` — count-based gate + index helpers; drop 0.9.7 tier helpers.
- **Modify** `src/TheLongestYear.Core/JpSettings.cs` — add `VaultGoldPerJp`.
- **Modify** `src/TheLongestYear.Core/JpCalculator.cs` — add `VaultPayment`.
- **Modify** `src/TheLongestYear.Core/RunState.cs` — add `TryMarkVaultBundlePaid`.
- **Modify** `src/TheLongestYear.Core/UpgradeCatalog.cs` — `keep_bus_unlocked` run-reach `bus:1`→`bus:4`.
- **Modify** `src/TheLongestYear/Donations/DonationService.cs` — add `OnVaultBundlePaid`.
- **Modify** `src/TheLongestYear/Donations/DonationObserver.cs` — route vault indices to `OnVaultBundlePaid`.
- **Create** `src/TheLongestYear/Integration/VaultPaymentSync.cs` — reconcile from vanilla CC.
- **Modify** `src/TheLongestYear/Loop/RunController.cs` — reconcile before the day-end gate.
- **Modify** `src/TheLongestYear/UI/MenuLauncher.cs` — reconcile before journal + shrine.
- **Modify** `src/TheLongestYear/Integration/RunReachEvaluator.cs` — `bus` returns count.
- **Modify** `src/TheLongestYear/UI/SeasonGoalsMenu.cs` — count-based vault line.
- **Modify** tests: `VaultRulesTests.cs`, `JpCalculatorTests.cs`, `RunStateTests.cs`, `UpgradeCatalogTests.cs`, `RunReachRequirementTests.cs`.

---

## Task 1: VaultRules — count-based gate + index helpers

**Files:**
- Modify: `src/TheLongestYear.Core/VaultRules.cs`
- Test: `tests/TheLongestYear.Tests/VaultRulesTests.cs`

- [ ] **Step 1: Replace the VaultRulesTests body with count-based tests**

Open `tests/TheLongestYear.Tests/VaultRulesTests.cs` and replace its entire contents with:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class VaultRulesTests
{
    [Theory]
    [InlineData(Season.Spring, 1)]
    [InlineData(Season.Summer, 2)]
    [InlineData(Season.Fall,   3)]
    [InlineData(Season.Winter, 4)]
    public void SeasonOrdinal_is_one_based(Season season, int expected)
        => Assert.Equal(expected, VaultRules.SeasonOrdinal(season));

    [Theory]
    [InlineData(VaultRules.Vault2500,  2500)]
    [InlineData(VaultRules.Vault5000,  5000)]
    [InlineData(VaultRules.Vault10000, 10000)]
    [InlineData(VaultRules.Vault25000, 25000)]
    public void GoldForIndex_maps_each_index_to_its_price(int index, int gold)
        => Assert.Equal(gold, VaultRules.GoldForIndex(index));

    [Theory]
    [InlineData(34, true)]
    [InlineData(37, true)]
    [InlineData(33, false)]
    [InlineData(38, false)]
    public void IsVaultIndex_only_true_for_34_to_37(int index, bool expected)
        => Assert.Equal(expected, VaultRules.IsVaultIndex(index));

    [Fact]
    public void Gate_needs_count_at_least_season_ordinal()
    {
        var run = new RunState();
        var meta = new MetaState();

        // Spring (ordinal 1): 0 paid fails, 1 paid passes — any tier.
        Assert.False(VaultRules.IsVaultGateSatisfied(Season.Spring, run, meta));
        run.VaultBundlesPaid.Add(VaultRules.Vault25000);   // tier doesn't matter
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Spring, run, meta));

        // Summer (ordinal 2): still only 1 paid → fails until a second.
        Assert.False(VaultRules.IsVaultGateSatisfied(Season.Summer, run, meta));
        run.VaultBundlesPaid.Add(VaultRules.Vault2500);
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Summer, run, meta));
    }

    [Fact]
    public void Paying_all_four_in_spring_satisfies_winter()
    {
        var run = new RunState();
        run.VaultBundlesPaid.AddRange(new[]
            { VaultRules.Vault2500, VaultRules.Vault5000, VaultRules.Vault10000, VaultRules.Vault25000 });
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Winter, run, new MetaState()));
    }

    [Fact]
    public void Keep_bus_unlocked_short_circuits_with_zero_paid()
    {
        var run = new RunState();   // nothing paid
        var meta = new MetaState { OwnedUpgrades = { VaultRules.KeepBusUnlockedId } };
        Assert.True(VaultRules.IsVaultGateSatisfied(Season.Winter, run, meta));
    }

    [Fact]
    public void PaidCount_reflects_the_list()
    {
        var run = new RunState();
        run.VaultBundlesPaid.Add(VaultRules.Vault2500);
        run.VaultBundlesPaid.Add(VaultRules.Vault5000);
        Assert.Equal(2, VaultRules.PaidCount(run));
    }

    [Fact]
    public void Keep_bus_unlocked_is_in_the_upgrade_catalog()
    {
        UpgradeDefinition? def = UpgradeCatalog.TryGet(VaultRules.KeepBusUnlockedId);
        Assert.NotNull(def);
        Assert.Equal(UpgradeCategory.Buildings, def!.Category);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~VaultRulesTests"`
Expected: FAIL — compile errors (`SeasonOrdinal`, `GoldForIndex`, `IsVaultIndex`, `PaidCount` do not exist; old `GoldCostForSeason`/`DescribeGate` tests removed).

- [ ] **Step 3: Rewrite VaultRules.cs**

Replace the entire contents of `src/TheLongestYear.Core/VaultRules.cs` with:

```csharp
namespace TheLongestYear.Core;

/// <summary>
/// Vault (bus-repair) gate rules. Each season requires a cumulative, tier-agnostic minimum number
/// of vanilla 1.6 Vault money bundles paid THIS run: at least the season ordinal (Spring 1,
/// Summer 2, Fall 3, Winter 4). Paying all four in Spring pre-satisfies every season. The
/// keep_bus_unlocked Buildings upgrade short-circuits the gate (bus stays restored across runs).
///
/// Vanilla indices (Data/Bundles "Vault/N"):
///   34 = 2,500g · 35 = 5,000g · 36 = 10,000g · 37 = 25,000g  (42,500g total)
/// </summary>
public static class VaultRules
{
    /// <summary>Upgrade id that, when owned, satisfies the vault gate every season.</summary>
    public const string KeepBusUnlockedId = "keep_bus_unlocked";

    public const int Vault2500   = 34;
    public const int Vault5000   = 35;
    public const int Vault10000  = 36;
    public const int Vault25000  = 37;

    /// <summary>The four vanilla vault bundle indices, low tier to high.</summary>
    public static readonly int[] VaultIndices = { Vault2500, Vault5000, Vault10000, Vault25000 };

    /// <summary>1-based count of vault bundles required by the given season's day-28 checkpoint
    /// (Spring 1 … Winter 4).</summary>
    public static int SeasonOrdinal(Season season) => (int)season + 1;

    /// <summary>True if <paramref name="index"/> is one of the four vault bundle indices.</summary>
    public static bool IsVaultIndex(int index) => index >= Vault2500 && index <= Vault25000;

    /// <summary>The gold price of a given vault bundle index (drives the JP scaling).</summary>
    public static int GoldForIndex(int index) => index switch
    {
        Vault2500  => 2500,
        Vault5000  => 5000,
        Vault10000 => 10000,
        Vault25000 => 25000,
        _ => 0
    };

    /// <summary>Number of distinct vault bundles paid this run.</summary>
    public static int PaidCount(RunState run) => run.VaultBundlesPaid.Count;

    /// <summary>Which vault bundle gates a season's monthly checkpoint. Kept for the tly_payvault
    /// debug command (resolves a season name to an index); the live gate is count-based and does
    /// NOT use this.</summary>
    public static int BundleIndexForSeason(Season season) => season switch
    {
        Season.Spring => Vault2500,
        Season.Summer => Vault5000,
        Season.Fall   => Vault10000,
        Season.Winter => Vault25000,
        _ => -1
    };

    /// <summary>True if the player has satisfied this season's vault gate: owns keep_bus_unlocked,
    /// or has paid at least <see cref="SeasonOrdinal"/> vault bundles this run (any tiers).</summary>
    public static bool IsVaultGateSatisfied(Season season, RunState run, MetaState meta)
    {
        if (meta.HasUpgrade(KeepBusUnlockedId))
            return true;
        return run.VaultBundlesPaid.Count >= SeasonOrdinal(season);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~VaultRulesTests"`
Expected: PASS (all VaultRulesTests green).

- [ ] **Step 5: Commit**

```bash
# bump manifest.json Version 0.9.7 -> 0.9.8
git add src/TheLongestYear.Core/VaultRules.cs tests/TheLongestYear.Tests/VaultRulesTests.cs src/TheLongestYear/manifest.json
git commit -m "Vault gate: count-based, tier-agnostic (count >= season ordinal)"
```

---

## Task 2: JP scaling for vault payments

**Files:**
- Modify: `src/TheLongestYear.Core/JpSettings.cs`
- Modify: `src/TheLongestYear.Core/JpCalculator.cs`
- Test: `tests/TheLongestYear.Tests/JpCalculatorTests.cs`

- [ ] **Step 1: Add the failing test**

Append inside the `JpCalculatorTests` class in `tests/TheLongestYear.Tests/JpCalculatorTests.cs` (before the closing brace):

```csharp
    [Theory]
    [InlineData(2500, 3)]      // 2500/1000 = 2.5 -> 3
    [InlineData(5000, 5)]
    [InlineData(10000, 10)]
    [InlineData(25000, 25)]
    public void VaultPayment_scales_with_gold(int gold, long expected)
        => Assert.Equal(expected, Make().VaultPayment(gold));

    [Fact]
    public void VaultPayment_is_at_least_one_jp()
        => Assert.Equal(1, Make().VaultPayment(100));   // 100/1000 = 0.1 -> floor would be 0; min 1

    [Fact]
    public void VaultPayment_does_not_season_scale()
    {
        // Same gold in any week returns the same JP (unlike PerItem). VaultPayment takes no week.
        Assert.Equal(Make().VaultPayment(25000), Make().VaultPayment(25000));
        Assert.True(Make().VaultPayment(25000) > Make().VaultPayment(2500));
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~JpCalculatorTests"`
Expected: FAIL — `VaultPayment` does not exist.

- [ ] **Step 3: Add `VaultGoldPerJp` to JpSettings**

In `src/TheLongestYear.Core/JpSettings.cs`, add after the `WeeklyQuestCompletionBonus` property (after line 25):

```csharp

    /// <summary>Gold paid to the CC Vault per 1 JP awarded. Vault payments reward JP proportional
    /// to the gold sunk (2,500g→3, 5,000g→5, 10,000g→10, 25,000g→25 at the default rate). Unlike
    /// item/bundle JP this is NOT season-multiplied — gold's value is season-independent.</summary>
    public int VaultGoldPerJp { get; set; } = 1000;
```

- [ ] **Step 4: Add `VaultPayment` to JpCalculator**

In `src/TheLongestYear.Core/JpCalculator.cs`, add after the `WeeklyQuestBonus` method (after line 36):

```csharp

    /// <summary>JP for paying a CC Vault money bundle, proportional to the gold spent
    /// (gold / VaultGoldPerJp, rounded, minimum 1). NOT season-multiplied.</summary>
    public long VaultPayment(int gold)
    {
        if (_s.VaultGoldPerJp <= 0) return 1;
        long jp = (long)Math.Round((double)gold / _s.VaultGoldPerJp, MidpointRounding.AwayFromZero);
        return jp < 1 ? 1 : jp;
    }
```

- [ ] **Step 5: Run the test to verify it passes**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~JpCalculatorTests"`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
# bump manifest.json Version 0.9.8 -> 0.9.9
git add src/TheLongestYear.Core/JpSettings.cs src/TheLongestYear.Core/JpCalculator.cs tests/TheLongestYear.Tests/JpCalculatorTests.cs src/TheLongestYear/manifest.json
git commit -m "JP: scale vault payment to gold paid (VaultGoldPerJp)"
```

---

## Task 3: RunState.TryMarkVaultBundlePaid

**Files:**
- Modify: `src/TheLongestYear.Core/RunState.cs`
- Test: `tests/TheLongestYear.Tests/RunStateTests.cs`

- [ ] **Step 1: Add the failing test**

Append inside the `RunStateTests` class in `tests/TheLongestYear.Tests/RunStateTests.cs` (before the closing brace):

```csharp
    [Fact]
    public void TryMarkVaultBundlePaid_is_true_once_then_false()
    {
        var run = new RunState();
        Assert.True(run.TryMarkVaultBundlePaid(34));
        Assert.False(run.TryMarkVaultBundlePaid(34));   // idempotent — no duplicate
        Assert.True(run.TryMarkVaultBundlePaid(35));
        Assert.Equal(2, run.VaultBundlesPaid.Count);
    }

    [Fact]
    public void BeginNewRun_clears_vault_bundles_paid()
    {
        var run = new RunState();
        run.TryMarkVaultBundlePaid(34);
        run.BeginNewRun(seed: 1);
        Assert.Empty(run.VaultBundlesPaid);
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~RunStateTests"`
Expected: FAIL — `TryMarkVaultBundlePaid` does not exist. (The `BeginNewRun` clearing already works, but the file won't compile without the new method.)

- [ ] **Step 3: Add the method**

In `src/TheLongestYear.Core/RunState.cs`, add after `TryMarkRoomAwarded` (after line 115):

```csharp

    /// <summary>Record a vault bundle as paid this run; returns false if it was already recorded
    /// (keeps <see cref="VaultBundlesPaid"/> deduped so the count maxes at 4).</summary>
    public bool TryMarkVaultBundlePaid(int bundleIndex)
    {
        if (VaultBundlesPaid.Contains(bundleIndex))
            return false;
        VaultBundlesPaid.Add(bundleIndex);
        return true;
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~RunStateTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
# bump manifest.json Version 0.9.9 -> 0.9.10
git add src/TheLongestYear.Core/RunState.cs tests/TheLongestYear.Tests/RunStateTests.cs src/TheLongestYear/manifest.json
git commit -m "RunState: TryMarkVaultBundlePaid (deduped vault ledger)"
```

---

## Task 4: keep_bus_unlocked needs bus:4

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs:292`
- Test: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

- [ ] **Step 1: Add the failing test**

Append inside the `UpgradeCatalogTests` class in `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs` (before the closing brace):

```csharp
    [Fact]
    public void Keep_bus_unlocked_requires_all_four_vault_bundles()
    {
        UpgradeDefinition? def = UpgradeCatalog.TryGet(VaultRules.KeepBusUnlockedId);
        Assert.NotNull(def);
        Assert.Equal("bus:4", def!.RunReachRequirement);
    }
```

> If `UpgradeDefinition`'s property is named differently than `RunReachRequirement`, open
> `src/TheLongestYear.Core/UpgradeDefinition.cs` and use the actual property name (it is the
> constructor arg `runReachRequirement`).

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~UpgradeCatalogTests"`
Expected: FAIL — actual value is `"bus:1"`.

- [ ] **Step 3: Change the catalog entry**

In `src/TheLongestYear.Core/UpgradeCatalog.cs:292`, change:

```csharp
            1500, metaRequirement: null, runReachRequirement: "bus:1"),
```
to:
```csharp
            1500, metaRequirement: null, runReachRequirement: "bus:4"),
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~UpgradeCatalogTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
# bump manifest.json Version 0.9.10 -> 0.9.11
git add src/TheLongestYear.Core/UpgradeCatalog.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs src/TheLongestYear/manifest.json
git commit -m "Upgrade: keep_bus_unlocked requires bus:4 (all four vault bundles)"
```

---

## Task 5: RunReachEvaluator — `bus` returns the paid count

**Files:**
- Modify: `src/TheLongestYear/Integration/RunReachEvaluator.cs:41`
- Test: `tests/TheLongestYear.Tests/RunReachRequirementTests.cs`

The parse/threshold logic is pure (Core). `RunReachRequirement.Parse("bus:4")` already yields
`Metric="bus", Threshold=4` via the existing 2-part numeric branch, and `IsMet(actual)` is
`actual >= 4`. Add a Core test for that, then change the glue to feed the live count.

- [ ] **Step 1: Add the failing test**

Append inside the test class in `tests/TheLongestYear.Tests/RunReachRequirementTests.cs` (before the closing brace):

```csharp
    [Theory]
    [InlineData(4, true)]
    [InlineData(3, false)]
    [InlineData(0, false)]
    public void Bus_four_is_met_only_with_four_paid(int actualCount, bool expectedMet)
    {
        RunReachRequirement? r = RunReachRequirement.Parse("bus:4");
        Assert.NotNull(r);
        Assert.Equal("bus", r!.Metric);
        Assert.Equal(4, r.Threshold);
        Assert.Equal(expectedMet, r.IsMet(actualCount));
    }
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~RunReachRequirementTests"`
Expected: FAIL only if the assertions don't hold. (If `Parse`/`IsMet` already behave this way the test PASSES immediately — that's acceptable here because the behavior we depend on is pre-existing; the test documents it. If it passes, note that and continue to Step 3.)

- [ ] **Step 3: Change the `bus` metric in RunReachEvaluator**

In `src/TheLongestYear/Integration/RunReachEvaluator.cs:41`, change:

```csharp
                "bus"      => (_runState?.Invoke()?.VaultBundlesPaid.Count ?? 0) > 0 ? 1 : 0,
```
to:
```csharp
                "bus"      => _runState?.Invoke()?.VaultBundlesPaid.Count ?? 0,   // 0–4 (deduped on insert)
```

- [ ] **Step 4: Build-check (glue change, not unit-tested) + run the Core test**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: Build succeeded, 0 errors.
Run: `dotnet test TheLongestYear.sln -c Release --filter "FullyQualifiedName~RunReachRequirementTests"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
# bump manifest.json Version 0.9.11 -> 0.9.12
git add src/TheLongestYear/Integration/RunReachEvaluator.cs tests/TheLongestYear.Tests/RunReachRequirementTests.cs src/TheLongestYear/manifest.json
git commit -m "RunReach: bus metric returns paid count (0-4) for bus:4"
```

---

## Task 6: DonationService.OnVaultBundlePaid

**Files:**
- Modify: `src/TheLongestYear/Donations/DonationService.cs`

This is thin glue over already-tested Core helpers (`TryMarkVaultBundlePaid`, `VaultPayment`,
`GoldForIndex`, `JpBoostHelper.Apply`). No new unit test (DonationService needs a `MetaStore`,
which is IModHelper-coupled — same reason `OnBundleCompleted` has no unit test). Verified by build
+ the Core tests already covering the math.

- [ ] **Step 1: Add the method**

In `src/TheLongestYear/Donations/DonationService.cs`, add after `OnBundleCompleted` (after line 94, before `OnRoomCompleted`):

```csharp

        /// <summary>The player paid a real CC Vault money bundle (index 34–37). Records it into the
        /// run ledger (idempotent) and awards JP proportional to the gold spent. Unlike a normal
        /// bundle there is NO completion bonus — the vault is a one-item (money) bundle, so it pays
        /// only the gold-scaled amount. No RunActivation check: <see cref="Active"/> is null on
        /// non-TLY saves and the reconcile caller gates, matching OnItemDonated/OnBundleCompleted.</summary>
        public void OnVaultBundlePaid(int bundleIndex)
        {
            if (!Run.TryMarkVaultBundlePaid(bundleIndex))
                return;

            long jp = JpBoostHelper.Apply(_store.State, _jp.VaultPayment(VaultRules.GoldForIndex(bundleIndex)));
            _store.State.JunimoPoints += jp;
            _monitor.Log(
                $"Vault bundle {bundleIndex} paid ({VaultRules.GoldForIndex(bundleIndex):N0}g) -> +{jp} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }
```

> `VaultRules` is in `TheLongestYear.Core`, already imported via `using TheLongestYear.Core;` at the
> top of the file. `_jp`, `_store`, `_monitor`, and `Run` are existing private members.

- [ ] **Step 2: Build-check**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
# bump manifest.json Version 0.9.12 -> 0.9.13
git add src/TheLongestYear/Donations/DonationService.cs src/TheLongestYear/manifest.json
git commit -m "DonationService: OnVaultBundlePaid (record + gold-scaled JP, no bonus)"
```

---

## Task 7: DonationObserver routes vault completions to OnVaultBundlePaid

**Files:**
- Modify: `src/TheLongestYear/Donations/DonationObserver.cs:145-152`

Glue change verified by build. Suppresses the completion bonus for vault bundles by routing them
to `OnVaultBundlePaid` instead of `OnBundleCompleted`.

- [ ] **Step 1: Change the bundle-complete diff branch**

In `src/TheLongestYear/Donations/DonationObserver.cs`, replace the block (currently lines 145–152):

```csharp
                if (_bundleCompleteSnapshot.TryGetValue(b.bundleIndex, out bool wasComplete))
                {
                    if (!wasComplete && b.complete)
                    {
                        DonationService.Active.OnBundleCompleted(b.bundleIndex);
                        _bundleCompleteSnapshot[b.bundleIndex] = true;
                    }
                }
```
with:
```csharp
                if (_bundleCompleteSnapshot.TryGetValue(b.bundleIndex, out bool wasComplete))
                {
                    if (!wasComplete && b.complete)
                    {
                        // Vault money bundles (34–37) pay gold, not items: record the payment +
                        // gold-scaled JP, NOT the standard bundle-completion bonus. Everything else
                        // takes the normal completion-bonus path.
                        if (TheLongestYear.Core.VaultRules.IsVaultIndex(b.bundleIndex))
                            DonationService.Active.OnVaultBundlePaid(b.bundleIndex);
                        else
                            DonationService.Active.OnBundleCompleted(b.bundleIndex);
                        _bundleCompleteSnapshot[b.bundleIndex] = true;
                    }
                }
```

- [ ] **Step 2: Build-check**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
# bump manifest.json Version 0.9.13 -> 0.9.14
git add src/TheLongestYear/Donations/DonationObserver.cs src/TheLongestYear/manifest.json
git commit -m "DonationObserver: route vault completions to OnVaultBundlePaid"
```

---

## Task 8: VaultPaymentSync reconcile (backstop)

**Files:**
- Create: `src/TheLongestYear/Integration/VaultPaymentSync.cs`

Reads `Game1`/`CommunityCenter`, so it's glue (no unit test, like `RunReachEvaluator`). Additive:
unions any vanilla-complete vault bundle into the ledger via the idempotent
`DonationService.OnVaultBundlePaid`. Guards against a missing bundle key (the documented
`isBundleComplete` KeyNotFound crash shape).

- [ ] **Step 1: Create the file**

Create `src/TheLongestYear/Integration/VaultPaymentSync.cs` with:

```csharp
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Donations;

namespace TheLongestYear.Integration
{
    /// <summary>
    /// Reconciles the run's vault ledger from the vanilla CC's own paid-state — the source of
    /// truth for whether a money bundle has been paid. Additive only: it unions any vanilla-complete
    /// vault bundle (34–37) into <see cref="RunState.VaultBundlesPaid"/> via the idempotent
    /// <see cref="DonationService.OnVaultBundlePaid"/>; it never removes.
    ///
    /// Backstops the live <see cref="DonationObserver"/> path for two cases the observer can't see:
    ///   - a payment made on an OLDER mod version (already complete on load, so no false→true
    ///     transition to observe) — the mid-run upgrade migration,
    ///   - any in-session payment the observer missed.
    /// Called before the day-end gate eval, the green journal, and the shrine (all need an accurate
    /// ledger). Single-player + master + TLY-active only.
    /// </summary>
    internal static class VaultPaymentSync
    {
        public static void Reconcile(RunState run)
        {
            if (run == null) return;
            if (!RunActivation.IsActive) return;
            if (!Game1.IsMasterGame || Game1.IsMultiplayer) return;

            if (Game1.getLocationFromName("CommunityCenter") is not CommunityCenter cc) return;
            var dict = Game1.netWorldState.Value?.Bundles?.FieldDict;
            if (dict == null) return;

            foreach (int idx in VaultRules.VaultIndices)
            {
                // Guard: isBundleComplete indexes bundles[idx] directly and throws
                // KeyNotFoundException if the index isn't present (see WorldResetService notes).
                if (!dict.ContainsKey(idx)) continue;
                if (cc.isBundleComplete(idx))
                    DonationService.Active?.OnVaultBundlePaid(idx);
            }
        }
    }
}
```

> Verify the `using` for `CommunityCenter`: it lives in `StardewValley.Locations`. If the build
> reports `IsMasterGame`/`IsMultiplayer` resolution issues, they are static members on `Game1`
> (PC SMAPI) — keep as written.

- [ ] **Step 2: Build-check**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```bash
# bump manifest.json Version 0.9.14 -> 0.9.15
git add src/TheLongestYear/Integration/VaultPaymentSync.cs src/TheLongestYear/manifest.json
git commit -m "VaultPaymentSync: reconcile ledger from vanilla CC (migration backstop)"
```

---

## Task 9: Wire reconcile into the three call sites

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs:482`
- Modify: `src/TheLongestYear/UI/MenuLauncher.cs:95` (OpenSeasonGoals) and `:71` (OpenShrineShop)

- [ ] **Step 1: Reconcile before the day-end gate**

In `src/TheLongestYear/Loop/RunController.cs`, in `OnDayEnding`, insert before line 482
(`bool vaultGateSatisfied = ...`):

```csharp
            TheLongestYear.Integration.VaultPaymentSync.Reconcile(Run);
```
so it reads:
```csharp
        public void OnDayEnding(object sender, DayEndingEventArgs e)
        {
            TheLongestYear.Integration.VaultPaymentSync.Reconcile(Run);
            bool vaultGateSatisfied = VaultRules.IsVaultGateSatisfied(Run.Season, Run, _store.State);
            RunAction action = _runManager.EvaluateDayEnd(Run, _requirements, vaultGateSatisfied);
```

- [ ] **Step 2: Reconcile before the journal**

In `src/TheLongestYear/UI/MenuLauncher.cs`, in `OpenSeasonGoals`, insert immediately after the
`if (!CanOpen()) return;` guard and before the `Game1.activeClickableMenu = new SeasonGoalsMenu(...)`
line (around line 95):

```csharp
            TheLongestYear.Integration.VaultPaymentSync.Reconcile(_store.Run);
```

- [ ] **Step 3: Reconcile before the shrine**

In `src/TheLongestYear/UI/MenuLauncher.cs`, in `OpenShrineShop`, insert immediately after the
`if (!CanOpen()) return;` guard and before `Game1.activeClickableMenu = new JunimoShrineMenu(...)`
(around line 71):

```csharp
            TheLongestYear.Integration.VaultPaymentSync.Reconcile(_store.Run);
```

> `MenuLauncher` exposes the run via `_store.Run` (see the existing `new SeasonGoalsMenu(... _store.Run ...)`).
> If `_store` is named differently in that class, use the field the existing menu construction uses.

- [ ] **Step 4: Build-check**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
# bump manifest.json Version 0.9.15 -> 0.9.16
git add src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/UI/MenuLauncher.cs src/TheLongestYear/manifest.json
git commit -m "Wire VaultPaymentSync into day-end gate, journal, and shrine"
```

---

## Task 10: SeasonGoalsMenu — count-based vault line

**Files:**
- Modify: `src/TheLongestYear/UI/SeasonGoalsMenu.cs`

Replaces the 0.9.7 `DescribeGate`/`GoldCostForSeason` banner (now-removed helpers) with the
count-vs-ordinal display. Glue/UI verified by build.

- [ ] **Step 1: Rewrite `DrawVaultLine`**

In `src/TheLongestYear/UI/SeasonGoalsMenu.cs`, replace the entire `DrawVaultLine` method (the one
added in 0.9.7, which calls `VaultRules.DescribeGate` / `GoldCostForSeason`) with:

```csharp
        /// <summary>Draws the pinned Vault payment banner just below the title bar. The vault
        /// requirement is ANDed into the season gate (<see cref="VaultRules.IsVaultGateSatisfied"/>)
        /// but isn't an item bundle, so it would otherwise be invisible. The gate is count-based:
        /// the player must have paid at least the season ordinal's worth of vault bundles this run
        /// (any tiers). This line shows that per-season sufficiency only — the keep_bus_unlocked
        /// upgrade is a shrine concern, not shown here (when owned the count is already full, so it
        /// simply reads as met).</summary>
        private void DrawVaultLine(SpriteBatch b)
        {
            int paid = VaultRules.PaidCount(_run);
            int need = VaultRules.SeasonOrdinal(_season);
            bool met = VaultRules.IsVaultGateSatisfied(_season, _run, _meta);

            int boxX = xPositionOnScreen + PanelPadding;
            int boxY = yPositionOnScreen + TitleBarHeight;
            int boxW = width - PanelPadding * 2;
            int boxH = VaultLineHeight - 8;

            Color tint = met ? Color.LightGreen * 0.7f : Color.White;
            IClickableMenu.drawTextureBox(b, Game1.menuTexture, new Rectangle(0, 256, 60, 60),
                boxX, boxY, boxW, boxH, tint, 1f, false);

            // Left label: per-season payment progress (paid vs. this season's required count).
            string label = met
                ? $"Vault (bus repair):  {paid} of {need} paid"
                : $"Vault (bus repair):  {paid} of {need} paid — pay any Vault bundle at the CC";
            Color labelColor = met ? Color.DarkGreen : Game1.textColor;
            float textY = boxY + (boxH - Game1.smallFont.MeasureString(label).Y) / 2f;
            Utility.drawTextWithShadow(b, label, Game1.smallFont,
                new Vector2(boxX + 16, textY), labelColor);

            // Right badge: MET / NOT MET for this season's checkpoint.
            string badge = met ? "MET" : "NOT MET";
            Vector2 badgeSize = Game1.smallFont.MeasureString(badge);
            Color badgeColor = met ? Color.DarkGreen : new Color(160, 34, 34);
            Utility.drawTextWithShadow(b, badge, Game1.smallFont,
                new Vector2(boxX + boxW - 16 - badgeSize.X, textY), badgeColor);
        }
```

> Keep the existing constants (`VaultLineHeight`, `PanelPadding`, `TitleBarHeight`), the
> `DrawVaultLine(b)` call inside `draw`, and the `listY`/`listHeight` offsets added in 0.9.7 — only
> the method body changes. `_run`, `_meta`, `_season` are existing fields.

- [ ] **Step 2: Build-check**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: Build succeeded, 0 errors. (If it reports `VaultRules.DescribeGate`/`GoldCostForSeason`
no longer exist anywhere else, good — those were only used here; this task removes the last refs.)

- [ ] **Step 3: Commit**

```bash
# bump manifest.json Version 0.9.16 -> 0.9.17
git add src/TheLongestYear/UI/SeasonGoalsMenu.cs src/TheLongestYear/manifest.json
git commit -m "Journal: count-based vault line (paid of needed, MET/NOT MET)"
```

---

## Task 11: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Full test suite**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: PASS — all tests green (≈ 449 baseline + the new tests, 0 failures).

- [ ] **Step 2: Full release build**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false`
Expected: Build succeeded, 0 Warning(s), 0 Error(s).

- [ ] **Step 3: Grep for dangling references to removed helpers**

Run: `rg "DescribeGate|GoldCostForSeason|VaultGateStatus" src tests`
Expected: no matches (all removed). If any remain, fix and re-run Steps 1–2.

- [ ] **Step 4: Manual playtest checklist (deploy → user tests → pull logs)**

This is the meaningful playtest. Deploy, then have the user:
1. New TLY run, open the green journal (default hotkey) → vault line reads **"0 of 1 paid — pay any Vault bundle at the CC", NOT MET**.
2. Pay the 2,500g Vault bundle at the CC → JP rises by 3; reopen journal → **"1 of 1 paid", MET**.
3. Reach day 28 with all shown bundles done + ≥1 vault paid → season advances (no FailReset).
4. (If feasible) pay all four vault bundles in one run → by Winter the shrine offers
   **Keep Bus Unlocked** (bus:4 met).

Confirm via the pulled SMAPI log: lines `Vault bundle 34 paid (2,500g) -> +3 JP`, and at day-end
no unexpected `FailReset` when the vault is satisfied.

---

## Notes for the executor

- **Run the game?** No relaunch is needed for the unit/build steps. Only Task 11 Step 4 needs a
  deploy + the user's hands-on test. The DLL copy makes deployed == tip.
- **If the game is running** during development, the deploy build locks the DLL — always use
  `-p:EnableModDeploy=false` for compile-checks (as every build step above already does).
- **Don't** touch the `feat/tly1-story-cutscenes` branch; all work is on `master`.
