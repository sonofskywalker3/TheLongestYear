# Year One Ending Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the black-card win with a staged ending event that plays the morning after the last bundle is donated, plants the Year 2 threads, and gates Year 2 behind a wall until it ships.

**Architecture:** Pure decision logic lives in `TheLongestYear.Core` (the only project the tests reference) as small static classes with xUnit tests. The SMAPI project gets a new `EndingEventDriver` (starts the event on the first step outside, runs the continuation), an `EndingEventInjector` (builds the vanilla event script string from i18n keys), a registered custom event command for the shrine candle, and a runtime-recoloured Morris sprite. `RunController` arms the ending at bedtime and owns the choice and the Spring 1 Year 2 wall.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony (existing patches only), xUnit 2.4, vanilla `Data/Events` command language, PC 1.6 decompile at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley\StardewValley` for reference.

**Spec:** `docs/superpowers/specs/2026-09-06-year-one-ending-design.md`

## Global Constraints

- Every commit bumps `"Version"` in `src/TheLongestYear/manifest.json` by one patch (this is `master`, the release line). The plan starts from 0.17.5; each task's commit step names the number to write.
- Commit locally only. Never push. Never release.
- No em dashes anywhere (code comments, i18n strings, docs). Use a comma, a colon, or a new sentence.
- All new player-facing text goes in `src/TheLongestYear/i18n/default.json` and is read through `Strings.Get`. No literal English in code paths that reach the screen.
- Every `except`-style catch (C# `catch`) logs through `IMonitor`; no empty catches.
- Tests: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release` must stay green. Build: `dotnet build TheLongestYear.sln -c Release` must be warning-free for new files.
- Test conventions: file-scoped `namespace TheLongestYear.Tests;`, `using TheLongestYear.Core;` + `using Xunit;`, class `<Type>Tests`, snake_case test names, `[Fact]` / `[Theory]` + `[InlineData]`.
- Dormancy: every SMAPI-side hook returns early when `RunActivation.IsActive` is false.
- No `/sdcard/` paths anywhere.

---

## File map

**Core (tested):**
- `src/TheLongestYear.Core/Ending/WinNightRule.cs`: when bedtime becomes the win night; tomorrow's date.
- `src/TheLongestYear.Core/Ending/EndingMorningDecider.cs`: snapshot in, action out (like `IntroSequenceDecider`).
- `src/TheLongestYear.Core/Ending/Year2WallRule.cs`: should the wall show this morning.
- `src/TheLongestYear.Core/VillagerMemory.cs`: per-villager counts record + rollup (extends `FamiliarityRollup`).
- `src/TheLongestYear.Core/Ending/EndingSpeaker.cs`: pick the speaker.
- `src/TheLongestYear.Core/Ending/EndingLine.cs`: tier + i18n key + scene table + voice overrides.
- `src/TheLongestYear.Core/Ending/EndingCast.cs`: what the injector needs.
- `MetaState.cs`, `RunState.cs`: new fields.

**SMAPI:**
- `src/TheLongestYear/Integration/EndingEventInjector.cs`: the script.
- `src/TheLongestYear/Integration/EndingEventDriver.cs`: start, watch, continue.
- `src/TheLongestYear/Integration/GrandpaCandleCommand.cs`: `tlyGrandpaCandle` event command.
- `src/TheLongestYear/Integration/MorrisDarkSprite.cs`: `Characters/Morris_Dark` asset.
- `src/TheLongestYear/Integration/FamiliarityGlue.cs`: birthday + heart event ids + loop number.
- `src/TheLongestYear/Loop/RunController.cs`: win night, choice, keep-playing lines, wall.
- `src/TheLongestYear/Loop/EventSuppressionPatch.cs`: 191393.
- `src/TheLongestYear/Loop/WorldResetService.cs`: zero `grandpaScore`.
- `src/TheLongestYear/ModEntry.cs`: wiring, commands.
- Deleted: `src/TheLongestYear/UI/VictoryMenu.cs`; `Day28Branch.Win` removed.

---

### Task 1: State fields and the win-night rule

**Files:**
- Modify: `src/TheLongestYear.Core/RunState.cs` (after `DoubleProduceToday`, ~line 150)
- Modify: `src/TheLongestYear.Core/MetaState.cs` (after `VictoryAcknowledged`, line 184)
- Create: `src/TheLongestYear.Core/Ending/WinNightRule.cs`
- Test: `tests/TheLongestYear.Tests/WinNightRuleTests.cs`

**Interfaces:**
- Produces: `RunState.EndingArmed : bool`; `MetaState.EndingSeen : bool`, `MetaState.Year2WallArmed : bool`, `MetaState.Year2Started : bool`; `WinNightRule.ShouldArm(bool fullCcDone, bool victoryAcknowledged, bool endingArmed) : bool`; `WinNightRule.Tomorrow(int dayOfMonth, int monthIndex) : (int day, int monthIndex)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class WinNightRuleTests
{
    [Theory]
    [InlineData(true, false, false, true)]   // board done, first time: arm
    [InlineData(true, true, false, false)]   // already acknowledged a win: silent
    [InlineData(true, false, true, false)]   // already armed (festival defer): do not re-arm
    [InlineData(false, false, false, false)] // board not done
    public void ShouldArm_only_on_a_fresh_completed_board(bool done, bool acked, bool armed, bool expected)
        => Assert.Equal(expected, WinNightRule.ShouldArm(done, acked, armed));

    [Theory]
    [InlineData(1, 0, 2, 0)]
    [InlineData(27, 0, 28, 0)]
    [InlineData(28, 0, 1, 1)]
    [InlineData(28, 3, 1, 0)]   // Winter 28 rolls to Spring (month 0)
    public void Tomorrow_rolls_the_month_on_28(int day, int month, int expDay, int expMonth)
        => Assert.Equal((expDay, expMonth), WinNightRule.Tomorrow(day, month));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~WinNightRuleTests"`
Expected: build error, `WinNightRule` not found.

- [ ] **Step 3: Add the fields and the rule**

`RunState.cs`, after `DoubleProduceToday`:
```csharp
    /// <summary>Year One Ending (spec 2026-09-06): set at bedtime on the night the board completes.
    /// The EndingEventDriver plays the ending the first time the player steps onto the farm the next
    /// morning, then clears it. Survives a quit overnight.</summary>
    public bool EndingArmed { get; set; }
```

`MetaState.cs`, after `VictoryAcknowledged`:
```csharp
    /// <summary>Year One Ending: the staged ending event has played once on this save. A later win
    /// skips the event and goes straight to the shrine and the choice.</summary>
    public bool EndingSeen { get; set; }

    /// <summary>Year One Ending: the player chose Keep playing on this version or later. Spring 1 of
    /// year 2 shows the "Year 2 is coming" wall until <see cref="Year2Started"/> is set by the Year 2
    /// update. Cleared, with VictoryAcknowledged, when the wall's Loop again runs.</summary>
    public bool Year2WallArmed { get; set; }

    /// <summary>Reserved for the Year 2 update. Never set by this version.</summary>
    public bool Year2Started { get; set; }
```

`src/TheLongestYear.Core/Ending/WinNightRule.cs`:
```csharp
namespace TheLongestYear.Core.Ending;

/// <summary>Year One Ending (spec 2026-09-06 §1): the night the board completes is the win night,
/// whatever the date. Pure; RunController.OnDayEnding supplies the inputs.</summary>
public static class WinNightRule
{
    public static bool ShouldArm(bool fullCcDone, bool victoryAcknowledged, bool endingArmed)
        => fullCcDone && !victoryAcknowledged && !endingArmed;

    /// <summary>Tomorrow's (day, monthIndex); day 28 rolls to day 1 of the next month, Winter to Spring.</summary>
    public static (int day, int monthIndex) Tomorrow(int dayOfMonth, int monthIndex)
        => Calendar.IsMonthEnd(dayOfMonth)
            ? (1, (monthIndex + 1) % Calendar.MonthsPerYear)
            : (dayOfMonth + 1, monthIndex);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~WinNightRuleTests"`
Expected: 8 passed.

- [ ] **Step 5: Commit (manifest 0.17.6)**

```bash
git add src/TheLongestYear.Core/RunState.cs src/TheLongestYear.Core/MetaState.cs src/TheLongestYear.Core/Ending/WinNightRule.cs tests/TheLongestYear.Tests/WinNightRuleTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.17.6: ending state fields and the win-night rule (board complete on any date arms the ending)"
```

---

### Task 2: Villager memory counts (Core rollup)

**Files:**
- Create: `src/TheLongestYear.Core/VillagerMemory.cs`
- Modify: `src/TheLongestYear.Core/FamiliarityRollup.cs` (whole file, 33 lines)
- Modify: `src/TheLongestYear.Core/MetaState.cs` (after `VillagerFamiliarity`, line 246)
- Test: `tests/TheLongestYear.Tests/VillagerMemoryTests.cs`

**Interfaces:**
- Consumes: `MetaState.VillagerFamiliarity`.
- Produces: `VillagerDaySignals(string Npc, bool Talked, int Gifts, int HeartEvents, bool BirthdayGift, IReadOnlyList<string> HeartEventIds)` (record, extended); `VillagerMemory` class with `Talks, Gifts, BirthdayGifts, HeartEvents : int`, `Loops : List<int>`, `BirthdayGiftLoops : List<int>`, `GiftLoops : List<int>`, `HeartEventLoops : Dictionary<string, List<int>>`; `MetaState.VillagerMemory : Dictionary<string, VillagerMemory>`; `FamiliarityRollup.Apply(MetaState meta, IEnumerable<VillagerDaySignals> signals, int loopNumber) : int`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class VillagerMemoryTests
{
    private static VillagerDaySignals Day(string npc, bool talked = false, int gifts = 0, int hearts = 0,
        bool birthday = false, params string[] heartIds)
        => new(npc, talked, gifts, hearts, birthday, heartIds);

    [Fact]
    public void Counts_and_score_move_together()
    {
        var m = new MetaState();
        FamiliarityRollup.Apply(m, new[] { Day("Pierre", talked: true, gifts: 1) }, loopNumber: 1);
        Assert.Equal(4, m.VillagerFamiliarity["Pierre"]);
        var mem = m.VillagerMemory["Pierre"];
        Assert.Equal(1, mem.Talks);
        Assert.Equal(1, mem.Gifts);
        Assert.Equal(new List<int> { 1 }, mem.Loops);
        Assert.Equal(new List<int> { 1 }, mem.GiftLoops);
    }

    [Fact]
    public void Birthday_gift_records_the_loop_once()
    {
        var m = new MetaState();
        FamiliarityRollup.Apply(m, new[] { Day("Pierre", gifts: 1, birthday: true) }, 2);
        FamiliarityRollup.Apply(m, new[] { Day("Pierre", gifts: 1, birthday: true) }, 2);
        FamiliarityRollup.Apply(m, new[] { Day("Pierre", gifts: 1, birthday: true) }, 3);
        var mem = m.VillagerMemory["Pierre"];
        Assert.Equal(3, mem.BirthdayGifts);
        Assert.Equal(new List<int> { 2, 3 }, mem.BirthdayGiftLoops);
    }

    [Fact]
    public void Heart_events_track_loops_per_event_id()
    {
        var m = new MetaState();
        FamiliarityRollup.Apply(m, new[] { Day("Abigail", hearts: 1, heartIds: "4") }, 1);
        FamiliarityRollup.Apply(m, new[] { Day("Abigail", hearts: 1, heartIds: "4") }, 3);
        Assert.Equal(new List<int> { 1, 3 }, m.VillagerMemory["Abigail"].HeartEventLoops["4"]);
        Assert.Equal(2, m.VillagerMemory["Abigail"].HeartEvents);
    }

    [Fact]
    public void A_silent_day_writes_nothing()
    {
        var m = new MetaState();
        FamiliarityRollup.Apply(m, new[] { Day("Pierre") }, 1);
        Assert.False(m.VillagerMemory.ContainsKey("Pierre"));
        Assert.False(m.VillagerFamiliarity.ContainsKey("Pierre"));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~VillagerMemoryTests"`
Expected: build error (record arity, `VillagerMemory` missing).

- [ ] **Step 3: Implement**

`src/TheLongestYear.Core/VillagerMemory.cs`:
```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Year One Ending (spec 2026-09-06 §6): what the save actually remembers about one villager
/// across every loop, beside the familiarity score. Lists keep JSON round-tripping simple; the rollup
/// enforces uniqueness on the loop lists.</summary>
public sealed class VillagerMemory
{
    public int Talks { get; set; }
    public int Gifts { get; set; }
    public int BirthdayGifts { get; set; }
    public int HeartEvents { get; set; }
    /// <summary>Loop numbers (CompletedResets + 1) in which the player dealt with this villager.</summary>
    public List<int> Loops { get; set; } = new();
    public List<int> GiftLoops { get; set; } = new();
    public List<int> BirthdayGiftLoops { get; set; } = new();
    /// <summary>Heart event id to the loops it was seen in.</summary>
    public Dictionary<string, List<int>> HeartEventLoops { get; set; } = new();

    internal static void AddLoop(List<int> loops, int loop)
    {
        if (!loops.Contains(loop)) loops.Add(loop);
    }
}
```

`MetaState.cs`, after `VillagerFamiliarity`:
```csharp
    /// <summary>Year One Ending: per-villager counts behind the score (talks, gifts, birthday gifts,
    /// heart events, and the loops each happened in). Feeds the ending's assembled line.</summary>
    public Dictionary<string, VillagerMemory> VillagerMemory { get; set; } = new();
```

`FamiliarityRollup.cs` (replace the file):
```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One villager's interaction signals for a single day, read from the live friendship data.
/// <paramref name="BirthdayGift"/>: a gift today on the villager's birthday. <paramref name="HeartEventIds"/>:
/// the relationship event ids first seen today.</summary>
public sealed record VillagerDaySignals(string Npc, bool Talked, int Gifts, int HeartEvents,
    bool BirthdayGift = false, IReadOnlyList<string>? HeartEventIds = null);

/// <summary>Nightly rollup of interaction into <see cref="MetaState.VillagerFamiliarity"/> (deja-vu
/// dialogue spec 2026-08-27) and <see cref="MetaState.VillagerMemory"/> (ending spec 2026-09-06).
/// Pure; the glue gathers the signals from Game1.</summary>
public static class FamiliarityRollup
{
    public const int TalkPoints = 1;
    public const int GiftPoints = 3;
    public const int HeartEventPoints = 10;

    /// <summary>Adds each villager's points and counts for the day. Returns the total points added.
    /// A villager with zero points gets no entry, so the dictionaries only list people the player
    /// has dealt with.</summary>
    public static int Apply(MetaState meta, IEnumerable<VillagerDaySignals> signals, int loopNumber)
    {
        int total = 0;
        foreach (VillagerDaySignals s in signals)
        {
            int points = (s.Talked ? TalkPoints : 0) + s.Gifts * GiftPoints + s.HeartEvents * HeartEventPoints;
            if (points <= 0) continue;
            meta.VillagerFamiliarity.TryGetValue(s.Npc, out int current);
            meta.VillagerFamiliarity[s.Npc] = current + points;
            total += points;

            if (!meta.VillagerMemory.TryGetValue(s.Npc, out VillagerMemory? mem))
                meta.VillagerMemory[s.Npc] = mem = new VillagerMemory();
            if (s.Talked) mem.Talks++;
            mem.Gifts += s.Gifts;
            mem.HeartEvents += s.HeartEvents;
            VillagerMemory.AddLoop(mem.Loops, loopNumber);
            if (s.Gifts > 0) VillagerMemory.AddLoop(mem.GiftLoops, loopNumber);
            if (s.BirthdayGift && s.Gifts > 0)
            {
                mem.BirthdayGifts++;
                VillagerMemory.AddLoop(mem.BirthdayGiftLoops, loopNumber);
            }
            foreach (string id in s.HeartEventIds ?? System.Array.Empty<string>())
            {
                if (!mem.HeartEventLoops.TryGetValue(id, out List<int>? loops))
                    mem.HeartEventLoops[id] = loops = new List<int>();
                VillagerMemory.AddLoop(loops, loopNumber);
            }
        }
        return total;
    }
}
```

Any existing caller of the two-argument `Apply` (only `FamiliarityGlue.Rollup`) now fails to compile; Task 3 fixes it. Existing `FamiliarityRollupTests` (if present) pass `loopNumber: 1`; update them.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~VillagerMemoryTests|FullyQualifiedName~FamiliarityRollupTests"`
Expected: all passed. (The SMAPI project does not build until Task 3; the test project references Core only.)

- [ ] **Step 5: Commit (manifest 0.17.7)**

```bash
git add src/TheLongestYear.Core/VillagerMemory.cs src/TheLongestYear.Core/FamiliarityRollup.cs src/TheLongestYear.Core/MetaState.cs tests/TheLongestYear.Tests/VillagerMemoryTests.cs tests/TheLongestYear.Tests/FamiliarityRollupTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.17.7: villager memory counts beside the familiarity score (talks, gifts, birthday gifts, heart events per loop)"
```

---

### Task 3: Glue: birthday gifts, heart event ids, loop number

**Files:**
- Modify: `src/TheLongestYear/Integration/FamiliarityGlue.cs` (whole file, 50 lines)

**Interfaces:**
- Consumes: `FamiliarityRollup.Apply(meta, signals, loopNumber)`, `RelationshipEventIndex.NpcFor(string)`.
- Produces: nothing new; `Rollup(MetaState, RunState, IMonitor)` keeps its signature.

- [ ] **Step 1: Rewrite the signal gathering**

Replace the body of `Rollup` from `var heartEventsToday` down to the `Apply` call:

```csharp
            var previous = new HashSet<string>(run.EventsSeenAtDayStart);
            var heartEventsToday = new Dictionary<string, List<string>>();
            foreach (string id in p.eventsSeen)
            {
                if (previous.Contains(id)) continue;
                string npc = RelationshipEventIndex.NpcFor(id);
                if (npc == null) continue;
                if (!heartEventsToday.TryGetValue(npc, out List<string> ids))
                    heartEventsToday[npc] = ids = new List<string>();
                ids.Add(id);
            }

            string seasonKey = Game1.currentSeason;   // "spring" .. "winter", the same key NPC.Birthday_Season uses
            int day = Game1.dayOfMonth;
            var signals = new List<VillagerDaySignals>();
            foreach (string name in p.friendshipData.Keys)
            {
                Friendship f = p.friendshipData[name];
                heartEventsToday.TryGetValue(name, out List<string> ids);
                NPC npc = Game1.getCharacterFromName(name);
                bool birthday = npc != null
                    && string.Equals(npc.Birthday_Season, seasonKey, System.StringComparison.OrdinalIgnoreCase)
                    && npc.Birthday_Day == day;
                signals.Add(new VillagerDaySignals(name, f.TalkedToToday, f.GiftsToday, ids?.Count ?? 0,
                    BirthdayGift: birthday && f.GiftsToday > 0, HeartEventIds: ids));
            }

            int loopNumber = meta.CompletedResets + 1;
            int added = FamiliarityRollup.Apply(meta, signals, loopNumber);
```

Keep the rest of the method (the `EventsSeenAtDayStart` refresh and the trace log) as it is.

- [ ] **Step 2: Build**

Run: `dotnet build TheLongestYear.sln -c Release`
Expected: 0 errors, no new warnings.

- [ ] **Step 3: Run the whole test suite**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release`
Expected: all passed.

- [ ] **Step 4: Commit (manifest 0.17.8)**

```bash
git add src/TheLongestYear/Integration/FamiliarityGlue.cs src/TheLongestYear/manifest.json
git commit -m "v0.17.8: nightly rollup records birthday gifts, heart event ids and the loop number"
```

---

### Task 4: Speaker pick

**Files:**
- Create: `src/TheLongestYear.Core/Ending/EndingSpeaker.cs`
- Test: `tests/TheLongestYear.Tests/EndingSpeakerTests.cs`

**Interfaces:**
- Consumes: `MetaState.VillagerFamiliarity`, `MetaState.VillagerMemory`, `GameplayConfig.DejaVuThreshold`.
- Produces: `EndingSpeaker.Pick(MetaState meta, int threshold, Func<string, bool> isEligibleInGame) : string?` where `isEligibleInGame(name)` is true when the villager exists, is not the spouse and is not a child (the glue supplies it).

- [ ] **Step 1: Write the failing tests**

```csharp
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class EndingSpeakerTests
{
    private static MetaState Meta(params (string npc, int fam, int loops)[] rows)
    {
        var m = new MetaState();
        foreach (var (npc, fam, loops) in rows)
        {
            m.VillagerFamiliarity[npc] = fam;
            var mem = new VillagerMemory();
            for (int i = 1; i <= loops; i++) mem.Loops.Add(i);
            m.VillagerMemory[npc] = mem;
        }
        return m;
    }

    [Fact]
    public void Highest_score_at_or_above_threshold_wins()
        => Assert.Equal("Pierre", EndingSpeaker.Pick(Meta(("Pierre", 90, 1), ("Robin", 70, 1)), 60, _ => true));

    [Fact]
    public void Nobody_below_threshold()
        => Assert.Null(EndingSpeaker.Pick(Meta(("Pierre", 59, 3)), 60, _ => true));

    [Fact]
    public void Ties_break_on_loops_then_name()
    {
        Assert.Equal("Robin", EndingSpeaker.Pick(Meta(("Pierre", 80, 1), ("Robin", 80, 2)), 60, _ => true));
        Assert.Equal("Pierre", EndingSpeaker.Pick(Meta(("Pierre", 80, 2), ("Robin", 80, 2)), 60, _ => true));
    }

    [Fact]
    public void Ineligible_villagers_are_skipped()
        => Assert.Equal("Robin", EndingSpeaker.Pick(Meta(("Pierre", 90, 1), ("Robin", 70, 1)), 60, n => n != "Pierre"));

    [Fact]
    public void Score_only_save_without_memory_still_qualifies()
    {
        var m = new MetaState();
        m.VillagerFamiliarity["Gus"] = 100;
        Assert.Equal("Gus", EndingSpeaker.Pick(m, 60, _ => true));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~EndingSpeakerTests"`
Expected: build error, `EndingSpeaker` missing.

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Linq;

namespace TheLongestYear.Core.Ending;

/// <summary>Year One Ending (spec 2026-09-06 §6): the villager who steps forward at the ceremony.
/// Highest familiarity at or above the deja-vu threshold, ties by most loops known, then by name.
/// <paramref name="isEligibleInGame"/> answers "present, not the spouse, not a child".</summary>
public static class EndingSpeaker
{
    public static string? Pick(MetaState meta, int threshold, Func<string, bool> isEligibleInGame)
    {
        if (threshold <= 0) threshold = 1;
        return meta.VillagerFamiliarity
            .Where(kv => kv.Value >= threshold && isEligibleInGame(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .ThenByDescending(kv => meta.VillagerMemory.TryGetValue(kv.Key, out var mem) ? mem.Loops.Count : 0)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key)
            .FirstOrDefault();
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~EndingSpeakerTests"`
Expected: 5 passed.

- [ ] **Step 5: Commit (manifest 0.17.9)**

```bash
git add src/TheLongestYear.Core/Ending/EndingSpeaker.cs tests/TheLongestYear.Tests/EndingSpeakerTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.17.9: ending speaker pick (highest familiarity, ties by loops, exclusions supplied by the glue)"
```

---

### Task 5: The assembled line (tiers, scene table, voice overrides)

**Files:**
- Create: `src/TheLongestYear.Core/Ending/EndingLine.cs`
- Test: `tests/TheLongestYear.Tests/EndingLineTests.cs`
- Modify: `src/TheLongestYear/i18n/default.json` (new keys, see Step 3)
- Create: `docs/superpowers/specs/2026-09-06-year-one-ending-lines.md` (every line, for Jeff's review)

**Interfaces:**
- Consumes: `VillagerMemory`.
- Produces: `enum EndingLineTier { Talks = 4, Gifts = 3, HeartEvent = 2, BirthdayGift = 1 }`; `EndingLine.Tier(VillagerMemory? mem, out string? sceneEventId) : EndingLineTier`; `EndingLine.MiddleKey(string npc, EndingLineTier tier) : string` (i18n key, voice override aware); `EndingLine.SceneKey(string eventId) : string?`; `EndingLine.SceneTable : IReadOnlyDictionary<string, string>` (event id to i18n scene key). Fixed keys: `event.ending.crack.open`, `event.ending.crack.close`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class EndingLineTests
{
    private static VillagerMemory Mem(int[]? bday = null, int[]? gifts = null, (string id, int[] loops)[]? hearts = null)
    {
        var m = new VillagerMemory();
        if (bday != null) m.BirthdayGiftLoops.AddRange(bday);
        if (gifts != null) m.GiftLoops.AddRange(gifts);
        if (hearts != null) foreach (var (id, loops) in hearts) m.HeartEventLoops[id] = new List<int>(loops);
        return m;
    }

    [Fact]
    public void Birthday_in_two_loops_is_tier_one()
        => Assert.Equal(EndingLineTier.BirthdayGift, EndingLine.Tier(Mem(bday: new[] { 1, 2 }), out _));

    [Fact]
    public void Birthday_in_one_loop_does_not_count()
        => Assert.Equal(EndingLineTier.Talks, EndingLine.Tier(Mem(bday: new[] { 2 }), out _));

    [Fact]
    public void Heart_event_needs_two_loops_and_a_scene_entry()
    {
        string knownId = EndingLine.SceneTable.Keys.First();
        Assert.Equal(EndingLineTier.HeartEvent, EndingLine.Tier(Mem(hearts: new[] { (knownId, new[] { 1, 3 }) }), out string? scene));
        Assert.Equal(knownId, scene);
        Assert.Equal(EndingLineTier.Talks, EndingLine.Tier(Mem(hearts: new[] { ("999999", new[] { 1, 3 }) }), out _));
        Assert.Equal(EndingLineTier.Talks, EndingLine.Tier(Mem(hearts: new[] { (knownId, new[] { 1 }) }), out _));
    }

    [Fact]
    public void Gifts_in_two_loops_is_tier_three()
        => Assert.Equal(EndingLineTier.Gifts, EndingLine.Tier(Mem(gifts: new[] { 1, 2 }), out _));

    [Fact]
    public void Score_only_save_is_tier_four()
        => Assert.Equal(EndingLineTier.Talks, EndingLine.Tier(null, out _));

    [Fact]
    public void Keys_use_voice_override_when_present()
    {
        Assert.Equal("event.ending.crack.tier1.Shane", EndingLine.MiddleKey("Shane", EndingLineTier.BirthdayGift));
        Assert.Equal("event.ending.crack.tier1", EndingLine.MiddleKey("Pierre", EndingLineTier.BirthdayGift));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~EndingLineTests"`
Expected: build error.

- [ ] **Step 3: Implement the Core class**

```csharp
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Ending;

public enum EndingLineTier { BirthdayGift = 1, HeartEvent = 2, Gifts = 3, Talks = 4 }

/// <summary>Year One Ending (spec 2026-09-06 §6): the speaker's middle sentence is the strongest fact
/// that is true of the save. Keys are i18n keys; the injector reads them through Strings.Get.</summary>
public static class EndingLine
{
    public const string OpenKey = "event.ending.crack.open";
    public const string CloseKey = "event.ending.crack.close";
    private const string MiddlePrefix = "event.ending.crack.tier";
    private const int LoopsNeeded = 2;

    /// <summary>Heart event id to the i18n key of its scene phrase ("we [scene]"). Ids come from
    /// Data/Events; RelationshipEventIndex already maps them to villagers. An id missing here falls
    /// through to the gifts tier. Verified against decompiled/content/Data/Events at build.</summary>
    public static readonly IReadOnlyDictionary<string, string> SceneTable = new Dictionary<string, string>
    {
        ["4"]  = "event.ending.scene.abigail-2",     // Abigail 2 hearts, video game in Pierre's
        ["6"]  = "event.ending.scene.abigail-6",     // Abigail 6 hearts, graveyard flute
        ["11"] = "event.ending.scene.penny-2",       // Penny 2 hearts, George's package
        ["13"] = "event.ending.scene.haley-2",       // Haley 2 hearts, the bracelet
        ["27"] = "event.ending.scene.sam-2",         // Sam 2 hearts, the skateboard
        ["36"] = "event.ending.scene.sebastian-2",   // Sebastian 2 hearts, the frog
        ["40"] = "event.ending.scene.leah-2",        // Leah 2 hearts, the sculpture
        ["46"] = "event.ending.scene.maru-2",        // Maru 2 hearts, the telescope
        ["55"] = "event.ending.scene.elliott-2",     // Elliott 2 hearts, the cabin reading
        ["58"] = "event.ending.scene.harvey-2",      // Harvey 2 hearts, the check-up
        ["63"] = "event.ending.scene.alex-2",        // Alex 2 hearts, gridball on the beach
        ["79"] = "event.ending.scene.emily-2",       // Emily 2 hearts, the saloon dance
        ["94"] = "event.ending.scene.shane-2",       // Shane 2 hearts, the dock
    };

    /// <summary>Villagers whose voice would not survive the generic line get their own key set
    /// (suffix appended to the tier key).</summary>
    public static readonly IReadOnlySet<string> VoiceOverrides =
        new HashSet<string> { "Shane", "George", "Haley", "Abigail", "Wizard" };

    public static EndingLineTier Tier(VillagerMemory? mem, out string? sceneEventId)
    {
        sceneEventId = null;
        if (mem == null) return EndingLineTier.Talks;
        if (mem.BirthdayGiftLoops.Count >= LoopsNeeded) return EndingLineTier.BirthdayGift;
        string? scene = mem.HeartEventLoops
            .Where(kv => kv.Value.Count >= LoopsNeeded && SceneTable.ContainsKey(kv.Key))
            .Select(kv => kv.Key)
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .FirstOrDefault();
        if (scene != null) { sceneEventId = scene; return EndingLineTier.HeartEvent; }
        if (mem.GiftLoops.Count >= LoopsNeeded) return EndingLineTier.Gifts;
        return EndingLineTier.Talks;
    }

    public static string MiddleKey(string npc, EndingLineTier tier)
        => VoiceOverrides.Contains(npc) ? $"{MiddlePrefix}{(int)tier}.{npc}" : $"{MiddlePrefix}{(int)tier}";

    public static string? SceneKey(string eventId)
        => SceneTable.TryGetValue(eventId, out string? key) ? key : null;
}
```

Verify every id in `SceneTable` before committing: the Android clone carries the game content. Run

```bash
cd "C:/Users/Jeff/Documents/Projects/Stardee Valoo/decompiled" && ls content/Data/Events 2>/dev/null | head; grep -o '"\(4\|6\|11\|13\|27\|36\|40\|46\|55\|58\|63\|79\|94\)/[^"]*"' content/Data/Events/*.json 2>/dev/null | head -30
```

If the content is XNB rather than JSON, use `RelationshipEventIndex` (already in Core) as the authority: every id in `SceneTable` must satisfy `RelationshipEventIndex.NpcFor(id) == <villager named in the comment>`. Add this test to `EndingLineTests`:

```csharp
    [Fact]
    public void Every_scene_id_belongs_to_a_villager()
    {
        foreach (string id in EndingLine.SceneTable.Keys)
            Assert.NotNull(RelationshipEventIndex.NpcFor(id));
    }
```

Drop any id that fails; the table may shrink, the tiers still work.

- [ ] **Step 4: Add the i18n keys**

In `src/TheLongestYear/i18n/default.json`, after the `event.intro.*` block:

```json
    "event.ending.crack.open": "I know something is going on here that I can't understand.#$b#I keep remembering conversations with you that never happened.",
    "event.ending.crack.tier1": "You've only been here a year, but I remember you bringing me a birthday present twice.",
    "event.ending.crack.tier2": "You've only been here a year, but I remember the day we {{scene}}, and I remember it twice.",
    "event.ending.crack.tier3": "You've only been here a year, but I remember you bringing me things you couldn't have known I liked.",
    "event.ending.crack.tier4": "You've only been here a year, but I remember talking with you before you ever arrived.",
    "event.ending.crack.close": "I don't know how, but I want to help.",
    "event.ending.crack.tier1.Shane": "You've been here a year. So why do I remember you turning up with a birthday present two years running?",
    "event.ending.crack.tier2.Shane": "You've been here a year. So why do I remember the day we {{scene}} like it happened twice?",
    "event.ending.crack.tier3.Shane": "You've been here a year. So why do I remember you bringing me stuff you had no business knowing I liked?",
    "event.ending.crack.tier4.Shane": "You've been here a year. So why do I remember talking to you before you ever showed up?",
    "event.ending.crack.tier1.George": "Only been here a year, you say. Then explain why I remember two birthday presents from you. Two.",
    "event.ending.crack.tier2.George": "Only been here a year, you say. Then explain why I remember the day we {{scene}} twice over.",
    "event.ending.crack.tier3.George": "Only been here a year, you say. Then explain why I remember you bringing me things nobody told you about.",
    "event.ending.crack.tier4.George": "Only been here a year, you say. Then explain why I remember talking to you before you got here.",
    "event.ending.crack.tier1.Haley": "You've only been here a year, right? Because I totally remember you bringing me a birthday present twice.",
    "event.ending.crack.tier2.Haley": "You've only been here a year, right? Because I totally remember the day we {{scene}}. Twice.",
    "event.ending.crack.tier3.Haley": "You've only been here a year, right? Because I totally remember you bringing me things you couldn't have known I'd like.",
    "event.ending.crack.tier4.Haley": "You've only been here a year, right? Because I totally remember talking to you before you moved here.",
    "event.ending.crack.tier1.Abigail": "You've only been here a year... but I swear I remember you bringing me a birthday present twice. That's so weird.",
    "event.ending.crack.tier2.Abigail": "You've only been here a year... but I swear I remember the day we {{scene}} happening twice. That's so weird.",
    "event.ending.crack.tier3.Abigail": "You've only been here a year... but I swear you kept bringing me things you couldn't have known I liked.",
    "event.ending.crack.tier4.Abigail": "You've only been here a year... but I swear I remember talking with you before you ever arrived.",
    "event.ending.crack.tier1.Wizard": "You have been here a single year. Yet I hold the memory of two birthday gifts from your hand. Curious.",
    "event.ending.crack.tier2.Wizard": "You have been here a single year. Yet I hold the memory of the day we {{scene}}, and I hold it twice. Curious.",
    "event.ending.crack.tier3.Wizard": "You have been here a single year. Yet you brought me things no mortal could have known I favour. Curious.",
    "event.ending.crack.tier4.Wizard": "You have been here a single year. Yet I recall our conversations from before you set foot in this valley. Curious.",
    "event.ending.scene.abigail-2": "played that game in the shop",
    "event.ending.scene.abigail-6": "sat in the graveyard with the flute",
    "event.ending.scene.penny-2": "took that package to George",
    "event.ending.scene.haley-2": "found my bracelet",
    "event.ending.scene.sam-2": "fixed the skateboard trick",
    "event.ending.scene.sebastian-2": "found the frog by the lake",
    "event.ending.scene.leah-2": "looked at the sculpture",
    "event.ending.scene.maru-2": "looked through the telescope",
    "event.ending.scene.elliott-2": "read in the cabin",
    "event.ending.scene.harvey-2": "did the check-up",
    "event.ending.scene.alex-2": "threw the gridball on the beach",
    "event.ending.scene.emily-2": "danced at the saloon",
    "event.ending.scene.shane-2": "sat on the dock",
```

Any scene id dropped in Step 3 loses its `event.ending.scene.*` key too.

- [ ] **Step 5: Write the lines file for Jeff**

`docs/superpowers/specs/2026-09-06-year-one-ending-lines.md`: a Markdown table of every `event.ending.*`, `dialog.ending.*` and `dialog.year2wall.*` key with its English text (this task adds the crack lines; Task 9 appends the rest). Header: "Status: pending Jeff's review; every line here goes past him before release."

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~EndingLineTests"`
Expected: 7 passed.

- [ ] **Step 7: Commit (manifest 0.17.10)**

```bash
git add src/TheLongestYear.Core/Ending/EndingLine.cs tests/TheLongestYear.Tests/EndingLineTests.cs src/TheLongestYear/i18n/default.json docs/superpowers/specs/2026-09-06-year-one-ending-lines.md src/TheLongestYear/manifest.json
git commit -m "v0.17.10: the crack line assembled from tracked facts (four tiers, scene table, five voice overrides)"
```

---

### Task 6: Cast and the morning decider

**Files:**
- Create: `src/TheLongestYear.Core/Ending/EndingCast.cs`
- Create: `src/TheLongestYear.Core/Ending/EndingMorningDecider.cs`
- Test: `tests/TheLongestYear.Tests/EndingMorningDeciderTests.cs`

**Interfaces:**
- Produces: `EndingCast(string? Speaker, string? SpeakerMiddleKey, string? SceneKey, IReadOnlyList<string> Crowd, int ShrineX, int ShrineY)`; `EndingCast.DefaultCrowd : IReadOnlyList<string>`; `EndingSnapshot(bool Armed, bool WorldReady, bool OnFarm, bool Busy, bool FestivalToday, bool SeenMailPresent, bool StartedThisMorning)`; `enum EndingAction { None, Start, Finish, ReArm }`; `EndingMorningDecider.Next(EndingSnapshot) : EndingAction`.

- [ ] **Step 1: Write the failing tests**

```csharp
using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class EndingMorningDeciderTests
{
    private static EndingSnapshot S(bool armed = true, bool ready = true, bool onFarm = true, bool busy = false,
        bool festival = false, bool seen = false, bool started = false)
        => new(armed, ready, onFarm, busy, festival, seen, started);

    [Fact] public void Not_armed_does_nothing() => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(armed: false)));
    [Fact] public void Waits_for_the_farm() => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(onFarm: false)));
    [Fact] public void Waits_while_busy() => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(busy: true)));
    [Fact] public void Never_on_a_festival() => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(festival: true)));
    [Fact] public void Starts_when_clear() => Assert.Equal(EndingAction.Start, EndingMorningDecider.Next(S()));
    [Fact] public void Finishes_when_seen_mail_lands_and_event_is_over()
        => Assert.Equal(EndingAction.Finish, EndingMorningDecider.Next(S(seen: true, started: true)));
    [Fact] public void Finishes_even_if_started_flag_was_lost_after_a_reload()
        => Assert.Equal(EndingAction.Finish, EndingMorningDecider.Next(S(seen: true, started: false)));
    [Fact] public void Rearms_when_the_event_ended_without_the_mail()
        => Assert.Equal(EndingAction.ReArm, EndingMorningDecider.Next(S(seen: false, started: true)));
    [Fact] public void Waits_while_event_runs()
        => Assert.Equal(EndingAction.None, EndingMorningDecider.Next(S(busy: true, started: true)));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~EndingMorningDeciderTests"`
Expected: build error.

- [ ] **Step 3: Implement**

`EndingCast.cs`:
```csharp
using System.Collections.Generic;

namespace TheLongestYear.Core.Ending;

/// <summary>Everything the ending script needs that depends on the save. Built by the driver each
/// morning it starts the event.</summary>
public sealed record EndingCast(
    string? Speaker,
    string? SpeakerMiddleKey,
    string? SceneKey,
    IReadOnlyList<string> Crowd,
    int ShrineX,
    int ShrineY)
{
    /// <summary>The townsfolk on the hall steps, in placement order. The driver drops anyone the game
    /// cannot find. The speaker is placed separately and removed from this list if present.</summary>
    public static readonly IReadOnlyList<string> DefaultCrowd = new[]
    {
        "Lewis", "Robin", "Pierre", "Caroline", "Marnie", "Gus", "Emily", "Evelyn", "Penny", "Jas", "Vincent", "Linus",
    };
}
```

`EndingMorningDecider.cs`:
```csharp
namespace TheLongestYear.Core.Ending;

public sealed record EndingSnapshot(
    bool Armed, bool WorldReady, bool OnFarm, bool Busy, bool FestivalToday, bool SeenMailPresent, bool StartedThisMorning);

public enum EndingAction { None, Start, Finish, ReArm }

/// <summary>Year One Ending (spec 2026-09-06 §2): the driver's per-tick decision. Busy = any event,
/// farm event, queued warp or open menu. Pure.</summary>
public static class EndingMorningDecider
{
    public static EndingAction Next(EndingSnapshot s)
    {
        if (!s.Armed || !s.WorldReady) return EndingAction.None;
        if (s.SeenMailPresent && !s.Busy) return EndingAction.Finish;
        if (s.Busy) return EndingAction.None;
        if (s.StartedThisMorning) return EndingAction.ReArm;   // event ended, no seen mail
        if (!s.OnFarm || s.FestivalToday) return EndingAction.None;
        return EndingAction.Start;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~EndingMorningDeciderTests"`
Expected: 9 passed.

- [ ] **Step 5: Commit (manifest 0.17.11)**

```bash
git add src/TheLongestYear.Core/Ending/EndingCast.cs src/TheLongestYear.Core/Ending/EndingMorningDecider.cs tests/TheLongestYear.Tests/EndingMorningDeciderTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.17.11: ending cast record and the morning decider"
```

---

### Task 7: The shrine candle command and its reset

**Files:**
- Create: `src/TheLongestYear/Integration/GrandpaCandleCommand.cs`
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs` (the CC block near line 290-342; add after line 338)
- Modify: `src/TheLongestYear/ModEntry.cs` `Entry` (near line 134, before the drivers)

**Interfaces:**
- Produces: `GrandpaCandleCommand.Name = "tlyGrandpaCandle"`; `GrandpaCandleCommand.Register(IMonitor)`.

- [ ] **Step 1: Write the command**

```csharp
using StardewModdingAPI;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>Year One Ending (spec 2026-09-06 §4 scene 6): lights ONE candle at grandpa's shrine the
    /// way vanilla's grandpaCandles event command does (Event.cs GrandpaCandles: set Farm.grandpaScore,
    /// one fireball sound per candle, Farm.addGrandpaCandles), with the count fixed at one instead of
    /// computed from the player's score. The farm relights from grandpaScore on every load, so the
    /// candle stays lit until the reset zeroes it.</summary>
    internal static class GrandpaCandleCommand
    {
        public const string Name = "tlyGrandpaCandle";
        private const int Candles = 1;

        public static void Register(IMonitor monitor)
        {
            Event.RegisterCommand(Name, (evt, args, context) =>
            {
                try
                {
                    Farm farm = Game1.getFarm();
                    farm.grandpaScore.Value = Candles;
                    Game1.playSound("fireball");
                    farm.addGrandpaCandles();
                }
                catch (System.Exception ex)
                {
                    monitor.Log($"{Name}: could not light the shrine candle ({ex.GetType().Name}: {ex.Message}); the scene continues.", LogLevel.Warn);
                }
                evt.CurrentCommand++;
            });
        }
    }
}
```

- [ ] **Step 2: Register it in `ModEntry.Entry`**

Immediately before `_introInjector = new IntroEventInjector(this.Monitor, _meta);`:
```csharp
            GrandpaCandleCommand.Register(this.Monitor);
```

- [ ] **Step 3: Zero the score on reset**

`WorldResetService.cs`, after line 338 (`Game1.MasterPlayer.mailForTomorrow.Remove("abandonedJojaMartAccessible");`), still inside the `if (cc != null)` block? No: outside it, right after the block closes, add:
```csharp
            // Year One Ending: the ending lights one shrine candle (Farm.grandpaScore = 1). A loop
            // starts with the shrine dark again; vanilla's year-3 judgement also keys off zero.
            Game1.getFarm().grandpaScore.Value = 0;
```

- [ ] **Step 4: Build**

Run: `dotnet build TheLongestYear.sln -c Release`
Expected: 0 errors. If `Event.RegisterCommand` does not resolve, the game reference is older than 1.6; check `<Reference>` paths in `src/TheLongestYear/TheLongestYear.csproj` against the PC decompile's `Event.cs:4014` signature `public static void RegisterCommand(string name, EventCommandDelegate action)`.

- [ ] **Step 5: Commit (manifest 0.17.12)**

```bash
git add src/TheLongestYear/Integration/GrandpaCandleCommand.cs src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.17.12: tlyGrandpaCandle event command (one candle, vanilla's own relight); reset darkens the shrine"
```

---

### Task 8: Morris with red eyes

**Files:**
- Create: `src/TheLongestYear/Integration/MorrisDarkSprite.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (`Entry`: subscribe; commands: `tly_dumpsprite`)

**Interfaces:**
- Produces: asset `Characters/Morris_Dark` (what `changeSprite Morris Dark` loads); `MorrisDarkSprite.OnAssetRequested(object, AssetRequestedEventArgs)`; console `tly_dumpsprite <Name>` writes `<modfolder>/test-output/sprite-<Name>.png`.

- [ ] **Step 1: Write the loader**

```csharp
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace TheLongestYear.Integration
{
    /// <summary>Year One Ending (spec 2026-09-06 §4 scene 4): Morris's sheet with the eyes recoloured
    /// red, built at load from the game's own Characters/Morris so no copyrighted sheet ships in the
    /// mod. The event command <c>changeSprite Morris Dark</c> loads Characters/Morris_Dark.</summary>
    internal sealed class MorrisDarkSprite
    {
        public const string AssetName = "Characters/Morris_Dark";
        private const string SourceAsset = "Characters/Morris";
        // Morris's iris pixels on the vanilla sheet. Find them with `tly_dumpsprite Morris` and an
        // image viewer's colour picker, then set the exact RGB here. A wrong value recolours nothing
        // (the scene still plays; the glow carries the beat).
        private static readonly Color EyeColour = new Color(0, 0, 0);
        private static readonly Color RedEye = new Color(220, 20, 20);
        private readonly IMonitor _monitor;

        public MorrisDarkSprite(IMonitor monitor) => _monitor = monitor;

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo(AssetName)) return;
            e.LoadFrom(Build, AssetLoadPriority.Medium);
        }

        private Texture2D Build()
        {
            Texture2D source = Game1.content.Load<Texture2D>(SourceAsset);
            var pixels = new Color[source.Width * source.Height];
            source.GetData(pixels);
            int changed = 0;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] == EyeColour) { pixels[i] = RedEye; changed++; }
            }
            var result = new Texture2D(Game1.graphics.GraphicsDevice, source.Width, source.Height);
            result.SetData(pixels);
            _monitor.Log($"Morris_Dark: recoloured {changed} pixel(s).", changed == 0 ? LogLevel.Warn : LogLevel.Trace);
            return result;
        }
    }
}
```

- [ ] **Step 2: Wire it and add the dump command**

`ModEntry.Entry`, next to the other `AssetRequested` subscriptions (line ~161):
```csharp
            _morrisDark = new MorrisDarkSprite(this.Monitor);
            helper.Events.Content.AssetRequested += _morrisDark.OnAssetRequested;
```
Field: `private MorrisDarkSprite _morrisDark;`

Command registration (with the other `helper.ConsoleCommands.Add` lines):
```csharp
            helper.ConsoleCommands.Add("tly_dumpsprite", "Write Characters/<Name> to test-output/sprite-<Name>.png so its colours can be read (debug). Usage: tly_dumpsprite Morris", this.CmdDumpSprite);
```
Handler:
```csharp
        private void CmdDumpSprite(string command, string[] args)
        {
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_dumpsprite <Name>", LogLevel.Warn); return; }
            try
            {
                var tex = Game1.content.Load<Microsoft.Xna.Framework.Graphics.Texture2D>("Characters/" + args[0]);
                string dir = System.IO.Path.Combine(this.Helper.DirectoryPath, "test-output");
                System.IO.Directory.CreateDirectory(dir);
                string path = System.IO.Path.Combine(dir, $"sprite-{args[0]}.png");
                using var fs = System.IO.File.Create(path);
                tex.SaveAsPng(fs, tex.Width, tex.Height);
                this.Monitor.Log($"Wrote {path} ({tex.Width}x{tex.Height}).", LogLevel.Info);
            }
            catch (Microsoft.Xna.Framework.Content.ContentLoadException ex)
            {
                this.Monitor.Log($"No sprite named {args[0]}: {ex.Message}", LogLevel.Warn);
            }
        }
```

- [ ] **Step 3: Find the eye colour**

Deploy (`tools/deploy.ps1 -Minimized`), load any save over the bridge, send `tly_dumpsprite Morris`, open `test-output/sprite-Morris.png` at 800% zoom, read the iris pixel RGB in frame 0 (the down-facing idle, top-left 16x32), and set `EyeColour` to it. Then trigger a reload of the asset (`patch reload` is not available; restart the game) and confirm the log line says `recoloured N pixel(s)` with N > 0.

- [ ] **Step 4: Build and commit (manifest 0.17.13)**

Run: `dotnet build TheLongestYear.sln -c Release` (0 errors).
```bash
git add src/TheLongestYear/Integration/MorrisDarkSprite.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.17.13: Characters/Morris_Dark built at load (red irises); tly_dumpsprite"
```
`test-output/` stays uncommitted (it is local scratch).

---

### Task 9: The ending script and its lines

**Files:**
- Create: `src/TheLongestYear/Integration/EndingEventInjector.cs`
- Modify: `src/TheLongestYear/i18n/default.json`
- Modify: `docs/superpowers/specs/2026-09-06-year-one-ending-lines.md` (append)

**Interfaces:**
- Consumes: `EndingCast`, `EndingLine.OpenKey/CloseKey`, `GrandpaCandleCommand.Name`, `Strings.Get`.
- Produces: `EndingEventKeys.EventId = "sonofskywalker3.TLY.Ending"`, `EndingEventKeys.SeenMail = "tly_ending_seen"`; `EndingEventInjector.Build(EndingCast cast) : string`.

- [ ] **Step 1: Write the injector**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;

namespace TheLongestYear.Integration
{
    internal static class EndingEventKeys
    {
        public const string EventId = "sonofskywalker3.TLY.Ending";
        public const string SeenMail = "tly_ending_seen";
    }

    /// <summary>Year One Ending (spec 2026-09-06 §4): the six-scene script, built the way
    /// IntroEventInjector builds the intro. Started by EndingEventDriver on the Farm, so scene 1 needs
    /// no changeLocation. Town tiles are fixed and known, so actors may walk there; on the Farm they
    /// are placed by warp. Not skippable: the last line adds the seen mail the driver waits for.</summary>
    internal static class EndingEventInjector
    {
        // Town: the Community Center doors are the warp at (52,20); the steps run along row 22.
        private const int HallX = 52, HallY = 22;

        internal static string Build(EndingCast cast)
        {
            var s = new List<string>
            {
                "junimoStarSong",
                "66 18",
                "farmer 66 18 2",

                // ---- Scene 1: the porch (Standard-farm tiles; the game offsets per farm type) ----
                "warp farmer 66 18 true",
                "addTemporaryActor Lewis 16 32 68 18 3 true Character",
                "viewport 66 18 true",
                "faceDirection farmer 1",
                "pause 800",
                $"speak Lewis \"{Strings.Get("event.ending.lewis-porch-1")}\"",
                "pause 200",
                $"speak Lewis \"{Strings.Get("event.ending.lewis-porch-2")}\"",
                "pause 400",
                "globalFade",

                // ---- Scene 2: the hall steps ----
                "changeLocation Town",
                $"warp farmer {HallX} {HallY + 2} true",
                "faceDirection farmer 0",
            };
            int col = HallX - 5;
            foreach (string name in cast.Crowd)
            {
                if (name == cast.Speaker) continue;
                s.Add($"addTemporaryActor {name} 16 32 {col} {HallY} 2 true Character");
                col += (col == HallX - 1) ? 3 : 1;   // leave the centre for the speaker
            }
            if (cast.Speaker != null)
                s.Add($"addTemporaryActor {cast.Speaker} 16 32 {HallX} {HallY} 2 true Character");
            for (int j = 0; j < 4; j++)
                s.Add($"addTemporaryActor Junimo 16 16 {HallX - 3 + j * 2} {HallY - 4} 2 false character Junimo{j}");
            s.AddRange(new[]
            {
                $"viewport {HallX} {HallY} true",
                "globalFadeIn",
                "pause 600",
                "playSound reward",
                "screenFlash 0.4",
                "jump Junimo0 8", "jump Junimo1 8", "jump Junimo2 8", "jump Junimo3 8",
                "playSound junimoMeep1",
                "pause 800",
                $"speak Lewis \"{Strings.Get("event.ending.lewis-hall-1")}\"",
                "pause 200",
                $"speak Lewis \"{Strings.Get("event.ending.lewis-hall-2")}\"",
                "pause 200",
                $"speak Lewis \"{Strings.Get("event.ending.lewis-hall-3")}\"",
                "pause 600",
            });

            // ---- Scene 3: the crack (only with a speaker) ----
            if (cast.Speaker != null && cast.SpeakerMiddleKey != null)
            {
                var tokens = new Dictionary<string, string>
                {
                    ["scene"] = cast.SceneKey != null ? Strings.Get(cast.SceneKey) : string.Empty,
                };
                s.AddRange(new[]
                {
                    $"move {cast.Speaker} 0 1 2",
                    "pause 400",
                    $"speak {cast.Speaker} \"{Strings.Get(EndingLine.OpenKey)}\"",
                    $"emote {cast.Speaker} 8",
                    "pause 900",
                    $"speak {cast.Speaker} \"{Strings.Get(cast.SpeakerMiddleKey, tokens)}\"",
                    "pause 400",
                    $"speak {cast.Speaker} \"{Strings.Get(EndingLine.CloseKey)}\"",
                    "pause 600",
                });
            }

            // ---- Scene 4: Morris ----
            s.AddRange(new[]
            {
                "stopMusic",
                $"addTemporaryActor Morris 16 32 {HallX + 9} {HallY + 2} 3 true Character",
                $"move Morris -5 0 3",
                "pause 500",
                $"speak Morris \"{Strings.Get("event.ending.morris-1")}\"",
                "pause 200",
                $"speak Morris \"{Strings.Get("event.ending.morris-2")}\"",
                "pause 200",
                $"speak Morris \"{Strings.Get("event.ending.morris-3")}\"",
                "pause 300",
                "changeSprite Morris Dark",
                "glow 90 0 0 false",
                "playSound shadowDie",
                $"speak Morris \"{Strings.Get("event.ending.morris-4")}\"",
                "pause 700",
                "stopGlowing",
                "changeSprite Morris",
                "pause 300",
                "jump Junimo0 4", "jump Junimo2 4",
                "playSound junimoMeep1",
                $"move Morris 5 0 1",
                "pause 400",
                "playSound doorClose",
                "pause 300",
                "playSound thudStep",
                "pause 800",
                "globalFade",

                // ---- Scene 5: inside the hall, six Junimos ----
                "changeLocation CommunityCenter",
                "warp farmer 32 16 true",
                "faceDirection farmer 0",
            });
            for (int j = 0; j < 6; j++)
                s.Add($"addTemporaryActor Junimo 16 16 {28 + j * 2} {11 + (j % 2)} 2 false character Junimo{j}");
            s.AddRange(new[]
            {
                "viewport 32 14 true",
                "globalFadeIn",
                "playSound junimoMeep1",
                "jump Junimo0 8", "jump Junimo1 8", "jump Junimo2 8", "jump Junimo3 8", "jump Junimo4 8", "jump Junimo5 8",
                "pause 800",
                $"speak Junimo0 \"{Strings.Get("event.ending.junimo-1")}\"",
                "pause 200",
                $"speak Junimo0 \"{Strings.Get("event.ending.junimo-2")}\"",
                "pause 400",
                $"speak Junimo0 \"{Strings.Get("event.ending.junimo-3")}\"",
                "pause 300",
                $"speak Junimo0 \"{Strings.Get("event.ending.junimo-4")}\"",
                "pause 300",
                $"speak Junimo0 \"{Strings.Get("event.ending.junimo-5")}\"",
                "pause 600",
                "globalFade",

                // ---- Scene 6: the shrine at dusk ----
                "changeLocation Farm",
                $"warp farmer {cast.ShrineX + 1} {cast.ShrineY + 2} true",
                "faceDirection farmer 0",
                $"viewport {cast.ShrineX} {cast.ShrineY} true",
                "ambientLight 120 100 160",
                "globalFadeIn",
                "pause 1200",
                GrandpaCandleCommand.Name,
                "pause 1500",
                $"message \"{Strings.Get("event.ending.grandpa")}\"",
                "pause 1500",
                "globalFade",
                $"addMailReceived {EndingEventKeys.SeenMail}",
                "end",
            });
            return string.Join("/", s);
        }
    }
}
```

Two build-time checks the executor must do before committing: (1) `addTemporaryActor ... character Junimo0` names the actor `Junimo0`; if the game rejects a suffixed name for the Junimo sprite, name them `Junimo` and address `jump`/`speak` to `Junimo` (one jumps, all speak through the first). (2) `move <actor> dx dy facing` walks that many tiles; if Town tiles at `(HallX+4..+9, HallY+2)` are blocked on the current map, place Morris by `warp` at `(HallX+4, HallY+2)` and drop both `move` lines. Record either change in the lines file's notes.

- [ ] **Step 2: Add the i18n keys**

```json
    "event.ending.lewis-porch-1": "@! There you are. Come quick, you have to see this.$h",
    "event.ending.lewis-porch-2": "The Community Center... it's lit up. The whole town is out there. I don't understand it, but come on!",
    "event.ending.lewis-hall-1": "Everyone, everyone... I still can't explain what happened here overnight.#$b#But this building is ours again.",
    "event.ending.lewis-hall-2": "And we all know whose hands did the work this year. Three cheers for our farmer!$h",
    "event.ending.lewis-hall-3": "Pelican Town has its heart back. Thank you.",
    "event.ending.morris-1": "Congratulations. Genuinely. Joja values a competitor who can deliver.",
    "event.ending.morris-2": "I'm here to let you all know that Joja will be closing its Pelican Town location, effective today.",
    "event.ending.morris-3": "Our survey crews found an iridium deposit in the Skull Cavern. It will fund a resort on Ginger Island. This lease was never worth the paperwork.",
    "event.ending.morris-4": "I wish you all the best. Nothing is going to slow this down now.",
    "event.ending.junimo-1": "You did it, @! The hall is whole, and so are we.$h",
    "event.ending.junimo-2": "We sang all night. We haven't sung like that in a very long time.",
    "event.ending.junimo-3": "But... did you see the man from Joja? The thing that has him did not leave. It only moved.",
    "event.ending.junimo-4": "And the town still forgets. Every one of them. You saw the crack in it today, though. Didn't you?",
    "event.ending.junimo-5": "You've done well so far, but the work isn't over. Prepare yourself for what's next.#$b#On Spring 1, we get to work freeing the townsfolk.",
    "event.ending.grandpa": "You've started what I couldn't finish. I'm so proud, but you must keep going.",
```

Note `junimo-5` carries the "keep playing" promise inside the event; Task 11's keep-playing dialogue repeats the second half so the player who loops again has still heard it.

- [ ] **Step 3: Append the lines to the review file**

Add every key from Step 2 to `docs/superpowers/specs/2026-09-06-year-one-ending-lines.md` in scene order.

- [ ] **Step 4: Build and commit (manifest 0.17.14)**

Run: `dotnet build TheLongestYear.sln -c Release` (0 errors).
```bash
git add src/TheLongestYear/Integration/EndingEventInjector.cs src/TheLongestYear/i18n/default.json docs/superpowers/specs/2026-09-06-year-one-ending-lines.md src/TheLongestYear/manifest.json
git commit -m "v0.17.14: the ending event script (porch, hall steps, the crack, Morris, six Junimos, the shrine candle)"
```

---

### Task 10: The morning driver and the ceremony suppression

**Files:**
- Create: `src/TheLongestYear/Integration/EndingEventDriver.cs`
- Modify: `src/TheLongestYear/Loop/EventSuppressionPatch.cs` (`SuppressedEventIds`, lines 37-46)
- Modify: `src/TheLongestYear/ModEntry.cs` (`Entry` wiring after `_day28Driver`)

**Interfaces:**
- Consumes: `EndingMorningDecider`, `EndingSpeaker`, `EndingLine`, `EndingCast`, `EndingEventInjector.Build`, `RunState.EndingArmed`, `MetaState.EndingSeen`.
- Produces: `EndingEventDriver(IMonitor, MetaStore, GameplayConfig)`; `Attach(IModHelper, Func<RunController>)`; `StartNow(string? forcedSpeaker)` (debug replay, no continuation); the driver calls `RunController.OnEndingFinished()` (Task 11 adds it).

- [ ] **Step 1: Write the driver**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Ending;
using TheLongestYear.Loop;

namespace TheLongestYear.Integration
{
    /// <summary>Year One Ending (spec 2026-09-06 §2): starts the ending event the first time the player
    /// steps onto the farm on an armed morning, waits for the seen mail, then hands the continuation
    /// to RunController. Decision logic is EndingMorningDecider; this is the Game1 glue.</summary>
    internal sealed class EndingEventDriver
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private readonly GameplayConfig _config;
        private Func<RunController> _runController;
        private bool _started;
        private bool _replayOnly;
        private int _cooldownUntilTick;

        public EndingEventDriver(IMonitor monitor, MetaStore meta, GameplayConfig config)
        {
            _monitor = monitor; _meta = meta; _config = config;
        }

        public void Attach(IModHelper helper, Func<RunController> runController)
        {
            _runController = runController;
            helper.Events.GameLoop.DayStarted += (s, e) => { _started = false; _replayOnly = false; };
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!RunActivation.IsActive || !_config.Enabled) return;
            if (!Context.IsWorldReady || Game1.currentMinigame != null) return;
            if (Game1.fadeToBlackAlpha > 0f || Game1.ticks < _cooldownUntilTick) return;
            Farmer p = Game1.player;
            if (p == null) return;

            bool busy = Game1.eventUp || Game1.eventOver || Game1.currentLocation?.currentEvent != null
                        || Game1.farmEvent != null || Game1.locationRequest != null || Game1.activeClickableMenu != null
                        || Game1.newDay;
            var snap = new EndingSnapshot(
                Armed: _meta.Run.EndingArmed || _replayOnly,
                WorldReady: true,
                OnFarm: Game1.currentLocation is Farm,
                Busy: busy,
                FestivalToday: Utility.isFestivalDay(),
                SeenMailPresent: p.mailReceived.Contains(EndingEventKeys.SeenMail),
                StartedThisMorning: _started);

            switch (EndingMorningDecider.Next(snap))
            {
                case EndingAction.Start:
                    Start(forcedSpeaker: null);
                    break;
                case EndingAction.Finish:
                    p.mailReceived.Remove(EndingEventKeys.SeenMail);   // transient signal, never persisted
                    _started = false;
                    if (_replayOnly)
                    {
                        _replayOnly = false;
                        _monitor.Log("Ending: replay finished (no continuation).", LogLevel.Info);
                        break;
                    }
                    _meta.Run.EndingArmed = false;
                    _meta.State.EndingSeen = true;
                    _meta.Save();
                    _monitor.Log("Ending: event finished, running the continuation.", LogLevel.Info);
                    _runController?.Invoke()?.OnEndingFinished();
                    break;
                case EndingAction.ReArm:
                    _monitor.Log("Ending: the event ended without its seen flag; it will play again on the next step outside.", LogLevel.Warn);
                    _started = false;
                    Bump();
                    break;
            }
        }

        /// <summary>Debug replay from tly_ending: plays the event now, in the current location, and
        /// runs no continuation. <paramref name="forcedSpeaker"/> overrides the pick.</summary>
        public void StartNow(string forcedSpeaker)
        {
            _replayOnly = true;
            Start(forcedSpeaker);
        }

        private void Start(string forcedSpeaker)
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null) return;
            EndingCast cast = BuildCast(forcedSpeaker);
            _monitor.Log($"Ending: starting (speaker={cast.Speaker ?? "none"}, crowd={cast.Crowd.Count}, shrine={cast.ShrineX},{cast.ShrineY}).", LogLevel.Info);
            loc.startEvent(new Event(EndingEventInjector.Build(cast), null, EndingEventKeys.EventId));
            _started = true;
            Bump();
        }

        private EndingCast BuildCast(string forcedSpeaker)
        {
            Farmer p = Game1.player;
            Func<string, bool> eligible = name =>
            {
                NPC npc = Game1.getCharacterFromName(name);
                if (npc == null || npc is StardewValley.Characters.Child) return false;
                if (p.spouse != null && p.spouse == name) return false;
                return true;
            };
            string speaker = forcedSpeaker != null && eligible(forcedSpeaker)
                ? forcedSpeaker
                : EndingSpeaker.Pick(_meta.State, _config.DejaVuThreshold, eligible);
            string middleKey = null, sceneKey = null;
            if (speaker != null)
            {
                _meta.State.VillagerMemory.TryGetValue(speaker, out VillagerMemory mem);
                EndingLineTier tier = EndingLine.Tier(mem, out string sceneId);
                middleKey = EndingLine.MiddleKey(speaker, tier);
                sceneKey = sceneId != null ? EndingLine.SceneKey(sceneId) : null;
            }
            List<string> crowd = EndingCast.DefaultCrowd.Where(n => Game1.getCharacterFromName(n) != null).ToList();
            Microsoft.Xna.Framework.Point shrine = Game1.getFarm().GetGrandpaShrinePosition();
            return new EndingCast(speaker, middleKey, sceneKey, crowd, shrine.X, shrine.Y);
        }

        private void Bump() => _cooldownUntilTick = Game1.ticks + 30;
    }
}
```

- [ ] **Step 2: Suppress the vanilla ceremony**

`EventSuppressionPatch.SuppressedEventIds`: add `"191393",` and replace the `// NOT 191393 ...` comment lines with:
```csharp
            // 191393 is the CC COMPLETION ceremony. Since the Year One Ending (0.17.x) the mod plays
            // its own ending in that slot; the keep-playing branch adds 191393 to eventsSeen itself so
            // the post-completion world (Joja shutdown, Pierre Wednesdays, abandoned-JojaMart
            // lightning) still flips exactly as vanilla flips it. 0.11.60 once suppressed it WITHOUT
            // that hand-off and froze the world (Nexus bug 1113630); the hand-off is the fix.
            "191393",
```
Also update the class summary line that mentions 191393 to match.

- [ ] **Step 3: Wire in `ModEntry.Entry`**

After `_day28Driver.Attach(helper, () => _runController);`:
```csharp
            _endingDriver = new EndingEventDriver(this.Monitor, _meta, _config);
            _endingDriver.Attach(helper, () => _runController);
```
Field: `private EndingEventDriver _endingDriver;`

- [ ] **Step 4: Build**

Run: `dotnet build TheLongestYear.sln -c Release`
Expected: one error, `RunController.OnEndingFinished` missing. Task 11 adds it; do not commit yet if the build is red. If you must commit separately, add a temporary stub `public void OnEndingFinished() { }` to `RunController` and replace it in Task 11.

- [ ] **Step 5: Commit (manifest 0.17.15)**

```bash
git add src/TheLongestYear/Integration/EndingEventDriver.cs src/TheLongestYear/Loop/EventSuppressionPatch.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/manifest.json
git commit -m "v0.17.15: EndingEventDriver (start on the first step outside, finish on the seen mail, re-arm on interruption); vanilla ceremony 191393 suppressed"
```

---

### Task 11: RunController: the win night, the choice, keep playing

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs` (OnDayEnding 877-946; ShowKeepPlayingChoice/ApplyKeepPlaying 300-344; OnCutsceneEnded 591-634; DebugForceWin 666)
- Modify: `src/TheLongestYear.Core/Day28/Day28Branch.cs` (remove `Win`)
- Modify: `src/TheLongestYear/Integration/Day28CutsceneDriver.cs` (line 108-110, the `branch == Day28Branch.Win` ternary)
- Delete: `src/TheLongestYear/UI/VictoryMenu.cs`
- Modify: `src/TheLongestYear/i18n/default.json` (`dialog.win.*` replaced by `dialog.ending.*`)
- Modify: `src/TheLongestYear/ModEntry.cs` (`tly_win` description; new `tly_ending`)

**Interfaces:**
- Consumes: `WinNightRule`, `RunState.EndingArmed`, `MetaState.EndingSeen/Year2WallArmed`, `EndingEventDriver.StartNow`.
- Produces: `RunController.OnEndingFinished()`, `RunController.ArmEnding(string reason)`.

- [ ] **Step 1: The win night in `OnDayEnding`**

Replace the `case RunAction.Win:` block with:
```csharp
                case RunAction.Win:
                    // Winter 28 with a complete board: the same win night as any other date (below).
                    break;
```
and, immediately after the `switch (action) { ... }`, add:
```csharp
            // Year One Ending (spec 2026-09-06 §1): the night the board completes is the win night,
            // whatever the date. RunAction.Win (Winter 28) lands here too.
            bool boardDone = BundleGate.IsFullyDone(Run.DonatedLedger(), _requirements);
            if (action != RunAction.FailReset
                && TheLongestYear.Core.Ending.WinNightRule.ShouldArm(boardDone, _store.State.VictoryAcknowledged, Run.EndingArmed))
            {
                ArmEnding("board complete");
            }
            else if (Run.EndingArmed)
            {
                ForceTomorrowSunny();   // a festival deferred us; keep tomorrow clear too
            }
```
Check the exact `BundleGate.IsFullyDone` signature in `src/TheLongestYear.Core/RunManager.cs:53` (`BundleGate.IsFullyDone(donated, bundles)` where `bundles` is the requirement list the RunManager receives) and pass the same arguments RunManager passes. If `_requirements` is not the same type, expose the value from `RunManager.EvaluateDayEnd` instead: add `public bool LastFullCcDone { get; private set; }` to `RunManager`, set it beside `fullCcDone`, and read `_runManager.LastFullCcDone` here.

New members:
```csharp
        /// <summary>Arm the Year One Ending for tomorrow morning: pity pass for the season, the run
        /// flag, and a sunny forecast for the Town scene. Idempotent.</summary>
        public void ArmEnding(string reason)
        {
            if (Run.EndingArmed) return;
            if (_store.State.EndingSeen)
            {
                // The event already played on this save: a later win goes straight to the shrine
                // and the choice on the wake frame (spec §1, "once per save, choice every time").
                _monitor.Log($"Win night ({reason}): ending already seen, queuing shrine + choice for the morning.", LogLevel.Info);
                _pendingChoice = true;
                return;
            }
            SeasonPity.RecordPass(_store.State, Run.Season, _config);
            Run.EndingArmed = true;
            ForceTomorrowSunny();
            _monitor.Log($"Win night ({reason}): ending armed for tomorrow morning, weather forced sunny.", LogLevel.Info);
        }

        private bool _pendingChoice;

        private void ForceTomorrowSunny()
        {
            const string sunny = "Sun";
            Game1.weatherForTomorrow = sunny;
            Game1.netWorldState.Value.WeatherForTomorrow = sunny;
            Game1.netWorldState.Value.GetWeatherForLocation("Default").WeatherForTomorrow = sunny;
        }
```
`_pendingChoice` is consumed in `OnDayStarted`: before `DoDayStartSeasonAndHub();` at the end of `OnDayStarted`, add:
```csharp
            if (_pendingChoice)
            {
                _pendingChoice = false;
                TryOpenShrineThenContinue(ShowEndingChoice);
                return;
            }
```

- [ ] **Step 2: The continuation**

Replace `ShowKeepPlayingChoice` and `ApplyKeepPlaying` with:
```csharp
        /// <summary>Year One Ending (spec §5): after the event and the shrine spend. Loop again resets
        /// right here; Keep playing marks the win, hands vanilla its post-completion world, arms the
        /// Spring 1 year-2 wall, and lets the Junimos say what comes next.</summary>
        public void OnEndingFinished() => TryOpenShrineThenContinue(ShowEndingChoice);

        private void ShowEndingChoice()
        {
            var responses = new[]
            {
                new StardewValley.Response("newLoop",     Strings.Get("dialog.ending.new-loop")),
                new StardewValley.Response("keepPlaying", Strings.Get("dialog.ending.keep-playing")),
            };
            string prompt = Strings.Get("dialog.ending.prompt",
                new Dictionary<string, string> { ["loopline"] = WinSummary.LoopLine(Run.RunNumber) });
            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                _monitor.Log("Ending choice: no currentLocation, defaulting to Keep playing.", LogLevel.Warn);
                ApplyKeepPlaying();
                return;
            }
            loc.createQuestionDialogue(prompt, responses, (Farmer who, string key) =>
            {
                if (key == "newLoop")
                {
                    _monitor.Log("Ending choice: Loop again.", LogLevel.Info);
                    ContinueAfterResetSpend();
                }
                else
                {
                    _monitor.Log("Ending choice: Keep playing.", LogLevel.Info);
                    ApplyKeepPlaying();
                }
            });
        }

        private void ApplyKeepPlaying()
        {
            _store.State.VictoryAcknowledged = true;
            _store.State.Year2WallArmed = true;
            if (!Game1.player.eventsSeen.Contains("191393"))
                Game1.player.eventsSeen.Add("191393");   // vanilla's post-completion world (spec §3)
            _store.Save();
            var lines = new List<string>
            {
                Strings.Get("dialog.ending.keep-1"),
                Strings.Get("dialog.ending.keep-2"),
            };
            Game1.afterDialogues = () => { Game1.afterDialogues = null; DoDayStartSeasonAndHub(); };
            Game1.activeClickableMenu = new StardewValley.Menus.DialogueBox(lines);
        }
```

- [ ] **Step 3: Remove the Win branch**

- `Day28Branch.cs`: delete the `Win` member and its comment.
- `RunController.OnCutsceneEnded`: delete the `case Day28Branch.Win:` block.
- `RunController.DebugForceWin`: body becomes `ArmEnding("tly_win");` with the log line "tly_win: arming the ending for tomorrow morning (sleep, then step outside)."
- `Day28CutsceneDriver.cs` lines 108-110: replace the ternary with `Game1.activeClickableMenu = new Day28CutsceneMenu(branch, onComplete);` and drop the `VictoryMenu` mention in the summary.
- Delete `src/TheLongestYear/UI/VictoryMenu.cs`. Grep for `VictoryMenu` and `Day28Branch.Win` across `src/` and `tests/`; fix every hit (the `WinSummary.LoopLine` helper stays, the choice uses it).
- `ModEntry`: `tly_win` description becomes "Arm the Year One Ending for tomorrow morning (debug; sleep, then step outside)."; add
  ```csharp
              helper.ConsoleCommands.Add("tly_ending", "Replay the Year One Ending event now, no continuation (debug). Usage: tly_ending [speaker <Name>]", this.CmdEnding);
  ```
  ```csharp
          private void CmdEnding(string command, string[] args)
          {
              if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
              string speaker = args.Length >= 2 && args[0] == "speaker" ? args[1] : null;
              _endingDriver?.StartNow(speaker);
          }
  ```
  Add `tly_ending` and `tly_win` to the file-driven bridge dispatcher near lines 2019-2028 the same way `tly_win` is already mapped.

- [ ] **Step 4: i18n**

Remove `dialog.win.prompt`, `dialog.win.new-loop`, `dialog.win.keep-playing` (lines 25-27) and add:
```json
    "dialog.ending.prompt": "{{loopline}}\nThe loop is yours to keep or to break. Begin a new loop now, or keep playing this year?",
    "dialog.ending.new-loop": "Loop again",
    "dialog.ending.keep-playing": "Keep playing this year",
    "dialog.ending.keep-1": "You've done well so far, but the work isn't over. Prepare yourself for what's next.",
    "dialog.ending.keep-2": "On Spring 1, we get to work freeing the townsfolk.",
```
Grep `src/` and `tests/` for `dialog.win.` and fix every reference (I18nGuardTests will fail on a missing key otherwise). Append the new keys to the lines review file.

- [ ] **Step 5: Build and run all tests**

Run: `dotnet build TheLongestYear.sln -c Release` then `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release`
Expected: 0 errors, all tests pass (I18n guard included).

- [ ] **Step 6: Commit (manifest 0.17.16)**

```bash
git add -A src/TheLongestYear src/TheLongestYear.Core tests docs/superpowers/specs/2026-09-06-year-one-ending-lines.md
git commit -m "v0.17.16: the win night on any date, the ending choice, keep-playing hand-off to vanilla's post-CC world; VictoryMenu and Day28Branch.Win removed"
```

---

### Task 12: The Spring 1 Year 2 wall

**Files:**
- Create: `src/TheLongestYear.Core/Ending/Year2WallRule.cs`
- Test: `tests/TheLongestYear.Tests/Year2WallRuleTests.cs`
- Modify: `src/TheLongestYear/Loop/RunController.cs` (`OnDayStarted`, `FinalizeReset`)
- Modify: `src/TheLongestYear/i18n/default.json`
- Modify: `src/TheLongestYear/ModEntry.cs` (`tly_year2wall`)

**Interfaces:**
- Produces: `Year2WallRule.ShouldShow(int year, bool wallArmed, bool year2Started) : bool`; `RunController.ShowYear2Wall()`; `RunController.DebugShowYear2Wall()`.

- [ ] **Step 1: Write the failing tests**

```csharp
using TheLongestYear.Core.Ending;
using Xunit;

namespace TheLongestYear.Tests;

public class Year2WallRuleTests
{
    [Theory]
    [InlineData(2, true, false, true)]    // the case: year 2, armed, no year-2 content
    [InlineData(3, true, false, true)]    // any later year too
    [InlineData(1, true, false, false)]   // still year 1: the prep season
    [InlineData(2, false, false, false)]  // legacy keep-playing save: exempt
    [InlineData(2, true, true, false)]    // the Year 2 update has taken over
    public void ShouldShow(int year, bool armed, bool started, bool expected)
        => Assert.Equal(expected, Year2WallRule.ShouldShow(year, armed, started));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release --filter "FullyQualifiedName~Year2WallRuleTests"`
Expected: build error.

- [ ] **Step 3: Implement**

```csharp
namespace TheLongestYear.Core.Ending;

/// <summary>Year One Ending (spec 2026-09-06 §5): Spring 1 of year 2 on a keep-playing save shows the
/// "Year 2 is coming" wall until the Year 2 update sets Year2Started. Legacy saves never armed it.</summary>
public static class Year2WallRule
{
    public static bool ShouldShow(int year, bool wallArmed, bool year2Started)
        => year >= 2 && wallArmed && !year2Started;
}
```

`RunController.OnDayStarted`, before the `_pendingChoice` check from Task 11:
```csharp
            if (TheLongestYear.Core.Ending.Year2WallRule.ShouldShow(Game1.year, _store.State.Year2WallArmed, _store.State.Year2Started))
            {
                ShowYear2Wall();
                return;
            }
```
New members:
```csharp
        /// <summary>The Year 2 wall: one response, Loop again. ESC also selects the last (only)
        /// response, so there is no way past it on this version; quitting re-shows it next load.</summary>
        public void ShowYear2Wall()
        {
            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null) { _monitor.Log("Year 2 wall: no location yet; will retry next morning.", LogLevel.Warn); return; }
            var responses = new[] { new StardewValley.Response("loop", Strings.Get("dialog.year2wall.loop")) };
            loc.createQuestionDialogue(Strings.Get("dialog.year2wall.prompt"), responses, (Farmer who, string key) =>
            {
                _monitor.Log("Year 2 wall: Loop again.", LogLevel.Info);
                _store.State.VictoryAcknowledged = false;
                _store.State.Year2WallArmed = false;
                _store.Save();
                FinalizeReset("year 2 wall");
            });
        }

        public void DebugShowYear2Wall() => ShowYear2Wall();
```

i18n:
```json
    "dialog.year2wall.prompt": "The loop is broken, and the Junimos are not yet ready to lead the next fight.\nYear 2 of The Longest Year is coming in a future beta. Stay tuned!\nFor now, the Junimos can send you around one more time.",
    "dialog.year2wall.loop": "Loop again",
```
`ModEntry`: `helper.ConsoleCommands.Add("tly_year2wall", "Show the Spring 1 year-2 wall dialog now (debug).", (c, a) => { if (Context.IsWorldReady) _runController?.DebugShowYear2Wall(); });` and map it in the bridge dispatcher. Append the two keys to the lines review file.

- [ ] **Step 4: Run tests and build**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -c Release` and `dotnet build TheLongestYear.sln -c Release`
Expected: all green.

- [ ] **Step 5: Commit (manifest 0.17.17)**

```bash
git add src/TheLongestYear.Core/Ending/Year2WallRule.cs tests/TheLongestYear.Tests/Year2WallRuleTests.cs src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/ModEntry.cs docs/superpowers/specs/2026-09-06-year-one-ending-lines.md src/TheLongestYear/manifest.json
git commit -m "v0.17.17: Spring 1 year-2 wall (Year 2 beta is coming; Loop again is the only way on)"
```

---

### Task 13: Live verification runbook and docs

**Files:**
- Modify: `docs/HEADLESS_DRIVING.md` (new section after `## Farm-type runs`)
- Modify: `CHANGELOG.md` (Unreleased entry), `TODO.md` (retire the "animated loop cutscene + real ending" item's ending half; note the speaker override table and the Junimo-colour verify result), `STATUS.md`
- Modify: `docs/superpowers/specs/2026-06-06-tly1-story-and-cutscenes-design.md` (§5.4: one line pointing at the new spec)

- [ ] **Step 1: Runbook**

Add to `HEADLESS_DRIVING.md`:

```markdown
## The Year One Ending

Arm, sleep, step outside, watch. Use the throwaway save (memory: the Rodger save is disposable).

    n = count; send "tly_win"                          # arms the ending; log: "Win night (tly_win): ending armed"
    send "debug sleep"  (or walk to bed)                # log next morning: "Ending: starting (speaker=..."
    send "debug warp Farm 64 16"                        # step outside if the wake put you indoors
    wait -Pattern "Ending: event finished" -TimeoutSec 240

The shrine opens, then the choice. `tly_dismiss` picks the last response (Keep playing); to take
Loop again, read the DialogueBox responses with `tly_menu` and select the first.

Replay the event alone: `tly_ending` (current location, no continuation); force a voice:
`tly_ending speaker Shane`. Wall: `tly_year2wall` on any keep-playing save.

Checks before a release: whole flow once on Standard; scenes 1 and 6 once on Meadowlands
(`tly_newgame meadowlands skipintro`, then `tly_win`); the log line "Morris_Dark: recoloured N"
with N > 0; every crowd member present on the steps; both branches of the choice; the wall on
Spring 1 year 2 of a keep-playing save; a loop-again reset leaves the shrine dark.
```

If `tly_menu` does not exist, replace that sentence with the existing way HEADLESS_DRIVING.md documents answering a question dialogue (search the file for "DialogueBox" or "response").

- [ ] **Step 2: Run the runbook once**

Follow it end to end on the deployed build. Record the outcome (pass, or what failed and the fix) in `STATUS.md` under a dated heading. This step is not complete until the log shows `Ending: event finished` on Standard and the Meadowlands shrine scene framed the shrine.

- [ ] **Step 3: Changelog, TODO, spec pointer**

`CHANGELOG.md` Unreleased:
```markdown
- **The Year One Ending.** Finishing the Community Center now ends the year the next morning, whatever the date: a sunny day, the whole town on the hall steps, a villager who half-remembers you (assembled from what your save actually records), Morris closing the Pelican Town store for greener ground, the Junimos' warning, and one candle at grandpa's shrine. Loop again resets on the spot; Keep playing gives you the rest of the year to prepare and a "Year 2 is coming" notice on Spring 1. Replaces the win card. Vanilla's own completion ceremony no longer plays.
```
`TODO.md`: under "★ NEXT NON-BUG-FIX UPGRADE", mark item 2 (real ending) SHIPPED with the version, leave item 1 (the rewind) open. The June story spec §5.4: prepend "Superseded by `2026-09-06-year-one-ending-design.md`."

- [ ] **Step 4: Commit (manifest 0.17.18)**

```bash
git add docs/HEADLESS_DRIVING.md CHANGELOG.md TODO.md STATUS.md docs/superpowers/specs/2026-06-06-tly1-story-and-cutscenes-design.md src/TheLongestYear/manifest.json
git commit -m "v0.17.18: Year One Ending runbook, changelog and TODO; live-verified on Standard and Meadowlands"
```

Release notes (README What's New, Nexus description, Nexus changelog) are Jeff's call after the voice pass; not part of this plan.
