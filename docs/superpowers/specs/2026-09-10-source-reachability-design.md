# Source reachability: keeping unreachable items off the board

Design spec, 2026-09-10. Target release 0.18 (Jeff).

## The problem

The board generator assumes an item is fair game unless something proves otherwise. Nothing in
the pipeline asks how an item is actually obtained, so an item that cannot be reached inside a
loop can be asked for by a bundle, and the season gate behind it becomes unwinnable.

This is not hypothetical. Nexus posts tab, pitytheviolins, 2026-09-10:

> Would you be interested in adding compatibility for The Fishmonger? My CC is asking for a
> couple items from this mod but none of them can be obtained until Ginger Island.

The mod (Nexus 16326, a Content Patcher pack) was read in full. What it adds and how it lands:

| Content | Count | Reaches the board via | Currently filtered? |
|---|---|---|---|
| Fish | 21 | `Data/Locations` on `IslandNorth`/`IslandSouth`/`IslandSouthEast`/`IslandSouthEastCave` | Yes, the `Island` marker catches all of them |
| `WornOutHat` | 1 | `Data/Locations` fish table on Beach, Sewer, WitchSwamp, BugLand | Yes, Type `Trash`, category -20, never vetted into a pool |
| Crops | 10 | `Data/Crops`, harvest categories -75 and -80, real season fields | **No** |
| Cooked dishes | 11 | `Data/Objects` category -7, straight into the `Cooking` pool | **No** |

The crops and dishes are the leak, and they leak for a reason worth stating precisely.

### Why the existing filter had no chance

`ExcludedLocationMarkers` (`Island`, `FableReef`, `CrimsonBadlands`, plus the built-in `BugLand`)
filters **spawn tables**. It matches a substring against a `Data/Locations` key.

The Fishmonger crops have no `Data/Locations` row at all. Their only source is a shop entry,
`VoidWitchCult.TheFishmongerNPC_TheFishmongerSeeds`, and shop data is never read by the pool
builder. There is nothing for a marker to match against, so no marker value could ever have fixed
this.

Worse, the obvious repair also fails. Tracing the shop to its owner's home map yields
`VoidWitchCult.TheFishmonger_Fishmonger_GI_Inside`, which does not contain the substring
`Island`, and Constance's `HomeRegion` is set to `Town`. String matching misses this mod twice.

The mod does say where the shop is, in the one place an author cannot fudge without breaking
their own mod:

```json
{ "Action": "EditMap",
  "Target": "VoidWitchCult.TheFishmonger_Fishmonger_GI_Inside",
  "AddWarps": [ "4 12 IslandSouth 26 43" ] }
```

The only door out leads to `IslandSouth`, and the only door in is a tile placed on `IslandSouth`.
A map's name is a label an author picks freely. Its warps are how players actually reach it.

### What already exists

`TheLongestYear.Core/Availability/` holds fifteen rules, including `ShopAvailability` and
`CookedDishAvailability`, and the recursive ingredient walk is already there: a dish whose
ingredient no rule can place scores Extreme and is left unplaced.

The gap is not a missing system. It is that the model's answers never gate pool membership.
`ItemAvailabilityModel.UnrecognisedEffort = 6` deliberately places an unrecognised item mid-scale
so it "neither leads nor trails the effort ranking of a bundle it appears in". The item still goes
on the board. This spec adds a harder verdict alongside the effort scale: not "unknown effort" but
"provably out of reach, therefore not a candidate".

## Decisions

All four taken by Jeff on 2026-09-10.

1. **Conservative rule.** Only exclude what is provably unreachable. An item we cannot trace stays
   allowed. Flipping the assumption (prove reachable or be dropped) would silently strip large
   amounts of legitimate content from packs like SVE.
2. **Full chain, recipes included.** Three source rules, not one: shops, crop seeds, and recipe
   ingredients. Fixing crops alone would leave this mod's eleven dishes leaking and would put us
   back here.
3. **Reachability by doors, seeded by the marker list.** The marker list stays as the set of
   forbidden places; everything reachable only through a forbidden place is out of reach too.
4. **Repair live boards on load.** A player mid-year is holding a board built by the old rules,
   and if the impossible ask is blocking their season gate, waiting a year for the next rewind is
   not a fix.

## Design

### 1. `SourceReachability` (new, Core)

A pure rule answering one question: is this qualified item id provably unreachable this run?

Pure matters here. The test project references only `TheLongestYear.Core`, which is why the
0.17.14 Joja fix could not be unit-tested and needed an in-game run to verify. Keeping this rule in
Core means it is covered by the suite.

**Inputs**, as `Raw*` boundary records following the existing `ItemPoolModel` pattern:

- `RawShopListing(ItemId, ShopId)`: who sells what, from `Data/Shops`.
- `RawShopPlacement(ShopId, LocationName)`: where a shop can be opened.
- `RawLocationLink(From, To)`: one warp edge.
- Seed to harvest, from `Data/Crops` (already keyed by seed id, so the seed is in hand).
- Recipe output to ingredients, from `Data/CookingRecipes` and `Data/CraftingRecipes`.

**Output**: the set of qualified ids that are provably unreachable, plus a reason string per id for
the log.

### 2. The reachability walk

1. Seed the forbidden set from `ExcludedLocationMarkers` plus the built-in markers, matched as
   today.
2. Build the warp graph from the live locations at the game boundary.
3. Flood-fill from the farm, refusing to enter any forbidden node.
4. Every location the fill does not reach is out of reach, whatever it is called.

**Doors that open during the year count as passable.** The bus to the Desert, the Rusty Key to the
Sewer, the Steel Axe to the Secret Woods: the walk asks whether a map is connected to the world by
some route other than through a forbidden place, not whether the player can walk there on Spring 1.

This is deliberate and load-bearing. Without it a fresh board would decide the Desert is
unreachable and strip Cactus Fruit, contradicting the standing ruling in `BundleCatalogBuilder`
that Desert and Deluxe Coop items are valid targets the player invests in during the run.
`LocationGating` already handles the *timing* of those places, and this rule must not duplicate or
contradict it: reachability answers "ever, this run", `LocationGating` answers "from which week".

### 3. The three source rules

An item is unreachable when **every** known source of it is unreachable. Any untraceable source
leaves the item allowed.

- **Shop**: unreachable if every shop listing it sits in an unreachable location.
- **Crop**: unreachable if its seed is unreachable.
- **Cooked or crafted**: unreachable if any single required ingredient is unreachable. (Any, not
  every: one impossible ingredient is enough to make the dish impossible.)

Recursive, with a visited set as a cycle guard and memoisation per generation. Category refs in
recipes ("any milk") resolve the way `CookedDishAvailability` already resolves them: a category is
unreachable only if every member is.

### 4. Applying the verdict

`ItemPoolBuilder.Build` already threads an `excluded` set through all thirteen pools. Merging the
unreachable ids into that set fixes Crops, Cooking, ArtisanGoods, Metals, ByKind and the rest in
one move with no per-pool work.

Merged at the same point as `YearTwoCrops.ExcludedFor`, and for the same reason, but applying at
**every** difficulty step. `YearTwoCrops` is Easy-only because a player can buy the Pierre upgrade
and reach Garlic. Nothing reaches Ginger Island inside a loop, so difficulty is irrelevant here.

Defaults must not live in `BundleGenerationTuning.ExcludedItemIds`: a saved `config.json` overrides
serialized list defaults wholesale (Nexus 1122358). This is derived per generation, so the question
does not arise, but the derived set must never be written back to config.

### 5. Repairing live boards

On save load, re-derive the unreachable set and walk the existing board. For any slot holding an
unreachable item, swap in a reachable one from the same pool, preserving the bundle's theme, slot
count and quality asks.

- Slots already donated are left exactly as they are. A player who somehow has the item keeps
  credit for it.
- The swap reuses the existing slot filler so a repaired slot is indistinguishable from a freshly
  generated one.
- Repairs are logged per slot and counted in one summary line.
- If nothing is unreachable, the board is not touched and nothing is written.

### 6. Diagnostics

- One log line per dropped item with its reason ("no reachable shop", "seed unreachable",
  "ingredient X unreachable").
- A summary count at generation.
- `tly_dumpbundles` reports the dropped set, so the next report of this kind is visible rather than
  inferred. This matters: the stale, gitignored `engine-bundle-catalogue.md` is what hid the Joja
  re-roll bug for a fortnight.

## Scope

**In:** the reachability walk, the three source rules, the pool-builder hook, the load-time board
repair, diagnostics, unit tests.

**Out:**

- Any per-mod exclusion list. The whole point is that no mod needs naming.
- Changes to `LocationGating`, `AvailabilityWeeks` or the effort model. Reachability is a separate
  question from pacing and must not disturb the difficulty work.
- Ginger Island access inside a loop. Still forbidden, unchanged.
- Year 2 play after the hall is finished. The board only matters during a loop.

## Testing

Core rules are pure, so the suite covers them directly:

- Warp walk: a map behind a forbidden map is unreachable; a map with a second door to the world is
  not; a map behind a gate that opens during the year (Desert) stays reachable; cycles terminate.
- Shop rule: an item in two shops, one reachable, stays allowed; both unreachable, dropped.
- Crop rule: unreachable seed drops the harvest.
- Recipe rule: one unreachable ingredient drops the dish; a category ref with one reachable member
  does not.
- Conservative default: an item with no traceable source is never dropped.
- A regression fixture built from the real Fishmonger data (10 crops, 11 dishes, the
  `IslandSouth` warp) asserting exactly those 21 ids drop and the 21 island fish are unaffected,
  since the `Island` marker already handles them.

The board repair needs an in-game run: load a save whose board holds an unreachable ask, confirm
the swap, confirm donated slots survive, confirm a clean board is untouched.

## Risks

1. **Custom locations must exist when the board is built.** They are created on load and generation
   runs after `loadForNewGame`, so this should hold, but it is the assumption most likely to be
   wrong and should be verified first, before the rest is built on it.
2. **The board repair mutates a live save.** Highest-risk piece. Donated slots and quality asks are
   the things to get wrong.
3. **Over-exclusion would be invisible and bad.** A bug in the walk could quietly strip real
   content. The conservative default limits the blast radius, and the diagnostics exist so it shows
   up in a log rather than as a confused player.
4. **Performance.** One graph walk plus a memoised recursion per generation. Expected to be
   negligible against the existing generation cost, but worth a timing check on a heavily modded
   setup.

## Verification of the original report

After this ships, a save with The Fishmonger installed should generate a board with none of the ten
crops, none of the eleven dishes, and all twenty-one island fish still absent (already handled by
the marker), with no config edits by the player.
