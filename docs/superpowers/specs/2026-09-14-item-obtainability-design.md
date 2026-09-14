# Item Obtainability Model

**Date:** 2026-09-14
**Status:** design approved in chat (Jeff, 2026-09-14); phase 1 (blind build + comparison) to be planned
**Branch:** `story`
**Followed by:** `2026-09-14-darkness-rework-design.md`, the first consumer

## Problem

The mod's item model (`ItemAvailabilityModel`) stores one earliest week per item: a pacing week and
a hard week. It can say "not before Winter" but not "only in Winter", and not "Spring and Winter but
never Summer". The darkness rework needs both: reversion must only empty a slot whose item can be
obtained again later this year (on Easy and Normal), and tampering must only swap in an item that can
be obtained in Winter (on Easy and Normal). Jeff, 2026-09-14: "the item model just needs to be updated
with complete obtainability info, that way no matter what I want to build in the future, it's within
reach".

A single end date was considered and rejected: a fish caught in Spring and Winter has no meaningful
end date that also says it is absent in Summer.

## Goals

- For every item, every way to obtain it in year 1, with the weeks each way works.
- Built at runtime from the installed game data, so other mods' content is covered.
- Queryable by week, by source reliability, and by allowed source kinds.
- Built blind first and compared against the existing figures before anything reads it.

## Non-goals (this phase)

- Wiring it into board generation, gates, goals, pacing or sabotage. Nothing reads it for gameplay in
  phase 1.
- Changing any existing earliest-week, effort, pin or quantity figure.
- Year 2 availability (recorded as a condition, never counted).
- Day-level precision (weeks are the unit; see below).

## Data shape

```
ItemObtainability
  ItemId            qualified id, normalized the way BundleParsing does
  Sources           list of ObtainSource

ObtainSource
  Kind              Forage | ArtifactSpot | Fish | CrabPot | Crop | GreenhouseCrop | FruitTree |
                    Shop | Cart | Machine | Cooking | Crafting | Animal | FishPond | MineNode |
                    MonsterDrop | Geode | Tapper | Festival | NightMarket | Trash | GarbageCan |
                    FishingTreasure | Other
  Weeks             set of weeks 1..16 (a 16-bit mask) in which this source can yield the item
  Reliability       Dependable | Chance
  Conditions        Skill + SkillLevel, Requires (notes: a location, building, machine, recipe,
                    shop, mine floor, prerequisite query), RainOnly, FewDays (the source works on
                    only some days of those weeks, e.g. Night Market Winter 15-17), CatchLimit,
                    YearTwo and GingerIsland (not counted by default), Unresolved (a condition or
                    query the model could not read; the weeks are a guess, not a fact)
  Detail            short human-readable origin, e.g. "Fish at Town, time 600-1900"
```

`Other` is a source the model knows exists but cannot read (an unsupported item query, a machine's
output method). Nothing is silently dropped: such sources are listed as diagnostics in the comparison
report.

**Weeks** are the unit (Jeff chose weekly over seasonal or daily): 16 weeks, the same unit gates,
goals and pacing already use. A source that works on only a few days of a week sets `FewDays` and
still owns that week.

**Reliability.** Every source is recorded and tagged (Jeff, option A). Dependable: forage spawns,
fish, crab pot, crops, shops with fixed or season-conditioned stock, machines, recipes, animals, fish
ponds, mine ores, tappers, festival shops. Chance: Traveling Cart, artifact spots, geodes, monster
drops, trash cans, fishing treasure. Each consuming rule decides which reliabilities it accepts; the
model never decides fairness.

**Meaning: weeks in isolation (Jeff, 2026-09-14).** "We're only tracking when something can be made
within a specific week in isolation. The whole game allows for saving items, even across loops, but to
be the most fair (for easy and normal) we need to assume they've saved nothing and make sure they can
still pivot from our interference, and it should be hard, but not impossible." A source's weeks mean
"can be obtained in that week by someone who saved nothing". Consequences:
- A recipe counts only in weeks where every ingredient can be obtained (the intersection).
- A crop, machine product or tree fruit counts from the day its seed, input or sapling can be obtained
  plus its growing or processing time, computed day by day. Time passing is allowed; stored items are not.
- Things built once that keep producing (a fish pond, an animal, a tapper, a learned recipe) count from
  the first week they can be set up.
Whether the player already holds an item is a separate question for the consuming rule.

**Item queries.** Game data may list an item as an item query ("RANDOM_ITEMS (O) 2 789",
"FLAVORED_ITEM Wine ..."). The model expands the queries it understands (RANDOM_ITEMS, FLAVORED_ITEM,
LOST_BOOK_OR_ITEM, SECRET_NOTE_OR_ITEM) and records the rest as unresolved diagnostics.

**Settling.** Made items are resolved by repeated passes. Every rule only gains weeks when its inputs
do, and there are finitely many item-week bits, so the passes always settle; a high pass cap only guards
against a future rule that breaks that property.

## Queries

- `IsObtainable(item, week, accept)` where `accept` filters by reliability and allowed kinds.
- `Weeks(item, accept)` the union of matching sources' weeks.
- `EarliestWeek(item, accept)` for the comparison report.
- `Sources(item)` for the debug command.

## Building it

- **Pure core, thin glue.** The rules live in Core and take plain records; the mod project reads the
  game data assets at save load and maps them into those records, the same split `ItemPoolBuilder`
  already uses.
- **Direct sources** from the data assets: locations' forage, artifact spots and fish; fish spawn
  rules (season, weather, time, level); crops (seasons, growth days, regrowth; a crop yields from the
  first week it can mature after its seed can be obtained); shops (stock conditions including season);
  machines; cooking and crafting recipes; farm animal produce; fish pond produce; monster drops; mine
  ore by floor; geodes; wild tree tap items; garbage cans; festivals and the Night Market.
- **Made items from ingredients** (Jeff, option A). An output is obtainable in the weeks its inputs
  are, carrying the machine's or recipe's unlock as a condition. Resolved by repeated passes until
  nothing changes, which handles chains (fruit to wine) and cycles (fish pond to roe to aged roe).
- **Greenhouse** crops are recorded as their own kind with the greenhouse unlock as a condition, all
  weeks, so a rule can accept or refuse them.
- **Cached** for the session and built at save load, never inside the rewind reset (the shrine close
  already stalls about 3 seconds; see TODO).

## Phase 1: blind build and comparison

Jeff, 2026-09-14: "before we fully wire it up, build it without any insight into existing figures and
see what it comes up with and compare to what we've already determined."

- **Blind.** The new code may not reference `ItemAvailabilityModel`, `ItemAvailability`, the
  `Availability` rule classes, `DefaultItemSeasonPins`, pacing or effort tables, or the quantity
  basis tables. A guard test enforces this by scanning the new namespace's source.
- **Comparison report.** `tly_obtain compare` writes a Markdown report next to the existing effort
  dump, listing for every item either model knows:
  - the existing model's pacing week and hard week;
  - the new model's earliest week, dependable-only and any-reliability;
  - a verdict: agree, new is earlier, new is later, only in the existing model, only in the new model;
  - the sources behind each disagreement.
  Summary counts at the top.
- **Debug:** `tly_obtain <item>` logs every source with kind, weeks, reliability and conditions.
- **Nothing wires in.** No board, gate, goal, pacing, sabotage or save state reads the new model, so
  boards are unaffected by construction.

## Known limitations (phase 1)

The model reads these loosely on purpose; the comparison report shows where they matter before
phase 2 decides whether any needs real handling.

- **Machine rule order.** The game uses the first rule that applies to an input; the model counts
  every rule that could, so a later rule's outputs can look reachable when an earlier rule always wins.
- **Per-item conditions.** `PerItemCondition` on shop and spawn rows is not read.
- **Raccoon seeds.** Fishing treasure's raccoon seed depends on the season it is caught in and is
  listed as an unresolved source, not a fact.
- **Fish pond growth.** A pond is counted from the week its fish is had; the days it takes to reach a
  product's population are not.
- **Magic Bait.** Rows that need Magic Bait are flagged, not routed through the bait's own weeks.
- **Fishing depth, `DAY_OF_WEEK`, negated weather.** Not narrowed; they read as "any week".
- **Machines that take any item.** A trigger with no required item or tags on item placement is
  skipped; an output-collected trigger reads as needing no input.

## Testing

- Unit tests in Core on plain records: week masks, crop maturity against season end, season
  conditions on shop stock, the ingredient fixed point (chains and a cycle), reliability tagging,
  the year-2 condition never counting.
- The blind guard test.
- Live: build on the throwaway save, log the build time and item count, write the comparison report,
  and spot-check a handful of items by hand (a Spring-and-Winter fish, Wine, a Night Market fish,
  Crystal Fruit, Copper Ore, a greenhouse-only-in-Winter crop).

## Phase 2 (separate plan, after Jeff reads the report)

Resolve disagreements, then wire the model into the darkness rework. Any later use by board
generation must keep existing earliest-week figures identical unless Jeff rules otherwise, proven by
byte-identical `tly_genbundles` and `tly_gatecheck` output across seeds.
