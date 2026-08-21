# Plan — `BundleSource: Engine | Vanilla` (spec `specs/2026-08-21-bundle-source-engine-vanilla-design.md`)

Rulings: "TLY Custom" stays the DEFAULT, pre-selected Advanced Options entry; Normal/Remixed are
deliberate picks (per save). Config flips apply at the next reset.

## Core (pure, tested)
1. `BundleSourceNames` — `Engine` / `Vanilla` constants + `IsVanilla(string)`.
2. `MetaState.BundleSource` (default `Engine`), `MetaState.VanillaBundleType` (null until known).
3. `GameplayConfig.BundleSource` (default `Engine`).
4. `EngineModeDecider.Decide(..., vanillaSource)` → `LegacyReadAndClassify` whenever vanilla.
5. `BoardInspection.Fingerprint(bundleData)` + `MatchesReference(live, reference)` (Standard
   inference: every live key exists in the reference with an identical value).
6. Fixture test: every themed bundle string in Challenging CC Bundles' `Vanilla` pack classifies
   (no nulls), ramps monotone and end at X.

## Mod
7. `BundleOptionPatch`: three entries — TLY Custom (default, index 0) / Normal / Remixed; own
   apply callback (vanilla's indexes its original 2-entry array) → `Game1.bundleType` +
   `LastChoice`. Default index follows the config (Vanilla ⇒ Normal).
8. `ModEntry`: stamp `BundleSource`/`VanillaBundleType` on the new-game load from `LastChoice`;
   `ResolveRequirements` honours the save's source (Vanilla ⇒ legacy path, log says so; infer
   `VanillaBundleType` from the Data/Bundles asset when unknown); GMCM `AddTextOption`;
   board-state rebuild extracted to `RebuildBoardState()`; `OnDayStarted` fingerprint check in
   Vanilla mode → rebuild catalog + requirements + `RunController.ReplaceCatalog/Requirements`.
9. `WorldResetService`: stamp `BundleSource` from config before step 0; Vanilla ⇒ set
   `Game1.bundleType` from `VanillaBundleType` before `loadForNewGame`, skip the engine write,
   marker −1, `LastGeneratedRequirements = null` (SaveLoaded re-resolves after the reload).
10. Classification completeness: `BundleCatalogBuilder.Build` catalogs every ingredient;
    `SeasonResolver` unions crop + forage seasons; legacy `BuildRequirements` applies
    `ClampRampForObtainability` with base + derived pins.
11. `RunController.ReplaceCatalog`.
12. i18n (AGO entry tooltips, GMCM strings), README + Nexus (Install step 5, Configuration row).

## Verify
- 700+ tests green; build; deploy.
- Smoke: Engine reset on a clone (engine manifest line); flip config to Vanilla → reset →
  `legacy read-and-classify` + Standard names; stamp Remixed → reset → remixed names differ.
