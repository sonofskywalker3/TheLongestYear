# Batch B notes — Junimo day-28 cutscene + cutscene/event-gating system

Captured 2026-06-01 from live playtest feedback. **Not yet designed/implemented** — these are
raw requirements to feed a proper brainstorm/spec before any code. Batch A (JP-save fix, green
owned rows, Obtainability tab fit, watering-can diagnostic, save-folder inner-file rename safety
net) already shipped.

## 1. Day-28 bedtime Junimo cutscene (replaces the silent reset)

Trigger: when the player **goes to bed on the 28th of ANY season**. Visual: player in bed on a
black background, a Junimo appears and speaks. Two branches keyed on the season's gate state:

- **Gate CLOSED (failed the season's goal → year resets):**
  Junimo: *"At this pace we won't be able to complete the CC in time, so we will use our magic to
  reset the year — but don't worry, we have enough power left over to help you get a head-start
  this time."* → **open the JP purchase window** → player buys whatever → **on close**, that's when
  day 28 ends, **the game saves**, and the loop starts over on Spring 1.
- **Gate OPEN (on track → continue):**
  Junimo: *"Great job, you're doing well — keep this up and we can save the valley together. We
  will gain even more power from the work you do this season!"* → roll into the next season
  normally.

Note: the full save happening at the **natural day-28 night save point** also closes the
save-folder rename window for good (see §4) — the reset's folder rename + the immediate full save
leave the on-disk save fully consistent, so no brick/orphan risk.

Open questions for the brainstorm: is the gate check per-season or year-end? How is "gate
closed/open" determined (CC monthly gate state)? Technical approach for an in-bed black-background
cutscene (custom event script vs. drawn overlay). How the JP shop is hosted inside the event.

## 2. Cutscene / event-gating system

Root cause of replays: `FarmerReset.Apply` does `p.eventsSeen.Clear()` every loop, so vanilla
story events become eligible again.

Rules the user wants:
- **Never replay a cutscene the player has already seen** across loops (default).
- **Per-run unlock gating (NOT "ever banked"):** a cutscene tied to an unlock is suppressed only
  while that unlock is **active this run**; otherwise it must play.
  - **Furnace/Clint (corrected rule):** suppress Clint's furnace-recipe scene only when the furnace
    recipe is *known this run* (the craftbook is granting it at loop start, via
    `FarmerReset.GrantBankedRecipes`). If the player drops the furnace recipe from the craftbook,
    next loop they don't know it → Clint's scene **must** play again that run. Do NOT suppress
    forever just because it was banked once. (Fallback: if a cutscene's unlock can't be detected
    per-run, just play it every time.)
  - **Demetrius bats/mushrooms:** a per-run choice that is never "banked" → must show **every
    loop**, just **held until ~Spring 5** (not day 1). See §3.
- **Forced cutscenes must not be skippable.** Current bug: on the opening cutscene, pressing skip
  exits then the "force-seen" logic immediately re-fires it (a soft loop). Fix: **remove the skip
  option** from any cutscene we force, rather than re-firing on skip. "If they skip the first time
  through without reading, that's on them" — but the clean answer is no skip button on forced scenes.

## 3. Hold jarring early-game events until ~Spring 5

Even first-time, delay early events (Demetrius cave choice, etc.) so a fresh loop doesn't dump them
on day 1. Threshold ~Spring 5.

## 4. Save-folder rename fragility (MITIGATED in batch A, fully fixed by §1)

`WorldResetService` changes `uniqueIDForThisGame` for forage-RNG variety and renames the save
**folder** to the new id, but the save **files** inside keep the old id until the next sleep-save.
A process kill/crash in that window made the save unlistable (2026-06-01: bricked the None farm;
data intact, recovered by renaming the folder back). Batch A adds `RenameInnerSaveFiles` so the
inner files are renamed to match the folder immediately → save always loadable (kill path degrades
to a recoverable duplicate folder instead of a brick). The §1 day-28 full-save closes the window
entirely.

## 5. Books added to any loaded save (design concern, not data loss)

`_bookFurniture.ReconcileInventory()` runs on every save load + reset, granting one of each of the
3 books (cookbook, craftbook, bundle-log) to inventory. User saw all 3 added to a save that "never
bought the upgrades." Decide: gate cookbook/craftbook granting on owning the relevant upgrade
(bundle-log likely always-on as a core mechanic), and/or only run TLY behaviors on TLY-active saves.

## 5b. Planning shrine: surface Weather Sage + Cart Whisperer info (2026-06-01 playtest)

The Weather Sage forecast (next week's weather) and Cart Whisperer stock (Traveling Cart items)
that those Foresight upgrades reveal currently only appear at the weekly planning hub. The user
wants them **also shown on the read-only planning shrine board** so they can re-reference the
revealed info later in the week (`ShrinePreviewMenu` / `PlanningShrineService`). Gate the display
on owning the relevant tier (show as much as the owned tier reveals).

## 5c. Recipes are never wiped on reset — cookbook/craftbook are no-ops (2026-06-01 playtest)

`FarmerReset` never clears `Farmer.cookingRecipes`/`craftingRecipes` (confirmed: no `.Clear()` on
them anywhere). `GrantBankedRecipes` only *adds* banked entries idempotently. So every recipe the
player learns persists across all loops, and the cookbook/craftbook (whose whole point is "bank
recipes to keep them across loops") do nothing on reset. User saw all crafting recipes retained
with an empty craftbook, and kept Fried Egg without banking it.

Fix needs a design decision: on reset, wipe learned recipes down to a **baseline** and re-grant
banked. What's the baseline? Likely the vanilla new-game starting recipe set (so run 2+ matches
run 1) + banked. Cleanest impl: capture the starting recipe set on the first run into MetaState,
then on reset set `cookingRecipes`/`craftingRecipes` = baseline + banked. MetaState.CraftbookRecipes
already documents the intended "re-granted on reset" contract — the wipe half is just missing.

## 6. Bed blocks the door after reset

`loadForNewGame` rebuilds the FarmHouse with default furniture; the player's run-1 arrangement is
lost and, in an upgraded (larger) layout, the default bed can block the door. Investigate
repositioning the bed to the upgrade-correct spot (or preserving/clearing furniture).
