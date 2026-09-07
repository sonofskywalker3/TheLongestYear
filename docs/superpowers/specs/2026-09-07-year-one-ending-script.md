# Year One Ending: the shooting script

Status: for Jeff's editing. This is the staging of the ending as the game plays it, scene by scene:
where everyone stands, who moves, who speaks, and how each transition is done. The lines
themselves live in `2026-09-06-year-one-ending-lines.md`; this file is about what happens around
them. The code that plays it is `EndingEventInjector.Build`; when this file changes, that changes.

How to read the tables: tiles are map coordinates (column, row). Facing: 0 up, 1 right, 2 down,
3 left. "Beat" is a pause in milliseconds. Every spoken line waits for the player's click.

## Cast

| Who | Where they come from |
| --- | --- |
| Lewis | Always present: the porch, then the top step of the hall. |
| The crowd | Robin, Pierre, Caroline, Marnie, Gus, Emily, Evelyn, Penny, Jas, Vincent, Linus, minus anyone the game cannot find and minus the speaker. |
| The speaker | The villager whose memory cracks (picked from the save's friendship history). Absent on a save with no history: then scene 3 does not play and the Junimos' fourth line changes. Never a spouse. |
| Morris | Scene 4 only. |
| Junimos | Four on the hall roof in Town (green, orange, turquoise, gold), six on the hall floor (the same four plus purple and salmon). Each is drawn at real Junimo size in its colour and bobs and hops on its own. Every one is called "Junimo" on screen; the code tells them apart as Junimo0 to Junimo5. |

## Scene 1: the porch

Music: `junimoStarSong` from the first frame.

| Step | What happens |
| --- | --- |
| Open | Farmer on the porch at (66,18), facing down. Lewis is already there at (68,18), facing left. Camera on the farmer. |
| | Farmer turns right to face Lewis. Beat 800. |
| Lewis | `lewis-porch-1`. Beat 200. |
| Lewis | `lewis-porch-2`. Beat 400. |

Transition: a straight cut through black to Town, landing the farmer directly on the paving in
front of the hall (the mod's own warp, so the first Town frame is already centred on the hall).

## Scene 2: the hall steps

On arrival the Community Center exterior is switched to its restored art before anything draws.

| Mark | Who | Tile | Facing |
| --- | --- | --- | --- |
| Top step, in front of the doors | Lewis | (52,21) | down |
| Paving below him | Farmer | (52,24) | up |
| Front row | crowd 1, 2 | (49,23), (55,23) | up |
| Second row | crowd 3, 4 | (48,24), (56,24) | up |
| Third row | crowd 5 to 8 | (47,25), (50,25), (54,25), (57,25) | up |
| Fourth row | crowd 9 to 11 | (49,26), (52,26), (55,26) | up |
| Back | crowd 12, 13 (spare) | (51,27), (53,27) | up |
| Speaker's mark | the speaker | (50,24), two tiles west of the farmer | up |
| Roof ridge | Junimo0 to Junimo3 | (49,14), (51,14), (53,14), (55,14) | |

Camera centred on the steps at (52,22).

| Step | What happens |
| --- | --- |
| | Beat 600. Sound `reward`, a white flash. The four roof Junimos hop, `junimoMeep1`. Beat 800. |
| Lewis | `lewis-hall-1`. Beat 200. |
| Lewis | `lewis-hall-2`. Beat 200. |
| Lewis | `lewis-hall-3`. Beat 600. |

## Scene 3: the crack (only with a speaker)

| Step | What happens |
| --- | --- |
| | The speaker walks one tile east, to (51,24), and ends facing right, beside the farmer. The farmer turns left to face them. Beat 400. |
| Speaker | `crack.open`. Then a question-mark emote. Beat 900. |
| Speaker | The tier line for what the save remembers (`crack.tier1` to `tier4`, voice override if one exists). Beat 400. |
| Speaker | `crack.close`. Beat 600. Farmer turns back up to Lewis. |

## Scene 4: Morris

Music stops.

| Step | What happens |
| --- | --- |
| Enter | Morris appears off screen east at (70,24), walking speed 5, and walks west along the paving row to (57,24), just outside the crowd. The farmer turns right to face him. Beat 500. |
| Morris | `morris-1`. Beat 200. |
| Morris | `morris-2`. Beat 200. |
| Morris | `morris-3`. Beat 300. |
| The shadow | Morris's whole figure darkens and his eyes go red; the screen takes on a held dim red glow; sound `shadowDie`. Beat 400. |
| Morris | `morris-4`. Beat 700. |
| | Glow off, Morris back to normal. Beat 300. Two roof Junimos hop, `junimoMeep1`. |
| Exit | Morris walks back east to (70,24), off screen. Farmer turns back up. Beat 400. `doorClose`, beat 300, `thudStep`, beat 800. |

Transition: cut through black to inside the hall, farmer at (32,16), camera on (32,14).

## Scene 5: inside the hall

| Seat | Junimo | Tile | Colour |
| --- | --- | --- | --- |
| 1 | Junimo0 | (29,12) | green |
| 2 | Junimo1 | (31,13) | orange |
| 3 | Junimo2 | (33,12) | turquoise |
| 4 | Junimo3 | (35,13) | gold |
| 5 | Junimo4 | (37,12) | purple |
| 6 | Junimo5 | (32,14) | salmon |

The dialogue portrait is tinted to match whichever Junimo is speaking. The speaker hops just before
its line.

| Step | Who | What happens |
| --- | --- | --- |
| | all six | `junimoMeep1`, all six hop. Beat 800. |
| 1 | Junimo0 (green) | `junimo-1`. Beat 200. |
| 2 | Junimo1 (orange) | `junimo-2`. Beat 400. |
| 3 | Junimo2 (turquoise) | `junimo-3`. Beat 300. |
| 4 | Junimo3 (gold) | `junimo-4` if scene 3 played, otherwise `junimo-4-nocrack`. Beat 300. |
| 5 | Junimo0 (green) | `junimo-5`. Beat 600. |

Transition: cut through black to the Farm, farmer two tiles below the farmhouse door.

## Scene 6: home, then the shrine

The light drops to dusk on arrival. Camera on the farmhouse door, clamped inside the map.

| Step | What happens |
| --- | --- |
| Home | Beat 600. The farmer walks two tiles up to the door. Beat 200. `doorClose`; the farmer is inside (hidden). Beat 900. |
| Pan | The camera glides to grandpa's shrine over 6 seconds, eased at both ends, and never past the map edge (in the corner it settles as close to the shrine as the map allows). Beat 1200. |
| Grandpa | `grandpa`, as a plain message with no portrait; `@` is the farmer's name. |
| Candle | Beat 900. `fireball`, one candle lights on the shrine. It stays lit for the rest of the year. Beat 2200. |
| Out | Fade to black. The farmer is put back on the doorstep under the fade. End. |

After the event the run controller takes over: the shrine spend, then the loop-again / keep-playing
choice (`dialog.ending.*` in the lines file).

## Known gaps and things to eyeball live

- Roof row 14 and hall seats on rows 12 to 14 were chosen from the map bounds, not from a
  screenshot. If a Junimo sinks into the wall or hides behind furniture, move that tile.
- Morris's walk along row 24 east of column 61 has not been watched yet.
- Nothing on the Standard farm stands between the camera and the shrine, so no building is made
  transparent during the pan. If a farm type puts something in the way, that needs a per-building
  alpha, which is not written.
- A test save started with `tly_newgame` has no completed bundles and no friendship history: the
  hall interior shows the unrestored rooms and scene 3 never plays. A real win looks different in
  both respects.
