# Changelog

All notable changes to **The Longest Year** are documented here. This project
aims to follow [Semantic Versioning](https://semver.org/).

## Unreleased

### Added

- **`tly_gateneeds` console command** prints, per bundle, what the current season's day-28 gate still needs (the same numbers the Season Goals page shows) and the vault (debug).
- **Keep the Garden Pot recipe.** A new permanent keep at the Junimo Shrine, 750 JP, under Obtainability. Once bought, the Garden Pot recipe is back in your crafting list after every rewind, so an Oasis seed can be grown indoors out of season.
- **Two Boosts, bought at the farm's planning shrine.** Boosts are spent Junimo Points that last a week or a season instead of forever. **Year-Two Seeds** (75 JP, this week) gives Mixed Seeds a 5 percent chance to roll the season's year-two crop: Garlic in Spring, Red Cabbage in Summer, Artichoke in Fall (not sold in Winter). **Sneak Peek** (100 JP, this season) has the Queen of Sauce air the year-two episode on Sunday, and teaches you both that week's year-one recipe and the year-two one, so nothing is lost. The shrine now has a Boosts section with a Buy button; the full shrine redesign is still a later spec.
- **`tly_boost <yeartwoseeds|sneakpeek>` console command** makes the same purchase the shrine's Buy button makes, and **`tly_tv`** runs the Queen of Sauce weekly-recipe lookup the TV uses and logs the dialogue and whether the recipe landed (debug).
- **`tly_playseason quarter <k>` console command** donates the season's gate share a quarter a week instead of all at once, round-robin across bundles, so four calls walk a season the way a real player would (debug).
- **`tly_reset <seedLoop>` and `tly_genbundles <seedLoop> [custom|standard|remixed]`** pin the board seed, so two sims can be run on the same board, and roll a diagnostic board from vanilla's Standard or Remixed bundle data through the same gates and audit (debug).
- **`tly_skipscene` console command** finishes the open day-28 Junimo scene as if clicked through, so an unattended run can cross a season gate over the file bridge (debug).
- **Unattended playtesting without the mouse (debug).** With `EnableDebugCommandBridge` on, the game no longer pauses when its window is not in front, so queued `tly_*` commands keep running while you work; `tly_select <theme>` on an open planning hub is the card click (hub closes). `tools/deploy.ps1 -Minimized` relaunches the game minimized. Runbook: `docs/HEADLESS_DRIVING.md`.
- **Three new weekly themes: Spelunking, Artisan and Kitchen.** Their goals match by item kind anywhere on the board (gems, minerals, monster loot and artifacts; artisan goods; cooked dishes and animal products). Spelunking: 10% chance a slain monster drops everything twice, but machines run 25% slower. Artisan: machines finish 25% sooner, but cooked food restores half its energy and health and gives no buffs. Kitchen: 20% chance an animal gives a second product each day, but monsters deal 25% more damage. Eight themes, still two cards a week.
- **`tly_themepool [theme]` console command** prints each theme's askable goal count for the current week and, with a theme, every candidate line with its due/filler status, tier and weight (debug).
- **`tly_dumpeffort` console command** writes `item-effort-model.md`: every pool item with its derived effort, tier and the game-data basis (gems and minerals, geodes, monster drops, artifacts, artisan goods, fish ponds, animal products, cooked dishes, crops, forage). `tly_itemmodel` now prints the effort source and tier (debug).
- **Keep your wallet items and Stardrops.** Eighteen new keeps at the Junimo Shrine: one per wallet item (Rusty Key, Skull Key, Club Card, Special Charm, Dark Talisman, Magic Ink, Dwarvish Translation Guide, Town Key, Magnifying Glass), one each for Bear's Knowledge and Spring Onion Mastery, and one per Stardrop source (Fair, fishing, mines, Krobus, spouse, Secret Woods, museum). A row appears on a Fail night once you have earned that item this loop; buy it and it survives every rewind. 150 to 750 JP. A kept Stardrop also keeps its source marked as claimed, so the same Stardrop cannot be collected again next loop. Keeping the Skull Key keeps the Skull Cavern door open too.
- **`tly_wallet` console command** to set or list wallet, power and Stardrop markers (debug).
- **`tly_playseason [goals]` console command** simulates a minimal compliant player for the current season (donates exactly what every gate demands by day 28 through real CC slot flips, pays the vault; `goals` also deposits the week's goal slots), for real-play audits (debug).
- **`tly_goals [season] [week]` console command** logs the weekly goals every theme would offer on the live board for a season, through the hub's own sampler (debug, read-only). `tly_genbundles` now lists every slot of every bundle by name, the gates each bundle would run under, and runs the same season-gate audit as `tly_gatecheck` on the diagnostic board.

### Changed

- **Every item now has two weeks, not one: a pacing week and a hard week.** The pacing week is when a normal player realistically reaches an item; the hard week is the earliest it is possible at all. Which one the board uses comes from the difficulty step: Normal paces, and the harder steps gate on the hard week. Nothing changes for a player who leaves the dials alone.
- **Stretch gates.** A bundle that gains nothing new in a season now reaches two weeks past that season instead of sitting idle, and if it holds no item that can do that, one is swapped in. This replaces the old Spring foothold. Stretch is a pacing mechanism, so the harder steps get hard gates instead of stretch lines, and Easy gets neither. `tly_gatecheck` tags the stretch line and the season it belongs to.
- **Every rolled bundle of four or more slots holds at least one genuinely hard item.** No board can hand you four easy lines and call it a room. The rule still applies on Hard and Extreme; Easy is exempt. `tly_gatecheck` tags a bundle that ended up without one.
- **Every bundle rolls its slots from the full pool of its kind.** A bundle keeps its name, its room and how many items it asks for, but the hand-written item lists are gone: a fish bundle draws from every fish, a forage bundle from every forage, and the mixed-kind bundles (Chef's, Dye, Fodder, Wild Medicine, Enchanter's, Field Research, Children's and the rest) draw from a named recipe of parts instead. Two boards of the same kind no longer look the same.
- **Legendary fish, mine fish and the year-two crops can appear on the board.** Stonefish, Ice Pip, Lava Eel, Crimsonfish, Angler, Legend, Glacierfish, Mutant Carp, Garlic, Red Cabbage and Artichoke all enter the pools at low odds (weight 1 against vanilla's 3). A legendary drawn into a four-of-four fish bundle is mandatory for that bundle: a hard roll is a challenge, not a mistake. Easy still leaves the year-two crops out.
- **A rewind clears your legendary catches.** They were marked caught for good, so a legendary asked for by a later loop's board could not be caught again. The new year starts with them uncaught, like everything else.
- **Item difficulty is measured on absolute bands, not against the rest of the board.** Effort tiers used to be quartiles of whatever a board happened to roll, so an easy board's hardest item scored as hard. The bands are now fixed, which is what makes "one hard item per bundle" mean the same thing on every board.
- **Weekly goals follow the gate with no look-ahead.** The half-a-season lead is gone: a theme may ask only for what this season's day-28 gate demands, so a player who completes every theme week no longer banks Winter's share in Fall.
- **Weekly goal ceilings are flat 5 in every season** (was 5 / 5 / 5 / 6), still budgeted over the weeks left.
- **The hand-written season pin table is retired.** It held 20-odd items whose seasons the rules could not see; the rules see them now, and only three judgement pins remain (Red Mushroom, Sea Urchin, Woodskip). Everything else is derived from the game's own data.
- **Weekly goals may run half a season ahead of the gate, and no further.** Filler is allowed in every season (the floor only stops an item showing too early); per bundle, the goals may ask for what the gate demands by this season plus half of what it demands next season. A goal-completing player neither empties the board by Fall (sim H) nor sits on empty weeks once a season's share is in (sim L). Books and guild hats and weapons are placed.
- **Weekly goals follow the gate exactly for pick-X-of-Y bundles.** A bundle may be asked for at most what its ramp demands by the end of the current season, minus what is already in, so a player who completes every theme week no longer donates Winter's share in Summer and Fall (sim H, 2026-08-28: that player reached Winter with 12 lines on the board).
- **Every item now has a first week it can exist, not just fish and metals.** The engine places gems, ores, geode minerals and monster drops by mine depth (30 floors a week through Spring, floors 80 and deeper a Summer gate, Skull Cavern from Fall), artifacts on day 1, animal products by building tier, artisan goods by the machine's skill level and its input, dishes by the kitchen and their ingredients, pond products a season after their fish, crops by their first harvest, forage by first spawn and location, saplings on day 1. Weekly goals ask for an item only from that week; day-28 gates use its gate season. Anything the engine still cannot place is listed as UNKNOWN by `tly_dumpavailability` instead of silently counting as Winter. (Spec `docs/superpowers/specs/2026-08-28-even-year-availability-design.md`.)
- **Pick-X-of-Y bundle ramps follow their own items.** An even quarter split of X, never above what the bundle's items can supply by each season's gate. The hand-curated ramp table is retired; a `BundleQuotas` entry in config still overrides by name.
- **Every re-rolled bundle keeps a Spring foothold.** The engine swaps in at least one item a Spring gate may demand (a quarter of the picks) when its pool has one; `tly_gatecheck` tags bundles without one.
- **Weekly goal ceilings are flat: 5 / 5 / 5 / 6** (was 3 / 4 / 5 / 7), budgeted over the weeks left as before. New config `AvailabilityWeekOverrides` moves a single item's first week later.
- **`tly_dumpavailability` shows Week, Gate and Placed per item and ends with the Unknown items and Rejected overrides lists; `tools/sim-year.sh` writes it after every run.**
- **Theme weeks ask for their share of what is left, not the season maximum.** Each week's goal count is the theme's open lines spread over the weeks left in the season (never above the season ceiling), so week 4 looks like week 1 instead of week 1 taking everything. Season ceilings are now 3 / 4 / 5 / 7 (was 4 / 5 / 6 / 7), filler is allowed only from Fall (0 / 0 / 1 / unlimited, was 0 / 1 / 2 / unlimited), and pick-X-of-Y bundles ask for less by Summer and Fall so Winter keeps lines to ask for (derived ramp 25 / 35 / 60 / 100%, was 25 / 50 / 75 / 100%, Spring unchanged; Exotic Foraging, Animal, Crab Pot, Artisan, Adventurer's and Mineral curated ramps moved one step later). Two headless year sims on 0.16.72 had a goal-completing player reach Winter with 11 open lines and weeks 2 to 4 offering 1 or 0 goals per theme.
- **Weekly goals follow the season gate first.** Goals are drawn from the lines the day-28 gate demands this season; other open lines are filler, at most one per bundle per week and capped per season (Spring 0, Summer 1, Fall 2, Winter unlimited; `ThemeFillerBySeason` in config.json). Easier items are weighted earlier in the year and harder ones later, using effort derived from the game's own data.
- **The weekly offer only shows themes that can ask for two or more goals**, weighted by how much they can ask, so a theme with nothing to donate never hands out a free drawback lift; the Bulletin Board's Mixed theme now draws from anything on the board.
- **The weekly theme bonus is paid per goal.** The 30 JP (times the season multiplier) that used to land only when every goal was done is now split evenly across the week's goals and paid as each one lands. The drawback still lifts only when every goal is done. A one-goal Winter week pays its share, not the full 120.
- **Weekly goals draw from every item you could bank this season, not only the ones due this season.** A bundle whose items each carry their own deadline only offered the items due right now, so the Mixed theme had one goal all Spring and nothing in weeks 3 and 4 once it was donated (real-play simulation, 2026-08-28). Any undonated ingredient that is obtainable in the current season is a goal candidate; the deadlines still drive the season gates as before.
- **At most one fruit-tree fruit and one crab-pot catch per weekly goal list.** A Spring week 2 Farming list named three tree fruits, and a week 1 Fishing list was all crab-pot catches and no fish (Jeff, 2026-08-28). Each theme's goals now hold at most one fruit from Data/FruitTrees and at most one Data/Fish trap catch; the caps are per theme list, not per season gate, and modded content counts.
- **No item is asked for twice across the board.** Re-rolled bundles are filled tightest pool first, and each one leaves out every item another bundle on the board already asks for (authored bundles and kept-vanilla bundles included), so the same fish or forage no longer shows up on three or four bundles at once. A bundle only allows a repeat when its pool would otherwise run dry. Crab Pot and the other fixed vanilla lists keep their items, so they can still overlap.
- **Midnight Squid, Spook Fish and Blobfish are valid Night Fishing picks.** Data/Objects flags the three ExcludeFromRandomSale (the game keeps them out of random shop stock) and the pool vet read that as never obtainable, so the "one Night Market fish" rule below only ever had Octopus and Sea Cucumber to choose from. A fish with a Night Market spawn row now passes the vet, keeps only its market rows (its Beach rows are gated in code, not data), and is gated to Winter like the market itself.
- **Night Fishing asks only for night fish.** Its vanilla ingredients span every water, so the re-roll could hand it a daytime ocean fish like Flounder. It now draws only fish that cannot be caught before 6pm anywhere (Bream, Squid, Super Cucumber, Midnight Carp in vanilla; Eel and Walleye bite in the afternoon, so they are out), plus the Night Market's fish, at most one per bundle. Modded fish follow the same rule from their own Data/Fish hours.
- **Seasonal foraging and crop bundles ask only for that season's own items.** Beach shellfish (Mussel, Clam, Cockle, Oyster) and desert fruit spawn all year, so they sat in all four seasonal forage pools at full weight and Mussel turned up in bundle after bundle (player report). Like vanilla, Spring/Summer/Fall/Winter Foraging now draw only season-specific forage; the year-round items still feed Crab Pot, Exotic Foraging and Four Seasons Sampler. Winter Root and Snow Yam join the Winter pool (vanilla's own Winter Foraging items, previously missing). Applies to modded forage and crops the same way: anything that spawns in every season is left out of season-named bundles.

- **Bear's Knowledge and Spring Onion Mastery no longer survive a rewind for free.** The game grants them by "you have seen this scene", and the rewind used to re-mark those scenes as seen, so both powers came back every loop unpaid. They are now wiped with the loop like every other power; the bear and the river lesson can be found again, or the keep can be bought.

### Fixed

- **Donations are tracked per Community Center slot and mirrored from the board.** One deposit credits one bundle (Children's no longer shows 3/3 after two donations when another bundle shared an item), a bundle with a repeated item (Construction's two Wood slots) needs every slot filled, and the mod can no longer declare a Winter win while the board still has an open slot. Existing saves migrate on load from the board's own state; nothing is lost.
- **Fishing trash is a week 1 item.** Trash, Driftwood, Broken Glasses, Broken CD, Soggy Newspaper and Joja Cola came off the line on day 1 in any water, but the board dated them by whatever machine or pond route it found first, so a Recycling bundle could sit undated until Winter.
- **Crop weeks are counted properly.** A crop's first harvest is its planting week plus its growth days over seven, not a rounded season guess, so a 12-day crop planted in week 5 lands in week 6 and a 13-day one planted in week 9 lands in week 10.
- **Tapper goods are placed from the game's own tree data.** Maple Syrup, Oak Resin, Pine Tar, Sap and the mushroom-tree goods now come from Data/WildTrees TapItems: the Tapper is a Foraging 4 recipe, and the good is ready the row's own number of nights later. They used to have no date at all outside the artisan rule.
- **Jack-O-Lantern is a week 12 item** (Spirit's Eve is Fall 27), and the Golden Pumpkin from the maze with it.
- **Cactus Fruit is Desert forage, week 9.** Its seed carries every season in the data, so it read as an ordinary all-year crop; it needs the bus, which this mod opens in Fall.
- **Ghostfish is a week 1 fish.** A leftover season pin held it to a later season while the mines it lives in are open from day 1. The pin table it came from is gone, and the mine-fish weeks place it directly.
- **Bone Fragment comes from mine area 40**, not week 3: the skeletons that drop it do not appear until then. Dig spots still supply it on the same date.
- **Dried Mushrooms are placed under their real item id.** The old id never matched anything, so every Dehydrator mushroom good fell through to the Winter default.
- **Secret Woods has a date.** Morel, Fiddlehead, Woodskip and its hardwood stumps need the Steel Axe, so the Woods is a week 4 unlock; nothing from it is asked for before then.
- **The Sewer opens in week 7.** Krobus, his shop and everything gated behind the sewer key now share that date, including the friendship routes that run through him.
- **Help Wanted rewards have real dates.** Prize Ticket is week 2 (every third quest) and Mystery Box week 3 (the Qi plane after the sixth quest or day 50), each with an earlier hard week for the harder difficulty steps.
- **Books are dated one by one, and the unreachable ones are off the board.** Every readable book has its own week from where you actually buy or find it; the year-two books and the drop-only books are out of the pool entirely, as are Banana and Mango, which need Ginger Island.
- **Late floors for Oasis-seed crops and Winter dig forage.** Cactus Fruit, Beet, Rhubarb and Starfruit read as ordinary crops (their seeds carry every season in the data) and Winter Root and Snow Yam as day-1 dig finds (the spot row's Winter condition is not read); a small table now holds them to the Desert week or Winter.
- **Availability rules, second pass from the sim J board:** the game's plural machine tags (`category_fruits`) now match, so jar, keg and dehydrator goods are placed; artifacts in the catalog pool without a spot row are week 1; Cave Carrot, Moss, Tea Leaves, Jack-O-Lantern, Oil of Garlic and fruit-tree fruit have table weeks marked for Jeff to confirm.
- **Availability rules: the earliest week wins across rules** (Wood read as week 5 from the Recycling Machine, Red Mushroom week 9 from the Mushroom Box, Sea Urchin week 5 from a fish pond); a season pin may move a rule's week earlier (only fish, crab-pot and metal floors are facts); deluxe animal produce keeps its building's week; every trap fish in Data/Fish is placed (week 2); Pierre's staples, the Saloon's menu, Adventurer's Guild rewards and Help Wanted rewards have weeks; the Spring foothold no longer touches season-named bundles (it had swapped a Spring item into Fall Crops).
- **Stale bridge queue at launch** (debug). `tools/deploy.ps1` now deletes a `tly_commands.txt` left over from a session that closed before the mod drained it, so old `tly_*` lines no longer run at the title screen on the next launch.
- **`tly_reset` with the planning hub open** (debug). The hub survived the in-place reset and the new run's week-1 offer was blocked for good ("Cannot open menu: another menu is already open"). The debug reset now closes an open hub first.
- **Weekly goals no longer name a fish out of its season.** A bundle whose items each carry their own deadline put an item into the week's goal list by its deadline season alone, so Lake Fish could offer Sturgeon (Summer and Winter only) as a Fall goal and Rainbow Trout (Summer only) as a Winter goal. Goals now also pass the same in-season check the other bundle kinds use (bundle-loop audit, 2026-08-29).
- **Red Mushroom counts as a Spring item.** Its curated season pin said Summer while the Spring forage pool already offered it, so a Spring Foraging bundle that drew it audited as impossible at its own gate. The pin is now Spring (the mines grow it on mushroom floors from level 41 in any season).
- **Weekly goals respect the location floors.** A pick-some-of-these bundle offered Scorpion Carp as a Summer goal: the desert pond lists it in every season, but the bus is a Fall unlock on this mod's start and the season gates already knew that. Goals now consult the same derived item model (Desert and Skull Cavern from Fall, the mines and the Sewer from Summer) for fish and metals (bundle-loop audit, 2026-08-29).
- **Weekly goals read forage seasons from the engine's own forage pool.** The goal side scanned Data/Locations on its own, without the location exclusions or the condition seasons the bundle pools apply, so Ginger Island's season-less cave rows made Chanterelle and Purple Mushroom read as year-round and the Foraging theme offered them in Summer. Forage now joins the fish spawn-season map (bundle-loop audit, 2026-08-29).
- **Night Market and festival fish are no longer treated as catchable all year.** The Submarine's spawn rows carry no season (the game gates the Night Market by date in code), so Sea Cucumber, Super Cucumber and Octopus read as year-round and one player was asked for a Sea Cucumber before Summer 1; Squid's SquidFest rows did the same. Spawns on a passive festival's own maps, or behind `IS_PASSIVE_FESTIVAL_OPEN`, now take that festival's season from Data/PassiveFestivals (Winter for the Night Market and SquidFest, Summer for the Trout Derby), which flows into the season gate and the weekly goals. Modded passive festivals are read the same way.
- **Ocean fish in lake bundles, river fish in ocean bundles.** The engine treated every Data/Locations key a fish spawns in as a habitat, including three that are not fishing spots: the Festival of Ice contest map (`Temp`, whose rows mix Red Mullet with Bream and carry no season), the Fair minigame (`fishingGame`) and the shared trash table (`Default`). That let Red Mullet into Lake Fish, Bream/Pike/Sunfish into Ocean Fish, marked river fish as catchable all year, and put Trash and Joja Cola in the fish pool Weatherman's draws from. Those keys are now ignored when the pools are built (player report, 2026-08-28).

## 0.16.17 - 2026-08-27

1153 tests. Both features live-smoked on the throwaway save (STATUS.md). 0.16.8 to 0.16.16 were
internal builds. Also smoked once with Stardew Valley Expanded enabled: SVE's crops, fish, saplings
and tapper goods join the engine pools and the board classifies fully, but the engine's own
board manifest no longer matches the live data on an SVE save, so TLY uses its read-only
classification path there and season-pity easing does not apply (tracked in TODO.md).

### Added

- **Keep your power books.** Nineteen new Carryover keeps at the Junimo Shrine, one per vanilla power book (Way of the Wind, Friendship 101, The Diamond Hunter and the rest). A row appears on a Fail night once you have read that book this loop; once bought, the book's power survives every rewind. Priced 150 to 750 JP by how much the power is worth over a year. Nothing stacks and nothing is free: the reset still wipes every book you did not buy. Spec: `docs/superpowers/specs/2026-08-27-keep-power-books-design.md`.
- Debug console command `tly_readbook [Book_Id]` to mark a book as read, or list every book's flag.
- **The town half-remembers.** Villagers you have spent a lot of time with across loops (talks, gifts and heart events add up in the background; hearts themselves still reset) occasionally open with an uncanny line in their own voice; their normal dialogue is still there the next time you talk to them that day. About one a week at most, one per villager per loop, never in loop 1, never on a villager's first-meeting day. No gameplay effect. Toggle "Deja-vu dialogue" in Features. Idea: u/Gribbleby on the beta announcement thread. Debug: `tly_dejavu`.

### Fixed

- **Villagers use their first-meeting dialogue again after a rewind.** The game keys that line on a six-day "Introduction" window created only when a farmer is born, so from loop 2 on every villager greeted a stranger with an ordinary daily line. The rewind now re-seeds that window. (Emmalution's stream, 2026-08-27.)

## 0.16.7 - 2026-08-27

1123 tests. Live-smoked on the throwaway save (STATUS.md).

### Fixed

- **Tools in the Junimo Stash keep their bait, tackle and enchantments.** A rod banked in the stash was rebuilt from its item id on the next loop, so it came back empty and un-enchanted. The 0.12.0 fix only covered a kept rod that stayed in your inventory. (Nexus posts, CausticOptimist and Bumblewyn.)
- **Keep Pet gives every pet its own bowl.** The rebuilt farm has one bowl and the game binds one pet per bowl; any pet without one is moved into the farmhouse each morning and loses friendship. With two pets, the second one looked like it had not come back. Extra bowls are now placed beside the first, and a save already in that state gets the missing bowl on the next morning. (Nexus bug 1122901.)
- **The beach bridge is broken again after a rewind.** Repairing the bridge edits the game's cached map in place, so the next loop drew a repaired bridge under a "?" marker you could not interact with. The rewind now reloads the Beach, Forest, Mountain and Town maps from clean data, which also stops Robin's shortcuts leaking into a loop where you did not keep them. (Nexus bug 1124076.)

### Added

- Debug console commands `tly_addpet`, `tly_fixbridge` and `tly_stashrod` (each with a `check` verb) so the three fixes above can be driven and verified from the SMAPI console.

## 0.16.0 - 2026-08-27

1113 tests.

### Added

- **Ten independent difficulty dials**, in the mod's settings menu (GMCM) under **Difficulty**. There is no overall difficulty setting: turn up only what you want turned up. Each dial has four steps (Easy / Normal / Hard / Extreme) and every one starts on Normal, which is the balance the mod already shipped with. Changing nothing changes nothing. A change applies at your next loop, not straight away. Full list and what each one does: see the README or the mod page.
- **`tly_itemmodel <item or bundle>` console command.** Prints what the mod believes about an item: the earliest season it can exist, how much work it scores as, and the reasoning behind both. Pass a bundle name to see every ingredient and its due season. Read-only.
- **`tly_gatecheck` now explains itself.** When it names an ingredient blocking a season gate, it prints why that ingredient is dated the way it is.

### Changed

- **Every bundle now applies pressure at the season checkpoints.** Bundles that require *all* of the items they show used to take their per-item due dates from a hand-written table of 40 items. An ingredient outside that table had no due date at all, so a bundle whose ingredients were all absent could be ignored until the Winter check. Because the mod re-rolls the six fish bundles from a 52-item pool and the two metals bundles from an 11-item pool, most re-rolled boards had at least one bundle applying no pressure for three seasons, and roughly a quarter of fish boards and a third of metals boards were entirely free.

  The mod now derives this itself from the game's own data. For every fish and metal it works out the earliest season the item can exist and an effort score (fishing level, weather, time window, mine depth, smelting), then spreads a bundle's items across the four checkpoints easiest first, weighting harder items later. Because it is read from live game data rather than a list, it covers remixed boards and re-rolls.

- **This makes the year harder, at every difficulty including Normal.** It is a deliberate balance change, not a bug fix, and it is the reason to give this release a fresh look even if the previous one felt right. Measured across three boards and two difficulty configurations before release: no season gate is unsatisfiable, and no bundle is free all year.

- **A due date can never precede the season an item can first exist in.** The deadline is clamped to the item's earliest possible season, so an impossible gate cannot be expressed at all rather than being caught by review. A season pin that would demand an item earlier than it can exist is now rejected and logged instead of honoured.

### Known limits

- Bundles whose ingredients come from crops, forage, monster drops, artisan goods, cooking, artifacts, books, saplings, geode minerals or tapper goods still only come due in Winter. Those domains are not modelled yet, and the safe default is a late date rather than a guessed one. They are next.

## 0.15.0 - 2026-08-26

865 tests.

### Fixed

- **The Help Wanted board starts empty again.** Spring 1 was inheriting the quest from the day your run ended, gold reward included. Worse, it was rolled against your OLD progress, so it could ask for a fish or a monster the fresh farmer had no way to reach yet. A new year now opens with a clean board, exactly as a new save does.
- **The Saloon's Dish of the Day resets with the year.** Gus kept serving whatever he had cooked on the day the loop ended. A real first day has no dish yet, so neither does a rewound one; the new run's first dish arrives on day 2.
- **The Traveling Cart's year-one guarantee survives a rewind.** On saves created with the "year one completable" option, the first rewind used to switch that guarantee off permanently. It is re-rolled for the new run instead.

### Added

- **`tly_netstate` debug command.** Prints every world-state field the loop reset is responsible for, so a reset can be checked field by field instead of by eye. Read-only.

### Internal

- These three came out of a one-time audit of every piece of world state the rewind was leaving behind, rather than being found one player report at a time. That whole class of leak is closed now. Ruling table: `docs/superpowers/2026-08-26-networldstate-field-rulings.md`.
- Removed three no-op date assignments in the reset path, and synced the world state from the game's own statics after the calendar rewind so a night event cannot restore the pre-reset run seed.

## 0.14.2 - 2026-08-26

865 tests.

### Fixed

- **Shop Discount now changes the price on the shelf.** It used to take the money off at the till, so shops still showed full price - and worse, the game checks whether you can afford the FULL price before it charges you, so the discount never extended your buying power. Tool upgrades are deliberately not discounted, and neither are buildings or animals (they never were); the upgrade text says so now.
- **Festivals work again in later loops.** 0.14.1's once-per-day rule leaked across a rewind: because the calendar goes back to Spring 1, loop 2's Egg Festival landed on the same day number and got refused. A rewind means the festival has not happened yet, so every loop gets its festivals back. Once per day still holds inside a loop.

### Added

- **New Features section in the mod settings.** Turn individual parts of the mod off or tune them without editing config.json: festival time-flow, the one-minigame-per-day rule, theme re-rolls, donating tools and rings, the weekly goal JP multiplier, and starting gold. Changes apply straight away.
- **A short "Is this a bug?" section on the mod page**, covering the things reported most often that are working as designed: the cave prompt that replaced Demetrius' cutscene, the one-item Traveling Cart, and the once-per-day festival rule.

## 0.14.1 - 2026-08-26

Two fixes from emmalution's stream. 853 tests.

### Fixed

- **Festival minigames run once a day.** Because a TLY festival does not end your day, you could walk out of the festival and back in and the whole thing would start over, host and all. The Egg Hunt could be run three times in one afternoon, the Luau soup tasting the same way. Each festival's main event now happens once per day; the stalls, the shop and everyone at the festival still work on a repeat visit.
- **Weekly goals never ask for more than a bundle can take.** Bundles that only need some of their listed items could hand you three goals in a bundle that needs two. With 0.14.0 requiring a real donation per goal, that third one was impossible and the week could not be completed. A bundle is now only ever asked for what it can still accept.

## 0.14.0 - 2026-08-26

Fix release for the 0.12.17 shrine regression, plus two fixes from player reports. 839 tests.

### Fixed

- **The Junimo Shrine (JP perk) screen never opened on a Fail night** (Nexus 1123181, SincerelyZoey +
  SilencedLink). Regression from 0.12.17: the keep/reshuffle hold prompt put a question in front of the
  shrine, and the shrine was opened from inside that question’s answer callback. Vanilla still has the
  DialogueBox up at that moment, so the launcher refused to open over it and the night fell through to
  the reset with no shop shown (and no JP spent). The shrine open is now deferred a tick and drained by
  the day-28 watchdog, the same fix the hold re-ask and the pity offer already use. A shrine that still
  cannot open now logs a warning instead of failing silently. Banked JP was never lost.
- **Weekly theme goals could tick without you donating anything** (@ggrace67, via emmalution's
  stream). Vanilla marks every ingredient slot in a bundle as filled the moment that bundle
  completes, so in a bundle that only needs some of its listed items, finishing it with the others
  ticked your goal, paid the weekly JP and lifted the drawback for free. A goal now needs an actual
  deposit into that slot. Goals you have already finished this week are kept.
- **No way to get another pet after letting one go** (Nexus post, rose1729). Declining Keep Pet left
  you with no pet and no offer of one, ever: the rewind re-marks the pet cutscene as seen and puts
  the year back to 1, which is what vanilla's adoption option keys off. A loop that ends with no pet
  on the farm now re-opens the Adopt option at Marnie's counter, at 0 hearts like the animals.


## 0.13.0 - 2026-08-25

Season pity (opt-in Junimo offer after five fails at one season) plus the 2026-08-25 sweep fixes.
830 tests; live-smoked on a throwaway save (TODO.md tables). Engine (TLY Custom) boards only.

### Added
- **Season pity** (spec `docs/superpowers/specs/2026-08-25-season-pity-design.md`). Fails are counted per
  season gate. The first 5 fails at a season are standard difficulty; from the 6th, keeping the board
  lowers that season's quota by 10% per extra fail (floor 50%), and reshuffling leaves the hardest
  eligible items out of the roll (2 per extra fail; quality asks go first). Passing a season drops its
  count back to 5. Season Goals title shows "eased Nx". Config `PityEnabled`, `PityThreshold`,
  `PityQuotaStep`, `PityQuotaFloor`, `PityTrimPerStep` (GMCM section "Season pity"). Debug `tly_pity`.
  TLY Custom boards only.

- **The easing is an offer, not automatic.** After the keep/reshuffle question on a Fail night where
  the season has been failed more than the threshold, the Junimos ask whether to use their power to
  make the town's requests easier. Yes applies the easing for the path you chose (kept board: lower
  quota; reshuffled board: hardest items left out) and costs JP on the same curve as the hold (first
  free, then `PityCosts` 50/100/200/300, reset by declining). No means a standard board. Debug
  `tly_pity accept|decline`.

### Changed
- The Traveling Cart merchant no longer mentions the Junimos (only the farmer and the Wizard can see them).

### Fixed
- **Quality asks only on items that can carry quality** (Nexus 1122358 follow-ups: gold Fiber, gold
  River Jelly, silver Tea Leaves). The engine now derives which items the game itself gives quality
  to (crop harvests, rod-caught fish that are not jellies, spawned forage in a forage category) and
  never asks for silver/gold on anything else, including curated forage additions such as Tea Leaves
  and bush drops. `tly_genbundles` lists every quality ask on the board.
- **Keep Pet keeps every pet** (Nexus 1122901). A second pet from Marnie used to be the only one that
  survived a reset; all pets are snapshotted and restored (old single-pet saves migrate at their next reset).
- **No bundle or weekly goal asks for Pierre's year-2 crops until you can grow them.** Garlic and
  Artichoke stay out of every pool until you own Pierre's Special Order; Red Cabbage until you own
  that or the Cultivation: Red Cabbage upgrade. On run 1 the only source of those seeds is a shrine
  upgrade, so a Garlic weekly goal was unwinnable by construction (Jeff, 2026-08-25 smoke).
- **Traveling Cart cap is per day, not per view** (Nexus post, lexihope). Buying an item used to pull
  the next item in the merchant's list into the freed slot; the day's selection is now remembered, so a
  purchase leaves a gap until tomorrow. The Cart Whisperer preview locks in the same selection, so what
  it shows is what the cart sells.

## 0.12.18 - 2026-08-24

### Fixed
- **Void Salmon removed from the bundle pools** (Nexus 1122358 follow-up). `WitchSwamp` joins the built-in excluded location markers and `(O)795` the built-in excluded ids: the Witch's Swamp is behind the Dark Talisman quest, which is post-CC, so the 0.12.16 "hard but fair" ruling was wrong. Existing saves get the change at their next reset.

## 0.12.17 - 2026-08-24

Keep-bundles hold (spec `docs/superpowers/specs/2026-08-24-keep-bundles-hold-design.md`). 750 tests;
live-smoked on a throwaway save. Applies to Engine (TLY Custom) boards only; Vanilla boards are unaffected.

### Added

- New: on a Fail night the Junimos ask whether to hold the town's wishes (keep the same bundle board for the next loop) or let time reshuffle them. The first hold is free; holding again in a row costs 50, 100, 200, then 300 JP (config `BundleHoldCosts`). Reshuffling resets the price.
- New: the day-1 Junimo speech says up front that impossible-looking asks are expected and can be held across a rewind.
- Season Goals title shows how many times the board has been held.

### Changed
- Text: removed all em dashes from in-game strings.
- Debug: `tly_hold keep|reshuffle|status`.

## 0.12.16 — 2026-08-24

All four bugs from the 2026-08-24 feedback sweep, root-caused and fixed same day (covers
0.12.12 – 0.12.16). Verified by unit tests (724) and an agent-driven live playtest on the
deployed build.

### Fixed
- **Engine bundles rolled Ginger Island / Qi-gated items** (Nexus 1122358; SincerelyZoey,
  IshoMoogoo, gazumbrado). Location markers could not catch them — crops derive from Data/Crops
  (no location field) and the metals/cooking/geode pools scan all of Data/Objects. Structural
  built-in exclusions (`ItemPoolBuilder.BuiltInExcludedItemIds`) now vet Qi Fruit, Pineapple,
  Taro Root/Tuber, Banana, Mango, Ginger, Magma Cap, Radioactive Ore/Bar, Cinder Shard, Dragon
  Tooth, Fossilized Skull, the five island dishes and Piña Colada; BugLand (Mutant Bug Lair —
  Dark Talisman is post-CC) joins the excluded-location markers, so Slimejack is out. Void
  Salmon stays (hard, not impossible — design ruling). Algae/Seaweed can no longer receive
  silver/gold quality asks (the game never gives them quality). (0.12.12, 0.12.16)
- **Config-override trap:** SMAPI's ReadConfig replaces serialized list defaults wholesale, so
  exclusions that lived only in tuning defaults were inert on any install with a saved
  config.json. All structural exclusions moved into code; the tuning lists are pure extension
  points; regression-tested against emptied lists. (0.12.16)
- **Weekly themes asked for out-of-season fish** (Nexus 1122423; spenderg, lexihope — Pike in a
  Spring theme). The CcItem catalog treated every fish as year-round; the new `SpawnSeasonMap`
  feeds it real fish/crab-pot spawn seasons from the engine pools. (0.12.13)
- **Advanced Options: selecting Remixed soft-locked the OK button** (Nexus 1122619;
  SincerelyZoey). The patch located vanilla's dropdown apply-callback positionally, but AGO
  header rows use the Default element style — off by one: the wrong callback was replaced,
  vanilla's 2-entry capture stayed live and threw on Remixed (index 2). The callback is now
  found by closure inspection (`DelegateClosures`), which also un-breaks the silently-eaten
  Year1Completable checkbox. (0.12.14)
- **Junimo Shrine bought every affordable tier of an upgrade in one press** (Nexus 1122027;
  spenderg). One gamepad A press dispatches both `receiveGamePadButton` and a synthesized
  `receiveLeftClick` in the same tick; after the first buy the next tier slid into the same row
  slot and was bought too. Same-tick purchase guard. (0.12.15)

## 0.12.11 — 2026-08-21

Release candidate of the 0.12 line after the beta: the bundle-source choice, the cult-upgrade
repricing (measured), the curated quota ramps, and the small follow-ups from the beta feedback.
Covers 0.12.1 – 0.12.11.

### Added
- **`BundleSource: Engine | Vanilla` (0.12.9–0.12.11).** The new-game Advanced Options
  "Community Center Bundles" dropdown offers **TLY Custom** (default), **Normal** and **Remixed**.
  The choice is stored per save: TLY Custom = the engine writes its own board every loop; Normal /
  Remixed = the game's own board, regenerated the same way on every reset (`Game1.bundleType` is
  persisted in the mod's meta — the root cause of Nexus bug 1108030 "remixed comes back vanilla").
  Vanilla mode reads-and-classifies the live board and re-classifies on DayStarted when another
  mod rewrites it (Challenging Community Center Bundles swaps bundle values each morning). GMCM
  dropdown; a config flip takes effect at the next reset. Diagnostics `tly_bundlesource`.
- **Pierre's Special Order** (`pierre_year2_seeds`, 10,000 JP): Pierre stocks Garlic / Red Cabbage /
  Artichoke seeds from year 1 (Data/Shops edit while owned). (0.12.7)
- `tly_jpbudget` diagnostics: the maximum JP one loop's board can pay out — "donate ASAP" and
  "strong player" models plus a ceiling — with a per-season breakdown and impossible-gate
  detection. Measured on five loops: strong player 8.0–9.5k JP, fixed awards 1,933
  (`docs/superpowers/notes/2026-08-21-jp-budget.md`). (0.12.5–0.12.6)
- Advanced Options screenshot on the mod page (khauser13). (docs)

### Changed
- **Cult repricing (0.12.7, user ruling).** `cult_red_cabbage` 750 → 5,000 JP; `cult_starfruit`
  removed (the desert needs no RNG); the 10,000-JP Pierre upgrade is the sure thing.
- **Curated quota ramps (0.12.8).** Twelve remix/authored pick-X-of-Y bundles whose derived
  schedule demanded a donation before any item could exist (or was plainly harsh/lax) now have
  hand-set ramps: Winter Star `[0,0,0,2]`, Forager's `[0,0,2,2]`, Gil's Trophies `[0,0,1,2]`,
  Brewer's / Preserver's / Home Cook's Feast / Artifact `[0,1,2,4]`, Mineral `[0,1,3,4]`,
  Fish Farmer's `[0,0,1,2]`, Four Seasons Sampler `[1,3,4,5]`, Rare Crops `[0,0,1,1]`, Garden
  `[1,2,4,4]`. Gil's Trophies draws from the 7 year-1-feasible trophies (Slime Charmer, Napalm,
  Knight's Helmet, Arcane Hat dropped).
- **`EnableNonObjectDonations` governs the next board only (0.12.4).** The weapon/hat donation
  patches stay live while the live board has (W)/(H) slots, and the engine-manifest check tries
  the opposite flag before falling back — the beta caveat is gone.
- Empty weekly theme: hub card "Themed donations completed", HUD "Themed donations completed -
  drawback lifted." (0.12.2–0.12.3, Bumblewyn).
- Version scheme: plain semver, no prerelease tags (0.12.1).

### Fixed
- The CcItem catalog only covered a bundle's first X ingredients — the remaining Y−X items had no
  rarity/season data (weekly-theme sampler treated them as Common + year-round). Every concrete
  ingredient is catalogued now. (0.12.9)
- Grape (Summer forage, Fall crop) read as Fall-only; crop and forage seasons are unioned. (0.12.9)
- Boards read from the game (Vanilla mode, pre-engine saves, bundle mods) get the same
  obtainability clamp on Percentage ramps the engine path had. (0.12.9)

## 0.12.0-beta.1 — 2026-08-21

Public beta of the 0.12.0 line. Consolidates the 0.11.61–0.11.111 dev line: the
owned-bundle engine (three plans), the economy/clarity pass, and the bugfix pass
on everything reported against 0.11.60.

### Added
- **Owned-bundle engine (0.11.69–0.11.100).** TLY writes its own Community
  Center board at run-create and every reset, seeded per loop, from the vanilla
  + remix pools. Picked bundles re-roll their slot contents from pools derived
  from the game's own data (season-valid crops/forage, habitat-matched fish,
  monster loot, metals, artisan goods) — SVE-proof by construction. Eleven
  authored bundles join the pools (Artifact, Mineral, Book, Tapper's, Four
  Seasons Sampler, Orchard, Preserver's, Home Cook's Feast, Weatherman's,
  Gil's Trophies, Recycler's). Weapon/hat donations (`EnableNonObjectDonations`
  kill-switch). Vault asks +25%. `ExcludedLocationMarkers` config for SVE/Island
  exclusives. `tly_genbundles` / `tly_classify` / `tly_trophytest` diagnostics.
- **Economy/clarity (0.11.61–0.11.68).** Season-checkpoint JP award
  (150/250/400), donation JP single-pay, `xp_mult` upgrade family (5 skills ×
  ×2–×5 + the ×10 "Junimo Insight" capstone), hub line for the season
  multiplier.
- `LimitTravelingCartStock` (config + GMCM) — turn off the one-item Traveling
  Cart cap. The cap is explained in-game on the first cart visit and on the
  mod page.

### Fixed (0.11.101–0.11.111, the 0.11.60 bug reports)

### Fixed
- **Community Center completion ceremony never played** (Joja stayed open, Pierre
  stayed closed on Wednesdays, the JojaMart lightning never struck). The mod
  suppressed event 191393 thinking it was the Spring-5 CC intro; 191393 is the
  ceremony. The intro (611439) is what's suppressed now. Affected saves recover
  on the next sunny day you enter Town.
- **Museum rewards only came once per profile** (Ancient Seeds + recipe, the
  artifact statues, Singing Stone, geodes). 1.6 tracks those on the farmer's
  `specialItems` lists, which the reset now clears.
- **Caroline's Tea Sapling event didn't replay after a reset** — the replayable-
  cutscene scan now recognises letter-delivered unlocks (`mail` / `mailToday`).
- **Cultivation upgrades never fired for Mixed Seeds** (and Summer Seeds grew Red
  Cabbage instead). The patch now sits on the actual Mixed Seeds path.
- **It never rained, Rain Totems did nothing, CJB said "the game forces sun".**
  The schedule is now written for *tomorrow* each morning so totems, CJB and
  console weather set later in the day stick; and the schedule itself has
  vanilla-like density (Spring/Fall 5 rain + 2 wind, Summer 3 rain + 2 storm,
  Winter 10 snow) instead of two wet days a season.
- **Junimo Stash lost anything put in on day 28** — the chest is banked right
  before the world rewinds, not only on save.
- **Kept coop/barn came back without its hay hopper** — kept buildings are now
  initialised the way construction does it.
- **Kept fishing rod lost its bait/tackle** (and kept tools their enchantments,
  watering cans their water).
- **A fail-night overnight event (owl/UFO sound, meteorite, fairy…) could swallow
  the Junimo scene and skip the rewind entirely**, leaving you on Summer 1. Fail
  nights now skip the overnight event, and the scene re-arms if anything
  replaces it.

### Added
- `LimitTravelingCartStock` (config + GMCM) — turn off the one-item Traveling
  Cart cap if you'd rather have the full vanilla cart. The cap is now explained
  in-game the first time you visit the cart, and documented on the mod page.

## 0.11.60 — 2026-07-14

Localization release: the mod is now fully translatable. Consolidates the
0.11.45–0.11.60 dev line.

### Added
- **Full i18n support (0.11.46–0.11.60).** Every player-visible string moved to
  `i18n/default.json` (SMAPI translation framework): the upgrade catalog
  (hand-authored rows keyed by id, generated rows via token templates),
  themes/modifiers/category labels, all seven self-drawn menus, weekly/stash/
  shrine quest text (including the composed objective checklist), HUD messages
  and question dialogues (with explicit plural variants), GMCM options (live
  language switch), the onboarding mail, furniture display names (re-injected
  on locale change), the Day-1 intro speak lines, and the Day-28 cutscene.
  English output is byte-identical to 0.11.44. Guard tests fail the build on
  missing/orphaned keys or broken `{{tokens}}`. `docs/TRANSLATING.md` documents
  the translator workflow — a translation is now a single JSON file.

### Fixed
- **World-state keep/wipe audit (0.11.45).** A one-time audit of every
  world-level (`netWorldState`) field the loop reset touches; closed the
  remaining "survives the reset" leak class with an explicit keep/wipe ruling
  per field.

## 0.11.44 — 2026-07-13

The big fix release: weekly goals redesigned around real bundle slots, remixed
bundles fully supported, the loop reset made airtight, and new upgrades.
Consolidates the 0.11.1–0.11.44 dev line. Changes since 0.11.0:

### Fixed
- **Weekly goals redesigned: slot-based checklists (0.11.12–0.11.23).** Each goal
  now names a specific still-open bundle slot (item, stack, quality — e.g.
  "Parsnip x5 (gold) — Quality Crops") and ticks only when that exact slot
  completes in live CC state. Kills three reported bugs at once: a single item
  could clear a x5-stack goal; themes could demand items with no matching open
  slot (structurally impossible weeks); goals could ask for items already
  donated. Fewer open slots → shorter checklist; zero → no quest that week and
  the drawback auto-lifts. The 1.5× banking bonus is slot-strict. Mid-week saves
  migrate with a one-time goal re-roll.
- **Remixed bundles all count (0.11.11).** Bundles matching no classification
  rule were silently dropped from season checkpoints and weekly themes — the
  gate shrank on the RECOMMENDED remixed config, and one report won a loop with
  a bundle still open. Unknown pick-X-of-Y bundles now classify with a derived
  cumulative quota ramp (custom-bundle mods included); nothing is skipped.
- **Reset-leak audit — the loop reset is now airtight (0.11.24–0.11.28,
  0.11.37–0.11.40).** Museum donations and lost library books, worn
  boots/rings/trinkets (and the trinket slot itself), monster-slayer kill
  progress, consumed mine milestone chests, power books / mastery / prize
  tickets, and max health/stamina all rewind with the year. Run-scoped stats
  are now wiped by default with an explicit keep-list, so future game versions
  can't silently leak progression across loops.
- **Your clothes survive the loop (0.11.41).** Hat, shirt, and pants stay worn
  through a reset — they carry no stats, and the wipe left farmers in their
  underwear with no way back to their look. Boots, rings, and trinkets still reset.
- **Kept buildings rebuild where you put them (0.11.42, 0.11.44).** Coop, barn,
  and silo keeps snapshot their position before the reset and rebuild exactly
  there (footprint cleared of regenerated debris), matching the stable's
  behavior — previously they landed on fixed tiles, one of which hid the silo
  behind the farmhouse.
- **Green rain is back in summer (0.11.26).** The weather scheduler was
  overriding vanilla's green-rain day; it's now reserved like a festival day,
  storm/rain minimums still hold, and forecasts (TV + Weather Sage) show it.
- **A reset no longer drags the old day's weather into Spring 1 (0.11.43).**
  Resetting mid-storm left lightning flashes, a storm HUD icon, and serialized
  storm state on the new Spring 1; the reset now re-resolves the day's weather
  through the game's own day-start path.
- **The farm cave asks again each loop (0.11.1).** Entering the cave offers the
  mushrooms / fruit bats / decide-later choice fresh whenever unchosen, instead
  of replaying the Demetrius scene (which only ran once, locking the first pick
  in forever).
- **Big-chest mod compatibility (0.11.35–0.11.36, 0.11.39).** Better Chests and
  Unlimited Storage no longer inflate the 4-slot Junimo Stash into a full chest
  grid; BC also no longer bulk-stashes into it or carries it away.
- **Horse fixes (0.11.21).** The horse no longer asks to be renamed every
  morning after a loop reset.
- **Theme picker polish (0.11.20, 0.11.22–0.11.23).** A pick can no longer be
  lost to a stale deferred offer; the quest tip moved below the checklist.

### Added
- **Keep Silo upgrade (0.11.27)** — 150 JP, Buildings; requires building a silo
  that run. Hay does not carry over.
- **Cart Whisperer I–V (0.11.5–0.11.10)** — Foresight chain; on Traveling Cart
  days the shrine planning view flags which of the cart's stock can feed a
  Community Center bundle (each tier previews more slots, gated on Cart Stall).
- Unattended-verification debug tooling (0.11.30–0.11.34): `tly_loadsave`,
  `tly_classify`, title-screen command bridge.

### Changed
- All reset paths route through one shared finalizer (0.11.2), so debug resets
  exercise the exact production path.

Fixes from this week's beta reports, plus a donation-JP rebalance. Consolidates
the 0.10.1–0.10.5 dev line. Changes since 0.10.0:

### Fixed
- **Theme-picker soft lock (0.10.4).** Quitting on the first day of a new season
  before completing it could reload into a weekly theme picker with no options and
  no way to close it. The save was written before the month rollover ran, and the
  load path's blind season sync erased the mismatch that triggers the rollover —
  last month's theme picks survived, accumulated past four, and eventually excluded
  every theme from the weekly offer. The load path now performs the month rollover
  itself (clearing month state and consuming the day-28 pre-pick), and as a backstop
  an empty offer skips the week instead of opening an unclosable menu — which also
  self-heals already-affected saves.
- **Dupe drops keep their quality (0.10.5).** The extra-item weekly bonuses
  (mine_drops_up / all_drops_up / tree + clump + monster paths) cloned drops by id
  only, always at base quality. The debris diff now carries `Item.Quality` /
  `Debris.itemQuality` through to the clone. Fish and hand-picked forage dupes
  already carried quality.
- **Vault money slots can no longer mint JP (0.10.2).** The donation observer's
  per-slot diff could treat a paid Vault bundle's gold amount as an item count
  (up to ~26,000 JP for the 25,000g vault) when the menu rebuilt mid-session.
  Money ingredients are now excluded from the per-item path; the Vault pays only
  its intended gold-scaled award.

### Changed
- **Donation JP rebalance: single-item slot awards (0.10.3).** A completed bundle
  slot awards the rarity JP of ONE item regardless of the slot's required stack —
  99 wood pays Common×1, not Common×99. The stack is an acquisition cost, not a JP
  multiplier; season scaling, weekly bonus items, and JP Boost apply unchanged.
  Bundle, room, and weekly-goal completion bonuses are now the dominant JP source.
- **Replayable-cutscene detection generalized (0.10.1).** Unlock-granting cutscenes
  are auto-detected from `Data/Events` instead of a hardcoded id list, so other
  mods' unlock scenes (e.g. Stardew Valley Expanded's) re-fire each loop the same
  way vanilla's do. Adds the `tly_dumpreplayable` debug audit command.

## 0.10.0 — 2026-06-09

A stability pass on the season-end gate and loop reset, plus fixes from beta reports.
Consolidates the 0.9.7–0.9.41 dev line. Changes since 0.9.6:

- **Season-end gate, part 1 (0.9.20).** Finishing every goal no longer occasionally
  resets you anyway — the item-donation ledger is reconciled from the Community
  Center's bundle state at day's end, so a missed deposit can't read as a failure.
- **Season-end gate, part 2 (0.9.37).** Failing the 28th no longer advances you to the
  next season. Completing the bus-repair Vault on day 28 queued the overnight bus
  `WorldChangeEvent`, which raced the loop reset; the rewind-doomed scene is now
  suppressed on a fail and the cutscene defers behind it on a pass.
- **Double theme pick on reset (0.9.25).** The reset presented the weekly theme picker
  twice and discarded the first pick — now persisted before the deferred reload.
- **Remix-aware Vault gate (0.9.26).** The bus-repair money bundles are renumbered under
  remixed bundles; indices + gold are now derived from live bundle data, so the gate
  can be satisfied. Season Goals also restyles the bus-repair line as a real list row
  (0.9.28–29).
- **Artisan goods keep value through the Junimo Stash (0.9.19).** Smoked/preserved fish
  and all flavored goods (wine, jelly, aged roe, honey, bait…) preserve identity +
  price across a reset.
- **Villagers stay out of the abandoned CC during a run (0.9.21).**
- **Mine elevator locks on reset (0.9.38).** Floors reached last loop are no longer
  accessible unless the keep-elevator upgrade was bought (cap-not-grant).
- **Weekly goals name the egg color (0.9.43).** A "Large Egg"/"Egg" goal shows
  "(Brown)" or "(White)" in the quest log — the two colors are distinct CC items, so
  the goal names which it wants instead of leaving the player to guess.
- **In-progress Clint tool upgrade no longer survives a reset (0.9.30)** as a free upgrade.
- **Removed the stale vanilla "Rat Problem" quest during a run (0.9.41).**
- **Week-1 special-weather guarantee (0.9.18).** Each season is guaranteed a special
  weather day in week 1, replacing vanilla's always-on day-3 rain.
- **Clearer Junimo Shrine wording** — the planning view states JP is spent on reset/win.

## 0.9.1–0.9.6 (earlier betas, shipped)

- **0.9.6 — SMAPI update notifications.** Added the Nexus update key to the manifest,
  so SMAPI now tells you in its console when a new version of The Longest Year is
  available. (Also wires up automatic Nexus uploads on each GitHub release — no
  player-facing change.)
- **0.9.5 — Fixed: loading a non-TLY save fired the intro cutscene.** The dormant
  gate from 0.9.3 bailed correctly, but the intro / day-28 cutscene drivers (and a
  warp tracker) are attached at startup with their own update loops and bypassed it,
  so the Lewis→Junimo intro still played on a save TLY didn't start. They now respect
  the per-save activation gate.
- **0.9.4 — Fixed: the Community Center bulletin board (Mixed room) did nothing.**
  Vanilla gates the bulletin board behind three completed bundles (unlike the other
  five rooms, which open immediately); TLY revealed the note but never patched that
  gate, so pressing it was a no-op. It now opens from day 1 like the rest.
- **0.9.3 — Safety: TLY stays fully dormant on saves it didn't start.** Loading a
  normal (non-TLY) save with the mod installed used to activate the full roguelite
  layer — including the day-28 world reset. Now only starting a NEW game begins a run;
  any other save is left completely untouched (no effects, HUD, or reset loop).
  Existing runs migrate automatically.
- **0.9.2 — Fixed: the weekly theme picker was lost when starting a new loop from
  the win screen.** The "Start a new loop" choice is a question dialogue; its answer
  callback ran the reset and tried to open the planning hub while that dialogue was
  still the active menu, so the open was refused — and the week was marked "offered"
  before the open was confirmed, so it never re-fired. The hub now marks the week
  presented only on a confirmed open and retries the deferred open each tick once the
  menu surface clears. Also hardens against the other "menu busy" cases.
- **0.9.1 — Win-screen copy** reworded to "You have restored the Community Center.
  The valley is saved!" (The jarring win → JP-shrine transition is deferred to the
  real 1.0 ending — see `TODO.md`.)

## 0.9.0 — 2026-06-01

First public beta. Feature-complete for v1 ("prove it's fun & stable on PC").
The focus for this beta is feedback on **difficulty, pricing, and pacing**.

### The loop
- Roguelite year-loop over the Community Center restoration: per-season donation
  minimums; falling short unwinds the year to Spring 1; completing the Center
  within a year breaks the loop.
- **Junimo Points** earned from donations (scaled by rarity and a per-season
  multiplier), banked across loops.
- **Junimo Shrine** JP shop, surfaced on every loop reset and on a win, with
  upgrades that carry strength forward: skill levels, tool tiers, recipes
  (Cookbook/Craftbook), buildings, backpack, starting gold, a kept pet, and more.
- **Weekly themes** — each week grants a paired bonus + liability; chosen at the
  weekly planning hub.
- **Season Goals tracker** above the CC fireplace; **Junimo Stash** chest and
  **Cookbook/Craftbook** carryover surfaces on the farm.
- Continue-after-victory: keep playing a won run or start a fresh loop.

### New-game intro
- A two-scene intro plays before you take control: Lewis on the farm porch, then
  a Junimo inside the Community Center, who frames the loop in the land-spirits'
  own terms (community, sharing the land's bounty, and what does — and doesn't —
  carry across a reset). Implemented as a single engine-played event that moves
  between locations, then opens the theme picker.
- The vanilla intro is skipped and its toggle hidden; the farm type is forced to
  Standard. Both are managed on the character-creation screen.

### Quality of life
- Season Goals menu auto-completes its intro quest on first open and sorts
  completed bundles to the bottom.
- The starter parsnip gift box is granted only on the first loop, not re-dropped
  on every reset.
- `forage_yield_up` grants its bonus on pickup (Gatherer-style), with no
  duplicate forage spawned overnight.

### Known limitations
- PC only; Standard farm only; new saves only; multiplayer untested.
- Intro cutscene and dialogue are a first pass.

### Debug commands (console)
`tly_addjp`, `tly_addmoney`, `tly_buyupgrade`, `tly_reset`, `tly_replayintro`,
`tly_openshop`, `tly_openhub`, `tly_set{board,cookbook,craftbook,stash}`, and
others. Intended for testing/setup, not normal play.
