# Handoff — 2026-06-03 (evening) — day-28 Junimo cutscene DONE + verified; hub/reset polish

All work below is **committed locally on `feat/v1-plan-07-junimo-stash` (tip `5eedfd3`), never pushed**,
deployed to the Steam `Mods/TheLongestYear/`, loads clean (**42 Harmony patch classes, 0 failed**),
and tests pass (**427 unit tests, 0 warnings** aside from one pre-existing `CS8625` in
`BonusDropResolverTests` that predates this session). Working tree clean; deployed build == tip.

## SMAPI may be running — do NOT relaunch on startup
Check first: `Get-Process StardewModdingAPI`. If it's up, the user is mid-session — leave it.
**Every redeploy force-kills it**, which is also why the game window resets size each launch (see
"Open items" #2).

## What shipped this session (the day-28 bedtime Junimo cutscene + fallout)

The day-28 cutscene was THE remaining connected-subsystem piece. It's built, deployed, and
**verified in-game on a real day-28 sleep (Reset #9)** — not just the debug path.

1. **The cutscene is a self-drawn `IClickableMenu` (`src/TheLongestYear/UI/Day28CutsceneMenu.cs`), NOT
   a vanilla Event.** Full black background + a green Junimo sprite + the parsed dialogue + meep SFX,
   advanced page-by-page, non-skippable (`readyToClose()=>false`). **Do not try to revert this to a
   vanilla event** — fade reveals the room, globalFade blinks back, a RenderedWorld overlay covers
   the event's own dialogue, and `speak` on the portrait-less Junimo spams `TryLoadPortraits` every
   frame. (User memory `day28-cutscene-is-a-menu-not-event` has the full why.)
2. **Driver** `src/TheLongestYear/Integration/Day28CutsceneDriver.cs`: on the morning after a day-28
   outcome it opens the menu **while the wake-fade is still dark** (`!Game1.newDay && !Game1.eventUp`,
   it does NOT wait for the fade to settle) — so it's black-to-black, no farmhouse flash. On the
   menu's completion it calls `RunController.OnCutsceneEnded`.
3. **Routing** (`RunController`): `OnDayEnding` sets `PendingCutscene` from the gate's `RunAction`
   (FailReset→`Fail`, AdvanceMonth→`Continue`; `Win` still uses the old `_pendingWinChoice` popup —
   WIN branch intentionally NOT a cutscene yet). `OnDayStarted` early-returns when a cutscene is
   pending. `OnCutsceneEnded`: FAIL → `TryOpenShrineThenContinue(ContinueAfterResetSpend)`; CONTINUE
   → `DoDayStartSeasonAndHub`.
4. **FAIL path** = cutscene → JP shop → on close `ContinueAfterResetSpend`: `PerformReset` →
   `BeginNewRun` → `_store.Save()` → **forced full `SaveGame.Save()`** (drains the enumerator) →
   `CleanupAbandonedSaveFolder()` → `DoDayStartSeasonAndHub`. This forced save is the natural day-28
   save point that closes the save-folder churn.
5. **Save-folder dedup fixed** (`WorldResetService`): the reset NO LONGER renames the on-disk folder
   (the old prefix diverged from `SaveGame.Save`'s canonical `<farmName>_<id>` name → two folders).
   It records the abandoned folder and **deletes it only after the forced save succeeds** (never a
   brick window). **Verified:** Reset #9 left exactly one `None_<id>` folder.
6. **Load-menu date label fixed:** the reset now sets `Game1.player.dayOfMonthForSaveGame/
   seasonForSaveGame/yearForSaveGame` — the load menu reads those, not `Game1.dayOfMonth`, so a
   Spring-1 save was showing "Day 5". **Verified:** new save reads `dayOfMonthForSaveGame=1`.
7. **Hub weather → shrine calendar** (`WeeklyHubMenu`): the "Pick a theme" screen now renders weather
   as the planning-shrine's calendar (a "Weather" header, day-number row, weather icons in faint
   cells) instead of plain text rows. Festival days (Spring 13 Egg Festival, 24 Flower Dance, etc.)
   show the festival icon. Card height grown + bonus-row bottom margin shared so the bonus icons
   clear both the header above and the frame below.
8. **Re-roll button gated** behind `GameplayConfig.EnableThemeReroll` (default **false**); the
   re-roll code is retained, just not shown/snapped to.
9. **Debug bridge triggers added** (console-free, write to
   `Mods/TheLongestYear/tly_commands.txt`): `tly_failreset` (real day-28 FAIL cutscene flow),
   `tly_day28continue` (CONTINUE cutscene), `tly_setday <n>` (jump the date, e.g. `tly_setday 28` to
   test a real sleep without grinding a month).

Spec: `docs/superpowers/specs/2026-06-03-day-28-junimo-cutscene-design.md`.
Plan: `docs/superpowers/plans/2026-06-03-day-28-junimo-cutscene.md`. (Both predate the menu pivot —
the menu rework happened during playtest iteration; the spec's "vanilla event" approach is
superseded.)

## Open items / what's next
1. **CONTINUE branch only log-/unit-verified, not watched in-game.** The "great job, next season"
   cutscene is built and the parser is unit-tested, but the user has only exercised FAIL in-game.
   Watch it via `tly_day28continue` (or naturally on a passing season-end). Low risk.
2. **Window resets size every launch (user pain point).** Stardew stores NO windowed width/height
   in `startup_preferences`, and the redeploy force-kills the game so it never saves on exit. The
   durable fix is Windowed-Borderless-Fullscreen via the in-game Options (saves instantly, survives
   force-kills) — user was offered this; they hadn't chosen yet. Don't auto-edit windowMode blindly
   (the Android decompile doesn't reveal the PC enum cleanly).
3. **WIN cutscene branch** (Winter 28 + full CC) — still the old popup; a Junimo "you saved the
   valley" branch was deferred (user chose "WIN later").
4. **Reset-dependent fixes from the PRIOR session** (recipe wipe→baseline, FarmHouse furniture
   restore, event/cutscene gating, Keep Horse) just ran on this real Reset #9 — NOT yet confirmed
   from the log this session. Quick win: scan `SMAPI-latest.txt` for those log lines.
5. **Optional polish:** hover tooltips on the hub's weather cells (parity with the shrine, which
   shows "Day N - Weather" on hover); a clearer festival icon.

## Operational notes (carry-over, still true)
- **Build:** SMAPI running → compile-only `dotnet build -p:EnableModDeploy=false -p:EnableModZip=false`.
  To deploy: kill SMAPI, `dotnet build src/TheLongestYear/TheLongestYear.csproj` (auto-copies to the
  Steam Mods folder), relaunch via PowerShell `Start-Process StardewModdingAPI.exe -WorkingDirectory
  "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley"` (gives it its own console).
- **Logs:** read `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` (no kill needed to read).
- **Self-verify UI by screenshot** via P/Invoke `PrintWindow` (flag 2) — find the window by title
  "Stardew Valley…SMAPI", works even when occluded; captures menus but NOT event dialogue. See user
  memory `printwindow-screenshot-sdv`.
- **Trigger debug commands** by writing a line to `Mods/TheLongestYear/tly_commands.txt` (polled
  ~0.5s, executed, deleted). Console injection did NOT work this session (detached launch).
- Only `TheLongestYear.Core` is unit-testable; glue (the menu, driver, reset) is log-/playtest-verified.
- Decompile at `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android` (Android — PC differs).
- **Never push / publish** without explicit approval. Local commits only.
- Test save: `None_440190177` (Spring 1, post-Reset-#9). The old `None2_440032185` is gone (consumed
  by resets); don't expect it.
