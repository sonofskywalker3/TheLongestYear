# TLY Custom boards use vanilla items only (design, 2026-10-08)

## Why

A player (PixxiePerfect, Nexus posts, 2026-10-07) asked for official support for Tech's Cross-Mod Bundles
(Nexus 51035), which fills the Community Center with items from SVE, Ridgeside, Cornucopia and ~7 other mods.
Jeff's call: supporting other mods' items on TLY Custom boards would mean investigating and hand-balancing every
new item from every mod, which is not possible in the current phase. So:

- **TLY Custom (BundleSource `Engine`) boards ask only for vanilla items and give only vanilla rewards.** Items
  from other mods never enter the pools, the templates, board repair replacements or the reward pool on that path.
- **Normal and Remixed are untouched.** Other mods' boards (Tech's, Challenging CC Bundles, anything that edits
  Data/Bundles) keep working there freely, as today.
- **README (and the Nexus description, which must match it) says plainly:** other mods are not officially
  supported, though Jeff will look at specific issues when they are reported; using Normal or Remixed with other
  mods may mean the bundles do not reshuffle each loop and may ask for items you cannot get.

## Today (traced 2026-10-08)

Engine boards read live game data on purpose ("SVE-proof by construction", `GameDataPools.cs:16-23`), so modded
items reach the board through: the Data/Bundles + RandomBundles templates (`VanillaBundlePool.cs:76-77`, mixed in by
`BundlePoolRecipes.cs:229-234,403-423`, kept-slot fallbacks `BundleEngine.cs:346,396-402`, pass-through Vault/Joja
`BundleEngine.cs:275-291`); the item pools (`GameDataPools.cs:60-145`, ByKind/Dye/WinterOnly walk all Data/Objects);
authored non-fixed sources and flavored slots (pool-fed); BoardRepairService replacements (`BoardRepairService.cs:277-295`,
pools from `ModEntry.cs:606-610`); rewards (`BundleEngine.cs:476-483`, `BundleRewardShuffle.CleanPool`). The only
origin notion today is `ItemPoolBuilder.WeightFor` (draw weight for ids containing `.`), which filters nothing and
misses unprefixed mod ids.

## Design

1. **A baked vanilla id set** in `TheLongestYear.Core` (frozen/readonly set of qualified ids), generated once from
   the unmodded 1.6 game content (Data/Objects, BigCraftables, Furniture, and any other item type the engine can
   ask for or reward), with the generator committed under `tools/` so it can be re-run after a game update. A
   prefix rule is not enough (unprefixed mod ids exist). A runtime read of raw content is acceptable only if it is
   provably unaffected by SMAPI asset edits; the baked set is preferred because it is deterministic and testable.
2. **One flag, Engine path only.** Thread a "vanilla only" option from the Engine generation entry points into
   `ItemPoolBuilder.Build` (add non-vanilla ids to `excluded`, which `ExcludedIds` already exports to the
   downstream consumers), filter template slots in the Engine-only template path, and filter the Engine reward
   pool via a parameter (Remixed reuses `RewardPool` at `WorldResetService.cs:1464`, so do not change it outright).
   BoardRepairService replacements on Engine boards must come from the filtered pools. Normal/Remixed behavior must
   be byte-identical to today.
3. **Determinism.** The manifest check regenerates and compares at load (`BundleEngine.cs:192-194`). The filter
   must give the same result on every load. Existing saves: the board already in play stays as it is (stored
   WrittenBoard path); the next loop's board is vanilla-only. Verify an existing save does not fall into the
   "foreign bundle data" fallback because of this change, and say what happens for saves without a stored board.
4. **Category slots** ("any fish") still accept modded items when donated. That is fine and out of scope.

## Out of scope

Cross-Mod Bundles integration, an API, per-mod support lists, Nexus page edits (README + bbcode source change in
this task; pushing the description to Nexus waits for Jeff's yes).
