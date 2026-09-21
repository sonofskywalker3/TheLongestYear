# Darkness Agents and Gate Scenes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** New gate scene lines, four silent overnight strike scenes (crows, thief, hall, cloud) that do the night's damage on screen, an every-loop guarantee for each kind of strike, witness dialogue for Linus and Shane, and the first-strike letters removed.

**Architecture:** Pure rules go in `TheLongestYear.Core` with xUnit tests (the guarantee, which scene is due, the witness window, the Summer closer). The night pass in `SabotageService` splits into pick (day end) and apply (in the scene, or at once). Each scene is a vanilla `FarmEvent` class returned from a postfix on `Utility.pickFarmEvent`, sharing one base class that owns the timeline, the skip and the apply-once safety.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony, xUnit. Stardew Valley 1.6 (decompile at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley`).

**Spec:** `docs/superpowers/specs/2026-09-21-darkness-agents-and-gate-scenes-design.md`. Session notes: `docs/superpowers/notes/2026-09-21-story-session-notes.md`.

## Global Constraints

- Branch `story`. **Do not bump `manifest.json` Version** (stays `0.18.15`). Commit per task, **push after every commit** (`git push`).
- Player-facing text is Jeff's, verbatim from the spec. Do not reword a line. **No em dashes, semicolons or ellipses** in any string, comment or doc you write.
- The strike scenes have **no text**. Morning popups (`hud.sabotage.*`) are not touched.
- i18n text lives only in `src/TheLongestYear/i18n/default.json`. Keys reached through a variable must be walked in `tests/TheLongestYear.Tests/I18nGuardTests.cs`.
- Build: `dotnet build -c Debug --nologo -v q` (0 errors). Tests: `dotnet test --nologo -v q` (all pass; 2683 before this plan).
- Deploy and drive the game only through `tools/deploy.ps1 -Minimized` and `tools/bridge.ps1` (see `docs/HEADLESS_DRIVING.md`). Every game launch is labelled an automated run. Throwaway farms only (`tly_newgame`), deleted afterwards. After a deploy run `git checkout -- test-output/log-archive`.
- One error pattern: exceptions in Core, log-and-fall-back in the glue. A scene that throws must apply its strike and end, never strand the night.
- Commit trailer on every commit:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  ```

## File Structure

| File | Responsibility |
| --- | --- |
| `src/TheLongestYear.Core/SeasonTurn.cs` (modify) | Gate scene line table, now rewound-aware |
| `src/TheLongestYear.Core/Sabotage/StrikeGuarantee.cs` (create) | Which strike is owed tonight |
| `src/TheLongestYear.Core/Sabotage/StrikeScenes.cs` (create) | Is a scene due, can it be skipped |
| `src/TheLongestYear.Core/Sabotage/WitnessLines.cs` (create) | Witness record, window, `when` phrase |
| `src/TheLongestYear.Core/RunState.cs`, `MetaState.cs` (modify) | New persisted fields |
| `src/TheLongestYear/Loop/BlightPass.cs`, `SpoilagePass.cs` (modify) | Pick and apply halves |
| `src/TheLongestYear/Loop/PendingStrike.cs` (create) | Tonight's picked strike and its targets |
| `src/TheLongestYear/Loop/SabotageService.cs` (modify) | Pick at day end, guarantee, apply once, scene hand-off |
| `src/TheLongestYear/Loop/SabotageMailService.cs` (delete) | Letters removed |
| `src/TheLongestYear/Loop/StrikeScenePatch.cs` (create) | `pickFarmEvent` postfix that hands out the scene |
| `src/TheLongestYear/Scenes/StrikeSceneBase.cs` (create) | Timeline, fade, skip, apply-once |
| `src/TheLongestYear/Scenes/CrowsScene.cs`, `ThiefScene.cs`, `HallScene.cs`, `CloudScene.cs` (create) | One scene each |
| `src/TheLongestYear/Loop/TaintedAuraPatch.cs` (create) | Dark aura on tampered-away items |
| `src/TheLongestYear/Loop/WitnessDialogueService.cs` (create) | Pushes the witness line onto the NPC |
| `src/TheLongestYear/Integration/SeasonTurnEventInjector.cs` (modify) | Winter glow holds, Summer closer |

---

### Task 1: Gate scene lines

**Files:**
- Modify: `src/TheLongestYear.Core/SeasonTurn.cs`
- Modify: `src/TheLongestYear/Integration/SeasonTurnEventInjector.cs` (the `Build(SeasonTurnKind, ...)` method and its caller)
- Modify: `src/TheLongestYear/Integration/SeasonTurnDriver.cs` (passes the rewound flag)
- Modify: `src/TheLongestYear/i18n/default.json` (keys `event.turn.*`)
- Test: `tests/TheLongestYear.Tests/SeasonTurnTests.cs`, `tests/TheLongestYear.Tests/I18nGuardTests.cs`

**Interfaces:**
- Produces: `SeasonTurn.Lines(SeasonTurnKind kind, bool rewound)`; `SeasonTurn.AllLineKeys` (every key any variant can return, for the i18n guard).

- [ ] **Step 1: Write the failing tests** (add to `SeasonTurnTests.cs`)

```csharp
[Fact]
public void Summer_closer_is_the_plain_one_on_a_save_never_rewound()
    => Assert.Equal("event.turn.summer-3", SeasonTurn.Lines(SeasonTurnKind.Summer, rewound: false)[2].Key);

[Fact]
public void Summer_closer_changes_once_the_save_has_been_rewound()
    => Assert.Equal("event.turn.summer-3-again", SeasonTurn.Lines(SeasonTurnKind.Summer, rewound: true)[2].Key);

[Theory]
[InlineData(false)]
[InlineData(true)]
public void Fall_has_three_lines_one_per_junimo(bool rewound)
{
    var lines = SeasonTurn.Lines(SeasonTurnKind.Fall, rewound);
    Assert.Equal(new[] { 0, 1, 2 }, lines.Select(l => l.Junimo).ToArray());
    Assert.Equal("event.turn.fall-3", lines[2].Key);
}

[Fact]
public void Winter_keeps_four_lines_and_ignores_the_rewound_flag()
    => Assert.Equal(SeasonTurn.Lines(SeasonTurnKind.Winter, false), SeasonTurn.Lines(SeasonTurnKind.Winter, true));

[Fact]
public void AllLineKeys_covers_both_summer_closers_and_drops_fall_4()
{
    Assert.Contains("event.turn.summer-3-again", SeasonTurn.AllLineKeys);
    Assert.DoesNotContain("event.turn.fall-4", SeasonTurn.AllLineKeys);
}
```

- [ ] **Step 2: Run** `dotnet test --nologo -v q --filter SeasonTurnTests`. Expected: compile failure (no `rewound` parameter).

- [ ] **Step 3: Implement in `SeasonTurn.cs`.** Replace `Lines` with:

```csharp
/// <summary>The scene's lines in order: which Junimo (0 = the green voice) says which i18n key.
/// Only the Summer closer changes once the save has been rewound (spec 2026-09-21).</summary>
public static IReadOnlyList<(int Junimo, string Key)> Lines(SeasonTurnKind kind, bool rewound) => kind switch
{
    SeasonTurnKind.Summer => new[] { (0, KeyPrefix + "summer-1"), (1, KeyPrefix + "summer-2"), (0, KeyPrefix + (rewound ? "summer-3-again" : "summer-3")) },
    SeasonTurnKind.Fall => new[] { (0, KeyPrefix + "fall-1"), (1, KeyPrefix + "fall-2"), (2, KeyPrefix + "fall-3") },
    _ => new[] { (0, KeyPrefix + "winter-1"), (1, KeyPrefix + "winter-2"), (2, KeyPrefix + "winter-3"), (0, KeyPrefix + "winter-4") },
};

/// <summary>Every key any variant can return, for the i18n guard.</summary>
public static IReadOnlyList<string> AllLineKeys { get; } = new[] { false, true }
    .SelectMany(r => Enum.GetValues<SeasonTurnKind>().SelectMany(k => Lines(k, r).Select(l => l.Key)))
    .Distinct().ToArray();
```

Add `using System.Linq;`. Fix every caller of the old one-argument `Lines` (grep `SeasonTurn.Lines(`): `SeasonTurnEventInjector.Build` gains a `bool rewound` parameter and passes it; `SeasonTurnDriver` passes `_meta.State.CompletedResets > 0`; the `tly_seasonturn` debug path passes the same.

- [ ] **Step 4: The Winter glow holds.** In `SeasonTurnEventInjector.Build(SeasonTurnKind ...)` delete this line and nothing else:

```csharp
if (kind == SeasonTurnKind.Winter && i == lines.Count - 1) s.Add("stopGlowing");
```

and add `if (kind == SeasonTurnKind.Winter) s.Add("stopGlowing");` immediately AFTER the `FadeOutName 1200` command, so the glow is still up while the screen fades. Leave the tamper scene's `Build` overload alone.

- [ ] **Step 5: i18n.** In `default.json` replace the eleven `event.turn.*` lines with exactly:

```json
"event.turn.summer-1": "Thank you @. You've done well. But something dark has noticed our work here.",
"event.turn.summer-2": "We have enough power to hold it back thanks to your gifts, but some attacks may slip through our guard.",
"event.turn.summer-3": "Prepare yourself, we know you can do this.",
"event.turn.summer-3-again": "Prepare yourself. We have taken much from you. We must take more, but know that it will end.",
"event.turn.fall-1": "You have been working hard. We are growing stronger.",
"event.turn.fall-2": "It grows stronger as well. As the days grow shorter, it will reach further.",
"event.turn.fall-3": "You must continue. We will hold back what we can, but the work is yours alone.",
"event.turn.winter-1": "This is the last season, @. If it is to end, it ends here.",
"event.turn.winter-2": "The agents of the enemy will not stop, but with the cold and dark of Winter, it may attack directly.",
"event.turn.winter-3": "It will corrupt whatever it can. To go on, you must mend what it breaks.",
"event.turn.winter-4": "If you win here, this cycle will be broken. But I fear our war will continue.",
```

In `I18nGuardTests`, where the season-turn keys are walked, walk `SeasonTurn.AllLineKeys` instead of calling `Lines` per kind.

- [ ] **Step 6:** `dotnet build -c Debug --nologo -v q` then `dotnet test --nologo -v q`. Expected: 0 errors, all pass.

- [ ] **Step 7: Update the script sheet.** In `docs/superpowers/specs/2026-09-07-season-turn-script.md` change the Mood beats row "Fall to Winter, before the last line, `stopGlowing`" to "Fall to Winter, under the fade-out, `stopGlowing`", and add one line under the title: "Lines superseded by `2026-09-21-darkness-agents-and-gate-scenes-design.md`."

- [ ] **Step 8: Commit and push.** `git add -A -- src tests docs && git commit -m "turns: Jeff's new gate scene lines, Fall drops to three, the Winter glow holds" && git push`

---

### Task 2: Remove the first-strike letters

**Files:**
- Delete: `src/TheLongestYear/Loop/SabotageMailService.cs`
- Modify: `src/TheLongestYear/Loop/SabotageService.cs` (field `_mail`, the constructor parameter, the `_mail?.SendFirstStrikeLetter(report.Kind);` call in `ShowMorningReports`)
- Modify: `src/TheLongestYear/ModEntry.cs` (field `_sabotageMail`, its construction near line 256, its `AssetRequested` hookup, the argument passed to `new SabotageService(...)`)
- Modify: `src/TheLongestYear/i18n/default.json` (delete the four `mail.darkness.*` keys)
- Modify: `src/TheLongestYear.Core/MetaState.cs` (keep `SabotageLettersSent` so old saves still deserialize; mark it)

- [ ] **Step 1:** Delete the file and every reference listed above. `git grep -n "SabotageMail\|mail\.darkness\|SendFirstStrikeLetter"` must return nothing under `src` and `tests` afterwards (delete any test that only covered the letters).

- [ ] **Step 2:** In `MetaState.cs` replace the doc comment on `SabotageLettersSent` with:

```csharp
/// <summary>Unused since 2026-09-21 (the first-strike letters were replaced by the overnight
/// scenes). Kept so a save written before then still loads.</summary>
```

- [ ] **Step 3:** Players who already received a letter keep it in their collection. Nothing to clean up: with the `Data/mail` entry gone the game shows no letter for an unknown key, and the rewind clears `mailReceived` anyway.

- [ ] **Step 4:** Build and test. Expected: 0 errors, all pass.

- [ ] **Step 5: Commit and push.** `git commit -am "darkness: the first-strike villager letters are gone" && git push` (use `git add -A src tests` first so the deletion is staged).

---

### Task 3: The every-loop guarantee (Core)

**Files:**
- Create: `src/TheLongestYear.Core/Sabotage/StrikeGuarantee.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs` (new field, cleared with the other darkness fields near line 363)
- Test: `tests/TheLongestYear.Tests/StrikeGuaranteeTests.cs`

**Interfaces:**
- Produces: `RunState.StruckEvents` (`HashSet<string>`, names of `DarknessEvent` values that have struck this loop); `StrikeGuarantee.ForceFromDay` (15); `StrikeGuarantee.Owed(Season, int dayOfMonth, IReadOnlyCollection<string> struck)`; `StrikeGuarantee.ForcedTonight(Season, int dayOfMonth, IReadOnlyCollection<string> struck, Func<DarknessEvent, bool> canAct)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21: every kind of strike lands at least once every loop. A kind not yet
/// struck by day 15 of its debut season is forced on the first night it can act.</summary>
public class StrikeGuaranteeTests
{
    private static readonly string[] None = Array.Empty<string>();
    private static bool Always(DarknessEvent e) => true;

    [Fact]
    public void Nothing_is_owed_before_day_15()
        => Assert.Empty(StrikeGuarantee.Owed(Season.Summer, 14, None));

    [Fact]
    public void Summer_owes_crows_and_thief_from_day_15()
        => Assert.Equal(new[] { DarknessEvent.CropBlight, DarknessEvent.ChestBlight }, StrikeGuarantee.Owed(Season.Summer, 15, None));

    [Fact]
    public void A_kind_that_already_struck_is_not_owed()
        => Assert.Equal(new[] { DarknessEvent.ChestBlight }, StrikeGuarantee.Owed(Season.Summer, 20, new[] { "CropBlight" }));

    [Fact]
    public void Fall_owes_only_the_hall()
        => Assert.Equal(new[] { DarknessEvent.Reversion }, StrikeGuarantee.Owed(Season.Fall, 15, None));

    [Theory]
    [InlineData(Season.Spring)]
    [InlineData(Season.Winter)]
    public void Spring_and_Winter_owe_nothing(Season season)
        => Assert.Empty(StrikeGuarantee.Owed(season, 20, None));

    [Fact]
    public void Crows_missed_in_Summer_are_not_owed_in_Fall()
        => Assert.DoesNotContain(DarknessEvent.CropBlight, StrikeGuarantee.Owed(Season.Fall, 20, None));

    [Fact]
    public void The_first_owed_kind_that_can_act_is_forced()
        => Assert.Equal(DarknessEvent.CropBlight, StrikeGuarantee.ForcedTonight(Season.Summer, 15, None, Always));

    [Fact]
    public void A_kind_that_cannot_act_hands_the_night_to_the_next_owed_kind()
        => Assert.Equal(DarknessEvent.ChestBlight,
            StrikeGuarantee.ForcedTonight(Season.Summer, 15, None, e => e != DarknessEvent.CropBlight));

    [Fact]
    public void Nothing_is_forced_when_no_owed_kind_can_act()
        => Assert.Null(StrikeGuarantee.ForcedTonight(Season.Summer, 15, None, e => false));

    [Fact]
    public void With_both_owed_they_land_on_consecutive_nights()
    {
        var struck = new HashSet<string>();
        DarknessEvent? first = StrikeGuarantee.ForcedTonight(Season.Summer, 15, struck, Always);
        struck.Add(first.Value.ToString());
        DarknessEvent? second = StrikeGuarantee.ForcedTonight(Season.Summer, 16, struck, Always);
        Assert.Equal(DarknessEvent.CropBlight, first);
        Assert.Equal(DarknessEvent.ChestBlight, second);
    }
}
```

- [ ] **Step 2: Run** `dotnet test --nologo -v q --filter StrikeGuaranteeTests`. Expected: compile failure.

- [ ] **Step 3: Implement `StrikeGuarantee.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Every kind of strike lands at least once every loop (spec 2026-09-21). A kind has a
/// debut season; if it has not struck by <see cref="ForceFromDay"/> of that season it is forced on
/// the first night it can act. Never overrides "cannot act" (warded, nothing stored, nothing
/// fair, capped): the caller's canAct decides that. Tampering has its own Winter rule in NightRoll.</summary>
public static class StrikeGuarantee
{
    /// <summary>The first half of week 3: two weeks left to pivot (Jeff, 2026-09-21).</summary>
    public const int ForceFromDay = 15;

    private static Season? DebutSeason(DarknessEvent e) => e switch
    {
        DarknessEvent.CropBlight => Season.Summer,
        DarknessEvent.ChestBlight => Season.Summer,
        DarknessEvent.Reversion => Season.Fall,
        _ => null,
    };

    /// <summary>The kinds still owed tonight, in NightRoll's option order.</summary>
    public static IReadOnlyList<DarknessEvent> Owed(Season season, int dayOfMonth, IReadOnlyCollection<string> struck)
    {
        if (struck is null) throw new ArgumentNullException(nameof(struck));
        if (dayOfMonth < ForceFromDay) return Array.Empty<DarknessEvent>();
        return NightRoll.Options(season)
            .Where(e => DebutSeason(e) == season && !struck.Contains(e.ToString()))
            .ToArray();
    }

    /// <summary>The owed kind to force tonight, or null when none is owed or none can act.</summary>
    public static DarknessEvent? ForcedTonight(Season season, int dayOfMonth, IReadOnlyCollection<string> struck, Func<DarknessEvent, bool> canAct)
    {
        if (canAct is null) throw new ArgumentNullException(nameof(canAct));
        foreach (DarknessEvent e in Owed(season, dayOfMonth, struck))
            if (canAct(e)) return e;
        return null;
    }
}
```

- [ ] **Step 4: `RunState.cs`.** Under `GuaranteedTamperDone` add:

```csharp
/// <summary>Names of the DarknessEvent values that have struck this loop (spec 2026-09-21: the
/// guarantee and the once-per-loop scenes both read it).</summary>
public HashSet<string> StruckEvents { get; set; } = new();
```

and in the reset block beside `GuaranteedTamperDone = false;` add `(StruckEvents ??= new()).Clear();`.

- [ ] **Step 5:** Run the filter again. Expected: 10 pass. Then the whole suite.

- [ ] **Step 6: Commit and push.** `git add -A src tests && git commit -m "darkness: the every-loop guarantee rule (Core)" && git push`

---

### Task 4: Which scene is due (Core)

**Files:**
- Create: `src/TheLongestYear.Core/Sabotage/StrikeScenes.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs`, `src/TheLongestYear.Core/MetaState.cs`
- Test: `tests/TheLongestYear.Tests/StrikeScenesTests.cs`

**Interfaces:**
- Produces: `RunState.StrikeScenesPlayed` (`HashSet<string>`, cleared at reset); `MetaState.StrikeScenesSeen` (`HashSet<string>`, permanent); `StrikeScenes.IsDue(DarknessEvent, IReadOnlyCollection<string> playedThisLoop)`; `StrikeScenes.IsSkippable(DarknessEvent, IReadOnlyCollection<string> seenOnSave)`; `StrikeScenes.MarkPlayed(DarknessEvent, RunState, MetaState)`.

- [ ] **Step 1: Failing tests**

```csharp
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21: the first strike of each kind in a loop plays its scene; a scene can be
/// skipped once that kind has ever played on the save.</summary>
public class StrikeScenesTests
{
    [Fact]
    public void A_kind_not_yet_played_this_loop_is_due()
        => Assert.True(StrikeScenes.IsDue(DarknessEvent.CropBlight, new string[0]));

    [Fact]
    public void A_kind_already_played_this_loop_is_not_due()
        => Assert.False(StrikeScenes.IsDue(DarknessEvent.CropBlight, new[] { "CropBlight" }));

    [Fact]
    public void Crows_playing_does_not_use_up_the_thief()
        => Assert.True(StrikeScenes.IsDue(DarknessEvent.ChestBlight, new[] { "CropBlight" }));

    [Fact]
    public void The_first_time_ever_cannot_be_skipped()
        => Assert.False(StrikeScenes.IsSkippable(DarknessEvent.Tampering, new string[0]));

    [Fact]
    public void Once_seen_on_the_save_it_can_be_skipped()
        => Assert.True(StrikeScenes.IsSkippable(DarknessEvent.Tampering, new[] { "Tampering" }));

    [Fact]
    public void MarkPlayed_records_the_loop_and_the_save()
    {
        var run = new RunState(); var meta = new MetaState();
        StrikeScenes.MarkPlayed(DarknessEvent.Reversion, run, meta);
        Assert.Contains("Reversion", run.StrikeScenesPlayed);
        Assert.Contains("Reversion", meta.StrikeScenesSeen);
    }
}
```

- [ ] **Step 2:** Run `--filter StrikeScenesTests`. Expected: compile failure.

- [ ] **Step 3: Implement**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>When an overnight strike scene plays (spec 2026-09-21).</summary>
public static class StrikeScenes
{
    public static bool IsDue(DarknessEvent e, IReadOnlyCollection<string> playedThisLoop)
    {
        if (playedThisLoop is null) throw new ArgumentNullException(nameof(playedThisLoop));
        return !Contains(playedThisLoop, e.ToString());
    }

    public static bool IsSkippable(DarknessEvent e, IReadOnlyCollection<string> seenOnSave)
    {
        if (seenOnSave is null) throw new ArgumentNullException(nameof(seenOnSave));
        return Contains(seenOnSave, e.ToString());
    }

    public static void MarkPlayed(DarknessEvent e, RunState run, MetaState meta)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (meta is null) throw new ArgumentNullException(nameof(meta));
        (run.StrikeScenesPlayed ??= new()).Add(e.ToString());
        (meta.StrikeScenesSeen ??= new()).Add(e.ToString());
    }

    private static bool Contains(IReadOnlyCollection<string> set, string name)
    {
        foreach (string s in set) if (s == name) return true;
        return false;
    }
}
```

`RunState`: add `public HashSet<string> StrikeScenesPlayed { get; set; } = new();` with a doc comment, and `(StrikeScenesPlayed ??= new()).Clear();` in the reset block. `MetaState`: add beside `SeasonTurnsSeen`:

```csharp
/// <summary>Strike scenes (DarknessEvent names) that have ever played on this save. A seen scene
/// can be skipped (spec 2026-09-21).</summary>
public HashSet<string> StrikeScenesSeen { get; set; } = new();
```

- [ ] **Step 4:** Tests pass, full suite passes.
- [ ] **Step 5: Commit and push.** `git commit -m "darkness: which strike scene is due and when it can be skipped (Core)"`

---

### Task 5: Pick at day end, apply once

> **Amended 2026-09-21 (Jeff): one chest per strike, machines as before.** `SpoilagePass.Plan` keeps the unit-by-unit weighted loop, but the first chest a roll lands on becomes the night's chest and every other chest leaves the pool; machines stay in the pool. `PendingStrike` gains `TargetChest` and `SceneTarget` (the first chest hit, else the first machine hit). The `Plan` code block below lacks the one-chest constraint; this note wins.

The heart of the plan. Today `NightPlan.Execute` picks and applies in one call. After this task the night pass produces a `PendingStrike` whose damage lands exactly once: in the scene, or at once when no scene will play.

**Files:**
- Modify: `src/TheLongestYear/Loop/BlightPass.cs`, `src/TheLongestYear/Loop/SpoilagePass.cs`
- Create: `src/TheLongestYear/Loop/PendingStrike.cs`
- Modify: `src/TheLongestYear/Loop/SabotageService.cs` (`RunNight`, `NightPlan`, `Blight`)
- Modify: `src/TheLongestYear/ModEntry.cs` (`OnSaving`: first line calls `_sabotage?.ApplyPendingIfAny("saving");`)

**Interfaces:**
- Consumes: `StrikeGuarantee.ForcedTonight`, `StrikeScenes.IsDue`, `RunState.StruckEvents`.
- Produces:
  - `BlightPass.Pick(int count, Random rng) : List<Vector2>`; `BlightPass.Kill(IEnumerable<Vector2> tiles) : int`
  - `SpoilagePass.Hit` (public class: `Chest Chest; GameLocation Location; Vector2 Tile; bool Machine; bool Perishable;`), `SpoilagePass.Plan(int count, Random rng, bool everything) : List<Hit>`, `SpoilagePass.Apply(List<Hit> hits) : Taken`
  - `PendingStrike` (see below)
  - `SabotageService.Pending : PendingStrike` (null when nothing is waiting), `SabotageService.ApplyPendingIfAny(string why) : bool`

- [ ] **Step 1: `BlightPass`.** Replace `Strike` with the two halves and keep `Strike` as their composition so the debug command still works:

```csharp
/// <summary>Which crops die tonight, chosen by <paramref name="rng"/>. Reads only.</summary>
public static List<Vector2> Pick(int count, Random rng)
{
    List<Vector2> tiles = LiveCropTiles();
    var picked = new List<Vector2>();
    if (tiles.Count == 0 || count <= 0) return picked;
    foreach (int index in BlightRule.PickIndexes(tiles.Count, count, rng)) picked.Add(tiles[index]);
    return picked;
}

/// <summary>Kill the crops on these tiles. A tile whose crop is gone or already dead is passed
/// over, so calling it twice is harmless. Returns how many died.</summary>
public static int Kill(IEnumerable<Vector2> tiles)
{
    Farm farm = Game1.getFarm();
    if (farm == null) return 0;
    int killed = 0;
    foreach (Vector2 tile in tiles)
        if (farm.terrainFeatures.TryGetValue(tile, out TerrainFeature tf)
            && tf is HoeDirt dirt && dirt.crop != null && !dirt.crop.dead.Value)
        {
            dirt.crop.Kill();
            killed++;
        }
    return killed;
}

public static int Strike(int count, Random rng) => Kill(Pick(count, rng));
```

- [ ] **Step 2: `SpoilagePass`.** The current `Strike` mutates as it rolls. Split it: `Plan` runs the same weighted roll against a working copy of each entry's units and records a `Hit` per unit taken, `Apply` does the removals.

```csharp
/// <summary>One unit the darkness takes tonight.</summary>
public sealed class Hit
{
    public Chest Chest;            // null for a placed machine
    public int Slot;
    public Item Item;
    public GameLocation Location;  // the chest's or the machine's map
    public Vector2 Tile;
    public bool Machine;
    public bool Perishable;
}

public static List<Hit> Plan(int count, Random rng, bool everything)
{
    var hits = new List<Hit>();
    if (count <= 0) return hits;
    List<Entry> entries = Entries(everything);
    var left = entries.ToDictionary(e => e, e => e.Units);
    int taken = 0;
    while (taken < count && entries.Count > 0)
    {
        int total = 0;
        foreach (Entry e in entries) total += left[e];
        if (total <= 0) break;
        int roll = rng.Next(total);
        Entry hit = entries[0];
        foreach (Entry e in entries) { roll -= left[e]; if (roll < 0) { hit = e; break; } }
        int cost = BlightRule.UnitsOf(1, hit.BigCraftable);
        taken += cost;
        hits.Add(new Hit
        {
            Chest = hit.Chest, Slot = hit.Slot, Item = hit.Item, Location = hit.Location, Tile = hit.Tile,
            Machine = hit.Chest == null,
            Perishable = !hit.BigCraftable && BlightRule.IsPerishableCategory(hit.Item.Category),
        });
        left[hit] -= cost;
        if (left[hit] <= 0) entries.Remove(hit);
    }
    return hits;
}

/// <summary>Do the removals. An item that has already gone (stack spent, machine moved) is passed
/// over, so a double call cannot take twice from a stack that the plan emptied.</summary>
public static Taken Apply(List<Hit> hits)
{
    int spoiled = 0, missing = 0;
    foreach (Hit h in hits)
    {
        if (h.Machine)
        {
            if (h.Location.objects.TryGetValue(h.Tile, out StardewValley.Object o) && ReferenceEquals(o, h.Item))
            { h.Location.objects.Remove(h.Tile); missing++; }
            continue;
        }
        if (h.Item.Stack <= 0 || h.Slot >= h.Chest.Items.Count || !ReferenceEquals(h.Chest.Items[h.Slot], h.Item)) continue;
        h.Item.Stack -= 1;
        if (h.Item.Stack <= 0) h.Chest.Items[h.Slot] = null;
        if (h.Perishable) spoiled++; else missing++;
    }
    return new Taken(spoiled, missing);
}

public static Taken Strike(int count, Random rng, bool everything) => Apply(Plan(count, rng, everything));
```

`Entry.Location` is only set for machines today. In `Entries`, set `Location = loc` on the chest entries too (the scene needs the chest's map). Add `using System.Linq;`.

The roll consumes `rng` exactly as before (one `Next(total)` per unit), so a re-slept night still rolls the same outcome.

- [ ] **Step 3: `PendingStrike.cs`**

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Loop
{
    /// <summary>Tonight's strike, picked at day end and not yet applied (spec 2026-09-21). The
    /// overnight scene applies it at its beat; with no scene it is applied at once. Apply runs
    /// its effect once however often it is called. In memory only: a strike is always applied
    /// before the night's save.</summary>
    internal sealed class PendingStrike
    {
        public DarknessEvent Event { get; }
        /// <summary>Crows: the crop tiles on the Farm that die.</summary>
        public IReadOnlyList<Vector2> CropTiles { get; init; } = Array.Empty<Vector2>();
        /// <summary>Thief: every unit taken tonight, across every chest and machine.</summary>
        public IReadOnlyList<SpoilagePass.Hit> Hits { get; init; } = Array.Empty<SpoilagePass.Hit>();
        public bool Applied { get; private set; }

        private readonly Action _effect;

        public PendingStrike(DarknessEvent e, Action effect)
        {
            Event = e;
            _effect = effect ?? throw new ArgumentNullException(nameof(effect));
        }

        public void Apply()
        {
            if (Applied) return;
            Applied = true;
            _effect();
        }
    }
}
```

- [ ] **Step 4: `SabotageService`.** Rename `NightPlan.Execute(DarknessEvent) : bool` to `Prepare(DarknessEvent) : PendingStrike` (null means "took nothing", the old `false`). Each case builds the effect closure from what it used to do inline:

```csharp
public PendingStrike Prepare(DarknessEvent e)
{
    switch (e)
    {
        case DarknessEvent.CropBlight:
        {
            List<Vector2> tiles = BlightPass.Pick(BlightRule.Count(_crops ?? BlightPass.LiveCropTiles().Count, _season, _level), _rng);
            if (tiles.Count == 0) return null;
            return new PendingStrike(e, () => _s.ReportBlight(BlightPass.Kill(tiles), default)) { CropTiles = tiles };
        }
        case DarknessEvent.ChestBlight:
        {
            bool everything = DarknessLevels.StorageReachesEverything(_level);
            int units = _stored ?? SpoilagePass.StoredUnits(everything);
            List<SpoilagePass.Hit> hits = SpoilagePass.Plan(BlightRule.SpoilCount(units, _season, _level), _rng, everything);
            if (hits.Count == 0) return null;
            return new PendingStrike(e, () => _s.ReportBlight(0, SpoilagePass.Apply(hits))) { Hits = hits };
        }
        case DarknessEvent.Reversion:
        {
            DonatedSlot pick = PlanReversion();
            if (pick == null) return null;
            bool unmoderated = _reversionUnmoderated;
            return new PendingStrike(e, () => { if (_s.RevertSlot(pick) && unmoderated) Run.UnmoderatedReversionSpent = true; });
        }
        case DarknessEvent.Tampering:
        {
            var worldState = Game1.netWorldState?.Value;
            if (worldState?.BundleData == null) return null;
            TamperPlan plan = PlanTamper();
            if (plan == null) return null;
            bool unmoderated = _tamperUnmoderated; int dayOfYear = _dayOfYear;
            return new PendingStrike(e, () => { if (_s.WriteTamper(worldState, plan.Target, plan.ItemId, plan.Stack, dayOfYear) && unmoderated) Run.UnmoderatedTamperSpent = true; });
        }
        default:
            return null;
    }
}
```

`ReportBlight(int killed, SpoilagePass.Taken taken)` is the report half of today's `Blight` (the `PendingSabotageReports.Add` and the log line, skipped when both are zero). `Blight(int, int, Random)` (the debug entry point) becomes `ReportBlight(BlightPass.Strike(crops, rng), SpoilagePass.Strike(spoil, rng, ...))` and returns the total as today.

- [ ] **Step 5: `RunNight`.** Add the field and helpers:

```csharp
/// <summary>Tonight's strike, waiting for its scene. Null when none.</summary>
public PendingStrike Pending { get; private set; }

/// <summary>Land a waiting strike now. The net under every path that does not play the scene:
/// no scene due, another overnight event won the slot, the scene threw, the save began.</summary>
public bool ApplyPendingIfAny(string why)
{
    PendingStrike p = Pending;
    if (p == null) return false;
    Pending = null;
    if (p.Applied) return false;
    _monitor.Log($"Darkness: applying tonight's {p.Event} without its scene ({why}).", LogLevel.Trace);
    p.Apply();
    return true;
}
```

Add one private method that every strike path goes through, and call it from both the guaranteed-tamper block and the ordinary path in place of `night.Execute(...)` plus the two `RecordStrike` calls:

```csharp
/// <summary>Pick tonight's strike, record it, and either leave it waiting for its scene or land it.</summary>
private bool Strike(NightPlan night, DarknessEvent e, int week, CoreSeason season, int dayOfYear)
{
    PendingStrike strike = night.Prepare(e);
    if (strike == null) return false;
    NightRoll.RecordStrike(Run, week, season);
    SabotageSchedule.RecordStrike(KindOf(e), Run, week, dayOfYear);
    (Run.StruckEvents ??= new()).Add(e.ToString());
    Pending = strike;
    if (!StrikeScenes.IsDue(e, Run.StrikeScenesPlayed ??= new()) || !SceneCanPlay(strike))
        ApplyPendingIfAny("no scene due");
    return true;
}

/// <summary>Set by ModEntry once the scenes exist (Task 6). Until then no scene can play.</summary>
public Func<PendingStrike, bool> SceneCanPlay { get; set; } = _ => false;
```

Then in `RunNight`, after the guaranteed-tamper block and before `if (!dice && forced == null) return;`, fold the guarantee into `forced`:

```csharp
DarknessEvent? owed = StrikeGuarantee.ForcedTonight(season, day, Run.StruckEvents ??= new(), night.CanAct);
if (forced == null && owed != null)
{
    forced = owed;
    _monitor.Log($"Darkness: {owed} has not struck this loop by {season} {day}; forced tonight.", LogLevel.Info);
}
```

At the top of `RunNight` call `ApplyPendingIfAny("a new night began")` so a strike can never be carried over.

- [ ] **Step 6: `ModEntry.OnSaving`**, first statement: `_sabotage?.ApplyPendingIfAny("saving");`. Also first statement of `SabotageService.ShowMorning`: `ApplyPendingIfAny("morning");`.

- [x] **Step 7: Verify the night order in the decompile.** **FOUND (2026-09-21): `Utility.pickFarmEvent()` (Game1.cs:8122) runs AFTER SMAPI's `DayEnding`, because `Game1.newDayAfterFade` wraps everything in `hooks.OnGame1_NewDayAfterFade` (Game1.cs:7272/7282) and SMAPI's `BeforeNewDayAfterFade` override raises DayEnding before the `_newDayAfterFade()` enumerator starts, so `RunNight` stays in `RunController.OnDayEnding`, no Harmony prefix and no `RunController.TonightIsOrdinary` were needed, and the full order is written up in `docs/HEADLESS_DRIVING.md` under "The night order at day end".** In `decompiled-pc/Stardew Valley/Game1.cs` find where `Utility.pickFarmEvent()` is called and confirm it runs AFTER SMAPI's `DayEnding` (raised at the start of `Game1.newDayAfterFade`'s caller). `RunController.OnDayEnding` is where `RunNight` runs. If `pickFarmEvent` runs first, move the pick: call `_sabotage.RunNight()` from a Harmony PREFIX on `Utility.pickFarmEvent` instead of from `OnDayEnding`, guarded by the same `action == RunAction.Continue && !Run.EndingArmed` test (expose it as `RunController.TonightIsOrdinary`). Record which order you found in the plan's checkbox line and in `docs/HEADLESS_DRIVING.md`.

- [ ] **Step 8:** Build, full tests. Then a live check (automated run): `tly_newgame standard skipintro`, `debug season summer`, plant and grow crops with `debug growcrops 5`, `tly_sabotage arm blight crops`, sleep. Expected log: `Darkness: Blight was armed; striking tonight as CropBlight.` then `Darkness: applying tonight's CropBlight without its scene (no scene due).` (no scene can play yet: `SceneCanPlay` is still false until Task 6). In the morning the crops are dead and the popup shows, exactly as before this task. Quote the log lines in the commit body.

- [ ] **Step 9: Commit and push.** `git commit -m "darkness: pick at day end, apply once; the every-loop guarantee wired into the night roll"`

---

### Task 6: The overnight slot and the scene base

**Files:**
- Create: `src/TheLongestYear/Scenes/StrikeSceneBase.cs`
- Create: `src/TheLongestYear/Loop/StrikeScenePatch.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (wire `SceneCanPlay`, the scene factory, the patch statics)

**Interfaces:**
- Consumes: `SabotageService.Pending`, `ApplyPendingIfAny`, `StrikeScenes.IsSkippable`, `StrikeScenes.MarkPlayed`.
- Produces: `abstract class StrikeSceneBase : FarmEvent` with `protected abstract void Build(Timeline t)`, `protected void ApplyStrike()`, `protected int ElapsedMs`; `StrikeScenePatch.SceneFor : Func<FarmEvent>`.

Read `WitchEvent.cs` and `FairyEvent.cs` in `decompiled-pc/Stardew Valley/Events/` first. They are the pattern: `setUp()` returns false to run, `tickUpdate` returns true when finished, `draw` and `drawAboveEverything` paint, `makeChangesToLocation` runs host-side at the end.

- [ ] **Step 1: `StrikeSceneBase`**

```csharp
using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Events;
using TheLongestYear.Loop;

namespace TheLongestYear.Scenes
{
    /// <summary>An overnight strike scene (spec 2026-09-21): silent, a few seconds, and it lands
    /// the night's damage at its own beat. A timeline of timed actions, a click to skip once the
    /// kind has been seen on the save, and one rule above all: however the scene ends, the strike
    /// is applied exactly once and the night goes on.</summary>
    internal abstract class StrikeSceneBase : FarmEvent
    {
        protected sealed class Timeline
        {
            internal readonly List<(int AtMs, Action Do)> Cues = new();
            public int EndMs { get; private set; }
            public void At(int ms, Action action) { Cues.Add((ms, action)); if (ms > EndMs) EndMs = ms; }
            public void EndAt(int ms) => EndMs = ms;
        }

        protected readonly PendingStrike Strike;
        protected readonly IMonitor Monitor;
        private readonly bool _skippable;
        private readonly Action _onFinished;
        private readonly Timeline _timeline = new();
        private int _next;
        private bool _failed;
        protected int ElapsedMs { get; private set; }

        protected StrikeSceneBase(PendingStrike strike, bool skippable, IMonitor monitor, Action onFinished)
        {
            Strike = strike; _skippable = skippable; Monitor = monitor; _onFinished = onFinished;
        }

        public NetFields NetFields { get; } = new NetFields("TLY.StrikeScene");

        /// <summary>Where the scene plays and what it needs. Return false to call the scene off.</summary>
        protected abstract bool Stage();
        protected abstract void Build(Timeline t);
        protected virtual void Paint(SpriteBatch b) { }
        protected virtual void PaintAbove(SpriteBatch b) { }

        protected void ApplyStrike() => Strike.Apply();

        public bool setUp()
        {
            try
            {
                if (!Stage()) { Finish(); return true; }
                Build(_timeline);
                _timeline.Cues.Sort((a, b) => a.AtMs.CompareTo(b.AtMs));
                Game1.freezeControls = true;
                return false;
            }
            catch (Exception ex) { Fail(ex); return true; }
        }

        public bool tickUpdate(GameTime time)
        {
            if (_failed) return true;
            try
            {
                ElapsedMs += time.ElapsedGameTime.Milliseconds;
                if (_skippable && (Game1.input.GetMouseState().LeftButton == Microsoft.Xna.Framework.Input.ButtonState.Pressed
                                   || Game1.input.GetGamePadState().IsButtonDown(Microsoft.Xna.Framework.Input.Buttons.A)))
                { Finish(); return true; }
                while (_next < _timeline.Cues.Count && _timeline.Cues[_next].AtMs <= ElapsedMs)
                    _timeline.Cues[_next++].Do();
                if (ElapsedMs >= _timeline.EndMs) { Finish(); return true; }
                return false;
            }
            catch (Exception ex) { Fail(ex); return true; }
        }

        public void draw(SpriteBatch b) { if (!_failed) try { Paint(b); } catch (Exception ex) { Fail(ex); } }
        public void drawAboveEverything(SpriteBatch b) { if (!_failed) try { PaintAbove(b); } catch (Exception ex) { Fail(ex); } }
        public void makeChangesToLocation() => ApplyStrike();

        private void Finish()
        {
            ApplyStrike();
            Game1.freezeControls = false;
            _onFinished?.Invoke();
        }

        private void Fail(Exception ex)
        {
            _failed = true;
            Monitor.Log($"Strike scene {GetType().Name} failed and was ended; the strike still lands. {ex}", LogLevel.Error);
            Finish();
        }
    }
}
```

Check `FarmEvent`'s exact members in the decompile (`Events/FarmEvent.cs`); if 1.6 declares `NetFields NetFields { get; }` differently or adds a member, match it.

- [ ] **Step 2: `StrikeScenePatch`**

```csharp
using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Events;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>Hands tonight's strike scene to the overnight slot (spec 2026-09-21). A random
    /// vanilla event (fairy, witch, meteorite, owl, capsule) gives way: it comes round again. A
    /// wedding, a WorldChangeEvent or anything unrecognised wins, and the strike lands at once.
    /// Runs after FarmEventSuppressionPatch, so a fail night is already null and stays null.</summary>
    [HarmonyPatch(typeof(Utility), nameof(Utility.pickFarmEvent))]
    [HarmonyPriority(Priority.Last)]
    internal static class StrikeScenePatch
    {
        /// <summary>Set by ModEntry: tonight's scene, or null when none is waiting.</summary>
        internal static Func<FarmEvent> SceneFor;
        /// <summary>Set by ModEntry: land the waiting strike with no scene.</summary>
        internal static Action<string> ApplyNow;
        /// <summary>Set by ModEntry: true on a fail night (the same test the suppression patch uses).</summary>
        internal static Func<bool> FailNight;
        internal static IMonitor Monitor;

        private static bool IsRandom(FarmEvent e)
            => e is SoundInTheNightEvent || e is FairyEvent || e is WitchEvent;

        // ReSharper disable once InconsistentNaming
        private static void Postfix(ref FarmEvent __result)
        {
            if (!RunActivation.IsActive || SceneFor == null) return;
            if (FailNight != null && FailNight()) return;
            if (__result != null && !IsRandom(__result))
            {
                ApplyNow?.Invoke($"{__result.GetType().Name} has the overnight slot");
                return;
            }
            FarmEvent scene = SceneFor();
            if (scene == null) return;
            if (__result != null)
                Monitor?.Log($"Strike scene takes the overnight slot from {__result.GetType().Name}.", LogLevel.Trace);
            __result = scene;
        }
    }
}
```

`SoundInTheNightEvent` covers the meteorite, owl, capsule and the rest of that family in 1.6; confirm in the decompile and add any other purely random class `pickFarmEvent` can return (for example the 1.6 raccoon or perfection events are NOT random: leave them winning).

- [ ] **Step 3: `ModEntry` wiring** (after `_sabotage` is constructed):

```csharp
_sabotage.SceneCanPlay = strike => Scenes.StrikeSceneFactory.CanPlay(strike);
Loop.StrikeScenePatch.Monitor = this.Monitor;
Loop.StrikeScenePatch.FailNight = Loop.FarmEventSuppressionPatch.SuppressTonight;
Loop.StrikeScenePatch.ApplyNow = why => _sabotage.ApplyPendingIfAny(why);
Loop.StrikeScenePatch.SceneFor = () =>
{
    PendingStrike strike = _sabotage.Pending;
    if (strike == null || strike.Applied) return null;
    bool skippable = StrikeScenes.IsSkippable(strike.Event, _meta.State.StrikeScenesSeen ??= new());
    return Scenes.StrikeSceneFactory.Create(strike, skippable, this.Monitor,
        onFinished: () => { StrikeScenes.MarkPlayed(strike.Event, _meta.Run, _meta.State); _witness?.OnScenePlayed(strike.Event); });
};
```

(`_witness` arrives in Task 12; until then omit that call.) Create `src/TheLongestYear/Scenes/StrikeSceneFactory.cs`:

```csharp
internal static class StrikeSceneFactory
{
    public static bool CanPlay(PendingStrike strike) => strike.Event switch
    {
        DarknessEvent.CropBlight => strike.CropTiles.Count > 0,
        DarknessEvent.ChestBlight => ThiefScene.PickChest(strike) != null,
        DarknessEvent.Reversion => true,
        DarknessEvent.Tampering => true,
        _ => false,
    };

    public static FarmEvent Create(PendingStrike strike, bool skippable, IMonitor monitor, Action onFinished) => strike.Event switch
    {
        DarknessEvent.CropBlight => new CrowsScene(strike, skippable, monitor, onFinished),
        DarknessEvent.ChestBlight => new ThiefScene(strike, skippable, monitor, onFinished),
        DarknessEvent.Reversion => new HallScene(strike, skippable, monitor, onFinished),
        DarknessEvent.Tampering => new CloudScene(strike, skippable, monitor, onFinished),
        _ => null,
    };
}
```

To keep this task buildable on its own, add the four scene classes now as minimal subclasses whose `Stage()` returns true and whose `Build` is `t.At(1500, ApplyStrike); t.EndAt(2500);` with `Game1.globalFadeToBlack` not used (a black 2.5 second hold). `ThiefScene.PickChest` returns the hit chest with the most hits whose `Location` is the Farm, a `FarmHouse`, a `Cellar` or a `Shed`, else null (full code in Task 8). Tasks 7 to 10 replace each body.

- [ ] **Step 4: Live check (automated run).** `tly_newgame standard skipintro`, Summer, grown crops, `tly_sabotage arm blight crops`, sleep. Expected log order: the arm line, NO "applying ... without its scene", the night plays a 2.5 second hold, crops dead in the morning, popup shown, `tly_sabotage status` lists `CropBlight` under scenes played. Then arm it again the next night: expected `applying tonight's CropBlight without its scene (no scene due)`. The overnight slot collision is checked in Task 13 Step 2. Quote the log.

- [ ] **Step 5: Commit and push.** `git commit -m "darkness: strike scenes take the overnight slot; base class lands the strike exactly once"`

---

### Task 7: The crows

**Files:** Modify `src/TheLongestYear/Scenes/CrowsScene.cs`. Create `src/TheLongestYear/Scenes/SceneCamera.cs` (shared by Tasks 7 to 9).

Read first: `decompiled-pc/Stardew Valley/BellsAndWhistles/Crow.cs` (the critter sheet `TileSheets\critters`, base frame 14, its landing and pecking frames) and how `WitchEvent` moves the viewport (`Game1.viewport.X/Y`, `Game1.currentLocation = ...`, `Game1.currentLightSources`).

- [ ] **Step 1: `SceneCamera`**: `static void CutTo(GameLocation where, Vector2 tile)` sets `Game1.currentLocation`, calls `where.resetForPlayerEntry()`, centres `Game1.viewport` on the tile clamped to the map, sets `Game1.ambientLight`/`Game1.outdoorLight` to the night value the location would have at 2600, and `static void Restore()` puts `Game1.currentLocation` back to the farmer's. Copy the exact save and restore pattern from `WitchEvent.setUp` and its end.

- [ ] **Step 2: Stage.** Largest cluster: group `Strike.CropTiles` by proximity (a tile joins a cluster when within 6 tiles of its first member), take the biggest cluster's centroid. Scarecrow: the nearest farm object whose `IsScarecrow()` is true within 12 tiles of the centroid; if one exists, move the camera to the midpoint so both are in frame. On-screen crows: the up to 6 cluster tiles nearest the centroid.

- [ ] **Step 3: Build the timeline** (times in ms):

| At | What |
| --- | --- |
| 0 | `SceneCamera.CutTo(Farm, focus)`, fade in from black over 600 |
| 600 | crows enter from above the viewport, 120 ms apart, each flying a straight line to its tile (3.5 tiles a second); `Game1.playSound("crow")` pitched down (`Game1.playSound("crow", -600)` in 1.6; check the overload) on the first |
| 2200 | if there is a scarecrow, a seventh crow lands on the tile beside it |
| 2600 | Linus enters at the frame's nearest map-edge side, walking toward the farm 2 tiles (NPC sprite `Characters\Linus`, drawn by the scene, not the real NPC) |
| 3600 | Linus stops, faces the crows, `emote`-style shake: offset x by ±2 px for 300 ms |
| 4200 | Linus steps back one tile |
| 4600 | crows peck (frames from `Crow.cs`), 3 pecks at 250 ms |
| 5400 | `ApplyStrike()`: every picked crop dies, on and off screen |
| 5600 | Linus turns and leaves at run speed |
| 6400 | all crows lift off together, up and out |
| 7200 | fade to black over 800 |
| 8000 | end (`t.EndAt(8000)`), `SceneCamera.Restore()` |

Red eyes: after drawing each crow, draw a 2x2 px `Game1.staminaRect` tinted `Color.Red` at the eye pixel of the current frame (eye offsets per frame read off the sheet), and add one small `LightSource` (radius 0.3, red) per crow to `Game1.currentLightSources` on landing, removed on lift-off.

Linus is a scene-drawn sprite (`AnimatedSprite` on `Characters\Linus`), never the real NPC: moving the real one at night fights his schedule and the new day.

- [ ] **Step 4: Debug command** `tly_sabotage scene crows`: build a `PendingStrike` from a fresh `BlightPass.Pick` (or, with no crops, from the 9 tiles around the farmer with a no-op effect) and set `Game1.farmEvent` to the scene directly. Register in the `tly_sabotage` switch in `ModEntry` near line 2299 and in its usage string.

- [ ] **Step 5: Live check (automated run), screenshots** with `tools/screenshot.ps1` at 1500, 3000, 4800, 5600 and 7000 ms into the scene, on a farm with a scarecrow in range and on one without. Look at them. Crows must sit ON the dying crops; the scarecrow crow must be beside it; Linus must be inside the frame and not standing on a crop.

- [ ] **Step 6: Commit and push.** `git commit -m "scenes: the crows"`

---

### Task 8: The thief

> **Amended 2026-09-21 (Jeff): one chest per strike, machines as before.** The scene is staged at `strike.SceneTarget` (the night's chest, else one machine taken) when its `Location` is the Farm, a FarmHouse, a Cellar or a Shed, else no scene. For a machine target the Brute walks to the machine, a beat, and it vanishes with everything else at `ApplyStrike()` (no lid). `PickChest` below becomes `SceneTargetOnFarm(strike) : SpoilagePass.Hit`; ignore the GroupBy version.

**Files:** Modify `src/TheLongestYear/Scenes/ThiefScene.cs`.

Read first: `Monsters/ShadowBrute.cs` (sheet `Characters\Monsters\Shadow Brute`, its walk frames), `Objects/Chest.cs` (the lid animation: `chest.frameCounter`, `chest.uses`, `openChestSound`; find the fields `Chest.draw` reads to show an open lid and drive those), `Locations/FarmHouse.cs` (`getSpouseBedSpot`, `GetSpouseBed`, `getChildBed` / crib tiles, `getEntryLocation`).

- [ ] **Step 1: `PickChest`** (static, used by the factory):

```csharp
/// <summary>The chest the thief is seen at: the one that loses the most tonight among chests on
/// the farm's own maps. Null when every hit is a machine or is off the farm (no scene then).</summary>
internal static Chest PickChest(PendingStrike strike)
    => strike.Hits.Where(h => !h.Machine && h.Chest != null && IsFarmMap(h.Location))
        .GroupBy(h => h.Chest).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault();

private static bool IsFarmMap(GameLocation loc)
    => loc is Farm || loc is FarmHouse || loc is Cellar || loc is Shed;
```

Note for Jeff's summary, not for code: the strike takes units across every chest, not from one chest. The scene shows the worst-hit chest and every unit goes at the same beat.

- [ ] **Step 2: Stage.** Location = the picked chest's `Location`. Entry tile: indoors, the location's first warp tile (the door); on the Farm, the nearest map edge tile to the chest that is passable. Path: `PathFindController.findPath` style is overkill; walk a straight Manhattan line (x first, then y) and, if any tile on it is impassable, fall back to the Brute already standing 3 tiles from the chest at fade-in. In a `FarmHouse`: for the scene, place the spouse at `getSpouseBedSpot`-adjacent sleeping pose and each child in its bed or crib (set position and `isSleeping`-style sprite frame; read `FarmHouse` for what the game itself does at 2600). Record their prior positions and restore them at the end; the new day repositions them anyway. The farmer is already in bed.

- [ ] **Step 3: Build:**

| At | What |
| --- | --- |
| 0 | cut to the location, camera on the chest, fade in 600 |
| 600 | Brute walks in (2.5 tiles a second), footsteps `shadowpeep` low |
| walk end (T) | beat 400 |
| T+400 | lid opens: drive the chest's open animation, `Game1.playSound("openChest")` |
| T+1000 | `ApplyStrike()` |
| T+1400 | lid closes, `doorCreakReverse` |
| T+1700 | Brute turns to face the camera (down), eyes red (same 2x2 overlay, plus a small red light), holds 500 |
| T+2200 | runs out the way it came at 5 tiles a second |
| end | fade 800, restore family positions and camera |

Clamp the whole scene to 9000 ms: if the walk would take longer, start the Brute closer.

- [ ] **Step 4: Debug** `tly_sabotage scene thief`: fresh `SpoilagePass.Plan`; with nothing stored, report "nothing stored, no thief" and do nothing.

- [ ] **Step 5: Live checks (automated runs), screenshots:** a chest on the Farm; a chest in a Shed; a chest in the FarmHouse on a married save with one child (use `debug marry Penny`, `debug child`, sleep the days needed), with the farmer, the spouse and the child all in bed in the frame. A chest on a Circle of Warding must never be the picked chest (it is never hit).

- [ ] **Step 6: Commit and push.** `git commit -m "scenes: the thief"`

---

### Task 9: The hall

**Files:** Modify `src/TheLongestYear/Scenes/HallScene.cs`.

Read first: the Town map's Community Center building tiles (front door at Town (52, 20) in vanilla; confirm in `Locations/Town.cs` and the map) and where Shane's walk home from the Saloon passes (the Saloon door is at Town (45, 71); he is staged, not pathed).

- [ ] **Step 1: Stage.** Location `Town`. Camera on the Community Center's front, framed so the full facade and the path below it are visible. Window rects: measure the four to six front windows in screen pixels from a screenshot of the facade at zoom 1 and store them as tile-relative `Rectangle`s in a static array.

- [ ] **Step 2: Paint.** Behind-the-glass glow: for each window rect fill with `new Color(255, 140, 40)` at alpha `0.55 + 0.15 * sin(ElapsedMs / 180.0 + i)` (firelight flicker), add one warm `LightSource` per window. Shadows: two or three black rounded shapes (a stretched `Game1.shadowTexture`) sliding across the windows left to right at different speeds and phases, clipped to each window rect with a scissor rectangle.

- [ ] **Step 3: Build:**

| At | What |
| --- | --- |
| 0 | cut to Town, fade in 700, glow and shadows already running |
| 1500 | Shane (scene-drawn `Characters\Shane`) walks in from the bottom-left of the frame |
| 3000 | stops dead, faces the hall |
| 3300 | small jump (the vanilla `jump` arc: y offset 0 to -16 to 0 over 300 ms), `ApplyStrike()` |
| 3800 | backs away two tiles, still facing the hall |
| 4600 | turns and runs out the way he came |
| 5400 | hold on the windows |
| 6200 | fade 800 |
| 7000 | end |

- [ ] **Step 4: Debug** `tly_sabotage scene hall`: fresh reversion pick at the current level; with no candidate, a no-op effect so the scene can still be watched.

- [ ] **Step 5: Live check, screenshots** at 1000, 3300, 4200, 5600. The glow must sit inside the window frames at every zoom level the game allows (test zoom 75% and 100%).

- [ ] **Step 6: Commit and push.** `git commit -m "scenes: shadows in the hall"`

---

### Task 10: The cloud

**Files:** Modify `src/TheLongestYear/Scenes/CloudScene.cs`.

Read first: `Menus/MapPage.cs` and `Data/WorldMaps` handling in 1.6 (`WorldMapManager`): how the map tab picks its texture per season and scales it to the screen, and the farm's position on it. Use the same texture and the same fit so the scene matches the map tab exactly. Do not open the real menu.

- [ ] **Step 1: Paint (above everything).** Black backdrop, then the Winter world map texture centred and scaled as `MapPage` does. The cloud is drawn procedurally: 40 soft blobs (`Game1.mouseCursors` cloud sprite or a generated radial-gradient `Texture2D` built once in `Stage`), tinted `new Color(20, 0, 30)`, each with its own path from a spawn line along the map's top-right (the mountain and mines side) to a rest position. Rest positions: 60% scattered over the whole map, 40% packed over the farm's rect, so the cloud covers the valley and settles thickest on the farm. Blob alpha ramps 0 to 0.7. A full-map dim (`Color.Black * 0.35`) eases in with the cloud.

- [ ] **Step 2: Build:**

| At | What |
| --- | --- |
| 0 | map fades in over 800 |
| 1500 | blobs begin to pour in, staggered over 3000 |
| 5500 | farm blobs reach rest; dim at full; `Game1.playSound("shadowDie")` low |
| 6500 | `ApplyStrike()` |
| 8500 | fade 1200 |
| 9700 | end |

- [ ] **Step 3: Debug** `tly_sabotage scene cloud`: a fresh tamper plan; with none, a no-op effect.

- [ ] **Step 4: Live check, screenshots** at 1000, 3500, 6000, 9000, at 1280x720 and at Jeff's native resolution. The farm must be visibly the darkest place on the map. The morning after must still play the Junimo "tainted" scene and the popup, unchanged.

- [ ] **Step 5: Commit and push.** `git commit -m "scenes: the cloud over the valley"`

---

### Task 11: The dark aura

**Files:** Create `src/TheLongestYear/Loop/TaintedAuraPatch.cs`. Modify `ModEntry` (set the static lookup).

**Interfaces:** Consumes `RunState.Tampers` (`TamperRecord.OldItemId`, qualified ids such as `(O)24`).

- [ ] **Step 1:** Harmony PREFIX on `StardewValley.Object.drawInMenu(SpriteBatch, Vector2, float, float, float, StackDrawType, Color, bool)` (confirm the 1.6 signature in the decompile) that draws the aura under the item, then lets the original run:

```csharp
internal static class TaintedAuraPatch
{
    /// <summary>Set by ModEntry: the qualified item ids tampered away this loop.</summary>
    internal static Func<ISet<string>> Tainted;

    private static void Prefix(StardewValley.Object __instance, SpriteBatch spriteBatch, Vector2 location, float scaleSize, float layerDepth)
    {
        ISet<string> ids = Tainted?.Invoke();
        if (ids == null || ids.Count == 0 || !ids.Contains(__instance.QualifiedItemId)) return;
        float pulse = 0.45f + 0.20f * (float)Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 450.0);
        spriteBatch.Draw(Game1.shadowTexture, location + new Vector2(32f, 32f), Game1.shadowTexture.Bounds,
            new Color(90, 0, 130) * pulse, 0f, new Vector2(Game1.shadowTexture.Bounds.Center.X, Game1.shadowTexture.Bounds.Center.Y),
            4.2f * scaleSize, SpriteEffects.None, Math.Max(0f, layerDepth - 0.0001f));
    }
}
```

ModEntry keeps a cached `HashSet<string>` rebuilt when `Run.Tampers.Count` changes (a draw call must not allocate), and returns it from `Tainted`. Patch manually in `ModEntry` with `harmony.Patch(AccessTools.Method(typeof(StardewValley.Object), nameof(StardewValley.Object.drawInMenu), <parameter types>), prefix: ...)` so an overload mismatch fails loudly at startup.

- [ ] **Step 2:** The held-overhead draw (`Object.drawWhenHeld`): same aura at the held position. Skip placed-in-world draws; the spec says wherever an ITEM is drawn.

- [ ] **Step 3: Live check, screenshots:** after `tly_sabotage tamper`, the old item in the inventory, in a chest and on the bundle page shows the pulse; an untouched item does not; after `tly_reset` nothing does.

- [ ] **Step 4: Commit and push.** `git commit -m "darkness: tainted items carry a dark aura"`

---

### Task 12: Witness dialogue

**Files:**
- Create: `src/TheLongestYear.Core/Sabotage/WitnessLines.cs`, `src/TheLongestYear/Loop/WitnessDialogueService.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs`, `src/TheLongestYear/i18n/default.json`, `src/TheLongestYear/ModEntry.cs`
- Test: `tests/TheLongestYear.Tests/WitnessLinesTests.cs`, `I18nGuardTests.cs`

**Interfaces:**
- Produces: `WitnessRecord { string Npc; int SceneDayOfYear; bool Said; }`; `RunState.WitnessLines : List<WitnessRecord>` (cleared at reset); `WitnessLines.WindowDays` (7); `WitnessLines.NpcFor(DarknessEvent) : string` ("Linus", "Shane" or null); `WitnessLines.LineKey(string npc)`; `WitnessLines.WhenKey(int sceneDay, int today)`; `WitnessLines.IsLive(WitnessRecord, int today)`; `WitnessLines.AllKeys`.

- [ ] **Step 1: Failing tests**

```csharp
public class WitnessLinesTests
{
    [Fact] public void Crows_are_seen_by_Linus() => Assert.Equal("Linus", WitnessLines.NpcFor(DarknessEvent.CropBlight));
    [Fact] public void The_hall_is_seen_by_Shane() => Assert.Equal("Shane", WitnessLines.NpcFor(DarknessEvent.Reversion));
    [Theory]
    [InlineData(DarknessEvent.ChestBlight)]
    [InlineData(DarknessEvent.Tampering)]
    public void Nobody_sees_the_thief_or_the_cloud(DarknessEvent e) => Assert.Null(WitnessLines.NpcFor(e));

    [Fact] public void The_morning_after_says_last_night()
        => Assert.Equal("dialogue.witness.when-last-night", WitnessLines.WhenKey(sceneDay: 40, today: 41));
    [Theory]
    [InlineData(42)]
    [InlineData(47)]
    public void Any_later_day_says_the_other_night(int today)
        => Assert.Equal("dialogue.witness.when-other-night", WitnessLines.WhenKey(40, today));

    [Theory]
    [InlineData(41, true)]
    [InlineData(47, true)]
    [InlineData(48, false)]
    [InlineData(40, false)]
    public void The_line_is_live_for_seven_days_after_the_scene(int today, bool live)
        => Assert.Equal(live, WitnessLines.IsLive(new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40 }, today));

    [Fact] public void A_line_already_said_is_not_live()
        => Assert.False(WitnessLines.IsLive(new WitnessRecord { Npc = "Linus", SceneDayOfYear = 40, Said = true }, 41));
}
```

The scene plays on the night of day N; "the morning after" is day N+1. `SceneDayOfYear` stores N.

- [ ] **Step 2: Implement `WitnessLines.cs`**

```csharp
namespace TheLongestYear.Core.Sabotage;

public sealed class WitnessRecord
{
    public string Npc { get; set; } = "";
    public int SceneDayOfYear { get; set; }
    public bool Said { get; set; }
}

/// <summary>A villager who saw a strike scene says so once, the next time the player talks to
/// him within a week (spec 2026-09-21). Every loop: the town forgets, so he is shaken afresh.</summary>
public static class WitnessLines
{
    public const int WindowDays = 7;
    public const string WhenLastNight = "dialogue.witness.when-last-night";
    public const string WhenOtherNight = "dialogue.witness.when-other-night";

    public static string NpcFor(DarknessEvent e) => e switch
    {
        DarknessEvent.CropBlight => "Linus",
        DarknessEvent.Reversion => "Shane",
        _ => null,
    };

    public static string LineKey(string npc) => "dialogue.witness." + npc.ToLowerInvariant();

    public static string WhenKey(int sceneDay, int today) => today - sceneDay <= 1 ? WhenLastNight : WhenOtherNight;

    public static bool IsLive(WitnessRecord record, int today)
        => record != null && !record.Said && today > record.SceneDayOfYear && today - record.SceneDayOfYear <= WindowDays;

    public static IReadOnlyList<string> AllKeys { get; } = new[] { LineKey("Linus"), LineKey("Shane"), WhenLastNight, WhenOtherNight };
}
```

(add `using System.Collections.Generic;`). `RunState`: `public List<Sabotage.WitnessRecord> WitnessLines { get; set; } = new();`, cleared in the reset block.

- [ ] **Step 3: i18n** (Jeff's lines, verbatim):

```json
"dialogue.witness.linus": "I was out walking {{when}} and I passed your farm. I've never seen crows like that before. I haven't slept well since.",
"dialogue.witness.shane": "When I left the bar {{when}} I could swear I saw something moving in the old Community Center. I'd had a few drinks, but not that many.",
"dialogue.witness.when-last-night": "last night",
"dialogue.witness.when-other-night": "the other night",
```

Walk `WitnessLines.AllKeys` in `I18nGuardTests`.

- [ ] **Step 4: `WitnessDialogueService`**

```csharp
internal sealed class WitnessDialogueService
{
    private readonly IMonitor _monitor; private readonly MetaStore _store;
    private const string Marker = "TLY_witness";
    public WitnessDialogueService(IMonitor monitor, MetaStore store) { _monitor = monitor; _store = store; }

    /// <summary>A scene just played (or was skipped): its witness, if it has one, owes a line.</summary>
    public void OnScenePlayed(DarknessEvent e)
    {
        string npc = WitnessLines.NpcFor(e);
        if (npc == null) return;
        RunState run = _store.Run;
        (run.WitnessLines ??= new()).Add(new WitnessRecord { Npc = npc, SceneDayOfYear = Calendar.DayOfYear((int)run.Season, run.DayOfMonth) });
    }

    /// <summary>Day start: put each live line on top of its NPC's dialogue, drop the dead ones.</summary>
    public void OnDayStarted()
    {
        RunState run = _store.Run;
        if (run.WitnessLines == null || run.WitnessLines.Count == 0) return;
        int today = Calendar.DayOfYear((int)run.Season, run.DayOfMonth);
        run.WitnessLines.RemoveAll(r => r.Said || today - r.SceneDayOfYear > WitnessLines.WindowDays);
        foreach (WitnessRecord r in run.WitnessLines)
        {
            if (!WitnessLines.IsLive(r, today)) continue;
            NPC npc = Game1.getCharacterFromName(r.Npc);
            if (npc == null) continue;
            string when = Strings.Get(WitnessLines.WhenKey(r.SceneDayOfYear, today));
            // Literal keys and inline token dictionaries: I18nGuardTests scans for both.
            string text = r.Npc == "Linus"
                ? Strings.Get("dialogue.witness.linus", new Dictionary<string, string> { ["when"] = when })
                : Strings.Get("dialogue.witness.shane", new Dictionary<string, string> { ["when"] = when });
            WitnessRecord record = r;
            var dialogue = new Dialogue(npc, Marker, text) { onFinish = () => record.Said = true };
            npc.CurrentDialogue.Push(dialogue);
        }
    }
}
```

`CurrentDialogue` is rebuilt every morning, so the push is redone each day with that day's `{{when}}`. Pushing onto the stack shows the witness line first and leaves the NPC's normal line for the next talk; that is "in place of" for the first conversation, which is what the spec asks. Confirm `Dialogue.onFinish` exists in 1.6 (it does in `Dialogue.cs`); if the player closes the box early it still fires on exit.

Wire in `ModEntry`: construct `_witness`, call `_witness.OnDayStarted()` from `OnDayStarted` after the run's day is synced, and add the `_witness?.OnScenePlayed(...)` call in the Task 6 factory lambda. The scene plays during the night of day N, before `Run.DayOfMonth` rolls, so `OnScenePlayed` records N. Verify that with a log line in the live check.

- [ ] **Step 5: Live check (automated run):** `tly_sabotage arm blight crops`, sleep, `debug warp Mountain`, talk to Linus through `tly_talk Linus` if it exists, else walk the farmer with `debug` and use `tly_eventstep` to read the box. Expected text contains "last night". Do not talk; `tly_setday` +3, check Linus's top dialogue contains "the other night". +8 days: the line is gone.

- [ ] **Step 6: Commit and push.** `git commit -m "darkness: Linus and Shane say what they saw, once, within a week"`

---

### Task 13: Status, docs, the collision check, Jeff's pass

**Files:** `src/TheLongestYear/Loop/SabotageService.cs` (`Status()`), `docs/HEADLESS_DRIVING.md`, `STATUS.md`, `TODO.md`, `CHANGELOG.md`, the two older specs.

- [ ] **Step 1: `Status()`** adds three lines: `Struck this loop: ...`, `Scenes played this loop: ... / seen on save: ...`, `Witness lines pending: Linus (day 40), ...`, and the guarantee's state: `Owed tonight: ...` from `StrikeGuarantee.Owed`.

- [ ] **Step 2: The slot collision (automated run).** On a throwaway farm complete the Vault (`tly_payvault` each season or `debug completecc`-style shortcut that queues the bus repair `WorldChangeEvent`), arm a crop blight the same night, sleep. Expected: the bus repair plays, log `applying tonight's CropBlight without its scene (WorldChangeEvent has the overnight slot)`, crops dead in the morning, `CropBlight` NOT in scenes played, and the next armed crop blight plays the crows.

- [ ] **Step 3: The guarantee, live.** Summer on a throwaway farm with crops and a full chest, `tly_setday 14`, make sure `tly_sabotage status` shows nothing struck, sleep twice. Expected: crows on the night of 15, thief on the night of 16, log `... has not struck this loop by Summer 15; forced tonight.` With a Ward of the Fields: Summer bought: no crows, the thief on 15.

- [ ] **Step 4: Docs.** `HEADLESS_DRIVING.md`: the `tly_sabotage scene` commands, the night order found in Task 5 Step 7, the collision recipe. Older specs: one line at the top of `2026-09-09-darkness-pushback-design.md` ("First-strike letters removed and presentation replaced by `2026-09-21-darkness-agents-and-gate-scenes-design.md`") and of `2026-09-14-darkness-rework-design.md` ("A per-loop guarantee was added by ..."). `CHANGELOG.md` under Unreleased, in player words, no file names. `STATUS.md` and `TODO.md`: what is built, what is deployed, what is pushed, and the pass Jeff owes.

- [ ] **Step 5: Hand to Jeff** (his launch, ask first): a developed throwaway farm on Summer 14 so he sleeps into the crows and the thief, talks to Linus; Fall 14 for the hall and Shane; Winter 1 eve for the cloud, the aura and the gate scene's held glow.

- [ ] **Step 6: Commit and push.** `git commit -m "docs: darkness agents built; status, headless notes, changelog"`
