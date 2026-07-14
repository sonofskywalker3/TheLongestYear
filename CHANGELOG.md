# Changelog

All notable changes to **The Longest Year** are documented here. This project
aims to follow [Semantic Versioning](https://semver.org/).

## 0.11.60 — 2026-07-14

Localization release: the mod is now fully translatable. Consolidates the
0.11.45–0.11.60 dev line.

### Added
- **Full i18n support (0.11.46–0.11.60).** Every player-visible string moved to
  `i18n/default.json` (SMAPI translation framework): the upgrade catalog
  (hand-authored rows keyed by id, generated rows via token templates),
  themes/modifiers/category labels, all seven self-drawn menus, weekly/stash/
  shrine quest text (including the composed objective checklist), HUD messages
  and question dialogues (with explicit plural variants), GMCM options (live
  language switch), the onboarding mail, furniture display names (re-injected
  on locale change), the Day-1 intro speak lines, and the Day-28 cutscene.
  English output is byte-identical to 0.11.44. Guard tests fail the build on
  missing/orphaned keys or broken `{{tokens}}`. `docs/TRANSLATING.md` documents
  the translator workflow — a translation is now a single JSON file.

### Fixed
- **World-state keep/wipe audit (0.11.45).** A one-time audit of every
  world-level (`netWorldState`) field the loop reset touches; closed the
  remaining "survives the reset" leak class with an explicit keep/wipe ruling
  per field.

## 0.11.44 — 2026-07-13

The big fix release: weekly goals redesigned around real bundle slots, remixed
bundles fully supported, the loop reset made airtight, and new upgrades.
Consolidates the 0.11.1–0.11.44 dev line. Changes since 0.11.0:

### Fixed
- **Weekly goals redesigned: slot-based checklists (0.11.12–0.11.23).** Each goal
  now names a specific still-open bundle slot (item, stack, quality — e.g.
  "Parsnip x5 (gold) — Quality Crops") and ticks only when that exact slot
  completes in live CC state. Kills three reported bugs at once: a single item
  could clear a x5-stack goal; themes could demand items with no matching open
  slot (structurally impossible weeks); goals could ask for items already
  donated. Fewer open slots → shorter checklist; zero → no quest that week and
  the drawback auto-lifts. The 1.5× banking bonus is slot-strict. Mid-week saves
  migrate with a one-time goal re-roll.
- **Remixed bundles all count (0.11.11).** Bundles matching no classification
  rule were silently dropped from season checkpoints and weekly themes — the
  gate shrank on the RECOMMENDED remixed config, and one report won a loop with
  a bundle still open. Unknown pick-X-of-Y bundles now classify with a derived
  cumulative quota ramp (custom-bundle mods included); nothing is skipped.
- **Reset-leak audit — the loop reset is now airtight (0.11.24–0.11.28,
  0.11.37–0.11.40).** Museum donations and lost library books, worn
  boots/rings/trinkets (and the trinket slot itself), monster-slayer kill
  progress, consumed mine milestone chests, power books / mastery / prize
  tickets, and max health/stamina all rewind with the year. Run-scoped stats
  are now wiped by default with an explicit keep-list, so future game versions
  can't silently leak progression across loops.
- **Your clothes survive the loop (0.11.41).** Hat, shirt, and pants stay worn
  through a reset — they carry no stats, and the wipe left farmers in their
  underwear with no way back to their look. Boots, rings, and trinkets still reset.
- **Kept buildings rebuild where you put them (0.11.42, 0.11.44).** Coop, barn,
  and silo keeps snapshot their position before the reset and rebuild exactly
  there (footprint cleared of regenerated debris), matching the stable's
  behavior — previously they landed on fixed tiles, one of which hid the silo
  behind the farmhouse.
- **Green rain is back in summer (0.11.26).** The weather scheduler was
  overriding vanilla's green-rain day; it's now reserved like a festival day,
  storm/rain minimums still hold, and forecasts (TV + Weather Sage) show it.
- **A reset no longer drags the old day's weather into Spring 1 (0.11.43).**
  Resetting mid-storm left lightning flashes, a storm HUD icon, and serialized
  storm state on the new Spring 1; the reset now re-resolves the day's weather
  through the game's own day-start path.
- **The farm cave asks again each loop (0.11.1).** Entering the cave offers the
  mushrooms / fruit bats / decide-later choice fresh whenever unchosen, instead
  of replaying the Demetrius scene (which only ran once, locking the first pick
  in forever).
- **Big-chest mod compatibility (0.11.35–0.11.36, 0.11.39).** Better Chests and
  Unlimited Storage no longer inflate the 4-slot Junimo Stash into a full chest
  grid; BC also no longer bulk-stashes into it or carries it away.
- **Horse fixes (0.11.21).** The horse no longer asks to be renamed every
  morning after a loop reset.
- **Theme picker polish (0.11.20, 0.11.22–0.11.23).** A pick can no longer be
  lost to a stale deferred offer; the quest tip moved below the checklist.

### Added
- **Keep Silo upgrade (0.11.27)** — 150 JP, Buildings; requires building a silo
  that run. Hay does not carry over.
- **Cart Whisperer I–V (0.11.5–0.11.10)** — Foresight chain; on Traveling Cart
  days the shrine planning view flags which of the cart's stock can feed a
  Community Center bundle (each tier previews more slots, gated on Cart Stall).
- Unattended-verification debug tooling (0.11.30–0.11.34): `tly_loadsave`,
  `tly_classify`, title-screen command bridge.

### Changed
- All reset paths route through one shared finalizer (0.11.2), so debug resets
  exercise the exact production path.

Fixes from this week's beta reports, plus a donation-JP rebalance. Consolidates
the 0.10.1–0.10.5 dev line. Changes since 0.10.0:

### Fixed
- **Theme-picker soft lock (0.10.4).** Quitting on the first day of a new season
  before completing it could reload into a weekly theme picker with no options and
  no way to close it. The save was written before the month rollover ran, and the
  load path's blind season sync erased the mismatch that triggers the rollover —
  last month's theme picks survived, accumulated past four, and eventually excluded
  every theme from the weekly offer. The load path now performs the month rollover
  itself (clearing month state and consuming the day-28 pre-pick), and as a backstop
  an empty offer skips the week instead of opening an unclosable menu — which also
  self-heals already-affected saves.
- **Dupe drops keep their quality (0.10.5).** The extra-item weekly bonuses
  (mine_drops_up / all_drops_up / tree + clump + monster paths) cloned drops by id
  only, always at base quality. The debris diff now carries `Item.Quality` /
  `Debris.itemQuality` through to the clone. Fish and hand-picked forage dupes
  already carried quality.
- **Vault money slots can no longer mint JP (0.10.2).** The donation observer's
  per-slot diff could treat a paid Vault bundle's gold amount as an item count
  (up to ~26,000 JP for the 25,000g vault) when the menu rebuilt mid-session.
  Money ingredients are now excluded from the per-item path; the Vault pays only
  its intended gold-scaled award.

### Changed
- **Donation JP rebalance: single-item slot awards (0.10.3).** A completed bundle
  slot awards the rarity JP of ONE item regardless of the slot's required stack —
  99 wood pays Common×1, not Common×99. The stack is an acquisition cost, not a JP
  multiplier; season scaling, weekly bonus items, and JP Boost apply unchanged.
  Bundle, room, and weekly-goal completion bonuses are now the dominant JP source.
- **Replayable-cutscene detection generalized (0.10.1).** Unlock-granting cutscenes
  are auto-detected from `Data/Events` instead of a hardcoded id list, so other
  mods' unlock scenes (e.g. Stardew Valley Expanded's) re-fire each loop the same
  way vanilla's do. Adds the `tly_dumpreplayable` debug audit command.

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
- **Weekly goals name the egg color (0.9.43).** A "Large Egg"/"Egg" goal shows
  "(Brown)" or "(White)" in the quest log — the two colors are distinct CC items, so
  the goal names which it wants instead of leaving the player to guess.
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
