# Handoff — 2026-06-01 (evening) — reset-playtest bugfix round done; design pass next

Picks up after a long live-playtest of the loop **reset**. All critical reset bugs found this
session are **fixed, deployed, and verified in-game**. What's left is a connected subsystem of
**design** work. The previous handoff (`2026-06-01-handoff-spec-a-keep-system-v2.md`) is superseded.

## Branch state
- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: **`f64997a`** — working tree clean, local only (**never push**).
- 403 tests passing, 0 warnings. The deployed DLL (Steam `Mods/TheLongestYear/`) == this tip.
- **SMAPI is RUNNING** — the user has been playtesting; do NOT relaunch on startup. To deploy a
  new build: compile-only first (`-p:EnableModDeploy=false -p:EnableModZip=false`), then kill
  (`Get-Process StardewModdingAPI | Stop-Process -Force`), build without those flags, relaunch
  `StardewModdingAPI.exe` from the Steam Stardew dir.

## Fixed + verified this session (all in commit f64997a)
1. **JP refund on reset** — `RunController.ContinueAfterResetSpend` never re-saved meta; a deferred
   `SaveLoaded` reverted JP + OwnedUpgrades to the pre-shrine on-disk state. Added `_store.Save()`.
   Verified: JP stayed spent, purchases persisted.
2. **Kept tools came back basic** — SDV 1.6 tool tier IS the `ItemId` (`CopperWateringCan`), not
   just `UpgradeLevel`. `FarmerReset.ApplyToolTiers` now grants the correct-tier item via
   `ItemRegistry.Create("(T)…")`. Verified: copper can is actually copper. (See user memory
   `sdv16-tool-tier-is-itemid`.)
3. **Farmhouse never reset** — `FarmerReset` only set `HouseUpgradeLevel` up; now resets to 0
   unless a house keep is owned. Verified: house came back as the cabin.
4. **Opening cutscene skip→reopen loop** — a skip bypassed the cc-seen end command → infinite
   re-fire. Intro is now **non-skippable** + a one-shot loop-breaker in `IntroSequenceDriver`.
   Verified: intro completed once and marked `HasSeenIntro`.
5. **Save bricked on mid-window kill** — `WorldResetService` now renames inner save files to match
   the renamed folder (a process kill after a reset but before the next sleep-save used to hide the
   save). The original brick this caused was recovered.
6. **Shrine UI** — green "Owned" leaf rows (`KeepShopFilter.OwnedLeavesInCategory`, unit-tested) +
   auto-scaled tab labels ("Obtainability" fits). New `tly_failreset` debug command reproduces the
   natural day-28 gate-miss reset on demand.

## Loose ends to settle with the user (NOT bugs in code)
- **200 JP owed:** during console-driven testing I ran `tly_buyupgrade keep_watering_can_1` (150) +
  `keep_mining_level_1` (50) = 200 JP. The user OWNS those 2 keeps now. Offered to refund
  (`tly_addjp 200`); not yet done. Apply to whichever save they keep.
- **Duplicate save** (from running TWO `tly_failreset`s in one session — SMAPI caches the save path,
  so the 2nd folder-rename no-op'd and the sleep wrote a new folder):
  - `None_440028750` — **Summer 1, Run 1** = the user's real pre-test progress (orphaned).
  - `None2_440030941` — **Spring 2, Run 3** = test-advanced (has intro-seen + the 2 test keeps).
  - BOTH backed up at `save-recovery-backup/dup-20260601-185019/` (gitignored).
  - Ask which to keep; consolidate to ONE folder whose **name == main-file name == content
    `uniqueIDForThisGame`** so it stops spawning more dupes. Do it only when no save is loaded
    (user at title). Continued play/resets will keep spawning dupes until design item #3 lands.

## Remaining work = ONE connected design subsystem (NOT yet started)
Full notes: **`docs/superpowers/specs/2026-06-01-batch-b-cutscene-and-event-system-notes.md`**.
The user asked to switch to a proper brainstorm → spec before more code. Items:
1. **Event/cutscene gating** — `FarmerReset` clears `eventsSeen` each loop, so vanilla early scenes
   replay (Demetrius cave, Willy). Rules: never-replay-seen; but a scene tied to an unlock plays
   until that unlock is active **this run** (furnace = per-run craftbook check, NOT "ever banked");
   Demetrius plays **every loop** but held to ~Spring 5; forced scenes are non-skippable.
2. **Day-28 Junimo bedtime cutscene** — the reset/continue flow the user specced (in-bed black
   screen, gate-closed → "we'll reset, here's a head-start" → JP shop → save → Spring 1; gate-open
   → "great job" → next season). This is the natural save point that also fixes #3.
3. **Save-folder churn** — the reset re-rolls `uniqueIDForThisGame` (for forage RNG) and renames the
   folder; this caused today's brick + the duplicate. Consider seeding forage off `CompletedResets`
   instead of changing the master id, or doing a full save right after reset.
4. **Recipe reset** — `FarmerReset` never clears `cooking/craftingRecipes`, so the cookbook/craftbook
   do nothing (recipes persist regardless). Decide a baseline (vanilla starting set + banked) and
   wipe→regrant on reset.
5. **Weather/Cart on the planning shrine** — surface Weather Sage forecast + Cart Whisperer stock on
   the read-only shrine board (`ShrinePreviewMenu`/`PlanningShrineService`), gated on owned tier.

Recommended order: brainstorm the **loop/reset flow** first (anchors 1–4), then fold in 5.

## Operational notes
- **Typing into the SMAPI console programmatically** (the user expects this; it works): the game
  console is interactive and `AppActivate`+`SendKeys` fails (Windows foreground-lock keeps focus on
  the user's window). What WORKS is injecting into the console input buffer via P/Invoke —
  `FreeConsole()` → `AttachConsole(<StardewModdingAPI pid>)` → `CreateFile("CONIN$")` →
  `WriteConsoleInput` with KEY_EVENT records per char + Enter. Reusable PowerShell snippet is in
  this session's transcript (the `ConIn`/`ConIn2`/`ConIn3` Add-Type blocks). Console commands need a
  save loaded (`Context.IsWorldReady`). Pull logs by copying `%APPDATA%\StardewValley\ErrorLogs\
  SMAPI-latest.txt` (no kill needed on PC).
- Decompiled Android source: `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android`
  (PC 1.6.15 differs in places — verify; the tool-tier ItemIds matched).
- Workflow: local commits only, Co-Authored-By footer; only `TheLongestYear.Core` is unit-testable,
  glue is log-verified; reserve the user's playtests for meaningful feedback.
