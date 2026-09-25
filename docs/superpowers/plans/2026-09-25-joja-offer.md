# Morris's Offer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Pay off Morris's opening line with a JojaMart side quest: an offer scene each loop, a cashier who won't sell until the player answers, escalating letters, a permanent blacklist on No, and a staged bad ending plus a Game Over screen on Yes that returns to the title without saving.

**Architecture:** Pure rules and state live in Core (`Joja/JojaOffer.cs`, fields on `RunState` and `MetaState`) and are unit tested. The game side is four small pieces: a letter service (Data/Mail + mailbox), a Harmony patch that takes over Morris's counter and the cashier, two event scripts (the offer scene, the bad ending) played with the mod's existing event commands plus a few new ones, and a Game Over menu that exits to the title.

**Tech Stack:** C# / .NET 6, SMAPI 4.4, Stardew Valley 1.6.15, HarmonyLib, xUnit. Build `dotnet build -c Release` (deploys to the game's Mods folder when the game is closed), tests `dotnet test -c Release --no-build`.

**Spec:** `docs/superpowers/specs/2026-09-25-joja-offer-design.md`

## Global Constraints

- Branch `story`. Do NOT bump `manifest.json`'s Version (it stays at the branch-point value `0.18.15`).
- Commit after every task; push right after (`git push origin story`). End every commit message with:
  `Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>` and `Claude-Session: https://claude.ai/code/session_01MLy6J9QCsqjg9XvYFmm4UG`
- Every player-facing string lives in `src/TheLongestYear/i18n/default.json` and is read with `Strings.Get(key)`. No em dashes, no semicolons in player text.
- Jeff's two lines are used verbatim:
  - `joja.morris.position-filled`: "I'm sorry, the position I mentioned has already been filled. We've determined that you aren't to be trusted. Please leave immediately."
  - `joja.gameover.message`: "The Junimos were forced to abandon the valley, and Joja was left to take over unopposed."
- Every other line is drafted with the game-writing skill and approved by Jeff (Task 1) before any code task starts.
- Never guess a tile or a walking route: take a gridded screenshot of the map first (method in Task 5, Step 1). Event moves ignore walls.
- Every custom event command never throws: log and skip (`evt.CurrentCommand++`), like `EndingEventCommands`.
- Everything is dormant on non-TLY saves: every patch and service checks `Core.RunActivation.IsActive` first.
- Game code facts this plan relies on (PC decompile at `C:\Users\Jeff\Documents\Projects\Stardee Valoo\decompiled-pc\Stardew Valley`):
  - Morris's counter is the tile action `JoinJoja`, handled in `JojaMart.checkAction` (JojaMart.cs:61). The talking NPC is the static `JojaMart.Morris`.
  - The cashier is the tile action `JojaShop`, handled in `GameLocation.performAction(string[] action, Farmer who, Location tileLocation)` (GameLocation.cs:8446, case at 8985).
  - A question with a portrait: dialogue text `$y '<question>_<answer0>_<unused>_<answer1>_<unused>'` plus `Dialogue.answerQuestionBehavior = i => {...}`; `chooseResponse` calls it with the answer index (Dialogue.cs:777 and 1444).
  - `Calendar.DayOfYear(monthIndex, dayOfMonth)` = `monthIndex * 28 + dayOfMonth` (1..112).

## Review Focus

1. **A reload after saying Yes.** Yes must never be written to disk: the player reloads onto that morning, still undecided, and Morris asks again. Test: `JojaOfferTests.Yes_changes_no_state` (Task 2) and the headless check that no save folder timestamp changed (Task 8).
2. **A rewind or voluntary restart mid-quest.** Per-loop state (scene seen, letters, 4-week clock) clears; Rejected never does. Test: `BeginNewRun_clears_the_loop_state_but_not_the_rejection` (Task 2).
3. **The 4-week clock across a season change and near year end.** Letters fall on day-of-year `seen + 7k`; a clock that would run past day 112 simply never finishes that loop. Test: `Decision_letters_fall_weekly_after_the_scene_and_stop_at_four` and `Decision_clock_past_winter_28_never_fires` (Task 2).
4. **The same loop vs a later loop after a rejection.** Morris repeats the refusal the same loop, and says the "position filled" line from the next loop on, even on Spring 1. Test: `Morris_line_after_rejection_depends_on_the_loop` (Task 2).
5. **Entering JojaMart during a festival, an event, or with a menu open.** The offer scene waits until the player is free; it never starts inside another event. Test: `Scene_waits_while_busy` (Task 2, via `JojaOffer.ShouldPlayScene` taking a `busy` flag).

---

### Task 1: Words (gate, done by the controller, not a subagent)

**Files:**
- Modify: `src/TheLongestYear/i18n/default.json` (new keys, added after `"dialog.joja"`)

- [ ] **Step 1: Draft every line with the game-writing skill.** Keys and what each says:
  - `event.joja-offer.morris-1` .. `-3`: the offer scene. Morris greets the farmer by name, repeats the assistant manager offer, Joja buys the farm for a huge sum to build a meat processing plant, the job comes with company housing, transportation and excellent benefits, "the whole package", ends with "Think about it."
  - `joja.cashier.undecided`: "I'm not supposed to let you buy anything until you talk to the manager." (Jeff's wording, keep)
  - `joja.cashier.refused`: the cashier refuses service to a blacklisted player.
  - `joja.morris.ask`: Morris asks if the player accepts. `joja.morris.accept` / `joja.morris.decline`: the two answer labels.
  - `joja.morris.refuse`: after No. Joja reserves the right to refuse service to anyone for any reason, the player may not shop here, and Morris will make sure they never work for Joja again.
  - `joja.morris.refuse-again`: the same loop, talking to him again after the rejection.
  - `joja.morris.position-filled`: Jeff's line verbatim (Global Constraints).
  - `mail.joja.come-1` .. `-8` and `mail.joja.come.title`: friendly but insistent, each one less friendly and more insistent. Signed "-Morris".
  - `mail.joja.decide-1` .. `-4` and `mail.joja.decide.title`: expectant, annoyed, demanding, then the rejection with the blacklisting threat. Signed "-Morris".
  - `joja.gameover.title`: "Game Over". `joja.gameover.message`: Jeff's line verbatim. `joja.gameover.button`: "Return to title".
- [ ] **Step 2: Show Jeff every drafted line verbatim in chat and wait for his OK.** Apply his edits verbatim.
- [ ] **Step 3: Add the approved keys to `default.json`** (UTF-8 with BOM, keep the file's existing formatting; `/` and `"` are allowed in i18n but event lines are sanitised by the injector).
- [ ] **Step 4: Commit** `i18n: Morris's offer lines (approved by Jeff)`.

---

### Task 2: Core rules and state

**Files:**
- Create: `src/TheLongestYear.Core/Joja/JojaOffer.cs`
- Modify: `src/TheLongestYear.Core/RunState.cs` (4 properties + clear in `BeginNewRun`)
- Modify: `src/TheLongestYear.Core/MetaState.cs` (2 properties)
- Test: `tests/TheLongestYear.Tests/JojaOfferTests.cs`

**Interfaces:**
- Produces (used by Tasks 3-7):
  - `RunState.JojaSceneSeenDay` (int, -1 = not this loop), `RunState.JojaLetterDays` (List<int>, empty = not planned), `RunState.JojaLettersSent` (int), `RunState.JojaDecisionLettersSent` (int)
  - `MetaState.JojaOfferEverSeen` (bool), `MetaState.JojaRejectedLoop` (int, 0 = not rejected)
  - `enum JojaMorrisLine { Ask, RefuseAgain, PositionFilled }`, `enum JojaCashierLine { Undecided, Refused }`
  - `static class JojaOffer` with: `const int ComeLetters = 8`, `const int DecisionLetters = 4`, `const int DaysPerWeek = 7`,
    `bool IsRejected(MetaState)`, `bool ShouldPlayScene(RunState, MetaState, bool busy)`, `bool SceneSkippable(MetaState)`,
    `void MarkSceneSeen(RunState, MetaState, int dayOfYear)`, `List<int> PlanLetterDays(int seed)`,
    `int ComeLetterDue(RunState, MetaState, int dayOfYear)` (1..8, or 0 = none),
    `int DecisionLetterDue(RunState, MetaState, int dayOfYear)` (1..4, or 0 = none),
    `void Reject(MetaState, int runNumber)`, `JojaMorrisLine MorrisLine(RunState, MetaState, int runNumber)`,
    `JojaCashierLine CashierLine(MetaState)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Morris's offer (spec 2026-09-25-joja-offer-design): the scene, the letters, the blacklist.</summary>
public class JojaOfferTests
{
    private static (RunState run, MetaState meta) Fresh() => (new RunState(), new MetaState());

    [Fact]
    public void Scene_plays_once_a_loop_until_rejected()
    {
        var (run, meta) = Fresh();
        Assert.True(JojaOffer.ShouldPlayScene(run, meta, busy: false));
        JojaOffer.MarkSceneSeen(run, meta, dayOfYear: 10);
        Assert.False(JojaOffer.ShouldPlayScene(run, meta, busy: false));
        run.BeginNewRun(seed: 1);
        Assert.True(JojaOffer.ShouldPlayScene(run, meta, busy: false));
        JojaOffer.Reject(meta, runNumber: run.RunNumber);
        Assert.False(JojaOffer.ShouldPlayScene(run, meta, busy: false));
    }

    [Fact]
    public void Scene_waits_while_busy()
    {
        var (run, meta) = Fresh();
        Assert.False(JojaOffer.ShouldPlayScene(run, meta, busy: true));
    }

    [Fact]
    public void Scene_is_skippable_only_after_it_has_ever_been_seen()
    {
        var (run, meta) = Fresh();
        Assert.False(JojaOffer.SceneSkippable(meta));
        JojaOffer.MarkSceneSeen(run, meta, 3);
        run.BeginNewRun(2);
        Assert.True(JojaOffer.SceneSkippable(meta));
    }

    [Fact]
    public void Letter_days_are_two_per_season_inside_the_season_and_repeatable()
    {
        List<int> days = JojaOffer.PlanLetterDays(seed: 42);
        Assert.Equal(JojaOffer.ComeLetters, days.Count);
        for (int season = 0; season < 4; season++)
        {
            var inSeason = days.Where(d => (d - 1) / 28 == season).ToList();
            Assert.Equal(2, inSeason.Count);
            Assert.All(inSeason, d => Assert.InRange((d - 1) % 28 + 1, 2, 27));
            Assert.NotEqual(inSeason[0], inSeason[1]);
        }
        Assert.Equal(days, JojaOffer.PlanLetterDays(seed: 42));
        Assert.Equal(days.OrderBy(d => d), days);
    }

    [Fact]
    public void Come_letters_go_out_in_order_on_their_days_and_stop_once_the_scene_is_seen()
    {
        var (run, meta) = Fresh();
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        Assert.Equal(0, JojaOffer.ComeLetterDue(run, meta, 4));
        Assert.Equal(1, JojaOffer.ComeLetterDue(run, meta, 5));
        run.JojaLettersSent = 1;
        Assert.Equal(0, JojaOffer.ComeLetterDue(run, meta, 5));
        Assert.Equal(2, JojaOffer.ComeLetterDue(run, meta, 21));   // a missed day still delivers the next letter
        JojaOffer.MarkSceneSeen(run, meta, 22);
        Assert.Equal(0, JojaOffer.ComeLetterDue(run, meta, 33));
    }

    [Fact]
    public void No_come_letters_after_a_rejection_in_any_loop()
    {
        var (run, meta) = Fresh();
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        JojaOffer.Reject(meta, 1);
        run.BeginNewRun(9);
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        Assert.Equal(0, JojaOffer.ComeLetterDue(run, meta, 5));
    }

    [Fact]
    public void Decision_letters_fall_weekly_after_the_scene_and_stop_at_four()
    {
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 26);          // Spring 26
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 32));
        Assert.Equal(1, JojaOffer.DecisionLetterDue(run, meta, 33));   // crosses into Summer
        run.JojaDecisionLettersSent = 1;
        Assert.Equal(2, JojaOffer.DecisionLetterDue(run, meta, 40));
        run.JojaDecisionLettersSent = 4;
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 80));
    }

    [Fact]
    public void Decision_clock_past_winter_28_never_fires()
    {
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 110);
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 112));
    }

    [Fact]
    public void No_decision_letters_before_the_scene_or_after_a_rejection()
    {
        var (run, meta) = Fresh();
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 50));
        JojaOffer.MarkSceneSeen(run, meta, 10);
        JojaOffer.Reject(meta, run.RunNumber);
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 17));
    }

    [Fact]
    public void Morris_line_after_rejection_depends_on_the_loop()
    {
        var (run, meta) = Fresh();
        Assert.Equal(JojaMorrisLine.Ask, JojaOffer.MorrisLine(run, meta, run.RunNumber));
        JojaOffer.Reject(meta, run.RunNumber);
        Assert.Equal(JojaMorrisLine.RefuseAgain, JojaOffer.MorrisLine(run, meta, run.RunNumber));
        run.BeginNewRun(3);
        Assert.Equal(JojaMorrisLine.PositionFilled, JojaOffer.MorrisLine(run, meta, run.RunNumber));
    }

    [Fact]
    public void Cashier_refuses_until_answered_then_for_good_after_a_rejection()
    {
        var meta = new MetaState();
        Assert.Equal(JojaCashierLine.Undecided, JojaOffer.CashierLine(meta));
        JojaOffer.Reject(meta, 1);
        Assert.Equal(JojaCashierLine.Refused, JojaOffer.CashierLine(meta));
    }

    [Fact]
    public void BeginNewRun_clears_the_loop_state_but_not_the_rejection()
    {
        var (run, meta) = Fresh();
        run.JojaLetterDays = new List<int> { 5 };
        run.JojaLettersSent = 3;
        JojaOffer.MarkSceneSeen(run, meta, 40);
        run.JojaDecisionLettersSent = 2;
        JojaOffer.Reject(meta, run.RunNumber);
        run.BeginNewRun(7);
        Assert.Equal(-1, run.JojaSceneSeenDay);
        Assert.Empty(run.JojaLetterDays);
        Assert.Equal(0, run.JojaLettersSent);
        Assert.Equal(0, run.JojaDecisionLettersSent);
        Assert.True(JojaOffer.IsRejected(meta));
        Assert.True(meta.JojaOfferEverSeen);
    }

    [Fact]
    public void A_second_rejection_keeps_the_first_loop()
    {
        var meta = new MetaState();
        JojaOffer.Reject(meta, 2);
        JojaOffer.Reject(meta, 5);
        Assert.Equal(2, meta.JojaRejectedLoop);
    }

    [Fact]
    public void Yes_changes_no_state()
    {
        // Yes is handled entirely on the game side (bad ending, exit to title without saving).
        // Nothing in Core records it: a fresh state asks again.
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 10);
        Assert.Equal(JojaMorrisLine.Ask, JojaOffer.MorrisLine(run, meta, run.RunNumber));
        Assert.False(JojaOffer.IsRejected(meta));
    }

    [Fact]
    public void Old_saves_load_with_nothing_happened()
    {
        var run = Newtonsoft.Json.JsonConvert.DeserializeObject<RunState>("{\"RunNumber\":3}")!;
        var meta = Newtonsoft.Json.JsonConvert.DeserializeObject<MetaState>("{}")!;
        Assert.Equal(-1, run.JojaSceneSeenDay);
        Assert.NotNull(run.JojaLetterDays);
        Assert.False(JojaOffer.IsRejected(meta));
        Assert.False(meta.JojaOfferEverSeen);
    }
}
```

- [ ] **Step 2: Run to verify they fail**

Run: `dotnet build tests/TheLongestYear.Tests -c Release` then `dotnet test -c Release --no-build --filter JojaOfferTests`
Expected: build FAILS (`JojaOffer` does not exist).

- [ ] **Step 3: Add the state**

In `RunState.cs`, before `BeginNewRun`:

```csharp
    // ---- Morris's offer (spec 2026-09-25-joja-offer-design). Per loop: a rewind starts all four over. ----

    /// <summary>Day of year (1..112) the offer scene played this loop, -1 = not yet this loop.</summary>
    public int JojaSceneSeenDay { get; set; } = -1;

    /// <summary>This loop's eight "come see me" letter days (day of year), planned on the first
    /// morning that needs them. Empty = not planned yet.</summary>
    public List<int> JojaLetterDays { get; set; } = new();

    /// <summary>How many "come see me" letters went out this loop.</summary>
    public int JojaLettersSent { get; set; }

    /// <summary>How many "make a decision" letters went out this loop.</summary>
    public int JojaDecisionLettersSent { get; set; }
```

At the end of `BeginNewRun`, after `(StrikeScenesPlayed ??= new()).Clear();`:

```csharp
        JojaSceneSeenDay = -1;
        (JojaLetterDays ??= new()).Clear();
        JojaLettersSent = 0;
        JojaDecisionLettersSent = 0;
```

In `MetaState.cs`, after `StrikeScenesSeen`:

```csharp
    /// <summary>Morris's offer scene has played at least once on this save, so it is skippable.</summary>
    public bool JojaOfferEverSeen { get; set; }

    /// <summary>The loop (RunState.RunNumber) Morris and the player parted ways in: a No at his
    /// counter or the fourth unanswered letter. 0 = never. Permanent: no reset clears it.</summary>
    public int JojaRejectedLoop { get; set; }
```

- [ ] **Step 4: Write `src/TheLongestYear.Core/Joja/JojaOffer.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Joja;

/// <summary>What Morris says when the player walks up to his counter.</summary>
public enum JojaMorrisLine { Ask, RefuseAgain, PositionFilled }

/// <summary>What the cashier says when the player tries to shop.</summary>
public enum JojaCashierLine { Undecided, Refused }

/// <summary>Morris's offer (spec 2026-09-25-joja-offer-design). Pure rules over RunState (per loop)
/// and MetaState (per save). Saying Yes is not recorded anywhere: the game side plays the bad
/// ending and returns to the title without saving.</summary>
public static class JojaOffer
{
    public const int ComeLetters = 8;
    public const int DecisionLetters = 4;
    public const int DaysPerWeek = 7;
    private const int LettersPerSeason = 2;
    private const int FirstLetterDay = 2, LastLetterDay = 27;

    public static bool IsRejected(MetaState meta) => meta.JojaRejectedLoop > 0;

    public static bool ShouldPlayScene(RunState run, MetaState meta, bool busy)
        => !busy && !IsRejected(meta) && run.JojaSceneSeenDay < 0;

    public static bool SceneSkippable(MetaState meta) => meta.JojaOfferEverSeen;

    public static void MarkSceneSeen(RunState run, MetaState meta, int dayOfYear)
    {
        run.JojaSceneSeenDay = dayOfYear;
        meta.JojaOfferEverSeen = true;
    }

    /// <summary>Two distinct days in each season, days 2..27, sorted, from the loop's seed so a
    /// reload plans the same days.</summary>
    public static List<int> PlanLetterDays(int seed)
    {
        var rng = new Random(seed);
        var days = new List<int>();
        for (int season = 0; season < 4; season++)
        {
            var picks = new HashSet<int>();
            while (picks.Count < LettersPerSeason)
                picks.Add(rng.Next(FirstLetterDay, LastLetterDay + 1));
            days.AddRange(picks.OrderBy(d => d).Select(d => Calendar.DayOfYear(season, d)));
        }
        return days;
    }

    /// <summary>The next "come see me" letter (1..8) due this morning, or 0. A day that passed
    /// unseen (a load past it) still delivers the next letter the next morning.</summary>
    public static int ComeLetterDue(RunState run, MetaState meta, int dayOfYear)
    {
        if (IsRejected(meta) || run.JojaSceneSeenDay >= 0) return 0;
        int next = run.JojaLettersSent;
        if (next >= ComeLetters || next >= run.JojaLetterDays.Count) return 0;
        return dayOfYear >= run.JojaLetterDays[next] ? next + 1 : 0;
    }

    /// <summary>The next "make a decision" letter (1..4) due this morning, or 0: one a week after
    /// the scene, while the player has not answered.</summary>
    public static int DecisionLetterDue(RunState run, MetaState meta, int dayOfYear)
    {
        if (IsRejected(meta) || run.JojaSceneSeenDay < 0) return 0;
        int next = run.JojaDecisionLettersSent;
        if (next >= DecisionLetters) return 0;
        int due = run.JojaSceneSeenDay + (next + 1) * DaysPerWeek;
        return dayOfYear >= due ? next + 1 : 0;
    }

    /// <summary>The player and Morris part ways. Keeps the FIRST loop it happened in.</summary>
    public static void Reject(MetaState meta, int runNumber)
    {
        if (!IsRejected(meta)) meta.JojaRejectedLoop = Math.Max(1, runNumber);
    }

    public static JojaMorrisLine MorrisLine(RunState run, MetaState meta, int runNumber)
    {
        if (!IsRejected(meta)) return JojaMorrisLine.Ask;
        return runNumber == meta.JojaRejectedLoop ? JojaMorrisLine.RefuseAgain : JojaMorrisLine.PositionFilled;
    }

    public static JojaCashierLine CashierLine(MetaState meta)
        => IsRejected(meta) ? JojaCashierLine.Refused : JojaCashierLine.Undecided;
}
```

- [ ] **Step 5: Run the tests, expect PASS**

Run: `dotnet build tests/TheLongestYear.Tests -c Release` then `dotnet test -c Release --no-build`
Expected: all pass (previous 3009 + the new ones).

- [ ] **Step 6: Commit** `Core: Morris's offer rules and state (scene, letters, blacklist)`, push.

---

### Task 3: Letters

**Files:**
- Create: `src/TheLongestYear/Loop/JojaLetterService.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (construct at Entry next to `_onboardingMail`; hook AssetRequested; call `OnDayStarted()` next to `_onboardingMail?.OnDayStarted();` at ModEntry.cs:3276; register `tly_joja` debug command, see Step 3)

**Interfaces:**
- Consumes: Task 2's `JojaOffer`, `RunState` fields, `MetaState` fields; `MetaStore.Run`, `MetaStore.State`.
- Produces: mail keys `TLY_JojaCome1`..`TLY_JojaCome8`, `TLY_JojaDecide1`..`TLY_JojaDecide4`; `JojaLetterService.OnDayStarted()`.

- [ ] **Step 1: Write the service**

```csharp
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;

namespace TheLongestYear.Loop
{
    /// <summary>Morris's letters (spec 2026-09-25-joja-offer-design): up to eight "come see me"
    /// letters a loop until the player walks into JojaMart, then one "make a decision" letter a week
    /// for four weeks. The fourth takes the silence as a rejection. None ever again once rejected.</summary>
    internal sealed class JojaLetterService
    {
        public const string ComePrefix = "TLY_JojaCome", DecidePrefix = "TLY_JojaDecide";
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;

        public JojaLetterService(IMonitor monitor, MetaStore meta) { _monitor = monitor; _meta = meta; }

        public void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo("Data/Mail")) return;
            e.Edit(asset =>
            {
                var data = asset.AsDictionary<string, string>().Data;
                for (int i = 1; i <= JojaOffer.ComeLetters; i++)
                    data[ComePrefix + i] = Strings.Get($"mail.joja.come-{i}") + "[#]" + Strings.Get("mail.joja.come.title");
                for (int i = 1; i <= JojaOffer.DecisionLetters; i++)
                    data[DecidePrefix + i] = Strings.Get($"mail.joja.decide-{i}") + "[#]" + Strings.Get("mail.joja.decide.title");
            }, AssetEditPriority.Default);
        }

        /// <summary>Called from ModEntry's DayStarted, after the run-state has synced the date.</summary>
        public void OnDayStarted()
        {
            if (!RunActivation.IsActive || Game1.player == null) return;
            RunState run = _meta.Run;
            MetaState meta = _meta.State;
            if (run.JojaLetterDays == null || run.JojaLetterDays.Count == 0)
                run.JojaLetterDays = JojaOffer.PlanLetterDays(unchecked(run.Seed * 31 + 0x4A6F6A61));
            int today = Calendar.DayOfYear((int)run.Season, run.DayOfMonth);

            int come = JojaOffer.ComeLetterDue(run, meta, today);
            if (come > 0)
            {
                Deliver(ComePrefix + come);
                run.JojaLettersSent = come;
                return;   // at most one Morris letter a morning
            }
            int decide = JojaOffer.DecisionLetterDue(run, meta, today);
            if (decide > 0)
            {
                Deliver(DecidePrefix + decide);
                run.JojaDecisionLettersSent = decide;
                if (decide == JojaOffer.DecisionLetters)
                {
                    JojaOffer.Reject(meta, run.RunNumber);
                    _monitor.Log($"Joja: the fourth decision letter went unanswered; rejected in loop {run.RunNumber}.", LogLevel.Info);
                }
            }
        }

        private void Deliver(string key)
        {
            // mailReceived clears on a rewind, but be safe on a same-loop reload.
            Game1.player.mailReceived.Remove(key);
            if (!Game1.mailbox.Contains(key)) Game1.mailbox.Add(key);
            _monitor.Log($"Joja: letter '{key}' delivered.", LogLevel.Info);
        }
    }
}
```

- [ ] **Step 2: Wire it in `ModEntry.cs`.** Field `private TheLongestYear.Loop.JojaLetterService _jojaLetters;` next to `_onboardingMail`. At Entry right after the `_onboardingMail` AssetRequested line: `_jojaLetters = new TheLongestYear.Loop.JojaLetterService(this.Monitor, _meta); helper.Events.Content.AssetRequested += _jojaLetters.OnAssetRequested;`. In the day-start method, after `_onboardingMail?.OnDayStarted();`: `_jojaLetters?.OnDayStarted();`.

- [ ] **Step 3: Debug command `tly_joja`** (register with the other `helper.ConsoleCommands.Add` lines and in the bridge switch near `case "tly_restart":`). Subcommands: `status` (logs every Joja field and `JojaOffer.MorrisLine`), `letter come <n>` / `letter decide <n>` (adds that mail to the mailbox now), `seen` (MarkSceneSeen today), `unseen` (JojaSceneSeenDay = -1), `reject`, `unreject` (JojaRejectedLoop = 0). Put the handler in `src/TheLongestYear/Debug/JojaDebugCommand.cs` as `internal static class JojaDebugCommand { public static void Run(IMonitor m, MetaStore meta, string[] args) }`, following `HerdBookDebugCommand`.

- [ ] **Step 4: Build and run all tests.** `dotnet build -c Release` (a deploy error just means the game is open; the compile must succeed) and `dotnet test -c Release --no-build`. Expected: pass.

- [ ] **Step 5: Commit** `Joja: Morris's letters (come see me, make a decision) and tly_joja`, push.

---

### Task 4: Morris's counter and the cashier

**Files:**
- Create: `src/TheLongestYear/Loop/JojaCounterPatch.cs`
- Modify: `src/TheLongestYear/Loop/JojaMembershipBlock.cs` (doc comment only: now a safety net)
- Modify: `src/TheLongestYear/ModEntry.cs` (one `JojaCounterPatch.Connect(...)` call in OnSaveLoaded next to `JunimoStashCapPatch.Connect`)

**Interfaces:**
- Consumes: Task 2's `JojaOffer`, `JojaMorrisLine`, `JojaCashierLine`; `MetaStore`.
- Produces: `internal static class JojaCounterPatch` with `static void Connect(IMonitor monitor, MetaStore meta, System.Action startBadEnding)`, plus two debug entry points that run exactly the prefix paths: `static void DebugCounter()` (Morris's counter) and `static void DebugCashier()` (the cashier). Task 4 Step 3 adds `tly_joja counter` and `tly_joja cashier` to the Task 3 debug command, calling these. Task 6 supplies `startBadEnding`; until then ModEntry passes `() => this.Monitor.Log("Joja: Yes (bad ending not built yet).", LogLevel.Warn)`.

- [ ] **Step 1: Write the patch**

```csharp
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;

namespace TheLongestYear.Loop
{
    /// <summary>Morris's offer at the store (spec 2026-09-25-joja-offer-design): Morris's counter
    /// (tile action JoinJoja) asks for the answer or turns the player away, and the cashier (tile
    /// action JojaShop) sells nothing until the player has answered, and nothing ever after a No.
    /// Joja never helps the player anywhere in the mod.</summary>
    internal static class JojaCounterPatch
    {
        private static IMonitor _monitor;
        private static MetaStore _meta;
        private static System.Action _startBadEnding;

        public static void Connect(IMonitor monitor, MetaStore meta, System.Action startBadEnding)
        { _monitor = monitor; _meta = meta; _startBadEnding = startBadEnding; }

        [HarmonyPatch(typeof(JojaMart), nameof(JojaMart.checkAction))]
        internal static class Counter
        {
            private static bool Prefix(JojaMart __instance, xTile.Dimensions.Location tileLocation, ref bool __result)
            {
                if (!RunActivation.IsActive || _meta == null) return true;
                if (__instance.doesTileHaveProperty(tileLocation.X, tileLocation.Y, "Action", "Buildings") != "JoinJoja") return true;
                NPC morris = JojaMart.Morris;
                if (morris == null) return true;
                morris.CurrentDialogue.Clear();
                RunState run = _meta.Run;
                switch (JojaOffer.MorrisLine(run, _meta.State, run.RunNumber))
                {
                    case JojaMorrisLine.Ask:
                        string q = $"$y '{Strings.Get("joja.morris.ask")}_{Strings.Get("joja.morris.accept")}_ _{Strings.Get("joja.morris.decline")}_ '";
                        var ask = new Dialogue(morris, null, q);
                        ask.answerQuestionBehavior = Answer;
                        morris.setNewDialogue(ask);
                        break;
                    case JojaMorrisLine.RefuseAgain:
                        morris.setNewDialogue(new Dialogue(morris, null, Strings.Get("joja.morris.refuse-again")));
                        break;
                    default:
                        morris.setNewDialogue(new Dialogue(morris, null, Strings.Get("joja.morris.position-filled")));
                        break;
                }
                Game1.drawDialogue(morris);
                __result = true;
                return false;
            }

            /// <summary>0 = accept, 1 = decline (the order the $y answers were written in).</summary>
            private static bool Answer(int index)
            {
                NPC morris = JojaMart.Morris;
                if (index == 0)
                {
                    _monitor.Log("Joja: the player accepted Morris's offer; bad ending.", LogLevel.Info);
                    // Leave the dialogue box first; the ending starts on the next tick.
                    DelayedAction.functionAfterDelay(() => _startBadEnding?.Invoke(), 100);
                    return true;
                }
                RunState run = _meta.Run;
                JojaOffer.Reject(_meta.State, run.RunNumber);
                _monitor.Log($"Joja: the player turned Morris down in loop {run.RunNumber}; refused service for good.", LogLevel.Info);
                morris.setNewDialogue(new Dialogue(morris, null, Strings.Get("joja.morris.refuse")));
                Game1.drawDialogue(morris);
                return false;
            }
        }

        [HarmonyPatch(typeof(GameLocation), nameof(GameLocation.performAction),
            new System.Type[] { typeof(string[]), typeof(Farmer), typeof(xTile.Dimensions.Location) })]
        internal static class Cashier
        {
            private static bool Prefix(GameLocation __instance, string[] action, ref bool __result)
            {
                if (!RunActivation.IsActive || _meta == null) return true;
                if (__instance is not JojaMart || action == null || action.Length == 0 || action[0] != "JojaShop") return true;
                string key = JojaOffer.CashierLine(_meta.State) == JojaCashierLine.Refused
                    ? "joja.cashier.refused" : "joja.cashier.undecided";
                Game1.drawObjectDialogue(Strings.Get(key));
                __result = true;
                return false;
            }
        }
    }
}
```

Note: the `$y` answer and reply fields are split on `_`, so the approved `joja.morris.*` strings must not contain `_` or `'` (Task 1 keeps them out; add an assert in Step 2).

- [ ] **Step 2: Add a guard test** to `tests/TheLongestYear.Tests/JojaOfferTests.cs` that loads `src/TheLongestYear/i18n/default.json` (as other i18n tests in this project do, see `OpeningStringsTests.cs` for how the file is located and parsed) and asserts `joja.morris.ask`, `joja.morris.accept`, `joja.morris.decline` contain neither `_` nor `'`, and that every Task 1 key exists.

- [ ] **Step 3: Wire it.** Add `DebugCounter()` (builds the same dialogue as the `Counter` prefix, shared through a private `static void OpenCounter()` both call) and `DebugCashier()` (shared `static void OpenCashier()`), and the two `tly_joja` subcommands. In ModEntry OnSaveLoaded next to `JunimoStashCapPatch.Connect(...)`: `JojaCounterPatch.Connect(this.Monitor, _meta, () => this.Monitor.Log("Joja: Yes (bad ending not built yet).", LogLevel.Warn));`. Harmony picks the nested classes up via PatchAll (the log line `Harmony: N patch class(es) applied, 0 failed` should go up by 2).

- [ ] **Step 4: Update `JojaMembershipBlock`'s summary**: the membership question is no longer reachable through Morris (JojaCounterPatch owns the counter); this prefix stays as a safety net for any other path to `JojaSignUp_Yes`.

- [ ] **Step 5: Build, run all tests, commit** `Joja: Morris's counter asks, the cashier refuses` and push.

---

### Task 5: The offer scene

**Files:**
- Create: `src/TheLongestYear/Integration/JojaOfferEvent.cs` (script builder + driver in one file: keys, `Build(bool skippable)`, `JojaOfferDriver`)
- Modify: `src/TheLongestYear/Integration/EndingEventCommands.cs` (`IsOurEvent` also accepts `JojaEventKeys.OfferId` and `JojaEventKeys.BadEndingId`)
- Modify: `src/TheLongestYear/ModEntry.cs` (construct and attach the driver at Entry)

**Interfaces:**
- Consumes: Task 2's `JojaOffer.ShouldPlayScene`, `SceneSkippable`, `MarkSceneSeen`; `EndingEventCommands` names (`FadeInName`, `BlackName`).
- Produces: `internal static class JojaEventKeys { public const string OfferId = "sonofskywalker3.TLY.JojaOffer"; public const string BadEndingId = "sonofskywalker3.TLY.JojaBadEnding"; }` (Task 6 uses `BadEndingId`).

- [ ] **Step 1: Find the tiles (no guessing).** With a TLY save loaded headless (docs/HEADLESS_DRIVING.md: `tly_newgame standard skipintro`, then `debug warp JojaMart 13 28`), capture a frame with the PrintWindow method in `tools/screenshot.ps1`, overlay a tile grid (tile = 64 px x window scale; anchor it on the farmer's known tile, and remember a character draws about one row below its tile), and read off: the door tile the player enters on, Morris's counter tile, a free floor tile two tiles in front of the counter, and a clear straight route between them. Record the tiles in a comment at the top of `JojaOfferEvent.cs` with the date. Then `tly_totitle` and delete the throwaway save.

- [ ] **Step 2: Write the script builder** (fill the tile constants from Step 1):

```csharp
internal static string Build(bool skippable)
{
    string Say(string key) => $"speak Morris \"{Sanitise(Strings.Get(key))}\"";
    var s = new List<string> { "none", "-1000 -1000", $"farmer {DoorX} {DoorY} 0" };
    if (skippable) s.Add("skippable");
    s.AddRange(new[]
    {
        $"addTemporaryActor Morris 16 32 {CounterX} {CounterY} 2 true Character",
        $"viewport {DoorX} {DoorY - 3} clamp",
        "pause 400",
        // Morris comes out from behind the counter to meet the farmer (route from Step 1).
        $"advancedMove Morris false {RouteLegs}",
        "tlyWaitWalk Morris 6000",
        "faceDirection Morris 2",
        Say("event.joja-offer.morris-1") ,
        Say("event.joja-offer.morris-2"),
        Say("event.joja-offer.morris-3"),
        "pause 300",
        "end",
    });
    return string.Join("/", s);
}
```

If the approved lines are back-to-back with nothing between them, join them into ONE `speak` with `#$b#` pages (Jeff, 2026-09-25: a box must not close and reopen without a reason). `Sanitise` is the same as `SeasonTurnEventInjector.Sanitise` (replace `"` with `'` and `/` with `,`).

- [ ] **Step 3: Write the driver.** On `Player.Warped` into a `JojaMart` (and each `UpdateTicked` while standing in JojaMart, throttled to every 30 ticks, so a scene that was blocked by a menu starts once the menu closes): if `RunActivation.IsActive` and `JojaOffer.ShouldPlayScene(run, meta, busy: Game1.eventUp || Game1.activeClickableMenu != null || Game1.isFestival() || !Context.CanPlayerMove)`, then `bool skip = JojaOffer.SceneSkippable(meta);` start `Game1.currentLocation.startEvent(new Event(Build(skip), null, JojaEventKeys.OfferId));` and call `JojaOffer.MarkSceneSeen(run, meta, Calendar.DayOfYear((int)run.Season, run.DayOfMonth))`. Log `Joja: offer scene (skippable=...)`. Add `tly_joja scene` to the Task 3 debug command: clears `JojaSceneSeenDay` and starts the scene now if in JojaMart.

- [ ] **Step 4: `IsOurEvent`** in `EndingEventCommands.cs`: add `|| ev.id == JojaEventKeys.OfferId || ev.id == JojaEventKeys.BadEndingId`.

- [ ] **Step 5: Build, run all tests, commit** `Joja: the offer scene on the first visit each loop`, push.

---

### Task 6: The bad ending scene

**Files:**
- Create: `src/TheLongestYear/Integration/JojaBadEnding.cs` (script builder + `Start()`)
- Create: `src/TheLongestYear/Integration/JojaBadEndingCommands.cs` (new event commands)
- Modify: `src/TheLongestYear/ModEntry.cs` (register commands at Entry; pass `JojaBadEnding.Start` into `JojaCounterPatch.Connect`)

**Interfaces:**
- Consumes: `JojaEventKeys.BadEndingId` (Task 5); `EndingEventCommands` names `ChangeLocationName` (`tlyChangeLocation <loc> <x> <y>`), `FadeInName`, `FadeOutName`, `BlackName`, `PanToName`; `OpeningEventCommands.WaitWalkName`.
- Produces: `internal static class JojaBadEnding { public static void Start(IMonitor monitor); }` and event commands:
  - `tlyHideFarmhouse` / `tlyShowFarmhouse`: while hidden, the farmhouse building is not drawn (Harmony prefix on `Building.draw` for the farm's main farmhouse, returning false while a static flag is set; the flag clears when the event ends, checked each tick like `EndingEventCommands.HoldTreesTranslucent`).
  - `tlyDust <x> <y> <w> <h> <ms>`: repeated `TemporaryAnimatedSprite(5, ...)` smoke puffs over the rectangle for that long.
  - `tlyBuildingSprite <buildingType> <x> <y>`: draws `Data/Buildings[buildingType].Texture`'s full sprite as a temporary sprite whose bottom-left sits on tile (x, y+1) (`Coop`, `Barn`).
  - `tlyItemSprite <qualifiedItemId> <x> <y> [rotation]`: an item's sprite lying on tile (x, y) until the event ends (dead fish `(O)145` Sunfish, `(O)132` Bream; driftwood `(O)169`; trash `(O)168`).
  - `tlyWaterTint <r> <g> <b>`: sets `Game1.currentLocation.waterColor` for the rest of the event, restored at the end.
  - `tlyClosedSign <x> <y>`: draws a board across a door: the `LooseSprites/Cursors` "closed" sign if one exists, else a brown 32x8 plank drawn from `Game1.staminaRect`, at tile (x, y).
  - `tlyGameOver`: opens `JojaGameOverMenu` (Task 7) and ends the event; the menu exits to the title.
  Every command logs and skips on bad args, and everything it draws is removed when the event ends.

- [ ] **Step 1: Find the tiles (no guessing).** Same gridded-screenshot method as Task 5 Step 1, on a throwaway `tly_newgame standard skipintro` farm: the farmhouse's tile rectangle and door (use `Farm.GetMainFarmHouseEntry()` as the anchor and offsets from it so every farm type works), two open spots left and right of the farmhouse for a coop and a barn, a route from the farm's east edge to each building door for the animal lines; Town: Pierre's door, the river tiles by the bridge at the south-east, a spot the camera can frame both; Beach: a stretch of shore for the driftwood. Record every tile in comments at the top of `JojaBadEnding.cs` with the date.

- [ ] **Step 2: Write the commands** in `JojaBadEndingCommands.cs` (register with `Event.RegisterCommand`, same shape as `OpeningEventCommands.Register`), each with a try/catch that logs and advances.

- [ ] **Step 3: Write the script** (`Build()`), in this order, all under the mod's black overlay transitions (`tlyChangeLocation` / `tlyFadeIn` / `tlyFadeOut`), no dialogue:
  1. JojaMart: `tlyFadeOut 1200`.
  2. Farm (`tlyChangeLocation Farm <doorX> <doorY+3>`, `farmer` warped off-screen at `-100 -100`, `viewport <door> clamp`, `tlyFadeIn`): `tlyDust` over the farmhouse for 2000 ms with `playSound explosion` twice, then `tlyHideFarmhouse`; `tlyDust` over each building spot, then `tlyBuildingSprite Coop ...` and `tlyBuildingSprite Barn ...`; then the animal lines: `addTemporaryActor` for each of White Cow, Sheep, Goat, White Chicken, Duck, Pig (asset names from `Animals/`, type `Animal`, sizes 32x32 for cows/sheep/goats/pigs, 16x16 for chickens/ducks), two of each, walking in single file with `advancedMove` along the Step 1 routes, heads down (`faceDirection` 2 at the door), `pause 4000`.
  3. Town (`tlyChangeLocation Town <x> <y>`, farmer off-screen): `tlyWaterTint 70 110 40`, three `tlyItemSprite` dead fish on the bank, `tlyClosedSign` on Pierre's door, `tlyPanTo` from the river to Pierre's, `pause 2500`.
  4. Beach (`tlyChangeLocation Beach <x> <y>`): eight `tlyItemSprite (O)169` and four `(O)168` along the shore, `pause 2500`.
  5. `tlyFadeOut 1500`, `tlyGameOver`.
  `Start()` builds the script and calls `Game1.currentLocation.startEvent(new Event(Build(), null, JojaEventKeys.BadEndingId))`.

- [ ] **Step 4: Wire it:** ModEntry registers `JojaBadEndingCommands.Register(this.Monitor, helper)` at Entry; the `JojaCounterPatch.Connect` call passes `() => JojaBadEnding.Start(this.Monitor)`. Add `tly_joja badending` to the debug command (starts it now).

- [ ] **Step 5: Build, run all tests, commit** `Joja: the bad ending scene`, push.

---

### Task 7: The Game Over screen

**Files:**
- Create: `src/TheLongestYear/UI/JojaGameOverMenu.cs`
- Modify: `src/TheLongestYear/Integration/JojaBadEndingCommands.cs` (`tlyGameOver` opens it)

**Interfaces:**
- Consumes: `MorrisDarkSprite.AssetName` (`Characters/Morris_Dark`), `Strings` keys `joja.gameover.title`, `joja.gameover.message`, `joja.gameover.button`.
- Produces: `internal sealed class JojaGameOverMenu : IClickableMenu` (no public API beyond construction).

- [ ] **Step 1: Write the menu.** Full-screen black. `SpriteText.drawStringHorizontallyCenteredAt(b, title, centerX, height / 3 - 40)` for "Game Over". The message with `Game1.parseText(message, Game1.dialogueFont, width * 2 / 3)` drawn centered at `height / 2`. Bottom third, centered, side by side at 4x scale: the farmer (Step 2) and Morris from `Characters/Morris_Dark` frame 0 (source rect 0,0,16,32). A `ClickableComponent` button with `joja.gameover.button` under them; clicking it, pressing any key, or pressing a controller button calls `Exit()`:

```csharp
private void Exit()
{
    if (_exiting) return;
    _exiting = true;
    // Never saves: the save on disk is still this morning, undecided (spec: Yes is not saved).
    Game1.ExitToTitle();
}
```

- [ ] **Step 2: The farmer in a suit, fedora and red eyes.** In the constructor, since the game will exit without saving, change the live farmer for display: find the hat whose `Data/Hats` name is `Fedora` and put it on (`Game1.player.hat.Value = ItemRegistry.Create<Hat>("(H)" + id)`); find a shirt and pants whose `Data/Shirts` / `Data/Pants` internal name contains `Suit` or `Tuxedo` (log which were used, skip a slot if none is found); set `Game1.player.changeEyeColor(Color.Red)`. Draw with `Game1.player.FarmerRenderer.draw(b, new FarmerSprite.AnimationFrame(0, 0, false, false), 0, new Rectangle(0, 0, 16, 32), position, Vector2.Zero, 0.8f, 2, Color.White, 0f, 1f, Game1.player)` (frame 0 facing down). Log `Joja: game over screen`. Glow: draw a soft red dot (`Game1.staminaRect`, 4x4, `Color.Red * pulse`) over each figure's eyes, pulsing with `Math.Sin(Game1.currentGameTime.TotalGameTime.TotalMilliseconds / 300)`; eye positions are measured from a screenshot in Step 3 and recorded in comments.

- [ ] **Step 3: Check it headless.** `tly_joja badending` on a throwaway save with frame capture (the recorder in the session scratchpad, or `tools/screenshot.ps1` in a loop); confirm the layout (title a third down, message centered, both figures bottom third, both with red eyes) and that the title screen follows the click. Delete the throwaway save.

- [ ] **Step 4: Build, run all tests, commit** `Joja: the Game Over screen`, push.

---

### Task 8: End-to-end headless check

**Files:** none (verification only; fix what it finds in the owning task's files and commit per fix)

- [ ] **Step 1:** Launch (label it "my automated run" to Jeff), `tly_newgame standard skipintro`, frame recorder on.
- [ ] **Step 2: Letters:** `tly_joja status`; `tly_setday` to the first planned letter day and sleep; confirm the log line `Joja: letter 'TLY_JojaCome1' delivered`.
- [ ] **Step 3: Scene:** `debug warp JojaMart <door>`; confirm `Joja: offer scene (skippable=False)` and that it plays through `tly_eventstep`.
- [ ] **Step 4: Cashier:** `tly_joja cashier`; confirm the undecided line on screen (frame) and in the log.
- [ ] **Step 5: Yes:** `tly_joja counter`, then `tly_answer 0`; confirm the bad ending plays through, the Game Over screen shows, and the title follows. Confirm the save folder's files have the SAME modified time as before the run (no save written).
- [ ] **Step 6: No:** reload, `tly_joja counter`, `tly_answer 1`; confirm the refusal, `tly_joja status` shows Rejected, the cashier refuses, then `tly_reset` and confirm Morris gives the "position filled" line and no letters are planned to arrive.
- [ ] **Step 7:** Clean up the throwaway saves, restore `test-output/log-archive` (`git checkout -- test-output/log-archive`), update `TODO.md` (spec entry -> BUILT, what Jeff still needs to see), commit, push.
