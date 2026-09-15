# Item Obtainability Model, Phase 2: Start-Day Meaning and Gap Closure

**Date:** 2026-09-14
**Status:** phase 2 built (commits `639dd34`..`51ab859`), review findings fixed (commits
`ea68649`..`3feb9fe`), rerun report awaiting Jeff's rulings (2026-09-15 live rerun on the throwaway
save after the fixes: `Obtainability model: 1113 items in 823 ms, 7 pass(es), 45 unresolved
source(s).`; NewEarlier 173, NewLater 14, LuckOnly 137, OnlyExisting 7, OnlyNew 606, Agree 141, 24
dependable only through an unresolved source; 331 items listed for ruling in STATUS.md and
`.superpowers/sdd/2026-09-14-obtainability-phase2/final-fixes-report.md`)
**Branch:** `story`
**Builds on:** `2026-09-14-item-obtainability-design.md` (phase 1, the blind build)
**Followed by:** Part B, wiring the model into `2026-09-14-darkness-rework-design.md` (its own
brainstorm after this report is clean)

## Problem

Phase 1 answers "in which weeks can this item come out of the ground, the machine, the water or the
shop?" That is the wrong question for a darkness hit. Jeff's ruling (2026-09-14): the question is
"the hit lands in week N, the player holds nothing and expected nothing; can they start from nothing
in week N and still get the item?" Phase 1 credits the finish week, so a Spring seed bought Spring 28
and planted in the greenhouse credits week 5 even though a player hit in week 5 cannot buy that seed.

The report also showed three patterns that hide the real disagreements: the Traveling Cart makes
almost everything "any week" by luck; 598 items the existing model never placed on a board pad the
report; and a list of real gaps (mine fish, Tea Leaves, Broccoli, guild rewards, Moss, machine output
methods, Magic Bait rows, the fair minigame map, farm-map fish delegation) plus three review bugs.

## Decisions (Jeff, 2026-09-14)

1. **Start week plus how long it takes (option B).** A source records, for every start day, the first
   day the item lands. The consumer applies its own deadline (Winter 28 for reversion, the bundle's
   gate for tampering). The model never decides the horizon.
2. **Days inside, weeks outside (option 3).** Each source holds a 112-slot day table (start day to
   landing day, or never). Every question is asked and printed in weeks, except the picker, which
   passes the true hit day and deadline day.
3. **Setup steps carry their days (option B).** Buildings, animals, ponds, saplings and bushes record
   their setup with the game's own day figures. The blind report still assumes buildings stand so its
   figures compare like for like; Part B adds the setup days for whatever the real farm lacks.
4. **Dependable-only is the headline comparison.** Verdicts compare the new dependable-only landing
   week from day 1 against the existing hard week.
5. **The report prints detail only where a ruling is needed.** NewEarlier, NewLater and OnlyExisting
   in full; Agree as a table; OnlyNew as names only at the bottom. Jeff: "let's try that and see how
   it stacks up. Report on any outliers during testing."

## Section 1: what a source records

```
ObtainSource
  Kind, Reliability, Conditions, Detail      as phase 1
  Lands            DayTable: 112 slots, start day (1 = Spring 1 .. 112 = Winter 28) to the first
                   landing day, or Never
  Setup            list of SetupStep (Name, Days): "building:Coop 3", "animal:Chicken 1",
                   "friendship:Chicken 200 (about 28 days)", "sapling 28", "tea bush 20". Empty for
                   most sources. Informational for the blind report; Part B adds the days it needs.
```

`WeekMask` stays for what the readers still express in weeks (a season, a festival window); it is
no longer the unit inside a source.

**Rules, restated as "start today, when does it land":**

- **Same-day sources** (forage, fish, crab pot, shop row, mine node, monster drop, artifact spot,
  garbage can, festival stock, cart): day N lands day N on every day the source is available, never
  on the others. A source that works on a few days of a week keeps `FewDays` and lands only on those
  days.
- **Crops:** started on day N, the seed must be obtainable on day N (its own landing table says day N
  lands day N), the crop must stay in season from planting to harvest, or be in the greenhouse
  (`mail:ccPantry` condition, its own kind as before). Landing is the first harvest day. Regrowth
  gives no earlier landing so it is not read for landing; it stays in Detail.
- **Fruit trees:** sapling obtainable day N, plus 28 days to mature, then the first day in the
  fruit's season. Greenhouse variant as before.
- **Machines and recipes:** the landing day is the latest of the inputs' landing days from N, plus
  processing time (days, or minutes rounded up to days). Machine rule order, "any item" triggers and
  the other phase 1 limitations stay as documented there unless closed below.
- **Set-up-once producers** (animal, fish pond, tapper, tea bush): landing = the input's landing
  from N, plus the setup days that the blind model counts (sapling 28, tea bush 20, tapper tree
  growth is a condition), then the produce interval. Building days are recorded as a SetupStep and
  NOT added by the blind model (decision 3).
- **Chains** settle by repeated passes. A landing day can only move earlier as passes run and there
  are finitely many day slots, so the build converges; the pass cap stays as a guard.
- **A machine may never make its input earlier than the input already lands.** This removes the
  self-feeding loop (a dehydrator or cask keyed under its own input) by construction.

**Queries:**

- `Lands(item, startDay, accept)` the earliest landing day over accepted sources, or null.
- `CanObtain(item, startDay, deadlineDay, accept)` = Lands is not null and not after the deadline.
- `LandingWeekFromDay1(item, accept)` for the report.
- `Sources(item)` for the debug command, each with its table rendered as week ranges.

## Section 2: gaps and their closure

Each fact below was read in the PC 1.6 decompile; the file and line go beside the code.

- **Stonefish, Ice Pip, Lava Eel** (`MineShaft.getFish`): rolled in code by mine area 0 to 39, 40 to
  79, 80 plus; any season, any weather. Hand-typed dependable `Fish` sources with the floor range as
  a condition.
- **Adventure Guild rewards:** in 1.6 these are the `Data/MonsterSlayerQuests` asset
  (`MonsterSlayerQuestData`: Targets, Count, RewardItemId). The glue reads it; each reward is a
  dependable source landing the same day with `guild:<quest> <count> kills` as a condition. The time
  to reach the count is a judgement for Part B, not a number the model invents.
- **Moss** (`Tree.cs` 842 to 971, 1274): mature trees grow moss outside Winter, scraped off for 1 to
  2 Moss. Hand-typed dependable forage, days in Spring to Fall; greenhouse variant all year.
- **Tea Leaves** (`Bush.inBloom`): Tea Sapling is a crafting recipe (Caroline, 2 hearts, the recipe
  asset already carries the unlock). The bush is a set-up-once producer: 20 days setup, then leaves
  on days 22 to 28 of any non-Winter month, any month when sheltered.
- **Season seeds from fishing treasure** (`Utility.getRaccoonSeedForCurrentTimeOfYear`): Carrot,
  Summer Squash, Broccoli, Powdermelon seeds by the season of the catch, switching to the next season
  after day 23 in Spring and day 20 otherwise. Chance sources by day. The Raccoon shop rows already
  read stay conditional on the raccoon bundles.
- **Seed Maker** (`Object.OutputSeedMaker`): any crop harvest to its seed (first crop row whose
  harvest matches), 2% Mixed Seeds, 0.5% Ancient Seeds. Read from the crops asset, no table.
- **Mushroom Log** (`Object.OutputMushroomLog`): Common, Red, Purple, Morel, Chanterelle by nearby
  tree type. Hand-typed chance sources, all year, condition `machine:(BC)MushroomLog`.
- **Cask** (`Cask.OutputCask`): raises quality of the same item only. Dropped as a source. The model
  tracks items, not quality; that is an explicit non-goal.
- **Magic Bait** `(O)908`: sold by Mr Qi and crafted from his recipe, both Ginger Island. Fish rows
  requiring it now route through the bait's own sources and so inherit the island flag.
- **`fishingGame`:** the Fair minigame map, dropped by name in the glue. Festival maps players really
  fish on (BeachNightMarket, DesertFestival) stay.
- **`LOCATION_FISH <name> ...`:** the glue expands the query by copying the named location's fish
  rows into the delegating location, so the farm-variant maps resolve instead of listing as
  unresolved.
- **Island negation** (`ConditionSeasons` line 51): a clause starting with `!` no longer sets the
  island hint.
- **Failed data section:** if any asset read fails, the glue logs it and publishes no model. The
  debug command reports "no model: Data/X failed to read". Part B treats "no model" as "no fairness
  filter" and keeps today's behaviour.
- **`LocationSpawn.Chance`:** carried but unread stays; forage and fish remain dependable by the
  spec's table.

Kept as diagnostics on purpose: furniture and wallpaper catalogues (`ALL_ITEMS`, out of pool), Dish
of the Day, tool upgrades, pet adoption, movie concessions, items sold by the player.

## Section 3: the report and the review loop

- `tly_obtain compare` writes the counts table, then item detail for NewEarlier, NewLater and
  OnlyExisting only. Each disagreeing item lists the existing basis and every source as
  `Kind, Reliability, from day 1 lands week N, conditions | detail`.
- Agree stays a table; OnlyNew is a names-only list at the bottom.
- `tly_obtain <item> [day]` prints every source with its landing week from day 1, or from the given
  day when one is passed.
- After the build, the model and comparison are rerun on a throwaway `None_*` save (an automated
  launch, never Jeff's), the counts and any outliers are posted, and Jeff rules item by item. Each
  ruling is appended to the ruling log below.

## Section 4: code shape and testing

- **Blind folder, guard intact.** New in `src/TheLongestYear.Core/Obtainability/`: `DayTable.cs`
  (the 112-slot table and its maths), `CodeSources.cs` (the hand-typed tables from the decompile,
  each entry annotated with file and line). The glue gains readers for `Data/Buildings` (BuildDays)
  and `Data/MonsterSlayerQuests`, the `fishingGame` drop, the `LOCATION_FISH` expansion and the
  refuse-to-publish path. The guard test's file list grows with the new files. Hand-typed tables are
  typed from the decompile, never from the existing model's tables.
- **Comparison** (`ObtainabilityComparison`, the only non-blind reader) switches to the landing week
  from day 1 and the new section layout.
- **Tests:** DayTable maths (shift by days, never past Winter 28, in-season growth, greenhouse),
  each new rule against plain records, the self-feeding machine no longer widening, island negation,
  refuse-to-publish, Magic Bait routing, LOCATION_FISH expansion, the comparison verdict and layout.
  Existing tests that assert phase 1 week masks are rewritten to the start-day meaning; any expected
  value that looks wrong stops the task for a report rather than an edit.
- **Nothing reads the model for gameplay in Part A**, so no byte-identical board proof is owed here.
  Part B owes it if board generation ever reads the model.
- Test command: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release -p:EnableModDeploy=false`.

## Non-goals

- Item quality (casks, gold-quality asks). The model tracks items only.
- Year 2 and Ginger Island sources stay recorded but never counted by default.
- Board generation, gates, goals, pacing: untouched.

## Known limitations (phase 2)

What the phase 2 model deliberately does not do. None of these is a bug to file; each is a decision
or a piece of work Part B can pick up.

- **Fish area is not modelled.** `FishAreaId` on a Data/Locations fish row picks a stretch of water
  (the Mountain lake's two halves, the Forest river and pond). The model reads every row of a
  location as if the whole map were one water, so a fish tied to one area reads as available
  anywhere in that location.
- **A delegating `LOCATION_FISH` row's `Chance` is not multiplied.** The expansion copies the target
  location's rows and keeps both rows' gates, but the two chances are not combined. Chance is not
  read for reliability anywhere in the model, so nothing downstream is wrong today; it would matter
  only if a consumer started reading odds.
- **Machine "any item placed in" triggers are skipped.** A trigger with no required id and no
  required tags has nothing to read, so the glue drops it rather than record a machine that needs no
  input at all. A machine whose only rule is such a trigger contributes nothing.
- **Setup days are recorded but never added** (decision 3). Buildings, animals, ponds, saplings and
  bushes carry their day figures as `SetupStep`s and the blind model assumes they stand, so animal
  produce and pond produce read earlier than a player starting from nothing could reach them. Part B
  adds the days the real farm still owes.
- **Queen of Sauce air weeks are not read.** `Data/TV/CookingChannel` says which week teaches which
  recipe. The model does not read it: a recipe whose unlock is "none" and that no shop teaches is
  recorded `Unresolved`, so it lands the same day its ingredients do. That is why 48 dishes read
  dependable week 1. The comparison now marks these ("new dependable N (unresolved; known-source
  week M)") and counts them, but whether the model should read the TV schedule, or keep treating the
  unlock as a condition, is Jeff's ruling and is still open.
- **Island and year 2 inputs are derived per filter variant.** An input is read under all four
  variants (plain, island, year 2, both) and each is derived separately, carrying its own flags, so a
  flagged row never reaches an answer that excludes it. The cost is a few extra sources per flagged
  input in the debug listing; the property it keeps is that for any filter F, a derived item's table
  under F is what deriving from the input's table under F gives.
- **A cooking recipe's friendship and skill unlocks are conditions, not delays.** "f Robin 7" or
  "s Farming 3" is recorded on the source and never turned into a number of weeks, for the same
  reason the guild kill counts are not: how long a heart or a skill level takes is a judgement for
  the consumer, not a figure this model invents.

## Ruling log

Filled in as Jeff rules on the rerun report's remaining disagreements.

| Item | Existing | New (dependable, from day 1) | Ruling |
|---|---|---|---|
| All 173 NewEarlier groups (2026-09-15) | conditions priced as weeks | conditions recorded, only the game's own waiting counted | Jeff: yes. The model counts only the waiting the game forces; everything else (machine, recipe, building, floor, pantry) is a condition the darkness picker checks against the real save. |
| The 48 Queen of Sauce dishes (2026-09-15) | recipe week from the TV schedule | unlock "none", recorded as a guess | Jeff: yes, read Data/TV/CookingChannel. Episode k airs on the Sunday of week k of year 1 (TV.cs 518, DaysPlayed / 7); episodes 17 to 32 are year 2; Wednesday reruns only repeat earlier episodes. |
