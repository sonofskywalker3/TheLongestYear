# The even year: week-granular item availability, ramps that follow the items, flat weekly goals

Date: 2026-08-28. Status: approved by Jeff in chat ("yes, and make sure when we sim it that
you're checking everything that's unknown and asking me to confirm. I want lists of every item
every run"). Supersedes the ramp numbers in `2026-08-28-theme-week-budget-design.md` (the goal
budget itself stays). Follows `2026-08-27-derived-item-availability-design.md`.

## Problem (measured on 0.16.77, board seed 595710513, `docs/board-availability.md`)

The engine knows a real earliest season for fish, crab-pot catches and metals only (27 of 154
board lines). 47 hand pins cover a few more. Everything else, 82 lines (gems, artifacts, geode
minerals, monster drops, artisan goods, animal products, dishes, saplings, most crops and forage),
defaults to **Winter** because no rule placed it. The mines are floored at Summer by a judgement
in `LocationGating`. The curated quota ramps were written knowing all that, so they ask for 0 in
Spring for Preserver's, Home Cook's Feast, Brewer's, Artifact, Mineral and Children's.

Result, gate-only sim F: gates 16 / 31 / 52 / 92 of 92; Spring theme weeks `Mining 0, Spelunking
0, Kitchen 1` all month; Winter healthy after the goal budget (0.16.73). Jeff's target: "balance
this hard so the weeks and seasons goals are similar all the way through. Challenging but mostly
achievable."

## Goal

Every season owns about a quarter of the board and every week asks for about the same amount,
for the gate-only player and the goal-completing player. Every item category has a Spring
presence where the game allows it. Nothing is silently unknown: every item on a board is listed
with its floor, and the ones the engine could not place are put in front of Jeff to rule on.

Not this build: the single-loop difficulty target (Normal winnable but not easily in one loop,
Hard not in one). Approach C (engine sizes Winter bundles up) stays parked.

## Design

### 1. Floors by week of the year

`ItemAvailability` gains `EarliestWeek` (1 to 16, `Calendar.WeekOfYear`) and `GateSeason`.
`EarliestSeason` stays as `SeasonOf(EarliestWeek)` for existing callers. `GateSeason` is the
season a day-28 gate may first demand the item; it equals `SeasonOf(EarliestWeek)` except where
the table below says otherwise (deep mines). Phase 2 rules (`EffortComposer`) now return
`ItemEffort` with an `EarliestWeek` and an optional `GateSeason`, so they place an item in time
as well as in effort. An item no rule places keeps the Winter default (week 13) and is reported
as **unknown** (section 6).

| Category (rule file) | Goal week | Gate season | Source of the number |
|---|---|---|---|
| Mines, floors 1 to 39 (area 0/10): Quartz, Earth Crystal, Amethyst, Topaz, Copper, Slime, Bug Meat, Bat Wing, Cave Carrot, regular Geode minerals | 1 | Spring | `MineAreas` + Jeff's 30 floors a week |
| Mines, floors 41 to 79 (area 40): Aquamarine, Frozen Tear, Iron, Frozen Geode minerals, Omni Geode minerals | 2 | Spring | same |
| Mines, floors 81 to 119 (area 80): Fire Quartz, Ruby, Emerald, Gold, Magma Geode minerals, Diamond | 3 | **Summer** | Jeff accepted the softer gate: below floor 80 is a Spring gate fact, 80 and deeper is Summer |
| Skull Cavern (area 121): Iridium Ore, Skull Cavern monster drops | 9 | Fall | LocationGating Desert/SkullCave = Fall |
| Sewer, Bug Land, Mutant Bug Lair drops | 5 | Summer | LocationGating |
| Witch's Swamp / Hut | 13 | Winter | LocationGating |
| Crops (`CropForageAvailability.DeriveCrop`) | first harvest week: season start + days to grow, from `RawCropGrowth`; seeds sold only at a festival (Strawberry) take the festival week | season of that week | Data/Crops |
| Forage (`DeriveForage`) | first spawn week from `RawSpawnEntry`, plus LocationGating (Desert forage week 9); bush berries: Salmonberry week 3, Blackberry week 10 | season of that week | Data/Locations |
| Fish, crab-pot, metals (Phase 1) | first week of their derived season; ores take the mine-area row above | as now | unchanged |
| Artifacts (`ArtifactAvailability`) | 1, or the location's gating week (Desert 9) | Spring | artifact spots exist on day 1 |
| Animal products (`AnimalProductAvailability`) | Coop/Barn: Egg, Milk, Wool week 2; Big Coop/Barn: Duck Egg, Duck Feather, Goat Milk week 5; Deluxe: Rabbit's Foot, Truffle, Pig products week 9 | season of that week | `RawBuilding` tier |
| Artisan goods (`ArtisanAvailability`) | by the machine's unlock skill level from `MachineUnlocks`: level 0 to 2 week 2, 3 week 3, 4 to 5 week 4, 6 to 7 week 6, 8 to 9 week 7, 10 week 9; then the max with every input item's week (Melon Wine is not before Melon) | season of that week | Data/Machines |
| Cooked dishes (`CookedDishAvailability`) | week 5 (kitchen upgrade), then the max with every ingredient's week | Summer or later | judgement: no kitchen by Spring 28 |
| Fish pond products (`FishPondAvailability`) | the fish's week + 4 (one season to build and populate a 5,000g pond) | season of that week | judgement |
| Saplings | 1 | Spring | Pierre sells them daily |
| Everything else | 13 (unknown) | Winter | reported, never guessed |

All judgement numbers live in one table, `AvailabilityWeeks` in `TheLongestYear.Core`, and
`GameplayConfig.AvailabilityWeekOverrides` (id to week) lets a config move any single item.
`DefaultItemSeasonPins` stays as the override layer it already is, converted to weeks (a pin is
the first week of its season); a pin earlier than a derived floor is still rejected.

### 2. Goals use the week, gates use the season

`GoalObtainability.IsObtainable` takes the current `weekOfYear` and refuses an item whose
`EarliestWeek` is later, for every placed item (Phase 1 or Phase 2). Catalog spawn seasons still
apply on top. Unknown items keep today's behaviour for goals (catalog seasons only) so a Spring
theme can still name a Potato the engine has not placed; the unknown list is how that gets fixed.
`BundleDeadlines` clamps a per-item deadline up to `GateSeason` instead of `EarliestSeason`.

### 3. The ramp follows the items

`BundleClassifier` derives every pick-X-of-Y ramp from the bundle's own ingredients:

```
reachable[s] = count of ingredients with GateSeason <= s
even[s]      = round(X * (s + 1) / 4)
ramp[s]      = min(even[s], reachable[s]), made monotone, ramp[Winter] = X
```

The difficulty shift keeps working because the derivation runs with the shifted X.
`DefaultBundleQuotas` is emptied; the user's `BundleQuotas` config still overrides by name for
anyone who wants a hand ramp. The classifier logs the derived ramp per bundle at board build.

### 4. The engine gives every bundle a Spring foothold

`BundleSlotFiller.Fill` (and the seasonal/per-item generators) re-roll a slot when the finished
bundle would have fewer than `max(1, ceil(X / 4))` ingredients with `GateSeason == Spring` and the
pool has candidates that qualify. When the pool cannot supply one (Winter Star), the bundle is
built anyway and `tly_gatecheck` reports it under a new `[no spring foothold]` tag. Per-item
bundles keep the even effort-sorted spread across the four checkpoints, now on real floors.

### 5. Flat weekly goals

`BonusItemSampler.DefaultMaxCountBySeason` becomes 5 / 5 / 5 / 6. Filler stays 0 / 0 / 1 /
unlimited. The goal budget (0.16.73) and the min-2 floor (0.16.74) are unchanged. Rule E's
effort weights are unchanged.

### 6. Nothing is silently unknown

`tly_dumpavailability` adds the `Week` column and a closing **Unknown items** section listing
every board ingredient still on the Winter default, with the bundle it sits in.
`tools/sim-year.sh` runs it at the end of every run and copies the file to
`docs/board-availability.md`. The report after every sim gives Jeff the full per-bundle list and
the unknown list and asks him to confirm or assign each unknown; a run is not verified until he
has answered. (Memory: `tly-sim-list-unknowns-each-run`.)

## Expected effect

Board seed 595710513 (99 required): gates from 16 / 31 / 52 / 92 to about 24 / 48 / 72 / 99.
Spring's gate roughly doubles and stops being the soft season; that is the accepted cost.
Every theme offerable in every Spring week.

## Verification

1. Unit tests: one per rule row above (a representative id each), the ramp derivation (even,
   reachable-bounded, monotone, difficulty-shifted), the foothold re-roll, week-based goal
   obtainability, deadline clamp to `GateSeason`, the unknown list in the dump.
2. Sims on the deployed build, both `tools/sim-year.sh goals` and `minimal`. Pass bands:
   each season's gate between 20% and 30% of the board's required slots; every offered theme
   asks 3 to 6 in every week of the year; no week where an offered theme asks under 2 while
   its domain still has lines; every gate passes; `tly_gatecheck` reports no impossible gate.
3. After each sim: full item list and unknown list to Jeff; his answers become
   `AvailabilityWeeks` or pin entries in the next commit.
4. STATUS.md and CHANGELOG carry the sim tables and the unknown list at ship time.

## Out of scope

Single-loop difficulty; Approach C; JP awards; stash; hub UI; Nexus description wording (done
at release with the README, house style).
