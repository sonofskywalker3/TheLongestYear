# Loop/reset cluster — recipe reset + FarmHouse furniture (2026-06-03)

Second spec of the remaining work (foresight shipped first). This covers the two
**log-verifiable** pieces of the loop/reset cluster — done while the user is remote and can't
playtest. The cluster's harder, playtest-dependent pieces are **deferred** to their own specs:

- Day-28 Junimo bedtime cutscene (reset/continue narrative + JP-shop-at-bedtime + the natural
  save point that also kills the save-folder churn).
- Event/cutscene gating (never-replay-seen, per-run unlock gating, hold-until-~Spring-5,
  forced-scenes-non-skippable).

## 1. Recipe reset

**Problem:** `FarmerReset` never cleared `Farmer.cookingRecipes` / `craftingRecipes`;
`GrantBankedRecipes` only *adds* banked entries. So every recipe the player ever learned persisted
across loops, and the cookbook/craftbook (whose whole purpose is banking recipes to keep them
across resets) did nothing — confirmed 2026-06-01 (all crafting recipes retained with an empty
craftbook; Fried Egg kept without banking).

**Fix (data-driven, no hardcoded list):** in `FarmerReset.Apply`, after clearing mail/events:
```
p.cookingRecipes.Clear();
p.craftingRecipes.Clear();
p.LearnDefaultRecipes();          // vanilla public API — re-seeds the new-game defaults
GrantBankedRecipes(p.cookingRecipes, cookbookRecipes);
GrantBankedRecipes(p.craftingRecipes, craftbookRecipes);
```
`Farmer.LearnDefaultRecipes()` (public; Farmer.cs:2366) re-adds exactly the recipes whose unlock
field is `"default"` in `Data/CookingRecipes` / `Data/CraftingRecipes`. Baseline therefore tracks
the game's own data — run 2+ matches a clean run 1 — with no captured snapshot and no hardcoded
set to drift. Banked entries are added on top at value 0 ("learned but never cooked").

**Verification:** `FarmerReset` log line now reports banked + total recipe counts. Glue, so
log-verified on the next deploy; no playtest, no unit test (Core-only is unit-testable).

## 2. FarmHouse furniture on reset

**Problem:** the loop downgrades the house to the starter cabin, but `loadForNewGame` + the
downgrade leave its furniture stale — the **fireplace was missing** after reset (and earlier a
stale bed blocked the door). `setMapForUpgradeLevel(0)` does **not** recreate built-in furniture.

**Decision (user, 2026-06-03):** re-add the **full** default set, not just bed + fireplace — "the
rug and table (with bowl)" included.

**Fix:** in `WorldResetService.PerformReset`, right after `home.resetForPlayerEntry()` (step 14a),
`RestoreFarmHouseFurniture(home)`:
- Cast home to `FarmHouse` (Cabin extends it; no-op otherwise).
- `fh.furniture.Clear()`, then reflect-invoke vanilla's private `FarmHouse.AddStarterFurniture(Farm)`
  with `Game1.getFarm()`. That lays down the complete level-aware starter set for `Game1.whichFarm`
  — bed `2048` at (9,8), fireplace `(F)1792`/`(F)1794` at `getFireplacePoint()` (level 0 → (8,4)),
  plus the table `(F)1120` at (5,4) holding `(F)1364`, rug, and chairs.
- Reflected via `AccessTools.Method` (it's private); wrapped in try/catch — a failure logs a warn
  and never breaks the reset.

This is applied for **any** post-reset house level (the method is level-aware via
`getFireplacePoint()`), so an owned house-keep still gets the correct starter set each loop. Side
effect by design: player-placed farmhouse decor does not persist across loops (consistent with the
roguelite reset; the user wants a fresh default cabin each loop).

**Known caveat:** for non-standard farm types whose starter set adds a world *object* (e.g.
Riverside's FishSmoker via `objects.Add`), a re-invoke could in theory collide on the object key;
the try/catch contains it and all furniture is added before that line. The primary test farm is
Standard (no object additions).

**Verification:** `RestoreFarmHouseFurniture` logs the piece count before/after. Log-verified on
next deploy; no playtest.
