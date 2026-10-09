# Animal powers: twelve permanent upgrades for barn, coop, horse and pet (design, 2026-10-09)

**Date:** 2026-10-09
**Status:** APPROVED by Jeff 2026-10-09 with the rulings below. Where a ruling and the body disagree, the
ruling wins.
**Scope:** PC 1.6 only (Android is not supported until PC runs well). Decompile references are to
`decompiled-pc/Stardew Valley`. Game data values were read from the live game's `Data/FarmAnimals`,
`Data/Machines` (Content Patcher `patch export`) and `Data/Pets` (scratch .NET dump of `Content/Data/Pets.xnb`).
**Dropped by Jeff, not in this spec:** half hay, large products more often.

## Rulings (Jeff, 2026-10-09)

1. **Busy Barnyard: no bundle or difficulty compensation of any kind.** TLY's contract is "earn JP, power up,
   make it further next time"; a power that makes things easier must never make bundles harder. It is a
   permanent power, and the effort model stays untouched (Open question 1 closed).
2. **Busy Barnyard is split into two rows** (Open question 3): a coop row (`animal_fast_produce_coop`: ducks
   2 to 1 day, rabbits 4 to 2) and a barn row (`animal_fast_produce_barn`: goats 2 to 1, sheep 3 to 1), 600 JP
   each, no gate. The tooltip only says they produce faster.
3. **Warm Welcome I / II / III as specced** (1 / 2 / 3 hearts, 200 / 450 / 800 JP). Large eggs and milk
   starting early is accepted (Open question 4, option a).
4. **The existing animal keeps move into the new Animals tab** (Open question 2): Keep Horse, Keep Pet, the
   "Start with a" rows and the Herd Book. Prices and save ids are unchanged, so owned rows stay owned. Animal
   buildings were left to judgement; the call made at build time: the Coop and Barn chains and Keep Silo move
   too (they only exist to house and feed animals, and every "Start with a" row chains from a Coop or Barn
   keep, so the chains stay readable in one tab); Keep Fish Pond stays in Buildings (fish, not farm animals).
5. **Rates confirmed** (Open question 5): Fine Feathers +25%, Truffle Nose 25%, Swift Horse +1.0, Loyal Pet 40%
   from 3 hearts.
6. **Defaults confirmed:** Morning Rounds has no gate (Open question 6); Swift Horse and Horse Flute require
   Keep Horse (Open question 8); the names are the spec's (placeholders Jeff may rename later, all in i18n,
   Open question 7).

## Why

The animal side of TLY has keeps (Keep Coop/Barn chains, "Start with a", the Herd Book, Keep Horse, Keep Pet)
but no power that changes how animals behave. Animals are slow to pay off inside a 112-day loop: babies take
3 to 10 days to grow, slow producers give every 2 to 4 days, quality needs hearts the loop rarely has time to
build. These twelve powers make animals worth their building in a single loop.

## How the existing powers work (what this spec builds on)

- **Permanent upgrades** are `UpgradeDefinition` rows in `UpgradeCatalog.Build()` (Core): id, `UpgradeCategory`,
  JP cost, optional `PrerequisiteId` (chains like `shop_discount_1..5`, or a single prerequisite like
  `start_chicken` needing `keep_coop`), optional `MetaRequirement` (`species:Rabbit`, read from
  `MetaState.AnimalSpeciesEverOwned`) and optional `RunReachRequirement` (`building:Stable`, `pet:1`, evaluated
  live by `RunReachEvaluator`). They are bought with JP in **the upgrade menu** (`JunimoShrineMenu`) that opens
  at a rewind, one vertical tab per `UpgradeCategory`. Effects read `meta.HasUpgrade(id)` and apply every loop.
  Text is `upgrade.<id>.name` / `upgrade.<id>.desc` in `i18n/default.json`.
- **Statue boosts** (spec 2026-08-29) are `BoostCatalog` rows bought on the Junimo Statue's Boosts tab, last a
  day, week, season or loop, and stack with the weekly theme through `ActiveEffectsProvider.BonusStacks`.
- **Price anchors used below:** Keep Pet 50, Keep Lost Books 100, keep a Convenience power book 150 (Horse: The
  Book is one, +0.5 riding speed), Green Thumb I 50 to V 650, Shop Discount I 75 to V 900, keep a Yield book 350,
  Start with a Chicken/Cow 400, Keep Horse 450, Keep Farm Decor 500, Keep Coop/Barn 600, Herd Book slots 600 to
  2,250, Keep Worn Gear 1,000, Keep Deluxe Coop/Barn 2,000.
- **Existing animal effects these must stack with:** `AnimalDoubleProductPatch` (Kitchen theme bonus and the
  Double Yolk boost: 20% chance of a second product, prefix/postfix on `FarmAnimal.dayUpdate`, postfixes on
  `MilkPail.DoFunction`, `Shears.DoFunction`, `FarmAnimal.DigUpProduce`); `WildcardAnimalPatch` (snow day keeps
  animals in and applies the Winter evening mood rule, by swapping `IsWinterHere()` for `WinterOrSnowDay`);
  `MachineSpeedPatch` (Artisan theme / Full Steam boost: `Object.OutputMachine` postfix scales
  `MinutesUntilReady` by 0.75 per stack, Spelunking liability 1.25, which already touches incubators);
  Quick Feet boost (+1 Speed buff, which also counts while riding); `HerdBookService` (restores animals with
  their hearts); `PetCarryoverService` (Keep Pet).

## Ruling proposed: all twelve are permanent upgrades, no statue boosts

Every power is something a player builds a farm around (an animal type, a horse, a pet), so a one-week or
one-season buy would expire before the animals pay back. All twelve go in the upgrade menu. The one row where a
statue boost is a defensible alternative is **Busy Barnyard** (faster production), because of its bundle risk;
see Open question 1.

## Proposed category: a new "Animals" tab

The rows would scatter across Efficiency, Carryover and Buildings today. A ninth tab, **Animals**
(`UpgradeCategory.Animals`, `upgrade-category.animals`), holds all twelve. The tab strip fits: 9 tabs x 64 px
(56 + 8 spacing) from y + 80 is 656 px inside the 700 px panel (668 usable). Whether Keep Horse, Keep Pet, the
"Start with a" rows and the Herd Book move into it too is Open question 2 (moving keeps ids, so owned rows stay
owned).

## Shared implementation rules

1. **Never edit `Data/FarmAnimals`.** The bundle effort model (`GameEffortData` into
   `AnimalProductAvailability`) and the Herd Book (`HerdBookRules.AdultAge`) read it. Editing `DaysToProduce`,
   `ProduceItemIds` or `DaysToMature` would silently re-rate bundles and change Herd Book ages. All animal effects
   are Harmony patches that read `meta.HasUpgrade`. (`Data/Pets` is read by nothing in TLY; an edit is allowed
   there, see power 12.)
2. Host-side only (`Context.IsMainPlayer`); farmhands get the effects through net fields. No multiplayer design
   beyond that (standing rule).
3. Each patch is its own class so one failing never drops another, and each degrades to "power does nothing,
   one Info line" when another mod has rewritten its target (the 0.19.21 ExtraAnimalConfig lesson).
4. Seeded randomness for any new roll: `Utility.CreateRandom(myID, Game1.stats.DaysPlayed, <salt>)`, so a reload
   does not reroll.
5. Every new player-facing string goes in i18n; tooltips below are placeholders in the house style.

## The twelve powers

Names for 1 to 4 are proposals (Jeff named 5 to 12).

### 1. Lucky Rabbits: rabbits only drop Rabbit's Feet

- **Vanilla:** Rabbit (`Data/FarmAnimals`): `DaysToProduce` 4, regular produce 440 Wool, deluxe produce 446
  Rabbit's Foot (`DeluxeProduceMinimumFriendship` 0, `DeluxeProduceCareDivisor` 5000, luck multiplier 1.02).
  In `FarmAnimal.dayUpdate` (FarmAnimal.cs:1005-1022), a laying rabbit first gets `GetProduceID(r)` (Wool), then
  if `r < happiness/150` a deluxe roll at `(friendship + happinessModifier)/5000 + luck x 1.02`. A content rabbit
  (happiness 255, modifier 382.5) rolls a foot 7.6% of the time at 0 friendship and 27.6% at 1000.
- **Effect:** every product a rabbit makes is a Rabbit's Foot. Quality rules unchanged.
- **Hook:** postfix on `FarmAnimal.GetProduceID(Random r, bool deluxe)`: when `deluxe == false`, the animal's type
  is `Rabbit` and the result is in its `ProduceItemIds`, return the deluxe pick (`GetProduceID(r, true)`, which
  keeps data conditions) if it is non-null. Covers `dayUpdate` and `growFully`.
- **Edge cases:** the stat `RabbitWoolProduced` still counts (cosmetic). Wool stays available from sheep. A
  modded rabbit variant with another type id is untouched. Double Yolk / Kitchen copies the dropped foot (already
  generic).
- **Price:** 450 JP. **Gate:** `MetaRequirement species:Rabbit`.
- **Tooltip:** "Rabbits always leave a Rabbit's Foot instead of Wool."

### 2. Fine Feathers: ducks drop more Duck Feathers

- **Vanilla:** Duck: `DaysToProduce` 2, regular 442 Duck Egg, deluxe 444 Duck Feather,
  `DeluxeProduceMinimumFriendship` 200, divisor 4750, luck x 1.01. A content duck rolls a feather 0% below
  200 friendship, 12.3% at 200, 16.5% at 400, 29.1% at 1000.
- **Proposed rate:** an extra, independent **25%** chance that a duck's egg is a Duck Feather instead, with no
  friendship gate. Combined for a content duck: 25% at 0 to 199 friendship, 34% at 200, 37% at 400, 47% at 1000.
  Eggs stay the majority, so Duck Egg and Duck Mayonnaise bundles are not starved.
- **Hook:** the same `GetProduceID` postfix: when `deluxe == false`, type `Duck`, result 442, and a seeded roll
  under 0.25, return 444. Vanilla's own deluxe roll still runs after it.
- **Price:** 250 JP. **Gate:** `MetaRequirement species:Duck`.
- **Tooltip:** "Ducks have an extra 25% chance to leave a Duck Feather instead of an egg."

### 3. Busy Barnyard: ducks, goats, sheep and rabbits produce faster

- **Vanilla:** `produceToday` (FarmAnimal.cs:1005) needs `daysSinceLastLay >= DaysToProduce - produceSpeedBonus`.
  `produceSpeedBonus` is +1 when `friendship >= FriendshipForFasterProduce` and +1 when the owner has
  `ProfessionForFasterProduce`. In 1.6 data only Sheep have either (Shepherd, profession 3, and 900 friendship),
  so a sheep goes 3 to 2 to 1 days. Coopmaster does not speed any coop animal in 1.6 (all coop rows have -1).
  `daysSinceLastLay` resets to 0 only inside the `r < happiness/150` branch, so an unhappy animal already
  produces on consecutive days in vanilla (quirk, left alone).
- **Effect (Jeff's numbers):** Duck 2 to 1 day, Goat 2 to 1, Sheep 3 to 1, Rabbit 4 to 2. The target replaces
  `DaysToProduce`; vanilla bonuses still subtract, which only matters for Rabbit (none apply) and Sheep
  (already 1, so Shepherd's speed half and the 900-friendship bonus become redundant for sheep; Shepherd still
  gives its quality and happiness boosts). Cows, chickens and pigs already produce daily. Dinosaur (7) and
  Ostrich (7) are not in Jeff's list and stay vanilla.
- **Hook:** prefix and postfix on `FarmAnimal.dayUpdate` (alongside `AnimalDoubleProductPatch`, not a transpiler):
  prefix adds `offset = DaysToProduce - target` to `daysSinceLastLay`; postfix subtracts it again unless the animal
  produced (`daysSinceLastLay == 0`). This also undoes itself on the early-return path (animal brought in at
  night). Fullness and happiness rolls are untouched, so an unfed animal still skips.
- **Edge cases:** stacks with Double Yolk / Kitchen (a second product roll on each, now more frequent days). With
  Lucky Rabbits, a rabbit gives a Rabbit's Foot every 2 days.
- **Price:** 1,200 JP (on par with Keep Big Coop/Barn: it doubles to triples four animals' output).
  **Gate:** none (four species across two buildings). Tiering: see Open question 3.
- **Tooltip (Jeff: just say faster):** "Ducks, goats, sheep and rabbits produce faster."
- **Balance flag:** see "Bundle balance" below. This is the one power that can move bundle difficulty.

### 4. Truffle Nose: a pig's dig can give 2 truffles

- **Vanilla:** a pig with `currentProduce` digs outdoors (not raining, not Winter) at 0.0002 per tick in
  `FarmAnimal.behaviors` and calls `FarmAnimal.DigUpProduce` (FarmAnimal.cs:1658): 0.2% chance of a Truffle Crab
  instead, else `Utility.spawnObjectAround` the truffle and `stats.TrufflesFound++`; then the pig keeps its
  produce for another dig with chance `friendship/1500` (66.7% at max hearts, about 3 truffles a day).
- **Proposed chance:** **25%** per successful dig to spawn a second identical truffle. Roughly +25% truffles.
- **Hook:** prefix records `Game1.stats.TrufflesFound`; postfix, if it went up (a truffle spawned, not a crab),
  rolls and spawns `produce.getOne()` beside it and increments `TrufflesFound` again. Same class family as
  `DigUpDoublePatch`, which already postfixes this method (that one restores `currentProduce` for an extra dig;
  the two stack).
- **Price:** 400 JP. **Gate:** `MetaRequirement species:Pig`.
- **Tooltip:** "Each truffle a pig digs up has a 25% chance to be two."

### 5. Swift Horse: your horse runs faster

- **Vanilla:** `Farmer.getMovementSpeed()` (Farmer.cs:7994-8005) while riding:
  `speed (5) + addedSpeed + 4.6 + (ateCarrotToday ? 0.4) + (Book_Horse ? 0.5)`, times `movementMultiplier x
  elapsed ms`. Base riding is 9.6. `addedSpeed` is `buffs.Speed` plus the two speed books (books only when not
  riding), so Coffee and the Quick Feet boost (+1) already add to the horse.
- **Proposed amount:** **+1.0** while riding (9.6 to 10.6, about 10% faster; twice Horse: The Book, the same as
  a Quick Feet buff). All sources add: carrot + book + Swift Horse + Quick Feet is 12.5.
- **Hook:** postfix on `Farmer.getMovementSpeed`: when `isRidingHorse()`, not `Game1.eventUp`, and the control
  branch was taken (`Game1.CurrentEvent == null || playerControlSequence`), add
  `1.0 x movementMultiplier x elapsed ms`, times 0.707 when moving diagonally. No buff, so nothing in the buff bar
  and walking speed is unchanged.
- **Price:** 300 JP (twice the Horse book keep's Convenience band for twice its effect).
  **Gate:** `PrerequisiteId early_horse` (Keep Horse).
- **Tooltip:** "Your horse runs 10% faster."

### 6. Horse Flute: a flute from the start of every loop

- **Vanilla:** Horse Flute `(O)911`, normally a late-game reward. Using it (`Object.performUseAction`,
  Object.cs:3470) calls `Utility.GetHorseWarpRestrictionsForFarmer`; with no owned horse it shows
  `HorseFlute_NoHorse` (Utility.cs:162). TLY's Keep Horse (`HorseCarryoverService.RestoreHorse`) rebuilds the
  stable at its saved tile on reset and sets `stable.owner` to the player before `grabHorse`, so the horse is
  owned from Spring 1 and the flute works on day 1.
- **Effect:** `FarmerReset` adds one Horse Flute to the backpack at every loop start, next to the Golden Scythe
  grant (`FarmerReset.cs:159-161`), unless one is already in the backpack (Junimo Stash, Keep Worn Gear do not
  hold it, but the check is cheap).
- **Edge cases:** Keep Horse owned but no stable built yet this save: the flute gives vanilla's "no horse" line
  until the stable is built. Selling or trashing it: no replacement until the next loop.
- **Price:** 350 JP. **Gate:** `PrerequisiteId early_horse`.
- **Tooltip:** "Start every loop with a Horse Flute."

### 7. Quick Growth: babies grow up in half the time

- **Vanilla:** `FarmAnimal.dayUpdate` (FarmAnimal.cs:984-994): if fed (`fullness > 200` or a roll), `age++`,
  and on the night `age == DaysToMature - 1` it calls `growFully` instead. Days to adult: chickens 3, Sheep 4,
  Duck, Cow, Goat 5, Rabbit 6, Ostrich 7, Pig 10, Dinosaur 0.
- **Effect:** half, rounded up: chickens 2, Sheep 2, Duck/Cow/Goat 3, Rabbit 3, Ostrich 4, Pig 5.
- **Hook:** prefix records `age`; postfix, when the animal aged this night and is still a baby, takes one more
  step: `growFully(r)` if `age == DaysToMature - 1`, else `age++`. An unfed night still does not age (vanilla's
  feeding rule stays).
- **Edge cases:** Herd Book animals come back as adults already. Animals born in the barn and hatched ones are
  covered (same method). Pairs with Fast Hatch: an incubated chicken goes egg to laying in about 5 days instead
  of 9.
- **Price:** 350 JP. **Gate:** none.
- **Tooltip:** "Baby animals grow up in half the time."

### 8. Fast Hatch: incubators hatch in half the time

- **Vanilla:** Incubator `(BC)101` and Ostrich Incubator `(BC)254` use `Object.OutputIncubator` (Object.cs:2221),
  which overrides the rule's time with the egg's `IncubationTime` or 9000 minutes (Dinosaur 18000; Ostrich's is -1
  in data, so it also gets 9000 despite the machine rule's 15000). `Data/Machines` then applies the `Coopmaster`
  `ReadyTimeModifier` (x0.5) in `Object.OutputMachine` (Object.cs:2511).
- **Effect:** x0.5 on top. Regular egg 9000 to 4500 minutes (about 3 days), with Coopmaster 2250; Dinosaur
  18000 to 9000.
- **Hook:** extend the existing `MachineSpeedPatch` postfix on `Object.OutputMachine`: when the machine's
  `MachineData.IsIncubator` is true and the power is owned, multiply `MinutesUntilReady` by 0.5 (through
  `MachineReadyTime.Scale`, floor 10). It multiplies with Coopmaster and with the Artisan theme / Full Steam
  0.75, and with Spelunking's 1.25.
- **Price:** 200 JP. **Gate:** none.
- **Tooltip:** "Eggs in an incubator hatch in half the time."

### 9. Warm Welcome: new animals arrive with hearts

- **Vanilla:** a new `FarmAnimal` starts at 0 friendship (0 to 1000, 200 per heart). Product quality
  (FarmAnimal.cs:1023-1043): `c = friendship/1000 - (1 - happiness/225)`, plus 0.33 with Coopmaster/Shepherd;
  gold at `c/2`, else silver at `c`. Deluxe products need `DeluxeProduceMinimumFriendship` 200 for every animal
  that has one except Rabbit. For a content animal (happiness 255):

  | Friendship | Gold | Silver | Large egg/milk chance (chicken, cow) |
  |---|---|---|---|
  | 0 | 6.7% | 12.4% | 0% (gated) |
  | 200 (1 heart) | 16.7% | 27.8% | 48.5% |
  | 400 (2 hearts) | 26.7% | 39.1% | 65.2% |
  | 600 (3 hearts) | 36.7% | 46.4% | 81.9% |

- **Proposed, tiered:** Warm Welcome I / II / III set a new animal's friendship to at least **200 / 400 / 600**.
  Highest owned tier wins. Floor semantics: `max(current, floor)`.
- **Hook:** prefix on `AnimalHouse.adoptAnimal(FarmAnimal)` (AnimalHouse.cs:143). Every arrival goes through it:
  Marnie (`PurchaseAnimalsMenu`), incubator hatch and barn birth (`AnimalHouse` 120-135), TLY's "Start with a"
  and Herd Book restores. Applies only when `daysOwned < 0` (a brand-new animal; moving an animal between
  buildings in `AnimalQueryMenu` also calls `adoptAnimal` but its `daysOwned` is already set). Herd Book restores
  are excluded explicitly (they come back with their own hearts by design); "Start with a" animals get it.
- **Edge cases:** friendship still decays without petting (`-(10 - friendship/200)` a night); pairs with Morning
  Rounds. Tier I already crosses the 200 deluxe gate, so large eggs and milk start on day one: see Open
  question 4 (Jeff dropped "large products more often").
- **Price:** I 200 / II 450 / III 800 JP (chain). **Gate:** none.
- **Tooltips:** I "New animals arrive with 1 heart." II "New animals arrive with 2 hearts." III "New animals
  arrive with 3 hearts."

### 10. Morning Rounds: every animal petted each morning

- **Vanilla Auto-Petter** `(BC)272`: in the overnight `Object.DayUpdate` (Object.cs:1720-1728) it calls
  `pet(Game1.player, is_auto_pet: true)` on every animal in its building. `FarmAnimal.pet` (FarmAnimal.cs:668):
  auto-pet gives +8 friendship (15 - 7) and `max(5, 30 + HappinessDrain)` happiness, no XP, no profession boost,
  and sets `wasAutoPet`, so the night's `dayUpdate` skips the no-pet penalty (-friendship, -50 happiness).
  Petting by hand afterwards still adds the other +7 and the profession boost. A second auto-pet is a no-op.
- **Effect:** the same as an Auto-Petter in every animal building and for animals outside, from day 1 of every
  loop.
- **Hook:** `DayStarted` (host): for each `FarmAnimal` in `Farm.getAllFarmAnimals()`, `pet(Game1.player,
  is_auto_pet: true)`. Also once in the Warm Welcome `adoptAnimal` hook, so an animal bought mid-day is covered
  for its first night. A real Auto-Petter in the same building adds nothing (vanilla no-op).
- **Edge cases:** Day 1 after a rewind covers Herd Book and "Start with a" animals. Animals off the farm
  (Ginger Island via mods) are out of scope.
- **Price:** 600 JP (an Auto-Petter for every building, every loop, from day one).
  **Gate:** `RunReachRequirement building:Coop` or `building:Barn`; the row needs one (Open question 6 if a
  two-way "either" reach is not supported; fall back to no gate).
- **Tooltip:** "Every animal on your farm gets petted each morning, like an Auto-Petter."

### 11. Snug Barn: no lost mood on days stuck indoors

- **Vanilla:** animals go out only when the door is open, `!IsRainingHere()` and `!IsWinterHere()`
  (`updateWhenNotCurrentLocation`; TLY's snow day adds itself through `WinterOrSnowDay`). Indoors the only mood
  rule is in `FarmAnimal.updatePerTenMinutes` (FarmAnimal.cs:1403): from 6pm, in Winter, an animal with happiness
  over 150 loses its `HappinessDrain` (3 to 10) every ten minutes, or gains it with a Heater. On a rainy day the
  loss is what they miss: grazing (`Eat`, FarmAnimal.cs:1455) sets happiness to 255 and adds friendship, and it
  does not happen in rain. Happiness feeds tomorrow's produce chance (`happiness/70`) and quality.
- **Effect:** on a day the animals cannot go out (rain, storm, green rain, snow, any Winter day, TLY's snow day),
  an animal indoors from 6pm gains its `HappinessDrain` every ten minutes at any happiness, as if a Heater were
  on. By midnight that is +108 to +360, so they go to bed content. No friendship bonus (grazing's +8 stays
  grazing's).
- **Hook:** prefix/postfix on `FarmAnimal.updatePerTenMinutes`: prefix records happiness; postfix, when owned,
  `timeOfDay >= 1800`, not outdoors, and the day is stuck-indoors (`IsRainingHere() ||
  WildcardAnimalPatch.WinterOrSnowDay(location)` on the Farm), sets `happiness = clamp(before + drain)`. A
  postfix, not a skip, so `WildcardAnimalPatch`'s transpiler and other mods' rewrites still run.
- **Edge cases:** Heater owners see no change in Winter. Animals left outside are not covered. Verify at build
  time that green rain reports `IsRainingHere()`.
- **Price:** 250 JP. **Gate:** none.
- **Tooltip:** "Animals stay happy on days they can't go outside."

### 12. Loyal Pet: presents more often

- **Vanilla:** `Pet.checkAction` (Pet.cs:641-660): the first pet of the day adds +12 friendship and rolls
  `GiftChance` (0.2 for Cat, Dog and Turtle) with a day-seeded random; `TryGetGiftItem` (Pet.cs:763) then picks
  from gifts whose `MinimumFriendshipThreshold <= friendship`. Every vanilla gift has threshold **1000** (5 full
  hearts). A full water bowl adds +6 a night (Pet.cs:467), so a new pet needs about 56 days of daily petting and
  water to reach its first present. Gift pools include bundle items: Cat (fish, Rabbit's Foot, Duck Feather at
  weight 0.05 of 2.9), Dog (Clay, Hardwood, Driftwood, Stone, Bone Fragments, artifacts), Turtle (Clam, Coral,
  Sea Urchin, Nautilus Shell, Rainbow Shell).
- **Proposed raise:** chance **20% to 40%**, and presents start at **600** friendship (3 hearts) instead of full.
  With Keep Pet (hearts carry) the chance is the part that matters. Without it, the threshold is what lets a
  first-loop pet bring anything at all.
- **Hook:** `AssetRequested` edit of `Data/Pets` (allowed, rule 1): `GiftChance = max(GiftChance, 0.4)` and each
  gift's `MinimumFriendshipThreshold = min(threshold, 600)`. `InvalidateCache("Data/Pets")` after the upgrade
  menu closes and on save load, the same pattern as `PastSeasonSpawnsService`. Modded pets get the same edit.
- **Price:** 150 JP (a Convenience-band power; Keep Pet is 50). **Gate:** `RunReachRequirement pet:1`.
- **Tooltip:** "Your pet brings presents twice as often, starting at 3 hearts."

## Price and number table

| # | Power (id) | Effect | JP | Gate |
|---|---|---|---|---|
| 1 | Lucky Rabbits (`animal_rabbit_feet`) | rabbit products are always Rabbit's Foot | 450 | species:Rabbit |
| 2 | Fine Feathers (`animal_duck_feathers`) | +25% feather instead of egg, no heart gate | 250 | species:Duck |
| 3 | Busy Barnyard (`animal_fast_produce`) | Duck 2>1, Goat 2>1, Sheep 3>1, Rabbit 4>2 days | 1,200 | none |
| 4 | Truffle Nose (`animal_truffle_double`) | 25% a dug truffle is two | 400 | species:Pig |
| 5 | Swift Horse (`horse_swift`) | +1.0 riding speed (about +10%) | 300 | Keep Horse |
| 6 | Horse Flute (`horse_flute`) | Horse Flute in the pack every loop start | 350 | Keep Horse |
| 7 | Quick Growth (`animal_quick_growth`) | babies grow up overnight (amended 2026-10-09; was halved) | 400 | none |
| 8 | Fast Hatch (`animal_fast_hatch`) | incubator time x0.5 | 200 | none |
| 9 | Warm Welcome I/II/III (`animal_warm_welcome_1..3`) | new animals start at 200 / 400 / 600 friendship | 200 / 450 / 800 | chain |
| 10 | Morning Rounds (`animal_morning_rounds`) | Auto-Petter on every animal each morning | 600 | a Coop or Barn this run |
| 11 | Snug Barn (`animal_snug_barn`) | Heater rule on every stuck-indoors evening | 250 | none |
| 12 | Loyal Pet (`pet_loyal`) | gift chance 20% to 40%, gifts from 3 hearts | 150 | pet:1 |

Whole roster: 5,950 JP (Warm Welcome counted at all three tiers).

## How they stack

- **With each other:** Lucky Rabbits + Busy Barnyard is a Rabbit's Foot every 2 days per rabbit (565g base each,
  and a loved gift for most villagers). Fine Feathers + Busy Barnyard makes ducks the best Duck Feather source by
  far. Quick Growth + Fast Hatch + Warm Welcome make an incubated flock productive within a week. Morning Rounds
  holds Warm Welcome's hearts from decaying and adds +8 a day on top. Swift Horse and Horse Flute both lean on
  Keep Horse.
- **With TLY systems:** the Wildcard snow day keeps animals in; Snug Barn treats it like rain (stays happy),
  Morning Rounds still pets, Busy Barnyard still produces indoors. Kitchen theme and Double Yolk roll a second
  product on every production day, so Busy Barnyard also raises their payout. Herd Book animals come back as
  adults with their own hearts (Warm Welcome skips them, Morning Rounds pets them on day 1). "Start with a"
  animals are new, so Warm Welcome applies. Every power is meta state: nothing is stored per loop, the rewind
  resets nothing, and each loop re-applies them through the same hooks.
- **Tiered:** only Warm Welcome, because its value scales cleanly with hearts and its top tier is strong. Busy
  Barnyard could be split by building (Open question 3). The rest are single rows.

## Bundle balance (flagged, not decided)

Animal products in bundles today come from the vanilla Animal bundle (Large Milk, Large Brown Egg, Large Egg,
Goat Milk, Wool, Duck Egg), artisan rows (Cheese, Goat Cheese, Cloth, Duck Mayonnaise, Truffle Oil) and TLY
Custom boards. Difficulty is rated by `AnimalProductAvailability` from `Data/FarmAnimals`: housing tier, price,
+1 for a deluxe product and +1 when `DaysToProduce > 1`. Because rule 1 keeps the data vanilla, the model keeps
rating these items at vanilla difficulty when the player owns the powers. That makes the powers a pure player
advantage, which is the point of a power, but:

- **Busy Barnyard** is the real risk. Goat Milk, Wool, Duck Egg and Duck Feather lose their slow-producer step in
  practice, and quantity-scaled bundles (several Wool for Cloth, several Goat Milk) fill two to three times
  faster. The gate is still the building (Big Coop, Big Barn, Deluxe Coop/Barn), so it does not open anything
  early. Recommendation: keep it permanent but priced at 1,200, and leave the effort model alone. Alternatives in
  Open question 1.
- **Warm Welcome** reaches large eggs and milk on day one at any tier (the 200 gate). That is close to the
  "large products more often" power Jeff dropped. Recommendation in Open question 4.
- **Lucky Rabbits / Fine Feathers** make Rabbit's Foot and Duck Feather common. Both are rated as deluxe products
  (+1 effort); with the powers they are not. Low risk: they need a Deluxe Coop / Big Coop first.
- **Loyal Pet** can turn up bundle items (Clay, Hardwood, Coral, Nautilus Shell, Rabbit's Foot). Low risk at one
  gift every few days at most.

## Diagnostics and tests

- `tly_animalpowers` prints owned powers and, per animal, type, age, `daysSinceLastLay`, effective produce days,
  friendship and happiness.
- Core tests: catalog rows (ids, costs, prerequisites, gates, Warm Welcome chain), the species target table
  (Duck 1, Goat 1, Sheep 1, Rabbit 2, others vanilla), the half-days-to-adult table, the Warm Welcome floor by
  tier, `I18nGuardTests` for every key.
- Live test on the Rodger throwaway save through the bridge (my launch, labeled): buy each power with
  `tly_addjp` and `tly_buyupgrade`, add animals with vanilla debug commands, `debug sleep`, read
  `tly_animalpowers`.

## Open questions for Jeff

1. **Busy Barnyard and bundles.** Keep it as one 1,200 JP permanent (recommended), or make it a Season statue
   boost (bounded, the Double Yolk shape), or teach the effort model to raise animal-product quantities when it is
   owned?
2. **The Animals tab.** New ninth tab for the twelve (recommended). Move Keep Horse, Keep Pet, the "Start with a"
   rows and the Herd Book into it too, or leave them where they are?
3. **Busy Barnyard split?** One row (Jeff's list), or two cheaper rows by building: coop (ducks, rabbits) and
   barn (goats, sheep), about 600 each?
4. **Warm Welcome and large products.** Any heart crosses the 200 friendship gate for large eggs and milk.
   Options: (a) accept it (recommended: the tiers are what buy quality, and the large products follow vanilla
   hearts); (b) tier I at 150 so only II and III unlock large products early; (c) a single tier at 400.
5. **Rates to confirm:** Fine Feathers +25%, Truffle Nose 25%, Swift Horse +1.0 (about 10%), Loyal Pet 40% from
   3 hearts.
6. **Morning Rounds gate:** "a Coop or Barn this run" needs a two-way reach the evaluator may not have. If it
   doesn't, no gate (recommended) or Coop only?
7. **Names** for 1 to 4 (Lucky Rabbits, Fine Feathers, Busy Barnyard, Truffle Nose) are placeholders.
8. **Horse Flute without Keep Horse.** Recommended gated on Keep Horse; a player who builds a stable every loop
   by hand could still use it. Drop the gate?

## Out of scope

Half hay and large products more often (dropped by Jeff). Silo hay keep and a second coop/barn keep (TODO
"IDEAS, NOT BUILT", separate spec). Multiplayer. Android.

## Round 2 (Jeff's feedback, 2026-10-09)

### Renames (i18n only, save ids unchanged; 0.19.28)

Fine Feathers is **Molting Season** (`animal_duck_feathers`), Truffle Nose is **Truffle Hog**
(`animal_truffle_double`), Busy Barnyard: Coop / Barn are **Busy Coop** (`animal_fast_produce_coop`) and **Busy Barn**
(`animal_fast_produce_barn`). The body above keeps the old names as the history of the design.

### Quick Growth measured live (throwaway farm, every vanilla farm animal)

How age works (FarmAnimal.dayUpdate, FarmAnimal.cs:984): every fed night `age++`, and the night `age == DaysToMature - 1`
calls `growFully`. An animal is a baby while `age < DaysToMature`, so a new animal (Marnie, an incubator, a barn
birth; all start at age 0) is an adult after DaysToMature fed nights. Age keeps counting after that. Quick Growth adds
one more step each fed night, so it is DaysToMature / 2 rounded up. Measured with `tly_animalpowers` each morning
(control set first, then a second set after buying the power):

| Animal | DaysToMature | Vanilla, nights to adult | Quick Growth | Saved |
|---|---|---|---|---|
| White / Brown / Blue / Void / Golden Chicken | 3 | 3 | 2 | 1 |
| Duck | 5 | 5 | 3 | 2 |
| Rabbit | 6 | 6 | 3 | 3 |
| Dinosaur | 0 | 0 (adult on arrival) | 0 | 0 |
| White / Brown Cow | 5 | 5 | 3 | 2 |
| Goat | 5 | 5 | 3 | 2 |
| Sheep | 4 | 4 | 2 | 2 |
| Pig | 10 | 10 | 5 | 5 |
| Ostrich | 7 | 7 | 4 | 3 |

It buys one day for chickens, two for most animals, five for a pig. Proposed stronger versions (not built, Jeff picks):

- **(a) Grow up overnight:** every baby is an adult after its first fed night. Chickens save 2, pigs 9. Simplest
  line ("Baby animals grow up overnight."). A bought or hatched animal produces from its second morning, which makes
  Marnie's babies nearly as good as adults and pairs hard with Growing Herd and Fast Hatch. Suggest 500 JP.
- **(b) Cap at 2 nights:** half the time, but never more than 2 nights. Chickens and sheep unchanged (2), the rest 2.
  Keeps a short wait so a baby is still a baby. Same price.
- **(c) Two tiers:** Quick Growth I as built (half, 350 JP), Quick Growth II overnight (option a, +400 JP).

None of these touches Data/FarmAnimals or the effort model, so no bundle changes (contract: powers only make things
easier). Products arrive earlier, which is the point.

### Horse Flute needs Keep Horse (confirmed from code)

The row has `PrerequisiteId early_horse` (UpgradeCatalogAnimals), and the loop-start grant is
`AnimalPowers.GrantsHorseFlute = has(early_horse) && has(horse_flute)` (RunBaselineBuilder, unit-tested). Keep Horse
(HorseCarryoverService) snapshots the stable's tile, horse name and hat before the reset and rebuilds the stable,
owned by the player, right after it, before FarmerReset hands out the flute, so the flute works on day 1. If the
player never built a stable (or tore it down) there is nothing to restore and the flute shows vanilla's "no horse"
line until a stable is built.

### Power 13: Growing Herd, barn animals give birth more often (built, 0.19.29)

- **Vanilla (1.6):** a barn birth is a night event. `Utility.pickPersonalFarmEvent` runs on nights with no other farm
  event and returns `QuestionEvent(2)` on a coin flip (the other half is the dogs sound). `QuestionEvent.setUp` case 2
  (QuestionEvent.cs:51) walks the player's buildings; the first building that allows pregnancy (Data/Buildings
  `AllowAnimalPregnancy`: **Big Barn and Deluxe Barn only**, not the plain Barn), is **not full**, and passes
  `Game1.random < animalsThatLiveHere x 0.0055` picks one animal living there at random. The birth happens only if
  that animal is an adult, `allowReproduction` is on, and its species can get pregnant (`CanGetPregnant`: cows, goat,
  sheep, pig; not ostrich, no coop animal). Then the "gave birth" dialogue and a NamingMenu, and
  `AnimalHouse.addNewHatchedAnimal` adopts a baby of the parent's type (so Warm Welcome applies). A Big Barn of 7
  adults: about 1.9% a night, two births a loop.
- **Effect:** the per-barn roll is **4x** (`animals x 0.022`), and the parent is drawn only from animals that can
  give birth, so a baby or an ostrich no longer wastes the night. The coin flip and the dogs night stay vanilla. The
  full-barn rule stays (births stop when the barn is full).
- **Hook:** prefix on `QuestionEvent.setUp` (`GrowingHerdPatch`), only for case 2 and only when owned: the same walk
  with the new chance and parent pick, then vanilla's own success path (dialogue, `animal` field, messagePause).
  Vanilla's roll runs when the power is not owned, on a rewind night (FarmEventSuppressionPatch's test) and if the
  prefix throws.
- **Price:** 400 JP (Truffle Hog's band; a birth is a free barn animal, 750 to 8,000g). **Gate:**
  `RunReachRequirement building:Big Barn` (a Deluxe Barn counts).
- **Text:** "Growing Herd" / "Cows, goats, sheep and pigs give birth more often." (placeholder for Jeff).
- **Pure rules + tests:** `AnimalPowers.BirthChance`, `AnimalPowers.CanGiveBirth`, catalog row (AnimalPowersTests).
- **Live (2026-10-09):** `tly_animalpowers births 5000` on a Deluxe Barn of 7 (5 adults that can breed, an ostrich,
  a calf): vanilla 140 births (2.80%), with Growing Herd 820 (16.40%), about 5.9x. A real night forced with
  `tly_animalpowers birthnight`: "animal_more_births: Trellu (Pig) gives birth tonight", the dialogue and NamingMenu
  closed by `tly_dismiss`, and "Warm Welcome: Zutsabell (Pig) arrives with friendship 400". A vanilla birth happened
  on its own earlier the same run (control, Bukell the White Cow).

### Live checks of Truffle Hog and hatching (2026-10-09)

- **Truffle Hog, real pigs outdoors:** 12 pigs at 1000 friendship, doors open, a full day on the farm each.
  Control day (power revoked with `tly_animalpowers revoke`): 19 truffles from 19 digs, no doubles. Power day: 34
  truffles from 29 digs, 5 doubled (17%, the roll is 25%; small sample), each logged "animal_truffle_double: <pig>
  dug up a second truffle".
- **Incubator hatch with Warm Welcome II:** entering the coop played vanilla's hatch event; `tly_dismiss` closed the
  message and named the chick through the NamingMenu's own Enter path: "Warm Welcome: Mep (White Chicken) arrives with
  friendship 400 (was 0)", age 0/3.
- **Fast Hatch:** "animal_fast_hatch: Incubator ready in 4500 min (was 9000)" (vanilla 9000 shown by the control egg).
  With that week's machines_slow liability stacked (x1.25): 5630 minutes, egg in on Spring 16 at 8am, ready the
  morning of Spring 20, hatched on entry. Without the liability 4500 minutes is about 3 days against vanilla's 6.25.

### Amendment 2026-10-09: Quick Growth is one row, overnight, 400 JP

Jeff, 2026-10-09: "just 1 quick growth, 400 JP". Option (a) above, as a single row with the same id
(`animal_quick_growth`), price 350 to 400 JP, no gate. Tooltip: "Baby animals grow up overnight."

- **Effect:** any baby farm animal (bought, hatched or born) that ages on a fed night becomes an adult that night.
  The Dinosaur is born adult and is untouched. An unfed night still does not age a baby, so it does not grow up.
- **Hook:** the same `QuickGrowthPatch` postfix on `FarmAnimal.dayUpdate`: when vanilla aged the animal tonight and it
  is still a baby, it calls vanilla's own `growFully`. `QuickGrowthStep` is now `None` or `GrowFully`;
  `QuickGrowthNights` is 1 for every baby animal and 0 for the Dinosaur.
- **Live check (throwaway farm, game minimized, log only):** power bought, a new White Chicken (age 0/3) and a new Pig
  (age 0/10) fed, one `debug sleep`: the log reads `animal_quick_growth: ... (White Chicken) grew up (age 3)` and
  `... (Pig) grew up (age 10)`, and `tly_animalpowers list` shows 3/3 and 10/10 the next morning.
