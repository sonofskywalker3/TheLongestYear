# Modded items TLY can't place yet (saved for the mod-support phase)

Saved 2026-10-08, from a live run with **Stardew Valley Expanded 1.15.11**, **Cornucopia More
Crops 1.6.3** and **Tech's Cross-Mod Bundles 1.0.2** installed.

**Status: not acted on.** Other mods are not officially supported. Revisit when Jeff starts tuning
for specific mods.

## Unknown (the item model has no week for them, so they default to week 13)

**Stardew Valley Expanded**

- Bearberry
- Bull Trout
- Four Leaf Clover (`Lucky_Four_Leaf_Clover`)
- Frog Legs (a dish; its ingredient `_Frog` is not recognised)
- Glazed Butterfish (a dish; its ingredient `_Butterfish` is not recognised)
- Minnow
- Mushroom Colony
- Nectarine
- Pear
- Poison Mushroom
- Puppyfish
- Radioactive Bass
- Red Baneberry
- Rafflesia (`Smelly_Rafflesia`)
- Starfish
- Tadpole
- Thistle

**Cornucopia More Crops**

- Fig
- Grapefruit
- Juniper Berries
- Lemon
- Lime
- Peppercorn
- Raspberry

**Only seen on a TLY Custom board with "Allow mod items in custom bundles" on**

- Durian
- Pistachio
- Olive Oil
- Sparkling Wine

## Placed, but the placement looks wrong

- **SVE Gold Carrot**: week 1 Spring crop, with seasons Spring/Summer/Fall.
- **SVE Birch Syrup**: treated as a cooked dish made from sap `(O)92`, week 6.
- **SVE Amber**: "artifact (catalog pool, no spot row)", week 3.

## Engine gaps behind these

- **Every SVE fish is unknown.** Fish derivation does not read the locations SVE adds, so none of
  its fish get a water, season or week.
- **Reachability is much stricter with these mods.** It kept 82 items off the board with them
  installed, against 9 without.
