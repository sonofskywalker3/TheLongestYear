# Activity themes: Spelunking, Artisan, Kitchen

**Date:** 2026-08-27
**Status:** approved in brainstorm (Jeff), not yet planned
**Supersedes:** the TODO entry "BRAINSTORM NEEDED: additional weekly themes"

## Problem

A weekly theme today is a Community Center room: Foraging = Crafts Room, Farming = Pantry,
Fishing = Fish Tank, Mining = Boiler Room, Mixed = Bulletin Board. Its goals are sampled from that
room's open bundle lines, its bonus and liability are fixed per theme (`ThemeModifiers`), and the
hub offers two of the five each week from seed + week + this month's picks, without looking at the
board.

Since the bundle engine re-rolls from thirteen item pools, a room no longer says what its bundles
ask for: a Pantry can be artisan goods and saplings, a Bulletin Board can be cooked dishes, a Boiler
Room can be gems and artifacts. No theme's bonus helps with artisan goods, cooking, animals, gems,
monster drops or artifacts, and a theme whose room has nothing in play offers a free drawback lift
("no quest this week").

Two more problems came out of the brainstorm, both measured:

- **Theme weeks fight the JP ramp.** Season multipliers are 1.0 / 1.5 / 2.5 / 4.0. A 4-item
  uncommon bundle donated whole in a Spring theme week is worth 57 JP (items + completion + weekly
  bonus); the same bundle donated one item per season and completed in Winter is worth 87 JP before
  any weekly bonus. Goals drawn freely from every open line pull donations forward against the ramp,
  and the only pressure to accept that is the drawback.
- **Narrow domains do not fill a week.** Simulating 100,000 boards from the engine's candidate lists
  (uniform per slot, no repeats in a room): monster drops are absent on 27% of boards and average 2.4
  lines; artifacts absent on 23%, 3.4 lines; animal products absent on 5%, 3.5 lines; minerals and
  gems 7.4 lines (57% of boards under 8); cooked dishes 7.4; artisan goods 13.5. A Spring week asks
  for 4 goals, Winter for 7.

## Rulings (Jeff, 2026-08-27)

1. Activity themes with goals matched by **item kind anywhere on the board**, not by room.
2. **Three** new themes, merged so each has a pool on nearly every board: Spelunking, Artisan,
   Kitchen. Not six narrow ones.
3. Every liability lands on a **different activity**, and the new liabilities land on the **new**
   activities (each bitten exactly once), never on the room activities a second time.
4. One plain effect per theme. Spelunking's bonus is the monster-kill double only; Kitchen's is the
   animal second product only. Geode doubling and cooked-dish doubling are out ("too much benefit,
   makes those feel like the must-pick option"). Card text is one line, no mechanics talk.
5. Goals follow the season gate first (rule A below).

## The themes

| Theme | Goals from | Bonus | Liability |
|---|---|---|---|
| **Spelunking** | minerals, gems, monster drops, artifacts | 10% chance a slain monster drops everything twice | **Artisan:** machines run 25% slower |
| **Artisan** | artisan goods | Machines finish 25% sooner | **Kitchen:** cooked food restores half its energy and health and gives no buffs |
| **Kitchen** | cooked dishes, animal products | 20% chance an animal gives a second product that day | **Spelunking:** monsters deal 25% more damage |

Existing five unchanged (Foraging, Farming, Fishing, Mining, Mixed), except Mixed's goal domain
(rule C). Sixteen weeks, eight themes, two cards a week.

Simulated pool sizes (lines per board, 100k boards): Spelunking 13.1 avg, under 4 on 1.6% of
boards; Artisan 13.5, under 4 on 0.5%; Kitchen about 11, under 4 on roughly 5%. For scale: Fishing
22, Foraging 8.5, Farming 6.7, Mining under 3.

## Design

### Theme enum and modifiers

`Theme` gains `Spelunking`, `Artisan`, `Kitchen` (appended; the enum value is persisted in
`RunState.SelectedThemesThisMonth` / `CurrentSelection`, so existing values keep their numbers).
`ThemeModifiers.For` gains three pairs:

| Theme | BonusId | LiabilityId |
|---|---|---|
| Spelunking | `monster_drops_double` | `machines_slow` |
| Artisan | `machines_fast` | `cooked_food_weak` |
| Kitchen | `animal_double_product` | `monster_damage_up` |

i18n: `theme.spelunking` "Spelunking", `theme.artisan` "Artisan", `theme.kitchen` "Kitchen";
`modifier.monster_drops_double` "10% chance a slain monster drops everything twice",
`modifier.machines_slow` "Machines run 25% slower", `modifier.machines_fast` "Machines finish 25%
sooner", `modifier.cooked_food_weak` "Cooked food restores half its energy and health and gives no
buffs", `modifier.animal_double_product` "20% chance an animal gives a second product each day",
`modifier.monster_damage_up` "Monsters deal 25% more damage". Plain ASCII (smallFont rule).

### Effects (Harmony, `src/TheLongestYear/Loop/`, one file per pair like today)

All read `ActiveEffectsProvider.ActiveBonus / ActiveLiability` and are no-ops otherwise; all are
suppressed by the existing `SuppressLiability` when the week's goals are met.

- `monster_drops_double`: postfix on `GameLocation.monsterDrop` (the per-kill drop routine): with 10%
  (seeded per kill from `Game1.random`) re-run the drop list once more. Not applied to the Prismatic
  Shard / rare-drop paths (`Monster.objectsToDrop` extra-drop list is included; hard-coded boss drops
  are not).
- `machines_slow` / `machines_fast`: postfix on `Object.PlaceInMachine` (1.6: `MachineDataUtility`
  path that sets `MinutesUntilReady`) multiplying the ready time by 1.25 or 0.75, rounded to the
  game's 10-minute tick. Both cannot be active at once (one theme per week).
- `cooked_food_weak`: prefix on `Farmer.eatObject`: when the item is a cooked dish (category -7),
  halve `staminaRecoveredOnConsumption` / `healthRecoveredOnConsumption` and skip the buff
  application (`Object.GetFoodOrDrinkBuffs` returns empty for that call). Week-scoped: nothing on the
  item changes.
- `animal_double_product`: postfix on `FarmAnimal.dayUpdate` (or the 1.6 `FarmAnimal.OnDayStarted`
  produce path): when the animal produced today and it is not already holding one, 20% chance to
  queue a second produce for pickup the same way `Duck Feather`-style extra drops work (a second
  item of the same id placed via `animal.currentProduce` after the first is collected, tracked in a
  small per-day set on `RunState`).
- `monster_damage_up`: prefix on `Farmer.takeDamage` multiplying `damage` by 1.25 when the source is
  a monster.

Exact hook names are confirmed against the decompile during planning; the spec fixes the behaviour.

### Goal domains (rule for "goals come from")

`BundleRequirement` gains an item-kind classifier used only for the three new themes and Mixed;
room themes keep `RoomThemeMap`. Kind is read from the game's own item category
(`Object.Category`): gems -2, minerals -12, monster loot -28, artifacts (`ItemRegistry` type
`(O)` with `Type == "Arch"`), artisan goods -26, cooking -7, eggs -5, milk -6, animal products
-18 (wool, feathers, truffles, rabbit's foot). `SlotPoolBuilder.OpenSlotsForTheme` takes a
predicate `slotMatchesTheme(theme, itemId)` instead of `req.Theme == theme`; for room themes the
predicate is the old bundle-level check, for activity themes it is per line.

### Rule A: goals follow the gate

`BonusSlotSampler.SampleSlots` gets a two-tier pool:

1. **Due lines**: open in-play lines whose item is single-season for the current season (the
   day-28 gate demands them). All of these are eligible.
2. **Filler**: every other open in-play line, but **at most one per bundle per week**.

The sampler fills from tier 1 first (existing rarity weighting), then tier 2 up to the season cap
(4/5/6/7) and the per-bundle remaining-need cap from 0.14.1. Applies to every theme, old and new.

### Rule B: offer only themes with a pool

`SelectionService.OfferForWeek` takes a `poolSizeFor(theme)` callback (open in-play goal lines for
the week). Candidates are themes not picked this month with `poolSize >= 2`; the two cards are
drawn seeded from the candidates, **weighted by pool size**. If fewer than two candidates qualify,
fill from the not-picked room themes in seed order (today's behaviour), so the offer is never
empty. The Sunday-night preview (`weekOfYear + 1`) uses the same callback for next week's season.

### Rule C: Mixed means anything

Mixed's goals are drawn from every open in-play line on the board (any kind, any room). Its
bonus/liability pair is unchanged.

### Hub and HUD

`WeeklyHubMenu` and the JP HUD read theme names through `ThemeDisplay` already; no layout change
(two cards). The card's bonus-item preview uses the same sampler, so Rule A shows on the card.

### Persistence

`RunState` unchanged except a `DoubleProduceToday` set for the animal bonus (cleared at day start).
Enum values appended; older saves load unchanged.

### Debug

`tly_themepool [theme]` prints each theme's open in-play line count for the current week (the
number Rule B uses) and, with a theme, the tier-1 / tier-2 lines Rule A would sample from.

## Testing

Core (xunit):
- `ThemeModifiers.For` covers all eight themes; each new liability id is distinct and none targets
  the same activity as its own bonus (table test).
- Classifier maps representative ids (Quartz, Ruby, Bat Wing, Ancient Doll, Wine, Pizza, Egg, Milk,
  Wool) to the expected themes; a Parsnip matches none of the three.
- Sampler Rule A: with 2 due lines and 6 filler lines in one bundle and cap 4, the sample is the 2
  due lines + 1 filler; with cap 2, the 2 due lines only.
- Rule B: pool sizes {Spelunking 0, Artisan 5, Kitchen 1, Farming 3, ...} never offer Spelunking or
  Kitchen; with all activity pools 0 the offer equals today's room-theme offer; weighting test with
  a fixed seed.
- Rule C: Mixed's pool equals the union of all open in-play lines.
- i18n guard for the new keys.

Live (throwaway save): `tly_themepool` on a fresh board; pick Kitchen with animals on the farm,
`debug sleep`, confirm a doubled product and the log line; pick Artisan, place a keg, confirm the
ready time is 75% of vanilla; pick Spelunking, kill slimes with `debug` weapon, confirm double
drops appear in the log at roughly 1 in 10; take damage and confirm the 1.25x under Kitchen.

## Docs

README and Nexus description: the "Weekly themes" feature line names the eight themes; a "What's
New" entry at release; CHANGELOG `## Unreleased`. The engine catalogue doc gains a "theme pools"
table with the simulated line counts.

## Out of scope

Data-driven themes (JSON), more than two cards, changing the room themes' bonuses or liabilities,
Impossible mode, any change to how JP is earned.
