# Animal Powers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Twelve permanent animal powers (thirteen rows after the Busy Barnyard split, fifteen counting the
Warm Welcome tiers) in a new Animals tab of the upgrade menu, with the existing animal keeps moved there.

**Architecture:** Pure rules (ids, tables, numbers) live in `TheLongestYear.Core/AnimalPowers.cs` and the
catalog rows in `TheLongestYear.Core/UpgradeCatalogAnimals.cs`, both unit tested. Each game effect is its own
Harmony patch class under `src/TheLongestYear/Loop/AnimalPowerPatches*.cs` reading
`UpgradeChecker.HasUpgrade` (null on non-TLY saves, so every patch is dormant there). Non-patch effects
(Morning Rounds at day start, Loyal Pet's `Data/Pets` edit, Horse Flute at the loop start) go through a small
`AnimalPowersService` and `RunBaseline`/`FarmerReset`. A `tly_animalpowers` console command gives the
live-check readouts.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony, xUnit (Core only).

**Spec:** `docs/superpowers/specs/2026-10-09-animal-powers-design.md` (read the "Rulings" section first; it
overrides the body).

## Global Constraints

- PC 1.6 only. Never edit `Data/FarmAnimals` (effort model and Herd Book read it). `Data/Pets` may be edited.
- Host only: every effect checks `Context.IsMainPlayer` / `Game1.IsMasterGame` where it mutates state.
- One patch class per effect; a patch that cannot apply logs once and does nothing.
- New randomness: `Utility.CreateRandom(myID, Game1.stats.DaysPlayed, <salt>)` so a reload does not reroll.
- Every player-facing string in `i18n/default.json`; text through the game-writing skill; no em dashes.
- Prices and ids from the spec table; Busy Barnyard is two rows of 600 JP (`animal_fast_produce_coop`,
  `animal_fast_produce_barn`). Moved keeps keep their ids and prices.
- On master: bump `manifest.json` PATCH per code commit, push after each commit (`git pull --no-rebase` first).
- Busy Barnyard never changes bundle difficulty (Ruling 1).

## Review Focus

1. A Herd Book restore must not get Warm Welcome's floor (it brings its own hearts): flag set around
   `HerdBookService.Restore`; checked live by restoring an animal with low hearts.
2. Busy Barnyard on the early-return path of `dayUpdate` (animal outside with the door open at night) must leave
   `daysSinceLastLay` unchanged: postfix subtracts the prefix offset unless the animal produced (pure test of
   `ProduceDayOffset` plus code review of the prefix/postfix pair).
3. Owned-leaf display: Keep Horse gets two successors (Swift Horse, Horse Flute); `KeepShopFilter.FindSuccessor`
   takes the first, so the Keep Horse leaf hides once Swift Horse is owned. Same shape as Keep Coop today;
   accepted, covered by a filter test.
4. Swift Horse must not add speed during events or while walking: postfix gated on `isRidingHorse()`,
   `!Game1.eventUp` and the control branch; verified live by reading `getMovementSpeed` off and on the horse.
5. Loyal Pet's `Data/Pets` edit must refresh when the power is bought mid-session and on save load
   (invalidate on purchase and on load), and must lower thresholds only (`min`), never raise.

---

### Task 1: Animals tab and the moved keeps

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCategory.cs` (append `Animals`)
- Create: `src/TheLongestYear.Core/UpgradeCatalogAnimals.cs` (`AnimalUpgradeRows.Build()` returning the moved
  rows: keep_pet, early_horse, keep_coop chain, keep_barn chain, keep_silo, start_* rows, herdbook_1..17)
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs` (remove those rows from `Build()`, add
  `entries.AddRange(AnimalUpgradeRows.Build(HerdBookCosts))`)
- Modify: `src/TheLongestYear/i18n/default.json` (`"upgrade-category.animals": "Animals"`)
- Modify: `src/TheLongestYear/UI/JunimoShrineMenu.cs` comment on `TabIdBase` only (layout already loops the enum;
  9 tabs x 64 px from y + 80 end at 648 inside the 700 px panel)
- Test: `tests/TheLongestYear.Tests/AnimalUpgradeRowsTests.cs`; update `UpgradeCatalogTests` (silo test,
  category theory), `AnimalCapacityRuleTests` (herdbook category)

- [ ] Step 1: failing test

```csharp
[Theory]
[InlineData("keep_pet", 50)] [InlineData("early_horse", 450)] [InlineData("keep_coop", 600)]
[InlineData("keep_deluxe_barn", 2000)] [InlineData("keep_silo", 150)] [InlineData("start_chicken", 400)]
[InlineData("start_ostrich", 1500)] [InlineData("herdbook_1", 750)] [InlineData("herdbook_17", 2250)]
public void Moved_keeps_sit_in_Animals_with_unchanged_price(string id, long cost)
{
    UpgradeDefinition def = UpgradeCatalog.TryGet(id)!;
    Assert.Equal(UpgradeCategory.Animals, def.Category);
    Assert.Equal(cost, def.Cost);
}

[Fact]
public void Fish_pond_stays_in_Buildings()
    => Assert.Equal(UpgradeCategory.Buildings, UpgradeCatalog.TryGet(FishPondKeep.UpgradeId)!.Category);
```

- [ ] Step 2: `dotnet test --filter AnimalUpgradeRowsTests` fails (no `Animals`).
- [ ] Step 3: implement (move rows verbatim, same order, category Animals).
- [ ] Step 4: full suite passes (fix the two existing tests that pinned the old categories).
- [ ] Step 5: bump to 0.19.24, commit "Animals tab in the upgrade menu; animal keeps move there", push.

### Task 2: Core rules and the new rows

**Files:**
- Create: `src/TheLongestYear.Core/AnimalPowers.cs`
- Modify: `src/TheLongestYear.Core/UpgradeCatalogAnimals.cs` (new rows, listed first in the tab)
- Modify: `src/TheLongestYear/i18n/default.json` (`upgrade.<id>.name/.desc` for every new id)
- Test: `tests/TheLongestYear.Tests/AnimalPowersTests.cs`

**Interfaces (Produces):**

```csharp
public static class AnimalPowers
{
    public const string LuckyRabbits = "animal_rabbit_feet", FineFeathers = "animal_duck_feathers",
        BusyCoop = "animal_fast_produce_coop", BusyBarn = "animal_fast_produce_barn",
        TruffleNose = "animal_truffle_double", SwiftHorse = "horse_swift", HorseFlute = "horse_flute",
        QuickGrowth = "animal_quick_growth", FastHatch = "animal_fast_hatch",
        WarmWelcomePrefix = "animal_warm_welcome_", MorningRounds = "animal_morning_rounds",
        SnugBarn = "animal_snug_barn", LoyalPet = "pet_loyal", KeepHorse = "early_horse";
    public const double FineFeatherChance = 0.25, TruffleDoubleChance = 0.25, SwiftHorseSpeed = 1.0,
        IncubatorFactor = 0.5, PetGiftChance = 0.4;
    public const int PetGiftFriendship = 600, WarmWelcomeTiers = 3, FriendshipPerHeart = 200;
    public static int? ProduceTarget(string animalType, Func<string, bool> has);          // Duck 1, Rabbit 2, Goat 1, Sheep 1
    public static int ProduceDayOffset(string animalType, int daysToProduce, Func<string, bool> has); // max(0, days - target)
    public static int QuickGrowthNights(int daysToMature);                                 // ceil(d/2), simulated
    public static int WarmWelcomeFloor(Func<string, bool> has);                            // 0/200/400/600
    public static int SnugBarnHappiness(int before, int drain);                            // clamp(before+drain,0,255)
    public static double SwiftHorseBonus(float movementMultiplier, int elapsedMs, bool diagonal);
    public static float GiftChance(float vanilla);                                         // max(vanilla, 0.4)
    public static int GiftThreshold(int vanilla);                                          // min(vanilla, 600)
}
```

New catalog rows (category Animals): `animal_morning_rounds` 600, `animal_warm_welcome_1/2/3` 200/450/800 chain,
`animal_quick_growth` 350, `animal_fast_hatch` 200, `animal_snug_barn` 250, `animal_fast_produce_coop` 600,
`animal_fast_produce_barn` 600, `animal_duck_feathers` 250 (`species:Duck`), `animal_rabbit_feet` 450
(`species:Rabbit`), `animal_truffle_double` 400 (`species:Pig`), `horse_swift` 300 (prereq `early_horse`),
`horse_flute` 350 (prereq `early_horse`), `pet_loyal` 150 (reach `pet:1`).

- [ ] Step 1: failing tests: target table (Duck 1 with coop row only, Goat 1 with barn row only, Sheep 1,
  Rabbit 2, Cow/Chicken/Pig/Dinosaur/Ostrich null), offsets (Sheep 3 -> 2, Rabbit 4 -> 2, never negative,
  0 without the row), QuickGrowthNights (3->2, 4->2, 5->3, 6->3, 7->4, 10->5, 0->0), WarmWelcomeFloor (highest
  tier wins), SnugBarnHappiness clamp, GiftChance/GiftThreshold min/max, every new row's price/gate, every
  new id has name+desc keys in i18n.
- [ ] Step 2: run, fail. Step 3: implement. Step 4: full suite. Step 5: bump 0.19.25, commit, push. (Rows are
  buyable but inert until their tasks land; nothing is released in between.)

### Task 3: tly_animalpowers diagnostic

**Files:** Create `src/TheLongestYear/Debug/AnimalPowersDebugCommand.cs`; register in `ModEntry` (console +
bridge switch). Subcommands: none (owned powers + per-animal type, age, daysToMature, daysSinceLastLay,
effective produce days, friendship, happiness, currentProduce); `produce <type> <n>` (tally `GetProduceID` on a
throwaway animal); `dig <n>` (call `DigUpProduce` on the first pig n times, count truffles and
`TrufflesFound`); `speed` (mount the horse if beside it, print `getMovementSpeed()` and the expected bonus);
`pets` (pet data GiftChance and thresholds from `Data/Pets`).
Commit with Task 4 if small. Version bump with it.

### Task 4: Lucky Rabbits and Fine Feathers (`GetProduceID` postfix)

**Files:** Create `src/TheLongestYear/Loop/AnimalProducePatch.cs`.

```csharp
[HarmonyPatch(typeof(FarmAnimal), nameof(FarmAnimal.GetProduceID))]
internal static class AnimalProducePatch
{
    private static void Postfix(FarmAnimal __instance, Random r, bool deluxe, ref string __result)
    {
        if (deluxe || __result == null || UpgradeChecker.HasUpgrade == null) return;
        string type = __instance.type.Value;
        if (type == "Rabbit" && UpgradeChecker.HasUpgrade(AnimalPowers.LuckyRabbits))
        { string foot = __instance.GetProduceID(r, deluxe: true); if (foot != null) __result = foot; return; }
        if (type == "Duck" && __result == "442" && UpgradeChecker.HasUpgrade(AnimalPowers.FineFeathers)
            && Utility.CreateRandom(__instance.myID.Value, Game1.stats.DaysPlayed, 4421).NextDouble() < AnimalPowers.FineFeatherChance)
            __result = "444";
    }
}
```

Live: `tly_animalpowers produce Rabbit 200` reads all 446 with the row, mostly 440 without; `produce Duck 200`
reads about 25% 444. Bump, commit, push.

### Task 5: Busy Barnyard (prefix + postfix on `dayUpdate`)

`AnimalFastProducePatch`: prefix computes `ProduceDayOffset(type, data.DaysToProduce, has)`, adds it to
`daysSinceLastLay`, stores it in `__state`; postfix subtracts it unless `daysSinceLastLay == 0`. Live: a duck
fed and content produces on consecutive nights (Duck Egg each morning), log line per production at Trace.

### Task 6: Truffle Nose (`DigUpProduce` prefix/postfix)

Prefix records `Game1.stats.TrufflesFound`; postfix, if it rose and the seeded roll (salt timeOfDay) is under
0.25, spawns `produce.getOne()` at the same spot rule and increments `TrufflesFound`. Live: `dig 200`.

### Task 7: Swift Horse (`Farmer.getMovementSpeed` postfix)

Adds `SwiftHorseBonus` when `__instance.IsLocalPlayer && isRidingHorse() && !Game1.eventUp &&
(Game1.CurrentEvent == null || Game1.CurrentEvent.playerControlSequence) && __result > 0` and not strafing a
tool. Live: `tly_animalpowers speed` on the horse, row off and on.

### Task 8: Horse Flute (loop start)

`RunBaseline.GrantHorseFlute` = owns `early_horse` and `horse_flute` (RunBaselineBuilder, unit test);
`FarmerReset` adds `(O)911` unless the backpack holds one. Live: buy, `tly_reset`, inventory shows the flute.

### Task 9: Quick Growth (`dayUpdate` prefix/postfix)

Prefix records age; postfix, when age rose and the animal is still a baby, `growFully(r)` if
`age == DaysToMature - 1` else `age++`. Live: a new chicken is an adult after 2 sleeps.

### Task 10: Fast Hatch (`MachineSpeedPatch`)

Restructure the postfix to multiply factors: theme fast/slow as today, times `IncubatorFactor` when the
machine data `IsIncubator` and the row is owned. Core: `MachineReadyTime.Combined(fastStacks, slow,
incubatorHalf)` tested. Live: put an egg in an incubator, log shows 9000 -> 4500.

### Task 11: Warm Welcome and Morning Rounds

`AnimalArrivalPatch` (postfix on `AnimalHouse.adoptAnimal`): if host, `daysOwned < 0` and not
`HerdBookService.Restoring`, friendship = max(friendship, floor); if Morning Rounds owned, `pet(player,
is_auto_pet: true)`. `AnimalPowersService.OnDayStarted` (host): Morning Rounds pets every
`Game1.getFarm().getAllFarmAnimals()`. Live: `debug animal White Chicken` arrives at 200/400/600; the next morning
the animals show `wasAutoPet`; a Herd Book restore keeps its stored friendship.

### Task 12: Snug Barn (`updatePerTenMinutes` prefix/postfix)

Prefix records happiness; postfix, when owned, `timeOfDay >= 1800`, environment not outdoors, and the Farm is
raining or `WildcardAnimalPatch.WinterOrSnowDay(farm)`: `happiness = SnugBarnHappiness(before, drain)`. Live:
`debug rain`, animals indoors, evening happiness climbs.

### Task 13: Loyal Pet (`Data/Pets` edit)

`AnimalPowersService.OnAssetRequested` edits `Data/Pets` when owned (GiftChance max, thresholds min);
invalidated on purchase and on save load. Live: `tly_animalpowers pets` shows 0.4 and 600.

### Task 14: Docs and wrap-up

CHANGELOG entry in player words, TODO updated, Animals tab screenshot (window parked at x = -12000), final
deploy so `Mods/TheLongestYear` holds the last build, game closed, throwaway farms deleted.
