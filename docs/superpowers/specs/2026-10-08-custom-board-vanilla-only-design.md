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

## Addendum 1: "Allow mod items in custom bundles" (per save, 2026-10-08)

Jeff changed his mind on making the filter unconditional: it stays, but becomes a per-save choice.

- **Option.** GMCM "Allow mod items in custom bundles", next to Bundle source. With a save loaded it reads and
  writes that save's value; on the title screen it edits the config default that a NEW game starts with. Same
  shape as BundleSource (config default, `MetaState` choice, applied at the next reset).
- **Fields.** `MetaState.AllowModItemsInCustomBundles` (bool?, the save's choice) and
  `MetaState.BoardAllowsModItems` (bool?, what the board on disk was generated with). `GameplayConfig.AllowModItemsInCustomBundles`
  (default false) is the new-game default only.
- **Defaults.** A new game (the load right after SaveCreating, or an adopted farm with no TLY data yet) writes
  the config default, which is OFF. Any other save whose stored choice is missing is an existing save from before
  this option and reads as ON, so it keeps the behavior it was created with. The rule lives in
  `Core.CustomBoardModItems` and is tested; the new-game write happens before ResolveRequirements, so the fresh-run
  board never reads the missing-field rule.
- **ON** passes null for `vanillaOnlyIds` everywhere the Engine path filters today (pools, templates, reward
  pool, board repair, dumpbundles/genbundles): exactly the pre-0.19.2 behavior. **OFF** is the 0.19.2 filter.
  Normal/Remixed never read it.
- **Applies at the next reset.** The reset stamps `BoardAllowsModItems` from the choice when it builds a new
  Engine board; a held board (Fail-night keep, `ConsecutiveHolds > 0`) keeps its stamp, like
  `RandomBundleRewardsBoard`, so keeping a board never changes it.
- **Determinism.** Load-time checks (board repair pools, the seed re-derivation in ResolveRequirements, the
  diagnostics) use the board stamp, never the live choice, so toggling mid-loop changes nothing until the next
  reset. A board with no stamp (written before this field) tries the existing-save value first and then the
  opposite, like the EnableNonObjectDonations retry, so a board written under 0.19.2/0.19.3 (vanilla-only) and
  one written earlier (mod items allowed) both verify instead of falling to the "foreign bundle data" path.

## Addendum 2: Normal and Remixed roll a fresh Tech's Cross-Mod Bundles board each loop (2026-10-08)

Tech's Cross-Mod Bundles (`TechnicalityCreations.CrossModBundles`, Nexus 51035) prefixes `DataLoader.Bundles` to
return its own static board, which it generates only at save creation (or when its save data is missing). So on
Normal every TLY reset (`loadForNewGame`) would get the same Tech board back, and on Remixed vanilla's remix starts
from that board and replaces positions, so the Tech board is lost or hybridized. Jeff's decision (0.19.6): Normal
rerolls a fresh Tech board each loop too, exactly like Remixed (0.19.5 rerolled on Remixed only).

- **Hook.** `WorldResetService.PerformReset`, vanilla branch, after `loadForNewGame` has built the board and only
  when no held board is being restored: if the save's source is Normal or Remixed and Tech's mod is loaded, call Tech's
  `TechsCrossModBundles.ModEntry.GenerateBundles` by reflection. It rolls a new board with `Game1.random`, stores
  it as its own Data/Bundles and writes it to the world. TLY's own passes (difficulty pass, capped-ask
  clamp, reward shuffle) then run over that board, and the post-reset reload classifies it, so the Tech board is
  the expected board for the loop (no "changed by another mod" pass, since the fingerprint is taken from the board
  the reload sees).
- **Skips.** TLY Custom, Tech not loaded, and the held-board restore (Fail-night keep).
- **Adapter.** `ITechBundlesRerollTarget` (IsLoaded, Reroll) wraps the reflection; `TechBundlesReroll.Decide/Run`
  in Core holds the decision and the fallback and is unit-tested with a fake. Reroll logs Info on success. If the
  type or method is missing or it throws, one Warn ("Tech's Cross-Mod Bundles changed; this loop uses the game's
  own board") and the reset continues with the game's own board.
- **No code or item lists from Tech's mod** are copied; TLY only names its type and method.
