# Future-Expansion Notes

**Date:** 2026-05-26
**Status:** Planning-only — nothing to implement yet.
**Purpose:** Capture design constraints so v1 work doesn't paint future expansions into a corner.

## Targeted future work (not in scope now)

### SVE compatibility pass
- After v1 ships and feels solid, add an explicit Stardew Valley Expanded compatibility pass.
- **Current code is already mostly SVE-safe:**
  - `BundleCatalogBuilder` reads live `Game1.netWorldState.Value.BundleData` — SVE-added bundles appear automatically.
  - `SeasonResolver` reads live `Data/Crops` and `Data/Locations` — SVE crops/forage get the right seasons.
  - Theme/season overrides use string ids, so SVE-added items can be pinned via config.
- Likely friction points to investigate later:
  - SVE adds rooms not in `RoomThemeMap` (e.g. "Bookseller Room"?). Need a fallback theme assignment.
  - Some SVE bundles have non-`(O)` qualifier prefixes (rings, big-craftables) that `BundleParsing.NormalizeItemId` already passes through but downstream rarity/season resolvers may not handle.

### Longest Year 2 (Year 2 perfection content)
- Continues the same JP/upgrade/stash systems but the loop starts Spring 1 of **Year 2**.
- New CC-equivalent goals: Ginger Island restoration, Movie Theater unlock, Qi quests, walnut hunt.
- Open design question (defer): does the player **start with JP-banked progress** (carry over upgrades and stash from LY1's winning run), or do they **start fresh** with only the meta-skills banked? The user wants to decide this when LY2 is on deck — both should remain implementable.

### Longest Year 3 (Year 3 ultimate perfection)
- Loop starts Spring 1 of Year 3. Same shape as LY2 but with stricter completion targets ("ultimate perfection" achievement, all stardrops, all friendships, etc.).

## v1 design constraints to preserve

These are things to **not regress** in v1 so the future expansions remain feasible:

1. **Start-of-run is not hard-coded to "Year 1 Spring 1."** `RunController.OnRunLoaded` reads `Game1.season` and `Game1.dayOfMonth` and `WorldResetService.PerformReset` is the only thing that explicitly resets the calendar. LY2/LY3 will swap `PerformReset` for a Year-2/Year-3 baseline reset.
2. **Catalog is driven by data, not hardcoded.** `CcItemCatalog` is the test fallback only; production uses `BundleCatalogBuilder`. LY2/LY3 will plug a different catalog source (Ginger Island walnuts, movie theater dishes, etc.) without touching the gate/JP code.
3. **Gate evaluator is catalog-agnostic.** It takes a `YearPlan` and a `catalog: IReadOnlyList<CcItem>` — works for any goal set, not just the vanilla CC.
4. **Meta-condition system is generic.** `UpgradeDefinition.MetaRequirement` uses a "namespace:value" string. LY2/LY3 can add `completed:GingerIsland`, `walnuts:50`, etc. without changing the schema. `MetaState.MeetsMetaRequirement` returns false for unknown namespaces — older code defaults to "requirement not met" rather than ignoring it (safer than the alternative).
5. **`MetaState` is the only thing that survives a reset.** Adding new banked accumulators (LY2: islands cleared, walnuts pooled?) goes here. Anti-save-scum rule still applies: commit only in `Saving` event.
6. **`AnimalSpeciesEverOwned` is a model for future "ever did X" tracking.** Other LY2/LY3 progress flags should follow the same pattern (list of strings, populated by SMAPI events at run-time, committed at save).

## Things that may need refactoring for LY2

- `Theme` enum is hardcoded to the five CC themes (Foraging/Farming/Fishing/Mining/Mixed). LY2 might want new themes (Island/Movie/Walnut). Easy expansion but worth noting.
- `Calendar` assumes 4 seasons × 4 weeks. Year 2 has the same calendar, so this is fine. LY3's "ultimate" might add post-Winter content — out of scope but flag if Calendar ever gates on year=1.
- `ChampionService.OfferForWeek` picks from the 5 vanilla themes. If LY2 wants weekly champions over a different theme set, this needs parameterizing.
