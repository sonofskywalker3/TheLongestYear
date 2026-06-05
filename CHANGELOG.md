# Changelog

All notable changes to **The Longest Year** are documented here. This project
aims to follow [Semantic Versioning](https://semver.org/).

## Unreleased (0.9.x dev)

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
