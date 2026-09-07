# Year One Ending: every ending/wall line, for review

Status: pending Jeff's review; every line here goes past him before release.

This task (Task 5) adds the "crack" lines: the villager who steps forward at the ceremony says
one line assembled from what the save actually remembers, framed by an opener and a closer.
Task 9 will append the `dialog.ending.*` and `dialog.year2wall.*` families to this same table
once they exist.

## The crack (`event.ending.crack.*`)

| Key | Text |
| --- | --- |
| `event.ending.crack.open` | I know something is going on here that I can't understand.#$b#I keep remembering conversations with you that never happened. |
| `event.ending.crack.tier1` | You've only been here a year, but I remember you bringing me a birthday present twice. |
| `event.ending.crack.tier2` | You've only been here a year, but I remember the day we {{scene}}, and I remember it twice. |
| `event.ending.crack.tier3` | You've only been here a year, but I remember you bringing me things you couldn't have known I liked. |
| `event.ending.crack.tier4` | You've only been here a year, but I remember talking with you before you ever arrived. |
| `event.ending.crack.close` | I don't know how, but I want to help. |

### Voice overrides

| Key | Text |
| --- | --- |
| `event.ending.crack.tier1.Shane` | You've been here a year. So why do I remember you turning up with a birthday present two years running? |
| `event.ending.crack.tier2.Shane` | You've been here a year. So why do I remember the day we {{scene}} like it happened twice? |
| `event.ending.crack.tier3.Shane` | You've been here a year. So why do I remember you bringing me stuff you had no business knowing I liked? |
| `event.ending.crack.tier4.Shane` | You've been here a year. So why do I remember talking to you before you ever showed up? |
| `event.ending.crack.tier1.George` | Only been here a year, you say. Then explain why I remember two birthday presents from you. Two. |
| `event.ending.crack.tier2.George` | Only been here a year, you say. Then explain why I remember the day we {{scene}} twice over. |
| `event.ending.crack.tier3.George` | Only been here a year, you say. Then explain why I remember you bringing me things nobody told you about. |
| `event.ending.crack.tier4.George` | Only been here a year, you say. Then explain why I remember talking to you before you got here. |
| `event.ending.crack.tier1.Haley` | You've only been here a year, right? Because I totally remember you bringing me a birthday present twice. |
| `event.ending.crack.tier2.Haley` | You've only been here a year, right? Because I totally remember the day we {{scene}}. Twice. |
| `event.ending.crack.tier3.Haley` | You've only been here a year, right? Because I totally remember you bringing me things you couldn't have known I'd like. |
| `event.ending.crack.tier4.Haley` | You've only been here a year, right? Because I totally remember talking to you before you moved here. |
| `event.ending.crack.tier1.Abigail` | You've only been here a year... but I swear I remember you bringing me a birthday present twice. That's so weird. |
| `event.ending.crack.tier2.Abigail` | You've only been here a year... but I swear I remember the day we {{scene}} happening twice. That's so weird. |
| `event.ending.crack.tier3.Abigail` | You've only been here a year... but I swear you kept bringing me things you couldn't have known I liked. |
| `event.ending.crack.tier4.Abigail` | You've only been here a year... but I swear I remember talking with you before you ever arrived. |
| `event.ending.crack.tier1.Wizard` | You have been here a single year. Yet I hold the memory of two birthday gifts from your hand. Curious. |
| `event.ending.crack.tier2.Wizard` | You have been here a single year. Yet I hold the memory of the day we {{scene}}, and I hold it twice. Curious. |
| `event.ending.crack.tier3.Wizard` | You have been here a single year. Yet you brought me things no mortal could have known I favour. Curious. |
| `event.ending.crack.tier4.Wizard` | You have been here a single year. Yet I recall our conversations from before you set foot in this valley. Curious. |

## Scene phrases (`event.ending.scene.*`)

Fill the `{{scene}}` token in a tier-2 line above: "the day we `{{scene}}`". Each row names the
real heart event behind the phrase (id, friendship points at the time, source location), read
from a live Data/Events dump against the installed game on 2026-09-06.

| Key | Text | Event id | Points (hearts) | Location |
| --- | --- | --- | --- | --- |
| `event.ending.scene.abigail-2` | played the video game together | 1 | 500 (2) | SeedShop |
| `event.ending.scene.alex-2` | played catch on the beach | 20 | 500 (2) | Beach |
| `event.ending.scene.elliott-2` | toured the cabin | 39 | 500 (2) | ElliottHouse |
| `event.ending.scene.emily-2` | shared a strange dream | 471942 | 500 (2) | HaleyHouse |
| `event.ending.scene.haley-2` | watched the sisters argue | 11 | 500 (2) | HaleyHouse |
| `event.ending.scene.harvey-2` | watched a check-up | 56 | 500 (2) | JoshHouse |
| `event.ending.scene.leah-2` | looked at the sculpture | 50 | 500 (2) | LeahHouse |
| `event.ending.scene.maru-2` | helped test the soil | 6 | 500 (2) | ScienceHouse |
| `event.ending.scene.penny-2` | watched her help an elder | 34 | 500 (2) | Town |
| `event.ending.scene.sam-2` | listened to the jam session | 44 | 500 (2) | SamHouse |
| `event.ending.scene.sebastian-4` | looked at the motorcycle | 384883 | 1000 (4) | Mountain |
| `event.ending.scene.shane-2` | shared a drink outside | 611944 | 500 (2) | Forest |

Villagers with no scene entry (their lowest-hearts events were too ambiguous, or reserved for a
higher tier already) fall through to the gifts or talks tier instead: no scene phrase is
required for them.

## The ending script (`event.ending.*`, Task 9)

The six-scene event built by `EndingEventInjector.Build`, in scene order.

### Scene 1: the porch (Lewis)

| Key | Text |
| --- | --- |
| `event.ending.lewis-porch-1` | @! There you are. Come quick, you have to see this.$h |
| `event.ending.lewis-porch-2` | The Community Center... it's lit up. The whole town is out there. I don't understand it, but come on! |

### Scene 2: the hall steps (Lewis)

| Key | Text |
| --- | --- |
| `event.ending.lewis-hall-1` | Everyone, everyone... I still can't explain what happened here overnight.#$b#But this building is ours again. |
| `event.ending.lewis-hall-2` | And we all know whose hands did the work this year. Three cheers for our farmer!$h |
| `event.ending.lewis-hall-3` | Pelican Town has its heart back. Thank you. |

### Scene 3: the crack

Uses the existing `event.ending.crack.*` and `event.ending.scene.*` families above; no new keys.

### Scene 4: Morris

| Key | Text |
| --- | --- |
| `event.ending.morris-1` | Congratulations. Genuinely. Joja values a competitor who can deliver. |
| `event.ending.morris-2` | I'm here to let you all know that Joja will be closing its Pelican Town location, effective today. |
| `event.ending.morris-3` | Our survey crews found an iridium deposit in the Skull Cavern. It will fund a resort on Ginger Island. This lease was never worth the paperwork. |
| `event.ending.morris-4` | I wish you all the best. Nothing is going to slow this down now. |

### Scene 5: inside the hall, six Junimos

The lines pass between the Junimos (green, orange, turquoise, gold, then green again); `junimo-4-nocrack` replaces `junimo-4` when no villager stepped forward in scene 3.

| Key | Text |
| --- | --- |
| `event.ending.junimo-1` | You did it, @! The hall is whole, and so are we.$h |
| `event.ending.junimo-2` | We sang all night. We haven't sung like that in a very long time. |
| `event.ending.junimo-3` | But... did you see the man from Joja? The thing that has him did not leave. It only moved. |
| `event.ending.junimo-4` | And the town still forgets. Every one of them. You saw the crack in it today, though. Didn't you? |
| `event.ending.junimo-4-nocrack` | And the town still forgets. Every one of them. Not one of them could hold on to you today. Not yet. |
| `event.ending.junimo-5` | You've done well so far, but the work isn't over. Prepare yourself for what's next.#$b#On Spring 1, we get to work freeing the townsfolk. |

Note: `junimo-5` carries the "keep playing" promise inside the event; Task 11's keep-playing
dialogue repeats the second half so a player who loops again has still heard it.

### Scene 6: the shrine at dusk

| Key | Text |
| --- | --- |
| `event.ending.grandpa` | Good job, @. You've started what I could never finish. I'm so proud of you, but you must keep going. For the sake of the valley... and the world. |

### Build-time notes

- Staging, marks, timings and transitions are in `2026-09-07-year-one-ending-script.md`.
- The Junimos are real coloured Junimo actors named `Junimo0`..`Junimo5` in the code and "Junimo"
  on screen; `jump`/`speak` address the code names.

## The continuation choice (`dialog.ending.*`, Task 11)

Shown after the event and the shrine spend (and, on a repeat win, straight off the wake frame).

| Key | Text |
| --- | --- |
| `dialog.ending.prompt` | {{loopline}}<br>The loop is yours to keep or to break. Begin a new loop now, or keep playing this year? |
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
| `dialog.year2wall.prompt` | The loop is broken, and the Junimos are not yet ready to lead the next fight.<br>Year 2 of The Longest Year is coming in a future beta. Stay tuned!<br>For now, the Junimos can send you around one more time. |
| `dialog.year2wall.loop` | Loop again |
