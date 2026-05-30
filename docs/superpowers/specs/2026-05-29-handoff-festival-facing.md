# Handoff — 2026-05-29 late evening (festival-facing bug)

Continuation handoff. Picks up from
[`2026-05-29-handoff-resume.md`](2026-05-29-handoff-resume.md). 10 more
commits since that doc, almost all about the festival-exit feature
plus a couple of Season Goals UX polish items. **Stop and read the
"Active bug" section below before doing anything else** — that's what
the user wants the next agent to start on.

## Branch state

- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: `71395fd` (Season Goals fixes — fireplace quest text + auto-complete + sort)
- 359 tests passing, 0 warnings
- Not pushed (no remote)
- **Deployed DLL: through `06117ca`** (festival exit through warp, not Farm) —
  SMAPI is running (PID 6432). The two newest commits (`71395fd` Season Goals
  fixes) are staged in `bin/` but NOT on the live DLL — they apply on next
  redeploy.

## Active bug — fix this first

**User report 2026-05-29 evening:** "the warp out works, but drops me
facing towards the town, which is backwards, I need to be facing away
because that's where I came from."

When the player walks through a map-edge warp during a festival, the
new `SkipExitFestivalPromptPatch` (in `FestivalTimeFlow.cs`) now exits
them through that warp instead of to the Farm — see commit `06117ca`.
That works. But the player lands at the destination tile facing the
**wrong way** — backward, toward the festival map they came from.

### Root cause

`setExitLocation(string, int, int)` only stores location + tile.
Vanilla's exit-warp machinery at `Game1.cs:7986` is:

```csharp
warpFarmer(locationRequest, (int)player.positionBeforeEvent.X, (int)player.positionBeforeEvent.Y, player.orientationBeforeEvent);
```

It reads `player.orientationBeforeEvent` for the facing direction. That
field gets set at event start by `Event.setUpCharacters` (Event.cs:5563)
to whatever the player's FacingDirection was when the festival kicked
off — typically facing UP/NORTH because vanilla festival intros put the
player in front of an NPC. So when we override the exit location to the
warp's target, the orientation field still holds the festival-entry
facing, which is the WRONG direction for an edge-warp continuation.

### Fix outline

In `SkipExitFestivalPromptPatch.Prefix` (FestivalTimeFlow.cs), capture
`who.FacingDirection` BEFORE the `who.Halt()` call — that's the
direction the player was walking when they hit the warp. After the
`setExitLocation` override, set:

```csharp
Game1.player.orientationBeforeEvent = walkingDirection;
```

That'll make vanilla's exit-warp use the same facing direction the
player was moving in. Walk south out of Town → land in Forest facing
south. Walk north out of Town → land at BusStop facing north.

The capture HAS to happen before Halt because Halt may zero out
movement state (though not necessarily facing — verify in vanilla).
Safer to capture first, no cost.

### Files

Patch lives in:
`src/TheLongestYear/Loop/FestivalTimeFlow.cs` in the
`SkipExitFestivalPromptPatch.Prefix` method (around line 122-145 in the
current tree).

### Test path

User would need to wait for a festival day on a non-Standard week (or
use `tly_setdate` debug if it exists). Egg Festival = Spring 13. The
user's current playthrough is on Spring week 2, so still close enough.

Verification: walk south through Town's south edge during the Egg
Festival → land in Forest at the north edge → face SOUTH (away from
Town), able to keep walking south into the Forest.

## Commits since `5075e8e` (prior handoff)

```
71395fd fix(goals): fireplace quest text + auto-complete on open; sort completed bundles to bottom
06117ca fix(festivals): exit through the warp the player walked into, not the Farm
0e62437 revert: delete FestivalExitPatch — duplicated and conflicted with FestivalTimeFlow
b49bf5b fix(events): festivals don't trap you on the host map
ed586cf fix(events): festival exit lands at player's current tile, not map entrance
df93ce1 feat(events): festival exit lands on host map; close fortune_rare_fish TODO
```

The festival-exit feature went through 4 iterations (`df93ce1` →
`ed586cf` → `b49bf5b` → `0e62437` revert → `06117ca`) before landing
on the right shape. The TL;DR of that arc:

- The TODO entry described "land on the host map" — actually less
  correct than what was already shipped in `FestivalTimeFlow.cs` (Plan
  06A). My first three patches duplicated and conflicted with that
  existing work.
- User clarified: they wanted festival exits to just go through the
  warp (Town south → Forest, north → BusStop) instead of the Farm.
- `0e62437` reverted my conflicting patches. `06117ca` then made a
  small targeted fix INSIDE the existing FestivalTimeFlow path: find
  the colliding warp before calling `forceEndFestival`, then override
  the "Farm" exit set by vanilla `endBehaviors` with the warp's
  TargetName/TargetX/TargetY.
- Time preservation (the FestivalTimeFlow.EndBehaviorsPatch) still
  works.
- Now there's just the orientation bug — see above.

## What's user-confirmed working on the deployed DLL

- **Color picker fix** (`74c221e`) — user confirmed "all good." Closed
  permanently per the [feedback_dont_re_offer_closed_tests] memory.
- **Festival exit through warp** (`06117ca`) — user confirmed "warp
  out works" — but with the facing direction bug.
- **Fireplace intro quest** — user confirmed it works (quest appears
  on load). But the quest TEXT and completion behaviour will improve
  with `71395fd` once redeployed.

## What's pending verification (still on the deployed DLL, no redeploy yet)

- Tree shake bonus (10% per shake → +1 seed)
- Monster bonus (10% per kill → +1 loot)
- Fish bonus (10% per catch → +1 fish at same quality)

## What's pending on the next deploy (bin/, post-`06117ca`)

- `71395fd` Season Goals fixes:
  - Fireplace intro quest text: "by" → "above the Community Center fireplace"
  - Quest auto-completes on first menu open (calls `Quest.questComplete()`)
  - Completed bundles sort to the bottom of the goals list

Plus once the orientation bug above is fixed, that commit too.

## What's NOT done (post-cleanup TODO)

Only one item left on the open list:

1. **Co-opted day-1 intro cutscene** (chunky, v1.1 narrative tier) —
   replaces vanilla event 191393 with a TLY intro. Lewis stages the
   Joja threat, Junimo pops up in the CC to explain the loop. Needs
   custom event scripting + `MetaState.HasSeenIntro` flag. See TODO.md
   for full spec.

## Memories saved this stretch (already in auto-memory)

- `feedback_dont_re_offer_closed_tests` — once confirmed, retire from
  test list permanently.
- `debris_item_null_for_objects` — vanilla Object debris stores id in
  `Debris.itemId.Value`, not `.item`. Clone via `createObjectDebris(id, ...)`.
- `jp_store_only_at_loop_boundaries` — JP shrine menu only opens at
  reset or win, never mid-run. No in-world tile, no NPC, no hotkey.

## Workflow reminders (unchanged)

- Local commits only — never `git push`.
- Game-running deploys fail — use `-p:EnableModDeploy=false
  -p:EnableModZip=false` if SMAPI is up.
- Standard farm only.
- Bash for bash, PowerShell for PS syntax.
- No `/sdcard/` paths (workspace rule).
- Co-Authored-By footer on every commit.
- Explicit `Type[]` in every `[HarmonyPatch]` for any method with
  overloads.

## How to pull the log without killing the game

```powershell
Copy-Item "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" `
  "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\SMAPI-latest.txt" -Force
```

## How to refresh the upgrade catalog HTML view

```powershell
& "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\scripts\view-upgrades.ps1"
```

Currently shows **161 upgrades, ~95k JP for the complete set**.
