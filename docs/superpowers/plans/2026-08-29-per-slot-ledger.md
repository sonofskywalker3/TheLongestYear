# Per-Slot Donation Ledger Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the flat item-id donation ledger with a per-slot ledger mirrored from the Community Center board, so one deposit credits one bundle slot and the gate, the Season Goals page and the win all count what the board shows.

**Architecture:** `RunState` stores `DonatedSlot` records keyed by (vanilla bundle index, ingredient index). `BundleRequirement` gains `BundleIndex` and a positional `Slots` list (duplicates kept) and evaluates against a `SlotLedger`. `ItemDonationSync.Reconcile` replaces the ledger from the board's per-slot state and runs on save load, before the Season Goals page and before the day-end gate. Debug donation paths flip the vanilla slot before recording so the mirror never wipes them.

**Tech Stack:** C# / .NET 6, SMAPI mod, xunit tests in `tests/TheLongestYear.Tests`.

**Spec:** `docs/superpowers/specs/2026-08-29-per-slot-ledger-design.md`

## Global Constraints

- Branch `master` is the release line: bump `src/TheLongestYear/manifest.json` `Version` by one PATCH per commit, starting at `0.16.135`.
- Never push. Local commits only.
- No em dashes anywhere (code, comments, docs, log strings). Use commas, colons or "to".
- Run tests with `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q` from the repo root. Compile the mod project WITHOUT deploying (the game may be running and locks the DLL): `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false --nologo`.
- Commit messages: `v0.16.NNN: <what changed>` plus the two trailer lines every commit in this repo carries:
  `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01AiTTrtzoYBJfp2kEKrFLYf`.
- `RunState` is a POCO persisted through SMAPI's save data (Newtonsoft on the game side, `System.Text.Json` in tests). New persisted types are plain classes with public get/set properties, like `DoubleProduceRecord`.
- Test ledgers: build them with the `TestLedger` helper from Task 2, never with `new HashSet<string>()`.

## File map

| File | Responsibility after this plan |
|---|---|
| `src/TheLongestYear.Core/DonatedSlot.cs` (new) | Persisted per-slot ledger entry. |
| `src/TheLongestYear.Core/SlotLedger.cs` (new) | Read view over ledger entries: `IsFilled`, `FilledCount`, `ItemIds`; `Add` for sims. |
| `src/TheLongestYear.Core/RunState.cs` | `DonatedSlots`, `RecordDonation(int,int,string)`, `ReplaceDonations`, `DonatedLedger()`; `DonatedItemIds` marked legacy. |
| `src/TheLongestYear.Core/BundleSlot.cs` (new) | `(IngredientIndex, ItemId)` positional slot on a requirement. |
| `src/TheLongestYear.Core/BundleRequirement.cs` | `BundleIndex`, `Slots`, `MissingForSeason`, evaluators on `SlotLedger`. |
| `src/TheLongestYear.Core/BundleGate.cs` | Takes `SlotLedger`. |
| `src/TheLongestYear.Core/BundleClassifier.cs` | Builds `Slots` positionally, passes `parsed.Index`. |
| `src/TheLongestYear.Core/SeasonEase.cs`, `GeneratedBundleSet.cs`, `src/TheLongestYear/Donations/BundleCatalogBuilder.cs` | Re-created requirements keep `BundleIndex` and `Slots`. |
| `src/TheLongestYear.Core/RunManager.cs` | Reads `run.DonatedLedger()`. |
| `src/TheLongestYear.Core/CcDonationReconciler.cs` | `DonatedSlots(...)` yields `DonatedSlot`. |
| `src/TheLongestYear/Integration/ItemDonationSync.cs` | Mirror write (`ReplaceDonations`). |
| `src/TheLongestYear/Integration/CcSlotWriter.cs` (new) | `FirstOpenSlotFor(itemId)` and `TryFill(bundleIndex, ingredientIndex)` over `Game1.netWorldState`. |
| `src/TheLongestYear/Loop/RunController.cs` | Mirror on load; `Donate` flips the board; slot counts in logs. |
| `src/TheLongestYear/UI/MenuLauncher.cs` | Mirror before the Season Goals page. |
| `src/TheLongestYear/UI/SeasonGoalsMenu.cs` | Uses Core `MissingForSeason`; own copy deleted. |
| `src/TheLongestYear/Donations/DonationService.cs` | Records per slot. |
| `src/TheLongestYear/ModEntry.cs` | `tly_playseason` per slot; `tly_gateneeds`; `tly_donate` flips the board. |
| `tests/TheLongestYear.Tests/TestLedger.cs` (new) | Test helper that fills a requirement's slots by id. |

---

### Task 1: `DonatedSlot`, `SlotLedger`, and the `RunState` ledger API

**Files:**
- Create: `src/TheLongestYear.Core/DonatedSlot.cs`
- Create: `src/TheLongestYear.Core/SlotLedger.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs` (lines 23-24, 193-212, 258)
- Test: `tests/TheLongestYear.Tests/RunStateTests.cs`, create `tests/TheLongestYear.Tests/SlotLedgerTests.cs`

**Interfaces:**
- Produces:
  - `public sealed class DonatedSlot { public int BundleIndex {get;set;} public int IngredientIndex {get;set;} public string ItemId {get;set;} = ""; }`
  - `public sealed class SlotLedger { public SlotLedger(); public SlotLedger(IEnumerable<DonatedSlot> slots); public bool IsFilled(int bundleIndex, int ingredientIndex); public int FilledCount(int bundleIndex); public IReadOnlySet<string> ItemIds {get;} public int Count {get;} public bool Add(int bundleIndex, int ingredientIndex, string itemId); public IReadOnlyList<DonatedSlot> Entries {get;} }`
  - `RunState.List<DonatedSlot> DonatedSlots`, `bool RecordDonation(int bundleIndex, int ingredientIndex, string itemId)` (true when newly added), `void ReplaceDonations(IEnumerable<DonatedSlot> slots)`, `SlotLedger DonatedLedger()`.
  - The old `RecordDonation(string)`, `RecordCumulativeDonation(string)` and `DonatedSet()` are DELETED (the compiler then lists every caller for the later tasks).

- [ ] **Step 1: Write the failing tests**

Replace the ledger tests in `tests/TheLongestYear.Tests/RunStateTests.cs`. Delete `RecordCumulativeDonation_adds_to_the_cumulative_ledger_idempotently` and `RecordDonation_is_idempotent_per_item_id`; add:

```csharp
    [Fact]
    public void RecordDonation_is_idempotent_per_slot_and_keeps_two_slots_with_one_id()
    {
        var run = new RunState();
        Assert.True(run.RecordDonation(7, 0, "(O)388"));
        Assert.False(run.RecordDonation(7, 0, "(O)388"));   // same slot twice
        Assert.True(run.RecordDonation(7, 1, "(O)388"));    // Construction's second Wood slot
        Assert.Equal(2, run.DonatedSlots.Count);
        SlotLedger ledger = run.DonatedLedger();
        Assert.True(ledger.IsFilled(7, 0));
        Assert.True(ledger.IsFilled(7, 1));
        Assert.False(ledger.IsFilled(7, 2));
        Assert.Equal(2, ledger.FilledCount(7));
        Assert.Equal(0, ledger.FilledCount(8));
        Assert.Contains("(O)388", ledger.ItemIds);
    }

    [Fact]
    public void ReplaceDonations_replaces_the_whole_ledger()
    {
        var run = new RunState();
        run.RecordDonation(1, 0, "(O)24");
        run.ReplaceDonations(new[] { new DonatedSlot { BundleIndex = 2, IngredientIndex = 3, ItemId = "(O)190" } });
        Assert.Single(run.DonatedSlots);
        Assert.True(run.DonatedLedger().IsFilled(2, 3));
        Assert.False(run.DonatedLedger().IsFilled(1, 0));
    }

    [Fact]
    public void Legacy_DonatedItemIds_deserializes_but_is_not_the_ledger()
    {
        string json = "{\"DonatedItemIds\":[\"(O)24\"],\"Season\":0,\"DayOfMonth\":1}";
        RunState restored = JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(new[] { "(O)24" }, restored.DonatedItemIds);
        Assert.Empty(restored.DonatedSlots);
        Assert.Equal(0, restored.DonatedLedger().Count);
    }
```

Update the existing assertions: in `New_run_state_starts_at_spring_one_week_one` and `BeginNewRun_resets_everything_and_bumps_run_number` add `Assert.Empty(run.DonatedSlots);` next to the `DonatedItemIds` line. In `BeginNewMonth_advances_season_and_clears_selections_only` change the donation setup to `run.RecordDonation(0, 0, "Parsnip");` and the survival assertion to `Assert.Single(run.DonatedSlots);`. In `BeginNewRun_resets_everything_and_bumps_run_number` change its donation setup the same way. In `Round_trips_through_json` replace `DonatedItemIds = { "Parsnip", "CopperBar" },` with `DonatedSlots = { new DonatedSlot { BundleIndex = 3, IngredientIndex = 1, ItemId = "Parsnip" } },` and the matching assertion with:

```csharp
        Assert.Single(restored.DonatedSlots);
        Assert.Equal(3, restored.DonatedSlots[0].BundleIndex);
        Assert.Equal(1, restored.DonatedSlots[0].IngredientIndex);
        Assert.Equal("Parsnip", restored.DonatedSlots[0].ItemId);
```

Create `tests/TheLongestYear.Tests/SlotLedgerTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SlotLedgerTests
{
    [Fact]
    public void Add_is_idempotent_and_counts_per_bundle()
    {
        var ledger = new SlotLedger();
        Assert.True(ledger.Add(5, 0, "(O)388"));
        Assert.False(ledger.Add(5, 0, "(O)388"));
        Assert.True(ledger.Add(5, 1, "(O)388"));
        Assert.True(ledger.Add(6, 0, "(O)390"));
        Assert.Equal(2, ledger.FilledCount(5));
        Assert.Equal(1, ledger.FilledCount(6));
        Assert.Equal(3, ledger.Count);
        Assert.Equal(2, ledger.ItemIds.Count);
    }

    [Fact]
    public void Constructed_from_entries_answers_IsFilled()
    {
        var ledger = new SlotLedger(new[]
        {
            new DonatedSlot { BundleIndex = 1, IngredientIndex = 2, ItemId = "(O)24" },
        });
        Assert.True(ledger.IsFilled(1, 2));
        Assert.False(ledger.IsFilled(1, 1));
        Assert.False(ledger.IsFilled(2, 2));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q`
Expected: build errors (`DonatedSlot`, `SlotLedger`, `DonatedSlots` not found).

- [ ] **Step 3: Implement**

`src/TheLongestYear.Core/DonatedSlot.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>One filled Community Center slot in the run's donation ledger, keyed by the vanilla
/// bundle index and the ingredient's position in that bundle's Data/Bundles line (category slots
/// included in the numbering, so the index lines up with the board's bool[]). ItemId is the
/// normalized qualified id for display and id-level asks. Plain POCO for save serialization.</summary>
public sealed class DonatedSlot
{
    public int BundleIndex { get; set; }
    public int IngredientIndex { get; set; }
    public string ItemId { get; set; } = "";
}
```

`src/TheLongestYear.Core/SlotLedger.cs`:

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Read view of the per-slot donation ledger for the gate, the page and the sims.
/// A slot is filled or not; counts are per bundle. <see cref="ItemIds"/> is the distinct id set
/// for logging and any id-level question (never for progress counting).</summary>
public sealed class SlotLedger
{
    private readonly List<DonatedSlot> _entries = new();
    private readonly HashSet<(int Bundle, int Slot)> _filled = new();
    private readonly Dictionary<int, int> _countByBundle = new();
    private readonly HashSet<string> _ids = new(System.StringComparer.Ordinal);

    public SlotLedger() { }

    public SlotLedger(IEnumerable<DonatedSlot> slots)
    {
        if (slots == null) return;
        foreach (DonatedSlot s in slots)
            Add(s.BundleIndex, s.IngredientIndex, s.ItemId);
    }

    public IReadOnlyList<DonatedSlot> Entries => _entries;
    public IReadOnlySet<string> ItemIds => _ids;
    public int Count => _entries.Count;

    public bool IsFilled(int bundleIndex, int ingredientIndex) => _filled.Contains((bundleIndex, ingredientIndex));

    public int FilledCount(int bundleIndex) => _countByBundle.TryGetValue(bundleIndex, out int n) ? n : 0;

    /// <summary>Record a filled slot. False when it was already in the ledger.</summary>
    public bool Add(int bundleIndex, int ingredientIndex, string itemId)
    {
        if (!_filled.Add((bundleIndex, ingredientIndex))) return false;
        _entries.Add(new DonatedSlot { BundleIndex = bundleIndex, IngredientIndex = ingredientIndex, ItemId = itemId ?? "" });
        _countByBundle[bundleIndex] = FilledCount(bundleIndex) + 1;
        if (!string.IsNullOrEmpty(itemId)) _ids.Add(itemId);
        return true;
    }
}
```

`src/TheLongestYear.Core/RunState.cs`: replace lines 23-24 with

```csharp
    /// <summary>
    /// LEGACY (pre-slot ledger, 2026-08-29): the old id-only donation ledger. Kept ONLY so saves
    /// from older versions deserialize. Never read and never written by current code; cleared on
    /// <see cref="BeginNewRun"/>. The ledger is <see cref="DonatedSlots"/>.
    /// </summary>
    public List<string> DonatedItemIds { get; set; } = new();

    /// <summary>The run's donation ledger: every Community Center slot the board shows filled, one
    /// entry per slot (spec 2026-08-29-per-slot-ledger). Mirrored from the board by
    /// ItemDonationSync on load, before the Season Goals page and before the day-end gate, and kept
    /// current in between by the live DonationObserver.</summary>
    public List<DonatedSlot> DonatedSlots { get; set; } = new();
```

Replace the three ledger methods (`RecordDonation(string)`, `RecordCumulativeDonation(string)`, `DonatedSet()`) with:

```csharp
    /// <summary>Record a filled slot. Idempotent per (bundle, ingredient) pair; returns true when
    /// the slot was newly added. A repeated id in one bundle is two slots and two entries.</summary>
    public bool RecordDonation(int bundleIndex, int ingredientIndex, string itemId)
    {
        DonatedSlots ??= new List<DonatedSlot>();
        if (DonatedSlots.Exists(s => s.BundleIndex == bundleIndex && s.IngredientIndex == ingredientIndex))
            return false;
        DonatedSlots.Add(new DonatedSlot { BundleIndex = bundleIndex, IngredientIndex = ingredientIndex, ItemId = itemId ?? "" });
        return true;
    }

    /// <summary>The mirror write: the ledger becomes exactly the given slots (the board's state).</summary>
    public void ReplaceDonations(IEnumerable<DonatedSlot> slots)
    {
        var next = new List<DonatedSlot>();
        var seen = new HashSet<(int, int)>();
        foreach (DonatedSlot s in slots ?? System.Array.Empty<DonatedSlot>())
            if (seen.Add((s.BundleIndex, s.IngredientIndex)))
                next.Add(new DonatedSlot { BundleIndex = s.BundleIndex, IngredientIndex = s.IngredientIndex, ItemId = s.ItemId ?? "" });
        DonatedSlots = next;
    }

    /// <summary>The ledger as a read view for the gate, the page and the sims.</summary>
    public SlotLedger DonatedLedger() => new SlotLedger(DonatedSlots ?? new List<DonatedSlot>());
```

In `BeginNewRun` add `(DonatedSlots ??= new()).Clear();` right after `DonatedItemIds.Clear();`.

- [ ] **Step 4: Build the test project only and run the new tests**

The mod project and other tests will not compile yet (callers of the deleted methods). Run only Core + these tests:

Run: `dotnet build src/TheLongestYear.Core/TheLongestYear.Core.csproj --nologo -v q`
Expected: 0 errors.

(The full test run comes back green at the end of Task 3; until then, the compile errors are the to-do list.)

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.16.135`.

```bash
git add src/TheLongestYear.Core/DonatedSlot.cs src/TheLongestYear.Core/SlotLedger.cs src/TheLongestYear.Core/RunState.cs tests/TheLongestYear.Tests/RunStateTests.cs tests/TheLongestYear.Tests/SlotLedgerTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.135: per-slot donation ledger on RunState (DonatedSlot, SlotLedger); DonatedItemIds legacy"
```

---

### Task 2: `BundleRequirement` carries slot identity and evaluates a `SlotLedger`

**Files:**
- Create: `src/TheLongestYear.Core/BundleSlot.cs`
- Modify: `src/TheLongestYear.Core/BundleRequirement.cs`
- Modify: `src/TheLongestYear.Core/BundleGate.cs`
- Modify: `src/TheLongestYear.Core/SeasonEase.cs:29-31, 45-47, 52-54`
- Modify: `src/TheLongestYear.Core/GeneratedBundleSet.cs:56-58`
- Modify: `src/TheLongestYear/Donations/BundleCatalogBuilder.cs:181-183`
- Create: `tests/TheLongestYear.Tests/TestLedger.cs`
- Test: `tests/TheLongestYear.Tests/BundleRequirementTests.cs`, `BundleGateTests.cs`, `CuratedQuotaRampTests.cs`, `GeneratedBundleSetTests.cs`, `BundleClassifierTests.cs` (only their ledger call sites), `SeasonEaseTests.cs` (only if it compares `Slots`)

**Interfaces:**
- Consumes: `SlotLedger` (Task 1).
- Produces:
  - `public readonly record struct BundleSlot(int IngredientIndex, string ItemId);`
  - `BundleRequirement.int BundleIndex` (default -1), `IReadOnlyList<BundleSlot> Slots`.
  - Every factory gains two trailing optional parameters `int bundleIndex = -1, IReadOnlyList<BundleSlot>? slots = null`. Null slots means "one slot per ingredient, in order" (what every existing test assumes).
  - `public (int Count, IReadOnlyList<string> ItemIds) MissingForSeason(Season season, SlotLedger ledger)`
  - `public bool IsSatisfiedAtSeasonEnd(Season currentSeason, SlotLedger ledger)`
  - `public bool IsFullyComplete(SlotLedger ledger)`
  - `BundleGate.IsSatisfied(Season, SlotLedger, IReadOnlyList<BundleRequirement>, bool)` and `BundleGate.IsFullyDone(SlotLedger, IReadOnlyList<BundleRequirement>)`.
  - Test helper `TestLedger.Fill(params (BundleRequirement Req, string Id)[] fills)` and `TestLedger.Fill(BundleRequirement req, params string[] ids)`: fills EVERY slot of `req` whose `ItemId` is in the ids (so an id-level test reads as before), and `TestLedger.Empty()`.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/TestLedger.cs`:

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Builds a SlotLedger for tests by naming ids per requirement: every slot of the
/// requirement whose id is named is filled. Multi-bundle tests must give each requirement its own
/// bundleIndex (the factories default to -1, and two -1 bundles would share slots).</summary>
internal static class TestLedger
{
    public static SlotLedger Empty() => new SlotLedger();

    public static SlotLedger Fill(BundleRequirement req, params string[] ids)
    {
        var ledger = new SlotLedger();
        FillInto(ledger, req, ids);
        return ledger;
    }

    public static SlotLedger Fill(params (BundleRequirement Req, string Id)[] fills)
    {
        var ledger = new SlotLedger();
        foreach (var (req, id) in fills)
            FillInto(ledger, req, new[] { id });
        return ledger;
    }

    private static void FillInto(SlotLedger ledger, BundleRequirement req, IEnumerable<string> ids)
    {
        var wanted = new HashSet<string>(ids, System.StringComparer.Ordinal);
        foreach (BundleSlot slot in req.Slots)
            if (wanted.Contains(slot.ItemId))
                ledger.Add(req.BundleIndex, slot.IngredientIndex, slot.ItemId);
    }
}
```

Add to `tests/TheLongestYear.Tests/BundleRequirementTests.cs` (new section at the end of the class):

```csharp
    // ===== Per-slot ledger (spec 2026-08-29-per-slot-ledger) =====

    private static BundleRequirement Construction(int bundleIndex) => BundleRequirement.CreatePerItem(
        "Construction", Theme.Foraging,
        new[] { "(O)388", "(O)390", "(O)709" },
        new Dictionary<string, Season> { ["(O)388"] = Season.Spring, ["(O)390"] = Season.Spring, ["(O)709"] = Season.Summer },
        bundleIndex: bundleIndex,
        slots: new[] { new BundleSlot(0, "(O)388"), new BundleSlot(1, "(O)388"), new BundleSlot(2, "(O)390"), new BundleSlot(3, "(O)709") });

    [Fact]
    public void Slots_default_to_one_per_ingredient_in_order_and_BundleIndex_to_minus_one()
    {
        var b = BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging, new[] { "A", "B" }, Season.Spring);
        Assert.Equal(-1, b.BundleIndex);
        Assert.Equal(new[] { new BundleSlot(0, "A"), new BundleSlot(1, "B") }, b.Slots);
    }

    [Fact]
    public void One_deposit_credits_one_bundle_not_every_bundle_listing_the_id()
    {
        var foraging = BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging, new[] { "(O)296", "(O)16" }, Season.Spring, bundleIndex: 1);
        var childrens = BundleRequirement.CreateSeasonal("Children's", Theme.Foraging, new[] { "(O)296", "(O)18" }, Season.Spring, bundleIndex: 2);
        SlotLedger ledger = TestLedger.Fill((foraging, "(O)296"), (foraging, "(O)16"));
        Assert.True(foraging.IsSatisfiedAtSeasonEnd(Season.Spring, ledger));
        Assert.False(childrens.IsSatisfiedAtSeasonEnd(Season.Spring, ledger));
        Assert.Equal(2, ledger.FilledCount(1));
        Assert.Equal(0, ledger.FilledCount(2));
    }

    [Fact]
    public void A_doubled_id_needs_both_slots_filled()
    {
        var construction = Construction(13);
        var ledger = new SlotLedger();
        ledger.Add(13, 0, "(O)388");
        ledger.Add(13, 2, "(O)390");
        ledger.Add(13, 3, "(O)709");
        Assert.False(construction.IsFullyComplete(ledger));            // 3 of 4 slots
        Assert.False(construction.IsSatisfiedAtSeasonEnd(Season.Spring, ledger));  // Wood slot 1 is pinned Spring too
        var missing = construction.MissingForSeason(Season.Spring, ledger);
        Assert.Equal(1, missing.Count);
        Assert.Equal(new[] { "(O)388" }, missing.ItemIds);
        ledger.Add(13, 1, "(O)388");
        Assert.True(construction.IsFullyComplete(ledger));
        Assert.True(construction.IsSatisfiedAtSeasonEnd(Season.Winter, ledger));
    }

    [Fact]
    public void Percentage_counts_only_its_own_bundles_slots()
    {
        var crab = BundleRequirement.CreatePercentage("Crab Pot", Theme.Fishing, new[] { "A", "B", "C", "D" }, 3, new[] { 1, 2, 3, 3 }, bundleIndex: 4);
        var other = BundleRequirement.CreateSeasonal("Other", Theme.Fishing, new[] { "A", "B" }, Season.Spring, bundleIndex: 5);
        SlotLedger ledger = TestLedger.Fill((other, "A"), (other, "B"));
        Assert.False(crab.IsSatisfiedAtSeasonEnd(Season.Spring, ledger));
        var missing = crab.MissingForSeason(Season.Summer, ledger);
        Assert.Equal(2, missing.Count);
        Assert.Equal(new[] { "A", "B", "C", "D" }, missing.ItemIds);
    }

    [Fact]
    public void MissingForSeason_count_zero_matches_IsSatisfiedAtSeasonEnd_for_every_kind()
    {
        var seasonal = BundleRequirement.CreateSeasonal("S", Theme.Foraging, new[] { "A", "B" }, Season.Summer, bundleIndex: 1);
        var perItem = BundleRequirement.CreatePerItem("P", Theme.Mining, new Dictionary<string, Season> { ["X"] = Season.Spring, ["Y"] = Season.Fall }, bundleIndex: 2);
        var pct = BundleRequirement.CreatePercentage("Q", Theme.Farming, new[] { "M", "N", "O" }, 2, new[] { 1, 1, 2, 2 }, bundleIndex: 3);
        SlotLedger ledger = TestLedger.Fill((seasonal, "A"), (perItem, "X"), (pct, "M"));
        foreach (Season s in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
        {
            foreach (BundleRequirement r in new[] { seasonal, perItem, pct })
                Assert.Equal(r.IsSatisfiedAtSeasonEnd(s, ledger), r.MissingForSeason(s, ledger).Count == 0);
        }
        Assert.Equal(0, seasonal.MissingForSeason(Season.Spring, ledger).Count);   // not due yet
        Assert.Equal(1, seasonal.MissingForSeason(Season.Summer, ledger).Count);
        Assert.Equal(1, perItem.MissingForSeason(Season.Fall, ledger).Count);
        Assert.Equal(1, pct.MissingForSeason(Season.Fall, ledger).Count);
    }
```

Mechanical rewrite of existing ledger call sites in the test files (10 in `BundleRequirementTests.cs`, 8 in `BundleGateTests.cs`, 3 in `CuratedQuotaRampTests.cs`, 1 in `GeneratedBundleSetTests.cs`, 3 in `BundleClassifierTests.cs`):

- `new HashSet<string>()` passed as a ledger becomes `TestLedger.Empty()`.
- `new HashSet<string> { "a", "b" }` used against ONE requirement `b` becomes `TestLedger.Fill(b, "a", "b")`.
- Multi-bundle ledgers (`BundleGateTests`, `GeneratedBundleSetTests:139`): give each requirement in the fixture a distinct `bundleIndex:` (0, 1, 2 ...) and build the ledger with the tuple overload, naming the bundle each id goes to. In `IsFullyDone_requires_every_bundle_to_be_X_complete` the Crab Pot 2-then-3 check becomes two ledgers (one with two Crab Pot ids, one with three).
- `GeneratedBundleSetTests:139` (`springOnly`): build the ledger by iterating the requirements: `foreach (var r in reqs) foreach (var slot in r.Slots) if (springOnlyIds.Contains(slot.ItemId)) ledger.Add(r.BundleIndex, slot.IngredientIndex, slot.ItemId);` (requirements from `GeneratedBundleSet.BuildRequirements` carry real indexes after Task 3; until then this test may fail on index collisions, which Task 3 resolves).
- `BundleClassifierTests:159-173` (Construction, "IsFullyComplete still demands all 3 distinct donations"): rewrite after Task 3 (see Task 3 Step 1); leave compiling here by switching to `TestLedger.Fill(req, ...)`.

- [ ] **Step 2: Run to verify failure**

Run: `dotnet build src/TheLongestYear.Core/TheLongestYear.Core.csproj --nologo -v q`
Expected: the Core builds (nothing there references the tests). Then `dotnet build tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q` fails on `BundleSlot`, `bundleIndex:`, `slots:`, `MissingForSeason`.

- [ ] **Step 3: Implement**

`src/TheLongestYear.Core/BundleSlot.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>One concrete slot of a bundle: its position in the Data/Bundles ingredient line
/// (category slots count in the numbering, so it lines up with the board's bool[]) and the
/// normalized qualified id it wants. A repeated id is two slots.</summary>
public readonly record struct BundleSlot(int IngredientIndex, string ItemId);
```

`src/TheLongestYear.Core/BundleRequirement.cs`:

1. Add the two properties after `NumberOfSlots`:

```csharp
    /// <summary>The vanilla bundle index (the number after the slash in the Data/Bundles key),
    /// the key the per-slot ledger uses. -1 when the requirement was built without a board
    /// (tests, legacy fixtures).</summary>
    public int BundleIndex { get; }

    /// <summary>Every concrete slot in board order, duplicates kept. Progress is counted here;
    /// <see cref="Ingredients"/> stays the distinct id list for pools, pins and goals.</summary>
    public IReadOnlyList<BundleSlot> Slots { get; }
```

2. Extend the private constructor with `int bundleIndex, IReadOnlyList<BundleSlot>? slots` (last two parameters) and set:

```csharp
        BundleIndex = bundleIndex;
        Slots = slots ?? ingredients.Select((id, i) => new BundleSlot(i, id)).ToList();
```

3. Add `int bundleIndex = -1, IReadOnlyList<BundleSlot>? slots = null` as the last two parameters of `CreateSeasonal`, both `CreatePerItem` overloads, and `CreatePercentage`; pass them through (`bundleIndex: bundleIndex, slots: slots`). The pins-only `CreatePerItem` overload forwards them to the full one.

4. Replace `IsSatisfiedAtSeasonEnd` and `IsFullyComplete` with:

```csharp
    /// <summary>True if this bundle's contribution to <paramref name="currentSeason"/>'s
    /// day-28 gate is satisfied by the per-slot ledger.</summary>
    public bool IsSatisfiedAtSeasonEnd(Season currentSeason, SlotLedger ledger)
        => MissingForSeason(currentSeason, ledger).Count == 0;

    /// <summary>True if the bundle is complete on the board: at least X of its slots filled.</summary>
    public bool IsFullyComplete(SlotLedger ledger)
    {
        if (ledger is null) throw new ArgumentNullException(nameof(ledger));
        return ledger.FilledCount(BundleIndex) >= NumberOfSlots;
    }

    /// <summary>For the <paramref name="season"/> checkpoint: how many more slots the gate needs and
    /// which ids could fill them. Seasonal: every unfilled slot once its season is due. PerItem:
    /// every unfilled slot whose id is pinned at or before the season (a doubled id demands every
    /// slot with that id). Percentage: required minus filled, with every unfilled slot's id as a
    /// candidate (the count and the list differ there: Quality Crops needs 1 of 4 in Spring). The
    /// Season Goals page, the gate and tly_gateneeds all read this one method.</summary>
    public (int Count, IReadOnlyList<string> ItemIds) MissingForSeason(Season season, SlotLedger ledger)
    {
        if (ledger is null) throw new ArgumentNullException(nameof(ledger));
        switch (Kind)
        {
            case BundleKind.Seasonal:
                if ((int)SeasonalSeason!.Value > (int)season) return (0, Array.Empty<string>());
                var sItems = UnfilledIds(ledger, _ => true);
                return (sItems.Count, sItems);

            case BundleKind.PerItem:
                var pItems = UnfilledIds(ledger, id => ItemSeasonPins!.TryGetValue(id, out Season due) && (int)due <= (int)season);
                return (pItems.Count, pItems);

            case BundleKind.Percentage:
                int required = CumulativeRequiredBySeason![(int)season];
                int countNeeded = Math.Max(0, required - ledger.FilledCount(BundleIndex));
                if (countNeeded == 0) return (0, Array.Empty<string>());
                return (countNeeded, UnfilledIds(ledger, _ => true));

            default:
                throw new InvalidOperationException($"Unknown bundle kind: {Kind}");
        }
    }

    /// <summary>Ids of the unfilled slots whose id passes <paramref name="wanted"/>, ordinal order,
    /// one entry per slot (a doubled id unfilled twice appears twice).</summary>
    private List<string> UnfilledIds(SlotLedger ledger, Func<string, bool> wanted)
    {
        var result = new List<string>();
        foreach (BundleSlot slot in Slots)
            if (!ledger.IsFilled(BundleIndex, slot.IngredientIndex) && wanted(slot.ItemId))
                result.Add(slot.ItemId);
        result.Sort(StringComparer.Ordinal);
        return result;
    }
```

Note for `Percentage_counts_only_its_own_bundles_slots`: `UnfilledIds` returns unfilled ids (all four), matching the assertion. Note for `A_doubled_id_needs_both_slots_filled`: with slots 0, 2, 3 filled, the only unfilled slot is 1 (Wood), so `ItemIds` is `["(O)388"]` and the count is 1.

5. `src/TheLongestYear.Core/BundleGate.cs`: change both `ISet<string> donated` parameters to `SlotLedger ledger`, the null checks to `ledger is null`, and the calls to `b.IsSatisfiedAtSeasonEnd(currentSeason, ledger)` / `b.IsFullyComplete(ledger)`. Update the class doc: "the question is whether the per-slot ledger fills every bundle's season-N requirement at day 28".

6. Pass-through in the three re-creation sites so a rebuilt requirement keeps its board identity. `SeasonEase.cs:29-31` becomes:

```csharp
                return BundleRequirement.CreatePercentage(
                    req.Name, req.Theme, req.Ingredients, req.NumberOfSlots, ramp,
                    req.IngredientStacks, req.IngredientQualities, stretchLines: req.StretchLines,
                    bundleIndex: req.BundleIndex, slots: req.Slots);
```

Same two named arguments appended at `SeasonEase.cs:45-47` (PerItem) and `SeasonEase.cs:52-54` (Seasonal), at `GeneratedBundleSet.cs:56-58`, and at `BundleCatalogBuilder.cs:181-183`.

- [ ] **Step 4: Build Core and the test project**

Run: `dotnet build src/TheLongestYear.Core/TheLongestYear.Core.csproj --nologo -v q`
Expected: 0 errors.
Run: `dotnet build tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q`
Expected: may still fail if the test project references the mod project (it does: check `tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj` ProjectReference). If so, the remaining errors are all in `src/TheLongestYear` callers of the deleted ledger methods, which Tasks 4 to 8 fix. Record the error list; it is the checklist.

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.16.136`.

```bash
git add src/TheLongestYear.Core/BundleSlot.cs src/TheLongestYear.Core/BundleRequirement.cs src/TheLongestYear.Core/BundleGate.cs src/TheLongestYear.Core/SeasonEase.cs src/TheLongestYear.Core/GeneratedBundleSet.cs src/TheLongestYear/Donations/BundleCatalogBuilder.cs tests/TheLongestYear.Tests/TestLedger.cs tests/TheLongestYear.Tests/BundleRequirementTests.cs tests/TheLongestYear.Tests/BundleGateTests.cs tests/TheLongestYear.Tests/CuratedQuotaRampTests.cs tests/TheLongestYear.Tests/GeneratedBundleSetTests.cs tests/TheLongestYear.Tests/BundleClassifierTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.136: BundleRequirement carries BundleIndex and positional Slots; MissingForSeason; gate evaluates a SlotLedger"
```

---

### Task 3: The classifier builds real slots; `RunManager` reads the ledger

**Files:**
- Modify: `src/TheLongestYear.Core/BundleClassifier.cs` (the `Classify` method's five factory calls at lines 124, 144, 158, 191, 199; add `CollectSlots` next to `CollectQualifiedIngredients` at line 255)
- Modify: `src/TheLongestYear.Core/RunManager.cs:35, 47-53`
- Test: `tests/TheLongestYear.Tests/BundleClassifierTests.cs` (the Construction test at lines ~155-175), `tests/TheLongestYear.Tests/RunManagerTests.cs`

**Interfaces:**
- Consumes: `BundleSlot`, factory `bundleIndex:`/`slots:` (Task 2), `RunState.DonatedLedger()` (Task 1).
- Produces: every requirement built from a `ParsedBundle` has `BundleIndex == parsed.Index` and `Slots` = concrete slots positionally.

- [ ] **Step 1: Write the failing tests**

In `tests/TheLongestYear.Tests/BundleClassifierTests.cs` find the Construction test around line 155 (the one whose comment says "IsFullyComplete still demands all 3 distinct donations"). Rewrite its body so it asserts the new truth (keep the bundle data string it already builds: Wood x99 twice, Stone, Hardwood, 4 slots):

```csharp
        Assert.Equal(new[] { "(O)388", "(O)390", "(O)709" }, req!.Ingredients);   // distinct ids stay
        Assert.Equal(4, req.Slots.Count);
        Assert.Equal(new BundleSlot(0, "(O)388"), req.Slots[0]);
        Assert.Equal(new BundleSlot(1, "(O)388"), req.Slots[1]);
        Assert.Equal(parsedIndex, req.BundleIndex);   // parsedIndex = the number in the key the test used, e.g. 13 for "Crafts Room/13"

        var ledger = new SlotLedger();
        ledger.Add(req.BundleIndex, 0, "(O)388");
        ledger.Add(req.BundleIndex, 2, "(O)390");
        ledger.Add(req.BundleIndex, 3, "(O)709");
        Assert.False(req.IsFullyComplete(ledger));   // 3 of 4: the board says Construction is open
        ledger.Add(req.BundleIndex, 1, "(O)388");
        Assert.True(req.IsFullyComplete(ledger));
```

Update the comment above the PerItem branch in the classifier (see Step 3) and the test's own comment to say the doubled slot is now a second slot.

Add to `BundleClassifierTests.cs`:

```csharp
    [Fact]
    public void Slots_skip_category_refs_without_shifting_indexes()
    {
        // slot 0 category (-5), slot 1 Parsnip, slot 2 Milk: concrete slots keep indexes 1 and 2.
        var parsed = BundleParsing.Parse("Pantry/2", "Animal/O 176 1/-5 1 0 24 1 0 184 1 0/0/3/0/Animal");
        var req = BundleClassifier.Classify(parsed, Theme.Farming,
            new Dictionary<string, Season>(), new Dictionary<string, int[]>(), null);
        Assert.NotNull(req);
        Assert.Equal(2, req!.BundleIndex);
        Assert.Equal(new[] { new BundleSlot(1, "(O)24"), new BundleSlot(2, "(O)184") }, req.Slots);
    }
```

(Match the `Classify` parameter list the file's other tests already use; if they pass an availability model or different dictionary types, copy that call shape.)

Rewrite `tests/TheLongestYear.Tests/RunManagerTests.cs`: give the four fixture bundles `bundleIndex: 0` to `3`, replace every `run.RecordDonation("id")` with a slot record through a local helper, and add the Construction case:

```csharp
    private static void Donate(RunState run, IReadOnlyList<BundleRequirement> bundles, params string[] ids)
    {
        var wanted = new HashSet<string>(ids);
        foreach (BundleRequirement b in bundles)
            foreach (BundleSlot slot in b.Slots)
                if (wanted.Contains(slot.ItemId))
                    run.RecordDonation(b.BundleIndex, slot.IngredientIndex, slot.ItemId);
    }
```

so e.g. `Month_end_with_all_in_season_items_donated_advances` becomes:

```csharp
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        var bundles = SimpleBundles();
        Donate(run, bundles, "spring-1", "spring-2");
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, bundles, VaultOk));
```

Apply the same shape to the other five donation tests. Add:

```csharp
    private static IReadOnlyList<BundleRequirement> BoardWithConstruction() => new[]
    {
        BundleRequirement.CreatePerItem("Construction", Theme.Foraging,
            new[] { "wood", "stone", "hardwood" },
            new Dictionary<string, Season> { ["wood"] = Season.Winter, ["stone"] = Season.Winter, ["hardwood"] = Season.Winter },
            bundleIndex: 13,
            slots: new[] { new BundleSlot(0, "wood"), new BundleSlot(1, "wood"), new BundleSlot(2, "stone"), new BundleSlot(3, "hardwood") })
    };

    [Fact]
    public void Winter_end_with_a_doubled_slot_open_does_not_win()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        run.RecordDonation(13, 0, "wood");
        run.RecordDonation(13, 2, "stone");
        run.RecordDonation(13, 3, "hardwood");
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, BoardWithConstruction(), VaultOk));
        run.RecordDonation(13, 1, "wood");
        Assert.Equal(RunAction.Win, Mgr().EvaluateDayEnd(run, BoardWithConstruction(), VaultOk));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet build tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q`
Expected: errors in `RunManager.cs` (`DonatedSet` missing) and, once that compiles, the new classifier tests fail on `BundleIndex == -1` / `Slots.Count == 3`.

- [ ] **Step 3: Implement**

`BundleClassifier.cs`: add next to `CollectQualifiedIngredients`:

```csharp
    /// <summary>Every concrete slot in board order, duplicates kept, category refs skipped WITHOUT
    /// renumbering (the index must line up with the board's per-slot bool[]).</summary>
    private static List<BundleSlot> CollectSlots(ParsedBundle parsed)
    {
        var result = new List<BundleSlot>();
        for (int i = 0; i < parsed.Ingredients.Count; i++)
        {
            string itemRef = parsed.Ingredients[i].ItemRef;
            if (BundleParsing.IsCategoryRef(itemRef)) continue;
            result.Add(new BundleSlot(i, BundleParsing.NormalizeItemId(itemRef)));
        }
        return result;
    }
```

At the top of `Classify`, right after `ingredients` is collected, add `List<BundleSlot> slots = CollectSlots(parsed);`. Append `bundleIndex: parsed.Index, slots: slots` to all five factory calls (lines 124, 144, 158, 191, 199). Replace the PerItem comment "Vanilla Construction lists Wood twice (X=4, Y=3 deduped); the set-based donation ledger satisfies the duplicate slot implicitly when wood is donated once." with "Vanilla Construction lists Wood twice (X=4, Y=3 deduped): both Wood slots are in Slots and the per-slot ledger needs both filled."

`RunManager.cs`: change line 47 to `SlotLedger donated = run.DonatedLedger();` and the doc line 35 to "only Season, DayOfMonth and DonatedSlots are read". The two `BundleGate` calls compile unchanged (parameter type changed in Task 2).

- [ ] **Step 4: Build**

Run: `dotnet build src/TheLongestYear.Core/TheLongestYear.Core.csproj --nologo -v q`
Expected: 0 errors. The test project still cannot run until the mod project compiles (Tasks 4 to 8).

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.16.137`.

```bash
git add src/TheLongestYear.Core/BundleClassifier.cs src/TheLongestYear.Core/RunManager.cs tests/TheLongestYear.Tests/BundleClassifierTests.cs tests/TheLongestYear.Tests/RunManagerTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.137: classifier builds positional slots with the vanilla index; RunManager gates on the slot ledger; Construction needs all four slots"
```

---

### Task 4: The reconciler yields slots and `ItemDonationSync` mirrors the board

**Files:**
- Modify: `src/TheLongestYear.Core/CcDonationReconciler.cs`
- Modify: `src/TheLongestYear/Integration/ItemDonationSync.cs`
- Test: `tests/TheLongestYear.Tests/CcDonationReconcilerTests.cs`

**Interfaces:**
- Produces: `public static IEnumerable<DonatedSlot> CcDonationReconciler.DonatedSlots(IReadOnlyDictionary<string,string>? bundleData, Func<int, bool[]?>? slotCompletionForIndex)`. `DonatedConcreteIds` is deleted.
- `ItemDonationSync.Reconcile(RunState run)` now returns `int` (slots mirrored, or -1 when the board was unavailable and the ledger was left alone).

- [ ] **Step 1: Write the failing tests**

In `CcDonationReconcilerTests.cs` change the `Run` helper to:

```csharp
    private static List<DonatedSlot> Run(
        Dictionary<string, string> data, Dictionary<int, bool[]> completion)
        => CcDonationReconciler
            .DonatedSlots(data, idx => completion.TryGetValue(idx, out var a) ? a : null)
            .ToList();

    private static (int, int, string) Key(DonatedSlot s) => (s.BundleIndex, s.IngredientIndex, s.ItemId);
```

and rewrite the assertions: `Yields_only_completed_concrete_slots` expects `new[] { (0, 0, "(O)24"), (0, 2, "(O)190") }` via `donated.Select(Key)`; `Skips_category_slot_but_keeps_concrete_slots_aligned` expects `new[] { (0, 1, "(O)24") }`; `Skips_vault_and_other_non_item_rooms` stays `Assert.Empty`; `Normalizes_bare_and_qualified_ids` expects `(13, 0, "(O)388")` and `(13, 1, "(O)709")`. Apply the same shape to any remaining tests in the file. Add:

```csharp
    [Fact]
    public void A_repeated_id_yields_one_slot_per_position()
    {
        var data = new Dictionary<string, string> { ["Crafts Room/13"] = Bundle("388 99 0 388 99 0 390 99 0 709 10 0", 4) };
        var completion = new Dictionary<int, bool[]> { [13] = new[] { true, true, false, true } };
        Assert.Equal(new[] { (13, 0, "(O)388"), (13, 1, "(O)388"), (13, 3, "(O)709") }, Run(data, completion).Select(Key));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet build tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q`
Expected: `DonatedSlots` not found.

- [ ] **Step 3: Implement**

`CcDonationReconciler.cs`: rename the method to `DonatedSlots`, return type `IEnumerable<DonatedSlot>`, and change the yield to

```csharp
                yield return new DonatedSlot
                {
                    BundleIndex = bundle.Index,
                    IngredientIndex = i,
                    ItemId = BundleParsing.NormalizeItemId(itemRef),
                };
```

Update the summary: it yields "the (bundle index, ingredient index, id) of every completed concrete slot", and the class doc's last paragraph: ids line up with `BundleRequirement.Slots`.

`ItemDonationSync.cs`: replace the class body with

```csharp
    /// <summary>
    /// Mirrors the run's per-slot donation ledger from the vanilla CC's own bundle state, the
    /// source of truth for what the player has deposited (spec 2026-08-29-per-slot-ledger). Whole
    /// replace, never a union: the ledger can lag the board (the live DonationObserver only sees
    /// deposits while the JunimoNoteMenu is open) but can never be ahead of it. Runs on save load
    /// (which is also the migration from the old id-only ledger), before the Season Goals page and
    /// before the day-end gate, so the page and the gate always judge the same board.
    ///
    /// JP is deliberately NOT awarded here: the live observer already paid for what it caught.
    /// Single-player + master + TLY-active only. Returns the number of filled slots mirrored, or
    /// -1 when the board was unavailable and the ledger was left untouched.
    /// </summary>
    internal static class ItemDonationSync
    {
        public static int Reconcile(RunState run)
        {
            if (run == null) return -1;
            if (!RunActivation.IsActive) return -1;
            if (!Game1.IsMasterGame || Game1.IsMultiplayer) return -1;

            var worldState = Game1.netWorldState?.Value;
            var bundleData = worldState?.BundleData;
            var bundles = worldState?.Bundles;
            if (bundleData == null || bundles?.FieldDict == null) return -1;

            // NetBundles' indexer returns the bool[] slot array directly; FieldDict.ContainsKey is
            // the safe presence check (indexing a missing key would throw, see VaultPaymentSync).
            var slots = CcDonationReconciler.DonatedSlots(
                bundleData,
                idx => bundles.FieldDict.ContainsKey(idx) ? bundles[idx] : null).ToList();
            run.ReplaceDonations(slots);
            return slots.Count;
        }
    }
```

(add `using System.Linq;`).

- [ ] **Step 4: Build Core and run the reconciler tests when the solution compiles**

Run: `dotnet build src/TheLongestYear.Core/TheLongestYear.Core.csproj --nologo -v q`
Expected: 0 errors.

- [ ] **Step 5: Commit**

Bump `manifest.json` to `0.16.138`.

```bash
git add src/TheLongestYear.Core/CcDonationReconciler.cs src/TheLongestYear/Integration/ItemDonationSync.cs tests/TheLongestYear.Tests/CcDonationReconcilerTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.138: reconciler yields DonatedSlot triples; ItemDonationSync mirrors the board into the ledger"
```

---

### Task 5: Mirror on load and before the Season Goals page; the page reads the Core `MissingForSeason`

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs:104-108` (start of `OnRunLoaded`), `:1264-1278` (`Donate`, `PrintRunState`: only the log fields here; `Donate` itself is Task 6)
- Modify: `src/TheLongestYear/UI/MenuLauncher.cs:92-96`
- Modify: `src/TheLongestYear/UI/SeasonGoalsMenu.cs:129, 147-150, 198-234`

**Interfaces:**
- Consumes: `ItemDonationSync.Reconcile` returning int (Task 4), `BundleRequirement.MissingForSeason` (Task 2), `RunState.DonatedLedger()` (Task 1).

- [ ] **Step 1: No unit test is possible for these game-side hooks; the live checks are in Task 10. Write the change.**

`RunController.OnRunLoaded`, immediately after the `Run.Seed == 0` block:

```csharp
            // The ledger is a mirror of the CC board (spec 2026-08-29-per-slot-ledger). Re-read it
            // now: this is also the migration for saves whose ledger was the old id-only list.
            int mirrored = TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            if (mirrored >= 0)
                _monitor.Log($"Ledger mirrored from the CC board: {mirrored} slot(s) filled.", LogLevel.Info);
```

`RunController.PrintRunState`: replace `donated={Run.DonatedItemIds.Count}` with `slots filled={Run.DonatedSlots.Count}`.

`RunController.OnDayEnding`: the existing `ItemDonationSync.Reconcile(Run);` call stays; update its comment to: "Mirror the ledger from the board before the gate reads it, so the gate judges exactly what the player sees on the board."

`MenuLauncher.OpenSeasonGoals`: after `VaultPaymentSync.Reconcile(_store.Run);` add `TheLongestYear.Integration.ItemDonationSync.Reconcile(_store.Run);`.

`SeasonGoalsMenu.BuildEntries`: line 129 becomes `SlotLedger donated = _run.DonatedLedger();`; lines 147-150 become

```csharp
                int have = donated.FilledCount(br.BundleIndex);
                int need = br.NumberOfSlots;

                var (missingCount, missingThisSeason) = br.MissingForSeason(_season, donated);
```

Delete the private `MissingForSeason` method (lines 198-234) and its summary. Check the `using` lines: `SlotLedger` is in `TheLongestYear.Core`, already imported (the file aliases `CoreSeason`).

- [ ] **Step 2: Compile the mod project without deploying**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false --nologo -v q`
Expected: errors remain ONLY in `DonationService.cs`, `RunController.Donate`, and `ModEntry.cs` (Tasks 6 to 8). No errors in `SeasonGoalsMenu.cs`, `MenuLauncher.cs`.

- [ ] **Step 3: Commit**

Bump `manifest.json` to `0.16.139`.

```bash
git add src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/UI/MenuLauncher.cs src/TheLongestYear/UI/SeasonGoalsMenu.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.139: ledger mirrored on load and before the Season Goals page; the page counts filled slots through Core MissingForSeason"
```

---

### Task 6: Debug donations write the board (`CcSlotWriter`, `Donate`, `DonationService`, `tly_donate`)

**Files:**
- Create: `src/TheLongestYear/Integration/CcSlotWriter.cs`
- Modify: `src/TheLongestYear/Loop/RunController.cs:1264-1268` (`Donate`)
- Modify: `src/TheLongestYear/Donations/DonationService.cs:50-71`
- Modify: `src/TheLongestYear/ModEntry.cs:3481-3488` (`CmdTestDonate`) and the `CmdDonate` handler for `tly_donate` (find with `grep -n "private void CmdDonate" src/TheLongestYear/ModEntry.cs`)

**Interfaces:**
- Produces:
  - `internal static class CcSlotWriter { public static (int BundleIndex, int IngredientIndex)? FirstOpenSlotFor(string qualifiedItemId); public static bool TryFill(int bundleIndex, int ingredientIndex); }`
  - `RunController.Donate(string itemId)` flips the board and records; logs `Donated '{id}' into bundle {b} slot {s}. Ledger {n} slot(s).` or warns `No open slot wants '{id}'.`

- [ ] **Step 1: Write the change (game-side, no unit test; covered by the live checks in Task 10)**

`src/TheLongestYear/Integration/CcSlotWriter.cs`:

```csharp
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>Debug and sim writes to the vanilla per-slot bundle state. Under the mirrored ledger
    /// (spec 2026-08-29-per-slot-ledger) a ledger-only donation is wiped by the next re-read, so
    /// every simulated donation flips the board first. Slot indexes are positions in the
    /// Data/Bundles ingredient line, category slots included, matching NetBundles' bool[].</summary>
    internal static class CcSlotWriter
    {
        /// <summary>The first open concrete slot on the live board whose id matches, bundle order
        /// then slot order, in a themed (item) room. Null when the board is unavailable or nothing
        /// open wants the id.</summary>
        public static (int BundleIndex, int IngredientIndex)? FirstOpenSlotFor(string qualifiedItemId)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null || worldState.Bundles?.FieldDict == null) return null;
            string wanted = BundleParsing.NormalizeItemId(qualifiedItemId);
            foreach (var kvp in worldState.BundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (!RoomThemeMap.TryGetTheme(parsed.Room, out _)) continue;
                if (!worldState.Bundles.FieldDict.ContainsKey(parsed.Index)) continue;
                bool[] state = worldState.Bundles[parsed.Index];
                for (int i = 0; i < parsed.Ingredients.Count && i < state.Length; i++)
                {
                    string itemRef = parsed.Ingredients[i].ItemRef;
                    if (BundleParsing.IsCategoryRef(itemRef)) continue;
                    if (state[i]) continue;
                    if (BundleParsing.NormalizeItemId(itemRef) == wanted) return (parsed.Index, i);
                }
            }
            return null;
        }

        /// <summary>Mark a slot complete on the board. True if it is complete afterwards (already
        /// complete counts); false when the bundle or the slot does not exist.</summary>
        public static bool TryFill(int bundleIndex, int ingredientIndex)
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.Bundles?.FieldDict == null) return false;
            if (!worldState.Bundles.FieldDict.ContainsKey(bundleIndex)) return false;
            bool[] arr = (bool[])worldState.Bundles[bundleIndex].Clone();
            if (ingredientIndex < 0 || ingredientIndex >= arr.Length) return false;
            if (arr[ingredientIndex]) return true;
            arr[ingredientIndex] = true;
            worldState.Bundles[bundleIndex] = arr;   // NetArray needs a whole-array assign
            return true;
        }
    }
}
```

`RunController.Donate`:

```csharp
        /// <summary>Simulate a CC donation: fill the first open slot on the board that wants the id,
        /// then record it. The board is written first because the ledger mirrors the board.</summary>
        public void Donate(string itemId)
        {
            var slot = TheLongestYear.Integration.CcSlotWriter.FirstOpenSlotFor(itemId);
            if (slot == null)
            {
                _monitor.Log($"No open slot wants '{itemId}'. Nothing donated.", LogLevel.Warn);
                return;
            }
            if (!TheLongestYear.Integration.CcSlotWriter.TryFill(slot.Value.BundleIndex, slot.Value.IngredientIndex))
            {
                _monitor.Log($"Could not fill bundle {slot.Value.BundleIndex} slot {slot.Value.IngredientIndex} for '{itemId}'.", LogLevel.Warn);
                return;
            }
            Run.RecordDonation(slot.Value.BundleIndex, slot.Value.IngredientIndex, BundleParsing.NormalizeItemId(itemId));
            _monitor.Log($"Donated '{itemId}' into bundle {slot.Value.BundleIndex} slot {slot.Value.IngredientIndex}. Ledger {Run.DonatedSlots.Count} slot(s).", LogLevel.Info);
        }
```

(`BundleParsing` is in `TheLongestYear.Core`; add the using if the file lacks it.)

`DonationService.OnItemDonated`: replace `Run.RecordDonation(qualifiedItemId);` with

```csharp
            if (bundleIndex >= 0 && ingredientIndex >= 0)
                Run.RecordDonation(bundleIndex, ingredientIndex, qualifiedItemId);
            else
                _monitor.Log($"OnItemDonated('{qualifiedItemId}') without a slot identity: JP paid, ledger untouched (the board mirror settles it).", LogLevel.Trace);
```

`ModEntry.CmdTestDonate`: resolve and fill the slot before paying, so the console path carries slot identity:

```csharp
            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
            var slot = TheLongestYear.Integration.CcSlotWriter.FirstOpenSlotFor(args[0]);
            if (slot == null) { this.Monitor.Log($"tly_testdonate: no open slot wants '{args[0]}'.", LogLevel.Warn); return; }
            TheLongestYear.Integration.CcSlotWriter.TryFill(slot.Value.BundleIndex, slot.Value.IngredientIndex);
            DonationService.Active?.OnItemDonated(args[0], count, slot.Value.BundleIndex, slot.Value.IngredientIndex);
```

`CmdDonate` (the `tly_donate` handler) already calls `_runController.Donate(args[0])`; leave it.

- [ ] **Step 2: Compile the mod project without deploying**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false --nologo -v q`
Expected: errors remain ONLY in `ModEntry.cs` `CmdPlaySeason` (Task 7).

- [ ] **Step 3: Commit**

Bump `manifest.json` to `0.16.140`.

```bash
git add src/TheLongestYear/Integration/CcSlotWriter.cs src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/Donations/DonationService.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.140: debug donations fill the board slot first (CcSlotWriter); DonationService records per slot"
```

---

### Task 7: `tly_playseason` plans and donates per slot

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (`CmdPlaySeason`, lines ~2200-2425; the `_playSeasonBaseline` field at line 74)

**Interfaces:**
- Consumes: `SlotLedger` (mutable `Add`), `RunState.DonatedLedger()`, `RecordDonation(int,int,string)`, `CcSlotWriter.TryFill`.

- [ ] **Step 1: Write the change**

1. Field: `private (TheLongestYear.Core.Season Season, List<DonatedSlot> Donated)? _playSeasonBaseline;` (was `HashSet<string>`).

2. Board map (lines 2239-2252): keep every slot, not the first per id:

```csharp
            // Bundle name -> (index, concrete slots in board order, duplicates kept) from the live board.
            var lines = new Dictionary<string, (int Index, List<BundleSlot> Slots)>(StringComparer.Ordinal);
            foreach (var kvp in worldState.BundleData)
            {
                ParsedBundle parsed = BundleParsing.Parse(kvp.Key, kvp.Value);
                var slots = new List<BundleSlot>();
                for (int i = 0; i < parsed.Ingredients.Count; i++)
                {
                    string refId = parsed.Ingredients[i].ItemRef;
                    if (BundleParsing.IsCategoryRef(refId)) continue;
                    slots.Add(new BundleSlot(i, BundleParsing.NormalizeItemId(refId)));
                }
                if (!lines.ContainsKey(parsed.Name)) lines[parsed.Name] = (parsed.Index, slots);
            }
```

3. Delete the local `Flip`; every `Flip(a, b)` call becomes `TheLongestYear.Integration.CcSlotWriter.TryFill(a, b)`.

4. After `ItemDonationSync.Reconcile(run);`: `SlotLedger donated = run.DonatedLedger();`.

5. `PlanShare` plans slots:

```csharp
            // The bundle's whole season share as (slot, id) picks against ONE simulated ledger shared
            // by every bundle: a shared id is planned once per SLOT now, so two bundles that both list
            // Salmonberry each plan their own slot.
            List<BundleSlot> PlanShare(BundleRequirement req, int bundleIndex, List<BundleSlot> slots, SlotLedger sim)
            {
                var picks = new List<BundleSlot>();
                int guard = 0;
                while (!req.IsSatisfiedAtSeasonEnd(season, sim) && guard++ < 32)
                {
                    BundleSlot? pick = null;
                    foreach (string id in Candidates(req))
                    {
                        foreach (BundleSlot s in slots)
                            if (s.ItemId == id && !sim.IsFilled(bundleIndex, s.IngredientIndex)) { pick = s; break; }
                        if (pick != null) break;
                    }
                    if (pick == null) break;
                    sim.Add(bundleIndex, pick.Value.IngredientIndex, pick.Value.ItemId);
                    picks.Add(pick.Value);
                }
                return picks;
            }
```

6. Quarter mode: baseline becomes `_playSeasonBaseline = (season, run.DonatedSlots.Select(s => new DonatedSlot { BundleIndex = s.BundleIndex, IngredientIndex = s.IngredientIndex, ItemId = s.ItemId }).ToList());`, `sim` becomes `new SlotLedger(_playSeasonBaseline.Value.Donated)`, `PlanShare(req, bundle.Index, bundle.Slots, sim)` and `forBundle.Add((req, bundle.Index, pick.IngredientIndex, pick.ItemId))`. In the flip loop replace `if (donated.Contains(step.ItemId)) continue;` with `if (donated.IsFilled(step.BundleIndex, step.SlotIndex)) continue;`, `run.RecordDonation(step.ItemId)` with `run.RecordDonation(step.BundleIndex, step.SlotIndex, step.ItemId)`, and `donated = run.DonatedSet()` with `donated = run.DonatedLedger()`.

7. Plain mode loop (lines 2358-2373):

```csharp
                while (!req.IsSatisfiedAtSeasonEnd(season, donated) && guard++ < 32)
                {
                    BundleSlot? pick = null;
                    foreach (string id in Candidates(req))
                    {
                        foreach (BundleSlot s in bundle.Slots)
                            if (s.ItemId == id && !donated.IsFilled(bundle.Index, s.IngredientIndex)) { pick = s; break; }
                        if (pick != null) break;
                    }
                    if (pick == null) { log.Add($"  {req.Name}: nothing left to donate but the gate is still open"); break; }
                    if (!TheLongestYear.Integration.CcSlotWriter.TryFill(bundle.Index, pick.Value.IngredientIndex)) { log.Add($"  {req.Name}: could not flip slot for {DisplayName(pick.Value.ItemId)}"); break; }
                    run.RecordDonation(bundle.Index, pick.Value.IngredientIndex, pick.Value.ItemId);
                    donated = run.DonatedLedger();
                    flipped++;
                    log.Add($"  {req.Name} ({req.Kind}): donated {DisplayName(pick.Value.ItemId)} ({pick.Value.ItemId}) slot {pick.Value.IngredientIndex}");
                }
```

8. Goals mode: `run.RecordDonation(slot.ItemId)` becomes `run.RecordDonation(slot.BundleIndex, slot.IngredientIndex, slot.ItemId)`.

9. After the vault loop: `donated = run.DonatedLedger();` and the pass line reads `Ledger {donated.Count} slot(s).`

- [ ] **Step 2: Compile the mod project without deploying**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false --nologo -v q`
Expected: 0 errors, and now the whole solution compiles.

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q`
Expected: `Failed: 0`. Total is the previous 1741 minus 2 deleted RunState tests plus the new ones (SlotLedger 2, RunState 3, BundleRequirement 5, BundleClassifier 1, RunManager 1, Reconciler 1): expect 1753. If anything fails, fix it in the task that owns the file; do not skip.

- [ ] **Step 4: Commit**

Bump `manifest.json` to `0.16.141`.

```bash
git add src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.141: tly_playseason plans and donates per slot; a doubled id plans both slots"
```

---

### Task 8: `tly_gateneeds`

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (register next to `tly_gatecheck` at line ~266; bridge `switch` at line ~1851; new `CmdGateNeeds` next to `CmdGateCheck`)
- Modify: `docs/HEADLESS_DRIVING.md` (one line in the command list)

**Interfaces:**
- Consumes: `BundleRequirement.MissingForSeason`, `ItemDonationSync.Reconcile`, `VaultRules.PaidCount`, `VaultRules.SeasonOrdinal`, `VaultRules.IsVaultGateSatisfied`, `DisplayName(string)` (ModEntry line ~2791), `_runController.Requirements`, `_meta.Run`, `_meta.State`.

- [ ] **Step 1: Write the change**

Register: `helper.ConsoleCommands.Add("tly_gateneeds", "Print, per bundle, what the current season's day-28 gate still needs (the same numbers the Season Goals page shows) plus the vault. Read-only.", this.CmdGateNeeds);` and add `case "tly_gateneeds": this.CmdGateNeeds(command, args); break;` to the bridge switch.

```csharp
        /// <summary><c>tly_gateneeds</c>: the season gate's remaining demand per bundle, from the
        /// same MissingForSeason the Season Goals page draws, after mirroring the ledger from the
        /// board. Read-only.</summary>
        private void CmdGateNeeds(string command, string[] args)
        {
            if (!Context.IsWorldReady || _runController == null) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            RunState run = _meta.Run;
            TheLongestYear.Core.Season season = run.Season;
            TheLongestYear.Integration.ItemDonationSync.Reconcile(run);
            SlotLedger ledger = run.DonatedLedger();
            string nextSeason = season == TheLongestYear.Core.Season.Winter ? "the win" : $"{(TheLongestYear.Core.Season)((int)season + 1)} 1";
            int open = 0;
            foreach (BundleRequirement req in _runController.Requirements)
            {
                var (count, ids) = req.MissingForSeason(season, ledger);
                if (count == 0) continue;
                open++;
                string names = string.Join(", ", ids.Distinct().Select(id => $"{DisplayName(id)} ({id})"));
                this.Monitor.Log($"  {req.Name} ({req.Kind}, {ledger.FilledCount(req.BundleIndex)}/{req.NumberOfSlots} filled): needs {count} before {nextSeason}: {names}", LogLevel.Info);
            }
            bool vaultOk = VaultRules.IsVaultGateSatisfied(season, run, _meta.State);
            this.Monitor.Log($"  vault: paid {VaultRules.PaidCount(run)} of {VaultRules.SeasonOrdinal(season)} needed{(vaultOk ? " (satisfied)" : "")}", vaultOk ? LogLevel.Info : LogLevel.Warn);
            this.Monitor.Log($"tly_gateneeds: {season} day {run.DayOfMonth}: {open} bundle(s) still owed before {nextSeason}, {ledger.Count} slot(s) filled on the board.", LogLevel.Info);
        }
```

`docs/HEADLESS_DRIVING.md`: in the command list add `tly_gateneeds`: "per-bundle remaining demand for the current season's gate, same numbers as the Season Goals page; run it after any donation to see what the gate still wants."

- [ ] **Step 2: Compile and test**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false --nologo -v q` then `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q`
Expected: 0 errors, `Failed: 0`.

- [ ] **Step 3: Commit**

Bump `manifest.json` to `0.16.142`.

```bash
git add src/TheLongestYear/ModEntry.cs docs/HEADLESS_DRIVING.md src/TheLongestYear/manifest.json
git commit -m "v0.16.142: tly_gateneeds prints the gate's remaining demand per bundle"
```

---

### Task 9: One slot, one goal (test only)

**Files:**
- Test: `tests/TheLongestYear.Tests/SlotPoolBuilderTests.cs`

- [ ] **Step 1: Write the test**

```csharp
    [Fact]
    public void A_doubled_id_offers_its_second_slot_once_the_first_is_filled()
    {
        // Construction shape: Wood, Wood, Stone, Hardwood. Slot 0 (Wood) already filled on the board.
        var board = new Dictionary<string, string>
        {
            ["Crafts Room/13"] = "Construction/O 388 1/388 99 0 388 99 0 390 99 0 709 10 0/4/4/13/Construction",
        };
        var req = BundleRequirement.CreatePerItem("Construction", Theme.Foraging,
            new[] { "(O)388", "(O)390", "(O)709" },
            new Dictionary<string, Season> { ["(O)388"] = Season.Spring, ["(O)390"] = Season.Spring, ["(O)709"] = Season.Spring },
            bundleIndex: 13,
            slots: new[] { new BundleSlot(0, "(O)388"), new BundleSlot(1, "(O)388"), new BundleSlot(2, "(O)390"), new BundleSlot(3, "(O)709") });
        bool[] state = { true, false, false, false };

        var pool = SlotPoolBuilder.OpenSlotsForTheme(board, _ => state, Reqs(req), Theme.Foraging, Season.Spring, _ => true, weekOfYear: 1);

        Assert.Contains(pool, s => s.BundleIndex == 13 && s.IngredientIndex == 1 && s.ItemId == "(O)388");
        Assert.DoesNotContain(pool, s => s.IngredientIndex == 0);
        Assert.Equal(3, pool.Count);   // Wood slot 1, Stone, Hardwood
    }
```

(Crafts Room maps to `Theme.Foraging` in `RoomThemeMap`; if the existing tests in this file show a different theme for that room, use that. Match the `OpenSlotsForTheme` parameter list used by the file's other tests.)

- [ ] **Step 2: Run the test**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q --filter "FullyQualifiedName~SlotPoolBuilderTests"`
Expected: PASS (the builder already walks positionally). If it fails, the builder is the bug: fix `SlotPoolBuilder.OpenSlotsForTheme` so the per-slot loop does not dedupe by id, and re-run.

- [ ] **Step 3: Commit**

Bump `manifest.json` to `0.16.143`.

```bash
git add tests/TheLongestYear.Tests/SlotPoolBuilderTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.143: test: a doubled bundle id offers its second slot as a goal (one slot one goal)"
```

---

### Task 10: Deploy, live checks, docs

**Files:**
- Modify: `CHANGELOG.md`, `STATUS.md`, `TODO.md` (move the #1 priority entry to the built state), `README.md` and `release-notes` only if a What's New line is being prepared (not in this task).

- [ ] **Step 1: Deploy**

Run: `pwsh -NoProfile -File tools/deploy.ps1 -Minimized` (archives the log, closes the game, builds Release into Mods, relaunches SMAPI minimized).
Expected: `0 Error(s)` and `Launched minimized`.

- [ ] **Step 2: Live checks over the bridge (`tools/bridge.ps1`, see `docs/HEADLESS_DRIVING.md`)**

On the throwaway save (never Jeff's real save; see `docs/HEADLESS_DRIVING.md` for the clone command):

1. Load. Expect `Ledger mirrored from the CC board: N slot(s) filled.` in `SMAPI-latest.txt`.
2. `tly_gateneeds`: bundles listed with `filled` counts.
3. `tly_donate (O)296` (Salmonberry): log names ONE bundle and slot. `tly_gateneeds` again: only that bundle's count dropped.
4. On the default board, Construction: `tly_donate (O)388`, `tly_donate (O)390`, `tly_donate (O)709`. `tly_gateneeds` shows Construction `3/4 filled`, needs 1, `Wood`. `tly_donate (O)388` again fills slot 1: `4/4`.
5. `tly_playseason` then `tly_setday 28` and a sleep: the season gate passes; log shows `Ledger N slot(s)`.
6. Open the Season Goals page (hotkey) and screenshot (`tools/screenshot.ps1`): the counts match `tly_gateneeds`.

Record every log line in `STATUS.md` under a new `## 2026-08-29: per-slot ledger (0.16.135 to 0.16.143)` section with the verdicts. Anything that does not match the spec is a bug: stop, fix in the owning task's files, bump, commit, redeploy, re-run.

- [ ] **Step 3: Docs**

`CHANGELOG.md`: add under the unreleased heading:

```
- Donations are tracked per Community Center slot and mirrored from the board. One deposit credits one bundle (Children's no longer shows 3/3 after two donations when another bundle shared an item), a bundle with a repeated item (Construction's two Wood slots) needs every slot filled, and the mod can no longer declare a Winter win while the board still has an open slot. Existing saves migrate on load from the board's own state.
- New console command tly_gateneeds: what the season gate still needs, per bundle.
```

`TODO.md`: retitle the `### #1 PRIORITY` entry to `### BUILT 2026-08-29 (0.16.135 to 0.16.143, local, not released): per-slot ledger mirrored from the CC board` and add one line pointing at the spec.

`STATUS.md`: header lines (Last updated, version range, test count) plus the section from Step 2.

- [ ] **Step 4: Run the suite one last time and commit**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --nologo -v q`
Expected: `Failed: 0`.

Bump `manifest.json` to `0.16.144`.

```bash
git add CHANGELOG.md STATUS.md TODO.md src/TheLongestYear/manifest.json
git commit -m "v0.16.144: STATUS, CHANGELOG and TODO for the per-slot ledger"
```

---

## Self-review

- Spec section 1 (data): Task 1. Section 2 (requirement slots, evaluators, `MissingForSeason`, page reuse): Tasks 2, 3, 5. Section 3 (mirror, four call sites): Task 4 (write), Task 5 (load, page; day-end and playseason already call it). Section 4 (debug paths write the board): Tasks 6, 7. Section 5 (one slot one goal): Task 9. Section 6 (`tly_gateneeds`, `tly_runstate` slots): Tasks 8, 5. Section 7 (logging): Tasks 5, 6, 7. Save compatibility: Task 1 (legacy field) + Task 5 (mirror on load). Testing list: Tasks 1, 2, 3, 4, 9 unit; Task 10 live.
- Names used consistently: `DonatedSlot`, `SlotLedger` (`IsFilled`, `FilledCount`, `ItemIds`, `Count`, `Add`, `Entries`), `BundleSlot(IngredientIndex, ItemId)`, `BundleRequirement.BundleIndex` / `Slots` / `MissingForSeason(Season, SlotLedger)`, `RunState.DonatedSlots` / `RecordDonation(int,int,string)` / `ReplaceDonations` / `DonatedLedger()`, `CcDonationReconciler.DonatedSlots`, `ItemDonationSync.Reconcile` (int), `CcSlotWriter.FirstOpenSlotFor` / `TryFill`, `TestLedger.Fill` / `Empty`.
- Known compile gap: the solution does not build between Task 1 and the end of Task 7 because the old ledger methods are deleted in Task 1 on purpose (the compiler enumerates every caller). Each task commits regardless; the suite is required green at Task 7 Step 3 and after.
