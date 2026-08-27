# Design: a derived item availability model, and deadlines built from it

Date: 2026-08-27
Branch: `feat/difficulty-modifiers`
Status: approved in brainstorm, awaiting spec review

## 1. What is broken

A bundle applies pressure only if it is due at a season checkpoint. There are three ways a
bundle gets a due date, and one of them leaks.

| Kind | How it gets a due date | Leaks? |
|---|---|---|
| Seasonal (`Fall Crops`) | its own named season | no |
| Percentage, requires fewer than it shows | a cumulative ramp, curated or derived | no |
| PerItem, requires everything it shows | each ingredient's entry in `GameplayConfig.DefaultItemSeasonPins` | **yes** |

`DefaultItemSeasonPins` holds 40 hand written entries covering twelve bundles. An ingredient
absent from it has no due date, so `DemandAtSeason` returns 0 for that slot at every season. A
PerItem bundle whose ingredients are all absent applies no pressure at all until the Winter 28
win check, so a player can ignore it for three seasons.

### The leak is far wider than the two bundles first reported

The pin table was written when those twelve bundles kept vanilla's fixed item lists. The engine
now **re-rolls** eight of them from pools:

- Six fish bundles (River Fish, Lake Fish, Ocean Fish, Night Fishing, Specialty Fish, Quality
  Fish) draw from a 52 item Fish pool. Fifteen of those 52 are pinned.
- Two metals bundles (Blacksmith's, Engineer's) draw from an 11 item Metals pool. Three of those
  11 are pinned.

So a re-rolled fish bundle is gated only on whichever of its four slots happen to land on one of
the fifteen. Assuming roughly even sampling, about a quarter of fish boards come out entirely
ungated and most of the rest are gated on one or two slots out of four. For metals it is worse:
roughly a third of boards are entirely ungated.

On top of that, seven PerItem bundles have no pinned ingredient at all: Orchard (the mod's own),
Helper's, Chef's, Forest, Home Cook's, Spirit's Eve and Sticky.

This is not a handful of stragglers. The whole PerItem category is leaking, and the leak widens
every time the engine re-rolls or a bundle mod adds an item nobody has rated.

### The hand pins are pacing, not availability

Reading the 40 entries closely: Sunfish is pinned Spring, Shad Summer, Tiger Trout Fall. All
three are catchable in every season. Carp is pinned Summer and is available year round. Those
dates are not facts about when the item can exist. They are pacing choices, spreading a bundle's
four slots across four checkpoints, easiest first. Only a few entries (Sandfish behind the
desert, Hardwood behind an axe upgrade) encode genuine availability.

Any replacement therefore needs **two** derived values per item, not one. Conflating them is
what produced the Purple Mushroom incident, where a pin meant as pacing was read as availability
and made a bundle unsatisfiable at its own gate.

## 2. Decisions taken in the brainstorm

Jeff's rulings, 2026-08-27:

1. **Fix it everywhere, accept that Normal shifts.** Ungated bundles start applying pressure at
   every difficulty. This is a balance change and it is intended.
2. **The engine derives the data itself.** Not a blunt whole-bundle fallback ramp, and not a
   longer hand rated table. Quoting: "You need to do the analysis yourself on every item and
   build all the details you need to make the engine actually work correctly. You can't call the
   engine built if it's missing so much data it needs to actually work right."
3. **Pacing is an even spread across the four checkpoints, effort weighted.** A four item bundle
   asks for one more item at each checkpoint. A high effort item slides one checkpoint later, a
   trivial one slides earlier.

Engineering calls made by the implementer and open to objection:

4. The model is computed **at load from live game data**, not baked into a static table, so
   re-rolls, remix draws, SVE and bundle mods are covered by construction. This matches how
   `ItemPoolBuilder` already works.
5. The existing 40 curated pins survive as **overrides**, not as a parallel system, and each one
   gets checked against its derived floor at build time with disagreements logged.

## 3. The model

A new pure Core component, `ItemAvailability`, produces one record per item id:

```
sealed record ItemAvailability(
    Season EarliestSeason,   // hard floor: before this the item cannot exist
    int Effort,              // derived judgement: how much work it is
    string Basis);           // human readable derivation, for diagnostics
```

`Basis` is not decoration. Every incident in this area so far was diagnosed by asking "why does
the engine think that", and the answer had to be reconstructed by hand each time.

### 3.1 EarliestSeason, the floor

A hard fact derived from data plus a small set of rules about world gating. Per domain:

| Domain | Floor derived from |
|---|---|
| Rod fish | `Data/Locations` spawn seasons, intersected with the gating season of the location itself (desert needs the bus, mine levels need depth, island is already excluded by `ExcludedLocationMarkers`) |
| Crab pot fish | same, plus the crab pot crafting recipe |
| Forage | `Data/Locations` forage spawn seasons |
| Crops | `Data/Crops` seasons and growth days measured against when the seed is purchasable and affordable, so a crop counts only if planting plus growth completes before day 28 of that season |
| Fruit from saplings | `Data/FruitTrees` plus the 28 day maturation, so the earliest fruit season is one season after the earliest affordable sapling |
| Ore and bars | mine depth tier per ore, plus the furnace recipe for bars. Depth tiers are code facts, not data facts, so they live in a small rule table |
| Monster drops | `Data/Monsters` crossed with the depth at which the monster spawns |
| Geode minerals | geode source depth plus Clint's processing, so most minerals float to the geode's own floor |
| Artifacts | artifact spot seasons and conditions from `Data/Locations`, plus geode and monster sources |
| Artisan goods | the machine's crafting recipe availability, plus the input item's floor, plus processing days |
| Cooked items | the kitchen (a 10,000g house upgrade), plus the recipe's unlock condition from `Data/CookingRecipes`, plus every ingredient's floor |
| Books | the bookseller's visit schedule and the cart, both of which are late and unreliable |
| Tapper goods | the tapper recipe (Foraging 3), the tree type, and the tapping duration |
| Anything unrecognised | Spring, with `Basis` recording that nothing was known |

The unrecognised fallback is deliberately permissive. A floor that is too early can only make a
deadline earlier than ideal; a floor that is too late is invisible, because the deadline logic
only ever clamps upward.

### 3.2 Effort, the judgement

An integer score from the same tables. Higher means more work.

- Rod fish: `Data/Fish` difficulty, minimum fishing level, a narrow time window, a weather
  restriction, and required cast depth.
- Ore and bars: depth tier, plus a step for needing a furnace and the smelting wait.
- Crops: seed cost against a 500g start, growth days, and whether the crop needs a trellis or
  a sprinkler to be practical.
- Artisan and cooked: the number of processing steps and their day cost, plus the funding cost
  of the machine or the kitchen.
- Geode minerals and artifacts: expected number of geodes or dig spots to see this item once.
- Books: flat high, because the bookseller is rare.
- Anything unrecognised: the median score of its domain.

`ItemHardness` already scores pool items for the pity trim on a similar idea (rarity tier, needs
a station, spawns late). It is a cruder instrument built for a different purpose, and it takes a
`PoolItem` rather than an item id. The plan should decide whether `ItemHardness` becomes a thin
caller of the new score or stays as it is, but the two must not disagree silently.

### 3.3 Where the data comes from

`GameDataPools` already reads `Data/Objects`, `Data/Crops`, `Data/FruitTrees`, `Data/Locations`,
`Data/Fish` and `Data/Monsters` into the Core `Raw*` boundary records, so Core never touches
Game1. The same boundary extends here. Note that `Data/Fish` is read today only to spot the
`trap` marker; the fields carrying difficulty, time window, weather and minimum level are
discarded and the raw record needs widening. Field indices must be verified against the
decompiled Android source rather than recalled, at
`C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android`.

`Data/Shops`, `Data/CraftingRecipes` and `Data/CookingRecipes` are new reads.

Failures degrade, never throw, matching the existing `GameDataPools` contract: an item the model
cannot derive gets the unrecognised fallback and a logged `Basis`.

## 4. Deadlines from the model

A new pure Core component, `BundleDeadlines`, replaces the pin lookup inside
`BundleClassifier`'s PerItem branch.

Given a bundle's deduplicated ingredient list:

1. Rank the ingredients by `Effort` ascending, breaking ties by ordinal item id so the result is
   deterministic and reproducible from a seed.
2. Assign a checkpoint index. For a bundle of four or fewer ingredients, back the spread against
   Winter: `index = 4 - count + rank`, so two ingredients land on Fall and Winter and three land
   on Summer, Fall and Winter. For more than four, spread proportionally:
   `index = floor(rank * 4 / count)`.
3. Apply the effort weighting: an ingredient scoring at or above the high effort threshold slides
   one checkpoint later, one at or below the trivial threshold slides one earlier. Clamp to the
   four checkpoints.
4. Clamp each deadline upward to that ingredient's `EarliestSeason`. A deadline may never precede
   existence. This step is what makes an impossible gate structurally unreachable rather than
   something a reviewer has to catch.

Every ingredient ends with a deadline no later than Winter, so completion is still required to
win, which is the property the current model has and must keep.

Two consequences fall out for free:

- **The partial pin case disappears.** Deadlines are now computed for a whole bundle at once, so
  there is no longer a state where some ingredients gate and others do not.
- **The ramp clamp gets better data.** `GeneratedBundleSet.ClampRampForObtainability` and the
  read-and-classify clamp in `BundleCatalogBuilder` both take a pin dictionary today, sourced
  from `ItemPools.DerivedSeasonPins`, which only records pins later than Spring and only for
  four pool domains. Both switch to `EarliestSeason` from the new model.

## 5. Configuration and compatibility

- `GameplayConfig.ItemSeasonPins` (the user facing config key) keeps working and keeps winning
  over derived values. An entry there is read as an `EarliestSeason` override.
- A new optional `ItemEffortOverrides` allows overriding the score for a single item.
- `DefaultItemSeasonPins` stops being the source of PerItem deadlines. Its 40 entries move into
  the override table, each validated against its derived floor at build time. An entry that
  demands an item earlier than the model says it can exist is logged loudly and ignored, which
  is exactly the Purple Mushroom failure made non fatal.
- Saves in flight are unaffected in structure. Gates are recomputed at load and at reset from
  live data, as they are today.

## 6. Diagnostics

- `tly_gatecheck` keeps its current output and gains the `Basis` string for each blocking
  ingredient, so a flagged gate explains itself.
- New console command `tly_itemmodel <itemId|bundleName>` prints `EarliestSeason`, `Effort` and
  `Basis`, for one item or for every ingredient of a bundle.
- A generated `docs/item-availability-model.md`, produced the way `docs/engine-bundle-catalogue.md`
  is by `tly_dumpbundles`, so the whole derived model is reviewable outside the game.

## 7. Testing

Core is pure, so every rule is unit testable against synthetic `Raw*` records.

- Per domain: a test per derivation rule, including the gated cases (desert fish, deep ore,
  kitchen recipes, sapling fruit).
- Invariant, asserted as a property over every bundle the catalogue can produce: no deadline
  ever precedes its ingredient's `EarliestSeason`.
- Invariant: every ingredient has a deadline no later than Winter.
- Determinism: the same ingredient list yields the same deadlines across runs.
- The spread and weighting rules at counts 1 through 12.
- Override precedence: user config beats curated override beats derived value.
- Regression: the twelve currently pinned bundles get their derived deadlines recorded in a
  characterisation test, so future rule changes show up as an intentional diff.

Beyond unit tests, `tly_gatecheck` runs on several live boards at Normal and at Hard, before and
after, and the before and after numbers get recorded in the plan.

`DifficultyResolverTests.Normal_Resolves_To_Todays_Config_Values` asserts difficulty dial values,
not gate outcomes, so it does not break. No existing test asserts that an ungated bundle stays
ungated. The balance shift is real but it is not currently pinned by any test, which is itself
worth noting.

## 8. Phasing

Each phase builds, tests and is verifiable on a live board on its own.

1. **Framework plus the leaking pool domains.** `ItemAvailability`, the widened `Data/Fish` read,
   Fish and Metals derivation, `BundleDeadlines`, and the switch of `BundleClassifier`'s PerItem
   branch onto it. Closes the largest part of the leak.
2. **The remaining pool domains.** Crops, forage, crab pot, monster drops. Switches both ramp
   clamps onto `EarliestSeason`.
3. **The authored bundle domains.** Artisan goods, cooking, artifacts, books, saplings, geode
   minerals, tapper goods. Closes Orchard, Chef's, Home Cook's, Spirit's Eve, Forest, Sticky and
   Helper's.
4. **Retire the old path.** Fold the 40 curated pins into the override table with validation,
   delete the PerItem pin lookup, generate `docs/item-availability-model.md`, and record the
   rebaselined difficulty in `STATUS.md` and the release notes.

## 9. Risks

- **Scope.** The pools total roughly 400 items across thirteen domains. The rules are per domain
  rather than per item, which is what makes this tractable, but domains like cooking and books
  carry real analysis. Phasing exists so that value lands before the whole is done.
- **Normal gets harder and nobody has played it.** The difficulty feature is unreleased and
  largely unplayed already. Verification is `tly_gatecheck` plus Jeff playing it, not a test.
- **A wrong floor is invisible.** A floor that is too early silently produces a tight deadline
  rather than an error. The `Basis` string and the generated model doc exist so that a wrong
  floor can be found by reading rather than by losing a run.
