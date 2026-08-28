# Activity themes: Spelunking, Artisan, Kitchen, and the theme-week economy

**Date:** 2026-08-27 (economics revision 2026-08-28 early)
**Status:** approved in brainstorm (Jeff); effort derived from game data (Phase 2 of the availability model); not yet planned
**Supersedes:** the TODO entry "BRAINSTORM NEEDED: additional weekly themes"

## Problem

A weekly theme today is a Community Center room: Foraging = Crafts Room, Farming = Pantry,
Fishing = Fish Tank, Mining = Boiler Room, Mixed = Bulletin Board. Its goals are sampled from that
room's open bundle lines, its bonus and liability are fixed per theme (`ThemeModifiers`), and the
hub offers two of the five each week from seed + week + this month's picks, without looking at the
board.

Since the bundle engine re-rolls from thirteen item pools, a room no longer says what its bundles
ask for. No theme's bonus helps with artisan goods, cooking, animals, gems, monster drops or
artifacts, and a theme whose room has nothing in play offers a free drawback lift.

### The theme-week economy is inverted (Jeff, 2026-08-27)

Season multipliers are 1.0 / 1.5 / 2.5 / 4.0 on every JP award. Early in a loop the drawback is
what can sink a run (mines closed in Spring with a Boiler Room gate coming), so the player is
pushed, sometimes required, to finish the week's goals to lift it. That is when goals are hardest
(no machines, no levels), when donating pays least (1x), and when the items spent are the ones that
would have paid 4x in Winter. By Winter the pools are spent, a one-goal week still pays the full
30 x 4 = 120 JP bonus, and the lift lands when the drawback barely matters. Worked example: a
4-item uncommon bundle donated whole in a Spring theme week is worth 57 JP (items 12 + completion
15 + weekly 30); the same bundle donated one item per season and completed in Winter is 87 JP
before any weekly bonus.

Activity themes whose items are all any-season (minerals, gems, artisan goods, dishes) make this
worse: a Spring Spelunking week would strip exactly the items Winter should be paid for, and three
Spelunking bundles could be emptied by Fall.

### Narrow domains do not fill a week (simulated)

100,000 boards from the engine's candidate lists (uniform per slot, no repeats in a room): monster
drops absent on 27% of boards, 2.4 lines avg; artifacts absent 23%, 3.4; animal products absent
5%, 3.5; minerals + gems 7.4 (57% of boards under 8); cooked dishes 7.4; artisan goods 13.5.
Merged: minerals + gems + monster drops + artifacts 13.1 lines (1.6% of boards under 4); animal +
artisan + cooked 24.5. A Spring week asks for 4 goals, Winter for 7.

## Rulings (Jeff, 2026-08-27/28)

1. Activity themes with goals matched by **item kind anywhere on the board**, not by room.
2. **Three** new themes, merged so each has a pool on nearly every board: Spelunking, Artisan,
   Kitchen. Not six narrow ones.
3. Every liability lands on a **different activity**, and the new liabilities land on the **new**
   activities (each bitten exactly once), never on the room activities a second time.
4. One plain effect per theme. Spelunking's bonus is the monster-kill double only; Kitchen's is
   the animal second product only. Geode and cooked-dish doubling are out ("too much benefit,
   makes those feel like the must-pick option"). Card text is one line.
5. **Fix the inverted economy** (rules A to D below): goals follow the gate, filler follows the
   ramp, the offer counts what a week can actually ask, the weekly bonus is per goal.
6. **Weight cheaper/easier items earlier in the year**, and effort must NOT be read from sell
   price: a Dinosaur Egg sells for less than a Diamond and is far harder to get. A hand-written
   tier list is insufficient (Jeff, 2026-08-28): effort is derived from the actual drop tables,
   geode tables, artifact-spot tables, machine and animal data and cooking recipes, as Phase 2 of
   the 0.16.0 availability model, and reviewed through a generated document.

## The themes

| Theme | Goals from | Bonus | Liability |
|---|---|---|---|
| **Spelunking** | minerals, gems, monster drops, artifacts | 10% chance a slain monster drops everything twice | **Artisan:** machines run 25% slower |
| **Artisan** | artisan goods | Machines finish 25% sooner | **Kitchen:** cooked food restores half its energy and health and gives no buffs |
| **Kitchen** | cooked dishes, animal products | 20% chance an animal gives a second product that day | **Spelunking:** monsters deal 25% more damage |

Existing five unchanged (Foraging, Farming, Fishing, Mining, Mixed), except Mixed's goal domain
(rule C). Sixteen weeks, eight themes, two cards a week.

## Design

### Theme enum and modifiers

`Theme` gains `Spelunking`, `Artisan`, `Kitchen` (appended; the enum value is persisted in
`RunState`, so existing values keep their numbers). `ThemeModifiers.For` gains:

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
`modifier.monster_damage_up` "Monsters deal 25% more damage". Plain ASCII.

### Effects (Harmony, `src/TheLongestYear/Loop/`, one file per pair like today)

All read `ActiveEffectsProvider.ActiveBonus / ActiveLiability` and are no-ops otherwise; every
liability is suppressed by the existing `SuppressLiability` once the week's goals are met.

- `monster_drops_double`: postfix on `GameLocation.monsterDrop(Monster, int, int, Farmer)`
  (decompile GameLocation.cs:4360). With 10% (`Game1.random`) clone every `Debris` the call added
  to `debris` for this kill (same shape as vanilla's own Book_Void 3% clone at the end of that
  method) and log once. Trinket spawns are not doubled.
- `machines_slow` / `machines_fast`: postfix on `Object.PlaceInMachine` after the output is
  queued: `MinutesUntilReady` x1.25 or x0.75, rounded to the nearest 10 minutes, floor 10.
- `cooked_food_weak`: prefix/postfix pair on `Farmer.eatObject(Object, bool)` (Farmer.cs:9140):
  when the item's category is cooking (-7), halve the stamina and health it restores and skip
  `GetFoodOrDrinkBuffs` (Object.cs:5057) for that call. Week-scoped: items are untouched.
- `animal_double_product`: postfix on `FarmAnimal.dayUpdate` where `currentProduce` is set
  (FarmAnimal.cs:1052): with 20%, record the animal id in `RunState.DoubleProduceToday`; a postfix
  on the collect path (`FarmAnimal.pet` / harvest via `Object` pickup) re-sets `currentProduce` to
  the same id once and removes the record. Cleared at day start.
- `monster_damage_up`: prefix on `Farmer.takeDamage(int, bool, Monster)` (Farmer.cs:7331):
  `damage = ceil(damage * 1.25)` when `damager != null`.

### Goal domains (rule for "goals come from")

`BundleRequirement` gains an item-kind classifier used for the three new themes and Mixed; room
themes keep `RoomThemeMap`. Kind is read from the game's own category (`Object.Category`) via an
injected `Func<string, ItemKind>` (the mod wires `ItemRegistry`; tests inject a dictionary): gems
-2, minerals -12, monster loot -28, artifacts (`Type == "Arch"`), artisan goods -26, cooking -7,
eggs -5, milk -6, animal products -18. `SlotPoolBuilder.OpenSlotsForTheme` takes a predicate
`slotMatchesTheme(theme, itemId)` instead of `req.Theme == theme`; for room themes the predicate is
the old bundle-level check, for activity themes and Mixed it is per line.

### Rule A: goals follow the gate

`BonusSlotSampler.SampleSlots` gets a two-tier pool:

1. **Due lines**: open in-play lines whose item is single-season for the current season (the
   day-28 gate demands them). All eligible.
2. **Filler**: every other open in-play line, at most **one per bundle per week**, and at most the
   season's filler allowance (rule B).

Tier 1 is drawn first, then tier 2, up to the season cap (4/5/6/7) and the per-bundle
remaining-need cap from 0.14.1. Applies to every theme, old and new.

### Rule B: filler follows the ramp

Filler allowance per week by season: **Spring 0, Summer 1, Fall 2, Winter unlimited** (config
`ThemeFillerBySeason`, default `[0, 1, 2, 99]`). In Spring a theme week is pure gate work (the
lift is earned by doing what the gate forces anyway, at no future cost); any-season stock is
untouched until Summer and mostly held for Winter, where it pays 4x. Three Spelunking bundles
cannot be emptied in Spring because Spring cannot ask for them.

### Rule C: the offer counts what the week can ask, and Mixed means anything

`SelectionService.OfferForWeek` takes an `askableFor(theme)` callback = the number of goals the
sampler would actually produce for that theme this week (tier 1 + allowed filler, after caps).
Candidates are themes not picked this month with `askable >= 2`; the two cards are drawn seeded
from the candidates, **weighted by askable count**. If fewer than two qualify, fill from the
not-picked room themes in seed order (today's behaviour) so the offer is never empty. The
Sunday-night preview uses the same callback for next week's season. The hub's playtest re-roll
uses the same candidate list.

Consequence: Spelunking, Artisan and Kitchen mostly appear from Summer on and cluster in Fall and
Winter, when their items are worth having held; Farming, Fishing and Foraging carry Spring because
their items are the Spring gate. A theme with nothing to ask is never on a card, so the "free
lift" week cannot happen.

Mixed's goals are drawn from every open in-play line on the board (any kind, any room), under the
same tiers. Its bonus/liability pair is unchanged.

### Rule D: the weekly bonus is per goal

`WeeklyQuestCompletionBonus` (30) x season multiplier is the bonus for a **full** week. It is
split evenly across the week's goals and paid as each goal lands
(`AwardCompletionRewards` becomes per-goal; the idempotency guard becomes a per-slot `Paid` flag
on `BonusSlot`). The drawback still lifts only when every goal is done. A one-goal Winter week
pays 120 / 7 = 17 JP, not 120. An empty week (no askable goals) is not offered at all (rule C).

### Rule E: easier items earlier

The sampler's per-id weight becomes a function of **effort tier and week**, replacing the
inverse-rarity weight for goal sampling (rarity still drives JP):

| Season | Easy | Medium | Hard | Extreme |
|---|---|---|---|---|
| Spring | 8 | 3 | 1 | 0 |
| Summer | 6 | 4 | 2 | 1 |
| Fall | 3 | 4 | 4 | 2 |
| Winter | 1 | 2 | 4 | 8 |

A zero weight means "not this season" (an Extreme item is never a Spring goal). If the weights
leave an empty pool the season cap is simply not reached; the offer floor (rule C) already
accounts for it because it asks the sampler.

### Effort, derived from the game's own tables (availability model, Phase 2)

Jeff, 2026-08-28: a hand-written tier list is insufficient; effort has to come from the actual
drop tables, recipes and animal data, the way the 0.16.0 model derives fish and metals. So this
feature carries **Phase 2 of `ItemAvailabilityBuilder`**: every item kind the three themes ask
for gets a `Derive` rule that reads live game data at SaveLoaded (through the same
`GameDataPools` shell that already loads Data/Fish), records a human-readable `Basis`, and
produces an integer `Effort` on the scale fish and metals already use (copper ore 1, iron 3, gold
5, iridium 7). Tiers for rule E are then quartiles of effort **within each theme's pool**, so
Easy/Medium/Hard/Extreme are relative to what that theme can ask.

Nothing in the tier table is typed by hand. The curated `effortOverrides` layer stays as the
user-editable escape hatch and ships **empty**. `tly_itemmodel` (existing) prints effort, tier and
basis for any id; a new `tly_dumpeffort` writes `docs/item-effort-model.md` (every pool item,
sorted by theme and effort, with its basis) so Jeff reviews derived values, not guesses. A tier
that looks wrong in that document is fixed by fixing the rule, not by an override.

The rules, per domain (data source, then the formula). Reachability facts that live in code, not
data, are recorded as rules with the decompile reference, exactly as `MetalsAvailability` does
for mine areas.

**Gems and mine minerals** (Data/Objects category -2 / -12; node floors are code facts,
`MineShaft.chooseStoneType`, decompile MineShaft.cs ~3885 onward and the gem-node branches):
effort = area effort of the shallowest area the node spawns in (area 0: 1, area 40: 3, area 80:
5, Skull Cavern 121: 7) + 1 if the node only appears in the dark/deep variant of that area. A
gem that only comes from geodes uses the geode rule.

**Geode contents** (Data/Objects `GeodeDrops` per geode item, with `Chance`, `Condition`,
`Precedence`; `GeodeDropsDefaultItems` pulls in the hard-coded list in
`Utility.getTreasureFromGeode`, decompile Utility.cs:6368): effort = effort of the easiest geode
that can yield the item + a rarity step from the per-geode chance (chance >= 1/8: +0, >= 1/20:
+1, else +2). Geode effort: Geode 1 (floors 1 to 39), Frozen Geode 3 (41 to 79), Magma Geode 5
(81 to 119), Omni Geode 4 (any area at low odds, Skull Cavern reliably; reachable by Fall via the
Vault), Trove/Golden Coconut not counted. Cracking costs 25g at Clint, which is ignored (the
availability floor already needs the mine).

**Monster drops** (Data/Monsters: field 6 is the drop list as `id chance` pairs; spawn floors are
code facts in `MineShaft.getMonsterForThisLevel`, decompile MineShaft.cs:4033, plus Skull Cavern
and the Quarry/Sewers spawns): effort = area effort of the shallowest area the dropping monster
spawns in + a rarity step from its drop chance (>= 0.5: +0, >= 0.1: +1, else +2), taking the
minimum over every monster that drops the item. The Slime Hutch is not counted (it needs the
Wizard). Items sold by Krobus or the Adventurer's Guild are still derived from monsters (shop
availability is a different fact and stays out of effort).

**Artifacts** (Data/Locations `ArtifactSpots` per location plus the `Default` list, each with
`Chance`, `Condition` and `Precedence`, decompile GameLocation.cs:14062; fossil bone nodes and
Ginger Island digs count as their own locations): effort = the location's reach effort (Farm /
Town / Forest / Beach / Mountain / Bus Stop 1, Mines 2, Desert 4, Skull Cavern and Ginger Island 7,
via the existing `LocationGating`) + a rarity step from the spot chance (>= 0.1: +0, >= 0.02: +1,
>= 0.005: +2, else +3), minimum over every location that can drop it. This is what makes a
Dinosaur Egg (Mountain spots at 0.005, and 0.02 in the Skull Cavern) Extreme while a Diamond (a
floor-80 node) is Hard, whatever they sell for.

**Artisan goods** (Data/Machines: `OutputRules` with `Triggers.RequiredItemId` /
`RequiredTags` and `OutputItem`, plus `MinutesUntilReady`; the machine's own recipe in
Data/CraftingRecipes with its unlock condition): effort = effort of the cheapest qualifying input
(recursively derived; a crop input uses the crop rule below, an animal product the animal rule) +
machine effort (1 for a machine unlocked by default or skill level <= 3, 2 for level 4 to 7, 3
for level 8+ or a friendship/quest unlock) + a time step (0 for under a day, 1 for up to 7 days,
2 beyond, which is what puts Casks and Aged Roe up a tier). Fish Pond outputs (Roe, Caviar, Squid
Ink) read Data/FishPondData: effort of the fish + 2 for the pond + 1 per required population
step above the first.

**Animal products** (Data/FarmAnimals: `House`, `PurchasePrice`, `DaysToMature`,
`DaysToProduce`, `ProduceItemIds`, `DeluxeProduceItemIds`,
`DeluxeProduceMinimumFriendship`; Data/Buildings for the housing chain: `BuildCost`,
`BuildMaterials`, `BuildingToUpgrade`): effort = housing effort (Coop / Barn 1, Big 2, Deluxe 3,
counting the upgrade chain) + a price step from the animal's purchase price (< 1,000g: 0, <
4,000g: 1, else 2) + 1 if the product is a deluxe produce (needs friendship) + 1 if
`DaysToProduce` > 1. Truffle = Deluxe Barn 3 + pig price 2 = 5, Egg = 1, Ostrich Egg = 3 + 2 +
1 (incubator, Ginger Island) = 6+.

**Cooked dishes** (Data/CookingRecipes: `ingredients / _ / output / unlock`): effort = max
ingredient effort (each ingredient derived by its own domain rule; category refs such as "any
milk" use the cheapest member) + unlock effort (`default` 0, TV or skill level <= 5: 1,
friendship or skill 6+: 2, Ginger Island / Qi: 3) + 1 for the kitchen (house upgrade 1, 10,000g;
dropped when `keep_kitchen` is owned). Dishes with an unobtainable ingredient are Extreme.

**Crops and forage** (already in the pools with seasons): effort = 1 + growth-days step
(Data/Crops: <= 6 days 0, <= 12 days 1, else 2) + 1 for a trellis or regrowing crop that needs
a season to pay; forage = 1 + 1 if only in one location + 1 if Secret Woods / Desert / Island.
Used only for rule E weighting of the room themes; their season floors are unchanged.

Every rule yields `null` for an id it does not recognise, and the composer tries the next domain;
an id no rule claims keeps the price bucket (existing behaviour) and is logged once at Trace so
the gap shows in `tly_dumpeffort`.

### Hub and HUD

`WeeklyHubMenu` and the JP HUD read theme names through `ThemeDisplay`; no layout change. The
card's bonus-item preview uses the same sampler, so rules A/B/E show on the card.

### Persistence

`RunState`: `DoubleProduceToday` set (cleared at day start); `BonusSlot.Paid` flag. Enum values
appended; older saves load unchanged, mid-week saves keep their sampled goals.

### Debug

`tly_themepool [theme]` prints each theme's askable count for the current week (rule C's number)
and, with a theme, the tier-1 / tier-2 lines with their effort tier and weight.
`tly_itemmodel` gains the effort tier and its source (derived / price / override).
`tly_dumpeffort` writes `docs/item-effort-model.md`: every pool item by theme, effort, tier and
basis, for review; regenerated on every release like the engine catalogue.

## Testing

Core (xunit):
- `ThemeModifiers.For` covers all eight; each new liability is distinct and lands on a different
  activity than its bonus (table test).
- Classifier maps Quartz, Ruby, Bat Wing, Ancient Doll, Wine, Pizza, Egg, Milk, Wool to the
  expected themes; Parsnip matches none of the three; Mixed matches everything.
- Rule A/B: 2 due lines + 6 filler lines in one bundle, cap 4: Spring samples the 2 due only;
  Summer 2 due + 1 filler; Winter 2 due + 1 filler (one-per-bundle); with filler spread over 3
  bundles, Fall takes 2 due + 2 filler.
- Rule C: askable {Spelunking 0, Artisan 5, Kitchen 1, Farming 3} never offers Spelunking or
  Kitchen; all activity pools 0 reproduces today's room-theme offer; weighting with a fixed seed.
- Rule D: a 3-goal Summer week pays 45 total in three 15s; completing the same slot twice pays once.
- Rule E: Spring never samples an Extreme id; Winter with one Easy and one Extreme id samples the
  Extreme first at seed 1 (weight 8 vs 1).
- Effort derivation, one test per domain with a hand-built data snapshot: a gem node at area 80
  scores 5; a geode drop at 1/32 from a Frozen Geode scores 3 + 2; Bat Wing from a floor-30 bat at
  0.9 scores 1; Dinosaur Egg from a Mountain spot at 0.005 scores 1 + 2 and from Skull Cavern at
  0.02 scores 7 + 1, minimum 3, and lands in the top quartile of the Spelunking pool while Diamond
  does not; Wine = grape effort + 1 + 2 (7 days); Truffle = 3 + 2; a dish whose ingredient is
  unrecognised is Extreme; an id no rule claims keeps its price bucket and is reported.
- Quartile tiering: a pool of 8 efforts {1,1,2,2,3,3,5,7} tiers as E,E,M,M,H,H,X,X.
- i18n guard for the new keys.

Live (throwaway save): `tly_themepool` on a fresh Spring board shows only room themes askable;
`tly_setday` to Fall, Spelunking askable with filler; pick Kitchen with animals, `debug sleep`,
confirm a doubled product; pick Artisan, place a keg, ready time 75%; pick Spelunking, kill slimes,
double drops at roughly 1 in 10; take damage under Kitchen, 1.25x in the log; complete one goal
of three and see a third of the bonus paid.

## Docs

README and Nexus description: the "Weekly themes" feature line names the eight themes and says
in one sentence that goals follow the season gate and weekly bonuses are paid per goal; "What's
New" at release; CHANGELOG `## Unreleased` (call out the bonus-per-goal change, players will
notice). Engine catalogue doc gains a "theme pools" table with the simulated line counts and the
effort override table.

## Phasing

1. **Availability model Phase 2** (effort rules for gems/minerals, geodes, monster drops,
   artifacts, artisan goods, fish pond, animal products, cooked dishes, crops/forage) with
   `tly_dumpeffort`. Ships alone first; Jeff reviews `docs/item-effort-model.md`.
2. **Theme economy** (rules A to E) on the existing five themes, with the classifier and the
   per-goal bonus. Smoked on the throwaway save.
3. **The three activity themes** and their five effects.

Each phase is its own plan and its own set of commits; 2 and 3 do not start until the document
from 1 has been looked at.

## Out of scope

Data-driven themes (JSON), more than two cards, changing the room themes' bonuses or liabilities,
Impossible mode, changing per-item JP values.
