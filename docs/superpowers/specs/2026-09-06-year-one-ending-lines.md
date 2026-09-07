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
