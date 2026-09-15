# The Longest Year - Status

**Last updated:** 2026-09-15 (story: the obtainability model now reads the Queen of Sauce TV schedule, rerun live)
**Branch:** `story`; master merged in at 0.18.4, everything PUSHED, nothing local-only
**Tests:** 2284 passing
**Build:** clean (Release, 0 errors); story HEAD deployed to the game; game LEFT RUNNING minimized from my automated run on the throwaway save `None_449077472`
**Last public release:** 0.18.4 (2026-09-14; overall Difficulty lever)

## 2026-09-15: the Queen of Sauce schedule is read (Jeff's ruling)

Commits `7f6539f` and `3a5a705` on top of the fix wave, pushed. `Data/TV/CookingChannel` is a glue section of its own
and hands the model each cooking recipe's episode number. Episode k airs on the Sunday that is day 7k
of year 1 (TV.cs `getWeeklyRecipe` 518: `whichWeek = DaysPlayed % 224 / 7`), so a dish's TV route is
its ingredients AND that Sunday; episodes 17 to 32 air in year 2 and are flagged, so the default
filters leave them out. A Wednesday rerun only repeats an EARLIER episode, so it never adds a week.

A recipe whose unlock is "none", "null" or a farmhouse level ("l 0") AND that no shop teaches has its
old "taught some other way" guess REPLACED by the TV route; only that guess is replaced. A skill
unlock, a friendship unlock or a shop that teaches the same recipe is a resolved route of its own, so
it keeps its source and gains the TV one beside it.

**Live build (automated run, throwaway save `None_449077472`, launched minimized):**

```
[08:43:20 INFO  The Longest Year] Obtainability model: 1113 items in 799 ms, 7 pass(es), 45 unresolved source(s).
[08:43:26 INFO  The Longest Year] (O)195 Omelet: 6 source(s); from day 1 dependable lands week 1, any lands week 1
  - Cooking, Dependable, lands wk1/wk5/wk9/wk13, needs recipe:Omelet; unlock:l 10 | recipe Omelet
  - Cooking, Dependable, lands wk4/wk5/wk9/wk13, needs recipe:Omelet; unlock:Queen of Sauce episode 4 (Sunday of week 4) | recipe Omelet taught by the Queen of Sauce
[08:43:26 INFO  The Longest Year] (O)220 Chocolate Cake: 3 source(s); from day 1 dependable lands week 14, any lands week 1
  - Cooking, Dependable, lands wk14/wk14/wk14/wk14, needs recipe:Chocolate Cake; unlock:Queen of Sauce episode 14 (Sunday of week 14) | recipe Chocolate Cake taught by the Queen of Sauce
[08:43:32 INFO  The Longest Year] tly_obtain compare: wrote ...obtainability-compare.md (1078 items: OnlyNew 606, NewEarlier 160, NewLater 14, LuckOnly 145, Agree 146, OnlyExisting 7; 45 unresolved).
```

| Verdict | Previous run | After the TV schedule |
|---|---|---|
| NewEarlier | 173 | 160 |
| NewLater | 14 | 14 |
| LuckOnly | 137 | 145 |
| OnlyExisting | 7 | 7 |
| OnlyNew | 606 | 606 |
| Agree | 141 | 146 |
| Dependable only through an unresolved source | 24 | 24 |
| Unresolved sources | 45 | 45 |

**Exactly 15 items moved, every one a cooked dish.** Two wait for their episode and stay NewEarlier:
Baked Fish 1 to 5, Maple Bar 2 to 3. Five now AGREE with the existing model: Glazed Yams 10 to 11,
Chocolate Cake 1 to 14, Plum Pudding 9 to 13, Pumpkin Pie 10 to 15, Cranberry Candy 10 to 16. Eight
fall to LuckOnly because their episode is a year 2 one and the default filters exclude it, leaving
only the cart: Complete Breakfast, Carp Surprise, Roasted Hazelnuts, Fruit Salad, Blackberry Cobbler,
Bruschetta, Poppyseed Muffin, Shrimp Cocktail. The existing model calls Bruschetta a "year-2 episode"
too, so that is agreement, not a loss.

A first build of this pushed seven more dishes later than they should be: the TV route was replacing
the old source whenever the unlock was taught elsewhere, shop or no shop, so the seven recipes the
Saloon ALSO teaches (Omelet, Pancakes, Maki Roll, Bread, Tortilla, Pizza, Hashbrowns, all farmhouse
level unlocks) lost their shop route, and Farmer's Lunch and Dish O' The Sea followed their ingredients.
Fixed in `3a5a705`: the TV route replaces only the unresolved guess, and otherwise sits beside the
shop route. The numbers above are the fixed build.

## 2026-09-15: obtainability phase 2, the review fix wave (12 findings, live rerun)

Commits `ea68649`..`3feb9fe` on top of phase 2, one per finding, all pushed. Full report:
`.superpowers/sdd/2026-09-14-obtainability-phase2/final-fixes-report.md`.

Two of the twelve were real model bugs. **Festival shops with no `Festival_` prefix** (the Desert
Festival stalls are `DesertFestival_Pam`, `DesertFestival_EggShop`) read as ordinary shops open all
year; they are now placed on Spring 15 to 17 like any other festival. **Every chained derivation**
(barter, crops, fruit trees, tea bush, machines, recipes, ponds, geodes) read its input through a
filter that excludes island and year 2 sources, so such an input produced NO derived source instead of
a flagged one; a new blind helper `Derived.cs` reads the input with both included and ORs the flags
onto the derived source. Two silent drops became records: a barter whose trade item has no source at
all, and a Magic Bait fish row with no bait anywhere. The rest were a report column
(`NewDependableKnown`, so a dependable week resting on an unresolved source says so), a 389-line file
split, four comment corrections, the Mushroom Log detail, the tea bush's sheltered note (an indoor pot
needs no pantry bundle, `Bush.IsSheltered`) and a "Known limitations" section in the spec.

A second round (`3feb9fe`) replaced the flag rule the first one used. Flagging a derived source only
when EVERY upstream source carried the flag let a mixed input (Garlic Seeds: a year 2 Pierre row beside
a chance cart row) drop the flag, so the year 2 row's landing day reached the default headline. An
input is now read under all four filter variants (plain, island, year 2, both) and each is derived
separately with its own flags, which pins the property the model needs: for any filter F, the derived
item's table under F is what deriving from the input's table under F gives. Garlic and Garlic Seeds
both read `dependable lands never` by default again, with the year 2 route recorded and flagged.

**Live build (automated run, throwaway save `None_449077472`, launched minimized):**

```
[00:50:28 INFO  The Longest Year] Obtainability model: 1113 items in 823 ms, 7 pass(es), 45 unresolved source(s).
```

```
[00:50:53 INFO  The Longest Year] tly_obtain compare: wrote ...obtainability-compare.md (1078 items: OnlyNew 606, NewEarlier 173, NewLater 14, LuckOnly 137, Agree 141, OnlyExisting 7; 45 unresolved).
```

| Verdict | Before the fix wave | Final |
|---|---|---|
| NewEarlier | 177 | 173 |
| NewLater | 5 | 14 |
| LuckOnly | 137 | 137 |
| OnlyExisting | 7 | 7 |
| OnlyNew | 606 | 606 |
| Agree | 146 | 141 |
| Dependable only through an unresolved source | (new row) | 24 |
| Unresolved sources | 21 | 45 |

Only 34 items moved at all, and every one of them is the Desert Festival fix: 22 kept their verdict
and moved to the real festival week, 8 went Agree to NewLater, 3 NewEarlier to Agree, 1 NewEarlier to
NewLater. LuckOnly, OnlyExisting and OnlyNew end where they started, which is the point: the island
and year 2 work changes no default answer, it only stops answers being lost or invented. The 24 new
unresolved sources are barter rows priced in a currency no data asset makes (22 Qi Gem, a Golden
Walnut, two island trades, one Bookseller trade); none is a year 1 bundle route.

**331 items still need Jeff's ruling** (173 NewEarlier, 14 NewLater, 137 LuckOnly, 7 OnlyExisting),
grouped with a plain-English reason each in the report's section 4, updated in section 7.4. The
biggest blocks: 48 cooked dishes whose recipe unlock is a condition rather than a delay, 26 machine
goods, 16 Desert Festival stall items, 16 shop rows, 15 animal produce.

## 2026-09-15: item obtainability model, phase 2 (start-day meaning, gap closure, live rerun)

Spec `docs/superpowers/specs/2026-09-14-obtainability-phase2-design.md`, commits `639dd34`..`51ab859`
(12 commits, tasks 1 to 9) plus this note. Sources now carry a `DayTable` (start day to landing day
over all 112 days); the model answers `Lands(item, startDay, filter)`; the comparison verdict is the
dependable landing week from Spring 1 against the existing hard week, with a new `LuckOnly` verdict
for items only a chance source reaches. `tly_obtain <itemId> [startDay 1-112]` prints every source
with `lands wkA/wkB/wkC/wkD` (the landing week starting Spring 1, Summer 1, Fall 1, Winter 1).
`manifest.json` was not bumped: this is a feature branch, the release line owns version bumps.

**Live build (automated run, throwaway save `None_449077472`, launched minimized):**

```
[00:06:29 INFO  The Longest Year] Obtainability model: 1105 items in 369 ms, 7 pass(es), 21 unresolved source(s).
```

The model published; no `no model published`, no failed data section. Phase 1 for comparison was
1086 items, 173 ms, 7 passes, 40 unresolved.

**Spot checks (ten commands, real output; nine pass, one difference, one partial):**

- `(O)24 1` Parsnip: `from day 1 dependable lands week 1, any lands week 1`. Pass.
- `(O)24 29` Parsnip from Summer 1: `from day 29 dependable lands week 15, any lands week 5`.
  Difference, not an error. The brief expected "never" for the dependable greenhouse route because
  the Spring seed is gone. It is week 15 because Parsnip Seeds has a
  `NightMarket, Dependable, lands wk15/wk15/wk15/wk15` row (the Magic Boat, Winter 15 to 17). Week 1
  is correctly gone and "any" drops to week 5.
- `(O)348 1` Wine: `dependable lands week 2, any lands week 1`, via
  `Machine, Dependable, lands wk2/wk6/wk10/wk14, needs machine:(BC)12`. Pass (week 2 or 3 expected).
  The Ginger Island ResortBar row is present but correctly not counted in the dependable headline.
- `(O)158` Stonefish: `Fish, Dependable, lands wk1/wk5/wk9/wk13, Fishing 0, needs mines:floor 1 |
  Stonefish, floors 1 to 39, 2% + 1% per level point (MineShaft.cs 1151-1157)`. Pass.
- `(O)815 1` Tea Leaves: `dependable lands week 4`, `Crop, Dependable, lands wk4/wk8/wk12/never,
  needs item:(O)251, setup tea bush 20d | tea bush, days 22 to 28` plus the sheltered variant
  landing in all four seasons. Pass.
- `(O)Moss`: `Forage, Dependable, lands wk1/wk5/wk9/never` (Spring to Fall) and
  `GreenhouseCrop, Dependable, lands wk1/wk5/wk9/wk13` (all year). Pass.
- `(H)27` Hard Hat: `Guild, Dependable, lands wk1/wk5/wk9/wk13, needs guild:Duggy 30 kills (Duggy,
  Magma Duggy); mines:floor 1 | Adventure Guild reward for Duggy`. Pass.
- `(O)798 1` Midnight Squid: `Fish, Dependable, lands wk15/wk15/wk15/wk15, few days, needs
  location:Submarine`. The week-15 Submarine half passes. The Magic Bait row the brief expected to
  survive with a Ginger Island flag is gone entirely, because `(O)908 Magic Bait: no source in the
  obtainability model.` Partial; recorded as an outlier below.
- `(O)139 1` Salmon: `dependable lands week 9`, seven `Fish, Dependable, lands wk9/wk9/wk9/never`
  rows including the five `Farm_*` maps from the `LOCATION_FISH` expansion. No `fishingGame`. Pass.
- `(O)472 1` Parsnip Seeds: has `Machine, Dependable, lands wk1/wk15/wk15/wk15, needs machine:(BC)25
  | (BC)25 seed maker from (O)24` plus the 2% Mixed Seeds chance row. Pass.
- `(O)BroccoliSeeds 1`: `FishingTreasure, Chance, lands wk7/wk7/wk9/never, few days, needs
  fishing:treasure chest | season seed from a treasure chest (Fall window)`. Week 7 is Summer 15 to
  28, the day-20 switch to the next season's seed, so the Summer 21 window the brief named. Pass.

**Comparison rerun** (`tly_obtain compare`, written to the gitignored
`Mods/TheLongestYear/obtainability-compare.md`; a copy of each report is in the gitignored
`test-output/obtainability-compare-phase1.md` and `-phase2.md`):

| Verdict | Phase 1 | Phase 2 |
|---|---|---|
| NewEarlier | 302 | 177 |
| NewLater | 1 | 5 |
| LuckOnly | (did not exist) | 137 |
| OnlyExisting | 18 | 7 |
| OnlyNew | 598 | 606 |
| Agree | 151 | 146 |
| Unresolved sources | 40 | 21 |

NewEarlier fell by 125 because the headline is now dependable-only from Spring 1, so the Traveling
Cart no longer makes almost everything week 1; the 137 items it used to swallow are the new
`LuckOnly` verdict. OnlyExisting fell from 18 to 7, and none of the section 2 gaps (Stonefish, Ice
Pip, Tea Leaves, Broccoli, the guild rewards, Moss, Moss Soup) is still in it. Unresolved halved to
21, all of them the diagnostics the spec keeps on purpose (catalogue `ALL_ITEMS` queries, Dish of
the Day, tool upgrades, pet adoption, movie concessions, items sold by the player, the two
input-less machine output methods).

**326 items need a ruling** (177 NewEarlier, 5 NewLater, 137 LuckOnly, 7 OnlyExisting). The grouped
list with reasons and item ids is in
`.superpowers/sdd/2026-09-14-obtainability-phase2/task-10-report.md`. The recurring reasons, largest
first: the cooking recipe unlock is a condition rather than a delay (48 items); the Desert Festival
barter stalls read as open in all four seasons when the festival is Spring 15 to 17 only (36); a
machine is assumed in hand (20); a barn or coop and its animal are assumed to stand, which is spec
decision 3 working as designed (15); the greenhouse keeps a crop or fruit tree in season (13); a
guild reward's kill count is a condition, not a timed grind (9).

**Outliers:**

- 115 of the 182 NewEarlier plus NewLater items are 4 or more weeks off the existing hard week. The
  widest: Mystic Syrup 16 to 2 (the Mystic Tree Seed is the Foraging Mastery reward and the model
  does not gate on mastery), Chocolate Cake 14 to 1, Squid 13 to 1 (Desert Festival barter), Fried
  Calamari 13 to 1, Cactus Seeds 13 to 1 (Sandy sells them all year, so the model is probably right
  and the existing fish-pond basis wrong), Roots Platter 13 to 2, Winter Root 13 to 2 (greenhouse),
  Napalm Ring 12 to 1, and Treasure Chest 5 to 14, the only wide move in the later direction.
- No OnlyExisting item is one the spec's section 2 claimed to close. All seven are the two stated
  non-goals: four Ginger Island items (Golden Coconut, Fossilized Ribs, Snake Skull, Snake
  Vertebrae) and three year-2 Bookseller books (Friendship 101, Mapping Cave Systems, The Alleyway
  Buffet).
- Unresolved is 21, below the 25 threshold.
- `(O)908 Magic Bait` has no source at all, where section 2 said it should carry Mr Qi's shop row and
  his crafting recipe, both Ginger Island. Because it has none, the three Beach rows requiring it
  were dropped instead of island-flagged, which is what moved Midnight Squid, Spook Fish and
  Blobfish into NewLater.

Nothing reads the model for gameplay. No code or test was touched in the rerun; the two outliers
above are recorded for Jeff, not fixed.

## 2026-09-14 evening: item obtainability model, phase 1 (blind build plus comparison)

Spec `docs/superpowers/specs/2026-09-14-item-obtainability-design.md`, plan
`docs/superpowers/plans/2026-09-14-item-obtainability-phase1.md`, 12 tasks, commits `811621c`..`46524b7`
plus this note. Built with no reference to the existing item model (guard test
`ObtainabilityBlindGuardTests` enforces it over 14 files). Nothing reads it for gameplay; `tly_obtain <id>`
and `tly_obtain compare` are its only readers. Pure rules in `src/TheLongestYear.Core/Obtainability/`,
one glue class `Loop/GameObtainabilityData.cs` reading the live Data assets at save load,
`Core/ObtainabilityComparison.cs` (not blind) for the report.

**Live build (my automated run, throwaway save `None_449077472`):**
`Obtainability model: 1086 items in 173 ms, 7 pass(es), 40 unresolved source(s).` No pass cap, no
`reading Data/... failed` warning.

**Spot checks (all nine pass):**
- `(O)775` Glacierfish: Fish, weeks 13-16, Fishing 7 (the live Data/Locations row says 7, the plan's
  note said 6), catch limit 1, needs Forest. Pass.
- `(O)147` Herring: Fish at Beach weeks 1-4 and 13-16 dependable, never Summer or Fall; Cart chance all
  year. Pass (the spec's Spring-and-Winter fish).
- `(O)348` Wine: Machine from the keg, dependable weeks 2-16 (some fruit is dependable in week 1, plus
  the 7 keg days); ResortBar shop flagged Ginger Island; Cart chance. Pass.
- `(O)798` Midnight Squid: Submarine week 15 only, few days, dependable; Beach with Magic Bait chance
  all year (Magic Bait is a known phase 1 limitation). Pass.
- `(O)414` Crystal Fruit: Winter forage at six locations, 13-16; Cart chance; Dust Spirit drop chance.
  Pass.
- `(O)378` Copper Ore: MineNode dependable every week, plus 19 other sources. Pass.
- `(O)24` Parsnip: Crop 1-4; GreenhouseCrop dependable 1-5 and 15-16 (the Night Market Magic Boat Day
  1 sells Parsnip Seeds in week 15, checked with `tly_obtain (O)472`); Mixed Seeds chance sources
  including the Winter greenhouse. Pass.
- `(O)142` Carp: Mountain and Backwoods 1-12, Sewer, BugLand and Woods all year. Pass.
- `(O)342` Pickles: Machine from any vegetable, dependable all year. Pass.

**Comparison report:** `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\obtainability-compare.md`
(gitignored), 1070 items: **NewEarlier 302, NewLater 1, OnlyExisting 18, OnlyNew 598, Agree 151;
40 unresolved sources.** The verdict compares the existing hard week with the new any-source week.

**The three biggest disagreement patterns:**
1. **The Traveling Cart makes almost everything "any week" by luck.** 158 of the 302 NewEarlier items
   get their week-1 answer from the cart's random stock (chance), and 81 more from a shop row the
   existing model never counted. Dependable-only weeks mostly agree with the existing hard week (the
   fish rows above show dependable 5 / 9 / 13 against existing 5 / 9 / 13). Phase 2 should compare
   dependable-only as the headline, not any-source.
2. **OnlyNew is out-of-pool inventory.** 598 items the existing model never placed: 319 shop rows
   (boots, hats, tools, catalogues), 68 crafting outputs, 66 festival shop items, 48 Night Market
   items. Expected: the existing model only knows bundle-pool items.
3. **OnlyExisting is code-only or deliberately excluded.** 18 items: Adventure Guild rewards (Hard
   Hat, Skeleton Mask, Crabshell Ring, Napalm Ring, Insect Head) live in code, not data; Moss and Moss
   Soup (Moss comes from trees in code); island artifacts and Golden Coconut are flagged Ginger
   Island and excluded by default; Stonefish and Ice Pip (mine fish) and Tea Leaves (tea bush) and
   Broccoli (seed source) are real gaps the new model should close. The one NewLater is Ancient
   Fruit (seed only by chance, so week 5 versus the existing 4).

Also seen: the Stardew Valley Fair's fishing minigame map (`fishingGame`) reads as a normal location
open all year, so Salmon is "dependable week 1"; phase 2 should drop minigame maps.

**Most common unresolved kinds (40):** `LOCATION_FISH` rows on the farm-variant maps (10, the game
delegates to another location's fish table), furniture and wallpaper catalogues (`ALL_ITEMS`, 8),
machine output methods (Cask aging 6, Seed Maker, Mushroom Log, Statue of Endless Fortune, Solar
Panel), shop code queries (Dish of the Day, tool upgrades, monster slayer rewards, pet adoption,
movie concessions, items sold by the player), `RANDOM_ARTIFACT_FOR_DIG_SPOT`,
`RANDOM_BASE_SEASON_ITEM`, and the golden chest raccoon seed.

**Phase 2 (after Jeff reads the report):** see TODO.md "Obtainability phase 2". Two rulings from the
build are recorded there: weeks should mean "start week" not "finish week", and the per-difficulty
live-inventory picker for darkness hits.

## 2026-09-14: rewind cutscene, where it stands (story branch)

Built 2026-09-11 (spec `docs/superpowers/specs/2026-09-11-rewind-cutscene-design.md`, plan and SDD
ledger `.superpowers/sdd/2026-09-11-rewind-cutscene/progress.md`). Task 8, the live visual pass, is
Jeff's feedback loop: the ledger's punch list 1 to 22 was worked through on 2026-09-11, then more
rounds (walker-only extras, season outfits, wind, extras at walking speed). The last of those,
"extras walk at walking speed even out of shot" (`45ec6ea`), has not been seen by Jeff yet.

- **Pan abort cap:** the re-review's residual (a) was already fixed in `b39acdf` (`MaxPanAborts = 2`,
  then the driver runs the reset directly). Residual (b) is fixed too (`_active` set before the
  first world write in `RewindPanScene.Start`).
- **Shrine foresight during a rewind:** checked live, not player-reachable. See TODO.md.
- **Live run 2026-09-14 (my automated run, throwaway Clone save `None_448848155`, backed up first):**
  `tly_failreset`, bedroom, pan and morning beats skipped, hold question showed Jeff's wording,
  `Hold choice: Reshuffled`, `Opened Junimo Shrine`, `FinalizeReset`, `RewindSpringPaint: released
  the Spring 1 hold`, `Loop reset complete. Run 165 begins`, planning hub, no ERROR.
- **Queued for Jeff:** Run 165 on the rotated Clone save, Fishing picked, `tly_setday 28`,
  `tly_gateneeds` = 17 bundles owed. Sleeping plays the real fail night into the full rewind.

## 2026-09-10 night: 0.18.0 The Mod Compatibility Update, then 0.18.1

**0.18.0** closed the two verification gaps the branch had left open before shipping:

- **Live re-run against the real Fishmonger pack on the final build.** The last code commit
  (`8592332`, the whole-branch final review) had only been checked against the test suite, never
  live. Re-ran it: **35 items kept off the board**, identical to the pre-review result, 31
  Fishmonger ids plus the four vanilla Ginger Island ones, five all-vanilla dishes still held on
  the unlearnable-recipe reason, and Driftwood, Rain Totem and Ostrich Egg all correctly absent.
- **Dirty-board repair, which had never been run in game.** Hand-wrote two condemned ids into the
  throwaway save: `(O)829` in an undonated slot and `(O)836` in one marked donated. On load the
  undonated one swapped (`'Spring Crops' slot 0: (O)829 -> (O)250`) and the donated one was left
  alone; `tly_gatecheck` reported no impossible gates; the replacement donated successfully into
  the repaired slot; and a reload of the clean original read 125 open slots and swapped 0,
  writing nothing.
- **Vanilla baseline:** 0 condemned ("Nothing. Every item in every pool has a route this run can
  reach."), and pool counts byte-identical to the Fishmonger run, so nothing legitimate was lost.

**0.18.1** came out of Tottelotta123's question about Garlic on a Spring board. Checking the game
data showed the three year-two crops never had matching year-1 routes, and that the shrine was
selling the wrong one:

- Garlic (476) has no year-1 route at all; Spring's Mixed Seeds roll is `Next(472, 476)`.
- Red Cabbage (485) is guaranteed from the Traveling Cart in year 1, though this mod's own cart
  slot cap throttles that back to luck.
- Artichoke (489) falls out of Fall's `Next(487, 491)` roll at 25% in any year, free. Only the
  shop listing is YEAR 2 gated, and Mixed Seeds is not a shop.

So `cult_garlic` was added at 3,000 JP, `cult_red_cabbage` re-costed 5,000 to 3,000, Artichoke
dropped from `YearTwoCrops.ExcludedFor` entirely and its pacing week moved 11 to 10. Verified live:
both Cultivation rows show at 3,000, no `cult_artichoke`, and buying `cult_garlic` deducted 3,000
with no errors anywhere in the log.

**Still open:** the Garlic Mixed Seeds substitution itself was not exercised in game (it needs a
Spring planting); only the catalogue, pricing and purchase path were. The quantity-realism audit
in TODO.md is untouched.

## 2026-09-09: Darkness pushback built on branch story (not live-tested, not merged)

The sabotage mechanic from the story spec section 6: blight withers crops from Summer, a donated
slot comes undone from Fall, an unfilled slot changes item in Winter. Wards tab at the shrine
(two crop wards, five hall wards), three GMCM switches under Features, `tly_sabotage` debug
command (status, blight [n], revert, tamper, report). Spec with every assumed number:
`docs/superpowers/specs/2026-09-09-darkness-pushback-design.md`. Commits fa2eaa6 (spec) and
b8ae6e3 (code) on `story`; nothing pushed. 2096 tests passing, build clean.
**Live run DONE 2026-09-09 (headless, throwaway None_ lineage):** fixture + Circle of Warding bought,
granted, placed; forced blight took 8 of 10 crops and nothing from the chest on the circle; Linus
and Shane letters delivered; forced reversion flipped the board and the ledger mirrored 4 of 5;
forced tamper rewrote Preserver's slot 3 (Wine -> 5 Winter Root), requirements re-resolved from
the stored engine board, and again after a reload; the porch scene played all three lines and the
day continued; the circle came back after a loop reset; six natural Fall nights fired a reversion
(Fall 4) and a spoilage (Fall 6) from the roll alone. One bug found and fixed: `EndingArmed`
survived a reset and silenced every night. Jeff has approved the numbers for testing; retune after
his own run. Debug: `tly_sabotage fixture` and `tly_sabotage circle` build the test farm.

## 2026-09-06: Year One Ending built on branch year-one-ending (not merged, not released)

Finishing the Community Center now ends the year the next morning with a real event (hall steps,
a villager's half-memory of the player, Morris closing the Pelican Town store, the Junimos'
warning, the shrine candle) and a Loop again / Keep playing this year choice, plus a Year 2 wall
on Spring 1 of a keep-playing save.
Branch `year-one-ending`, 12 tasks' worth of commits ahead of `master` (this session's Task 13 adds
docs, the runbook and `tly_answer`); nothing pushed.
2023 tests passing, 0 failing (`dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release`).
Live run: DONE 2026-09-06, PASS on Standard and Meadowlands after five fixes (see the subsection below).
Manifest still 0.17.5 on the branch; version set at merge.

### Live run (2026-09-06)

Headless per `docs/HEADLESS_DRIVING.md`, throwaway Rodger lineage only, no mouse/keyboard/screenshot.
Standard flow on `None_448421991` (rotated to `None_448424597` by the loop-again reset); Meadowlands
on a scratch `tly_newgame meadowlands skipintro` save, deleted afterwards.

| Check | Result | Proof |
|---|---|---|
| A. Deploy, mod loads clean, `Morris_Dark` recolour | PASS | `Debug bridge: 'pause when window is inactive' switched off at launch`; `Morris_Dark: recoloured 40 pixel(s).` |
| B. Standard: whole flow, both choice branches, wall, dark shrine, repeat win | PASS | see quotes below |
| C. `tly_ending speaker Shane` replay | PASS | `Ending: starting (speaker=Shane, crowd=12, shrine=8,7).` then `Ending: replay finished (no continuation).` |
| D. Meadowlands: scene 1 porch offset and scene 6 shrine tile | PASS | `Ending: starting (speaker=none, crowd=12, shrine=14,9).` then `Ending: event finished, running the continuation.` |

**B, the quoted line trail (final, post-fix run):**

```
Win night (tly_win): ending armed for tomorrow morning, weather forced sunny.
Ending: starting (speaker=George, crowd=12, shrine=8,7).
Ending: event finished, running the continuation.
Opened Junimo Shrine (JP: 85229).
Ending choice: Keep playing.
Year 2 wall: Loop again.
FinalizeReset (year 2 wall): applying reset (eventUp=False, farmEvent=none, season was Spring 2).
Loop reset complete. Run 127 begins (seed -32721836).
Opened planning hub (week 1, offer: Mining,Spelunking).
Win night (tly_win): ending already seen, queuing shrine + choice for the morning.
Ending choice: Loop again.
```

- **Shrine dark after loop again:** the rotated save reads `<grandpaScore>0</grandpaScore>`,
  `VictoryAcknowledged:false`, `Year2WallArmed:false`, `EndingSeen:true` (once per save, as specced).
- **Candle really lights:** the Meadowlands keep-playing save reads `<grandpaScore>1</grandpaScore>`,
  `VictoryAcknowledged:true`, `Year2WallArmed:true`.
- **Farm-type offset (scene 1):** Lewis on the porch at `(68, 18)` on Standard and `(85, 22)` on
  Meadowlands, so the game's own per-farm-type Farm-event offset applies and the script must not
  offset again.
- **Crowd:** 12 temporary actors on Town row 22 (Lewis 47, Robin 48, Pierre 49, Caroline 50,
  Marnie 51, speaker 52, Gus 54 ...), speaker front and centre; the speaker's `move` walked
  (Shane 52,22 -> 52,23).
- **No speaker on a fresh save:** Meadowlands ran `speaker=none` and scene 3 was omitted, per spec.
- Final post-fix session log: zero `ERROR`, zero "couldn't be parsed", zero "can't load portraits".

**Fixes made during the run (branch `year-one-ending`, no manifest bump):**

| Commit | What the log demanded |
|---|---|
| `686b2a5` | `tly_answer` clears `DialogueBox.transitioning` and verifies the click landed; `tly_dismiss` must never answer a question box (it calls `exitThisMenu`, which skips `answerDialogue`). Runbook corrected. |
| `80a0ed0` | `globalFadeIn` is not a vanilla command: `Event 'sonofskywalker3.TLY.Ending' has command 'globalFadeIn' which couldn't be parsed`. Now `globalFadeToClear`. |
| `c04cc29` | 3,951 `NPC Junimo0 can't load portraits from 'Portraits/Junimo0'` warnings in one run (the dialogue box retries the failed load every frame). A portrait is now generated from `Characters/Junimo`. Adds `tly_eventstep`. |
| `718e124` | `tly_ending` now names which flag is holding the frame, so "never armed" is distinguishable from "a vanilla farm event is still up". |
| `4f6873a` | The event hung in Town with `eventUp` + a pending `Game1.locationRequest` after `globalFade` then `changeLocation CommunityCenter`. `changeLocation` warps through `Game1.warpFarmer`, which fades on its own, so every `globalFade`/`globalFadeToClear` wrapped around a `changeLocation` is removed. |

**Findings not fixed:**

1. **An event's `speak` needs a click, so a headless run cannot watch the ending unattended.** The
   new `tly_eventstep` is the tool: it prints the current command, the actors and their tiles, and
   clicks an open dialogue box on. Every check above was driven with it. Any future ending or intro
   verification has to poll it about every 4 s.
2. **A vanilla farm event can block the armed morning.** On this save Evelyn's Garden Pot event was
   up on the Farm, and `EndingMorningDecider` correctly treats that as Busy, so the ending waited
   until it finished. Correct behaviour, but a real player who leaves that event running sees the
   ending only after it. Worth a note, not a change.
3. **`tly_answer`'s new "the box did not close" warning fires spuriously** when the answer handler
   itself opens another menu (the reset re-opens the planning hub in the same tick). The answer
   still landed both times (`Year 2 wall: Loop again.`, `Ending choice: Loop again.`). Debug-only
   noise; left alone rather than churn the guard further.
4. **Not verified:** the visual pass (every crowd member's sprite on screen, Morris's glow, the
   Junimo colours, the dusk ambient light, pacing). The log proves the script runs and the actors
   are placed; only Jeff's playtest can judge the look. `tly_win` on a board finished by real
   donations was not exercised either; the debug arm was used throughout.

### Fix wave live check (2026-09-06)

The whole-branch review's fix wave (`3cc1d71`, `086fd29`, `ef0bd7b`, `bb589f6`) re-checked headless on
the throwaway Rodger save `None_448424597` (rotated by the run to `None_448426690`, then
`None_448427173`). The critical finding was that vanilla event `191393`, the CC completion ceremony
the Keep-playing branch hands to `eventsSeen`, was being banked into `MetaState.SeenEventsEver` and
re-seeded into every later loop, so a loop after Keep playing would open on Spring 1 with a destroyed
JojaMart, Pierre on post-completion hours and a lightning cutscene queued for the first storm.

| Check | Result | Proof |
|---|---|---|
| Keep playing still flips vanilla's post-completion world for the current year | PASS | `Ending choice: Keep playing.` then, on the next day-start, `Applied trigger action 'Mail_Pierre_ExtendedHours' with actions [AddMail Current pierreHours]` |
| The hand-off id is written to this run's `eventsSeen` | PASS | save `None_448426690`: `<eventsSeen>...<int>65</int><int>191393</int></eventsSeen>` |
| It is NOT banked into the cross-loop memory | PASS | same save, after a full save cycle: `"SeenEventsEver":["65","112","60367","897405","100162","611439","0","992553","2120303"]` (no `191393`) |
| It does not survive the loop reset | PASS | `Year 2 wall: Loop again.` then `Run 130 ready (seed 979028916). Spring day 1 (week 1).`; save `None_448427173`: `<eventsSeen><int>2120303</int><int>611439</int><int>897405</int><int>60367</int><int>112</int><int>65</int></eventsSeen>` (no `191393`) |
| Mod loads clean on the new build | PASS | `Debug bridge: 'pause when window is inactive' switched off at launch so queued commands run without focus.` |

Notes:

- This save already carries the `tly_ending_seen` mail, so `tly_win` logged `Win night (tly_win):
  ending already seen, queuing shrine + choice for the morning` and the run took the repeat-win path
  (shrine, then the choice) rather than replaying the six-scene event. The event script itself was
  unchanged by this fix wave, and the code paths that write and strip `191393` are exactly the ones
  exercised here.
- `RunState.EventsSeenAtDayStart` still carries `191393` in the post-reset save. Harmless: it is only
  the previous-day snapshot `FamiliarityGlue` diffs against to spot new heart events, `191393` is not
  a relationship event, and the field is overwritten at the next day-end rollup.
- No `PurgeHandedOffEvents` line fired at load, i.e. this save had never banked the id, so the
  heal-on-load path was not exercised live. It is covered by `PostCompletionEventsTests`.

Game left running minimized, in a fresh loop, healthy.




## 2026-08-29 (late afternoon): played years on the real STANDARD and REMIXED boards, three-way comparison

`tools/sim-year.sh minimal` twice on build 0.16.145 (so Hay is still "unknown" here; 0.16.148 places
it), config `BundleSource` set to `Normal` on disk plus `tly_bundlesource Vanilla Default` /
`Vanilla Remixed` in memory before each reset; the log confirmed `Requirements source: vanilla board
(BundleSource=Vanilla, Default ...)` at 14:43:08 and `(... Vanilla, Remixed ...)` at 14:49:48.
Transcripts `docs/superpowers/notes/2026-08-29-sim-standardP4.txt` and `-remixedP5.txt`. No WARN or
ERROR lines in either. Each year ran in about 6.5 minutes.

**Every gate passed on all three boards.** Ledger at each season end (slots), and the season's own
plan in brackets:

| Board | Spring | Summer | Fall | Winter | tight | stretch | no hard item | Spring tight |
|---|---|---|---|---|---|---|---|---|
| Custom, seed loop 3 (P2, last night) | 22 | 50 | 79 | 100 | 26 | 1 | 5 | 2 |
| Custom, seed loop 100 (standardP3, mislabelled) | 26 [26] | 48 [22] | 71 [23] | 97 [26] | 30 | 2 | 4 | 5 |
| Standard (standardP4) | 25 [25] | 52 [27] | 84 [32] | 106 [22] | 36 | 0 | 7 | 4 |
| Remixed (remixedP5) | 18 [18] | 44 [26] | 70 [26] | 89 [19] | 36 | 1 | 4 | 5 |

Reading: Standard is the heaviest board (106 slots, a 32-slot Fall) and the tightest (36 of 26
bundles demand everything obtainable by the gate); Remixed is the lightest (89 slots, an 18-slot
Spring) but just as tight; the custom boards sit between (97 to 100) with the fewest tight gates.
Standard has no stretch line at all; Remixed keeps the Engineer's `[stretch: Iridium Ore Summer]`
the seed audit always found. No-hard-item: Standard 7 (Exotic Foraging, Crab Pot, Lake Fish,
Geologist's, Chef's, Dye and one more), Remixed 4 (Crab Pot, Lake Fish, Treasure Hunter's and one).

Askable goals by week (Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki):

Standard: Spring 2/2/4/2/5/2/0/0, 2/2/5/3/5/3/0/0, 2/3/0/2/5/3/1/0, 3/2/0/1/5/1/0/0; Summer
3/3/5/1/5/1/1/2, 3/4/4/1/5/1/0/2, 3/4/1/0/5/1/1/3, 1/2/1/0/5/1/0/2; Fall 3/4/4/2/5/2/2/2,
3/4/5/3/5/2/2/2, 3/4/1/0/5/1/2/3, 2/2/1/0/5/0/1/3; Winter 3/2/3/2/5/1/1/2, 2/0/3/2/5/1/0/2,
2/0/1/2/5/1/0/2, 3/0/0/0/4/0/0/1.

Remixed: Spring 2/2/4/1/5/1/0/0, 2/2/3/1/5/1/0/0, 3/3/0/1/5/1/0/0, 2/2/0/0/4/0/0/0; Summer
2/3/4/3/5/2/0/2, 2/2/4/3/5/2/0/1, 2/5/1/2/5/1/1/1, 1/2/1/2/5/1/0/0; Fall 2/3/4/2/5/1/2/2,
2/3/5/2/5/1/1/2, 3/3/1/0/5/0/1/2, 2/2/1/0/5/0/1/1; Winter 2/2/3/2/5/1/1/2, 2/0/3/2/5/1/0/2,
2/0/0/2/5/1/0/2, 3/0/0/0/4/0/0/1.

The vanilla boards are kinder to Mining than the custom ones (Standard has Mining askable in 12 of
16 weeks, Remixed in 12; custom had it in 4), because the vanilla Boiler Room asks for more distinct
things. Farming dies in Winter on both vanilla boards (0 askable weeks 14 to 16; the crops are done).
Fishing has the same Spring week 3 and 4 hole everywhere. Artisan and Kitchen stay 0 to 3 all year
on every board. Mixed is 5 nearly always.

Judgement rows. Standard (9): the six fruit-tree fruits in Artisan (Apple 9, Apricot 13, Orange 5,
Peach 5, Pomegranate 9, Cherry 13), Winter Root 13 and Snow Yam 13 (Winter Foraging), Cave Carrot 1
(Exotic Foraging). Remixed (3): Winter Root 13 (Winter Foraging), Moss 1 (Forest), Pomegranate 9
(Enchanter's). **Unknown items: Standard Hay (placed by 0.16.148, not yet deployed); Remixed Spring
Onion `(O)399` in Spring Foraging, Jeff to rule.**

## 2026-08-29 (afternoon): sim standardP3 was NOT the Standard board (custom, seed loop 100); corrected

**CORRECTION (same day):** the run was labelled standardP3 but the sim's `tly_reset` built an
`engine manifest (loop 100, seed loop 100)` board. `tly_bundlesource Vanilla Default` only sets the
config in memory; the relaunch before the sim reloaded `config.json` (`BundleSource: Engine`) and a
reset re-stamps the source from config (`RunController.cs:584`). So this is a second CUSTOM-board
year (seed loop 100), not Standard. The numbers below are still valid for what they are. The real
Standard run is the next section; the deployed `config.json` now says `"BundleSource": "Normal"`
(backup `config.json.bak-before-Normal` in the scratchpad).
`bash tools/sim-year.sh minimal standardP3` on build 0.16.145, minimal player: donates only the gate
demand, a quarter per week. Transcript: `docs/superpowers/notes/2026-08-29-sim-standardP3.txt`.
No WARN or ERROR lines in the whole run. Earlier today the same board was also driven to the Winter
28 win by hand (`tly_playseason` at Winter 8, 106 slots).

**Every gate passed.** Ledger at each season end 26 / 48 / 71 / 97 slots (quarter 4 flipped 6 / 5 /
5 / 6; per-season plan 26 / 22 / 23 / 26). Compare custom P2 last night on seed loop 3: 22 / 50 / 79 / 100;
this board (seed loop 100) front-loads Spring and ends lighter. Audit: `no impossible gates. 30
tight, 0 never gated. 2 stretch lines, 4 without a hard item, 5 Spring tight.` Stretch: Engineer's
`[stretch: Iridium Ore Summer]` (the one the seed audit kept finding) and Field Research `[stretch:
Coconut Summer]`. No hard item: Exotic Foraging, Mineral, Four Seasons Sampler, Orchard. Spring
tight: Four Seasons Sampler, Quality Fish, River Fish, Spring Crops, Spring Foraging.

Askable goals by week (Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki): Spring 3/3/3/2/5/3/0/0, 3/4/4/0/5/0/0/1,
3/5/2/0/5/0/0/1, 2/4/0/0/5/0/0/0; Summer 4/3/3/3/5/4/1/2, 5/4/3/2/5/1/1/2, 2/5/2/2/5/1/1/2,
2/1/1/1/5/1/0/1; Fall 3/4/2/2/5/3/1/1, 4/5/2/0/5/0/1/1, 0/5/3/0/5/0/1/1, 0/4/0/0/4/0/1/0; Winter
4/2/2/2/5/2/1/1, 4/2/3/0/5/0/1/1, 2/3/3/0/5/0/1/1, 3/0/3/0/5/0/0/0. Same shape as custom: Mixed
always 5, Mining and Spelunking thin outside week 1 of a season (Summer is the exception, 3/2/2/1),
Artisan and Kitchen 0 to 2 all year. Fishing dies in Spring week 4 and Fall week 12; Foraging in
Fall weeks 11 and 12.

Judgement rows (7): Apple week 9 (Fodder), Prize Ticket week 2 and Mystery Box week 3 (Helper's),
Winter Root week 13 (Four Seasons Sampler), Snow Yam week 13 (Winter Foraging), Cave Carrot week 1
(Exotic Foraging), Tea Leaves week 4 (Spring Crops). **Unknown items (2), gate treats as Winter,
Jeff to rule:** Hay `(O)178` in Fodder; Treasure Appraisal Guide `(O)Book_Artifact` in Field Research.

## 2026-08-29: per-slot ledger mirrored from the CC board, 0.16.135 to 0.16.146

Jeff's #1 priority (TODO, 2026-08-28; found live on emmalution's stream). Spec
`docs/superpowers/specs/2026-08-29-per-slot-ledger-design.md`, plan
`docs/superpowers/plans/2026-08-29-per-slot-ledger.md`, ten tasks, one commit each, 1754 tests green.
Committed LOCALLY ONLY, not pushed, not released.

**What changed.** `RunState.DonatedSlots` (bundle index, ingredient index, id) replaces the flat
`DonatedItemIds` list, which stays on the class as a legacy field so old saves deserialize.
`BundleRequirement` carries `BundleIndex` and a positional `Slots` list (duplicates kept; category
slots skipped without renumbering), and `MissingForSeason(season, ledger)` is the one method the
gate, the Season Goals page and `tly_gateneeds` all read. `CreatePerItem` / `CreateSeasonal` take
`NumberOfSlots` from the slot count, so vanilla Construction (Wood, Wood, Stone, Hardwood) is 4 of
4, not 3 of 3 (a second defect the TODO write-up did not name). `ItemDonationSync.Reconcile` is a
whole-replace mirror of the board's per-slot state and runs on save load (the migration), before the
Season Goals page, before the day-end gate and inside `tly_playseason`. Debug donations
(`tly_donate`, `tly_testdonate`, `tly_playseason`) fill the vanilla slot first through
`CcSlotWriter`, because a ledger-only write would be wiped by the next mirror. Rulings: mirror, not a
second "ledger AND board" check (a page saying complete while the gate fails is the worst outcome);
one slot one goal (Construction's second Wood slot is its own weekly goal once the first is filled;
`SlotPoolBuilder` already did this, now pinned by a test).

**Live checks over the bridge, build 0.16.143, throwaway save `None_447665404` (rotated by the
reset), log lines quoted:**

- Load of last night's fully donated board: `Ledger mirrored from the CC board: 101 slot(s) filled.`
  (the migration path; the old id list was ignored).
- `tly_reset` to a fresh board: `Ledger mirrored from the CC board: 0 slot(s) filled.`, then
  `tly_gateneeds: Spring day 1: 18 bundle(s) still owed before Summer 1, 0 slot(s) filled on the board.`
  with one line per bundle (`Construction (PerItem, 0/4 filled): needs 2 before Summer 1: Clay, Stone`).
- `tly_donate (O)709` (Hardwood): `Donated '(O)709' into bundle 13 slot 4. Ledger 1 slot(s).`
  Tapper's left the owed list, Construction stayed `0/4`, `17 bundle(s) still owed`. A second
  `tly_donate (O)709`: `No open slot wants '(O)709'. Nothing donated.` The custom board never asks an
  item twice (0.16.30), so the shared-item case cannot be reproduced on it; it is unit-tested
  (`One_deposit_credits_one_bundle_not_every_bundle_listing_the_id`) and applies to the vanilla and
  remixed boards.
- `tly_runstate`: `slots filled=1`.
- `tly_playseason`: `21 slot(s) flipped, vault 1/1`, `Spring gate WOULD PASS. Ledger 22 slot(s).`;
  `tly_gateneeds`: `0 bundle(s) still owed before Summer 1, 22 slot(s) filled on the board.`;
  `tly_setday 28` and `debug sleep`: `Month cleared (Spring). Advancing.`

**Second pass, desktop authorised (Jeff out), build 0.16.145 (`tly_seasongoals` opens the page),
Standard vanilla board via `tly_bundlesource Vanilla Default` + `tly_reset`, save `None_447693385`
lineage. Everything the first pass could not reach, all from the log and PrintWindow screenshots:**

- Construction (Wood, Wood, Stone, Hardwood): `tly_donate (O)388` -> `bundle 17 slot 0`, `(O)390` ->
  `slot 2`, `(O)709` -> `slot 3`, `Ledger 3 slot(s)`; the page shows **Construction (Foraging) 3/4**;
  a second `tly_donate (O)388` -> `bundle 17 slot 1`, `Ledger 4 slot(s)`, page **4/4 checkpoint
  met**; a third Wood: `No open slot wants '(O)388'`.
- Shared item, Parsnip `(O)24` (Spring Crops and Quality Crops both list it): first donate ->
  `bundle 0 slot 0`, `tly_gateneeds` then shows `Spring Crops 1/4 needs 3` AND `Quality Crops 0/3
  needs 1: Parsnip, ...` (not credited there); second donate -> `bundle 3 slot 0`, Quality Crops
  satisfied; third refused. Page: **Spring Crops 1/4 needs 3** (Green Bean, Cauliflower, Potato
  left), **Quality Crops 1/3 checkpoint met**.
- The whole page, scrolled through (Bus Repair 0/1, Exotic Foraging 0/5, Animal 0/5, Artisan 0/6
  needs 2, Crab Pot 0/5, the fish bundles, Adventurer's, Geologist's needs 2, Chef's, Dye, Field
  Research): every row matches the `tly_gateneeds` line for that bundle.
- The Winter win on the Standard board: `debug season winter`, `tly_playseason` (`101 slot(s)
  flipped, vault 4/4`, and it logged `Construction (PerItem): donated Wood ((O)388) slot 1`, the
  doubled slot), `tly_gateneeds: Winter day 8: 0 bundle(s) still owed before the win, 106 slot(s)
  filled on the board`, `tly_setday 28`, `debug sleep`: `Day-28 cutscene: opening the Win Junimo
  scene`. The win needs every slot on the board, Construction's second Wood included.
- Gotcha found on the way (runbook updated): Escape does not close the Season Goals page (click its
  X); a page left open across `tly_reset` keeps the old run's rows and `tly_seasongoals` is refused
  with `Cannot open menu: another menu is already open`. The four identical "4/4" screenshots that
  looked like a counting bug were that stale instance.

## 2026-08-29: the obtainable board, 0.16.85 to 0.16.134

Spec `docs/superpowers/specs/2026-08-28-obtainable-board-design.md`, five plans
`docs/superpowers/plans/2026-08-28-obtainable-board-1-model.md` through `-5-sims.md`, commits
83c192b (0.16.85) to 1669975 (0.16.133) plus this review fix wave (0.16.134), every plan
reviewed, 1741 tests green.
Per-plan ledgers with every ruling: `.superpowers/sdd/2026-08-28-obtainable-board-*/progress.md`.
Committed LOCALLY ONLY, not pushed, not released.

**Plan 1, the two-week model (0.16.85 to 0.16.96).** Every item now carries two weeks: a pacing week
(the week a normal player realistically reaches it) and a hard week (the earliest week it is possible
at all). `ItemAvailabilityModel` is built with a `WeekMode` (Pacing, HardGates, HardAll) taken from
the difficulty step, and answers `Week` for gates and `GoalWeek` for goals from that mode, so a Hard
board gates on the hard week while Normal paces. Rule E's tiers became absolute effort bands instead
of per-board quartiles; `SeasonNeed` and `BonusItemSampler` arithmetic follow; goals no longer look
half a season ahead of the gate (they follow the gate exactly); goal ceilings are flat 5 in every
season. The hand-written `DefaultItemSeasonPins` table is retired down to three rows (Red Mushroom,
Sea Urchin, Woodskip). New rules: tapper goods from Data/WildTrees TapItems at Foraging 4 plus the
row's nights, fishing trash (167 to 172) at week 1, machine weeks from recipe price and friendship
(`UnlockWeeks`), book weeks from a table with the year-2 and drop-only books out of the pool.

**Plan 2, stretch gates and the hard-item rule (0.16.97 to 0.16.107).** The Spring foothold is gone.
In its place, `StretchRule`: a bundle that gains nothing new in a season reaches two weeks past that
season, swapping a stretch item in if it holds none, and the gate then demands that line. Stretch is
a pacing mechanism only, so `StretchRule.Applies` returns false on HardGates and HardAll (on Hard the
gates already demand by hard week). Separately, every rolled bundle of 4 or more slots must hold at
least one genuinely hard item (effort 6 and up); that rule keeps applying on Hard and Extreme and is
exempt only on Easy. Both surface in `tly_gatecheck` (`[stretch: item season]`, `[no hard item]`) and
on the weekly cards. `BundleRequirement` carries `StretchLines`, recomputed from the model whenever
requirements are rebuilt.

**Plan 3, full pools with no fixed lists (0.16.108 to 0.16.119).** Every TLY Custom bundle keeps its
name, room and pick count but rolls its slots from the full pool of its kind; the mixed-kind bundles
roll from a named recipe in the new `BundlePoolRecipes` table (ordered parts, each a source and a
count). `PoolDomainClassifier` returns `Recipe` for any non-money bundle it cannot place in a legacy
domain, and an Other-majority bundle rolls from its own vanilla ids only and audits `[no recipe]`
until Jeff names one. String-id vanilla items (Goby, the jellies, Broccoli, Moss, Mystery Box,
Book_*) weigh like vanilla, 3, since vanilla is now "any id without a dot". Pool additions at weight
1: Stonefish, Ice Pip, Lava Eel, the five legendaries, and the year-2 crops Garlic, Red Cabbage and
Artichoke (Easy still excludes the year-2 crops). A legendary drawn into a 4-of-4 fish bundle is
mandatory for that bundle, and the rewind now clears legendary catches so it can be caught again.

**Plan 4, the Garden Pot keep and two Boosts (0.16.120 to 0.16.125, plus the fix wave at 0.16.128).** A permanent Garden Pot recipe
keep at the Junimo Shrine, 750 JP, `Obtainability` category, granted back after the rewind like the
book keeps. Two in-loop Boosts, bought at the farm's planning shrine (`ShrinePreviewMenu` gains a
Boosts section): Year-Two Seeds, 75 JP, makes Mixed Seeds roll the season's year-2 crop at 5 percent
for the current week; Sneak Peek, 100 JP, makes the Queen of Sauce air the year-2 episode for the
season (it grants both the year-1 and the year-2 recipe, so no year-1 recipe is lost). Year-2 crops
carry hard weeks 2 / 6 / 10 and pacing weeks Garlic 4, Red Cabbage 7, Artichoke 11.

**Plan 5, diagnostics and sims (0.16.126 to 0.16.133, interleaved with plan 4's fix wave at 0.16.128).** `tly_playseason quarter <k>` donates the
season's gate share a quarter a week, round-robin across bundles (one open demanded slot per bundle
per pass) before the prefix cut; `tly_reset <seedLoop>` and `tly_genbundles <seedLoop>
[custom|standard|remixed]` pin the board seed and roll vanilla boards through the same audit;
`tools/sim-year.sh <mode> <label> [seedLoop]` prints all 16 weeks, the gate audit, the judgement rows
and the unknown items. Runbook `docs/HEADLESS_DRIVING.md`.

### Sims P2 (minimal player) and Q2 (goal-completing player), both on seed loop 3, build 0.16.132

Gate ledger per season, then the season verdict. Read the four quarter figures as the quarter's
CUMULATIVE POSITION IN THE SEASON'S DONATION PLAN, not as slots actually flipped: the sim's plan is
computed once from the season baseline and each quarter donates a prefix of it, so a quarter whose
prefix was already in the ledger flipped nothing while still reporting its position. Q2 Spring
quarter 3 is exactly that case: it reported 15 and flipped 0, because the goal deposits had already
covered those steps. 0.16.134 splits the two numbers in the log line (flipped, donated this season,
plan position of plan size) and lets a quarter reach past already-donated steps, so a later rerun
will not repeat this ambiguity.

| Season | P2 quarters | P2 verdict | Q2 quarters | Q2 verdict |
|---|---|---|---|---|
| Spring | 6 / 11 / 17 / 22 | WOULD PASS, ledger 22 | 5 / 10 / 15 / 20 | WOULD PASS, ledger 23 |
| Summer | 7 / 14 / 21 / 28 | WOULD PASS, ledger 50 | 6 / 12 / 18 / 24 | WOULD PASS, ledger 53 |
| Fall | 8 / 15 / 22 / 29 | WOULD PASS, ledger 79 | 6 / 11 / 16 / 21 | WOULD PASS, ledger 79 |
| Winter | 6 / 11 / 16 / 21 | WOULD PASS, ledger 100 | 4 / 8 / 12 / 16 | WOULD PASS, ledger 101 |

Askable goals by week, P2 (Foraging / Farming / Fishing / Mining / Mixed / Spelunking / Artisan / Kitchen):

| Week | Fo | Fa | Fi | Mi | Mx | Sp | Ar | Ki |
|---|---|---|---|---|---|---|---|---|
| Spring 1 | 2 | 2 | 4 | 0 | 5 | 1 | 0 | 1 |
| Spring 2 | 3 | 3 | 5 | 0 | 5 | 0 | 1 | 1 |
| Spring 3 | 1 | 4 | 4 | 0 | 5 | 0 | 1 | 1 |
| Spring 4 | 1 | 2 | 0 | 0 | 5 | 0 | 1 | 1 |
| Summer 5 | 4 | 3 | 3 | 2 | 5 | 2 | 2 | 0 |
| Summer 6 | 4 | 5 | 4 | 0 | 5 | 0 | 2 | 2 |
| Summer 7 | 3 | 5 | 0 | 0 | 5 | 0 | 3 | 1 |
| Summer 8 | 2 | 5 | 0 | 0 | 5 | 0 | 1 | 1 |
| Fall 9 | 3 | 3 | 3 | 2 | 5 | 2 | 2 | 2 |
| Fall 10 | 4 | 5 | 3 | 0 | 5 | 0 | 3 | 2 |
| Fall 11 | 3 | 5 | 2 | 0 | 5 | 0 | 3 | 2 |
| Fall 12 | 3 | 0 | 0 | 0 | 5 | 0 | 1 | 2 |
| Winter 13 | 3 | 3 | 2 | 2 | 5 | 1 | 2 | 2 |
| Winter 14 | 3 | 3 | 2 | 0 | 5 | 0 | 2 | 1 |
| Winter 15 | 1 | 4 | 2 | 0 | 5 | 0 | 2 | 1 |
| Winter 16 | 1 | 2 | 1 | 0 | 4 | 0 | 1 | 0 |

Askable goals by week, Q2 (same columns):

| Week | Fo | Fa | Fi | Mi | Mx | Sp | Ar | Ki |
|---|---|---|---|---|---|---|---|---|
| Spring 1 | 2 | 2 | 4 | 0 | 5 | 1 | 0 | 1 |
| Spring 2 | 3 | 1 | 5 | 0 | 5 | 0 | 1 | 1 |
| Spring 3 | 0 | 2 | 3 | 0 | 5 | 0 | 1 | 1 |
| Spring 4 | 0 | 2 | 0 | 0 | 4 | 0 | 1 | 1 |
| Summer 5 | 4 | 3 | 3 | 2 | 5 | 2 | 2 | 0 |
| Summer 6 | 4 | 5 | 2 | 0 | 5 | 0 | 2 | 2 |
| Summer 7 | 0 | 5 | 0 | 0 | 5 | 0 | 3 | 1 |
| Summer 8 | 0 | 2 | 0 | 0 | 2 | 0 | 0 | 0 |
| Fall 9 | 3 | 3 | 3 | 2 | 5 | 2 | 2 | 2 |
| Fall 10 | 3 | 2 | 3 | 0 | 5 | 0 | 1 | 2 |
| Fall 11 | 3 | 0 | 1 | 0 | 5 | 0 | 1 | 2 |
| Fall 12 | 0 | 0 | 0 | 0 | 2 | 0 | 1 | 1 |
| Winter 13 | 3 | 3 | 2 | 2 | 5 | 1 | 2 | 2 |
| Winter 14 | 3 | 2 | 2 | 0 | 5 | 0 | 1 | 0 |
| Winter 15 | 1 | 0 | 2 | 0 | 3 | 0 | 0 | 0 |
| Winter 16 | 1 | 0 | 1 | 0 | 2 | 0 | 0 | 0 |

Week 4 of a season is alive again in both runs (it was 0 across the board before the round-robin
fix). Mining is still thin: 2 askable in week 1 of Summer, Fall and Winter, 0 everywhere else, 0 all
Spring. Spelunking is 1 or 2 in the first week of a season and 0 otherwise. Both look like a
pool-size problem, not a donation-order one. Full sim outputs and board dumps are in the scratchpad
(`simP2.txt`, `simQ2.txt`, `board-P2.md`, `board-Q2.md`); the two dumps are byte-identical except the
`loop seed` header line, so the seed pin fixes the board but not the loop's own RNG seed.

Because of that, P2 and Q2 share the BOARD but not the theme offers: the weekly theme roll reads the
run seed, which the pin does not fix. So the askable differences between the two tables are not
attributable to the goal deposits alone; part of the gap is simply a different offer sequence. Any
conclusion of the form "goal deposits drain the pool by week 4" needs a run where both sims share the
run seed as well.

### Gate audit (identical in both runs)

`tly_gatecheck RESULT: no impossible gates. 26 tight (demands everything obtainable by then), 0
bundle(s) never gated.` and `tly_gatecheck RESULT: 1 stretch line(s), 5 without a hard item, 2 Spring
tight.` Unknown items: 0 (was 1, Crispy Bass, fixed by the Placeable filter in 0.16.132). Tag lines:
`[stretch: Battery Pack Summer]` on Construction; `[no hard item]` on Four Seasons Sampler, Tapper's,
Garden, Orchard and Weatherman's; `[spring tight]` on Garden and Forager's. No `[no recipe]` line on
either board. Vault gate unchanged: 1 money bundle by Spring 28, 2 by Summer, 3 by Fall, 4 by Winter
(2,500g / 5,000g / 10,000g / 25,000g), satisfied outright by owning `keep_bus_unlocked`. The audit
checks calendar feasibility only: an item that exists in Spring but needs a keg, a fish pond or a
tool upgrade still counts as obtainable there.

### Judgement rows (9, unchanged by the fixes)

Rows placed by Jeff's own ruling rather than a game-data fact. They gate and appear on cards like any
other rule, but are worth a second look:

- Skeleton Mask `(H)8`, week 3, in Gil's Trophies
- Vampire Ring `(O)522`, week 4, in Gil's Trophies
- Burglar's Ring `(O)526`, week 6, in Gil's Trophies
- Insect Head `(W)13`, week 3, in Gil's Trophies
- Mystery Box `(O)MysteryBox`, week 3, in Helper's
- Prize Ticket `(O)PrizeTicket`, week 2, in Helper's
- Moss `(O)Moss`, week 1, in Tapper's
- Cave Carrot `(O)78`, week 1, in Exotic Foraging
- Snow Yam `(O)416`, week 13, in Exotic Foraging

### Vanilla boards through the same audit (`tly_genbundles <seed> standard|remixed`, seeds 0 to 9)

Standard is ONE board, not ten: it reads `Data/Bundles` verbatim and never touches the seed, so the
ten "seeds" all audited the same board and the determinism self-check compared it to itself (it was
vacuous there). 0.16.134 says so in the command output and skips that self-check for standard.

| Seeds | Sp/Su/Fa/Wi demanded | season share % | tight | impossible | stretch | no hard item | spring tight |
|---|---|---|---|---|---|---|---|
| 0 to 9, one board (the seed is ignored) | 25/52/83/105 | 9/20/31/40 | 32 | 0 | 0 | 7 | 2 |

Remixed (bundle contents randomized per seed):

| Loop | Sp/Su/Fa/Wi demanded | season share % | tight | impossible | stretch | no hard item | spring tight |
|---|---|---|---|---|---|---|---|
| 0 | 22/46/74/93 | 9/20/31/40 | 30 | 0 | 0 | 7 | 2 |
| 1 | 22/46/75/95 | 9/19/32/40 | 31 | 0 | 0 | 9 | 2 |
| 2 | 20/42/68/89 | 9/19/31/41 | 33 | 0 | 1 | 6 | 2 |
| 3 | 23/48/75/96 | 10/20/31/40 | 31 | 0 | 0 | 7 | 2 |
| 4 | 19/44/71/93 | 8/19/31/41 | 33 | 0 | 1 | 7 | 2 |
| 5 | 21/46/72/92 | 9/20/31/40 | 29 | 0 | 1 | 6 | 2 |
| 6 | 23/44/71/89 | 10/19/31/39 | 30 | 0 | 1 | 5 | 2 |
| 7 | 21/45/73/93 | 9/19/31/40 | 28 | 0 | 0 | 6 | 2 |
| 8 | 25/46/73/91 | 11/20/31/39 | 28 | 0 | 0 | 6 | 2 |
| 9 | 24/47/72/93 | 10/20/31/39 | 32 | 0 | 1 | 6 | 2 |

Zero IMPOSSIBLE bundles in all 20 runs, which is 11 distinct boards: 1 standard (the same board ten
times) plus 10 remixed. Every remixed stretch line is the same one: Engineer's PerItem,
`[stretch: Iridium Ore Summer]` (seeds 2, 4, 5, 6, 9), which looks like a structural artifact of that
one vanilla recipe rather than anything the stretch rule does wrong. It is NOT the same line Custom
raises: Custom's single stretch line on this board is `[stretch: Battery Pack Summer]` on
Construction, a different bundle and a different item, so the two are not one shared cause.

Vanilla demand is heavier than Custom's 21/50/79/100 at every season, but the two numbers are not the
same measurement: 21/50/79/100 is the CUMULATIVE LEDGER the sim ended each season with (items actually
donated, including goal deposits and anything donated beyond the gate), while the vanilla rows count
what the GATE DEMANDS by each season's day 28. The honest comparison is demand against demand; the
ledger figure runs one or two items above the demand it satisfies. Vanilla is also tighter overall
(standard 32 tight, remixed 28 to 33, against Custom's 26); season shares match Custom's back-loaded
curve closely. No-hard-item counts run higher in vanilla (6 to 9) than Custom's 5. Summary: scratchpad `vanilla-boards-summary.md`.

### Open for Jeff: rule on these or look at them

Collected from the five plan ledgers' `Ruling:` lines:

- **The difficulty step.** No single overall difficulty setting exists (there are ten dials), so the
  **ItemRarity** dial was made "the step" everywhere: it drives `WeekModes.For`, the stretch rule and
  the pools. Renaming the driver dial is a one-line change if Jeff wants a different one.
- **Dehydrator week 3 by cost.** The cost table Jeff adopted beats the cave route; the spec prose
  said 6 before the table existed. If wrong, Dehydrator goods are three weeks early on Normal.
- **BlackberryWeek stays 10** (a bush calendar fact; the ground-forage rule already yields 9 and the
  earliest wins).
- **Field Research rolls forage quality.** The `RecipeRollDomain` tie-break left as is, flagged.
- **Winter Star and Fish Farmer's roll from narrow pools.** Accepted, flagged.
- **Five recipe rows are approximations** because `PoolItem` carries no name or tags: Fodder (grains
  by id plus the fruit category, no category -75, so it never asks for a non-grain vegetable), Wild
  Medicine (mushroom ids plus category -81), Children's (fixed sweet list), Enchanter's (essences by
  id, the ' Essence' suffix), Chef's ingredient half (Crops, Forage, Egg, Milk, AnimalProduct plus
  five staples), Field Research shells (fixed list). Jeff sees the rolled boards in the genbundles run.
- **P2 and Q2 do not share a run seed.** `tly_reset <seedLoop>` pins the BOARD only; the weekly theme
  offers roll off the run seed, which the pin does not fix. The two sims' askable tables therefore
  differ for two reasons at once (goal deposits and a different offer sequence), and nothing in the
  current data separates them. If Jeff wants the goal-deposit effect measured, the sims need a run
  seed pin too.
- **Mining and Spelunking are thin all year.** The Boiler Room is three bundles and filler is one per
  bundle per week, so those two themes have almost nothing askable outside the first week of a season.
- **Year-Two Seeds has no live proof.** There is no plant debug command, so it rests on the Trace hook
  plus unit tests; Jeff should plant Mixed Seeds once with the trace on to see the 5 percent roll.
- **The shrine Boosts menu is unexercised over the bridge.** The draw and click path needs one human
  look at the planning shrine.
- **Garlic pacing moved 3 to 4** (75 JP is about three weekly bonuses, so run 1 may not have it at 3);
  Red Cabbage 7 and Artichoke 11 stand. If wrong, Garlic is a week early or late on cards.
- **The deployed `config.json` was edited for the sims.** `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\config.json`
  is Jeff's live file and a deploy does not
  overwrite it; its `ThemeFillerBySeason` was still the pre-0.16.82 `[0, 1, 2, 99]` and is now the
  current default `[99, 99, 99, 99]`. It is NOT in any commit; a backup of the old file is in the
  scratchpad as `config.json.bak`.
- **Existing engine saves demote once on first launch.** The model now decides board bytes, so a save
  made on an older board fails the SaveLoaded manifest re-derivation and falls back to the legacy
  read path (WARN in the log), the same as every earlier pool change. The board on disk stays valid
  until the next reset.

**2026-08-28: activity themes (Spelunking, Artisan, Kitchen) and the theme-week economy, 0.16.42 to
0.16.67.** Spec `docs/superpowers/specs/2026-08-27-activity-themes-design.md`, plan
`docs/superpowers/plans/2026-08-28-activity-themes.md`, 28 tasks, one behaviour per commit.
- Phase 1 (0.16.42 to 0.16.55): effort rules derived from game data for gems and minerals (mine
  node floors), geodes, monster drops, artifacts, animal products, artisan goods, fish ponds,
  cooked dishes, crops and forage (`Core/Availability/*Availability.cs`, composed by
  `EffortComposer`); `tly_dumpeffort` writes `item-effort-model.md` (gitignored, copy to `docs/`
  for review); `tly_itemmodel` prints source and tier. **Decision: these rules produce effort
  only.** No season floor moves, so no day-28 gate changes; an effort-only id still floors at
  Winter for gates exactly as before.
- Phase 2 (0.16.56 to 0.16.62): the eight-member `Theme` enum, `ItemKind` classifier and
  `ThemeDomains`; rule A (`BundleRequirement.DueItemsFor`, the 0.16.41 stopgap folded into the
  filler tier); rule B (`ThemeFillerBySeason` config, default 0/0/1/99 since 0.16.73, one filler per bundle);
  rule E (effort quartile tiers x season weights, `GoalWeighting`); rule C (`SelectionService`
  offers only themes with 2+ askable goals, weighted by count, room themes as the floor; the hub,
  console pick and re-roll all go through `RunController.OfferFor`); rule D (weekly bonus paid
  per goal, `BonusSlot.Paid` guard, `hud.goal-paid`); `tly_themepool [theme]`.
- Phase 3 (0.16.63 to 0.16.67): the five effects. `MonsterThemePatches` (monster drops doubled
  10%, monster damage +25%), `MachineSpeedPatch` (0.75x / 1.25x on `Object.OutputMachine`, rounded
  to 10 min), `CookedFoodWeakPatch` (three postfixes on the consumption methods, category -7),
  `AnimalDoubleProductPatch` (records in `RunState.DoubleProduceToday`, cleared on DayEnding).
- Not done here: the real-play simulation (Jeff's call; `tools/sim-season.sh` and
  `tly_playseason` untouched) and the in-game effect confirmations from the spec's live list.
  The spec's Dinosaur Egg vs Diamond tiering claim does not hold under its own formula (Dino Egg
  min 3 from Mountain spots, Diamond 5): the review document shows the real numbers.
- `TODO.md` carries an uncommitted "2026-08-28 brainstorm batch" block that is not from this
  build and was left alone; its "SPEC APPROVED, NOT PLANNED: activity themes" heading is now out
  of date (built, not real-play tested).

**Last updated:** 2026-08-29 late night (review fixes 0.16.166, sim diagnostics 0.16.167; two full-year sims on the boost build)
**Branch:** `master`; 0.16.167 PUSHED and RELEASED; nothing local-only
**Tests:** 1822 passing, 0 failing
**Build:** clean; 0.16.167 deployed to the game; game CLOSED at end of session
**Last public release:** 0.16.167 (2026-08-29 night; GitHub release + Nexus file, version, description and changelog all live)

**2026-08-28 late: 20-loop audit of the 0.16.26 to 0.16.31 pool fixes, report in
`docs/superpowers/AUDIT-2026-08-29-bundle-loops.md`.** 20 diagnostic boards (`tly_genbundles 0..19`),
6 real `tly_reset` cycles (Runs 59 to 63), weekly goals for all four seasons on every live board
plus a real season advance on Run 62. All five of last night's fixes observed working (no wrong-water
fish, no any-season item in a season-named bundle, Night Fishing night-only, festival fish gated to
their season, Trash gone from the fish pool); `tly_gatecheck` found no impossible gate on any live
board. Found and fixed, one commit each, verified in-game after redeploy:
- 0.16.32 diagnostics: `tly_genbundles` lists every slot, logs gates, runs the gate audit; `tly_goals`.
- 0.16.33 PerItem weekly goals pass the obtainability predicate (Sturgeon offered in Fall, Rainbow Trout in Winter).
- 0.16.34 weekly-goal forage seasons from the engine forage pool (Chanterelle and Purple Mushroom offered in Summer; Ginger Island rows).
- 0.16.35 Jeff's request: at most one fruit-tree fruit per theme's weekly goal list.
- 0.16.36 weekly goals honour location floors (Scorpion Carp offered in Summer).
Open, need Jeff's ruling (details in the report): Red Mushroom sits in the Spring forage pool but is
pinned Summer, so loop 16's Spring Foraging audits IMPOSSIBLE; Night Fishing's "one market fish" is
Sea Cucumber or Octopus because the real trio is `ExcludeFromRandomSale`; seasonal forage bundles
repeat items on 14 of 20 boards because their pools are 6 or 7 items and the fixed lists take them
first. `docs/engine-bundle-catalogue.md` regenerated (it is gitignored, so it lives locally only).
Throwaway save is now `None_447607703` (left in place).

**2026-08-28 bundle pool fixes (two player reports: Flounder on three bundles incl. a lake bundle,
Mussel on four foraging bundles; then Salmon as a Spring weekly goal and Sea Cucumber due before
Summer 1).** Root cause traced against the game's own Data/Locations (dumped via a scratch .NET
loader): the pools treated three non-fishing keys (`Temp` = Festival of Ice map, `fishingGame` =
Fair minigame, `Default` = trash table) as habitats, and Night Market / SquidFest rows carry no
season. Landed, one behavior per commit:
- 0.16.26 ignore non-habitat location keys (fixes ocean fish in Lake Fish, river fish in Ocean Fish,
  Salmon reading as all-year, Trash in the fish pool).
- 0.16.27 season-named bundles ask only season-specific items; Winter Root + Snow Yam join Winter.
- 0.16.28 Night Fishing = fish not catchable before 6pm, plus at most one Night Market fish (Jeff's rule).
- 0.16.29 passive-festival spawns take the festival's season from Data/PassiveFestivals (Sea Cucumber
  Fall/Winter, Squid Winter); season tokens must be season names (Enum.TryParse accepted "0600").
- 0.16.30 no item asked twice across the board (fills run tightest pool first, each avoids what
  earlier bundles ask; repeats only when a pool would run dry).
- 0.16.31 `tly_dumpbundles` catalogue wording describes the fish rules.
**Not yet verified in-game.** Next: fresh reset on a throwaway clone, `tly_genbundles`, `tly_gatecheck`,
regenerate `docs/engine-bundle-catalogue.md` via `tly_dumpbundles`. Known consequence: a save mid-loop
on an older board fails the SaveLoaded manifest re-derivation and falls back to the legacy read path
(WARN in log), same as every earlier pool change; the board on disk stays valid until the next reset.

**Decision 2026-08-27 late (TODO walk with Jeff):** next build is **keep wallet items + Stardrops via
per-item JP keeps at the shrine** (same shape as the book keeps). Brainstorm, then spec, then plan; see
`docs/superpowers/HANDOFF-2026-08-27-wallet-stardrops.md`. TODO headings were caught up with the
0.14.0 / 0.14.2 / 0.16.0 / 0.16.17 releases (1123181, rose1729, difficulty, books, deja-vu all shipped).
The settings screenshots (Features, Difficulty) are confirmed live on Nexus (gallery + description) and
GitHub. The Egg Hunt per-loop guard is still open: 0.14.1 only guards once per DAY.

## 2026-08-28 (evening): the even year, 0.16.73 to 0.16.83

Spec `docs/superpowers/specs/2026-08-28-even-year-availability-design.md`, plan
`docs/superpowers/plans/2026-08-28-even-year-availability.md`. Every item on a board now has a
first week it can exist (mines 30 floors a week, Skull Cavern from Fall, machines by skill level,
animals by building tier, crops by first harvest, forage by first spawn plus location); goals may
name an item only from that week; per-item deadlines clamp to the gate season; pick-X-of-Y ramps
derive from their own items (curated table retired); every re-rolled season-less bundle keeps a
Spring foothold; goal ceilings 5/5/5/6 budgeted over the weeks left; the goals may run half a
season ahead of the gate and no further; `tly_dumpavailability` lists every item with Week, Gate,
Placed and ends with the Unknown items Jeff rules on (memory `tly-sim-list-unknowns-each-run`).
Jeff's rule (2026-08-28): the floor only stops an item showing up too early; nothing may force
one to show up. Headless sims (`tools/sim-year.sh`), gates as cumulative required slots:

| Sim | Build | Player | Gates Sp/Su/Fa/Wi | Winter weeks 1 and 2 (Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki) | Unknown |
|---|---|---|---|---|---|
| G | 0.16.78 | gate-only | 19/40/67/98 (19/41/68/100%) | 3/2/3/3/6/1/1/3 then 4/3/4/3/6/1/1/3 | 20 |
| H | 0.16.78 | goal-completing | 19/50/82/103 | 2/1/2/1/4/0/1/0 (board nearly done: 84 of 96 by Fall 28) | 17 |
| L | 0.16.81 | goal-completing | 23/45/73/102 | 3/4/3/1/6/0/2/2 then 4/0/4/1/6/0/0/1 | 6 |
| M | 0.16.81 | gate-only | 24/46/79/98 | 3/4/3/2/6/2/2/2 then 4/5/4/2/6/2/2/2 | 0 |
| N | 0.16.82 | goal-completing | 21/49/74/96 | 1/1/3/1/6/0/0/0 then the same (22 lines left, all Winter-only) | 1 (Pickles) |

Weeks 3 and 4 of every season carry goals for both players since 0.16.82. Open question for
Jeff: a goal-completing player's Winter is two themes wide (Mixed and Fishing) because 80 goals a
year on a 96-line board leaves 22 Winter-only lines; a 4/4/4/5 ceiling would leave more, at the
cost of thinner weeks earlier. Sims I, J and K were invalid (a task-stopped sim kept running and
poisoned the next two; see HEADLESS_DRIVING). Not pushed.

## Keep wallet items + Stardrops (0.16.19 to 0.16.25): built, unit-tested, LIVE SMOKE PASSED 2026-08-27 20:43 to 20:50

Spec `docs/superpowers/specs/2026-08-27-keep-wallet-stardrops-design.md`, plan
`docs/superpowers/plans/2026-08-27-keep-wallet-stardrops.md`. Eighteen Carryover rows
(`keep_wallet_*` x11, `keep_stardrop_*` x7, 6,950 JP) reach-gated on `mail:<flag>`,
`event:<id>` or `stardrop_mines`; `RunBaseline.KeptMailFlags` / `KeptEventIds` /
`KeptStardropCount`; `FarmerReset` re-adds the flags after the mail wipe, re-marks kept power
events after the re-seed, and sets max stamina to 270 + 34 per kept Stardrop. Bear's Knowledge
(2120303) and Spring Onion Mastery (3910979) joined `EventGatingTables.Default.ReplayableEventIds`,
so they no longer survive a rewind unless bought (they used to, for free). Debug: `tly_wallet`.
CHANGELOG `## Unreleased` written; README and Nexus description got the Shrine feature line (What's
New waits for the release).

**Live smoke (throwaway save None_447549305, loaded Spring 2 via `tly_loadsave`, driven with
`send-smapi-command.ps1` + `game.ps1`; screenshots `test-output/wallet-0*.png`):**

| Step | Result |
|---|---|
| `tly_wallet HasSkullKey`, `stardrop:fair`, `event:2120303`, `event:3910979`; `tly_wallet` | `HasSkullKey+HasUnlockedSkullDoor-`, `CF_Fair+`, bear `seen`, spring onion `seen`, `maxStamina=304` | PASS |
| `tly_addjp 2000`; buy `keep_wallet_skullkey` (750), `keep_stardrop_fair` (500), `keep_wallet_bearsknowledge` (150) | all three "Purchased", JP 2888 left | PASS |
| `tly_openshop`, Carryover tab | buyable rows first: "Keep Spring Onion Mastery, Cost: 150 JP" (earned, unbought); owned Bear / Skull Key / Stardrop (Fair) at the end; the other fourteen wallet/Stardrop rows hidden (`wallet-02`, `-03`) | PASS |
| `tly_reset` (Spring 2 -> Spring 1, reset log) | `FarmerReset: ... wallet=[HasSkullKey,HasUnlockedSkullDoor,CF_Fair], events=[2120303], stardrops=1 ... eventsReseeded=5 (of 8 seen-ever)` | PASS |
| `tly_wallet` after the reset | `HasSkullKey+HasUnlockedSkullDoor+` (door now open too), `CF_Fair+`, bear `seen`, spring onion `unseen`, `maxStamina=304` | PASS |
| Shrine after the reset | Spring Onion row gone (scene unseen again), Bear / Skull Key / Stardrop (Fair) still Owned (`wallet-06`, `-07`) | PASS |

Not exercised live: the Fair stall refusing a second Stardrop (needs Fall 16; the CF_Fair gate is
vanilla's own check, `Utility.cs:5848`), and the wallet tab of the inventory menu (the E / Escape
key presses were not received by the game, a known driving gotcha; the probe covers the flags).

## netWorldState keep/wipe audit (0.14.8): LIVE SMOKE PASSED 2026-08-27 19:09 to 19:20

Save None_447546774 (renamed to None_447549305 by the reset), driven from the SMAPI console and
`game.ps1`. Baseline taken on Spring 1, then slept to Spring 5 so the leak preconditions existed
(board quest "Delivery: Robin" 300g, dish Parsnip Soup x1, Y1 cart guarantee armed at 5 via the new
`tly_netstate army1 5`, ticking to 4 by day 5), then `tly_reset` (reset #56, run 57). Screenshots
in `test-output/smoke-*.png`.

| # | Check | Before reset (Spring 5) | After reset (Spring 1) | Result |
|---|---|---|---|---|
| 1 | Help Wanted board empty on Spring 1 | Board opened "Help Wanted: Amethyst for Robin, 300g" | Board opened "Nothing is posted today."; probe `QuestOfTheDay = null` | PASS |
| 2 | No Dish of the Day on Spring 1 | Probe `DishOfTheDay = Parsnip Soup x1` | Gus's full stock: Beer, Salad, Bread, Spaghetti, Pizza, Coffee, 4 recipes; no dish row; probe null. Spring 2 probe: `Glazed Yams x2` (first dish arrives day 2) | PASS |
| 3 | JP, upgrades, stash, pet, horse, buildings survive `UpdateFromGame1()` | `tly_meta`: JP=2288, 4 stash items, 35 upgrades | `tly_meta` line byte-identical; log: Rex + Mochi restored with two bowls, stable + horse restored, Coop/Barn/Silo placed; farm screenshot shows all of it | PASS |
| 4 | Spring 1 weather matches the new run's schedule | schedule tomorrow=Rain (Spring 6) | HUD sun icon; probe live/netWorldState/schedule all Sun, tomorrow Sun; log `Weather: scheduled Sun for Spring 2`; hub forecast 2..7 = Sun Rain Rain Sun Petals Sun; Spring 2 probe tomorrow=Rain, log `scheduled Rain for Spring 3` | PASS |
| 5 | Traveling Cart Y1 guarantee re-rolls per loop | `VisitsUntilY1Guarantee = 4` (armed at 5, decremented once) | `= 8` off the new uniqueID 447549305 (vanilla range 2..30); an unarmed save (-1) is left alone | PASS |

Nothing failed, so no code fix. The one code change is 0.16.18: `tly_netstate` now prints a
`[weather]` line (live Game1 flags, netWorldState Default weather, scheduler pick for today and
tomorrow) and accepts `army1 <n>` to arm the Y1 guarantee in memory, because the throwaway save
has it at -1 and the reset deliberately leaves -1 alone.

**Driving notes from this smoke (added to the gotchas):** quest board = stand at Town (42,56),
`debug fd farmer 0`, `game.ps1 -RightClick 960,470` (from (42,57) the click misses). Escape does
NOT close the board menu; click its X at (1642,168). The planning hub opens on top of everything
after a reset; pick a theme (Mixed at (1210,530)) before anything else. Gus is not at the bar on
Spring 1 at 1pm: `debug wct Gus Saloon 14 17`, stand at (14,20) facing up, `-RightClick 928,709`
opens his stock; the dish of the day is the FIRST row when present (ItemQueryResolver
DISH_OF_THE_DAY, no counter sprite). His "Can you smell that? It's the Coffee" greeting names a
random stock item, not the dish. Shop scroll arrows: down (1640,835), up (1640,235).

## Deja-vu villager dialogue (0.16.13 to 0.16.17): built, unit-tested, LIVE SMOKE PASSED

Spec `docs/superpowers/specs/2026-08-27-deja-vu-dialogue-design.md`, lines (all approved by Jeff)
`...-deja-vu-dialogue-lines.md`, plan `docs/superpowers/plans/2026-08-27-deja-vu-dialogue.md`.
Nightly rollup (talk +1, gift +3, heart event +10) into `MetaState.VillagerFamiliarity`; threshold
60, 6% per talk, tier 2 at 180, one line per villager per loop, one per 7 days town-wide, never in
loop 1; postfix on `NPC.checkForNewCurrentDialogue` prepends the line; GMCM "Deja-vu dialogue"
toggle; `tly_dejavu status|set|force|reset`.

**Live smoke PASSED 2026-08-27 17:26 to 17:46 (save None_447540453, loop 54, driven with game.ps1):**

| Step | Result |
|---|---|
| Spring 1, `tly_dejavu set Pierre 200` + `force Pierre`, talk to Pierre | Introduction line played ("Hey, it's Mr. Clone..."), NO deja-vu line, force still armed (guard works) |
| `debug sleep` | `Familiarity rollup: +1 across 1 villagers` (Pierre 200 -> 201); save carries `VillagerFamiliarity` |
| Spring 2, talk to Pierre (warped beside the farmer) | "You're my best customer. Have been for... hm. How long, exactly?" in Pierre's portrait box; log `Deja-vu: Pierre tier 2 on day 2 (forced)`; status `shownThisLoop=[Pierre] lastDay=2`, Pierre eligible=False |
| `set George 200`, `reset`, `force George`, Spring 2 talk at his chair | Introduction line; log "George is playing the 'Introduction' event line; not touching it" |
| Spring 3, talk to George | "...You're all right. Don't let it go to your head."; log `Deja-vu: George tier 2 on day 3 (forced)` |
| Talk to George again | His own daily line ("Alex is my grandson...") plays, so the ordinary line survives underneath ours |

Findings: (1) vanilla clears the stack when it plays an Introduction, so nothing else can play that
day (vanilla, not ours); (2) a villager WARPED off his schedule drops location lines flagged
`removeOnNextMove` (NPC.cs 4263), so smoke on someone at his natural spot (George's chair). Not
exercised live: the real 6% roll and the weekly cap (unit-tested).

**Driving notes that cost time (now in TODO gotchas):** talk = `game.ps1 -RightClick x,y` on the
villager (new switch, left click uses the tool); the farmer must FACE the villager
(`debug fd farmer 0`); `debug wct <npc> <loc> <x> <y>` needs a location, `debug warpcharactertome`
puts the NPC on the farmer's tile; keyboard walks did not move the farmer; indoor maps do not centre
the camera, tile (tx,ty) in JoshHouse is at screen (1215+(tx-16)*64, 870+(ty-22)*64).

## Keep power books (0.16.9 to 0.16.12): built, unit-tested, LIVE SMOKE PASSED

**Live smoke PASSED 2026-08-27 16:47 (0.16.12 deployed, save None_447536393, SMAPI console only):**

| Step | Result |
|---|---|
| `tly_readbook Book_Speed` then `tly_readbook` | `Book_Speed=1`, all other 18 books 0 |
| `tly_addjp 1000` + `tly_buyupgrade keep_book_speed` | "Purchased 'keep_book_speed' (Keep Way Of The Wind pt. 1) for 750 JP" (name from ItemRegistry) |
| `tly_readbook Book_Defense` (read, NOT bought) then `tly_reset` | `FarmerReset: ... books=[Book_Speed] ... dialogueEvents=[Introduction:6]` |
| `tly_readbook` after the reset | `Book_Speed=1`, `Book_Defense=0` (unbought book wiped), rest 0 |

Not eyeballed: the shrine row itself (no desktop driving needed; the purchase log shows the resolved name).

Jeff's brainstorm ruling (2026-08-27): per book, bought at the shrine. Nineteen `keep_book_*`
Carryover rows, reach-gated on having read the book this loop, 150 / 350 / 500 to 750 JP.
`StatResetRules` unchanged (wipe-by-default); `FarmerReset` re-grants bought flags from
`RunBaseline.KeptBookStats`. Spec + plan in `docs/superpowers/{specs,plans}/2026-08-27-keep-power-books*`.

**Docs:** CHANGELOG `## Unreleased` covers 0.16.8 (first-meeting dialogue) and the books; README and
Nexus Shrine feature line updated identically. "What's New" still says 0.16.7 until the release
number is chosen.

**Next:** Part 2 of the same brief, Deja-vu villager dialogue (TODO.md `[1.0.0]` entry, credit
u/Gribbleby), brainstorm first.

## Previous state (2026-08-27 afternoon)

**Last updated:** 2026-08-29 late night (review fixes 0.16.166, sim diagnostics 0.16.167; two full-year sims on the boost build)
**Branch:** `master`; 0.16.167 PUSHED and RELEASED; nothing local-only
**Tests:** 1822 passing, 0 failing
**Build:** clean; 0.16.167 deployed to the game; game CLOSED at end of session
**Last public release:** 0.16.167 (2026-08-29 night; GitHub release + Nexus file, version, description and changelog all live)

## THE PERITEM GATE BASELINE HAS SHIFTED. The next release notes must say so.

Bundles that require every item they show used to take their per-item due dates from a
40-entry hand table (`GameplayConfig.DefaultItemSeasonPins`). Anything outside it had no due date
and applied no pressure until the Winter win check. Because the engine re-rolls the six fish
bundles from a 52-item pool and the two metals bundles from an 11-item pool, most re-rolled boards
were partly or wholly ungated.

Phase 1 replaces that with a model the engine derives from the game's own data: per item an
earliest-possible season and an effort score; per bundle, deadlines spread across the four
checkpoints by effort and clamped upward to each item's floor so an impossible gate cannot be
expressed.

**This makes the game harder at every difficulty, deliberately** (Jeff's ruling, 2026-08-27).
`DifficultyResolverTests.Normal_Resolves_To_Todays_Config_Values` still passes because it asserts
difficulty dial values, not gate outcomes, but "Normal equals the 0.12 shipping balance" is no
longer true of season gates.

Measured on three live boards, two configurations including a full Hard sweep: no impossible
gates, 0 never-gated bundles, 66 ids derived, 0 curated pins rejected. Numbers and per-bundle
detail are in the plan's Results section.

- Spec: `docs/superpowers/specs/2026-08-27-derived-item-availability-design.md`
- Plan + results: `docs/superpowers/plans/2026-08-27-derived-item-availability-phase-1.md`

**Phases 2 to 4 are not built.** Orchard, Tapper's, Forest, Spirit's Eve, Home Cook's and Wild
Medicine still wait for Winter, because their ingredients come from domains Phase 1 does not model
(crops, forage, monster drops, artisan goods, cooking, artifacts, books, saplings, geode minerals,
tapper goods). Each later phase needs its own plan.

**Live smoke PASSED 2026-08-27 (0.16.1-0.16.7, Rodger throwaway save, driven from the SMAPI console):**
`tly_addpet Cat Mochi 1` + `tly_addpet Dog Rex 0` + `tly_fixbridge` + `tly_stashrod`, then `tly_reset`.
After the reset: both pets restored, Mochi owns the default bowl (53,7), a second bowl placed at (51,7)
and assigned to Rex (screenshot: two bowls on the fence pads); Beach tile (58,13) back to 284 with the
Action property present and bridgeFixed=false; the stashed Iridium Rod still has 20 bait, a spinner
and Auto-Hook. Loading the rotated save also placed the missing bowl on DayStarted (the 0.16.4
self-heal). One fix came out of it: vanilla isBuildable reads tile properties off
Game1.currentLocation (the farmhouse during a reset), so 0.16.7 checks the Farm map directly.

**Live smoke PASSED 2026-08-27 (0.16.8, villager first-contact dialogue):** after `tly_reset` the
FarmerReset summary logs `dialogueEvents=[Introduction:6]`, and Pierre greets the player with his
Introduction line ("Hey, it's Mr. Clone, the new farmer! I'm Pierre...") on reset #52. Driving note:
left-click on an NPC only talks when the selected hotbar slot is EMPTY; with furniture selected the
click tries to place it. `tools/game.ps1 -Key x` is now supported. 0.16.8 is local only, not released.

## NEXT SESSION: difficulty modifiers need an in-game smoke, then a merge decision

Jeff brainstormed this the night of 2026-08-26 and said "write the spec, plan, and build" before
going to bed. All 16 planned tasks are done and committed on `feat/difficulty-modifiers`. Nothing
is pushed and nothing is merged to `master`: both are Jeff's call.

- Spec: `docs/superpowers/specs/2026-08-26-difficulty-modifiers-design.md`
- Plan: `docs/superpowers/plans/2026-08-26-difficulty-modifiers.md`

**What it is:** ten independent Easy/Normal/Hard/Extreme dials in a new GMCM "Difficulty" section.
No overall tier (Jeff killed that mid-brainstorm). Everything defaults to Normal, which resolves to
today's exact config values, so an untouched save is unchanged. A change applies at the NEXT reset,
because the resolved profile is stamped onto the save and every consumer reads the stamp.

**Nobody has seen any of it run.** The whole thing is unit-tested and builds, but it has never been
loaded in the game. What needs smoking, in rough priority order:

1. `tly_difficulty` on a loaded save prints sensible output and says whether the stamp or live
   config is in force.
2. GMCM shows the Difficulty section with ten dropdowns, and a change survives a save/reload.
3. Set stack size + required slots to Hard, `tly_reset`, and check the board actually changed:
   `tly_genbundles` should show bigger stacks and higher pick-X counts.
4. **The Vanilla post-pass is the riskiest change here.** `BundleSource=Vanilla` previously wrote
   NOTHING at reset; it now rewrites the board when any ask-side dial is off Normal. On a Vanilla
   save, reset at Hard and confirm the CC menu still opens, ingredient ITEMS are unchanged, and
   stacks/pick-X moved.
5. Set everything back to Normal, reset, and confirm a board identical to a pre-branch one.

**Known gap, minor:** a brand-new VANILLA-source save has no stamp until its first reset, so a GMCM
change during loop 1 of such a save applies immediately rather than next loop. Self-corrects at the
first reset. Engine saves stamp during fresh-run generation, so they do not have this.

**Resolved 2026-08-27 on deploy (was flagged overnight as an open question).** The ten steps
serialize into config.json as readable NAMES, not integers: the deployed
`Mods/TheLongestYear/config.json` shows `"StackSize": "Normal"` and so on for all ten. The
overnight worry, based on `StringEnumConverter` not appearing in StardewModdingAPI.dll, was wrong.
No fix needed and no ruling required.

**Deliberate deviation from the spec, recorded:** the spec describes the rarity bias as applying
inside the sampler. It is applied to `ItemPools` before generation instead, and the stack/quality
modifiers are applied by scaling the tuning block. Same effect, and it meant `BundleSlotFiller` and
`AuthoredBundleComposer` needed no edits at all.

**Also parked this session:** Impossible mode (post-1.0), written up in `TODO.md`.

**Two things NOT done, both waiting on Jeff:**
- No manifest version bump (branch rule: only the release line bumps).
- No "What's New" entry in the README or Nexus description, because the release number is not
  decided. The Difficulty section itself is written into both, content-identical.

## Previous state

**Last updated:** 2026-08-29 late night (review fixes 0.16.166, sim diagnostics 0.16.167; two full-year sims on the boost build)
**Branch:** `master`; 0.16.167 PUSHED and RELEASED; nothing local-only
**Tests:** 1822 passing, 0 failing
**Build:** clean; 0.16.167 deployed to the game; game CLOSED at end of session
**Last public release:** 0.16.167 (2026-08-29 night; GitHub release + Nexus file, version, description and changelog all live)
+ description + changelog synced, FAQ live)

Today, driven by finding that **emmalution (82.7K subs) has been streaming the mod since 16 July**:

- **0.14.0** — the Junimo Shrine never opened on a Fail night (Nexus 1123181, a 0.12.17 regression
  that killed meta-progression); weekly goals could tick without a donation; no way to get another
  pet after declining Keep Pet.
- **0.14.1** — festival main events run once per day (the Egg Hunt and the Luau soup could be
  repeated by leaving and re-entering); weekly goals capped to what a bundle can still accept.
- **0.14.2** — Shop Discount discounts the price rather than the payment (tool upgrades exempt);
  **fixed a bug shipped in 0.14.1** where the once-per-day festival stamp survived a rewind and
  blocked festivals in every later loop; new GMCM "Features" section; mod-page FAQ.

Playtest tooling was rebuilt: `tools/game.ps1` + `tools/screenshot.ps1` (the old pair lived in
gitignored `test-output/`). An unfocused game is a PAUSED game, and SetForegroundWindow fails
silently, which is why keyboard input never reached the farmer. Both handled; screenshots are
cropped to the client area so image pixels are click coordinates.

## NEXT SESSION: run the netWorldState audit

Jeff wants a fresh agent on this tonight. The brief is self-contained in
`docs/superpowers/HANDOFF-2026-08-26-networldstate-audit.md` - enumerate every NetWorldState
field, rule each keep or wipe against the reset philosophy, implement the wipes, smoke it.
Difficulty setting is also queued but Jeff is brainstorming it tomorrow; do not design it alone.

## Current state (2026-08-25 afternoon): 0.13.0 released, fully closed

Shipped on top of the merge below, all live-smoked on the Rodger throwaway save (TODO.md tables):
the year-2 crop gate (Garlic/Artichoke need Pierre's Special Order, Red Cabbage that or Cultivation),
the merchant's Junimo line removed, and **season pity as an opt-in offer** (second Fail-night question
after keep/reshuffle; `PityCosts` curve like the hold; `tly_pity accept|decline`; the offer is deferred
one tick because a nested question inside the hold callback gets torn down by answerDialogue).
Bug 1122901 (Keep pet) left OPEN on purpose: the reply asks a multi-pet tester to confirm on 0.13.0.
Bug 1122358 stays Fixed; reply asks the reporters to run a loop on 0.13.0 and report any leftover
impossible ask. Chrome-extension gotcha: after a long session the automation bridge went stale even
though chat worked; `/mcp` reconnect was not enough, killing and relaunching Chrome fixed it.

**Open (new, 2026-08-25 12:41 post by rose1729):** did NOT keep the pet at the end of loop 1 and was
never offered a pet again in loops 2/3. Likely the reset leaves a vanilla pet-adoption flag set
(check `MarniePetAdoption` handling in the reset path); needs a code check + reply. Not yet answered.

**Next:** rose1729's pet-offer question; watch the 0.13.0 replies; 1113831 Day-3 crash still silent.

## Previous state (2026-08-25): v0.13.0 on master, not released, three fixes not yet live-smoked

Merged `worktree-fixes-0-13-0` (plan `docs/superpowers/plans/2026-08-25-0-13-0-fixes.md`, 11 commits,
subagent-driven with per-task reviews + final review) on top of the season pity merge:
- **Quality-ask vetting v2** (Nexus 1122358 follow-ups): `ItemPools.QualityEligibleIds` derived from
  Data/Crops (skipping `HarvestMaxQuality == 0`, i.e. Fiber), rod-caught non-jelly fish, and spawned
  forage passing the game's isForage category test; `BundleSlotFiller.RollQuality` refuses quality on
  anything else; `tly_genbundles` prints "quality asks:" per bundle. Curated additions (Tea Leaves,
  Red/Purple Mushroom) never carry quality (accepted).
- **Keep Pet keeps every pet** (Nexus 1122901): `MetaState.PetStates` list, legacy `PetState`
  migrates at the next reset, restore tiles stagger west from (54,8).
- **Traveling Cart cap per day** (lexihope): `CartDayStock` remembers the day's ids on
  `RunState.CartStockDay/Ids`; `CartSlotLimitPatch` filters later builds; recipes keyed `#Recipe`.
Reply drafts for all three: `release-notes/2026-08-25-replies-draft.md` (post only on "yes, push").

**Next:** live smoke of the three fixes on the Rodger save (`tly_genbundles` quality-asks lines: no
771 / jellies / 815; buy from the cart then reopen it, the slot stays empty; reset with two pets,
both come back), then README + Nexus "What's New in 0.13.0" (identical content), CHANGELOG
`## Unreleased` -> `## 0.13.0`, release on "yes, push", post the three replies, flip 1122901 to Fixed.

## Previous state (2026-08-25): v0.12.19 on master, season pity merged, not released

Merged `worktree-season-pity` (spec `docs/superpowers/specs/2026-08-25-season-pity-design.md`, plan
`docs/superpowers/plans/2026-08-25-season-pity.md`, 15 commits, subagent-driven with per-task reviews
and a final whole-branch review). Per-season fail counter (`MetaState.SeasonFailCounts`); first 5 fails
at a season are standard; from the 6th, KEEP lowers that season's quota 10%/step (floor 50%) via a
`BoardEaseSeason/Steps` stamp read back on load, RESHUFFLE trims the 2 hardest eligible items/step via
`BoardTrimSeason/Steps` (both stamps keep reloads byte-identical to the reset). Passing a season drops
its count to 5; Winter never gets the keep-path ease. `tly_pity status|set`, GMCM "Season pity" section,
eased Fail-night prompt (+ Winter variant), "eased Nx" title. Rules in `Core/SeasonPity.cs`,
`SeasonEase.cs`, `ItemHardness.cs`, `PityTrim.cs`; trim inside `BundleSlotFiller.Fill`.

**Live smoke PASSED 2026-08-25** (table in TODO.md): eased prompt, keep stamps the ease and the reset
applies it, reload clean, reshuffle trims (Blacksmith's 11 -> 7) and clears the ease, reload clean,
`tly_genbundles` determinism OK. Not eyeballed: the "eased Nx" title (book not placed). Not exercised
live: the real day-28 RecordFail/RecordPass path (unit-tested).

**Next:** README + Nexus "What's New in 0.12.19" (identical content, TLY Custom only), CHANGELOG
`## Unreleased` -> version, then the release on "yes, push" (`release.ps1`, then the Nexus page via
Claude-in-Chrome).

## Previous state (2026-08-25): 0.12.18 released, fully closed

0.12.17 (hold feature) and 0.12.18 (Void Salmon out: WitchSwamp joins the built-in excluded
location markers and `(O)795` the built-in excluded ids, since the Witch's Swamp is behind the
post-CC Dark Talisman quest; Jeff's "hard but fair" ruling from 0.12.16 reversed) went out
back-to-back. Nexus description = README (What's New in 0.12.18 incl. the Void Salmon apology),
changelog entry added, version 0.12.18. Bug 1122358 got a follow-up reply with the apology
(status stays Fixed). Release mechanics note: `release.ps1` step 3 (Playwright description
sync) is retired; run it with `-SkipNexusDesc` and do the Nexus page via Claude-in-Chrome.

**Next:** the 0.13.x DerivePins brainstorm (TODO.md); open Nexus bug 1113831 (Day-3 crash, silent).

## Previous state (2026-08-24 evening): v0.12.17 on master, keep-bundles hold done, not released

Merged `feat/keep-bundles-hold` (spec `docs/superpowers/specs/2026-08-24-keep-bundles-hold-design.md`,
plan `docs/superpowers/plans/2026-08-24-keep-bundles-hold.md`). Fail night now asks, before the shrine,
whether to keep the same bundle board next loop (first hold free, then 50/100/200/300 JP via
`GameplayConfig.BundleHoldCosts`, counter resets on reshuffle). State: `MetaState.BundleSeedLoop`,
`ConsecutiveHolds`, `HoldChoiceMadeForReset`; rules in `Core/BundleHold.cs` + `BundleHoldPricing.cs`;
both seed call sites use `EffectiveBundleSeedLoop`. Day-1 CC speech gained `event.intro.junimo-9b`;
Season Goals title shows "held Nx"; `tly_hold keep|reshuffle|status` debug command; every em dash removed
from player-facing strings (house rule: never use em dashes in anything for Jeff). Live-smoked on the
Rodger throwaway save (TODO.md table): free/paid hold, reload from title, reshuffle, full Fail-night chain,
too-little-JP re-ask (fixed to defer one tick). Not eyeballed: the held title and the intro line.

**Next:** release 0.12.17 as a normal patch release (or a minor if Jeff declares it): write README +
Nexus "What's New" (identical content), move the CHANGELOG `## Unreleased` entry under the version,
`release.ps1 -SkipNexusDesc` + Claude-in-Chrome description/version/changelog, all only on "yes, push".
Then the 0.13.x DerivePins brainstorm parked in TODO.md (escalating per-season likelihood, pity counter).
Open Nexus bug: 1113831 Day-3 crash (Needs more info, silent). 1117543 muting closed Not a bug today.

## Previous state (2026-08-21 night) — 0.12.11 release candidate

Everything in `HANDOFF-2026-08-21-pre-0.12-release-work.md` is done and smoked on the deployed build:
A1 screenshot, A2 empty-theme card (no fix needed), A3 `EnableNonObjectDonations` next-board rule
(v0.12.4), B5 `tly_jpbudget` + 5-loop measurement (v0.12.5–6), B6 cult repricing per ruling (v0.12.7:
starfruit gone, red cabbage 5k, Pierre's Special Order 10k — smoked at Pierre's), A4 twelve curated ramps
+ trophy trim (v0.12.8), C7 `BundleSource` Engine|Vanilla with the TLY Custom / Normal / Remixed dropdown
(v0.12.9–11 — smoked: Engine → Vanilla/Default → Vanilla/Remixed → Engine resets all classify correctly,
dropdown eyeballed). Release docs written (README ≡ Nexus What's New, CHANGELOG 0.12.11, Nexus changelog
file). **Next: user says "yes, push" → `release.ps1 -SkipNexusDesc`, description/version sync + changelog
paste via Claude-in-Chrome, upload `release-notes/advanced-options-tly-custom.png` to the gallery and
replace the `[img]` placeholder, verify live.**

## Previous state (2026-08-21 midday) — post-sweep bugfix pass, ready for smoke + beta decision

The 07-17→08-21 sweep surfaced nine 0.11.60 bug threads (see `TODO.md` "6th sweep" table for the
full root-cause/fix matrix). All are fixed on master as one-commit-each v0.11.101–110 — CC ceremony
id swap, museum `specialItems` wipe, `mail`-granted event replay, Mixed Seeds retarget, weather
rewrite (totems/CJB survive; vanilla-like density), stash banked pre-wipe, kept-building
`InitializeIndoor`, kept-tool state transplant, fail-night FarmEvent suppression + scene watchdog,
plus the Cart Stall cap toggle/flavour/docs. The remix-bundles thread (the loudest one) is already
moot on master because the engine writes the board.

**Released as 0.12.0-beta.1 on 2026-08-21** (user call: ship master, no backport). **v0.12.1 smoke PASSED 2026-08-21**
(TLY Custom dropdown + every bugfix from the sweep re-verified on a real loop reset — TODO has the table). **Next:** watch the beta
feedback; answer the two PRIVATE bug reports (see TODO); decide the Standard-vs-engine bundle opt-out; the
Normal-bar PoolTuning playtest loop + cult repricing remain the gate for a non-beta 0.12.0.

## Previous state (2026-07-20) — beta-release decision point

All three 0.12.0 engine plans are shipped (v0.11.61→v0.11.100): authored bundles (11 defs
incl. Gil's Trophies with Warrior Ring), weapon/hat donations (`EnableNonObjectDonations`),
Vault engine-owned +25%, SVE compat pass. Final review passed after 2 trivial fixes
(v0.11.99/100). `TODO.md` is the live source of truth — see its "0.12.0 ENGINE PLAN 3 of 3"
entry for full detail.

**Assessed 2026-07-20: ready for a public BETA with two gates:**
1. **One human check outstanding** — a live CC click-through of a weapon/hat donation into a
   trophy bundle (`tly_trophytest` proved match/accept programmatically; no human has run the
   real menu flow). Riskiest untested surface; 10 min on the already-deployed PC build.
2. **Version framing** — 0.12.0 is reserved for after the Normal-bar `PoolTuning` playtest
   loop + cult repricing decision. Ship the beta as **0.12.0-beta.1** (or 0.11.100 marked
   beta/optional on Nexus), NOT as 0.12.0. Beta feedback feeds the tuning pass.

Release-note caveat to include: flipping `EnableNonObjectDonations` mid-loop can strand an
in-flight trophy bundle until the next reset (known, documented).

Release mechanics: `gh release create` → publish-nexus workflow (TLY flow verified live by
0.11.60; `file_id` 7502657); description sync via `release.ps1`; Nexus changelog = manual
browser paste. **No push/release without explicit "yes, push."**

---

## Historical — v1 snapshot (2026-05-27, after Plan 07)

**Status then:** v1 ready for first meaningful playtest (328 tests).

## What v1 means

Per the original design spec §14, v1 = "MVP — prove it's fun & stable on PC." Everything below
either ships in v1 or is explicitly deferred.

## Done

| Plan | Branch / commits | Shipped |
|---|---|---|
| **Plan 01 — Foundation** | merged | Core types: `MetaState`, `RunState`, `MetaStore`, `GameplayConfig`, `Calendar`, `Theme`/`Season`/`Rarity` enums, `JpSettings`. |
| **Plan 02 — Contracts** | merged | `RunManager`, `GateEvaluator`, `SelectionService`, `BundleCatalogBuilder`, `BundleGate`, theme/season classification, solvable-partition contract generator. |
| **Plan 03 — Lifecycle / reset** | merged | `WorldResetService` (in-place reset via `Game1.loadForNewGame`), `SaveBackup`, `WorldStateProbe` (leak test), `CommunityCenterUnlock`, `CcLocationAccessiblePatch`. |
| **Plan 04 — Donations + JP** | merged | `DonationService`, `DonationObserver` (Harmony-patched), `BundleCatalogBuilder` (catalog from `Data/Bundles`), `JpCalculator`, `UpgradePurchase` rule, `VaultRules`. |
| **Plan 05 — UI** | `feat/v1-plan-05-ui` | `WeeklyHubMenu` (planning hub), `JunimoShrineMenu` (upgrade shop), `MenuLauncher`, `SeasonGoalsBoard` (CC interactable), `UpgradeCatalog` + `UpgradePurchaseService`. |
| **Festival fixes** | `feat/v1-plan-05-ui` | Time flows during festivals, exit at real in-game time, auto-eject at festival end, HUD redraw during festivals, "Are you sure" suppression, day-8 hub unblock, day-3 forced rain removed, RNG re-seed on reset, Joja root-cause fix. |
| **Plan 06A — Persistence effects + per-stat keep upgrades** | `feat/v1-plan-06a-persistence-effects` | Wires `OwnedUpgrades` into reset effects (backpack, gold, kept coops/barns, kitchen, vault bus, horse, starting animals). Adds 80 chained keep entries (16 tool tiers + 2 rods + 50 skill levels + 12 mine elevator floors). Cap-not-grant via `PlayerSnapshot` (in-run peak captured pre-wipe) + `RunState.PeakMineFloor`. Profession picker re-fires for kept L5/L10 skills. Shrine UI hides locked entries. Generalised `MeetsMetaRequirement` (upgrade/quest/mail/season). |
| **Plan 06B — Cookbook + Craftbook** | `feat/v1-plan-06b-cookbook-craftbook` | 6 Carryover catalog entries (Cookbook/Craftbook I/II/III @ 150/350/700 JP, 5/10/20 slots). `CookbookMenu` + `CraftbookMenu` slot-grid IClickableMenus with sub-mode recipe picker (currently-known only) and confirm-remove dialog. `FarmHouse.checkAction` Harmony patches open menus on configurable tile coords (`tly_setcookbook`/`tly_setcraftbook`). `IndicatorRegistry` for reusable ?/! bubbles over world tiles. Quest intros via vanilla `Quest` on first reset after purchase. Recipe re-grant on `FarmerReset.Apply`. `MetaState` extended with `CookbookRecipes`/`CraftbookRecipes` (List<string>) + `DismissedIndicators` (HashSet<string>). |
| **Plan 06 — Theme effects layer** | `feat/v1-plan-06-theme-effects` | `ThemeModifiers` ids corrected to match signed-off spec (mines_closed / fish_bite_down / forage_off). `ActiveEffectsProvider` + `BonusDropResolver` Core types wired through `RunController` (Set/Clear on theme select + reset). 6 Harmony patch files implementing all 10 bonus/liability effects: forage_yield_up / forage_off / crop_growth_up / crop_growth_down / fish_bite_up / fish_bite_down / mine_drops_up / mines_closed / all_drops_up / all_sell_prices_down. `MixedSeedsPatch` injects Red Cabbage / Starfruit per cultivation upgrades (bool overload pinned). `fortune_rare_fish` gives +25% bite rate. `WeatherForecast` + `CartStockPreview` Core types deliver real foresight data to `WeeklyHubMenu` per owned Weather Sage / Cart Whisperer tiers. `tly_activeeffects` debug command. |
| **Plan 07 — Junimo Stash** | `feat/v1-plan-07-junimo-stash` | Pure Core: `StashItemRecord` POCO + `MetaState.StashItems` + `MetaState.StashSlotCount` (0/4/8 from `stash_1`/`stash_2`) + `GameplayConfig.StashTileX/Y`. Mod-side: `JunimoStashService` manages the tagged Chest lifecycle (place + populate + bank + register indicator + find), `JunimoStashCapPatch` enforces the slot cap via `Chest.addItem` postfix (HUD message on rejection), `JunimoStashShowMenuPatch` dismisses the `tly.stash` indicator on first open. Wired into `WorldResetService.PerformReset` step 13b, `MetaStore.Save` (anti-save-scum invariant preserved), and `ModEntry.OnSaveLoaded` (mid-run save-load safety). Quest intro `tly.-9003` fires on first run after stash_1 + tile configured. Debug commands: `tly_setstash`, `tly_openstash`, `tly_stashclear`. `tly_meta` extended with stash summary. |

## v1 implementation complete

All §14 v1-scope items shipped. Ready for first meaningful playtest.

**Pre-playtest setup checklist (debug-only, no in-game onboarding for v1):**

1. Build + deploy the mod (build is clean as of branch `feat/v1-plan-07-junimo-stash`).
2. Load a save.
3. Anchor the interactable world tiles via debug commands — each requires standing on/facing the target tile:
   - `tly_setboard` (Season Goals board, inside CC)
   - `tly_setcookbook` (kitchen counter)
   - `tly_setcraftbook` (farmhouse table)
   - `tly_setstash` (any farm tile)
4. Purchase upgrades via `tly_addjp 5000` + `tly_buyupgrade <id>` for the features to verify.
5. `tly_reset` to land on Spring 1 with the configured surfaces active.

## Deferred beyond v1

- **Cookbook/Craftbook Phase C (LY3)** — friendship per-NPC + wallet-flag per-item retention.
- **Cutscenes / full narrative** — placeholder text only in v1.
- **Endless victory-lap mode** — single-win run for v1.
- **Android port** — PC first.
- **Deep balancing pass** — calibrate numbers after v1 has been played.
- **Advanced contract modifiers** — per-run "blessings" etc.
- **SVE compatibility pass** — most pieces are SVE-safe already (see future-expansions notes).
- **LY2 / LY3** — Year 2/3 ultimate-perfection content, separate JP economies, possibly separate mods.

## Known playtest carryovers

From 06B:
- **Indicator `?` source rect** `(397, 489, 10, 10)` in `IndicatorRegistry` is approximate; visually verify the right sprite renders. One-line constant fix if wrong.
- **Indicator tile coords** start at `(0, 0)` (= disabled). After buying `cookbook_1` / `craftbook_1`, the player needs to run `tly_setcookbook` / `tly_setcraftbook` once each to anchor the interactable + bubble.

From 06:
- **`forage_off` over-suppression (JC-4)** — Mining liability also blocks weeds/stones via `spawnObjects`. Flag for playtest to assess if too punishing.
- **`fortune_rare_fish` is a 0.75× bite-rate multiplier (JC-2)** — v1 approximation for rare-fish boost (true rarity intercept requires deeper Stardew internals investigation).

## Small follow-ups (not blocking v1, can land any time)

- **Festival exit to host map.** Currently `Event.endBehaviors` warps to the farm entry; should land on the festival's host map (Town for Egg/Fair/Spirit's Eve; Beach for Luau/Jellies; Forest for Flower Dance). ~20 lines (`endBehaviors` postfix or transpiler).
- **Seed-driven weather scheduler** with per-season minimums. Spec'd in `TODO.md`.
- **Wipe-meta debug command** (`tly_wipemeta`). Trivial — replace `_meta.State` with `new MetaState()` + `_meta.Save()`.
- **Weekly Theme Journal entry.** Player-facing reminder + bonus-item completion tracking → liability suppression on completion. Spec'd in `TODO.md`.

## Workflow rules in effect

- Local commits only. Never push without explicit "yes, push".
- Co-Authored-By footer on every commit.
- Build/test/deploy: I do, user plays, I pull logs.
- Reserve playtests for MEANINGFUL feedback opportunities. Don't request a playtest just to confirm wiring fires — verify that solo.
- Run with `-p:EnableModDeploy=false` while Stardew is open (file-lock on the deployed DLL).
