# Herd Book (keep your own animals) + fix the "start with a" animal keeps

## Context

tanky24u (Nexus, 24 Sep) asked for a way to keep barn/coop animals across a rewind; Jeff promised it. The
existing "start with a <species>" keeps (UpgradeCatalog.cs:299-309) give a brand-new 0-heart animal of a species
each loop, but exploration found **none of the ten can be bought**: their `species:X` gate
(MetaState.MeetsMetaRequirement, MetaState.cs:324) checks `AnimalSpeciesEverOwned`, which is only ever written
by ApplyStartingAnimals itself (WorldResetService.cs:1174), and three gate names (Chicken, VoidChicken, Cow)
don't match the recorded vanilla names (White Chicken, Void Chicken, White Cow).

Jeff's design (2026-09-25):
- Keep "start with a" as is (fix it so it can be bought).
- Add a **Herd Book**, a carried book like the Cookbook/Craftbook. Buying it gives **1 chicken slot**; each
  further level adds **one slot, in a fixed order**. Players register the specific animals they want back.
- A registered animal comes back with **everything, hearts included** ("more expensive but better than the start
  with an animal option").
- Chicken slots take white, brown and blue chickens only. Void, Golden, Dinosaur and Ostrich get their own slots
  at the end.

## Slot ladder (18 levels; prices are a proposal for Jeff to tune)

Jeff (2026-09-25): two slots for each regular animal, one for each special. The first slot of a pair costs
about 1.5x the matching "start with a" price, since hearts carry over, and the second slot about 25% more.

| Level | Slot added | JP | | Level | Slot added | JP |
|---|---|---|---|---|---|---|
| 1 (the book) | Chicken | 600 | | 10 | Rabbit | 1300 |
| 2 | Chicken | 750 | | 11 | Sheep | 900 |
| 3 | Cow | 600 | | 12 | Sheep | 1150 |
| 4 | Cow | 750 | | 13 | Pig | 1050 |
| 5 | Duck | 750 | | 14 | Pig | 1300 |
| 6 | Duck | 950 | | 15 | Void Chicken | 900 |
| 7 | Goat | 750 | | 16 | Golden Chicken | 1500 |
| 8 | Goat | 950 | | 17 | Dinosaur | 1350 |
| 9 | Rabbit | 1050 | | 18 | Ostrich | 2250 |

**Players start with the book (Jeff, 2026-09-25), like the Cookbook and Craftbook.** Level 1 (the first Chicken
slot) is free and owned from the first loop. Levels 2-18 are the purchasable upgrades `herdbook_1..herdbook_17`
(herdbook_N adds slot N+1), so the prices above apply from level 2 on.

Each level requires the previous one (the same chain pattern as cookbook_1..4). Purchases have no building gate.
A slot whose building keep isn't owned just shows a "needs Keep Coop/Barn" note, and its animal waits.

## Approach

### 1. Fix "start with a" (a bug fix, ships first)
- Core: add `AnimalSpecies.Normalize(string vanillaType)` that maps White/Brown/Blue Chicken to Chicken,
  White/Brown Cow to Cow, Void Chicken to VoidChicken, Golden Chicken to GoldenChicken, and passes the others
  through. Use it where `AnimalSpeciesEverOwned` is written, and when the gate is checked, so old recorded values
  match too.
- Mod: record species during play. On DayStarted and Saving, scan `Utility.getAllFarmAnimals()` (or
  `Farm.getAllFarmAnimals()`) and add each normalized type to `meta.AnimalSpeciesEverOwned`.
- Tests: normalization table, and the gate passing for every start_* row once its species is recorded.

### 2. Herd Book data (Core)
- `UpgradeCatalog`: the 17 `herdbook_N` rows (Carryover category, chained), plus
  `HerdBookSlots(int highestTier) -> IReadOnlyList<HerdSlotKind>` built from one ordered table (Chicken x2,
  Cow x2, Duck x2, Goat x2, Rabbit x2, Sheep x2, Pig x2, VoidChicken, GoldenChicken, Dinosaur, Ostrich). It
  returns the first 1 + tier entries. The owned tier comes from `MetaState.HighestKeptTier("herdbook_", 17)`
  (MetaState.cs:281).
- `HerdSlotKind` plus `HerdSlotRules.Accepts(kind, vanillaType)`: a Chicken slot takes White/Brown/Blue Chicken
  only, a Cow slot White/Brown Cow, and the other kinds their one type.
- `HerdEntry` record: SlotIndex, Type, Name, SkinId, Friendship, Happiness, plus the flags needed to restore
  hearts and produce. Reuse the `PetSnapshot` shape (Core/PetSnapshot.cs). It is always restored as an adult.
- `MetaState.HerdBook : List<HerdEntry>` (persists through WriteSaveData like CookbookRecipes, MetaState.cs:242).
- Tests: the slot ladder per tier, Accepts, the MetaState JSON round-trip, prices and the prerequisite chain
  (mirroring UpgradeCatalogTests.cs:231-287).

### 3. Herd Book item, sprite and menu (Mod)
- Copy the Cookbook pattern: a `BookKit` id `sonofskywalker3.TheLongestYear_HerdBook`, a `BookFurniture` row and
  checkForAction case, `ReconcileInventory` keeps one copy (so every player has it from the start, including
  existing saves), `MenuLauncher.OpenHerdbook`, and `UI/HerdBookMenu.cs` modeled on CookbookMenu.cs.
- **Sprite:** a fourth 16x16 cover added to `src/TheLongestYear/assets/books.png` (48x16 becomes 64x16, sprite
  index 3). It copies the style of supercam19's covers (c7b8a92: Cookbook red, Craftbook blue, Bundle Log green):
  - a dark outline;
  - a mid-tone cover with bright corner highlights;
  - the cream page block along the bottom;
  - a bright centered emblem.

  The Herd Book gets its own leather color (warm brown or tan, distinct from the other three), and its emblem is a
  simple front-facing animal face (a cow, with pig and chicken as alternatives) drawn in the bright accent tone. Draw
  it with Pillow in a scratch script from the existing sheet, so the three original covers stay byte-identical. Show
  Jeff a 12x preview of 2-3 face options before committing. Credit supercam19's style in the commit and in the
  README credits line if Jeff agrees.
- The menu lists the slots in ladder order, each labeled with its kind. Clicking an empty slot opens a picker of
  the animals on the farm that the slot accepts and that aren't already registered, showing name, species and
  hearts. Clicking a filled slot asks to remove it (ConfirmationDialog, as CookbookMenu.PromptRemove does). A slot
  whose building keep isn't owned shows a note such as "Needs Keep Barn to come back".
- Registering stores the animal's current snapshot. Players can use the book mid-loop (placed furniture), like
  the Cookbook.

### 4. Rewind night and restore
- `RunController.OfferRecipeBanking` (RunController.cs:657) gets a third link after the Craftbook: open the Herd
  Book on a rewind night when there's an empty slot and an eligible animal, with the same `_menuWatch` watchdog.
- Just before the reset, refresh every registered entry from its live animal (matched by the stored animal id) so
  it carries the hearts from the end of this loop. An entry whose animal is gone keeps its last snapshot.
- Restore in WorldResetService right after ApplyStartingAnimals (step 10, WorldResetService.cs:551): for each
  entry, find a building of the right family and tier with free room (reuse `ChainInfo`,
  WorldResetService.cs:1185), create the FarmAnimal with the saved type, name, skin, friendship and happiness as
  an adult, `adoptAnimal`, then store the new animal id back on the entry. If there's no building or no room, log,
  show a HUD line, and keep the entry for next time. The "start with a" animals are placed first, and both count
  toward capacity (fixes the current never-checks-full gap, WorldResetService.cs:1169).
- Rewinds always land on Spring 1, so animals can graze and need no hay grant.

### 5. Text, docs, release
- i18n: `upgrade.herdbook_N.name/.desc`, `furniture.herdbook`, `menu.herdbook.*`, slot-kind names. Player-facing
  lines go through the game-writing skill.
- README and docs/nexus-description.bbcode What's New (identical), CHANGELOG, TODO (close the animal-keep promise;
  record tanky24u's "+1 animal" preference as met).
- Patch bump per code commit on master. Execute subagent-driven.

## Critical files
- src/TheLongestYear.Core/UpgradeCatalog.cs, MetaState.cs, RunBaselineBuilder.cs (StartingAnimalMap names),
  new Core/HerdBook*.cs, new Core/AnimalSpecies.cs
- src/TheLongestYear/Loop/WorldResetService.cs (ApplyStartingAnimals, new restore step), Loop/RunController.cs
  (OfferRecipeBanking)
- src/TheLongestYear/Integration/BookFurniture.cs, Core/Interactables/BookKit.cs, UI/MenuLauncher.cs,
  new UI/HerdBookMenu.cs, ModEntry.cs (species recording, `tly_openherdbook` debug command)
- src/TheLongestYear/i18n/default.json; tests/TheLongestYear.Tests/*

## Verification
- `dotnet test` in tests/TheLongestYear.Tests (new Core tests plus the existing suite).
- Build with `-p:EnableModDeploy=false`, then deploy for the live test.
- Live test on the Rodger throwaway save through the headless bridge (my launch, labeled):
  - Own a coop and a chicken; confirm `start_chicken` can now be bought.
  - Buy herdbook_1..3 with `tly_addjp` and `tly_buyupgrade`, then register two chickens and a cow.
  - `tly_failreset`: confirm the Herd Book opens after the Craftbook, and that on Spring 1 the same names come
    back as adults with the same hearts, in their kept buildings.
  - Register an animal without its building keep: it stays registered, and a HUD line and log line explain why.
  - A full coop: the extra animal is skipped and stays registered.
