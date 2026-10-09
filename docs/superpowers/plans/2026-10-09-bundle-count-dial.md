# Bundle-count dial: plan (2026-10-09)

Spec: `docs/superpowers/specs/2026-10-09-bundle-count-dial-design.md`. Branch `bundle-count-dial`
(no manifest version bumps on this branch). TDD: each Core task starts with a failing test.

1. **Vault fallback indices** (separate commit): `Core/VaultRules.cs` 34-37 to vanilla 1.6.15's
   23-26, checked by a test against `VanillaBundleBoard.Standard`. Done first.
2. **Rule + plumbing (Core):** `BundleCountRule` (Target, ForStep), `DifficultySettings.BundleCount`
   (SetAll, Clone, IsAllNormal), `DifficultyProfile.BundleCount` (nullable), resolver row.
   Tests: rule table, floors and caps, rng only consumed on a range, resolver per step, old stamp
   null.
3. **Planner (Core):** `RoomBundleCountPlanner.Plan(picks, positions, target, rng, allocate, out
   shortfall)` and `ReservedBundleIndices`. Tests: fewer keeps crops, more adds unique names with
   reserved indices, short pool stops and reports, equal count returns the same picks, allocation
   skips used indices and is deterministic.
4. **Engine wiring (Mod):** pass 1 in `BundleEngine.Generate` calls the planner when the profile has
   a rule; log per room when the count changes or falls short.
5. **Stale keys (Core + Mod):** `StaleBundleKeys.Find(board, liveKeys)` (tests), `BundleKeySync`
   (reflection prune + CC lookup rebuild + ingredient refresh), called from `WriteToWorld` and from
   SaveLoaded for engine boards with a stored board.
6. **Held board carry (Core + Mod):** `BundleCountStamp.ForReset(newProfile, previous, holding)`
   (tests), used in `WorldResetService` once `holdingBoard` is known.
7. **Settings surface:** GMCM row + i18n name/tooltip, `tly_difficulty` row, reset log line.
8. **CHANGELOG** (Unreleased section, credit ThirteenRedCats), TODO entry updated.
9. **Live check** per the brief (if the game is free), then restore master's build in Mods.
