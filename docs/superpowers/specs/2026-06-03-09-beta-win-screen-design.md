# 0.9 Beta Milestone — Basic Win Screen + `tly_win` + Version Bump

**Date:** 2026-06-03
**Branch:** `feat/v1-plan-07-junimo-stash`
**Status:** Approved (design)

## Goal

Ship a "0.9 beta" milestone so the mod can go out for public beta feedback. Three pieces:

- **A.** A *basic* win screen (the real payoff cutscene is deferred to 1.0 — keep this minimal).
- **B.** A debug trigger (`tly_win`) to exercise the win path without grinding to a real win.
- **C.** Bump the version label to `0.9.0`.

## Critical lesson (do not relearn)

The win screen MUST be a self-drawn `IClickableMenu`, NOT a vanilla `Event`. Every event approach
failed for the day-28 cutscene (`fade` reveals the room, `globalFade` blinks, a `RenderedWorld`
overlay covers dialogue, a portrait-less Junimo spams `TryLoadPortraits`). We mirror the proven
`src/TheLongestYear/UI/Day28CutsceneMenu.cs`: full-black background + Junimo sprite(s) + parsed text,
advanced/dismissed by input.

## Task A — Win screen (`VictoryMenu`)

New file `src/TheLongestYear/UI/VictoryMenu.cs` — a self-drawn `IClickableMenu` mirroring
`Day28CutsceneMenu`'s structure:

- Full-screen black background (`Game1.fadeToBlackRect`), sized to `Game1.uiViewport`.
- Advanced/dismissed by **A / left-click / Enter / Space / action button**. Ignores **B / ESC /
  cancel** and `readyToClose()` returns `false` — it closes itself in `Finish()` and invokes an
  `Action onComplete` callback (same shape as `Day28CutsceneMenu`).
- Plays `junimoMeep1` on open.
- Loads `Characters\Junimo` (frame 0 of the 16×16 sheet) with the same try/catch null-guard.

**Visual (per user):** three Junimo sprites in a centered horizontal row, each a **different tint**
(classic green `(110,200,74)`, a blue, and a red/orange), scaled up like `Day28CutsceneMenu`'s
single Junimo. Null-guard: if the texture failed to load, skip the sprite row (text still shows).

**Content — SINGLE page (per user):** title + loop-count line together, then the continue hint.

- Title: `"The Junimos sing! You restored the Community Center."`
- Loop-count line from `Run.RunNumber`, grammar-cased:
  - first loop → `"You restored it on your very first loop!"`
  - else → `"It took {N} loops."`
- Continue hint: `"(press A or click to continue)"` (small font, dimmed), matching the existing menu.

Because there is only one page, the first dismiss input calls `Finish()` directly.

## Wiring — reuse the cutscene plumbing (no parallel mechanism)

1. **`Day28Branch` enum** (`src/TheLongestYear.Core/Day28/Day28Branch.cs`): add `Win`.
2. **`RunController.OnDayEnding`** `RunAction.Win` branch: on the first win (`!VictoryAcknowledged`)
   → `AwardInterimJp("run WON — loop broken")` + `_pendingCutscene = Day28Branch.Win`
   (replacing `_pendingWinChoice = true`). Subsequent wins stay silent (gate unchanged).
3. **Retire `_pendingWinChoice`**: remove the field and its `OnDayStarted` consumption block. The
   existing `_pendingCutscene != Day28Branch.None` early-return in `OnDayStarted` already suppresses
   the normal season-sync/hub flow until the scene resolves — same as Fail/Continue.
4. **`RunController.OnCutsceneEnded`**: add `case Day28Branch.Win:` →
   `TryOpenShrineThenContinue(ShowKeepPlayingChoice)`. **Order preserved:** win screen → shrine
   (spend the fresh win-JP) → keep-playing question dialog (which already prints `Run.RunNumber`).
5. **`RunController.CurrentRunNumber`**: add `public int CurrentRunNumber => Run.RunNumber;` so the
   driver can pass the loop count into `VictoryMenu`.
6. **`Day28CutsceneDriver.OnUpdateTicked`**: when `branch == Day28Branch.Win`, construct
   `new VictoryMenu(rc.CurrentRunNumber, () => _runController?.Invoke()?.OnCutsceneEnded())`;
   otherwise construct `Day28CutsceneMenu` as today. All the settled-but-dark frame gating is
   unchanged.

### Why this fits

The win already wanted a "screen before the keep-playing choice." Routing it through the existing
`Day28Branch`/`Day28CutsceneDriver`/`OnCutsceneEnded` machinery is exactly the "reuse rather than
invent a parallel mechanism" the project mandates — and it lets us delete the one-off
`_pendingWinChoice`/`OnDayStarted` path.

### Flow after change

- **Real win:** Winter 28 night, CC restored → `OnDayEnding` sets `_pendingCutscene = Win`. Next
  morning (Spring 1, Y+1) `OnDayStarted` early-returns; the driver opens `VictoryMenu` while the
  wake-fade is still dark → on close `OnCutsceneEnded(Win)` → shrine → keep-playing choice.
  - **Keep playing** → `ApplyKeepPlaying` sets `VictoryAcknowledged` + `DoDayStartSeasonAndHub`.
  - **New loop** → `ContinueAfterResetSpend` (reset to Spring 1 + forced save).
  - No HUD-hide needed (unlike Fail): the post-win date (Spring 1) is already correct.

## Task B — `tly_win` debug trigger

- **`RunController.DebugForceWin()`** mirroring `DebugForceContinueCutscene()`:
  `_pendingCutscene = Day28Branch.Win;` + an info log. Setting the branch directly **bypasses** the
  `VictoryAcknowledged` "first win only" gate (that gate lives only in `OnDayEnding`), so the command
  is re-runnable from any loaded save.
- **`ModEntry`**:
  - Register `helper.ConsoleCommands.Add("tly_win", "...", this.CmdForceWin);` near the other
    `tly_` commands.
  - Add `CmdForceWin(string command, string[] args)` mirroring `CmdFailReset` (the `Context.IsWorldReady`
    guard, then `_runController?.DebugForceWin();`).
  - Add `case "tly_win":` to the `ExecuteDebugLine` file-bridge switch → `_runController?.DebugForceWin();`.

## Task C — Version

`src/TheLongestYear/manifest.json` `Version` → `"0.9.0"` (exactly, per user confirmation — a
deliberate down-label from `1.0.0-beta.1`). Prints in the SMAPI load log.

## Components / boundaries

| Unit | Responsibility | Depends on |
|------|----------------|------------|
| `VictoryMenu` (UI) | Draw the black + 3-Junimo + text win screen, dismiss → `onComplete` | `Run.RunNumber` (via ctor int), `Characters\Junimo` |
| `Day28Branch.Win` (Core) | Enum value tagging the queued win cutscene | — |
| `RunController` (Loop) | Queue `Win` on real win; route close → shrine → choice; expose `CurrentRunNumber`; `DebugForceWin` | `Day28Branch`, launcher, store |
| `Day28CutsceneDriver` (Integration) | Open the right menu for the pending branch | `RunController`, `VictoryMenu`, `Day28CutsceneMenu` |
| `ModEntry` | Register `tly_win` (console + file bridge) | `RunController.DebugForceWin` |

## Testing / verification

- `dotnet test` — expect 427 pass; the pre-existing CS8625 warning in `BonusDropResolverTests` is
  fine. Only `TheLongestYear.Core` is unit-testable; `VictoryMenu`/driver/`ModEntry` glue is
  log- and screenshot-verified.
- Build compile-only if SMAPI is running:
  `dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false -p:EnableModZip=false`.
- Deploy → `tly_win` via `Mods\TheLongestYear\tly_commands.txt` → PrintWindow (flag 2) screenshot of
  the win screen → confirm shrine → keep-playing choice follows; no crash.
- SMAPI load log shows version `0.9.0` and 0 failed Harmony patches.

## Acceptance

- `tly_win` opens the basic win screen on the test save; closing it leads to the keep-playing choice;
  no crash; SMAPI loads clean (0 failed Harmony patches).
- Win screen shows the loop count.
- SMAPI load log shows version `0.9.0`.
- A PrintWindow screenshot of the win screen.
- **Local commits only — do NOT push.**

## Out of scope (deferred to 1.0)

- The real, elaborate payoff cutscene (animation, music, multi-beat dialogue).
- Any new reward/unlock on win beyond the existing JP award + shrine.
