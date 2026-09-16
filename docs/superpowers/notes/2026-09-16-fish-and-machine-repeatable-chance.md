# Repeatable chance, second pass: ordinary fish and machine output (for Jeff, no model change yet)

The 2026-09-16 ruling ("a chance route you can retry many times a day with a decent chance each
try counts as dependable") left two families to be judged on their own. This note is the threshold
and the item list for each. Nothing in the model changed for this note. Numbers come from the PC
1.6 decompile, `fish-catch-rates-2026-09-04.md`, `Data/Machines` (patch export) and the fresh
report `obtainability-compare.md` written 2026-09-16 16:37 on the story build (1077 items:
OnlyNew 606, NewEarlier 172, NewLater 9, LuckOnly 119, Agree 164, OnlyExisting 7).

## 1. Ordinary fish

### Where the model stands today

Every ordinary species catch is already **Dependable**. The only `Fish` rows marked Chance are the
five fishing trash items (promoted on 2026-09-16), Wood and Coral from farm-map water, and the
three Magic Bait fish (island-gated, out of scope). The `Data/Locations` per-row `Chance` is
carried on `LocationSpawn.Chance` but never read.

So the fish question is the opposite of the mine one. It is not "which chance fish should be
promoted" but **"which dependable fish are really a lottery ticket and should be demoted"**. What
a demotion changes: the darkness's fairness picker (Easy and Normal) would stop assuming the
player can re-catch that fish before the deadline, so it would not take it out of a slot. The
board does not read the model, so no ask changes.

### The threshold

Same shape as the mine ruling, turned around: a fish stays Dependable when a full day of fishing
at its best spot in its season lands **at least 2 expected catches** (about an 86% chance of at
least one, Poisson). Under 2 it becomes Chance. The number used is the catch-rate note's "20h"
column for a level-10 angler with plain bait (its sunny or rainy figure, whichever the fish is
for; rain-only fish are judged on a rainy day since the model already carries the RainOnly flag).

Why 2 and not 1: at 1 expected catch the chance of going home empty is 37%, which is not what
"dependable" should mean when the darkness is deciding it can take the fish from you.

### Species that fall under the line (demote to Chance)

| Fish | Best day, 20h | Note |
|---|---|---|
| Legend | 1.6 (rain, spring) | catch limit 1 anyway |
| Mutant Carp | 0.8 | catch limit 1 anyway |
| Octopus | 1.2 (summer) | |
| Pufferfish | 1.5 (summer) | |
| Sea Jelly | 1.1 to 1.4 | |
| Cave Jelly | 1.9 | borderline; its only other routes are Lava Eel pond chance |

Plus, by the one-off argument rather than the number: **Crimsonfish, Angler, Glacierfish** (catch
limit 1 like Legend and Mutant Carp; a route you can take once in the year is not a repeatable
try). Recommend all five legendaries go Chance together so they are treated alike.

### Species that stay Dependable but are close (for the record)

| Fish | Best day, 20h |
|---|---|
| Midnight Carp | 2.1 fall, 1.7 winter |
| Super Cucumber | 2.6 summer, 2.0 fall |
| Tuna | 3.0 summer, 2.2 winter |
| Dorado | 2.6 |
| Squid | 2.8 |
| Sturgeon | 3.8 |
| Lava Eel (mines, floors 80 to 119) | about 2.2 at fishing 10 (per-cast 1% + 0.8% per level point, MineShaft.cs 1166-1172) |
| Ice Pip (mines 40 to 79) | about 2.6 at fishing 10 |
| Stonefish (mines 1 to 39) | about 3.0 at fishing 10 |

The three mine fish are a warning sign: their per-cast odds scale with fishing level, so at level
5 they sit at 1.6 to 2.2. The model is level-blind for them. If you want them safe, say so and
they go Chance too.

Rain-only fish (Catfish, Eel, Shad, Walleye, Red Snapper) all clear 3.7 on a rainy day and keep
their RainOnly flag; no change proposed.

### What moves in the report if you say yes

- Mutant Carp: NewEarlier to LuckOnly. Cave Jelly: NewEarlier to LuckOnly.
- Legend, Crimsonfish, Angler, Glacierfish, Octopus, Pufferfish, Sea Jelly: Agree to LuckOnly
  (or OnlyExisting where the old model still has a week).
- Nothing else moves; the demotion touches only the species' own catch rows.

### The alternative, if you would rather not touch fish at all

Leave every species Dependable and demote only the five legendaries (one-off, not a try). That
covers the two worst offenders and the "same rule for all five" tidiness, and leaves Octopus,
Pufferfish and the jellies to the darkness's deadline logic (a summer fish taken in fall already
fails the deadline because it cannot be caught until next summer).

**My recommendation: the 2-per-day line, six species plus the other three legendaries.**

## 2. Machine output

### Where the model stands today

The morning report had 111 `Machine, Chance` rows. Reading them against `Data/Machines`, all but a
handful are **inherited**: the machine is deterministic and the row is Chance only because the
input's own table is Chance (Pale Ale from Hops carries both a Dependable and a Chance row for the
same rule). The crystalarium is the big one: all 44 of its rows are a `DROP_IN` copy of the input,
so they are exactly as dependable as the mineral you feed it. Nothing to rule on there.

The machines whose **own output rule rolls dice** are these:

| Machine | Roll | Items |
|---|---|---|
| Recycling Machine (BC)20 | Trash: 30% Coal, 30% Iron Ore, else Stone. Driftwood: 25% Coal, else Wood. Newspaper: 10% Cloth, else Torch. Glasses and CD: always Refined Quartz | Coal, Iron Ore, Stone, Wood, Cloth, Torch |
| Bone Mill (BC)90 | one of four fertilizers, equal odds | Quality Fertilizer, Speed-Gro, Deluxe Speed-Gro, Tree Fertilizer |
| Slime Egg-Press (BC)158 | per 100 Slime: 5% Purple, 10% Red, 25% Blue, else Green | the four Slime Eggs |
| Wood Chipper (BC)211 | 2% of Hardwood loads give a syrup instead of wood | Maple Syrup, Oak Resin, Pine Tar |
| Mushroom Cave (BC)128 | daily: 2.5% Purple, 7.5% Chanterelle, 9% Morel, 15% Red, else Common | the five mushrooms |
| Seed Maker (BC)25 | 2% Mixed Seeds, 0.5% Ancient Seeds | Mixed Seeds, Ancient Seeds |
| Mushroom Log | random mushroom by nearby trees | the five mushrooms |

### What a machine rule would actually change

Checked against the fresh report: **every item those machines can roll already has a Dependable
route from somewhere else** (Coal, Iron Ore, Stone, Wood, Cloth, Torch, the four fertilizers,
Green Slime Egg, the mushrooms, the syrups, Mixed Seeds), except:

- **Blue, Red and Purple Slime Egg**: LuckOnly, and the Egg-Press is their only route (the Slime
  Incubator rows are the same eggs going in). Slime itself is Dependable week 1.
- Rice Shoot: LuckOnly in year 1, but its seed-maker route needs Unmilled Rice, which needs Rice
  Shoot. Circular; the year 2 Pierre row is the real source. Not a machine question.

So the whole machine family comes down to one decision: **does pressing Slime count as a
repeatable try?** A press is 100 Slime, and how many presses a day depends on the slime stock, not
on a fixed action. A player farming the mines can press a handful a day; a slime hutch owner can
press dozens. Under the monster-drop rule (25% or better, dozens of tries) Blue Slime Egg would be
dependable and Red and Purple would stay Chance.

### Recommendation

**Leave machines alone.** The recycling machine, bone mill, chipper, cave and seed maker change
nothing either way, and the one item that would move (Blue Slime Egg) rides on an input rate that
varies too much between players to call "many tries a day". If you would rather have one rule for
every rolled drop, the alternative is: apply the 25% monster-drop threshold to machine output rules
too, which promotes Blue Slime Egg and nothing else.

## What I need from you

1. Fish: the 2-per-day line (6 species + the other 3 legendaries), legendaries only, or no change.
   And whether the three mine fish join the demotion.
2. Machines: leave alone (recommended), or apply the 25% rule (Blue Slime Egg only).
