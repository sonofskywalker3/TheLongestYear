# Activity themes: Spelunking, Artisan, Kitchen, and the theme-week economy

**Date:** 2026-08-27 (economics revision 2026-08-28 early)
**Status:** approved in brainstorm (Jeff), effort lists awaiting his review, not yet planned
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
   price: a Dinosaur Egg sells for less than a Diamond and is far harder to get. Effort tiers are
   hand-checked (section "Effort tiers", awaiting Jeff's review).

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

### Effort tiers (NOT sell price; awaiting Jeff's review)

Source of truth, lowest to highest precedence: (1) the 0.16.0 `ItemAvailability.Effort` where
the domain is modelled (fish, metals), mapped to tiers by quartile; (2) the price rarity bucket as
a fallback for unmodelled domains; (3) a **curated override table** in `GameplayConfig`
(`ItemEffortOverrides`, id -> tier), shipped with the entries below and user-editable. The
override table is the part Jeff asked to be checked by hand; the proposed entries are the
ones where price and effort disagree. Everything not listed keeps its fallback tier.

**Gems and minerals (Spelunking).** Quartz, Earth Crystal, Amethyst, Topaz: Easy. Frozen Tear,
Aquamarine, Jade, minerals from Frozen Geodes (floors 40+): Medium. Emerald, Ruby, Fire Quartz,
Diamond, minerals from Magma Geodes (floors 80+): Hard. Prismatic Shard, Omni Geode-only minerals
(e.g. Star Shards, Helvite, Kyanite, Soapstone), Iridium-tier minerals: Extreme.

**Monster drops (Spelunking).** Slime, Bug Meat, Bat Wing: Easy. Solar Essence (floors 80+, or
Skull Cavern): Hard. Void Essence (100+ / Shadow Brutes), Squid Ink (Fish Pond or Skull Cavern):
Extreme.

**Artifacts (Spelunking).** Chipped Amphora, Arrowhead, Ancient Doll, Rusty Spoon/Spur/Cog, Chewing
Stick, Ornamental Fan, Glass Shards: Easy (common artifact spots and the mines). Ancient Sword,
Ancient Drum, Bone Flute, Prehistoric Tool, Dried Starfish, Anchor, Elvish Jewelry: Medium.
Dwarvish Helm, Dwarf Gadget, Rare Disc, Golden Mask, Golden Relic, Strange Doll, the prehistoric
bones (Scapula, Tibia, Skull, Rib, Vertebra, Skeletal Hand/Tail, Nautilus/Amphibian/Palm Fossil,
Trilobite): Hard (Skull Cavern, desert, bone nodes). **Dinosaur Egg** and **Ancient Seed**
(the artifact): Extreme regardless of price.

**Artisan goods (Artisan).** Honey, Mayonnaise, Jelly, Pickles, Juice: Easy (one cheap machine).
Wine, Beer, Pale Ale, Mead, Cheese, Oil, Coffee, Green Tea, Duck Mayonnaise: Medium. Cloth
(Loom + sheep/rabbit), Goat Cheese (Big Barn + goat), Aged Roe (Cask + cellar): Hard. Truffle
Oil (pig + Oil Maker), Caviar (Fish Pond + Sturgeon), Dinosaur Mayonnaise, Void Mayonnaise:
Extreme.

**Animal products (Kitchen).** Egg, Milk: Easy. Large Egg, Large Milk, Brown Egg, Wool, Duck Egg,
Duck Feather: Medium. Goat Milk, Large Goat Milk, Rabbit's Foot, Void Egg: Hard. Truffle, Ostrich
Egg, Golden Egg: Extreme.

**Cooked dishes (Kitchen).** Tier by the hardest ingredient plus one step for needing the kitchen:
dishes whose ingredients are all Easy and whose recipe is starter or TV-week-1 (Fried Egg,
Omelet, Salad, Bread, Fried Mushroom, Pancakes): Medium; the rest: Hard; dishes needing an Extreme
ingredient or a 10-heart / Ginger Island recipe (Lucky Lunch, Seafoam Pudding, Tropical Curry,
Magic Rock Candy is not craftable): Extreme.

The room domains (crops, forage, fish, metals) keep their existing rarity/effort behaviour except
that the week table above applies to their goal sampling too, using the fish/metals effort model
and price fallback for crops and forage.

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
- Effort overrides: Dinosaur Egg resolves Extreme although its price bucket is Rare; an id absent
  from every layer falls back to its price bucket.
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

## Out of scope

Data-driven themes (JSON), more than two cards, changing the room themes' bonuses or liabilities,
Impossible mode, changing per-item JP values.
