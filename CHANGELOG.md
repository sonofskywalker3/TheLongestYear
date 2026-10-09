# Changelog

All notable changes to **The Longest Year** are documented here. This project
aims to follow [Semantic Versioning](https://semver.org/).

## Unreleased

### Added

- **The Year One Ending.** Finishing the Community Center now ends the year the next morning, whatever the date: a sunny day, the whole town on the hall steps, a villager who half-remembers you (assembled from what your save actually records), Morris closing the Pelican Town store for greener ground, the Junimos' warning, and one candle at grandpa's shrine. Loop again resets on the spot; Keep playing gives you the rest of the year to prepare and a "Year 2 is coming" notice on Spring 1. Replaces the win card. Vanilla's own completion ceremony no longer plays.
- **The darkness sends its agents.** From Summer, something dark strikes the farm at night: crows kill crops, a thief from the mines raids a chest, in Fall a shape in the Community Center undoes a donation, and in Winter a cloud over the valley taints an item the board was waiting for. The first strike of each kind in a loop plays its own short silent scene in the night, where the fairy and the witch would come. When something else already owns that night (a wedding, a birth, the bus repair or another Community Center repair, a wildcard night that promised something would happen on the farm), the strike waits: nothing is taken and no scene plays, and it comes back on the next night that is free, with its scene. If a scene can't play at all on your game (something it needs is missing, or another mod breaks it), you still get hit that night, just without the scene, and the scene can still play the next time that kind strikes. Later strikes of that kind in the same loop show only the morning message. A scene can't be skipped the first time you ever see it; after that a click skips it.
  - **The crows.** Red-eyed crows swoop in from both sides onto your field and peck the crops dead, each pecking in its own time. A scarecrow does nothing to them: one crow perches right on top of it.
  - **The thief.** A Shadow Brute walks in to one chest, opens it and takes from it, then looks back at you and runs. Trees, bushes and buildings in front of the chest go see-through while he is there. Everything he takes is stolen, food included; nothing spoils. One chest per night, never the Junimo Stash or a Mini-Shipping Bin. Your Junimo Chests count as one chest, since they share their contents: what the thief takes from one is gone from all of them. It plays on the farm, in a shed, the cellar or the farmhouse (with your family asleep in their beds). Until you have seen him once in a loop he only goes for chests there; after that, a chest anywhere else (a barn, a coop, the greenhouse) can be hit too, without a scene.
  - **The hall.** The Community Center at night, its windows lit like firelight, and one dark figure at work behind the glass. Shane, walking home past the hall, sees it, jumps and hurries off. Nothing on screen says which bundle was hit.
  - **The cloud.** The valley map in its Winter colours, in the map's own frame, while a dark cloud drifts down from the north and settles over everything, thickest on your farm.
- **The morning after a strike, a message box tells you.** The darkness's morning messages show in the same corner box as vanilla's "spreading weeds" message, one after another, after any menu or scene that is up: "Some strange crows visited the farm last night. They didn't eat your crops, but 3 have withered away.", "Some of your things have gone missing overnight: 6 Parsnips, 1 bottle of Wine and 3 other things.", or the feeling that something is wrong at the Community Center, followed by what is gone and from where: "The Parsnips are gone from the Spring Crops bundle."
- **The bundle menu marks what the darkness took.** A slot the hall emptied has a dark purple glow around its item in the bundle menu until you fill it again.
- **Every kind of strike lands at least once a loop.** If the crows or the thief haven't come by the 15th of Summer, or the hall by the 15th of Fall, they come on the first night after that they can, which leaves two weeks to recover. A Ward of the Fields still keeps the crows away for its season, nothing stored means no thief, and nothing donated means nothing to undo.
- **Witnesses.** Linus saw the crows and Shane saw the hall. For a week after the scene, the first time you talk to him he tells you about it, once, every loop. If you have never talked to him, that day he only introduces himself, and he tells you the next day.
- **The Junimos tell you what was tainted.** After a Winter tamper, the first time you arrive on the farm, whichever way you come in (the farmhouse door, the bus stop, the forest, a totem), two Junimos meet you on the farmhouse porch, then you are back where you came in. They name the exact item the darkness took, flavour and all ("It has tainted all the Dried Apples"), say the darkness has touched them (or "it", for something like Wood or Honey), then what to bring instead. Item names in their ask read naturally: "Bring us a Nautilus Shell", "Bring us Clay", "3 jars of Honey", "3 bottles of Wine", "3 Parsnips", "7 Pike", "7 Oysters", "7 Sea Cucumbers", "8 sprigs of Holly".
- **A tamper only takes an item one bundle asks for.** It picks an item no other bundle wants, so "all the X" is always true. When that bundle asks for it twice (Construction's two Wood), every open slot of it changes to the same new item and amount; a slot you already filled stays filled. Its replacement is something new: never an item the board already asks for, and never one that was already tainted, in any flavour. A weekly goal on that slot changes with it, so the quest asks for the new item.
- **Tainted items glow.** From the morning after a Winter tamper until the loop ends, every copy of the tainted item has a dim purple aura wherever it is drawn: your inventory, chests, shops, the bundle menu and held over your head. It is the exact item: tainted Dried Apples glow, Dried Cucumbers don't.
- **The Junimos meet you at the season gates.** On Summer 1, Fall 1 and Winter 1 after a passed gate, the Junimos are on the porch when you first step onto the farm. Summer warns that something dark has noticed the work (with a different last line once you have rewound), Fall says it grows stronger as the days shorten, and Winter says this is the last season and the darkness may now attack directly, under a purple glow that holds to the end of the scene.

### Changed

- **The season-gate Junimos wait for you outside.** The porch scene on Summer 1, Fall 1 and Winter 1 no longer plays the moment you wake, before you are even out of bed. The morning starts as usual (the planning hub opens in the farmhouse), and the Junimos meet you on the porch the first time you arrive on the farm that day, whichever way you come in, then you are back where you came in. If you don't set foot on the farm that day, they are still waiting next time you do.
- Vanilla's Community Center completion ceremony no longer plays; the ending owns that slot. The win card and its keep-playing prompt are gone.

### Fixed

- **No Junimo hides behind your things in a porch scene.** The Junimos at the season gates and after a tamper used to stand on fixed spots below the porch, so the Junimo Shrine statue could hide one completely and a chest could crowd another. They now stand only where nothing covers them or stands right above them, evenly spaced either side of you where they can.

### Debug

- `tly_win` (now arms the ending), `tly_ending [speaker <Name>]`, `tly_eventstep`, `tly_year2wall`, `tly_answer <n>`, `tly_dumpsprite <Name>`.
- `tly_sabotage scene crows|thief|hall|cloud` and `tly_sabotage scene [old] [new] [mass]` (play a darkness scene now), `tly_sabotage fixture [scarecrow] [rows=<n>] [here]`, `tly_sabotage aurachest`, `tly_sabotage breakscene <crows|thief|hall|cloud|off> [pick|setup]` (break a scene on purpose), `tly_witness list|peek|show|talk|click|arm|fresh` (`talk` runs a real conversation, `fresh` makes a villager unmet again); `tly_sabotage status` lists the scenes played and seen, the kinds struck and still owed this loop, and the pending witness lines.

## 0.19.20 - 2026-10-08

3209 tests.

### Changed

- **Items a rare pond product used to date now have their real routes.** The 0.19.19 pond rule left some vanilla items with no route at all. New readers, all from game data or the game's own code: wild trees (Data/WildTrees plus the trees standing in the world: Acorn, Maple Seed and Pine Cone at week 1, Wood at week 1, Sap from chopping oaks), the beach tide pools past the broken bridge (Coral and Sea Urchin, from Beach.cs; week 1 with the 300 Wood bridge), geodes from mine stones (MineShaft.checkStoneForItems: Geode floors 1 to 39, Frozen Geode week 2, Magma Geode week 3, Omni Geode below floor 20 at week 1), and Squid Fest rewards (Pearl for 5 squid on Winter 12, Treasure Chest for 7 squid on Winter 13, both week 14). Coconut and Tom Kha Soup keep week 9 with lower effort. Pools and reachability are unchanged. With SVE and Cornucopia, SVE Birch Seed and Fir Cone get week 1 and Cornucopia Date Palm Seed week 9 from their wild trees.

## 0.19.19 - 2026-10-08

3189 tests.

### Changed

- **A fish pond product too rare to turn up in a run no longer dates an item.** Every Data/FishPondData ProducedItems row used to count as a route whatever its Chance, so SVE's Goldenfish pond (Golden Pumpkin at Chance 0.01) put Golden Pumpkin at week 5 instead of the Spirit's Eve maze in week 12. A row now counts only when its Chance is at least 0.10 on Easy and Normal Item Rarity and 0.05 on Hard and Extreme (Jeff's ruling), read for every pond, vanilla or modded.
- Vanilla items that lose a pond route (`tly_dumpmodel` before and after, unmodded):
  - Every step: Treasure Chest week 5 to unknown (13); Nautilus Shell week 5 to 13 (Winter beach forage); Pale Broth week 5 to 6 (the dish); Magma Geode week 8 to unknown (13); Pearl week 16 to unknown (13).
  - Easy and Normal only: Acorn, Maple Seed, Pine Cone week 5 to unknown (13); Coral week 5 to unknown (13); Sea Urchin week 5 to week 1 (its Spring pin, no longer rejected by the pond floor); Cactus Seeds week 13 to unknown (13, effort 8 to 6).
  - No pool or reachability changes.
- With SVE: Golden Pumpkin week 5 to 12 (Spirit's Eve maze) at every step; Slime Egg week 5 to 6 (Slime Egg-Press); Iridium Sprinkler week 5 (Meteor Carp pond) to unknown.

## 0.19.18 - 2026-10-08

3164 tests. Release of 0.19.11 to 0.19.18.

### Docs

- README and the Nexus description: What's New in 0.19.18 keeps the 0.19.10 content and adds one sentence to the other-mods warning: a quick way was found to cut down how many impossible mod items show up, but some will still appear. Beta line set to 0.19.18.
- `release-notes/0.19.18-nexus-changelog.txt`; README and description backups as `release-notes/README-0.19.18-backup.md` and `release-notes/nexus-description-0.19.18-backup.bbcode`. The two older Nexus description backups, `release-notes/_tly-desc-before-0.18.144.bbcode` and `release-notes/_tly-desc-before-0.19.10.bbcode`, are now committed.

## 0.19.17 - 2026-10-08

3164 tests.

### Docs

- `docs/mod-support-unknowns.md` rewritten after the mod-support pass (0.19.11 to 0.19.16): what was fixed and by which reader, what is still unknown and why (Farm Type Manager forage, Custom Bush bushes, SpaceCore spawn groups, Spring fruit trees, maps no door reaches, Cornucopia's year-2 seeds), and the placements checked and left as they are (Birch Syrup, Amber, Golden Pumpkin with SVE). With SVE and Cornucopia, 240 of 429 modded items are unknown, down from 283.

## 0.19.16 - 2026-10-08

3164 tests.

### Fixed (mod support)

- **SVE's flagged artifacts get a week from their own dig spots.** Boomerang, Faded Button, Fossilized Apple, Old Coin, Rusty Shield and Stone of Yoba list their dig spots in their own Data/Objects ArtifactSpotChances, not in Data/Locations, and carry ExcludeFromRandomSale, so neither the spot rule nor the catalog-pool fallback saw them: unknown, week 13. An artifact (Type "Arch") nothing else places is now dug from its own spot chances on maps the walked weeks can date: all six week 3, the artifact floor.
- A fallback only, so every placed item keeps its week and effort, and a non-artifact with spot chances (vanilla's Lost Book) is left to its own rules. Vanilla unchanged: `tly_dumpmodel` before and after on an unmodded game is identical.

## 0.19.15 - 2026-10-08

3161 tests.

### Fixed (mod support)

- **A crop whose seed is first sold late waits for the seed.** The crop rule read Data/Crops seasons and growth only, plus a ruled table for vanilla's festival, cart and Oasis seeds, so SVE's Gold Carrot (seed sold only by the Desert Trader) read as a week-1 Spring harvest. A crop with no ruled row whose seed is first sold after week 1 (`ShopWeeks`) is now planted no earlier than that week, in a season it can still finish in. Gold Carrot: week 9, hard week 6 (Hard), 3 (Extreme), the desert's weeks.
- With Cornucopia, six crops whose seeds only Sandy sells and whose seasons end before the desert opens in pacing (Agave, Aloe, Bamboo, Blue Agave, Lemongrass, Sugarcane) go from too-early weeks (2 to 6) to not placed. A seed sold from week 1, or by no walkable shop (Mixed Seeds, a mod's own framework), keeps the season arithmetic.
- Vanilla unchanged: `tly_dumpmodel` before and after on an unmodded game is identical (the ruled seed rows win for Beet, Rhubarb, Starfruit and Cactus Fruit).

## 0.19.14 - 2026-10-08

3157 tests.

### Fixed (mod support)

- **Fruit from modded fruit trees gets a week.** Only vanilla's six orchard fruits had one (a ruled table), so SVE's Nectarine and Persimmon and Cornucopia's Fig, Grapefruit, Pistachio, Almond, Walnut and the rest read as unknown. New rule from Data/FruitTrees: the sapling is bought the first week a walkable shop sells it, the tree matures in 28 days, and it fruits from the first week after that in one of its seasons, the same arithmetic as the ruled rows. With SVE and Cornucopia: Summer trees week 5 (Nectarine, Avocado, Ume, Nutmeg), Fall trees week 9 (Grapefruit, Pistachio, Almond, Cashew, Pecan, Walnut, Pomelo, SVE Persimmon), Winter trees week 13 (Fig, Yuzu, Camphor Leaves, Cinnamon Sticks). Dishes made from them follow (Nectarine Fruit Bread week 6).
- **Not placed, on purpose:** a Spring tree (SVE Pear, Cornucopia Lemon and Lime) first fruits next Spring, and the Traveling Cart route that puts Apricot and Cherry at week 13 sells only vanilla fruit; a sapling no walkable shop sells (Cornucopia's Durian, only at the island trader) gives no week.
- New `ShopWeeks`: the earliest week a walkable shop sells an item, from the same Data/Shops read as the reachability rule (item queries, year-2 lines closed), each shop dated by its owners' maps, its OpenShop tiles, or the map the game opens it at in code (Desert Trader, island trader, Volcano shop, resort bar). `tly_dumpmodel` lists it.
- Vanilla unchanged: `tly_dumpmodel` before and after on an unmodded game is identical.

## 0.19.13 - 2026-10-08

3145 tests.

### Fixed (mod support)

- **With Stardew Valley Expanded, Holly, Crocus and Crystal Fruit are Winter forage again, not week 1.** SVE gives its Grampleton Suburbs forage rows for them (and Daffodil, Sweet Pea, Blackberry) with no season. The forage rule read that map by name, found no gate and called it open from Spring 1, so a Spring gate could ask for Holly. Forage rows are now dated by the walked map weeks (`LocationWeeks`, 0.19.12): a row in a map no door leads to does not place the item. With SVE: Holly, Crocus and Crystal Fruit week 13, Sweet Pea week 5, Blackberry week 9, the same as vanilla; Baked Berry Oatmeal follows its Blackberry to week 9.
- `tly_dumpmodel` also lists every map the walk reached, with its three weeks.
- Vanilla unchanged: `tly_dumpmodel` before and after on an unmodded game is identical.

## 0.19.12 - 2026-10-08

3141 tests.

### Fixed (mod support)

- **Stardew Valley Expanded's fish get a real week instead of the unknown week 13.** SVE flags every one of its fish ExcludeFromRandomSale, which keeps an item out of random shop stock and out of TLY's bundle pools; the availability model only placed pooled fish, so a board that asked for a Bull Trout, Minnow or Tadpole (Tech's Cross-Mod Bundles, a TLY Custom board with mod items on) treated it as Winter. Fish the pool leaves out only for that flag are now placed from their own Data/Locations rows and Data/Fish row, never sampled. Bull Trout week 5, Minnow, Tadpole, Starfish and Puppyfish week 1, Radioactive Bass week 7 (the Sewer), and so on. Dishes and fish-pond goods made from them follow (Frog Legs, Big Bark Burger).
- **A fish is placed only from maps TLY can date.** New `LocationWeeks`: the maps' own warps and door warps, walked one way from the farm, give each map the latest gate on its easiest path (a vanilla gate still dates a map no door leads to, such as the Sewer). A row in a map no door leads to (SVE's Highlands, Junimo Woods, Forbidden Maze) neither places the fish nor widens its seasons, so the Highlands Bass, Gemfish, Fiber Goby, Alligator, Swamp Crab and Diamond Carp stay unknown rather than week 1. Island, Fable Reef and Crimson Badlands fish stay out as before.
- Vanilla unchanged: `tly_dumpmodel` before and after on an unmodded game is byte-identical (every item, all four difficulty steps, pools, pins, reachability). With SVE installed, some vanilla items gain an SVE fish-pond route (Golden Pumpkin from a Goldenfish pond, week 5).

## 0.19.11 - 2026-10-08

3129 tests.

### Debug

- `tly_dumpmodel [fileName]` writes the whole item availability model as a TSV in the mod folder: every Data/Objects id (plus the guild reward ids) under all four difficulty steps (placed, pacing week, hard week, gate, effort, source, basis), every engine pool's membership, the derived season pins, the reachability verdicts and the generated dish bases. Read-only; the models are built on the side. Made for before/after comparisons of a rule change (the vanilla-unchanged check of the mod-support work) and for listing every modded item still at the unknown week.

## 0.19.10 - 2026-10-08

3129 tests. Release of 0.19.2 to 0.19.10 (the live Nexus page was on 0.19.1).

### Docs

- README and the Nexus description: What's New in 0.19.10 covers everything since 0.19.1 (TLY Custom boards vanilla-only by default, the per-save Allow mod items in custom bundles option, a fresh Tech's Cross-Mod Bundles board each loop on Normal and Remixed, the loop's board and repair swaps surviving reloads) and says plainly that turning the option on, or playing Normal or Remixed with other mods, can ask for items that are impossible to get until Jeff tunes for specific mods. Beta line set to 0.19.10. PixxiePerfect added to Thanks.
- `docs/mod-support-unknowns.md`: the SVE and Cornucopia items TLY can't place yet, saved for the mod-support phase, with a pointer in TODO.md.
- `release-notes/0.19.10-nexus-changelog.txt`; README and description backups as `release-notes/README-0.19.10-backup.md` and `release-notes/nexus-description-0.19.10-backup.bbcode`.

## 0.19.9 - 2026-10-08

3129 tests.

### Fixed

- **An unfilled bundle slot no longer changes item when you reload.** The load-time board repair (which swaps an ask this run cannot reach, such as Cornucopia's Zucchini when its seed is out of reach) drew its replacement from the run seed, which a new game only sets after that first repair, and walked the board in whatever order the live data happened to be in. Its swaps also never reached the stored board of record, so with Tech's Cross-Mod Bundles each load restored the unreachable ask and the repair drew again, often a different item (loop 1: Fall Crops took (O)300/(O)266 at creation, (O)284/Cornucopia Bamboo after a reload). Now the repair seeds from the board's own basis (farmer id and bundle seed loop, the same on every load of a loop), walks bundles in key order, and writes every swap into the stored board, so the next load restores the repaired board and the repair finds nothing to do.
- **Same seam on TLY Custom.** A repair swap on a TLY Custom board (possible with "Allow mod items in custom bundles" on) used to leave the stored board unrepaired, so the next load saw the live board as different, failed the seed re-derivation too and fell back to "engine manifest mismatch ... falling back to read path" for the rest of the loop (with Tech's mod, the restore also put the unreachable ask back each load). The stored board now takes the swap, so the stored-board check matches.

## 0.19.8 - 2026-10-08

3124 tests.

### Fixed

- **Loop 1 of a new Normal or Remixed game with Tech's Cross-Mod Bundles keeps its board across reloads.** 0.19.7 stored the board of record only at a reset, so in the first loop a plain reload let that mod write its saved board back with nothing restoring it (a theme week discount, Adventurer's 99 to 74, came back as 99). The new-game load now stores the live board right after Tech's handler and before TLY's own load-time edits, and the unstackable-ask clamp and the week discount keep it current from then on. Only with Tech's mod loaded, on the host, and never over a board already stored. An existing Normal or Remixed save still in loop 1 with no stored board is not adopted at load (that mod has already rewritten its board by then, so storing it would lock in the wrong board); it picks this up at its next reset. Spec addendum 3 updated.

## 0.19.7 - 2026-10-08

3118 tests.

### Fixed

- **With Tech's Cross-Mod Bundles installed, reloading a save no longer swaps TLY's board for an older Tech board.** That mod writes its own saved board over the live one every time a save loads, and TLY's save right after a reset does not fire the save event it listens to, so its saved board never learned TLY's. On Normal and Remixed the reload wiped the fresh reroll and TLY's difficulty pass, capped-ask clamp and reward shuffle, and every loop after the first night replayed one raw Tech board. On TLY Custom, from loop 2 the reload replaced TLY's board with Tech's ("engine manifest mismatch ... falling back to read path"). Now TLY is the board of record whenever that mod is loaded: a Normal or Remixed reset stores the final board it wrote (a kept Fail-night board too) next to the TLY Custom board it already stored, TLY's save-load work runs at low priority after other mods' handlers, and before it repairs or classifies anything it writes back every bundle where the live board differs and logs one line ("Tech's Cross-Mod Bundles rewrote the board on load; restored this loop's board"). Without that mod nothing changes: same event order, no stored Normal/Remixed board, no restore, and other bundle mods (Challenging CC Bundles) keep the detect-and-reclassify path. Tech's own saved board is left alone; it only feeds bundle display names, which it cannot change (spec addendum 3). A Normal or Remixed save from 0.19.6 picks this up at its next reset. Decision logic in `Core.TechBoardOfRecord`, unit-tested with a fake board.

### Docs

- README and the Nexus description: What's New in 0.19.7 adds a line about reloads keeping the loop's board with Tech's mod. Spec addendum 3.

## 0.19.6 - 2026-10-08

3105 tests.

### Changed

- **Normal now rolls a fresh Tech's Cross-Mod Bundles board each loop too, exactly like Remixed.** In 0.19.5 only Remixed asked that mod for a new board at each reset, and Normal got the same Tech board back every loop. Now both do; TLY Custom, a save without that mod, and a board kept on a Fail night still skip it. The one warning logged when the mod's code is not where TLY expects it, or it fails, no longer names Remixed: "Tech's Cross-Mod Bundles changed; this loop uses the game's own board."

### Docs

- README and the Nexus description: What's New in 0.19.6 and Switching bundle source later now say that with Tech's Cross-Mod Bundles, both Normal and Remixed roll a fresh board each loop. Spec addendum 2 updated to match.

## 0.19.5 - 2026-10-08

3103 tests. Rolls up 0.19.4 to 0.19.5.

### Added

- **Remixed rolls a fresh Tech's Cross-Mod Bundles board each loop.** Tech's Cross-Mod Bundles (Nexus 51035) builds its board only when a save is created and serves that board as the game's bundle data from then on, so Normal got the same Tech board back every loop (which is what Normal means) and Remixed mixed the old Tech board with the game's remix. On a Remixed save with that mod loaded, each reset now asks it for a new board right after the game rebuilds the world, before TLY's own Remixed passes (difficulty, capped asks, reward shuffle) run over it. A board kept on a Fail night is restored as it was, never rerolled. If the mod's code is not where TLY expects it, or it fails, the log gets one warning and the reset goes on with the game's remix. The call is reached by reflection behind a small adapter (`ITechBundlesRerollTarget`), and the decision and fallback are unit-tested with a fake.

### Docs

- README and the Nexus description: What's New in 0.19.5 covers both changes, and Switching bundle source later says that with Tech's Cross-Mod Bundles, Normal keeps the same board every loop and Remixed rolls a fresh one.

## 0.19.4 - 2026-10-08

3093 tests.

### Added

- **Allow mod items in custom bundles.** A new per-save setting next to Bundle source in GMCM (config `AllowModItemsInCustomBundles`). Off, TLY Custom boards ask only for vanilla items and give only vanilla rewards, as in 0.19.2. On, they work as before 0.19.2 and take other mods' items into their pools, templates, rewards and board repair. It is off on new games; a save from before this version has no value stored and starts with it on, so it keeps the behavior it was created with. Like Bundle source, the title-screen value only sets what a new game starts with, and a change on a loaded save applies at its next loop. The board on disk records which value built it, so the load-time check re-derives it the same way after a mid-loop toggle, and a board kept on a Fail night keeps its value. Normal and Remixed are unaffected.

## 0.19.3 - 2026-10-08

3082 tests. Rolls up 0.19.2 to 0.19.3.

### Changed

- **TLY Custom boards ask only for vanilla items.** Items from other mods never enter a TLY Custom board: not its item pools, not the bundle templates it draws from (a bundle mod's bundle has its modded items taken out, and a position left with nothing gets vanilla's own bundle), not the reward pool, and not the replacements the board repair picks. A modded bundle reward is swapped for vanilla's reward at that spot. Balancing every item of every mod is not possible, so the balance now holds whatever else is installed. Category slots ("any fish") still take a modded item when you donate one. Normal and Remixed are unchanged and keep working with other mods, bundle mods included. The vanilla item list is read from the unmodded game (1.6.15) by a new tool, `tools/vanilla-ids`, and is re-run after a game update. A board you already have keeps its bundles until your next loop. Asked about by PixxiePerfect (Nexus posts, 2026-10-07), on Tech's Cross-Mod Bundles.

### Docs

- README and the Nexus description say plainly that other mods are not officially supported, that TLY Custom boards use vanilla items only, and that Normal or Remixed with other mods may not reshuffle each loop and may ask for items you can't get.

## 0.19.1 - 2026-10-07

### Fixed

- **The Bundle Log shows the same item counts as the Community Center.** The Log read its counts once, when the save loaded, while the theme week discount lowers a week's goal lines on the board and puts the un-donated ones back when the week ends. A save loaded during a discount week kept the discounted count in the Log after the Community Center went back to the full ask, and picking a theme mid-session showed the reverse. The Log now reads the counts from the live board each time it opens. Reported on Reddit (2026-10-07).

## 0.19.0 - 2026-10-07 - The Randomizer

### Added

- **Randomizer section in the settings menu (GMCM).** Ten options that trade the shipped balance for variety. Every option is off by default, so a player who never opens the section plays the game exactly as balanced. The weekly options take effect from the next weekly offer, so whatever the current week rolled stays as rolled; Random bundle rewards take effect at the next loop. Everything a week rolls is saved with the run, so a reload gives the same week. Suggested by Nijah (Nexus posts, 2026-09-28), whose thread on reroll repeats, fixed items and fixed pairings started it.
  - **Theme rerolls** (Off / Costs JP / Free). Adds the Re-roll Themes button to the planning hub. Costs JP: the first reroll each week is 50 JP and each one after costs double (50, 100, 200 and so on), back to 50 each week. The price is flat, not scaled by season. The button shows the price, and a reroll you can't afford is refused without charging you. Free: no cost. A rerolled offer and the pairs already shown are saved for the week, so closing the hub no longer throws away a pair you paid for.
  - **Random theme items.** Weekly goals are drawn evenly from every line the theme can ask for, instead of favoring items the season gate needs and easier items early in the year. The rules that stop impossible goals still apply: the item must be obtainable that week, a bundle is never asked for more than it can take, the fruit-tree and crab-pot caps hold, and the week asks for the usual number of goals.
  - **Random pairings.** Each card keeps its theme's buff and draws a random drawback, fixed once offered. A theme never draws a drawback that blocks its own goals: Foraging never gets foraging off, Farming never slower crops, Fishing never slower bites, Mining and Spelunking never closed mines, Artisan never slower machines.
  - **Random multiplier.** Each card shows its own JP multiplier, 0.5x to 1.5x in steps of 0.05. It applies only to the JP from that theme's goals: the weekly bonus and donations into goal slots. Other JP is unchanged.
  - **Mystery card.** About one week in four, one of the two cards is dealt face down. It shows only its multiplier, always 1.25x to 1.75x, and its theme, buff, drawback and goals are revealed when you pick it. A reroll on a mystery week keeps one card sealed. Never on a double week.
  - **Double theme week.** Once a season, in week 2 or week 3, the hub says "Double week" and you take both cards: both buffs, both drawbacks and two goal lists in the quest log. Each list lifts its own drawback when done, and both themes count as used for the month.
  - **Wildcard days.** One random day each week (never a festival, never the last day of the season) gets a one-day twist. The day shows in the quest log from the start of the week; the twist is revealed that morning with a HUD message and shown in the quest log and on the statue's Active tab. Good: every foraged item gives one extra, fish bite 30% sooner, watered crops grow an extra day, shops 25% off, or luck as good as it gets. Bad: the mines are closed, fish bite 30% slower, sell prices 25% lower, or energy drains 50% faster. Odd: snow out of season, one of the game's own night events on the farm, stumps, boulders and logs back on the farm, or a rockslide that blocks the path to the mines. Snow never replaces a green rain day.
  - **Random shrine donations.** Tops the week's goals up with items that have no Community Center slot. The total goal count follows the Required slots dial (Easy 3, Normal 4, Hard 5, Extreme 6). On Easy and Normal Item rarity the items are obtainable that week; on Hard and Extreme, that week or any earlier point this loop. Stacks follow the same rules a bundle slot would, and each pays what a Community Center donation of that item would, times the goal bonus and the card's multiplier. Donate them on the farm statue's new Donate tab. Missing them never fails a season, but that theme keeps its drawback for the week.
  - **Random bundle rewards.** Each loop, every bundle's reward, the Vault's included, is drawn at random from any bundle's reward list, with no value matching. The Vault still asks for the same gold. Applied when the board is built, on every bundle source.
  - **Random cart days.** Each day of the week gets its own chance of a Traveling Cart visit, about two a week, and never none unless every day that week is a festival. Festival days are skipped, and the Night Market keeps its own boat cart.

### Changed

- **The "Allow re-rolling the weekly themes" switch moved into the Randomizer section** as Theme rerolls. A config that had it on becomes Free; off becomes Off.

### Fixed

- **Dried Fruit slots no longer ask for grapes.** The Dehydrator turns grapes into Raisins, not Dried Fruit, so a Dried Grapes ask could never be filled. Reported by Treedomy (Nexus posts, 2026-10-05). (0.18.145)
- **A festival that ends while someone is talking no longer freezes the night.** When a festival closed itself at its end time with a dialogue box open, the game could think the dialogue was still up and the next night never finished.

## 0.18.144 - 2026-10-05

2856 tests. Rolls up 0.18.140 to 0.18.144.

### Added

- **Keep Special Orders Board** (Junimo Upgrades, Buildings, 1,500 JP). The Special Orders board outside Mayor Lewis's house is open from Spring 1 of every loop. You can buy it once the board has opened in a loop (Fall 2). Suggested by elaineofshalott.
- **Extreme opens the desert in Spring week 3.** With Item rarity on Extreme, the bus counts as repaired by Spring week 3, so Spring Crops can ask for Rhubarb and desert items can turn up in Spring. Hard and the other settings are unchanged. Suggested by elaineofshalott.

### Fixed

- **Fish bundles ask for fish from their own water.** Lake Fish could ask for Catfish and Woodskip, and Specialty Fish for Herring. Lake, River and Ocean Fish now pick only fish that live in that water. Specialty Fish picks hard-to-reach fish: Secret Woods, desert, mine and Night Market fish, the legendaries, and the hard short-window catches (Pufferfish, Octopus, Super Cucumber), with at most one Night Market fish. Fish bundles never ask for a jelly. A board you already have keeps its fish until it re-rolls. Reported by Nerlana.
- **Special orders start fresh every loop.** An order taken in one loop carried into the next with its old due date, the board kept last loop's offers, and one-time orders you had finished never came back. Each rewind now drops orders in progress and puts every town order back on offer.
- **The recipe book picker steps back instead of closing.** Pressing X, Escape or B on the recipe list now goes back to the slot list. Before, it closed the Cookbook or Craftbook and started the run. Reported by gmastern1.
- **The crop fairy turns wild seeds into forage.** Wild seeds the fairy grew stayed half-grown and all harvested as Wild Horseradish. They now become seasonal forage on the spot. A stuck crop already in your save fixes itself overnight if you leave it. Reported by sigyn2002.

## 0.18.139 - 2026-10-02

2783 tests. Rolls up 0.18.136 to 0.18.139.

### Fixed

- **Season crop bundles only ask for what the season can grow.** Spring Crops could ask for Rhubarb or Coffee Bean, and Summer Crops for Starfruit, which you can't get in time for that season's deadline. They now pick only crops you can have by then. If your current board already has one, the amount due is lowered to what that season can supply when you load your save, and the rest is due by Winter.
- **Ginger Island fish stay out of bundles.** Weatherman's could ask for Stingray, and Lionfish or Blue Discus could turn up too, all island-only fish. Ginger Island and its modded counterparts are now always off limits, and a board that already asks for one gets it swapped for a reachable fish when you load your save. Reported by paigefromabook.

## 0.18.135 - 2026-10-02

2761 tests. Rolls up 0.18.119 to 0.18.135.

### Fixed

- **A dresser in the Junimo Stash keeps what's in it.** Hats, shirts, pants, furniture, wallpaper and flooring stay inside through the rewind. Anything else in it (rings, boots, fish, tools) has to come out first: the stash hands the dresser back with a message. A dresser stashed by an older version with other things inside gets them taken out into their own stash slots, or set down beside the stash if it is full. Reported by sarahwinchester97.
- **Stashed items keep their looks and stats.** Dyed shirts and pants keep their colour, tailored boots keep their stats, a Combined Ring keeps both rings and a trinket keeps its rolled stats. A fishing rod keeps its bait and tackle.

### Added

- **Keep Farm Decor** (Junimo Upgrades, Buildings, 500 JP). Start each loop with your paths, fences, gates, lamp-posts, torches, braziers, signs and decorations where you left them. Furniture is not kept. A stump or hollow log in the way is cleared if your kept axe could break it, and you get its hardwood; a boulder is cleared if your kept pickaxe could. A piece with no room goes to the stash. If the stash is full, it is left where it was blocked, so pick it up on day 1.
- **Keep Worn Gear** (Junimo Upgrades, Loadout, 1,000 JP). The boots, rings and trinket you're wearing when the year rewinds stay on you.
- **Keep Farmhouse Furniture** (Junimo Upgrades, Buildings, 250 JP). Your farmhouse furniture stays where you put it, dressers included. Anything inside it that isn't a hat, shirt, pants, furniture, wallpaper or flooring is wiped, so move it to the stash first. If your house is a different size after the rewind, furniture moves with its room. Furniture with no room in the new house is left by the front door. Pick it up on day 1.

## 0.18.118 - 2026-09-30

2576 tests. Rolls up 0.18.99 to 0.18.118.

### Fixed

- **The Sticky bundle asks for sticky things, and more of them.** It picked one item from every resource plus the tapper extras, so it could ask for a single Acorn, some Stone or Fiber. It now shows six sticky things and needs four (three on Easy, five on Hard, all six on Extreme): Sap, Maple Syrup, Oak Resin, Pine Tar, Honey, Jelly, Sugar, Slime, Ice Cream, Maple Bar, Cranberry Sauce and Miner's Treat, only what a year can reach. Amounts follow the season the bundle is due: Ice Cream asks 5 to 13 on Normal once the Summer stand sells it, and nothing appears before the season it can exist; Sugar asks less in Spring while money is short. Reported by elaineofshalott.
- **Bundle amounts follow what a year can actually produce.** A Sticky bundle asking for one Acorn started a check of every ask the board had no amount rule for, and most of the gaps are filled. Tree seeds (Acorn, Maple Seed, Pine Cone) and Moss, tree fruit, forage (Cave Carrot, Tea Leaves, Spring Onion, Salmonberry), trash, pantry goods (Sugar, Rice, Vinegar and the like), Hay, Prize Tickets and the rare fish (Goby, Stonefish, Ice Pip, Lava Eel, Slimejack and the three Night Market fish) now ask for amounts a year can reach, and cooked dishes scale where your kitchen, recipes and shops allow more than one (some dishes still ask for one). The Treasure Chest and the Home Cook's egg and milk asks have no rule yet. A dish is never asked for before you can cook it. Thanks to elaineofshalott for the Acorn report that started it.
- **Mystic Syrup is no longer asked for.** It only comes from a late-game reward, so it could never be a fair year-1 ask.
- **Prismatic Shard and Mystery Box are rare asks now.** On the boards The Longest Year builds, the bundles you need for the year hold a limited number of them, set by the Stack size setting: at most one on Normal, two on Hard and three on Extreme, none on Easy, and each is asked for one at a time. On the Normal and Remixed bundle sources (the game's own board) each is asked for one at a time, but the number of them is not limited. The Abandoned Joja Mart's own bundle still asks for one Prismatic Shard on every board. Helper's asks for one Mystery Box instead of five. Prismatic Shard is also no longer counted as a Spring item; it comes from deep Skull Cavern nodes, so it is a Fall-or-later ask.
- **The Night Market fish wait for Winter 15.** Midnight Squid, Spook Fish and Blobfish were asked for before the market opens. Moss is asked from Summer, when the trees have grown.

## 0.18.98 - 2026-09-29

2497 tests. Rolls up 0.18.85 to 0.18.98.

### Fixed

- **Restarting the year at the Junimo Shrine asks its questions first.** It used to end the day first, so you saw the shipping payout, any level-ups and the next day's date before being asked about your bundles and upgrades. Now the bundle question, the upgrade menu and the books come right after you say yes. Then the day fades out and you wake on Spring 1. Anything left in the shipping bin that night is not sold, since the rewind resets your gold anyway. Seen in Dummy Dog Ben's stream.
- **Qi Beans counted as a year-1 find.** The game's default artifact spot drops Qi Beans only while Mr. Qi's Qi Beans challenge is running, which needs Ginger Island. TLY read the row without its condition. Conditions that need a special-order rule only Mr. Qi's orders grant (read from the game's special orders, so mod orders count too) now close the row, for artifact spots, forage, fish and shop lines alike. Across 60 test boards nothing asks for a Qi Bean.

### Added

- **Keep Lost Books** (Junimo Upgrades, Carryover, 100 JP). Lost Books you have found stay found through a rewind, so artifact spots, fishing chests and the mines stop turning up ones you already have and you can dig for bundle artifacts instead. Suggested by tanky24u (Nexus posts, 2026-09-24).

### Changed

- **Picking a theme lowers that week's goals.** A goal that asks for more than 10 items asks for less for the rest of the week. With Stack size on Easy it asks for half, on Normal a quarter less, and on Hard or Extreme nothing changes. It rounds down and never goes below 10, so 31 Cauliflower becomes 23 on Normal. The planning hub shows the lowered counts before you pick. Finish a goal that week and it stays finished. A goal you have not finished goes back to its full count when the next week starts or when you pick a different theme. Reported by Nijah (Nexus posts, 2026-09-29): 31 Cauliflower in week 1 was too many.
- **Marnie brings a pet again after a rewind, and Keep Pet costs 50 JP.** Without Keep Pet, a rewind marked Marnie's pet visit as already seen, so it never came back and the only route was the paid Adopt option at her counter. A farm with no pet after a rewind now gets the visit again. Keep Pet drops from 75 to 50 JP. Seen in Dummy Dog Ben's stream.
- **The "Guarantee Year 1 Completable" checkbox says it does not affect TLY Custom bundles.** It only arms the Traveling Cart's Red Cabbage Seeds visit for the game's own board. It still works for Normal and Remixed, though TLY's cart-slot limit can hide the seeds that day.
- **Artifacts wait for week 3.** Every artifact, whatever finds it (dig spots, geodes, monster drops, fishing chests), now counts as available from week 3, giving geodes and dig spots time to turn up. A week-1 board could ask for Elvish Jewelry. Seen in Dummy Dog Ben's stream.

- **The Dye bundle asks for grown and gathered things.** It used to pick any object with the right colour tag, so across 60 test boards it asked for cooked dishes, artifacts, bombs, skill books, Joja Cola, Energy Tonic and a Qi Bean. It now picks only from the six vanilla Dye items (Red Mushroom, Sea Urchin, Sunflower, Duck Feather, Aquamarine, Red Cabbage), the common gems and crystals (Emerald, Aquamarine, Ruby, Amethyst, Topaz, Jade, Quartz, Fire Quartz, Frozen Tear; the deeper crystals still wait for their mine floors), and coloured crops, fruit, flowers, forage and beach finds. Each colour takes the game's own dye-pot shades, so gold counts as yellow, jade as green and aquamarine as blue. The same 60 boards now ask only for those. Seen in Dummy Dog Ben's stream (Elvish Jewelry and a book).

## 0.18.84 - 2026-09-29

2447 tests. Rolls up 0.18.82 to 0.18.84.

### Fixed

- **Weekly goals could ask for items before they could exist.** Seasonal bundles (Spring Crops and the like) skipped the obtainable-by-this-week check the other bundle kinds already had, so in any week of that season a goal could name an item that wasn't reachable yet: Strawberries (seeds go on sale day 13) or Cauliflower (12 days to grow) in Spring week 1, for example. Seasonal lines now wait for their week like everything else. Reported by Nijah (Nexus posts, 2026-09-29); also seen in Dummy Dog Ben's stream.
- **Week 1 asked for silver and gold quality crops.** A weekly goal could name a silver or gold line from day 1, when a gold Carrot is about a 1% roll. Lines that ask for silver or better are now held until week 3 of the year. Seen in Dummy Dog Ben's stream.
- **Long upgrade and boost descriptions ran off the screen.** Hover tooltips in the Junimo Upgrades, planning statue, weekly hub and Season Goals menus were one unwrapped line, so a long description (Sneak Peek) ran off the edge at larger UI scales. They now wrap. Seen in Dummy Dog Ben's stream.

## 0.18.81 - 2026-09-28

2442 tests. Rolls up 0.18.80 to 0.18.81.

### Fixed

- **A Normal-bundles save switched to custom bundles at its rewind.** The new-game bundle pick was copied into config.json, and every reset read config.json, so starting a TLY Custom game changed the board of every older save at its next rewind. Each save now keeps its own choice; the settings menu changes the loaded save's choice (the title-screen value is only the new-game default). Saves from before this keep the board they run under. If this already happened to your farm, set Bundle source back in the settings menu and your next rewind uses that board again. Reported by victoriatauanem (Nexus posts, 2026-09-28).
- **Ostrich Mayo from Blue Eggs and Golden Mayo reached the board.** The reachability check only read shops, seeds and recipes, and an item with no route it could read stayed allowed. It now reads Data/Machines (a good made only from one out-of-reach input is out of reach) and Data/FarmAnimals (an animal nobody sells, hatched only from the egg it lays, makes that egg out of reach), for every mod's items. Artifact spots count as proof, so the Dinosaur Egg stays. Checked in game with the mod installed: Ostrich Egg and Ostrich Mayo are the only two new exclusions. Reported by Ninjamaid (Nexus posts, 2026-09-28).

## 0.18.79 - 2026-09-28

2422 tests. Rolls up 0.18.77 to 0.18.79.

### Fixed

- **Re-roll Themes could show the same pair again and again.** A re-roll only drew from themes that could ask for two or more goals, so a week where exactly two qualified re-rolled that pair forever. A re-roll now offers any theme not picked this month that has at least one goal, never repeats a pair until every pair has been shown, and the re-rolled pair is kept for the week when the hub is closed. Reported by Nijah (Nexus posts, 2026-09-28).

### Added

- **Keep Fish Pond** (Junimo Upgrades, Buildings, 750 JP; unlocked once a Fish Pond is built this loop). The rewind puts one Fish Pond back where the player had it, finished and empty: no fish, no output, no population gates, no request, no Golden Animal Cracker. You skip the rebuild and the seaweed; carry a fish in the Junimo Stash if you want to restock. With several ponds, the one with the most fish is the one remembered (the first one on a tie). If that spot is taken on the fresh farm, the pond goes to (46,18) or the nearest free 5x5 around it; it is placed after the other kept buildings, the greenhouse and the stable, and never duplicates a pond the fresh farm already has. Requested by elaineofshalott (Nexus, 2026-09-27).

- `tly_reroll [count|reopen]` presses the planning hub's re-roll button, or closes and reopens the hub, for headless checks. (debug)

## 0.18.76 - 2026-09-27

2398 tests. Rolls up 0.18.73 to 0.18.76.

### Fixed

- **A finished season failed under Challenging CC Bundles.** CCCB swaps its board in on DayStarted and the base board back on DayEnding/Saving; it loads first, so the day-end ledger mirror read the smaller base board and the gate counted too few filled slots. OnDayEnding is now EventPriority.High and OnDayStarted Low. Separately, a "<Season> Crops/Foraging" bundle was classified Seasonal and demanded every slot; it now honors pick-X-of-Y (CCCB Spring Crops is 8 of 9). The Bundle Log said "needs 9" for that bundle and now says 8. Reported by ozzy2540 (Nexus, 2026-09-25).
- **Eggs in Foraging.** BuildForagePool took anything on any location's forage list, and Visit Mount Vapius spawns eggs on the ground. Categories -5, -6 and -18 (eggs, milk, animal products) are now skipped for Foraging; Animal and Chef's bundles still reach them. Reported by Thrippa (Nexus, 2026-09-25).
- **Item-query shop and spawn lines are read.** Data/Shops and Data/Locations ItemIds that are fixed-set queries (ALL_ITEMS, FLAVORED_ITEM, and keys other mods register) are resolved through the game's ItemQueryResolver with the line's PerItemCondition. Top-level YEAR clauses are judged for year 1: a year-2 shop line is a known but closed route, a year-2 spawn row is not read. Pierre's three year-2 seeds stay exempt. Cornucopia's 13 rare flowers (Spring Rose included) are now kept off the board; vanilla newly kept off: Tea Set and Animal Catalogue. Reported by Thrippa.

### Added

- **`tly_newgame` takes a bundle choice** (`custom` / `standard` / `remixed`) so headless runs can start on another bundle mod's board.

## 0.18.72 - 2026-09-25

2364 tests.

### Added

- **Herd Book.** A fourth carried book (sprite index 3 in `assets/books.png`), granted to every save including existing ones. 18 slots on a fixed ladder: one free Chicken slot, then `herdbook_1..17` in Junimo Upgrades (Carryover, chained, 600 to 2250 JP): Chicken, Cow x2, Duck x2, Goat x2, Rabbit x2, Sheep x2, Pig x2, Void Chicken, Golden Chicken, Dinosaur, Ostrich. A Chicken slot takes white, brown and blue chickens only. A registered animal is refreshed from the live farm just before the reset (an animal that is gone keeps its last snapshot) and rebuilt with its id, name, skin, friendship, happiness, age (at least adult), days owned, Golden Animal Cracker and reproduction setting; Herd Book animals move in before the Start-with animals, which then get whatever room is left. It needs its slot kind's building keep (Duck and Dinosaur: Big Coop, Rabbit: Deluxe Coop, Goat: Big Barn, Sheep and Pig: Deluxe Barn, Ostrich: Barn) and room; otherwise it is logged, gets a HUD line, and stays registered. Opens on a rewind night after the Craftbook when an empty slot has a fitting animal. Suggested by tanky24u (Nexus, 2026-09-24).
- **`tly_openherdbook`** and **`tly_herdbook list | record | register | remove | friend`** debug commands.
- **Herd Book rows match the Animals tab layout**, and were sized up 15% with their content centered in the frame, so a slot's animal is easier to read at a glance.
- **Room rule for keeps.** A Start with keep can't be bought if it would overflow the coop or barn you've kept, and Herd Book slots count toward that room too. A Herd Book slot only counts against other Herd Book slots, so the full Herd Book always fits inside one Deluxe Coop and one Deluxe Barn. The Junimo Upgrades menu greys out a blocked row with "Needs more coop room" or "Needs more barn room".

### Fixed

- **Start with an animal keeps could never be bought.** Nothing recorded the species you owned, and three gate names (Chicken, VoidChicken, Cow) never matched vanilla's (White Chicken, Void Chicken, White Cow). Species are now recorded from the farm on every DayStarted and Saving, normalized on both sides of the gate, so saves that stored the vanilla names match too. Once you have had a chicken, Start with Chicken is for sale after Keep Coop.
- **Start with Ostrich** needed Keep Deluxe Coop and put the ostrich in a coop; ostriches live in barns. It now needs Keep Barn.
- **Start-with animals ignored building capacity.** A full coop or barn is now skipped.
- **Gift of the Junimos room keeps could never be bought.** Keep Greenhouse, Keep Quarry Bridge, Keep Glittering Boulder and Keep Minecarts each check a different room's unlock flag, and the mod could not read any of them. They now unlock once you finish that room. Keep the Bus was fine. Reported by tanky24u.

## 0.18.58 - 2026-09-24

2159 tests.

### Added

- **Restart the year at the Junimo Shrine.** A button on the statue's planning view asks a yes/no, then ends the day at once and runs the Fail-night chain with the Junimo scene removed: keep-or-reshuffle question, Junimo Upgrades, Cookbook and Craftbook banking, reset to Spring 1. Nothing is paid out; JP is already banked. It counts as a loop for the loop number and for consecutive hold prices. Hidden on day 28 (the real gate owns that night), while an event, cutscene or festival is playing, and while another reset is running. Choosing No returns to the shrine. After Keep playing it clears the won-run flag so the next loop can be won. Suggested by tanky24u (Nexus posts, 2026-09-23).
- **`tly_restart`** debug command: presses the button headlessly (answer with `tly_answer 0` / `1`).

### Removed

- **Season pity.** The Junimos no longer offer to ease a season you keep failing (Jeff, 2026-09-24: "they can adjust the difficulty themselves"). A Fail night now goes cutscene, keep-or-reshuffle question, upgrade menu, reset. The quota cut on a kept board and the hardest-item trim on a reshuffle are gone, and a save that had either goes back to the standard board. The "Season pity" difficulty dial, the Season pity settings section, the `Pity*` config keys and the `tly_pity` console command are removed; old saves and old config.json files still load. The "Hold and pity prices" dial is now "Hold prices".

### Fixed

- **Fortune: Rare Fish's description says what it does.** It read "Rare fish catch chance increased by 25%", which is not how the upgrade works. It now reads "Your rod always works as if it has a Curiosity Lure." Reported by tanky24u.

## 0.18.50 - 2026-09-24

2202 tests.

### Added

- **Spring Returns, Summer Returns and Fall Returns.** Three new one-week boosts at the Junimo Shrine, 100 JP each. Each is sold once its season has passed this loop: Spring Returns from Summer on, Summer Returns from Fall on, Fall Returns in Winter. While one runs, that season's fish bite and its forage turns up again, on top of the current season's. Time of day, weather and fishing spots still apply, and the legendary fish are included, still once per loop.

### Changed

- **Seasonal items are back on their normal deadlines.** 0.18.47 made a seasonal fish or forage item due by the last season it could be found. The Returns boosts replace that: a Summer-only fish can be due at Winter again, and if you missed it, Summer Returns brings it back. Reported by tanky24u.
- **Rain Dance works in Winter.** A past season's rain fish can be caught in Winter with Rain Dance and a Returns boost. Snow still does not count as rain. Storm Call is still not sold in Winter.

### Fixed

- **Two descriptions swapped a line.** 0.18.47 took Artichoke out of the wrong description. Year-Two Seeds now lists only Garlic and Red Cabbage, and Pierre's year-2 seeds upgrade lists Artichoke again, which he does stock.

## 0.18.47 - 2026-09-24

2188 tests.

### Fixed

- **Crops sped up by a boost look ready when they are ready.** Growth Spurt, the Farming week bonus and Green Thumb added their extra day after the game's own nightly growth, so a crop they finished could be picked while it still looked half grown. Wild seeds were hit hardest, because they never turned into their forage. The extra day now comes first. Green Thumb also no longer makes a ready regrowing crop look unripe for a day. Reported by tanky24u.
- **A seasonal item is never due after its season ends.** A Summer-only fish like the Pufferfish could be due by Winter, so Summer and Fall let you through without it and the loop then failed at Winter, when it could no longer be caught. Every item is now due by the last season it can be found, so a missing Pufferfish shows up at the end of Summer instead of two seasons later. Reported by tanky24u.

### Changed

- **Year-Two Seeds is Spring and Summer only.** Fall Mixed Seeds already grow Artichoke a quarter of the time, so the Fall version only nudged that to about 29%. It can no longer be bought in Fall. Raised by tanky24u.
- **The upgrade menu has its own name.** The menu where you spend JP on keeps after a rewind is now called Junimo Upgrades, and the Shrine prices setting is now Upgrade prices. The Junimo Shrine is the statue on your farm.

## 0.18.43 - 2026-09-23

2183 tests.

### Fixed

- **The Difficulty setting can reach Hard and Extreme.** Moving the mouse over the list counted as picking, so the page redrew the moment the list opened and only Easy and Normal could ever be chosen. It now waits for your click, and the page stays where you were instead of jumping back to the top. Reported by goblinslayer66666.
- **Marlon no longer sells back last loop's items.** The rewind never cleared the list of things you dropped when you passed out, so the Adventurer's Guild could hand back an item from a previous loop. The rewind now empties it. Reported by Mycatisinapiano1528.
- **Cactus Fruit waits for the desert.** It could land on a board as a crop from Spring, because Cactus Seeds were missing from the list of seeds that wait for Sandy's shop. It now shows up from Fall, like the desert's other forage, or from mid-Summer on Hard. Reported by Mycatisinapiano1528.

### Changed

- **Four config labels no longer run under their controls.** They are now Better Start gift every loop, Replay mod unlock cutscenes, Gear slots in Gil's Trophies, and Item rarity (TLY Custom only). The tooltips are unchanged.

## 0.18.38 - 2026-09-22

2182 tests.

### Fixed

- **Mine carts and barrels refill when the year rewinds.** A new year in Stardew refills the coal carts and resets the barrels and crates in the mines, but a rewind never counted as a new year, so once you had emptied them they stayed empty for every loop after. The rewind now refills them the way a new year does. Treasure chests still only open once. Contributed by supercam19.

## 0.18.37 - 2026-09-21

2182 tests.

### Changed

- **Sneak Peek airs on Wednesday.** It used to air next year's episode on Sunday and quietly teach you that week's own recipe alongside it, so the television named one dish and gave you two. It now takes over Wednesday's rerun and shows next year's version of the episode that aired on Sunday, so the two are separate and you can see which one you are getting. You give up the reruns for the season you buy it in. Winter 28's episode has no Wednesday after it, so Shrimp Cocktail is no longer asked for by any bundle.

### Fixed

- **A season you had finished could still fail.** A bundle can only hold as many items as it has slots, but a season's goal could ask for more than that. The bundle went green, the goal still counted you short, and the season failed with nothing you could do about it. Boards from other bundle mods were hit hardest, because a bundle there can share a vanilla bundle's name while having fewer slots. A save already in progress repairs itself the next time you load it. Reported by ozzy2540.
- **Dried fruit and smoked fish now name the fruit and the fish.** These slots only read "Dried" or "Smoked", and nothing in game would tell you which one was wanted, not even Lookup Anything. A dried fruit slot now asks for a named fruit, Dried Apples for instance, and takes only that one. Smoked fish works the same way. Dried mushrooms still take any mushroom, and now say "Any Dried Mushrooms" instead of just "Dried". Reported by ShadowedAciexox.
- **The dried and smoked asks come down.** A dehydrator eats five fruit or five mushrooms for every dried one it makes, which the old numbers never counted, so a slot could ask for thirteen dried mushrooms and mean sixty-five mushrooms. These asks are now sized by what you have to gather rather than by how fast the machines run.

## 0.18.28 - 2026-09-17

2138 tests.

### Added

- **Cookbook and Craftbook start with four free slots.** Each book used to start empty until you bought its first tier. Now both hold four recipes from day one, each tier adds four (8, 12, 16), and a new fourth tier at 1200 JP takes them to 20. The first three tiers cost what they did. If your book already holds more than its new limit, every recipe stays banked and usable; the book only refuses new entries until you are back under the limit. (0.18.17 to 0.18.23; the grandfathering is `RecipeBanking.VisibleRows` /
  `IsOverCap` / `CanBank`, the ladder `UpgradeCatalog.BookBaseSlots` + `BookSlotsPerTier`.)

### Fixed

- **Dye and the other pools cannot ask for what a year cannot reach.** The Dye bundle asked for the Queen of Sauce Cookbook, which needs 100 golden walnuts. The colour pools behind Dye, and the pools behind the gem, resource, egg, milk, artifact, mineral, artisan and monster loot asks, now drop anything the mod cannot place inside a year, instead of one item being banned at a time. Reported by SilviaVA. (0.18.24 banned the books outside `AvailabilityWeeks.BookWeeks` from the
  colour index; 0.18.26 routed Dye and every by-kind bucket through the `Placeable` filter.)
- **A second visit to a festival lets you leave.** Going back into the Flower Dance on the same day left you stuck: the dance only runs once a day, and that clearing has no way out. When a festival's main event has already happened, the host now offers to let you leave instead. Reported by asteriaths. Vanilla has no persistent "already danced" flag: the partner is cleared every
  morning and the rewind clears friendship. (0.18.27)

### Debug

- `tly_bankrecipes <cook|craft> <n>` sets a book to an exact fill; `tly_festival mainevent`
  answers the festival host's start question with yes.

## 0.18.16 - 2026-09-16

2131 tests.

### Changed

- **Mine and monster asks follow measured yields.** A new debug sweep generated every mine floor
  from 1 to 120 and the first 40 Skull Cavern floors on seven different days, broke every stone,
  cracked every geode and killed every monster through the game's own drop code with a bare
  farmer, and counted what landed. The ore, geode and crystal numbers the balance already used
  came out right. Four did not, and now match the measurement: Coal asks go up (a week of mining
  yields three to four times what the old number said), Quartz asks come down by more than half,
  and Topaz, Jade, Aquamarine, Emerald and Ruby asks come down by about half. Monster drops that
  only one uncommon monster gives (Squid Ink, Crab Cakes, Cloth, Refined Quartz, the bars, the
  algae and a few others) are now capped at twice what clearing floors actually yields, instead of
  assuming a whole week spent hunting that one monster.

### Debug

- `tly_minesweep <from> <to> [samples] [crack]` (0.18.13 to 0.18.15): the measurement above,
  headless, nothing persisted.

## 0.18.12 - 2026-09-16

2131 tests.

### Added

- **Once-per-loop items ask for one, as a setting.** Some items a loop can only give once: the
  five legendary fish, the two books hidden around town (The Alleyway Buffet and Mapping Cave
  Systems) and the Golden Pumpkin from the Spirit's Eve maze. The Stack size dial used to turn
  their ask into two on Hard and Extreme, which no one can ever fill. A new Difficulty setting,
  on by default, holds every one of them at one whatever the dial says; off, they scale like
  everything else. A board already asking for two is lowered on load while the setting is on.
  The list was checked against the game data: every other book restocks, and weapons, hats and
  rings are already held at one by their own rule. Reported by FayGabi.

## 0.18.11 - 2026-09-15

2106 tests.

### Changed

- **The two hidden books get the time their routes take.** Both of the books hidden around town
  take real work to reach, so their bundle deadlines now sit in Fall and Winter rather than
  Spring, and on Easy the Book bundle never asks for them at all.

## 0.18.10 - 2026-09-15

2103 tests.

### Fixed

- **Festival contests and cutscenes stop the clock.** The ice fishing contest and the Egg Hunt run
  on their own real-time timer, and the Flower Dance, the Luau soup and the grange judging take
  the controls away. The in-game clock kept running through all of them, so the festival's end
  could pull you out in the middle of the ice fishing contest and leave you stuck holding the
  contest rod. Time now only passes while you are walking around the festival, and the end of the
  festival waits until any contest or scene is over. Reported by amaliekirstine.
- **Rings stay off the Dye bundle, and a ring a bundle does ask for can be donated.** The game
  tags the Amethyst Ring purple, so the Dye bundle could ask for it, and the donation menu then
  refused to pick it up because a ring is not an ordinary item. Rings are kept out of the colour
  bundles now, a ring on a board you already have can be picked up and donated, and Hard never
  asks for two of a ring. The setting called Allow donating tools and rings only ever controlled
  whether Gil's Trophies asks for a weapon or a hat, so it is now called Weapon and hat slots in
  Gil's Trophies and says what off does. Reported by FayGabi.
- **Friendship 101 is no longer in the Book bundle.** It is the ninth prize from the prize ticket
  machine, about 25 Help Wanted quests, and the ticket count starts over every loop, so no one
  reaches it inside a year. On Hard the Book bundle wants four of its five books, so that one book
  made the season unwinnable. The Alleyway Buffet and Mapping Cave Systems stay: those books are actually hidden around
  town, and can be grabbed each loop under the right conditions. Reported by FayGabi.
- **A new farm quit before its first night is still a Longest Year run.** The game saves a brand-
  new farm the moment you finish making your character, before this mod has written anything to
  it. Quitting on that first Spring 1 without sleeping and reloading gave you a farm with no
  books, no shrine, no Junimo chest and a locked Community Center. The run is now marked in that
  first save, and a farm you already have in that state is picked up as a run the next time you
  load it. Reported by FayGabi.
- **The Junimo stash works with Chests Anywhere.** You can name it, give it a category, and switch
  to another chest from it. The stash was opened in a way that hid it from Chests Anywhere, to
  keep Better Chests and Unlimited Storage from resizing it; that now only happens when one of
  those two mods is installed, and in that case the stash is simply left out of the Chests
  Anywhere list so you can never get stuck in it. Reported by asteriaths and FayGabi.

## 0.18.4 - 2026-09-14

2086 tests.

### Added

- **One Difficulty setting sets all ten dials.** A new Difficulty option sits at the top of the
  Difficulty section of the settings menu. Picking Easy, Normal, Hard or Extreme switches every
  dial below it to that level, and any single dial can still be changed afterwards. It is a setup
  shortcut, not a tier: nothing reads it for gameplay. Normal is the default, and an existing
  config keeps its dials as they are.

### Fixed

- **Gil's Trophies never asks for more than one of a trophy.** On Hard and Extreme the Stack size
  dial raised a trophy ask from one to two, but Gil gives each trophy only once and hats, the
  Insect Head and rings never stack, so the slot could never be filled and blocked the season.
  Anything that never stacks now asks for one at every step, and a board that already has a two
  on it is lowered the next time the save loads. Reported by ShadowedAciexox.
- **Quitting after a failed season's last night no longer skips the rewind.** Going to bed on day
  28 saves the game already dated to the next season, while the day-28 outcome (rewind, next
  season or win) only lived in memory until the morning scene. Quitting before it played and
  reloading carried the run into a season that was never earned. The outcome is now saved with
  the night, so reloading plays the missed scene. Reported by gmastern1.

## 0.18.1 - 2026-09-10

2063 tests.

### Changed

- **Garlic can be cultivated too, and both Cultivation upgrades now cost less.** The shrine sold
  Cultivation: Red Cabbage and nothing for Garlic, which was backwards: Red Cabbage is guaranteed
  from the Traveling Cart once in year 1, while Garlic has no year-1 source at all. Cultivation:
  Garlic joins it, giving Spring Mixed Seeds a 10% chance of Garlic, and both now cost 3,000
  Junimo Points instead of Red Cabbage's old 5,000. Buying both is still cheaper than Pierre's
  Special Order, which stays the guaranteed route. Raised by Tottelotta123, who asked why Garlic
  was on a Spring board when the cart was the only way to get it.

### Fixed

- **Artichoke is no longer held off an Easy board.** On the Easy item-rarity setting the mod kept
  all three of Pierre's year-two crops off the board until you bought an upgrade, but Artichoke
  never needed one: planting Mixed Seeds in Fall gives Artichoke Seeds about a quarter of the
  time, in any year, for free. It is now treated as the ordinary Fall crop it is, and its pacing
  week moves from 11 to 10 to match when you can actually harvest one.

## 0.18.0 - 2026-09-10 - The Mod Compatibility Update

2059 tests.

### Fixed

- **Bundles no longer ask for things this run cannot reach.** Reported by pitytheviolins:
  with The Fishmonger installed, a Community Center bundle asked for a dish only Constance
  sells, from a shop on Ginger Island, which this mod's time loop never reaches. There was no
  way to complete that bundle. The game now traces every item back to where it actually comes
  from (a shop, a crop's seed, a cooking recipe) and keeps an item off the board unless at
  least one of those routes is somewhere this run can reach. An item with no known source at
  all is left alone, so this only ever removes things proven unreachable, never guesses.
- **A board you already have gets repaired, not just new ones.** If a bundle on your current
  board is asking for something this run can never reach, it is swapped for something you can
  actually get the next time you load that save. Anything you already donated stays donated.
- **Driftwood, Rain Totem and Ostrich Egg were wrongly kept off a board that had The Fishmonger
  installed, even though all three are freely obtainable.** The check above did not yet know
  that fishing trash comes off the line in any water from day one, or that a craftable item is
  reachable by definition, so a mod listing an item like these in an unreachable shop could tip
  it the wrong way. All three are now confirmed reachable in a live re-run, closing that gap.
  Found and fixed during live verification against the real reported mod, before this release
  ever shipped.
- **The bundle catalogue now says what got kept off the board and why**, instead of silently
  listing candidates as if nothing had changed. This is a developer/debug tool
  (`tly_dumpbundles`), not something players see in normal play.
- **Final review before merge caught three more ways this same check was too strict.** A
  recipe taught only by mail, an event, or a quest (no shop selling it at all) was being
  treated as unlearnable instead of unknown; a recipe taught by both an unreachable shop and
  one this mod can't place (the Traveling Cart, the Night Market, a festival vendor) was
  outvoted by the one shop that could be placed; and crab-pot catches (Lobster, Crab, Cockle,
  Mussel, Oyster, Shrimp, Snail, Periwinkle, Crayfish) had no source recorded for them at all,
  the same gap Driftwood and Rain Totem fell through above. All three now count as reachable,
  same direction as everything else in this release: never taking real content off a board,
  only ever putting it back.

### Added

- A developer command (`tly_warpgraph`) for tracing which places a run can and cannot reach,
  used to build and verify the fix above.
- A regression test built from the exact case reported: The Fishmonger's 10 crop seeds and 11
  dishes, all sold only on Ginger Island, are now provably kept off the board. Five of those
  dishes use entirely ordinary ingredients and are only blocked because their recipe itself
  can't be learned anywhere reachable, which an ingredient-only check would have missed.

## 0.17.15 - 2026-09-10

2004 tests.

### Fixed

- **The bundle catalogue reports the two untouched rooms honestly.** `tly_dumpbundles`
  classifies each candidate on its own, so it described the Vault and the Abandoned Joja
  Mart as re-rolling from a pool when the generator never touches either. Developer
  diagnostic only, no effect on play.

## 0.17.14 - 2026-09-10

2004 tests.

### Fixed

- **The Abandoned Joja Mart's bundle is vanilla again.** The Missing Bundle was being
  re-rolled by the mod's own board generator, which is how it came to ask for a legendary
  fish. That room sits outside the loop entirely: it never carries a season theme, never
  counts toward a goal and only opens once the hall is finished and the year has turned.
  It is now passed through untouched, exactly as the Vault always has been, and a save
  that already picked up a re-rolled version gets the real one back on its next rewind.
  Reported by ChaoticMindset.

## 0.17.13 - 2026-09-10

2004 tests.

### Fixed

- **Zoom Level and UI Scale survive the rewind.** Both dials snapped back to the game's
  defaults every time the Junimos rewound the year, and there was no way to change what
  those defaults were. The game treats zoom and UI scale as belonging to the save rather
  than to the player, so the fresh year the rewind builds was starting them over while
  every other setting came back on its own. The loop now carries both across the reset.
  Reported by RiseiJaku.

## 0.17.12 - 2026-09-09

2004 tests.

### Fixed

- **Quest-finishing scenes play again every loop.** The rewind lets a scene replay only when it
  hands something out (a recipe, a letter, a quest). The scenes that finish a quest, Jodi's fish
  casserole dinner and Marnie's cave carrot, were being treated as already seen from the first
  loop, so the re-granted quest could never be turned in. Reported by ChaoticMindset.
- **Gunther brings the Rusty Key again every loop.** The rewind empties the museum, so the
  60-donation reward re-fires each loop, but Gunther's farm visit that actually hands the key
  over was stuck "seen" and never played again. Without the Keep Rusty Key upgrade there was no
  way to open the sewers after the first loop. Reported by ChaoticMindset.
- **Willy's Copper Pan, the one-off gifts and the second half of every chained scene come back
  each loop.** Same family as the two above, found by sweeping every vanilla scene: Willy's pan
  scene (gated on the Fish Tank letter the reset re-sends) grants the pan with a command the
  replay scan didn't know, and a scene that only unlocks after another replayable scene (Sam's,
  Shane's, Penny's and Maru's 14-heart chains, Shane's morning-after, Pam's bus follow-up) was
  stuck as seen from the first loop. The scan now treats gift and world-state commands as grants
  and lets a scene replay when the scene it chains onto replays.
- **A restored Community Center counts as complete.** With the loop active the mod answers
  the game's "is the Community Center complete" question from the bundle board, so the Gifts of
  the Junimos can't flip the hall to complete with rooms still open. On a finished hall that board
  walk could still say no, and everything downstream of the game's own question went quiet:
  Willy's back-room letter never came, so Ginger Island stayed closed after choosing to keep
  playing. Once every room's restoration has played, the hall itself is the authority. Reported
  by ChaoticMindset.

### Changed

- `tly_runstate` now prints the Community Center completion check the way Willy's back-room
  letter reads it (the board, the room flags, the completion mail, whether the letter trigger
  already ran), for diagnosing a keep-playing save that never got the letter.
- `tly_answer <n>` picks a response on an open question dialogue and `tly_skipscene` finishes the
  win screen, so the keep-playing path can be driven from the headless runbook.

## 0.17.8 - 2026-09-08

1991 tests.

### Fixed

- **A must-donate-all bundle's season gate takes any of its items.** The Bundle Log showed only
  the items the deadline spread had pinned to the current season, and the gate refused the
  bundle's other items even though the board takes any of them. The pins now decide how many of
  the bundle are due by each checkpoint; which ones you bring is your call, as it already was for
  pick-X-of-Y bundles. Winter still wants every slot. Reported by ada113.
- **The Cookbook and Craftbook open between the shrine and the reset.** The books start at 0
  slots, the first tier is bought at the loop-boundary shrine, and the reset that follows wipes
  every learned recipe, so a book bought at the shrine had nothing to bank by the time it was
  first opened. After the shrine closes, each book with a free slot and a recipe worth keeping now
  opens with a one-line prompt, and the reset runs once both are closed. The pickers also stop
  listing the new-save starter recipes, which the reset re-seeds anyway. Reported by ada113.

## 0.17.6 - 2026-09-07

1987 tests.

### Changed

- **A seasonal crops bundle every season.** The Pantry roller now always takes Spring Crops, Summer
  Crops and Fall Crops when a position offers them, the way vanilla's Pantry has all three. A board
  could roll Orchard, Preserver's and Home Cook's Feast into those positions instead, and then the
  Farming theme had nothing reachable to ask for in week 1 except saplings. The rng is still
  consumed at those positions, so every other bundle on a given seed lands where it did.
- **At most one jelly in a week's goals.** Sea Jelly, River Jelly and Cave Jelly are a capped goal
  group (one per weekly theme list), beside the fruit-tree and crab-pot caps. A week that asked for
  Sea Jelly and River Jelly together meant two slow single-spot fishing trips for one theme.

## 0.17.5 - 2026-09-05

1984 tests.

### Added

- **Every farm type is allowed.** The new-game screen no longer hides the other farm layouts and
  the save-load guard that refused them is gone. Kept buildings return to the player's own spots
  and the stash chest places relative to the farmhouse door, so nothing depended on Standard any
  more. Balance is not tuned per type; the README notes the trade-offs. Verified unattended on all
  eight types (new game, coop + barn + silo kept, rewind, same tiles) with two farm-specific fixes:
  a Meadowlands rewind respawns the starter coop at its default tile, which used to make the kept
  coop skip as "already there" (it is now walked onto the player's spot, like Keep Greenhouse), and
  a Riverland rewind threw on the farmhouse's starter Fish Smoker while rebuilding the cabin furniture.
- Dev commands `tly_newgame <farmtype> [skipintro]`, `tly_buildings`, `tly_totitle` and the
  `tools/farmtype-cycle.ps1` / `tools/farmtype-intro.ps1` runbooks behind that verification.
- **The Skip intro checkbox on character creation works again** (gazumbrado, Nexus bug 1127469).
  It skips The Longest Year's own Lewis to Junimo opening and goes straight to the theme picker;
  the vanilla bus ride stays skipped either way. Ticking it shows a one-button notice recommending
  first-time players watch the scene, since it is the only in-game explanation of the loop.

### Changed

- **New book art.** The Cookbook, Craftbook and Bundle Log furniture now use cover art drawn by
  supercam19 (same 48x16 sheet layout, so it is a straight asset swap). Thank you!

## 0.17.1 - 2026-09-05

1980 tests.

### Fixed

- **The Dye bundle never asks for a legendary fish.** It draws by colour from every object, so
  Legend (green) and Crimsonfish (red) were legal picks and an Extreme sweep board wanted both.
  Its season gates were already right: a per-item deadline can never precede the season the item
  first exists in, so red Cranberries stay a Fall ask.

## 0.17.0 - 2026-09-04

1979 tests. **The Reasonable Quantities Update.** 0.16.179 to 0.16.192 were internal builds;
their entries below are the detail. Reports: gazumbrado, nyxnyx2234, spenderg, Bumblewyn.

### Changed

- **Every bundle slot asks basis x band.** The basis is one week of going after the item: fish
  modelled from Data/Locations and Data/Fish at two catches a game hour, forage measured by
  three full-year map sweeps, crops on 100 tiles with no cost cap, ten crab pots, 60 mine kills a
  day (Skull Cavern a third), a modelled mine day, stations by unlock cost, animals from a Big
  Coop or Barn of eight, raw resources, geode minerals at four. The Stack size dial is the band:
  Easy 10-30%, Normal 20-50%, Hard 50-65%, Extreme 65-80%, ceiling 80%. Gold asks keep three
  quarters (fish) or half (crops). Where an item has several sources the largest basis stands.
  Hard and Extreme now mean it: an Extreme Spring Foraging bundle wants 40 to 60 of each item, an
  Extreme Adventurer's 80 Slimes, an Extreme Jeweler's a dozen of each gem.
- **Legendary fish**: once, plain, per-bundle cap 1/1/2/4 by step, never two from one season, and a
  board allowance of none on Easy, one on a quarter of Normal boards, two on Hard, three on Extreme.
- **Jeweler's and Rockhound's** authored bundles in the Boiler Room; Mineral, Jeweler's and
  Rockhound's draw minerals only.
- The Stack size tooltip, README and Nexus description describe the new meaning.

### Fixed

- Golden Egg and the five Extended Family fish are excluded at the object level.
- The Wild Seed exemption reads the slot's deadline; a farmable forage is measured mean plus 30.
- Normal caps a vanilla ask at 99; the multiplier skips only what the pass banded; required
  slots are applied before the quantity pass.
- Coffee Bean 20, Cloth 35, Skeleton Warrior out of the drop table, Large animal rows halved.

### Removed

- The one-slot 40-99 forage roll, the price-banded monster roll and the fixed x5 gold crop ask.

## 0.16.192 - 2026-09-04

1979 tests. The board's legendary allowance now bounds each bundle's cap as well: an Extreme
board allowed three was landing four, because the ban only tripped between bundles and one
bundle may hold two.

## 0.16.191 - 2026-09-04

1978 tests. The balance calls from the Codex review, Jeff's rulings 2026-09-04 (Iridium Ore
stays at 99: "there are other sources").

### Changed

- **Farmable forage is measured mean plus a seed-loop allowance of 30**, not a flat stack. A Normal
  Spring Foraging bundle had asked Leek x50 and Daffodil x50, the whole measured wild mean.
- **Gold crops keep half the roll** (fish keep three quarters): crop gold is fertiliser and Farming
  level, never automatic. 57 gold Grapes on Extreme becomes about 38.
- **Large eggs and milk are half the herd**, since the same animals give one size or the other.
- **Wood, Fiber, Hardwood and Clay are banded** (99, 99, 40, 20 a week), so Extreme no longer sits
  Fiber x2 next to Stone x79.
- **Legendaries per board: Hard two, Extreme three.** Open allowances had Glacierfish on every
  Extreme board and Legend on eight of ten, a fixed route rather than a roll.
- The Stack size tooltip, README and Nexus text say that a yield is capped at a stack of 99, so an
  Extreme 80 Slime is a stack, not a share.

## 0.16.190 - 2026-09-04

1977 tests. Internal build: the correctness findings from a Codex review of the design and the
two sweeps.

### Fixed

- **One aggregation rule for every item.** The quantity pass took fish first, forage plus crab
  pot second, and only then the largest of the flat tables, so Crab sat at its five pot catches
  while Lava Crabs drop it at 25%, and Cactus Fruit at its measured forage while the Oasis sells
  the seed. Now the largest basis stands wherever it comes from; forage plus pot is still one
  candidate.
- **The multiplier skips only what the pass actually banded.** An item with a basis in some season
  but none reachable by its deadline was skipped by both, so it kept its stack and dodged the
  dial. The pass now reports the slots it set and the multiplier skips exactly those.
- **Required slots are applied before the quantity pass**, so the deadline the pass reads (whole
  year for a pick-X-of-Y, per item once every slot is required) is the shape that ships. Extreme
  turning 3-of-4 into 4-of-4 could otherwise re-create the 90-mushrooms-in-Spring case.
- **Normal caps a vanilla stack at 99 too.** A factor of 1.0 returned early and let Forest's Fiber
  x200 through while Hard capped it at 99, which made Hard ask for fewer.

## 0.16.189 - 2026-09-04

1974 tests. Mineral and Rockhound's draw minerals only: the geode pool holds everything a geode
can drop, so Mineral had asked for a Golden Pumpkin, an Ornamental Fan and a Treasure Chest, and
Rockhound's for a Dwarf Gadget and a Rusty Spoon.

## 0.16.188 - 2026-09-04

Rockhound's minerals-only source; the engine logs each board's legendary allowance.

## 0.16.187 - 2026-09-04

1974 tests. From the first ten-board sweep read out of the running game.

### Changed

- **Legendary fish are rare on Normal.** They were rolling on eight boards in ten, about two a
  board. A board now gets a legendary allowance rolled off its seed: none on Easy, one on a
  quarter of Normal boards, uncapped on Hard and Extreme (the per-bundle cap of two and four still
  holds). Once the allowance is spent the five legendaries are banned from every draw the board
  makes, the hard-item swap and authored bundles included.
- **Two mineral bundles.** Jeweler's (six of the seven fancy gems, four needed, a Diamond for
  finishing it) and Rockhound's (eight geode minerals, five needed, a Geode Crusher) join Mineral
  in the Boiler Room.

### Fixed

- **Son of Crimsonfish can no longer be asked for.** The Dye bundle draws by colour from every
  object rather than from the fish pool, so 0.16.176's spawn-row rule never saw the Extended
  Family fish there. All five are now banned at the object level.
- **Skull Cavern drops are modelled at a third of the mine kill rate**, so Cloth from Mummies is
  35 a week instead of 99. Skeleton Warriors are dangerous-mines only and are out of the table,
  which stops Prismatic Shard x2 and Diamond x6 asks. Coffee Bean is supply-limited at 20.

## 0.16.186 - 2026-09-04

The Stack size tooltip, README and Nexus source describe the dial as a share of a week's real
yield, with the four bands spelled out.

## 0.16.185 - 2026-09-04

1972 tests. Internal build.

### Changed

- **The mines have a modelled basis (Jeff: "quartz is much more common").** Ore, coal, stone,
  geodes, the four node crystals, the six gems and Diamond now take their weekly basis from one
  modelled mine day: 10 floors cleared, 300 clear tiles a floor, a stone on a fifth of them, with
  every chance read off the decompiled mine code (ore nodes, the plain-stone ore roll by floor
  band, coal, geode and Omni Geode rolls, floor items, gem nodes). Quartz is 80 a week (floor items
  plus Stone Golems) instead of the 42 placeholder; the crystals 18; a specific gem 14; Diamond 3;
  ore and stone a full stack. The furnace bars follow the ore and coal: Copper and Iron 80, Gold
  60, Iridium 14, replacing the 40/30/20/5 guesses. Arithmetic in tools/fish-sim/mineday.py.

## 0.16.184 - 2026-09-04

1972 tests. Internal build.

### Changed

- **Every category is now basis x band.** Crops, crab pot, monster drops, station goods, animal
  products and geode minerals join fish and forage on the same rule, so Easy, Normal, Hard and
  Extreme mean the same thing on every slot of the board. Jeff's rulings, 2026-09-04:
  - Crops: 100 tiles and no cost cap (seed makers, the stash and JP upgrades are what multi-loop
    play is for). A shop seed is a full stack, so Extreme can ask 80 Parsnips or 80 Starfruit.
    Cart-only and rare seeds carry their own supply: Sweet Gem Berry 16, Ancient Fruit 5, the
    Raccoon crops 30. The fixed x5 gold Quality Crops ask is gone; those slots band like the rest.
  - Crab pots: 10 pots a season, modelled from CrabPot.DayUpdate (Lobster 3, Crab 5, Oyster 7,
    Cockle 8, Periwinkle 15 a week). A shellfish that is also beach forage adds the pot yield to
    its measured forage mean.
  - Monster drops: 60 kills a day for a week of the best monster a loop can reach, every drop in
    Data/Monsters, volcano, island and Qi dangerous-mines monsters excluded. Replaces the old
    price band that ignored difficulty.
  - Stations by what they cost and when they unlock: ore-limited bars, 10 tappers, 10 bee houses,
    10 preserves jars, 5 kegs, and animal products from a Big Coop or Big Barn of eight.
  - Geode minerals: base 4. Gems, books, artifacts and cooking stay single asks.
  Where an item has more than one source (Quartz from Stone Golems, Green Algae from Slimes) the
  largest basis stands. The Crops, MonsterDrops and Minerals tables are generated from the game's
  data by tools/fish-sim/gen_tables.py.

## 0.16.183 - 2026-09-04

1964 tests. Internal build.

### Fixed

- **Golden Egg can no longer be asked for (Nexus 1127469, gazumbrado).** Golden Chickens need
  Perfection, so no one-year loop can lay one. The game does not flag it out of random sale the
  way it flags Void Egg and Ostrich Egg, so the pool vet let it into the Chef's and Animal
  recipe buckets. It is now on the built-in exclusion list.

## 0.16.182 - 2026-09-04

1963 tests. Internal build.

### Changed

- **Forage asks are basis x band too, and the old 40-99 big-ask roll is gone.** Every forage slot
  on every bundle now rolls inside its step's band of the measured seasonal mean from the
  2026-08-30 sweeps (the same Easy 10-30% / Normal 20-50% / Hard 50-65% / Extreme 65-80% bands
  as fish, ceiling 80%), reachable by the slot's deadline. A ruling such as Purple Mushroom's 5
  stands in as its ceiling. A Wild Seed crop whose seeds grow by the deadline is farmable and
  rolls its band of a full 99-stack; before its season it is measured like anything else, so a
  Spring bundle asks at most 14 Common Mushrooms and a Fall one may ask 20-50 on Normal. The
  one-slot 40-99 roll that produced the 95 Rainbow Shells is removed; the PoolTuning fields for it
  stay in config for compatibility but nothing reads them. The stack multiplier skips banded
  forage as it does fish.

## 0.16.181 - 2026-09-04

1956 tests. Internal build.

### Changed

- **Fish asks are basis x band, so Hard and Extreme finally differ from Normal (Nexus post,
  gazumbrado: "2 smallmouth bass isn't hard or extreme").** Every fish slot used to roll x1 and
  the stack dial multiplied it, so Hard and Extreme both asked for 2 of everything. Now each fish
  has a per-season basis: what a level-10 player with bait lands on its best ten-hour day, times
  seven days, modelled by replaying the game's own fish pick over the real Data/Locations and
  Data/Fish tables (docs/superpowers/notes/fish-catch-rates-2026-09-04.md, tools/fish-sim). Each
  step rolls inside its band of the basis: Easy 10-30%, Normal 20-50%, Hard 50-65%, Extreme
  65-80%, and nothing rolls above 80%. Spring Smallmouth Bass (basis 66) asks 7-20 / 14-33 /
  33-43 / 43-53; Summer Octopus (basis 8) asks 1-3 / 2-4 / 4-6 / 6-7. A gold ask keeps three
  quarters of the roll, because fish quality is cast distance and level rather than luck (gold is
  automatic from level 6 on a full cast). The ask reads the slot's deadline like the forage clamp
  does, and the stack multiplier no longer touches a banded fish. Legendaries, mine-floor fish
  and anything landed less than twice a week stay at one. Applies to every bundle the engine
  emits, kept-vanilla ones included, on the engine path only; the vanilla-board pass is unchanged.

## 0.16.180 - 2026-09-04

1936 tests. Internal build.

### Fixed

- **The Wild Seed exemption now reads the bundle's deadline (Nexus post, nyxnyx2234).** A bundle
  asked for 90 Common Mushrooms on a first Spring. Common Mushroom escaped the measured ceiling
  because Wild Seeds can farm it, but those are Fall Wild Seeds, and a Spring deadline cannot wait
  for them. The big forage ask now works out the season the slot will be due (the named season of
  a Spring/Summer/Fall/Winter bundle, or the same effort-spread deadline the classifier gives a
  per-item bundle) and only lifts the ceiling when that item's seeds can grow by then. Otherwise it
  takes the most generous measured ceiling of the seasons reached so far: 14 Common Mushrooms for
  a Spring deadline, not 90.

## 0.16.179 - 2026-09-04

1924 tests. Internal build.

### Fixed

- **A legendary fish is asked for once, plain, and never crowded (Nexus 1127469, gazumbrado).**
  Legend, Crimsonfish, Angler, Glacierfish and Mutant Carp can each be caught once per loop, so a
  bundle asking for two of one was impossible, not hard. The Hard and Extreme stack-size dial was
  rounding their x1 ask up to x2 (the "2 silver Mutant Carps" and "2 glacier fish for Winter Star"
  reports), and the fish quality roll could star them. Now every legendary ask is x1 at base
  quality on every step, on TLY's own boards, on authored bundles and on a vanilla board the
  difficulty pass adjusts. A bundle also holds at most one legendary on Easy and Normal, two from
  different seasons on Hard, and four (one per season) on Extreme; a surplus roll is swapped for an
  ordinary fish from the same pool.

## 0.16.178 - 2026-08-31

1907 tests. A balance and correctness release driven by player reports (Nijah, spenderg,
gazumbrado). 0.16.168 to 0.16.177 were internal builds.

### Fixed

- **Bundle quantities are clamped to measured yields (0.16.172).** A Summer Foraging bundle had
  asked for 95 Rainbow Shells; a real Summer grows about seven. Every forage item now carries a
  ceiling of 80% of what a season actually produces, with the easy and hard bands at 20-50% and
  50-80% of it. The numbers are MEASURED, not modelled: `tly_sweepforage` harvests every spawned
  forage object on every map every day, and the table is the mean of three full 112-day runs. An
  expected-value pass over the spawn tables was tried first and came out 2.3x too high. Bonuses
  (Gatherer, the Foraging theme, Overgrowth) are deliberately excluded from the measurement, so
  an ask is out of reach on a lean month played bare and reachable on one played well.
- **Anything Wild Seeds can grow is never capped (0.16.174).** Its supply is tilled land and time,
  not the spawn rate, so a measured ceiling means nothing for it. Shells and mushrooms are not in
  that set and keep their caps: you cannot grow a Rainbow Shell.
- **Desert forage is only measured once the bus can run (0.16.175).** The measuring save had the
  desert already open, so Cactus Fruit and Coconut were credited about 38 a season from Spring 1.
  Their Spring and Summer rows are dropped; the ceiling comes from Fall and Winter.
- **Extended Family fish can no longer be asked for (0.16.176).** Ms. Angler, Son of Crimsonfish,
  Legend II, Radioactive Carp and Glacierfish Jr. only spawn while Mr. Qi's Extended Family order
  is active, which needs the Walnut Room on Ginger Island. Unlike the five vanilla legendaries
  they are not flagged out of random sale, and they spawn on ordinary maps, so nothing had caught
  them. Any spawn row gated on a live special order is now skipped, which covers Qi Beans too.
- **Mushroom ceilings corrected for Ginger Island (0.16.177).** The sweep walks every loaded map,
  and the measuring save had the island created, so the island cave's all-season mushroom spawns
  were counted as reachable. Purple Mushroom has no mainland forage row anywhere in the game yet
  had been credited 17-19 a season; it is now capped at 5 by ruling (the mines' mushroom floors).
  Chanterelle, Red Mushroom and Common Mushroom were halved.
- **Mystic Syrup is gated on its tree, not just the tapper (0.16.168).**

### Changed

- **Experience upgrades rebalanced (0.16.178).** The per-skill chain was x2/x3/x4/x5 for
  100/200/350/550 JP; it is now +25/50/75/100% for 150/350/650/1,000. Junimo Insight adds 50%
  instead of doubling, price unchanged at 3,000, and still feeds Mastery XP. The percentages add
  rather than compound, so the ceiling is x2.5 instead of x10. Reported by gazumbrado, who bought
  every first tier for 100 JP each and had three skills at level 5 by day 4.
- **Existing owners are refunded, not grandfathered.** On first load of this version the JP spent
  on the old experience upgrades comes back at the old prices and the tiers are cleared, so the
  same points are re-spent under the new ladder.

### Added

- **`tly_sweepforage` (0.16.171)** harvests every spawned forage object on every map into a
  per-season chest, which is how the yield table was measured. **`tly_crabpots` (0.16.173)** does
  the same for crab pots. **`tly_forageyield` (0.16.170)** reads the read-only yield simulator
  (0.16.169), now superseded for forage by the measured data.
- **`docs/superpowers/notes/forage-sweep-caveats.md`** records what the sweep counted that a real
  loop cannot reach (desert, Ginger Island, Secret Woods) and what a future re-run needs first.

## 0.16.167 - 2026-08-29

1822 tests. Two full simulated years on this build (STATUS.md), the boosts, gifts and every fix live-checked over the bridge. 0.16.18 to 0.16.166 were internal builds; this is the first release since 0.16.17.

### Added

- **JP Boosts (0.16.159 to 0.16.162).** The planning shrine (the Junimo statue on the farm) now has three tabs. **Boosts** sells fifteen this-loop buys for JP, grouped by how long they last: Rain Dance and Storm Call (tomorrow's weather), Fortune's Favor (a guaranteed lucky day), Second Wind (a free late night), Overgrowth / Feeding Frenzy / Growth Spurt / Rich Veins / Windfall (a week of extra rolls that stack with the weekly theme), Quick Feet, Year-Two Seeds, Haggler (another 10% off shops for the season), Fast Friends, Iron Lungs, Sneak Peek, Crash Course (a skill level on the spot, capped at two per skill per loop, never to 10, never keepable, price triples per level bought) and Elevator Pass (the elevator reaches the next floor ending in 0). **Active** lists what is running and when it ends, plus the week's theme. **Plan** keeps the price preview and the weather/cart foresight, and adds a collapsed Locked section that says what each keep still needs this loop. Keeps are still bought only on the Fail-night perk screen.
- **Gifts of the Junimos (0.16.164).** A new shrine category with one keep per Community Center room reward: Keep Greenhouse (put back where you left it), Keep Quarry Bridge, Keep Boulder Cleared, Keep Minecarts, and Keep Bus Unlocked (moved here from Buildings). Each is offered once you have earned that reward this loop. They share one price ladder: the first costs 1,000 JP and every Gift you own raises the price of the rest by 1,000, up to 5,000. A kept reward stands from day 1; the room's bundles stay on the board, still have to be paid, still earn JP, and completing them again does not replay the repair scene.
- **GMCM toggle "Re-send Better Start gift each loop"** (default on), with the trigger-action rewind below.

- **`tly_seasongoals` console command** opens the Season Goals page, the same one the Bundle Log book opens (debug).
- **`tly_gateneeds` console command** prints, per bundle, what the current season's day-28 gate still needs (the same numbers the Season Goals page shows) and the vault (debug).
- **Keep the Garden Pot recipe.** A new permanent keep at the Junimo Shrine, 750 JP, under Obtainability. Once bought, the Garden Pot recipe is back in your crafting list after every rewind, so an Oasis seed can be grown indoors out of season.
- **Year-Two Seeds and Sneak Peek** (the first two Boosts, now part of the roster above). Year-Two Seeds (75 JP, this week) gives Mixed Seeds a 5 percent chance to roll the season's year-two crop: Garlic in Spring, Red Cabbage in Summer, Artichoke in Fall (not sold in Winter). Sneak Peek (100 JP, this season) has the Queen of Sauce air the year-two episode on Sunday, and teaches you both that week's year-one recipe and the year-two one, so nothing is lost.
- **`tly_boost list | <id> [skill]`, `tly_activeeffects`, `tly_boostexpire`, `tly_openshrine [tab]`, `tly_dismiss` console commands** buy any boost the way the shrine's Buy button does, print the running boosts and stacks, force the day-start expiry pass, open the shrine on a tab, and click through end-of-night menus headlessly; and **`tly_tv`** runs the Queen of Sauce weekly-recipe lookup the TV uses and logs the dialogue and whether the recipe landed (debug).
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

- **Keep Bus Unlocked did not restore the bus** (Nexus, gazumbrado). The upgrade now keeps the bus and the desert open from day 1 of every loop; the four vault bundles stay on the board and still have to be paid, and still earn their JP. (0.16.157)
- **Heart-event invites never re-sent after a rewind.** The 8 and 10 heart invite letters (and any other one-shot letter the game marks as sent) fire again each loop; the money-milestone letters from Mom and Dad stay once per save. (0.16.154)
- **Keep Horse was offered with no stable.** The row now waits until this loop has built one. (0.16.155)
- **Stardew Valley Expanded saves lost season pity.** Every load re-derived the board from the seed and compared a display-name field that SVE renames, so SVE saves fell back to the plain read path; the mod now keeps the exact board it wrote and checks the live board against that. (0.16.158)
- **Stash enchantments and forges** were already round-tripping since 0.16.2; confirmed on request (Nexus, Bumblewyn).

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
