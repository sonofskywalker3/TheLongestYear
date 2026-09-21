# Flavored bundle slots (Dried Fruit, Dried Mushrooms, Smoked Fish)

**Status:** planned, not started. Jeff's rulings 2026-09-21 are recorded inline.

## The problem

Nexus 1137151: an Artisan slot asking for dried fruit reads only "Dried". 0.18.32 relabelled it
"Any Dried Fruit", which is honest but, in Jeff's words, "WAY too easy, what's the point?". The
slot should name one fruit and demand that fruit, the way Mr. Raccoon's bundles do.

## Why the name is blank (settled)

These are flavored goods: the dried or smoked thing lives in `preservesId`, and the display name is
a format string fed from it. A Community Center bundle never sets it, because vanilla parses
ingredients as `(id, stack, quality)` triples and nothing else (`Bundle.cs:133`). With no flavor,
the name falls back to the object's bare `Name`, which is literally "Dried" / "Smoked", and the
match falls back to `ItemRegistry.HasItemId(item, "DriedFruit")` (`Bundle.cs:231`), i.e. any flavor.

Set `preservesId` and vanilla gives us both halves for free: it renders "Dried Apples" and accepts
only dried apples.

## The mechanism: derive, never persist

Mr. Raccoon stores no flavor. It re-derives its whole ingredient list from
`Utility.CreateRandom(Game1.uniqueIDForThisGame, timesFed * 377)` every time (`Raccoon.cs:136`).

We do the same against our own seed. The flavor is a pure function of
`(BundleEngineSeed, bundleIndex, ingredientIndex)`, recomputed identically at generation time and at
every load. Nothing new is persisted, the bundle data string is untouched, and the byte-for-byte
`EngineManifestCheck` (`ModEntry.cs:5011`) keeps working.

Seed basis is `BundleEngineSeed.For(UniqueMultiplayerID, EffectiveBundleSeedLoop)`, already stable
across reloads by construction.

## Rulings (Jeff, 2026-09-21)

1. **Candidates** come from the machine's own accepted inputs, filtered by TLY's reachability, not
   from hand-written seasonal lists. A second source of truth would drift from the difficulty model.
2. **Rollout is new boards only.** Nobody's in-flight slot changes under them.
3. **Stack basis** follows the chosen input's own supply, divided by the machine's input ratio
   ("sounds good"), with the tree caveat below.

## The input ratio, which is the whole balance problem

`Data/Machines`:

| Machine | In | Out |
|---|---|---|
| Dehydrator | **5** fruit | 1 Dried Fruit |
| Dehydrator | **5** mushrooms | 1 Dried Mushroom |
| Dehydrator | 5 Grapes | 1 Raisins (own id, not flavored) |
| Fish Smoker | **1** fish | 1 Smoked Fish |

Today all three sit at basis 35 in `QuantityBasisTables.Stations` ("5 machines, one a day each"),
which counts machine throughput and ignores the 5:1 ratio. Through `AskBands`, Normal rolls 20-50%
of basis, so a board can already ask for 18 Dried Fruit = **90 fruit**; Extreme reaches 28 = 140.
That is only survivable because "any" lets the player throw in whatever they have. Pinning the
flavor without fixing the basis turns it into "90 Apples" and makes the slot impossible.

**New basis for a flavored slot:** `min(BasisByDeadline(input, deadline) / ratio, 35)`.
`QuantityAskPass.BasisByDeadline` already aggregates every source for an item, so this reuses the
existing supply model rather than inventing a table.

## The fruit tree trap (Jeff raised it; TLY already rules on it)

A sapling takes 28 days, and a tree bears only in its own season, so a Spring tree planted in week 1
matures in Summer and would not fruit until a Spring the run never reaches.
`AvailabilityWeeks.FruitTreeFruitWeeks` already encodes this:

- Apricot `(O)634`, Cherry `(O)638`: week 13, "second year or the cart"
- Orange `(O)635`, Peach `(O)636`: week 5, Summer tree from a week-1 sapling
- Apple `(O)613`, Pomegranate `(O)637`: week 9, Fall tree

The reachability filter therefore handles trees correctly as long as candidates are gated on
availability week against **that bundle's own deadline**.

**Additional rule:** Apricot and Cherry are barred from flavored slots outright. "Second year or the
cart" is a coin flip on cart stock, which is no basis for a mandatory slot. (Confirm with Jeff.)

## Work

1. **Core `FlavoredSlotRules`** (pure, tested): candidate set per base id, from
   `category_fruits` (-79) / the existing `EdibleMushrooms` list / `category_fish` (-4), minus the
   cart-only bar and the pool exclusions; then filtered to items the availability model places at or
   before the bundle's deadline.
2. **Core `FlavoredSlotPicker`** (pure, tested): `(seed, bundleIndex, ingredientIndex, baseId,
   candidates) -> input id`, own rng stream salted like `BoardRepairService` so it cannot perturb
   other draws.
3. **Quantity**: teach `QuantityAskPass` the flavored basis above, so the stack is rolled for the
   fruit actually chosen.
4. **Stamp**: `MetaState.FlavoredSlots` written at board write time; generation and re-derivation
   both read the stamp, so old boards reproduce byte for byte under the old rules and keep matching
   the manifest check.
5. **Runtime**: Harmony postfix setting `ingredients[i].preservesId` on the constructed `Bundle`, so
   display AND matching both follow. Replaces the hover-text patch for flavored boards; the
   0.18.32 "Any ..." label stays as the fallback for pre-stamp boards.
6. **Tests**: determinism (same seed twice, same flavor), deadline gating (no Cherry before week 13,
   never at all under the bar), ratio maths, and an upgrade test that a pre-stamp board still
   matches `EngineManifestCheck`.

## Risks

- **Board determinism.** Any input that moves derived effort or week for a rolled-pool item breaks
  the byte-for-byte check (`CookedDishAvailability.cs:60-64`). The stamp in step 4 is what contains
  this. Without it, every existing save falls to the legacy classify path with a WARN on load.
- **Smoked fish is 1:1**, so its basis stays close to the fish's own supply and the asks will be
  bigger than dried fruit's. Worth a look at real numbers before shipping.
- **Raisins** are a separate item id, not a flavored one, so they are out of scope and keep the
  station basis.
