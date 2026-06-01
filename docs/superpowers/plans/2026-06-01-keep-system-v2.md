# Keep System v2 — In-Run Reach Gating Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Gate every Junimo Shrine "keep" upgrade on what the player actually reached during the current run, so the shop (and the read-only preview) only ever offer the lowest un-owned tier of a chain whose reach the player has earned.

**Architecture:** A new optional `RunReachRequirement` string on `UpgradeDefinition` (parsed + compared in pure Core, evaluated against live `Game1` state by a thin glue `RunReachEvaluator`). A pure `KeepShopFilter` predicate decides visibility from `(MetaState owned, prerequisite, MetaRequirement, reach)`. Both menus call the same filter. New keeps: Bamboo-rod chain root, Mastery 1–5, Golden Scythe; the latter two are permanent floors applied in `RunBaselineBuilder`/`FarmerReset`.

**Tech Stack:** C# / .NET 6, SMAPI + Harmony, xUnit. Spec: `docs/superpowers/specs/2026-06-01-keep-system-v2-design.md`.

**Build/test conventions (this repo):**
- Tests: `dotnet test TheLongestYear.sln -c Release` (test project references ONLY `TheLongestYear.Core`).
- SMAPI is running during dev → compile-only with `dotnet build src\TheLongestYear\TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`. For a deploy build (SMAPI closed): `dotnet build src\TheLongestYear\TheLongestYear.csproj -c Release`.
- Commit locally only (never push). Co-Authored-By footer on every commit.

---

## File Structure

**Core (pure, unit-tested):**
- Create `src/TheLongestYear.Core/RunReachRequirement.cs` — parse + threshold compare.
- Modify `src/TheLongestYear.Core/UpgradeDefinition.cs` — add `RunReachRequirement` property + ctor param.
- Modify `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs` — stamp reach on tools/skills/mine; restructure rod chain (add Bamboo); add mastery generator.
- Modify `src/TheLongestYear.Core/UpgradeCatalog.cs` — backpack reach strings; add Golden Scythe; append mastery generator.
- Create `src/TheLongestYear.Core/KeepShopFilter.cs` — pure visibility predicate.
- Modify `src/TheLongestYear.Core/RunBaseline.cs` — add `MasteryLevel`, `GrantGoldenScythe`.
- Modify `src/TheLongestYear.Core/RunBaselineBuilder.cs` — rod explicit mapping incl. Bamboo; mastery + golden scythe floors.

**Glue (log-verified):**
- Create `src/TheLongestYear/Integration/RunReachEvaluator.cs` — live `Game1` reads.
- Modify `src/TheLongestYear/UI/JunimoShrineMenu.cs` — reach filter via `KeepShopFilter`.
- Modify `src/TheLongestYear/UI/ShrinePreviewMenu.cs` — reach filter + title/layout fix + scroll.
- Modify `src/TheLongestYear/Loop/FarmerReset.cs` — Golden Scythe grant swap + mastery restore.

**Tests:**
- Modify `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs` — reach-string coverage.
- Create `tests/TheLongestYear.Tests/RunReachRequirementTests.cs`.
- Create `tests/TheLongestYear.Tests/KeepShopFilterTests.cs` — incl. the watering-can worked example.
- Modify `tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs` — rod/mastery/golden-scythe.

---

## Task 1: `RunReachRequirement` value type (Core)

**Files:**
- Create: `src/TheLongestYear.Core/RunReachRequirement.cs`
- Test: `tests/TheLongestYear.Tests/RunReachRequirementTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunReachRequirementTests
{
    [Theory]
    [InlineData("tool:watering_can:2", "tool", "watering_can", 2)]
    [InlineData("skill:fishing:5", "skill", "fishing", 5)]
    [InlineData("rod:3", "rod", null, 3)]
    [InlineData("backpack:1", "backpack", null, 1)]
    [InlineData("mine:60", "mine", null, 60)]
    [InlineData("mastery:4", "mastery", null, 4)]
    [InlineData("scythe:golden", "scythe", "golden", 1)]
    public void Parse_extracts_metric_key_threshold(string raw, string metric, string? key, int threshold)
    {
        RunReachRequirement? r = RunReachRequirement.Parse(raw);
        Assert.NotNull(r);
        Assert.Equal(metric, r!.Metric);
        Assert.Equal(key, r.Key);
        Assert.Equal(threshold, r.Threshold);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("tool::2")]
    [InlineData("mine:notanumber")]
    public void Parse_returns_null_for_empty_or_malformed(string? raw)
        => Assert.Null(RunReachRequirement.Parse(raw));

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, false)]
    public void IsMet_is_actual_geq_threshold(int actual, int threshold, bool expected)
    {
        var r = RunReachRequirement.Parse($"mine:{threshold}")!;
        Assert.Equal(expected, r.IsMet(actual));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test TheLongestYear.sln -c Release --filter RunReachRequirementTests`
Expected: FAIL (type `RunReachRequirement` does not exist).

- [ ] **Step 3: Write minimal implementation**

```csharp
namespace TheLongestYear.Core;

/// <summary>
/// A parsed "what did the player reach THIS run" gate on a keep upgrade. Distinct from
/// <see cref="UpgradeDefinition.MetaRequirement"/> (which checks cross-run accumulators):
/// this compares a live in-run value against a threshold. Format "metric[:key]:threshold",
/// or the flag form "scythe:golden". Parsing + comparison live here (pure, testable); the
/// live value per metric is supplied by the glue RunReachEvaluator.
/// </summary>
public sealed class RunReachRequirement
{
    public string Metric { get; }
    public string? Key { get; }
    public int Threshold { get; }

    private RunReachRequirement(string metric, string? key, int threshold)
    {
        Metric = metric;
        Key = key;
        Threshold = threshold;
    }

    /// <summary>actual reach value ≥ the required threshold.</summary>
    public bool IsMet(int actualReach) => actualReach >= Threshold;

    /// <summary>Parse a requirement string, or null if empty/malformed.</summary>
    public static RunReachRequirement? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        string[] parts = raw.Split(':');
        // Flag form: scythe:golden (treated as threshold 1; the evaluator supplies 0/1).
        if (parts.Length == 2 && parts[0] == "scythe" && parts[1] == "golden")
            return new RunReachRequirement("scythe", "golden", 1);
        // 2-part numeric: metric:threshold (rod / backpack / mine / mastery).
        if (parts.Length == 2 && parts[0].Length > 0 && int.TryParse(parts[1], out int t2))
            return new RunReachRequirement(parts[0], null, t2);
        // 3-part numeric: metric:key:threshold (tool / skill).
        if (parts.Length == 3 && parts[0].Length > 0 && parts[1].Length > 0
            && int.TryParse(parts[2], out int t3))
            return new RunReachRequirement(parts[0], parts[1], t3);
        return null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test TheLongestYear.sln -c Release --filter RunReachRequirementTests`
Expected: PASS (all cases).

- [ ] **Step 5: Commit**

```
git add src/TheLongestYear.Core/RunReachRequirement.cs tests/TheLongestYear.Tests/RunReachRequirementTests.cs
git commit -m "feat(spec-a): RunReachRequirement parser + threshold compare (Core)"
```

---

## Task 2: Add `RunReachRequirement` field to `UpgradeDefinition`

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeDefinition.cs`

- [ ] **Step 1: Add the property + ctor param (no test — exercised via catalog tests in later tasks)**

In `UpgradeDefinition.cs`, add a property after `MetaRequirement`:

```csharp
    /// <summary>Live in-run reach gate (e.g. "tool:watering_can:2"), or null if the upgrade is
    /// not reach-gated. Parsed/evaluated separately (see RunReachRequirement + RunReachEvaluator).</summary>
    public string? RunReachRequirement { get; }
```

Add a trailing optional ctor parameter (keeps all existing call sites valid):

```csharp
    public UpgradeDefinition(
        string id,
        UpgradeCategory category,
        string displayName,
        string description,
        long cost,
        string? prerequisiteId = null,
        string? metaRequirement = null,
        string? runReachRequirement = null)
    {
        // ...existing validation unchanged...
        Id = id;
        Category = category;
        DisplayName = displayName;
        Description = description ?? "";
        Cost = cost;
        PrerequisiteId = prerequisiteId;
        MetaRequirement = metaRequirement;
        RunReachRequirement = runReachRequirement;
    }
```

- [ ] **Step 2: Build Core to verify it compiles**

Run: `dotnet build src/TheLongestYear.Core/TheLongestYear.Core.csproj -c Release`
Expected: Build succeeded, 0 errors.

- [ ] **Step 3: Commit**

```
git add src/TheLongestYear.Core/UpgradeDefinition.cs
git commit -m "feat(spec-a): add optional RunReachRequirement to UpgradeDefinition"
```

---

## Task 3: Stamp reach strings on generated tool / skill / mine keeps

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`
- Test: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

- [ ] **Step 1: Write the failing test (append to `UpgradeCatalogTests`)**

```csharp
    [Fact]
    public void Tool_keeps_carry_a_tool_reach_requirement()
    {
        foreach (UpgradeDefinition u in UpgradeCatalog.All
                     .Where(u => u.Id.StartsWith("keep_") &&
                            (u.Id.StartsWith("keep_hoe_") || u.Id.StartsWith("keep_pickaxe_") ||
                             u.Id.StartsWith("keep_axe_") || u.Id.StartsWith("keep_watering_can_"))))
        {
            Assert.NotNull(u.RunReachRequirement);
            Assert.StartsWith("tool:", u.RunReachRequirement!);
        }
    }

    [Fact]
    public void Skill_level_keeps_carry_a_skill_reach_requirement()
    {
        var skillKeeps = UpgradeCatalog.All.Where(u => u.Id.Contains("_level_") && u.Id.StartsWith("keep_")).ToList();
        Assert.NotEmpty(skillKeeps);
        foreach (UpgradeDefinition u in skillKeeps)
            Assert.StartsWith("skill:", u.RunReachRequirement ?? "");
    }

    [Fact]
    public void Mine_elevator_keeps_carry_a_mine_reach_requirement()
    {
        foreach (UpgradeDefinition u in UpgradeCatalog.All.Where(u => u.Id.StartsWith("keep_mine_elevator_")))
            Assert.Equal($"mine:{u.Id.Substring("keep_mine_elevator_".Length)}", u.RunReachRequirement);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test TheLongestYear.sln -c Release --filter UpgradeCatalogTests`
Expected: FAIL (RunReachRequirement is null on those keeps).

- [ ] **Step 3: Add reach strings in the generators**

In `LoadoutToolKeeps()`, change the tool-keep yield to pass the reach string:

```csharp
        foreach (var (slug, displayName) in ToolKinds)
            for (int tier = 1; tier <= 4; tier++)
            {
                string id = $"keep_{slug}_{tier}";
                string? prereq = tier == 1 ? null : $"keep_{slug}_{tier - 1}";
                string name = $"Keep {TierNames[tier - 1]} {displayName}";
                string desc = $"Start each run with your {displayName} at the {TierNames[tier - 1]} tier " +
                              "or whatever lower tier you actually reached this run, whichever is lower.";
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Loadout, name, desc, ToolTierCosts[tier - 1], prereq,
                    metaRequirement: null, runReachRequirement: $"tool:{slug}:{tier}");
            }
```

In `CarryoverSkillLevelKeeps()`, add the reach string to the yield:

```csharp
                yield return new UpgradeDefinition(
                    id, UpgradeCategory.Carryover, name, desc, SkillLevelCosts[level], prereq,
                    metaRequirement: null, runReachRequirement: $"skill:{slug}:{level}");
```

In `CarryoverMineElevatorKeeps()`, add the reach string to the yield:

```csharp
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Carryover,
                $"Keep Mine Elevator Floor {floor}",
                $"Start each run with the mine elevator accessible to floor {floor} (or your " +
                "in-run deepest floor, whichever is shallower).",
                cost, prereq, metaRequirement: null, runReachRequirement: $"mine:{floor}");
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test TheLongestYear.sln -c Release --filter UpgradeCatalogTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```
git add src/TheLongestYear.Core/UpgradeCatalogGenerators.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs
git commit -m "feat(spec-a): stamp reach requirements on tool/skill/mine keeps"
```

---

## Task 4: Restructure the fishing-rod chain (add Bamboo root)

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs`
- Test: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

- [ ] **Step 1: Write the failing test (append to `UpgradeCatalogTests`)**

```csharp
    [Fact]
    public void Fishing_rod_chain_starts_with_bamboo_and_is_reach_gated()
    {
        UpgradeDefinition bamboo = UpgradeCatalog.TryGet("keep_fishing_rod_0")!;
        Assert.NotNull(bamboo);
        Assert.Equal("Keep Bamboo Pole", bamboo.DisplayName);
        Assert.Equal(25, bamboo.Cost);
        Assert.Null(bamboo.PrerequisiteId);
        Assert.Equal("rod:1", bamboo.RunReachRequirement);

        UpgradeDefinition fiberglass = UpgradeCatalog.TryGet("keep_fishing_rod_1")!;
        Assert.Equal("keep_fishing_rod_0", fiberglass.PrerequisiteId);
        Assert.Equal("rod:2", fiberglass.RunReachRequirement);

        UpgradeDefinition iridium = UpgradeCatalog.TryGet("keep_fishing_rod_2")!;
        Assert.Equal("keep_fishing_rod_1", iridium.PrerequisiteId);
        Assert.Equal("rod:3", iridium.RunReachRequirement);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test TheLongestYear.sln -c Release --filter Fishing_rod_chain_starts_with_bamboo`
Expected: FAIL (keep_fishing_rod_0 not found).

- [ ] **Step 3: Replace the `FishingRodTiers` table and its emitter**

Replace the existing `FishingRodTiers` array (the one whose comment says "no value in a keep bamboo entry") with:

```csharp
    // Fishing rod chain. 2026-06-01 (Spec A): added the Bamboo Pole root so the rod
    // chain mirrors the tool chains; reach gating (rod:N where 1=bamboo, 2=fiberglass,
    // 3=iridium = FishingRod.UpgradeLevel) keeps un-earned tiers out of the shop.
    private static readonly (string Id, string DisplayName, long Cost, string? Prereq, string Reach)[] FishingRodTiers =
    {
        ("keep_fishing_rod_0", "Keep Bamboo Pole",     25,  null,                 "rod:1"),
        ("keep_fishing_rod_1", "Keep Fiberglass Rod",  150, "keep_fishing_rod_0", "rod:2"),
        ("keep_fishing_rod_2", "Keep Iridium Rod",     425, "keep_fishing_rod_1", "rod:3"),
    };
```

Replace the rod emit loop in `LoadoutToolKeeps()` with:

```csharp
        foreach (var (id, name, cost, prereq, reach) in FishingRodTiers)
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Loadout, name,
                "Start each run with your Fishing Rod at this tier (capped at your in-run reach).",
                cost, prereq, metaRequirement: null, runReachRequirement: reach);
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test TheLongestYear.sln -c Release --filter UpgradeCatalogTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```
git add src/TheLongestYear.Core/UpgradeCatalogGenerators.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs
git commit -m "feat(spec-a): add Keep Bamboo Pole root to the fishing-rod chain"
```

---

## Task 5: Backpack reach + Golden Scythe + Mastery chain

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalogGenerators.cs` (mastery generator)
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs` (backpack reach, golden scythe, append mastery)
- Test: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

- [ ] **Step 1: Write the failing test (append to `UpgradeCatalogTests`)**

```csharp
    [Fact]
    public void Backpack_keeps_are_reach_gated()
    {
        Assert.Equal("backpack:1", UpgradeCatalog.TryGet("backpack_1")!.RunReachRequirement);
        Assert.Equal("backpack:2", UpgradeCatalog.TryGet("backpack_2")!.RunReachRequirement);
    }

    [Fact]
    public void Golden_scythe_keep_exists_and_is_reach_gated()
    {
        UpgradeDefinition gs = UpgradeCatalog.TryGet("keep_golden_scythe")!;
        Assert.NotNull(gs);
        Assert.Equal(UpgradeCategory.Loadout, gs.Category);
        Assert.Null(gs.PrerequisiteId);
        Assert.Equal("scythe:golden", gs.RunReachRequirement);
    }

    [Fact]
    public void Mastery_chain_has_five_reach_gated_tiers()
    {
        for (int n = 1; n <= 5; n++)
        {
            UpgradeDefinition m = UpgradeCatalog.TryGet($"keep_mastery_{n}")!;
            Assert.NotNull(m);
            Assert.Equal(UpgradeCategory.Carryover, m.Category);
            Assert.Equal($"mastery:{n}", m.RunReachRequirement);
            Assert.Equal(n == 1 ? null : $"keep_mastery_{n - 1}", m.PrerequisiteId);
        }
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test TheLongestYear.sln -c Release --filter UpgradeCatalogTests`
Expected: FAIL (backpack reach null; keep_golden_scythe + keep_mastery_* missing).

- [ ] **Step 3a: Add backpack reach strings in `UpgradeCatalog.Build()`**

Replace the two backpack entries with:

```csharp
        new UpgradeDefinition("backpack_1", UpgradeCategory.Loadout, "Backpack I",
            "Start each run with the 24-slot backpack.", 150,
            metaRequirement: null, runReachRequirement: "backpack:1"),
        new UpgradeDefinition("backpack_2", UpgradeCategory.Loadout, "Backpack II",
            "Start each run with the 36-slot backpack.", 375, "backpack_1",
            metaRequirement: null, runReachRequirement: "backpack:2"),
```

- [ ] **Step 3b: Add the Golden Scythe entry in `UpgradeCatalog.Build()`**

Immediately after the `backpack_2` entry, add:

```csharp
        // Golden Scythe — a convenience keep. Reach-gated on having obtained the Golden
        // Scythe this run (mail "gotGoldenScythe"); once owned it is a permanent floor —
        // FarmerReset grants the Golden Scythe instead of the basic scythe every loop.
        new UpgradeDefinition("keep_golden_scythe", UpgradeCategory.Loadout, "Keep Golden Scythe",
            "Start each run with the Golden Scythe instead of the basic scythe.", 250,
            metaRequirement: null, runReachRequirement: "scythe:golden"),
```

- [ ] **Step 3c: Add the mastery generator in `UpgradeCatalogGenerators.cs`**

```csharp
    // Mastery keep costs, indexed [1..5]. End-game progression (post all-skills-10), so a
    // steep ramp. Owning a tier is a PERMANENT floor (not in-run-peak capped like skill
    // keeps) — mastery is hard-won and persists across loops once kept.
    private static readonly long[] MasteryCosts = { 0, 1000, 1500, 2000, 2750, 3500 };

    /// <summary>Yield the 5 Carryover Keep-Mastery tiers.</summary>
    public static IEnumerable<UpgradeDefinition> CarryoverMasteryKeeps()
    {
        for (int level = 1; level <= 5; level++)
        {
            string id = $"keep_mastery_{level}";
            string? prereq = level == 1 ? null : $"keep_mastery_{level - 1}";
            yield return new UpgradeDefinition(
                id, UpgradeCategory.Carryover, $"Keep Mastery {level}",
                $"Start each run at Mastery Level {level}. Persists across loops once kept.",
                MasteryCosts[level], prereq, metaRequirement: null, runReachRequirement: $"mastery:{level}");
        }
    }
```

- [ ] **Step 3d: Append the mastery generator in `UpgradeCatalog.Build()`**

After `entries.AddRange(UpgradeCatalogGenerators.CarryoverMineElevatorKeeps());` add:

```csharp
        entries.AddRange(UpgradeCatalogGenerators.CarryoverMasteryKeeps());
```

- [ ] **Step 4: Run to verify pass (full catalog suite)**

Run: `dotnet test TheLongestYear.sln -c Release --filter UpgradeCatalogTests`
Expected: PASS (incl. existing `Every_prerequisite_points_to_a_real_upgrade_id`, which now covers the new chains).

- [ ] **Step 5: Commit**

```
git add src/TheLongestYear.Core/UpgradeCatalog.cs src/TheLongestYear.Core/UpgradeCatalogGenerators.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs
git commit -m "feat(spec-a): backpack reach + Keep Golden Scythe + Keep Mastery 1-5"
```

---

## Task 6: `KeepShopFilter` pure visibility predicate (Core)

**Files:**
- Create: `src/TheLongestYear.Core/KeepShopFilter.cs`
- Test: `tests/TheLongestYear.Tests/KeepShopFilterTests.cs`

- [ ] **Step 1: Write the failing test (encodes the watering-can worked example)**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class KeepShopFilterTests
{
    // Build a reachMet predicate from a dict of metric:key -> reached value.
    private static Func<string?, bool> Reach(Dictionary<string, int> reached) => req =>
    {
        RunReachRequirement? r = RunReachRequirement.Parse(req);
        if (r == null) return true;                       // no requirement -> always met
        string key = r.Key == null ? r.Metric : $"{r.Metric}:{r.Key}";
        return r.IsMet(reached.TryGetValue(key, out int v) ? v : 0);
    };

    [Fact]
    public void Watering_can_chain_reveals_one_tier_at_a_time_gated_by_reach()
    {
        // This run reached a Steel watering can (tier 2) and a Copper hoe (tier 1).
        var reached = new Dictionary<string, int>
        {
            ["tool:watering_can"] = 2,
            ["tool:hoe"] = 1,
        };
        var meta = new MetaState();   // owns nothing yet

        List<string> Buyable() => KeepShopFilter
            .BuyableInCategory(UpgradeCategory.Loadout, meta, Reach(reached))
            .Select(d => d.Id).ToList();

        // Lowest un-owned tier of each chain, gated by reach: Copper can + Copper hoe show.
        Assert.Contains("keep_watering_can_1", Buyable());
        Assert.Contains("keep_hoe_1", Buyable());
        // Steel can is chain-locked behind Copper can; not shown yet.
        Assert.DoesNotContain("keep_watering_can_2", Buyable());

        // Buy Copper can -> Steel can appears (reached 2 >= 2).
        meta.OwnedUpgrades.Add("keep_watering_can_1");
        Assert.Contains("keep_watering_can_2", Buyable());
        Assert.DoesNotContain("keep_watering_can_1", Buyable());   // owned -> not buyable

        // Buy Steel can -> Gold can does NOT show (reached 2 < 3).
        meta.OwnedUpgrades.Add("keep_watering_can_2");
        Assert.DoesNotContain("keep_watering_can_3", Buyable());

        // A later run reaches a Gold can -> Gold keep appears.
        reached["tool:watering_can"] = 3;
        Assert.Contains("keep_watering_can_3", Buyable());
    }

    [Fact]
    public void Non_reach_upgrades_are_unaffected_by_reach()
    {
        var meta = new MetaState();
        var buyable = KeepShopFilter
            .BuyableInCategory(UpgradeCategory.Efficiency, meta, _ => false)   // reach always false
            .Select(d => d.Id).ToList();
        // early_horse has no RunReachRequirement, so a false reach predicate must not hide it.
        Assert.Contains("early_horse", buyable);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test TheLongestYear.sln -c Release --filter KeepShopFilterTests`
Expected: FAIL (type `KeepShopFilter` does not exist).

- [ ] **Step 3: Implement the pure filter**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Pure visibility rules for the Junimo Shrine keep shop, shared by the purchase menu and the
/// read-only preview. A keep is BUYABLE when it is not owned, its chain prerequisite is owned,
/// its cross-run MetaRequirement is met, AND its in-run RunReachRequirement is met. The reach
/// check is injected as a delegate so this stays pure/testable (the glue passes a live evaluator).
/// </summary>
public static class KeepShopFilter
{
    /// <summary>True if <paramref name="def"/> should be offered for purchase right now.</summary>
    public static bool IsBuyable(UpgradeDefinition def, MetaState state, Func<string?, bool> reachMet)
    {
        if (state.HasUpgrade(def.Id))
            return false;
        if (def.PrerequisiteId != null && !state.HasUpgrade(def.PrerequisiteId))
            return false;
        if (!state.MeetsMetaRequirement(def.MetaRequirement))
            return false;
        if (!reachMet(def.RunReachRequirement))
            return false;
        return true;
    }

    /// <summary>The buyable keeps in a category, catalog order preserved.</summary>
    public static IReadOnlyList<UpgradeDefinition> BuyableInCategory(
        UpgradeCategory category, MetaState state, Func<string?, bool> reachMet)
    {
        var visible = new List<UpgradeDefinition>();
        foreach (UpgradeDefinition def in UpgradeCatalog.ByCategory(category))
            if (IsBuyable(def, state, reachMet))
                visible.Add(def);
        return visible;
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test TheLongestYear.sln -c Release --filter KeepShopFilterTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```
git add src/TheLongestYear.Core/KeepShopFilter.cs tests/TheLongestYear.Tests/KeepShopFilterTests.cs
git commit -m "feat(spec-a): KeepShopFilter pure visibility predicate + watering-can test"
```

---

## Task 7: `RunReachEvaluator` (glue, live `Game1` reads)

**Files:**
- Create: `src/TheLongestYear/Integration/RunReachEvaluator.cs`

This is glue (reads `Game1`), so it is not unit-tested; it is verified in-game via the log in Task 12.

- [ ] **Step 1: Implement the evaluator**

```csharp
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>Evaluates a keep's <see cref="UpgradeDefinition.RunReachRequirement"/> against the
    /// player's LIVE in-run state (read at the moment a shrine menu is built). Reads Game1, so it
    /// lives in the glue layer; the parse + threshold compare are in Core (RunReachRequirement).</summary>
    internal static class RunReachEvaluator
    {
        /// <summary>Null/empty requirement ⇒ always met (non-reach upgrades). Unknown metric ⇒ false.</summary>
        public static bool Meets(string? requirement)
        {
            RunReachRequirement? r = RunReachRequirement.Parse(requirement);
            if (r == null)
                return string.IsNullOrWhiteSpace(requirement);   // no requirement = met; malformed = not
            Farmer p = Game1.player;
            if (p == null) return false;

            int actual = r.Metric switch
            {
                "tool"     => ToolLevel(p, r.Key),
                "rod"      => RodLevel(p),
                "backpack" => BackpackTier(p),
                "skill"    => SkillLevel(p, r.Key),
                "mine"     => p.deepestMineLevel,
                "mastery"  => MasteryTrackerMenu.getCurrentMasteryLevel(),
                "scythe"   => p.mailReceived.Contains("gotGoldenScythe") ? 1 : 0,
                _          => -1,   // unknown metric fails closed
            };
            return actual >= 0 && r.IsMet(actual);
        }

        private static int ToolLevel(Farmer p, string? kind)
        {
            foreach (Item it in p.Items)
            {
                switch (kind)
                {
                    case "hoe"          when it is Hoe h:          return h.UpgradeLevel;
                    case "pickaxe"      when it is Pickaxe pk:     return pk.UpgradeLevel;
                    case "axe"          when it is Axe a:          return a.UpgradeLevel;
                    case "watering_can" when it is WateringCan w:  return w.UpgradeLevel;
                }
            }
            return 0;
        }

        private static int RodLevel(Farmer p)
        {
            foreach (Item it in p.Items)
                if (it is FishingRod rod)
                    return rod.UpgradeLevel;   // 1 bamboo, 2 fiberglass, 3 iridium
            return 0;
        }

        private static int BackpackTier(Farmer p) => p.MaxItems switch
        {
            >= 36 => 2,
            >= 24 => 1,
            _     => 0,
        };

        private static int SkillLevel(Farmer p, string? name) => name switch
        {
            "farming"  => p.farmingLevel.Value,
            "mining"   => p.miningLevel.Value,
            "foraging" => p.foragingLevel.Value,
            "fishing"  => p.fishingLevel.Value,
            "combat"   => p.combatLevel.Value,
            _          => 0,
        };
    }
}
```

- [ ] **Step 2: Build the mod project (compile-only, SMAPI running)**

Run: `dotnet build src\TheLongestYear\TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```
git add src/TheLongestYear/Integration/RunReachEvaluator.cs
git commit -m "feat(spec-a): RunReachEvaluator — live in-run reach reads (glue)"
```

---

## Task 8: Wire the reach filter into `JunimoShrineMenu`

**Files:**
- Modify: `src/TheLongestYear/UI/JunimoShrineMenu.cs:253-268` (`VisibleCatalogForActiveCategory`)

- [ ] **Step 1: Replace the body of `VisibleCatalogForActiveCategory` with the shared filter**

```csharp
        private IReadOnlyList<UpgradeDefinition> VisibleCatalogForActiveCategory()
            => KeepShopFilter.BuyableInCategory(_activeCategory, _store.State, RunReachEvaluator.Meets);
```

Add `using TheLongestYear.Integration;` to the file's usings if not present (and ensure `TheLongestYear.Core` is imported — it already is for `UpgradeDefinition`).

- [ ] **Step 2: Build (compile-only)**

Run: `dotnet build src\TheLongestYear\TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 3: Commit**

```
git add src/TheLongestYear/UI/JunimoShrineMenu.cs
git commit -m "feat(spec-a): gate the purchase shrine on in-run reach"
```

---

## Task 9: Re-derive `ShrinePreviewMenu` + fix title/layout

**Files:**
- Modify: `src/TheLongestYear/UI/ShrinePreviewMenu.cs`

- [ ] **Step 1: Rebuild the line list through the shared filter + show owned**

Replace the category loop in the constructor (the `foreach (UpgradeCategory cat ...)` block, lines ~21-39) with:

```csharp
            foreach (UpgradeCategory cat in System.Enum.GetValues(typeof(UpgradeCategory)))
            {
                var owned = new List<string>();
                foreach (UpgradeDefinition def in UpgradeCatalog.ByCategory(cat))
                    if (state.HasUpgrade(def.Id))
                        owned.Add($"   [x] {def.DisplayName}");

                var buyable = new List<string>();
                foreach (UpgradeDefinition def in
                         KeepShopFilter.BuyableInCategory(cat, state, TheLongestYear.Integration.RunReachEvaluator.Meets))
                    buyable.Add($"   [ ] {def.DisplayName}  ({def.Cost} JP)");

                if (owned.Count == 0 && buyable.Count == 0)
                    continue;
                _lines.Add($"{cat}:");
                _lines.AddRange(owned);
                _lines.AddRange(buyable);
                _lines.Add("");
            }
```

- [ ] **Step 2: Fix the title rendering above the box, and make the box taller for the now-longer list**

In the constructor's `: base(...)` call, grow the box and raise the top so the title sits inside it. Replace the base call with:

```csharp
            : base(Game1.uiViewport.Width / 2 - 420, Game1.uiViewport.Height / 2 - 320, 840, 640, showUpperRightCloseButton: true)
```

In `draw`, move the title down so it is inside the dialogue box, and start the list below it:

```csharp
            Game1.drawDialogueBox(xPositionOnScreen, yPositionOnScreen, width, height, speaker: false, drawOnlyBox: true);
            Utility.drawTextWithShadow(b, "Junimo Shrine - Planning", Game1.dialogueFont,
                new Vector2(xPositionOnScreen + 80, yPositionOnScreen + 96), Game1.textColor);

            int y = yPositionOnScreen + 150;
            foreach (string line in _lines)
            {
                Utility.drawTextWithShadow(b, line, Game1.smallFont,
                    new Vector2(xPositionOnScreen + 80, y), Game1.textColor);
                y += 28;
                if (y > yPositionOnScreen + height - 90)
                {
                    Utility.drawTextWithShadow(b, "   …more (see the reset shrine)", Game1.smallFont,
                        new Vector2(xPositionOnScreen + 80, y), Game1.textColor);
                    break;   // planning glance; full list + purchasing is the loop-boundary shrine
                }
            }
```

(The "…more" line makes the existing clamp explicit rather than silently truncating — the list is longer now that reach-gated tiers surface.)

- [ ] **Step 3: Build (compile-only)**

Run: `dotnet build src\TheLongestYear\TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

- [ ] **Step 4: Commit**

```
git add src/TheLongestYear/UI/ShrinePreviewMenu.cs
git commit -m "fix(spec-a): preview filters through reach gate; title inside box; explicit clamp"
```

---

## Task 10: `RunBaseline` + builder — rod mapping, mastery, golden scythe

**Files:**
- Modify: `src/TheLongestYear.Core/RunBaseline.cs`
- Modify: `src/TheLongestYear.Core/RunBaselineBuilder.cs`
- Test: `tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs`

- [ ] **Step 1: Write the failing tests (append to `RunBaselineBuilderTests`)**

Note: these mirror the existing test setup in that file — match its helper for constructing `MetaState`, `RunState`, and `PlayerSnapshot`. The snippet below assumes the same constructors used elsewhere in the file; adjust the `peaks`/`run` construction to match the existing tests' style if different.

```csharp
    [Fact]
    public void Bamboo_rod_keep_grants_upgrade_level_1_capped_at_peak()
    {
        var meta = new MetaState { OwnedUpgrades = { "keep_fishing_rod_0" } };   // Bamboo
        // PlayerSnapshot.ToolTiers is init-only — set it via an initializer, not by indexing.
        var peaks = new PlayerSnapshot
        {
            ToolTiers = new Dictionary<string, int> { ["fishing_rod"] = 1 },     // reached bamboo this run
        };
        var baseline = RunBaselineBuilder.Build(meta, new RunState(), peaks, 0);
        Assert.True(baseline.ToolTiers.TryGetValue("fishing_rod", out int lvl));
        Assert.Equal(1, lvl);
    }

    [Fact]
    public void Golden_scythe_keep_sets_baseline_flag()
    {
        var meta = new MetaState { OwnedUpgrades = { "keep_golden_scythe" } };
        var baseline = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 0);
        Assert.True(baseline.GrantGoldenScythe);
    }

    [Fact]
    public void Mastery_keep_sets_baseline_level_as_permanent_floor()
    {
        var meta = new MetaState { OwnedUpgrades = { "keep_mastery_1", "keep_mastery_2", "keep_mastery_3" } };
        // No in-run peak needed: mastery is a permanent floor.
        var baseline = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 0);
        Assert.Equal(3, baseline.MasteryLevel);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test TheLongestYear.sln -c Release --filter RunBaselineBuilderTests`
Expected: FAIL (RunBaseline has no `GrantGoldenScythe`/`MasteryLevel`; bamboo not granted).

- [ ] **Step 3a: Add the two fields to `RunBaseline`**

In `RunBaseline.cs`, add:

```csharp
    /// <summary>Mastery level to restore at run start (0 = none). Permanent floor — owning the
    /// keep_mastery_N tiers always restores level N, not capped at in-run reach.</summary>
    public int MasteryLevel { get; init; }

    /// <summary>Grant the Golden Scythe instead of the basic scythe each run (Keep Golden Scythe).</summary>
    public bool GrantGoldenScythe { get; init; }
```

(Match the existing property style in the file — if other properties use `{ get; set; }` rather than `{ get; init; }`, use the same.)

- [ ] **Step 3b: Replace the fishing-rod block in `RunBaselineBuilder.Build`**

Replace the existing rod block (the `int rodKeep = meta.HighestKeptTier("keep_fishing_rod_", ...)` section) with an explicit id→UpgradeLevel mapping that handles the tier-0 Bamboo keep (`HighestKeptTier` ignores tier 0):

```csharp
        // Fishing rod — explicit keep-id → UpgradeLevel mapping (HighestKeptTier can't see the
        // tier-0 Bamboo keep). 1=bamboo, 2=fiberglass, 3=iridium = FishingRod.UpgradeLevel.
        int rodUpgradeLevel =
            meta.HasUpgrade("keep_fishing_rod_2") ? 3 :
            meta.HasUpgrade("keep_fishing_rod_1") ? 2 :
            meta.HasUpgrade("keep_fishing_rod_0") ? 1 : 0;
        if (rodUpgradeLevel > 0)
        {
            int rodPeak = peaks.ToolTiers.TryGetValue("fishing_rod", out int rp) ? rp : 0;
            int rodCapped = System.Math.Min(rodUpgradeLevel, rodPeak);
            if (rodCapped > 0)
                toolTiers["fishing_rod"] = rodCapped;
        }
```

Remove the now-unused `FishingRodKeepToUpgradeLevelOffset` and `FishingRodMaxKeepTier` constants.

- [ ] **Step 3c: Set the two new baseline fields in the returned object**

In the `return new RunBaseline { ... }` initializer, add:

```csharp
            MasteryLevel = MasteryFloor(meta),
            GrantGoldenScythe = meta.HasUpgrade("keep_golden_scythe"),
```

And add the helper at the bottom of the class:

```csharp
    // Highest owned keep_mastery_N (1..5). Permanent floor — NOT capped at in-run peak,
    // unlike skill/tool keeps (mastery is hard-won end-game progression).
    private static int MasteryFloor(MetaState meta)
    {
        int best = 0;
        for (int n = 1; n <= 5; n++)
            if (meta.HasUpgrade($"keep_mastery_{n}"))
                best = n;
        return best;
    }
```

- [ ] **Step 4: Run to verify pass (full Core suite — confirms no regression in existing rod tests)**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: PASS (all tests, including the pre-existing 369 + the new ones).

- [ ] **Step 5: Commit**

```
git add src/TheLongestYear.Core/RunBaseline.cs src/TheLongestYear.Core/RunBaselineBuilder.cs tests/TheLongestYear.Tests/RunBaselineBuilderTests.cs
git commit -m "feat(spec-a): baseline grants Bamboo rod, mastery floor, golden scythe flag"
```

---

## Task 11: `FarmerReset` — Golden Scythe grant swap + mastery restore

**Files:**
- Modify: `src/TheLongestYear/Loop/FarmerReset.cs`

Note: `Apply` already receives the `RunBaseline` (as `baseline`). The two new fields are now on it.

- [ ] **Step 1: Swap the basic scythe for the Golden Scythe when kept**

`EnsureBasicTools(p)` grants the 5 vanilla starters (including the basic scythe, `MeleeWeapon` id `47`). Add a parameter so it can skip the basic scythe, and grant the Golden Scythe instead. Change the call in `Apply`:

```csharp
            EnsureBasicTools(p, skipBasicScythe: baseline.GrantGoldenScythe);
            if (baseline.GrantGoldenScythe)
                GrantGoldenScythe(p);
```

Update `EnsureBasicTools` to skip the basic scythe when requested (the basic scythe is the `MeleeWeapon` in `Farmer.initialTools()`):

```csharp
        private static void EnsureBasicTools(Farmer p, bool skipBasicScythe = false)
        {
            foreach (Item tool in Farmer.initialTools())
            {
                // The basic scythe is the MeleeWeapon in the initial-tools set; skip it when the
                // player has Keep Golden Scythe (GrantGoldenScythe grants (W)53 instead).
                if (skipBasicScythe && tool is StardewValley.Tools.MeleeWeapon)
                    continue;
                // ...existing presence-check + first-empty-slot insertion unchanged...
            }
        }
```

Add the grant helper:

```csharp
        /// <summary>Add the Golden Scythe (W)53 into the first empty slot if not already held.</summary>
        private static void GrantGoldenScythe(Farmer p)
        {
            const string goldenScytheQid = "(W)53";
            foreach (Item held in p.Items)
                if (held != null && held.QualifiedItemId == goldenScytheQid)
                    return;
            Item scythe = StardewValley.ItemRegistry.Create(goldenScytheQid);
            for (int i = 0; i < p.Items.Count; i++)
                if (p.Items[i] == null) { p.Items[i] = scythe; return; }
        }
```

- [ ] **Step 2: Restore mastery level when kept**

After the skill-grant loop in `Apply` (after the `foreach (var kvp in baseline.SkillLevels)` block), add:

```csharp
            // Mastery — permanent floor from Keep Mastery. Set the global MasteryExp stat to the
            // threshold for the kept level so MasteryTrackerMenu.getCurrentMasteryLevel() reports it.
            if (baseline.MasteryLevel > 0)
            {
                int needed = StardewValley.Menus.MasteryTrackerMenu.getMasteryExpNeededForLevel(baseline.MasteryLevel);
                Game1.stats.Set("MasteryExp", (uint)needed);
            }
```

- [ ] **Step 3: Build (compile-only)**

Run: `dotnet build src\TheLongestYear\TheLongestYear.csproj -c Release -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded, 0 warnings.

(If `Game1.stats.Set` has a different signature on the PC runtime, adjust to the available setter — the field is `Game1.stats` with a `Set(string, uint)` method per the decompile; verify against the referenced `StardewModdingAPI`/`Stardew Valley` assemblies at build time.)

- [ ] **Step 4: Commit**

```
git add src/TheLongestYear/Loop/FarmerReset.cs
git commit -m "feat(spec-a): grant Golden Scythe + restore mastery on reset"
```

---

## Task 12: Full build, deploy, and in-game log verification

**Files:** none (verification only).

- [ ] **Step 1: Run the full test suite**

Run: `dotnet test TheLongestYear.sln -c Release`
Expected: PASS, 0 failures.

- [ ] **Step 2: Deploy build (close SMAPI first)**

```
Get-Process StardewModdingAPI -ErrorAction SilentlyContinue | Stop-Process -Force
dotnet build src\TheLongestYear\TheLongestYear.csproj -c Release
```
Expected: Build succeeded, 0 warnings; mod copied to the Stardew `Mods\TheLongestYear` folder.

- [ ] **Step 3: Relaunch SMAPI and load the save**

```
Start-Process "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.exe" -WorkingDirectory "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley"
```

- [ ] **Step 4: Verify via the SMAPI log + in-game (user-assisted)**

Pull `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` → `TheLongestYear/SMAPI-latest.txt` and confirm:
  - Harmony applied with `0 failed`, no exceptions building either shrine menu.
  - Open the read-only planning shrine: each chain shows only the lowest un-owned tier the player has reached (e.g. with a Steel can + Copper hoe and no keeps owned: Copper Watering Can + Copper Hoe present; Steel Watering Can absent); un-earned backpack/tool tiers no longer appear; other categories' next-buyable keeps appear when reached.
  - Title renders inside the box.
  - Buy Copper Watering Can at the reset shrine → Steel appears; buy Steel → no can keep until a Gold can is reached.
  - With Keep Golden Scythe owned, a reset grants the Golden Scythe `(W)53` and no basic scythe.
  - With Keep Mastery N owned, a reset reports mastery level N (`MasteryTrackerMenu`).

- [ ] **Step 5: Commit any log/notes artifacts (if updated)**

```
git add -A
git commit -m "chore(spec-a): in-game verification notes for Keep System v2"
```

---

## Notes / decisions baked in

- **Mastery + Golden Scythe are permanent floors**, not in-run-peak capped (unlike tool/skill keeps). Rationale: both are hard-won; peak-capping would make the keep near-useless. This avoids any `PlayerSnapshot` changes.
- **Rod chain uses explicit id→UpgradeLevel mapping** in the builder because `MetaState.HighestKeptTier` ignores the tier-0 Bamboo keep.
- **Reach is read live at menu-build time** (`RunReachEvaluator`), which equals end-of-run reach at the purchase shrine (pre-wipe) and current reach at the read-only preview.
- **No change to non-reach upgrades** (Stash, Buildings, Efficiency, Foresight, Obtainability, flat Loadout perks) — `RunReachRequirement` is null on them, so `reachMet` returns true.
