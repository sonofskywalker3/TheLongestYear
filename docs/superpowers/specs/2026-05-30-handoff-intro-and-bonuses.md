# Handoff — 2026-05-30 (day-1 intro + tree-shake Foraging fix)

Continuation handoff. Picks up from
[`2026-05-29-handoff-festival-facing.md`](2026-05-29-handoff-festival-facing.md).
Three commits since that doc, all deployed to the live mods folder.
SMAPI is **NOT running** — user closed the game and went to bed before
testing. Do NOT launch the game on startup; wait for them to do it.

## Branch state

- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: `749c32c` (tree-shake fires on Foraging week too)
- 359 tests passing, 0 warnings
- **Deployed DLL is at the tip** — every commit below is on disk in
  `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\`.
- Not pushed (no remote, never push without explicit approval)

## Commits since the prior handoff (`5620b1f`)

```
749c32c fix(bonuses): tree shake fires on Foraging week too
85b029b feat(narrative): day-1 Lewis+Junimo intro cutscene
6ed4ee9 fix(festivals): carry walking direction through the exit-warp
```

## Current playthrough snapshot

- User is on **Spring week 2**, **Foraging theme** (`forage_yield_up`,
  liability `mines_closed`). Saved in the log at line 147.
- Foraging spawn bonus IS firing normally — 30+ `forage_yield_up: +1`
  entries across the session from daily ground-forage spawns. The
  engine works; the gaps were coverage gaps, not wiring bugs.

## What's deployed and waiting on the user

### 1. Festival exit facing direction (`6ed4ee9`) — DEFERRED
Captures `who.FacingDirection` before `Halt()` in
`SkipExitFestivalPromptPatch.Prefix`, writes it to
`Game1.player.orientationBeforeEvent` alongside the `setExitLocation`
override. Walk south out of Town → land in Forest facing south.

**Test path:** needs an actual festival day. Egg Festival = Spring 13.
User is currently Spring week 2 (~Spring 8–14), so this might naturally
hit very soon. If not, it's deferred until they next hit a festival.

### 2. Day-1 intro cutscene (`85b029b`) — NEEDS RETEST GESTURE
Two new events injected via SMAPI asset edits:

- **`tly_intro_porch`** on Data/Events/Farm — Lewis spawns at (68, 18),
  welcomes the player, frames the Joja "revitalization initiative" +
  Winter 28 demolition deadline, mentions historical-landmark
  protection ("the state will protect it — but only if it's restored to
  full working condition before that deadline"), hands over the key,
  walks off south.
- **`tly_intro_cc`** on Data/Events/CommunityCenter — fires when player
  walks into the CC after the porch event. A Junimo
  (`addTemporaryActor Junimo 16 16 32 11 2 false character Junimo`)
  drops in from the north, gives the vanilla Bundle pitch + the
  TLY-specific loop framing (year too short, we'll rewind at Winter 28,
  progress dies but energy banks into next loop, no shame in starting
  again).

Both events are **prepended** to the dict via the asset edit so they
win first-match iteration against any vanilla day-1 farm event (e.g.
60367 Robin arrival).

Gating:
- Per-run mail flags `tly_intro_porch_seen` / `tly_intro_cc_seen` chain
  the events. Porch sets the first via `addMailReceived` at end-of-event;
  CC's precondition requires it. Both end with their own seen-flag.
- Cross-run `MetaState.HasSeenIntro` — set in `ModEntry.OnSaving`
  whenever `tly_intro_cc_seen` mail is present. Promotes to a
  `tly_intro_done` mail flag injected on every future `OnSaveLoaded`
  via `IntroEventInjector.ApplyMailFlagsForRun()`. That mail flag
  suppresses both event preconditions forever after.

**Test path** (user is mid-run, won't naturally hit Spring 1 for ~3 weeks):
```
tly_replayintro    # wipes HasSeenIntro + intro mail flags + eventsSeen entries
tly_reset          # rolls back to Spring 1, opens picker
                   # close picker → exit farmhouse → porch event fires
                   # walk to Town → into CC → Junimo event fires
```

User can also wait for natural year reset.

Files:
- `src/TheLongestYear/Integration/IntroEventInjector.cs` (new)
- `src/TheLongestYear.Core/MetaState.cs` (new `HasSeenIntro` field)
- `src/TheLongestYear/ModEntry.cs` (wire-up in `Entry` / `OnSaveLoaded`
  / `OnSaving` + new `tly_replayintro` debug command + dispatcher)

The dialogue text was written in one pass with no playtest. Probably
needs polish on the second look — particularly the "the state will
protect it" beat which currently reads a bit policy-wonky. Don't touch
unless the user comments.

### 3. Tree shake on Foraging week (`749c32c`) — TESTABLE NOW
User report 2026-05-29: "I shook a bunch of trees and never hit." Log
confirmed zero tree-shake bonuses across the whole session. Root cause
was a coverage gap, not a wiring bug.

`TerrainBonusPatches.TryDoubleNewDrops` now accepts an opt-in
`applyForageYieldUp: bool` and routes by `ActiveEffectsProvider.BonusId`:
- `all_drops_up` (Mixed): 10% chance, any drop. Existing behavior.
- `forage_yield_up` (Foraging): 20% chance, stone/wood excluded.
  Applied **only when caller opts in**.

Only `Tree.shake` opts in (seeds are forage-adjacent). `ResourceClump`
and `MonsterDrop` don't — wood/stone/coal/hardwood/slime aren't forage.

**Test:** user is on Foraging week. Shake trees → expect occasional
`forage_yield_up (terrain): +1 '(O)309' at ...` log lines (acorn id
shown; pine cone is 311, maple seed 310, mahogany seed 292). At 20%
per shake, 10 shakes should give ~2 hits.

### 4. Season Goals fixes (`71395fd`, deployed last session) — STILL UNTESTED
Quoting the prior handoff: fireplace quest text "above the Community
Center fireplace" (not "by"), quest auto-completes on first Season
Goals menu open, completed bundles sort to the bottom. None of these
have been seen by the user yet. They need to open the goals menu in
the CC and visually inspect.

## What's NOT in scope right now

- **All-drops-up (Mixed) tree/monster/fish bonus verification** —
  retired from active test list per user direction
  ("mark them done … I'll mention when I hit one naturally"). The
  wiring is correct (gated on `all_drops_up`); they'll surface
  whenever the player picks Mixed. Don't re-offer per
  [feedback_dont_re_offer_closed_tests].
- **Color picker, festival warp** — confirmed working previously,
  retired permanently.

## What's NOT done (open TODO)

Looking at TODO.md the only remaining open item NOT addressed this
session is:

1. **Co-opted day-1 intro cutscene** — actually that just shipped this
   session as commits 85b029b, partially closing TODO.md §95. Update
   the TODO entry to "shipped pending playtest" if it still reads as
   open. (I didn't touch TODO.md this session — quick TODO refresh
   is a natural early-session action for the next agent.)

That leaves zero open items in TODO.md once that update lands. The
backlog is the v1.x deferred polish (post-win auto-reset toggle, JP
upgrade keep_pet, etc.) all spec'd-not-planned.

## Active memories the next agent should heed

- `feedback_dont_re_offer_closed_tests` — once confirmed, retire from
  test list permanently. Don't re-offer Mixed-week shake/monster/fish
  "just to sanity check," don't re-offer color picker, don't re-offer
  festival-warp-through-edge (the v3-iteration feature that landed in
  06117ca, confirmed last night).
- `feedback_dont_ask_just_execute` — diagnose and roll, don't ask
  permission. User isn't a coder; "want me to add a log line?" is
  noise.
- `feedback_meaningful_playtests_only` — engineering correctness
  (wiring fires, no crash, no warn) is solo from logs. Only ask for a
  playtest when feedback beyond yes/no is possible.
- `feedback_no_auto_publish` — local commits only. Never `git push`.
- `debris_item_null_for_objects` — vanilla Object debris stores id in
  `Debris.itemId.Value`, not `.item`. Clone via `createObjectDebris(id, ...)`.
- `jp_store_only_at_loop_boundaries` — JP shrine only at reset/win,
  never mid-run. No in-world tile, no NPC, no hotkey.

## Workflow reminders (unchanged)

- Local commits only — never `git push`.
- Game-running deploys fail — use `-p:EnableModDeploy=false -p:EnableModZip=false`
  if SMAPI is up. Currently SMAPI is DOWN — full deploy is fine.
- Standard farm only.
- No `/sdcard/` paths (workspace rule — Android-side; not relevant for
  PC TLY work but the rule still applies).
- Co-Authored-By footer on every commit.
- Explicit `Type[]` in every `[HarmonyPatch]` for any method with
  overloads.
- The intro events use string event ids (`tly_intro_porch`,
  `tly_intro_cc`). Stardew 1.6 supports these.

## How to pull the log without killing the game

```powershell
Copy-Item "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" `
  "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\SMAPI-latest.txt" -Force
```

## Canonical deploy + log loop (do this when user reports done playtesting)

1. Kill SMAPI: `Get-Process StardewModdingAPI -ErrorAction SilentlyContinue | Stop-Process -Force`
2. Pull log to `TheLongestYear/SMAPI-latest.txt`
3. Build with deploy on: `dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release`
4. Relaunch SMAPI (if user is mid-session): `Start-Process "C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\StardewModdingAPI.exe"`
5. Verify from log: `Select-String -Path TheLongestYear/SMAPI-latest.txt -Pattern "TheLongestYear|patch.*failed|ERROR"`

Skip step 4 if the user said "I'm going to bed" or similar.

## Notes for retesting the intro chain

The two events have a few areas worth watching in the playtest log:

- `[The Longest Year] IntroEventInjector: tly_intro_cc_seen detected —
  promoting to MetaState.HasSeenIntro.` should land on the OnSaving
  after the CC event completes.
- If the porch event doesn't fire when expected, check the log for
  precondition-fail lines on `tly_intro_porch` — vanilla logs
  "Cannot fire event" or similar at TRACE.
- If a vanilla day-1 event fires INSTEAD of ours, our prepend-to-front
  failed. Falling back to suppressing the vanilla id in
  `EventSuppressionPatch` is the escape hatch.
- Lewis position (66, 18 / 68, 18) is a guess for the Standard farm
  porch area. May need tuning during the playtest — adjust the
  `farmer 66 18 1 Lewis 68 18 3` line in
  `IntroEventInjector.PorchEventScript`.
- Junimo position (32, 11) inside CC ditto. CC entrance warp lands the
  player at (32, 23); event spawns at (32, 22) facing north and walks
  3 tiles up. Adjust if it feels off.
