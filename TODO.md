# The Longest Year — TODO

Ongoing scratchpad for design / feature ideas captured during playtesting.
Items here are NOT yet planned; they need spec'ing before execution.
Once an item is planned, it moves into `docs/superpowers/plans/`.

## Open

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

### Weekly Theme Journal entry (active quest for bonus items)
Source: 2026-05-26 playtest discussion with user. User asked "when does
that get built?" during 2026-05-27 playtest — confirmed deferred to v1.1
(not blocking; player can still pick themes via the planning hub and
read the bonus list there). Implementation sketch below.

Each week's selected theme creates a Stardew Valley journal entry (the
quest log, accessible via the bookmark icon on the menu). The entry:
- Lists the 4 bonus items for that week (uses the same sample as the hub
  card so it stays consistent).
- Marks each item as "donated" when the player donates one to the CC.
- Completes when all 4 are donated.
- **On completion, suppresses that week's liability for the remaining
  days of the week.** The bonus stays active either way.

Goals served:
1. Harder push to chase the bonus items — completing the journal gives
   the player a tangible "I beat the liability" payoff beyond the 1.5×
   JP per item.
2. Reminder UI — if the player puts the game down mid-week and comes
   back, the journal entry tells them what the bonus items were
   without having to re-open the hub.

Implementation sketch (not yet a plan):
- Hook into vanilla's `Quest` system OR build a custom journal entry
  via Content Patcher / SMAPI.
- New `RunState.LiabilitySuppressedThisWeek` flag (bool, cleared on week
  transition in `OnDayStarted`).
- The effects layer (Plan 06, currently deferred) reads the flag and
  skips applying the liability when true.
- A donation observer (we already have one — `DonationObserver`) can
  flip the per-item completion state; when all 4 are flagged, set the
  liability-suppressed flag.
- Edge case: if one of the 4 bonus items is in a bundle that's already
  fully complete (no slot to donate into), the quest is unachievable.
  Either pick replacement items at sample time (skip already-satisfied
  bundles), or accept incompleteness and document it.

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

(empty — items move here after they're shipped)
