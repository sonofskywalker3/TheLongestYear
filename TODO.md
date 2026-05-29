# The Longest Year — TODO

Ongoing scratchpad for design / feature ideas captured during playtesting.
Items here are NOT yet planned; they need spec'ing before execution.
Once an item is planned, it moves into `docs/superpowers/plans/`.

## Open

### Co-opted day-1 intro cutscene (replaces vanilla 191393)
SEE BELOW for full spec — still genuinely open, v1.1 narrative tier.

### Small playtest carryovers (from STATUS.md)
Picked up during the 2026-05-29 audit; STATUS.md was stale (last update
2026-05-27) so these were drifting:

- **Festival exit to host map** — `Event.endBehaviors` currently warps to
  the farm entry; should land on the festival's host map (Town for
  Egg/Fair/Spirit's Eve; Beach for Luau/Jellies; Forest for Flower
  Dance). ~20 lines (endBehaviors postfix or transpiler).
- **Indicator `?` source rect** `(397, 489, 10, 10)` in
  `IndicatorRegistry` is approximate — visually verify the right sprite
  renders. One-line constant fix if wrong.
- **`forage_off` over-suppression (JC-4)** — Mining liability also
  blocks weeds/stones via `spawnObjects`. Flag for a playtest to assess
  whether this reads as "too punishing"; no code change planned yet.
- **`fortune_rare_fish` is a 0.75× bite-rate multiplier (JC-2)** — v1
  approximation. True rarity intercept (the spec'd "rare fish catch
  chance increased by 25%" reading) needs deeper Stardew internals
  investigation — patching whatever rolls the fish-rarity table rather
  than the bite-rate path. Currently piggybacks on Curiosity Lure
  semantics via `FishRareLurePatch`.

### (closed — moved here from "Open" 2026-05-29 audit)
### ~~Continue-after-victory mode~~ — SHIPPED 2026-05-29 as `5959de0`
Source: 2026-05-29 playtest spec. After the win condition fires (CC restored,
year complete, all bundles), the player should have the option to keep
playing the same run instead of being forced into a reset. Currently the
reset-trigger fires automatically at year-end on a completed CC.

Implementation notes:
- New flag in `MetaState` or `RunState` — `VictoryAcknowledged` — set when the
  player picks "continue" on the post-win screen.
- `WorldResetService` checks the flag before scheduling a reset; if set, the
  current run keeps going indefinitely (next month, next season, no roll-over).
- Acknowledgement UI: the existing JunimoShrineMenu or a one-off "you won"
  modal with "New loop" / "Keep playing" options.
- The player can still trigger a manual reset later via the shrine — the
  acknowledgement isn't permanent, just defers the auto-reset.
- JP banking can keep accruing during the continued run; donations after win
  still award JP at the usual season-multiplier, no special bonus.
- **JP-spend dialog at the end of the win scene.** Whether the player picks
  "New loop" or "Keep playing", surface the same Junimo Shrine purchase menu
  one more time so they can dump their banked JP on whatever upgrades they
  want active for the infinite run (or for the next loop) before the choice
  finalises. Reuses the existing `JunimoShrineMenu` — no new UI to design.
  Important: the menu has to fire AFTER the victory cutscene is fully closed,
  not stack on top of it, or controller focus + drawing layer get fighting.
- **JP-spend dialog ALSO pops on every natural loop reset** (Winter 28 → next
  Spring 1). User clarification 2026-05-29: "it's going to pop when you reset
  the loop or when you complete it, that's it." Same menu, two trigger paths.
  Important: must fire BEFORE `WorldResetService.PerformReset` commits, since
  the reset zeroes run-state (but MetaState.JunimoPoints survives the reset,
  so the spending CAN happen here — the constraint is purely UX, not data).
- **~~Remove the in-world JP shrine tile interactable.~~** Audited
  2026-05-29: no tile interactable was ever shipped. Plan 05 docs reference
  it as a design intent, but `JunimoShrineMenu` is only opened by
  `MenuLauncher.OpenShrineShop()`, which is in turn only called by the new
  reset/win popup paths and by the `tly_openshop` debug command. No tile
  removal needed.

Status: spec'd, not planned. Tagged as v1.x polish (the auto-reset isn't a
blocker — the player can manually save before the auto-reset hits if they
want to keep their post-win state preserved on a backup save).

### Co-opted day-1 intro cutscene (replaces vanilla 191393)
Source: 2026-05-29 playtest. User saw vanilla event 191393 (Demetrius +
Lewis CC intro) fire on Spring 5 of a TLY loop. Suppressed for now via
`EventSuppressionPatch` (returns `-1` from `checkEventPrecondition` for
the 191393 key). The eventual replacement is a TLY-specific intro that
RE-USES the 191393 staging (Lewis at Town near the CC) with new
dialogue:

1. Plays the FIRST time on a new save, on day 1 — **before** the
   weekly-theme picker opens. (Currently the picker opens immediately
   on `SaveLoaded` if it's a new run.)
2. Lewis explains the Joja takeover threat in TLY terms (the year-loop
   stakes — Junimos rewinding the year if the CC isn't restored).
3. Lewis walks off; the player walks into the CC; a Junimo pops up to
   explain the loop mechanic (themes, donations, Junimo Points).
4. Must fire on a new save **even if the player skips the intro on the
   first try** — track a `MetaState.HasSeenIntro` flag, set only after
   the intro completes OR after the picker is shown post-intro.
5. Skippable on first run (vanilla `Esc` / B). Auto-skipped on every
   subsequent loop (the meta-state flag is preserved across resets).

Implementation surface:
- New cutscene script in a custom `Data/Events/TLYIntro` or appended to
  Town events.
- Hook `OnSaveLoaded` (existing TLY entry point) to play the intro
  before the picker the first time only.
- `WeeklyThemeQuestService` should know to wait for the intro to finish.
- Junimo NPC sprite already in `Characters/Junimo` (used by hub menu);
  reuse for the loop-explainer beat.

Status: spec'd, not planned. Will be one of the v1.1 narrative tasks.

### ~~JP upgrade: `keep_pet`~~ — SHIPPED 2026-05-29
See Resolved section below. Cost landed at 75 JP, sentimental tier.

### (closed) JP upgrade: `keep_pet` — pet persists with hearts
Source: 2026-05-29 playtest. New JP upgrade in the Animals / Buildings
category that preserves the player's pet (cat / dog / turtle) AND its
friendship hearts across loops, so a long-tenured pet stays maxed out
between runs.

Implementation notes:
- Pet is a `Pet` instance hanging off `Game1.player.activePet` (or the
  per-Farm `Farm.characters`). On reset (`loadForNewGame`) the pet is
  typically wiped along with the rest of the world.
- Need to snapshot in MetaState: pet kind (which species), name, water
  bowl state, and `friendshipTowardFarmer.Value`.
- On reset, re-instantiate the pet of the saved kind, set hearts, place
  in the farmhouse / on the porch the way vanilla day-1 adoption does.

**Critical contrast — barn/coop animals (the existing `keep_*_animal`
upgrades) must continue to start each loop at 0 hearts.** User spec:
"the 'keep 1 cow' should still start over with 0 hearts so they can't
be getting large milk day 1. same for all barn/coop animals." The
existing `WorldResetService.ApplyStartingAnimals` builds fresh
`FarmAnimal` instances each reset (friendshipTowardFarmer defaults
to 0), which already matches this requirement — but call this out in
the `keep_pet` design so future cleanup doesn't accidentally unify the
two paths and start propagating animal hearts too.

JP cost ballpark: 50–100 JP. User: "they can't do much for a run, it's
mostly for feelings." Pet doesn't gate a measurable progression vector
(no Large Milk, no shipping value) so the cost should reflect that
sentimental-only payoff rather than a typical run-saver price.

Status: spec'd, not planned.

### ~~JP upgrades: keep kitchen / keep basement / keep shortcuts~~ — SHIPPED
Audited 2026-05-29: all three are wired end-to-end. Catalog entries:
`keep_kitchen` (800 JP), `keep_basement` (1800 JP, requires keep_kitchen),
`keep_shortcuts` (900 JP). Effects:
- `RunBaselineBuilder` reads them into `KitchenOnDay1` / `BasementOnDay1`
  / `ShortcutsUnlocked`.
- `FarmerReset` forces `HouseUpgradeLevel = 1` or `3` accordingly.
- `WorldResetService` step 7b adds the `communityUpgradeShortcuts` mail
  flag (vanilla reads it in Forest/Mountain/Town/Beach for the five
  shortcut tile overrides). Step 7c creates the `Cellar` location for
  L3-house resets so the FarmHouse warp doesn't dead-end.

(Original spec preserved below for design history.)

### (original spec, kept for design history) JP upgrades: keep kitchen / keep basement / keep shortcuts
Source: 2026-05-28 playtest. User correction after a first-pass sketch
that bundled all Robin-related kept-state into one upgrade: "NO don't
bundle robin's upgrades, I want one for keeping the kitchen, one for
keeping the basement, and one for keeping the shortcuts, that's it."

Three separate JP upgrades. All three are independent of CC completion
(Robin sells them for gold in vanilla without any CC dependency).

**1. `kept_kitchen`** — preserve farmhouse upgrade level 1 across runs
   - Vanilla: 10,000g + 450 wood, 3-day build. Adds the kitchen room
     (cooking + fridge) and bumps `Game1.player.HouseUpgradeLevel` 0→1.
   - Reset behaviour: `FarmerReset` currently wipes HouseUpgradeLevel
     back to 0 every run. When this upgrade is owned, skip that wipe
     (or restore L1 after `loadForNewGame` rebuilds the FarmHouse so
     `resetForPlayerEntry` lays out the kitchen-tier interior).
   - The current cookbook unlock (`cookbook_1`) needs review for
     interaction — cookbook is meta-state, kitchen is run-state, but
     the player would expect both to feel "I have a kitchen this run."

**2. `kept_basement`** — preserve farmhouse upgrade level 3
   - Vanilla: 100,000g, requires L2 first. L3 adds the cellar (basement
     with 33 cask slots — the aging infrastructure for wine/cheese).
   - This upgrade should imply L2 (kids' room) as a side effect since
     L3 can't exist without L2 in vanilla data — the menu shouldn't
     even offer `kept_basement` until `kept_kitchen` is owned.
   - On reset, restore HouseUpgradeLevel = 3 (or use the highest owned:
     3 if `kept_basement`, else 1 if `kept_kitchen`, else 0).

**3. `kept_shortcuts`** — preserve Robin's 5 map shortcuts (one upgrade
   for all five, NOT five separate upgrades — user spec)
   - Vanilla: each shortcut purchased separately from Robin
     post-`Mountain_Shortcuts_Spoke_Robin` mail flag.
   - The five shortcuts (1.6):
     - Forest south fence → south Town path
     - Bus stop tunnel north
     - Forest tree stump bridge → Backwoods
     - Mountain → quarry path
     - Mountain → Town side route
   - Each unlocks via a mail flag like `OpenedTreeStumpShortcut` plus
     a passable-tile property toggle on the Mountain/Forest map.
     Need to verify exact flag names against the 1.6 PC source —
     check `Mountain.cs`, `Forest.cs`, `Town.cs` for `mailReceived.Add`
     calls keyed to shortcut tile properties.
   - On reset: `WorldResetService.PerformReset` re-adds all five mail
     flags after `loadForNewGame` (similar pattern to `landslideDone`
     in MountainUnlock).

JP cost ballpark (relative to bus repair = 100 JP):
- `kept_kitchen`: 75 JP (adds cooking ability + fridge — meaningful)
- `kept_basement`: 200 JP (skips two L3 prerequisites + 100k gold)
- `kept_shortcuts`: 100 JP (saves Robin's 15k×5 = 75k gold per run)

Status: spec'd, not planned. Out of scope for the current playtest
batch; queue as its own commit chain.

## Resolved / closed

- **Continue-after-victory mode** — shipped 2026-05-29 as commit `5959de0`.
  JP-spend popup pops on both reset AND win paths; post-win choice
  dialog ("Start a new loop" / "Keep playing this run") sets
  `MetaState.VictoryAcknowledged` on Keep, which suppresses the popup on
  subsequent Winter 28 wins. Manual `tly_reset` stays raw (debug path).
  Plan-05 in-world shrine tile was never actually shipped — no removal
  needed.

- **`keep_kitchen` / `keep_basement` / `keep_shortcuts`** — shipped
  earlier; audit 2026-05-29 confirmed all three are wired end-to-end
  (RunBaselineBuilder → FarmerReset HouseUpgradeLevel + WorldResetService
  cellar/mail-flag step). TODO entry above kept for design history.

- **`keep_pet` upgrade** — shipped 2026-05-29 as `PetCarryoverService`
  + `MetaState.PetState` + `PetSnapshot` record. 75 JP, Buildings
  category. Snapshots kind / breed / name / friendship before
  `loadForNewGame`, restores at the Farm porch after starting-animal
  placement, sets the `MarniePetAdoption` mail flag to suppress
  vanilla's day-1 adoption offer. Barn/coop animals still start fresh
  (0 hearts) per spec — only the pet carries hearts.

- **Seed-driven weather scheduler** — shipped 2026-05-28 as
  `WeatherScheduler` + `WeatherModificationsPatch`. Per-season minimums
  (≥2 rain Spring/Fall, ≥2 storm + ≥2 rain Summer, ≥2 snow Winter),
  deterministic from `(uniqueIDForThisGame, seasonIndex)`. Subsumes
  the prior day-3 forced-rain bypass + Summer 13/26 hardcoded storms.
  Commit 14322d4.

- **`tly_wipemeta` debug command** — shipped 2026-05-28 as
  `MetaStore.WipeMeta()` + `CmdWipeMeta`. Replaces State with a fresh
  MetaState() and persists immediately. Commit 61ab125.

- **UX6 — always-on JP HUD** — shipped 2026-05-28 as `DrawJpHud` on the
  existing `Display.RenderedHud` hook. Top-right corner, 2 lines (banked
  JP + active theme + 1.5×/lifted suffix). GMCM toggle. Commit 1a8e2b2.

- **Plan 06 effects layer (UX5)** — ALL ten modifier ids wired with real
  Harmony patches: `forage_yield_up` (ForageYieldPatch), `mines_closed` +
  `mine_drops_up` (MineDropsPatch), `crop_growth_up/down` (CropGrowthPatch),
  `fish_bite_up/down` (FishBiteRatePatch), `forage_off` (ForageOffPatch),
  `all_drops_up` + `all_sell_prices_down` (AllDropsPatch). Liability/bonus
  mapping table preserved in design-spec docs.

- **Weekly Theme Journal entry** — shipped 2026-05-28 as `WeeklyThemeQuestService`.
  Creates a vanilla Quest on theme select with a 4-item checklist; each CC donation
  ticks a box; on completion awards +N JP (season-scaled) and suppresses the week's
  liability via `ActiveEffectsProvider.SuppressLiability`. Bonus stays active.
  Persisted via `RunState.LiabilitySuppressedThisWeek`. Commits 5bdb8f6 + 13776ed.
