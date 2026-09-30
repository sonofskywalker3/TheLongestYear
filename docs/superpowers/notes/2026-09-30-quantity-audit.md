# Quantity audit: board slots with no amount rule (2026-09-30)

Jeff, 2026-09-30: "anywhere we're reusing an existing bundle we need to re-evaluate quantity based
on the availability data we've gathered."

**Method.** 60 boards (`tly_genbundles 1-60`, Normal, 0.18.100, Rodger save). Every slot whose item
`QuantityAskPass.Covers` rejects: no basis row, so its amount is the vanilla slot's stack (when the
re-roll lands on the bundle's own item) or the filler's small default roll. 1,449 slots of that
kind. Grouped below; "x1-1" means every sighting asked for one.

## Single on purpose (existing rulings, no change proposed)

- **Artifacts** (Artifact, Field Research): "Books and artifacts stay single asks" (2026-09-04).
- **Books** (Book): same ruling.
- **Gil's Trophies**: rings, hats and the Insect Head are one-of items.
- **Saplings** (Orchard, ~170 slots): 2,000 to 6,000g each; one is the ask.
- **Gems / Prismatic Shard** (Treasure Hunter's, Geologist's): "Gems stay single."
- **Legendary fish** (Crimsonfish, Glacierfish, Mutant Carp): once per loop.

## Always x1 today, and the data says they should scale

| Group | Items (sightings) | Where | What the amount should follow |
|---|---|---|---|
| Tree seeds and Moss | Moss 49, Acorn 45, Maple Seed 43, Pine Cone 46 (x1 to 10) | Tapper's, Forest, Exotic Foraging, Construction | Chopping and shaking trees; Moss off old trees. A Resources-style row, like Wood and Hardwood. |
| Fruit tree fruit | Apple 20, Pomegranate 29, Cherry 18, Peach 17, Orange 16, Apricot 11 (x1 to 3) | Artisan, Fodder, Rare Crops, Dye, Enchanter's | A tree gives one a day in its season. Season-aware, from when a sapling bought on day 1 matures (28 days). |
| Cooked dishes | ~60 dishes, 1 to 19 sightings each | Chef's, Home Cook's Feast, Children's, Winter Star | Kitchen, recipe and ingredients. Same dish rule Sticky now uses (small, season-aware, 0 before the recipe exists). |
| Fish with no sim row | Goby 24, Spook Fish 23, Midnight Squid 20, Blobfish 15, Lava Eel 9, Stonefish 7, Ice Pip 2 | Night Fishing, Master Fisher's, Weatherman's, River Fish, Specialty Fish | Night Market submarine (3 days of Winter) and mine-floor fish. Need a FishAskBasis row each. |
| Forage and odd crops | Cave Carrot 25, Tea Leaves 36, Spring Onion 5, Salmonberry 15 (x10 to 50) | Exotic Foraging, crop bundles, Dye, Forager's | Cave Carrot from mine floors; Tea Leaves one bush a day from late Spring; Salmonberry is a 4-day window (the x50 came from vanilla's stack, not our data). |
| Trash | Trash, Driftwood, Broken CD, Broken Glasses, Soggy Newspaper (11 to 14 each) | Recycler's | Crab pots and fishing junk; plentiful. |
| Pantry staples | Vinegar, Rice (x1) | Chef's | Pierre's, like Sugar (Sugar is now seasonal). Oil and Wheat Flour did not come up in these 60. |
| Festival and fixed | Hay x10, Mystery Box x5, Prize Ticket x1, Jack-O-Lantern x1 | Fodder, Helper's, Spirit's Eve | Hay is bought or cut; Prize Tickets come from Lewis's daily quests; Mystery Boxes need the Mastery/Qi route in year 1. |

## Possible leaks found on the way (not quantity)

- **Mystic Syrup, 39 sightings** (Tapper's, Exotic Foraging). Its week is floored at the last week
  of the year because the Mystic Tree Seed is the Foraging Mastery reward. That is not a realistic
  year-1 item, and the Hard/Extreme hard-item swap probably pulls it in.
- **Prismatic Shard, 37 sightings.** Year-1 possible (mines 100+ drops, rare), but frequent for how
  rare it is.
- **Mystery Box, 19 sightings** in Helper's, always x5.
