# Year One Ending: every ending/wall line, for review

Status: pending Jeff's review; every line here goes past him before release.

This task (Task 5) adds the "crack" lines: the villager who steps forward at the ceremony says
one line assembled from what the save actually remembers, framed by an opener and a closer.
Task 9 will append the `dialog.ending.*` and `dialog.year2wall.*` families to this same table
once they exist.

## The crack (`event.ending.crack.*`)

| Key | Text |
| --- | --- |
| `event.ending.crack.open` | Something is going on here that I don't understand. |
| `event.ending.crack.tier1` | You've only been here a year, but I remember you giving me a present at least two different times. |
| `event.ending.crack.tier2` | I can remember {{scene}}, but how many times did that happen? |
| `event.ending.crack.tier3` (also the Talks tier, Jeff 2026-09-09) | The very first time I saw you, I could have sworn we'd already met. |
| `event.ending.crack.close` | I don't know how. But I want to help. |

### Voice overrides

| Key | Text |
| --- | --- |
| `event.ending.crack.tier1.Shane` | You've been here a year. So how come I remember you giving me a present at least twice? |
| `event.ending.crack.tier2.Shane` | I remember {{scene}}. So how come I remember it more than once? |
| `event.ending.crack.tier3.Shane` | The first time I saw you, I could've sworn I already knew you. Figured it was the beer. |
| `event.ending.crack.tier1.George` | A year, you say. Then why do I remember you giving me presents two different times? Hmph. |
| `event.ending.crack.tier2.George` | I remember {{scene}}. So why do I remember it twice, if you've only been here a year? |
| `event.ending.crack.tier3.George` | First time I laid eyes on you, I could've sworn we'd met. And my memory isn't that bad. |
| `event.ending.crack.tier1.Haley` | You've only been here a year, right? Because I totally remember you giving me a present at least two different times. |
| `event.ending.crack.tier2.Haley` | I totally remember {{scene}}. But how many times did that even happen? |
| `event.ending.crack.tier3.Haley` | The very first time I saw you, I thought I already knew you. Weird, right? |
| `event.ending.crack.tier1.Abigail` | You've only been here a year, but I remember you giving me a present at least two different times. That's so weird. |
| `event.ending.crack.tier2.Abigail` | I remember {{scene}}. But I swear it happened more than once. Weird. |
| `event.ending.crack.tier3.Abigail` | The very first time I saw you, I could have sworn we'd already met. Isn't that weird? |
| `event.ending.crack.tier1.Wizard` | You have been here one year. Yet I recall gifts from your hand on two occasions. Curious. |
| `event.ending.crack.tier2.Wizard` | I recall {{scene}}. Yet it seems to have happened twice. Curious. |
| `event.ending.crack.tier3.Wizard` | The first time I saw you, I was certain we had met before. Curious. |

## Scene phrases (`event.ending.scene.*`)

Fill the `{{scene}}` token in a tier-2 line above: "I can remember `{{scene}}`, but how many times did that happen?" (Jeff, 2026-09-09: custom wording per villager so it never reads awkwardly; the villager is the speaker). Each row names the
real heart event behind the phrase (id, friendship points at the time, source location), read
from a live Data/Events dump against the installed game on 2026-09-06.

| Key | Text | Event id | Points (hearts) | Location |
| --- | --- | --- | --- | --- |
| `event.ending.scene.abigail-2` | playing that video game with you | 1 | 500 (2) | SeedShop |
| `event.ending.scene.alex-2` | throwing the gridball around on the beach with you | 20 | 500 (2) | Beach |
| `event.ending.scene.elliott-2` | showing you around my cabin | 39 | 500 (2) | ElliottHouse |
| `event.ending.scene.emily-2` | that dream we both had | 471942 | 500 (2) | HaleyHouse |
| `event.ending.scene.haley-2` | you walking in on Emily and me fighting | 11 | 500 (2) | HaleyHouse |
| `event.ending.scene.harvey-2` | your check-up at the clinic | 56 | 500 (2) | JoshHouse |
| `event.ending.scene.leah-2` | showing you my sculpture | 50 | 500 (2) | LeahHouse |
| `event.ending.scene.maru-2` | testing soil samples with you | 6 | 500 (2) | ScienceHouse |
| `event.ending.scene.penny-2` | you helping me with George's wheelchair | 34 | 500 (2) | Town |
| `event.ending.scene.sam-2` | you sitting in on our band practice | 44 | 500 (2) | SamHouse |
| `event.ending.scene.sebastian-4` | showing you my bike up on the mountain | 384883 | 1000 (4) | Mountain |
| `event.ending.scene.shane-2` | that beer we had outside the ranch | 611944 | 500 (2) | Forest |

Villagers with no scene entry (their lowest-hearts events were too ambiguous, or reserved for a
higher tier already) fall through to the gifts or talks tier instead: no scene phrase is
required for them.

## The ending script (`event.ending.*`, Task 9)

The six-scene event built by `EndingEventInjector.Build`, in scene order.

### Scene 1: the porch (Lewis)

| Key | Text |
| --- | --- |
| `event.ending.lewis-porch-1` | @! There you are. Come quick.$h |
| `event.ending.lewis-porch-2` | The Community Center is lit up. The whole town is out there. Come on! |

### Scene 2: the hall steps (Lewis)

| Key | Text |
| --- | --- |
| `event.ending.lewis-hall-1` | Everyone. I can't explain what happened here overnight.#$b#But this building is ours again. |
| `event.ending.lewis-hall-2` | We may not have said it, but we all saw you working in here this past year. |
| `event.ending.lewis-hall-3` | The Community Center belongs to Pelican Town again. Thank you. |

### Scene 3: the crack

Uses the existing `event.ending.crack.*` and `event.ending.scene.*` families above; no new keys.

### Scene 4: Morris

| Key | Text |
| --- | --- |
| `event.ending.morris-1` | Congratulations. Joja respects a competitor who can deliver. |
| `event.ending.morris-2` | Joja will be closing its Pelican Town store, effective today. |
| `event.ending.morris-3` | Our survey crews found enough iridium in the Skull Cavern to fund a resort on Ginger Island. This lease was never worth the paperwork. |
| `event.ending.morris-4` | I wish you all the best. Nothing is going to slow this down now. |

### Scene 5: inside the hall, six Junimos

The lines pass between the Junimos (green, orange, turquoise, gold, then green again); `junimo-4-nocrack` replaces `junimo-4` when no villager stepped forward in scene 3.

| Key | Text |
| --- | --- |
| `event.ending.junimo-1` | You did it, @. The hall is whole, and so are we. |
| `event.ending.junimo-2` | We sang all night. |
| `event.ending.junimo-3` | Did you see the man from Joja? The thing that has him did not leave. It only moved. |
| `event.ending.junimo-4` | The town still forgets. But you saw the crack in it today. |
| `event.ending.junimo-4-nocrack` | The town still forgets. Not one of them could hold on to you today. |
| `event.ending.junimo-5` | You've done well so far, but the work isn't over. Prepare yourself for what's next.#$b#On Spring 1, we get to work freeing the townsfolk. |

Note: `junimo-5` carries the "keep playing" promise inside the event; Task 11's keep-playing
dialogue repeats the second half so a player who loops again has still heard it.

### Scene 6: the shrine at dusk

| Key | Text |
| --- | --- |
| `event.ending.grandpa` | @. You've started what I couldn't finish. I'm so proud, but you must keep going. |

### Build-time notes

- Staging, marks, timings and transitions are in `2026-09-07-year-one-ending-script.md`.
- The Junimos are real coloured Junimo actors named `Junimo0`..`Junimo5` in the code and "Junimo"
  on screen; `jump`/`speak` address the code names.

## The continuation choice (`dialog.ending.*`, Task 11)

Shown after the event and the shrine spend (and, on a repeat win, straight off the wake frame).

| Key | Text |
| --- | --- |
| `dialog.ending.prompt` | {{loopline}}<br>Begin a new loop, or keep playing this year? |
| `dialog.ending.new-loop` | Loop again |
| `dialog.ending.keep-playing` | Keep playing this year |
| `dialog.ending.keep-1` | You've done well so far, but the work isn't over. Prepare yourself for what's next. |
| `dialog.ending.keep-2` | On Spring 1, we get to work freeing the townsfolk. |

`keep-1`/`keep-2` deliberately repeat `event.ending.junimo-5`: the player who chooses Keep playing
hears the promise again as the last thing before the day starts.

## Year 2 wall (`dialog.year2wall.*`, Task 12)

Shown on Spring 1 of year 2 on a keep-playing save, in place of the normal day-start flow, until
the Year 2 update ships. One response only: Loop again.

| Key | Text |
| --- | --- |
| `dialog.year2wall.prompt` | The loop is broken, but the Junimos are not ready for what comes next.<br>Year 2 is coming in a future beta.<br>For now, the Junimos can begin another loop. |
| `dialog.year2wall.loop` | Loop again |

## Season turns (`event.turn.*`, spec 2026-09-07-season-turn-beats)

Placeholders: Jeff owns the wording. The porch scene on the morning of Summer 1 (two Junimos,
hopeful), Fall 1 (three, uneasy) and Winter 1 (four, alarmed) after a passed gate. Who speaks
which line is a table in `SeasonTurn.Lines`; the text is here and in i18n only.

### Spring to Summer

| Key | Who | Text |
| --- | --- | --- |
| `event.turn.summer-1` | A (green) | You did it, @. Spring is done. |
| `event.turn.summer-2` | B (orange) | Every gift makes us stronger. |
| `event.turn.summer-3` | A (green) | Keep going. We will be watching. |

### Summer to Fall

| Key | Who | Text |
| --- | --- | --- |
| `event.turn.fall-1` | A (green) | Summer is done, @. You held on. |
| `event.turn.fall-2` | B (orange) | Did you see your crops? Something is in the soil. |
| `event.turn.fall-3` | C (turquoise) | It has noticed you. |
| `event.turn.fall-4` | A (green) | Guard what you grow. We will help where we can. |

### Fall to Winter

| Key | Who | Text |
| --- | --- | --- |
| `event.turn.winter-1` | A (green) | Fall is over, @. The hall is still standing. |
| `event.turn.winter-2` | B (orange) | It has found the hall. Some of your gifts have been taken. |
| `event.turn.winter-3` | C (turquoise) | It will corrupt whatever it can. Our plans may be changed, but not thwarted. |
| `event.turn.winter-4` | A (green) | One final season. The darkness is strong. But because of you, we may yet grow stronger. |
