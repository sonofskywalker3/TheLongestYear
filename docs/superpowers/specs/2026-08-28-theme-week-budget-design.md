# Theme weeks: light in Spring, full in Winter (goal budget + late ramp)

Date: 2026-08-28. Status: approved by Jeff in chat ("build b, sim it, then c if you need to").
Follows the activity-themes spec (2026-08-27) and the headless sims of 2026-08-28.

## Problem (measured)

Two headless year sims on 0.16.72 (`tools/sim-year.sh`, board seed -1905615454, 26 bundles,
90 required slots of about 106) and Jeff's live report on Remixed agree:

- A player who completes every theme week reaches Winter with 11 open lines on the whole
  board. Winter week 1 offers Mixed 7 and everything else 0 to 2; weeks 2 to 4 offer 1 or 0
  for every theme. Spring week 4, Fall week 4 and Winter weeks 2 to 4 each ended in
  `Goal slots this week: []`.
- A player who only meets the gates has a fine Winter (7/7/6/4/7/3/7/3) until the win.

Cause, in numbers. The gates demand cumulative 11 / 34 / 65 / 90 slots, so Winter's own share
of the work is 25 lines. The theme weeks want 4 + 5 + 6 + 7 = 22 goals a season and 28 in
Winter. Over the year the board offers 90 lines and the theme weeks ask for 88: a zero-sum
board with Winter last in line. Rule B's filler (Summer 1, Fall 2 a week) lets a goal-chasing
player pull up to 12 of Winter's 25 lines forward (sim A was 15 ahead of the Fall gate). And
the fixed per-season cap hands week 1 everything the season has, so every season ends on a
cliff (week 1 gets 7, week 4 gets 0), not just Winter.

## Goal

Theme weeks are never impossible in Spring and never free in Winter, on any board, for both
the gate-only player and the goal-completing player. Spring asks for 2 to 3 things, Winter for
6 to 7, and week 4 of a season looks like week 1.

Not this build: single-loop difficulty (Jeff, 2026-08-28: Normal should be possible but not
easy to win in one loop; Hard should be impossible in one). That is the gate's job and gets its
own spec once the custom-bundle boards have been out for a while. emmalution won on one Summer
fail without touching the stash, so that spec is needed; it is just not this one.

## Design (approach B from the brainstorm)

Four changes. The first is the mechanism; the other three are numbers the mechanism makes safe.

### 1. Budget the weekly goal count from what is left

`GoalBudget.For(seasonCap, dueLines, fillerLines, fillerAllowance, weeksLeftInSeason)`:

```
fillerBudget = allowance unlimited ? fillerLines : min(fillerLines, allowance * weeksLeft)
askable      = dueLines + fillerBudget
perWeek      = ceil(askable / weeksLeft)
floor        = min(askable, 2)          # rule C offers a theme only at 2 or more
result       = askable == 0 ? 0 : clamp(max(perWeek, floor), 1, seasonCap)
```

The floor keeps a small domain on the cards: a theme with three lines at week 1 asks 2 now
(then 1 the week after) instead of 1 a week for three weeks and never being offered.

`weeksLeftInSeason = 4 - ((weekOfYear - 1) mod 4)`. `RunController.SampleSlotsForTheme` counts
the theme's open pool (Due vs not) and passes the budget as the sampler's `maxCount` instead of
the flat season cap. The season cap is now a ceiling, never a target. Worked examples on the
sim A board: Spring week 1 with 11 due lines asks for 3; Winter week 1 with 11 lines left asks
for 3, week 2 with 8 left asks for 3, week 4 with 2 left asks for 2 (today: 7, 1, 1, 0).

`AskableCount` (rule C, the offer) reads the same number, so a theme is offered only when its
budget is at least one.

### 2. Season caps 4 / 5 / 6 / 7 become 3 / 4 / 5 / 7

`BonusItemSampler.DefaultMaxCountBySeason`. 76 goals a year instead of 88, leaving slack on a
90-line board. Spring themes ask for 2 to 3 (Jeff's number). The JP budget calculator reads
the same table, so its projection moves with it.

### 3. Filler allowance 0 / 1 / 2 / 99 becomes 0 / 0 / 1 / 99

`GameplayConfig.ThemeFillerBySeason` default. Summer stops pulling Winter's any-season stock
forward; Fall may take one filler a week (four lines) so a thin Fall domain still fills.

### 4. Percentage ramps lean later

Derived default `floor(X * [0.25, 0.50, 0.75, 1.0])` becomes `floor(X * [0.15, 0.35, 0.60, 1.0])`.
Curated entries whose Fall value already equalled Winter move one step later: Exotic Foraging,
Animal, Crab Pot 1/3/5/5 become 1/2/4/5; Artisan 1/2/4/6 becomes 1/2/3/6; Adventurer's 0/1/2/2
becomes 0/1/1/2; Mineral 0/1/3/4 becomes 0/1/2/4. Crop, forage-expiry and already-late entries
(Quality Crops, Garden, Four Seasons Sampler, Rare Crops, Chef's, Brewer's, Preserver's, Home
Cook's Feast, Artifact, Fish Farmer's, Gil's Trophies, Forager's, Winter Star) stay. Seasonal
and per-item bundles are unaffected (their timing comes from pins). Expected on the sim A
board: Fall gate about 58 instead of 65, Winter's share about 32 lines instead of 25. The
difficulty shift (`ShiftRampToSlotCount`) still applies on top.

## Out of scope

- Approach C (engine sizes Winter bundles up, Winter forage demands everything). Only if the
  sims below still show Winter under 5 a week for the goal-completing player.
- Single-loop difficulty target (see Goal).
- Any change to JP awards, the stash, or the hub UI.

## Verification

1. Unit tests: `GoalBudget` (zero pool, thin pool, cap ceiling, unlimited filler, last week),
   updated ramp and config tests.
2. `tools/sim-year.sh goals` and `tools/sim-year.sh minimal` on the deployed build. Pass when:
   no season has a week with 0 askable for the offered themes while lines remain in its
   domain; Spring weeks ask 2 to 3; Winter weeks ask at least 4 for the gate-only player and at
   least 2 in every week for the goal-completing player; every gate still passes.
3. STATUS.md and CHANGELOG updated with the sim tables.
