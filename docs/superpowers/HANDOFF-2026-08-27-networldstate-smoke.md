# Handoff: smoke the netWorldState audit fixes in game

Copy this whole file as the prompt for a fresh agent.

---

You are working in `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear` (SMAPI mod
"The Longest Year", PC). Read `STATUS.md`, `TODO.md` and the workspace `.claude/CLAUDE.md` first.
Work on `master`. Do not push or release anything; Jeff says "yes, push" himself. Commit locally per
change with a patch bump of `src/TheLongestYear/manifest.json` if you change code (start from the
current version in the manifest). No em dashes in anything you write for Jeff or players.

## The job

The netWorldState keep/wipe audit shipped in code (0.14.8, released inside 0.16.0) with a full ruling
table (TODO.md, "one-time complete netWorldState keep/wipe audit"), but **nobody has ever watched
the five fixes work in the game.** Verify them live, fix anything that fails, and record the result.

The five things to check after a rewind:

1. The Help Wanted board (quest board outside Pierre's) is EMPTY on Spring 1. This is the one that
   was handing out gold and quests from the previous year, so check it first.
2. The Saloon has no Dish of the Day on Spring 1 (Gus's counter, the "dish of the day" slot). A
   real first day has none; the first dish arrives on day 2.
3. JP, owned upgrades, Junimo Stash contents, kept pet, horse and kept buildings all survived the
   reset. The audit added an `UpdateFromGame1()` call mid-reset that touches shared state.
4. Spring 1 weather matches the new run's schedule (the same call syncs weather). Compare the
   weather the game shows with what the Weekly Hub / Weather Sage says, or the log's schedule line.
5. The Traveling Cart's "year one completable" guarantee re-rolls per loop (audit item; check the
   log for the re-roll line on reset if nothing visible tells you).

## How to drive it (all from the repo, no manual steps)

- `pwsh -NoProfile -File tools/deploy.ps1` archives the log, closes the game, builds, deploys and
  relaunches SMAPI. **Ask Jeff before running it: it takes the desktop.** Use `-NoBuild` if you
  only need a relaunch.
- Console commands go in through `pwsh -NoProfile -File tools/send-smapi-command.ps1 "<cmd>" "<cmd>"`.
  Read results from `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`.
- Load the THROWAWAY save with `tly_loadsave <folder>`; the folder is `None_<id>` under
  `%APPDATA%\StardewValley\Saves` (a reset renames it; `tly_loadsave` with a stale name lists the
  current ones). Never use the Load menu and never touch `None_443632257` (Jeff's real save).
  Wait about 40 s after the load before sending `tly_*` commands.
- `tly_reset` performs the rewind; wait for `In-place reset: complete` in the log. `tly_meta` prints
  JP, upgrades and stash; `tly_dejavu status`, `tly_readbook`, `tly_difficulty` exist too.
- Vanilla debug commands work through the same console: `debug warp <Location> <x> <y>`,
  `debug time 1000`, `debug sleep` (ends the day), `debug where <npc>`.
- Screenshots and input: `tools/game.ps1 -Shot <png>`, `-RightClick x,y` (this is the ACTION
  button: talk / interact), `-Click x,y` (left button, uses the tool), `-Focus`. Keyboard walking
  through `-Walk` did not move the farmer in the last session; use warps and clicks. The farmer must
  face a target: `debug fd farmer 0` (up) 1 (right) 2 (down) 3 (left). Indoor maps do not centre the
  camera, so take a shot and read positions off it before clicking.
- Read `tools/game.ps1` and `STATUS.md` "Driving notes" for the gotchas that cost the last session
  time; the user memory `tly-game-driving-gotchas` has the same list.

Suggested sequence: deploy, load the throwaway save, `tly_meta` (note JP/upgrades/stash), run to
Spring 28 with `tly_setday 28` then `debug sleep`, or simply `tly_reset`; then warp to Town
(`debug warp Town 42 57` is beside the quest board) and right-click the board; warp to Saloon and
look at the counter; `tly_meta` again and compare; check the weather against the log.

## Reporting

Put a PASS/FAIL table in `STATUS.md` (top section) and mark the TODO.md entry "NEXT SESSION: smoke
the netWorldState audit fixes in game" as smoked, with what failed if anything. For a failure, use
the systematic-debugging skill: reproduce, find the cause in `WorldResetService` /
`NetWorldStateReset` (grep for the audit's ruling table), fix, unit test, re-smoke, commit.
Report to Jeff plainly: what passed, what failed, what is committed, that nothing is pushed.
