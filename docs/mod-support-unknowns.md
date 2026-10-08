# Modded items TLY can't place yet

First saved 2026-10-08 from a live run with **Stardew Valley Expanded 1.15.11**, **Cornucopia More
Crops 1.6.3** and **Tech's Cross-Mod Bundles 1.0.2** (0.19.10). Updated the same day after the
mod-support pass (0.19.11 to 0.19.16), checked live with `tly_dumpmodel` (every item, all four
difficulty steps) on an agent-run game with those mods, and on an unmodded game before and after
(identical: no vanilla item changed).

With these mods, 283 of 429 modded items were unknown before; 240 are now. Every one left is
either a kind vanilla leaves unknown too (seeds, crafted tools, quest items) or comes from a source
TLY does not read (below). The 0.19.8 boards with Tech's bundles listed 11 to 15 unknown items
each; a fresh Normal board with Tech's bundles on 0.19.16 lists 9, all from the framework and
Spring-tree lists below (Pear, Four Leaf Clover, Raspberry, Mushroom Colony, Bearberry, Rafflesia,
Juniper Berries, Chicken of the Woods, Red Baneberry).

## What was fixed (and how)

| Item | Before | Now | Why it was wrong, what reads it now |
|---|---|---|---|
| SVE Bull Trout | unknown | week 5 | Every SVE fish carries ExcludeFromRandomSale, so the pool dropped it and only pooled fish were placed. Fish the pool leaves out for that flag are now placed from their own spawn rows (0.19.12). |
| SVE Minnow, Tadpole, Starfish, Puppyfish | unknown | week 1 | Same. |
| SVE Radioactive Bass | unknown | week 7 | Same; the Sewer's week. |
| Every other SVE fish caught on walkable maps (Butterfish, Frog, King Salmon, Kittyfish, Goldenfish, Meteor Carp, Grass Carp, Razor Trout, Wolf Snapper, Dulse Seaweed) | unknown | weeks 1 to 9 | Same. |
| SVE Frog Legs | unknown | week 6 | Its ingredient `_Frog` is one of those fish; the dish rule now finds it. |
| SVE Glazed Butterfish | unknown | week 5 | `_Butterfish` is an SVE fish; its Butterfish pond yields the dish at population 10, earlier than cooking it (week 6). |
| SVE Nectarine, Persimmon | unknown | week 5, week 9 | Only vanilla's six orchard fruits had a week. New Data/FruitTrees rule: sapling bought the first week a walkable shop sells it, 28 days to mature, first week in one of its seasons (0.19.14). |
| Cornucopia Grapefruit, Pistachio (and Almond, Cashew, Pecan, Walnut, Pomelo) | unknown | week 9 | Same (Fall trees). |
| Cornucopia Fig (and Yuzu, Camphor Leaves, Cinnamon Sticks) | unknown | week 13 | Same (Winter trees): placed, not unknown. |
| Cornucopia Avocado, Ume, Nutmeg | unknown | week 5 | Same (Summer trees). |
| SVE Gold Carrot | week 1 | week 9 (hard 6, Extreme 3) | Its seed is sold only by the Desert Trader; the crop rule read seasons alone. A crop whose seed is first sold after week 1 now waits for it (0.19.15). |
| Cornucopia Agave, Aloe, Bamboo, Blue Agave, Lemongrass, Sugarcane | weeks 2 to 6 | not placed | Seeds only at Sandy's; their seasons end before the desert opens (pacing week 9). |
| SVE Boomerang, Faded Button, Fossilized Apple, Old Coin, Rusty Shield, Stone of Yoba | unknown | week 3 | Artifacts whose dig spots are in their own ArtifactSpotChances, flagged ExcludeFromRandomSale. Read as a fallback for an artifact nothing else places (0.19.16). |
| Holly, Crocus, Crystal Fruit (vanilla items, with SVE) | week 1 | week 13 | SVE's Grampleton Suburbs rows (no season) on a map no door leads to. Forage rows are now dated by the walked map weeks; Sweet Pea and Blackberry are back to weeks 5 and 9 too (0.19.13). |

The map weeks (`LocationWeeks`, 0.19.12) walk every map warp and door warp one way from the farm; a
map's week is the latest vanilla gate on the easiest path. A map no door leads to is not dated, so
nothing is placed from it.

## Still unknown, and why

**From a framework TLY does not read (not Data/Locations, Data/Crops or Data/FruitTrees):**

- SVE Bearberry, Thistle, Poison Mushroom, Red Baneberry, Rafflesia (`Smelly_Rafflesia`), Four Leaf
  Clover (`Lucky_Four_Leaf_Clover`), Mushroom Colony, Dewdrop Berry, Green Mushroom, Goldenrod and
  SVE's other wild flowers: spawned only by Farm Type Manager (`[FTM] Stardew Valley Expanded`).
  Dishes made from them stay unknown too (Mixed Berry Pie, Mushroom Berry Rice).
- Cornucopia Juniper Berries, Raspberry, Olive, White Grape, Bay Leaves, Black Currants,
  Elderberries, Gooseberry, Shiitake, Chicken of the Woods, Vanilla: Custom Bush bushes. So Olive
  Oil and Sparkling Wine (made from Olive and White Grape) stay unknown.
- Cornucopia Peppercorn: SpaceCore spawn groups (trigger actions).

**No year-1 route in the data:**

- SVE Pear, Cornucopia Lemon, Lime, Cocoa Pod, Breadfruit, Eucalyptus and Melaleuca Leaves: Spring
  fruit trees. A week-1 sapling is mature in week 5, so the first in-season fruit is next Spring.
  Vanilla's Apricot and Cherry sit at week 13 only because the Traveling Cart sells them, and the
  cart sells only vanilla fruit. (Vanilla dishes that SVE or Cornucopia rewrite to need these, Salmon
  Dinner with Lemon and Chocolate Cake with Cocoa Pod, are unknown with those mods for the same
  reason.)
- Cornucopia Dragon Fruit, Lychee (Summer trees) and Breadfruit: saplings only at Sandy's, week 9,
  too late for a Summer harvest.
- Cornucopia Durian, Papaya, Plantain: saplings only at the island trader. Nectarine, Pear and
  Persimmon (Cornucopia's): their saplings are on Pierre's year-2 lines.
- SVE fish caught only on maps no door reaches (Highlands Bass, Fiber Goby, Gemfish, Diamond Carp,
  Alligator, Swamp Crab), and only on excluded maps (Fable Reef, Ginger Island, Crimson Badlands,
  the Mutant Bug Lair, the Witch's Swamp: Lunaloo, Shark, Bonefish, Void Eel and the rest).
- Cornucopia's rare seeds and their crops (Zucchini, Asparagus, Barley...): sold only on Pierre's
  year-2 lines. Reachability keeps these off the board, which is most of the 82 items it held back
  with these mods (56 Cornucopia seeds and crops, 17 SVE fish on excluded maps, 2 artisan goods
  from those crops, 7 vanilla, the same as an unmodded game). All of them are correct by the data.

**Kinds vanilla also leaves unknown:** seeds, saplings of trees TLY has no sapling route for,
crafted elixirs and totems, deeds, schedules and keys (SVE quest items), monster loot from SVE's
late-area monsters, flours from crops that are themselves out of reach.

## Placed, checked, left as is

- **SVE Birch Syrup**, week 6: correct. It is a Data/CookingRecipes dish (3 Birch Water, 25 Sap),
  recipe sold at Pierre's, so it waits for the kitchen like every dish. Birch Water is a tapper good
  from week 4.
- **SVE Amber**, week 3: the week is right (the artifact floor). Its basis still says "catalog
  pool, no spot row" because its dig spots are in its own ArtifactSpotChances; reading those for
  items that are already placed would change the effort of vanilla's Ancient Doll, Anchor, Bone
  Flute, Golden Relic and Prehistoric Handaxe, so the new reader is a fallback only.
- **Golden Pumpkin** (vanilla item, with SVE), week 5 instead of 12: SVE's Goldenfish pond yields
  it at population 10, and Goldenfish is catchable in Sprite Spring from week 1. Correct by SVE's
  data; worth a look if it shows up on a board.
