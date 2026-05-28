# The Longest Year — TODO

Ongoing scratchpad for design / feature ideas captured during playtesting.
Items here are NOT yet planned; they need spec'ing before execution.
Once an item is planned, it moves into `docs/superpowers/plans/`.

## Open

### JP upgrades: keep kitchen / keep basement / keep shortcuts
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

### Seed-driven weather scheduler with per-season minimums
Source: 2026-05-27 playtest. User asked for "at least 2 days of rain
every season, at least 2 storms in summer, but mix them up every new
seed."

Why this isn't a 10-line patch: vanilla rolls weather day-by-day with
no per-season guarantees, and the hardcoded forced days (Spring 3 =
Rain, Summer 13/26 = Storm) repeat every loop. Stripping the hardcoded
days AND guaranteeing minimums needs a custom scheduler:

- At each season transition, deterministically schedule the season's
  weather from `(uniqueIDForThisGame, season#)`.
- Per-season constraints (v1 sketch):
  - Spring: ≥2 rain days; no storms.
  - Summer: ≥2 storms; ≥2 rain days (storms count as rain).
  - Fall: ≥2 rain days.
  - Winter: ≥2 snow days.
- Constraints exclude festival days + days 1–2 (forced sun) so we don't
  schedule rain on a Festival/Sun-forced day.
- Harmony patch on `Game1.getWeatherModificationsForDate` (or
  `Utility.PickWeatherForLocation`) returns the scheduled value.
- Day-3 forced rain already patched out
  (`WeatherModificationsPatch`). Summer 13/26 forced storms still
  active and would need to be subsumed by the new scheduler.

Status: plan-worthy on its own. Coordinate with effects layer (Plan 06)
since some liabilities key off weather.

### Wipe-meta debug command (`tly_wipemeta`)
Source: 2026-05-27 playtest. User noticed JP banked = 8 after a reset
and asked "how do I have JP banked? I didn't donate anything." Banked
JP is meta-state from earlier sessions, intentionally preserved by
`tly_reset`. For testing a true clean-slate run without deleting the
save, add a debug command that wipes `MetaState` (JP, owned upgrades,
backup-done flag) while keeping the save. Cheap: replace
`_meta.State` with `new MetaState()` + `_meta.Save()`.

## Deferred to Plan 06

- **UX5 — effects layer.** Wire `forage_yield_*` / `crop_growth_*` /
  `fish_bite_*` / `mine_drops_*` / `all_drops_up` / `all_sell_prices_down`
  to actual gameplay effects. Currently display-only via
  `ThemeModifiers.DisplayNameFor`.

  ### Liability/bonus mapping (designed 2026-05-26, awaiting sign-off)

  | Theme | Bonus | Liability |
  |---|---|---|
  | Foraging | 25% chance for +1 on any forage drop (incl. stone/wood) | **Mines Closed** — elevator + ladder from entrance don't function |
  | Farming | +25% Crop Growth | -30% Fish Bite Rate |
  | Fishing | +30% Fish Bite Rate | -25% Crop Growth |
  | Mining | 30% chance for +1 on any mine drop (incl. stone) | **Forage Off** — no wild produce / mushrooms (incl. mines) / fiddleheads spawn |
  | Mixed | 10% chance for +1 on any drop | -50% All Sell Prices |

  Design principles:
  - No "rounds to zero" — every bonus is a +1-probability so single-drop
    nodes (seeds, ore, coal, most forage) aren't no-ops.
  - +1 (not double) means multi-drop nodes like trees stay balanced
    even when included — a tree dropping 8 wood instead of 7 once in
    a while is small relative to the day's total. So no exclusions
    needed.
  - Each liability hurts an activity the focused player WASN'T going to
    prioritize that week. Quarry + skull-cavern setups become a hedge
    against Mines Closed.

  Implementation notes for the effects layer:
  - **`forage_yield_up` (Foraging bonus):** per-drop 25% roll on
    `Object.cutWeed/ShakeFromTree/forage spawn` paths; +1 the drop.
    Exclude qualified-item-id matches for stone (390) and wood (388).
  - **`mines_closed` (Foraging liability):** patch
    `MineShaft.checkAction` (the ladder/elevator) to no-op; patch
    `Mine.checkAction` for the entrance ladder. Maybe show a "the
    cave entrance is collapsed" message.
  - **`crop_growth_up/down`:** patch `HoeDirt.dayUpdate` growth-rate
    multiplier. Down = roll a per-crop chance to skip the day's tick.
  - **`fish_bite_up/down`:** patch `BobberBar.update` /
    `FishingRod.startFishing` bite-timer multiplier.
  - **`mine_drops_up`:** per-drop 30% roll on `MineShaft` /
    `ResourceClump.performToolAction` ore/coal/geode drops; exclude
    stone (390). Quarry is technically separate location so quarry
    drops aren't affected unless we want them to be.
  - **`forage_off` (Mining liability):** patch the forage-spawn
    routines for outdoor maps + `MineShaft`'s mushroom spawn to
    return early when active. Includes fiddleheads in secret woods,
    cave carrots, etc.
  - **`all_drops_up` (Mixed bonus):** per-drop 10% roll, +1, on EVERY
    drop path. Stone and wood included.
  - **`all_sell_prices_down`:** patch `Object.sellToStorePrice` with
    0.5× multiplier.

- **UX6 — always-on JP HUD.** Small corner counter showing banked JP +
  this week's selection + bonus-multiplier indicator.

## Resolved / closed

- **Weekly Theme Journal entry** — shipped 2026-05-28 as `WeeklyThemeQuestService`.
  Creates a vanilla Quest on theme select with a 4-item checklist; each CC donation
  ticks a box; on completion awards +N JP (season-scaled) and suppresses the week's
  liability via `ActiveEffectsProvider.SuppressLiability`. Bonus stays active.
  Persisted via `RunState.LiabilitySuppressedThisWeek`. Commits 5bdb8f6 + 13776ed.
