# Obtainable Board, Plan 5 of 5: diagnostics and sims Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** The year sim donates the gate share a quarter per week instead of all on day 15, runs gate-only and goal-completing players on the same seed, reports all 16 weeks, and `tly_genbundles` can roll a board under the Vanilla Standard and Vanilla Remixed bundle options so those boards can be judged under the new rules too.

**Architecture:** `tly_playseason` gains a `quarter <k>` argument that flips the first k quarters of every bundle's season share (bundle order and slot order fixed, so the four calls are cumulative and deterministic); `tly_reset` and `tly_genbundles` accept an explicit seed loop; `tools/sim-year.sh` calls the quarter donations and prints a 16-row askable table at the end; `tly_genbundles` takes an optional board mode.

**Tech Stack:** C#, bash (Git Bash), PowerShell bridge scripts. Depends on plans 1 to 4 being deployed.

**Spec:** `docs/superpowers/specs/2026-08-28-obtainable-board-design.md` section 9.

## Global Constraints

- No em dashes. Patch bump per commit, local commits only.
- Never task-stop a running sim (HEADLESS_DRIVING.md); one sim at a time; throwaway Rodger save only.
- Deploy with `pwsh -NoProfile -File tools/deploy.ps1 -Minimized`, then `git checkout -- test-output/log-archive`.

---

### Task 1: `tly_playseason quarter <k>`

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (`CmdPlaySeason`, near line 2047)

- [ ] **Step 1: Implement**: parse `quarter <k>` (1 to 4). For each bundle the gate demands this season, compute the season's share (demanded by this season minus already donated at the season start) and flip only the first `ceil(share * k / 4)` slots that are still open, in ingredient order; the vault is paid on `k == 4`. Log `tly_playseason: {season} quarter {k}, {flipped} slot(s) flipped, cumulative {total}`.
- [ ] **Step 2: Build the mod; deploy; on the Rodger save run `tly_playseason quarter 1` through `quarter 4` on Spring 1 and confirm the last call reports `gate WOULD PASS` with the same ledger count a plain `tly_playseason` gives on a fresh reset.
- [ ] **Step 3: Commit** (bump patch)

---

### Task 2: Same-seed resets and board modes

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (`tly_reset [seedLoop]`, `tly_genbundles [seedLoop] [custom|standard|remixed]`), `src/TheLongestYear/Loop/BundleOptionPatch.cs` (expose a way to generate under a chosen `Choice` without touching the save's option)

- [ ] **Step 1: Implement**: `tly_reset <n>` resets with the given seed loop (the same number `tly_genbundles` takes). `tly_genbundles <n> standard|remixed` generates the diagnostic board from vanilla's Standard or Remixed bundle data for that seed (the engine's classification, gates and audit run over it exactly as for a TLY Custom board; nothing written). Log the mode in the `generated for loop` line.
- [ ] **Step 2: Build, deploy, run `tly_genbundles 0 standard` and `tly_genbundles 0 remixed`; confirm the audit prints and determinism holds.
- [ ] **Step 3: Commit** (bump patch)

---

### Task 3: `tools/sim-year.sh` per-week donations, same seed, 16-week table

**Files:**
- Modify: `tools/sim-year.sh`, `docs/HEADLESS_DRIVING.md`

- [ ] **Step 1: Implement**: `sim-year.sh <mode> <label> [seedLoop]`; reset with the seed; in week k of every season call `tly_playseason quarter k` before the pick (goals mode also deposits goal slots after the pick as today); at the end print `=== <label>: askable by week` as 16 rows `Season week N: Fo/Fa/Fi/Mi/Mx/Sp/Ar/Ki` parsed from the log, then the gate audit and the judgement and unknown lists from the dump. Update HEADLESS_DRIVING's year-sims section (quarter donations, seed argument, the table).
- [ ] **Step 2: Run `bash tools/sim-year.sh minimal P 0` and `bash tools/sim-year.sh goals Q 0` (same seed), never overlapping; save the outputs to the session scratchpad.
- [ ] **Step 3: Commit** (bump patch)

---

### Task 4: Ship notes

**Files:**
- Modify: `STATUS.md` (top section), `CHANGELOG.md` (Unreleased)

- [ ] **Step 1**: STATUS gets a new top block for the obtainable-board build: spec, plans, version range, test count, the two same-seed sim tables (gates, askable by week), the Standard and Remixed genbundles audit summaries, and the judgement list. CHANGELOG Unreleased gets one Changed bullet per user-visible rule (two weeks per item and difficulty modes, stretch gates, hard item per bundle, full pools with no fixed lists, legendaries and mine fish and year-2 crops on the board, rewind clears legendary catches, absolute effort bands, no look-ahead, flat 5 ceilings, the Garden Pot keep, the two Boosts) and one Fixed bullet per corrected placement (trash, crop arithmetic, tapper, Jack-O-Lantern, Cactus Fruit, Ghostfish and the pin table, Bone Fragment, Dried Mushrooms).
- [ ] **Step 2: Commit** (bump patch)
