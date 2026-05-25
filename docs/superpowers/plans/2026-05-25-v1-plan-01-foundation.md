# The Longest Year — v1 Plan 01: Foundation & Core Loop Math

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Stand up a buildable, loadable SMAPI mod whose meta-state (Junimo Points, owned upgrades, stash tier) persists across game restarts, with the loop's pure math (calendar, JP scoring, gate evaluation) fully unit-tested.

**Architecture:** Three projects in one solution — `TheLongestYear.Core` (pure C#, **zero** references to StardewValley/SMAPI, holds all testable logic), `TheLongestYear` (the SMAPI mod: reads config, persists meta via SMAPI global data, exposes debug console commands), and `TheLongestYear.Tests` (xUnit against Core). Game-integration code is verified in-game; pure logic is verified by `dotnet test`.

**Tech Stack:** C# / .NET 6, SMAPI 4.x via `Pathoschild.Stardew.ModBuildConfig` 4.*, Harmony (enabled but unused in this plan), xUnit.

**Repo conventions:** All commits end with the workspace `Co-Authored-By` footer. Local commits only — never push without explicit approval.

---

## v1 Plan Roadmap (this file = Plan 01)

| Plan | Title | Produces |
|------|-------|----------|
| **01** | **Foundation & Core Loop Math** *(this file)* | Buildable mod, persistent meta-state, tested calendar/JP/gate logic |
| 02 | Contract System (pure logic) | `CCMapping` (vanilla bundle ground truth), contract model, solvable-partition generator + validation, championing rules, item rarity — all unit-tested |
| 03 | Run Lifecycle & In-Place Reset | `RunManager` wired to day events, `WorldReset`, save backup, in-game two-loop leak test |
| 04 | Donations & JP Wiring | consume-on-donate, cumulative donation ledger, bundle/room completion → JP |
| 05 | UI | `ContractPickMenu` (planning hub) + `JunimoShrineMenu` (shop) |
| 06 | Upgrades, Obtainability & Foresight | apply-at-run-start upgrades, Mixed Seeds cultivation, Fortune drop boosts, Weather Sage + Cart Whisperer |
| 07 | Stash, Narrative & polish | scarce Junimo Stash + UI, placeholder narrative, final config |

> Refinement of spec §13: the mod's files live under `src/TheLongestYear/` rather than the repo root, to accommodate the Core/Tests split. Otherwise the module responsibilities are unchanged.

**Target file layout after Plan 01:**

```
TheLongestYear/                          (repo root — already has docs/, .gitignore, .git)
  TheLongestYear.sln
  src/
    TheLongestYear.Core/
      TheLongestYear.Core.csproj
      Rarity.cs
      Calendar.cs
      JpSettings.cs
      JpCalculator.cs
      GateEvaluator.cs
      MetaState.cs
      GameplayConfig.cs
    TheLongestYear/
      TheLongestYear.csproj
      manifest.json
      ModEntry.cs
      MetaStore.cs
  tests/
    TheLongestYear.Tests/
      TheLongestYear.Tests.csproj
      CalendarTests.cs
      JpCalculatorTests.cs
      GateEvaluatorTests.cs
      MetaStateTests.cs
```

---

## Task 1: Scaffold the solution and three projects

**Files:**
- Create: `TheLongestYear.sln`
- Create: `src/TheLongestYear.Core/TheLongestYear.Core.csproj`
- Create: `src/TheLongestYear/TheLongestYear.csproj`
- Create: `tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`

All commands run from the repo root: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`.

- [ ] **Step 1: Create the solution and the Core + Tests projects**

```bash
dotnet new sln -n TheLongestYear
dotnet new classlib -n TheLongestYear.Core -o src/TheLongestYear.Core -f net6.0
dotnet new xunit -n TheLongestYear.Tests -o tests/TheLongestYear.Tests -f net6.0
```

- [ ] **Step 2: Delete the template placeholder classes**

Remove `src/TheLongestYear.Core/Class1.cs` and `tests/TheLongestYear.Tests/UnitTest1.cs`.

```bash
rm src/TheLongestYear.Core/Class1.cs tests/TheLongestYear.Tests/UnitTest1.cs
```

- [ ] **Step 3: Set Core's csproj to nullable-enabled, no implicit usings**

Overwrite `src/TheLongestYear.Core/TheLongestYear.Core.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>

</Project>
```

- [ ] **Step 4: Hand-create the SMAPI mod csproj**

Create `src/TheLongestYear/TheLongestYear.csproj` (matches the sibling-mod pattern, plus a ProjectReference to Core):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <EnableHarmony>true</EnableHarmony>
    <LangVersion>latest</LangVersion>
    <Nullable>disable</Nullable>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Pathoschild.Stardew.ModBuildConfig" Version="4.*" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\TheLongestYear.Core\TheLongestYear.Core.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Add all three projects to the solution and wire references**

```bash
dotnet sln add src/TheLongestYear.Core/TheLongestYear.Core.csproj src/TheLongestYear/TheLongestYear.csproj tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
dotnet add tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj reference src/TheLongestYear.Core/TheLongestYear.Core.csproj
dotnet add src/TheLongestYear/TheLongestYear.csproj reference src/TheLongestYear.Core/TheLongestYear.Core.csproj
```

- [ ] **Step 6: Verify the test project builds (no game required)**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: build succeeds, `Passed!  - Failed: 0, Passed: 0` (no tests yet).

> Note: building `TheLongestYear.csproj` (the mod) requires Stardew Valley installed so ModBuildConfig can locate the game DLLs — it is installed on this machine. Building only the Tests project does **not** need the game, which is what lets CI/`dotnet test` run anywhere.

- [ ] **Step 7: Verify the mod project builds against the game**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: build succeeds; ModBuildConfig logs the detected game path. (It will warn that `manifest.json` is missing — that is fixed in Task 6.)

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "chore: scaffold solution (Core, mod, Tests projects)"
```

---

## Task 2: Core — `Rarity` and `Calendar`

**Files:**
- Create: `src/TheLongestYear.Core/Rarity.cs`
- Create: `src/TheLongestYear.Core/Calendar.cs`
- Test: `tests/TheLongestYear.Tests/CalendarTests.cs`

- [ ] **Step 1: Write the failing calendar tests**

Create `tests/TheLongestYear.Tests/CalendarTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class CalendarTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(7, 1)]
    [InlineData(8, 2)]
    [InlineData(21, 3)]
    [InlineData(28, 4)]
    public void WeekInMonth_maps_day_to_week(int day, int expected)
        => Assert.Equal(expected, Calendar.WeekInMonth(day));

    [Theory]
    [InlineData(7, true)]
    [InlineData(8, false)]
    [InlineData(14, true)]
    [InlineData(28, true)]
    public void IsWeekEnd_true_on_multiples_of_seven(int day, bool expected)
        => Assert.Equal(expected, Calendar.IsWeekEnd(day));

    [Theory]
    [InlineData(28, true)]
    [InlineData(21, false)]
    public void IsMonthEnd_true_only_on_day_28(int day, bool expected)
        => Assert.Equal(expected, Calendar.IsMonthEnd(day));

    [Theory]
    [InlineData(0, 7, 1)]
    [InlineData(1, 1, 5)]
    [InlineData(3, 28, 16)]
    public void WeekOfYear_combines_month_and_week(int month, int day, int expected)
        => Assert.Equal(expected, Calendar.WeekOfYear(month, day));

    [Fact]
    public void WeekInMonth_rejects_out_of_range_day()
        => Assert.Throws<System.ArgumentOutOfRangeException>(() => Calendar.WeekInMonth(0));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `Calendar` does not exist.

- [ ] **Step 3: Create the `Rarity` enum**

Create `src/TheLongestYear.Core/Rarity.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>How hard a Community Center item is to obtain; drives Junimo Point value.</summary>
public enum Rarity
{
    Common,
    Uncommon,
    Rare,
    VeryRare
}
```

- [ ] **Step 4: Implement `Calendar`**

Create `src/TheLongestYear.Core/Calendar.cs`:

```csharp
using System;

namespace TheLongestYear.Core;

/// <summary>Pure helpers over Stardew's fixed 28-day month / 4-season year.</summary>
public static class Calendar
{
    public const int DaysPerWeek = 7;
    public const int DaysPerMonth = 28;
    public const int WeeksPerMonth = 4;
    public const int MonthsPerYear = 4;
    public const int WeeksPerYear = WeeksPerMonth * MonthsPerYear; // 16

    /// <summary>Week within the month (1-4) for a 1-based day of month.</summary>
    public static int WeekInMonth(int dayOfMonth)
    {
        if (dayOfMonth is < 1 or > DaysPerMonth)
            throw new ArgumentOutOfRangeException(nameof(dayOfMonth), dayOfMonth, "Day must be 1-28.");
        return ((dayOfMonth - 1) / DaysPerWeek) + 1;
    }

    public static bool IsWeekEnd(int dayOfMonth) => dayOfMonth % DaysPerWeek == 0;

    public static bool IsMonthEnd(int dayOfMonth) => dayOfMonth == DaysPerMonth;

    /// <summary>Week index across the whole year (1-16). monthIndex: Spring=0..Winter=3.</summary>
    public static int WeekOfYear(int monthIndex, int dayOfMonth)
        => monthIndex * WeeksPerMonth + WeekInMonth(dayOfMonth);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS (all CalendarTests green).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): add Rarity enum and Calendar helpers"
```

---

## Task 3: Core — `JpSettings` and `JpCalculator`

**Files:**
- Create: `src/TheLongestYear.Core/JpSettings.cs`
- Create: `src/TheLongestYear.Core/JpCalculator.cs`
- Test: `tests/TheLongestYear.Tests/JpCalculatorTests.cs`

- [ ] **Step 1: Write the failing JP tests**

Create `tests/TheLongestYear.Tests/JpCalculatorTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class JpCalculatorTests
{
    private static JpCalculator Make() => new JpCalculator(new JpSettings());

    [Theory]
    [InlineData(Rarity.Common, 1, 1)]
    [InlineData(Rarity.Rare, 1, 10)]
    [InlineData(Rarity.Rare, 11, 20)]   // 10 * (1 + 10*0.1) = 20
    [InlineData(Rarity.VeryRare, 11, 50)] // 25 * 2.0 = 50
    public void PerItem_scales_by_rarity_and_week_depth(Rarity rarity, int week, long expected)
        => Assert.Equal(expected, Make().PerItem(rarity, week));

    [Fact]
    public void PerItem_rounds_half_away_from_zero()
        => Assert.Equal(3, Make().PerItem(Rarity.Common, 16)); // 1 * 2.5 = 2.5 -> 3

    [Fact]
    public void ForDonationBatch_sums_items_and_bundle_bonus()
    {
        var lines = new[] { new DonationLine(Rarity.Rare, 2), new DonationLine(Rarity.Common, 3) };
        // week 1: 2*10 + 3*1 = 23; + 1 bundle (15) = 38
        Assert.Equal(38, Make().ForDonationBatch(lines, weekOfYear: 1, bundlesCompleted: 1, roomsCompleted: 0));
    }

    [Fact]
    public void ForDonationBatch_adds_room_bonus()
    {
        var lines = new[] { new DonationLine(Rarity.Common, 0) };
        // 0 item JP + 1 room (60)
        Assert.Equal(60, Make().ForDonationBatch(lines, weekOfYear: 5, bundlesCompleted: 0, roomsCompleted: 1));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `JpSettings`/`JpCalculator`/`DonationLine` do not exist.

- [ ] **Step 3: Implement `JpSettings`**

Create `src/TheLongestYear.Core/JpSettings.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Tunable Junimo Point values. Serialized as part of the mod config.</summary>
public sealed class JpSettings
{
    public int CommonJp { get; set; } = 1;
    public int UncommonJp { get; set; } = 3;
    public int RareJp { get; set; } = 10;
    public int VeryRareJp { get; set; } = 25;

    /// <summary>Per-week-of-year multiplier step. Multiplier = 1 + (week-1) * step.</summary>
    public double WeekDepthStep { get; set; } = 0.1;

    public int BundleCompletionBonus { get; set; } = 15;
    public int RoomCompletionBonus { get; set; } = 60;

    public int BaseFor(Rarity rarity) => rarity switch
    {
        Rarity.Common => CommonJp,
        Rarity.Uncommon => UncommonJp,
        Rarity.Rare => RareJp,
        Rarity.VeryRare => VeryRareJp,
        _ => 0
    };
}
```

- [ ] **Step 4: Implement `JpCalculator` and `DonationLine`**

Create `src/TheLongestYear.Core/JpCalculator.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One line of a donation batch: a quantity of items at a given rarity.</summary>
public readonly record struct DonationLine(Rarity Rarity, int Count);

/// <summary>
/// Computes Junimo Points. Per-item JP scales by rarity and how deep into the year you are.
/// Completion bonuses attach to vanilla bundles/rooms only — never to contracts/gates (no double-dip).
/// </summary>
public sealed class JpCalculator
{
    private readonly JpSettings _s;

    public JpCalculator(JpSettings settings) => _s = settings;

    public double Multiplier(int weekOfYear) => 1.0 + (weekOfYear - 1) * _s.WeekDepthStep;

    public long PerItem(Rarity rarity, int weekOfYear)
        => (long)Math.Round(_s.BaseFor(rarity) * Multiplier(weekOfYear), MidpointRounding.AwayFromZero);

    public long ForDonationBatch(
        IEnumerable<DonationLine> lines,
        int weekOfYear,
        int bundlesCompleted,
        int roomsCompleted)
    {
        long total = 0;
        foreach (var line in lines)
            total += PerItem(line.Rarity, weekOfYear) * line.Count;
        total += (long)bundlesCompleted * _s.BundleCompletionBonus;
        total += (long)roomsCompleted * _s.RoomCompletionBonus;
        return total;
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): add JP settings and calculator with rarity/depth scaling"
```

---

## Task 4: Core — `GateEvaluator`

**Files:**
- Create: `src/TheLongestYear.Core/GateEvaluator.cs`
- Test: `tests/TheLongestYear.Tests/GateEvaluatorTests.cs`

- [ ] **Step 1: Write the failing gate tests**

Create `tests/TheLongestYear.Tests/GateEvaluatorTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GateEvaluatorTests
{
    private static GateEvaluator E() => new GateEvaluator();

    [Fact]
    public void Midweek_day_always_continues()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(dayOfMonth: 3, monthIndex: 0, championedComplete: false, allFiveComplete: false));

    [Fact]
    public void Week_end_with_incomplete_champion_fails_weekly()
        => Assert.Equal(GateResult.WeeklyFail,
            E().EvaluateDayEnd(7, 0, championedComplete: false, allFiveComplete: false));

    [Fact]
    public void Week_end_with_complete_champion_continues()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(7, 0, championedComplete: true, allFiveComplete: false));

    [Fact]
    public void Month_end_missing_fifth_contract_fails_monthly()
        => Assert.Equal(GateResult.MonthlyFail,
            E().EvaluateDayEnd(28, 0, championedComplete: true, allFiveComplete: false));

    [Fact]
    public void Month_end_all_complete_advances_when_not_winter()
        => Assert.Equal(GateResult.Continue,
            E().EvaluateDayEnd(28, 0, championedComplete: true, allFiveComplete: true));

    [Fact]
    public void Winter_end_all_complete_wins()
        => Assert.Equal(GateResult.Win,
            E().EvaluateDayEnd(28, 3, championedComplete: true, allFiveComplete: true));

    [Fact]
    public void Month_end_incomplete_champion_fails_weekly_first()
        => Assert.Equal(GateResult.WeeklyFail,
            E().EvaluateDayEnd(28, 3, championedComplete: false, allFiveComplete: true));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `GateEvaluator`/`GateResult` do not exist.

- [ ] **Step 3: Implement `GateEvaluator` and `GateResult`**

Create `src/TheLongestYear.Core/GateEvaluator.cs`:

```csharp
namespace TheLongestYear.Core;

public enum GateResult
{
    /// <summary>Keep playing (mid-week, or a passed checkpoint that isn't the final win).</summary>
    Continue,
    /// <summary>Championed contract not finished by week's end — run ends.</summary>
    WeeklyFail,
    /// <summary>Not all five contracts finished by month's end — run ends.</summary>
    MonthlyFail,
    /// <summary>Winter cleared with the whole CC done — loop breaks.</summary>
    Win
}

/// <summary>
/// Decides, at the end of a day, whether the run continues, fails, or wins.
/// Weekly gate (championed contract) is checked before the monthly gate (all five).
/// </summary>
public sealed class GateEvaluator
{
    public GateResult EvaluateDayEnd(int dayOfMonth, int monthIndex, bool championedComplete, bool allFiveComplete)
    {
        if (!Calendar.IsWeekEnd(dayOfMonth))
            return GateResult.Continue;

        if (!championedComplete)
            return GateResult.WeeklyFail;

        if (Calendar.IsMonthEnd(dayOfMonth))
        {
            if (!allFiveComplete)
                return GateResult.MonthlyFail;

            if (monthIndex == Calendar.MonthsPerYear - 1)
                return GateResult.Win;
        }

        return GateResult.Continue;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat(core): add GateEvaluator for weekly/monthly/win checkpoints"
```

---

## Task 5: Core — `MetaState` and `GameplayConfig`

**Files:**
- Create: `src/TheLongestYear.Core/MetaState.cs`
- Create: `src/TheLongestYear.Core/GameplayConfig.cs`
- Test: `tests/TheLongestYear.Tests/MetaStateTests.cs`

- [ ] **Step 1: Write the failing serialization test**

Create `tests/TheLongestYear.Tests/MetaStateTests.cs`:

```csharp
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class MetaStateTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var original = new MetaState
        {
            JunimoPoints = 123,
            StashCapacityTier = 2,
            OwnedUpgrades = { "backpack_1", "cult_redcabbage" }
        };

        string json = JsonSerializer.Serialize(original);
        MetaState restored = JsonSerializer.Deserialize<MetaState>(json)!;

        Assert.Equal(123, restored.JunimoPoints);
        Assert.Equal(2, restored.StashCapacityTier);
        Assert.Equal(new[] { "backpack_1", "cult_redcabbage" }, restored.OwnedUpgrades);
    }

    [Fact]
    public void New_meta_state_starts_empty()
    {
        var s = new MetaState();
        Assert.Equal(0, s.JunimoPoints);
        Assert.Equal(0, s.StashCapacityTier);
        Assert.Empty(s.OwnedUpgrades);
    }

    [Fact]
    public void Has_upgrade_checks_membership()
    {
        var s = new MetaState { OwnedUpgrades = { "horse_early" } };
        Assert.True(s.HasUpgrade("horse_early"));
        Assert.False(s.HasUpgrade("backpack_1"));
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: FAIL to compile — `MetaState` does not exist.

- [ ] **Step 3: Implement `MetaState`**

Create `src/TheLongestYear.Core/MetaState.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Everything that survives a loop reset ("banked forever").
/// Persisted via SMAPI global data, so it lives outside any single save.
/// </summary>
public sealed class MetaState
{
    public long JunimoPoints { get; set; }

    /// <summary>IDs of permanently purchased upgrades.</summary>
    public List<string> OwnedUpgrades { get; set; } = new();

    /// <summary>Tier of the Junimo Stash capacity upgrade (0 = base).</summary>
    public int StashCapacityTier { get; set; }

    public bool HasUpgrade(string id) => OwnedUpgrades.Contains(id);
}
```

- [ ] **Step 4: Implement `GameplayConfig`**

Create `src/TheLongestYear.Core/GameplayConfig.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Root config object read by the mod via SMAPI. All tuning dials hang off this.</summary>
public sealed class GameplayConfig
{
    public JpSettings Jp { get; set; } = new JpSettings();
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS (all suites green).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(core): add MetaState (banked progress) and GameplayConfig"
```

---

## Task 6: Mod — manifest, `MetaStore`, and `ModEntry`

**Files:**
- Create: `src/TheLongestYear/manifest.json`
- Create: `src/TheLongestYear/MetaStore.cs`
- Create: `src/TheLongestYear/ModEntry.cs`

This task is verified in-game (SMAPI global-data persistence can't be unit-tested without the game). The acceptance check is: the mod loads, `tly_addjp` mutates and saves JP, and the value survives a full game restart — proving the "banked forever" persistence the whole design depends on.

- [ ] **Step 1: Create the manifest**

Create `src/TheLongestYear/manifest.json`:

```json
{
    "Name": "The Longest Year",
    "Author": "sonofskywalker3",
    "Version": "0.1.0",
    "Description": "Roguelite time-loop: restore the Community Center within the year, or the Junimos rewind it and you try again, stronger.",
    "UniqueID": "sonofskywalker3.TheLongestYear",
    "EntryDll": "TheLongestYear.dll",
    "MinimumApiVersion": "4.0.0",
    "UpdateKeys": []
}
```

- [ ] **Step 2: Implement `MetaStore`**

Create `src/TheLongestYear/MetaStore.cs`:

```csharp
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear
{
    /// <summary>Loads and persists <see cref="MetaState"/> via SMAPI global data (outlives any save).</summary>
    internal sealed class MetaStore
    {
        private const string DataKey = "meta-state";
        private readonly IDataHelper _data;

        public MetaState State { get; private set; }

        public MetaStore(IDataHelper data)
        {
            _data = data;
            State = _data.ReadGlobalData<MetaState>(DataKey) ?? new MetaState();
        }

        public void Save() => _data.WriteGlobalData(DataKey, State);
    }
}
```

- [ ] **Step 3: Implement `ModEntry`**

Create `src/TheLongestYear/ModEntry.cs`:

```csharp
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear
{
    public sealed class ModEntry : Mod
    {
        private GameplayConfig _config;
        private MetaStore _meta;

        public override void Entry(IModHelper helper)
        {
            _config = helper.ReadConfig<GameplayConfig>();
            _meta = new MetaStore(helper.Data);

            this.Monitor.Log(
                $"The Longest Year loaded. JP banked: {_meta.State.JunimoPoints}.",
                LogLevel.Info);

            helper.ConsoleCommands.Add("tly_meta", "Print The Longest Year meta-state.", this.PrintMeta);
            helper.ConsoleCommands.Add("tly_addjp", "Add Junimo Points (debug). Usage: tly_addjp <amount>", this.AddJp);
        }

        private void PrintMeta(string command, string[] args)
        {
            MetaState s = _meta.State;
            this.Monitor.Log(
                $"JP={s.JunimoPoints}, StashTier={s.StashCapacityTier}, Upgrades=[{string.Join(", ", s.OwnedUpgrades)}]",
                LogLevel.Info);
        }

        private void AddJp(string command, string[] args)
        {
            if (args.Length < 1 || !long.TryParse(args[0], out long amount))
            {
                this.Monitor.Log("Usage: tly_addjp <amount>", LogLevel.Warn);
                return;
            }

            _meta.State.JunimoPoints += amount;
            _meta.Save();
            this.Monitor.Log($"JP is now {_meta.State.JunimoPoints} (saved).", LogLevel.Info);
        }
    }
}
```

- [ ] **Step 4: Build the mod and confirm it deploys**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: build succeeds with no manifest warning; ModBuildConfig reports copying the mod (and `TheLongestYear.Core.dll`) into the Stardew `Mods/TheLongestYear` folder.

- [ ] **Step 5: In-game persistence check (manual)**

1. Launch the game through SMAPI (`StardewModdingAPI.exe`).
2. In the SMAPI console, run `tly_meta` → expect `JP=0, StashTier=0, Upgrades=[]`.
3. Run `tly_addjp 50` → expect `JP is now 50 (saved).`
4. **Fully quit** the game, relaunch through SMAPI.
5. Confirm the startup log line reads `JP banked: 50`, and `tly_meta` again shows `JP=50`.

This proves global-data persistence survives a restart — the foundation of "Junimo Points are banked forever."

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(mod): load config, persist meta via global data, add debug commands"
```

---

## Done criteria for Plan 01

- `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj` is green (Calendar, JP, Gate, MetaState).
- `dotnet build src/TheLongestYear/TheLongestYear.csproj` succeeds and deploys to the Mods folder.
- In-game: JP added via `tly_addjp` survives a full game restart.
- The Core library has **no** reference to StardewValley or SMAPI assemblies (keeps the loop math unit-testable).

## Self-review notes (spec coverage)

- **Calendar / 16-week structure** (spec §3) → Task 2.
- **JP economy: rarity-scaled per-item + bundle/room bonuses, no contract reward** (spec §8) → Task 3 (`JpCalculator` takes bundle/room counts only; contracts never feed it).
- **Weekly + monthly gates, Winter→win** (spec §5) → Task 4 (`GateEvaluator`).
- **Banked-forever meta-state: JP, upgrades, stash tier** (spec §7) → Tasks 5 + 6 (`MetaState` + `MetaStore` global data).
- **Config in one place** (workspace rule) → `GameplayConfig` (Task 5).
- Deferred to later plans by design: contract generation/solvability (Plan 02), reset/leak test (Plan 03), donations wiring (Plan 04), UI (Plan 05), upgrades/obtainability/foresight (Plan 06), stash contents + narrative (Plan 07).
