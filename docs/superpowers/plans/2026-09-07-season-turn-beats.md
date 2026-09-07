# Season Turn Beats Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the black "great job" card on a passed day 28 with a short porch scene where two to four Junimos greet the new season, darker each turn.

**Architecture:** A pure Core table (`SeasonTurn`) says which turn a season start is, how many Junimos come, which line keys play and whether the scene may be skipped. An injector builds a vanilla event script from that table using the Year One Ending's custom commands (fade-to-black location change, overlay fade in/out, coloured Junimo actors, half-height speech box). A driver starts the event on the wake frame in place of the Continue-branch menu and hands the morning back to `RunController.OnCutsceneEnded` when the event ends.

**Tech Stack:** C# / SMAPI 4 / Stardew 1.6 event scripts; xUnit tests in `tests/TheLongestYear.Tests`.

**Spec:** `docs/superpowers/specs/2026-09-07-season-turn-beats-design.md`

## Global Constraints

- Branch `story`; do not bump `manifest.json` (parallel-branch rule).
- No em dashes in any string or doc.
- Every user-visible string in `i18n/default.json`; `I18nGuardTests.NoOrphanKeys` scans source for `"event.turn.*"` literals, so every key must appear as a literal in a `Strings.Get(...)` / `EventText(...)` call site.
- The Fail branch and the ending are untouched.
- Lines are placeholders Jeff will rewrite: keep text only in `i18n/default.json` and the lines doc, never in code.

---

### Task 1: Core `SeasonTurn` table

**Files:**
- Create: `src/TheLongestYear.Core/SeasonTurn.cs`
- Test: `tests/TheLongestYear.Tests/SeasonTurnTests.cs`

**Interfaces:**
- Produces: `enum SeasonTurnKind { Summer, Fall, Winter }`; `static class SeasonTurn` with `SeasonTurnKind? ForSeasonStart(Season)`, `int JunimoCount(SeasonTurnKind)`, `IReadOnlyList<(int Junimo, string Key)> Lines(SeasonTurnKind)`, `bool IsSkippable(SeasonTurnKind, IReadOnlySet<string> seen)`, `string SeenName(SeasonTurnKind)`, `bool TryParse(string, out SeasonTurnKind)`.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SeasonTurnTests
{
    [Theory]
    [InlineData(Season.Summer, SeasonTurnKind.Summer)]
    [InlineData(Season.Fall, SeasonTurnKind.Fall)]
    [InlineData(Season.Winter, SeasonTurnKind.Winter)]
    public void ForSeasonStart_MapsTheThreeTurns(Season season, SeasonTurnKind expected)
        => Assert.Equal(expected, SeasonTurn.ForSeasonStart(season));

    [Fact]
    public void ForSeasonStart_SpringHasNoTurn()
        => Assert.Null(SeasonTurn.ForSeasonStart(Season.Spring));

    [Theory]
    [InlineData(SeasonTurnKind.Summer, 2)]
    [InlineData(SeasonTurnKind.Fall, 3)]
    [InlineData(SeasonTurnKind.Winter, 4)]
    public void JunimoCount_Escalates(SeasonTurnKind kind, int expected)
        => Assert.Equal(expected, SeasonTurn.JunimoCount(kind));

    [Theory]
    [InlineData(SeasonTurnKind.Summer)]
    [InlineData(SeasonTurnKind.Fall)]
    [InlineData(SeasonTurnKind.Winter)]
    public void Lines_OnlyNameJunimosWhoArePresent(SeasonTurnKind kind)
    {
        var lines = SeasonTurn.Lines(kind);
        Assert.NotEmpty(lines);
        Assert.All(lines, l => Assert.InRange(l.Junimo, 0, SeasonTurn.JunimoCount(kind) - 1));
        Assert.All(lines, l => Assert.StartsWith("event.turn." + kind.ToString().ToLowerInvariant() + "-", l.Key));
    }

    [Fact]
    public void IsSkippable_OnlyAfterTheTurnWasSeen()
    {
        var seen = new HashSet<string>();
        Assert.False(SeasonTurn.IsSkippable(SeasonTurnKind.Fall, seen));
        seen.Add(SeasonTurn.SeenName(SeasonTurnKind.Fall));
        Assert.True(SeasonTurn.IsSkippable(SeasonTurnKind.Fall, seen));
        Assert.False(SeasonTurn.IsSkippable(SeasonTurnKind.Winter, seen));
    }

    [Theory]
    [InlineData("summer", true, SeasonTurnKind.Summer)]
    [InlineData("Winter", true, SeasonTurnKind.Winter)]
    [InlineData("spring", false, SeasonTurnKind.Summer)]
    public void TryParse_AcceptsTheThreeNames(string text, bool ok, SeasonTurnKind expected)
    {
        Assert.Equal(ok, SeasonTurn.TryParse(text, out SeasonTurnKind kind));
        if (ok) Assert.Equal(expected, kind);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/TheLongestYear.Tests -c Release --filter SeasonTurnTests`
Expected: build error, `SeasonTurn` not defined.

- [ ] **Step 3: Write the implementation**

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Which season turn a morning is, and what its Junimo scene needs
/// (spec 2026-09-07-season-turn-beats). Summer = the Spring-to-Summer turn, etc.</summary>
public enum SeasonTurnKind { Summer, Fall, Winter }

public static class SeasonTurn
{
    public const string KeyPrefix = "event.turn.";

    public static SeasonTurnKind? ForSeasonStart(Season season) => season switch
    {
        Season.Summer => SeasonTurnKind.Summer,
        Season.Fall => SeasonTurnKind.Fall,
        Season.Winter => SeasonTurnKind.Winter,
        _ => null,
    };

    /// <summary>Two at the hopeful turn, three at the uneasy one, four at the alarmed one.</summary>
    public static int JunimoCount(SeasonTurnKind kind) => kind switch
    {
        SeasonTurnKind.Summer => 2,
        SeasonTurnKind.Fall => 3,
        _ => 4,
    };

    /// <summary>The scene's lines in order: which Junimo (0 = the green voice) says which i18n key.
    /// Text lives in i18n only; add or remove rows here when the script changes shape.</summary>
    public static IReadOnlyList<(int Junimo, string Key)> Lines(SeasonTurnKind kind) => kind switch
    {
        SeasonTurnKind.Summer => new[] { (0, KeyPrefix + "summer-1"), (1, KeyPrefix + "summer-2"), (0, KeyPrefix + "summer-3") },
        SeasonTurnKind.Fall => new[] { (0, KeyPrefix + "fall-1"), (1, KeyPrefix + "fall-2"), (2, KeyPrefix + "fall-3"), (0, KeyPrefix + "fall-4") },
        _ => new[] { (0, KeyPrefix + "winter-1"), (1, KeyPrefix + "winter-2"), (2, KeyPrefix + "winter-3"), (0, KeyPrefix + "winter-4") },
    };

    public static string SeenName(SeasonTurnKind kind) => kind.ToString();

    /// <summary>A turn the save has already watched once may be skipped.</summary>
    public static bool IsSkippable(SeasonTurnKind kind, IReadOnlySet<string> seen)
        => seen != null && seen.Contains(SeenName(kind));

    public static bool TryParse(string text, out SeasonTurnKind kind)
        => Enum.TryParse(text, ignoreCase: true, out kind) && Enum.IsDefined(typeof(SeasonTurnKind), kind);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/TheLongestYear.Tests -c Release --filter SeasonTurnTests`
Expected: 12 passed.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/SeasonTurn.cs tests/TheLongestYear.Tests/SeasonTurnTests.cs
git commit -m "Season turns: Core table (kind per season start, Junimo count, line keys, skippable)"
```

---

### Task 2: Persisted "seen" set and the lines

**Files:**
- Modify: `src/TheLongestYear.Core/MetaState.cs` (next to `EndingSeen`, line ~188)
- Modify: `src/TheLongestYear/i18n/default.json` (after `event.ending.grandpa`)
- Modify: `docs/superpowers/specs/2026-09-06-year-one-ending-lines.md` (append a section)

**Interfaces:**
- Produces: `MetaState.SeasonTurnsSeen : HashSet<string>` (names from `SeasonTurn.SeenName`).

- [ ] **Step 1: Add the field**

```csharp
    /// <summary>Season turns whose Junimo scene has played at least once on this save
    /// (SeasonTurn.SeenName values). A seen turn is skippable next time.</summary>
    public HashSet<string> SeasonTurnsSeen { get; set; } = new();
```

- [ ] **Step 2: Add the eleven placeholder lines to `i18n/default.json`** (text from the spec, keys `event.turn.summer-1` .. `event.turn.winter-4`). `@` is the farmer's name.

- [ ] **Step 3: Append the lines to the review doc** under a heading "Season turns (`event.turn.*`)", one table per turn with Who and Text columns, and the note that Jeff owns the wording.

- [ ] **Step 4: Run all tests** (`I18nGuardTests.NoOrphanKeys` will FAIL until Task 3 adds the call sites; that is expected here, note it and move on).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/MetaState.cs src/TheLongestYear/i18n/default.json docs/superpowers/specs/2026-09-06-year-one-ending-lines.md
git commit -m "Season turns: seen set on MetaState, placeholder lines, review doc"
```

---

### Task 3: Script injector and the `tlyBlack` command

**Files:**
- Create: `src/TheLongestYear/Integration/SeasonTurnEventInjector.cs`
- Modify: `src/TheLongestYear/Integration/EndingEventCommands.cs` (add `tlyBlack`; make the "our event" check cover both ids)

**Interfaces:**
- Consumes: `SeasonTurn.*`, `EndingEventCommands.{ChangeLocationName,FadeInName,FadeOutName,JunimoName,SayName}`.
- Produces: `SeasonTurnEventKeys.EventId = "sonofskywalker3.TLY.SeasonTurn"`, `SeasonTurnEventKeys.SeenMail = "tly_turn_seen"`, `static string SeasonTurnEventInjector.Build(SeasonTurnKind kind, int doorX, int doorY, bool skippable)`, `EndingEventCommands.BlackName = "tlyBlack"`, `static bool EndingEventCommands.IsOurEvent(Event)`.

- [ ] **Step 1: `tlyBlack` and the id check** in `EndingEventCommands.cs`

```csharp
        public const string BlackName = "tlyBlack";
        ...
        /// <summary>Events whose overlay, tree fade and speech box this class serves.</summary>
        internal static bool IsOurEvent(Event ev)
            => ev != null && (ev.id == EndingEventKeys.EventId || ev.id == SeasonTurnEventKeys.EventId);
        ...
            // tlyBlack: put the overlay to black at once (a scene that starts under the wake-up
            // fade uses it before its first location change so no frame of the bedroom draws).
            Event.RegisterCommand(BlackName, (evt, args, context) => { _black = 1f; evt.CurrentCommand++; });
```

Replace both `ev.id != EndingEventKeys.EventId` checks (in `DrawBlack` and `HoldTreesTranslucent`) with `!IsOurEvent(ev)`.

- [ ] **Step 2: The injector**

```csharp
using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    internal static class SeasonTurnEventKeys
    {
        public const string EventId = "sonofskywalker3.TLY.SeasonTurn";
        public const string SeenMail = "tly_turn_seen";
    }

    /// <summary>Season Turn Beats (spec 2026-09-07): the porch scene on the morning of Summer 1,
    /// Fall 1 or Winter 1 after a passed gate. Built like EndingEventInjector; starts wherever the
    /// farmer woke (the farmhouse) under black and moves to the doorstep. Marks are relative to the
    /// farm's door tile so every farm type works.</summary>
    internal static class SeasonTurnEventInjector
    {
        private static readonly (int X, int Y)[] Marks = { (0, 2), (-2, 2), (2, 2), (-3, 4) };
        private const int FadeMs = 1400;

        private static string Junimo(int i) => $"Junimo{i}";

        private static string EventText(string key)
        {
            string value = Strings.Get(key);
            return string.IsNullOrEmpty(value) ? value : value.Replace('"', '\'').Replace('/', ',');
        }

        internal static string Build(SeasonTurnKind kind, int doorX, int doorY, bool skippable)
        {
            int count = SeasonTurn.JunimoCount(kind);
            var s = new List<string>
            {
                kind == SeasonTurnKind.Winter ? "none" : "junimoStarSong",
                "-1000 -1000",
                $"farmer {doorX} {doorY} 2",
                EndingEventCommands.BlackName,
            };
            if (skippable) s.Add("skippable");
            s.AddRange(new[]
            {
                $"{EndingEventCommands.ChangeLocationName} Farm {doorX} {doorY}",
                $"warp farmer {doorX} {doorY}",
                "faceDirection farmer 2",
                $"viewport {doorX} {doorY} clamp",
            });
            for (int j = 0; j < count; j++)
                s.Add($"{EndingEventCommands.JunimoName} {Junimo(j)} {doorX + Marks[j].X} {doorY + Marks[j].Y} {j}");
            s.Add($"{EndingEventCommands.FadeInName} {FadeMs}");
            s.Add("pause 500");
            for (int j = 0; j < count; j++) { s.Add($"jump {Junimo(j)} 8"); }
            s.Add("playSound junimoMeep1");
            s.Add("pause 700");

            var lines = SeasonTurn.Lines(kind);
            for (int i = 0; i < lines.Count; i++)
            {
                var (who, key) = lines[i];
                // Mood beats: the uneasy turn drops the music at its second line; the alarmed turn
                // holds a dim purple glow under its second line with a low sound.
                if (kind == SeasonTurnKind.Fall && i == 1) s.Add("stopMusic");
                if (kind == SeasonTurnKind.Winter && i == 1) { s.Add("glow 60 0 90 true"); s.Add("playSound shadowDie"); }
                if (kind == SeasonTurnKind.Winter && i == lines.Count - 1) s.Add("stopGlowing");
                s.Add($"jump {Junimo(who)} 6");
                s.Add($"{EndingEventCommands.SayName} {Junimo(who)} \"{EventText(key)}\"");
                s.Add("pause 250");
            }

            s.Add("pause 400");
            for (int j = 0; j < count; j++) { s.Add($"jump {Junimo(j)} 8"); }
            s.Add("playSound junimoMeep1");
            s.Add("pause 600");
            s.Add($"{EndingEventCommands.FadeOutName} 1200");
            s.Add($"addMailReceived {SeasonTurnEventKeys.SeenMail}");
            s.Add("end");
            return string.Join("/", s);
        }
    }
}
```

The eleven keys must appear as literals for the orphan test: add a private static readonly array in the injector listing every `event.turn.*` key inside `Strings.Get(...)`-shaped literals is NOT enough (the guard matches call sites). Instead, keep `EventText("event.turn.summer-1")` etc. reachable: add this method to the injector and never call it (the guard is a source scan):

```csharp
        // I18nGuardTests scans source for the keys a call site names; SeasonTurn.Lines builds them
        // from a prefix, so list them here once, plainly, for the guard and for grep.
        private static readonly string[] AllKeys =
        {
            "event.turn.summer-1", "event.turn.summer-2", "event.turn.summer-3",
            "event.turn.fall-1", "event.turn.fall-2", "event.turn.fall-3", "event.turn.fall-4",
            "event.turn.winter-1", "event.turn.winter-2", "event.turn.winter-3", "event.turn.winter-4",
        };
```

Check `I18nGuardTests` first for the exact regex it uses (`TokenCallSite` near line 222); if it needs `Strings.Get("...")` form, wrap each entry as `Strings.Get("event.turn.summer-1")` inside a static method `WarmKeys()` instead.

- [ ] **Step 3: Build and run all tests**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release -p:EnableModDeploy=false` then `dotnet test tests/TheLongestYear.Tests -c Release`
Expected: build clean, all tests pass including `NoOrphanKeys`.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Integration/SeasonTurnEventInjector.cs src/TheLongestYear/Integration/EndingEventCommands.cs
git commit -m "Season turns: event script injector, tlyBlack, overlay serves both event ids"
```

---

### Task 4: Driver, day-28 hook, debug command

**Files:**
- Create: `src/TheLongestYear/Integration/SeasonTurnDriver.cs`
- Modify: `src/TheLongestYear/Integration/Day28CutsceneDriver.cs:104-112` (Continue branch starts the event)
- Modify: `src/TheLongestYear/ModEntry.cs` (construct + attach the driver near line 155; `tly_seasonturn` console command near `tly_ending` line 283; bridge case near line 2237)

**Interfaces:**
- Consumes: `SeasonTurnEventInjector.Build`, `SeasonTurn.*`, `MetaStore` (`_meta.State.SeasonTurnsSeen`, `_meta.Save()`).
- Produces: `SeasonTurnDriver.Start(SeasonTurnKind kind, Action onComplete)` returns bool (false if busy), `SeasonTurnDriver.StartNow(SeasonTurnKind)` (replay, no continuation), `SeasonTurnDriver.Running`.

- [ ] **Step 1: The driver**

```csharp
using System;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Integration
{
    /// <summary>Starts a season-turn scene and reports when it ends. The Day28CutsceneDriver calls
    /// Start on a Continue morning instead of opening its menu; the completion callback is the same
    /// RunController.OnCutsceneEnded. The event adds SeenMail on its last line; a skipped or lost
    /// event never does, so "event gone" also counts as finished (the morning is never stranded).</summary>
    internal sealed class SeasonTurnDriver
    {
        private readonly IMonitor _monitor;
        private readonly MetaStore _meta;
        private Action _onComplete;
        private bool _running;
        private int _startedTick;

        public bool Running => _running;

        public SeasonTurnDriver(IMonitor monitor, MetaStore meta) { _monitor = monitor; _meta = meta; }

        public void Attach(IModHelper helper)
        {
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
            helper.Events.GameLoop.ReturnedToTitle += (_, _) => { _running = false; _onComplete = null; };
        }

        public bool Start(SeasonTurnKind kind, Action onComplete)
        {
            GameLocation loc = Game1.currentLocation;
            if (loc == null || Game1.eventUp || loc.currentEvent != null) return false;
            Microsoft.Xna.Framework.Point door = Game1.getFarm().GetMainFarmHouseEntry();
            bool skippable = SeasonTurn.IsSkippable(kind, _meta.State.SeasonTurnsSeen);
            _monitor.Log($"Season turn: starting {kind} (junimos={SeasonTurn.JunimoCount(kind)}, skippable={skippable}, door={door.X},{door.Y}, in {loc.Name}).", LogLevel.Info);
            loc.startEvent(new Event(SeasonTurnEventInjector.Build(kind, door.X, door.Y, skippable), null, SeasonTurnEventKeys.EventId));
            _meta.State.SeasonTurnsSeen.Add(SeasonTurn.SeenName(kind));
            _onComplete = onComplete;
            _running = true;
            _startedTick = Game1.ticks;
            return true;
        }

        /// <summary>Debug replay (tly_seasonturn): the scene alone, no continuation.</summary>
        public void StartNow(SeasonTurnKind kind)
        {
            if (Game1.activeClickableMenu != null || Game1.eventUp || Game1.farmEvent != null || Game1.locationRequest != null)
            {
                _monitor.Log("tly_seasonturn: the game is busy (event, menu or warp up); try again with nothing open.", LogLevel.Warn);
                return;
            }
            if (!Start(kind, () => _monitor.Log("Season turn: replay finished (no continuation).", LogLevel.Info)))
                _monitor.Log("tly_seasonturn: could not start (no location or an event is up).", LogLevel.Warn);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (!_running || !Context.IsWorldReady) return;
            if (Game1.ticks - _startedTick < 30) return;               // let the event register
            bool eventGone = !Game1.eventUp && Game1.currentLocation?.currentEvent == null;
            if (!eventGone) return;
            Farmer p = Game1.player;
            bool seen = p.mailReceived.Contains(SeasonTurnEventKeys.SeenMail);
            p.mailReceived.Remove(SeasonTurnEventKeys.SeenMail);        // transient signal, never persisted
            _monitor.Log(seen ? "Season turn: scene finished." : "Season turn: scene ended early (skipped or lost); continuing.", seen ? LogLevel.Info : LogLevel.Warn);
            _running = false;
            Action cb = _onComplete; _onComplete = null;
            cb?.Invoke();
        }
    }
}
```

- [ ] **Step 2: Day-28 driver hook.** In `Day28CutsceneDriver`, add a constructor/`Attach` parameter `Func<SeasonTurnDriver> turnDriver` and, at the point the menu is opened (after the `activeClickableMenu != null` guard):

```csharp
            Day28Branch branch = rc.PendingCutscene;
            if (branch == Day28Branch.Continue)
            {
                SeasonTurnKind? kind = SeasonTurn.ForSeasonStart((Season)(int)Game1.season);
                SeasonTurnDriver turn = _turnDriver?.Invoke();
                if (kind != null && turn != null && turn.Start(kind.Value, () => _runController?.Invoke()?.OnCutsceneEnded()))
                {
                    _openedMenu = null;   // an event, not a menu: the replaced-menu watchdog does not apply
                    _opened = true;
                    return;
                }
                _monitor.Log("Day-28 cutscene: no season turn scene for this morning; showing the card.", LogLevel.Info);
            }
```

and make the watchdog skip when `_openedMenu == null`:

```csharp
                if (_openedMenu != null && !ReferenceEquals(Game1.activeClickableMenu, _openedMenu))
```
(already the case; keep it). `Game1.season` is the vanilla `Season` enum; cast through int to Core's `Season`.

- [ ] **Step 3: ModEntry wiring.** Construct `_seasonTurnDriver = new SeasonTurnDriver(this.Monitor, _meta); _seasonTurnDriver.Attach(helper);` before the day-28 driver's `Attach`, pass `() => _seasonTurnDriver` into it. Add the console command and the bridge case:

```csharp
            helper.ConsoleCommands.Add("tly_seasonturn", "Replay a season-turn Junimo scene now, no continuation (debug). Usage: tly_seasonturn <summer|fall|winter>", this.CmdSeasonTurn);
...
        private void CmdSeasonTurn(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1 || !TheLongestYear.Core.SeasonTurn.TryParse(args[0], out var kind))
            { this.Monitor.Log("Usage: tly_seasonturn <summer|fall|winter>", LogLevel.Warn); return; }
            _seasonTurnDriver?.StartNow(kind);
        }
...
                case "tly_seasonturn": this.CmdSeasonTurn(command, args); break;
```

- [ ] **Step 4: Build, test, deploy, run the scene**

Run: `dotnet test tests/TheLongestYear.Tests -c Release`; `pwsh -NoProfile -File tools/deploy.ps1`; load a farm; `tly_seasonturn summer` through the bridge; step with `tly_eventstep` (it clicks the speech box); screenshot with `tools/screenshot.ps1`. Then the real path: `tly_setday 28`, `debug sleep` with the gate passing (use `tly_playseason quarter 4` first or a save that passes), confirm the log shows `Season turn: starting Summer`, then `Season turn: scene finished.`, then the week-1 hub opens.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Integration/SeasonTurnDriver.cs src/TheLongestYear/Integration/Day28CutsceneDriver.cs src/TheLongestYear/ModEntry.cs
git commit -m "Season turns: driver on the Continue morning, tly_seasonturn replay"
```

---

### Task 5: Script doc and runbook

**Files:**
- Create: `docs/superpowers/specs/2026-09-07-season-turn-script.md` (staging table per turn, like the ending's script doc)
- Modify: `docs/HEADLESS_DRIVING.md` (a "Season turns" paragraph: `tly_seasonturn <kind>`, step with `tly_eventstep`, the real path via `tly_setday 28` + sleep)

- [ ] **Step 1: Write both docs from the live run (real tiles, real timings).**
- [ ] **Step 2: Commit**

```bash
git add docs/superpowers/specs/2026-09-07-season-turn-script.md docs/HEADLESS_DRIVING.md
git commit -m "docs: season turn script sheet and headless runbook"
```
