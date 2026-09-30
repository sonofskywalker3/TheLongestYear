# Quantity rules for reused bundles (design)

Date: 2026-09-30. Status: awaiting Jeff's review.

## Why

Jeff, 2026-09-30: "anywhere we're reusing an existing bundle we need to re-evaluate quantity based on
the availability data we've gathered." The 60-board audit (`docs/superpowers/notes/2026-09-30-quantity-audit.md`)
found 1,449 slots whose item has no amount rule. They ask for the game's own stack or the filler's
small default, almost always 1: one Acorn, one Apple, one Pizza, one Goby.

Jeff's rulings for this spec (2026-09-30):
- Scale every group the audit flagged, from the data.
- Keep the three tough items in, but "their quantity requirement is very limited, and their
  obtainability data is correct":
  - **Mystic Syrup:** remove it if the Mystic Tree Seed only comes from Foraging Mastery and the
    syrup only from that tree. Both are true (research below), so it is removed.
  - **Prismatic Shard:** no more than 0/1/2/3 across EVERY bundle on the board, by difficulty.
  - **Mystery Box:** the same, 0/1/2/3 across the board.

## What stays single on purpose (no change)

Artifacts and books (ruling 2026-09-04), Gil's Trophies, saplings (2,000 to 6,000g each), gems,
legendary fish (their own allowance), and the Spirit's Eve Jack-O-Lantern.

## How an amount is set today (unchanged machinery)

Each item has a **basis**: what a dedicated player yields in a week of year 1. `AskBands` rolls a
band of it by the Stack size setting (Easy 10-30%, Normal 20-50%, Hard 50-65%, Extreme 65-80%,
never above 80%). `QuantityBasisTables.Seasonal` holds per-season bases [Spring, Summer, Fall,
Winter]. A 0 means the item cannot exist yet. The pass takes the best season up to the slot's
deadline; a pick-X-of-Y slot has no deadline and reads the whole year. Everything below only adds
rows and one generated table; the rolling rule does not change.

## 1. Mystic Syrup: removed

- **Research.** The Mystic Tree Seed has one source, a crafting recipe granted by claiming Foraging
  Mastery. Mastery XP only starts once all five skills are 10, then needs 10,000 XP more. No shop,
  box, drop or treasure gives the seed. Mystic Syrup only comes from a tapper on a Mystic Tree.
- **Why it reached 39 of 60 boards.** Tapper's (an authored bundle) draws from the TapperGoods pool
  with no availability filter, and Exotic Foraging joins Forage and TapperGoods with none either.
  The late-week floor (week 16) still counts as inside the run.
- **Change.** Add `(O)MysticSyrup` to `ItemPoolBuilder.BuiltInExcludedItemIds`. The late-floor row
  in `AvailabilityWeeks` becomes dead and is deleted.

## 2. Prismatic Shard and Mystery Box: capped per board

### The cap

A board may ask for at most **0 on Easy, 1 on Normal, 2 on Hard, 3 on Extreme** of each, counted
as total items across every bundle. The cap follows the **Stack size** setting (the dial that
already sizes every ask).

Mechanism (reuses the legendary-fish pattern, `LegendaryFishRules`):
- Every slot of these two items asks for **exactly 1**. The quantity pass, the stack multiplier and
  a vanilla-kept stack (Helper's "5 Mystery Box") are all overridden to 1. Then the item total
  equals the slot count, and a slot cap is the item cap.
- A per-board allowance per item (0/1/2/3) is spent across bundles in fill order. When it is spent,
  the item is banned from later draws, in both fill paths:
  - engine re-rolls (`BundleSlotFiller.Fill`, the same `banned` set legendaries use);
  - authored bundles (`AuthoredBundleComposer`), which today only honour a binary ban. They get the
    running count too.
- **Helper's.** Its only items are Prize Ticket and Mystery Box. When the Mystery Box allowance is
  spent, Helper's is taken out of its position's candidate list for that board, so the engine picks
  another bundle there. Otherwise the filler would fall back to vanilla's "5 Mystery Box".
- A final board check counts both items after every pass and logs an error if a board exceeds the
  cap (a guard; the fill rules above are what enforce it).

### Prismatic Shard obtainability fix

The model places it at **Spring week 2** through the Omni Geode route. That route only exists once
16 geodes have been cracked, and only on the half of cracks that read the geode table (about 0.4%
per Omni Geode). The glue ignores both.
- **Change.** The geode reader honours the cracked-geodes condition as a week delay, and applies
  the 50% split to the chance.
- **Expected.** Prismatic Shard's earliest week moves to the mine-bottom routes (the Skull Cavern
  and iridium-node rule, week 9) with effort 7. That is Hard, so it only lands as a hard-item pick.
  Checked with `tly_itemmodel (O)74` after the change.

### Mystery Box obtainability (checked, no change)

Boxes only drop after Mr. Qi's plane flies over. That happens overnight after the first Help Wanted
prize ticket (the 6th Help Wanted quest), or after day 50. Then they drop from kills, fishing
treasure, panning, artifact spots, tree shaking and barrels. The model's week 3 (hard week 2) fits
a Help Wanted player. It stays as is.

## 3. New seasonal rows [Spring, Summer, Fall, Winter]

Added to `QuantityBasisTables.Seasonal`. Numbers are weekly yields from the research. The
arithmetic is in the notes file.

| Items | Row | Basis for the number |
|---|---|---|
| Acorn, Pine Cone | 20, 20, 20, 20 | 8 trees chopped a week (75% seed chance, 1-2 each) plus shaking about 30 trees (5%). |
| Maple Seed | 20, 20, 17, 20 | Same; from Fall 14 a shake gives a Hazelnut instead. |
| Moss | 5, 99, 45, 45 | Trees restart young every loop; moss needs growth stage 14. Green rain (one Summer day) floods it. |
| Orange, Peach | 0, 14, 14, 14 | 2 trees each, one fruit a day in season, 28 days to mature. |
| Apple, Pomegranate | 0, 0, 14, 14 | Same, Fall fruit. |
| Apricot, Cherry | stay single | Spring fruit: a day-1 sapling bears only in year 2. Cart and bat cave give a trickle. |
| Tea Leaves | 0, 17, 17, 17 | 10 bushes (matches the Green Tea row), leaves on days 22-28. |
| Cave Carrot | 15, 15, 15, 15 | Mine barrels, about 30 a mine day, 4 mine days a week. |
| Spring Onion | 35, 0, 0, 0 | Spring forage in the south-east Forest. |
| Salmonberry | 80, 0, 0, 0 | Spring 15-18 only, about 20-25 bushes a day. Replaces vanilla's stack. |
| Hay | 60, 99, 99, 99 | Marnie, 50g. Spring is money-limited. Scything needs a silo. |
| Trash, Driftwood, Broken Glasses, Broken CD, Soggy Newspaper | 6, 8, 8, 8 each | Crab-pot junk (10 pots) split five ways, plus fishing junk. Spring is lower: pots need Fishing 3. |
| Wheat Flour | 20, 40, 40, 40 | Pierre, 100g, like Sugar. |
| Oil, Vinegar, Rice | 10, 20, 20, 20 | Pierre, 200g. |
| Prize Ticket | 2, 2, 3, 3 | Every 3rd Help Wanted quest; town Special Orders from Fall. |

## 4. Fish with no data

Added as hand rows beside the simulated fish table. `tools/fish-sim` cannot simulate the mines,
the Submarine or rows with a bobber position, so the rows carry their arithmetic in comments.
Same assumptions as the simulated rows: Fishing 10, bait, 2 catches a game hour.

| Fish | Row | Where |
|---|---|---|
| Goby | 21, 21, 21, 21 | Forest waterfall pool, all day, any season. The game ignores its Data/Fish window. |
| Stonefish | 10.5 x4 | Mine floor 20. |
| Ice Pip | 9 x4 | Mine floor 60. |
| Lava Eel | 7.6 x4 | Mine floor 100. |
| Slimejack | 36.5 x4 | Mutant Bug Lair. |
| Midnight Squid | 0, 0, 0, 11.2 | Night Market submarine, Winter 15-17. |
| Spook Fish | 0, 0, 0, 8.7 | Same. |
| Blobfish | 0, 0, 0, 5.4 | Same. |

The Night Market rows count the market's **3 days**, not a 7-day week (7 days would ask for more
than the event allows).

## 5. Cooked dishes: a generated table

Around 60 dishes appear on boards. Instead of 60 hand rows, a `DishSeasonal` table is built once
per board from data the mod already loads.

For each season s, `basis[s] = max(cooked, shop)`:
- **Gate.** 0 before the season of the dish's model week. A dish the model cannot place stays
  uncovered and asks for 1.
- **Cooked.**
  - Take each ingredient's basis by s, divided by how many the recipe takes.
  - The scarcest ingredient sets the number. A category (any egg, any milk) takes its best member.
    A shop staple uses its seasonal row. A cooked ingredient (Tortilla, Bread) uses its own dish basis.
  - Multiply by **0.25**, since the ingredient has other uses. Round, and never go below 1.
  - Cap by effort, not counting the kitchen point: **12** for effort 3 or less, **8** for 4-6, **4**
    for 7 and up.
- **Shop.** For a dish sold all season: 6000 / price, capped at 25 (Saloon Salad, Pizza,
  Spaghetti, Bread; Trout Soup; the Summer ice cream stand, open Summer then carried forward).
  One-off festival stock and the Traveling Cart don't count.

The 0.25 share, the effort caps and the 6000/price rule are named constants for Jeff to tune.

**Hand rows win.** `QuantityBasisTables.Seasonal` is an override layer on top of the generated
table, so Sticky's tuned rows (Ice Cream, Maple Bar, Cranberry Sauce, Miner's Treat) stand. Sugar
is an ingredient and keeps its row.

**Plumbing.**
- `RawCookingRecipe` gains ingredient counts; the glue drops them today (Plum Pudding needs 2
  Wild Plums, Triple Shot Espresso 3 Coffee).
- The table is built where the availability model is built, and passed into `QuantityAskPass` and
  `FlavoredSlotPass`.
- The effort cap uses effort without the kitchen point, so the Keep Kitchen upgrade can never
  change an already-stored board.

Sample outputs, Normal ask in brackets:

| Dish | Spring, Summer, Fall, Winter | Normal ask |
|---|---|---|
| Salad (Saloon) | 25, 25, 25, 25 | 5-13 |
| Pizza | 10, 12, 12, 12 | 2-6 |
| Fried Egg, Cookies | 0, 7, 7, 7 | 2-4 |
| Maki Roll | 0, 8, 8, 8 | 2-4 |
| Fish Taco | 0, 4, 4, 4 | 1-2 |
| Stuffing | 0, 0, 6, 6 | 2-3 |
| Plum Pudding | 0, 0, 0, 5 | 1-3 |
| Lobster Bisque | 0, 0, 1, 1 | 1 |

## 6. Availability corrections found on the way

These are dates the model has wrong, not amounts. They are fixed in the same change because the new
rows read them:
- **Night Market fish** rate week 13 (Winter 1). The market opens Winter 15. Add a dated week (15)
  like the festival seeds.
- **Moss** rates week 1, any season. Trees restart young every loop, and Winter clears moss. Pacing
  week 6 (Summer's green rain), hard week 4.

## Out of scope (noted, not changed)

- Orange and Peach rate week 5, and Tea Leaves week 4. Both assume a day-1 sapling or a day-2
  recipe, which the rows above already cover through their 0 seasons. Raising those weeks is a
  pacing call for Jeff (Open questions).
- Authored bundles (Tapper's, Jeweler's, Four Seasons Sampler) have no availability filter at all.
  Mystic Syrup was the only leak it produced in the audit. A general filter is its own change.
- The fish simulator ignores `IgnoreFishDataRequirements`, and some pool comments name wrong mine
  floors. Tooling cleanup, not board behaviour.

## Testing

- **Unit tests:**
  - every new row;
  - the dish generator on a fixture recipe set (the gate, the scarcest ingredient, a count
    divisor, a cooked ingredient, the shop rule, the effort cap, a hand row overriding);
  - the Prismatic Shard and Mystery Box cap at each step (Easy bans both; Extreme allows 3; a
    vanilla-kept x5 becomes 1; Helper's leaves the pool when the Mystery Box is spent);
  - Mystic Syrup excluded.
- **In game (headless, my own runs):**
  - 60 boards at each Stack size step;
  - the audit script re-run: zero uncovered slots outside the single-on-purpose list, no Mystic
    Syrup, Prismatic Shard and Mystery Box totals within the cap on every board;
  - `tly_itemmodel` for Prismatic Shard, Moss and the three Night Market fish.
- **Existing saves.** Boards are stored when written, so a save in progress keeps its board until
  its next rewind. New rules apply to boards generated after the update.

## Open questions for Jeff

1. The Night Market fish are sized to the 3 market days. OK?
2. Orange and Peach at week 5, and Tea at week 4, assume buying a 4,000-6,000g sapling on day 1 or
   getting the tea recipe on day 2. Leave those, or move them later (week 6-7 and 8)?
3. The Prismatic Shard and Mystery Box cap follows Stack size. Or should it follow Item rarity?
