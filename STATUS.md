# The Longest Year - Status

**Last updated:** 2026-10-09 night (hygiene pass, 0.19.61 to 0.19.90)
**Branch:** `master` at 0.19.90; every commit pushed to `origin/master`, nothing local-only
**Last public release:** 0.19.21 (tag `v0.19.21`, 2026-10-09). Everything from 0.19.22 to 0.19.90 is built and pushed but NOT released (GitHub release, Nexus file, version, description and changelog untouched)
**Tests:** 3638 passing, 0 failing (`dotnet test TheLongestYear.sln -c Release`)
**Build:** Release, 0 errors, 0 warnings (mod, Core and tests)
**Deployed:** `Mods/TheLongestYear` holds the 0.19.90 build; game closed

The day-by-day history up to 2026-09-21 is archived in
[`docs/status-archive/STATUS-to-2026-09-21.md`](docs/status-archive/STATUS-to-2026-09-21.md).
What is waiting for a release is listed in `TODO.md` under the "BUILT, NOT RELEASED" headings.

## Branches

- **`master`**: the release line (bug fixes and balance). Owns every version bump.
- **`story`**: The Longest Year story line (ending, rewind, opening montage, season beats,
  sabotage, Year 2). Long-lived on purpose (Jeff, 2026-09-06); ships to master once, as one
  release, when Jeff says so. Last merge of master into story: 0.19.21. Story is 551 commits ahead
  of master; master is 114 commits ahead of story (0.19.22 to 0.19.90), to be merged into story
  after the next master release (merge, never rebase).

## Not released yet (0.19.22 to 0.19.90)

- 0.19.22 to 0.19.60: kept building spots, animal powers and the Animals tab, the Vault in
  Bundles per room, and the core-systems bug hunt fixes (details in `TODO.md`).
- 0.19.61 to 0.19.90: the hygiene pass below. No gameplay change except one config rule:
  a `BundleQuotas` entry with a negative count is now logged and skipped (its doc always said so).

## 2026-10-09 night: hygiene pass (0.19.61 to 0.19.90)

- One debug command table (`ModEntry.Commands.cs`) drives both the SMAPI console and the file
  bridge, so every `tly_*` command works in both (`tly_netstate`, `tly_dumpevents`,
  `tly_dumpreplayable` now work through the bridge; `tly_day28continue`, `tly_raritystep` and
  `tly_booktest` can be typed).
- `ModEntry.cs` went from 5,901 lines to about 550: GMCM, HUD, save lifecycle, board resolution and
  twelve themed `ModEntry.Commands.*.cs` files, each a pure move.
- `WorldResetService.PerformReset` and `ModEntry.OnSaveLoaded` read as named phases in their
  original order (phase bodies in `WorldResetService.ResetPhases.cs` and `ModEntry.SaveLoad.cs`).
- Cookbook and Craftbook share `RecipeBookMenu`.
- Dead config fields (`DefaultWeatherPreviewSlots`, `DefaultCartPreviewSlots`, `WeeklyHubHotkey`),
  dead members and the never-read `StashCapacityTier` removed; old configs and saves still load.
- Silent catches now log (Warn for the shrine's cart stock and the stash's farmhouse entry, Trace
  elsewhere, once per id on draw paths).
- Nullable warnings 66 to 0. New tests: JpBoostHelper, Core.ConfigOverrides (the config merges,
  moved out of ModEntry), BundleAskRewrite.LowerAsks, WeightedSampler, legacy config/save keys.
  The i18n test fixture finds the repo itself, so `dotnet test -o` works.
- `test-output/` untracked (it was always in `.gitignore`); 16 README backups that duplicated git
  history and an unused banner placeholder removed.
- Live checks (game minimized, log only, throwaway farms deleted after): new game, two
  `tly_reset`s, a fail-night rewind through the hold question, upgrade menu and Cookbook,
  `tly_leaktest` PASS, reload, Normal-board new game and reset, `tly_booktest both` 16/16 PASS,
  13 commands through the console and the bridge; Harmony 106 applied, 0 failed; no ERROR lines.
