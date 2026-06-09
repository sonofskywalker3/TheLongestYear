# Changelog

All notable changes to **The Longest Year** are documented here. This project
aims to follow [Semantic Versioning](https://semver.org/).

## 0.10.0 — 2026-06-09

A stability pass on the season-end gate and loop reset, plus fixes from beta reports.
Consolidates the 0.9.7–0.9.41 dev line. Changes since 0.9.6:

- **Season-end gate, part 1 (0.9.20).** Finishing every goal no longer occasionally
  resets you anyway — the item-donation ledger is reconciled from the Community
  Center's bundle state at day's end, so a missed deposit can't read as a failure.
- **Season-end gate, part 2 (0.9.37).** Failing the 28th no longer advances you to the
  next season. Completing the bus-repair Vault on day 28 queued the overnight bus
  `WorldChangeEvent`, which raced the loop reset; the rewind-doomed scene is now
  suppressed on a fail and the cutscene defers behind it on a pass.
- **Double theme pick on reset (0.9.25).** The reset presented the weekly theme picker
  twice and discarded the first pick — now persisted before the deferred reload.
- **Remix-aware Vault gate (0.9.26).** The bus-repair money bundles are renumbered under
  remixed bundles; indices + gold are now derived from live bundle data, so the gate
  can be satisfied. Season Goals also restyles the bus-repair line as a real list row
  (0.9.28–29).
- **Artisan goods keep value through the Junimo Stash (0.9.19).** Smoked/preserved fish
  and all flavored goods (wine, jelly, aged roe, honey, bait…) preserve identity +
  price across a reset.
- **Villagers stay out of the abandoned CC during a run (0.9.21).**
- **Mine elevator locks on reset (0.9.38).** Floors reached last loop are no longer
  accessible unless the keep-elevator upgrade was bought (cap-not-grant).
- **Weekly goals accept either egg color (0.9.40).** A "Large Egg"/"Egg" task ticks for
  the brown or the white variant.
- **In-progress Clint tool upgrade no longer survives a reset (0.9.30)** as a free upgrade.
- **Removed the stale vanilla "Rat Problem" quest during a run (0.9.41).**
- **Week-1 special-weather guarantee (0.9.18).** Each season is guaranteed a special
  weather day in week 1, replacing vanilla's always-on day-3 rain.
- **Clearer Junimo Shrine wording** — the planning view states JP is spent on reset/win.

## 0.9.1–0.9.6 (earlier betas, shipped)

- **0.9.6 — SMAPI update notifications.** Added the Nexus update key to the manifest,
  so SMAPI now tells you in its console when a new version of The Longest Year is
  available. (Also wires up automatic Nexus uploads on each GitHub release — no
  player-facing change.)
- **0.9.5 — Fixed: loading a non-TLY save fired the intro cutscene.** The dormant
  gate from 0.9.3 bailed correctly, but the intro / day-28 cutscene drivers (and a
  warp tracker) are attached at startup with their own update loops and bypassed it,
  so the Lewis→Junimo intro still played on a save TLY didn't start. They now respect
  the per-save activation gate.
- **0.9.4 — Fixed: the Community Center bulletin board (Mixed room) did nothing.**
  Vanilla gates the bulletin board behind three completed bundles (unlike the other
  five rooms, which open immediately); TLY revealed the note but never patched that
  gate, so pressing it was a no-op. It now opens from day 1 like the rest.
- **0.9.3 — Safety: TLY stays fully dormant on saves it didn't start.** Loading a
  normal (non-TLY) save with the mod installed used to activate the full roguelite
  layer — including the day-28 world reset. Now only starting a NEW game begins a run;
  any other save is left completely untouched (no effects, HUD, or reset loop).
  Existing runs migrate automatically.
- **0.9.2 — Fixed: the weekly theme picker was lost when starting a new loop from
  the win screen.** The "Start a new loop" choice is a question dialogue; its answer
  callback ran the reset and tried to open the planning hub while that dialogue was
  still the active menu, so the open was refused — and the week was marked "offered"
  before the open was confirmed, so it never re-fired. The hub now marks the week
  presented only on a confirmed open and retries the deferred open each tick once the
  menu surface clears. Also hardens against the other "menu busy" cases.
- **0.9.1 — Win-screen copy** reworded to "You have restored the Community Center.
  The valley is saved!" (The jarring win → JP-shrine transition is deferred to the
  real 1.0 ending — see `TODO.md`.)

## 0.9.0 — 2026-06-01

First public beta. Feature-complete for v1 ("prove it's fun & stable on PC").
The focus for this beta is feedback on **difficulty, pricing, and pacing**.

### The loop
- Roguelite year-loop over the Community Center restoration: per-season donation
  minimums; falling short unwinds the year to Spring 1; completing the Center
  within a year breaks the loop.
- **Junimo Points** earned from donations (scaled by rarity and a per-season
  multiplier), banked across loops.
- **Junimo Shrine** JP shop, surfaced on every loop reset and on a win, with
  upgrades that carry strength forward: skill levels, tool tiers, recipes
  (Cookbook/Craftbook), buildings, backpack, starting gold, a kept pet, and more.
- **Weekly themes** — each week grants a paired bonus + liability; chosen at the
  weekly planning hub.
- **Season Goals tracker** above the CC fireplace; **Junimo Stash** chest and
  **Cookbook/Craftbook** carryover surfaces on the farm.
- Continue-after-victory: keep playing a won run or start a fresh loop.

### New-game intro
- A two-scene intro plays before you take control: Lewis on the farm porch, then
  a Junimo inside the Community Center, who frames the loop in the land-spirits'
  own terms (community, sharing the land's bounty, and what does — and doesn't —
  carry across a reset). Implemented as a single engine-played event that moves
  between locations, then opens the theme picker.
- The vanilla intro is skipped and its toggle hidden; the farm type is forced to
  Standard. Both are managed on the character-creation screen.

### Quality of life
- Season Goals menu auto-completes its intro quest on first open and sorts
  completed bundles to the bottom.
- The starter parsnip gift box is granted only on the first loop, not re-dropped
  on every reset.
- `forage_yield_up` grants its bonus on pickup (Gatherer-style), with no
  duplicate forage spawned overnight.

### Known limitations
- PC only; Standard farm only; new saves only; multiplayer untested.
- Intro cutscene and dialogue are a first pass.

### Debug commands (console)
`tly_addjp`, `tly_addmoney`, `tly_buyupgrade`, `tly_reset`, `tly_replayintro`,
`tly_openshop`, `tly_openhub`, `tly_set{board,cookbook,craftbook,stash}`, and
others. Intended for testing/setup, not normal play.
