# The obtainable board: hard weeks, pacing weeks, stretch gates, full pools

Date: 2026-08-28. Status: DRAFT from Jeff's rulings in the even-year review session (Claude, with
a Codex second opinion). Open questions are marked **OPEN** and listed at the end; nothing here is
built. Supersedes the foothold and rule E sections of `2026-08-28-even-year-availability-design.md`
and the pin layer in `GameplayConfig.DefaultItemSeasonPins`. Follows the review
`docs/superpowers/handoffs/2026-08-28-even-year-review.md` and its findings.

Game data cited below was read from the running game with Content Patcher's `patch export`
(`Data/Shops`, `Data/Objects`, `Data/Locations`, `Data/CraftingRecipes`, `Data/CookingRecipes`,
`Data/Machines`, `Data/WildTrees`, `Data/TV/CookingChannel`, `Data/mail`, `Data/SpecialOrders`)
and from the Android decompile where the rule lives in code.

## The principle

**Per-item obtainability is guaranteed; total load is the difficulty.** Every item a gate
demands must be gettable on its own by that gate, by a route the mod can point at (a game route
or a JP buy). The gate may still demand more than a player can manage in the time. Failing
because you could not do everything is fair; failing because one item had no route is not.

Two other rules stand from before: the availability floor may only stop an item showing up
before it is reasonably accessible, nothing forces an item early; and weeks and seasons should
ask for similar amounts all year.

## 1. Two weeks per item

Every item carries:

- **HardWeek**: the first week of the year (1 to 16) the item can exist at all, from facts:
  crop and forage seasons, fish seasons and locations, festival dates, recipe air dates, Jeff's
  location rulings (Desert and Skull Cavern hard week 6: a Spring bus is possible but is days
  of fishing and selling, so it is not asked for), second-year rows.
- **Week** (pacing): the week a normal player reasonably has it. Every number in
  `AvailabilityWeeks` is a pacing number except the festival dates, the Desert on Normal, the
  Swamp and the second-year rows, which are hard.

Goals and gates on Easy and Normal use Week. Hard and Extreme gates use HardWeek. Hard cards stay
on Week; Extreme cards use HardWeek. `tly_dumpavailability` shows both columns.

The unknown default (13, Winter) stays for the gate. An unplaced item is never a weekly goal
until Jeff rules on it (unknown means not on a card). After this spec the vanilla pools have no
unknowns: Golden Pumpkin 12 (Spirit's Eve maze), Dried Mushrooms (see 6), Pickles 4.

## 2. The stretch rule (replaces the Spring foothold)

Not on Easy. For each season s and each rolled bundle:

1. If the bundle gains at least one item reachable by pacing Week in season s (the ramp would
   demand something new), nothing happens.
2. Else, if the bundle holds a **stretch item** for s (HardWeek inside s, Week at most two weeks
   past the end of s: Spring reaches weeks 5 and 6, Summer 9 and 10, Fall 13 and 14), the gate
   for s counts that item as reachable and demands 1; the line is tagged **stretch** on the
   board, in `tly_gatecheck` and on the hub.
3. Else, swap one slot for a stretch item from the bundle's pool. A hard roll stays hard; a true
   in-season item is never swapped in.

Stretch items may be weekly goals from week 4 of the season they stretch into, tagged stretch.
Season-named bundles are exempt (they gate in their own season).

Stretch examples for Spring under the tables below: Lava Eel and Cave Jelly (floor 100), gold and
area-80 gems, Duck Egg and Goat Milk, Cheese, Large Egg and Large Milk, Hardwood, Sewer items,
a fish-pond good from a week-1 fish, a dish whose ingredients are Spring. Never: Summer crops and
forage, Summer-only fish, Desert items on Normal, fruit-tree fruit, deluxe-building produce,
second-year recipes.

## 3. Full pools, no fixed lists (TLY Custom)

Every TLY Custom bundle keeps its name, room, pick count and quality or stack asks. Its authored
or vanilla items remain candidates. Slots roll from the full pool of the bundle's kind. No bundle
carries a fixed list; a bundle whose vanilla items span kinds gets a theme, or is replaced by a
new themed bundle (**OPEN**: the list to name is The Missing, Winter Star, Wild Medicine, Dye,
Field Research, Fodder, Construction, Chef's, Treasure Hunter's; items that fit no theme, Rice,
Sugar, bombs, totems, are left out or get a bundle of their own).

Pools, one per `ItemKind`, built from Data/Objects through the existing vet: Gem, Mineral (geodes
included), MonsterLoot (the whole drop table, not five items), Artifact, ArtisanGood, Cooking,
Egg, Milk, AnimalProduct, Resource (wood, stone, hardwood, fiber, sap, clay, coal, hay), Sapling
and Seed, Book (see 7), Trophy (Gil's seven year-1 rewards), plus the existing crop, forage, fish,
crab-pot, metals pools. Weights: vanilla 3, modded 1. **Vanilla items with string ids (Goby, the
jellies, Broccoli, Powdermelon, Moss, Mystery Box, Smoked Fish, Prize Ticket, Book_*) are vanilla
and weigh 3**; the builder gets a known-vanilla set because Data/Objects cannot tell them apart.

Additions at weight 1 (the jelly rate):

- **Stonefish, Ice Pip, Lava Eel**: hard-coded in `MineShaft.getFish` by area (floors 1 to 39,
  40 to 79, 80 to 119), not in Data/Locations, and flagged `ExcludeFromRandomSale`, so they need a
  pool addition with a vet exception like the Night Market fish. Weeks 1, 2, 4.
- **Legendaries**: Legend (Mountain lake, Spring, rain, Fishing 10), Crimsonfish (Beach, Summer,
  Fishing 5), Angler (Town, Fall, Fishing 3), Glacierfish (Forest, Winter, Fishing 6), Mutant
  Carp (Sewer). Remove the `fish_legendary` vet. Pacing weeks 4, 5, 9, 13, 7. **Rewind must
  clear them**: the game blocks a repeat catch through `CatchLimit` against
  `player.fishCaught` (`GameLocation.cs:13831`) and `FarmerReset` never touches `fishCaught`;
  the reset removes the catch-limited ids and leaves the rest of the collection alone.
- **Year-2 crops** (Garlic, Red Cabbage, Artichoke): `YearTwoCrops.ExcludedFor` stops excluding
  them on Normal and above (Easy keeps the exclusion). Route: the Year-Two Seeds Boost (section 8)
  or the permanent buys. Hard weeks Garlic 2, Red Cabbage 6, Artichoke 10; pacing 3, 7, 11. Cards
  may name them from the pacing week, tagged "Boost: Year-Two Seeds".

Every rolled bundle of 4 or more slots holds at least one **hard item** (effort 6 or more) on
Normal and above; `tly_gatecheck` reports bundles without one. Bundles with no hard option in
their pool are reconsidered.

## 4. Rule E on absolute bands

Tiers come from the effort number itself, not from quartiles of the pool: Easy 0 to 2, Medium
3 to 5, Hard 6 to 8, Extreme 9 and up. The season weight table is unchanged; Spring's Extreme 0
now means "a Prismatic Shard is never a Spring goal", not "the hardest of two items is never a
Spring goal". Cave Jelly, Sea Jelly and River Jelly get an effort row (they have no Data/Fish
row the parser reads and fall back to 6).

## 5. Goals follow the gate, no look-ahead

`SeasonNeed` asks only for what the current gate demands (the half-season look-ahead is
removed; its round-half-up let a one-line Winter increment be donated in Fall). A player who
donates a season's share early sees quiet cards for the rest of that season by design; filler
still fills. Goal budget unchanged; the ceilings become flat 5/5/5/5 (Jeff: similar all year).

`SelectionService` pads a short offer only with themes that can ask for at least one goal; a
single card is allowed.

## 6. Placement rules and tables (Normal pacing; hard in brackets where different)

Mines: 30 floors a week. Floors 1 to 30 week 1, 31 to 60 week 2, 61 to 90 week 3, 91 to 120
week 4; every mine item is Spring-gated (the area-80 Summer gate is gone). Skull Cavern and Desert
week 9 [hard 6]. Sewer and Bug Land 7 (60 donations). Swamp: post-win, excluded. Monster drops
under 5 percent: effort only on Normal; Hard and Extreme place them at the monster's week.
Volcano and Dangerous Mines monsters are removed from the spawn table (items only they drop are
excluded). Bone Fragment is area 40 (skeletons).

Machines: by skill level 0 to 2 week 2, 3 week 3, 4 to 5 week 4, 6 to 7 week 6, 8 to 9 week 7;
plus run time in whole days. Non-skill unlocks map from the recipe's own requirement:

| Hearts | Week | Cost | Week |
|---:|---:|---:|---:|
| 2 | 2 | up to 1,000g | 1 |
| 3 | 3 | up to 3,000g | 2 |
| 4 | 4 | up to 5,000g | 3 |
| 5 | 5 | up to 10,000g | 5 |
| 6 | 6 | up to 25,000g | 7 |
| 7 | 8 | up to 50,000g | 10 |
| 8 | 9 | more | 13 |
| 10 | 12 | | |

A villager you cannot meet before a week (Sandy, 9) adds that week first. Specific machines from
the data: Tapper is **Foraging 4** (1.6), so Maple Syrup week 5 (9 nights), Oak Resin 5 (7),
Pine Tar 4 (5); the tapper rule reads `Data/WildTrees` TapItems, not the Wood Chipper. Dehydrator:
Pierre sells the recipe for 5,000g, and choosing mushrooms for the cave places one in it
(`FarmCave.cs:273`, Demetrius at 25,000g earned): week 6; the id map fix is
`FLAVORED_ITEM DriedMushroom` (singular in the game data). Fish Smoker: Willy, 10,000g, week 5.
Cask: house upgrade 3, post-year for pacing. Garden Pot: Evelyn's mail after the greenhouse.
Geode Crusher, Bone Mill, Solar Panel, Farm Computer, Mini-Obelisk: special-order mail, week 9
unless Jeff says otherwise.

Animals: coop or barn week 2, big 5, deluxe 9; deluxe produce keeps the building's week.
Kitchen 6. Fish pond: fish week plus 4. Crab pot 2. Books: section 7.

Crops: `start + growthDays / 7` (a 7-day crop planted day 1 harvests day 8). Seeds must have a
source in the season: Coffee Bean 5 (Dust Sprite seed then 10 days), Strawberry 3, Sweet Gem Berry
12, Beet 10, Cactus Fruit 9 (Desert forage). Rhubarb and Starfruit: Oasis seeds, grow only in
their own season outdoors; the year-1 route is greenhouse (Pantry reward) or garden pots
(recipe arrives after the greenhouse) plus 13 days: 13 for pacing, hard 11; 12 if the Garden
Pot buy in section 8 is added (**OPEN**). Fruit trees: Orange and
Peach 5, Apple and Pomegranate 9, Apricot and Cherry 13, Banana and Mango excluded (island).

Forage: first spawn week plus location; Secret Woods marker week 4 (Morel, Fiddlehead, Woodskip,
hardwood stumps). Clam 1 (beach forage 0.9 spring to fall), Cockle, Mussel, Oyster 1. Winter Root
and Snow Yam 13. Salmonberry 3, Blackberry 9. Crystal Fruit 13 [hard 2].

Dishes: the week is the later of the kitchen, the ingredients and the **recipe**: `f NPC N` by
the hearts table (Sandy from 9), `s Skill N` by the level, `l 100` the Queen of Sauce air week
(year-1 episodes 1 to 16 by week; year-2 episodes 17 to 32 only through the Boost in section 8),
`default` and Saloon purchases week 1. Cookies: Evelyn's event (`PLAYER_HAS_SEEN_EVENT 19` at the
Saloon), 5. Ice Cream: the Summer stand, 5.

Shops and rewards: Pierre's staples and the Saloon menu 1. Prize Ticket 2 [hard 1]: every 3rd
Help Wanted quest (`Quest.cs:549`). Mystery Box 3 [hard 2]: the Qi plane fires the night after the
6th Help Wanted quest or day 50, whichever first (`Utility.cs:4433`). Guild rewards as tabled,
Gil's seven year-1 trophies only. Oil of Garlic: the Dwarf sells it, 3,000g, week 2 by cost
(needs the Dwarvish Translation Guide), or the Boost.

Pins: `DefaultItemSeasonPins` is deleted except Woodskip, Sea Urchin and Red Mushroom (Spring
by Jeff's ruling) until the Woods marker and the bridge rule replace them. A pin or
`AvailabilityWeekOverrides` entry can never move an item earlier than a rule (the check covers
Phase 2 weeks, not only fish, crab pot and metals). Ghostfish is week 1 (floor 20).

## 7. Books, from Data/Shops

The Bookseller visits twice a season on dates from a fixed list (Spring 11, 12, 21, 22 or 25 for
the first visit). Year-1 stock: Price Catalogue 3,000 (always), one random skill book 5,000
(always), a second 8,000 (80 percent), a third 10,000 (60 percent), Woodcutter's Weekly 8,000
(33 percent), Book of Stars 15,000 (25 percent), Way of the Wind pt. 1 15,000, pt. 2 35,000,
Horse and Grass books 25,000. The eleven story books are **YEAR 3** at the Bookseller.

Story-book routes in year 1 (code): The Alleyway Buffet, free gift box in Town; Mapping Cave
Systems, free gift box in the Adventurer's Guild; Dwarvish Safety Manual, the Dwarf 4,000g; Ways
of the Wild, the raccoon's second trade; Woody's Secret, tree chopping after 20 trees at 0.03
percent rising; Jack Be Nimble, artifact spots after 2 at 0.8 percent rising; Jewels of the Sea,
fishing treasure at Fishing 5 and 2 percent rising; Monster Compendium, kills after 10 at 0.01
percent rising; The Art O' Crabbing, a mine chest; Friendship 101, the prize ticket machine (8
tickets); Book of Mysteries, the first mystery-box book; Animal Catalogue, Marnie **YEAR 2**;
The Diamond Hunter, Volcano only; Queen of Sauce Cookbook, 100 walnuts; Book_Artifact, no year-1
source found. Skill books also drop from rock golems, fishing treasure, mystery boxes and the
prize machine.

Book bundle pool (ruled): Price Catalogue (week 2), the five skill books (3),
Woodcutter's Weekly (3), Way of the Wind pt. 1 (5), Book of Stars (5), The Alleyway Buffet (1),
Mapping Cave Systems (1), Dwarvish Safety Manual (3), Friendship 101 (5). The random-drop and
year-2 or later books are excluded.

## 8. JP Boosts this spec adds (roster per TODO "shrine tabs + JP Boosts")

- **Year-Two Seeds**, about 75 JP, the current week: Mixed Seeds planted this week roll the
  season's year-2 crop at **5 percent** (Garlic Spring, Red Cabbage Summer, Artichoke Fall).
  Pairs with the Farming theme's growth bonus to make the gate.
- **Garden Pots** (**OPEN**): the Garden Pot recipe is granted only by Evelyn's event after the
  greenhouse is restored (`Event.cs`, event 900553), so pots are never a route before the
  greenhouse. A JP buy that grants the recipe (10 Stone, 1 Clay, 1 Refined Quartz per pot) opens
  an indoor route for Rhubarb and Starfruit from the Oasis week: week 9 seeds plus 13 days is
  week 11.
- **Sneak Peek**, 100 JP, the current season: the Sunday
  episode is the year-2 episode for that week (`TV.getWeeklyRecipe`, `DaysPlayed % 224 / 7`),
  so every year-2 dish has a year-1 route at its natural week.

## 9. Diagnostics and sims

`tly_dumpavailability` gains HardWeek and a `judgement` Placed kind so every table row keeps
appearing in the post-sim list. `tly_gatecheck` reports stretch lines, bundles without a hard
item, and Spring-tight bundles. `tools/sim-year.sh` donates the gate per week (a quarter of the
season's share each week) instead of the day-15 front-load, runs gate-only and goals on the same
seed, and reports all 16 weeks.

## Rulings on the mixed-kind bundles

Treasure Hunter's: Gem pool. Construction: Resource pool. Fodder: hay, grain crops, fruit. Dye:
one item per colour from the game's `color_*` context tags. Field Research: one forage, one shell
or artifact, one fish, one geode or mineral. Wild Medicine: edible forage and mushrooms. Chef's:
half dishes from the Cooking pool, half ingredients drawn from those recipes (Rice, Sugar, Oil,
Vinegar and Wheat Flour are Chef's candidates, not orphans). Winter Star: any kind, Winter-only.
The Missing: the Extreme band, any kind. Children's: sweets, berries, dolls. Enchanter's: its
vanilla items plus every totem and the essences. Fish Farmer's: pond goods. Animal, Artisan,
Adventurer's, Forager's: their kind's pool. Legendaries drawn into a 4-of-4 fish bundle are
mandatory for it (Jeff: a hard roll is a challenge, not a shaft). The hearts and cost tables in
section 6 are adopted as written. Remaining orphans (bombs, bait, fertilizer) stay out.

## Open questions for Jeff

1. Garden Pot route (section 8): add it, and if so as a permanent keep or a Boost, and its price;
   Rhubarb and Starfruit then sit at 12 instead of 13.

## Out of scope

Single-loop difficulty target; JP awards; the stash; hub UI beyond the stretch and Boost tags.
