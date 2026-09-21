# Story session notes, 2026-09-21 (Jeff, live review)

Working notes for three specs to come: the season gate scenes review, the darkness first-strike
cutscenes, and story beats inside the seasons. Rulings here are Jeff's words unless marked.

## Tone ruling: the Light uses him

Jeff, 2026-09-21. The conflict reads like The Dark Is Rising. The Light is not the good or the
kind, and it is not soft. It does what must be done for the most people and for its own side.

- Vanilla's farmer leaves the corporate job and finds happiness in the town. Our struggle robs
  him of that: the results of his work, the friendships and marriages he forms, sometimes over
  and over. Sisyphus, or a private purgatory. He labors unseen and unappreciated.
- The Junimos know this and hate it, but they cannot stop. They used up his grandfather to buy
  time, and they will use him up too if he cannot succeed.
- No promise that it counts. If the dark rises, none of it counts for anything. Lines stay
  ambiguous and give less hope.

## Reversal: the rewind costs JP is back in the 1.0 story update

Jeff, 2026-09-21, reversing the 2026-09-16 ruling that it was not a story item. The farmer needs
to be able to lose, and the player needs to sit with the consequences. Needs its own spec. Parked
until the scenes are settled.

## Spring to Summer gate scene: new lines (Jeff's, verbatim)

Replaces `event.turn.summer-1..3`. The scene now foreshadows the darkness, because the first
blight can land on the night of Summer 1.

| Who | Line |
| --- | --- |
| Green | Thank you @. You've done well. But something dark has noticed our work here. |
| Orange | We have enough power to hold it back thanks to your gifts, but some attacks may slip through our guard. |
| Green, first loop | Prepare yourself, we know you can do this. |
| Green, after at least one rewind | Prepare yourself. We have taken much from you. We must take more, but know that it will end. |

The closing line changes once the save has been rewound at least once (my proposal, Jeff picked
the wording; the cost is only true after a loss).

## Summer to Fall gate scene: new lines (locked 2026-09-21)

Three Junimos, one line each, the same every loop (Jeff did not want a changed closer here).
The old orange line is cut: no character tells the player what he just watched (game-writing
rule 11).

| Who | Line |
| --- | --- |
| Green | You have been working hard. We are growing stronger. |
| Orange | It grows stronger as well. As the days grow shorter, it will reach further. |
| Turquoise | You must continue. We will hold back what we can, but the work is yours alone. |

## Fall to Winter gate scene: new lines (2026-09-21; Jeff picked line 3; "war" over "fight" is my pick, his to confirm)

Four lines, the same every loop.

| Who | Line |
| --- | --- |
| Green | This is the last season, @. If it is to end, it ends here. |
| Orange | The agents of the enemy will not stop, but with the cold and dark of Winter, it may attack directly. |
| Turquoise | It will corrupt whatever it can. To go on, you must mend what it breaks. |
| Green | If you win here, this cycle will be broken. But I fear our war will continue. |

Jeff's version of line 3: "It will corrupt whatever it can, but you must mend what is broken to
win." (shortened because line 4 opens on "If you win here").

## Darkness strikes (Jeff, 2026-09-21)

Remove the three first-strike villager letters. Overnight cutscenes in the fairy and witch slot.
Keep the morning popup messages. The darkness works through agents until Winter:

| Strike | Agent | Overnight scene |
| --- | --- | --- |
| Crop blight | Crows with glowing red eyes | They eat the crops that die, not fazed by the scarecrows. |
| Chest blight | A shadow man from the mines | He opens the chest that was picked, then runs away. |
| Reversion | Shadows | Shadows moving inside the Community Center. |
| Tampering (Winter only) | The darkness itself | The map view with a dark cloud flowing over it. From that night on every affected item has a dark aura. |

Only in Winter can the dark act directly: nature is subdued by the cold, the snow and the lack of
sun. The scenes play silent; no Junimo explains a strike afterwards.

**Every assault happens at least once, every loop** (Jeff). A guarantee, not a rising chance: a
kind that has not struck by the last week of the season it first appears in is forced on its first
eligible night of that week (crows and thief in Summer, hall shadows in Fall; tampering keeps its
Winter 1 rule). Open: what the thief guarantee does when nothing is stored.

## Problems flagged in the gate scenes (mine, for the review)

1. `fall-2` and `winter-2` report strikes that may not have happened now that strikes are a
   nightly chance.
2. The turns will overlap with the first-strike cutscenes; they may work better looking ahead.
3. Nothing moves: no walk-on, no exit, the farmer never reacts.
4. The same lines every loop.
5. The checkpoint "+JP" toast sits under the speech box for the first seconds.
