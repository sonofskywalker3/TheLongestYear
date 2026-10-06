# Randomizer 0.19.0 Part 2 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the Randomizer for 0.19.0 (Jeff, 2026-10-06: "the full randomizer is included in 19.0"): double theme week, wildcard days, random shrine donations, plus headless debug routes so the card multiplier, mystery card and paid rerolls can be verified in game.

**Architecture:** Same as part 1 (`docs/superpowers/plans/2026-10-06-randomizer-0.19.md`): pure Core statics seeded through `RollSeed.Rng(seed, week, salt, theme)` (src/TheLongestYear.Core/RollSeed.cs), weekly settings read through `RunState.RandomizerFor` / `RunController.Randomizer` / `RunController.RandomizerForWeekPeek(week)`, game glue in src/TheLongestYear. Double week adds a SECOND selection slot alongside the existing single one (no list refactor, so the off path is untouched). Wildcard days add a per-day effect channel (`DayEffects`) that the theme-drawback suppression never touches. Shrine goals are a second goal kind on RunState, paid and credited by the farm statue's new Donate tab.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony, xUnit 2.4.1, GMCM.

**Spec:** `docs/superpowers/specs/2026-09-28-randomizer-design.md` sections 6, 7, 8 (and the timing rules at the top).

## Global Constraints

- Branch `randomizer`, worktree `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear-randomizer`. Do NOT change `manifest.json` `Version`.
- Every option OFF by default; with every option off the game behaves exactly as 0.18.145 (no extra rng draws, no new writes to bundle data, weather, farm, or payouts).
- Weekly options take effect from the next weekly offer (read the week's snapshot, never live config, except where a task says Peek).
- All new rolls seed through `RollSeed.Rng(...)` with a distinct salt per rule. Never `new Random(a ^ b ^ salt)` (correlated first draws for related seeds; this shipped as a bug twice in part 1).
- Testable logic lives in Core. Test project references Core only.
- Player-facing strings go through the `game-writing` skill (invoke it with the Skill tool before writing them). No em dashes anywhere.
- i18n: every key in `src/TheLongestYear/i18n/default.json` used by a literal `Strings.Get("...")`; tokens supplied at the call site.
- Commands: tests `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`; build `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo -p:EnableModDeploy=false -p:EnableModZip=false` (WITHOUT those two flags the build copies into Jeff's Mods folder).
- Commit per task, push `origin randomizer`, messages end with the session attribution lines.
- Never add a Harmony patch without a `RunActivation.IsActive` / provider-null guard (dormant on non-TLY saves and at the title).

## Review Focus

1. **Everything off = 0.18.145.** Each task adds a test that its rule returns the vanilla/default answer when its option is off, and its glue returns before any side effect.
2. **Reload mid-week / mid-day.** Double week, wildcard day state and shrine goals all persist on RunState and are re-applied on load (`RunController.OnRunLoaded` effects block ~232-260); provider statics are never the source of truth.
3. **Rewind / new run.** Every new RunState field is cleared in `BeginNewRun` (and in `Select` / `BeginNewMonth` where the field is week- or month-scoped); every new static (DayEffects, ActiveEffectsProvider second entry) is cleared in `FinalizeReset` (RunController ~806) and `DeactivateTly` (ModEntry ~809).
4. **Theme drawback lift vs wildcard bad day.** Lifting the theme drawback must not lift a wildcard bad effect, and a wildcard bad effect must not keep a lifted theme drawback alive.
5. **Shrine donate tab never eats items it does not credit.** An item is only removed from the inventory when a goal is credited in the same call; a full or wrong-item click changes nothing.

---

### Task 1: Debug routes for slot picks and paid rerolls; two log nits

**Files:** `src/TheLongestYear/UI/WeeklyHubMenu.cs` (`ConfirmByName` ~651, `RerollForDebug` ~586), `src/TheLongestYear/ModEntry.cs` (`tly_select`, `tly_reroll` handlers; migration log ~131), `docs/HEADLESS_DRIVING.md`.

- `tly_select <theme> [left|right]`: with a side given, call `ConfirmSelection(theme, LeftSlot|RightSlot)` (the real card path, multiplier and mystery included) after checking the theme is on that side of `_offer`; without a side keep today's behavior (NoSlot, 1x). Log `Selected <theme> (slot N, goal JP xM)`.
- `tly_reroll paid`: run the same code as a reroll-button click (price check, JP spend, `RerollCanChange` gate); log `Reroll paid <cost> JP (JP <before> -> <after>)` or `Reroll refused: <reason>`. `tly_reroll` alone stays free.
- `tly_hubcards`: log each card's slot, theme (or `?` and the real theme at Trace if sealed), drawback id, multiplier.
- Nit: the stash-tile migration log line must only print when the tile migration ran (today any migration flips the shared flag and prints it).
- Nit: when a stored reroll is restored on reopening the hub, log the restored pair, not the seeded one.
- Document the three commands in HEADLESS_DRIVING.md.
- No Core changes, no tests beyond the full suite. Commit: "Debug: pick a card side, paid rerolls and card dump for headless checks".

---

### Task 2: Three new settings

**Files:** `src/TheLongestYear.Core/RandomizerSettings.cs`, `src/TheLongestYear/ModEntry.cs` (GMCM Randomizer section ~2400-2450), `src/TheLongestYear/i18n/default.json` (`gmcm.randomizer.*` ~651), `tests/TheLongestYear.Tests/RandomizerSettingsTests.cs`.

- Add `bool DoubleThemeWeek`, `bool WildcardDays`, `bool RandomShrineDonations` (default false). Extend `Every_option_is_off_by_default` and the snapshot round-trip test.
- Three `AddBoolOption`s with names/tooltips through `game-writing`:
  - Double theme week: once a season, in week 2 or 3, you take both cards: both buffs, both drawbacks, two goal lists; each list lifts its own drawback. Takes effect from next week's offer.
  - Wildcard days: one random day a week gets a one-day twist, good, bad or odd. You see which day at the start of the week and the twist that morning. Takes effect from next week.
  - Random shrine donations: tops the week's goals up with items that have no Community Center slot, donated at the farm statue for JP. Missing them never fails you. Takes effect from next week.
- Commit: "Randomizer: double week, wildcard days and shrine donations settings".

---

### Task 3: Double week, Core

**Files:** Create `src/TheLongestYear.Core/DoubleWeek.cs`; modify `RunState.cs`, `ActiveEffectsProvider.cs`, `CardMultiplier.cs` (no mystery on a double week); tests `DoubleWeekTests.cs`, `ActiveEffectsProviderTests.cs`, `RunStateTests.cs`.

**Interfaces produced:**
- `public static bool DoubleWeek.Is(int seed, int weekOfYear, bool enabled)`: true for exactly one week per season, week-in-month 2 or 3 (`(weekOfYear - 1) % 4 + 1`), chosen by `RollSeed.Rng(seed, seasonIndex, Salt).Next(2) + 2` where `seasonIndex = (weekOfYear - 1) / 4`. Salt `0x0D0B`.
- RunState (all public get/set, persisted): `Theme? SecondSelection`, `List<BonusSlot> SecondWeekBonusSlots = new()`, `string? SecondLiabilityId`, `double SecondGoalMultiplier = 1.0`, `bool SecondLiabilitySuppressedThisWeek`, `bool IsDoubleWeekSelection => SecondSelection.HasValue`, and `void SelectSecond(Theme theme)` (sets SecondSelection, adds to `SelectedThemesThisMonth`, clears SecondWeekBonusSlots, resets the second liability/multiplier/suppressed). `Select`, `BeginNewMonth`, `BeginNewRun` clear every Second* field.
- ActiveEffectsProvider: a second entry `(_bonusId2, _liabilityId2, _liabilitySuppressed2)`; `static void SetSecond(string? bonusId, string? liabilityId)`, `static void SuppressSecondLiability()`, `static string? SecondBonusId`, `SecondLiabilityId`, `SecondLiabilitySuppressed`. `Set` and `Clear` clear the second entry too. `BonusStacks(id)` adds 1 when `_bonusId2 == id`; `ActiveLiability(id)` is true if either entry matches and is not suppressed.
- `CardMultiplier.ForCard` / `IsSealed` take a `bool doubleWeek` (or read it through a new overload) and never seal on a double week.

**Tests (write first):**
- `DoubleWeek.Is` off → false for all 16 weeks; on → exactly one true week per season, always week-in-month 2 or 3, deterministic per seed, both 2 and 3 occur over seeds.
- RunState: `SelectSecond` adds to SelectedThemesThisMonth; `Select`, `BeginNewMonth`, `BeginNewRun` clear Second*; JSON round-trip keeps Second*; `{}` deserializes with SecondSelection null and SecondGoalMultiplier 1.0.
- ActiveEffectsProvider (self-cleaning, `try/finally Clear()`): two bonuses stack; liability of either entry active; suppressing the second leaves the first active and vice versa; `Set` clears the second entry.
- CardMultiplier: no sealed card on a double week even when mystery would roll.

Commit: "Double week: Core rules, second selection on RunState, two effect entries".

---

### Task 4: Double week, game glue

**Files:** `RunController.cs` (SelectByName ~1056, PopulateBonusSlotsForCurrentSelection ~1116, ApplyEmptyPoolLiftIfNeeded ~1131, OnRunLoaded effects ~232-260, forage sweep ~912/1110, PresentOffer ~1461), `RunController.WeekDiscount.cs`, `UI/WeeklyHubMenu.cs`, `UI/MenuLauncher.cs`, `Loop/WeeklyThemeQuestService.cs`, `Donations/DonationService.cs`, `UI/ShrinePreviewMenu.cs` (BuildActiveRows ~263), `Loop/TerrainBonusPatches.cs:53`, `ModEntry.cs` (`tly_activeeffects`, `tly_goals`), i18n.

Rules (spec section 6 plus rulings):
- The hub knows `_double = DoubleWeek.Is(seed, OfferWeek, _rand.DoubleThemeWeek) && _offer.Count == 2 && !_isPreSelectForNextMonth`. On a double week: title line `menu.hub.double-week` (game-writing), clicking either card (or A on either) confirms BOTH via `RunController.SelectBoth(Theme first, Theme second)` with slots 0 and 1. Rerolls still work and reroll the pair. A double offer with fewer than 2 cards is a normal week.
- `SelectBoth`: `Run.Select(first)` then the existing per-card settings (liability, multiplier slot 0), then `Run.SelectSecond(second)` with its own `RandomPairing.LiabilityFor` and `CardMultiplier.ForCard(..., slot 1, ...)`. Populate the first list as today; populate the second with `SampleSlotsForTheme(second, ...)` MINUS any (bundle, ingredient) already in the first list (dedupe rule: the first card owns a shared slot). The week discount applies to both lists, never twice to one line.
- Effects: `ActiveEffectsProvider.Set(first...)` then `SetSecond(second...)`; same on load restore (and `SuppressSecondLiability()` if `SecondLiabilitySuppressedThisWeek`). Forage sweep runs if either liability is `forage_off`.
- `TerrainBonusPatches` line ~53: replace the single `BonusId` routing with `ActiveBonus("all_drops_up")` / `ActiveBonus("forage_yield_up")` so either entry (and boosts) count. Keep behavior identical when only one theme is selected.
- Quests: the second list gets its own quest `tly.weekly.{week}.b` (title with its theme, description with its bonus/drawback/multiplier). Each quest refreshes, pays and lifts only its own list: list B pays `WeeklyQuestBonus(week) * SecondGoalMultiplier` split over its goals, lifts via `SuppressSecondLiability()` + `SecondLiabilitySuppressedThisWeek`. `RemoveExistingWeeklyQuests` (prefix) still removes both. `FindCurrentWeeklyQuest` gets a list parameter.
- Donations: `IsSelectedBonusSlot` returns which list owns the slot; record the deposit into that list and use that list's multiplier.
- Empty pool: each list lifts its own drawback when its own list is empty.
- Shrine Active tab: one Note per selected theme with its own "(lifted)".
- `tly_activeeffects` and `tly_goals` print both entries/lists.
- No mystery card on a double week (Task 3 rule, wire the hub to pass it).

**Tests:** Core already covers the rules; add a Core helper `GoalLists.Dedupe(IReadOnlyList<BonusSlot> first, IReadOnlyList<BonusSlot> second)` with tests (removes shared (bundle, ingredient) pairs from second, keeps order). Full suite + build.

Commit: "Double week: take both cards, two goal lists, each lifts its own drawback".

---

### Task 5: Wildcard days, Core

**Files:** Create `src/TheLongestYear.Core/WildcardSchedule.cs`, `src/TheLongestYear.Core/DayEffects.cs`; modify `RunState.cs`; tests `WildcardScheduleTests.cs`, `DayEffectsTests.cs`.

**Interfaces produced:**
- Twist ids (constants in `WildcardSchedule`): good `double_forage`, `fast_bites`, `extra_growth`, `shop_sale`, `max_luck`; bad `mines_closed_day`, `slow_bites`, `sell_down`, `energy_drain`; odd `snow_day`, `night_event`, `debris_return`, `rockslide`. `IReadOnlyList<string> AllTwists`.
- `public static int WildcardSchedule.DayFor(int seed, int weekOfYear, IReadOnlyCollection<int> blockedDays)`: day-of-month in the week (`weekStart = ((weekOfYear-1)%4)*7+1` .. +6), never a blocked day, never day 28; returns 0 if none is legal. Salt `0x3C1D`.
- `public static IReadOnlyList<int> WildcardSchedule.BlockedDays(int seasonIndex)` = `CartSchedule.BlockedDays(seasonIndex)` plus 28.
- `public static string WildcardSchedule.TwistFor(int seed, int weekOfYear, bool minecartsRepaired)`: uniform over `AllTwists`, excluding `rockslide` when minecarts are not repaired. Salt `0x5E2B`.
- `DayEffects` (static, process-wide like ActiveEffectsProvider): `static void Set(string? twistId)`, `static void Clear()`, `static string? Today`, `static bool Has(string id) => RunActivation.IsActive && _today == id`.
- RunState: `int WildcardWeek = -1`, `int WildcardDay` (0 = none), `string? WildcardTwist` (null until revealed), `int WildcardTwistDay` (the day it was revealed, for reload). Cleared in `BeginNewRun`.

**Tests:** day always inside the week and never blocked, never 28; a week with every day blocked → 0; Spring week 2 (days 8-14) never 13; twists uniform-ish over seeds (each id seen); rockslide never when minecarts unrepaired; deterministic; DayEffects.Has false when inactive or cleared; round-trip of the RunState fields.

Commit: "Wildcard days: Core schedule, twist pool and the day-effect channel".

---

### Task 6: Wildcard days, glue and the good/bad twists

**Files:** `RunController.cs` (DoDayStartSeasonAndHub after the Season/DayOfMonth sync ~917; OnRunLoaded effects block; FinalizeReset), `ModEntry.cs` (DeactivateTly), `Loop/ForageYieldPatch.cs`, `Loop/FishBiteRatePatch.cs`, `Loop/CropGrowthPatch.cs`, `Loop/PassiveBonusPatches.cs` (ShopDiscountPatch), `Loop/AllDropsPatch.cs` (SellPricePatch), `Loop/MineDropsPatch.cs` (MinesClosedPatch, MinesEntranceClosedPatch), new `Loop/EnergyDrainPatch.cs`, `Loop/BoostEffectsService.cs` (luck), `Loop/WeeklyThemeQuestService.cs` or a new `Loop/WildcardQuestService.cs`, `UI/ShrinePreviewMenu.cs` (Active tab), i18n.

Flow:
- Week start (day 1/8/15/22, after the Run date sync, before PresentOffer), when the week's snapshot has `WildcardDays`: set `WildcardWeek`, `WildcardDay = DayFor(...)`, `WildcardTwist = null`. Create/refresh a basic quest `tly.wildcard.{week}` ("Wildcard day: <Season> <day>") that never completes (daysLeft -1) and is removed at the next week start and on rewind.
- Morning of `WildcardDay`: `WildcardTwist = TwistFor(...)`, `DayEffects.Set(twist)`, HUD message (twist text), quest text updated, Active tab Note. Any other morning: `DayEffects.Clear()`. On load: if today is `WildcardDay` and `WildcardTwist` is set, `DayEffects.Set` again (never re-roll).
- Twists (each patch checks `DayEffects.Has(id)` in addition to its existing condition; nothing else changes when the id is absent):
  - `double_forage`: forage pickups give +1 (ForageYieldPatch, guaranteed rather than the 20% roll).
  - `fast_bites`: FishBiteRatePatch multiplies by 0.70 once more. `slow_bites`: by 1.30 once more. Independent of the theme suppression.
  - `extra_growth`: the night after the wildcard day, each watered unripe crop advances one extra tick via `CropGrowthPatch.AdvanceOneTick`. The overnight patch sees the date as the next day, so key it on a RunState flag set on the wildcard day's DayEnding (`WildcardGrowthNight = true`, cleared next morning).
  - `shop_sale`: ShopDiscountPatch adds 25 percent to `percent` (gold prices only, same exclusions as today).
  - `max_luck`: after BoostEffectsService's luck write, set `Game1.player.team.sharedDailyLuck.Value` to the same 0.10 ceiling.
  - `mines_closed_day`: MinesClosedPatch/MinesEntranceClosedPatch also block when `DayEffects.Has("mines_closed_day")`, with its own dialogue line (game-writing; the theme one says "all week").
  - `sell_down`: SellPricePatch multiplies by 0.75 (separate from the halving drawback; both can apply).
  - `energy_drain`: new prefix on the `Farmer.Stamina` setter: when the new value is below the current one and the day effect is active, scale the decrease by 1.5. Never on the night/pass-out restore paths (only when `Game1.timeOfDay` is between 600 and 2600 and the player is not sleeping).
- Clear `DayEffects` in FinalizeReset and DeactivateTly; clear the quest on rewind.
- All new strings through game-writing (13 twist names, the HUD line, the quest line, the Active tab line, the closed-mines dialogue).

Commit: "Wildcard days: weekly day, morning reveal, good and bad twists".

---

### Task 7: Wildcard days, odd twists

**Files:** `Loop/WeatherModificationsPatch.cs` (or a new `Loop/WildcardWeatherPatch.cs`), `Loop/CropGrowthPatch.cs`, new `Loop/WildcardAnimalPatch.cs`, `Loop/FarmEventSuppressionPatch.cs` (or a sibling postfix), new `Loop/DebrisReturn.cs`, new `Loop/RockslidePatch.cs`, `RunController.cs`.

- `snow_day`: at the reveal (morning of the wildcard day) set snow for today: `Game1.netWorldState.Value.GetWeatherForLocation("Default").IsSnowing = true`, `IsRaining = false`, `Game1.isSnowing = true`, `Game1.isRaining = false` (and the location weather the game reads; verify against the decompile `LocationWeather` and `Game1.ApplyWeatherForNewDay`). Day 1 never (vanilla forces Sun) and festivals are already blocked. That night crops do not grow (CropGrowthPatch prefix skips `newDay` growth for every crop on the farm and greenhouse? Ruling: outdoor crops only, the greenhouse is indoors). Animals: a postfix keeps them inside that day (FarmAnimal leave-the-barn check) and applies the vanilla no-heater happiness loss once overnight (reuse the vanilla Winter numbers, FarmAnimal.cs ~1408-1416). Nothing dies.
- `night_event`: on the wildcard day's night, in the `Utility.pickFarmEvent` postfix: if `__result == null` and the night is not suppressed (`SuppressTonight()`), not a day-28 night, set `__result` to one of FairyEvent, WitchEvent, SoundInTheNightEvent(1) meteorite, (3) owl, (0) capsule, chosen by `RollSeed.Rng(seed, week, SaltNight)`; if that event's `setUp()` would cancel, try the next in order; if all cancel, nothing. Key it on a RunState flag set at DayEnding of the wildcard day (the overnight date is already tomorrow).
- `debris_return`: at the reveal, scan the Farm's Paths layer for tiles 19/20/21 and re-add hollow log 602 / boulder 672 / stump 600 (2x2) via `addResourceClumpAndRemoveUnderlyingTerrain` where all four tiles are clear (no building, crop/terrain feature, object, furniture, existing clump, flooring, path). Log the count.
- `rockslide`: postfix on `Mountain.resetSharedState` (and `DayUpdate`) setting the private `landslide` NetBool to true while `DayEffects.Has("rockslide")`, false otherwise only if WE set it (never undo the vanilla/`MountainUnlock` state on other days). The minecart route still works. Only drawn when minecarts are repaired (Task 5).
- Tests: Core helper `DebrisPlacement.FreeFootprint(Func<int,int,bool> isFree, int x, int y)` with tests; the rest is verified in the smoke.

Commit: "Wildcard days: snow, night events, returning debris and the rockslide".

---

### Task 8: Shrine donations, Core

**Files:** Create `src/TheLongestYear.Core/ShrineGoal.cs`, `ShrineGoalSampler.cs`, `ShrineStack.cs`; modify `RunState.cs`; tests `ShrineGoalSamplerTests.cs`, `ShrineStackTests.cs`.

**Interfaces produced:**
- `public sealed class ShrineGoal { string ItemId; int Stack; int ListIndex; bool Deposited; bool Paid; }` (ListIndex 0 = first theme, 1 = double-week second theme).
- RunState: `List<ShrineGoal> CurrentWeekShrineGoals = new()`; cleared in `Select` (ListIndex 0 entries), `SelectSecond` (ListIndex 1 entries), `BeginNewMonth`, `BeginNewRun`.
- `public static int ShrineGoalSampler.TargetFor(DifficultyStep requiredSlots)` = Easy 3, Normal 4, Hard 5, Extreme 6.
- `public static IReadOnlyList<string> ShrineGoalSampler.Pick(int seed, int weekOfYear, Theme theme, IReadOnlyList<string> candidateIds, int count, IReadOnlyList<GoalGroupCap> caps)`: uniform without replacement via `RollSeed.Rng(seed, weekOfYear, Salt, (int)theme)`, honouring group caps, sorted input for determinism. Salt `0x6A1F`.
- `public static bool ShrineGoalSampler.IsAllowed(DifficultyStep itemRarity, bool placed, int goalWeek, int hardWeekOrPacing, bool inSeasonNow, int weekOfYear)`: Easy/Normal: `placed && inSeasonNow && goalWeek <= week`; Hard/Extreme: `placed && hardWeekOrPacing <= week`.
- `public static int ShrineStack.For(double? basis, DifficultyProfile profile, Random rng, Func<int,int> clamps)`: `basis != null ? AskBands.Roll(basis, profile, rng) : StackScaling.ScaleStack(1, profile.StackFactor)`, then `clamps`, result 1..99.

**Tests:** target table; Pick deterministic, distinct, respects caps, returns fewer when the pool is short, empty when count 0; IsAllowed table per step; ShrineStack falls back to scaled 1 without a basis and clamps.

Commit: "Shrine donations: Core goal picking, availability rule and stacks".

---

### Task 9: Shrine donations, rolling, credit and pay

**Files:** `RunController.cs` (PopulateBonusSlotsForCurrentSelection, SelectBoth, ApplyEmptyPoolLiftIfNeeded), `Loop/WeeklyThemeQuestService.cs`, new `Donations/ShrineDonationService.cs`, `ModEntry.cs` (wiring; `tly_shrinegoals`, `tly_shrinedonate <index>` debug commands), i18n.

- Rolling (when the week's snapshot has `RandomShrineDonations`): per selected list, `count = max(0, TargetFor(EffectiveDifficulty.Steps.RequiredSlots) - ccGoals.Count)`. Candidates: `ThemeEffortPools.IdsFor(theme, _enginePools, _effortData.Objects)` minus `pools.ExcludedIds`, minus legendaries (`LegendaryFishRules.IsLegendary`), minus every id with a slot on the live board (parse `Game1.netWorldState.Value.BundleData` with `BundleParsing.Parse`, normalise, also drop items matching a category ref on the board, template `Loop/BundleRelevanceIndex.cs:41-58`), filtered by `IsAllowed(Steps.ItemRarity, model.IsPlaced(id), model.For(id).GoalWeek, model.For(id).HardWeekOrPacing, seasonResolver.SeasonsFor(id).Contains(season), week)`. Do NOT use `RunController.IsObtainableInWeek` or `RarityForItem` for these ids (both are wrong for off-board items). Log ids the model cannot place and skip them. Stack: `ShrineStack.For(QuantityAskPass.Covers(id, model) ? QuantityAskPass.BasisByDeadline(id, currentSeason, model) : null, profile, rng, s => CappedAsks.ClampStack(id, UnstackableAsks.ClampStack(id, OncePerLoopAsks.ClampStack(id, s, profile.OncePerLoopAsksOne))))`, then the theme week discount (`WeeklyGoalDiscount.Stack`) if the week has one.
- Credit and pay (`ShrineDonationService.Donate(int goalIndex, Item item)`): only when the item matches `ItemId`, quality >= 0, `item.Stack >= goal.Stack`, goal not deposited. Remove exactly `goal.Stack` from the item, set `Deposited`. JP = `Jp.PerItem(ItemRarityResolver.Resolve(id, thresholds), week) * stack * SelectionBonusMultiplier * listMultiplier`, then `JpBoostHelper.Apply`. Then the quest refresh for that list. No CC `RecordDonation`.
- Quest: each list's total, done count and paid shares span its CC goals AND its shrine goals; a shrine goal is complete when `Deposited`. The quest checklist shows shrine goals with their own label (game-writing, e.g. "<item> x<stack> (statue)"). `OnThemeSelected` no longer returns early when there are no CC slots but there are shrine goals. The drawback lifts only when every goal of that list (both kinds) is done. Missing shrine goals never fails anything.
- Empty pool lift: only when both the CC list and that list's shrine goals are empty.
- Debug: `tly_shrinegoals` lists goals; `tly_shrinedonate <index>` spawns the needed stack into the inventory if missing and donates it through `ShrineDonationService` (same code path as the tab).

Commit: "Shrine donations: weekly top-up goals, statue credit and pay".

---

### Task 10: Shrine donations, the Donate tab

**Files:** `UI/ShrinePreviewMenu.cs`, `UI/PlanningShrineService.cs`, `ModEntry.cs` (`AttachDonate` hook; `tly_openshrine` usage string), i18n.

- Append `ShrineTab.Donate` (enum value index matters; append at the end). Show the tab only when this week has shrine goals. Check the tab strip still fits next to the restart button (`tly_openshrine donate` logs `overlap=`).
- Content: one row of goal slots (icon via `ItemRegistry.Create(id).drawInMenu`, stack text, a check when deposited) and a vanilla `InventoryMenu(x, y, playerInventory: true, null, highlight, 36, 3)` below. Highlight only items that can fill an undone goal (`ItemId` match, enough stack).
- Click an inventory item: `inventory.getItemAt(x, y)`; if it fills an undone goal, `ShrineDonationService.Donate(goalIndex, item)` (which removes the stack), play `"newArtifact"`, rebuild rows; otherwise `"cancel"` and nothing changes. No item ever held on the cursor. Gamepad: the inventory slots are snap targets.
- Hover on a goal: item name and "x<stack>"; hover on an inventory item: vanilla tooltip.
- Strings (tab label, empty text, hover) through game-writing.

Commit: "Shrine donations: Donate tab on the farm statue".

---

### Task 11: Headless smoke, docs, release prep

- Smoke per `docs/HEADLESS_DRIVING.md` (Claude's launch, game minimized, throwaway Rodger save, back up and restore Jeff's config.json, redeploy the story build afterwards is the controller's job). Verify: card multiplier and mystery via `tly_select <theme> left|right` and `tly_hubcards`; paid rerolls via `tly_reroll paid` (50 then 100, refused when broke or nothing to change); double week (both cards taken, two quests, each lifts its own drawback, JP per list); each wildcard twist forced through a debug override `tly_wildcard <twistId>` (add it in this task if Task 6 did not: sets today's twist for testing); shrine goals (`tly_shrinegoals`, `tly_shrinedonate`, JP paid, drawback lifts only when both kinds are done); everything off unchanged.
- Docs: README + Nexus description content-identical (house style): collapse the 0.18.x What's New stack into one short paragraph and move detail to CHANGELOG.md; "What's New in 0.19.0: the Randomizer" with all ten options, all off by default; credit Nijah's thread. No em dashes. Do NOT bump the manifest here.
- Commit + push. Merge to master, setting 0.19.0 and publishing are Jeff's call.
