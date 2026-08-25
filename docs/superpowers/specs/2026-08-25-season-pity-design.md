# Season pity: easing a gate after repeated fails

**Date:** 2026-08-25
**Target:** 0.13.x
**Status:** approved design, awaiting implementation plan
**Replaces:** the parked "DerivePins for artisan goods / dishes / geode tiers" idea in TODO.md.
Hard season pins for station-made items are rejected: a loop-40 player with a full stash and
every station can legitimately hand those in on Spring 2.

## Goal

Each season gate should take roughly 3 to 5 loops to learn: build a routine, buy the shrine
upgrades that gate needs, pass it at standard difficulty. A player who is still failing the
same gate after that gets a little relief per further fail, so they can reach the next
season, whose own gate starts at standard difficulty again. Nothing eases before the
threshold, and nothing gets harder because of what the player owns.

## Decisions (user rulings, 2026-08-25)

| Question | Ruling |
|---|---|
| Scope | Fairness of the ask and easing after repeated fails, designed together, one release. |
| Signal | Per-season fail count. Not loop count, not carryover inventory. |
| Threshold | First 5 fails at a season are standard difficulty. Easing starts on the 6th fail at that season. |
| Lever | Follows the Fail-night choice. **Keep** the board: that season's quota comes down. **Reshuffle**: the re-roll leaves out the hardest items still eligible for that season's slots. |
| Rate | Gentle. Quota -10% per ease step, floor 50%. Trim 2 items per ease step. |
| Hardness | Rarity tier, needs a station or recipe, late-season-only spawn, quality asks. |
| Counter on pass | Drops back to the threshold: `min(count, 5)`. Standard again, but one more fail at that gate resumes easing at step 1. |
| Produce rule | Real produce that only spawns in Fall/Winter is never required by the Spring gate. Already structural; pinned by a regression test. |

## 1. State

`MetaState.SeasonFailCounts` : `int[4]`, index = `(int)Season` (Spring 0 .. Winter 3).
Serialized with MetaState; missing on old saves = all zeros.

The requirements a reload rebuilds must match what the player was told, so both pity paths are
stamped on the board rather than re-derived from live counters at load time (review finding F1):
`MetaState.BoardEaseSeason` / `BoardEaseSteps` (keep path, this section) and `BoardTrimSeason` /
`BoardTrimSteps` (reshuffle path, section 3). The keep-path stamp is written by
`SeasonPity.StampKeepEase` at the Fail-night KEEP choice and read back by
`SeasonPity.CurrentQuotaEase` on every requirements build (including a reload) instead of
re-deriving from `SeasonFailCounts`/held state; passing season S clears the stamp on the next
loop's fail/keep cycle (the stamp is only written on a fresh keep, never refreshed by
`RecordPass`), so passing S removes the easing from the next loop as intended.

Rules (pure functions in `Core/SeasonPity.cs`, same shape as `BundleHold` /
`BundleHoldPricing`):

```
RecordFail(state, season)      -> SeasonFailCounts[season] += 1
RecordPass(state, season)      -> SeasonFailCounts[season] = min(SeasonFailCounts[season], PityThreshold)
EaseSteps(state, season, cfg)  -> max(0, SeasonFailCounts[season] - cfg.PityThreshold)
QuotaFactor(steps, cfg)        -> max(cfg.PityQuotaFloor, 1 - cfg.PityQuotaStep * steps)
TrimCount(steps, cfg)          -> steps * cfg.PityTrimPerStep
```

Ordering on a Fail night at season S: `RecordFail` runs before the hold/reshuffle prompt so the
prompt can describe what the next loop will do. `RecordPass` runs where the day-28 gate is
evaluated and passes. Win, new game and `tly_reset` do not touch the array beyond what a
normal fail/pass does; a fresh save starts at zeros.

`PityEnabled = false` makes `EaseSteps` return 0 everywhere; counting still happens so the
setting can be turned on later without losing history.

## 2. Keep path: quota easing

Applies when the player keeps the board (hold) and `EaseSteps(S) > 0` for the season S they
just failed. It is applied in `GeneratedBundleSet.BuildRequirements` (where the obtainability
clamp already runs) from a new `SeasonPityAdjustment` argument, so it is deterministic from
state and a reload reproduces the same requirements (same reason the hold uses
`EffectiveBundleSeedLoop`).

- **Percentage bundles:** `ramp[S] = ceil(ramp[S] * QuotaFactor)`. Later seasons keep their
  values, then the ramp is re-monotonised (`ramp[s] = max(ramp[s], ramp[s-1])`), so Summer can
  never demand less than the eased Spring. Winter still demands full completion.
- **PerItem bundles** with a due season of S: the due season moves one later per ease step,
  capped at Winter. The gate stops demanding them in S.
- **Seasonal bundles** ("Spring Crops") require every ingredient by their due season and have
  no count to scale, so they follow the PerItem rule: due season moves one later per ease step,
  capped at Winter.
- Only season S eases. Every other season's requirement is untouched, whatever its own count.
- Vault gate is unaffected.
- The adjustment comes from the `BoardEaseSeason`/`BoardEaseSteps` stamp (section 1), not a
  live recompute from `SeasonFailCounts`, so a reload of a held board reproduces the same eased
  requirements even after `RecordPass` has dropped the counter mid-loop. The stamp is only
  written at a keep choice, so passing S removes the easing starting the next fail/keep cycle.
- Winter is never stamped and `CurrentQuotaEase` is null whenever the stamped season is Winter,
  even past the threshold (review finding F2): the prompt below has a dedicated Winter variant
  that never claims the quota itself will ease.

## 3. Reshuffle path: hardness trim

Applies when the player reshuffles and `EaseSteps(S) > 0`. Before `BundleSlotFiller` samples,
the candidate pool for each bundle that contributes to season S's gate is trimmed by
`TrimCount` items, highest hardness first. Bundles whose gate contribution is a later season
roll from the full pool.

"Contributes to season S's gate" = Percentage bundles with `ramp[S] > 0`, PerItem bundles due
in S, Seasonal bundles for S. This is implemented at fill time (`BundleSlotFiller.TrimApplies`)
by the pool domain's own season, not by a lookup against the eventual requirement classification:
a candidate pool is trimmed when `match.Season == null` (season-agnostic pools such as Metals,
ArtisanGoods, Fish, CrabPot, MonsterDrops, which feed every season) or `match.Season == S`.
Requirement classification (Percentage/PerItem/Seasonal, due seasons) runs later, off the
already-filled board, so it cannot be the trim's own gate.

Hardness score (`Core/ItemHardness.cs`), higher = harder, ties broken by ordinal item id so
the trim is deterministic:

| Component | Score |
|---|---|
| Rarity tier | VeryRare 4, Rare 3, Uncommon 2, Common 1 (existing JP tiers) |
| Needs a station or recipe (ArtisanGoods, Cooking, crafted, TapperGoods pools) | +2 |
| Earliest spawn season Fall or Winter | +1 |

Quality asks: when a trim applies to a bundle whose domain can roll quality (Quality Crops, seasonal crops, seasonal forage, fish), every slot in that bundle is forced to base quality and that costs one trim unit for the bundle; the remaining units remove whole items. Domains without quality rolls spend every unit on items.

Guard: a pool is never trimmed below the number of distinct items its bundle needs
(`targetCount` in `BundleSlotFiller.Fill`). If the trim would cross that line it stops early;
the filler's existing "can't fill, keep vanilla slots" fallback stays the last resort and is
logged as today.

## 4. Invariant: late-season produce and the Spring gate

Real produce (crops, forage, fish, crab-pot catches) whose earliest spawn season is Fall or
Winter is never *required* by the Spring gate. This is already enforced by three existing
pieces: derived season pins (`ItemPoolBuilder.DerivePins`), the obtainability clamp
(`GeneratedBundleSet.ClampRampForObtainability`), and season-filtered fills for season-named
bundles (`BundleSlotFiller.FilterSeason`). This spec adds no mechanism for it; it adds a
regression test that generates a full board from the engine pools, builds requirements, and
asserts that no Spring requirement can only be met with a Fall/Winter-pinned item. Station-made
goods and geode tiers are deliberately outside this invariant; the pity system covers them.

## 5. Player-facing

- **Fail-night prompt** (`ShowHoldChoice`, before the shrine): when `EaseSteps(S)` will be > 0
  for the next loop, the prompt is a single combined sentence variant covering both choices
  (`dialog.hold.prompt-eased`) rather than an extra line per choice: "...Keep them and we will
  ask a little less of {{season}}. Let time reshuffle them and we will leave out the hardest of
  {{season}}'s asks." Winter uses a dedicated variant (`dialog.hold.prompt-eased-winter`, no
  tokens) that drops the keep-side promise, since the quota itself never eases for Winter
  (section 2 / review finding F2): "...Let time reshuffle them and we will leave out the hardest
  of Winter's asks." Standard fails (steps = 0) show the plain `dialog.hold.prompt`.
- **Season Goals title:** "Season Goals: {{season}} (day {{day}}) eased {{steps}}x". Combines
  with the existing held title: "... held {{holds}}x eased {{steps}}x".
- **Bundle Log:** the eased season's quota lines show the eased number (no strike-through of
  the original; keep it simple).
- **Debug:** `tly_pity status` prints the four counts and ease steps; `tly_pity set <season> <n>`
  sets a count for smoke tests.
- **Config** (`GameplayConfig`, all exposed in GMCM under a "Season pity" section):

| Key | Default | Meaning |
|---|---|---|
| `PityEnabled` | `true` | Master switch for the easing (counting always on) |
| `PityThreshold` | `5` | Fails at a season before easing starts |
| `PityQuotaStep` | `0.10` | Quota reduction per ease step on a kept board |
| `PityQuotaFloor` | `0.50` | Lowest quota factor |
| `PityTrimPerStep` | `2` | Hardest items removed per ease step on a reshuffle |

No em dashes in any player-facing string (house rule).

## 6. Error handling

Documented deviation from the original plan (review finding F3): `Core/` has no `IMonitor` and
takes no logging dependency, so the pad and clamp cases below are silent in Core, not Trace/Warn
logs as originally specced. Only the trim guard logs, and it does so through a delegate the
caller supplies rather than a monitor reference held by Core.

- Missing or short `SeasonFailCounts` on load: `SeasonPity.Counts` silently pads with zeros
  (`MetaState.SeasonFailCounts` itself is padded to four entries the first time it's read).
- Config outside sane ranges (`PityThreshold < 0`, step or floor outside 0..1, trim < 0):
  `SeasonPity`'s pure functions (`QuotaFactor`, `TrimUnits`, `EaseSteps`) silently clamp at read
  time with `Math.Max`/`Math.Clamp`; there is no separate logged clamp pass.
- Trim guard hit: `BundleSlotFiller.Fill` takes an optional `Action<string>? log` delegate; when
  a trim applies it reports the before/after candidate counts, the units spent, whether quality
  was forced off, and (when the guard stopped the trim early) says so in the same message.
  `BundleEngine.Generate` is the only caller that supplies one, wiring it to
  `_monitor.Log("BundleEngine: " + msg, LogLevel.Info)` so the log only exists on the mod side,
  where a monitor is actually available.

## 7. Testing

Core unit tests (`SeasonPityTests`, `ItemHardnessTests`, extensions to
`GeneratedBundleSetTests` / `BundleSlotFillerTests`):

- Threshold: counts 0..5 give 0 steps; 6 gives 1; `PityEnabled=false` gives 0.
- Pass drops the counter to the threshold, never raises it.
- Quota easing: only season S changes; ramp stays monotonic; floor respected; Winter still
  demands completion; PerItem and Seasonal due seasons shift one per step and cap at Winter.
- Hardness ordering and tie-break; quality downgrade before removal; trim count per step.
- Trim guard: pool never drops below the bundle's distinct-item need.
- Produce invariant over a full generated board (fixture pools).
- I18n guard for the new strings.

Live smoke on the Rodger throwaway save (`tly_loadsave`, never the Load menu):
`tly_pity set spring 7`, fail Spring, confirm the extra prompt line for both choices; keep, then
check the Bundle Log shows the lower Spring quota and the title says "eased 2x"; reshuffle,
then check the log lists the trimmed items and the new board omits them.

## Out of scope

- Any easing driven by carried-over buildings, recipes or stash (explicitly rejected).
- Hard season pins for artisan goods, dishes or geode tiers (explicitly rejected).
- Vanilla (Normal/Remixed) boards: the pity system applies to Engine (TLY Custom) boards only,
  matching the keep-bundles hold.
- Weekly-theme wording ("68 daffodils" reads as a one-week ask): separate small fix, tracked in
  TODO.md.
