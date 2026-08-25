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
- The adjustment is recomputed from `SeasonFailCounts` every time requirements are built, so
  passing S (which drops the counter to the threshold) removes the easing automatically.

## 3. Reshuffle path: hardness trim

Applies when the player reshuffles and `EaseSteps(S) > 0`. Before `BundleSlotFiller` samples,
the candidate pool for each bundle that contributes to season S's gate is trimmed by
`TrimCount` items, highest hardness first. Bundles whose gate contribution is a later season
roll from the full pool.

"Contributes to season S's gate" = Percentage bundles with `ramp[S] > 0`, PerItem bundles due
in S, Seasonal bundles for S.

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

- **Fail-night prompt** (`cutscene.day28.fail` flow, before the shrine): when `EaseSteps(S)` will
  be > 0 for the next loop, the prompt gains one line. Keep: "The Junimos will ask a little less
  of {{season}} next time." Reshuffle: "The Junimos will leave out the hardest of {{season}}'s
  asks." Standard fails (steps = 0) show nothing new.
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

- Missing or short `SeasonFailCounts` on load: pad with zeros, log at Trace.
- Config outside sane ranges (`PityThreshold < 0`, step or floor outside 0..1, trim < 0): clamp
  at read time and log a Warn once, same pattern as other tuning lists.
- Trim guard hit: Info log listing the bundle and how many items were actually removed.

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
