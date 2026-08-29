# Per-slot donation ledger mirrored from the Community Center board

**Date:** 2026-08-29
**Status:** approved in chat (Jeff, 2026-08-29): per-slot ledger, mirror not double-check, one slot one goal
**Priority:** #1 (TODO.md, Jeff 2026-08-28). Found live on emmalution's stream at v0.16.17.

## The bug

The run's donation ledger is a flat list of item ids (`RunState.DonatedItemIds`). One entry per
id for the whole run. Every reader counts progress by asking "is this id in the set":

- `SeasonGoalsMenu` shows `have = Ingredients.Count(donated.Contains)`.
- `BundleRequirement.IsSatisfiedAtSeasonEnd` and `IsFullyComplete` evaluate the same set.
- `RunManager.EvaluateDayEnd` computes the day-28 gate and the Winter-28 win from it.

Two wrong outcomes follow:

1. **One deposit credits every bundle that lists the item.** Salmonberry donated to Spring
   Foraging also counts for Children's. The page showed 3/3 after two real donations, then 4/3.
   The gate is lenient in the same way (never a false fail, but never strict either).
2. **A bundle with a repeated id cannot be represented.** `BundleClassifier.CollectQualifiedIngredients`
   dedupes, so Construction (Wood x99, Wood x99, Stone x99, Hardwood x10) is modelled as 3
   ingredients. Donate Wood once, Stone, Hardwood: the board says 3/4, the mod says complete. At
   Winter 28 the mod can declare a Win with the Community Center unrestored.

Root cause is the ledger's shape, not any one reader. `BundleRequirement` also carries no link to
the vanilla bundle (only `Name`), so a per-slot ledger has nothing to be matched against.

## The ruling that shapes the design

The mod's ledger becomes a **mirror of the board**, not a second opinion. Jeff's concern with a
"ledger AND board" double check: the page could say complete while the gate fails the run, which is
the worst outcome. So there is one source of truth, the game's own per-slot bundle state, and the
mod re-reads it before every place that judges progress. The ledger can never be ahead of the board
because it is copied from it; it can only lag, and the re-read closes that.

## Design

### 1. Core data: `DonatedSlot` and the ledger

```csharp
public sealed record DonatedSlot(int BundleIndex, int IngredientIndex, string ItemId);
```

`RunState` gains `List<DonatedSlot> DonatedSlots` (POCO list, JSON round-trips like every other
field). `DonatedItemIds` stays on the class marked LEGACY like `CurrentWeekBonusItems`: still
deserialized so old saves load, never written by current code, cleared in `BeginNewRun`. Nothing
reads it any more.

Ledger API on `RunState`:

- `RecordDonation(int bundleIndex, int ingredientIndex, string itemId)`: add if absent (keyed on
  the index pair). Replaces both `RecordDonation(string)` and `RecordCumulativeDonation(string)`;
  the two had identical bodies and the distinction was a comment.
- `ReplaceDonations(IEnumerable<DonatedSlot>)`: the mirror write. Whole-list replace.
- `SlotLedger DonatedLedger()`: a read view for the evaluators.

```csharp
public sealed class SlotLedger
{
    public bool IsFilled(int bundleIndex, int ingredientIndex);
    public int FilledCount(int bundleIndex);
    public IReadOnlySet<string> ItemIds { get; }   // distinct ids, for logging and any id-level ask
}
```

### 2. `BundleRequirement` carries slot identity

New members:

- `int BundleIndex`: the vanilla bundle index from `ParsedBundle.Index`. Both builders already
  parse it and drop it (`BundleCatalogBuilder.BuildRequirements`, `GeneratedBundleSet.BuildRequirements`).
- `IReadOnlyList<BundleSlot> Slots` where `BundleSlot(int IngredientIndex, string ItemId)`: every
  concrete (non-category) slot in board order, **duplicates kept**. Category slots are skipped as
  today; the index is the position in the raw ingredient list, so it lines up with the vanilla
  `bool[]` (the positional rule `CcDonationReconciler` already documents).

`Ingredients` (distinct ids) stays exactly as it is. The obtainability model, pools, ramps, stretch
lines, pins and goal askability are all id-level and correct that way. `NumberOfSlots` unchanged.

The evaluators switch from `ISet<string> donated` to `SlotLedger ledger`:

| Method | Rule |
|---|---|
| `IsSatisfiedAtSeasonEnd(season, ledger)` | `MissingForSeason(season, ledger).Count == 0` |
| `IsFullyComplete(ledger)` | `ledger.FilledCount(BundleIndex) >= NumberOfSlots` |
| `MissingForSeason(season, ledger)` (new, moved from the UI) | Seasonal: every unfilled slot once the season is due, else none. PerItem: every unfilled slot whose id is pinned at or before `season` (a doubled id demands every slot with that id). Percentage: `max(0, required - filled)` and the unfilled slots' ids as candidates. Returns `(int Count, IReadOnlyList<string> ItemIds)`. |

`SeasonGoalsMenu.MissingForSeason` is deleted; the page calls the Core method. The gate and the
page therefore run the same code, which is the whole point.

Vanilla blanket-sets every slot flag when a pick-X-of-Y bundle completes. Under slot counting that
reads as Y filled of X needed, which is complete. Correct, and it matches the board.

### 3. The mirror

`CcDonationReconciler.DonatedSlots(bundleData, slotStateForIndex)` yields `DonatedSlot` triples
(today's `DonatedConcreteIds` yields ids from the same loop; it goes away). `ItemDonationSync.Reconcile(run)`
calls `run.ReplaceDonations(...)` with the result. If `netWorldState` or the bundle dictionaries
are unavailable it returns without touching the ledger (never wipe on a missing read).

Call sites (the first two are new):

1. `RunController.OnRunLoaded` (save loaded, TLY active): this is the migration. An old save's
   `DonatedItemIds` is ignored; whatever the board shows filled is credited. One log line at Info:
   `Ledger mirrored from the CC board: N slot(s) filled.`
2. `MenuLauncher.OpenSeasonGoals`, next to the existing `VaultPaymentSync.Reconcile`, before the
   page is built.
3. `RunController.OnDayEnding`, before `EvaluateDayEnd` (already there).
4. `tly_playseason` (already there).

The live `DonationObserver` path keeps recording as deposits happen (it is what pays JP and ticks
weekly goals), so the ledger is current between re-reads too:
`DonationService.OnItemDonated(id, count, bundleIndex, ingredientIndex)` calls
`Run.RecordDonation(bundleIndex, ingredientIndex, id)`.

### 4. Debug donations write the board

Under a mirror, a ledger-only write is wiped by the next re-read, so every debug path must flip the
vanilla slot as well as the ledger:

- `RunController.Donate(itemId)` (`tly_donate <id>`): find the first open concrete slot whose id
  matches across the live board (bundle order, then slot order), flip it in `netWorldState.Bundles`
  the way `tly_playseason`'s `Flip` does, then record. No match: warn and do nothing.
- `tly_playseason` already flips per slot; its `RecordDonation(itemId)` calls become
  `RecordDonation(bundleIndex, slotIndex, itemId)`. Its name-to-slot table (`ModEntry.cs` around
  line 2239) keeps only the first slot per id today; it becomes a list so a doubled id plans both
  slots. Plans and quarters count slots, not ids.
- `DonationService.OnItemDonated` with `bundleIndex == -1` (the console path) resolves the slot the
  same way `Donate` does before recording.

### 5. Weekly goals: one slot, one goal

`SlotPoolBuilder` already walks the board positionally and emits one `BonusSlot` per open slot, so
Construction's second Wood slot is already a separate candidate once the first is filled. This spec
adds a test asserting it and changes nothing there. `WeeklyGoalCredit` is untouched.

### 6. `tly_gateneeds`

New console command. For the current season, prints one line per bundle with an obligation:
`<bundle> (<kind>): needs N before <next season> 1: id, id, ...`, then `vault: paid K of M`. Uses the
same `MissingForSeason` the page uses. `tly_runstate` adds `slots filled=N` next to its JP line.

### 7. Logging

`RunController.Donate` and the run-state summary print slot counts (`Ledger N slot(s)`), not id
counts.

## Save compatibility

- Old save, mid-loop: `DonatedItemIds` deserializes and is ignored; the load-time mirror fills
  `DonatedSlots` from the board. No JP is awarded by the mirror (same rule as today's backstop).
- New save on this build: `DonatedItemIds` stays empty forever.
- Downgrade to an older build: it reads `DonatedItemIds`, which is empty, and rebuilds it from its
  own day-end backstop. Lenient for one day, then as before.

## Testing

Unit (Core, no game):

- `RunState`: `RecordDonation` idempotent on the index pair; two slots with the same id are two
  entries; `ReplaceDonations` replaces; JSON round-trip of `DonatedSlots`; `BeginNewRun` clears both
  lists; legacy `DonatedItemIds` survives deserialization and is not read.
- `BundleRequirement`: one id in two bundles, one deposit credits one bundle (page count and gate);
  Construction shape (Wood, Wood, Stone, Hardwood, 4 of 4) needs four deposits, three is not
  complete; Percentage counts its own bundle's slots only; PerItem pin on a doubled id demands
  both slots; `MissingForSeason` returns the unfilled ids; `IsSatisfiedAtSeasonEnd` equals
  `MissingForSeason.Count == 0` across the three kinds.
- `CcDonationReconciler.DonatedSlots`: positional walk with a category slot in the middle keeps
  indexes; a repeated id yields two triples; missing bundle index skipped.
- `RunManager`: existing six cases rewritten per slot; Winter 28 with a doubled-id bundle three of
  four filled is `FailReset`, four of four is `Win`.
- `SlotPoolBuilder`: a bundle with a doubled id offers the second slot as a goal once the first is
  filled (one slot one goal).

Live (Jeff, or the bridge where it can):

- Old save from 0.16.17-era board: load, open Season Goals, counts match the CC board.
- Donate Salmonberry to one of two bundles that list it: the other stays unticked.
- `tly_gateneeds` output matches the page.
- Construction on the default board: three donations, page 3/4, `tly_playseason` Winter would not
  win; fourth donation, win.

## Out of scope

- Per-slot stack and quality (`IngredientStacks` / `IngredientQualities` stay per-id, MAX-aggregated).
- Multiplayer (the mirror keeps the existing master-only guard).
- Any change to JP amounts or the weekly bonus.
