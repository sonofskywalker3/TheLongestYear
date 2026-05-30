# Design — New-game intro sequence rework (2026-05-30)

## Problem

A fresh new game is broken on three fronts, discovered in the 2026-05-30 playtest:

1. **The two intro cutscenes never fire.** `IntroEventInjector` gates the porch
   (Lewis) and CC (Junimo) events with invalid event-precondition letters:
   - `D 1` → `D` is `Dating` (NPC name), not day-of-month. Always false.
   - `s spring` → `s` is `Shipped` (item id + count), not season. Parse-fails.
   - `m <flag>` / `!m <flag>` → `m` is `EarnedMoney` (an int), not mail.
   Confirmed in the SMAPI log:
   `Event 'tly_intro_cc' ... invalid event precondition 'm tly_intro_porch_seen':
   required index 1 (int minMoney) has value 'tly_intro_porch_seen', which can't
   be parsed as an integer.` The correct keys are `u 1`, `Season spring`,
   `n <flag>` (has-mail) and `!n <flag>` (not-mail).

2. **The theme picker opens on the black load screen.** On a *new* game,
   `DayStarted` fires during save-creation, before the farm renders. The log
   shows `Opened planning hub (week 1 ...)` in the same second as
   `loaded save ... starting spring 1`. The player is asked to pick a theme
   before they have seen anything.

3. **Every loop reset re-drops the starter parsnip seeds.** The 15 parsnip seeds
   come from a starter gift box dropped by the `FarmHouse` constructor
   (`AddStarterGiftBox`, `(O)472` ×15 at tile 3,7 on Standard farm). `PerformReset`
   calls `loadForNewGame`, which rebuilds the FarmHouse → a fresh box every loop.
   `FarmerReset` wipes the inventory but never the box, so it persists.

## Desired experience

On a brand-new game: **Start → Lewis porch cutscene → Junimo CC cutscene → wake on
Spring 1 at 6am → theme picker → play.** The cutscenes play before the player ever
controls the farmer (like the vanilla intro). The vanilla bus-drive intro is
skipped entirely and its toggle hidden, so our chain *is* the intro.

## Approach (chosen)

Drive the events programmatically rather than relying on walk-into-location
triggers. Rejected alternatives: merging both into one Farm event (loses the
"walk into the CC, Junimo is there" staging); only fixing preconditions so they
fire on walk-in (that is the "take control first" behaviour the user rejected).

### Component 1 — Fix event preconditions (`IntroEventInjector`)

- Porch key: `tly_intro_porch/u 1/Season spring/!n tly_intro_porch_seen/!n tly_intro_done`
- CC key:    `tly_intro_cc/n tly_intro_porch_seen/!n tly_intro_cc_seen/!n tly_intro_done`

This makes the events *valid and eligible*. The programmatic driver still owns
when they actually start; valid preconditions also keep them from auto-firing at
the wrong time (e.g. on later days / once seen).

### Component 2 — `IntroSequenceDriver` (new, small state machine)

Fires only on the **first morning of a fresh run** (Spring 1, day 1, and
`!MetaState.HasSeenIntro` — the cross-run gate that is already promoted from
`tly_intro_cc_seen` on save and survives resets). Lives as its own service; one
clear job: stage the two cutscenes back-to-back and hand off to the picker.

State flow (advanced from a `UpdateTicked`/`DayStarted` hook):
1. **Start:** warp farmer to the Farm and `startEvent` the porch (Lewis) event.
   The event's own actor lines position Lewis + farmer (66,18 / 68,18). Ends by
   adding `tly_intro_porch_seen`.
2. **Porch done** (porch_seen present, no active event): warp farmer into the
   `CommunityCenter`. The CC (Junimo) event auto-fires — its precondition
   (`n tly_intro_porch_seen`) is now satisfied. Ends by adding `tly_intro_cc_seen`.
3. **CC done** (cc_seen present, no active event): warp farmer back to the
   farmhouse, leave the clock at 6am, then open the theme picker.

`MarkIntroSeenIfApplicable` (already promotes `cc_seen` → `HasSeenIntro` on save)
keeps the chain from replaying on later loops. `tly_replayintro` still re-arms it.

### Component 3 — Defer the day-1 picker

Suppress the existing week-start auto-open of the planning hub on the fresh-intro
morning (RunController). The driver opens the picker at the end of the chain
(step 3) instead. Week-2+ and loop-reset openings are unchanged — those fire after
a normal wake-up, not during save creation, so they were never broken.

### Component 4 — Strip the starter gift box on reset (generous: first loop only)

In `PerformReset`, after `loadForNewGame`, remove the starter gift box from the
FarmHouse. `PerformReset` only ever runs on a loop reset (or `tly_reset`), never
on the genuine first new game — so removing the box unconditionally here *is*
"first loop only": run 1 keeps the vanilla 15-parsnip nudge, every loop after
gets none. No RunNumber guard needed (and `Run.RunNumber` is still the
pre-increment value at this point anyway). Identify the box by its
`giftbox`/starter-gift marker among the FarmHouse objects.

### Component 5 — Force-skip vanilla intro + hide the toggle

Mirror `StandardFarmEnforcer`: while no save is loaded and a `CharacterCustomization`
is open, reflect its skip-intro state to forced-on and hide/disable the skip-intro
button. PC field names differ from the Android `MobileCustomizer.skipIntro` /
`skipIntroButton` the decompile shows, so use reflection with a loud sanity-log if
the field is absent (same robustness pattern as the farm-type scrub). Disabled
when `GameplayConfig.Enabled` is false so non-TLY play is untouched.

## Testing

- **Solo / log-verifiable:** Harmony patches apply 0-failed; no `Unknown
  precondition` / `invalid event precondition` errors; log shows the driver
  advancing porch → CC → picker; reset log shows the gift box removed on loop 2+
  but present on loop 1; skip-intro scrub logged at character creation.
- **Playtest (meaningful):** start a new game — confirm Start → Lewis → Junimo →
  6am wake → picker, with no black-screen picker and no farmer control before the
  cutscenes. Confirm Lewis/Junimo tile placement reads right. After a loop reset,
  confirm no parsnip gift box in the farmhouse. (Tile guesses 66,18 / 32,11 may
  need nudging — that is what the playtest is for.)

## Out of scope

- Dialogue polish of the Lewis/Junimo scripts (only triggering is in scope).
- Noon vs 6am: locked to 6am (player keeps the full first day); revisit only if
  playtesters object.
- Picker behaviour/content — unchanged; only *when* it opens changes.
