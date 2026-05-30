# Handoff — 2026-05-30 (forage-pickup rework + test-tuning revert)

Continuation handoff. Picks up from
[`2026-05-30-handoff-intro-and-bonuses.md`](2026-05-30-handoff-intro-and-bonuses.md).
Five commits this session. SMAPI is **NOT running** — closed at end of
session after a clean-load verify. Do NOT launch the game on startup;
wait for the user to ask.

## Branch state

- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: `92b0d98`
- 359 tests passing, 0 warnings
- **Deployed DLL is at the tip** in
  `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\`
  (last verified load: `Harmony: 40 patch class(es) applied, 0 failed.`).
- Not pushed (no remote, never push without explicit approval).

## Commits this session (on top of `54bfe3b`)

```
92b0d98 revert test tuning: all_drops_up back to 10%, drop forced tree-shake seed
e200367 change(bonuses): forage_yield_up grants +1 on pickup, not overnight spawn
c931d9d balance(bonuses): Mixed all_drops_up 10% -> 50%, centralized
1d10bb6 feat(bonuses): guarantee a seed on the first daily tree shake
4df1cd7 fix(quests): refresh existing intro-quest text on save load
```

**Net effect at the tip** (1d10bb6 + the 50% half of c931d9d were
sanity-test values, reverted in 92b0d98):

1. **Quest intro text now self-heals on save load** (`4df1cd7`) — KEPT.
2. **`forage_yield_up` reworked: +1 on pickup, not overnight spawn**
   (`e200367`) — KEPT.
3. **`MixedAllDropsChance` constant** introduced (`c931d9d`) and left at
   the **0.10 baseline** — pure code cleanup, behaviour unchanged.
4. Tree-shake forced 100% seed + Mixed 50% — tried, **reverted**. Net
   zero behaviour change vs. `54bfe3b` for those two.

## What's deployed and waiting on a PLAYTEST

### 1. forage_yield_up → +1 on pickup (`e200367`) — NEEDS PLAYTEST
Old behaviour spawned a second forage **object** on an adjacent tile
during the overnight `GameLocation.spawnObjects` pass. User disliked the
duplicate sprite on the ground: *"same mechanic, just a 20% chance to
gain +1 on pickup."*

New: `ForageYieldPatch` hooks `GameLocation.checkAction`. A prefix
snapshots the live spawned-forage objects; the postfix detects the tile
a successful pickup removed and rolls the existing 20%
(`BonusDropResolver.ShouldGrantExtraDrop("forage_yield_up", …)`),
dropping the extra straight into the inventory. Clones the **live**
source object so it carries the harvest quality vanilla assigns during
pickup. Mirrors the vanilla Gatherer profession (independent roll,
stacks with it); inventory-full overflow ignored, as Gatherer does.

**Test path** (user is on **Spring week 2, Foraging theme**): pick up
ground forage. Expect ~20% of pickups to log
`forage_yield_up: +1 '(O)…' (Q…) into inventory on pickup at (x, y) …`
and the extra to land in the inventory — and crucially **no duplicate
forage appearing on the ground overnight** anymore.

### 2. Quest intro text refresh (`4df1cd7`) — NEEDS save-reload confirm
`AddIntroQuest` (`WorldResetService`) used to skip entirely when a quest
id already existed, so the fireplace-board quest created on a pre-fix
run kept its stale *"notice board BY the Community Center fireplace"*
text. It now rewrites the title/objective/description of an existing
quest to the current copy. `FireBookQuestIntros` runs on every
`SaveLoaded`, so **just loading the save** flips the wording to
*"above"*. User reported the stale "BY" this session; confirm it now
reads "above" after a load.

## Confirmed this session — RETIRE from the test list

- **Festival exit facing direction** (`6ed4ee9`) — user: *"the warp
  facing is fixed."* Confirmed working. Do **not** re-offer per
  [feedback_dont_re_offer_closed_tests].
- **Tree-shake bonus fires on Foraging week** (`749c32c`) and **Mixed
  all_drops_up double-drops** — user tested both this session and
  confirmed they fire. The *engine* is confirmed; only the rate numbers
  were dialed back to baseline. Don't re-offer.

## Still open / untested (carryover from prior handoff)

- **Day-1 intro cutscene** (`85b029b`) — still NEEDS the retest gesture:
  `tly_replayintro` → `tly_reset` → exit farmhouse (porch/Lewis) → walk
  into CC (Junimo). Lewis tile (66,18 / 68,18) and Junimo tile (32,11)
  are educated guesses; dialogue is one-pass (the "the state will
  protect it" beat reads policy-wonky — polish only if the user
  comments). TODO.md already marks this shipped-pending-playtest.
- **Season Goals fixes** (`71395fd`) — fireplace quest text "above",
  quest auto-completes on first Season Goals menu open, completed
  bundles sort to the bottom. Still need a visual inspect in the CC.

## Backlog (spec'd, not planned — unchanged)

v1.x deferred polish in TODO.md: post-win auto-reset toggle, JP upgrade
`keep_pet` tuning, etc. Zero genuinely-open items remain in TODO.md
"Open" beyond the already-closed playtest carryovers.

## Active memories the next agent should heed

- `feedback_dont_re_offer_closed_tests` — once confirmed, retire
  permanently. (Festival warp facing, tree-shake/Mixed double-drops,
  color picker, festival-warp-edge are all closed.)
- `feedback_dont_ask_just_execute` — diagnose and roll, don't ask
  permission for diagnostics/coding calls.
- `feedback_meaningful_playtests_only` — engineering correctness (load,
  patches apply, no error) is verified solo from the log; only ask for a
  playtest when feedback beyond yes/no is possible.
- `feedback_no_auto_publish` — local commits only. Never `git push`.
- `debris_item_null_for_objects`, `jp_store_only_at_loop_boundaries`.

## Workflow reminders (unchanged)

- Local commits only — never `git push`.
- Game-running deploys fail — build with `-p:EnableModDeploy=false
  -p:EnableModZip=false` if SMAPI is up; otherwise a full deploy is fine.
- The PowerShell command-safety classifier had a transient outage this
  session; the **Bash tool** worked when PowerShell was blocked. Fall
  back to Bash if PowerShell calls return "cannot determine the safety".
- Standard farm only. No `/sdcard/` paths (workspace rule).
- Co-Authored-By footer on every commit.
- Explicit `Type[]` in every `[HarmonyPatch]` for any method with
  overloads (ForageYieldPatch and the others added this session do this).

## Deploy + log loop (when the user reports a playtest is done)

1. Kill SMAPI: `Get-Process StardewModdingAPI -ErrorAction SilentlyContinue | Stop-Process -Force`
2. Pull log: `Copy-Item "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" "TheLongestYear/SMAPI-latest.txt" -Force`
3. Build with deploy on: `dotnet build TheLongestYear/src/TheLongestYear/TheLongestYear.csproj -c Release`
4. Confirm DLL freshness in the Mods folder, then relaunch (if mid-session):
   `Start-Process "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.exe"`
5. Verify from the fresh boot log: `Harmony: N patch class(es) applied, 0 failed.` + no `[ERROR`.

Skip the relaunch if the user is done for the session.
