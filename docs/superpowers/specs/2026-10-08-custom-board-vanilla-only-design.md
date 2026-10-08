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

## Addendum 3: TLY is the board of record on every load when Tech's mod is loaded (2026-10-08)

A live check (TLY 0.19.6 + Tech's Cross-Mod Bundles 1.0.2) found Tech's `SaveLoaded` handler reads its
save key `TCMB` and calls `SetBundleData(DataLoader.Bundles)` with its own raw board on every load (or
generates a brand-new one when `TCMB` is empty). TLY's post-reset save calls `SaveGame.Save()` directly, so
SMAPI's `Saving` never fires and `TCMB` never learns TLY's reroll. Tech loads before TLY, so its handler ran
first and TLY classified the replaced board. Normal/Remixed lost the reroll and TLY's difficulty pass,
capped-ask clamp and reward shuffle on the post-reset reload, and replayed one raw Tech board every loop; TLY
Custom from loop 2 fell to "engine manifest mismatch (stale or foreign bundle data)" and played Tech's board.

- **The stored board.** Engine already keeps `MetaState.WrittenBoard`. On Normal/Remixed the reset now stores
  the final board it wrote (after the difficulty pass, capped-ask clamp and reward shuffle, or the held-board
  restore) in the same field, but only when Tech's mod is loaded at that reset; otherwise it stays null as
  before. Reusing the field means the existing mirrors (unstackable-ask clamp, theme week discount) keep it in
  step with every later TLY board write. The Engine manifest path never reads it on a vanilla source
  (`EngineModeDecider` returns read-and-classify there).
- **Load order.** TLY's `SaveLoaded` work is registered twice: a normal-priority handler that runs it when
  Tech's mod is NOT loaded (unchanged for everyone else), and an `[EventPriority(EventPriority.Low)]` handler
  that runs it when Tech's mod IS loaded, so it always follows Tech's normal-priority handler regardless of
  load order.
- **Restore.** Inside that load, after the save's TLY state is read and before any board repair,
  classification or `ResolveRequirements`: when Tech's mod is loaded, this is the host, and a stored board
  exists, every stored key whose live value is missing or differs (display-name field ignored, as the
  manifest check does) is written back with `SetBundleData`, the CC ingredient cache is refreshed, and one Info
  line is logged ("Tech's Cross-Mod Bundles rewrote the board on load; restored this loop's board"). Tech's
  board and TLY's share vanilla's key space (Tech uses the vanilla `Room/index` keys; the Engine writes every
  key of `Data/Bundles`, which Tech does not edit since it patches `DataLoader.Bundles`, not the asset), so
  after the write every key TLY owns equals TLY's board and no Tech-only key is left behind. Classification,
  the fingerprint and, for Engine, the stored-board manifest check then see TLY's board. Decision logic lives
  in `Core.TechBoardOfRecord` behind `ILiveBundleBoard`, unit-tested with a fake.
- **Tech's own state is left alone (design step 3 skipped).** After the restore, Tech's static board and its
  `TCMB` still hold an older raw board. That is harmless: TLY wins on every load anyway, and `TCMB` could never
  match TLY's post-pass board, so syncing it would not remove the restore. The stale static only feeds
  `DataLoader.Bundles`, whose readers are the reset (Normal/Remixed reroll right after it, Engine overwrites
  every key, a held board is written back from the snapshot), the empty-board lazy init (never after a load),
  and `UpdateBundleDisplayNames` (below). Writing another mod's save data through its private `Helper` was
  judged too invasive for no gain.
- **Localized names.** `NetWorldState.UpdateBundleDisplayNames` fills field 6 of every live bundle by matching
  the bundle NAME against `DataLoader.Bundles` values (Tech's board when Tech has data); Tech's values have
  only five fields, so the lookup finds no display name and falls back to `Strings\BundleNames:<name>`, then
  the raw name. That is the same for a stale or a fresh Tech board and for TLY's restored values, and it is
  recomputed on every `BundleData` refresh, so a restored value's field 6 never sticks. The CC menu keeps
  working; names on a Tech-loaded save are the BundleNames strings or the raw names, as already the case.
- **Scope.** Without Tech's mod nothing changes: same handler priority, no stored vanilla board, no restore.
  Challenging CC Bundles and other bundle mods keep the detect-and-reclassify path. A Normal/Remixed save from
  0.19.6 has no stored board until its next reset; until then it keeps today's behavior.
- **Loop 1 (0.19.8).** A new Normal/Remixed game has no reset to store its board, so the new-game load (the
  low-priority one, after Tech's handler, before TLY's clamp and any week discount) stores the live board as
  the board of record when Tech's mod is loaded and none is stored yet; the existing mirrors keep it current.
  An existing save still in loop 1 without a stored board is not adopted at load, since Tech has already
  rewritten its board by then; it gets one at its next reset.
- **Board repair is part of the board of record (0.19.9).** The load-time repair seeds from
  `BoardRepairStability.Seed` (farmer id + `EffectiveBundleSeedLoop`, salted), not the run seed that a new game
  assigns after the first repair; walks bundles in ordinal key order; and mirrors every swap into
  `WrittenBoard` when one is stored, like the unstackable clamp. A reload then restores the repaired board and
  the repair has nothing to do. This also fixes TLY Custom, where an unmirrored swap made the stored-board
  check fail and the save fall to the read path.
