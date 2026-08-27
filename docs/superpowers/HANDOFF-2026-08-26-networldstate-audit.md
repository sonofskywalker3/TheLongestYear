# HANDOFF: the netWorldState keep/wipe audit

**Written 2026-08-26 for a fresh agent. If Jeff says "run the audit", this is the task.**

Everything you need is in this file. You do not need to read the rest of TODO.md first, and you do
not need to ask Jeff what the audit is.

---

## The one-sentence version

`NetWorldState` is a finite vanilla class whose fields the loop reset's `loadForNewGame` path does
not rebuild. Every field that survives a rewind but should not is a leak. Enumerate the class ONCE,
rule every field keep-or-wipe against the reset philosophy, implement the wipes, and this whole
class of bug is closed instead of being caught one player report at a time.

## Why it is worth a session

These were all the same bug, found separately, months apart, each by a player rather than by us:

| Leak | Found by | Fixed in |
|---|---|---|
| Community Center bundle state survived the rewind | playtest | early beta |
| Museum donations survived | player report | v0.11.x |
| Lost books count survived | player report | v0.11.x |

Each cost a report, a repro, a fix and a release. The class is finite; the reactive approach is not.

This pairs with the v0.11.38 `StatResetRules` change, which flipped stats to **wipe by default** and
enumerated the exemptions instead of the leaks. Do the same thing here. That file is the model for
both the philosophy and the shape of the result.

## The reset philosophy (what "should" means)

A rewind puts the farmer back at Spring 1 of a fresh year with nothing but what the Junimo Shrine
upgrades explicitly grant. So:

- **Wipe by default.** If a field represents progress the player made during the run, it goes.
- **Keep only with a reason**, and the reason is one of: (a) it is meta-progression the mod owns
  (JP, upgrades, the stash, kept pets/buildings/horse), (b) it is a cross-loop memory the mod
  deliberately maintains (`seenEventsEver` and the eventsSeen re-seed, so watched cutscenes stay
  watched), or (c) wiping it breaks vanilla in a way worse than the leak - and if you claim (c),
  say exactly how.
- When in doubt, wipe: a too-aggressive wipe shows up immediately in a smoke, a leak hides for
  months and reaches players.

## How to do it

1. **Enumerate the class.** Read `StardewValley.Network/NetWorldState.cs` in the decompile at
   `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android\decompiled`. List every
   field. Do not sample: the whole point is completeness.
2. **For each field, rule keep or wipe** with a one-line reason, in a table. Note which fields the
   reset already handles - `WorldResetService` and `FarmerReset` wipe a fair amount today, and
   double-wiping is fine but the table should say what is already covered so the diff is small.
3. **Implement the wipes** in the existing reset path (`WorldResetService`), following the pattern
   already there. Do not invent a new mechanism.
4. **Unit-test what is testable in Core.** Most of this touches `Game1` statics, so expect the
   table plus a live smoke to be the real evidence - see the verification note below.
5. **Live-smoke it**: reset on the throwaway save and confirm nothing that should have been wiped
   comes back, and nothing that should persist (JP, upgrades, stash contents, kept pet) got eaten.

## Where things are

- Reset paths: `src/TheLongestYear/Loop/WorldResetService.cs` (the world), `FarmerReset.cs` (the
  farmer, incl. the `mailReceived.Clear()` / `eventsSeen` re-seed).
- The model to copy: `src/TheLongestYear.Core/StatResetRules.cs` - wipe-by-default with enumerated
  exemptions.
- Decompile: `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android\decompiled`.
  It is the ANDROID decompile; for anything where the PC build might differ, say so rather than
  assuming.
- Tests: `tests/TheLongestYear.Tests/` (xunit; 865 passing as of 0.14.2).

## Running the game (read this before you try to play it)

Two facts cost a whole session on 2026-08-26 before they were understood:

- **An unfocused Stardew is a PAUSED Stardew.** A `debug warp` queued over the SMAPI console does
  not execute until the game updates, and a screenshot returns the last drawn frame - so a sleeping
  game looks exactly like a command that failed.
- **`SetForegroundWindow` alone does not work and fails silently** under Windows' foreground lock,
  and XNA reads the keyboard per input queue, so an unfocused window sees no synthesised keys at
  all. Mouse clicks appeared to work only because a click focuses the window under the cursor.

`tools/game.ps1` handles both: it attaches the input queue to lift the lock, verifies with
`GetForegroundWindow`, and exits non-zero rather than reporting a result it did not achieve.

    pwsh -File tools/game.ps1 -Focus
    pwsh -File tools/game.ps1 -Click 74,1010        # CLIENT coords == screenshot pixels
    pwsh -File tools/game.ps1 -Key Escape
    pwsh -File tools/game.ps1 -Walk right -Ms 1500  # walking needs a HELD key, a tap does nothing
    pwsh -File tools/game.ps1 -Shot "test-output/x.png"

Screenshots are cropped to the client area, so coordinates read off an image are the coordinates to
click. Other tools: `tools/deploy.ps1` (archive log, close game, build, relaunch),
`tools/send-smapi-command.ps1` (focus-independent console injection), `tools/pull-logs.ps1`.

Console commands that matter here: `tly_loadsave <folder>` (load from the title screen - never use
the Load menu), `tly_reset` (raw reset), `tly_failreset` (the full day-28 fail chain),
`tly_meta` / `tly_runstate` (inspect state). The throwaway save is the `None_*` folder under
`%APPDATA%\StardewValley\Saves` - **the folder name rotates on every reset**, so list the directory
and take the newest rather than reusing a name from these notes.

## Verification standard

Jeff's standard, learned the hard way today: **do not report something as verified that you have
not seen.** If you cannot drive it, say what you could not verify and why, and let him play it -
he will, and it takes him thirty seconds. A table of rulings plus an honest "smoked these three,
could not reach these two" is worth more than a confident claim.

## Conventions you must follow

- One change per commit, with a real message. Bump the patch version in
  `src/TheLongestYear/manifest.json` per change on `master`.
- **Never push, release, or post anything without Jeff saying so explicitly.** Local commits are
  fine and expected.
- No em dashes in anything written for Jeff.
- README and the Nexus description must stay content-identical if you touch either.
- Full workspace rules: `.claude/CLAUDE.md` at the workspace root and `TheLongestYear/CLAUDE.md`.

## Definition of done

1. A complete table of every `NetWorldState` field with a keep/wipe ruling and a one-line reason,
   committed under `docs/superpowers/` next to this file.
2. The wipes implemented in the reset path, tests green.
3. A live smoke on the throwaway save: nothing wiped that should persist, nothing persisting that
   should have been wiped.
4. TODO.md updated: the audit entry closed, with anything deliberately left open written down as
   its own item rather than dropped.
