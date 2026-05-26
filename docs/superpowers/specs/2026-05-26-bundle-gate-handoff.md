# Bundle-Gate Refactor — Session Handoff

**Date:** 2026-05-26
**Branch:** `feat/v1-plan-05-ui`
**Status:** Foundation committed (`ac71216`). Migration to live game-side gate is half-done.
**Tests:** 215 passing.

---

## Where we are right now

The gate model has been rewritten in spec (round 7), foundation classes are in place, but the live `RunManager` path is **still using the obsolete `Contract.IsSatisfiedBy` pool-count gate**. The compile is green and tests pass because both models coexist; only the new one's data structures haven't been wired in.

### What is DONE

- `BundleKind` enum (Seasonal / PerItem / Percentage) — `src/TheLongestYear.Core/BundleKind.cs`
- `BundleRequirement` class with three factory methods + `IsSatisfiedAtSeasonEnd` + `IsFullyComplete` + `InPlayItemsFor` — `src/TheLongestYear.Core/BundleRequirement.cs`
- `BundleRequirementTests` (13 cases covering all three gate behaviours + in-play sampling) — `tests/TheLongestYear.Tests/BundleRequirementTests.cs`
- All earlier commits stand: vault gate, Keep Bus Unlocked, Buildings upgrades (Keep Coop chain, start-with-animal), `MetaState.AnimalSpeciesEverOwned`, `UpgradePurchase.MetaRequirementMissing`, JP per-season multipliers, future-expansion notes.

### What is STILL TODO (in order)

0. **Pre-unlock ALL CC rooms** (`src/TheLongestYear/Loop/CommunityCenterUnlock.cs`)
   - Vanilla unlocks rooms sequentially: completing a room's bundles unlocks the
     next. With our mod's 16-week parallel-attack design, every room must be
     donatable on Spring 1.
   - Current `CommunityCenterUnlock` only opens the CC door — extend it to
     pre-discover every inner room's Junimo Note.
   - Investigate in the decompile:
     - `CommunityCenter.areaToRestore` / `areasComplete` (per-area state)
     - mailReceived flags `ccCraftsRoom`, `ccPantry`, `ccFishTank`,
       `ccBoilerRoom`, `ccBulletinAvailable`, `ccVault` (these typically gate
       note visibility)
     - `eventsSeen` entries for the "Junimo wanders in and shows you the note"
       reveal events per area
   - Manual playtest: enter CC on Spring 1, confirm all 6 Junimo notes are
     visible and clickable.
   - **This is a prerequisite for the gate model** — the gate is meaningless if
     the player physically can't reach the bundles.

1. **`BundleCatalogBuilder` rewrite** (mod side, `src/TheLongestYear/Donations/BundleCatalogBuilder.cs`)
   - Currently returns `IReadOnlyList<CcItem>` (still useful for season-resolver / rarity metadata).
   - Add a new return type or sibling builder that produces `IReadOnlyList<BundleRequirement>`.
   - Classify each vanilla bundle by name + slot count + ingredient count:
     - Name matches `(Spring|Summer|Fall|Winter)_(Foraging|Crops)` → KIND 1 Seasonal.
     - X == Y otherwise → KIND 2 PerItem (apply `DefaultItemSeasonPins`).
     - X < Y → KIND 3 Percentage (apply `DefaultBundleQuotas`).
   - Move the per-item pinning data **out** of `GameplayConfig.DefaultSeasonOverrides` (currently used by the dead `ContractGenerator` path) and **into** `GameplayConfig.DefaultItemSeasonPins`. Same dict shape, just used differently.
   - Quotas go in `GameplayConfig.DefaultBundleQuotas` (keyed by bundle name).

2. **`BundleGate` evaluator** — pure function in Core that ANDs every bundle's `IsSatisfiedAtSeasonEnd` + the vault gate.

3. **`RunManager.EvaluateDayEnd` migration**
   - New signature: `EvaluateDayEnd(RunState, IReadOnlyList<BundleRequirement>, bool vaultGateSatisfied)`.
   - Drop the `YearPlan` parameter. `Contract.IsSatisfiedBy` becomes unused.
   - `fullCcDone` semantics → `bundles.All(b => b.IsFullyComplete(donated))` (replaces the old "every catalog item donated" check).

4. **Per-week bonus list**
   - Add `RunState.CurrentWeekBonusItems : List<string>` (cleared on `BeginNewMonth` / `BeginNewRun`).
   - At planning-hub open, sample (up to `BonusListSizeBySeason[season]`) items from the championed theme's bundles' `InPlayItemsFor(currentSeason, isObtainable)`. `isObtainable` lookup uses `CcItem.ObtainableSeasons` (kept on the catalog metadata).
   - Re-rolls every hub-open (per the user's "shuffle every restart" + Sunday-night model).
   - **NOTE:** The user wants the hub on Sunday night, not Monday morning. Day-28 cross-season case is the tricky bit — see TODO comment in `RunController.OnDayStarted`. Quick spike: thread a `seasonOverride` into `PresentOffer` so the offer pool uses next season when on day 28.

5. **`DonationService.OnItemDonated`**
   - Replace `IsChampionedBonusItem` (currently reads `Contract.BonusItemIds`) with `Run.CurrentWeekBonusItems.Contains(itemId)`.
   - The 1.5× `ChampionBonusMultiplier` already wired and tested.

6. **`ContractPickMenu` UI redraw**
   - Two cards (1-of-2 offer) — same shape as today.
   - For each card, show the THEME's bundles, each with:
     - Bundle name + "X of Y" (e.g. "Artisan Bundle (6/12)")
     - Required-by-season progress badge (e.g. "Spring needs 1 more")
     - Bonus items badge (the per-week random sample with the 1.5× tag)
   - The refresh button is dev-only and gets removed before release (user's word).

7. **Strip dead code** (do this LAST, after everything new is wired)
   - `Contract.RequiredItemIds` (pool semantics), `GateRequirement`, `BonusItemIds` — these are now nonsensical.
   - `ContractGenerator` whole class — bundles are catalog-built, not generated.
   - `GameplayConfig.GateRequirementBySeason`, `BonusListSizeBySeason` (the latter moves into the per-week sampler).
   - `GameplayConfig.SeasonOverrides` and `DefaultSeasonOverrides` — rename to `ItemSeasonPins` / `DefaultItemSeasonPins` and use only for KIND 2 bundles.
   - All `Contract`-based tests in `ContractTests.cs`, `ContractGeneratorTests.cs`, `YearPlanTests.cs`, parts of `RunManagerTests.cs`. Keep `YearPlan` as a structure only if it's still needed for the planning hub view-model.

---

## Bundle classification (user-confirmed, with notes)

### KIND 1 — Seasonal (X=Y, gates on its named season)

| Bundle | X=Y | Season |
|---|---|---|
| Spring Foraging | 4 | Spring |
| Summer Foraging | 3 | Summer |
| Fall Foraging | 4 | Fall |
| Winter Foraging | 4 | Winter |
| Spring Crops | 4 | Spring |
| Summer Crops | 4 | Summer |
| Fall Crops | 4 | Fall |

### KIND 2 — PerItem (X=Y, per-item season pin)

| Bundle | Item → Season pin |
|---|---|
| Construction | Wood→Spr · Stone→Spr · Hardwood→Summer |
| Blacksmith's | Copper Bar→Spr · Iron Bar→Sum · Gold Bar→Fall |
| Geologist's | Quartz→Spr · Earth Crystal→Spr · Frozen Tear→Sum · Fire Quartz→Fall |
| River Fish | Sunfish→Spr · Shad→Sum · Tiger Trout→Fall · Catfish→Spr |
| Lake Fish | Largemouth Bass→Spr · Carp→Sum · Bullhead→Fall · Sturgeon→Sum |
| Ocean Fish | Sardine→Spr · Tuna→Sum · Red Snapper→Sum · Tilapia→Fall |
| Night Fishing | Bream→Sum · Walleye→Fall · Eel→Fall |
| Specialty Fish | Pufferfish→Sum · **Ghostfish→Summer** · Sandfish→Fall · Woodskip→Sum |
| Dye | Red Mushroom→Sum · Sea Urchin→Spr · Sunflower→Sum · Duck Feather→Summer · Aquamarine→Sum · Red Cabbage→Sum |
| Field Research | Purple Mushroom→Winter · Nautilus Shell→Winter · Chub→Spr · Frozen Geode→Summer |
| Fodder | Wheat→Sum · Hay→Spr · Apple→Fall |
| Enchanter's | Oak Resin→Sum · Wine→Fall · Rabbit's Foot→Fall · Pomegranate→Fall |

**Ghostfish note**: caught in Mines water levels ~floor 20+. Year-round but Spring access is tight without JP investment. Pin defaults to Summer for a forgiving early run.

### KIND 3 — Percentage (X<Y, cumulative quota by Sp/Su/Fa/Wi day 28)

Quota values are **cumulative checkpoints**, not deltas. Final value = X (= bundle complete).

| Bundle | X / Y | Spring → Summer → Fall → Winter |
|---|---|---|
| Exotic Foraging | 5 / 9 | 1 → 3 → 5 → 5 |
| Quality Crops | 3 / 4 | 1 → 2 → 3 → 3 |
| Animal Bundle | 5 / 6 | 1 → 3 → 5 → 5 |
| Artisan Bundle | 6 / 12 | 1 → 2 → 4 → 6 |
| Crab Pot | 5 / 10 | 1 → 3 → 5 → 5 |
| Adventurer's | 2 / 5 | 0 → 1 → 2 → 2 |

---

## Design rules etched in stone (don't regress)

- **No item ever requires donation before it's reachable.** All KIND 2 pins reflect realistic obtainability. Late items get late pins.
- **KIND 3 player choice is preserved.** The cumulative quota lets the player pick *which* X of Y; we never lock the specifics.
- **Bonus list = items in play THIS week for THIS theme.** Re-roll per planning hub open. Hard items can appear ("hold off on a theme until the roll favors you").
- **`MetaState` survives every reset.** Vault gate is per-run; `keep_bus_unlocked` short-circuits every season.
- **The vanilla CC behaviour is not modified.** Bundles are vanilla; our gate sits on top.

## Things still up to the user (mention in next session)

- Sunday-night hub timing — day-28 cross-season handling needs a clean implementation.
- The 12+ default item pins for fishing bundles were *my guesses*; the user looked at one (Ghostfish) and adjusted on the fly. They may revisit any of them after first playtest. The new code should make these trivially tunable via `GameplayConfig.ItemSeasonPins`.
- The Adventurer's `0/1/2/2` and Crab Pot `1/3/5/5` quotas — user OK'd implicitly by not objecting after I clarified the cumulative reading; explicit confirmation never came.
- Removing the dev-only refresh button on `ContractPickMenu` before release.

## Workflow rules (from user CLAUDE.md + memory)

- Local commits only. Never push without explicit "yes, push".
- Each commit body ends with `Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`.
- `TheLongestYear.Core` has zero game refs. `TheLongestYear` is the mod (Game1/Harmony/IClickableMenu).
- Per-save data committed ONLY in the game's `Saving` event.
- Files < 400 lines; tuning constants in `GameplayConfig`.
- Test command: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
- Build command: `dotnet build src/TheLongestYear/TheLongestYear.csproj` (IOException on deploy-copy = game running, not a real error).
- Decompiled Android source at `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android` for verifying game internals.
