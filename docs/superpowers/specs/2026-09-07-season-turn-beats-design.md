# Season Turn Beats: design

Status: draft for Jeff's review, written while he was away (2026-09-07). Every choice marked
**assumption** is mine and is his to overturn. Lines are placeholders for him to rewrite.

Story spec: `2026-06-06-tly1-story-and-cutscenes-design.md` section 5.3. Toolkit: the Year One
Ending's event commands (`EndingEventCommands`), speech box and Junimo actors.

## What it is

Three short Junimo scenes, one at each season turn a player earns: the morning of Summer 1, Fall 1
and Winter 1, on a loop where the day-28 gate passed. They replace the black-screen "Great job,
keep it up" card the pass branch shows today. Each one acknowledges the season the player just
held, and each one is a little darker than the last, tracking the sabotage fronts the story spec
plans for Summer (blight), Fall (bundle reversion) and Winter (requirement tampering):

| Turn | Mood | Junimos present | What it plants |
| --- | --- | --- | --- |
| Spring to Summer | hopeful | 2 | The hall is stronger for the work. Nothing has struck. |
| Summer to Fall | uneasy | 3 | Something got into the soil this season. It is not weather. It noticed you. |
| Fall to Winter | alarmed | 4 | It has reached the hall itself. Winter is its season; it will change what the hall asks. |

The fail night (rewind) is not part of this spec. Its black-screen scene stays as it is until the
rewind spec replaces it.

## Where and when

**Assumption: the porch at dawn, the same staging as the ending's first scene.** The scene starts
on the wake frame of day 1 (where the pass card opens today), fades the farmhouse interior to black
before a frame draws, puts the farmer on the doorstep facing the lawn, and fades in. The Junimos
hop in from the grass below the porch, say their lines in the half-height speech box, hop away, and
the scene fades out. The planning hub for week 1 opens after it, exactly where it opens today. The
farmer starts the day on the doorstep instead of in bed.

Alternatives considered: the in-bed black screen with Junimo sprites drawn on it (closest to
today, but the ending has set the porch as "where the Junimos come to you"); the shrine or the
statue (wrong end of the farm, and the shrine now belongs to grandpa).

## Staging

Tiles are the ending's porch marks, offset by the farm's own door tile so every farm type works
(the ending already reads `Farm.GetMainFarmHouseEntry`). Door = the doorstep tile.

| Mark | Tile (relative to the door) | Facing |
| --- | --- | --- |
| Farmer | door | down |
| Junimo A (green, the voice) | door + (0, +2) | |
| Junimo B (orange) | door + (-2, +2) | |
| Junimo C (turquoise), Summer and Fall only | door + (+2, +2) | |
| Junimo D (gold), Fall only | door + (-3, +4) | |

Camera clamped on the farmer, same as the ending's porch shot. Music: `junimoStarSong` low for
Spring; the same track for Summer, then `stopMusic` at the uneasy line; no music for Fall, the
Junimos arrive in silence and a low `shadowDie` plays under the "found the hall" line with a held
dim purple glow (`glow 60 0 90 true`, released before the last line).

Each Junimo hops as it arrives (`jump`, `junimoMeep1`) and hops before its own line. They leave
by hopping twice and then a `warp` off screen under the fade.

## Lines (placeholders)

`event.turn.summer.*`, `event.turn.fall.*`, `event.turn.winter.*`. `@` is the farmer's name. All
go through the ending's `tlySay`, so `#$b#` pages work and `$h`-style codes are stripped.

**Spring to Summer**

| Key | Who | Text |
| --- | --- | --- |
| `summer-1` | A | You did it, @. Spring is done. |
| `summer-2` | B | Every gift makes us stronger. |
| `summer-3` | A | Keep going. We will be watching. |

**Summer to Fall**

| Key | Who | Text |
| --- | --- | --- |
| `fall-1` | A | Summer is done, @. You held on. |
| `fall-2` | B | Did you see your crops? Something is in the soil. |
| `fall-3` | C | It has noticed you. |
| `fall-4` | A | Guard what you grow. We will help where we can. |

**Fall to Winter**

| Key | Who | Text |
| --- | --- | --- |
| `winter-1` | A | Fall is over, @. The hall is still standing. |
| `winter-2` | B | It has found the hall. Some of your gifts have been taken. |
| `winter-3` | C | It will corrupt whatever it can. Our plans may be changed, but not thwarted. |
| `winter-4` | A | One final season. The darkness is strong. But because of you, we may yet grow stronger. |

Lines rewritten 2026-09-09 through the game-writing skill; `winter-3` and `winter-4` are Jeff's
own. The sabotage mechanics now exist (spec 2026-09-09-darkness-pushback), so the lines describe
things the player has seen.

## Repeat loops

**Assumption:** the scene plays every loop, and is skippable (vanilla `skippable`, the Escape
prompt) from the second time a given turn has been seen on the save. `MetaState` gains
`SeasonTurnsSeen` (a set of the three season names). The ending's rule that its own scene plays
once per save does not apply here: the turn is the loop's reward for a season held, and the
player who never fails Summer would otherwise never see the Fall beat again.

## How it fits the code

- `Day28Branch.Continue` in `RunController.OnCutsceneEnded` currently calls
  `DoDayStartSeasonAndHub()` after the black-screen card. New: the driver plays the event first;
  its completion runs the same call. The Fail branch is untouched.
- `Integration/SeasonTurnEventInjector` builds the script from the season and the door tile, the
  way `EndingEventInjector` does; `Integration/SeasonTurnDriver` starts it on a settled frame and
  watches for its end mail, the way `EndingEventDriver` does. The `tlyChangeLocation`,
  `tlyFadeIn`, `tlyFadeOut`, `tlyJunimo` and `tlySay` commands are reused as they are; the
  overlay's "event is over" check learns the new event id.
- `Core/SeasonTurn.cs` (pure): which turn a season start is (`Summer 1` = Spring turn), how many
  Junimos, which line keys, and whether the scene is skippable given `SeasonTurnsSeen`. Unit
  tested. `I18nGuardTests` cover the keys.
- Debug: `tly_seasonturn <summer|fall|winter>` replays a turn now with no continuation, over the
  file bridge too.
- Docs: a script sheet like the ending's, and these lines added to the lines review file.

## Testing

- Core tests for `SeasonTurn`.
- Live: `tly_newgame standard skipintro`, `tly_setday 28`, `debug sleep` on a passing gate for each
  season (or `tly_seasonturn` for the scene alone), stepped with `tly_eventstep`, screenshots of
  each turn, then the hub opens. All eight farm types get the Summer turn once for the door offset.

## Out of scope

The rewind scene, the sabotage mechanics and their shrine counters, the opening montage.
