# Derived Item Availability, Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every fish and metal item a derived earliest-possible season and effort score, and use those to compute per-ingredient deadlines for PerItem bundles, closing the largest part of the season-gate leak.

**Architecture:** A new pure Core component derives two values per item id from the game's own data tables, reached through the existing `Raw*` boundary records so Core never touches Game1. A second pure component turns a bundle's ingredient list plus those values into per-ingredient deadlines: rank by effort, spread across the four checkpoints, then clamp each deadline upward to that item's floor so an impossible gate cannot be expressed. `BundleClassifier`'s PerItem branch stops reading the hand written pin table and calls the new component instead.

**Tech Stack:** C# 10, .NET 6, xUnit. SMAPI 4.x for the glue layer only. `TheLongestYear.Core` is pure and has no Game1 or SMAPI reference, which is what makes all of this unit testable.

**Spec:** `docs/superpowers/specs/2026-08-27-derived-item-availability-design.md`

## Global Constraints

- Branch is `feat/difficulty-modifiers`. Do **not** bump `manifest.json`'s `Version` on this branch; the release line owns version bumps. Integrate via merge, never rebase.
- Commit after every task. Never push. Pushing needs Jeff's explicit "yes, push".
- No em dashes in any file, comment, log line or commit message.
- No `/sdcard/` paths anywhere.
- `TheLongestYear.Core` must not reference `StardewValley`, `Game1` or `StardewModdingAPI`. Live game data reaches Core only through the `Raw*` records in `src/TheLongestYear.Core/ItemPoolModel.cs`, populated by `src/TheLongestYear/Loop/GameDataPools.cs`.
- Build: `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo`
- Test: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`
- Baseline at the start of this plan: **1047 tests passing**. Every task must leave the suite green.
- A running Stardew LOCKS the mod DLL. To deploy, use `pwsh -NoProfile -File tools/deploy.ps1`, not a plain build.
- Item ids in Core are **qualified** (`(O)128`). `BundleParsing.NormalizeItemId` produces them. Raw game data tables are keyed by **unqualified** ids (`128`). Every lookup across that boundary must qualify or strip deliberately, and the tests below pin which side is which.
- `Season` is `Spring = 0, Summer = 1, Fall = 2, Winter = 3` (`src/TheLongestYear.Core/Season.cs`). Comparisons use the ordinal.

## Scope

This plan implements **Phase 1 only** of the spec's four phases: the framework, the two leaking pool domains (Fish and Metals), deadline assignment, and diagnostics. Phases 2 to 4 (remaining pool domains, authored bundle domains, retiring the old pin path) get their own plans. At the end of this plan the mod builds, tests pass, and re-rolled fish and metals bundles gate correctly. The seven bundles with no pinned ingredient at all (Orchard, Helper's, Chef's, Forest, Home Cook's, Spirit's Eve, Sticky) are still ungated after this plan; Phase 3 closes them.

## File Structure

**Create:**
- `src/TheLongestYear.Core/ItemAvailability.cs` - the record and the lookup container with override precedence. One responsibility: hold derived values and answer `For(itemId)`.
- `src/TheLongestYear.Core/Availability/FishAvailability.cs` - fish derivation rules.
- `src/TheLongestYear.Core/Availability/MetalsAvailability.cs` - ore and bar derivation rules.
- `src/TheLongestYear.Core/Availability/LocationGating.cs` - the season floor implied by a location being locked behind world progress.
- `src/TheLongestYear.Core/Availability/ItemAvailabilityBuilder.cs` - composes the domain rules into one model.
- `src/TheLongestYear.Core/BundleDeadlines.cs` - ingredient list plus model to per-ingredient deadlines.
- `tests/TheLongestYear.Tests/ItemAvailabilityTests.cs`
- `tests/TheLongestYear.Tests/FishAvailabilityTests.cs`
- `tests/TheLongestYear.Tests/MetalsAvailabilityTests.cs`
- `tests/TheLongestYear.Tests/BundleDeadlinesTests.cs`

**Modify:**
- `src/TheLongestYear.Core/ItemPoolModel.cs` - add `RawFishEntry`, add `Fish` raw data to what the builder receives.
- `src/TheLongestYear/Loop/GameDataPools.cs:90-95` - the `Data/Fish` read currently keeps only the `trap` marker; widen it.
- `src/TheLongestYear.Core/BundleClassifier.cs:145-160` - the PerItem branch.
- `src/TheLongestYear/ModEntry.cs` - build the model at load, pass it down, register the diagnostic command.
- `src/TheLongestYear/Donations/BundleCatalogBuilder.cs` - accept and forward the model.

A new `Availability/` subfolder keeps the domain rule files together, matching the existing `Day28/`, `Intro/` and `Interactables/` subfolders in Core.

---

### Task 1: The availability record and lookup container

**Files:**
- Create: `src/TheLongestYear.Core/ItemAvailability.cs`
- Test: `tests/TheLongestYear.Tests/ItemAvailabilityTests.cs`

**Interfaces:**
- Consumes: `Season` from `src/TheLongestYear.Core/Season.cs`.
- Produces:
  - `sealed record ItemAvailability(Season EarliestSeason, int Effort, string Basis)`
  - `sealed class ItemAvailabilityModel` with constructor `(IReadOnlyDictionary<string, ItemAvailability> derived, IReadOnlyDictionary<string, Season>? seasonOverrides = null, IReadOnlyDictionary<string, int>? effortOverrides = null)`, method `ItemAvailability For(string qualifiedItemId)`, property `IReadOnlyCollection<string> UnrecognisedIds`, and constants `ItemAvailabilityModel.UnrecognisedEffort = 6`.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/ItemAvailabilityTests.cs`:

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ItemAvailabilityTests
{
    private static ItemAvailabilityModel Model(
        Dictionary<string, ItemAvailability>? derived = null,
        Dictionary<string, Season>? seasonOverrides = null,
        Dictionary<string, int>? effortOverrides = null)
        => new ItemAvailabilityModel(
            derived ?? new Dictionary<string, ItemAvailability>(),
            seasonOverrides, effortOverrides);

    [Fact]
    public void A_Derived_Item_Comes_Back_As_Derived()
    {
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)128"] = new ItemAvailability(Season.Summer, 7, "summer-only fish"),
        });

        ItemAvailability result = model.For("(O)128");

        Assert.Equal(Season.Summer, result.EarliestSeason);
        Assert.Equal(7, result.Effort);
        Assert.Equal("summer-only fish", result.Basis);
    }

    /// <summary>An unrecognised item floors at WINTER, not Spring. Deadlines clamp UPWARD to the
    /// floor, so a floor guessed too early permits an impossible gate, which bricks a run. Late is
    /// merely lenient. Spec section 3.1.</summary>
    [Fact]
    public void An_Unknown_Item_Floors_At_Winter_And_Is_Recorded()
    {
        var model = Model();

        ItemAvailability result = model.For("(O)9999");

        Assert.Equal(Season.Winter, result.EarliestSeason);
        Assert.Equal(ItemAvailabilityModel.UnrecognisedEffort, result.Effort);
        Assert.Contains("no derivation rule", result.Basis);
        Assert.Contains("(O)9999", model.UnrecognisedIds);
    }

    [Fact]
    public void A_Season_Override_Replaces_The_Derived_Floor_And_Says_So()
    {
        var model = Model(
            new Dictionary<string, ItemAvailability>
            {
                ["(O)128"] = new ItemAvailability(Season.Summer, 7, "summer-only fish"),
            },
            seasonOverrides: new Dictionary<string, Season> { ["(O)128"] = Season.Fall });

        ItemAvailability result = model.For("(O)128");

        Assert.Equal(Season.Fall, result.EarliestSeason);
        Assert.Equal(7, result.Effort);
        Assert.Contains("override", result.Basis);
        Assert.Contains("summer-only fish", result.Basis);
    }

    [Fact]
    public void An_Effort_Override_Replaces_Only_The_Effort()
    {
        var model = Model(
            new Dictionary<string, ItemAvailability>
            {
                ["(O)128"] = new ItemAvailability(Season.Summer, 7, "summer-only fish"),
            },
            effortOverrides: new Dictionary<string, int> { ["(O)128"] = 2 });

        ItemAvailability result = model.For("(O)128");

        Assert.Equal(Season.Summer, result.EarliestSeason);
        Assert.Equal(2, result.Effort);
    }

    [Fact]
    public void An_Override_Applies_To_An_Item_With_No_Derived_Entry()
    {
        var model = Model(
            seasonOverrides: new Dictionary<string, Season> { ["(O)9999"] = Season.Spring });

        ItemAvailability result = model.For("(O)9999");

        Assert.Equal(Season.Spring, result.EarliestSeason);
    }

    [Fact]
    public void Lookup_Is_Ordinal_Not_Case_Insensitive()
    {
        var model = Model(new Dictionary<string, ItemAvailability>
        {
            ["(O)Sunfish"] = new ItemAvailability(Season.Spring, 1, "test"),
        });

        Assert.Equal(Season.Winter, model.For("(o)sunfish").EarliestSeason);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter ItemAvailabilityTests`
Expected: build error, `ItemAvailability` and `ItemAvailabilityModel` do not exist.

- [ ] **Step 3: Write the implementation**

Create `src/TheLongestYear.Core/ItemAvailability.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>What the engine knows about one item, derived from the game's own data tables.
///
/// Two values, deliberately separate. Before this existed, PerItem bundles gated on a single
/// hand written table (GameplayConfig.DefaultItemSeasonPins) whose entries conflated the two:
/// Sunfish was pinned Spring and Shad Summer although both are catchable year round, so those
/// dates were pacing choices wearing the costume of availability facts. Reading a pacing choice
/// as an availability fact is what made a Fall Foraging bundle unsatisfiable at its own gate
/// (the Purple Mushroom incident, 2026-08-27).</summary>
/// <param name="EarliestSeason">Hard floor: before this season the item cannot exist at all.</param>
/// <param name="Effort">Derived judgement of how much work the item is. Higher is harder.</param>
/// <param name="Basis">Human readable derivation, for tly_itemmodel and the generated model doc.</param>
public sealed record ItemAvailability(Season EarliestSeason, int Effort, string Basis);

/// <summary>Every item's <see cref="ItemAvailability"/>, plus the override layers.
///
/// Precedence, lowest to highest: derived value, then curated season/effort overrides
/// (GameplayConfig defaults merged with the user's config, merged by the caller before it gets
/// here). An id with no derived entry and no override floors at WINTER, which is the safe
/// direction: BundleDeadlines clamps a deadline UPWARD to the floor, so a floor guessed too
/// early permits a gate the world cannot satisfy and bricks the run, while a floor guessed too
/// late only makes the gate lenient.</summary>
public sealed class ItemAvailabilityModel
{
    /// <summary>Effort assigned to an item no rule recognised. Mid scale, so an unrecognised item
    /// neither leads nor trails the effort ranking of a bundle it appears in.</summary>
    public const int UnrecognisedEffort = 6;

    private const string UnrecognisedBasis = "no derivation rule matched this item";

    private readonly IReadOnlyDictionary<string, ItemAvailability> _derived;
    private readonly IReadOnlyDictionary<string, Season> _seasonOverrides;
    private readonly IReadOnlyDictionary<string, int> _effortOverrides;
    private readonly HashSet<string> _unrecognised = new(StringComparer.Ordinal);

    public ItemAvailabilityModel(
        IReadOnlyDictionary<string, ItemAvailability> derived,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null)
    {
        _derived = derived ?? throw new ArgumentNullException(nameof(derived));
        _seasonOverrides = seasonOverrides ?? new Dictionary<string, Season>(StringComparer.Ordinal);
        _effortOverrides = effortOverrides ?? new Dictionary<string, int>(StringComparer.Ordinal);
    }

    /// <summary>Ids that fell through to the unrecognised default during this session's lookups.
    /// Surfaced by tly_itemmodel so a modded item the engine cannot place is visible rather than
    /// silently ungated.</summary>
    public IReadOnlyCollection<string> UnrecognisedIds => _unrecognised;

    public ItemAvailability For(string qualifiedItemId)
    {
        if (qualifiedItemId == null) throw new ArgumentNullException(nameof(qualifiedItemId));

        bool known = _derived.TryGetValue(qualifiedItemId, out ItemAvailability? derived);
        bool hasSeasonOverride = _seasonOverrides.TryGetValue(qualifiedItemId, out Season overrideSeason);
        bool hasEffortOverride = _effortOverrides.TryGetValue(qualifiedItemId, out int overrideEffort);

        if (!known && !hasSeasonOverride && !hasEffortOverride)
        {
            _unrecognised.Add(qualifiedItemId);
            return new ItemAvailability(Season.Winter, UnrecognisedEffort, UnrecognisedBasis);
        }

        Season season = derived?.EarliestSeason ?? Season.Winter;
        int effort = derived?.Effort ?? UnrecognisedEffort;
        string basis = derived?.Basis ?? UnrecognisedBasis;

        if (hasSeasonOverride)
        {
            basis = $"season override to {overrideSeason} (derived: {basis})";
            season = overrideSeason;
        }
        if (hasEffortOverride)
        {
            basis = $"{basis}; effort override to {overrideEffort}";
            effort = overrideEffort;
        }

        return new ItemAvailability(season, effort, basis);
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter ItemAvailabilityTests`
Expected: 6 passed.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`
Expected: 1053 passed (1047 baseline plus the 6 new).

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear.Core/ItemAvailability.cs tests/TheLongestYear.Tests/ItemAvailabilityTests.cs
git commit -m "feat(availability): item availability record and lookup with override layers"
```

---

### Task 2: Location gating rules

**Files:**
- Create: `src/TheLongestYear.Core/Availability/LocationGating.cs`
- Test: `tests/TheLongestYear.Tests/FishAvailabilityTests.cs` (created here, extended in Task 4)

**Interfaces:**
- Consumes: `Season`.
- Produces: `static class LocationGating` with `Season FloorFor(string locationKey)` and `Season FloorForAny(IReadOnlyList<string> locationKeys)`.

A fish's spawn seasons say when it bites, not when the player can reach the water. The Desert needs the bus repaired, which needs the Vault bundle funded. The Sewer needs the Rusty Key, which needs 60 museum donations. `FloorForAny` returns the **easiest** of a set of locations, because reaching any one of them is enough.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/FishAvailabilityTests.cs`:

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class LocationGatingTests
{
    [Theory]
    [InlineData("Desert", Season.Fall)]
    [InlineData("Sewer", Season.Summer)]
    [InlineData("WitchSwamp", Season.Winter)]
    [InlineData("Mountain", Season.Spring)]
    [InlineData("Beach", Season.Spring)]
    [InlineData("UndergroundMine", Season.Spring)]
    public void A_Gated_Location_Carries_Its_Own_Season_Floor(string key, Season expected)
        => Assert.Equal(expected, LocationGating.FloorFor(key));

    [Fact]
    public void An_Unknown_Location_Is_Treated_As_Ungated()
        => Assert.Equal(Season.Spring, LocationGating.FloorFor("SomeModdedPlace"));

    /// <summary>Reaching ANY listed location is enough, so the easiest one wins.</summary>
    [Fact]
    public void The_Easiest_Location_In_A_Set_Wins()
        => Assert.Equal(Season.Spring,
            LocationGating.FloorForAny(new List<string> { "Desert", "Mountain" }));

    [Fact]
    public void An_Empty_Location_Set_Is_Ungated()
        => Assert.Equal(Season.Spring, LocationGating.FloorForAny(new List<string>()));
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter LocationGatingTests`
Expected: build error, `LocationGating` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/TheLongestYear.Core/Availability/LocationGating.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>The season floor a location imposes because the world locks it behind progress.
///
/// A fish's spawn seasons in Data/Locations say when it bites, not when the player can stand
/// next to that water. Without this, a Sandfish would read as Spring-available because the
/// Desert lists it in every season, and a Spring deadline on a Sandfish is unsatisfiable.
///
/// Values are judgements about a first-year run on this mod's 500g start, not vanilla speedrun
/// records. They lean LATE on purpose: BundleDeadlines clamps a deadline upward to the floor, so
/// too early permits an impossible gate while too late is merely lenient.</summary>
public static class LocationGating
{
    /// <summary>Matched as case-sensitive substrings of the location key, so "Desert",
    /// "SkullCave" and "IslandSouth" all catch their family of map keys.</summary>
    private static readonly (string Marker, Season Floor)[] GatedMarkers =
    {
        // Bus repair costs 40,000g through the Vault bundle. Not a Spring or Summer thing on a
        // 500g start with the board also demanding donations.
        ("Desert",     Season.Fall),
        ("SkullCave",  Season.Fall),
        // Rusty Key: 60 museum donations. Reachable mid-run by a player who digs, not before.
        ("Sewer",      Season.Summer),
        ("BugLand",    Season.Summer),
        // Witch's Swamp needs the Dark Talisman, which needs the Sewer first, then the Mutant
        // Bug Lair quest. Last stop of a long chain.
        ("WitchSwamp", Season.Winter),
        ("WitchHut",   Season.Winter),
    };

    public static Season FloorFor(string locationKey)
    {
        if (string.IsNullOrEmpty(locationKey))
            return Season.Spring;

        foreach ((string marker, Season floor) in GatedMarkers)
            if (locationKey.Contains(marker, StringComparison.Ordinal))
                return floor;

        return Season.Spring;
    }

    /// <summary>The EASIEST floor among the given locations, because reaching any one of them is
    /// enough to get the item. An empty list means no location signal, which reads as ungated.</summary>
    public static Season FloorForAny(IReadOnlyList<string> locationKeys)
    {
        if (locationKeys == null || locationKeys.Count == 0)
            return Season.Spring;

        Season best = Season.Winter;
        foreach (string key in locationKeys)
        {
            Season floor = FloorFor(key);
            if (floor < best) best = floor;
        }
        return best;
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter LocationGatingTests`
Expected: 10 passed.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Availability/LocationGating.cs tests/TheLongestYear.Tests/FishAvailabilityTests.cs
git commit -m "feat(availability): season floors for locations locked behind world progress"
```

---

### Task 3: Widen the Data/Fish read

**Files:**
- Modify: `src/TheLongestYear.Core/ItemPoolModel.cs` (add `RawFishEntry` next to the other `Raw*` records, around line 79)
- Modify: `src/TheLongestYear/Loop/GameDataPools.cs:90-95`
- Test: `tests/TheLongestYear.Tests/FishAvailabilityTests.cs` (append)

**Interfaces:**
- Produces: `sealed record RawFishEntry(string ItemId, bool IsTrap, int Difficulty, string RawTimeSpans, string Weather, int MaxDepth, int MinFishingLevel)` and `static RawFishEntry RawFishEntry.Parse(string itemId, string dataFishRow)`.

**Field indices are verified against the decompiled Android source**, `GameLocation.CheckGenericFishRequirements` in `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android\decompiled\StardewValley\StardewValley\GameLocation.cs` around line 13860. Do not trust any other ordering:

| Index | Meaning |
|---|---|
| 1 | difficulty, or the literal `trap` |
| 5 | time spans, space separated start/end pairs on a 600 to 2600 clock |
| 7 | weather: `rainy`, `sunny` or `both` |
| 9 | max depth |
| 12 | minimum fishing level |

`GameDataPools` reads `Data/Fish` today only to collect trap ids (`FishTrapFieldIndex = 1`, `FishTrapMarker = "trap"`). Keep that behaviour working; this task adds the rest of the row alongside it.

- [ ] **Step 1: Write the failing tests**

Append to `tests/TheLongestYear.Tests/FishAvailabilityTests.cs`:

```csharp
public class RawFishEntryTests
{
    // Vanilla Pufferfish row shape: name/difficulty/behavior/minSize/maxSize/times/seasons/
    // weather/unused/maxDepth/chance/depthMultiplier/minLevel/...
    private const string PufferfishRow =
        "Pufferfish/80/floater/1/36/1200 1600/summer/sunny/690 .4 .1/5/.4/.2/0";

    private const string LobsterTrapRow = "Lobster/trap/.05/688 .05/ocean/1/10";

    [Fact]
    public void A_Rod_Fish_Row_Parses_Every_Field_We_Gate_On()
    {
        RawFishEntry entry = RawFishEntry.Parse("128", PufferfishRow);

        Assert.Equal("128", entry.ItemId);
        Assert.False(entry.IsTrap);
        Assert.Equal(80, entry.Difficulty);
        Assert.Equal("1200 1600", entry.RawTimeSpans);
        Assert.Equal("sunny", entry.Weather);
        Assert.Equal(5, entry.MaxDepth);
        Assert.Equal(0, entry.MinFishingLevel);
    }

    [Fact]
    public void A_Trap_Row_Is_Flagged_And_Does_Not_Throw_On_Its_Short_Fields()
    {
        RawFishEntry entry = RawFishEntry.Parse("715", LobsterTrapRow);

        Assert.True(entry.IsTrap);
        Assert.Equal(0, entry.Difficulty);
    }

    [Fact]
    public void A_Malformed_Row_Degrades_Instead_Of_Throwing()
    {
        RawFishEntry entry = RawFishEntry.Parse("999", "Nonsense/notanumber");

        Assert.Equal("999", entry.ItemId);
        Assert.False(entry.IsTrap);
        Assert.Equal(0, entry.Difficulty);
    }

    [Fact]
    public void An_Empty_Row_Degrades_Instead_Of_Throwing()
    {
        RawFishEntry entry = RawFishEntry.Parse("999", "");

        Assert.Equal(0, entry.MinFishingLevel);
        Assert.Equal("", entry.Weather);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter RawFishEntryTests`
Expected: build error, `RawFishEntry` does not exist.

- [ ] **Step 3: Add the record to Core**

Add to `src/TheLongestYear.Core/ItemPoolModel.cs`, after `RawSpawnEntry`:

```csharp
/// <summary>One Data/Fish row, reduced to the fields the availability model gates on.
///
/// Field indices verified against the decompiled Android source, GameLocation.
/// CheckGenericFishRequirements: 1 = difficulty or the literal "trap", 5 = time spans,
/// 7 = weather, 9 = max depth, 12 = minimum fishing level. Do not reorder from memory.
///
/// Parse never throws. A row the game itself would reject degrades to zeros, and the fish then
/// scores as an easy year-round catch, which is the lenient direction.</summary>
public sealed record RawFishEntry(
    string ItemId, bool IsTrap, int Difficulty, string RawTimeSpans,
    string Weather, int MaxDepth, int MinFishingLevel)
{
    private const int DifficultyIndex = 1;
    private const int TimeSpansIndex = 5;
    private const int WeatherIndex = 7;
    private const int MaxDepthIndex = 9;
    private const int MinFishingLevelIndex = 12;
    private const string TrapMarker = "trap";

    public static RawFishEntry Parse(string itemId, string? row)
    {
        string[] fields = (row ?? "").Split('/');
        bool isTrap = Field(fields, DifficultyIndex) == TrapMarker;
        return new RawFishEntry(
            ItemId: itemId,
            IsTrap: isTrap,
            Difficulty: isTrap ? 0 : Int(fields, DifficultyIndex),
            RawTimeSpans: isTrap ? "" : Field(fields, TimeSpansIndex),
            Weather: isTrap ? "" : Field(fields, WeatherIndex),
            MaxDepth: isTrap ? 0 : Int(fields, MaxDepthIndex),
            MinFishingLevel: isTrap ? 0 : Int(fields, MinFishingLevelIndex));
    }

    private static string Field(string[] fields, int index)
        => index < fields.Length ? fields[index] : "";

    private static int Int(string[] fields, int index)
        => int.TryParse(Field(fields, index), out int value) ? value : 0;
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter RawFishEntryTests`
Expected: 4 passed.

- [ ] **Step 5: Widen the glue layer**

In `src/TheLongestYear/Loop/GameDataPools.cs`, add a collection next to the existing `trapIds` set:

```csharp
            var fishRows = new List<RawFishEntry>();
```

Replace the existing `Data/Fish` loop (currently at lines 90 to 95) with:

```csharp
                foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/Fish"))
                {
                    RawFishEntry entry = RawFishEntry.Parse(kv.Key, kv.Value);
                    fishRows.Add(entry);
                    if (entry.IsTrap)
                        trapIds.Add(kv.Key);
                }
```

Then expose the rows on the pools so the availability builder can reach them. Add to `ItemPools` in `src/TheLongestYear.Core/ItemPoolModel.cs`:

```csharp
    /// <summary>Raw Data/Fish rows keyed by UNQUALIFIED item id, for the availability model.
    /// The pools themselves carry qualified ids; this table mirrors the game's own keying.</summary>
    public IReadOnlyDictionary<string, RawFishEntry> FishRows { get; init; }
        = new Dictionary<string, RawFishEntry>();
```

In `src/TheLongestYear.Core/ItemPoolBuilder.cs`, add a `fishRows` parameter to `Build` (defaulting to null so existing test callers keep compiling) and set `FishRows = fishRows ?? new Dictionary<string, RawFishEntry>()` in the returned `ItemPools`. Pass `fishRows.ToDictionary(r => r.ItemId, StringComparer.Ordinal)` from `GameDataPools`.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`
Expected: 1067 passed, and specifically `ItemPoolBuilderTests` still green, which proves the trap id behaviour survived.

- [ ] **Step 7: Build the mod project**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo`
Expected: no errors.

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear.Core/ItemPoolModel.cs src/TheLongestYear.Core/ItemPoolBuilder.cs src/TheLongestYear/Loop/GameDataPools.cs tests/TheLongestYear.Tests/FishAvailabilityTests.cs
git commit -m "feat(availability): keep the Data/Fish fields the gate model needs"
```

---

### Task 4: Fish derivation

**Files:**
- Create: `src/TheLongestYear.Core/Availability/FishAvailability.cs`
- Test: `tests/TheLongestYear.Tests/FishAvailabilityTests.cs` (append)

**Interfaces:**
- Consumes: `PoolItem` (`ItemId`, `Price`, `Weight`, `Seasons`, `Locations`), `RawFishEntry`, `LocationGating`, `ItemAvailability`.
- Produces: `static class FishAvailability` with `ItemAvailability Derive(PoolItem item, RawFishEntry? row)`.

Rules. Floor is the later of the earliest spawn season and the easiest location's gating floor. Effort is a sum, each term named as a constant:

| Term | Value |
|---|---|
| difficulty band | `Difficulty / 20`, so a 0 to 100 scale becomes 0 to 5 |
| level band | `MinFishingLevel / 3`, so 0 to 10 becomes 0 to 3 |
| weather restriction | 2 if `rainy` or `sunny`, 0 if `both` or blank |
| narrow window | 2 if the total open window is under 8 game hours, 1 if under 14, else 0 |
| deep cast | 1 if `MaxDepth >= 4` |
| few seasons | 1 if the fish spawns in fewer than 2 seasons |

- [ ] **Step 1: Write the failing tests**

Append to `tests/TheLongestYear.Tests/FishAvailabilityTests.cs`:

```csharp
public class FishAvailabilityDeriveTests
{
    private static PoolItem Fish(
        string id, int price = 100, IReadOnlyList<Season>? seasons = null,
        IReadOnlyList<string>? locations = null)
        => new PoolItem(id, price, 1,
            seasons ?? new List<Season> { Season.Spring, Season.Summer, Season.Fall, Season.Winter },
            locations ?? new List<string> { "Mountain" });

    private static RawFishEntry Row(
        int difficulty = 30, string times = "600 2600", string weather = "both",
        int maxDepth = 0, int minLevel = 0)
        => new RawFishEntry("x", false, difficulty, times, weather, maxDepth, minLevel);

    [Fact]
    public void An_Easy_Year_Round_Fish_Floors_At_Spring_And_Scores_Low()
    {
        ItemAvailability result = FishAvailability.Derive(Fish("(O)145"), Row(difficulty: 15));

        Assert.Equal(Season.Spring, result.EarliestSeason);
        Assert.True(result.Effort <= 2, $"expected an easy score, got {result.Effort}");
    }

    [Fact]
    public void A_Summer_Only_Fish_Floors_At_Summer()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)128", seasons: new List<Season> { Season.Summer }), Row());

        Assert.Equal(Season.Summer, result.EarliestSeason);
    }

    /// <summary>Sandfish lists every season in the Desert, but the Desert needs a 40,000g bus
    /// repair. Spawn seasons alone would read this as Spring and put an unsatisfiable Spring
    /// deadline on it.</summary>
    [Fact]
    public void A_Desert_Fish_Inherits_The_Deserts_Floor_Not_Its_Spawn_Seasons()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)164", locations: new List<string> { "Desert" }), Row());

        Assert.Equal(Season.Fall, result.EarliestSeason);
        Assert.Contains("Desert", result.Basis);
    }

    [Fact]
    public void The_Later_Of_Spawn_Season_And_Location_Floor_Wins()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)164",
                seasons: new List<Season> { Season.Winter },
                locations: new List<string> { "Desert" }),
            Row());

        Assert.Equal(Season.Winter, result.EarliestSeason);
    }

    [Fact]
    public void A_Hard_Restricted_Fish_Outscores_An_Easy_One()
    {
        int easy = FishAvailability.Derive(Fish("(O)145"), Row(difficulty: 15)).Effort;
        int hard = FishAvailability.Derive(
            Fish("(O)128", seasons: new List<Season> { Season.Summer }),
            Row(difficulty: 80, times: "1200 1600", weather: "sunny", maxDepth: 5, minLevel: 0))
            .Effort;

        Assert.True(hard > easy, $"hard {hard} should outscore easy {easy}");
    }

    [Fact]
    public void A_Rainy_Only_Fish_Costs_More_Than_An_All_Weather_One()
    {
        int both = FishAvailability.Derive(Fish("(O)1"), Row(weather: "both")).Effort;
        int rainy = FishAvailability.Derive(Fish("(O)2"), Row(weather: "rainy")).Effort;

        Assert.Equal(both + 2, rainy);
    }

    [Fact]
    public void A_High_Level_Requirement_Raises_The_Score()
    {
        int low = FishAvailability.Derive(Fish("(O)1"), Row(minLevel: 0)).Effort;
        int high = FishAvailability.Derive(Fish("(O)2"), Row(minLevel: 9)).Effort;

        Assert.Equal(low + 3, high);
    }

    [Fact]
    public void A_Fish_With_No_Data_Row_Still_Gets_A_Floor_From_Its_Spawn_Data()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)9999", seasons: new List<Season> { Season.Fall }), row: null);

        Assert.Equal(Season.Fall, result.EarliestSeason);
        Assert.Contains("no Data/Fish row", result.Basis);
    }

    [Fact]
    public void The_Basis_Names_The_Season_And_The_Score()
    {
        ItemAvailability result = FishAvailability.Derive(
            Fish("(O)128", seasons: new List<Season> { Season.Summer }), Row());

        Assert.Contains("Summer", result.Basis);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter FishAvailabilityDeriveTests`
Expected: build error, `FishAvailability` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/TheLongestYear.Core/Availability/FishAvailability.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Earliest season and effort for a rod or trap fish.
///
/// Why this exists: the fish bundles re-roll their slots from a 52 item pool, while the old hand
/// written pin table named 15 specific fish. A re-rolled bundle was therefore gated only on
/// whichever slots happened to land on one of those 15, and roughly a quarter of boards came out
/// with no season pressure at all.</summary>
public static class FishAvailability
{
    private const int DifficultyBandSize = 20;
    private const int LevelBandSize = 3;
    private const int RestrictedWeatherCost = 2;
    private const int NarrowWindowCost = 2;
    private const int ShortWindowCost = 1;
    private const int DeepCastCost = 1;
    private const int FewSeasonsCost = 1;
    private const int DeepCastDepth = 4;
    private const int NarrowWindowHours = 8;
    private const int ShortWindowHours = 14;
    private const int FewSeasonsThreshold = 2;
    private const int FullDayHours = 24;

    /// <summary>Stardew's clock runs 600 to 2600 and the hundreds digit is the hour, so an hour
    /// is 100 units and the span arithmetic is plain subtraction.</summary>
    private const int ClockUnitsPerHour = 100;

    public static ItemAvailability Derive(PoolItem item, RawFishEntry? row)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        Season spawnFloor = item.Seasons.Count == 0 ? Season.Spring : item.Seasons.Min();
        Season locationFloor = LocationGating.FloorForAny(item.Locations);
        Season floor = spawnFloor > locationFloor ? spawnFloor : locationFloor;

        string locationNote = locationFloor > Season.Spring
            ? $", gated by location ({string.Join(", ", item.Locations)})"
            : "";

        if (row == null)
        {
            return new ItemAvailability(floor, ItemAvailabilityModel.UnrecognisedEffort,
                $"fish, no Data/Fish row, spawns {SeasonList(item.Seasons)}{locationNote}");
        }

        int effort =
            row.Difficulty / DifficultyBandSize
            + row.MinFishingLevel / LevelBandSize
            + WeatherCost(row.Weather)
            + WindowCost(row.RawTimeSpans)
            + (row.MaxDepth >= DeepCastDepth ? DeepCastCost : 0)
            + (item.Seasons.Count > 0 && item.Seasons.Count < FewSeasonsThreshold ? FewSeasonsCost : 0);

        return new ItemAvailability(floor, effort,
            $"fish, earliest {floor}, spawns {SeasonList(item.Seasons)}{locationNote}, "
            + $"difficulty {row.Difficulty}, level {row.MinFishingLevel}, weather {WeatherLabel(row.Weather)}, "
            + $"window {OpenHours(row.RawTimeSpans)}h, effort {effort}");
    }

    private static string SeasonList(IReadOnlyList<Season> seasons)
        => seasons.Count == 0 ? "any season" : string.Join("/", seasons);

    private static string WeatherLabel(string weather)
        => string.IsNullOrEmpty(weather) ? "any" : weather;

    private static int WeatherCost(string weather)
        => weather == "rainy" || weather == "sunny" ? RestrictedWeatherCost : 0;

    private static int WindowCost(string rawTimeSpans)
    {
        int hours = OpenHours(rawTimeSpans);
        if (hours < NarrowWindowHours) return NarrowWindowCost;
        if (hours < ShortWindowHours) return ShortWindowCost;
        return 0;
    }

    /// <summary>Total hours the fish is biting, summed over every start/end pair. A row with no
    /// parseable span reads as open all day, which is the lenient direction.</summary>
    private static int OpenHours(string rawTimeSpans)
    {
        string[] parts = (rawTimeSpans ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int units = 0;
        for (int i = 0; i + 1 < parts.Length; i += 2)
        {
            if (int.TryParse(parts[i], out int start) && int.TryParse(parts[i + 1], out int end)
                && end > start)
                units += end - start;
        }
        return units == 0 ? FullDayHours : units / ClockUnitsPerHour;
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter FishAvailabilityDeriveTests`
Expected: 9 passed.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Availability/FishAvailability.cs tests/TheLongestYear.Tests/FishAvailabilityTests.cs
git commit -m "feat(availability): derive fish floors and effort from spawn and Data/Fish rows"
```

---

### Task 5: Metals derivation

**Files:**
- Create: `src/TheLongestYear.Core/Availability/MetalsAvailability.cs`
- Test: `tests/TheLongestYear.Tests/MetalsAvailabilityTests.cs`

**Interfaces:**
- Consumes: `PoolItem`, `ItemAvailability`.
- Produces: `static class MetalsAvailability` with `ItemAvailability? Derive(PoolItem item)`, returning null for an id it does not recognise so the composer can fall through.

Ore depth tiers are **code facts, not data facts**, verified against `MineShaft.getAppropriateOre` and `MineShaft.getMineArea` in the decompiled Android source at `decompiled/StardewValley/StardewValley.Locations/MineShaft.cs` around line 1729: mine area 0 and 10 give copper, area 40 gives iron, area 80 gives gold, and area 121 (Skull Cavern, behind the desert bus) gives iridium.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/MetalsAvailabilityTests.cs`:

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class MetalsAvailabilityTests
{
    private static PoolItem Metal(string id, int price = 100)
        => new PoolItem(id, price, 1, new List<Season>(), new List<string>());

    [Theory]
    [InlineData("(O)378", Season.Spring)]   // Copper Ore, mine area 0
    [InlineData("(O)380", Season.Spring)]   // Iron Ore, mine area 40
    [InlineData("(O)384", Season.Summer)]   // Gold Ore, mine area 80
    [InlineData("(O)386", Season.Fall)]     // Iridium Ore, Skull Cavern behind the bus
    [InlineData("(O)334", Season.Spring)]   // Copper Bar
    [InlineData("(O)335", Season.Spring)]   // Iron Bar
    [InlineData("(O)336", Season.Summer)]   // Gold Bar
    [InlineData("(O)337", Season.Fall)]     // Iridium Bar
    public void Each_Metal_Floors_At_Its_Mine_Depth(string id, Season expected)
    {
        ItemAvailability? result = MetalsAvailability.Derive(Metal(id));

        Assert.NotNull(result);
        Assert.Equal(expected, result!.EarliestSeason);
    }

    [Fact]
    public void A_Bar_Costs_More_Effort_Than_Its_Ore()
    {
        int ore = MetalsAvailability.Derive(Metal("(O)378"))!.Effort;
        int bar = MetalsAvailability.Derive(Metal("(O)334"))!.Effort;

        Assert.True(bar > ore, $"bar {bar} should outscore ore {ore}");
    }

    [Fact]
    public void Deeper_Metal_Costs_More_Effort()
    {
        int copper = MetalsAvailability.Derive(Metal("(O)378"))!.Effort;
        int gold = MetalsAvailability.Derive(Metal("(O)384"))!.Effort;
        int iridium = MetalsAvailability.Derive(Metal("(O)386"))!.Effort;

        Assert.True(copper < gold);
        Assert.True(gold < iridium);
    }

    [Fact]
    public void An_Unrecognised_Id_Returns_Null_So_The_Composer_Falls_Through()
        => Assert.Null(MetalsAvailability.Derive(Metal("(O)9999")));

    [Fact]
    public void The_Basis_Explains_The_Depth()
    {
        ItemAvailability result = MetalsAvailability.Derive(Metal("(O)384"))!;

        Assert.Contains("80", result.Basis);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter MetalsAvailabilityTests`
Expected: build error, `MetalsAvailability` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/TheLongestYear.Core/Availability/MetalsAvailability.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Earliest season and effort for ore, bars and the other Metals pool items.
///
/// Mine depth is a CODE fact, not a data fact: MineShaft.getAppropriateOre switches on
/// getMineArea, giving copper in areas 0 and 10, iron in area 40, gold in area 80 and iridium in
/// area 121 (Skull Cavern, which is behind the 40,000g bus repair). Verified against the
/// decompiled Android source, decompiled/StardewValley/StardewValley.Locations/MineShaft.cs
/// around line 1729. There is no data table to read this from, so it lives here as a rule.
///
/// Season floors are judgements about a first-year run on a 500g start, leaning late on purpose:
/// a floor set too early permits an impossible deadline, a floor set too late is merely lenient.
/// Floor 41 is reachable in Spring by a player who commits to the mine. Floor 81 is not, so gold
/// floors at Summer. Skull Cavern needs the Vault bundle funded first, so iridium floors at Fall.</summary>
public static class MetalsAvailability
{
    private const int SmeltingCost = 2;

    private sealed record MetalRule(Season Floor, int Effort, string Basis);

    private static readonly IReadOnlyDictionary<string, MetalRule> Rules =
        new Dictionary<string, MetalRule>(StringComparer.Ordinal)
        {
            // Ore, straight off the mine floor.
            ["(O)378"] = new(Season.Spring, 1, "copper ore, mine area 0, floors 1 to 39"),
            ["(O)380"] = new(Season.Spring, 3, "iron ore, mine area 40, floors 41 to 79"),
            ["(O)384"] = new(Season.Summer, 5, "gold ore, mine area 80, floors 81 to 119"),
            ["(O)386"] = new(Season.Fall,   8, "iridium ore, mine area 121, Skull Cavern behind the bus repair"),

            // Bars: the ore, plus a furnace, plus the smelt.
            ["(O)334"] = new(Season.Spring, 1 + SmeltingCost, "copper bar, mine area 0 plus a furnace smelt"),
            ["(O)335"] = new(Season.Spring, 3 + SmeltingCost, "iron bar, mine area 40 plus a furnace smelt"),
            ["(O)336"] = new(Season.Summer, 5 + SmeltingCost, "gold bar, mine area 80 plus a furnace smelt"),
            ["(O)337"] = new(Season.Fall,   8 + SmeltingCost, "iridium bar, mine area 121 plus a furnace smelt"),

            // The rest of the Metals pool.
            ["(O)382"] = new(Season.Spring, 2, "coal, mine rocks and the occasional node, floors 1 to 39"),
            ["(O)338"] = new(Season.Spring, 3, "refined quartz, quartz from floor 1 plus a furnace smelt"),
            ["(O)881"] = new(Season.Summer, 4, "bone fragment, skeletons from mine area 80 and dig spots"),
        };

    /// <summary>Null means "not a metal this rule set knows", so the composer can try another
    /// domain or fall through to the unrecognised default.</summary>
    public static ItemAvailability? Derive(PoolItem item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (!Rules.TryGetValue(item.ItemId, out MetalRule? rule))
            return null;
        return new ItemAvailability(rule.Floor, rule.Effort,
            $"{rule.Basis}, earliest {rule.Floor}, effort {rule.Effort}");
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter MetalsAvailabilityTests`
Expected: 13 passed.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/Availability/MetalsAvailability.cs tests/TheLongestYear.Tests/MetalsAvailabilityTests.cs
git commit -m "feat(availability): derive ore and bar floors from verified mine depth tiers"
```

---

### Task 6: Compose the domains into a model

**Files:**
- Create: `src/TheLongestYear.Core/Availability/ItemAvailabilityBuilder.cs`
- Test: `tests/TheLongestYear.Tests/ItemAvailabilityTests.cs` (append)

**Interfaces:**
- Consumes: `ItemPools` (`Fish`, `CrabPot`, `Metals`, `FishRows`), `FishAvailability`, `MetalsAvailability`.
- Produces: `static class ItemAvailabilityBuilder` with `ItemAvailabilityModel Build(ItemPools pools, IReadOnlyDictionary<string, Season>? seasonOverrides = null, IReadOnlyDictionary<string, int>? effortOverrides = null)`.

Phase 1 composes Fish, CrabPot and Metals only. Pools not yet covered are simply absent from the derived dictionary, so their items fall through to the unrecognised default, which is safe by construction and gets closed in Phases 2 and 3.

- [ ] **Step 1: Write the failing tests**

Append to `tests/TheLongestYear.Tests/ItemAvailabilityTests.cs`:

```csharp
public class ItemAvailabilityBuilderTests
{
    private static PoolItem Item(string id, IReadOnlyList<Season>? seasons = null)
        => new PoolItem(id, 100, 1, seasons ?? new List<Season>(), new List<string> { "Mountain" });

    private static ItemPools Pools()
        => new ItemPools
        {
            Fish = new List<PoolItem> { Item("(O)128", new List<Season> { Season.Summer }) },
            Metals = new List<PoolItem> { Item("(O)384") },
            FishRows = new Dictionary<string, RawFishEntry>
            {
                ["128"] = new RawFishEntry("128", false, 80, "1200 1600", "sunny", 5, 0),
            },
        };

    /// <summary>The pools carry QUALIFIED ids while Data/Fish is keyed UNQUALIFIED, so the
    /// builder has to strip the prefix to join them. Getting this wrong silently produces a
    /// model where every fish looks like it has no data row.</summary>
    [Fact]
    public void A_Fish_Is_Joined_To_Its_Unqualified_Data_Row()
    {
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(Pools());

        ItemAvailability result = model.For("(O)128");

        Assert.Equal(Season.Summer, result.EarliestSeason);
        Assert.DoesNotContain("no Data/Fish row", result.Basis);
    }

    [Fact]
    public void A_Metal_Is_Derived_By_Its_Own_Rules()
    {
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(Pools());

        Assert.Equal(Season.Summer, model.For("(O)384").EarliestSeason);
    }

    [Fact]
    public void An_Item_From_A_Pool_Phase_1_Does_Not_Cover_Falls_Through_Safely()
    {
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(Pools());

        Assert.Equal(Season.Winter, model.For("(O)24").EarliestSeason);
    }

    [Fact]
    public void Overrides_Reach_The_Built_Model()
    {
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(
            Pools(),
            seasonOverrides: new Dictionary<string, Season> { ["(O)128"] = Season.Fall });

        Assert.Equal(Season.Fall, model.For("(O)128").EarliestSeason);
    }

    [Fact]
    public void An_Empty_Pool_Set_Builds_Without_Throwing()
    {
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(new ItemPools());

        Assert.Equal(Season.Winter, model.For("(O)1").EarliestSeason);
    }
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter ItemAvailabilityBuilderTests`
Expected: build error, `ItemAvailabilityBuilder` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/TheLongestYear.Core/Availability/ItemAvailabilityBuilder.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Composes the per-domain rules into one <see cref="ItemAvailabilityModel"/>.
///
/// Phase 1 covers Fish, CrabPot and Metals, which are the pools the re-rolled PerItem bundles
/// draw from and therefore the largest part of the season-gate leak. Items from pools not yet
/// covered are absent from the derived table and fall through to the model's unrecognised
/// default, which floors them at Winter. That is the safe direction while the remaining domains
/// land in later phases.</summary>
public static class ItemAvailabilityBuilder
{
    public static ItemAvailabilityModel Build(
        ItemPools pools,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null)
    {
        if (pools == null) throw new ArgumentNullException(nameof(pools));

        var derived = new Dictionary<string, ItemAvailability>(StringComparer.Ordinal);

        foreach (PoolItem item in pools.Fish ?? new List<PoolItem>())
            derived[item.ItemId] = FishAvailability.Derive(item, RowFor(pools, item.ItemId));

        foreach (PoolItem item in pools.CrabPot ?? new List<PoolItem>())
            derived[item.ItemId] = FishAvailability.Derive(item, RowFor(pools, item.ItemId));

        foreach (PoolItem item in pools.Metals ?? new List<PoolItem>())
        {
            ItemAvailability? metal = MetalsAvailability.Derive(item);
            if (metal != null)
                derived[item.ItemId] = metal;
        }

        return new ItemAvailabilityModel(derived, seasonOverrides, effortOverrides);
    }

    /// <summary>Pools carry qualified ids ("(O)128"); Data/Fish is keyed unqualified ("128").
    /// Strip the prefix to join them.</summary>
    private static RawFishEntry? RowFor(ItemPools pools, string qualifiedId)
    {
        string unqualified = BundleParsing.StripQualifier(qualifiedId);
        return pools.FishRows != null && pools.FishRows.TryGetValue(unqualified, out RawFishEntry? row)
            ? row
            : null;
    }
}
```

If `BundleParsing.StripQualifier` does not already exist, add it next to `NormalizeItemId` in `src/TheLongestYear.Core/BundleParsing.cs`:

```csharp
    /// <summary>"(O)128" to "128". Ids without a qualifier come back unchanged. The inverse of
    /// the qualifying half of <see cref="NormalizeItemId"/>, needed wherever Core joins its
    /// qualified ids against a raw game data table keyed by the game's unqualified ids.</summary>
    public static string StripQualifier(string itemId)
    {
        if (string.IsNullOrEmpty(itemId) || itemId[0] != '(') return itemId;
        int close = itemId.IndexOf(')');
        return close < 0 ? itemId : itemId.Substring(close + 1);
    }
```

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter ItemAvailabilityBuilderTests`
Expected: 5 passed.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`
Expected: all green.

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear.Core/Availability/ItemAvailabilityBuilder.cs src/TheLongestYear.Core/BundleParsing.cs tests/TheLongestYear.Tests/ItemAvailabilityTests.cs
git commit -m "feat(availability): compose fish, crab pot and metals into one model"
```

---

### Task 7: Deadlines from the model

**Files:**
- Create: `src/TheLongestYear.Core/BundleDeadlines.cs`
- Test: `tests/TheLongestYear.Tests/BundleDeadlinesTests.cs`

**Interfaces:**
- Consumes: `ItemAvailabilityModel`, `Season`.
- Produces: `static class BundleDeadlines` with `IReadOnlyDictionary<string, Season> For(IReadOnlyList<string> ingredients, ItemAvailabilityModel model)`.

The rule, from spec section 4:

1. Rank by effort ascending, ties broken by ordinal item id so the result is deterministic.
2. Checkpoint index: for four or fewer ingredients, back the spread against Winter with `4 - count + rank`, so two ingredients land on Fall and Winter. For more than four, spread proportionally with `rank * 4 / count`.
3. Effort weighting: at or above `HighEffortThreshold` slide one checkpoint later; at or below `TrivialEffortThreshold` slide one earlier. Clamp into range.
4. Clamp upward to the item's floor. This is the step that makes an impossible deadline unrepresentable.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/BundleDeadlinesTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleDeadlinesTests
{
    private static ItemAvailabilityModel Model(params (string Id, Season Floor, int Effort)[] items)
        => new ItemAvailabilityModel(
            items.ToDictionary(
                i => i.Id,
                i => new ItemAvailability(i.Floor, i.Effort, "test"),
                System.StringComparer.Ordinal));

    [Fact]
    public void Four_Easy_Items_Spread_One_Per_Checkpoint_Easiest_First()
    {
        var model = Model(
            ("(O)a", Season.Spring, 1),
            ("(O)b", Season.Spring, 2),
            ("(O)c", Season.Spring, 3),
            ("(O)d", Season.Spring, 4));

        var result = BundleDeadlines.For(
            new List<string> { "(O)d", "(O)c", "(O)b", "(O)a" }, model);

        Assert.Equal(Season.Spring, result["(O)a"]);
        Assert.Equal(Season.Summer, result["(O)b"]);
        Assert.Equal(Season.Fall, result["(O)c"]);
        Assert.Equal(Season.Winter, result["(O)d"]);
    }

    /// <summary>Helper's has two ingredients. A two item bundle backs against Winter rather than
    /// starting at Spring, so it asks at Fall and Winter.</summary>
    [Fact]
    public void Two_Items_Land_On_Fall_And_Winter()
    {
        var model = Model(
            ("(O)a", Season.Spring, 1),
            ("(O)b", Season.Spring, 2));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)b" }, model);

        Assert.Equal(Season.Fall, result["(O)a"]);
        Assert.Equal(Season.Winter, result["(O)b"]);
    }

    [Fact]
    public void Three_Items_Land_On_Summer_Fall_And_Winter()
    {
        var model = Model(
            ("(O)a", Season.Spring, 1),
            ("(O)b", Season.Spring, 2),
            ("(O)c", Season.Spring, 3));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)b", "(O)c" }, model);

        Assert.Equal(Season.Summer, result["(O)a"]);
        Assert.Equal(Season.Fall, result["(O)b"]);
        Assert.Equal(Season.Winter, result["(O)c"]);
    }

    [Fact]
    public void One_Item_Is_Due_At_Winter()
    {
        var model = Model(("(O)a", Season.Spring, 1));

        var result = BundleDeadlines.For(new List<string> { "(O)a" }, model);

        Assert.Equal(Season.Winter, result["(O)a"]);
    }

    [Fact]
    public void Six_Items_Spread_Proportionally_Across_The_Four_Checkpoints()
    {
        var model = Model(
            ("(O)a", Season.Spring, 1), ("(O)b", Season.Spring, 2), ("(O)c", Season.Spring, 3),
            ("(O)d", Season.Spring, 4), ("(O)e", Season.Spring, 5), ("(O)f", Season.Spring, 6));

        var result = BundleDeadlines.For(
            new List<string> { "(O)a", "(O)b", "(O)c", "(O)d", "(O)e", "(O)f" }, model);

        Assert.Equal(Season.Spring, result["(O)a"]);
        Assert.Equal(Season.Spring, result["(O)b"]);
        Assert.Equal(Season.Summer, result["(O)c"]);
        Assert.Equal(Season.Fall, result["(O)d"]);
        Assert.Equal(Season.Fall, result["(O)e"]);
        Assert.Equal(Season.Winter, result["(O)f"]);
    }

    /// <summary>The load-bearing safety property: a deadline may never precede the season in
    /// which the item can first exist. This is the invariant whose absence made a Fall Foraging
    /// bundle unsatisfiable at its own gate.</summary>
    [Fact]
    public void A_Deadline_Never_Precedes_The_Items_Floor()
    {
        var model = Model(
            ("(O)a", Season.Winter, 1),
            ("(O)b", Season.Spring, 2),
            ("(O)c", Season.Spring, 3),
            ("(O)d", Season.Spring, 4));

        var result = BundleDeadlines.For(
            new List<string> { "(O)a", "(O)b", "(O)c", "(O)d" }, model);

        Assert.Equal(Season.Winter, result["(O)a"]);
    }

    [Fact]
    public void A_High_Effort_Item_Slides_One_Checkpoint_Later()
    {
        var model = Model(
            ("(O)a", Season.Spring, 0),
            ("(O)b", Season.Spring, BundleDeadlines.HighEffortThreshold));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)b" }, model);

        // Base spread for two items is Fall then Winter; the easy one slides earlier and the
        // hard one is already at the last checkpoint.
        Assert.Equal(Season.Summer, result["(O)a"]);
        Assert.Equal(Season.Winter, result["(O)b"]);
    }

    [Fact]
    public void Every_Ingredient_Is_Due_By_Winter_At_The_Latest()
    {
        var model = Model(
            ("(O)a", Season.Winter, 99),
            ("(O)b", Season.Winter, 99));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)b" }, model);

        Assert.All(result.Values, s => Assert.True(s <= Season.Winter));
    }

    [Fact]
    public void The_Result_Is_Deterministic_Regardless_Of_Input_Order()
    {
        var model = Model(
            ("(O)a", Season.Spring, 5),
            ("(O)b", Season.Spring, 5),
            ("(O)c", Season.Spring, 5),
            ("(O)d", Season.Spring, 5));

        var forward = BundleDeadlines.For(
            new List<string> { "(O)a", "(O)b", "(O)c", "(O)d" }, model);
        var reversed = BundleDeadlines.For(
            new List<string> { "(O)d", "(O)c", "(O)b", "(O)a" }, model);

        Assert.Equal(forward["(O)a"], reversed["(O)a"]);
        Assert.Equal(forward["(O)d"], reversed["(O)d"]);
    }

    [Fact]
    public void Every_Ingredient_Gets_A_Deadline()
    {
        var model = Model(("(O)a", Season.Spring, 1));

        var result = BundleDeadlines.For(new List<string> { "(O)a", "(O)unknown" }, model);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void An_Empty_Ingredient_List_Returns_An_Empty_Map()
        => Assert.Empty(BundleDeadlines.For(new List<string>(), Model()));
}
```

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter BundleDeadlinesTests`
Expected: build error, `BundleDeadlines` does not exist.

- [ ] **Step 3: Write the implementation**

Create `src/TheLongestYear.Core/BundleDeadlines.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Turns a PerItem bundle's ingredient list into a per-ingredient season deadline.
///
/// Replaces the hand written GameplayConfig.DefaultItemSeasonPins lookup, which covered 40 items
/// across 12 bundles and left every other ingredient with no deadline at all. An ingredient with
/// no deadline applies no checkpoint pressure, so a bundle whose ingredients were all unlisted
/// could be ignored for three seasons. The engine re-rolls eight of those 12 bundles from pools
/// far larger than the table, so most re-rolled boards were partly or wholly ungated.
///
/// Pacing is Jeff's ruling of 2026-08-27: an even spread across the four checkpoints, easiest
/// first, weighted so a hard item slides later and a trivial one slides earlier.</summary>
public static class BundleDeadlines
{
    private const int CheckpointCount = 4;

    /// <summary>At or above this effort an ingredient slides one checkpoint later.</summary>
    public const int HighEffortThreshold = 8;

    /// <summary>At or below this effort an ingredient slides one checkpoint earlier.</summary>
    public const int TrivialEffortThreshold = 1;

    public static IReadOnlyDictionary<string, Season> For(
        IReadOnlyList<string> ingredients, ItemAvailabilityModel model)
    {
        if (ingredients == null) throw new ArgumentNullException(nameof(ingredients));
        if (model == null) throw new ArgumentNullException(nameof(model));

        var result = new Dictionary<string, Season>(StringComparer.Ordinal);
        if (ingredients.Count == 0)
            return result;

        // Rank easiest first. The id tiebreak keeps the output reproducible from a seed, which
        // matters because a held or reshuffled board must classify the same way twice.
        List<(string Id, ItemAvailability Availability)> ranked = ingredients
            .Select(id => (Id: id, Availability: model.For(id)))
            .OrderBy(pair => pair.Availability.Effort)
            .ThenBy(pair => pair.Id, StringComparer.Ordinal)
            .ToList();

        for (int rank = 0; rank < ranked.Count; rank++)
        {
            (string id, ItemAvailability availability) = ranked[rank];

            int index = BaseCheckpoint(rank, ranked.Count);
            index += EffortShift(availability.Effort);
            index = Math.Clamp(index, 0, CheckpointCount - 1);

            var deadline = (Season)index;
            // The safety step. A deadline earlier than the season the item can first exist in is
            // unsatisfiable, and an unsatisfiable gate loses the year every loop.
            if (availability.EarliestSeason > deadline)
                deadline = availability.EarliestSeason;

            result[id] = deadline;
        }

        return result;
    }

    /// <summary>A bundle with four or fewer ingredients backs its spread against Winter, so two
    /// ingredients land on Fall and Winter rather than Spring and Fall. A larger bundle spreads
    /// proportionally across the four checkpoints.</summary>
    private static int BaseCheckpoint(int rank, int count)
        => count <= CheckpointCount
            ? CheckpointCount - count + rank
            : rank * CheckpointCount / count;

    private static int EffortShift(int effort)
    {
        if (effort >= HighEffortThreshold) return 1;
        if (effort <= TrivialEffortThreshold) return -1;
        return 0;
    }
}
```

- [ ] **Step 4: Run the tests and verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter BundleDeadlinesTests`
Expected: 11 passed.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/BundleDeadlines.cs tests/TheLongestYear.Tests/BundleDeadlinesTests.cs
git commit -m "feat(gates): per-ingredient deadlines spread by effort and clamped to obtainability"
```

---

### Task 8: Wire deadlines into the classifier

**Files:**
- Modify: `src/TheLongestYear.Core/BundleClassifier.cs` (the PerItem branch, currently lines 145 to 160)
- Modify: `src/TheLongestYear/Donations/BundleCatalogBuilder.cs` (constructor and the `Classify` call at line 141)
- Modify: `src/TheLongestYear/ModEntry.cs` (build the model at line 421 area, pass it into the builder at line 437 area)
- Modify: `src/TheLongestYear.Core/GeneratedBundleSet.cs` (`BuildRequirements` signature)
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs:602`
- Test: `tests/TheLongestYear.Tests/BundleClassifierTests.cs` (append)

**Interfaces:**
- `BundleClassifier.Classify` gains a trailing optional parameter `ItemAvailabilityModel? availability = null`. When it is null the classifier keeps using `itemSeasonPins` exactly as today, so every existing test and caller compiles and passes unchanged. When it is supplied, the PerItem branch calls `BundleDeadlines.For` instead of the pin lookup.

The optional parameter is deliberate: it keeps this task small enough to review, and Phase 4 removes the old path once every caller passes a model.

- [ ] **Step 1: Write the failing tests**

Append to `tests/TheLongestYear.Tests/BundleClassifierTests.cs`:

```csharp
public class BundleClassifierAvailabilityTests
{
    private static ParsedBundle Bundle(string name, params string[] ingredientIds)
        => new ParsedBundle
        {
            Name = name,
            NumberOfSlots = ingredientIds.Length,
            Ingredients = ingredientIds
                .Select(id => new BundleIngredient(id, 1, 0))
                .ToList(),
        };

    private static ItemAvailabilityModel Model(params (string Id, Season Floor, int Effort)[] items)
        => new ItemAvailabilityModel(
            items.ToDictionary(
                i => i.Id,
                i => new ItemAvailability(i.Floor, i.Effort, "test"),
                System.StringComparer.Ordinal));

    /// <summary>The bug this whole change exists to fix: a PerItem bundle whose ingredients are
    /// absent from the pin table gates on nothing until the Winter win check.</summary>
    [Fact]
    public void Without_A_Model_An_Unpinned_PerItem_Bundle_Is_Still_Ungated()
    {
        BundleRequirement? req = BundleClassifier.Classify(
            Bundle("Helper's", "(O)9999", "(O)9998"), Theme.None,
            new Dictionary<string, Season>(), new Dictionary<string, int[]>());

        Assert.NotNull(req);
        Assert.Equal(BundleKind.PerItem, req!.Kind);
        Assert.Empty(req.ItemSeasonPins);
    }

    [Fact]
    public void With_A_Model_Every_Ingredient_Gets_A_Deadline()
    {
        var model = Model(
            ("(O)9999", Season.Spring, 2),
            ("(O)9998", Season.Spring, 4));

        BundleRequirement? req = BundleClassifier.Classify(
            Bundle("Helper's", "(O)9999", "(O)9998"), Theme.None,
            new Dictionary<string, Season>(), new Dictionary<string, int[]>(),
            availability: model);

        Assert.NotNull(req);
        Assert.Equal(BundleKind.PerItem, req!.Kind);
        Assert.Equal(2, req.ItemSeasonPins.Count);
        Assert.Equal(Season.Fall, req.ItemSeasonPins["(O)9999"]);
        Assert.Equal(Season.Winter, req.ItemSeasonPins["(O)9998"]);
    }

    [Fact]
    public void With_A_Model_The_Bundle_Demands_Something_Before_Winter()
    {
        var model = Model(
            ("(O)9999", Season.Spring, 2),
            ("(O)9998", Season.Spring, 4));

        BundleRequirement req = BundleClassifier.Classify(
            Bundle("Helper's", "(O)9999", "(O)9998"), Theme.None,
            new Dictionary<string, Season>(), new Dictionary<string, int[]>(),
            availability: model)!;

        Assert.True(req.DemandAtSeason(Season.Fall) > 0,
            "an all-of-them bundle must apply pressure before the Winter win check");
    }

    [Fact]
    public void A_Model_Does_Not_Change_A_Seasonal_Bundle()
    {
        var model = Model(("(O)16", Season.Spring, 1));

        BundleRequirement req = BundleClassifier.Classify(
            Bundle("Spring Foraging", "(O)16"), Theme.None,
            new Dictionary<string, Season>(), new Dictionary<string, int[]>(),
            availability: model)!;

        Assert.Equal(BundleKind.Seasonal, req.Kind);
    }
}
```

Add `using System.Linq;` to the top of `BundleClassifierTests.cs` if it is not already there.

- [ ] **Step 2: Run the tests and verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter BundleClassifierAvailabilityTests`
Expected: build error, `Classify` has no `availability` parameter.

- [ ] **Step 3: Change the classifier**

In `src/TheLongestYear.Core/BundleClassifier.cs`, add the parameter to `Classify`:

```csharp
    /// <param name="availability">Derived item model. When supplied, the PerItem branch computes
    /// deadlines with <see cref="BundleDeadlines"/> instead of looking ingredients up in
    /// <paramref name="itemSeasonPins"/>. Null keeps the legacy pin-table behaviour, which exists
    /// only until every caller passes a model (Phase 4 of the availability spec).</param>
    public static BundleRequirement? Classify(
        ParsedBundle parsed, Theme theme,
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas,
        ItemAvailabilityModel? availability = null)
```

Replace the body of the PerItem branch (the `if (parsed.NumberOfSlots >= ingredients.Count)` block) with:

```csharp
        if (parsed.NumberOfSlots >= ingredients.Count)
        {
            Dictionary<string, Season> pins = new();
            if (availability != null)
            {
                // Derived model: every ingredient gets a deadline, spread by effort and clamped
                // up to the season it can first exist in. No ingredient can fall through
                // ungated, which is the whole point of the change.
                foreach (KeyValuePair<string, Season> deadline
                         in BundleDeadlines.For(ingredients, availability))
                    pins[deadline.Key] = deadline.Value;
            }
            else
            {
                // Legacy path: only ingredients named in the hand written table gate anything.
                foreach (string id in ingredients)
                    if (itemSeasonPins.TryGetValue(id, out Season s))
                        pins[id] = s;
            }
            return BundleRequirement.CreatePerItem(name, theme, ingredients, pins,
                ingredientStacks, ingredientQualities);
        }
```

- [ ] **Step 4: Run the new tests and verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo --filter BundleClassifierAvailabilityTests`
Expected: 4 passed.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`
Expected: all green. The optional parameter means no existing test changes behaviour.

- [ ] **Step 6: Build the model at load and pass it down**

In `src/TheLongestYear/ModEntry.cs`, immediately after `enginePools` is built (currently around line 432), add:

```csharp
            // Derived item model: earliest-possible season and effort per item, from the same
            // live pools the engine generates from. Curated pins ride along as season overrides.
            _availability = TheLongestYear.Core.Availability.ItemAvailabilityBuilder.Build(
                enginePools, seasonOverrides: itemSeasonPins);
            this.Monitor.Log(
                $"Item availability model built from live pools; "
                + $"{_availability.UnrecognisedIds.Count} id(s) unrecognised so far.",
                LogLevel.Trace);
```

Declare the field alongside the other services near the top of the class:

```csharp
        private TheLongestYear.Core.ItemAvailabilityModel _availability;
```

Pass it into `BundleCatalogBuilder`'s constructor as a new trailing optional parameter, store it, and forward it at the `BundleClassifier.Classify` call in `src/TheLongestYear/Donations/BundleCatalogBuilder.cs:141`:

```csharp
                    req = BundleClassifier.Classify(bundle, theme, _itemSeasonPins, _bundleQuotas, _availability);
```

Do the same for the engine path: add an optional `ItemAvailabilityModel? availability = null` parameter to `GeneratedBundleSet.BuildRequirements`, forward it into its `Classify` call, and pass `_availability` from `src/TheLongestYear/Loop/WorldResetService.cs:602` and from the `BuildRequirements` calls in `ModEntry.cs` at lines 2210, 2709 and 2737. `WorldResetService` needs the model handed to its constructor the same way `_itemSeasonPins` already is.

- [ ] **Step 7: Build and run the whole suite**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo`
Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`
Expected: no build errors, all tests green.

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear.Core/BundleClassifier.cs src/TheLongestYear.Core/GeneratedBundleSet.cs src/TheLongestYear/Donations/BundleCatalogBuilder.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/Loop/WorldResetService.cs tests/TheLongestYear.Tests/BundleClassifierTests.cs
git commit -m "feat(gates): PerItem bundles gate on the derived model instead of the pin table"
```

---

### Task 9: Diagnostics

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (register the command next to the existing `tly_gatecheck` registration)
- Test: manual, in game

**Interfaces:**
- Consumes: `_availability`, `_requirements`.
- Produces: console command `tly_itemmodel <itemId|bundleName>`.

- [ ] **Step 1: Find the existing command registrations**

Run: `grep -n "tly_gatecheck\|tly_dumpbundles" src/TheLongestYear/ModEntry.cs`

Register the new command in the same place and in the same style as those two.

- [ ] **Step 2: Add the command**

```csharp
            helper.ConsoleCommands.Add("tly_itemmodel",
                "Print the derived availability model for one item id or every ingredient of a bundle.\n\n"
                + "Usage: tly_itemmodel <itemId|bundleName>",
                (cmd, args) =>
                {
                    if (_availability == null)
                    {
                        this.Monitor.Log("No availability model yet; load a save first.", LogLevel.Warn);
                        return;
                    }
                    if (args.Length == 0)
                    {
                        this.Monitor.Log("Usage: tly_itemmodel <itemId|bundleName>", LogLevel.Info);
                        return;
                    }

                    string target = string.Join(" ", args);
                    BundleRequirement req = _requirements?
                        .FirstOrDefault(r => string.Equals(r.Name, target, StringComparison.OrdinalIgnoreCase));

                    if (req != null)
                    {
                        this.Monitor.Log($"Bundle '{req.Name}' ({req.Kind}):", LogLevel.Info);
                        foreach (string id in req.Ingredients)
                        {
                            TheLongestYear.Core.ItemAvailability a = _availability.For(id);
                            string due = req.ItemSeasonPins != null
                                && req.ItemSeasonPins.TryGetValue(id, out TheLongestYear.Core.Season d)
                                ? d.ToString()
                                : "never";
                            this.Monitor.Log(
                                $"  {id}: due {due}; earliest {a.EarliestSeason}, effort {a.Effort} [{a.Basis}]",
                                LogLevel.Info);
                        }
                        return;
                    }

                    string itemId = target.StartsWith("(", StringComparison.Ordinal) ? target : $"(O){target}";
                    TheLongestYear.Core.ItemAvailability single = _availability.For(itemId);
                    this.Monitor.Log(
                        $"{itemId}: earliest {single.EarliestSeason}, effort {single.Effort} [{single.Basis}]",
                        LogLevel.Info);
                });
```

- [ ] **Step 3: Add the Basis to gatecheck output**

Find the `tly_gatecheck` implementation and, wherever it names a blocking ingredient, append `_availability.For(id).Basis` so a flagged gate explains itself. Keep the existing IMPOSSIBLE and FREE flags and the calendar-only caveat line exactly as they are.

- [ ] **Step 4: Build and deploy**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo`
Run: `pwsh -NoProfile -File tools/deploy.ps1`

A running Stardew locks the DLL, so close the game first or the deploy fails.

- [ ] **Step 5: Verify in game**

Load a save. Remember that an unfocused Stardew is a paused Stardew, so queued console commands never run until the window has focus. Send commands with `tools/send-smapi-command.ps1`. The live log is `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`; the copy in the repo root is stale.

Run in order and record the output in the plan file under Task 10:

```
tly_itemmodel 128
tly_itemmodel Helper's
tly_itemmodel River Fish
tly_gatecheck
```

Expected: Pufferfish reports a Summer floor with a high effort score; Helper's and River Fish now show a due season on every ingredient rather than "never".

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs
git commit -m "feat(diagnostics): tly_itemmodel, and gatecheck explains its blocking ingredients"
```

---

### Task 10: Verify the balance shift on live boards

**Files:**
- Modify: `docs/superpowers/plans/2026-08-27-derived-item-availability-phase-1.md` (record results here)
- Modify: `STATUS.md`

This task produces evidence, not code. The spec's whole risk section rests on it.

- [ ] **Step 1: Capture the before state**

Before this plan's changes are deployed, or from a build of commit `c9f0df0`, run `tly_gatecheck` on at least three different boards and save the output to the session scratchpad. If that build is gone, note it and compare against the checked-in `docs/engine-bundle-catalogue.md` instead.

- [ ] **Step 2: Capture the after state at Normal**

With the new build deployed, reset to generate a fresh board and run `tly_gatecheck`. Repeat for three boards.

Record for each board: how many bundles were ungated before and after, and any IMPOSSIBLE flag.

- [ ] **Step 3: Capture the after state at Hard**

Set the required-slots and stack-size difficulty modifiers to Hard in `config.json`, reset (a change takes effect at the next reset, not mid-run), and run `tly_gatecheck` again on three boards.

Note that a reset RENAMES the save folder, because the folder name embeds `uniqueIDForThisGame` and the reset re-seeds it. Re-read the folder list after any reset before loading.

- [ ] **Step 4: Judge the results**

Any IMPOSSIBLE flag is a stop-the-line defect. The likely cause is a floor set too early in `LocationGating` or `MetalsAvailability`. Fix the floor, do not loosen the deadline rule.

A FREE flag on a Phase 1 bundle (fish or metals) is also a defect: it means an ingredient still fell through to the unrecognised default. A FREE flag on any other bundle is expected until Phases 2 and 3.

- [ ] **Step 5: Record the outcome**

Append a "Results" section to this plan file with the before and after numbers, then update `STATUS.md` to say that the PerItem gate baseline has shifted and that Normal is harder than the 0.12 release, so the next release notes have to say so.

- [ ] **Step 6: Commit**

```bash
git add docs/superpowers/plans/2026-08-27-derived-item-availability-phase-1.md STATUS.md
git commit -m "docs: record the Phase 1 gate baseline shift measured on live boards"
```

---

## Self-Review Notes

Checked against the spec:

- Spec 3.1 (floors), 3.2 (effort), 3.3 (data sources): Tasks 2 to 5 cover the Fish and Metals rows of the domain table. The remaining eleven domain rows are explicitly deferred to Phases 2 and 3 and named in the Scope section.
- Spec section 4 (deadlines): Task 7, including the upward clamp and the small-bundle back-loading.
- Spec section 5 (config and compatibility): the season override layer is in Task 1 and wired in Task 8. `ItemEffortOverrides` as a config key is deferred to Phase 4, when the curated pins move wholesale; the model already accepts effort overrides, so no rework is needed.
- Spec section 6 (diagnostics): Task 9. The generated `docs/item-availability-model.md` is Phase 4, since it should document the complete model rather than a quarter of it.
- Spec section 7 (testing): the floor invariant, the Winter-at-latest invariant, determinism, spread at several counts, and override precedence all have named tests. The characterisation test over the twelve currently pinned bundles is deferred to Phase 4, when the old path is deleted and a diff is meaningful.
- Spec section 9 (risks): Task 10 is the mitigation for the balance risk.

Two things the executor should expect to hit:

1. `ItemPoolBuilder.Build` has many callers in `ItemPoolBuilderTests`. Adding the `fishRows` parameter with a null default keeps them compiling. If any caller uses positional arguments past that point, add the parameter last.
2. `GeneratedBundleSet.BuildRequirements` is called from four places. Task 8 names all four. Missing one is silent: that path keeps the legacy behaviour and the bundles it produces stay ungated.
