# Event / cutscene gating — design (2026-06-03)

Third spec of the loop/reset cluster. Covers the whole event-gating subsystem in one plan
(Phase 1 = generic seen-memory; Phase 2 = the curated per-run exceptions). The day-28 Junimo
bedtime cutscene remains its own later spec.

## Problem

`FarmerReset.Apply` calls `Farmer.eventsSeen.Clear()` every loop. Vanilla suppresses an event
only while its id sits in `eventsSeen` (the `checkEventPrecondition` seen-check returns "-1"), so
wiping it makes every vanilla story event eligible again — Demetrius' cave choice, Clint's furnace
scene, Willy intros, etc. all replay on each loop, out of order.

## Rules (from batch-B notes §2–3)

1. **Never replay a scene the player has already seen**, across loops (the default).
2. **Per-run unlock gating, not "ever banked":** a scene tied to an unlock is suppressed only while
   that unlock is active *this run*. Furnace/Clint: suppress only when the Furnace recipe is known
   this run (the craftbook grants it at loop start). Drop it from the craftbook → next loop it's
   unknown → Clint's scene must play again.
3. **Demetrius cave (bats/mushrooms):** a per-run choice that is never banked → must be offered
   **every loop**, but **held until ~Spring 5** (not dumped on day 1).
4. **Hold jarring early events to ~Spring 5** even first-time.
5. **Forced cutscenes must not be skippable.**

## Mechanism

Vanilla's own seen-check does the suppression — we just control what's in `eventsSeen` at loop
start, plus a thin `checkEventPrecondition` gate for the timing/unlock rules.

### Core (`TheLongestYear.Core`, unit-tested) — `EventGatingPolicy`

A pure decision function so the rules are testable without the game:

```
EventGatingDecision Decide(
    string eventId,
    int currentSeasonIndex, int currentDayOfMonth,
    bool unlockActiveThisRun,          // for unlock-gated events (e.g. furnace known)
    EventGatingTables tables)          // the curated id sets, below
→ Allow | Suppress
```

Rules in the policy:
- If `eventId` ∈ `HoldUntilSpring5` and not (Spring && day ≥ `HoldThresholdDay` = 5) → **Suppress**
  (defer the early event).
- If `eventId` ∈ `UnlockGated` and `unlockActiveThisRun` → **Suppress** (don't replay the teach
  scene when the unlock is already in hand).
- Otherwise → **Allow** (defer to vanilla's own precondition).

`EventGatingTables` (Core constants, populated from the **id dump** below — never guessed):
- `ReplayableEventIds`: events excluded from the cross-loop seen re-seed so they can fire again
  each loop (furnace, cave, …).
- `HoldUntilSpring5`: events deferred until Spring 5 (cave + the jarring early-spring set).
- `UnlockGated`: map of eventId → unlock predicate key (furnace → "Furnace recipe known").

### Glue (log-verified + playtest)

- **`MetaState.SeenEventsEver`** (`List<string>`): cross-loop memory of every event id ever seen.
- **Producer** — `ModEntry.OnSaving`: merge `Game1.player.eventsSeen` into `SeenEventsEver`
  before `_meta.Save()`. (OnSaving already promotes the intro flag; add the merge there.)
- **`FarmerReset`** — replace `eventsSeen.Clear()` with a **re-seed**:
  `eventsSeen = SeenEventsEver − ReplayableEventIds`, then keep the existing `Add("60367")`.
  Now any seen, non-replayable scene stays suppressed (rule 1); replayable scenes get a fresh
  chance each loop (rules 2–3).
- **`EventSuppressionPatch`** (the existing `checkEventPrecondition(string)` prefix) — call
  `EventGatingPolicy.Decide` with live `Game1.season`/`dayOfMonth` and the unlock predicate
  (`Game1.player.craftingRecipes.ContainsKey("Furnace")` for the furnace gate). On `Suppress`,
  return "-1" (as it already does for the hard-suppressed `191393`). This adds the Spring-5 hold
  and the furnace unlock-gate on top of the re-seed.
- **Forced non-skippable** (rule 5): vanilla has an `unskippable` event command. The custom intro
  is already non-skippable; the day-28 cutscene (later spec) will include `unskippable`. No code
  here beyond noting the convention.

## Getting the real event IDs (no guessing)

The specific vanilla ids (furnace, cave, early-spring scenes) live in compiled `Data/Events/*`
content — not in the decompiled C#. A decompile search returned only low-confidence guesses, so we
**dump them at runtime** instead:

- Add a `tly_dumpevents` debug command: load `Data/Events/<Location>` for the relevant locations
  via the content API and log `location: eventId` for every event whose **script** contains a
  telltale token — `addCraftingRecipe Furnace` / "Furnace" (furnace scene), the `cave` command /
  "caveChoice" (Demetrius cave), and Demetrius/Willy early-spring markers.
- Run it once (game is up), read the log, and hard-code the confirmed ids into the Core tables.
  This keeps the curated sets accurate and auditable.

## Consequences (accepted)

- A seen scene won't replay, so anything it granted that you didn't bank is gone until re-earned —
  e.g. the Furnace recipe if unbanked (consistent with the recipe-reset wipe). The furnace
  unlock-gate (rule 2) is the deliberate kindness: Clint re-teaches it when it's not known.
- Cave choice persists across loops unless the cave event is in `ReplayableEventIds` (it is →
  re-offered each loop, held to Spring 5).

## Testing

- **Core:** `EventGatingPolicy` unit tests — Spring-5 hold (before/after threshold, wrong season),
  furnace gate (known vs unknown), pass-through for unlisted ids, season-boundary day math.
- **Glue:** log-verified — re-seed count on reset, `tly_dumpevents` output, suppression decisions
  logged at `Trace`.
- **Playtest (user back at PC):** confirm seen scenes don't replay, Demetrius cave re-offers ~Spring
  5, Clint furnace re-fires only when furnace isn't banked, forced scenes can't be skipped.

## Build order

1. Phase 1 core: `SeenEventsEver` + producer + `FarmerReset` re-seed (no ids needed) + the pure
   `EventGatingPolicy`/`EventGatingTables` skeleton with empty curated sets + tests.
2. `tly_dumpevents`; deploy; capture the real ids.
3. Fill the curated sets; wire `EventSuppressionPatch` → `EventGatingPolicy`; deploy; playtest.
