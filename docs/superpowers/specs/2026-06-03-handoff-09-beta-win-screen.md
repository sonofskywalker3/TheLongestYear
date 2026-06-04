# Handoff — 2026-06-03 — 0.9 beta win screen (code DONE, in-game capture PENDING)

**Branch:** `feat/v1-plan-07-junimo-stash` — **all commits LOCAL, never pushed.** Do NOT push.

## Status: code complete, builds, deploys, loads clean, tests pass. One step left: the in-game win-screen screenshot.

### What's done (committed)
Tip commits (newest first):
```
5b64819 chore: set version to 0.9.0 (0.9 beta milestone)
e5607ad feat: register tly_win debug trigger (console + file bridge)
344501c feat(integration): open VictoryMenu for the Win branch
ebca3b8 feat(loop): route win through Day28Branch.Win + add DebugForceWin, retire _pendingWinChoice
c413540 feat(ui): add VictoryMenu basic win screen (3 Junimos, loop count)
ed5a158 feat(core): add Day28Branch.Win for the win-screen cutscene branch
ad93e56 docs: implementation plan for 0.9 beta win screen
0f4b881 docs: design spec for 0.9 beta win screen + tly_win + version bump
```

The 0.9 beta milestone (per `docs/superpowers/specs/2026-06-03-09-beta-win-screen-design.md` +
`docs/superpowers/plans/2026-06-03-09-beta-win-screen.md`):

1. **`VictoryMenu`** (`src/TheLongestYear/UI/VictoryMenu.cs`) — a basic self-drawn win screen
   mirroring `Day28CutsceneMenu` (NOT a vanilla event — same hard-won reason). Full black bg, a row
   of **three differently-tinted Junimos** (green/blue/red), a **single page**: title
   ("The Junimos sing! You restored the Community Center.") + a loop-count line from `Run.RunNumber`
   (loop 1 → "You restored it on your very first loop!", else "It took N loops."). Dismiss with
   A / click / Enter; B/ESC ignored; `readyToClose()=>false`; closes itself and runs `onComplete`.
2. **`Day28Branch.Win`** added (`src/TheLongestYear.Core/Day28/Day28Branch.cs`). The win now flows
   through the SAME `Day28CutsceneDriver` → `RunController.OnCutsceneEnded` plumbing as Fail/Continue.
   - `RunController.OnDayEnding` `RunAction.Win` (first win only, `!VictoryAcknowledged`) →
     `AwardInterimJp(...)` + `_pendingCutscene = Day28Branch.Win` (replaced the old `_pendingWinChoice`).
   - The old `_pendingWinChoice` field + its `OnDayStarted` block were **removed** (the
     `_pendingCutscene != None` early-return already suppresses the hub). `git grep _pendingWinChoice`
     → only comment references remain.
   - `OnCutsceneEnded` gained `case Day28Branch.Win:` → `TryOpenShrineThenContinue(ShowKeepPlayingChoice)`.
     **Order: win screen → JP shrine → keep-playing question dialog** (which still prints the loop count).
   - `RunController.CurrentRunNumber => Run.RunNumber` added so the driver can pass the loop count.
   - `Day28CutsceneDriver.OnUpdateTicked` now constructs `VictoryMenu(rc.CurrentRunNumber, onComplete)`
     when `branch == Win`, else `Day28CutsceneMenu` (all the settled-but-dark frame gating unchanged).
3. **`tly_win` debug trigger** — `RunController.DebugForceWin()` (sets `_pendingCutscene = Win`,
   **bypasses the `VictoryAcknowledged` first-win-only gate** → re-runnable). Registered in
   `ModEntry` as a console command (`CmdForceWin`, with the `Context.IsWorldReady` guard) AND a
   `case "tly_win":` in the `ExecuteDebugLine` file-bridge switch.
4. **Version → `0.9.0`** in `src/TheLongestYear/manifest.json` (deliberate down-label from
   1.0.0-beta.1 — user explicitly confirmed "0.9.0 as written").

### Verified this session
- `dotnet build` → **Build succeeded, 0 errors.** Deployed to
  `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\` (manifest there
  reads **0.9.0**).
- `dotnet test` → **427 passed, 0 failed** (one pre-existing CS8625 warning in BonusDropResolverTests
  is unrelated/fine).
- SMAPI launched; log shows **`The Longest Year 0.9.0 by sonofskywalker3`** and
  **`Harmony: 42 patch class(es) applied, 0 failed.`** → clean load. (Log:
  `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`.)
- PrintWindow screenshot tooling confirmed working this session (captured the title menu).

### ⚠️ Current runtime state
- **SMAPI IS RUNNING** (launched this session), sitting at the **title menu** with **no save loaded**.
  Check `Get-Process StardewModdingAPI` before doing anything — do NOT relaunch/redeploy unless you
  intend to (a redeploy force-kills it). The deployed DLL already == tip, so no redeploy is needed
  just to test.

### The ONE remaining step: capture the win screen in-game
I could not navigate the title→load menu myself — MonoGame/DesktopGL does not reliably accept
injected input and `SetForegroundWindow` is blocked (foreground lock). So a human must load the save.

**Procedure:**
1. Have the user load the test save **`None_440190177`** (or confirm a save is already loaded).
2. Fire the win screen via the file bridge (polled ~0.5s):
   ```powershell
   Add-Content "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\tly_commands.txt" "tly_win"
   ```
3. Screenshot it (reusable helper committed this session):
   ```powershell
   pwsh -NoProfile -File "<repo>\test-output\printwindow.ps1" "win-screen-0.9.png"
   ```
   Then `Read` the PNG. Expect: black bg, three differently-coloured Junimos, the title line, and the
   loop-count line. (The win screen is a MENU, so PrintWindow captures it.)
4. Confirm the post-close flow: have the user press A / click to dismiss, then read the SMAPI log for
   the shrine opening and the keep-playing question dialog (loop count in the prompt). No crash / no
   error lines.
5. Optionally save a short "verified" note + the screenshot; commit locally (do NOT push). Note:
   `test-output/` is NOT gitignored — `printwindow.ps1` is committed; decide whether to commit PNGs.

### Acceptance recap (from the task)
- [x] Version 0.9.0 in the SMAPI load log.
- [x] SMAPI loads clean (0 failed Harmony patches).
- [x] `tly_win` wired (console + file bridge); `DebugForceWin` bypasses the first-win gate.
- [x] Win screen shows the loop count (code; visual capture pending).
- [ ] **PrintWindow screenshot of the win screen** (needs a loaded save — the step above).
- [ ] Confirm closing the win screen → keep-playing choice (in-game, the step above).
- [x] Local commits only; nothing pushed.

### Gotchas
- The win screen is a self-drawn `IClickableMenu`. **Never** revert it (or the Day28 cutscene) to a
  vanilla `Event` — fade/globalFade/RenderedWorld/TryLoadPortraits all fail (see memory
  `day28-cutscene-is-a-menu-not-event`).
- Real win path: Winter 28 + CC restored → next morning (Spring 1) the driver opens `VictoryMenu`
  while the wake-fade is still dark → shrine → keep-playing. Debug `tly_win` opens it mid-day from
  any save.
- No HUD-hide needed for Win (unlike Fail) — the post-win date (Spring 1) is already correct.
