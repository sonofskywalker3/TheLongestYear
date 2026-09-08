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
| The speaker | The villager whose memory cracks (picked from the save's friendship history). Absent on a save with no history: then scene 3 does not play and the Junimos' fourth line changes. Never a spouse. `tly_remember <Name> [tier]` seeds a test save. |
| Morris | Scene 4 only. |
| Junimos | Four on the ground in Town (green, orange, turquoise, gold), six on the hall floor (the same four plus purple and salmon). Real Junimo actors: their size, their colour, their idle bob and hops. Every one is called "Junimo" on screen. |

Dialogue: the porch uses the game's portrait box. Every Town and hall line uses the mod's own
half-height portrait box (portrait at half size, line beside it, no name label), so the scene
stays visible behind it.

Transitions: every location change fades to black with the world intact, swaps the map and the
cast under black, and fades in only once everyone is placed. Nothing pops in after a fade.

## Scene 1: the porch

Music: `junimoStarSong` from the first frame. Camera clamped inside the farm map.

| Step | What happens |
| --- | --- |
| Open | Farmer on the porch at (66,18), facing down. Lewis is already there at (68,18), facing left. |
| | Farmer turns right to face Lewis. Beat 800. |
| Lewis | `lewis-porch-1`. Beat 200. |
| Lewis | `lewis-porch-2`. Beat 400. |

## Scene 2: the hall steps

On arrival the Community Center exterior is switched to its restored art before anything draws.
Camera centred on (52,24): at 1080p that shows rows 16 to 33, and the speech box covers rows 27
and below.

| Mark | Who | Tile | Facing |
| --- | --- | --- | --- |
| Top step, in front of the doors | Lewis | (52,21) | down |
| Paving below him | Farmer | (52,24) | up |
| Row 23 | crowd 1 to 3 | (48,23), (54,23), (56,23) | up |
| Row 24 | crowd 4 to 7 | (47,24), (49,24), (55,24), (57,24) | up |
| Row 25 | crowd 8 to 12 | (46,25), (48,25), (50,25), (54,25), (56,25) | up |
| Row 26 | crowd 13 (spare) | (52,26) | up |
| Speaker's mark | the speaker | (50,24), two tiles west of the farmer | up |
| Edges | Junimo0 to Junimo3 | (40,23), (42,25), (62,23), (64,25) | |

| Step | What happens |
| --- | --- |
| | Fade in. Beat 600. Sound `reward`, a white flash. The four Junimos hop, `junimoMeep1`. Beat 800. |
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
| Enter | Morris appears off screen east at (70,28), walking speed 5, walks west along row 28 (the open plaza; the paving row runs through a bush) to (58,28), then steps up three tiles to (58,25) at the crowd's east edge. The farmer turns right to face him. Beat 500. |
| Morris | `morris-1`. Beat 200. |
| Morris | `morris-2`. Beat 200. |
| Morris | `morris-3`. Beat 300. |
| The shadow | Morris's whole figure darkens and his eyes go red; the screen takes on a held dim red glow; sound `shadowDie`. Beat 400. |
| Morris | `morris-4`. Beat 700. |
| | Glow off. The shadow STAYS on him. Beat 300. Two Junimos hop, `junimoMeep1`. |
| Exit | Morris steps back down to row 28 and walks east off screen. The walk does not hold the script: the farmer turns back up, beat 900, and the cut to the hall fades out over his walk. |

## Scene 5: inside the hall

Camera on (32,13); the cast sits on rows 12 to 15, clear of the potted plant at columns 34 to 36.

| Seat | Junimo | Tile | Colour |
| --- | --- | --- | --- |
| 1 | Junimo0 | (29,13) | green |
| 2 | Junimo1 | (31,12) | orange |
| 3 | Junimo2 | (33,12) | turquoise |
| 4 | Junimo3 | (30,14) | gold |
| 5 | Junimo4 | (28,15) | purple |
| 6 | Junimo5 | (36,15) | salmon |

Farmer at (32,15), facing up. The portrait is tinted to match whichever Junimo is speaking, and
the speaker hops just before its line.

| Step | Who | What happens |
| --- | --- | --- |
| | all six | Fade in. `junimoMeep1`, all six hop. Beat 800. |
| 1 | Junimo0 (green) | `junimo-1`. Beat 200. |
| 2 | Junimo1 (orange) | `junimo-2`. Beat 400. |
| 3 | Junimo2 (turquoise) | `junimo-3`. Beat 300. |
| 4 | Junimo3 (gold) | `junimo-4` if scene 3 played, otherwise `junimo-4-nocrack`. Beat 300. |
| 5 | Junimo0 (green) | `junimo-5`. Beat 600. |

## Scene 6: home, then the shrine

Land on the Farm two tiles below the farmhouse doorstep. The light drops to a bluish evening
(vanilla's dusk at about half strength). Camera on the door, clamped inside the map.

| Step | What happens |
| --- | --- |
| Home | Fade in. Beat 600. The farmer walks one tile up onto the doorstep. Beat 200. `doorClose`; the farmer is inside (hidden). Beat 900. |
| Pan | Trees within four tiles of the shrine go see-through for the rest of the scene. The camera glides to grandpa's shrine over 6 seconds, eased at both ends, and never past the map edge. Beat 1200. |
| Grandpa | `grandpa`, as a plain message with no portrait; `@` is the farmer's name. |
| Candle | Beat 900. `fireball`, one candle lights on the shrine. It stays lit for the rest of the year. Beat 2200. |
| Out | Fade to black over 1.5 s. The farmer is put back on the doorstep under the fade. The black holds 1.5 s past the end of the event, so the hand-off to the shrine menu happens unseen. End. |

After the event the run controller takes over: the shrine spend, then the loop-again / keep-playing
choice (`dialog.ending.*` in the lines file).

## Known gaps

- A test save started with `tly_newgame` has no completed bundles: the hall interior shows the
  unrestored rooms. `tly_remember` covers the friendship side, not this one. A real win looks
  different there.
- Farm types other than Standard: scene 1's porch tiles are fixed at (66,18) with the game's own
  per-type offset; scene 6 is door-relative. Beach and Four Corners are the ones to check.
