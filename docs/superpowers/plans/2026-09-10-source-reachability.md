# Source Reachability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep provably unreachable items off the Community Center board by tracing how each item is actually obtained, so no mod ever needs naming in an exclusion list.

**Architecture:** A new pure rule in `TheLongestYear.Core` decides whether an item is provably out of reach. It flood-fills the world's warp graph from the farm (refusing to enter locations matched by `ExcludedLocationMarkers`) to learn which places are unreachable, then walks each item's sources (shop listing, crop seed, cooking recipe) to see whether every route to it passes through one of those places. The resulting id set is merged into the `excluded` set that `ItemPoolBuilder.Build` already threads through all thirteen pools. A separate load-time service repairs boards generated before the fix.

**Tech Stack:** C# / .NET 6, SMAPI 4.0+, Harmony, xunit 2.4.1. `TheLongestYear.Core` is `Nullable enable`, `ImplicitUsings disable` (write explicit `using` lines). The mod project `TheLongestYear` is `Nullable disable`.

**Spec:** `docs/superpowers/specs/2026-09-10-source-reachability-design.md`

## Global Constraints

- Target release **0.18**. **This work happens on a feature branch, so the manifest `Version` stays at its branch-point value of `0.17.15` and is NEVER bumped by a task.** The workspace rule is explicit: only the release line owns version bumps, because a `Version` line changed on two branches conflicts on every merge. Master sets `0.18.0` once, at merge time (Task 11). Accumulate changelog entries under a single `## Unreleased` heading at the top of `CHANGELOG.md`; Task 11 retitles it. (Controller ruling, 2026-09-10, reconciling the plan with the workspace CLAUDE.md.)
- **Never push and never release without Jeff's explicit "yes".** Local commits only.
- **No em dashes** in any string, log line, comment or doc. Purge on sight.
- The test project references **only** `TheLongestYear.Core`. Anything needing `Game1` cannot be unit-tested, so put logic in Core and keep the mod-project layer a thin data reader.
- Qualified item ids throughout (`(O)412`). Normalise with `BundleParsing.NormalizeItemId`.
- **Never write the derived unreachable set into `config.json`.** A saved config overrides serialized list defaults wholesale (Nexus 1122358). This set is derived per generation and stays in memory.
- Conservative rule: an item is dropped **only** when every known source is unreachable. Untraceable means allowed.
- Applies at **every** difficulty step. Unlike `YearTwoCrops`, this is impossibility, not pacing.
- Run `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj` before every commit. Baseline is 2004 passing.
- Close the game before building: the deployed `TheLongestYear.Core.dll` is locked while SMAPI runs, and the build fails with `IOException`.

## File Structure

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/Availability/ReachabilityGraph.cs` | **Create.** Pure warp-graph flood fill. Knows nothing about items. |
| `src/TheLongestYear.Core/Availability/SourceReachability.cs` | **Create.** The source rules and the memoised verdict per item id. |
| `src/TheLongestYear.Core/ItemPoolModel.cs` | **Modify.** Add `RawLocationLink`, `RawShopListing`, `RawShopPlacement`, `RawRecipeEntry`; add `SeedItemId` to `RawCropEntry`. |
| `src/TheLongestYear.Core/ItemPoolBuilder.cs` | **Modify.** Accept the unreachable set and merge it into `excluded`. |
| `src/TheLongestYear/Loop/GameDataPools.cs` | **Modify.** Read `Data/Shops`, `Data/CookingRecipes`, live warps; populate the new records. |
| `src/TheLongestYear/Loop/BoardRepairService.cs` | **Create.** Load-time board repair. |
| `src/TheLongestYear/ModEntry.cs` | **Modify.** Register `tly_warpgraph`, wire the repair on save load, report drops in `tly_dumpbundles`. |
| `tests/TheLongestYear.Tests/ReachabilityGraphTests.cs` | **Create.** |
| `tests/TheLongestYear.Tests/SourceReachabilityTests.cs` | **Create.** |
| `tests/TheLongestYear.Tests/Fixtures/fishmonger_sources.json` | **Create.** Regression fixture from the real pack. |

---

### Task 1: Prove the warp graph is visible at generation time

The spec's highest risk: custom locations are created on load, and the board is generated during a reset. If custom maps or their warps are not present at that moment, the whole design fails. Find out before building on it.

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (register one console command near the other `tly_dump*` registrations, around line 281)

**Interfaces:**
- Consumes: nothing.
- Produces: console command `tly_warpgraph`, kept permanently as a diagnostic.

- [ ] **Step 1: Register the command**

In `ModEntry.Entry`, beside the other `helper.ConsoleCommands.Add` calls:

```csharp
helper.ConsoleCommands.Add(
    "tly_warpgraph",
    "Print every loaded location and its warp targets, for verifying reachability derivation. Usage: tly_warpgraph [filter]",
    this.CmdWarpGraph);
```

- [ ] **Step 2: Implement it**

```csharp
private void CmdWarpGraph(string command, string[] args)
{
    string filter = args.Length > 0 ? args[0] : null;
    int locations = 0, edges = 0;
    var lines = new System.Collections.Generic.List<string>();
    foreach (GameLocation location in Game1.locations)
    {
        if (location?.Name == null) continue;
        locations++;
        var targets = new System.Collections.Generic.List<string>();
        foreach (StardewValley.Warp warp in location.warps)
        {
            if (string.IsNullOrEmpty(warp?.TargetName)) continue;
            edges++;
            if (!targets.Contains(warp.TargetName)) targets.Add(warp.TargetName);
        }
        if (filter != null && location.Name.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) < 0)
            continue;
        lines.Add($"  {location.Name} -> {(targets.Count == 0 ? "(none)" : string.Join(", ", targets))}");
    }
    this.Monitor.Log($"Warp graph: {locations} locations, {edges} warp edges.", LogLevel.Info);
    foreach (string line in lines)
        this.Monitor.Log(line, LogLevel.Info);
}
```

- [ ] **Step 3: Build and deploy**

Run: `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`
Expected: `Build succeeded.` then the SMAPI banner in the log.

- [ ] **Step 4: Run it on a loaded save and read the output**

Load the throwaway Rodger save (`None_*`, newest; never `PuffPuff_*` or `Cheatside_*`), then:

```
tools/bridge.ps1 -Action send -Lines "tly_warpgraph"
```

Expected in `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`: well over 100 locations, and interiors present with warps back out, for example `SeedShop -> Town` and `Saloon -> Town`. Confirm `IslandSouth` appears and has edges.

- [ ] **Step 5: Run it again immediately after a reset**

```
tools/bridge.ps1 -Action send -Lines "tly_reset|tly_warpgraph"
```

Expected: the same location and edge counts as step 4, give or take the farm rebuild. **If interiors are missing or the counts collapse, STOP and report to Jeff before continuing.** The design assumes this data exists at generation time; if it does not, the reachability walk must move to a later hook and the plan needs revising.

- [ ] **Step 6: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

Add to `CHANGELOG.md`:

```markdown
## Unreleased

2004 tests.

### Added

- **`tly_warpgraph`**, a developer command that prints every loaded location and its warp
  targets. Groundwork for deciding which places a run can actually reach.
```

```bash
git add src/TheLongestYear/ModEntry.cs CHANGELOG.md
git commit -m "v0.17.16: tly_warpgraph diagnostic for reachability derivation"
```

---

### Task 2: `ReachabilityGraph`, the pure warp walk

**Files:**
- Create: `src/TheLongestYear.Core/Availability/ReachabilityGraph.cs`
- Modify: `src/TheLongestYear.Core/ItemPoolModel.cs` (add `RawLocationLink` beside the other `Raw*` records, after `RawGeodeDropEntry` around line 157)
- Test: `tests/TheLongestYear.Tests/ReachabilityGraphTests.cs`

**Interfaces:**
- Consumes: nothing.
- Produces:
  - `public sealed record RawLocationLink(string From, string To);`
  - `public static IReadOnlySet<string> ReachabilityGraph.UnreachableLocations(IReadOnlyList<RawLocationLink> links, IReadOnlyCollection<string> allLocations, Func<string, bool> isForbidden, string start = "Farm")`

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/ReachabilityGraphTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ReachabilityGraphTests
{
    private static readonly string[] Everything =
    {
        "Farm", "Town", "Beach", "IslandSouth", "FishmongerShop", "Desert", "BusStop",
    };

    private static readonly RawLocationLink[] Links =
    {
        new("Farm", "Town"),
        new("Town", "Beach"),
        new("Town", "BusStop"),
        new("BusStop", "Desert"),
        new("Beach", "IslandSouth"),
        new("IslandSouth", "FishmongerShop"),
    };

    private static bool IslandForbidden(string name) => name.Contains("Island", StringComparison.Ordinal);

    [Fact]
    public void Forbidden_location_is_unreachable()
    {
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden);
        Assert.Contains("IslandSouth", unreachable);
    }

    [Fact]
    public void Location_reachable_only_through_a_forbidden_place_is_unreachable()
    {
        // The Fishmonger shop: its one door leads to IslandSouth, and its NAME matches no marker,
        // which is the whole point. The real map is called VoidWitchCult.TheFishmonger_Fishmonger_GI_Inside.
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden);
        Assert.Contains("FishmongerShop", unreachable);
    }

    [Fact]
    public void Ordinary_locations_stay_reachable()
    {
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden);
        Assert.DoesNotContain("Town", unreachable);
        Assert.DoesNotContain("Beach", unreachable);
    }

    [Fact]
    public void Location_behind_a_gate_that_opens_during_the_year_stays_reachable()
    {
        // The bus is broken on Spring 1, but the Desert is still connected to the world, so
        // Cactus Fruit must remain a legal board target (BundleCatalogBuilder's standing ruling).
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden);
        Assert.DoesNotContain("Desert", unreachable);
    }

    [Fact]
    public void Second_door_to_the_world_keeps_a_location_reachable()
    {
        var links = new List<RawLocationLink>(Links) { new("FishmongerShop", "Town") };
        var unreachable = ReachabilityGraph.UnreachableLocations(links, Everything, IslandForbidden);
        Assert.DoesNotContain("FishmongerShop", unreachable);
    }

    [Fact]
    public void Warps_are_followed_in_both_directions()
    {
        // Vanilla interiors often declare the warp only on one side.
        var links = new[] { new RawLocationLink("SeedShop", "Town"), new RawLocationLink("Farm", "Town") };
        var all = new[] { "Farm", "Town", "SeedShop" };
        var unreachable = ReachabilityGraph.UnreachableLocations(links, all, _ => false);
        Assert.Empty(unreachable);
    }

    [Fact]
    public void Cycles_terminate()
    {
        var links = new[]
        {
            new RawLocationLink("Farm", "Town"), new RawLocationLink("Town", "Farm"),
            new RawLocationLink("Town", "Beach"), new RawLocationLink("Beach", "Town"),
        };
        var unreachable = ReachabilityGraph.UnreachableLocations(links, new[] { "Farm", "Town", "Beach" }, _ => false);
        Assert.Empty(unreachable);
    }

    [Fact]
    public void Location_with_no_doors_at_all_is_unknown_not_unreachable()
    {
        // Verified in-game 2026-09-10: MovieTheater, WizardHouseBasement and LewisBasement are
        // loaded with zero warps. A map with no doors is evidence of our ignorance about how it
        // is entered, never proof that a player cannot get there.
        var all = new[] { "Farm", "Town", "MovieTheater" };
        var links = new[] { new RawLocationLink("Farm", "Town") };
        var unreachable = ReachabilityGraph.UnreachableLocations(links, all, _ => false);
        Assert.DoesNotContain("MovieTheater", unreachable);
    }

    [Fact]
    public void Island_only_world_does_not_strip_everything_when_start_is_missing()
    {
        // Fail open: an unknown start location must not mark the whole world unreachable.
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden, start: "NoSuchPlace");
        Assert.Empty(unreachable);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter ReachabilityGraphTests`
Expected: build error, `ReachabilityGraph` and `RawLocationLink` do not exist.

- [ ] **Step 3: Add the record**

In `src/TheLongestYear.Core/ItemPoolModel.cs`, after `RawGeodeDropEntry`:

```csharp
/// <summary>One warp edge between two locations, as the world actually connects them.
/// Direction is recorded but the reachability walk treats edges as two-way: vanilla interiors
/// frequently declare the door on one side only.</summary>
public sealed record RawLocationLink(string From, string To);
```

- [ ] **Step 4: Implement the walk**

Create `src/TheLongestYear.Core/Availability/ReachabilityGraph.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Which places a run cannot get to, decided by walking doors rather than matching
/// map names.
///
/// The marker list (BundleGenerationTuning.ExcludedLocationMarkers) names the forbidden places
/// directly. This walk adds everything that can only be reached THROUGH one of them: start at the
/// farm, refuse to step into a forbidden location, and see what is left over.
///
/// Why names are not enough (2026-09-10, Nexus posts, pitytheviolins): The Fishmonger puts its
/// shop in a custom map called "VoidWitchCult.TheFishmonger_Fishmonger_GI_Inside" whose only door
/// leads to IslandSouth. No substring of that name matches "Island", and the owning NPC's
/// HomeRegion is set to "Town". A map's name is a label its author picks freely. Its warps are how
/// players actually reach it.
///
/// Gates that open DURING the year are deliberately treated as passable: the bus to the Desert,
/// the Rusty Key to the Sewer, the Steel Axe to the Secret Woods. This walk answers "is this place
/// connected to the world other than through a forbidden one", not "can the player stand there on
/// Spring 1". Without that, a fresh board would call the Desert unreachable and strip Cactus Fruit,
/// contradicting BundleCatalogBuilder's ruling that such items are valid targets a player invests
/// in. Timing is LocationGating's job, and this rule must not duplicate it.</summary>
public static class ReachabilityGraph
{
    /// <summary>Where a run always begins.</summary>
    public const string FarmLocation = "Farm";

    /// <summary>Locations the walk cannot reach from <paramref name="start"/>, including the
    /// forbidden ones themselves.
    ///
    /// Fails open on purpose. An unknown start, or a start that is itself forbidden, returns the
    /// empty set rather than condemning the whole world: over-exclusion silently strips real
    /// content, which is the one failure mode a player would never understand.</summary>
    public static IReadOnlySet<string> UnreachableLocations(
        IReadOnlyList<RawLocationLink> links,
        IReadOnlyCollection<string> allLocations,
        Func<string, bool> isForbidden,
        string start = FarmLocation)
    {
        var unreachable = new HashSet<string>(StringComparer.Ordinal);
        if (links == null || allLocations == null || isForbidden == null) return unreachable;
        if (string.IsNullOrEmpty(start)) return unreachable;

        var known = new HashSet<string>(allLocations, StringComparer.Ordinal);
        if (!known.Contains(start) || isForbidden(start)) return unreachable;

        var neighbours = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        void Link(string from, string to)
        {
            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to)) return;
            if (!neighbours.TryGetValue(from, out List<string>? list))
                neighbours[from] = list = new List<string>();
            if (!list.Contains(to, StringComparer.Ordinal)) list.Add(to);
        }
        foreach (RawLocationLink link in links)
        {
            if (link == null) continue;
            Link(link.From, link.To);
            Link(link.To, link.From);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal) { start };
        var queue = new Queue<string>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string here = queue.Dequeue();
            if (!neighbours.TryGetValue(here, out List<string>? next)) continue;
            foreach (string there in next)
            {
                if (isForbidden(there)) continue;   // never step through a forbidden place
                if (!visited.Add(there)) continue;
                queue.Enqueue(there);
            }
        }

        foreach (string name in known)
        {
            if (visited.Contains(name)) continue;
            // A location with no doors at all was never proved unreachable, only never
            // explained. Verified in-game 2026-09-10: MovieTheater, WizardHouseBasement and
            // LewisBasement all load with zero warps because they are entered by scripted
            // actions. Condemning them would be inventing proof we do not have.
            if (!neighbours.ContainsKey(name) && !isForbidden(name)) continue;
            unreachable.Add(name);
        }
        return unreachable;
    }
}
```

Note the `List<string>.Contains(string, StringComparer)` call needs `using System.Linq;` for the overload. Add it.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter ReachabilityGraphTests`
Expected: 8 passed.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: 2012 passed, 0 failed.

- [ ] **Step 7: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

Changelog entry under `### Added`: "The groundwork for judging which places a run can reach by walking doors rather than matching map names."

```bash
git add src/TheLongestYear.Core/Availability/ReachabilityGraph.cs src/TheLongestYear.Core/ItemPoolModel.cs tests/TheLongestYear.Tests/ReachabilityGraphTests.cs CHANGELOG.md
git commit -m "v0.17.17: ReachabilityGraph walks warps to find places a run cannot reach"
```

---

### Task 3: The shop source rule

**Files:**
- Create: `src/TheLongestYear.Core/Availability/SourceReachability.cs`
- Modify: `src/TheLongestYear.Core/ItemPoolModel.cs`
- Test: `tests/TheLongestYear.Tests/SourceReachabilityTests.cs`

**Interfaces:**
- Consumes: `ReachabilityGraph.UnreachableLocations` (Task 2).
- Produces:
  - `public sealed record RawShopListing(string ItemId, string ShopId, bool IsRecipe = false);`
  - `public sealed record RawShopPlacement(string ShopId, string LocationName);`
  - `public sealed class SourceReachability` with constructor `(IReadOnlySet<string> unreachableLocations, IReadOnlyList<RawShopListing> shopListings, IReadOnlyList<RawShopPlacement> shopPlacements, IReadOnlyList<RawCropEntry> crops, IReadOnlyList<RawRecipeEntry> recipes, IReadOnlySet<string> reachableSpawnIds)`, method `bool IsUnreachable(string qualifiedItemId)`, property `IReadOnlyDictionary<string, string> Reasons`.
  - `reachableSpawnIds` is POSITIVE proof: any id in it is reachable and no rule may condemn it. It carries every forage, fish, crab-pot, monster-drop, geode-drop and fruit-tree id whose source is not itself gated. Without it, "every known source" silently means "every source this class happens to model", which is not conservative: a forageable item also listed in an island shop would be condemned by the shop rule while its perfectly good spawn never got a vote.
  - Tasks 4 and 5 extend the same class; the constructor signature above is final, so build it now and leave the crop and recipe parameters unused until then.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/SourceReachabilityTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class SourceReachabilityTests
{
    private const string IslandShop = "ConstanceSeeds";
    private const string TownShop = "Pierre";
    private const string IslandMap = "TheFishmonger_GI_Inside";
    private const string TownMap = "SeedShop";

    private static readonly IReadOnlySet<string> Unreachable =
        new HashSet<string>(StringComparer.Ordinal) { IslandMap, "IslandSouth" };

    private static readonly RawShopPlacement[] Placements =
    {
        new(IslandShop, IslandMap),
        new(TownShop, TownMap),
    };

    private static readonly IReadOnlySet<string> NoSpawns = new HashSet<string>(StringComparer.Ordinal);

    private static SourceReachability Build(params RawShopListing[] listings) => new(
        Unreachable, listings, Placements,
        Array.Empty<RawCropEntry>(), Array.Empty<RawRecipeEntry>(), NoSpawns);

    [Fact]
    public void Item_sold_only_in_an_unreachable_shop_is_unreachable()
    {
        var rule = Build(new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.True(rule.IsUnreachable("(O)FishmongerSeed"));
    }

    [Fact]
    public void Item_sold_somewhere_reachable_too_is_allowed()
    {
        var rule = Build(
            new RawShopListing("(O)FishmongerSeed", IslandShop),
            new RawShopListing("(O)FishmongerSeed", TownShop));
        Assert.False(rule.IsUnreachable("(O)FishmongerSeed"));
    }

    [Fact]
    public void Item_with_no_known_source_is_allowed()
    {
        // The conservative rule (Jeff, 2026-09-10): untraceable means allowed.
        var rule = Build();
        Assert.False(rule.IsUnreachable("(O)16"));
    }

    [Fact]
    public void Shop_with_no_known_location_leaves_the_item_allowed()
    {
        var rule = Build(new RawShopListing("(O)Mystery", "ShopNobodyPlaced"));
        Assert.False(rule.IsUnreachable("(O)Mystery"));
    }

    [Fact]
    public void Recipe_listing_does_not_count_as_selling_the_item()
    {
        // Buying a recipe teaches you to cook it; it does not hand you the dish.
        var rule = Build(new RawShopListing("(O)Dish", TownShop, IsRecipe: true));
        Assert.False(rule.IsUnreachable("(O)Dish"));
    }

    [Fact]
    public void Unqualified_ids_are_normalised()
    {
        var rule = Build(new RawShopListing("FishmongerSeed", IslandShop));
        Assert.True(rule.IsUnreachable("(O)FishmongerSeed"));
    }

    [Fact]
    public void Item_with_a_reachable_spawn_is_never_condemned()
    {
        // A forageable or fishable item that a mod ALSO lists in an island shop must stay.
        var spawns = new HashSet<string>(StringComparer.Ordinal) { "(O)Forageable" };
        var rule = new SourceReachability(
            Unreachable, new[] { new RawShopListing("(O)Forageable", IslandShop) }, Placements,
            Array.Empty<RawCropEntry>(), Array.Empty<RawRecipeEntry>(), spawns);
        Assert.False(rule.IsUnreachable("(O)Forageable"));
    }

    [Fact]
    public void Unreachable_shop_plus_an_unplaced_shop_leaves_the_item_allowed()
    {
        // The Traveling Cart and festival vendors are opened from code, so they have no
        // discoverable placement. An unplaced shop is an unknown, and unknown means allowed.
        var rule = Build(
            new RawShopListing("(O)Seed", IslandShop),
            new RawShopListing("(O)Seed", "ShopNobodyPlaced"));
        Assert.False(rule.IsUnreachable("(O)Seed"));
    }

    [Fact]
    public void Reason_names_the_rule_that_dropped_the_item()
    {
        var rule = Build(new RawShopListing("(O)FishmongerSeed", IslandShop));
        rule.IsUnreachable("(O)FishmongerSeed");
        Assert.Contains("shop", rule.Reasons["(O)FishmongerSeed"], StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter SourceReachabilityTests`
Expected: build error, `SourceReachability`, `RawShopListing`, `RawShopPlacement` and `RawRecipeEntry` do not exist.

- [ ] **Step 3: Add the records**

In `src/TheLongestYear.Core/ItemPoolModel.cs`, beside `RawLocationLink`:

```csharp
/// <summary>One Data/Shops stock line. <paramref name="IsRecipe"/> is the shop entry's own
/// IsRecipe flag: such a line teaches a recipe rather than selling the item, so it is a source of
/// the KNOWLEDGE, never of the item itself.</summary>
public sealed record RawShopListing(string ItemId, string ShopId, bool IsRecipe = false);

/// <summary>Where a shop can actually be opened. A shop with no known placement leaves everything
/// it sells allowed, per the conservative rule.</summary>
public sealed record RawShopPlacement(string ShopId, string LocationName);

/// <summary>One cooking or crafting recipe. <paramref name="Unlock"/> is the raw unlock-conditions
/// field from Data/CookingRecipes ("default", "s Farming 3", "f Robin 7", "l", or "none").</summary>
public sealed record RawRecipeEntry(
    string OutputItemId, IReadOnlyList<string> IngredientItemIds, string Unlock);
```

- [ ] **Step 4: Implement the class with only the shop rule**

Create `src/TheLongestYear.Core/Availability/SourceReachability.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Whether an item is PROVABLY out of reach this run.
///
/// Sources are alternatives, so an item is only condemned when every known route to it is
/// unreachable, and an item nothing can trace stays allowed (Jeff, 2026-09-10). Over-exclusion
/// silently strips real mod content, which no player could diagnose; under-exclusion merely leaves
/// the status quo.
///
/// Answers are memoised per instance, so build one per generation and throw it away after.</summary>
public sealed class SourceReachability
{
    private readonly IReadOnlySet<string> _unreachableLocations;
    private readonly Dictionary<string, List<RawShopListing>> _listingsByItem;
    private readonly Dictionary<string, List<string>> _shopLocations;
    private readonly IReadOnlySet<string> _reachableSpawnIds;
    private readonly Dictionary<string, bool> _memo = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _reasons = new(StringComparer.Ordinal);

    public SourceReachability(
        IReadOnlySet<string> unreachableLocations,
        IReadOnlyList<RawShopListing> shopListings,
        IReadOnlyList<RawShopPlacement> shopPlacements,
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyList<RawRecipeEntry> recipes,
        IReadOnlySet<string> reachableSpawnIds)
    {
        _unreachableLocations = unreachableLocations ?? new HashSet<string>(StringComparer.Ordinal);
        _reachableSpawnIds = reachableSpawnIds ?? new HashSet<string>(StringComparer.Ordinal);

        _listingsByItem = new Dictionary<string, List<RawShopListing>>(StringComparer.Ordinal);
        foreach (RawShopListing listing in shopListings ?? Array.Empty<RawShopListing>())
        {
            if (listing == null || string.IsNullOrEmpty(listing.ItemId)) continue;
            string id = Qualify(listing.ItemId);
            if (!_listingsByItem.TryGetValue(id, out List<RawShopListing>? list))
                _listingsByItem[id] = list = new List<RawShopListing>();
            list.Add(listing);
        }

        _shopLocations = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (RawShopPlacement placement in shopPlacements ?? Array.Empty<RawShopPlacement>())
        {
            if (placement == null || string.IsNullOrEmpty(placement.ShopId)) continue;
            if (!_shopLocations.TryGetValue(placement.ShopId, out List<string>? list))
                _shopLocations[placement.ShopId] = list = new List<string>();
            if (!string.IsNullOrEmpty(placement.LocationName)) list.Add(placement.LocationName);
        }
    }

    /// <summary>Why each condemned id was condemned, for the log. Populated as
    /// <see cref="IsUnreachable"/> runs.</summary>
    public IReadOnlyDictionary<string, string> Reasons => _reasons;

    public bool IsUnreachable(string qualifiedItemId)
    {
        if (string.IsNullOrEmpty(qualifiedItemId)) return false;
        string id = Qualify(qualifiedItemId);
        if (_memo.TryGetValue(id, out bool cached)) return cached;

        bool verdict = Decide(id, out string reason);
        _memo[id] = verdict;
        if (verdict) _reasons[id] = reason;
        return verdict;
    }

    private bool Decide(string id, out string reason)
    {
        reason = "";

        // Positive proof beats every condemning rule. An item that spawns somewhere reachable
        // is reachable, whatever else also happens to list it.
        if (_reachableSpawnIds.Contains(id)) return false;

        bool anySourceKnown = false;

        if (BoughtSomewhere(id, out bool shopUnreachable))
        {
            anySourceKnown = true;
            if (!shopUnreachable) return false;   // a reachable shop is enough
            reason = "no reachable shop sells it";
        }

        return anySourceKnown;
    }

    /// <summary>True when some shop SELLS this item (recipe listings excluded).
    /// <paramref name="unreachable"/> is set when every such shop with a known placement sits
    /// somewhere unreachable.</summary>
    private bool BoughtSomewhere(string id, out bool unreachable)
    {
        unreachable = false;
        if (!_listingsByItem.TryGetValue(id, out List<RawShopListing>? listings)) return false;

        bool anySale = false, anyPlaced = false, allUnreachable = true;
        foreach (RawShopListing listing in listings)
        {
            if (listing.IsRecipe) continue;       // teaches the recipe, does not sell the item
            anySale = true;
            if (!_shopLocations.TryGetValue(listing.ShopId, out List<string>? places) || places.Count == 0)
            {
                // A shop nobody could place is an UNKNOWN route, not a closed one. The
                // Traveling Cart, Night Market and festival vendors are opened from game code
                // and have no discoverable placement, and mods open shops from dialogue and
                // events. Skipping these would let one island shop condemn an item the cart
                // sells every spring, so a single unplaced seller keeps the item allowed.
                allUnreachable = false;
                continue;
            }
            foreach (string place in places)
            {
                anyPlaced = true;
                if (!_unreachableLocations.Contains(place)) allUnreachable = false;
            }
        }
        if (!anySale) return false;
        unreachable = anyPlaced && allUnreachable;
        return true;
    }

    private static string Qualify(string id) => BundleParsing.NormalizeItemId(id);
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter SourceReachabilityTests`
Expected: 9 passed.

- [ ] **Step 6: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: 2022 passed, 0 failed.

- [ ] **Step 7: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

```bash
git add src/TheLongestYear.Core/Availability/SourceReachability.cs src/TheLongestYear.Core/ItemPoolModel.cs tests/TheLongestYear.Tests/SourceReachabilityTests.cs CHANGELOG.md
git commit -m "v0.17.18: shop source rule, an item sold only where you cannot go is out of reach"
```

---

### Task 4: The crop seed rule

**Files:**
- Modify: `src/TheLongestYear.Core/ItemPoolModel.cs` (`RawCropEntry`, line 119)
- Modify: `src/TheLongestYear.Core/Availability/SourceReachability.cs`
- Modify: `src/TheLongestYear/Loop/GameDataPools.cs` (populate the new field where `RawCropEntry` is constructed)
- Test: `tests/TheLongestYear.Tests/SourceReachabilityTests.cs`

**Interfaces:**
- Consumes: `SourceReachability` (Task 3).
- Produces: `RawCropEntry` gains `string? SeedItemId = null` as its fourth positional parameter. Existing call sites keep working because it is optional.

- [ ] **Step 1: Write the failing tests**

Append to `SourceReachabilityTests`:

```csharp
    private static SourceReachability WithCrops(
        IReadOnlyList<RawCropEntry> crops, params RawShopListing[] listings) => new(
        Unreachable, listings, Placements, crops, Array.Empty<RawRecipeEntry>(), NoSpawns);

    [Fact]
    public void Crop_whose_seed_is_unreachable_is_unreachable()
    {
        var crops = new[] { new RawCropEntry("(O)FishmongerCrop", new[] { Season.Fall }, null, "(O)FishmongerSeed") };
        var rule = WithCrops(crops, new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.True(rule.IsUnreachable("(O)FishmongerCrop"));
        Assert.Contains("seed", rule.Reasons["(O)FishmongerCrop"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Crop_whose_seed_is_reachable_is_allowed()
    {
        var crops = new[] { new RawCropEntry("(O)Parsnip", new[] { Season.Spring }, null, "(O)ParsnipSeed") };
        var rule = WithCrops(crops, new RawShopListing("(O)ParsnipSeed", TownShop));
        Assert.False(rule.IsUnreachable("(O)Parsnip"));
    }

    [Fact]
    public void Crop_also_sold_somewhere_reachable_is_allowed()
    {
        var crops = new[] { new RawCropEntry("(O)FishmongerCrop", new[] { Season.Fall }, null, "(O)FishmongerSeed") };
        var rule = WithCrops(crops,
            new RawShopListing("(O)FishmongerSeed", IslandShop),
            new RawShopListing("(O)FishmongerCrop", TownShop));
        Assert.False(rule.IsUnreachable("(O)FishmongerCrop"));
    }

    // BOTH orderings, deliberately. A single ordering only catches HALF the scalar bug: with the
    // reachable seed last, a "last row wins" scalar dictionary keeps the reachable one and the test
    // still passes, hiding exactly the defect it was written to catch. One test per ordering means
    // a scalar fails whichever way the rows enumerate.
    [Fact]
    public void A_reachable_alternative_seed_rescues_the_crop_reachable_first()
    {
        var crops = new[]
        {
            new RawCropEntry("(O)Shared", new[] { Season.Spring }, null, "(O)ParsnipSeed"),
            new RawCropEntry("(O)Shared", new[] { Season.Fall }, null, "(O)FishmongerSeed"),
        };
        var rule = WithCrops(crops,
            new RawShopListing("(O)FishmongerSeed", IslandShop),
            new RawShopListing("(O)ParsnipSeed", TownShop));
        Assert.False(rule.IsUnreachable("(O)Shared"));
    }

    [Fact]
    public void A_reachable_alternative_seed_rescues_the_crop_reachable_last()
    {
        var crops = new[]
        {
            new RawCropEntry("(O)Shared", new[] { Season.Fall }, null, "(O)FishmongerSeed"),
            new RawCropEntry("(O)Shared", new[] { Season.Spring }, null, "(O)ParsnipSeed"),
        };
        var rule = WithCrops(crops,
            new RawShopListing("(O)FishmongerSeed", IslandShop),
            new RawShopListing("(O)ParsnipSeed", TownShop));
        Assert.False(rule.IsUnreachable("(O)Shared"));
    }

    [Fact]
    public void Crop_with_no_recorded_seed_is_allowed()
    {
        var crops = new[] { new RawCropEntry("(O)MysteryCrop", new[] { Season.Spring }) };
        var rule = WithCrops(crops);
        Assert.False(rule.IsUnreachable("(O)MysteryCrop"));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter SourceReachabilityTests`
Expected: build error, `RawCropEntry` takes no fourth argument.

- [ ] **Step 3: Extend the record**

In `ItemPoolModel.cs`, replace `RawCropEntry`:

```csharp
/// <summary>One Data/Crops row. <paramref name="SeedItemId"/> is the row's KEY (Data/Crops is
/// keyed by seed id), which is how a crop's reachability is traced: you cannot grow what you
/// cannot plant. Optional because hand-built test pools and the curated CropPoolAdditions have no
/// seed.</summary>
public sealed record RawCropEntry(
    string HarvestItemId, IReadOnlyList<Season> Seasons, int? HarvestMaxQuality = null,
    string? SeedItemId = null);
```

- [ ] **Step 4: Add the rule**

In `SourceReachability`, add a field, fill it in the constructor, and extend `Decide`:

```csharp
    private readonly Dictionary<string, List<string>> _seedByHarvest;
```

In the constructor, after the shop maps:

```csharp
        // A LIST, not a single value: mods define several seeds yielding one harvest, and a
        // scalar would let whichever row enumerated last erase a reachable alternative.
        _seedByHarvest = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (RawCropEntry crop in crops ?? Array.Empty<RawCropEntry>())
        {
            if (crop?.HarvestItemId == null || string.IsNullOrEmpty(crop.SeedItemId)) continue;
            string harvest = Qualify(crop.HarvestItemId);
            if (!_seedByHarvest.TryGetValue(harvest, out List<string>? seeds))
                _seedByHarvest[harvest] = seeds = new List<string>();
            string seed = Qualify(crop.SeedItemId);
            if (!seeds.Contains(seed)) seeds.Add(seed);
        }
```

In `Decide`, after the shop block:

```csharp
        if (_seedByHarvest.TryGetValue(id, out List<string>? seeds) && seeds.Count > 0)
        {
            anySourceKnown = true;
            string? blockedSeed = null;
            foreach (string seed in seeds)
            {
                if (!IsUnreachable(seed)) { blockedSeed = null; break; }   // one good seed is enough
                blockedSeed ??= seed;
            }
            if (blockedSeed == null) return false;
            reason = $"its seed {blockedSeed} is out of reach";
        }
```

- [ ] **Step 5: Populate the seed at the boundary**

In `src/TheLongestYear/Loop/GameDataPools.cs`, find where `RawCropEntry` is constructed from `Data/Crops` and pass the dictionary key as the fourth argument, for example `new RawCropEntry(harvestId, seasons, maxQuality, kv.Key)`. Read the surrounding lines first; the exact local names may differ.

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter SourceReachabilityTests`
Expected: 15 passed (9 from Task 3 plus 6 new).

- [ ] **Step 7: Run the whole suite and build the mod**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: 2028 passed, 0 failed.
Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: `Build succeeded.` (close the game first if it is running)

- [ ] **Step 8: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

```bash
git add src/TheLongestYear.Core src/TheLongestYear/Loop/GameDataPools.cs tests/TheLongestYear.Tests/SourceReachabilityTests.cs CHANGELOG.md
git commit -m "v0.17.19: crop seed rule, you cannot grow what you cannot plant"
```

---

### Task 5: The recipe rules, ingredients and learnability

The subtle one. Five of The Fishmonger's eleven dishes cook from entirely vanilla ingredients, so an ingredient-only rule leaves them on the board. What rules them out is that the recipe cannot be learned.

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/SourceReachability.cs`
- Test: `tests/TheLongestYear.Tests/SourceReachabilityTests.cs`

**Interfaces:**
- Consumes: `SourceReachability` (Tasks 3 and 4), `RawRecipeEntry` (Task 3).
- Produces: no new public surface. `IsUnreachable` gains the cooking route internally.

- [ ] **Step 1: Write the failing tests**

Append to `SourceReachabilityTests`:

```csharp
    private static SourceReachability WithRecipes(
        IReadOnlyList<RawRecipeEntry> recipes, params RawShopListing[] listings) => new(
        Unreachable, listings, Placements, Array.Empty<RawCropEntry>(), recipes, NoSpawns);

    [Fact]
    public void Dish_with_an_unreachable_ingredient_is_unreachable()
    {
        var recipes = new[] { new RawRecipeEntry("(O)Sauce", new[] { "(O)FishmongerSeed", "(O)246" }, "default") };
        var rule = WithRecipes(recipes, new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.True(rule.IsUnreachable("(O)Sauce"));
        Assert.Contains("ingredient", rule.Reasons["(O)Sauce"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dish_with_vanilla_ingredients_but_an_unlearnable_recipe_is_unreachable()
    {
        // Baked Red Snapper Curry: Red Snapper, Potato, Hot Pepper, all vanilla. Only Constance
        // teaches it, and its unlock field is "none".
        var recipes = new[] { new RawRecipeEntry("(O)Curry", new[] { "(O)150", "(O)192", "(O)260" }, "none") };
        var rule = WithRecipes(recipes, new RawShopListing("(O)Curry", IslandShop, IsRecipe: true));
        Assert.True(rule.IsUnreachable("(O)Curry"));
        Assert.Contains("recipe", rule.Reasons["(O)Curry"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Dish_taught_by_a_reachable_shop_is_allowed()
    {
        var recipes = new[] { new RawRecipeEntry("(O)Curry", new[] { "(O)150" }, "none") };
        var rule = WithRecipes(recipes, new RawShopListing("(O)Curry", TownShop, IsRecipe: true));
        Assert.False(rule.IsUnreachable("(O)Curry"));
    }

    [Theory]
    [InlineData("default")]
    [InlineData("s Farming 3")]
    [InlineData("l")]
    [InlineData("f Robin 7")]
    public void Dish_with_a_normal_unlock_route_is_allowed(string unlock)
    {
        var recipes = new[] { new RawRecipeEntry("(O)Dish", new[] { "(O)150" }, unlock) };
        var rule = WithRecipes(recipes);
        Assert.False(rule.IsUnreachable("(O)Dish"));
    }

    // BOTH orderings, for the same reason as the seed tests: with the cookable recipe last, a
    // "last row wins" scalar dictionary keeps it and the test passes despite the bug.
    [Fact]
    public void A_reachable_alternative_recipe_rescues_the_dish_cookable_first()
    {
        var recipes = new[]
        {
            new RawRecipeEntry("(O)Dish", new[] { "(O)150" }, "s Farming 3"),
            new RawRecipeEntry("(O)Dish", new[] { "(O)FishmongerSeed" }, "none"),
        };
        var rule = WithRecipes(recipes, new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.False(rule.IsUnreachable("(O)Dish"));
    }

    [Fact]
    public void A_reachable_alternative_recipe_rescues_the_dish_cookable_last()
    {
        var recipes = new[]
        {
            new RawRecipeEntry("(O)Dish", new[] { "(O)FishmongerSeed" }, "none"),
            new RawRecipeEntry("(O)Dish", new[] { "(O)150" }, "s Farming 3"),
        };
        var rule = WithRecipes(recipes, new RawShopListing("(O)FishmongerSeed", IslandShop));
        Assert.False(rule.IsUnreachable("(O)Dish"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    public void Dish_with_an_empty_or_null_unlock_field_is_allowed(string unlock)
    {
        // Vanilla Cookies carries the literal string "null" and is taught by Evelyn's event.
        var recipes = new[] { new RawRecipeEntry("(O)Cookies", new[] { "(O)150" }, unlock) };
        var rule = WithRecipes(recipes);
        Assert.False(rule.IsUnreachable("(O)Cookies"));
    }

    [Fact]
    public void Recipe_cycles_terminate_and_do_not_condemn()
    {
        var recipes = new[]
        {
            new RawRecipeEntry("(O)A", new[] { "(O)B" }, "default"),
            new RawRecipeEntry("(O)B", new[] { "(O)A" }, "default"),
        };
        var rule = WithRecipes(recipes);
        Assert.False(rule.IsUnreachable("(O)A"));
    }
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter SourceReachabilityTests`
Expected: the five new tests fail (recipes are ignored, so nothing is condemned).

- [ ] **Step 3: Implement both rules**

Add fields and constructor wiring:

```csharp
    private readonly Dictionary<string, List<RawRecipeEntry>> _recipesByOutput;
    private readonly HashSet<string> _inProgress = new(StringComparer.Ordinal);
```

In the constructor:

```csharp
        // A LIST for the same reason as the seeds: a reachable vanilla recipe and an
        // unreachable mod recipe can produce the same object, and the last one written must not
        // become the only one considered.
        _recipesByOutput = new Dictionary<string, List<RawRecipeEntry>>(StringComparer.Ordinal);
        foreach (RawRecipeEntry recipe in recipes ?? Array.Empty<RawRecipeEntry>())
        {
            if (recipe?.OutputItemId == null) continue;
            string output = Qualify(recipe.OutputItemId);
            if (!_recipesByOutput.TryGetValue(output, out List<RawRecipeEntry>? list))
                _recipesByOutput[output] = list = new List<RawRecipeEntry>();
            list.Add(recipe);
        }
```

Guard the recursion in `IsUnreachable`, right after the memo lookup:

```csharp
        // A cycle means we are already asking this question further up the stack. Answer
        // "reachable" so a loop can never condemn an item on its own account.
        if (!_inProgress.Add(id)) return false;
        try
        {
            bool verdict = Decide(id, out string reason);
            _memo[id] = verdict;
            if (verdict) _reasons[id] = reason;
            return verdict;
        }
        finally
        {
            _inProgress.Remove(id);
        }
```

(Replace the previous `Decide` call block with this; do not call `Decide` twice.)

Extend `Decide`, after the crop block:

```csharp
        if (_recipesByOutput.TryGetValue(id, out List<RawRecipeEntry>? recipeList) && recipeList.Count > 0)
        {
            anySourceKnown = true;
            string? recipeReason = null;
            foreach (RawRecipeEntry recipe in recipeList)
            {
                string? blocked = FirstUnreachableIngredient(recipe);
                if (blocked != null)
                    recipeReason ??= $"ingredient {blocked} is out of reach";
                else if (!RecipeLearnable(id, recipe))
                    recipeReason ??= "its recipe cannot be learned anywhere reachable";
                else
                    return false;   // one cookable route is enough
            }
            reason = recipeReason ?? "";
        }
```

Add the helpers:

```csharp
    private string? FirstUnreachableIngredient(RawRecipeEntry recipe)
    {
        foreach (string ingredient in recipe.IngredientItemIds ?? Array.Empty<string>())
        {
            if (string.IsNullOrEmpty(ingredient)) continue;
            // A category ref ("any fish") is satisfied by many items; treat it as reachable.
            if (BundleParsing.IsCategoryRef(ingredient)) continue;
            string qualified = Qualify(ingredient);
            if (IsUnreachable(qualified)) return qualified;
        }
        return null;
    }

    /// <summary>Whether the player could ever learn this recipe. The unlock field names a normal
    /// route (a skill level, the TV, friendship, or simply known from the start), OR some shop in a
    /// reachable place teaches it. "none" means no normal route exists, which leaves only the
    /// shops. Nexus posts 2026-09-10: every Fishmonger recipe is "none" and taught only on Ginger
    /// Island, which is what rules out its five all-vanilla dishes.</summary>
    private bool RecipeLearnable(string id, RawRecipeEntry recipe)
    {
        string unlock = (recipe.Unlock ?? "").Trim();
        // Only the LITERAL string "none" is treated as "no normal route". Anything else,
        // including an empty or missing field, counts as learnable. Verified against the live
        // Data/CookingRecipes on 2026-09-10: all 81 vanilla recipes use l, f, s, default or the
        // literal string "null", and NOT ONE uses "none" or an empty field. So this rule cannot
        // touch vanilla cooking, and condemning an unparsed field would be inventing proof.
        if (!unlock.Equals("none", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!_listingsByItem.TryGetValue(id, out List<RawShopListing>? listings)) return false;
        bool anyPlaced = false, allUnreachable = true;
        foreach (RawShopListing listing in listings)
        {
            if (!listing.IsRecipe) continue;
            if (!_shopLocations.TryGetValue(listing.ShopId, out List<string>? places)) continue;
            foreach (string place in places)
            {
                anyPlaced = true;
                if (!_unreachableLocations.Contains(place)) allUnreachable = false;
            }
        }
        // Nothing teaches it anywhere we can place: not learnable. A recipe taught by an
        // unplaced shop keeps the benefit of the doubt.
        return !anyPlaced || !allUnreachable;
    }
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter SourceReachabilityTests`
Expected: 27 passed (15 from Tasks 3 and 4 plus 12 new; the two [Theory] blocks contribute 6 cases between them).

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: 2040 passed, 0 failed.

- [ ] **Step 6: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

```bash
git add src/TheLongestYear.Core/Availability/SourceReachability.cs tests/TheLongestYear.Tests/SourceReachabilityTests.cs CHANGELOG.md
git commit -m "v0.17.20: cooking rules, a dish needs both its ingredients and a learnable recipe"
```

---

### Task 6: Merge the verdict into the pools

**Files:**
- Modify: `src/TheLongestYear.Core/ItemPoolBuilder.cs` (`Build`, lines 62-84)
- Test: `tests/TheLongestYear.Tests/SourceReachabilityTests.cs`

**Interfaces:**
- Consumes: `SourceReachability.IsUnreachable` (Tasks 3 to 5).
- Produces: `ItemPoolBuilder.Build` gains a final optional parameter `SourceReachability? reachability = null`. Null keeps today's behaviour exactly, so every existing call site and test is unaffected.

- [ ] **Step 1: Write the failing test**

Append to `SourceReachabilityTests`:

```csharp
    [Fact]
    public void Unreachable_crop_never_reaches_the_crop_pool()
    {
        var crops = new[]
        {
            new RawCropEntry("(O)Parsnip", new[] { Season.Spring }, null, "(O)ParsnipSeed"),
            new RawCropEntry("(O)FishmongerCrop", new[] { Season.Fall }, null, "(O)FishmongerSeed"),
        };
        var objects = new Dictionary<string, RawObjectEntry>(StringComparer.Ordinal)
        {
            ["Parsnip"] = new("Basic", -75, 35, false, Array.Empty<string>()),
            ["FishmongerCrop"] = new("Basic", -75, 100, false, Array.Empty<string>()),
        };
        var rule = new SourceReachability(
            Unreachable,
            new[] { new RawShopListing("(O)FishmongerSeed", IslandShop), new RawShopListing("(O)ParsnipSeed", TownShop) },
            Placements, crops, Array.Empty<RawRecipeEntry>(), NoSpawns);

        ItemPools pools = ItemPoolBuilder.Build(
            crops, objects, Array.Empty<RawSpawnEntry>(), Array.Empty<RawSpawnEntry>(),
            new HashSet<string>(StringComparer.Ordinal), Array.Empty<RawMonsterDropEntry>(),
            Array.Empty<RawFruitTreeEntry>(), Array.Empty<RawGeodeDropEntry>(),
            new BundleGenerationTuning(), null, null, null, rule);

        Assert.DoesNotContain(pools.Crops, item => item.ItemId == "(O)FishmongerCrop");
        Assert.Contains(pools.Crops, item => item.ItemId == "(O)Parsnip");
    }
```

Read `RawObjectEntry`'s real constructor in `ItemPoolModel.cs:111` before writing this; match its parameter order exactly.

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter Unreachable_crop_never_reaches`
Expected: build error, `Build` takes 12 arguments, not 13.

- [ ] **Step 3: Add the parameter and merge**

In `ItemPoolBuilder.Build`, add the parameter after `festivalSeasons`:

```csharp
        IReadOnlyDictionary<string, Season>? festivalSeasons = null,
        SourceReachability? reachability = null)
```

Then extend the `excluded` set built at line 76, right after the `extraExcludedIds` union:

```csharp
        // Provably unreachable items (spec 2026-09-10-source-reachability). Merged HERE so every
        // pool inherits it: Vets() consults `excluded`, and all thirteen pools go through Vets.
        // Applies at every difficulty, unlike YearTwoCrops: this is impossibility, not pacing.
        if (reachability != null)
        {
            foreach (string id in AllCandidateIds(crops, objects, forageSpawns, fishSpawns))
                if (reachability.IsUnreachable(id))
                    excluded.Add(id);
        }
```

Add the helper near the other private statics:

```csharp
    /// <summary>Every id that could enter a pool, so the reachability rule is asked about each
    /// exactly once. Data/Objects is the superset for the category pools; crops and spawns are
    /// added because a harvest or catch need not have its own Data/Objects row in a mod.</summary>
    private static IEnumerable<string> AllCandidateIds(
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyList<RawSpawnEntry> fishSpawns)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string bare in objects.Keys)
            if (seen.Add(Qualify(bare))) yield return Qualify(bare);
        foreach (RawCropEntry crop in crops)
            if (crop?.HarvestItemId != null && seen.Add(Qualify(Unqualify(crop.HarvestItemId))))
                yield return Qualify(Unqualify(crop.HarvestItemId));
        foreach (RawSpawnEntry spawn in forageSpawns)
            if (spawn?.ItemId != null && seen.Add(Qualify(Unqualify(spawn.ItemId))))
                yield return Qualify(Unqualify(spawn.ItemId));
        foreach (RawSpawnEntry spawn in fishSpawns)
            if (spawn?.ItemId != null && seen.Add(Qualify(Unqualify(spawn.ItemId))))
                yield return Qualify(Unqualify(spawn.ItemId));
    }
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter Unreachable_crop_never_reaches`
Expected: 1 passed.

- [ ] **Step 5: Run the whole suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: 2041 passed, 0 failed. **Every pre-existing test must still pass**: `reachability` defaults to null, so nothing else changes behaviour. If any existing test fails, the merge is too eager. Stop and investigate.

- [ ] **Step 6: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

```bash
git add src/TheLongestYear.Core/ItemPoolBuilder.cs tests/TheLongestYear.Tests/SourceReachabilityTests.cs CHANGELOG.md
git commit -m "v0.17.21: unreachable items are excluded from every pool"
```

---

### Task 7: Read the real data at the game boundary

**Files:**
- Modify: `src/TheLongestYear/Loop/GameDataPools.cs`

**Interfaces:**
- Consumes: `RawShopListing`, `RawShopPlacement`, `RawLocationLink`, `RawRecipeEntry` (Tasks 2 and 3), `ReachabilityGraph.UnreachableLocations` (Task 2), `SourceReachability` (Tasks 3 to 5), `ItemPoolBuilder.Build`'s new parameter (Task 6).
- Produces: `GameDataPools.Build` constructs the `SourceReachability` and passes it through. Its public signature does not change.

- [ ] **Step 1: Read the shop and recipe tables**

Inside the existing `try` block in `Build`, alongside the other `Game1.content.Load` calls:

```csharp
            var shopListings = new List<RawShopListing>();
            var shopPlacements = new List<RawShopPlacement>();
            foreach (var kv in Game1.content.Load<Dictionary<string, StardewValley.GameData.Shops.ShopData>>("Data/Shops"))
            {
                if (kv.Value?.Items == null) continue;
                foreach (var entry in kv.Value.Items)
                {
                    if (entry == null || string.IsNullOrEmpty(entry.ItemId)) continue;
                    if (!ItemIsObject(entry.ItemId)) continue;
                    shopListings.Add(new RawShopListing(entry.ItemId, kv.Key, entry.IsRecipe));
                }
            }

            var recipes = new List<RawRecipeEntry>();
            foreach (var kv in Game1.content.Load<Dictionary<string, string>>("Data/CookingRecipes"))
            {
                // ingredients / unused / yield / unlockConditions / displayName
                string[] fields = (kv.Value ?? "").Split('/');
                if (fields.Length < 4) continue;
                string[] tokens = fields[0].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var ingredients = new List<string>();
                for (int i = 0; i < tokens.Length; i += 2) ingredients.Add(tokens[i]);
                string output = fields[2].Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? kv.Key;
                recipes.Add(new RawRecipeEntry(output, ingredients, fields[3]));
            }
```

Verify the `ShopData` / `ShopItemData` type and property names against the decompile at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley\StardewValley.GameData.Shops\` before compiling. `IsRecipe` is the flag The Fishmonger sets.

- [ ] **Step 2: Build the warp graph and place the shops**

```csharp
            var links = new List<RawLocationLink>();
            var allLocations = new List<string>();
            foreach (GameLocation location in Game1.locations)
            {
                if (location?.Name == null) continue;
                allLocations.Add(location.Name);
                foreach (StardewValley.Warp warp in location.warps)
                    if (!string.IsNullOrEmpty(warp?.TargetName))
                        links.Add(new RawLocationLink(location.Name, warp.TargetName));

                // A shop is "in" the location whose tiles open it.
                foreach (string shopId in ShopIdsOpenedIn(location))
                    shopPlacements.Add(new RawShopPlacement(shopId, location.Name));
            }
```

Add the tile scan:

```csharp
        /// <summary>Shop ids opened by an "OpenShop" tile action anywhere in this location. This is
        /// how a shop gets a place: Data/Shops itself records no location.</summary>
        private static IEnumerable<string> ShopIdsOpenedIn(GameLocation location)
        {
            var found = new HashSet<string>(StringComparer.Ordinal);
            xTile.Layers.Layer layer = location.Map?.GetLayer("Buildings");
            if (layer == null) yield break;
            for (int x = 0; x < layer.LayerWidth; x++)
            for (int y = 0; y < layer.LayerHeight; y++)
            {
                xTile.Tiles.Tile tile = layer.Tiles[x, y];
                if (tile == null) continue;
                if (!tile.Properties.TryGetValue("Action", out xTile.ObjectModel.PropertyValue value)) continue;
                string action = value?.ToString() ?? "";
                if (!action.StartsWith("OpenShop", StringComparison.OrdinalIgnoreCase)) continue;
                string[] parts = action.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && found.Add(parts[1])) yield return parts[1];
            }
        }
```

**The owner path is the important one, not the tile scan.** Verified against the real pack on
2026-09-10: The Fishmonger's shop is opened by talking to Constance. It has an `Owners` list and the
mod adds **no** `OpenShop` tile action anywhere. A tile scan alone finds nothing and the whole fix
does nothing for the mod that prompted it. Vanilla shopkeepers live at their shop (Pierre at
`SeedShop`, Willy at `FishShop`, Marnie at `AnimalShop`), so an owner's home is a sound placement.

Collect owners while reading `Data/Shops` in step 1, in the same loop:

```csharp
            // shopId -> owner NPC names, for placing shops that are opened by talking to someone.
            var shopOwners = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            // ... inside the Data/Shops loop, alongside the Items loop:
                foreach (var owner in kv.Value.Owners ?? new List<StardewValley.GameData.Shops.ShopOwnerData>())
                {
                    if (string.IsNullOrEmpty(owner?.Name)) continue;
                    if (!shopOwners.TryGetValue(kv.Key, out List<string> names))
                        shopOwners[kv.Key] = names = new List<string>();
                    if (!names.Contains(owner.Name)) names.Add(owner.Name);
                }
```

Then place each shop at both its owner's home and its owner's current location. Both count, and a
shop is only condemned when every placement is unreachable, so listing more places is the
conservative direction:

```csharp
            foreach (KeyValuePair<string, List<string>> shop in shopOwners)
            {
                foreach (string ownerName in shop.Value)
                {
                    if (ownerName == "AnyOrNone" || ownerName == "None") continue;
                    NPC npc = Game1.getCharacterFromName(ownerName);
                    if (npc == null) continue;
                    if (!string.IsNullOrEmpty(npc.DefaultMap))
                        shopPlacements.Add(new RawShopPlacement(shop.Key, npc.DefaultMap));
                    if (!string.IsNullOrEmpty(npc.currentLocation?.Name))
                        shopPlacements.Add(new RawShopPlacement(shop.Key, npc.currentLocation.Name));
                }
            }
```

Check `ShopOwnerData`'s real property names and the `DefaultMap` property against the decompile
before compiling. `DefaultMap` is the NPC's home map, which is what
`Data/Characters`'s `Home` entry sets.

- [ ] **Step 3: Construct the rule and pass it to the builder**

Just before the `ItemPoolBuilder.Build` call:

```csharp
            // Positive-reachability evidence: every id the game already told us spawns
            // somewhere, from tables this method has ALREADY read. Without this, an item that
            // is forageable AND listed in an island shop would be condemned by the shop rule
            // while its perfectly good spawn never got a vote.
            var reachableSpawnIds = new HashSet<string>(StringComparer.Ordinal);
            void MarkSpawn(string rawId)
            {
                if (string.IsNullOrEmpty(rawId)) return;
                reachableSpawnIds.Add(BundleParsing.NormalizeItemId(rawId));
            }
            foreach (RawSpawnEntry spawn in forage) MarkSpawn(spawn?.ItemId);
            foreach (RawSpawnEntry spawn in fish) MarkSpawn(spawn?.ItemId);
            foreach (RawMonsterDropEntry drop in drops) MarkSpawn(drop?.ItemId);
            foreach (RawGeodeDropEntry drop in geodeDrops) MarkSpawn(drop?.ItemId);
            foreach (RawFruitTreeEntry tree in fruitTrees)
                foreach (string fruit in tree?.FruitItemIds ?? System.Array.Empty<string>())
                    MarkSpawn(fruit);

            IReadOnlySet<string> unreachablePlaces = ReachabilityGraph.UnreachableLocations(
                links, allLocations,
                name => ItemPoolBuilder.IsExcludedLocation(name, tuning.ExcludedLocationMarkers));
            var reachability = new SourceReachability(
                unreachablePlaces, shopListings, shopPlacements, crops, recipes, reachableSpawnIds);
            this.LastReachability = reachability;
            _monitor?.Log(
                $"Reachability: {unreachablePlaces.Count} of {allLocations.Count} locations out of reach.",
                LogLevel.Trace);
```

**Fail open if any input read throws.** Every read above sits inside the existing `try` whose
`catch` logs "pools may be partial". A partial source graph is far worse than no source graph: it
looks authoritative while missing exactly the alternative route that would have kept an item. So
wrap the shop, recipe and warp reads in their own `try`, and on ANY exception leave
`LastReachability` null and pass null to the builder, which restores today's behaviour exactly:

```csharp
            SourceReachability reachability = null;
            try
            {
                // ... the shop, recipe, warp and spawn reads above ...
                reachability = new SourceReachability(
                    unreachablePlaces, shopListings, shopPlacements, crops, recipes, reachableSpawnIds);
            }
            catch (System.Exception ex)
            {
                _monitor?.Log(
                    $"Reachability derivation failed ({ex.GetType().Name}: {ex.Message}). " +
                    "No item will be excluded for reachability this generation.",
                    LogLevel.Warn);
                reachability = null;
            }
            this.LastReachability = reachability;
```

and add `reachability` as the final argument to `ItemPoolBuilder.Build`.

**Expose the instance.** Tasks 9 and 10 both need the same verdicts (the board repair and the
dump), and rebuilding them would re-read every table and risk a different answer. Add to
`GameDataPools`:

```csharp
        /// <summary>The reachability verdicts from the most recent <see cref="Build"/> on this
        /// instance. Null before the first call. Held so the board repair and tly_dumpbundles
        /// report exactly what the pools were built from, rather than re-deriving it.</summary>
        public SourceReachability LastReachability { get; private set; }
```

Callers that need it must keep the `GameDataPools` instance rather than discarding it:

```csharp
        var dataPools = new GameDataPools(this.Monitor);
        ItemPools pools = dataPools.Build(tuning, extraExcludedIds);
        SourceReachability reachability = dataPools.LastReachability;
```

Update every existing `new GameDataPools(...).Build(...)` call site that later needs the verdicts.
Call sites that do not need them may stay as they are.

- [ ] **Step 4: Log what was dropped**

After the `Build` call, beside the existing pool-count log:

```csharp
            if (reachability.Reasons.Count > 0)
            {
                _monitor?.Log($"Reachability: {reachability.Reasons.Count} items kept off the board.", LogLevel.Info);
                foreach (var reason in reachability.Reasons)
                    _monitor?.Log($"  {reason.Key}: {reason.Value}", LogLevel.Trace);
            }
```

- [ ] **Step 5: Build and run the suite**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: `Build succeeded.`
Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: 2041 passed.

- [ ] **Step 6: Verify on a vanilla save that nothing is wrongly dropped**

```
pwsh -NoProfile -File tools/deploy.ps1 -Minimized
tools/bridge.ps1 -Action send -Lines "tly_loadsave <None_*>"
tools/bridge.ps1 -Action send -Lines "tly_reset"
```

Expected in the log: `Reachability: N of M locations out of reach` with N covering the island maps, and **`Reachability: 0 items kept off the board`** on an unmodded install. Cactus Fruit and every Desert item must still appear in the pools; check the pool counts against the previous run's log line. **A non-zero drop count on vanilla means the rule is over-eager. Stop and report.**

- [ ] **Step 7: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

```bash
git add src/TheLongestYear/Loop/GameDataPools.cs CHANGELOG.md
git commit -m "v0.17.22: read shops, recipes and warps so reachability runs on live game data"
```

---

### Task 8: The Fishmonger regression fixture

Locks in the exact case that prompted the work, so it cannot silently regress.

**Files:**
- Create: `tests/TheLongestYear.Tests/Fixtures/fishmonger_sources.json`
- Create: `tests/TheLongestYear.Tests/FishmongerRegressionTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 2 to 6.
- Produces: nothing.

- [ ] **Step 1: Write the fixture**

Source data is in the extracted pack (re-extract from Nexus 16326 if the scratchpad is gone). Create `Fixtures/fishmonger_sources.json` with the real ids. Prefix every id with `(O)VoidWitchCult.CP.TheFishmongerNPC_`:

```json
{
  "shopId": "VoidWitchCult.TheFishmongerNPC_TheFishmongerSeeds",
  "shopLocation": "VoidWitchCult.TheFishmonger_Fishmonger_GI_Inside",
  "warpOut": "IslandSouth",
  "seeds": ["bloodstainedroseseeds", "freshdillseeds", "fisheyebeansseeds", "halcyonflowerseeds",
            "spicymustardseeds", "oceanflaxseeds", "petrichorseeds", "saltemmerseeds",
            "scallureseeds", "sunsetroseseeds"],
  "crops": ["bloodstainedrose", "freshdill", "fisheyebeans", "halcyonflower", "spicymustard",
            "oceanflax", "petrichor", "saltemmer", "scallure", "sunsetrose"],
  "dishesWithModdedIngredients": ["fishdillsauce", "savoryfishballs", "freshdillsauce",
                                  "Plokkfiskur", "potatoesdillsauce", "spicymustardsauce"],
  "dishesAllVanillaIngredients": ["bakedredsnappercurry", "crispyfishandchips",
                                  "mouthwateringfishburger", "fishcroquettesaioli",
                                  "crispysalmonschnitzel"],
  "islandFish": ["bicolorpiranha", "redfinhap", "bigfinsquid", "firemouth", "garfish", "matjes",
                 "moray", "scarletvampiresquid", "stripedtoby", "viperfish", "charcoalbetta",
                 "fairyflossbetta", "golddustblackmolly", "neontetra", "sheephead", "sunrisebetta",
                 "sunsetbetta", "glasseyesnapper", "kissingfish", "queentriggerfish",
                 "stoplightparrotfish"]
}
```

- [ ] **Step 2: Write the test**

Create `tests/TheLongestYear.Tests/FishmongerRegressionTests.cs`. Build the world (`Farm - Town - Beach - IslandSouth - GI_Inside`), mark `IslandSouth` forbidden via a marker predicate, list every seed and recipe in the island shop (recipes with `IsRecipe: true`, `unlock: "none"`), give each crop its seed, give the six modded-ingredient dishes an ingredient from the crop list and the five vanilla dishes only ids like `(O)150`, then assert:

**Use the real ingredient lists, category refs included.** Task 5's review noted that no test yet
exercises the category-ref skip in `FirstUnreachableIngredient`, because that task's fixtures had no
category refs in them. This fixture closes that gap for free, because the real recipes are full of
them: Crispy Fish and Chips is `-4` (any fish), `192`, `247`; Mouth Watering Fishburger is `-5` (any
egg), `216`, `256`; Fish Croquettes Aioli is `-4`, `246`, `247`, `248`. Transcribe those ingredient
lists as they actually are. A category ref must never condemn a dish, so these five must still drop
via the learnability rule alone, which is exactly what the second assertion below checks.

```csharp
    [Fact]
    public void All_ten_crops_and_all_eleven_dishes_are_unreachable()
    {
        SourceReachability rule = BuildFromFixture();
        foreach (string id in Fixture.Crops.Concat(Fixture.AllDishes))
            Assert.True(rule.IsUnreachable(id), $"{id} should be unreachable");
    }

    [Fact]
    public void The_five_all_vanilla_dishes_drop_via_learnability_not_ingredients()
    {
        SourceReachability rule = BuildFromFixture();
        foreach (string id in Fixture.DishesAllVanillaIngredients)
        {
            Assert.True(rule.IsUnreachable(id));
            Assert.Contains("recipe", rule.Reasons[id], StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Vanilla_ingredients_used_by_those_dishes_stay_reachable()
    {
        SourceReachability rule = BuildFromFixture();
        foreach (string id in new[] { "(O)150", "(O)192", "(O)260", "(O)246", "(O)247", "(O)216" })
            Assert.False(rule.IsUnreachable(id), $"{id} is vanilla and must stay allowed");
    }
```

- [ ] **Step 3: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter FishmongerRegressionTests`
Expected: 3 passed (full suite 2044). If the five all-vanilla dishes fail, the learnability rule from Task 5 is not firing; that is the whole point of this fixture.

- [ ] **Step 4: Run the whole suite, then commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

```bash
git add tests/TheLongestYear.Tests/Fixtures/fishmonger_sources.json tests/TheLongestYear.Tests/FishmongerRegressionTests.cs CHANGELOG.md
git commit -m "v0.17.23: regression fixture for the Fishmonger crops and dishes"
```

---

### Task 9: Repair boards built before the fix

**Files:**
- Create: `src/TheLongestYear/Loop/BoardRepairService.cs`
- Modify: `src/TheLongestYear.Core/BundleSlotFiller.cs` (add the `ReplacementFor` API below)
- Modify: `src/TheLongestYear/ModEntry.cs` (call it from the existing `SaveLoaded` handler, after the availability model is built)
- Test: `tests/TheLongestYear.Tests/BundleSlotFillerReplacementTests.cs`

**Interfaces:**
- Consumes: `SourceReachability` via `GameDataPools.LastReachability` (Task 7), `PoolDomainClassifier.Classify`, `BundleParsing`, `Game1.netWorldState.Value.BundleData`.
- Produces:
  - `BundleSlotFiller.ReplacementFor(...)` returning `PoolItem?` (signature below), the only new Core surface.
  - `internal sealed class BoardRepairService` with `public int RepairIfNeeded()`, returning the number of slots swapped.

**Three corrections from the plan review (2026-09-10), all verified against the code:**

1. **`BundleSlotFiller.Fill` fills a WHOLE bundle** and its candidate picker is private. Calling it
   on a live bundle fights the donated-slot rule, and calling it on a fabricated one-slot bundle
   loses recipe-part identity, which breaks composite bundles like Dye and Field Research where
   each slot belongs to a different part. **Add a narrow public Core API instead**, in
   `BundleSlotFiller`, and list that file as Modified for this task:

   ```csharp
   /// <summary>One replacement candidate for a single slot of an existing bundle, drawn from the
   /// same pool (and, for a Recipe bundle, the same PART) the slot came from, excluding everything
   /// already asked for on the board. Null when nothing suitable exists, which the caller must
   /// treat as "leave the slot alone", never as success.</summary>
   public static PoolItem? ReplacementFor(
       BundleSpec spec, int slotIndex, DomainMatch match, ItemPools pools,
       BundleGenerationTuning tuning, Random rng, IReadOnlySet<string> avoid,
       ItemAvailabilityModel? availability, PoolRecipe? knownRecipe)
   ```

2. **`SetBundleData` does not refresh the Community Center's ingredient cache.**
   `CommunityCenter` builds `bundlesIngredientsInfo` separately, and donation checks read THAT.
   Rewriting the board without refreshing it leaves the new ask on screen while the donation logic
   still wants the old item. After the final write, call the CC's refresh
   (`refreshBundlesIngredientsInfo`, verify the exact name in the decompile at
   `StardewValley.Locations/CommunityCenter.cs`). Step 4 must physically donate the replacement to
   prove it, not merely look at it.

3. **Host only.** `SaveLoaded` fires on multiplayer farmhands too, and mutating `NetWorldState`
   from a peer races the host. Guard the whole repair with `Context.IsMainPlayer`.

Quality and stack are preserved from the old slot ONLY when the replacement can carry them: check
`pools.QualityEligibleIds` before keeping a silver or gold ask, and fall back to quality 0.

- [ ] **Step 1: Write the service**

```csharp
/// <summary>Swaps provably unreachable asks out of a board that was generated before the
/// reachability rule existed (spec 2026-09-10-source-reachability).
///
/// Runs at save load, not at reset: a player mid-year is holding an old board, and if the
/// impossible ask is what blocks their season gate, waiting a year for the next rewind is not a
/// fix. A donated slot is NEVER touched, so nobody loses credit for something already handed in.</summary>
internal sealed class BoardRepairService
{
    // ... constructor takes IMonitor, SourceReachability, ItemPools, BundleGenerationTuning,
    // ItemAvailabilityModel, and the run seed for a deterministic Random.

    public int RepairIfNeeded()
    {
        // For each BundleData entry in a themed room (RoomThemeMap.TryGetTheme):
        //   parse it (BundleParsing.Parse)
        //   for each concrete ingredient slot i:
        //     skip category refs
        //     skip slots already donated (Game1.netWorldState.Value.Bundles[index][i] == true)
        //     if !reachability.IsUnreachable(id) continue
        //     pick a replacement from the same pool as the bundle's domain, avoiding ids already
        //     asked for anywhere on the board, preserving Stack and Quality
        //   if anything changed, rewrite that one key via SetBundleData
        // Return the number of slots swapped; log one line per swap plus a summary.
    }
}
```

Use `PoolDomainClassifier.Classify` to find the bundle's domain and `BundleSlotFiller.ReplacementFor` (added above) to pick the replacement, so a repaired slot comes from the same pool and the same recipe part the original did. Write back with the same merge-and-upsert call the engine uses (`Game1.netWorldState.Value.SetBundleData`), one key at a time, and never remove a key. A null return from `ReplacementFor` means leave the slot exactly as it is and log that the ask could not be replaced; it is never counted as a repair.

- [ ] **Step 2: Wire it into save load**

In `ModEntry`'s `SaveLoaded` handler, after `_availability` is built and the pools exist:

```csharp
            int repaired = new BoardRepairService(
                this.Monitor, reachability, pools, _config.PoolTuning, _availability, seed).RepairIfNeeded();
            if (repaired > 0)
                this.Monitor.Log(
                    $"Board repair: {repaired} unreachable asks replaced. Your donated items were left alone.",
                    LogLevel.Info);
```

- [ ] **Step 2b: Write the Core tests for the new API**

`ReplacementFor` is pure Core, so it is unit-testable: a slot whose pool has candidates returns one
that is not in `avoid`; a slot whose pool is exhausted returns null; a Recipe bundle returns a
candidate from the same part, not merely the same domain.

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter BundleSlotFiller`

- [ ] **Step 3: Verify on a clean board**

Deploy, load the throwaway save, confirm the log says nothing about repairs and `tly_gatecheck` reports the same numbers as before the update. **A clean board must not be touched.**

- [ ] **Step 4: Verify on a dirty board**

Hand-write an unreachable id into one slot of the throwaway save's board (edit `BundleData` in the
save XML while the game is closed, using an id the rule condemns), load, and confirm all five:

1. the slot is swapped and the log names it,
2. donated slots elsewhere are unchanged,
3. `tly_gatecheck` still passes,
4. **the replacement can actually be donated** (give yourself the item and hand it in). This is the
   cache test: if the donation is refused, the ingredient cache was not refreshed,
5. loading the same save a second time repairs nothing further (idempotent).

- [ ] **Step 5: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

Changelog under `### Fixed`, in player language:

```markdown
- **Impossible asks are cleared from boards that already have them.** If a bundle on your current
  board wants something this run can never reach, it is swapped for something you can actually
  get, the next time you load. Anything you had already donated stays donated.
```

```bash
git add src/TheLongestYear/Loop/BoardRepairService.cs src/TheLongestYear/ModEntry.cs CHANGELOG.md
git commit -m "v0.17.24: repair boards that already carry unreachable asks"
```

---

### Task 10: Report drops in `tly_dumpbundles`

The stale catalogue is what hid the Joja re-roll bug for a fortnight. Do not repeat it.

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (`CmdDumpBundles`, around line 3379, and `AppendPools`)

- [ ] **Step 1: Add a section to the dump**

After `AppendQuantityRules`, add `AppendReachability(sb, reachability)`:

```csharp
        private void AppendReachability(System.Text.StringBuilder sb, SourceReachability reachability)
        {
            sb.AppendLine("## Items kept off the board");
            sb.AppendLine();
            if (reachability.Reasons.Count == 0)
            {
                sb.AppendLine("Nothing. Every item in every pool has a route this run can reach.");
                sb.AppendLine();
                return;
            }
            sb.AppendLine($"{reachability.Reasons.Count} item(s) cannot be reached in a single loop:");
            sb.AppendLine();
            foreach (var reason in reachability.Reasons.OrderBy(r => r.Key, System.StringComparer.Ordinal))
                sb.AppendLine($"- **{reason.Key}**: {reason.Value}");
            sb.AppendLine();
        }
```

- [ ] **Step 2: Verify**

Deploy, run `tly_dumpbundles` on the vanilla install, confirm the new section reads "Nothing." Copy the file to `docs/engine-bundle-catalogue.md` (gitignored) so the repo copy stays current.

- [ ] **Step 3: Changelog and commit**

Add the changelog entry under `## Unreleased`. Do NOT touch `manifest.json`.

```bash
git add src/TheLongestYear/ModEntry.cs CHANGELOG.md
git commit -m "v0.17.25: tly_dumpbundles lists what reachability kept off the board"
```

---

### Task 11: End-to-end verification and the 0.18 release

**Files:**
- Modify: `src/TheLongestYear/manifest.json`, `CHANGELOG.md`, `README.md`, `docs/nexus-description.bbcode`

- [ ] **Step 1: Verify against the real mod**

Install The Fishmonger (Nexus 16326) plus its Custom Companions dependency into a **scratch** Stardew install or a temporary Mods folder, never Jeff's live one without asking. Load a save, reset, and run `tly_dumpbundles`.

Expected, matching the spec's success criterion: the 10 crops and 11 dishes appear under "Items kept off the board", the 21 island fish do not appear in any pool (the `Island` marker already handled them), and no player config edit was needed.

- [ ] **Step 2: Confirm nothing legitimate was lost**

On the same run, confirm Cactus Fruit and the other Desert items are still in the pools, and compare pool counts against a vanilla run. Report both numbers to Jeff.

- [ ] **Step 3: Run the full suite one more time**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: all passing. The running total after Task 8 is 2044, plus whatever Task 9's ReplacementFor tests add.

- [ ] **Step 4: Roll the version to 0.18.0 (the ONLY step in the whole plan that touches manifest.json)**

Set `manifest.json` to `0.18.0` (it has sat at `0.17.15` for the whole branch). Retitle the accumulated `## Unreleased` changelog section to `## 0.18.0 - <date>`, rewriting it as one coherent section in player language: lead with the player-visible fix, keep the developer entries brief.

- [ ] **Step 5: Update README and Nexus description together**

They must stay content-identical, differing only in markup. Update the "What's New in v0.18.0" section in both.

- [ ] **Step 6: Commit, then STOP**

```bash
git add -A
git commit -m "v0.18.0: items you cannot reach never reach your board"
```

**Do not push. Do not release. Do not post anything.** Take the release, the Nexus upload and the replies to pitytheviolins, RiseiJaku and ChaoticMindset to Jeff for an explicit yes.

---

## Self-Review

**Spec coverage:** Section 1 `SourceReachability` covers Tasks 3 to 5. Section 2 reachability walk covers Tasks 1 and 2. Section 3 source rules covers Tasks 3 to 5, learnability included. Section 4 applying the verdict covers Task 6, with the boundary reading in Task 7. Section 5 board repair covers Task 9. Section 6 diagnostics covers Tasks 1 and 10. Testing section covers Tasks 2 to 5 and 8. Risk 1 is Task 1, deliberately first. Risk 2 is Task 9 steps 3 and 4. Risk 3 is Task 7 step 6 and Task 11 step 2. Risk 4 has no dedicated task and is folded into Task 11 step 1 as an observation, which is proportionate for a walk over a few hundred nodes.

**Type consistency:** `SourceReachability`'s five-parameter constructor is fixed in Task 3 and unchanged by Tasks 4 and 5. `IsUnreachable(string)` and `Reasons` keep their signatures throughout. `RawCropEntry`'s new `SeedItemId` is the fourth positional parameter and optional, so existing call sites compile. `ItemPoolBuilder.Build`'s `reachability` parameter is last and optional for the same reason.

**Known gaps, deliberate:** heart-gated shop stock (`PLAYER_HEARTS Current TheFishmonger 6`) is not modelled; it does not matter here because the shop's location already condemns the items. Crafting recipes are read only if `Data/CraftingRecipes` is added alongside `Data/CookingRecipes` in Task 7 step 1; cooking alone covers the reported case, and crafting can follow the same shape if a report ever needs it.
