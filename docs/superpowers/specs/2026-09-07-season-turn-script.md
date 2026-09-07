# Season Turn Beats: the shooting script

Status: for Jeff's editing, alongside `2026-09-07-year-one-ending-script.md`. The lines are in
`2026-09-06-year-one-ending-lines.md` (section "Season turns") and in i18n; this file is the
staging. The code that plays it is `SeasonTurnEventInjector.Build`; who says what is the table in
`SeasonTurn.Lines`.

Plays on the morning of Summer 1, Fall 1 and Winter 1 after a passed day-28 gate, in place of the
black "great job" card. Tiles are relative to the farm's door tile (the doorway the farm reports),
so every farm type stages the same. Facing: 0 up, 1 right, 2 down, 3 left. Beat is a pause in ms.
Every line waits for the player's click. From the second time a turn has been seen on the save,
the scene can be skipped.

## Cast per turn

| Turn | Mood | Junimos |
| --- | --- | --- |
| Spring to Summer | hopeful | A (green), B (orange) |
| Summer to Fall | uneasy | A, B, C (turquoise) |
| Fall to Winter | alarmed | A, B, C, D (gold) |

## Marks

| Who | Tile | Notes |
| --- | --- | --- |
| Farmer | door + (0, +1), facing down | the doorstep, one below the doorway |
| A | door + (0, +3) | on the path below the deck |
| B | door + (-2, +3) | |
| C | door + (+2, +3) | |
| D | door + (-4, +4) | out wide |

Camera on the farmer, clamped inside the map. On Standard the door is (64,15), so the farmer
stands at (64,16) and the Junimos on row 18 and 19.

## Sequence (every turn)

| Step | What happens |
| --- | --- |
| Open | Music `junimoStarSong` (none for the Winter turn). The screen is black from the first frame (the scene starts in the bedroom under the wake-up fade). Cut to the Farm, farmer on the doorstep, Junimos on their marks, then fade in over 1.4 s. Beat 500. |
| Arrive | Every Junimo hops, `junimoMeep1`. Beat 700. |
| Lines | Each line: the speaker hops, then the line in the half-height portrait box. Beat 250 after each. |
| Leave | Beat 400. Every Junimo hops, `junimoMeep1`. Beat 600. Fade to black over 1.2 s. End. |

After the scene the run controller opens the week-1 planning hub, as it did after the card.

## Mood beats

| Turn | Where | What |
| --- | --- | --- |
| Summer to Fall | before line 2 | `stopMusic` |
| Fall to Winter | before line 2 | held dim purple glow (`glow 60 0 90 true`) and `shadowDie` |
| Fall to Winter | before the last line | `stopGlowing` |

## Things to eyeball live

- The checkpoint "+JP" toast from the night before is still on screen for the first few seconds
  of the scene (it sits under the speech box).
- Farm types other than Standard: the marks are door-relative but the deck shape differs; Beach
  and Four Corners are the ones to check.
