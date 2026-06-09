# Handoff — #1b FIXED; next: review feedback for the bugfix release (2026-06-09)

**Dev version: v0.9.37 on `master`, pushed to `origin/master` (clean fast-forward, 54 commits).
NO release cut yet (no tag, no `gh release`, no Nexus upload).** Game may be running at the title
screen or on the test save (see "Current save state").

---

## ✅ What got done this session (all committed + pushed)

**#1b "day-28 fail advanced to Summer, JP shrine flashed away" — ROOT-CAUSED + FIXED + confirmed
on BOTH branches by faithful repro.** The earlier "eventUp / whole-CC-completion" theory was WRONG.

**Root cause:** completing the Vault on day 28 queues `ccVault` in `mailForTomorrow`; that night
vanilla plays the bus-repair **`WorldChangeEvent(7)`** — a **`Game1.farmEvent`, a SEPARATE flag from
`Game1.eventUp`** (plays with `newDay==false` AND `eventUp==false` but `farmEvent!=null`;
`Game1.cs:9340` clears newDay before `:9361` assigns farmEvent). TLY's day-28 driver + `MenuLauncher`
guarded only on `eventUp`, so the shrine/cutscene opened DURING the bus scene and the event's
end-of-play warp (`Game1.cs:4977-4989`) tore it down without firing `exitFunction` → reset dropped.
`pickFarmEvent` is **mail-gated, not day-gated** (`Utility.cs:4369-4416`), and **debug bundle/area
completions skip the reward mail** (`DebugCommands` `loadArea`+`markAreaAsComplete` never call
`doAreaCompleteReward`) — so a faithful repro REQUIRES a real Junimo-Note donation, not a console shortcut.

**Fix (3 parts, all confirmed):**
1. **FAIL loop** — `RunController.OnDayEnding` strips this-night's CC-restoration mail from
   `mailForTomorrow` (`SuppressResetDoomedRoomScenes` → `CcRestorationMail.PurgeFromMailForTomorrow`),
   so the rewind-doomed bus scene never plays. Log proof: `Fail loop: suppressed 1 reset-doomed CC
   restoration scene ([ccVault%&NL&%])`, `farmEvent=none` through the reset → clean Spring rewind.
2. **PASS loop (Continue/Win)** — `Day28CutsceneDriver` + `MenuLauncher.CanOpen` now also wait on
   `Game1.farmEvent != null`, so TLY's "next season" cutscene defers behind a legit overnight scene.
   Log proof: `Day-28 cutscene: deferring the Continue scene until the overnight FarmEvent
   (WorldChangeEvent) finishes` → opens 14s later with `farmEvent=none` → clean Summer advance.
3. **Reset** — `WorldResetService.PerformReset` purges CC-restoration mail from `mailForTomorrow`
   (it only cleared `mailReceived` before). Fixes the **carryover** the user caught mid-session:
   a stuck `ccVault` survived a reset and fired "bus fixed, 0 bundles done" on the fresh run.

Mail list + purge: `src/TheLongestYear/Loop/CcRestorationMail.cs`. Diagnostics added (farmEvent/eventUp/
newDay state at driver open/defer + `ContinueAfterResetSpend`; `PerformReset` exception capture).
Commits: `638a1cf` (tooling), `a866816` (fix pts 1-2), `e18c367` (fix pt 3), `e564b4c`/`b838113` (TODO).
Memory: `tly-1b-ccvault-worldchangeevent-races-reset.md` (+ MEMORY.md index line).

**Also: log-archiving tooling** — TLY had NONE (that's why the original #1b repro log was lost). Added
`tools/pull-logs.ps1` (archive live log → `test-output/log-archive/SMAPI-v<ver>-<ts>.txt`, 30-file prune)
and `tools/deploy.ps1` (archive → close → build → relaunch). **Use `tools/deploy.ps1` for every deploy.**

## 🧪 The faithful #1b repro recipe (reusable — don't re-derive)
On a clean TLY run at a real day 28 (`tly_setday 28` is fine — the bug is mail-gated, not day-gated):
1. Grant gold (`tly_addmoney`), grant any needed items via Console Commands `player_add (O)<id> <n>`.
2. **Pay all 4 Vault bundles for real at the CC Junimo Note** (queues `ccVault` → the bus WorldChangeEvent).
3. FAIL test: leave the season gate unmet → sleep → suppression fires, clean Spring rewind.
4. PASS test: also donate every Spring-gating item (see below) → sleep → bus scene plays, Continue cutscene defers.
   - **Spring gate = every Spring-only item + Spring-pinned items + Percentage Spring quotas.** This save's
     standard-layout set was exactly 24 ids: `16 18 20 22 24 80 86 88 131 136 143 145 174 178 188 190 192
     334 340 372 388 390 397 702`. The pins/quotas live in `GameplayConfig.DefaultItemSeasonPins` +
     `DefaultBundleQuotas`; the classifier is `BundleClassifier.cs`. Verify donations before sleeping via
     the log (`Donated ...` lines) so you don't burn a reset.

## 📂 Current save state (test artifact — not precious)
- Live save `None_440642758` is **Run 24, advanced to Summer 1** with the granted test items and a 36-slot
  backpack (`debug backpack 24`). Fine to keep, reset, or delete.
- Evidence backups preserved at `test-output/save-evidence-1b/` (the two original `None_*` #1b saves).
- The Summer orphan was removed from the live Saves at the user's request ("only need one copy").

---

## ▶️ NEXT: review feedback, then ship the bugfix release

**Start by reviewing OLD + NEW feedback to decide what else (if anything) belongs in this bugfix release,
THEN cut the release (needs the user's explicit "yes" — release ≠ push).**

1. **NEW feedback** — re-scrape since the last scrape (2026-06-08):
   - Nexus mod **47192** comments/bugs/posts, and Reddit (TLY threads). **Playwright MCP is BROKEN; use the
     Node/CLI scraper** — reuse `AndroidConsolizer/release-notes/scrape-tly-comments.mjs` (adapt modId=47192;
     Reddit via `old.reddit.com` HTML). Don't defer this — write the script and run it (memory has bitten on this).
2. **OLD feedback** — walk `TODO.md` "## Open" and reconcile against what's actually fixed now:
   - `🔴 INVESTIGATE — Vault indices wrong on REMIXED saves` (TODO:18): likely already addressed by the
     v0.9.26-29 remix-aware `VaultBundleMap` work — THIS save resolved 23-26 correctly. Verify + close or keep.
   - `⏳ "Keep tool upgrades" missing from JP screen` (TODO:83): prior handoff says #3 is CONFIRMED WORKING
     (not a bug). Confirm against the latest reports + close.
   - Anything else under "## Open" that's a genuine BUG (not a 1.0 feature — the cutscene/déjà-vu/trilogy
     items are explicitly NOT part of this bugfix release).
3. **Decide scope + version.** Per the workspace scheme: PATCH for more iteration, MINOR (`0.x.0`) for a fix
   *release*, MAJOR (`1.0.0`) reserved for the big milestone. This is shaping up as a **fix release** — ask
   the user what number they want (and whether to roll any new-found bugs in first).
4. **Cut the release ONLY on the user's explicit OK.** Pipeline (see memory `nexus-publishing.md` +
   `docs/RELEASE_TOOLING.md`): `gh release create vX.Y.Z "<zip>"` auto-uploads the zip to Nexus via
   `publish-nexus.yml`; description/version via `release-notes/nexus-update.mjs` (NOT present yet for TLY —
   check/create); changelog is a manual paste. Keep the GitHub README ≡ Nexus description (house style).

## Rules / tooling (unchanged)
- **I deploy + pull logs; the user playtests.** Deploy via `tools/deploy.ps1` (archives the log first!).
  Console injection: `tools/send-smapi-command.ps1 "<cmd>" ...`. SMAPI log at
  `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` (overwritten on relaunch — deploy.ps1 archives it).
- **Faithful repro only — never a debug shortcut that bypasses the bug, or state-faking** (memory
  `feedback_no_test_workarounds`). Granting items/gold/days is fine; faking the gate ledger or completing
  bundles via `debug ccload` is NOT (and skips the mail → won't reproduce mail-gated events).
- **Push anytime; RELEASE needs explicit approval** (memory `feedback_no_auto_publish`). Commit + bump
  version on every code change; one change per commit.
