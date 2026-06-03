# 0.9 Beta Win Screen Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship the 0.9 beta milestone — a basic self-drawn win screen (`VictoryMenu`), a `tly_win` debug trigger, and a version bump to `0.9.0`.

**Architecture:** Add `Day28Branch.Win` and route the win through the existing `Day28CutsceneDriver` → `RunController.OnCutsceneEnded` plumbing (retiring the parallel `_pendingWinChoice` path). The win screen is a new `IClickableMenu` mirroring `Day28CutsceneMenu`: black bg + three differently-tinted Junimos + a single text page showing the loop count, then → shrine → keep-playing choice.

**Tech Stack:** C# / .NET 6, SMAPI 4.x, Harmony, MonoGame (XNA). `dotnet build`/`dotnet test`. Self-verify via PrintWindow screenshot.

**Reference design:** `docs/superpowers/specs/2026-06-03-09-beta-win-screen-design.md`

**Note on testing:** Per project convention only `TheLongestYear.Core` is unit-testable; UI (`VictoryMenu`), the driver, and `ModEntry` glue are verified by compile + SMAPI log + PrintWindow screenshot. Tasks reflect that.

---

## File Structure

| File | Change | Responsibility |
|------|--------|----------------|
| `src/TheLongestYear.Core/Day28/Day28Branch.cs` | Modify | Add `Win` enum value |
| `src/TheLongestYear/UI/VictoryMenu.cs` | Create | Self-drawn win screen (3 Junimos, single page, loop count) |
| `src/TheLongestYear/Loop/RunController.cs` | Modify | Queue `Win`; `OnCutsceneEnded` Win case; `CurrentRunNumber`; `DebugForceWin`; retire `_pendingWinChoice` |
| `src/TheLongestYear/Integration/Day28CutsceneDriver.cs` | Modify | Open `VictoryMenu` for the `Win` branch |
| `src/TheLongestYear/ModEntry.cs` | Modify | Register `tly_win` (console + file bridge) + `CmdForceWin` |
| `src/TheLongestYear/manifest.json` | Modify | `Version` → `0.9.0` |

---

## Task 1: Add `Day28Branch.Win`

**Files:**
- Modify: `src/TheLongestYear.Core/Day28/Day28Branch.cs`

- [ ] **Step 1: Add the enum value**

Replace the enum body so it reads:

```csharp
    public enum Day28Branch
    {
        None,
        Fail,     // gate closed → rewind dialogue → JP shop → reset to Spring 1
        Continue, // gate open → congratulations → roll into the next season
        Win       // loop completed (CC restored) → win screen → JP shop → keep-playing choice
    }
```

- [ ] **Step 2: Compile-only build to verify it still builds**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded (the new value is unused so far — that's fine).

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear.Core/Day28/Day28Branch.cs
git commit -m "feat(core): add Day28Branch.Win for the win-screen cutscene branch"
```

---

## Task 2: Create `VictoryMenu`

**Files:**
- Create: `src/TheLongestYear/UI/VictoryMenu.cs`

- [ ] **Step 1: Write the menu**

Create `src/TheLongestYear/UI/VictoryMenu.cs` with exactly:

```csharp
using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewValley;
using StardewValley.Menus;

namespace TheLongestYear.UI
{
    /// <summary>The 0.9 "basic win" screen, drawn entirely by us (NOT a vanilla Event — same hard-won
    /// reason as <see cref="Day28CutsceneMenu"/>: events fade/blink/cover the dialogue and a portrait-
    /// less Junimo spams TryLoadPortraits). A single page over full black: a row of three differently-
    /// tinted Junimos, a title line, and the loop-count payoff. Dismissed by A / click / Enter; B and
    /// ESC are ignored. Closes itself and invokes <paramref name="onComplete"/> (the win path then
    /// opens the JP shrine and the keep-playing choice). The elaborate payoff cutscene is deferred to 1.0.</summary>
    internal sealed class VictoryMenu : IClickableMenu
    {
        private const string TitleLine = "The Junimos sing! You restored the Community Center.";
        private const string ContinueHint = "(press A or click to continue)";

        // Junimo sprite: frame 0 of the 16×16 Characters\Junimo sheet (a white silhouette meant to be
        // tinted), scaled up. Three are drawn in a row, each a different colour, for a little payoff.
        private const int JunimoFrame = 16;
        private const float JunimoScale = 5f;
        private const float JunimoGap = 48f; // gap between the scaled sprites
        private static readonly Color[] JunimoTints =
        {
            new Color(110, 200, 74),   // classic green
            new Color(90, 160, 235),   // blue
            new Color(235, 110, 110),  // red
        };

        private readonly string _loopLine;
        private readonly Action _onComplete;
        private readonly Texture2D _junimoTexture;
        private bool _done;

        public VictoryMenu(int runNumber, Action onComplete)
            : base(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height, showUpperRightCloseButton: false)
        {
            // RunNumber is the attempt count (incremented only on a full loop reset), so it reads as
            // "which loop you won on" — the roguelite payoff stat. Grammar-cased for loop 1.
            _loopLine = runNumber <= 1
                ? "You restored it on your very first loop!"
                : $"It took {runNumber} loops.";
            _onComplete = onComplete;

            try { _junimoTexture = Game1.content.Load<Texture2D>("Characters\\Junimo"); }
            catch (Exception) { _junimoTexture = null; }

            Game1.playSound("junimoMeep1");
        }

        private void Finish()
        {
            if (_done) return;
            _done = true;
            if (Game1.activeClickableMenu == this)
                Game1.activeClickableMenu = null;
            _onComplete?.Invoke();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true) => Finish();

        public override void receiveGamePadButton(Buttons b)
        {
            if (b == Buttons.A || b == Buttons.Start)
                Finish();
            // ignore B / Back.
        }

        public override void receiveKeyPress(Keys key)
        {
            if (key == Keys.Enter || key == Keys.Space
                || Game1.options.doesInputListContain(Game1.options.actionButton, key))
                Finish();
            // ignore the menu/cancel button.
        }

        public override void receiveRightClick(int x, int y, bool playSound = true) { }

        // Forced screen: never satisfy the engine's close paths (ESC / controller-B). We close
        // ourselves in Finish() on the dismiss input.
        public override bool readyToClose() => false;

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            width = Game1.uiViewport.Width;
            height = Game1.uiViewport.Height;
        }

        public override void draw(SpriteBatch b)
        {
            int w = Game1.uiViewport.Width;
            int h = Game1.uiViewport.Height;

            b.Draw(Game1.fadeToBlackRect, new Rectangle(0, 0, w, h), Color.Black);

            float junimoSize = JunimoFrame * JunimoScale;
            float junimoY = h * 0.32f;

            // Row of three differently-tinted Junimos, centered horizontally.
            if (_junimoTexture != null)
            {
                int count = JunimoTints.Length;
                float rowWidth = count * junimoSize + (count - 1) * JunimoGap;
                float startX = (w - rowWidth) / 2f;
                for (int i = 0; i < count; i++)
                {
                    float x = startX + i * (junimoSize + JunimoGap);
                    b.Draw(_junimoTexture,
                        new Vector2(x, junimoY),
                        new Rectangle(0, 0, JunimoFrame, JunimoFrame),
                        JunimoTints[i], 0f, Vector2.Zero, JunimoScale, SpriteEffects.None, 0.9f);
                }
            }

            float textY = junimoY + junimoSize + 48f;

            // Title line.
            int maxWidth = System.Math.Min(900, (int)(w * 0.6f));
            string title = Game1.parseText(TitleLine, Game1.dialogueFont, maxWidth);
            Vector2 titleSize = Game1.dialogueFont.MeasureString(title);
            Utility.drawTextWithShadow(b, title, Game1.dialogueFont,
                new Vector2((w - titleSize.X) / 2f, textY), Color.White);

            // Loop-count line, below the title.
            float loopY = textY + titleSize.Y + 24f;
            string loop = Game1.parseText(_loopLine, Game1.dialogueFont, maxWidth);
            Vector2 loopSize = Game1.dialogueFont.MeasureString(loop);
            Utility.drawTextWithShadow(b, loop, Game1.dialogueFont,
                new Vector2((w - loopSize.X) / 2f, loopY), Color.White);

            // Continue hint, near the bottom.
            Vector2 hintSize = Game1.smallFont.MeasureString(ContinueHint);
            Utility.drawTextWithShadow(b, ContinueHint, Game1.smallFont,
                new Vector2((w - hintSize.X) / 2f, h - 96), Color.White * 0.7f);
        }
    }
}
```

- [ ] **Step 2: Compile-only build**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded (menu is not referenced yet — fine).

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/UI/VictoryMenu.cs
git commit -m "feat(ui): add VictoryMenu basic win screen (3 Junimos, loop count)"
```

---

## Task 3: Route the win through the cutscene plumbing in `RunController`

**Files:**
- Modify: `src/TheLongestYear/Loop/RunController.cs`

- [ ] **Step 1: Expose the loop number for the driver**

After the `private RunState Run => _store.Run;` line (~line 87), add:

```csharp
        /// <summary>The current attempt count (loop the player is on / won), surfaced so the
        /// <see cref="TheLongestYear.Integration.Day28CutsceneDriver"/> can pass it into the
        /// <see cref="TheLongestYear.UI.VictoryMenu"/> loop-count line.</summary>
        public int CurrentRunNumber => Run.RunNumber;
```

- [ ] **Step 2: Queue the Win cutscene instead of `_pendingWinChoice`**

In `OnDayEnding`, the `case RunAction.Win:` block currently sets `_pendingWinChoice = true;`.
Replace the body of that `if (!_store.State.VictoryAcknowledged)` block so it reads:

```csharp
                    if (!_store.State.VictoryAcknowledged)
                    {
                        AwardInterimJp("run WON — loop broken");
                        // Queue the win screen for the morning. Routes through the same
                        // Day28CutsceneDriver/OnCutsceneEnded path as Fail/Continue (the driver
                        // opens VictoryMenu for the Win branch); OnCutsceneEnded then opens the
                        // JP shrine and the keep-playing choice. Replaces the old _pendingWinChoice.
                        _pendingCutscene = Day28Branch.Win;
                    }
```

- [ ] **Step 3: Add the Win case to `OnCutsceneEnded`**

In `OnCutsceneEnded`'s `switch (branch)`, add a `case Day28Branch.Win:` BEFORE `case Day28Branch.None:`:

```csharp
                case Day28Branch.Win:
                    // After the win screen closes: open the JP-spend shrine (the player has a fresh
                    // win-JP bonus), then ask "start a new loop" vs "keep playing". Same order as the
                    // old _pendingWinChoice path, with the win screen now in front of it.
                    TryOpenShrineThenContinue(ShowKeepPlayingChoice);
                    break;
```

- [ ] **Step 4: Retire the `_pendingWinChoice` field**

Delete the field + its doc comment (~lines 41-45):

```csharp
        /// <summary>Set in OnDayEnding's Win branch when the loop is completed (CC restored on
        /// Winter 28). Consumed on the next OnDayStarted: opens the JP-spend shrine, then asks
        /// the player to choose "Start a new loop" or "Keep playing this run". Suppressed when
        /// <c>MetaState.VictoryAcknowledged</c> is already set (player previously chose Keep).</summary>
        private bool _pendingWinChoice;
```

- [ ] **Step 5: Retire the `_pendingWinChoice` consumption block in `OnDayStarted`**

Delete this block from `OnDayStarted` (the `_pendingCutscene != None` early-return above it now
covers the Win branch too):

```csharp
            if (_pendingWinChoice)
            {
                _pendingWinChoice = false;
                // 2026-05-29 spec: on the morning after winning the loop, pop the JP-spend
                // shrine first (the player has a fresh JP bonus from the win), then ask
                // them whether to start a new loop or keep playing this run.
                TryOpenShrineThenContinue(ShowKeepPlayingChoice);
                return;
            }
```

- [ ] **Step 6: Add `DebugForceWin`**

After `DebugForceContinueCutscene()` (~line 324), add:

```csharp
        /// <summary>Debug: queue the WIN screen so a playtest can watch the win → shrine →
        /// keep-playing flow without grinding to a real Winter-28 win. Sets the pending branch;
        /// the Day28CutsceneDriver opens VictoryMenu this tick and OnCutsceneEnded opens the JP
        /// shrine then the keep-playing choice. Bypasses the VictoryAcknowledged "first win only"
        /// gate (that lives in OnDayEnding), so it is re-runnable from any loaded save.</summary>
        public void DebugForceWin()
        {
            _monitor.Log("tly_win: queuing the WIN screen (Junimos → shrine → keep-playing choice).", LogLevel.Info);
            _pendingCutscene = Day28Branch.Win;
        }
```

- [ ] **Step 7: Build to verify**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded. (If it complains `_pendingWinChoice` is undefined anywhere else, grep — there should be no other references.)

- [ ] **Step 8: Verify no stray `_pendingWinChoice` references remain**

Run: `git -C . grep -n "_pendingWinChoice" -- src/`
Expected: no output (all references removed).

- [ ] **Step 9: Commit**

```bash
git add src/TheLongestYear/Loop/RunController.cs
git commit -m "feat(loop): route win through Day28Branch.Win + add DebugForceWin, retire _pendingWinChoice"
```

---

## Task 4: Open `VictoryMenu` for the Win branch in the driver

**Files:**
- Modify: `src/TheLongestYear/Integration/Day28CutsceneDriver.cs`

- [ ] **Step 1: Branch the menu construction**

In `OnUpdateTicked`, replace these two lines:

```csharp
            Day28Branch branch = rc.PendingCutscene;
            _monitor.Log($"Day-28 cutscene: opening the {branch} Junimo scene.", LogLevel.Info);
            Game1.activeClickableMenu = new Day28CutsceneMenu(branch, () => _runController?.Invoke()?.OnCutsceneEnded());
            _opened = true;
```

with:

```csharp
            Day28Branch branch = rc.PendingCutscene;
            _monitor.Log($"Day-28 cutscene: opening the {branch} Junimo scene.", LogLevel.Info);
            Action onComplete = () => _runController?.Invoke()?.OnCutsceneEnded();
            Game1.activeClickableMenu = branch == Day28Branch.Win
                ? new VictoryMenu(rc.CurrentRunNumber, onComplete)
                : new Day28CutsceneMenu(branch, onComplete);
            _opened = true;
```

- [ ] **Step 2: Ensure `System` is imported**

The file already has `using System;` (for `Func<>`). `Action` lives in `System` too — no new using needed. Confirm the top of the file still has `using System;`.

- [ ] **Step 3: Build to verify**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Integration/Day28CutsceneDriver.cs
git commit -m "feat(integration): open VictoryMenu for the Win branch"
```

---

## Task 5: Register `tly_win` in `ModEntry`

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs`

- [ ] **Step 1: Register the console command**

In the `ConsoleCommands.Add(...)` block, immediately AFTER the `tly_failreset` registration
(line 160), add:

```csharp
            helper.ConsoleCommands.Add("tly_win", "Open the basic win screen, then the JP shrine + keep-playing choice (debug — bypasses the first-win-only gate, re-runnable).", this.CmdForceWin);
```

- [ ] **Step 2: Add the `CmdForceWin` handler**

Immediately AFTER the `CmdFailReset` method (which ends ~line 670), add:

```csharp
        /// <summary>Debug: open the basic win screen → JP shrine → keep-playing choice, the real
        /// win-path flow. See <see cref="RunController.DebugForceWin"/>.</summary>
        private void CmdForceWin(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            _runController?.DebugForceWin();
        }
```

- [ ] **Step 3: Add the file-bridge case**

In `ExecuteDebugLine`'s `switch`, immediately AFTER the `case "tly_failreset":` block
(ends ~line 944), add:

```csharp
                case "tly_win":
                    if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); break; }
                    _runController?.DebugForceWin(); break;
```

- [ ] **Step 4: Build to verify**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs
git commit -m "feat: register tly_win debug trigger (console + file bridge)"
```

---

## Task 6: Bump the version to 0.9.0

**Files:**
- Modify: `src/TheLongestYear/manifest.json`

- [ ] **Step 1: Edit the version**

Change `"Version": "1.0.0-beta.1",` to `"Version": "0.9.0",`.

- [ ] **Step 2: Commit**

```bash
git add src/TheLongestYear/manifest.json
git commit -m "chore: set version to 0.9.0 (0.9 beta milestone)"
```

---

## Task 7: Full test suite + deploy + screenshot verification

**Files:** none (verification only)

- [ ] **Step 1: Run the unit tests**

Run: `dotnet test`
Expected: 427 passed (0 failed). The pre-existing CS8625 warning in `BonusDropResolverTests` is fine.

- [ ] **Step 2: Confirm SMAPI is not running, then full build (auto-deploys to Steam Mods)**

Run: `Get-Process StardewModdingAPI -ErrorAction SilentlyContinue` (if present, `Stop-Process`).
Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: Build succeeded; mod copied to the Steam Mods folder.

- [ ] **Step 3: Launch SMAPI**

Run: `Start-Process StardewModdingAPI.exe -WorkingDirectory "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley"`
Then load the test save (`None_440190177`).

- [ ] **Step 4: Confirm clean load + version in the log**

Read `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`.
Expected: a line `The Longest Year 0.9.0 by sonofskywalker3`; 0 failed Harmony patches.

- [ ] **Step 5: Trigger the win screen via the file bridge**

Append `tly_win` to `<Steam>\steamapps\common\Stardew Valley\Mods\TheLongestYear\tly_commands.txt`.
Wait ~1s (polled ~0.5s). The win screen should appear.

- [ ] **Step 6: Screenshot the win screen (PrintWindow, flag 2)**

P/Invoke `PrintWindow` with flag `2` on the window titled `Stardew Valley…SMAPI`. Save the PNG to
`test-output/win-screen-0.9.png`. Confirm: black bg, three differently-coloured Junimos, the title
line, and the loop-count line.

- [ ] **Step 7: Confirm the post-close flow**

Dismiss the win screen (the harness can send the action key / click). Confirm in the SMAPI log:
the JP shrine opens, then on its close the keep-playing question dialog appears (loop count in the
prompt). No crash, no error lines.

- [ ] **Step 8: Final commit if any verification artifacts/notes need saving**

```bash
git add -A
git commit -m "test: 0.9 win screen verified (load 0.9.0 clean, tly_win → screen → shrine → choice)"
```
(Only if there is something to commit — e.g. a handoff note. The screenshot in `test-output/` may be gitignored; check before adding.)

---

## Notes for the implementer

- **Never push.** Local commits only.
- If SMAPI is running, every redeploy force-kills the game — kill it yourself before `dotnet build` (the default build auto-deploys).
- The win screen is a MENU, so PrintWindow captures it even when occluded. (It does NOT capture vanilla event dialogue — another reason it must be a menu.)
- Android decompile is irrelevant here (PC-only mod), but reflect for any platform-differing member if one surfaces.
