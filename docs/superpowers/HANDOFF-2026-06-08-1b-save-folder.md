# Handoff — #1b "day-28 fail advances to Summer" — RE-EVALUATE FROM SCRATCH (2026-06-08)

**Dev version: v0.9.34 on `master`, clean baseline. All speculative #1b fixes have been REVERTED.**
Game was running v0.9.34 at the title screen. Two `None` save folders exist on disk (see Evidence) —
**do NOT delete them; they are the bug frozen in place.**

> ⚠️ The previous agent (me) chased two WRONG theories for #1b and reverted both. Treat my conclusions
> as hypotheses to verify, not facts. The one finding I'm fairly confident in is the **Evidence** below;
> re-derive the rest yourself.

---

## The bug (#1b), reporter's words (verbatim — trust THIS, not paraphrases)

emmainthealps (Nexus, mod 47192), 08 Jun 2026:
> "after finishing the 28th of Spring, (and only needing to go back because I was unlucky with sea
> urchin spawns) … the menu to pick how to spend the Junimo Points popped up, then disappeared before
> I could look at or select anything and the game jumped to Summer 1 - all my chests and stuff still in
> place… I was so pumped to be going back to Spring 1 …"
> **Edit:** "I re-did the day and **didn't finish the Vault on the 28th** as I had the first time… this
> let me pick to spend my points and reset to Spring 1."

Decoded: he FAILED the Spring gate (missing sea urchin), so it should reset to Spring 1. Instead the
JP-spend shrine **opened then vanished** and the game **advanced to Summer 1** with progress intact.
**Trigger he identified himself: finishing the Vault on day 28.** He did NOT complete the Community
Center. He has a backup save AT the bugged 28th and offered to retry — getting a log from him is the
highest-fidelity path if local repro stays elusive.

## 🔑 Evidence found on disk (the bug, frozen) — START HERE

Two save folders, BOTH `RunNumber=21` in the TLY data (parse the inner save XML / SMAPI modData):
- `…\StardewValley\Saves\None_440622857\` — **spring 1, Y1** — the TLY reset result (correct).
- `…\StardewValley\Saves\None_440621909\` — **summer 1, Y1** — the **vanilla day-28 sleep rollover that
  survived cleanup** (the orphan; emmainthealps's "advanced to Summer" is THIS save being loaded).

(`%APPDATA%` = `C:\Users\Jeff\AppData\Roaming`. This save is REMIXED-bundles; vault indices 23–26.)

**My current hypothesis (VERIFY, don't trust):** when you sleep on Spring 28, vanilla's normal
save-on-sleep writes a **Summer-1** save first. TLY's reset then changes `uniqueIDForThisGame`, writes a
fresh **Spring-1** save under the NEW folder, and is supposed to delete the old (now Summer-1) folder via
`CleanupAbandonedSaveFolder`. That cleanup is **unreliable** — in my repro it did NOT log "deleted the
abandoned pre-reset save folder," and the Summer-1 folder remained. Whichever folder the game loads next
decides the outcome: I landed on Spring-1 (looked fine); emmainthealps's game loaded the leftover
Summer-1 → "advanced to Summer." **So #1b is most likely a reset SAVE-FOLDER RECONCILIATION bug, not a
cutscene / `eventUp` / gate-evaluation bug.**

### Things to re-evaluate (don't inherit my assumptions)
1. Is the Summer-1 orphan really the vanilla sleep-rollover, or written by something else? (Its folder
   mtime was ~2 min AFTER the reset — I never explained that. Could be a second save event. CHECK.)
2. How does `None_440621909` end up with `RunNumber=21` (the post-reset meta) if it's the *pre*-reset
   rollover? That contradicts "it's the abandoned Run-20 folder." Re-derive which folder is which and
   when each was written. The mtime + the RunNumber both deserve explanation before you commit to a fix.
3. Why did `CleanupAbandonedSaveFolder` not fire/log on the repro reset? Was `_abandonedSaveFolder` null
   (`Constants.CurrentSavePath` cached stale by SMAPI)? Did `ForceFullSave` run but cleanup get skipped?
4. Which folder does the game actually LOAD when two exist for the same farmer? (That's what flips the
   outcome between me and emmainthealps.) Stardew picks by… verify (newest? alphabetical? SaveGameInfo?).

### Key code (read these first)
- `WorldResetService.PerformReset` — `src/TheLongestYear/Loop/WorldResetService.cs:~84-122`: changes
  `Game1.uniqueIDForThisGame = Utility.NewUniqueIdForThisGame()`, captures
  `_abandonedSaveFolder = Constants.CurrentSavePath` (SMAPI caches this at SaveLoaded — see the big
  comment at line 96-102 about a PRIOR orphan bug from exactly this caching).
- `WorldResetService.CleanupAbandonedSaveFolder` — `:403-430`: deletes `_abandonedSaveFolder` unless its
  name ends with the CURRENT uniqueID. Called from `RunController.ForceFullSave` (`RunController.cs:382`)
  AFTER `SaveGame.Save()`. **`ForceFullSave` SKIPS the whole save (and thus cleanup) when
  `Game1.eventUp || Game1.currentMinigame != null` (`:407`)** — note this is still a plausible factor.
- `RunController.ContinueAfterResetSpend` (`:353`) → `PerformReset` → `ForceFullSave` → `DoDayStartSeasonAndHub`.
- The day-28 decision: `RunController.OnDayEnding` → `RunManager.EvaluateDayEnd` (`Core/RunManager.cs`).

## ❌ Ruled out / REVERTED this session (do NOT re-attempt without new evidence)
- **CC-completion ceremony `eventUp` race** → v0.9.31 suppressed `doRestoreAreaCutscene`; reverted
  v0.9.32. Premise was wrong (emmainthealps never completed the CC) AND suppression independently
  rejected (lost the star-over-the-fireplace flourish). Keep the vanilla cutscene.
- **Defer shrine+reset until `!eventUp`** → v0.9.33 (`_pendingShrineContinuation` / `TryDrainDeferredShrine`);
  reverted v0.9.34. In the local repro (fail gate + pay vault on day 28, `tly_setday 28`) the reset worked
  fine and **the defer NEVER fired** — no `eventUp` block. So that failure mode wasn't present.
- Local repro caveat: I set up a FAR-from-complete gate; emmainthealps was ~25/26 bundles done
  (near-complete). The completion level may matter, but a faithful near-complete repro is impractical to
  fake (26 Spring bundles, some quality-gated) and faking via `tly_donate`/`ccload` is untrustworthy
  (sets TLY's ledger but not vanilla CC/area state). The on-disk Evidence above is a better lead than
  re-running repros.

## ✅ Confirmed-fixed THIS session (separate from #1b — all on master, verified)
| Ver | Fix | Verified |
|-----|-----|----------|
| 0.9.25 | Double-pick theme on reset (persist `OfferPresentedWeek` before deferred reload) | playtest |
| 0.9.26 | Vault indices remix-aware (`VaultBundleMap` from live BundleData; this save = 23–26) | log diag |
| 0.9.27 | Vault-derivation diagnostic in `OnRunLoaded` | — |
| 0.9.28-29 | Season Goals: vault is now a real list row w/ coin icon (not a pinned banner) | user OK |
| 0.9.30 | Cancel in-flight Clint tool upgrade on reset (`toolBeingUpgraded`/`daysLeftForToolUpgrade`) | save proof |
| — | #3 "keep tool upgrades" = CONFIRMED WORKING on a faithful Clint upgrade (planner + shop + persists). NOT a bug. |

Also: Cart Catalog porch-crate cross-mod loop-pollution logged (fix lives in OUR mods — make CC
TLY-aware; memory `feedback_fix_our_mods_not_theirs`). Story branch `feat/tly1-story-cutscenes` got a
TODO for a varied "We are the Junimos" message pool (keep vanilla cutscene, vary the line per loop).

## Tooling / workflow (unchanged)
- **Console injector:** `pwsh -NoProfile -File tools/send-smapi-command.ps1 "tly_setday 28" "tly_runstate"`.
  Useful: `tly_setday <n>`, `tly_additem (O)<id> [n]` (quality-0 only — use Console Commands `player_add`
  for quality), `tly_addmoney`, `tly_failreset`, `tly_runstate`, `tly_donate`. Vanilla via Console Commands
  (loaded): `debug warp <Loc> <x> <y>` (CC = `CommunityCenter 32 16`, Blacksmith counter = `Blacksmith 3 15`,
  FarmHouse bed = `FarmHouse 9 11`), `world_settime <hhmm>`, `debug bundle <idx>` / `debug ccload <area>`.
- **Deploy cycle:** game LOCKS the DLL. Close game → `dotnet build src/TheLongestYear/TheLongestYear.csproj
  -c Release` (auto-deploys) → relaunch `"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.exe"`.
  Save-reload does NOT reload the DLL. Log at `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`
  (OVERWRITTEN on relaunch — archive first if you need the prior run's lines).
- **Rules:** I deploy + pull logs; the user playtests. NEVER use a debug shortcut that bypasses the
  bug-under-test or that a player can't do — reproduce faithfully or ASK (memory `feedback_no_test_workarounds`;
  the user enforced this twice today). Commit + bump version every change (one change per commit). Don't
  push/release without explicit OK.
- The two `None_*` saves are EVIDENCE — the user has NOT decided whether to delete the Summer-1 orphan.
  Leave them until the investigation is done (or confirm with the user).
