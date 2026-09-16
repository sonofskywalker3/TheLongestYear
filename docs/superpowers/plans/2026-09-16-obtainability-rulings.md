# Obtainability Rulings (2026-09-16) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Apply Jeff's 2026-09-16 rulings on the obtainability rerun report to the item obtainability model and the darkness fairness rule, then give Jeff a before and after per item.

**Architecture:** The model lives in `src/TheLongestYear.Core/Obtainability/` (blind: never names the older item model) and is built by `ObtainabilityBuilder` from records the glue (`src/TheLongestYear/Loop/GameObtainabilityData*.cs`) reads from game data. Facts that live in game code are hand-typed tables citing the PC 1.6 decompile. The darkness consumer is `src/TheLongestYear.Core/Sabotage/FairnessRule.cs`, which reads every source directly and checks its `Requires` strings against a `SaveSnapshot`.

**Tech Stack:** C# (.NET 6), SMAPI 4, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-14-obtainability-phase2-design.md`, section "Ruling log" (the rows dated 2026-09-16). Decompile: `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley`.

## Global Constraints

- Branch `story`. Commit after each task and push straight away. Do NOT touch the `Version` in `src/TheLongestYear/manifest.json`.
- Before every commit: `dotnet test tests/TheLongestYear.Tests` (2445 passing at the start) and `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` (0 errors).
- Every new `.cs` file under `src/TheLongestYear.Core/Obtainability/` must be added to `BlindFiles` in `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs`, and must not contain any word from that test's `Forbidden` list, in code or comments.
- Every hand-typed game fact carries a decompile file and line citation in a comment.
- Board generation must not change: no file the board reads may name the model (existing static guard test).
- No em dashes in code, comments, docs or commit messages. Never use `/sdcard/`.
- Live runs: my automated run only, throwaway save (the Rodger lineage, latest `None_449242947`), per `docs/HEADLESS_DRIVING.md`. Label every launch.

---

### Task 1: Repeatable chance: the proposal for Jeff (no model change)

Ruling: "a chance route the player can retry many times a day with a decent chance each try counts as dependable. The threshold and the resulting item list go to Jeff before the model changes."

**Files:**
- Create: `docs/superpowers/notes/2026-09-16-repeatable-chance-proposal.md`

- [ ] **Step 1: Classify every Chance source kind by tries per day.** Using the decompile, write one table row per chance source family with: tries a player can make in a day, the chance per try, and PROMOTE or KEEP. Starting proposal to check against the code:

| Family | Where in code | Tries a day | Proposal |
|---|---|---|---|
| Mine node rows in `MineSources.NodeTable` marked Chance (coal, amethyst, topaz, jade, aquamarine, ruby, emerald, geode, frozen geode, magma geode, omni geode) | MineShaft.cs 3642-3673, 3962-3975 | hundreds of stones | PROMOTE |
| Diamond node | MineShaft.cs 4607 | hundreds of stones, very rare node | KEEP unless the per-stone chance is at least 1% |
| Monster drops (`MineSources.MonsterDrops`) | Data/Monsters field 6 | dozens of kills | PROMOTE when `row.Chance >= 0.25` |
| Fishing trash (`SpawnSources.FishingTrash`) | FishingRod.cs trash roll | about 30 casts | PROMOTE (confirm the per-cast chance in the decompile) |
| Garbage cans, artifact spots, fish ponds, cart, fishing treasure, geode contents, festival chance rewards | various | at most a handful | KEEP |

- [ ] **Step 2: List the items that change.** Run the model against the proposal without changing it: in a scratch test (not committed) or by reading `obtain-compare-2026-09-16.md` (in the game's `Mods/TheLongestYear/` folder), list every LuckOnly item whose only chance sources are PROMOTE families, and every NewLater/Agree item whose dependable week would move. Write the list into the note with each item's current and proposed dependable week.

- [ ] **Step 3: Commit the note and STOP.**

```bash
git add docs/superpowers/notes/2026-09-16-repeatable-chance-proposal.md
git commit -m "obtainability: repeatable chance proposal for Jeff"
git push
```

Report the threshold and the list to Jeff verbatim in chat. Task 2 waits for his yes; Tasks 3 to 9 do not.

---

### Task 2: Repeatable chance: apply Jeff's approved threshold

Runs only after Jeff approves Task 1. The values below are the Task 1 proposal; replace them with what Jeff approved before starting.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/MineSources.cs` (NodeTable reliabilities; `MonsterDrops`)
- Modify: `src/TheLongestYear.Core/Obtainability/SpawnSources.cs:212-217` (`FishingTrash`)
- Test: `tests/TheLongestYear.Tests/ObtainabilityMineTests.cs`, `tests/TheLongestYear.Tests/ObtainabilitySpawnTests.cs`

**Interfaces:**
- Produces: `MineSources.RepeatableDropChance` (`public const double`, 0.25), used by later reports.

- [ ] **Step 1: Write the failing tests** (add to `ObtainabilityMineTests.cs`):

```csharp
[Fact]
public void A_common_monster_drop_is_dependable_and_a_rare_one_is_chance()
{
    var rows = new[]
    {
        new MonsterDropRow("Grub", "(O)684", 0.6),
        new MonsterDropRow("Grub", "(O)717", 0.1),
    };
    var objects = new Dictionary<string, ObjInfo>
    {
        ["(O)684"] = new("(O)684", "Bug Meat", -28, 8, new List<string>(), false),
        ["(O)717"] = new("(O)717", "Crab", -4, 100, new List<string>(), false),
    };
    var drops = MineSources.MonsterDrops(rows, objects).ToDictionary(d => d.ItemId, d => d.Source);
    Assert.Equal(Reliability.Dependable, drops["(O)684"].Reliability);
    Assert.Equal(Reliability.Chance, drops["(O)717"].Reliability);
}

[Fact]
public void Geodes_from_stones_are_dependable()
{
    var nodes = MineSources.Nodes().Where(n => n.ItemId == "(O)535").Select(n => n.Source).ToList();
    Assert.Contains(nodes, s => s.Reliability == Reliability.Dependable);
}
```

And to `ObtainabilitySpawnTests.cs`:

```csharp
[Fact]
public void Fishing_trash_is_dependable()
    => Assert.All(SpawnSources.FishingTrash(), t => Assert.Equal(Reliability.Dependable, t.Source.Reliability));
```

- [ ] **Step 2: Run them:** `dotnet test tests/TheLongestYear.Tests --filter "ObtainabilityMineTests|ObtainabilitySpawnTests"`. Expected: the three new tests FAIL.

- [ ] **Step 3: Implement.** In `MineSources.NodeTable`, change the approved rows from `Reliability.Chance` to `Reliability.Dependable` and add `, repeatable (ruling 2026-09-16)` to their note. In `MonsterDrops`, replace `Reliability.Chance` with:

```csharp
row.Chance >= RepeatableDropChance ? Reliability.Dependable : Reliability.Chance,
```

and add near the other constants:

```csharp
/// <summary>A drop at least this likely per kill counts as dependable: mine monsters can be killed
/// dozens of times a day (Jeff's ruling 2026-09-16, repeatable chance).</summary>
public const double RepeatableDropChance = 0.25;
```

In `SpawnSources.FishingTrash`, use `Reliability.Dependable` and the detail `"fishing trash, repeatable (ruling 2026-09-16)"`.

- [ ] **Step 4: Run the full suite.** Existing tests that assert these rows are Chance must be updated to the ruling; any other failure stops the task for a report. Expected: all pass.

- [ ] **Step 5: Commit and push.** `git commit -m "obtainability: repeatable chance routes count as dependable (Jeff's threshold)"`

---

### Task 3: World routes the data files miss

**Files:**
- Create: `src/TheLongestYear.Core/Obtainability/WorldSources.cs`
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs` (direct sources list)
- Modify: `tests/TheLongestYear.Tests/ObtainabilityBlindGuardTests.cs` (add the new file)
- Test: `tests/TheLongestYear.Tests/ObtainabilityWorldSourcesTests.cs`

**Interfaces:**
- Produces: `public static class WorldSources` with `public static IEnumerable<(string ItemId, ObtainSource Source)> All()`.

Facts (all dependable unless stated, per Jeff's rulings):

| Item | Route | Days | Requires | Citation |
|---|---|---|---|---|
| (O)771 Fiber | cutting weeds, 50% a weed | always | `weeds:any` | Object.cs 1395-1397 |
| (O)309 Acorn, (O)310 Maple Seed, (O)311 Pine Cone | shaking or chopping mature wild trees | always | `trees:mature wild trees` | Tree.cs 574-576, 940 |
| (O)296 Salmonberry | bushes | Spring 15 to 18 | `bushes:wild` | Bush.cs 229-235, 451 |
| (O)410 Blackberry | bushes | Fall 8 to 11 | `bushes:wild` | Bush.cs 236-241, 452 |
| (O)330 Clay | tilling outdoors, 3% a tile by default | always | `tilling:outdoors` | GameLocation.cs 14222 |
| (O)412 Winter Root, (O)416 Snow Yam | tilling outdoors off the farm in Winter, 8% a tile | Winter | `tilling:outdoors off the farm` | GameLocation.cs 14212-14214 |
| (O)330 Clay, (O)78 Cave Carrot | tilling a mine floor (15% a tile, then the table) | always | `mines:floor 1` | MineShaft.cs 3095-3181 |
| (O)80 Quartz | mine floor finds, any floor | always | `mines:floor 1` | MineShaft.cs 3849-3851, 3936-3938 |
| (O)86 Earth Crystal | mine floor finds, floors 1 to 39 | always | `mines:floor 1` | MineShaft.cs 3895-3897 |
| (O)84 Frozen Tear | mine floor finds, floors 40 to 79 | always | `mines:floor 40` | MineShaft.cs 3918-3920 |
| (O)82 Fire Quartz | mine floor finds, floors 80 to 119 | always | `mines:floor 80` | MineShaft.cs 3923-3925 |
| (O)420 Red Mushroom | mine floor finds above floor 20, 10% | always | `mines:floor 21` | MineShaft.cs 3856-3858 |
| (O)422 Purple Mushroom | mine floor finds above floor 80, 5% | always | `mines:floor 81` | MineShaft.cs 3852-3854 |
| (O)107 Dinosaur Egg | Skull Cavern dinosaur floors, 6% a spawn | always, CHANCE | `location:SkullCave` | MineShaft.cs 3939-3944 |

- [ ] **Step 1: Write the failing tests** in `ObtainabilityWorldSourcesTests.cs`:

```csharp
using System.Linq;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

public class ObtainabilityWorldSourcesTests
{
    private static ObtainSource One(string id, SourceKind kind)
        => WorldSources.All().Where(s => s.ItemId == id && s.Source.Kind == kind).Select(s => s.Source).First();

    [Fact]
    public void Fiber_from_weeds_is_dependable_from_day_1()
    {
        ObtainSource fiber = One("(O)771", SourceKind.Forage);
        Assert.Equal(Reliability.Dependable, fiber.Reliability);
        Assert.Equal(1, fiber.Lands.Lands(1));
    }

    [Fact]
    public void Salmonberries_land_on_spring_15_and_blackberries_on_fall_8()
    {
        Assert.Equal(15, One("(O)296", SourceKind.Forage).Lands.Lands(1));
        Assert.Null(One("(O)296", SourceKind.Forage).Lands.Lands(19));
        Assert.Equal(64, One("(O)410", SourceKind.Forage).Lands.Lands(1));   // Fall 8 = day 56 + 8
    }

    [Fact]
    public void Snow_yam_is_winter_tilling()
    {
        ObtainSource yam = One("(O)416", SourceKind.Forage);
        Assert.Equal(85, yam.Lands.Lands(1));   // Winter 1
        Assert.Contains("tilling:outdoors off the farm", yam.Conditions.Requires);
    }

    [Fact]
    public void Cave_carrot_comes_from_mine_tilling()
    {
        ObtainSource carrot = One("(O)78", SourceKind.MineNode);
        Assert.Equal(Reliability.Dependable, carrot.Reliability);
        Assert.Contains("mines:floor 1", carrot.Conditions.Requires);
    }

    [Fact]
    public void Dinosaur_egg_from_the_cavern_is_chance()
        => Assert.Equal(Reliability.Chance, One("(O)107", SourceKind.MineNode).Reliability);

    [Fact]
    public void Quartz_lands_from_day_1_in_the_mines()
        => Assert.Equal(1, One("(O)80", SourceKind.MineNode).Lands.Lands(1));
}
```

- [ ] **Step 2: Run:** `dotnet test tests/TheLongestYear.Tests --filter ObtainabilityWorldSourcesTests`. Expected: FAIL (`WorldSources` does not exist).

- [ ] **Step 3: Implement `WorldSources.cs`:**

```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Everyday routes that live in game CODE rather than data (Jeff's ruling 2026-09-16):
/// weeds, trees, bushes, tilling and the mine floor finds. Typed from the PC 1.6 decompile; each row
/// cites its lines.</summary>
public static class WorldSources
{
    private const int SpringSalmonberryFirst = 15, SpringSalmonberryLast = 18;   // Bush.cs 229-235
    private const int FallBlackberryFirst = 8, FallBlackberryLast = 11;          // Bush.cs 236-241

    private static readonly (string ItemId, SourceKind Kind, Reliability Reliability, string Requires, string Note)[] AnyDay =
    {
        ("(O)771", SourceKind.Forage, Reliability.Dependable, "weeds:any", "fiber from cutting weeds, 50% a weed (Object.cs 1395-1397)"),
        ("(O)309", SourceKind.Forage, Reliability.Dependable, "trees:mature wild trees", "acorn from shaking or chopping an oak (Tree.cs 574-576, 940)"),
        ("(O)310", SourceKind.Forage, Reliability.Dependable, "trees:mature wild trees", "maple seed from shaking or chopping a maple (Tree.cs 574-576, 940)"),
        ("(O)311", SourceKind.Forage, Reliability.Dependable, "trees:mature wild trees", "pine cone from shaking or chopping a pine (Tree.cs 574-576, 940)"),
        ("(O)330", SourceKind.Forage, Reliability.Dependable, "tilling:outdoors", "clay from tilling outdoors, 3% a tile by default (GameLocation.cs 14222)"),
        ("(O)330", SourceKind.MineNode, Reliability.Dependable, "mines:floor 1", "clay from tilling a mine floor (MineShaft.cs 3101-3103, 3149-3152)"),
        ("(O)78", SourceKind.MineNode, Reliability.Dependable, "mines:floor 1", "cave carrot from tilling a mine floor (MineShaft.cs 3179-3181)"),
        ("(O)80", SourceKind.MineNode, Reliability.Dependable, "mines:floor 1", "quartz, a mine floor find on any floor (MineShaft.cs 3849-3851, 3936-3938)"),
        ("(O)86", SourceKind.MineNode, Reliability.Dependable, "mines:floor 1", "earth crystal, floors 1 to 39 (MineShaft.cs 3895-3897)"),
        ("(O)84", SourceKind.MineNode, Reliability.Dependable, "mines:floor 40", "frozen tear, floors 40 to 79 (MineShaft.cs 3918-3920)"),
        ("(O)82", SourceKind.MineNode, Reliability.Dependable, "mines:floor 80", "fire quartz, floors 80 to 119 (MineShaft.cs 3923-3925)"),
        ("(O)420", SourceKind.MineNode, Reliability.Dependable, "mines:floor 21", "red mushroom above floor 20, 10% a find (MineShaft.cs 3856-3858)"),
        ("(O)422", SourceKind.MineNode, Reliability.Dependable, "mines:floor 81", "purple mushroom above floor 80, 5% a find (MineShaft.cs 3852-3854)"),
        ("(O)107", SourceKind.MineNode, Reliability.Chance, MineSources.SkullCave, "dinosaur egg on the cavern's dinosaur floors, 6% a spawn (MineShaft.cs 3939-3944)"),
    };

    public static IEnumerable<(string ItemId, ObtainSource Source)> All()
    {
        foreach ((string id, SourceKind kind, Reliability reliability, string requires, string note) in AnyDay)
            yield return (id, Make(kind, DayTable.Always, reliability, requires, note));

        yield return ("(O)296", Make(SourceKind.Forage, Window(Season.Spring, SpringSalmonberryFirst, SpringSalmonberryLast),
            Reliability.Dependable, "bushes:wild", "salmonberry bushes, Spring 15 to 18 (Bush.cs 229-235, 451)"));
        yield return ("(O)410", Make(SourceKind.Forage, Window(Season.Fall, FallBlackberryFirst, FallBlackberryLast),
            Reliability.Dependable, "bushes:wild", "blackberry bushes, Fall 8 to 11 (Bush.cs 236-241, 452)"));

        DayTable winter = DayTable.InWeeks(WeekMask.ForSeason(Season.Winter));
        foreach (string id in new[] { "(O)412", "(O)416" })
            yield return (id, Make(SourceKind.Forage, winter, Reliability.Dependable, "tilling:outdoors off the farm",
                "winter root or snow yam from tilling off the farm in Winter, 8% a tile (GameLocation.cs 14212-14214)"));
    }

    private static DayTable Window(Season season, int firstDay, int lastDay)
    {
        int first = Calendar.DayOfYear((int)season, firstDay);
        int last = Calendar.DayOfYear((int)season, lastDay);
        return DayTable.Available(day => day >= first && day <= last);
    }

    private static ObtainSource Make(SourceKind kind, DayTable days, Reliability reliability, string requires, string note)
        => new(kind, days, reliability, ObtainConditions.None with { Requires = new[] { requires } }, note);
}
```

Check `Calendar.DayOfYear`'s signature before relying on it (`ObtainabilityInputs.cs` line 15 uses `Calendar.DayOfYear((int)Season, StartDay)`). `MineSources.SkullCave` is `internal`; both classes are in the same assembly.

- [ ] **Step 4: Wire it in.** In `ObtainabilityBuilder.Build`, after `direct.AddRange(CodeSources.GuildRewards(inputs.SlayerQuests));` add `direct.AddRange(WorldSources.All());`. Add `"TheLongestYear.Core/Obtainability/WorldSources.cs",` to `BlindFiles`.

- [ ] **Step 5: FairnessRule check.** `weeds:`, `trees:`, `bushes:` and `tilling:` fall through to "count as met" (FairnessRule.cs 278). That is the intent: every farm has them. No change; add one test to `FairnessRuleTests.cs`:

```csharp
[Fact]
public void Everyday_world_requirements_count_as_met()
{
    var model = Model(Route(requires: new[] { "weeds:any" }));
    Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
}
```

- [ ] **Step 6: Run the full suite, then commit and push.** `git commit -m "obtainability: weeds, trees, bushes, tilling and mine finds (Jeff's ruling)"`

---

### Task 4: Beach tide pools and the bridge

Coral (80%) and Sea Urchin (20%) spawn in the east beach's tide pools every day, at least one a day (Beach.cs 81-94), and across the whole beach on Summer 12 to 14 (Beach.cs 112-125). The tide pools are past the broken bridge (repair sets world state `beachBridgeFixed`, Beach.cs 734-736).

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/WorldSources.cs`
- Modify: `src/TheLongestYear/Loop/SaveSnapshotReader.cs:89-90` (add the bridge flag)
- Test: `tests/TheLongestYear.Tests/ObtainabilityWorldSourcesTests.cs`, `tests/TheLongestYear.Tests/FairnessRuleTests.cs`

**Interfaces:**
- Produces: requirement string `"mail:beachBridgeFixed"` (FairnessRule already checks `mail:` flags against `SaveSnapshot.MailFlags`).

- [ ] **Step 1: Failing tests** (WorldSources tests):

```csharp
[Fact]
public void Coral_is_daily_past_the_bridge_and_anywhere_on_summer_12_to_14()
{
    var coral = WorldSources.All().Where(s => s.ItemId == "(O)393").Select(s => s.Source).ToList();
    ObtainSource pools = coral.Single(s => s.Conditions.Requires.Contains("mail:beachBridgeFixed"));
    Assert.Equal(1, pools.Lands.Lands(1));
    ObtainSource summer = coral.Single(s => !s.Conditions.Requires.Contains("mail:beachBridgeFixed"));
    Assert.Equal(40, summer.Lands.Lands(1));   // Summer 12 = day 28 + 12
    Assert.All(coral, s => Assert.Equal(Reliability.Dependable, s.Reliability));
}

[Fact]
public void Sea_urchin_follows_the_same_two_routes()
    => Assert.Equal(2, WorldSources.All().Count(s => s.ItemId == "(O)397"));
```

FairnessRule test:

```csharp
[Fact]
public void A_tide_pool_route_needs_the_bridge()
{
    var model = Model(Route(requires: new[] { "mail:beachBridgeFixed" }));
    Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
    SaveSnapshot fixedBridge = SaveSnapshot.Empty with { MailFlags = new HashSet<string> { "beachBridgeFixed" } };
    Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, fixedBridge, model));
}
```

- [ ] **Step 2: Run; expect FAIL.**

- [ ] **Step 3: Implement.** In `WorldSources.All()` append:

```csharp
// Beach.cs 81-94: the east tide pools grow at least one coral (80%) or sea urchin (20%) a day,
// past the bridge (world state beachBridgeFixed, Beach.cs 734-736). Beach.cs 112-125: on Summer 12
// to 14 they also wash up anywhere on the beach.
DayTable summerTide = Window(Season.Summer, SummerTideFirst, SummerTideLast);
foreach (string id in new[] { "(O)393", "(O)397" })
{
    yield return (id, Make(SourceKind.Forage, DayTable.Always, Reliability.Dependable, BeachBridge,
        "tide pools past the beach bridge (Beach.cs 81-94)"));
    yield return (id, Make(SourceKind.Forage, summerTide, Reliability.Dependable, "location:Beach",
        "washed up anywhere on the beach, Summer 12 to 14 (Beach.cs 112-125)"));
}
```

with constants `private const int SummerTideFirst = 12, SummerTideLast = 14;` and `private const string BeachBridge = "mail:beachBridgeFixed";`.

In `SaveSnapshotReader`, after the two `mailReceived` loops, add:

```csharp
// The beach bridge is world state, not mail; the fairness rule reads both as one flag set.
if (NetWorldState.checkAnywhereForWorldStateID(BeachBridgeFixed)) mail.Add(BeachBridgeFixed);
```

with `private const string BeachBridgeFixed = "beachBridgeFixed";` and `using StardewValley.Network;` if needed.

- [ ] **Step 4: Full suite, build, commit, push.** `git commit -m "obtainability: beach tide pools, gated on the bridge"`

---

### Task 5: Recipes taught by events and letters

Tea Sapling: the Sunroom opens at Caroline 2 hearts (GameLocation.cs 8863-8871); its event (Data/Events/Sunroom `719926/t 900 1700/w sunny`) sends mail `CarolineTea`, which teaches the recipe. Wild Bait: Linus's event 26 (Data/Events/Mountain `26/f Linus 1000/...`, 4 hearts) adds the recipe (Event.cs 10463-10465). Both rows have unlock `null` in Data/CraftingRecipes, so the model marks them unresolved today.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/MadeSources.cs` (`UnlockConditions`, a code table)
- Test: `tests/TheLongestYear.Tests/ObtainabilityMadeTests.cs`

- [ ] **Step 1: Failing test:**

```csharp
[Fact]
public void Tea_sapling_and_wild_bait_are_taught_by_friendship_not_unresolved()
{
    var rows = new[]
    {
        new RecipeRow("Tea Sapling", new[] { "(O)771" }, "(O)251", "null", IsCooking: false),
        new RecipeRow("Wild Bait", new[] { "(O)771" }, "(O)774", "null", IsCooking: false),
    };
    var objects = new Dictionary<string, ObjInfo> { ["(O)771"] = new("(O)771", "Fiber", -16, 1, new List<string>(), false) };
    var snapshot = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)771"] = new[] { new ObtainSource(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None, "weeds") },
    });
    var made = MadeSources.Recipes(rows, objects, new Dictionary<string, WeekMask>(), snapshot).ToList();
    ObtainSource tea = made.First(m => m.ItemId == "(O)251").Source;
    ObtainSource bait = made.First(m => m.ItemId == "(O)774").Source;
    Assert.False(tea.Conditions.Unresolved);
    Assert.Contains("unlock:f Caroline 2", tea.Conditions.Requires);
    Assert.False(bait.Conditions.Unresolved);
    Assert.Contains("unlock:f Linus 4", bait.Conditions.Requires);
}
```

Check `MadeSources.Recipes`' parameter list (MadeSources.cs line 148-151) and adjust the call if it differs.

- [ ] **Step 2: Run; expect FAIL.**

- [ ] **Step 3: Implement.** In `MadeSources`, add:

```csharp
/// <summary>Recipes whose Data/CraftingRecipes unlock is "null" but which an event or letter
/// teaches on a friendship level (Jeff's ruling 2026-09-16). Tea Sapling: the Sunroom opens at
/// Caroline 2 hearts (GameLocation.cs 8863-8871) and its event mails CarolineTea. Wild Bait: Linus's
/// event 26 needs 4 hearts and adds the recipe (Event.cs 10463-10465).</summary>
private static readonly IReadOnlyDictionary<string, string> EventTaughtUnlocks = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["Tea Sapling"] = "f Caroline 2",
    ["Wild Bait"] = "f Linus 4",
};
```

At the top of `UnlockConditions`, before the tokens are read:

```csharp
if (EventTaughtUnlocks.TryGetValue(recipe.Name, out string? taughtBy))
    return ObtainConditions.None with { Requires = new[] { "recipe:" + recipe.Name, "unlock:" + taughtBy } };
```

`TaughtElsewhere` must return false for these two so the TV branch never replaces them: add `if (EventTaughtUnlocks.ContainsKey(recipe.Name)) return false;` as its first line.

- [ ] **Step 4: Full suite, build, commit, push.** `git commit -m "obtainability: Tea Sapling and Wild Bait are taught by friendship"`

---

### Task 6: Mystery Box after the Qi plane

No box drops until mail `sawQiPlane` (Utility.cs 6385). The plane flies after the 6th Help Wanted quest or once DaysPlayed passes 50 (Utility.cs 4409-4413), so day 51 at the latest. After that, boxes roll from trees (3%, Tree.cs 696), fishing (8% a catch, FishingRod.cs 2458), crates (BreakableContainer.cs 249), panning (Pan.cs 230) and tilling (GameLocation.cs 4392): repeatable, so dependable per ruling 1. The earlier Help Wanted route is not modelled (the table takes the forced day).

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/WorldSources.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityWorldSourcesTests.cs`

- [ ] **Step 1: Failing test:**

```csharp
[Fact]
public void Mystery_boxes_are_dependable_from_day_51()
{
    ObtainSource box = WorldSources.All().Single(s => s.ItemId == "(O)MysteryBox").Source;
    Assert.Equal(Reliability.Dependable, box.Reliability);
    Assert.Equal(51, box.Lands.Lands(1));
}
```

- [ ] **Step 2: Run; expect FAIL.**

- [ ] **Step 3: Implement** in `WorldSources.All()`:

```csharp
// Utility.cs 6385: no box before the Qi plane; Utility.cs 4409-4413: the plane flies after the 6th
// Help Wanted quest or once 50 days have passed. The quest route is earlier but not modelled.
yield return ("(O)MysteryBox", Make(SourceKind.Forage, DayTable.Available(day => day >= QiPlaneLatestDay),
    Reliability.Dependable, "trees, fishing, crates, panning or tilling",
    "mystery box finds after the Qi plane, repeatable (Tree.cs 696, FishingRod.cs 2458, BreakableContainer.cs 249, Pan.cs 230, GameLocation.cs 4392)"));
```

with `private const int QiPlaneLatestDay = 51;`. The requirement has no known prefix, so FairnessRule treats it as met.

- [ ] **Step 4: Full suite, build, commit, push.** `git commit -m "obtainability: mystery boxes from the Qi plane on"`

---

### Task 7: Animals: growing up, hatching, and owned-only produce

Rulings: produce of an animal that cannot be bought is chance from nothing (its egg is luck) and dependable only with the animal owned; every animal waits out its growing up before producing. Data (Data/FarmAnimals): `DaysToMature` (White Chicken 3, Duck 5, Rabbit 6, Dinosaur 0, cows and goats 5, Sheep 4, Pig 10, Ostrich 7); `IncubationTime` in minutes (-1 means the default; Dinosaur 18000); `EggItemIds`; `PurchasePrice` (-1 when not sold directly); `AlternatePurchaseTypes` (Brown Chicken and Brown Cow are bought through White Chicken and White Cow; confirm the field in `StardewValley.GameData.FarmAnimals` and in PurchaseAnimalsMenu.cs).

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs:93-95` (`AnimalRow`: add `DaysToMature`, `IncubationDays`, `SoldAsAlternate`)
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainTypes.cs` (`ObtainConditions.OwnedOnly`, in `Equals` and `GetHashCode`)
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityModel.cs` (`ObtainFilter.IncludeOwnedOnly`, default false)
- Modify: `src/TheLongestYear.Core/Obtainability/MadeSources.cs:214-252` (`AnimalSetup`, `Animals`)
- Modify: `src/TheLongestYear/Loop/GameObtainabilityData.cs:247-252` (read the new fields)
- Test: `tests/TheLongestYear.Tests/ObtainabilityMadeTests.cs`, `tests/TheLongestYear.Tests/ObtainabilityModelTests.cs`

**Interfaces:**
- Produces: `AnimalRow(... int DaysToMature = 0, int IncubationDays = 0, bool SoldAsAlternate = false)` (appended optional parameters so existing constructions compile); `ObtainConditions.OwnedOnly` (`bool`); `ObtainFilter.IncludeOwnedOnly` (`bool`).

- [ ] **Step 1: Failing tests** (`ObtainabilityMadeTests.cs`):

```csharp
[Fact]
public void A_bought_animal_waits_out_growing_up()
{
    var cow = new AnimalRow("White Cow", "Barn", 750, new[] { new AnimalProduce("(O)184", null, 0) },
        new AnimalProduce[0], DaysToMature: 5);
    SetupStep step = MadeSources.AnimalSetup(cow, new Dictionary<string, int>(), 0).Single(s => s.Name == "animal:White Cow");
    Assert.Equal(5, step.Days);
}

[Fact]
public void A_hatched_only_animal_is_owned_only_and_waits_for_incubation_and_growing_up()
{
    var dino = new AnimalRow("Dinosaur", "Coop", -1, new[] { new AnimalProduce("(O)107", null, 0) },
        new AnimalProduce[0], DaysToProduce: 7, DaysToMature: 0, IncubationDays: 13);
    ObtainSource egg = MadeSources.Animals(new[] { dino }, new Dictionary<string, FestivalDates>(), new Dictionary<string, int>()).Single().Source;
    Assert.True(egg.Conditions.OwnedOnly);
    Assert.Equal(13, egg.Setup.Single(s => s.Name == "animal:Dinosaur").Days);
}

[Fact]
public void An_alternate_purchase_is_sold()
{
    var brown = new AnimalRow("Brown Cow", "Barn", -1, new[] { new AnimalProduce("(O)184", null, 0) },
        new AnimalProduce[0], SoldAsAlternate: true);
    ObtainSource milk = MadeSources.Animals(new[] { brown }, new Dictionary<string, FestivalDates>(), new Dictionary<string, int>()).Single().Source;
    Assert.False(milk.Conditions.OwnedOnly);
    Assert.DoesNotContain(milk.Conditions.Requires, r => r.EndsWith("(not sold)"));
}
```

(`ObtainabilityModelTests.cs`):

```csharp
[Fact]
public void Owned_only_sources_are_left_out_unless_asked_for()
{
    var owned = new ObtainSource(SourceKind.Animal, DayTable.Always, Reliability.Dependable,
        ObtainConditions.None with { OwnedOnly = true }, "dino");
    Assert.False(ObtainFilter.DependableOnly.Accepts(owned));
    Assert.True((ObtainFilter.DependableOnly with { IncludeOwnedOnly = true }).Accepts(owned));
}
```

- [ ] **Step 2: Run; expect FAIL (compile errors count).**

- [ ] **Step 3: Implement.**
  - `ObtainConditions`: `public bool OwnedOnly { get; init; }` with a summary ("the route needs something the player already owns and cannot get dependably from nothing: a not-sold animal"), and add it to `Equals` and `GetHashCode`.
  - `ObtainFilter`: `public bool IncludeOwnedOnly { get; init; }` and `&& (IncludeOwnedOnly || !source.Conditions.OwnedOnly)` in `Accepts`.
  - `AnimalRow`: append `int DaysToMature = 0, int IncubationDays = 0, bool SoldAsAlternate = false`.
  - `MadeSources.AnimalSetup`: the animal step's days become `Math.Max(1, animal.IncubationDays + animal.DaysToMature)`.
  - `MadeSources.Animals`: `bool sold = animal.PurchasePrice > 0 || animal.SoldAsAlternate;` replaces `PurchasePrice <= 0`; when not sold, add the `(not sold)` requirement as today AND set `OwnedOnly = true` on the conditions.
  - Glue: for each animal, `DaysToMature = a.DaysToMature`; `IncubationDays = a.EggItemIds is { Count: > 0 } && !sold ? (int)Math.Ceiling((a.IncubationTime > 0 ? a.IncubationTime : DefaultIncubationMinutes) / 1440.0) : 0` with `DefaultIncubationMinutes = 9000` (confirm the default in the decompile's incubator code before typing it, and cite it); `SoldAsAlternate` = true when another animal's `AlternatePurchaseTypes` lists this id.
  - FairnessRule reads sources without a filter, so owned-only routes still count when the save owns the animal (existing `(not sold)` check). No FairnessRule change. The comparison report uses `ObtainFilter.DependableOnly`, so from nothing these routes drop out, which is the ruling.

- [ ] **Step 4: Full suite, build, commit, push.** `git commit -m "obtainability: animals grow up first; not-sold animals are owned-only"`

---

### Task 8: Desert shops need the bus

Every desert shop row counts as met today because FairnessRule only checks `location:Desert`. Shop ids in the desert (Data/Shops): `Sandy`, `DesertTrade`, `Casino`, and every `DesertFestival_*`.

**Files:**
- Modify: `src/TheLongestYear.Core/Sabotage/FairnessRule.cs:36` and the requirement loop
- Test: `tests/TheLongestYear.Tests/FairnessRuleTests.cs`

- [ ] **Step 1: Failing tests:**

```csharp
[Theory]
[InlineData("shop:DesertFestival_Vincent")]
[InlineData("shop:DesertTrade")]
[InlineData("shop:Sandy")]
[InlineData("shop:Casino")]
public void A_desert_shop_needs_the_bus(string shop)
{
    var model = Model(Route(kind: SourceKind.Shop, requires: new[] { shop }));
    Assert.False(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
    SaveSnapshot bus = SaveSnapshot.Empty with { MailFlags = new HashSet<string> { "ccVault" } };
    Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, bus, model));
}

[Fact]
public void A_town_shop_needs_nothing()
{
    var model = Model(Route(kind: SourceKind.Shop, requires: new[] { "shop:SeedShop" }));
    Assert.True(FairnessRule.Counts(Item, Hit, Deadline, DifficultyStep.Normal, SaveSnapshot.Empty, model));
}
```

- [ ] **Step 2: Run; expect the desert cases to FAIL.**

- [ ] **Step 3: Implement.** Constants:

```csharp
private const string ShopPrefix = "shop:";
/// <summary>Shops that stand in the Calico Desert (Data/Shops ids), reached only once the bus runs.</summary>
private static readonly IReadOnlySet<string> DesertShops = new HashSet<string>(StringComparer.Ordinal) { "Sandy", "DesertTrade", "Casino" };
private const string DesertFestivalShopPrefix = "DesertFestival_";
```

In the requirement loop, beside the `r == Desert` branch:

```csharp
if (r.StartsWith(ShopPrefix, StringComparison.Ordinal))
{
    string shop = r.Substring(ShopPrefix.Length);
    bool inDesert = DesertShops.Contains(shop) || shop.StartsWith(DesertFestivalShopPrefix, StringComparison.Ordinal);
    if (inDesert && !save.MailFlags.Contains(BusMail)) return $"{shop} is in the desert, which is not open";
    continue;
}
```

- [ ] **Step 4: Full suite, build, commit, push.** `git commit -m "darkness fairness: desert shops and festival stalls need the bus"`

---

### Task 9: Magic Bait is a Ginger Island item

Both Magic Bait routes run through Mr. Qi (the QiGemShop barter and his island recipe); nothing in data produces the inputs. Ruling: out of scope as an island item, not unresolved. Giving the bait an island-flagged source makes `SpawnSources`' `ThroughBait` carry the island flag (it copies the bait's flags when every bait source has them) instead of the `bait.IsEmpty` unresolved branch.

**Files:**
- Modify: `src/TheLongestYear.Core/Obtainability/CodeSources.cs`
- Modify: `src/TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs`
- Test: `tests/TheLongestYear.Tests/ObtainabilityCodeSourcesTests.cs`, `tests/TheLongestYear.Tests/ObtainabilitySpawnTests.cs`

- [ ] **Step 1: Failing tests:**

```csharp
[Fact]
public void Magic_bait_is_an_island_item()
{
    ObtainSource bait = CodeSources.MagicBait().Single().Source;
    Assert.True(bait.Conditions.GingerIsland);
    Assert.False(bait.Conditions.Unresolved);
}
```

In `ObtainabilitySpawnTests.cs`, a Beach row with `RequireMagicBait: true`, built against a snapshot model that contains `CodeSources.MagicBait()`, must come out `GingerIsland == true` and `Unresolved == false`. Model the fixture on the existing Magic Bait spawn test in that file.

- [ ] **Step 2: Run; expect FAIL.**

- [ ] **Step 3: Implement** in `CodeSources`:

```csharp
/// <summary>Magic Bait: Mr. Qi's (QiGemShop barter, his island recipe), so a Ginger Island item and
/// out of scope (Jeff's ruling 2026-09-16), not an unresolved one.</summary>
public static IEnumerable<(string ItemId, ObtainSource Source)> MagicBait()
{
    yield return ("(O)908", new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable,
        ObtainConditions.None with { GingerIsland = true, Requires = new[] { "shop:QiGemShop" } },
        "Mr. Qi's magic bait (island)"));
}
```

and `direct.AddRange(CodeSources.MagicBait());` in the builder.

- [ ] **Step 4: Full suite, build, commit, push.** `git commit -m "obtainability: magic bait is an island item"`

---

### Task 10: Live rerun, before and after, docs

**Files:**
- Modify: `TODO.md` (Obtainability phase 2 section), `STATUS.md` (top section)

- [ ] **Step 1: Deploy and rerun** (my automated run): `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`, `tly_loadsave <latest Rodger save>`, wait for `Obtainability model:`, then `tly_obtain compare obtain-compare-2026-09-16-after.md`. Quote the counts line.
- [ ] **Step 2: Diff.** Compare the before file (`obtain-compare-2026-09-16.md`) and the after file item by item: for every item whose verdict or dependable week changed, list id, name, before, after, and which task moved it.
- [ ] **Step 3: Spot checks** with `tly_obtain`: (O)815 Tea Leaves (expect about week 4), (O)393 Coral, (O)107 Dinosaur Egg (dependable none from nothing), (O)MysteryBox (week 8), (O)908 Magic Bait (island), and `tly_sabotage fair (O)151` on the throwaway save (a desert festival route must be out when the bus is not fixed).
- [ ] **Step 4: Update TODO.md and STATUS.md** with the counts, the moves and what is committed and pushed. Commit and push.
- [ ] **Step 5: Report to Jeff** with the before and after list, verbatim from the diff.
