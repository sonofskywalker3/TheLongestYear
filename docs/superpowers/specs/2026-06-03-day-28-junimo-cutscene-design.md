# Day-28 Junimo bedtime cutscene — design spec

**Date:** 2026-06-03
**Branch:** `feat/v1-plan-07-junimo-stash`
**Supersedes the silent reset** described in batch-B notes §1
(`2026-06-01-batch-b-cutscene-and-event-system-notes.md`).

## Goal

When the player sleeps on the **28th of any season**, replace the current silent
loop-boundary handling with a forced, non-skippable **black-screen Junimo cutscene** that
branches on the season's gate state:

- **Gate CLOSED (FAIL → year rewinds):** Junimo explains the rewind and offers a head-start →
  **JP shop** → on close, **reset to Spring 1** and **write a single consistent full save**.
- **Gate OPEN (CONTINUE → next season):** Junimo congratulates the player → roll into the next
  season normally (no shop, no reset).

The full save on the FAIL branch is the real fix for the save-folder churn (notes §3/§4): the
reset's folder/inner-file rename plus an immediate full `SaveGame.Save` leave the on-disk save
fully consistent (new `uniqueIDForThisGame`, Spring 1), closing the rename window for good.

## Scope (confirmed with user)

- **Branches now:** FAIL (reset) + CONTINUE (next season). **WIN is out of scope for now** —
  the existing `_pendingWinChoice` popup path is left untouched. A WIN cutscene branch is a
  follow-up after these two are verified in-game.
- **Visual:** black screen + Junimo dialogue (dialogue box + meep SFX). **No** farmer-in-bed
  sprite — simplest and most robust; reads as "the Junimo visited while you slept."
- **CONTINUE branch has NO JP shop** — dialogue only, then next season. Shop opens only on FAIL,
  matching today's behavior.

## Why the morning-after timing (not the literal night)

The batch-B note says "when the player goes to bed on the 28th." Mechanically the cutscene fires
on the **first settled frame of the morning after the 28th**, not during the night sleep/save
fade. This is a hard constraint already discovered and documented in the codebase:

> `RunController.DoDayStartSeasonAndHub` (lines ~326-331): the planning hub and the reset JP shop
> were **deliberately moved off the Sunday-night `DayEnding` path to `OnDayStarted`** because menus
> cannot open during the night sequence — "Cannot open menu: cutscene or input lock" (2026-05-26
> playtest). `OnDayStarted` runs after the wake-up cutscene resolves, so menus open cleanly.

The event holds a black screen, so the player goes `[sleep] → black → Junimo speaks → [shop] →
wakes on Spring 1 / next season` and never meaningfully sees the intermediate morning. The
fiction ("Junimo visited in the night") is preserved.

## Approach

**Chosen: a forced vanilla `Event`, driven exactly like the shipped intro.**

The scene is a vanilla `Event` script (black screen, `speak Junimo "…"`, meep SFX), made
non-skippable by **omitting the `skippable` command** — the same technique
`IntroEventInjector.BuildIntroEvent` uses. A new `Day28CutsceneDriver` mirrors the proven
`IntroSequenceDriver`:

1. On a settled morning frame, if a day-28 outcome is pending and `activeClickableMenu == null`
   and no event is active, `location.startEvent(new Event(script, null, eventId))`.
2. Poll `UpdateTicked`; treat `Game1.eventUp || Game1.eventOver || currentLocation.currentEvent != null`
   as "still busy" (same `EventActive` fold the intro uses).
3. When the event has ended, run the branch continuation (open shop / continue).
4. Re-fire guard: if the event ended but the continuation hasn't run (interrupted edge case),
   force-proceed rather than restart — mirrors the intro's "force the flag to avoid a re-fire loop."

**Alternative considered (rejected):** a hand-rolled black overlay + `DialogueBox` chain with
callback chaining instead of polling. Rejected because the vanilla `Event` path is already proven
in this codebase (intro), gives the black screen + input lock for free, and the
"open a menu when a TLY event ends" pattern is already shipped (`IntroSequenceDriver` →
`OpenWeeklyHub`). The minimal scene (dialogue only, no actor choreography) makes the polling
overhead negligible.

## Branch routing

`RunController.OnDayEnding` already computes the outcome as a `RunAction` via
`RunManager.EvaluateDayEnd`. Route it to a single pending-cutscene flag instead of acting
immediately:

| `RunAction`   | Today                                   | New                                            |
|---------------|-----------------------------------------|------------------------------------------------|
| `FailReset`   | `AwardInterimJp` + `_pendingReset=true` | `AwardInterimJp` + `_pendingCutscene = Fail`   |
| `AdvanceMonth`| `break` (silent)                        | `_pendingCutscene = Continue`                  |
| `Win`         | `_pendingWinChoice=true` (first win)    | **unchanged** (no cutscene)                    |
| `Continue`    | `break`                                 | `break` (no cutscene — not a 28th)             |

`OnDayStarted`: if `_pendingCutscene` is set, **hand the morning off to the driver and `return`**
(suppress the normal season-sync/hub flow until the scene resolves) — same shape as today's
`_pendingReset` early-return. The driver runs the continuation when the event ends.

Interaction with `IntroSequenceDriver`: no conflict. The intro fires only on a fresh run's Spring 1
(`HasSeenIntro == false`); after a reset `HasSeenIntro` is true and the reset cutscene already
played pre-reset. The continue-cutscene morning (e.g. Fall 1) is a week-start, so the hub must
open **after** the cutscene — the driver's continuation calls `DoDayStartSeasonAndHub`, which
opens it.

## FAIL flow (gate closed → rewind)

1. Driver starts the black-screen Junimo event (unskippable).
2. Junimo speaks the rewind/head-start line. Event ends.
3. Continuation opens `JunimoShrineMenu` (existing JP shop) with
   `exitFunction = ContinueAfterResetSpend`.
4. On shop close, `ContinueAfterResetSpend` runs (existing): `PerformReset` → `BeginNewRun` →
   `_store.Save()`, **then a new step: force a full `SaveGame.Save`** so the on-disk files reflect
   Spring 1 + the new `uniqueIDForThisGame` immediately. → `DoDayStartSeasonAndHub`.
5. Player wakes on Spring 1.

If the shop can't open (defensive), fall through straight to `ContinueAfterResetSpend` — same
fallback `TryOpenShrineThenContinue` already has, so a reset is never stranded.

## CONTINUE flow (gate open → next season)

1. Driver starts the black-screen Junimo event (unskippable).
2. Junimo speaks the congratulations line. Event ends.
3. Continuation runs `DoDayStartSeasonAndHub` directly — **no shop, no reset**. The night's normal
   save already wrote the correct next-season date, so no forced save is needed.
4. Player lands on day 1 of the next season; the planning hub opens as usual.

## Dialogue (content constants)

- **FAIL:** "At this pace we won't be able to complete the Community Center in time, so we'll use
  our magic to rewind the year. But don't worry — we've enough power left to give you a head-start
  this time."
- **CONTINUE:** "Great job — you're doing well. Keep this up and we'll save the valley together.
  We'll gain even more power from the work you do this season!"

Final wording may be lightly adjusted for dialogue-box line breaks (`#$b#`) during implementation.

## Components

### Core (`TheLongestYear.Core`, unit-tested)
- `Day28CutsceneDecider` — pure decision logic mirroring `IntroSequenceDecider`:
  given (pending branch, already-started-this-morning, event-active, menu-open) → next action
  (`StartCutscene` / `OpenShop` / `Continue` / `Waiting` / `None`) + the re-fire guard.
- Dialogue strings + the cutscene's event id as content constants (alongside or modeled on
  `IntroEventKeys`).

### Glue (`TheLongestYear`, log-verified)
- `Day28CutsceneDriver` — `UpdateTicked`/`DayStarted` poll loop; starts the event, detects end,
  runs the continuation. Mirrors `IntroSequenceDriver`. Wired in `ModEntry` with access to the
  `MenuLauncher`/`RunController` continuations.
- Event-script builder (mirrors `IntroEventInjector.BuildIntroEvent`) producing the black-screen
  Junimo script per branch.
- `RunController` changes: `_pendingCutscene` enum field replacing `_pendingReset`; set it in
  `OnDayEnding`; hand off + early-return in `OnDayStarted`; expose the two continuations
  (open-shop-then-reset for FAIL, `DoDayStartSeasonAndHub` for CONTINUE) to the driver.
- `ContinueAfterResetSpend`: append the forced full `SaveGame.Save`.

No cross-loop suppression flag (unlike the intro): this fires on **every** qualifying 28th. Gating
is purely "a day-28 outcome is pending this morning."

## Testing

- **Core (unit tests):** `Day28CutsceneDeciderTests` — every (branch × state) combination returns
  the right action; the re-fire guard fires; CONTINUE never routes to the shop; WIN never routes
  to the cutscene. Mirror the existing `IntroSequenceDeciderTests` / `EventGatingPolicyTests`
  style. Keep the suite green (currently 421 passing, 0 warnings).
- **Glue (log-verified + playtest):** build loads clean (Harmony patch count unchanged, 0 failed);
  `tly_failreset` exercises the FAIL cutscene → shop → reset → forced save; sleeping a real
  Spring/Summer/Fall 28 with the gate passed exercises CONTINUE.

## Risks / verify in the reserved playtest

1. **Forced `SaveGame.Save` mid-morning** — saving off the normal night cycle is the one piece
   that fights the engine. Must confirm: (a) it writes a single consistent on-disk save (new id,
   Spring 1, no orphan/dupe folder), and (b) re-entrant SMAPI `Saving`/`Saved` during the morning
   doesn't misbehave (`ModEntry.OnSaving` should be idempotent — verify). Implementation must drain
   the save enumerator correctly (not just kick it off). If this proves fragile, fall back to the
   existing `RenameInnerSaveFiles` mitigation and defer the forced save — but the forced save is
   the intended fix for the churn, so try it first.
2. **Brief morning flash** before the event's black screen (driver waits for a settled frame, like
   the intro). If jarring, add a black overlay or fade as the event's first command; otherwise
   accept it.
3. **Reset-dependent fixes ride along** — because the FAIL cutscene *is* the reset path now, this
   same playtest verifies last session's still-unconfirmed reset fixes: recipe wipe to baseline +
   re-grant banked, FarmHouse furniture restore (bed + fireplace), event/cutscene gating (seen
   scenes don't replay; Demetrius re-offers ~Spring 5; furnace re-fires only when unbanked), and
   Keep Horse carry-over.

## Out of scope

- WIN cutscene branch (follow-up).
- Farmer-in-bed sprite / elaborate staging.
- Any change to the WIN popup, the JP shop contents, or the gate-evaluation logic.
