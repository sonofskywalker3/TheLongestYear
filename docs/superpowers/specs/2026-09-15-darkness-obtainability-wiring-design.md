# Darkness Rework, Part B: Wiring the Obtainability Model

**Date:** 2026-09-15
**Status:** built (commits `4613d36`..`93b29c4`), live-checked 2026-09-15 on the throwaway save after the fix waves; Jeff's played run pending
**Branch:** `story`
**Builds on:** `2026-09-14-darkness-rework-design.md` (the approved scheduling, dial, blight numbers,
guaranteed Winter tamper and stack limits, all unbuilt until now), `2026-09-14-obtainability-phase2-design.md`
(the model this wires in), `2026-09-09-darkness-pushback-design.md` (the effects, wards, letters and
scene, unchanged)
**Scope (Jeff, 2026-09-15: "let's knock the whole thing out"):** the whole rework plus the fairness
wiring, in one plan.

## Problem

The darkness has three separate dice each night and no idea whether the player can recover. Reversion
empties a slot the player already donated and the item is gone. Tampering rewrites an empty slot to
ask for a different item, chosen from the old item model by "similar effort, placed in Winter". On
easier settings this is rage bait, and the fronts pile onto the same night. The obtainability model
(Part A) can now answer "start from nothing on day N, when does this item land, by which route, needing
what", so the darkness can be fair where fairness is promised and cruel only where it is chosen.

## Plain-words summary of what changes

Before reversion empties a slot, the darkness asks the model: if this player starts from nothing
tomorrow, can they get this item back in time through means the level allows? Before tampering swaps an
item in, it asks the same about the new item and the bundle's deadline. "Checked against the real save"
means the model's conditions (a keg, a recipe, a coop, the greenhouse, mine floor 80, Fishing 6) are
looked up on the player's actual farm; what the player already has costs nothing, what they lack either
adds days or rules the route out, by level. Easy and Normal must find a fair pick or the hit goes
elsewhere. Hard and Extreme mostly follow Normal, but each has a once-per-loop chance of an unmoderated
hit that ignores the model entirely. The rest of the approved rework lands with it: one roll a night with
a decaying chance, one event per strike, the Darkness dial, blight numbers by level, the guaranteed
Winter tamper, and the stack limits.

## Section 1: the fairness picker

### 1.1 Inputs

Every reversion and tamper hit goes through one pure rule in Core, given:

- the obtainability model (may be null: a data section failed at load, so no fairness filter and every
  route counts, which is today's behaviour);
- the hit day (day of year, 1 to 112) and the deadline day;
- the Darkness level (Easy, Normal, Hard, Extreme; section 2.3);
- a snapshot of the real save, built by the glue just before the pick.

**SaveSnapshot** (Core record, filled from the game by the glue):

| Field | Read from |
|---|---|
| Cooking and crafting recipes known | `Farmer.cookingRecipes`, `Farmer.craftingRecipes` |
| Buildings on the farm, by type name | `Farm.buildings` (`Building.buildingType`) |
| Machines owned (placed anywhere, or in any chest or the inventory), by qualified id | placed big craftables on every location; chest and inventory items |
| Greenhouse open | `ccPantry` in the player's mail flags |
| Deepest regular mine floor reached | `MineShaft.lowestLevelReached` |
| Desert open (bus repaired) | the vanilla bus condition (`ccVault` mail), read the way the mod already reads it |
| Skill levels, all six | `Farmer.GetSkillLevel` |

Skull Cavern floors are not a wait; they are a condition (Jeff, 2026-09-15): the desert must be open
and the player must be able to craft Staircases (Mining 2). No days are added for the floor itself.

### 1.2 Which routes count

For each source the model has for the item, the rule decides "counts" or "does not count" at this
level, then the item counts when at least one route lands (after any added days) on or before the
deadline.

| Route property | Easy | Normal | Hard (moderated) | Extreme (moderated) |
|---|---|---|---|---|
| Chance route (cart, drop, geode, treasure, night market luck) | no | no | no | yes |
| Unresolved route (a guess) | no | no | no | yes |
| Ginger Island route | no | no | no | no |
| Year 2 route, other than TV | no | no | no | no |
| Year 2 Queen of Sauce episode (the eight dishes only the TV teaches: Complete Breakfast, Carp Surprise, Roasted Hazelnuts, Fruit Salad, Blackberry Cobbler, Bruschetta, Poppyseed Muffin, Shrimp Cocktail; the other eight year 2 episodes have a shop, festival, skill or friendship route too) | no | no | yes, always | yes, always |
| Missing recipe (cooking or crafting) | rules out | rules out | rules out | ignored |
| Missing machine | rules out | +1 day if the player knows its crafting recipe, else rules out | as Normal | ignored |
| Missing building, animal, sapling, tea bush, pond | rules out | adds the game's own days (the source's SetupStep) | as Normal | ignored |
| Missing skill level | rules out | adds the skill table days (1.3) | as Normal | ignored |
| Regular mine floor not reached | rules out | adds 1 day per 10 floors (1.3) | as Normal | ignored |
| Skull Cavern route | desert open AND Mining 2 already, else rules out | desert open required (repairing the bus is a board matter, never priced); Mining 2 via the skill table | as Normal | ignored |
| Greenhouse route without the pantry | rules out | rules out | rules out | ignored |

The Traveling Cart is a Chance route in the model, so it is excluded on Easy, Normal and Hard by the
first row (Jeff's 2026-09-14 ruling: not a source on Easy).

Conditions the model records that this table does not name (a location the map always has, a season
already encoded in the landing table, a festival) count as met.

### 1.3 The gap tables (tunables in `SabotageTuning`, all Jeff's first-draft numbers, 2026-09-15)

**Skill levels.** Days for a player deliberately working that skill to reach a level, from level 0. The
gap added is the target level's days minus the current level's days.

| Level | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| Days | 1 | 2 | 3 | 5 | 7 | 10 | 14 | 19 | 25 | 32 |

**Regular mine.** 1 day per 10 floors below the deepest floor reached, rounded up ("call it 10 floors
per day, we want it to be a stretch"). Floor 80 from floor 40 adds 4 days; floor 120 from scratch adds
12.

**Buffs and boosts** are not modelled: the table already assumes the player is working hard, eating the
right food and using the mod's boosts.

If the added days push the landing past the deadline, the route is out, the same as any other wait.

### 1.4 Deadlines (the consumer owns the deadline; Jeff, 2026-09-14)

- **Reversion, Easy:** the last day of the current season. The item must land again dependably before
  the season gate the player is about to face.
- **Reversion, Normal and above:** Winter 28.
- **Tampering:** Winter 28. Tampering only happens in Winter, and every bundle still unfinished then is
  judged on Winter 28.

### 1.5 The unmoderated roll (Jeff, 2026-09-15: "I don't want to FORCE an impossible item, only ALLOW one")

On Hard and Extreme, each reversion and each tamper first rolls whether it is unmoderated: **10% on
Hard, 30% on Extreme**. At most **one unmoderated reversion and one unmoderated tamper per loop**, and
the roll is spent when it fires even if what it picks turns out easy. When it fires:

- **Reversion** takes any filled slot in an unfinished item-room bundle, as today, with no model check.
- **Tampering** draws from the whole item catalog (every item the board can ask for, room themes
  respected as today), with no obtainability check at all. The stack limits (section 2.6) still apply.

When it does not fire, Hard and Extreme use their moderated columns from 1.2.

### 1.6 How each front picks

**Reversion.** Candidates are the filled, non-category slots in unfinished item-room bundles (as today).
Each candidate's item is tested with the rule from the day after the hit to the deadline. Pick uniformly
among the ones that pass. If none pass, reversion has nothing to act on and the night's event falls
through (section 2.2).

**Tampering.** Targets keep today's shape: unfilled slots in unfinished item-room bundles, the slots
whose item the player holds first. The replacement pool is the item catalog filtered by room theme (the
Bulletin Board's Mixed takes any), not already an ingredient of that bundle, and passing the rule from
the day after the hit to Winter 28. Among those, the five closest in effort to the original item, one at
random. The effort figure still comes from the existing item model (`ItemAvailabilityModel.For(id).Effort`),
because the new model has no effort number and this only ranks items the rule has already passed as
fair. The darkness code may read both models: the blind guard covers the obtainability folder and its
two glue files only, and `ObtainabilityComparison` stays the only file that names both models for the
purpose of comparing them. If no target has a fair replacement, tampering has nothing to act on.

**Sneak Peek Boost** is not consulted. The year 2 TV route is by level only (Jeff, 2026-09-15: "count
it as available always on hard and extreme only"). A Normal player who bought the boost is never asked
for one of the eight dishes and never has one reverted; that only makes Normal gentler.

**Built as:** a derived route (grown, crafted or machine-made from another item) carries its own
inputs and is judged recursively, with the input's own setup days propagated into the derived route's
landing day, the largest such push winning across sibling groups and the smallest winning within a
group. The case of a fast but currently-blocked input against a slower always-available alternative is
resolved leniently, by the size of the setup-day difference rather than by a hard cutoff. Fish pond
routes rule out below Extreme in every case, because pond contents are never modelled in the save
snapshot.

## Section 2: the night, the dial and the numbers (the approved rework, unchanged in substance)

### 2.1 One roll a night with a decaying weekly chance

Each ordinary night in Summer, Fall and Winter rolls once: does the darkness strike? The chance starts
each week at the season's value (**Summer 25%, Fall 35%, Winter 35%**), drops **5 percentage points**
after every strike that week, and resets to the season's value on the first day of each week. Never on
day 28, never on the win night, never in Spring. The same chance on every Darkness level; only the hits
scale. The roll is seeded from the run seed and the day as today, so re-sleeping the same night gives the
same outcome.

### 2.2 One event per strike, split evenly, with fall-through

| Season | Options, each equally likely |
|---|---|
| Summer | crop blight, chest blight |
| Fall | crop blight, chest blight, reversion |
| Winter | crop blight, chest blight, reversion, tampering |

The existing caps stay: blight on at most 2 nights a week; reversion at most once a week and never from
day 25; tampering at most twice a Winter, at least 5 days apart, never from day 21. An option that is
capped, switched off in the config, warded, or has nothing to act on (no live crops, nothing stored, no
filled slot in an unfinished bundle, **no fair candidate**) hands its share evenly to the remaining
options. If none can act, there is no strike that night and the chance does not drop. So on Easy, a night
where every filled slot fails the fairness test becomes a blight night or a quiet night.

### 2.3 The Darkness dial

An eleventh dial, **Darkness**, in the Difficulty section, with the same four steps as the others. The
overall Difficulty lever sets it with the other ten; the lever stays a setup shortcut no gameplay reads.
Stamped into the save's difficulty profile at reset like every other dial, so a change takes effect at
the next rewind. **Migration:** an existing config, and a save stamped before the dial existed, set the
Darkness dial AND the overall lever to the **lowest** of the ten existing dials. The three per-front off
switches (`EnableBlight`, `EnableBundleReversion`, `EnableRequirementTampering`) stay as they are.

### 2.4 Blight by level

Share of live crops (crop blight) or of stored units (chest blight), at least 1, capped:

| Darkness | Share | Summer cap | Fall / Winter cap |
|---|---|---|---|
| Easy | 4% | 8 | 12 |
| Normal | 5% | 10 | 15 |
| Hard | 6% | 12 | 18 |
| Extreme | 7% | 14 | 21 |

Crop blight: `HoeDirt` with a live crop on the Farm only; the Ward of the Fields for the season stops it.
Chest blight: the Junimo Stash never, chests on a Circle of Warding never.

### 2.5 Chest blight on Extreme reaches everything (Jeff, 2026-09-15)

On Easy, Normal and Hard, chest blight keeps today's pool: plain object stacks only, so tools, weapons,
rings, boots, hats and big craftables in a chest are never taken (a test pins this). **On Extreme the
exclusion is lifted:**

- **Anything in an unwarded chest** can go missing: tools, weapons, rings, boots, hats, and machines kept
  in storage. One unit at a time, weighted by stack size like everything else.
- **Machines placed on the farm map** (kegs, furnaces, preserves jars, crystalariums, bee houses and any
  other big craftable) join the same pool. A picked machine vanishes with whatever was inside it. A
  machine standing on a Circle of Warding's nine tiles is protected, the same as a chest there. Other
  maps (greenhouse, sheds, the island) are exempt, matching crop blight's farm-only rule.
- **A big craftable counts as 3 units** when picked, against the night's take and in the pick weighting,
  so a night that takes 15 units loses at most 5 machines ("so they can't be decimated in one night").
- All of it falls under the existing "gone missing overnight" line; no new player-facing text.

**What the game does about a stolen tool** (PC 1.6 decompile, `Game1.fixProblems`, run on save load and
each new day): a missing Axe, Pickaxe, Hoe, Watering Can, Scythe or Return Scepter is replaced by a
BASIC one in the Lost and Found box at the Mayor's Manor, with the "found lost tools" letter. The
upgrade is what the player loses. Fishing rods are bought from Willy, the pail and shears from Marnie,
weapons from the Guild. Nothing soft-locks. Jeff: "inconvenience rather than game changer, but I can
live with it."

### 2.6 Guaranteed Winter tamper and the stack limits

The first Winter a save ever reaches, tampering strikes on **Winter 1**. Every later Winter it strikes on
a random night of week 1. It counts as that week's first strike (the chance drops 5 points) and uses one
of the two tampers. It goes through the fairness picker like any other tamper and can be the unmoderated
one if that roll says so. If no target has a fair replacement, the guaranteed tamper is skipped that night
and retried on the next eligible night of week 1; a Winter with no possible tamper at all simply has none.

The tampered ask goes through the board's own limits: legendary fish always 1 (`LegendaryFishRules`, as
`QuantityAskPass` does; today's tamper path skips this, which is a bug this fixes); never above the item's
basis ceiling by Winter 28; the gold-quality reduction when a quality is asked; 1 when the item has no
basis. The stack's slice keeps scaling by level (Easy 0 to 10%, Normal 10 to 20%, Hard 20 to 30%,
Extreme 30 to 40%), now read from the Darkness dial instead of Stack Size, after the divide-by-Winter-
weeks rule that already exists.

### 2.7 Debug tools

`tly_sabotage` keeps its shape. `arm <blight [crops|chest]|revert|tamper>` arms tonight's single roll and
names the event; the forced strikes stay. `status` adds the week's current chance, the Darkness level,
and whether this loop's unmoderated reversion and tamper have been spent. A new `tly_sabotage fair
<itemId> [level]` prints the rule's verdict for one item against the current save (each route: counts or
not, why, days added, landing day), which is how the live check and Jeff's play test read the picker.

## Section 3: code shape and testing

### 3.1 Core (pure, tested against a fake model)

- `Core/Sabotage/DarknessLevel.cs`: the per-level numbers (blight share and caps, unmoderated chance,
  stack slice) keyed by the Darkness dial's `DifficultyStep`.
- `Core/Sabotage/SaveSnapshot.cs`: the record from 1.1.
- `Core/Sabotage/FairnessRule.cs`: `Counts(itemId, hitDay, deadlineDay, level, snapshot, model)` and
  `Explain(...)` for the debug command, using `ObtainabilityModel.Sources`, `ObtainSource.Conditions`,
  `ObtainSource.Setup` and `DayTable.Lands` only. Reads `Conditions.Requires` strings by their recorded
  prefixes (`machine:`, `recipe:`, `building:`, `mail:ccPantry`, `floor:`, `location:SkullCave`, and the
  skill fields). The skill days table and the floors-per-day figure live in `SabotageTuning`.
- `Core/Sabotage/NightRoll.cs`: the single roll, the decaying chance and weekly reset, the even split
  with fall-through, the guaranteed Winter tamper.
- `SabotageSchedule` and `BlightRule` gain the per-level numbers and the Extreme storage rules (the
  3-unit weight, the widened pool); `TamperRule.Stack` reads the Darkness level and applies the
  legendary and basis limits.
- `RunState`: `DarknessChanceWeek`, `DarknessChance` (the week's current value), `UnmoderatedReversionSpent`,
  `UnmoderatedTamperSpent`, `GuaranteedTamperDone` (per Winter), all cleared at reset.
- `DifficultySettings.Darkness`, the `DifficultyProfile` stamp, the `DifficultyResolver` lever mapping,
  the migration rule (lowest of the ten).

### 3.2 Glue

- `SabotageService`: one night roll instead of three; builds the `SaveSnapshot`; hands the model in
  (`Func<ObtainabilityModel>`, may return null); the tamper candidate list is drawn from the item catalog
  and filtered by the rule; the existing model is read only for effort closeness.
- `SpoilagePass`: the Extreme pool (all chest items, placed farm machines outside a circle, 3-unit
  weight). `CircleOfWardingService` exposes "is this tile warded" for machines as it does for chests.
- `ModEntry`: the eleventh dial in GMCM, the config migration, the `fair` debug subcommand.
- No new player-facing strings. If one turns out to be needed, it goes through the game-writing skill.

### 3.3 Guards and proofs

- The blind guard stays; nothing under `Core/Obtainability/` or its two glue files changes.
- `ObtainabilityComparison.cs` stays the only file naming both models for comparison. The darkness code
  reads the new model through its public API and the old model for effort, in separate files.
- Board generation, gates, goals and pacing do not read the model. A test pins byte-identical
  `tly_genbundles` and `tly_gatecheck` output across seeds before and after Part B.
- No `/sdcard/` paths, no em dash characters, no manifest version bump on this branch.

### 3.4 Tests

Each cell of the 1.2 table against a fake model (chance routes, unresolved routes, island routes, year 2
TV per level, each condition kind per level, added days crossing the deadline, the machine-with-recipe
day, Skull Cavern as a condition); the skill and floor gap maths; both deadlines; the roll, the decay
and the weekly reset; the even split and its fall-through including "no fair candidate"; blight by
level; the Extreme storage pool, the 3-unit weight and the circle protecting a machine; tools and
weapons surviving a full blight below Extreme; the migration's lowest-dial rule; the stack limits
including legendary fish; the once-per-loop unmoderated roll for each front; the guaranteed Winter tamper
and its retry; the null-model fallback; the run-state reset.

### 3.5 Live

An automated launch on a throwaway `None_*` save (never Jeff's saves, never the mouse or keyboard,
`docs/HEADLESS_DRIVING.md`) confirms the dial migrates, the model publishes, `tly_sabotage fair` reads the
real save, and a forced tamper picks through the model. The played run through every front, staged with
`tly_sabotage arm` so Jeff sleeps into each one, stays Jeff's own (TODO: "Darkness pushback: Jeff's live
test before it ships").

## Non-goals

- Item quality in the fairness test (the model tracks items only).
- Pricing the Sneak Peek Boost, food buffs or the mod's other boosts into the gap tables.
- Repairing the bus, or any board-owned unlock, as a priced wait.
- Any change to board generation, gates, goals or pacing.
- A curated "cruel list" for the unmoderated roll (Jeff: full item list, no restrictions).

## Rulings log (Jeff, 2026-09-15)

| Ruling | Effect |
|---|---|
| "let's knock the whole thing out" | Part B builds the whole rework plus the wiring in one plan. |
| Hard: "limit reversion and tampering to 1 of each as above and any remainder same as normal", then refined: "10% chance for hard, 30% on extreme, still only 1 per loop even if it picks an easy item" | Section 1.5. |
| "I'd count it as available always on hard and extreme only" (year 2 Queen of Sauce) | The year 2 TV row of 1.2. |
| "I don't want to FORCE an impossible item, only ALLOW one ... full item list, no restrictions" | The unmoderated tamper draws from the whole catalog with no check. |
| "Easy reversion shouldn't be 14 days, it should also be by the end of the season" | Reversion Easy deadline = last day of the current season. |
| "Normal reversion should disqualify based on missing recipes, but not skills or floors, within reason" | The gap tables of 1.3; recipes rule out on Normal. |
| "call it 10 floors per day, we want it to be a stretch" | Regular mine: 1 day per 10 floors. |
| "Skull Cavern is completely different, it resets from 0 every day, but you can use staircases" | Skull Cavern is a condition (desert open, Mining 2), never a wait. |
| Extreme's moderated hits keep "any route including chance" (Jeff corrected my restatement) | The Extreme column of 1.2. |
| "add the excluded items for extreme difficulty ... include machines lying around the farm too" | Section 2.5. |
| "I want those big craftables to count as 3 items if picked" | The 3-unit weight in 2.5. |
| Stolen tools: "inconvenience rather than game changer, but I can live with it" | Tools stay in the Extreme pool; the game's Lost and Found returns a basic one. |
