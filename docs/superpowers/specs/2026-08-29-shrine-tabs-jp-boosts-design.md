# Shrine tabs and JP Boosts: temporary "this loop only" buys at the planning shrine

**Date:** 2026-08-29
**Status:** rulings approved in brainstorm (Jeff, 2026-08-28); spec written 2026-08-29 for the same release as the bug pass (Jeff: "it's stupid to release it with just 2 options")
**Supersedes:** TODO "APPROVED FOR SPEC 2026-08-28: shrine tabs + JP Boosts"; extends plan 4 of the obtainable board (Year-Two Seeds, Sneak Peek)

## Problem

Junimo Points are earned inside a loop and spent only at the loop boundary, on permanents. The
two in-loop buys that shipped with the obtainable board (Year-Two Seeds, Sneak Peek) live in a
single flat list on the planning shrine under every permanent category, with no expiry shown,
no place that says what is currently running, and no way to tell an owned permanent from a
buyable one without reading the row colour. Two rows is a teaser, not a system.

## Rulings (Jeff, 2026-08-28), binding

1. The **shrine** is the statue on the farm (`ShrinePreviewMenu`, opened from
   `PlanningShrineService.ShrineActionPatch`). It gets three tabs: **Active** (default,
   read-only), **Boosts** (the in-loop buys), **Plan** (unowned-and-buyable permanents, then a
   collapsed Locked section per category, plus the foresight weather and cart panel). Owned rows
   leave Plan and live on Active. The **JP perk screen** (`JunimoShrineMenu`, the fail-loop
   popup) is unchanged: permanents are still bought there.
2. No JP-earning boosts. No "cart visit" boost.
3. Boosts stack additively with the weekly theme and with each other (two independent rolls, no
   cap). The same effect cannot be re-bought until it expires.
4. Shop discount boost is additive with the permanent chain, same exclusions, price floors at 1g.
5. Weather boosts set tomorrow's weather; refuse the sale on festival days and in Winter.
6. Lucky Day sets tomorrow's `dailyLuck` to +0.1 (the vanilla ceiling; Special Charm still adds).
7. No warnings about buying a long boost near the end of a season or loop.
8. Boost state and expiries live in `RunState`; effects go through a generalized effects
   provider; a reload rolls back JP and boost together (the existing anti-save-scum rule).
9. Skill levels (Crash Course): buyable any time, applied the vanilla way (XP grant, level-up at
   sleep), never spends earned XP, cost `0.2 x keepCost(target) x 3^(n-1)` with `n` the levels
   bought so far this loop across all skills, cap 2 per skill per loop, a bought level can never
   take a skill to 10, bought levels are never keepable (every `skill:` reach check uses earned
   level = current minus bought this loop).
10. The "20% twin" rule is general for earnable progress only (skill levels, elevator floors,
    backpack), never for built things.
11. Naming: no time words in names, one scope per effect, no collisions with perk names.

## Scope of this release

Every roster row from the brainstorm except two, which the spike below rules out for this
release and hands back to Jeff:

- **Night Owl (2am to 6am).** Vanilla clamps the clock at 2600 in
  `Game1.performTenMinuteClockUpdate` (`timeOfDay = Math.Min(timeOfDay, 2600)`, Game1.cs:6599)
  and starts the pass-out from that same value (`if (timeOfDay >= 2600 || player.stamina <= -15f)`,
  Game1.cs:7191). Running to 3000 means patching both, plus everything that assumes the day ends
  at 2600: outdoor lighting and music, NPC schedule end times, the 2500 and 2600 clock cases
  (dismount, stop fishing), and the sleep-penalty maths that keys on 2700
  (`Farmer.cs:3535-3545`). Prior art exists on PC; nothing is verified on Android. **Deferred**
  to a follow-up spec of its own. The Second Wind row stays (it is the no-penalty half and needs
  none of that).
- **Backpack Organizer (+12 slots to 48).** Vanilla's inventory page draws and hit-tests three
  rows; a fourth needs `InventoryMenu` draw and click patches on PC and on Android's separate
  inventory UI. **Deferred** with Night Owl.

Both are listed in "Open questions" so Jeff can pull either back in.

## 1. The model

### 1.1 Catalog

`BoostCatalog` (Core, `Boosts.cs`) becomes the full roster. `BoostId` grows; `BoostDefinition`
gains a duration class and an effect binding:

```csharp
public enum BoostDuration { Instant, Week, Season, Loop }

public sealed record BoostDefinition(
    BoostId Id, long Cost, BoostDuration Duration,
    string NameKey, string DescKey,
    string? ModifierId = null);   // theme-modifier id this boost stacks onto (reuse rows)
```

`Cost` is the opening bid from the brainstorm; Crash Course and Elevator Pass have computed
costs (`Cost = 0`, see 1.5 and 1.6) and the catalog exposes `BoostPricing.CostOf(def, meta, run, live)`.

| Id | Name | JP | Duration | Effect | Implementation |
|---|---|---|---|---|---|
| `RainDance` | Rain Dance | 25 | Instant | tomorrow is Rain | weather override (2.3) |
| `StormCall` | Storm Call | 40 | Instant | tomorrow is Storm | weather override (2.3) |
| `FortunesFavor` | Fortune's Favor | 30 | Instant | tomorrow's daily luck +0.1 | luck override (2.4) |
| `SecondWind` | Second Wind | 20 | Instant | tonight's late sleep costs no stamina, no Exhausted | sleep-penalty patch (2.5) |
| `Overgrowth` | Overgrowth | 50 | Week | +1 forage roll per spawn | reuse `forage_yield_up` |
| `FeedingFrenzy` | Feeding Frenzy | 45 | Week | fish bite faster | reuse `fish_bite_up` |
| `GrowthSpurt` | Growth Spurt | 60 | Week | crops grow a day faster | reuse `crop_growth_up` |
| `RichVeins` | Rich Veins | 55 | Week | extra mine drops | reuse `mine_drops_up` |
| `Windfall` | Windfall | 90 | Week | all drops up | reuse `all_drops_up` |
| `QuickFeet` | Quick Feet | 40 | Week | +1 movement speed | daily buff (2.6) |
| `YearTwoSeeds` | Year-Two Seeds | 75 | Week | Mixed Seeds roll the year-2 crop at 5% | existing `MixedSeedsPatch` |
| `Haggler` | Haggler | 120 | Season | +10% shop discount, additive with the chain | `ShopDiscount` (2.7) |
| `FastFriends` | Fast Friends | 150 | Season | friendship gains x1.5 | `changeFriendship` prefix (2.8) |
| `IronLungs` | Iron Lungs | 90 | Season | +50 max stamina | daily buff (2.6) |
| `SneakPeek` | Sneak Peek | 100 | Season | the Queen of Sauce airs the year-2 episode | existing `QueenOfSaucePatch` |
| `CrashCourse` | Crash Course | computed | Loop | +1 level in a chosen skill | XP grant (1.5, 2.9) |
| `ElevatorPass` | Elevator Pass | computed | Loop | elevator extended to the next multiple of 10 | floor write (1.6, 2.10) |

Rule 11 check: no time words in any name; Haggler, Windfall and Overgrowth do not collide with
perk names (`shop_discount`, `all_drops_up` are ids, not names; the perks are "Shop Discount",
"Green Thumb", "Coal Vein", "Forager's Eye", "Quick Bite", "Weather Sage", "Cart Whisper").

### 1.2 Run state

`RunState` gains one list and two counters; the two plan-4 fields are retired.

```csharp
public sealed class ActiveBoost
{
    public string Id { get; set; }        // BoostId name, e.g. "Overgrowth"
    public int BoughtDay { get; set; }    // day of year 1..112 (Calendar)
    public int ExpiresAfterDay { get; set; }   // last day of year the boost is active, inclusive; 112 for Loop
    public int Skill { get; set; } = -1;  // CrashCourse only: which skill
}
public List<ActiveBoost> ActiveBoosts { get; set; } = new();
public Dictionary<int, int> SkillLevelsBoughtThisLoop { get; set; } = new();  // skill index -> count (cap 2)
public int SkillLevelsBoughtTotal { get; set; }   // n for the 3^(n-1) price
```

Expiry by class, computed at purchase from the current day of year `d` (`Calendar` already
maps season + day to week of year):
- Instant: `ExpiresAfterDay = d + 1` (weather and luck land on `d + 1`; Second Wind lands on the
  night of `d`, `ExpiresAfterDay = d`).
- Week: the last day of the current week of year (weeks are 7-day blocks, day 28 is in week 4).
- Season: day 28 of the current season.
- Loop: 112.

"Active" = `BoughtDay <= today <= ExpiresAfterDay`. Expired entries are pruned on `DayStarted`
(kept in the list until then so the Active tab can show "expires tonight"). `BeginNewRun` clears
the list and both counters. `YearTwoSeedsWeek` and `SneakPeekSeason` are removed from
`RunState`; a save that still carries them (0.16.117 to 0.16.158, unreleased) is migrated once at
load into `ActiveBoosts` entries with the matching expiry. `BoostState.YearTwoSeedsActive` and
`SneakPeekActive` keep their signatures and read the list.

### 1.3 Purchase

`BoostPurchase.StateOf / TryBuy(MetaState, RunState, BoostId, BoostContext ctx)` where
`BoostContext` carries the live facts Core cannot read: day of year, season, whether tomorrow is
a festival, the skill levels, the current elevator floor. Results: `Success`, `NotEnoughJp`,
`AlreadyActive` (an unexpired entry with the same id, or for reuse rows any unexpired entry with
the same `ModifierId`), `NotAvailable` (weather in Winter or before a festival, Year-Two Seeds
in Winter, Crash Course at cap or targeting level 10, Elevator Pass at floor 120 or with no mine
progress). `TryBuy` deducts JP and appends the `ActiveBoost`; it never touches game state. The
mod-side `BoostPurchaseService` applies the immediate part (weather write, XP grant, floor
write) after a `Success`, then plays the purchase sound. Both JP and the entry are committed by
the game's Saving event, as today.

### 1.4 Effect stacks

`ActiveEffectsProvider` keeps the theme pair and gains a boost source:

```csharp
public static void AttachBoosts(Func<IReadOnlyList<(string ModifierId, bool Active)>> source);
public static int BonusStacks(string modifierId);   // (theme bonus == id ? 1 : 0) + active boosts bound to id
public static bool ActiveBonus(string id) => BonusStacks(id) > 0;   // unchanged for callers
```

The five reuse patches (`ForageYieldPatch`, `FishBiteRatePatch`, `CropGrowthPatch`,
`MineDropsPatch`, `AllDropsPatch`) switch from `if (ActiveBonus(id))` to
`for (int i = 0; i < BonusStacks(id); i++)` around their roll (ruling 3: two independent rolls).
Fish bite: the 0.70 factor applies once per stack. Crop growth: one extra growth day per stack.
`TerrainBonusPatches` and `MineDropsPatch`'s second `all_drops_up` read follow the same shape.
Liabilities are untouched.

### 1.5 Crash Course

Price: `BoostPricing.CrashCourse(targetLevel, n) = round(0.2 x SkillLevelCosts[targetLevel] x 3^(n-1))`
with `n = run.SkillLevelsBoughtTotal + 1`. Table check from the ruling: first buy of level 1 = 10;
a second buy at level 2 = 60; a third buy of level 1 in another skill = 90.
Availability per skill: `SkillLevelsBoughtThisLoop[skill] < 2` and `currentLevel + 1 < 10`.
Application: `Farmer.gainExperience(skill, width)` where
`width = getBaseExperienceForLevel(target) - getBaseExperienceForLevel(current)` (the full width
of the target level on top of current XP; at 80/100 buying level 1 lands at 180). Vanilla queues
the level-up for that night's sleep. The entry records `Skill`.

Earned level: `RunReachEvaluator.SkillLevel` returns `p.<skill>Level - run.SkillLevelsBoughtThisLoop[skill]`.
`RunBaselineBuilder` keeps are by owned rows, so a bought level can never be kept; the shrine's
`skill:` reach keeps the row for Keep Level N hidden past the earned level. Verify at build time
(ruling 9, last bullet): after a reset the profession queue must not carry a profession from a
level that was bought and not kept. `FarmerReset` re-sets skill levels from the baseline and
`ProfessionPickerScheduler` requeues from `baseline.SkillLevels`; a bought 5 that is not kept is
reset to the kept level, so its profession is stripped with the level. Recipes learned from a
bought level leaking into the cookbook or craftbook keeps: accepted, do nothing.

### 1.6 Elevator Pass

Relative to the player's current deepest elevator stop: `landing = ((current / 10) + 1) x 10`
where `current = Game1.netWorldState.Value.LowestMineLevel` (35 lands 40, then 50). Price
`round(0.2 x (75 + ((landing - 10) / 10) x 100))`: 15 JP for floor 10, 35 for 20, ... 235 for 120.
Not available when `current >= 120` or `current == 0` (no mine progress yet; the mine must be
opened first). Application: raise `LowestMineLevel` and `LowestMineLevelForOrder` to `landing` (never lower
them; `current` is read through `MineShaft.lowestLevelReached`, the getter the elevator uses).
`Farmer.deepestMineLevel` is NOT touched: the `mine:` reach for the keep rows reads it, and a
bought floor is not an earned one, the same rule as Crash Course levels (review 2026-08-29).
Repeatable within the loop, one step per buy; the entry is Loop-class and purely a record (the
floor is state).

## 2. Effects, one by one

### 2.1 Reuse rows

Overgrowth, Feeding Frenzy, Growth Spurt, Rich Veins, Windfall, Year-Two Seeds: nothing new in
the patches beyond the stack loop (1.4) and the list read (1.2).

### 2.2 Sneak Peek

Reads the list (Season class). `QueenOfSaucePatch` unchanged.

### 2.3 Rain Dance, Storm Call

At purchase (mod side): write the three fields `WeatherScheduleWriterPatch` writes
(`Game1.weatherForTomorrow`, `netWorldState.WeatherForTomorrow`,
`GetWeatherForLocation("Default").WeatherForTomorrow`) with `"Rain"` or `"Storm"`. Record the
override in `RunState.WeatherOverride { Day, Weather }` so the Weather Sage forecast on the Plan
tab shows it and so `WeatherScheduleWriterPatch` does not overwrite it: the postfix runs at the
start of the next day for the day after, so no conflict for tomorrow; the forecast cell for
tomorrow reads the override first. Refusals (`NotAvailable`): tomorrow is a festival day
(`WeatherScheduler` festival tables or `Utility.isFestivalDay(tomorrow)`), or the season is
Winter. Both rows can be bought on the same day only in sequence (the second replaces the
first's weather; the second sale is allowed, the first entry is expired by the purchase).

### 2.4 Fortune's Favor

On `DayStarted` of the target day, `Game1.player.team.sharedDailyLuck.Value = 0.10`. Vanilla
sets luck in `_newDayAfterFade` before `DayStarted`, so the override wins; the TV fortune
teller reads the same field. Special Charm adds on top through `DailyLuck`, as ruled.

### 2.5 Second Wind

Bought on day `d`, applies to the sleep that ends day `d`. Harmony prefix on the Farmer
new-day method that holds the penalty block (`Farmer.cs:3520-3545`: `ClearBuffs`, the
`exhausted` half-stamina rule, then the `timeWentToBed > 2400` deduction): when Second Wind is
active for tonight, set `exhausted.Value = false` and record `timeWentToBed.Value = 0`,
`timeWentToSleep = 2400` before the block runs (both conditions become false and the "keep
yesterday's stamina if lower" rule stays sane). Never a refill after the fact: the block is the
only thing touched.

### 2.6 Quick Feet, Iron Lungs

Vanilla buffs, re-applied on every `DayStarted` while active (vanilla clears buffs at sleep):
`Game1.player.applyBuff(new Buff(id: "tly.quick_feet", duration: Buff.ENDLESS, effects: new BuffEffects { Speed = { 1 } }))`
and `MaxStamina = { 50 }`. Buff ids are stable so re-application replaces rather than stacks.
They show in the vanilla buff bar with the boost name as display name (source
`Strings.Get(NameKey)`). No `maxStamina.Value` edits, so `FarmerReset` and the Stardrop keep are
untouched.

### 2.7 Haggler

`ShopDiscount.Apply(price, tier)` becomes `Apply(price, percent)`; the patch computes
`percent = tier x 5 + (Haggler active ? 10 : 0)`. Same call site, same exclusions (tool upgrades
in the patch; buildings and animals never reach `GetShopStock`; non-gold currencies skipped),
floor 1g.

### 2.8 Fast Friends

Harmony prefix on `Farmer.changeFriendship(int amount, NPC n)`: when active and `amount > 0`,
`amount = (int)Math.Ceiling(amount * 1.5)`. Runs before vanilla's own Book_Friendship 1.1
multiplier, so the two compound; accepted (the book is a permanent the player also paid for).

### 2.9 Crash Course

See 1.5. The Boosts tab row opens a five-button skill picker (Farming, Fishing, Foraging,
Mining, Combat) inline in the row; each button shows the target level and price, greyed at cap
or at level 9.

### 2.10 Elevator Pass

See 1.6. The row shows "Floor N to M" and the price.

## 3. The shrine menu

`ShrinePreviewMenu` gets three horizontal tabs under the title, drawn with the same
`ClickableTextureComponent` idiom as `JunimoShrineMenu`'s tab strip (ids from a new
`TabIdBase = 6200`), gamepad-navigable, active tab white and others at 0.7 alpha. Default tab
Active. The scroll list and the row renderer stay; each tab builds its own row list.

**Active** (read-only): three groups. "Running boosts": one row per active entry, name plus
"expires tonight" / "through <Season> <day>" / "this loop" (Instant entries show "tomorrow").
"This week": the theme's bonus and liability by `ThemeModifiers.DisplayNameFor`, with
"(lifted)" when suppressed. "Owned": every owned permanent leaf by category
(`KeepShopFilter.OwnedLeavesInCategory`), green, no cost. Empty groups say so in one line.

**Boosts**: four headers (Instant, This week, This season, This loop) in that order, rows in
catalog order, the existing Buy / Active / Not now button states, plus the Crash Course skill
picker and the Elevator Pass floor label. Multiplayer: the tab is shown only to the host
(`Context.IsMainPlayer`); farmhands see a one-line "The host buys boosts" note. JP is one pool
per save and every effect above is host-side state.

**Plan**: today's list without owned rows: per category, the buyable rows with cost, then a
collapsed **Locked** row ("Locked (N)") that expands in place to the next unowned tier of every
chain in the category whose reach is not met, each with its requirement in words
(`RunReachRequirement` rendered by a new `ReachText.Describe`, e.g. "needs Farming 3 this loop",
"needs a Stable this loop", "needs floor 40 this loop"). Owned rows are gone from Plan. The
foresight weather and cart panel stays at the top of Plan; the weather cell for tomorrow reads
`RunState.WeatherOverride` first.

## 4. Diagnostics

- `tly_boost <id> [skill]` buys any catalog id (skill for CrashCourse). `tly_boost list` prints
  the roster with the current state per row.
- `tly_activeeffects` prints the theme pair, every `ActiveBoost` with its expiry and the
  stack count per modifier id.
- `tly_boostexpire` forces the `DayStarted` prune (with `tly_setday` for expiry tests).
- The existing bridge sequence covers everything: buy, `debug sleep`, read `tly_activeeffects`.

## 5. Tests (Core)

`BoostsTests` grows: each row's expiry class from a given day of year; `AlreadyActive` on a
second buy and on a reuse-row collision; Winter and festival refusals; Crash Course price table
(10 / 60 / 90 and the cap); Elevator Pass landing floor and price (35 to 40, 40 to 50, 120
unavailable, 0 unavailable); migration of `YearTwoSeedsWeek` / `SneakPeekSeason`; JSON
round-trip; `BeginNewRun` clears. `ActiveEffectsProviderTests`: `BonusStacks` counts theme +
boosts. `ShopDiscountTests`: percent form, 25 + 10, floor 1g. `ReachTextTests`: one line per
metric. `I18nGuardTests` will enforce every new key.

## 6. Open questions for Jeff

1. Night Owl and Backpack Organizer are deferred by the spike above. Pull either back in?
2. Elevator Pass unavailable before the mine is opened (`current == 0`) and above 120. OK?
3. Fast Friends compounds with the Friendship book (x1.65 together). OK, or cap at 1.5?
4. Boosts tab host-only in multiplayer (recommended). OK?

## Out of scope

Night Owl, Backpack Organizer (deferred, above). The JP perk screen. Any JP-earning boost.
The vocabulary rename `JunimoShrineMenu` to `JpPerkMenu` (TODO, separate sweep).
