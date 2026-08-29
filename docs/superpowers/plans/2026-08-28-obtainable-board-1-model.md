# Obtainable Board, Plan 1 of 5: the two-week model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Every item carries a pacing week and a hard week; gates and goals read the right one for the difficulty; the availability tables and rules match the game data Jeff ruled on; rule E uses absolute effort bands; goals stop looking ahead of the gate; the old pin table is gone.

**Architecture:** `ItemEffort` and `ItemAvailability` (Core) gain `HardWeek`; `ItemAvailabilityModel` is built with a `WeekMode` and answers `Week` (gate) and `GoalWeek` from it. Rules in `Core/Availability` are corrected in place; new judgement numbers stay in `AvailabilityWeeks`. `EffortTiers`, `SeasonNeed`, `SelectionService`, `BonusItemSampler` change their arithmetic. The glue (`ModEntry`, `GameEffortData`) loads two more tables (`Data/WildTrees` tap items, recipe prices from `Data/Shops`) and picks the mode from the difficulty step.

**Tech Stack:** C# / .NET 6, xunit tests in `tests/TheLongestYear.Tests`, SMAPI mod in `src/TheLongestYear`, pure core in `src/TheLongestYear.Core`. Run tests with `dotnet test tests/TheLongestYear.Tests` from the repo root. Build the mod with `dotnet build src/TheLongestYear -c Release` (do not deploy in this plan).

**Spec:** `docs/superpowers/specs/2026-08-28-obtainable-board-design.md` (sections 1, 4, 5, 6, 7 and Easy).

## Global Constraints

- No em dashes anywhere (code comments, strings, docs, commit messages). Use commas, colons or hyphens.
- Every commit bumps `src/TheLongestYear/manifest.json` `Version` by a patch step (current 0.16.84; this plan starts at 0.16.85). Stage only the files of the task.
- Commit locally only; never push.
- Weeks are 1 to 16 (`Calendar.WeekOfYear`); Spring 1 to 4, Summer 5 to 8, Fall 9 to 12, Winter 13 to 16. `AvailabilityWeeks.SeasonOf`, `FirstWeekOf`, `LastWeekOf` are the only converters.
- A board must regenerate byte for byte from its seed at save load, so nothing in Core may read live game state; the difficulty mode is passed in at model build.
- Every `except`-style fallback must log, never swallow silently (SMAPI `Monitor.Log`).
- `AvailabilityWeeks` holds every judgement number; rules hold facts. A table row that is a judgement carries "(for Jeff to confirm)" in its note only while unconfirmed; the rows in this plan are confirmed.

---

## File map

Create:
- `src/TheLongestYear.Core/WeekMode.cs` (enum: Pacing, HardGates, HardAll)
- `src/TheLongestYear.Core/Availability/UnlockWeeks.cs` (hearts and cost tables, villager first weeks)
- `src/TheLongestYear.Core/Availability/TapperAvailability.cs` (tap items from Data/WildTrees)
- `src/TheLongestYear.Core/Availability/FishingTrashAvailability.cs` (trash ids 167 to 172)
- `tests/TheLongestYear.Tests/WeekModeTests.cs`, `UnlockWeeksTests.cs`, `TapperAvailabilityTests.cs`, `FishingTrashTests.cs`, `RecipeTimingTests.cs`

Modify:
- `src/TheLongestYear.Core/ItemAvailability.cs` (records and model: HardWeek, WeekMode, GoalWeek, Phase 2 override rejection)
- `src/TheLongestYear.Core/AvailabilityWeeks.cs` (tables)
- `src/TheLongestYear.Core/Availability/MineAreas.cs`, `MineralNodeAvailability.cs`, `MetalsAvailability.cs`, `MonsterDropAvailability.cs`, `LocationGating.cs`, `CropForageAvailability.cs`, `ArtisanAvailability.cs`, `CookedDishAvailability.cs`, `EffortComposer.cs`, `ItemAvailabilityBuilder.cs`, `EffortData.cs`, `ItemQueryIds.cs`, `FishAvailability.cs`
- `src/TheLongestYear.Core/EffortTiers.cs`, `SeasonNeed.cs`, `SelectionService.cs`, `BonusItemSampler.cs`, `GoalObtainability.cs`, `GameplayConfig.cs` (pins), `ItemPoolBuilder.cs` (book pool filter, excluded ids)
- `src/TheLongestYear/Loop/GameEffortData.cs` (WildTrees, recipe prices, cooking channel), `src/TheLongestYear/ModEntry.cs` (model build with mode, dump columns)
- Tests named per task.

---

### Task 1: WeekMode and HardWeek on the records

**Files:**
- Create: `src/TheLongestYear.Core/WeekMode.cs`
- Modify: `src/TheLongestYear.Core/ItemAvailability.cs`
- Test: `tests/TheLongestYear.Tests/WeekModeTests.cs`

**Interfaces:**
- Produces: `enum WeekMode { Pacing, HardGates, HardAll }`; `ItemEffort(int Effort, string Basis, int? EarliestWeek = null, Season? GateSeason = null, int? HardWeek = null)`; `ItemAvailability(..., int EarliestWeek = 0, Season? GateSeason = null, int HardWeek = 0)` with `PacingWeek`, `HardWeek` (falls back to PacingWeek when 0), `Week` (gate week for the model's mode), `GoalWeek`; `ItemAvailabilityModel(..., WeekMode mode = WeekMode.Pacing)` and `ItemAvailabilityModel.Mode`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WeekModeTests
{
    private static ItemAvailabilityModel Model(WeekMode mode)
    {
        var derived = new Dictionary<string, ItemAvailability>
        {
            // Desert forage: pacing week 9 (Fall), hard week 6 (Summer).
            ["(O)90"] = new ItemAvailability(Season.Fall, 3, "cactus", EffortSource.Derived, 9, Season.Fall, HardWeek: 6),
        };
        return new ItemAvailabilityModel(derived, mode: mode);
    }

    [Fact]
    public void Pacing_mode_reads_the_pacing_week_for_gates_and_goals()
    {
        ItemAvailability a = Model(WeekMode.Pacing).For("(O)90");
        Assert.Equal(9, a.Week);
        Assert.Equal(9, a.GoalWeek);
        Assert.Equal(Season.Fall, a.Gate);
        Assert.Equal(6, a.HardWeek);
    }

    [Fact]
    public void HardGates_mode_moves_the_gate_but_not_the_goal()
    {
        ItemAvailability a = Model(WeekMode.HardGates).For("(O)90");
        Assert.Equal(6, a.Week);
        Assert.Equal(Season.Summer, a.Gate);
        Assert.Equal(9, a.GoalWeek);
    }

    [Fact]
    public void HardAll_mode_moves_both()
    {
        ItemAvailability a = Model(WeekMode.HardAll).For("(O)90");
        Assert.Equal(6, a.Week);
        Assert.Equal(6, a.GoalWeek);
    }

    [Fact]
    public void Hard_week_defaults_to_the_pacing_week()
    {
        var a = new ItemAvailability(Season.Spring, 1, "quartz", EffortSource.Derived, 1, Season.Spring);
        Assert.Equal(1, a.HardWeek);
    }
}
```

- [ ] **Step 2: Run the tests, expect a compile failure** (`WeekMode`, `HardWeek`, `GoalWeek` do not exist)

Run: `dotnet test tests/TheLongestYear.Tests --filter WeekModeTests`

- [ ] **Step 3: Implement**

`src/TheLongestYear.Core/WeekMode.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Which week the availability model answers with (spec 2026-08-28-obtainable-board,
/// section 1). Pacing: Easy and Normal, gates and goals use the pacing week. HardGates: Hard,
/// gates use the hard week, cards stay on pacing. HardAll: Extreme, both use the hard week.</summary>
public enum WeekMode { Pacing, HardGates, HardAll }
```

In `ItemAvailability.cs`, change the record and add the properties:

```csharp
public sealed record ItemAvailability(
    Season EarliestSeason, int Effort, string Basis, EffortSource Source = EffortSource.Derived,
    int EarliestWeek = 0, Season? GateSeason = null, int HardWeek = 0, WeekMode Mode = WeekMode.Pacing)
{
    /// <summary>Pacing week: the week a normal player reasonably has the item.</summary>
    public int PacingWeek => EarliestWeek > 0 ? EarliestWeek : AvailabilityWeeks.FirstWeekOf(EarliestSeason);
    /// <summary>Hard week: the first week the item can exist at all (facts). Falls back to pacing.</summary>
    public int HardWeekOrPacing => HardWeek > 0 ? HardWeek : PacingWeek;
    /// <summary>The week a day-28 gate reads, by mode.</summary>
    public int Week => Mode == WeekMode.Pacing ? PacingWeek : HardWeekOrPacing;
    /// <summary>The week a weekly card reads, by mode.</summary>
    public int GoalWeek => Mode == WeekMode.HardAll ? HardWeekOrPacing : PacingWeek;
    /// <summary>Season a day-28 gate may first demand the item, by mode.</summary>
    public Season Gate => Mode == WeekMode.Pacing ? (GateSeason ?? EarliestSeason) : AvailabilityWeeks.SeasonOf(Week);
}
```

Rename the test's `a.HardWeek` expectations to `a.HardWeekOrPacing` where the record's raw `HardWeek` is 0 (the fourth test), keep `HardWeek` for the explicit one. `ItemEffort` gains `int? HardWeek = null` as its fifth positional parameter.

`ItemAvailabilityModel`: add a constructor parameter `WeekMode mode = WeekMode.Pacing`, store it in `public WeekMode Mode { get; }`, and in `For()` build the returned record with `HardWeek: derived?.HardWeek ?? effortOnly?.HardWeek ?? 0` and `Mode: Mode`. The unrecognised early return also passes `Mode: Mode` (HardWeek 0 keeps it at the unknown week in every mode).

- [ ] **Step 4: Run all tests, expect green**

Run: `dotnet test tests/TheLongestYear.Tests`

- [ ] **Step 5: Commit**

Bump manifest to 0.16.85.

```bash
git add src/TheLongestYear.Core/WeekMode.cs src/TheLongestYear.Core/ItemAvailability.cs tests/TheLongestYear.Tests/WeekModeTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.16.85: WeekMode and HardWeek on the availability records"
```

---

### Task 2: Mines at 30 floors a week, all Spring-gated; Desert hard week 6

**Files:**
- Modify: `src/TheLongestYear.Core/AvailabilityWeeks.cs` (`MineAreaWeek`, `MineAreaGateSeason`, add `MineFloorWeek`, `DesertHardWeek`), `src/TheLongestYear.Core/Availability/LocationGating.cs` (add `HardWeekFor`), `MineralNodeAvailability.cs`, `MetalsAvailability.cs`, `GeodeAvailability.cs`, `MonsterDropAvailability.cs`, `FishAvailability.cs`, `ArtifactAvailability.cs`, `CropForageAvailability.cs` (pass hard week through)
- Test: `tests/TheLongestYear.Tests/AvailabilityWeeksTests.cs` (extend)

**Interfaces:**
- Produces: `AvailabilityWeeks.MineFloorWeek(int floor)`; `AvailabilityWeeks.DesertHardWeek = 6`; `LocationGating.HardWeekFor(string key)` and `HardWeekForAny(IReadOnlyList<string>)`; `MineAreas.GateSeason` returns Spring for every mine area and Fall for the Skull Cavern.

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData(1, 1)] [InlineData(30, 1)] [InlineData(31, 2)] [InlineData(60, 2)]
[InlineData(61, 3)] [InlineData(90, 3)] [InlineData(91, 4)] [InlineData(120, 4)]
public void Thirty_floors_a_week(int floor, int week) => Assert.Equal(week, AvailabilityWeeks.MineFloorWeek(floor));

[Fact]
public void Every_mine_area_gates_in_spring_and_skull_cavern_in_fall()
{
    Assert.Equal(Season.Spring, MineAreas.GateSeason(MineAreas.Area80));
    Assert.Equal(3, MineAreas.Week(MineAreas.Area80));
    Assert.Equal(Season.Fall, MineAreas.GateSeason(MineAreas.SkullCavern));
}

[Fact]
public void Desert_has_a_fall_pacing_week_and_a_summer_hard_week()
{
    Assert.Equal(9, LocationGating.WeekFor("Desert"));
    Assert.Equal(6, LocationGating.HardWeekFor("Desert"));
    Assert.Equal(6, LocationGating.HardWeekFor("SkullCave"));
    Assert.Equal(1, LocationGating.HardWeekFor("Town"));
}

[Fact]
public void Gold_ore_is_a_spring_gate_at_week_3()
{
    var gold = MetalsAvailability.Derive(new PoolItem("(O)384", 25, 3, new List<Season>(), new List<string>()))!;
    Assert.Equal(3, gold.Week);
    Assert.Equal(Season.Spring, gold.Gate);
}
```

(`PoolItem`'s constructor: check `ItemPoolModel.cs` for the positional order and use it; the test above assumes `(ItemId, Price, Weight, Seasons, Locations)`.)

- [ ] **Step 2: Run, expect failures** (`MineFloorWeek`, `HardWeekFor` missing; Area80 gate is Summer)

- [ ] **Step 3: Implement**

`AvailabilityWeeks.cs`:

```csharp
public const int DesertHardWeek = 6;   // Jeff: a Spring bus is possible but not fun; Hard may ask from Summer week 2

/// <summary>30 floors a week (Jeff): floor 1 to 30 week 1, 31 to 60 week 2, and so on.</summary>
public static int MineFloorWeek(int floor) => Math.Max(1, (Math.Max(1, floor) - 1) / MineFloorsPerWeek + 1);

public static int MineAreaWeek(int area) => area switch
{
    MineAreas.Area0 or MineAreas.Area10 => MineFloorWeek(1),
    MineAreas.Area40 => MineFloorWeek(41),
    MineAreas.Area80 => MineFloorWeek(81),
    _ => SkullCavernWeek,
};

public static Season MineAreaGateSeason(int area) => area == MineAreas.SkullCavern ? Season.Fall : Season.Spring;

/// <summary>Hard week for a mine area: the same floors, Skull Cavern at the Desert hard week.</summary>
public static int MineAreaHardWeek(int area) => area == MineAreas.SkullCavern ? DesertHardWeek : MineAreaWeek(area);
```

`MineAreas.cs`: add `public static int HardWeek(int area) => AvailabilityWeeks.MineAreaHardWeek(area);`.

`LocationGating.cs`: give `GatedMarkers` a third tuple field `Hard`: Desert and SkullCave `(9, 6)`, UndergroundMine `(1, 1)`, Sewer and BugLand `(SewerWeek, SewerWeek)`, WitchSwamp and WitchHut `(SwampWeek, SwampWeek)`. Add:

```csharp
public static int HardWeekFor(string locationKey)
{
    if (string.IsNullOrEmpty(locationKey)) return 1;
    foreach ((string marker, int _, int hard) in GatedMarkers)
        if (locationKey.Contains(marker, StringComparison.Ordinal)) return hard;
    return 1;
}

public static int HardWeekForAny(IReadOnlyList<string> locationKeys)
{
    if (locationKeys == null || locationKeys.Count == 0) return 1;
    int best = Calendar.WeeksPerYear;
    foreach (string key in locationKeys) best = Math.Min(best, HardWeekFor(key));
    return best;
}
```

Then thread the hard week through every rule that builds an `ItemEffort` or `ItemAvailability` from a mine area or a location:
- `MineralNodeAvailability`, `GeodeAvailability`, `MonsterDropAvailability`: pass `HardWeek: MineAreas.HardWeek(area)`.
- `MetalsAvailability.Derive`: `new ItemAvailability(AvailabilityWeeks.SeasonOf(week), rule.Effort, basis, EffortSource.Derived, week, gate, HardWeek: MineAreas.HardWeek(rule.Area))`.
- `FishAvailability.Derive`: compute `int hardWeek = Math.Max(spawnWeek, LocationGating.HardWeekForAny(item.Locations));` (mine fish rows: `Math.Max(hardWeek, mineFish.Week)`) and pass `HardWeek: hardWeek`.
- `ArtifactAvailability`, `CropForageAvailability.DeriveForage`: `HardWeek: Math.Max(AvailabilityWeeks.ArtifactWeek, LocationGating.HardWeekFor(spot.Location))` and the forage equivalent using `s.Season` first week and `LocationGating.HardWeekFor`.

Update `ShopAvailability` Savage Ring row to `Season.Spring` (area 80 is Spring now) and the `ShopAvailabilityTests` `Savage_ring_gates_in_summer...` test to expect Spring. Update every existing test that expected a Summer gate for area 80 (grep tests for `Season.Summer` next to Fire Quartz, Gold, Lava Eel, Bone Fragment) to Spring, and `MineFishWeeks` Lava Eel and Cave Jelly to `(4, Season.Spring)`.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.86)

```bash
git add src/TheLongestYear.Core tests/TheLongestYear.Tests src/TheLongestYear/manifest.json
git commit -m "v0.16.86: mines at 30 floors a week, every mine item Spring-gated; Desert hard week 6"
```

---

### Task 3: Crop arithmetic, seed-source weeks, and the crop and forage table rows

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/CropForageAvailability.cs:467`, `src/TheLongestYear.Core/AvailabilityWeeks.cs` (`FestivalCropWeeks` becomes `SeedSourceWeeks`; `LateFloors`; `OtherPlacements`; `FruitTreeFruitWeeks`), `src/TheLongestYear.Core/Availability/LocationGating.cs` (Woods marker)
- Test: `tests/TheLongestYear.Tests/CropForageWeekTests.cs`, `AvailabilityWeeksTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void A_seven_day_crop_planted_day_1_is_harvested_in_week_2()
{
    var crops = new List<RawCropGrowth>
    {
        new("(O)597", 7, false, false, new[] { Season.Spring }),   // Blue Jazz
        new("(O)270", 14, true, false, new[] { Season.Summer }),   // Corn
        new("(O)24", 4, false, false, new[] { Season.Spring }),    // Parsnip
    };
    Assert.Equal(2, CropForageAvailability.DeriveCrop("(O)597", crops)!.EarliestWeek);
    Assert.Equal(7, CropForageAvailability.DeriveCrop("(O)270", crops)!.EarliestWeek);
    Assert.Equal(1, CropForageAvailability.DeriveCrop("(O)24", crops)!.EarliestWeek);
}

[Theory]
[InlineData("(O)433", 5)]   // Coffee Bean, Dust Sprite seed then 10 days
[InlineData("(O)400", 3)]   // Strawberry, Egg Festival
[InlineData("(O)417", 12)]  // Sweet Gem Berry
[InlineData("(O)284", 10)]  // Beet, Oasis week 9 plus 6 days
[InlineData("(O)252", 11)]  // Rhubarb, Oasis seeds in a garden pot
[InlineData("(O)268", 11)]  // Starfruit
public void Seed_source_weeks(string id, int week) => Assert.Equal(week, AvailabilityWeeks.SeedSourceWeeks[id]);

[Fact]
public void Cactus_fruit_is_desert_forage_not_an_oasis_crop()
    => Assert.False(AvailabilityWeeks.LateFloors.ContainsKey("(O)90"));

[Theory]
[InlineData("(O)746", 12)]   // Jack-O-Lantern, Spirit's Eve Fall 27
[InlineData("(O)373", 12)]   // Golden Pumpkin, the maze
[InlineData("(O)634", 13)]   // Apricot
public void Table_rows(string id, int week) => Assert.Equal(week, ShopAvailability.Derive(id)!.EarliestWeek);

[Fact]
public void Secret_woods_is_week_4() => Assert.Equal(4, LocationGating.WeekFor("Woods"));
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`CropForageAvailability.DeriveCrop`: replace the growth-week line with

```csharp
int growWeeks = crop.GrowthDays / Calendar.DaysPerWeek;   // planted day 1, harvest day 1 + days
week = Math.Min(AvailabilityWeeks.FirstWeekOf(first) + growWeeks, AvailabilityWeeks.LastWeekOf(first));
if (AvailabilityWeeks.SeedSourceWeeks.TryGetValue(qualifiedId, out int seedWeek))
    week = Math.Max(week.Value, seedWeek);
```

`AvailabilityWeeks.cs`: rename `FestivalCropWeeks` to `SeedSourceWeeks` with rows `(O)400 = 3`, `(O)417 = 12`, `(O)433 = 5`, `(O)284 = 10`, `(O)252 = 11`, `(O)268 = 11`; each with a comment naming the route (Egg Festival; Rare Seed from the cart plus 24 days; Dust Sprite seed plus 10 days; Oasis week 9 plus 6 days; Oasis seeds in a garden pot, Garden Pot recipe keep). Remove `(O)90`, `(O)284`, `(O)252`, `(O)268` from `LateFloors` (Winter Root and Snow Yam stay). Change `OtherPlacements` Jack-O-Lantern to 12 and add `["(O)373"] = (12, "Golden Pumpkin, Spirit's Eve maze")`. Remove "(for Jeff to confirm)" from the confirmed rows (Cave Carrot, Moss, Tea Leaves, Jack-O-Lantern, Oil of Garlic, Pickles). `FruitTreeFruitWeeks`: keep Apricot and Cherry 13, remove Banana and Mango (they join the excluded ids in Task 12). Add `("Woods", 4, 4)` to `LocationGating.GatedMarkers` with a comment: Secret Woods needs the Steel Axe (Morel, Fiddlehead, Woodskip, hardwood stumps).

Update `CropForageWeekTests.Melon_is_summer_week_6...` if the new arithmetic changes an expectation (Melon 12 days: 5 + 1 = 6, unchanged; Pumpkin 13 days: 9 + 1 = 10, unchanged; Cauliflower 12: week 2, unchanged).

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.87)

```bash
git commit -m "v0.16.87: crop week is start plus days/7; seed-source weeks; Cactus Fruit is Desert forage; Woods marker week 4"
```

---

### Task 4: Fishing trash rule and the Phase 1 short-circuit (Clam)

**Files:**
- Create: `src/TheLongestYear.Core/Availability/FishingTrashAvailability.cs`
- Modify: `src/TheLongestYear.Core/Availability/EffortComposer.cs` (rule list; `WeekOf` takes the earlier of Phase 1 and Phase 2), `ItemAvailabilityBuilder.cs`
- Test: `tests/TheLongestYear.Tests/FishingTrashTests.cs`

**Interfaces:**
- Produces: `FishingTrashAvailability.Derive(string id)` returns `ItemEffort(2, "fishing trash, any water from day 1, week 1, effort 2", 1, Season.Spring, 1)` for `(O)167` to `(O)172`, else null.

- [ ] **Step 1: Write the failing tests**

```csharp
public class FishingTrashTests
{
    [Theory]
    [InlineData("(O)168")] [InlineData("(O)169")] [InlineData("(O)170")] [InlineData("(O)171")] [InlineData("(O)172")] [InlineData("(O)167")]
    public void Trash_is_week_1(string id) => Assert.Equal(1, FishingTrashAvailability.Derive(id)!.EarliestWeek);

    [Fact]
    public void Trash_beats_the_fish_pond_route()
    {
        var data = new EffortData
        {
            Objects = new Dictionary<string, RawObjectEntry> { ["718"] = new RawObjectEntry("Fish", -4, 30, false, new string[0], "Cockle") },
            FishPonds = new List<RawFishPondRule>
            {
                new(new[] { "item_cockle" }, new[] { new RawFishPondProduct("(O)168", 0) }),
            },
        };
        var derived = new Dictionary<string, ItemAvailability>
        {
            ["(O)718"] = new ItemAvailability(Season.Spring, 0, "cockle", EffortSource.Derived, 1, Season.Spring),
        };
        var composer = new EffortComposer(data, derived, hasKitchen: false);
        Assert.Equal(1, composer.Derive("(O)168")!.EarliestWeek);
    }

    [Fact]
    public void A_forage_route_beats_a_trap_row_for_clam()
    {
        var pools = new ItemPools { TrapFishIds = new HashSet<string> { "(O)372" } };
        var data = new EffortData
        {
            ForageSpawns = new List<RawSpawnEntry> { new RawSpawnEntry("(O)372", "Beach", null) },
        };
        ItemAvailabilityModel model = ItemAvailabilityBuilder.Build(pools, effortData: data);
        Assert.Equal(1, model.For("(O)372").Week);
    }
}
```

(Check `RawSpawnEntry`'s constructor in `ItemPoolModel.cs` and `ItemPools`'s settable members before running; adjust the test's construction to match the real shapes.)

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

```csharp
namespace TheLongestYear.Core.Availability;

/// <summary>Trash (167 to 172) comes off the line from day 1 in any water, including the mine
/// floors (MineShaft.getFish falls through to Random.Next(167, 173)). Nothing in Data/Locations
/// says so, which let the fish-pond route place it at week 5 (review 2026-08-28).</summary>
public static class FishingTrashAvailability
{
    private const int FirstTrashId = 167;
    private const int LastTrashId = 172;
    private const int TrashEffort = 2;

    public static ItemEffort? Derive(string qualifiedId)
    {
        string bare = BundleParsing.StripQualifier(qualifiedId ?? "");
        if (!int.TryParse(bare, out int id) || id < FirstTrashId || id > LastTrashId) return null;
        return new ItemEffort(TrashEffort, $"fishing trash, any water from day 1, week 1, effort {TrashEffort}", 1, Season.Spring, 1);
    }
}
```

Add `FishingTrashAvailability.Derive(qualifiedId)` as the first entry of the rule array in `EffortComposer.Derive`.

For Clam: in `ItemAvailabilityBuilder.Build`, after the composer runs, for each id in `derived` that came from `TrapFishIds` (not from a real fish row), ask `composer.Derive(id)` and, when it returns an earlier week, replace the entry's `EarliestWeek` and `HardWeek` with the earlier value and append `"; forage route week N"` to the basis. Keep the composer instance in a local so it is built once.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.88)

```bash
git commit -m "v0.16.88: fishing trash is week 1; a forage route beats a trap row (Clam)"
```

---

### Task 5: Delete the pin table; overrides can never move a rule earlier

**Files:**
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs` (`DefaultItemSeasonPins` keeps only Woodskip, Sea Urchin, Red Mushroom), `src/TheLongestYear.Core/ItemAvailability.cs` (constructor rejection covers `_effortDerived`)
- Test: `tests/TheLongestYear.Tests/ItemAvailabilityModelTests.cs` (find the existing model tests file by grepping `RejectedSeasonOverrides`; extend it)

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void A_pin_earlier_than_a_phase_2_week_is_rejected()
{
    var effortDerived = new Dictionary<string, ItemEffort>
    {
        ["(O)421"] = new ItemEffort(2, "crop, Sunflower", 6, Season.Summer),   // Summer 1 plus 8 days
    };
    var model = new ItemAvailabilityModel(
        new Dictionary<string, ItemAvailability>(),
        seasonOverrides: new Dictionary<string, Season> { ["(O)421"] = Season.Summer },   // week 5 < 6
        effortDerived: effortDerived);
    Assert.Contains("(O)421", model.RejectedSeasonOverrides);
    Assert.Equal(6, model.For("(O)421").Week);
}

[Fact]
public void A_week_override_later_than_the_rule_is_accepted()
{
    var effortDerived = new Dictionary<string, ItemEffort> { ["(O)421"] = new ItemEffort(2, "crop", 6, Season.Summer) };
    var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>(), effortDerived: effortDerived,
        weekOverrides: new Dictionary<string, int> { ["(O)421"] = 8 });
    Assert.Equal(8, model.For("(O)421").Week);
}

[Fact]
public void Default_pins_are_the_three_the_rules_cannot_see()
    => Assert.Equal(new[] { "(O)397", "(O)420", "(O)734" }, GameplayConfig.DefaultItemSeasonPins.Keys.OrderBy(k => k).ToArray());
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

In the `ItemAvailabilityModel` constructor, replace the two rejection loops with:

```csharp
int? RuleWeek(string id)
{
    if (_derived.TryGetValue(id, out ItemAvailability? d)) return d.PacingWeek;
    if (_effortDerived.TryGetValue(id, out ItemEffort? e)) return e.EarliestWeek;
    return null;
}
foreach (KeyValuePair<string, Season> pin in _seasonOverrides)
{
    int? rule = RuleWeek(pin.Key);
    if (rule != null && AvailabilityWeeks.FirstWeekOf(pin.Value) < rule.Value)
        _rejectedSeasonOverrides.Add(pin.Key);
}
foreach (KeyValuePair<string, int> pin in _weekOverrides)
{
    int? rule = RuleWeek(pin.Key);
    if (rule != null && pin.Value < rule.Value)
        _rejectedSeasonOverrides.Add(pin.Key);
}
```

Update the class doc comment: an override may only move any placed week later (Phase 1 or Phase 2); the 0.16.79 "pins may move a rule earlier" behaviour is withdrawn (spec 2026-08-28-obtainable-board, section 6).

`GameplayConfig.DefaultItemSeasonPins`: delete every row except `(O)420` Spring (Red Mushroom, Jeff's ruling), `(O)397` Spring (Sea Urchin, bridge repair), `(O)734` Summer (Woodskip, Secret Woods; drop this row once the Woods marker places Woodskip at week 4, Task 3 does that, so delete it too if `FishAvailability` already yields 4 for Woodskip: check with a test and keep whichever is true). Update the doc comment to say the table is the override layer for rulings the rules cannot see. Fix every test that asserted a deleted pin (grep tests for `DefaultItemSeasonPins` and the ids 84, 62, 335, 82, 336, 700, 148, 142, 706, 132, 156, 701, 421, 266, 709, 422, 725, 536).

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.89)

```bash
git commit -m "v0.16.89: pin table reduced to the rulings the rules cannot see; no override moves a placed week earlier"
```

---

### Task 6: Unlock weeks from hearts and cost; villager first weeks

**Files:**
- Create: `src/TheLongestYear.Core/Availability/UnlockWeeks.cs`
- Test: `tests/TheLongestYear.Tests/UnlockWeeksTests.cs`

**Interfaces:**
- Produces: `UnlockWeeks.ForHearts(int hearts)`, `UnlockWeeks.ForCost(int gold)`, `UnlockWeeks.VillagerFirstWeek(string name)` (Sandy 9, Krobus 5 (Sewer week), Kent and Leo `null` meaning not in year 1, everyone else 1), `UnlockWeeks.ForFriendship(string villager, int hearts)` = `null` when the villager is null, else `Math.Max(VillagerFirstWeek, ForHearts(hearts))`.

- [ ] **Step 1: Write the failing tests**

```csharp
public class UnlockWeeksTests
{
    [Theory]
    [InlineData(2, 2)] [InlineData(3, 3)] [InlineData(4, 4)] [InlineData(5, 5)] [InlineData(6, 6)]
    [InlineData(7, 8)] [InlineData(8, 9)] [InlineData(9, 10)] [InlineData(10, 12)] [InlineData(1, 1)]
    public void Hearts(int hearts, int week) => Assert.Equal(week, UnlockWeeks.ForHearts(hearts));

    [Theory]
    [InlineData(500, 1)] [InlineData(1000, 1)] [InlineData(3000, 2)] [InlineData(5000, 3)]
    [InlineData(10000, 5)] [InlineData(25000, 7)] [InlineData(50000, 10)] [InlineData(50001, 13)]
    public void Cost(int gold, int week) => Assert.Equal(week, UnlockWeeks.ForCost(gold));

    [Fact]
    public void Sandy_is_not_met_before_the_desert()
    {
        Assert.Equal(9, UnlockWeeks.ForFriendship("Sandy", 3));
        Assert.Equal(9, UnlockWeeks.ForFriendship("Sandy", 7));
        Assert.Equal(8, UnlockWeeks.ForFriendship("Caroline", 7));
        Assert.Null(UnlockWeeks.ForFriendship("Kent", 3));
        Assert.Null(UnlockWeeks.ForFriendship("Leo", 3));
    }
}
```

- [ ] **Step 2: Run, expect a compile failure**

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Weeks for unlocks that are not a skill level (spec 2026-08-28-obtainable-board,
/// section 6, tables adopted by Jeff): a friendship recipe by its hearts, a bought recipe by its
/// price. Hearts run about one a week with two loved gifts and daily talk; the cost bands assume
/// the 500g start.</summary>
public static class UnlockWeeks
{
    private static readonly (int Hearts, int Week)[] HeartWeeks =
        { (2, 2), (3, 3), (4, 4), (5, 5), (6, 6), (7, 8), (8, 9), (9, 10), (10, 12) };

    private static readonly (int MaxGold, int Week)[] CostWeeks =
        { (1000, 1), (3000, 2), (5000, 3), (10000, 5), (25000, 7), (50000, 10) };

    private const int OverCostWeek = 13;

    /// <summary>Villagers the player cannot befriend from week 1; null means not in year 1
    /// (Kent returns in Spring of year 2, Leo lives on Ginger Island).</summary>
    private static readonly IReadOnlyDictionary<string, int?> FirstWeeks =
        new Dictionary<string, int?>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sandy"] = AvailabilityWeeks.SkullCavernWeek,
            ["Krobus"] = AvailabilityWeeks.SewerWeek,
            ["Kent"] = null,
            ["Leo"] = null,
        };

    public static int ForHearts(int hearts)
    {
        int week = 1;
        foreach ((int h, int w) in HeartWeeks)
            if (hearts >= h) week = w;
        return week;
    }

    public static int ForCost(int gold)
    {
        foreach ((int max, int week) in CostWeeks)
            if (gold <= max) return week;
        return OverCostWeek;
    }

    public static int? VillagerFirstWeek(string villager)
        => villager != null && FirstWeeks.TryGetValue(villager, out int? week) ? week : 1;

    public static int? ForFriendship(string villager, int hearts)
    {
        int? first = VillagerFirstWeek(villager);
        return first == null ? null : Math.Max(first.Value, ForHearts(hearts));
    }
}
```

- [ ] **Step 4: Run, expect green**

- [ ] **Step 5: Commit** (bump 0.16.90)

```bash
git commit -m "v0.16.90: unlock weeks from hearts and cost, villager first weeks"
```

---

### Task 7: Machine unlocks by recipe price and friendship; run time counts; the cave Dehydrator; Dried Mushrooms id

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/EffortData.cs` (`RecipePrices`), `ArtisanAvailability.cs`, `ItemQueryIds.cs`, `AvailabilityWeeks.cs` (`MachineRouteWeeks`), `src/TheLongestYear/Loop/GameEffortData.cs` (load `Data/Shops` recipe rows)
- Test: `tests/TheLongestYear.Tests/EffortRuleTests.cs` (extend) or a new `MachineUnlockWeekTests.cs`

**Interfaces:**
- Consumes: `UnlockWeeks` (Task 6).
- Produces: `EffortData.RecipePrices : IReadOnlyDictionary<string, int>` keyed by the machine's qualified id (`(BC)Dehydrator` 5000, `(BC)FishSmoker` 10000, from `Data/Shops` rows with `IsRecipe`); `AvailabilityWeeks.MachineRouteWeeks` (`(BC)Dehydrator` 6 "mushroom cave, Demetrius at 25,000g"; the cost route wins when earlier); `ArtisanAvailability.MachineWeek(string machineId, string? unlock, EffortData data)`.

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void A_bought_recipe_takes_its_price_week_and_a_friendship_recipe_its_hearts()
{
    var data = new EffortData
    {
        MachineUnlocks = new Dictionary<string, string> { ["(BC)FishSmoker"] = "null", ["(BC)39"] = "f Krobus 3", ["(BC)12"] = "s Farming 8" },
        RecipePrices = new Dictionary<string, int> { ["(BC)FishSmoker"] = 10000 },
    };
    Assert.Equal(5, ArtisanAvailability.MachineWeek("(BC)FishSmoker", "null", data));   // 10,000g
    Assert.Equal(5, ArtisanAvailability.MachineWeek("(BC)39", "f Krobus 3", data));     // Krobus from the Sewer week
    Assert.Equal(7, ArtisanAvailability.MachineWeek("(BC)12", "s Farming 8", data));
    Assert.Equal(9, ArtisanAvailability.MachineWeek("(BC)182", "null", data));          // special-order mail, no price
}

[Fact]
public void The_dehydrator_takes_the_earlier_of_pierre_and_the_cave()
{
    var data = new EffortData
    {
        MachineUnlocks = new Dictionary<string, string> { ["(BC)Dehydrator"] = "null" },
        RecipePrices = new Dictionary<string, int> { ["(BC)Dehydrator"] = 5000 },
    };
    Assert.Equal(3, ArtisanAvailability.MachineWeek("(BC)Dehydrator", "null", data));
}

[Fact]
public void Run_time_adds_whole_weeks()
{
    // Wine: keg, Farming 8 (week 7), 10,000 minutes is under a week: still 7. Cask (14 days): plus 2.
    var data = new EffortData
    {
        Objects = new Dictionary<string, RawObjectEntry> { ["398"] = new RawObjectEntry("Basic", -79, 80, false, new string[0], "Grape") },
        MachineRules = new List<RawMachineRule>
        {
            new("(BC)12", "(O)398", new string[0], new[] { "(O)348" }, 10000, -1),
            new("(BC)163", "(O)348", new string[0], new[] { "(O)AgedWine" }, -1, 14),
        },
        MachineUnlocks = new Dictionary<string, string> { ["(BC)12"] = "s Farming 8", ["(BC)163"] = "null" },
    };
    int? weekOf(string id) => id == "(O)398" ? 6 : id == "(O)348" ? 7 : null;
    int? effortOf(string id) => 1;
    Assert.Equal(7, ArtisanAvailability.Derive("(O)348", data, effortOf, weekOf)!.EarliestWeek);
    Assert.Equal(11, ArtisanAvailability.Derive("(O)AgedWine", data, effortOf, weekOf)!.EarliestWeek);   // cask week 9 + 2
}

[Fact]
public void Dried_mushroom_query_maps_to_the_plural_id()
    => Assert.Equal(new[] { "(O)DriedMushrooms" }, ItemQueryIds.Expand("FLAVORED_ITEM DriedMushroom DROP_IN_ID"));
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`EffortData`: add `public IReadOnlyDictionary<string, int> RecipePrices { get; init; } = new Dictionary<string, int>();`.

`AvailabilityWeeks`: add

```csharp
/// <summary>Machines with a route no recipe field shows. Dehydrator: choosing mushrooms for the
/// farm cave places one (FarmCave.cs:273), Demetrius comes at 25,000g earned, about week 6.</summary>
public static readonly IReadOnlyDictionary<string, (int Week, string Note)> MachineRouteWeeks =
    new Dictionary<string, (int, string)>(StringComparer.Ordinal)
    {
        ["(BC)Dehydrator"] = (6, "Dehydrator, mushroom cave"),
    };
public const int SpecialOrderMachineWeek = 9;
```

`ArtisanAvailability`: add

```csharp
public static int MachineWeek(string machineId, string? unlock, EffortData data)
{
    string text = (unlock ?? "").Trim();
    string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    int? week = null;
    if (tokens.Length >= 3 && tokens[0].Equals(FriendshipPrefix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(tokens[^1], out int hearts))
        week = UnlockWeeks.ForFriendship(tokens[1], hearts) ?? AvailabilityWeeks.UnknownWeek;
    else if (MachineUnlockLevel(unlock) != QuestUnlockLevel)
        week = AvailabilityWeeks.MachineLevelWeek(MachineUnlockLevel(unlock));
    if (data.RecipePrices.TryGetValue(machineId, out int price))
        week = Math.Min(week ?? int.MaxValue, UnlockWeeks.ForCost(price));
    if (AvailabilityWeeks.MachineRouteWeeks.TryGetValue(machineId, out (int Week, string Note) route))
        week = Math.Min(week ?? int.MaxValue, route.Week);
    return week ?? AvailabilityWeeks.SpecialOrderMachineWeek;
}
```

In `Derive`, replace `int machineWeek = AvailabilityWeeks.MachineLevelWeek(MachineUnlockLevel(unlock));` with `int machineWeek = MachineWeek(rule.MachineItemId, unlock, data);` and add run time: `int runWeeks = RunDays(rule.MinutesUntilReady, rule.DaysUntilReady) / Calendar.DaysPerWeek;` where `RunDays` returns `daysUntilReady >= 0 ? daysUntilReady : minutesUntilReady / MinutesPerDay`, and `week = Math.Max(machineWeek, inputWeek ?? 1) + runWeeks` (clamped to `Calendar.WeeksPerYear`). Pass `HardWeek: week` (no separate hard week for machines).

`ItemQueryIds.FlavoredBaseIds`: add `["DriedMushroom"] = "(O)DriedMushrooms"` and `["DriedFruit"]` already exists; keep both spellings.

`GameEffortData.cs`: when loading, read `Game1.content.Load<Dictionary<string, ShopData>>("Data/Shops")` (type `StardewValley.GameData.Shops.ShopData`), and for every shop item with `IsRecipe == true` and a positive `Price`, record `NormalizeItemId(item.ItemId) -> Price` (first wins). Wrap in the same try/catch pattern the file uses for the other tables, logging on failure. Pass it as `RecipePrices` in the `EffortData` initialiser.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.91)

```bash
git commit -m "v0.16.91: machine weeks from recipe price and hearts, run time counts, cave Dehydrator, Dried Mushrooms id"
```

---

### Task 8: Tapper goods from Data/WildTrees

**Files:**
- Create: `src/TheLongestYear.Core/Availability/TapperAvailability.cs`
- Modify: `src/TheLongestYear.Core/Availability/EffortData.cs` (`RawTapItem`, `TapItems`), `EffortComposer.cs` (rule list), `src/TheLongestYear/Loop/GameEffortData.cs` (load `Data/WildTrees`)
- Test: `tests/TheLongestYear.Tests/TapperAvailabilityTests.cs`

**Interfaces:**
- Produces: `RawTapItem(string TreeId, string ItemId, int Days)`; `EffortData.TapItems`; `TapperAvailability.Derive(id, data)` with week `MachineLevelWeek(TapperSkillLevel) + Days / 7`, effort `2 + (Days >= 7 ? 1 : 0)`.

- [ ] **Step 1: Write the failing tests**

```csharp
public class TapperAvailabilityTests
{
    private static readonly EffortData Data = new()
    {
        TapItems = new List<RawTapItem>
        {
            new("1", "(O)725", 7),    // Oak Resin
            new("2", "(O)724", 9),    // Maple Syrup
            new("3", "(O)726", 5),    // Pine Tar
        },
    };

    [Theory]
    [InlineData("(O)724", 5)] [InlineData("(O)725", 5)] [InlineData("(O)726", 4)]
    public void Tapper_goods_follow_foraging_4_plus_nights(string id, int week)
        => Assert.Equal(week, TapperAvailability.Derive(id, Data)!.EarliestWeek);

    [Fact]
    public void Not_a_tap_item_is_null() => Assert.Null(TapperAvailability.Derive("(O)24", Data));
}
```

- [ ] **Step 2: Run, expect a compile failure**

- [ ] **Step 3: Implement**

`EffortData.cs`: `public sealed record RawTapItem(string TreeId, string ItemId, int Days);` and `public IReadOnlyList<RawTapItem> TapItems { get; init; } = new List<RawTapItem>();`.

```csharp
namespace TheLongestYear.Core.Availability;

/// <summary>Tapper goods from Data/WildTrees TapItems. The Tapper is Foraging 4 in 1.6
/// (Data/CraftingRecipes "s Foraging 4"); the good is ready Days nights later. The artisan rule
/// used to reach these through the Wood Chipper at week 9 (review 2026-08-28).</summary>
public static class TapperAvailability
{
    public const int TapperSkillLevel = 4;
    private const int BaseEffort = 2;
    private const int SlowDays = 7;

    public static ItemEffort? Derive(string qualifiedId, EffortData data)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        ItemEffort? best = null;
        foreach (RawTapItem tap in data.TapItems)
        {
            if (tap.ItemId != qualifiedId) continue;
            int week = Math.Min(Calendar.WeeksPerYear,
                AvailabilityWeeks.MachineLevelWeek(TapperSkillLevel) + tap.Days / Calendar.DaysPerWeek);
            int effort = BaseEffort + (tap.Days >= SlowDays ? 1 : 0);
            if (best == null || week < best.EarliestWeek || (week == best.EarliestWeek && effort < best.Effort))
                best = new ItemEffort(effort, $"tapper, tree {tap.TreeId}, {tap.Days} nights, Foraging {TapperSkillLevel}, week {week}, effort {effort}",
                    week, AvailabilityWeeks.SeasonOf(week), week);
        }
        return best;
    }
}
```

Add `TapperAvailability.Derive(qualifiedId, _data)` to the composer's rule array after the crop and forage rules. In `GameEffortData.cs`, load `Game1.content.Load<Dictionary<string, WildTreeData>>("Data/WildTrees")` (`StardewValley.GameData.WildTrees`), and for each tree's `TapItems` with a plain item id (skip `PREVIOUS_OUTPUT_ID` and any id with a space) add `new RawTapItem(tree.Key, NormalizeItemId(tap.ItemId), tap.DaysUntilReady)`.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.92)

```bash
git commit -m "v0.16.92: tapper goods placed from Data/WildTrees at Foraging 4 plus nights"
```

---

### Task 9: Dish weeks include the recipe (hearts, skill, Queen of Sauce, Saloon, Cookies)

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/CookedDishAvailability.cs`, `EffortData.cs` (`CookingChannel`, `RecipePrices` reuse), `AvailabilityWeeks.cs` (`CookiesWeek`, `IceCreamStandWeek`), `src/TheLongestYear/Loop/GameEffortData.cs` (load `Data/TV/CookingChannel`, Saloon recipe rows already caught by Task 7's `IsRecipe` loader keyed by `(O)` id)
- Test: `tests/TheLongestYear.Tests/RecipeTimingTests.cs`

**Interfaces:**
- Produces: `EffortData.CookingChannel : IReadOnlyDictionary<string, int>` (recipe name -> episode index 1 to 32); `CookedDishAvailability.RecipeWeek(RawCookingRecipe recipe, EffortData data)` returning `int?` (null = not in year 1).

- [ ] **Step 1: Write the failing tests**

```csharp
public class RecipeTimingTests
{
    private static EffortData Data() => new()
    {
        CookingChannel = new Dictionary<string, int> { ["Stir Fry"] = 1, ["Blackberry Cobbler"] = 26, ["Pizza"] = 17 },
        RecipePrices = new Dictionary<string, int> { ["(O)206"] = 150 },   // Pizza at the Saloon
    };

    [Theory]
    [InlineData("Stir Fry", "l 100", 1)]
    [InlineData("Vegetable Stew", "f Caroline 7", 8)]
    [InlineData("Tom Kha Soup", "f Sandy 7", 9)]
    [InlineData("Farmer's Lunch", "s Farming 3", 3)]
    [InlineData("Fried Egg", "default", 1)]
    [InlineData("Cookies", "null", 5)]
    public void Recipe_weeks(string name, string unlock, int week)
        => Assert.Equal(week, CookedDishAvailability.RecipeWeek(new RawCookingRecipe(name, new string[0], "(O)1", unlock), Data()));

    [Fact]
    public void A_year_2_episode_is_not_in_year_1()
        => Assert.Null(CookedDishAvailability.RecipeWeek(new RawCookingRecipe("Blackberry Cobbler", new string[0], "(O)611", "l 100"), Data()));

    [Fact]
    public void A_saloon_recipe_uses_its_price_even_when_its_episode_is_year_2()
        => Assert.Equal(1, CookedDishAvailability.RecipeWeek(new RawCookingRecipe("Pizza", new string[0], "(O)206", "l 20"), Data()));

    [Fact]
    public void A_kent_recipe_is_not_in_year_1()
        => Assert.Null(CookedDishAvailability.RecipeWeek(new RawCookingRecipe("Crispy Bass", new string[0], "(O)214", "f Kent 3"), Data()));
}
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`EffortData`: `public IReadOnlyDictionary<string, int> CookingChannel { get; init; } = new Dictionary<string, int>();`.

`AvailabilityWeeks`: `public const int CookiesWeek = 5;` (Evelyn's Saloon event 19), `public const int YearOneEpisodes = 16;`.

`CookedDishAvailability`:

```csharp
private const string TvUnlock = "l";
private const int QueenOfSauceOnly = 100;

/// <summary>Week the recipe itself is learned, or null when it is not in year 1 (a year-2
/// episode, Kent, Leo). "l 100" is Queen of Sauce only: the episode's week for episodes 1 to
/// 16. "l N" under 100 is sold (the Saloon) and takes the price week. "f NPC N" takes the
/// hearts table. "s Skill N" the level week. "default" week 1. "null" is Cookies (event 19).</summary>
public static int? RecipeWeek(RawCookingRecipe recipe, EffortData data)
{
    string text = (recipe.UnlockCondition ?? "").Trim();
    if (text.Equals("default", StringComparison.OrdinalIgnoreCase)) return 1;
    if (text.Length == 0 || text.Equals("null", StringComparison.OrdinalIgnoreCase)) return AvailabilityWeeks.CookiesWeek;
    string[] tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    int? priceWeek = data.RecipePrices.TryGetValue(recipe.OutputItemId, out int price) ? UnlockWeeks.ForCost(price) : null;
    if (tokens[0].Equals(FriendshipPrefix, StringComparison.OrdinalIgnoreCase) && tokens.Length >= 3 && int.TryParse(tokens[^1], out int hearts))
        return Min(UnlockWeeks.ForFriendship(tokens[1], hearts), priceWeek);
    if (tokens[0].Equals(SkillPrefix, StringComparison.OrdinalIgnoreCase) && tokens.Length >= 3 && int.TryParse(tokens[^1], out int level))
        return Min(AvailabilityWeeks.MachineLevelWeek(level), priceWeek);
    if (tokens[0].Equals(TvUnlock, StringComparison.OrdinalIgnoreCase))
    {
        int? episode = data.CookingChannel.TryGetValue(recipe.Name, out int e) ? e : null;
        int? tvWeek = episode != null && episode.Value <= AvailabilityWeeks.YearOneEpisodes ? episode : null;
        return Min(tvWeek, priceWeek);
    }
    return Min(null, priceWeek);
}

private static int? Min(int? a, int? b) => a == null ? b : b == null ? a : Math.Min(a.Value, b.Value);
```

In `Derive`, after the ingredient loop: `int? recipeWeek = RecipeWeek(recipe, data); if (recipeWeek == null) latestWeek = null; else if (latestWeek != null) latestWeek = Math.Max(latestWeek.Value, recipeWeek.Value);` and append `", recipe week N"` (or "recipe not in year 1") to the basis. Set `KitchenWeek` to 6 in `AvailabilityWeeks` (spec) and fix the tests that assert 5 (`Kitchen dishes week 5` in `EffortRuleTests` or wherever `KitchenWeek` is asserted).

`GameEffortData.cs`: load `Game1.content.Load<Dictionary<string, string>>("Data/TV/CookingChannel")`, key is the episode index as a string, value's first `/` field is the recipe name; build `name -> int.Parse(key)`.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.93)

```bash
git commit -m "v0.16.93: dish weeks include the recipe: hearts, skill, Queen of Sauce year 1, Saloon price, Cookies; kitchen week 6"
```

---

### Task 10: Monster drops: rare drops are effort-only on pacing, minimum week on hard; Volcano and Dangerous Mines monsters out; Bone Fragment area 40

**Files:**
- Modify: `src/TheLongestYear.Core/Availability/MonsterDropAvailability.cs`, `MetalsAvailability.cs`
- Test: `tests/TheLongestYear.Tests/EffortRuleTests.cs` (extend)

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void A_rare_drop_sets_effort_only_on_pacing_and_its_week_on_hard()
{
    var drops = new List<RawMonsterDrop> { new("Dust Spirit", "(O)414", 0.02) };   // Crystal Fruit
    ItemEffort e = MonsterDropAvailability.Derive("(O)414", drops)!;
    Assert.Equal(AvailabilityWeeks.UnknownWeek, e.EarliestWeek);
    Assert.Equal(2, e.HardWeek);
}

[Fact]
public void Volcano_and_dangerous_mines_monsters_place_nothing()
{
    var drops = new List<RawMonsterDrop> { new("Magma Sprite", "(O)848", 0.5), new("Shadow Sniper", "(O)769", 0.5) };
    Assert.Null(MonsterDropAvailability.Derive("(O)848", drops));
    Assert.Null(MonsterDropAvailability.Derive("(O)769", drops));
}

[Fact]
public void Bone_fragment_is_area_40()
    => Assert.Equal(2, MetalsAvailability.Derive(new PoolItem("(O)881", 12, 3, new List<Season>(), new List<string>()))!.Week);
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`MonsterDropAvailability`: add `private const double RareDropChance = 0.05;`. Remove from `SpawnArea` every Volcano and Dangerous Mines name: Blue Squid, Haunted Skull, Tiger Slime, Lava Lurk, Hot Head, Magma Sprite, Magma Sparker, Magma Duggy, False Magma Cap, Dwarvish Sentry, Putrid Ghost, Shadow Sniper, Skeleton Mage, Spider, Stick Bug, Fireball, Spiker (comment: post-Community-Center content, spec 2026-08-28-obtainable-board section 6). In `Derive`, compute `int monsterWeek = MineAreas.Week(area.Value);` and `bool rare = drop.Chance < RareDropChance;` then `int week = rare ? AvailabilityWeeks.UnknownWeek : monsterWeek;` and build `new ItemEffort(effort, basis + (rare ? ", rare drop: pacing Winter, hard week N" : ""), week, rare ? Season.Winter : MineAreas.GateSeason(area.Value), MineAreas.HardWeek(area.Value))`. The "better" comparison keeps using `week`.

`MetalsAvailability`: change `(O)881` to `new(MineAreas.Area40, 4, "bone fragment, skeletons from mine area 40 and dig spots")`.

- [ ] **Step 4: Run all tests, expect green** (fix any test asserting Bone Fragment at week 3 or a Volcano monster)

- [ ] **Step 5: Commit** (bump 0.16.94)

```bash
git commit -m "v0.16.94: rare monster drops effort-only on pacing; Volcano and Dangerous Mines monsters removed; Bone Fragment area 40"
```

---

### Task 11: Remaining table rows (Sewer 7, Prize Ticket, Mystery Box, jelly efforts, Ghostfish)

**Files:**
- Modify: `src/TheLongestYear.Core/AvailabilityWeeks.cs`, `src/TheLongestYear.Core/Availability/FishAvailability.cs` (jelly effort rows)
- Test: `tests/TheLongestYear.Tests/AvailabilityWeeksTests.cs`, `FishAvailabilityTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void Sewer_is_week_7() => Assert.Equal(7, AvailabilityWeeks.SewerWeek);

[Theory]
[InlineData("(O)PrizeTicket", 2, 1)]
[InlineData("(O)MysteryBox", 3, 2)]
public void Quest_rewards_have_pacing_and_hard_weeks(string id, int week, int hard)
{
    ItemEffort e = ShopAvailability.Derive(id)!;
    Assert.Equal(week, e.EarliestWeek);
    Assert.Equal(hard, e.HardWeek);
}

[Theory]
[InlineData("(O)CaveJelly", 3)] [InlineData("(O)SeaJelly", 1)] [InlineData("(O)RiverJelly", 1)]
public void Jellies_have_an_effort_row(string id, int effort)
{
    var item = new PoolItem(id, 150, 3, new List<Season>(), new List<string> { "Forest" });
    Assert.Equal(effort, FishAvailability.Derive(item, null).Effort);
}
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`AvailabilityWeeks`: `SewerWeek = 7`; `QuestRewardWeeks` becomes `(int Week, int Hard, string Note)` with `(O)PrizeTicket (2, 1, "every 3rd Help Wanted quest, Quest.cs")` and `(O)MysteryBox (3, 2, "Qi plane after the 6th Help Wanted quest or day 50, Utility.cs")`; `ShopAvailability` passes `HardWeek: quest.Hard`. Add

```csharp
/// <summary>Fish with no Data/Fish row the parser reads (the 1.6 jellies): effort by hand so the
/// absolute bands do not call a trivial catch Extreme.</summary>
public static readonly IReadOnlyDictionary<string, int> FishEffortRows =
    new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["(O)CaveJelly"] = 3, ["(O)SeaJelly"] = 1, ["(O)RiverJelly"] = 1,
    };
```

and in `FishAvailability.Derive`'s `row == null` branch use `AvailabilityWeeks.FishEffortRows.TryGetValue(item.ItemId, out int rowEffort) ? rowEffort : ItemAvailabilityModel.UnrecognisedEffort`. Ghostfish needs no row change: with its pin gone (Task 5) `MineFishWeeks` places it at week 1; add a test in `FishAvailabilityTests` asserting `(O)156` week 1 with locations `["UndergroundMine"]`.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.95)

```bash
git commit -m "v0.16.95: Sewer week 7, Prize Ticket and Mystery Box pacing and hard weeks, jelly effort rows, Ghostfish week 1"
```

---

### Task 12: Book weeks and the Book pool; island fruit excluded

**Files:**
- Modify: `src/TheLongestYear.Core/AvailabilityWeeks.cs` (`BookWeeks`), `src/TheLongestYear.Core/Availability/EffortComposer.cs` (`PoolBook` uses the table), `src/TheLongestYear.Core/ItemPoolBuilder.cs` (book pool keeps only `BookWeeks` keys; `BuiltInExcludedItemIds` adds `(O)91` Banana and `(O)834` Mango)
- Test: `tests/TheLongestYear.Tests/BookKitTests.cs` or `ShopAvailabilityTests.cs` (extend), `ItemPoolBuilderTests` (find by grepping `BuiltInExcludedItemIds`)

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData("(O)Book_PriceCatalogue", 2)] [InlineData("(O)SkillBook_0", 3)] [InlineData("(O)SkillBook_2", 3)]
[InlineData("(O)Book_Speed", 5)] [InlineData("(O)PurpleBook", 5)] [InlineData("(O)Book_Trash", 1)]
[InlineData("(O)Book_Marlon", 1)] [InlineData("(O)Book_Bombs", 3)] [InlineData("(O)Book_Friendship", 5)]
public void Book_weeks(string id, int week) => Assert.Equal(week, AvailabilityWeeks.BookWeeks[id]);

[Fact]
public void Year_2_and_drop_only_books_are_not_in_the_table()
{
    Assert.False(AvailabilityWeeks.BookWeeks.ContainsKey("(O)Book_Void"));
    Assert.False(AvailabilityWeeks.BookWeeks.ContainsKey("(O)Book_AnimalCatalogue"));
    Assert.False(AvailabilityWeeks.BookWeeks.ContainsKey("(O)Book_Diamonds"));
}
```

Plus one pool test: build `ItemPools` through `ItemPoolBuilder` with a `RawObjectEntry` for `Book_Void` and one for `Book_PriceCatalogue` (both with the `book_item` context tag) and assert only the catalogue is in `pools.Books`. Read the existing pool-builder tests for how `Build` is called and copy that shape.

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`AvailabilityWeeks`:

```csharp
/// <summary>Books with a year-1 route (Data/Shops and code, review 2026-08-28). The Bookseller's
/// eleven story books are YEAR 3 in his stock; the ones here have a free gift box, a shop, or a
/// prize-machine route. Everything else is drop-only and stays out of the Book pool.</summary>
public static readonly IReadOnlyDictionary<string, int> BookWeeks =
    new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["(O)Book_PriceCatalogue"] = 2,   // Bookseller, 3,000g, always
        ["(O)SkillBook_0"] = 3, ["(O)SkillBook_1"] = 3, ["(O)SkillBook_2"] = 3, ["(O)SkillBook_3"] = 3, ["(O)SkillBook_4"] = 3,  // 5,000g and up
        ["(O)Book_Speed"] = 5,            // Way of the Wind pt. 1, 15,000g
        ["(O)PurpleBook"] = 5,            // Book of Stars, 15,000g at 25 percent
        ["(O)Book_Trash"] = 1,            // gift box in Town
        ["(O)Book_Marlon"] = 1,           // gift box in the Adventurer's Guild
        ["(O)Book_Bombs"] = 3,            // the Dwarf, 4,000g
        ["(O)Book_Friendship"] = 5,       // prize ticket machine
    };
```

`EffortComposer.PoolBook`: return the table week (`BookWeeks.TryGetValue`) instead of `BookWeek`; an id in the pool but not in the table returns null (it should no longer be in the pool). Delete `BookWeek`. `ItemPoolBuilder`: where the Books pool is assembled, filter to `AvailabilityWeeks.BookWeeks.ContainsKey(id)`. Add `(O)91` and `(O)834` to `BuiltInExcludedItemIds` with the comment "Ginger Island, after the Community Center". Remove Banana and Mango from `FruitTreeFruitWeeks` if Task 3 did not.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.96)

```bash
git commit -m "v0.16.96: book weeks from the real year-1 routes; Book pool limited to them; island fruit excluded"
```

---

### Task 13: Rule E on absolute bands

**Files:**
- Modify: `src/TheLongestYear.Core/EffortTiers.cs`, `GoalWeighting.cs` (no cutoff computation)
- Test: `tests/TheLongestYear.Tests/EffortTiersAndComposerTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Theory]
[InlineData(0, EffortTier.Easy)] [InlineData(2, EffortTier.Easy)] [InlineData(3, EffortTier.Medium)]
[InlineData(5, EffortTier.Medium)] [InlineData(6, EffortTier.Hard)] [InlineData(8, EffortTier.Hard)]
[InlineData(9, EffortTier.Extreme)] [InlineData(12, EffortTier.Extreme)]
public void Tiers_are_absolute(int effort, EffortTier tier) => Assert.Equal(tier, EffortTiers.Tier(effort));

[Fact]
public void The_harder_of_two_easy_items_is_still_askable_in_spring()
{
    var rules = new GoalSamplingRules(Season.Spring, 0, id => id == "(O)80" ? 1 : 3);
    var weights = GoalWeighting.For(new[] { "(O)80", "(O)334" }, rules, _ => Rarity.Common);
    Assert.All(weights, w => Assert.True(w.Weight > 0));
}
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`EffortTiers`: replace `Cutoffs` and the two-argument `Tier` with

```csharp
public const int EasyMax = 2;
public const int MediumMax = 5;
public const int HardMax = 8;

/// <summary>Absolute bands on the effort scale (spec 2026-08-28-obtainable-board, section 4):
/// Easy 0 to 2, Medium 3 to 5, Hard 6 to 8, Extreme 9 and up. Relative quartiles made the
/// hardest of two easy items Extreme and unaskable in Spring (review 2026-08-28).</summary>
public static EffortTier Tier(int effort)
    => effort <= EasyMax ? EffortTier.Easy : effort <= MediumMax ? EffortTier.Medium : effort <= HardMax ? EffortTier.Hard : EffortTier.Extreme;
```

Keep `TierCutoffs` and the old overloads only if something outside tests uses them (grep); otherwise delete them and fix the callers (`GoalWeighting.For` drops the `Cutoffs` call and calls `EffortTiers.Tier(effort.Value)`; `tly_dumpeffort` in `ModEntry` prints the band names without cutoffs). Update the `item-effort-model.md` header text in `ModEntry` to say bands are absolute.

- [ ] **Step 4: Run all tests, expect green** (rewrite the quartile tests in `EffortTiersAndComposerTests` to the absolute expectations)

- [ ] **Step 5: Commit** (bump 0.16.97)

```bash
git commit -m "v0.16.97: rule E tiers are absolute effort bands"
```

---

### Task 14: Goals follow the gate with no look-ahead; flat ceilings; no zero-goal fallback card

**Files:**
- Modify: `src/TheLongestYear.Core/SeasonNeed.cs`, `BonusItemSampler.cs:19`, `SelectionService.cs:85-93`
- Test: `tests/TheLongestYear.Tests/SeasonNeedTests.cs`, `BonusItemSamplerTests.cs`, `SelectionServiceTests.cs` (find the file by grepping `OfferForWeek`)

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void A_percentage_bundle_may_be_asked_only_for_this_seasons_share()
{
    BundleRequirement req = BundleRequirement.CreatePercentage("Recycler's", Theme.Mixed,
        new[] { "(O)168", "(O)169", "(O)170", "(O)171", "(O)172", "(O)338" }, numberOfSlots: 4,
        cumulativeRequiredBySeason: new[] { 1, 2, 3, 4 });
    Assert.Equal(1, SeasonNeed.For(req, Season.Spring, completed: 0));
    Assert.Equal(0, SeasonNeed.For(req, Season.Spring, completed: 1));
    Assert.Equal(1, SeasonNeed.For(req, Season.Fall, completed: 2));
    Assert.Equal(4, SeasonNeed.For(req, Season.Winter, completed: 0));
}

[Fact]
public void A_per_item_bundle_may_be_asked_only_for_items_due_by_now()
{
    var pins = new Dictionary<string, Season> { ["(O)153"] = Season.Spring, ["(O)700"] = Season.Fall, ["(O)140"] = Season.Winter, ["(O)141"] = Season.Winter };
    BundleRequirement req = BundleRequirement.CreatePerItem("Lake Fish", Theme.Fishing, pins.Keys.ToList(), pins);
    Assert.Equal(1, SeasonNeed.For(req, Season.Spring, 0));
    Assert.Equal(1, SeasonNeed.For(req, Season.Summer, 0));
    Assert.Equal(2, SeasonNeed.For(req, Season.Fall, 0));
    Assert.Equal(4, SeasonNeed.For(req, Season.Winter, 0));
}

[Fact]
public void Ceilings_are_flat_5() => Assert.Equal(new[] { 5, 5, 5, 5 }, BonusItemSampler.DefaultMaxCountBySeason);

[Fact]
public void The_offer_never_pads_with_a_theme_that_can_ask_nothing()
{
    int Askable(Theme t) => t == Theme.Fishing ? 3 : 0;
    IReadOnlyList<Theme> offer = SelectionService.OfferForWeek(1, 1, Array.Empty<Theme>(), Askable);
    Assert.Equal(new[] { Theme.Fishing }, offer);
}

[Fact]
public void A_theme_with_one_goal_may_pad_the_second_card()
{
    int Askable(Theme t) => t == Theme.Fishing ? 3 : t == Theme.Foraging ? 1 : 0;
    IReadOnlyList<Theme> offer = SelectionService.OfferForWeek(1, 1, Array.Empty<Theme>(), Askable);
    Assert.Equal(2, offer.Count);
    Assert.Contains(Theme.Foraging, offer);
}
```

(Check `BundleRequirement.CreatePercentage` and `CreatePerItem` signatures in `BundleRequirement.cs` and match them; the theme enum member names are in `Theme.cs`.)

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

`SeasonNeed.For`:

```csharp
int allowed = required;
if (requirement.Kind == BundleKind.Percentage && requirement.CumulativeRequiredBySeason != null)
    allowed = Math.Min(required, requirement.CumulativeRequiredBySeason[s]);
else if (requirement.Kind == BundleKind.PerItem && requirement.ItemSeasonPins != null)
{
    int due = requirement.ItemSeasonPins.Count(p => (int)p.Value <= s);
    int unpinned = requirement.Ingredients.Count(id => !requirement.ItemSeasonPins.ContainsKey(id));
    allowed = Math.Min(required, due + (s == (int)Season.Winter ? unpinned : 0));
}
return Math.Max(0, allowed - Math.Max(0, completed));
```

Delete `HalfUp` and rewrite the class comment: goals follow the gate exactly, no look-ahead (Jeff, 2026-08-28); a player ahead of the gate sees quiet cards until the next season, by design.

`BonusItemSampler.DefaultMaxCountBySeason = new[] { 5, 5, 5, 5 }`.

`SelectionService.OfferForWeek` fallback block: filter `fallback` to `askableFor == null || askableFor(t) >= 1` before the shuffle; when nothing qualifies the offer stays short (a single card is allowed; the hub already handles one card). Apply the same filter in `Candidates`. Fix the existing tests that expected a padded two-card offer from zero-goal themes.

- [ ] **Step 4: Run all tests, expect green**

- [ ] **Step 5: Commit** (bump 0.16.98)

```bash
git commit -m "v0.16.98: goals follow the gate with no look-ahead; flat 5 ceilings; no zero-goal fallback card"
```

---

### Task 15: Goals read GoalWeek; unknown items are not goals

**Files:**
- Modify: `src/TheLongestYear.Core/GoalObtainability.cs`
- Test: `tests/TheLongestYear.Tests/GoalObtainabilityTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
[Fact]
public void An_unplaced_item_is_never_a_goal()
{
    var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>());
    Assert.False(GoalObtainability.IsObtainable(null, model, "(O)999", 16));
}

[Fact]
public void Goals_read_the_goal_week_for_the_mode()
{
    var derived = new Dictionary<string, ItemAvailability>
    {
        ["(O)90"] = new ItemAvailability(Season.Fall, 3, "cactus", EffortSource.Derived, 9, Season.Fall, HardWeek: 6),
    };
    Assert.False(GoalObtainability.IsObtainable(null, new ItemAvailabilityModel(derived, mode: WeekMode.HardGates), "(O)90", 6));
    Assert.True(GoalObtainability.IsObtainable(null, new ItemAvailabilityModel(derived, mode: WeekMode.HardAll), "(O)90", 6));
}
```

- [ ] **Step 2: Run, expect failures**

- [ ] **Step 3: Implement**

```csharp
if (availability != null)
{
    if (!availability.IsPlaced(itemId)) return false;   // unknown means not on a card (Jeff, 2026-08-28)
    if (availability.For(itemId).GoalWeek > weekOfYear) return false;
}
return true;
```

Rewrite the class comment accordingly. Keep the `availability == null` path (legacy tests with no model) returning true on catalog seasons alone.

- [ ] **Step 4: Run all tests, expect green** (some existing tests relied on unknown items being askable; change them to place the item first)

- [ ] **Step 5: Commit** (bump 0.16.99)

```bash
git commit -m "v0.16.99: weekly goals read the mode's goal week; unplaced items are never goals"
```

---

### Task 16: Glue: build the model with the difficulty's WeekMode; dump shows both weeks and a judgement kind

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs:486-489` (model build), `ModEntry.cs:1954-1985` (`tly_dumpavailability`), `src/TheLongestYear.Core/AvailabilityWeeks.cs` (`IsJudgementBasis`)
- Test: `tests/TheLongestYear.Tests/DifficultyResolverTests.cs` (extend with the mode mapping) and a Core test for `WeekModes.For(DifficultyStep)`

**Interfaces:**
- Produces: `WeekModes.For(DifficultyStep step)`: Easy and Normal `Pacing`, Hard `HardGates`, Extreme `HardAll` (put it in `WeekMode.cs`).

- [ ] **Step 1: Write the failing test**

```csharp
[Theory]
[InlineData(DifficultyStep.Easy, WeekMode.Pacing)] [InlineData(DifficultyStep.Normal, WeekMode.Pacing)]
[InlineData(DifficultyStep.Hard, WeekMode.HardGates)] [InlineData(DifficultyStep.Extreme, WeekMode.HardAll)]
public void Difficulty_picks_the_week_mode(DifficultyStep step, WeekMode mode) => Assert.Equal(mode, WeekModes.For(step));
```

- [ ] **Step 2: Run, expect a compile failure**

- [ ] **Step 3: Implement**

`WeekMode.cs`:

```csharp
public static class WeekModes
{
    public static WeekMode For(DifficultyStep step) => step switch
    {
        DifficultyStep.Hard => WeekMode.HardGates,
        DifficultyStep.Extreme => WeekMode.HardAll,
        _ => WeekMode.Pacing,
    };
}
```

`ItemAvailabilityBuilder.Build` gains `WeekMode mode = WeekMode.Pacing` and passes it to the model constructor. In `ModEntry` where `_availability` is built (line 486), pass `mode: WeekModes.For(<the live difficulty step>)`; find how the live step is resolved (`DifficultyResolver`, see `LogStep` near line 2633) and reuse that value. The model must be rebuilt whenever the step changes at the loop boundary: call the same build where the difficulty is applied at reset (grep `DifficultyResolver` in `RunController`/`ModEntry` for the reset hook) and log `Availability model rebuilt for {step} ({mode})`.

`tly_dumpavailability`: the table header becomes `| Item | Id | Week | Hard | Gate | Placed | Catalog seasons | Due | Effort | Basis |`, the row prints `a.PacingWeek`, `a.HardWeekOrPacing`; `Placed` prints `judgement` when the basis starts with `table,` or contains `(for Jeff to confirm)` (add `AvailabilityWeeks.IsJudgementBasis(string basis)` doing that check). Add a closing section `## Judgement rows (N)` listing every judgement-placed item with its week, before the Unknown section. Update the intro paragraph to describe the Hard column and the judgement kind.

- [ ] **Step 4: Run all tests, then build the mod** (`dotnet build src/TheLongestYear -c Release`), expect green and a clean build

- [ ] **Step 5: Commit** (bump 0.16.100)

```bash
git commit -m "v0.16.100: model built for the difficulty's week mode; dump shows pacing and hard weeks and judgement rows"
```

---

## Self-review

Spec coverage for the sections this plan owns: section 1 (two weeks, modes, unknown not a goal) Tasks 1, 15, 16; section 4 (absolute bands, jelly rows) Tasks 13, 11; section 5 (no look-ahead, ceilings, fallback card) Task 14; section 6 (mines, Desert, Sewer, machines, tapper, dehydrator, dishes, crops, forage, Woods, trash, Clam, quest rewards, pins, Bone Fragment, monsters) Tasks 2 to 11; section 7 (books) Task 12; Easy (no stretch is plan 2; the pin, band and look-ahead changes apply to Easy through the same code) covered. Not in this plan, by design: section 2 stretch rule and the hard-item rule (plan 2), section 3 pools and additions and rewind (plan 3), section 8 Boosts and the Garden Pot keep (plan 4), section 9 sim script and gatecheck tags (plan 5); `tly_dumpavailability` columns are here because the model changed.

Types: `ItemEffort` positional order is `(Effort, Basis, EarliestWeek, GateSeason, HardWeek)` everywhere; `ItemAvailability` adds `HardWeek` then `Mode`; `MineAreas.HardWeek`, `LocationGating.HardWeekFor`, `UnlockWeeks.ForFriendship` return types match their callers in Tasks 7, 9, 10.
