# Voluntary Restart Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A **Restart the year** button on the Junimo Shrine (the farm statue) that, after a yes/no confirm, ends the day at once and runs the Fail-night chain with the Junimo scene removed: bundle hold question, upgrade menu, Cookbook/Craftbook banking, reset to Spring 1 of the next loop.

**Architecture:** A new `Day28Branch.Restart` value rides the existing morning-after machinery. On Yes the mod queues `Restart` in `RunState.PendingDay28` and puts the player to sleep where they stand with vanilla's own `debug sleep` recipe (`isInBed` + `sleptInTemporaryBed` + `answerDialogueAction("Sleep_Yes")`, which reaches `Game1.NewDay`). That night `RunController.OnDayEnding` sees `Restart` and skips the gate. In the morning `Day28CutsceneDriver` sees `Restart`, opens no scene, and calls `RunController.OnCutsceneEnded()`, whose `Restart` case clears the won-run flag and runs the same chain as `Fail`. Button visibility is a pure rule in `TheLongestYear.Core.Day28.VoluntaryRestart`, unit tested.

**Tech Stack:** C# / .NET 6, SMAPI 4.0+, Harmony, xunit. `TheLongestYear.Core` is `Nullable enable`, `ImplicitUsings disable`. The mod project is `Nullable disable`, `ImplicitUsings disable`. Tests reference Core only.

**Spec:** `docs/superpowers/specs/2026-09-24-voluntary-restart-design.md` (approved by Jeff 2026-09-24). Season pity was removed after the spec was written (c441c94), so the Fail chain today is: day-28 scene, `ShowHoldChoice` (skipped when `BundleHold.IsOfferable` is false), `TryOpenShrineThenContinue` / `DeferShrineThenContinue`, `OfferRecipeBanking`, `FinalizeReset`.

## Global Constraints

- Branch **`master`** (the release line). Every code commit bumps the PATCH in `src/TheLongestYear/manifest.json` (current `0.18.52`). Docs-only commits do not bump.
- Push every commit right after making it (`git push`), per Jeff's standing rule of 2026-09-14. Releases, Nexus edits and player replies still need his explicit yes; nothing in this plan releases.
- Do not touch `TODO.md` (another session edits it).
- **No em dashes** anywhere: code comments, log lines, strings, docs, commit messages.
- Player-facing strings live in `src/TheLongestYear/i18n/default.json` and go through `Strings.Get`. The two new strings are DRAFT placeholders marked with a `// DRAFT` comment; Task 6 sends them through the `game-writing` skill before release.
- Every `Strings.Get("key")` literal must have a key in `default.json`, and every key must be referenced (`I18nGuardTests`). Add a key in the same task as its first use.
- Build without deploying: `dotnet build "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\src\TheLongestYear\TheLongestYear.csproj" -p:EnableModDeploy=false`
- Test: `dotnet test` in `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\tests\TheLongestYear.Tests`. Record the baseline count in Task 0; it must only grow.
- Commit messages end with:
  ```
  Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01MLy6J9QCsqjg9XvYFmm4UG
  ```
- `RunController.cs` is 1437 lines. New restart code goes in a new partial file `Loop/RunController.Restart.cs`; the main file only gets the small edits listed in Task 2.
- Story branch follow-up (not in this plan): when `master` is next merged into `story`, the `Restart` case in `OnCutsceneEnded` must also clear `Year2WallArmed`, as the spec says. Leave a note in the merge commit.

## Review Focus

1. **The sleep call** (`RunController.Restart.cs`, `EndDayNow`): it must reach `Game1.NewDay` with `player.isInBed` true, or vanilla never fades and never runs `newDayAfterFade`. See "Timing, settled" below.
2. **The night gate skip** (`RunController.OnDayEnding`): a `Restart` night must not evaluate the gate, pay checkpoint JP, or overwrite the queued branch.
3. **Everything that reads `PendingCutscene`** must treat `Restart` like `Fail` where the reason is "the morning rewinds" (FarmEvent suppression) and differently where the reason is "play the scene" (the driver).
4. **Won-run flag**: cleared only on the `Restart` branch, at the start of the chain, and persisted by `FinalizeReset`'s `_store.Save()`.
5. **Quit mid-chain**: `PendingDay28 = Restart` is written by the night save, so a reload replays the chain (the existing `OnRunLoaded` path already leaves the date alone when anything is pending).

## Timing, settled (the spec's open question)

Read from the PC 1.6 decompile (`decompiled-pc/Stardew Valley`):

- `Game1.NewDay(float)` (`Game1.cs:9906`) sets `newDay = true`, but it starts the fade to black **only** when `player.isInBed || player.passedOut` (`Game1.cs:9915-9924`). `newDayAfterFade` runs only from `onFadeToBlackComplete` when `newDay` is true (`Game1.cs:5911-5915`). So a bare `NewDay` from the farm sets the flag and then hangs with no fade: it must be called with `isInBed` true.
- `Farmer.Update` recomputes `isInBed` every tick as "standing on a Bed tile, or `sleptInTemporaryBed`" (`Farmer.cs:7478`). Setting `isInBed` alone is undone next tick; `sleptInTemporaryBed` keeps it true.
- Vanilla's own `debug sleep` command (`DebugCommands.cs:4573-4578`) is exactly: `isInBed = true; sleptInTemporaryBed = true; currentLocation.answerDialogueAction("Sleep_Yes", null)`. `Sleep_Yes` (`GameLocation.cs:12187`) calls `startSleep` (records `timeWentToBed`), then `doSleep` (`GameLocation.cs:11362`), which calls `Game1.NewDay(0 or 600)` for the host and records `lastSleepLocation`/`lastSleepPoint`. This is the same call chain as answering Yes to the bed's "Go to sleep?" question, and it is also made from inside a question's answer callback there.
- **No pass-out penalty on this path.** The gold loss and the "passedOut" mail live only in `Farmer.performPassoutWarp` (`Farmer.cs:5766-5850`), reached from `passOutFromTired` / `sendPassoutRequest`. `NewDay` and `doSleep` never touch money.
- `sleptInTemporaryBed` cleans itself up: `SaveGame.Save` sets it false after writing (`SaveGame.cs:562`), and the load path does too (`SaveGame.cs:1259`).
- Wake spot: the player wakes where they stood (the statue) because nothing warps a sleeping host overnight. `doSleep` recorded the statue as `lastSleepLocation`; we overwrite it with the farmhouse bed so a quit-and-reload mid-chain loads them in bed (`SaveGame.cs:1051-1065`). `WorldResetService.PerformReset` step 14 then puts them home in the farmhouse as it always does.
- The overnight `FarmEvent` is suppressed on a `Restart` night for the same reason as on a Fail night (its end warp can orphan the chain; `FarmEventSuppressionPatch`).
- SMAPI raises `DayEnding` from `hooks.OnGame1_NewDayAfterFade` (`Game1.cs:7259-7300`), before the date rolls, and runs the new-day task synchronously, so `RunController.OnDayEnding` sees the queued `Restart`.
- **Run-day bookkeeping needs no new code beyond the gate skip.** `Run.DayOfMonth` is only synced in `DoDayStartSeasonAndHub` and `OnRunLoaded`, and both are already skipped while any `PendingDay28` is set. The run stays on the confirm day until `FinalizeReset` calls `BeginNewRun`, exactly as on a Fail night. The button is hidden on day 28, so a restart night never crosses a month (a day-27 restart wakes on day 28 and rewinds before anything reads it).
- **Morning hand-off:** the driver already waits for `!newDay`, no event, no FarmEvent, no pending warp and no open menu. For `Restart` it additionally waits for `!Game1.showingEndOfNightStuff` (cleared by `Game1.cs:3903-3915` once the save menu is gone), because there is no black scene to cover the morning fade and the hold question should not sit under vanilla's end-of-night hand-off.
- **Fallback is automatic and visible.** If `NewDay` ever fails to start (`Game1.newDay` still false after `EndDayNow`), the driver finds `Restart` pending on the next clear tick and runs the chain mid-day. `EndDayNow` logs a Warn line when that happens. Note that `tly_failreset` already runs the same chain mid-day from the debug bridge, so mid-day is less untested than the spec assumed. Per the spec, mid-day stays the fallback only.

## File Structure

| File | Responsibility |
|---|---|
| `src/TheLongestYear.Core/Day28/Day28Branch.cs` | **Modify.** Append `Restart`. |
| `src/TheLongestYear.Core/Day28/VoluntaryRestart.cs` | **Create.** `RestartSituation`, `RestartBlock`, `VoluntaryRestart` (visibility rule, won-run clearing, `IsRewind`). |
| `tests/TheLongestYear.Tests/VoluntaryRestartTests.cs` | **Create.** |
| `src/TheLongestYear/Loop/RunController.cs` | **Modify.** `partial`; `OnDayEnding` skips the gate on a Restart night; `OnCutsceneEnded` gets a `Restart` case; Fail body extracted to `StartRewindChain`. |
| `src/TheLongestYear/Loop/RunController.Restart.cs` | **Create.** Visibility snapshot, confirm popup, `BeginVoluntaryRestart`, `EndDayNow`. |
| `src/TheLongestYear/Integration/Day28CutsceneDriver.cs` | **Modify.** No scene for `Restart`; call the continuation directly. |
| `src/TheLongestYear/Loop/FarmEventSuppressionPatch.cs` | **Modify.** Doc comment only. |
| `src/TheLongestYear/ModEntry.cs` | **Modify.** FarmEvent suppression uses `IsRewind`; `tly_restart` command; restart hooks for the shrine and `tly_openshrine`. |
| `src/TheLongestYear/UI/PlanningShrineService.cs` | **Modify.** `OpenMenu` (the statue's open path as one method, reused when the player answers No); `AttachRestart` static hooks, passed into the menu. |
| `src/TheLongestYear/UI/ShrinePreviewMenu.cs` | **Modify.** The button at the right end of the tab strip. |
| `src/TheLongestYear/i18n/default.json` | **Modify.** `dialog.restart.prompt`, `shrine.restart.button` (DRAFT). |
| `src/TheLongestYear/manifest.json` | **Modify.** PATCH bump per code commit. |
| `README.md`, `docs/nexus-description.bbcode`, `CHANGELOG.md` | **Modify.** Task 7. |

---

### Task 0: Baseline

- [ ] **Step 1: Confirm branch and clean tree**

Run in `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`:
```
git checkout master
git pull
git status
```
Expected: `On branch master`, up to date, clean (TODO.md may show as modified by the other session; leave it alone and never stage it).

- [ ] **Step 2: Record the test baseline**

```
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\tests\TheLongestYear.Tests"
dotnet test
```
Expected: all pass. Write down the total (the 0.18.51 changelog says 2142).

- [ ] **Step 3: Confirm the mod builds**

```
dotnet build "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\src\TheLongestYear\TheLongestYear.csproj" -p:EnableModDeploy=false
```
Expected: `Build succeeded`.

---

### Task 1: The pure rule (Core) and the `Restart` branch

**Files:**
- Modify: `src/TheLongestYear.Core/Day28/Day28Branch.cs`
- Create: `src/TheLongestYear.Core/Day28/VoluntaryRestart.cs`
- Create: `tests/TheLongestYear.Tests/VoluntaryRestartTests.cs`
- Modify: `src/TheLongestYear/manifest.json` (0.18.52 -> 0.18.53)

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/VoluntaryRestartTests.cs`:

```csharp
using System.Text.Json;
using TheLongestYear.Core;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Tests;

/// <summary>Voluntary restart at the Junimo Shrine (spec 2026-09-24-voluntary-restart-design):
/// when the button shows, how the won-run flag is cleared, and which branches rewind.</summary>
public class VoluntaryRestartTests
{
    private static readonly RestartSituation OrdinaryDay = new(
        DayOfMonth: 12, EventUp: false, ResetRunning: false);

    [Fact]
    public void Shown_on_an_ordinary_day()
    {
        Assert.Equal(RestartBlock.None, VoluntaryRestart.BlockedBy(OrdinaryDay));
        Assert.True(VoluntaryRestart.IsOffered(OrdinaryDay));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(27)]
    public void Shown_on_the_first_day_and_the_day_before_the_season_ends(int day)
        => Assert.True(VoluntaryRestart.IsOffered(OrdinaryDay with { DayOfMonth = day }));

    [Fact]
    public void Hidden_on_day_28_because_that_night_belongs_to_the_real_gate()
        => Assert.Equal(RestartBlock.SeasonEndDay, VoluntaryRestart.BlockedBy(OrdinaryDay with { DayOfMonth = 28 }));

    [Fact]
    public void Hidden_during_an_event_or_cutscene()
        => Assert.Equal(RestartBlock.EventUp, VoluntaryRestart.BlockedBy(OrdinaryDay with { EventUp = true }));

    [Fact]
    public void Hidden_while_another_reset_chain_is_running()
        => Assert.Equal(RestartBlock.ResetRunning, VoluntaryRestart.BlockedBy(OrdinaryDay with { ResetRunning = true }));

    [Fact]
    public void A_running_reset_is_reported_before_every_other_reason()
    {
        RestartSituation all = new(DayOfMonth: 28, EventUp: true, ResetRunning: true);
        Assert.Equal(RestartBlock.ResetRunning, VoluntaryRestart.BlockedBy(all));
    }

    [Fact]
    public void Restart_after_keep_playing_clears_the_won_run_flag()
    {
        var meta = new MetaState { VictoryAcknowledged = true };
        Assert.True(VoluntaryRestart.ClearWonRun(meta));
        Assert.False(meta.VictoryAcknowledged);
    }

    [Fact]
    public void Restart_before_any_win_leaves_the_flag_alone()
    {
        var meta = new MetaState { VictoryAcknowledged = false };
        Assert.False(VoluntaryRestart.ClearWonRun(meta));
        Assert.False(meta.VictoryAcknowledged);
    }

    [Theory]
    [InlineData(Day28Branch.Fail, true)]
    [InlineData(Day28Branch.Restart, true)]
    [InlineData(Day28Branch.None, false)]
    [InlineData(Day28Branch.Continue, false)]
    [InlineData(Day28Branch.Win, false)]
    public void Fail_and_restart_are_the_rewind_branches(Day28Branch branch, bool rewinds)
        => Assert.Equal(rewinds, VoluntaryRestart.IsRewind(branch));

    // PendingDay28 is persisted in the night save; appending Restart must not renumber the
    // values old saves already hold.
    [Fact]
    public void Existing_branch_values_keep_their_numbers()
    {
        Assert.Equal(0, (int)Day28Branch.None);
        Assert.Equal(1, (int)Day28Branch.Fail);
        Assert.Equal(2, (int)Day28Branch.Continue);
        Assert.Equal(3, (int)Day28Branch.Win);
        Assert.Equal(4, (int)Day28Branch.Restart);
    }

    [Fact]
    public void A_pending_restart_survives_a_save_round_trip()
    {
        var run = new RunState { PendingDay28 = Day28Branch.Restart };
        RunState back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(run))!;
        Assert.Equal(Day28Branch.Restart, back.PendingDay28);
    }

    // A quit after the restart night's save must replay the chain, not roll the month.
    [Fact]
    public void A_pending_restart_blocks_the_load_time_month_rollover()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 27, PendingDay28 = Day28Branch.Restart };
        Assert.False(run.OwesMonthRolloverOnLoad(Season.Summer));
    }
}
```

- [ ] **Step 2: Run the tests to see them fail**

```
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\tests\TheLongestYear.Tests"
dotnet test --filter "FullyQualifiedName~VoluntaryRestartTests"
```
Expected: build error, `Day28Branch` has no `Restart`, `RestartSituation` / `VoluntaryRestart` not found.

- [ ] **Step 3: Append the branch**

In `src/TheLongestYear.Core/Day28/Day28Branch.cs`, replace:
```csharp
        Win       // loop completed (CC restored) → win screen → JP shop → keep-playing choice
    }
```
with:
```csharp
        Win,      // loop completed (CC restored) → win screen → JP shop → keep-playing choice
        Restart   // voluntary restart from the Junimo Shrine → no scene → the Fail chain (hold, upgrades, books, reset)
    }
```
(Append only. The values are persisted in `RunState.PendingDay28`.)

- [ ] **Step 4: Create the rule**

Create `src/TheLongestYear.Core/Day28/VoluntaryRestart.cs`:

```csharp
namespace TheLongestYear.Core.Day28
{
    /// <summary>Why the Junimo Shrine's Restart the year button is hidden right now. None means it
    /// shows. <see cref="VoluntaryRestart.BlockedBy"/> checks in declaration order, so a log line
    /// names the most basic reason first.</summary>
    public enum RestartBlock
    {
        None,
        /// <summary>A Fail, Win or Restart chain is already queued or mid-way, or the game is already ending the day.</summary>
        ResetRunning,
        /// <summary>An event, cutscene, festival in progress or overnight farm event is playing.</summary>
        EventUp,
        /// <summary>Day 28: the real gate owns tonight (Fail, Continue or Win).</summary>
        SeasonEndDay,
    }

    /// <summary>The world facts the button depends on, read by the mod when the shrine opens and
    /// again when the player confirms.</summary>
    public readonly record struct RestartSituation(
        int DayOfMonth,
        bool EventUp,
        bool ResetRunning);

    /// <summary>Voluntary restart at the Junimo Shrine (spec 2026-09-24-voluntary-restart-design).
    /// A restart is a normal loop reset that skips the Junimo scene and happens tonight.</summary>
    public static class VoluntaryRestart
    {
        public static RestartBlock BlockedBy(RestartSituation s)
        {
            if (s.ResetRunning) return RestartBlock.ResetRunning;
            if (s.EventUp) return RestartBlock.EventUp;
            if (Calendar.IsMonthEnd(s.DayOfMonth)) return RestartBlock.SeasonEndDay;
            return RestartBlock.None;
        }

        public static bool IsOffered(RestartSituation s) => BlockedBy(s) == RestartBlock.None;

        /// <summary>After "Keep playing" the won-run flag silences later wins. A restart starts a
        /// loop that can be won again, like "Start a new loop" on the win screen. Returns whether
        /// the flag was set (and is now cleared).</summary>
        public static bool ClearWonRun(MetaState meta)
        {
            if (!meta.VictoryAcknowledged) return false;
            meta.VictoryAcknowledged = false;
            return true;
        }

        /// <summary>Branches whose morning rewinds the world: tonight's farm event is pointless and
        /// its end warp can orphan the chain.</summary>
        public static bool IsRewind(Day28Branch branch)
            => branch == Day28Branch.Fail || branch == Day28Branch.Restart;
    }
}
```

- [ ] **Step 5: Run the tests to see them pass**

```
dotnet test --filter "FullyQualifiedName~VoluntaryRestartTests"
```
Expected: 17 passed (10 facts plus 2 + 5 theory cases). Then the full suite:
```
dotnet test
```
Expected: baseline + 17, all passing.

- [ ] **Step 6: Build the mod** (the enum change must not break the `switch` in `OnCutsceneEnded`, which has a `default`)

```
dotnet build "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\src\TheLongestYear\TheLongestYear.csproj" -p:EnableModDeploy=false
```
Expected: `Build succeeded`.

- [ ] **Step 7: Bump and commit**

In `src/TheLongestYear/manifest.json` change `"Version": "0.18.52"` to `"Version": "0.18.53"`.

```
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear"
git add src/TheLongestYear.Core/Day28/Day28Branch.cs src/TheLongestYear.Core/Day28/VoluntaryRestart.cs tests/TheLongestYear.Tests/VoluntaryRestartTests.cs src/TheLongestYear/manifest.json
git commit -m "v0.18.53: voluntary restart rule (when the shrine button shows) and the Restart branch

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01MLy6J9QCsqjg9XvYFmm4UG"
git push
```

---

### Task 2: The chain understands `Restart`

Nothing sets `Restart` yet, so this task is behaviour-neutral for players. It teaches every reader of the pending branch what `Restart` means.

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs`
- Modify: `src/TheLongestYear/Integration/Day28CutsceneDriver.cs`
- Modify: `src/TheLongestYear/Loop/FarmEventSuppressionPatch.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (line ~158)
- Modify: `src/TheLongestYear/manifest.json` (0.18.53 -> 0.18.54)

- [ ] **Step 1: Make `RunController` partial**

In `src/TheLongestYear/Loop/RunController.cs` replace:
```csharp
    internal sealed class RunController
```
with:
```csharp
    internal sealed partial class RunController
```

- [ ] **Step 2: Skip the gate on a Restart night**

In `RunController.OnDayEnding`, replace:
```csharp
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            bool vaultGateSatisfied = VaultRules.IsVaultGateSatisfied(Run.Season, Run, _store.State);
```
with:
```csharp
            TheLongestYear.Integration.ItemDonationSync.Reconcile(Run);
            if (_pendingCutscene == Day28Branch.Restart)
            {
                // Voluntary restart (RunController.Restart.cs): the player chose to rewind tonight.
                // The gate does not judge this night: no checkpoint JP, and no second outcome may
                // overwrite the queued Restart. A room finished today must not play its restoration
                // scene just before the rewind undoes it, same as a Fail night.
                SuppressResetDoomedRoomScenes();
                _monitor.Log("Voluntary restart night: the day-end gate is skipped; the rewind runs in the morning.", LogLevel.Info);
                return;
            }
            bool vaultGateSatisfied = VaultRules.IsVaultGateSatisfied(Run.Season, Run, _store.State);
```

- [ ] **Step 3: Extract the Fail body and add the `Restart` case**

In `RunController.OnCutsceneEnded`, replace the whole `case Day28Branch.Fail:` block:
```csharp
                case Day28Branch.Fail:
                    // Hide the day/time HUD across the choice -> shop -> reset so the stale
                    // (pre-rewind) calendar date isn't shown while the player decides and shops.
                    // ContinueAfterResetSpend restores it once the world is back on Spring 1.
                    Game1.displayHUD = false;
                    // Vanilla mode's reset regenerates the board via loadForNewGame and never
                    // consults BundleSeedLoop, so holding would be a no-op that still charges JP.
                    // Read _config, not _store.State.BundleSource: PerformReset re-stamps the
                    // save's BundleSource from config at reset time, so config is what this reset
                    // will actually run under.
                    if (!BundleHold.IsOfferable(_config.BundleSource))
                    {
                        _monitor.Log("Hold choice skipped: BundleSource=Vanilla", LogLevel.Info);
                        TryOpenShrineThenContinue(ContinueAfterResetSpend);
                    }
                    else
                    {
                        ShowHoldChoice();
                    }
                    break;
```
with:
```csharp
                case Day28Branch.Fail:
                    StartRewindChain();
                    break;
                case Day28Branch.Restart:
                    // Voluntary restart: the Fail chain without the scene (the driver skipped it).
                    // After "Keep playing" the won-run flag silences later wins; a restart starts a
                    // loop that can be won again. FinalizeReset's _store.Save() persists the clear.
                    if (VoluntaryRestart.ClearWonRun(_store.State))
                        _monitor.Log("Voluntary restart after Keep playing: the won-run flag is cleared, so the next loop can be won again.", LogLevel.Info);
                    StartRewindChain();
                    break;
```

Then add this method directly after `OnCutsceneEnded` (the old Fail body, unchanged):
```csharp
        /// <summary>The rewind chain shared by a Fail night and a voluntary restart: hold question
        /// (whenever BundleHold.IsOfferable, which is every bundle source today), upgrade menu,
        /// recipe banking, reset.</summary>
        private void StartRewindChain()
        {
            // Hide the day/time HUD across the choice -> shop -> reset so the stale
            // (pre-rewind) calendar date isn't shown while the player decides and shops.
            // FinalizeReset restores it once the world is back on Spring 1.
            Game1.displayHUD = false;
            // Vanilla mode's reset regenerates the board via loadForNewGame and never
            // consults BundleSeedLoop, so holding would be a no-op that still charges JP.
            // Read _config, not _store.State.BundleSource: PerformReset re-stamps the
            // save's BundleSource from config at reset time, so config is what this reset
            // will actually run under.
            if (!BundleHold.IsOfferable(_config.BundleSource))
            {
                _monitor.Log("Hold choice skipped: BundleSource=Vanilla", LogLevel.Info);
                TryOpenShrineThenContinue(ContinueAfterResetSpend);
            }
            else
            {
                ShowHoldChoice();
            }
        }
```

Update the `OnCutsceneEnded` doc comment's first sentence to: `Called by the Day28CutsceneDriver when the day-28 bedtime cutscene has finished, or directly (no scene) for a voluntary Restart.` Remove any em dash you touch.

- [ ] **Step 4: The driver skips the scene for `Restart`**

In `src/TheLongestYear/Integration/Day28CutsceneDriver.cs`, replace:
```csharp
            if (Game1.activeClickableMenu != null) return;         // don't stack on another menu

            Day28Branch branch = rc.PendingCutscene;
```
with:
```csharp
            if (Game1.activeClickableMenu != null) return;         // don't stack on another menu

            Day28Branch branch = rc.PendingCutscene;
            if (branch == Day28Branch.Restart)
            {
                // Voluntary restart: no Junimo scene. There is no black scene to cover the morning
                // fade, so also wait for vanilla's end-of-night hand-off to finish (it clears
                // showingEndOfNightStuff once the save menu is gone), then run the continuation the
                // Fail scene's end would run. If the day never ended (the sleep did not take), this
                // runs the chain mid-day instead; RunController.EndDayNow logs that case.
                if (Game1.showingEndOfNightStuff) return;
                _monitor.Log("Voluntary restart: no Junimo scene; starting hold -> upgrade menu -> banking -> reset.", LogLevel.Info);
                rc.OnCutsceneEnded();
                return;
            }
```

- [ ] **Step 5: FarmEvent suppression covers `Restart`**

In `src/TheLongestYear/ModEntry.cs` replace:
```csharp
            FarmEventSuppressionPatch.SuppressTonight =
                () => _runController?.PendingCutscene == TheLongestYear.Core.Day28.Day28Branch.Fail;
```
with:
```csharp
            FarmEventSuppressionPatch.SuppressTonight =
                () => TheLongestYear.Core.Day28.VoluntaryRestart.IsRewind(
                    _runController?.PendingCutscene ?? TheLongestYear.Core.Day28.Day28Branch.None);
```
and change the comment above it from `Skip the overnight FarmEvent on FAIL nights` to `Skip the overnight FarmEvent on nights whose morning rewinds (Fail or a voluntary restart)`.

In `src/TheLongestYear/Loop/FarmEventSuppressionPatch.cs`, change the summary's first line to `Skips the overnight FarmEvent (...) on a night whose morning is a rewind (a FAIL night or a voluntary restart).` and the field comment to `Set by ModEntry: true when tonight's morning rewinds (Fail or Restart).` No logic change.

- [ ] **Step 6: Build and test**

```
dotnet build "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\src\TheLongestYear\TheLongestYear.csproj" -p:EnableModDeploy=false
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\tests\TheLongestYear.Tests"
dotnet test
```
Expected: `Build succeeded`; all tests pass (same count as after Task 1).

- [ ] **Step 7: Bump and commit**

`manifest.json`: `0.18.53` -> `0.18.54`.
```
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear"
git add src/TheLongestYear/Loop/RunController.cs src/TheLongestYear/Integration/Day28CutsceneDriver.cs src/TheLongestYear/Loop/FarmEventSuppressionPatch.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.18.54: the rewind chain handles a Restart branch (no scene, gate skipped that night)

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01MLy6J9QCsqjg9XvYFmm4UG"
git push
```

---

### Task 3: Confirm popup, end the day, and `tly_restart`

**Files:**
- Create: `src/TheLongestYear/Loop/RunController.Restart.cs`
- Modify: `src/TheLongestYear/UI/PlanningShrineService.cs` (`OpenMenu`)
- Modify: `src/TheLongestYear/i18n/default.json`
- Modify: `src/TheLongestYear/ModEntry.cs` (console command, bridge case, handler)
- Modify: `src/TheLongestYear/manifest.json` (0.18.54 -> 0.18.55)

- [ ] **Step 1: The confirm string (DRAFT)**

In `src/TheLongestYear/i18n/default.json`, directly after the `"dialog.hold.not-enough-jp"` line, add:
```json
    // DRAFT: wording pending the game-writing skill (voluntary restart, 2026-09-24). Jeff's draft.
    "dialog.restart.prompt": "Are you sure? This resets all progress, just like a failed season.",
```
The Yes/No answers are vanilla's own (`GameLocation.createYesNoResponses`), already translated by the game.

- [ ] **Step 2: The shrine's open path as one method**

Choosing No returns the player to the Junimo Shrine view, so the restart code must open it exactly as the statue does. In `src/TheLongestYear/UI/PlanningShrineService.cs`, after `AttachBoosts`, add:
```csharp

        /// <summary>Open the Junimo Shrine view exactly as acting on the statue does, with the same
        /// attached hooks. Also used to return to it when the player answers No to Restart the
        /// year. Returns false (and opens nothing) when no save state is attached.</summary>
        internal static bool OpenMenu()
        {
            MetaState state = _state?.Invoke();
            if (state == null) return false;
            Game1.activeClickableMenu = new ShrinePreviewMenu(
                state, _priceFactor?.Invoke() ?? 1.0, _run?.Invoke(), _buyBoost);
            return true;
        }
```
and in `ShrineActionPatch.Prefix` replace:
```csharp
                Game1.activeClickableMenu = new ShrinePreviewMenu(
                    state, _priceFactor?.Invoke() ?? 1.0, _run?.Invoke(), _buyBoost);
                __result = true;
```
with:
```csharp
                OpenMenu();
                __result = true;
```
(`state` was already checked non-null above, so `OpenMenu` always opens here. The intro-quest code stays in the prefix: it belongs to a real statue click only.)

- [ ] **Step 3: Create the partial**

Create `src/TheLongestYear/Loop/RunController.Restart.cs`:

```csharp
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;
using TheLongestYear.Core.Day28;

namespace TheLongestYear.Loop
{
    /// <summary>Voluntary restart at the Junimo Shrine (spec
    /// docs/superpowers/specs/2026-09-24-voluntary-restart-design.md). The shrine's button asks a
    /// vanilla yes/no question; Yes queues <see cref="Day28Branch.Restart"/> and ends the day at
    /// once. That night <see cref="OnDayEnding"/> skips the gate; in the morning the
    /// Day28CutsceneDriver skips the scene and calls <see cref="OnCutsceneEnded"/>, which runs the
    /// Fail chain (hold, upgrade menu, books, reset).</summary>
    internal sealed partial class RunController
    {
        /// <summary>True while any part of a rewind or win chain is queued or waiting on a menu.</summary>
        public bool IsRewindChainRunning =>
            _pendingCutscene != Day28Branch.None
            || _shrineOpenPending != null
            || _menuWatch != null
            || _holdReaskPending;

        private RestartSituation RestartSituationNow() => new(
            DayOfMonth: Game1.dayOfMonth,
            EventUp: Game1.eventUp || Game1.CurrentEvent != null || Game1.farmEvent != null || Game1.isFestival(),
            ResetRunning: IsRewindChainRunning || Game1.newDay);

        /// <summary>Why the shrine's Restart the year button is hidden right now (None = shown).</summary>
        public RestartBlock VoluntaryRestartBlock() => VoluntaryRestart.BlockedBy(RestartSituationNow());

        public bool IsVoluntaryRestartOffered() => VoluntaryRestartBlock() == RestartBlock.None;

        /// <summary>The button's action (and <c>tly_restart</c>): ask the vanilla yes/no question.
        /// No changes nothing and returns the player to the Junimo Shrine view, opened the same way
        /// the statue opens it (<see cref="TheLongestYear.UI.PlanningShrineService.OpenMenu"/>).</summary>
        public void AskVoluntaryRestart()
        {
            RestartBlock block = VoluntaryRestartBlock();
            if (block != RestartBlock.None)
            {
                _monitor.Log($"Voluntary restart not offered right now ({block}).", LogLevel.Info);
                return;
            }
            GameLocation loc = Game1.currentLocation ?? Game1.player?.currentLocation;
            if (loc == null)
            {
                _monitor.Log("Voluntary restart: no current location to host the question; nothing changed.", LogLevel.Warn);
                return;
            }
            loc.createQuestionDialogue(Strings.Get("dialog.restart.prompt"), loc.createYesNoResponses(), (Farmer who, string key) =>
            {
                if (key == "Yes")
                {
                    BeginVoluntaryRestart();
                    return;
                }
                // No: back to the shrine view the button was pressed from. Opening a menu from a
                // question answer is what vanilla does too: the DialogueBox only closes itself while
                // it is still the active menu, so the shrine replaces it cleanly.
                if (TheLongestYear.UI.PlanningShrineService.OpenMenu())
                    _monitor.Log("Voluntary restart: the player chose No. Nothing changed; back to the Junimo Shrine.", LogLevel.Info);
                else
                    _monitor.Log("Voluntary restart: the player chose No. Nothing changed; the shrine could not reopen (no save state attached).", LogLevel.Warn);
            });
            _monitor.Log("Voluntary restart: confirm opened.", LogLevel.Info);
        }

        /// <summary>Yes: queue the Restart branch and end the day. Runs inside the question's
        /// answer callback, the same place vanilla's bed question runs its own sleep.</summary>
        private void BeginVoluntaryRestart()
        {
            // Re-check: the answer arrives after the question opened. The question box itself is a
            // menu, not an event, so the snapshot is still honest.
            RestartBlock block = VoluntaryRestartBlock();
            if (block != RestartBlock.None)
            {
                _monitor.Log($"Voluntary restart: confirmed, but no longer allowed ({block}); nothing changed.", LogLevel.Warn);
                return;
            }
            _monitor.Log(
                $"Voluntary restart confirmed on {Game1.season} {Game1.dayOfMonth} at {Game1.timeOfDay} " +
                $"(run {Run.RunNumber}, {_store.State.JunimoPoints} JP banked). Ending the day now.",
                LogLevel.Info);
            _pendingCutscene = Day28Branch.Restart;
            EndDayNow();
        }

        /// <summary>Put the host to sleep where they stand, with vanilla's own <c>debug sleep</c>
        /// recipe (DebugCommands.Sleep): <c>Game1.NewDay</c> only fades to black, and so only ever
        /// reaches newDayAfterFade, when <c>isInBed</c> is true, and Farmer.Update re-derives
        /// <c>isInBed</c> every tick unless <c>sleptInTemporaryBed</c> is set. "Sleep_Yes" is the
        /// bed question's answer: startSleep, then doSleep, then NewDay. No pass-out penalty: that
        /// lives only in Farmer.performPassoutWarp. SaveGame.Save clears sleptInTemporaryBed.</summary>
        private void EndDayNow()
        {
            Farmer player = Game1.player;
            GameLocation here = Game1.currentLocation ?? player.currentLocation;
            player.isInBed.Value = true;
            player.sleptInTemporaryBed.Value = true;
            here.answerDialogueAction("Sleep_Yes", null);

            // doSleep recorded the statue as the sleep spot. Point it at the real bed so a quit
            // after tonight's save reloads the player in the farmhouse (SaveGame load reads it).
            FarmHouse home = Utility.getHomeOfFarmer(player);
            if (home != null)
            {
                player.lastSleepLocation.Value = home.NameOrUniqueName;
                player.lastSleepPoint.Value = home.GetPlayerBedSpot();
            }

            if (Game1.newDay)
                _monitor.Log("Voluntary restart: the day is ending.", LogLevel.Info);
            else
                _monitor.Log(
                    "Voluntary restart: the game did not start a new day. The chain will run right away, mid-day, " +
                    "once the question box closes.",
                    LogLevel.Warn);
        }
    }
}
```

Note: `_pendingCutscene`, `_shrineOpenPending`, `_menuWatch`, `_holdReaskPending`, `_monitor`, `_store` and `Run` are private members of the main file; a partial shares them.

- [ ] **Step 4: `tly_restart` (console and bridge)**

In `src/TheLongestYear/ModEntry.cs`, after the `tly_failreset` registration (line ~276), add:
```csharp
            helper.ConsoleCommands.Add("tly_restart", "Debug: press the Junimo Shrine's Restart the year button. Opens the same yes/no (tly_answer 0 = Yes, 1 = No); refuses and logs why when the button would be hidden.", this.CmdRestart);
```
In `ExecuteDebugLine`, after the `case "tly_failreset":` block, add:
```csharp
                case "tly_restart": this.CmdRestart(command, args); break;
```
Next to `CmdFailReset`, add:
```csharp
        /// <summary>Debug: the shrine's Restart the year button without the mouse.</summary>
        private void CmdRestart(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _runController?.AskVoluntaryRestart();
        }
```

- [ ] **Step 5: Build and test**

```
dotnet build "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\src\TheLongestYear\TheLongestYear.csproj" -p:EnableModDeploy=false
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\tests\TheLongestYear.Tests"
dotnet test
```
Expected: `Build succeeded`; all tests pass, including `I18nGuardTests` (the new key is referenced by `Strings.Get("dialog.restart.prompt")`).

- [ ] **Step 6: Bump and commit**

`manifest.json`: `0.18.54` -> `0.18.55`.
```
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear"
git add src/TheLongestYear/Loop/RunController.Restart.cs src/TheLongestYear/UI/PlanningShrineService.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/ModEntry.cs src/TheLongestYear/manifest.json
git commit -m "v0.18.55: voluntary restart confirm ends the day at once (No returns to the shrine); tly_restart debug command

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01MLy6J9QCsqjg9XvYFmm4UG"
git push
```

---

### Task 4: The button on the Junimo Shrine

**Files:**
- Modify: `src/TheLongestYear/UI/ShrinePreviewMenu.cs`
- Modify: `src/TheLongestYear/UI/PlanningShrineService.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (wiring after `AttachBoosts`, `CmdOpenShrine`)
- Modify: `src/TheLongestYear/i18n/default.json`
- Modify: `src/TheLongestYear/manifest.json` (0.18.55 -> 0.18.56)

- [ ] **Step 1: The label string (DRAFT)**

In `default.json`, directly after `"shrine.tab.plan": "Plan",` add:
```json
    // DRAFT: wording pending the game-writing skill (voluntary restart, 2026-09-24).
    "shrine.restart.button": "Restart the year",
```

- [ ] **Step 2: Menu constants and fields**

In `ShrinePreviewMenu.cs`, after the tab-strip constants (after `private const int TabStripH = TabHeight + 12;`), add:
```csharp

        // ---- Restart the year (spec 2026-09-24-voluntary-restart): right end of the tab strip ----
        private const int RestartButtonId = 6300;
        private const int RestartButtonMinWidth = 220;
        private const int RestartButtonPadding = 32;
```
After `private readonly double _priceFactor;` add:
```csharp
        private readonly bool _showRestart;
        private readonly Action _requestRestart;
        private ClickableComponent _restartButton;
```

- [ ] **Step 3: Constructor**

Replace:
```csharp
        public ShrinePreviewMenu(MetaState state, double priceFactor = 1.0, RunState run = null,
            Func<BoostId, int, BoostPurchase.Result> buyBoost = null)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _state = state;
            _priceFactor = priceFactor;
            _run = run;
            _buyBoost = buyBoost;
```
with:
```csharp
        public ShrinePreviewMenu(MetaState state, double priceFactor = 1.0, RunState run = null,
            Func<BoostId, int, BoostPurchase.Result> buyBoost = null,
            Func<bool> restartOffered = null, Action requestRestart = null)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _state = state;
            _priceFactor = priceFactor;
            _run = run;
            _buyBoost = buyBoost;
            // Read once at open: single-player time is paused while a menu is up, so nothing the
            // rule reads can change before the menu closes.
            _requestRestart = requestRestart;
            _showRestart = requestRestart != null && (restartOffered?.Invoke() ?? false);
```

- [ ] **Step 4: Layout**

In `RecomputeBoundsAndLayout`, replace:
```csharp
            LayoutForesight();
```
with:
```csharp
            _restartButton = null;
            if (_showRestart)
            {
                string restartLabel = Strings.Get("shrine.restart.button");
                int w = Math.Max(RestartButtonMinWidth, (int)Game1.smallFont.MeasureString(restartLabel).X + RestartButtonPadding);
                _restartButton = new ClickableComponent(
                    new Rectangle(_listX + _listWidth - w, yPositionOnScreen + TabsTop, w, TabHeight), "restart")
                {
                    myID = RestartButtonId,
                    leftNeighborID = TabIdBase + tabs.Length - 1,
                    downNeighborID = RowIdBase,
                };
                _tabs[tabs.Length - 1].rightNeighborID = RestartButtonId;
            }

            LayoutForesight();
```
and replace:
```csharp
            allClickableComponents = new List<ClickableComponent>(_tabs) { _scrollUp, _scrollDown };
```
with:
```csharp
            allClickableComponents = new List<ClickableComponent>(_tabs) { _scrollUp, _scrollDown };
            if (_restartButton != null)
                allClickableComponents.Add(_restartButton);
```

- [ ] **Step 5: Input**

In `receiveGamePadButton`, replace:
```csharp
                if (id == ScrollUpId) { Scroll(-1); return; }
```
with:
```csharp
                if (id == RestartButtonId && _restartButton != null) { RequestRestart(); return; }
                if (id == ScrollUpId) { Scroll(-1); return; }
```
In `receiveLeftClick`, replace:
```csharp
            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
```
with:
```csharp
            if (_restartButton != null && _restartButton.containsPoint(x, y)) { RequestRestart(); return; }
            if (_scrollUp.containsPoint(x, y)) { Scroll(-1); return; }
```
Add after `ActivateRow`:
```csharp
        /// <summary>Close the shrine and hand over to the mod's yes/no. The question box would
        /// replace this menu anyway; closing first keeps the hand-off clean. Answering No reopens
        /// the shrine (RunController.AskVoluntaryRestart).</summary>
        private void RequestRestart()
        {
            Game1.playSound("smallSelect");
            exitThisMenuNoSound();
            _requestRestart();
        }
```

- [ ] **Step 6: Draw**

In `draw`, replace:
```csharp
            DrawForesight(b);
```
with:
```csharp
            if (_restartButton != null)
            {
                Rectangle r = _restartButton.bounds;
                IClickableMenu.drawTextureBox(b, Game1.mouseCursors, new Rectangle(432, 439, 9, 9),
                    r.X, r.Y, r.Width, r.Height, Color.White, 4f, drawShadow: false);
                string restartLabel = Strings.Get("shrine.restart.button");
                Vector2 restartSize = Game1.smallFont.MeasureString(restartLabel);
                Utility.drawTextWithShadow(b, restartLabel, Game1.smallFont,
                    new Vector2(r.X + (r.Width - restartSize.X) / 2f, r.Y + (r.Height - restartSize.Y) / 2f),
                    Game1.textColor);
            }

            DrawForesight(b);
```
(Same box sprite as the Boosts tab's Buy button.)

- [ ] **Step 7: Service hooks**

In `PlanningShrineService.cs`, after the `_buyBoost` field add:
```csharp

        /// <summary>The voluntary restart hooks: whether the button shows, and what it does.
        /// Same static-hook idiom as <see cref="_state"/>. Unattached, the menu shows no button.</summary>
        private static System.Func<bool> _restartOffered;
        private static System.Action _requestRestart;
```
after `AttachBoosts` add:
```csharp

        public void AttachRestart(System.Func<bool> offered, System.Action request)
        {
            _restartOffered = offered;
            _requestRestart = request;
        }
```
and in `OpenMenu` (Task 3) replace:
```csharp
            Game1.activeClickableMenu = new ShrinePreviewMenu(
                state, _priceFactor?.Invoke() ?? 1.0, _run?.Invoke(), _buyBoost);
```
with:
```csharp
            Game1.activeClickableMenu = new ShrinePreviewMenu(
                state, _priceFactor?.Invoke() ?? 1.0, _run?.Invoke(), _buyBoost,
                _restartOffered, _requestRestart);
```
(Both the statue click and the No answer go through `OpenMenu`, so a reopened shrine shows the button again.)

- [ ] **Step 8: ModEntry wiring**

After the `_planningShrine.AttachBoosts(...)` statement (it ends with `});` around line 719), add:
```csharp
            _planningShrine.AttachRestart(
                () => _runController?.IsVoluntaryRestartOffered() == true,
                () => _runController?.AskVoluntaryRestart());
```
In `CmdOpenShrine`, replace:
```csharp
            var menu = new TheLongestYear.UI.ShrinePreviewMenu(
                _meta.State, _meta.State.EffectiveDifficulty(_config).ShrinePriceFactor, _meta.Run,
                (id, skill) => _boostPurchases.TryBuy(id, skill));
            menu.ShowTab(tab);
            Game1.activeClickableMenu = menu;
            this.Monitor.Log($"tly_openshrine: shrine opened on the {tab} tab.", LogLevel.Info);
```
with:
```csharp
            var menu = new TheLongestYear.UI.ShrinePreviewMenu(
                _meta.State, _meta.State.EffectiveDifficulty(_config).ShrinePriceFactor, _meta.Run,
                (id, skill) => _boostPurchases.TryBuy(id, skill),
                () => _runController?.IsVoluntaryRestartOffered() == true,
                () => _runController?.AskVoluntaryRestart());
            menu.ShowTab(tab);
            Game1.activeClickableMenu = menu;
            this.Monitor.Log($"tly_openshrine: shrine opened on the {tab} tab.", LogLevel.Info);
            var block = _runController?.VoluntaryRestartBlock() ?? TheLongestYear.Core.Day28.RestartBlock.ResetRunning;
            this.Monitor.Log(
                block == TheLongestYear.Core.Day28.RestartBlock.None
                    ? "tly_openshrine: restart button shown."
                    : $"tly_openshrine: restart button hidden ({block}).",
                LogLevel.Info);
```
(The block is read after the menu opened; an open menu is not an event, so the verdict matches what the constructor saw.)

- [ ] **Step 9: Build and test**

```
dotnet build "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\src\TheLongestYear\TheLongestYear.csproj" -p:EnableModDeploy=false
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\tests\TheLongestYear.Tests"
dotnet test
```
Expected: `Build succeeded`; all tests pass (the i18n guard sees `shrine.restart.button` referenced).

- [ ] **Step 10: Bump and commit**

`manifest.json`: `0.18.55` -> `0.18.56`.
```
cd "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear"
git add src/TheLongestYear/UI/ShrinePreviewMenu.cs src/TheLongestYear/UI/PlanningShrineService.cs src/TheLongestYear/ModEntry.cs src/TheLongestYear/i18n/default.json src/TheLongestYear/manifest.json
git commit -m "v0.18.56: Restart the year button on the Junimo Shrine

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01MLy6J9QCsqjg9XvYFmm4UG"
git push
```

---

### Task 5: Live test on the Rodger throwaway save

Follows `docs/HEADLESS_DRIVING.md`. Headless only: bridge, SMAPI log, no mouse or keyboard tools.

- [ ] **Step 1: Ask Jeff before launching** (memory `ask-before-driving-desktop`). Say it is **my automated launch** on the Rodger save. One yes covers the session.

- [ ] **Step 2: Find the current Rodger folder**

```
Get-ChildItem "$env:APPDATA\StardewValley\Saves" -Directory | Where-Object Name -like 'None_*' | Sort-Object LastWriteTime -Descending | Select-Object Name, LastWriteTime
```
Pick the Rodger lineage (the log's last `tly_loadsave` / reset rename names it). Never `None_443632257`, `PuffPuff_*` or `Cheatside_*`. Confirm `EnableDebugCommandBridge: true` in the deployed `config.json`.

- [ ] **Step 3: Deploy and load** (standard sequence; `tly_loadsave` only, never the Load menu)

```
pwsh -NoProfile -File tools/bridge.ps1 -Action count          # note n
pwsh -NoProfile -File tools/deploy.ps1 -Minimized
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "Debug bridge: 'pause when window is inactive'" -FromLine <n> -TimeoutSec 180
pwsh -NoProfile -File tools/bridge.ps1 -Action send -Lines "tly_loadsave <RodgerFolder>"
pwsh -NoProfile -File tools/bridge.ps1 -Action wait -Pattern "Run \d+ ready" -FromLine <n> -TimeoutSec 120
```
Wait about 45 s more. Then `git checkout -- test-output/log-archive` (never stage the pruned archives). Confirm the log shows `0.18.56`.

If Rodger is not in Spring: `tly_reset`, wait `Opened planning hub`, `tly_select Farming`. Then `tly_setday 12`.

For each step below take `n = count` before sending and wait with `-FromLine n`.

- [ ] **Step 4: No returns to the shrine and changes nothing**

Send `tly_runstate` and note run N, day, JP banked. Send `tly_openshrine`; expect `tly_openshrine: restart button shown.`; send `tly_dismiss`. Send `tly_restart`; wait `Voluntary restart: confirm opened.` Send `tly_answer 1`; wait `the player chose No. Nothing changed; back to the Junimo Shrine.` (a Warn `could not reopen` fails this step). The shrine is open again: send `tly_dismiss` and expect `tly_dismiss: ShrinePreviewMenu closed.` Send `tly_runstate`: run, day and JP identical.

- [ ] **Step 5: Yes, reshuffle, full chain**

Send `tly_restart`, wait `confirm opened`, send `tly_answer 0`. Expect in order:
- `Voluntary restart confirmed on Spring 12 ... Ending the day now.`
- `Voluntary restart: the day is ending.` (a Warn `did not start a new day` here means the timing approach failed; go to Step 10)
- `Voluntary restart night: the day-end gate is skipped`
- no `Season checkpoint passed` line, no `Day-28 cutscene: opening`
- after the night save (send `tly_dismiss` if a LevelUpMenu is up): `Voluntary restart: no Junimo scene; starting hold`

Then send `tly_restart` once while the hold question is up; expect `not offered right now (ResetRunning)`. Send `tly_answer 1` (reshuffle); expect `Hold choice: Reshuffled`. Expect `Opened Junimo Shrine (JP: <same as before>)`; send `tly_dismiss`. If a book opens (`offered before the reset`), `tly_dismiss` it. Expect `FinalizeReset (shrine closed)` and `Loop reset complete. Run <N+1> begins`. Send `tly_runstate`: Spring day 1, run N+1, JP unchanged.

- [ ] **Step 6: Keep carries the board**

Pick the week-1 theme (`tly_select <theme>` once `Opened planning hub`). `tly_setday 12`. Send `tly_gateneeds` and save the bundle list. Restart as in Step 5 but answer the hold question `tly_answer 0`; expect `Hold choice: KEEP (cost 0 JP, consecutive holds now 1, seed loop <S>)`. After `Loop reset complete`, `tly_gateneeds` lists the same bundles; `tly_hold status` shows seed loop S.

- [ ] **Step 7: After a win and Keep playing**

`tly_setday 12`. Send `tly_win`; wait `opening the Win Junimo scene`; `tly_skipscene`; `Opened Junimo Shrine`; `tly_dismiss`; on the win question `tly_answer 1`; expect `VictoryAcknowledged set`. `tly_runstate` shows `victoryAcknowledged=True`. Restart as in Step 5 (reshuffle); expect `the won-run flag is cleared, so the next loop can be won again`. After `Loop reset complete`, `tly_runstate` shows `victoryAcknowledged=False`.

- [ ] **Step 8: Hidden on day 28**

`tly_setday 28`; `tly_openshrine`: `restart button hidden (SeasonEndDay)`; `tly_dismiss`; `tly_restart`: `not offered right now (SeasonEndDay)`. `tly_setday 12` afterwards.

- [ ] **Step 9: The real Fail night still works** (regression)

`tly_failreset`; expect `opening the Fail Junimo scene`; `tly_skipscene`; hold question; `tly_answer 1`; shrine; `tly_dismiss`; `Loop reset complete`.

- [ ] **Step 10: Only if Step 5 showed the day did not end**

Mid-day is the fallback and needs a clean live run before it ships (spec). Delete the `EndDayNow();` call in `BeginVoluntaryRestart` (keep the method for a later fix), rebuild, redeploy, rerun Steps 4 to 9, report the result to Jeff and let him choose. Any code fix in this task is its own commit with a PATCH bump.

- [ ] **Step 11: Offer Jeff a look** at the button and the fade in his own game (his launch, his call). Report from the log, quoting lines; say what is committed, pushed and deployed.

---

### Task 6: Final wording through the game-writing skill

- [ ] **Step 1:** Invoke the `game-writing` skill for the two DRAFT strings: `shrine.restart.button` (button label, fits about 220 px of small font) and `dialog.restart.prompt` (Jeff's draft: "Are you sure? This resets all progress, just like a failed season."). Remember `tly-shrine-terminology`: the statue is the Junimo Shrine; the menu after a rewind is Junimo Upgrades.
- [ ] **Step 2:** Show Jeff the options verbatim and wait for his pick.
- [ ] **Step 3:** Put the chosen text in `default.json`, delete both `// DRAFT` comment lines, build, `dotnet test`, bump `manifest.json` PATCH (0.18.57 if Task 5 needed no fixes), commit `v0.18.57: voluntary restart wording`, push.

---

### Task 7: README, Nexus description, changelog

README and `docs/nexus-description.bbcode` must say the same thing (house style). Use the final label from Task 6 and the manifest version at this point (0.18.57 if nothing else changed). Replace `<ver>` below with that version. No em dashes.

- [ ] **Step 1: README** (`README.md`), insert above `## What's New in 0.18.51`:

```markdown
## What's New in <ver>

**Start the year over whenever you like, from the Junimo Shrine.**

- **Restart the year.** The Junimo Shrine on your farm has a new Restart the year button. It works like a failed season without the Junimo scene: the day ends right away, you choose whether to keep your bundles, spend JP in Junimo Upgrades, bank recipes, and wake on Spring 1 of the next loop. Your JP carries over as usual, and a restart counts as a loop, so holding the same board again still costs more each time in a row. The button is hidden on the last day of a season and while an event or festival is playing. Suggested by tanky24u.
- **It works after Keep playing too.** If you won and kept playing, a restart starts a fresh loop that can be won again.

```

- [ ] **Step 2: Nexus description** (`docs/nexus-description.bbcode`), insert above `[size=5][b]What's New in 0.18.51[/b][/size]`:

```
[size=5][b]What's New in <ver>[/b][/size]

[b]Start the year over whenever you like, from the Junimo Shrine.[/b]

[list]
[*][b]Restart the year.[/b] The Junimo Shrine on your farm has a new Restart the year button. It works like a failed season without the Junimo scene: the day ends right away, you choose whether to keep your bundles, spend JP in Junimo Upgrades, bank recipes, and wake on Spring 1 of the next loop. Your JP carries over as usual, and a restart counts as a loop, so holding the same board again still costs more each time in a row. The button is hidden on the last day of a season and while an event or festival is playing. Suggested by tanky24u.
[*][b]It works after Keep playing too.[/b] If you won and kept playing, a restart starts a fresh loop that can be won again.
[/list]

[line]

```

- [ ] **Step 3: CHANGELOG** (`CHANGELOG.md`), insert above `## 0.18.51 - 2026-09-24` (fill the date and the test count from the last `dotnet test`):

```markdown
## <ver> - <date>

<count> tests.

### Added

- **Restart the year at the Junimo Shrine.** A button on the statue's planning view asks a yes/no, then ends the day at once and runs the Fail-night chain with the Junimo scene removed: keep-or-reshuffle question, Junimo Upgrades, Cookbook and Craftbook banking, reset to Spring 1. Nothing is paid out; JP is already banked. It counts as a loop for the loop number and for consecutive hold prices. Hidden on day 28 (the real gate owns that night), while an event, cutscene or festival is playing, and while another reset is running. Choosing No returns to the shrine. After Keep playing it clears the won-run flag so the next loop can be won. Suggested by tanky24u (Nexus posts, 2026-09-23).
- **`tly_restart`** debug command: presses the button headlessly (answer with `tly_answer 0` / `1`).
```

- [ ] **Step 4: Commit (docs only, no version bump) and push**

```
git add README.md docs/nexus-description.bbcode CHANGELOG.md
git commit -m "docs: voluntary restart release notes (changelog, README, Nexus description)

Co-Authored-By: Claude Opus 5.5 (1M context) <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01MLy6J9QCsqjg9XvYFmm4UG"
git push
```
The live Nexus page is not touched here; that needs Jeff's yes and the Claude-in-Chrome route in `docs/RELEASE_TOOLING.md`.

---

## Self-Review

- Spec coverage: button and confirm (Tasks 3, 4); hold, upgrade menu, banking, reset reused unchanged (Task 2 `StartRewindChain`); no payout (gate skipped, nothing awarded); counts as a fail (`FinalizeReset`, same hold curve); won-run flag (Task 2); No returns to the shrine (Task 3 `OpenMenu`); hidden during events (a festival in progress included), on day 28 and while a reset is running (Task 1 rule, Task 3 snapshot); no festival-day or multiplayer rule (Jeff, 2026-09-24); timing via the morning path with the mid-day fallback visible (Tasks 2, 3, 5); Testing section (Task 1 unit tests, Task 5 live).
- Spec drift found while planning: the spec says the hold question "is skipped on a Vanilla board, exactly as on a fail night", but `BundleHold.IsOfferable` now returns true for every bundle source (Jeff's ruling 2026-08-27), so the question is asked on every board. This plan keeps exact Fail-night parity by reusing the same code, so a restart asks it wherever a Fail night does.
- Placeholder scan: the only placeholders are the two DRAFT strings (by design, Task 6) and `<ver>` / `<date>` / `<count>` in Task 7, which depend on earlier results.
- Type consistency: `RestartSituation`, `RestartBlock`, `VoluntaryRestart.BlockedBy/IsOffered/ClearWonRun/IsRewind`, `RunController.IsRewindChainRunning/VoluntaryRestartBlock/IsVoluntaryRestartOffered/AskVoluntaryRestart` are used with the same names everywhere.
