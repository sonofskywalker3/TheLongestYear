# The Year One Ending

**Date:** 2026-09-06
**Status:** design approved in brainstorm (Jeff, 2026-09-06); spec pending Jeff's review, then plan
**Story spec:** `2026-06-06-tly1-story-and-cutscenes-design.md` §5.4 (the win) and §3 (the spine).
This spec replaces §5.4 and adds the Year 2 hooks Jeff named on 2026-09-06.
**Related:** `2026-08-27-deja-vu-dialogue-design.md` (the familiarity meta this spec widens)
**Decompile:** PC 1.6 at `Stardee Valoo/decompiled-pc/Stardew Valley` (default), Android at
`Stardee Valoo/decompiled/decompiled`

## Problem

The win is a three-Junimo card on a black screen, then the shrine, then a yes/no box. It plays on the
Spring 1 wake frame after Winter 28, so a player who finishes the hall on Winter 12 has sixteen empty
days before anything acknowledges it (Nexus feedback). The card carries no story, so nothing sets up
Year 2. Vanilla's own completion ceremony still plays on the next sunny Town visit, telling a story
that is not ours.

## Goals

- The ending plays the morning after the last bundle is donated, whatever the date.
- It is a staged event in real locations with real sprites: the celebration, the crack in the town's
  forgetting, Morris leaving with the shadow on him, the Junimos' warning, one candle at grandpa's shrine.
- It plants every Year 2 thread: the town joining the fight, Joja pivoting to a Ginger Island resort
  funded by Skull Cavern iridium, grandpa as the previous keeper who could not finish, Perfection as
  the blow against the darkness.
- Loop again or keep playing, right there. Keep playing gives the rest of Year 1 as preparation and
  a wall on Spring 1 Year 2 until the Year 2 update exists.

## Non-goals

- Year 2 gameplay. This spec promises it in the fiction and leaves a flag for it to read.
- The rewind, season-turn and opening cutscenes (their own specs, same toolkit).
- Naming the darkness (June lock).
- Spouse memory. A married villager is never the speaker.

## Narrative locks (Jeff, 2026-09-06)

- **Grandpa was the previous keeper.** The Junimos chose him in his day; he saved the hall but the
  darkness wore him down before he could finish. Vanilla's four shrine candles are the work he left
  undone. Year 2 and 3 Perfection finish his evaluation, and the Statue of Perfection is the blow
  against the darkness. His letter stays innocent. The intro rework foreshadows this.
- **Morris is not gloating.** He gives a company update: Joja's survey crews found an iridium deposit
  in Skull Cavern big enough to fund the Ginger Island resort, so Pelican Town is no longer worth the
  lease. He wishes the town well, almost sincerely. The red eyes land on a throwaway line and only the
  player and the Junimos react.
- **The crack in the forgetting is a true memory.** The speaker's line is assembled from counts the
  save actually holds. Nothing is invented.
- **Grandpa's line:** "You've started what I couldn't finish. I'm so proud, but you must keep going."
- **Junimos on keep playing:** you've done well so far, but the work isn't over. Prepare yourself for
  what's next. On Spring 1 we get to work freeing the townsfolk.

## 1. The win night

Today `GateEvaluator.EvaluateDayEnd` returns Win only on Winter 28. New rule, evaluated at every
bedtime in `RunController.OnDayEnding` before the gate: if the board is complete (the same
`fullCcDone` the gate reads) and `MetaState.VictoryAcknowledged` is false and `RunState.EndingArmed`
is false, tonight is the win night. The Winter 28 path keeps returning Win and routes to the same
place, so a board finished on the last night behaves like any other.

The win night:

1. Lets the day end normally. The last room's overnight restoration scene plays; nothing is being
   rewound.
2. Records season pity as a pass for the current season (today's Win branch does this for Winter).
3. Sets `RunState.EndingArmed = true` (persisted with the run).
4. Forces tomorrow's weather to sunny, after vanilla has rolled its own, for the Town context.
   If tomorrow is a festival day the ending defers: the flag stays armed, the sunny force is applied
   again the next night, and the morning driver refuses to start on a festival day.

`RunAction.Win` at Winter 28 no longer opens `VictoryMenu`; it arms the ending the same way. The
`Day28Branch.Win` value and `VictoryMenu` are deleted with their driver branch.

**Once per save, choice every time.** `VictoryAcknowledged` is never cleared today, so a player who
keeps playing, hits the wall and loops again could never reach the choice (and later Year 2) on the
next win. Split the two jobs: new `MetaState.EndingSeen` gates the event (plays once per save; a
later win goes straight from the wake frame to the shrine and the choice, as the win does today), and
the wall's loop-again clears `VictoryAcknowledged` and `Year2WallArmed` so the next win night arms
again. Repeat Winter 28 wins inside one keep-playing run stay silent, as today.

## 2. The morning driver

`EndingEventDriver` (new, `Integration/`), subscribed on `UpdateTicked` like the two existing drivers,
dormant unless `RunActivation.IsActive`:

- Waits for `RunState.EndingArmed`, world ready, no `newDay`, no `eventUp`, no `farmEvent`, no
  `locationRequest`, no active menu, not a festival day, and `Game1.currentLocation` is the Farm
  after the player has left the farmhouse (`Game1.player.currentLocation.Name == "Farm"`).
- Starts the event exactly as `IntroSequenceDriver` starts the intro (a `new Event(script)` assigned
  to `currentLocation.currentEvent`, `eventUp` set) and remembers it started.
- Watches `Game1.eventUp` fall while `RunState.EndingSeenMail` is present in `mailReceived` (the
  script's last command adds it). Then clears `EndingArmed`, and runs the continuation in §5.
- If `eventUp` falls without the seen mail (something replaced the event), it logs a warning and
  re-arms: the ending plays the next time the player steps outside. Quitting overnight loses nothing,
  the flag is saved.

The event is not skippable, for the intro's reason: a skip bypasses the seen mail and the driver would
loop. `tly_ending` (§7) replays it.

## 3. Vanilla's ceremony

`EventSuppressionPatch.SuppressedEventIds` gains `191393`, the completion ceremony. The comment there
that warns against suppressing it is rewritten: the ending owns the slot now, and the keep-playing
branch (§5) adds `191393` to `eventsSeen` itself so the post-completion world (abandoned JojaMart on
the next storm, Pierre open Wednesdays, the Joja shutdown) flips exactly as vanilla flips it. The
loop-again branch never sets it, and the reset already scrubs every related mail (`WorldResetService`
`mailToClear`).

`191393` is a **current-run hand-off only**: it is written into `eventsSeen` for the rest of that year
and must never reach the next loop. That takes explicit work, because the reset does not scrub
`eventsSeen`: it clears it and then *re-seeds it FROM* the cross-loop memory `MetaState.SeenEventsEver`
(which `ModEntry.RecordSeenEvents` merges `eventsSeen` into on every save). Left alone, the first
"Keep playing" would bank `191393` forever and every later loop would open on Spring 1 with a
destroyed JojaMart, Pierre on post-completion hours and the lightning cutscene queued for the first
storm, on a zero-bundle board. `TheLongestYear.Core.Ending.PostCompletionEvents.IsHandedOffOnly` names
the id, and three places filter through it: `RecordSeenEvents` never banks it, `FarmerReset` skips it
in the re-seed loop and removes it from `eventsSeen` afterwards, and `ModEntry` purges it from
`SeenEventsEver` at load so a save that already banked it is healed. The replayable-cutscene scan
cannot cover this: `BuildReplayableExclude` seeds from `EventSuppressionPatch.SuppressedEventIds`,
which now contains `191393`, so the scan can never flag it replayable.

## 4. The event script

`EndingEventInjector.BuildEndingEvent(EndingCast cast)` (new, `Integration/`) assembles the script the
way `IntroEventInjector.BuildIntroEvent` does: one string, every line a `Strings.Get` key, no
Data/Events entry. `EndingCast` (Core) carries the speaker name and assembled line (or none), the
crowd list, and the shrine tile.

Six scenes:

1. **Porch.** `changeLocation Farm`, farmer and Lewis placed by `warp`/`addTemporaryActor` at the
   Standard-farm porch tiles (the game offsets Farm events per farm type; do not offset again). Two
   Lewis lines: the hall lit up overnight, everyone is there, come.
2. **Town, the hall steps.** `changeLocation Town`, viewport on the Community Center doors. The crowd
   is a fixed cast placed as temporary actors on known Town tiles, each checked with
   `Game1.getCharacterFromName` first and dropped if absent. The speaker (if any) is placed front and
   centre. Junimo temporary actors on the roof line, `jump` and `junimoMeep1`. Lewis, three lines: he
   does not understand how, but the hall is theirs. `playSound` sparkle, `screenFlash` low.
3. **The crack.** The speaker takes two steps forward (`move`, Town tiles are fixed), the assembled
   line, `emote` question mark, a beat, the closer line. Absent when no speaker qualifies.
4. **Morris.** Enters from the JojaMart side by `move`. Four lines in the company-update voice. On the
   throwaway line: `changeSprite Morris MorrisDark` (a palette-swapped copy of his sheet with red eyes,
   shipped in `assets/` and served as `Characters/MorrisDark` via `AssetRequested`), `glow` dim red,
   a low sound, one second, `stopGlowing`, sprite back. He leaves the way he came. `playSound`
   doorClose then a shutter sound; a temporary sprite darkens the Joja sign if a clean source frame
   exists, otherwise the sound carries it.
5. **Inside the hall.** `changeLocation CommunityCenter`, six Junimo temporary actors (Dusklight7's
   ask), jumping and chirping. Warm lines, the uneasy turn (the thing that had Morris moved, it did not
   leave; the town still forgets), then the prepare-yourself lines. Whether temporary-actor Junimos
   take a colour is verified at build; the intro's single white Junimo is the fallback.
6. **The shrine.** `changeLocation Farm`, farmer warped beside the shrine at the tile the farm reports
   (`Farm.GetGrandpaShrinePosition()`), `ambientLight` dropped to dusk. Custom command
   `tlyGrandpaCandle` (registered through `Event.RegisterCommand`): sets `Farm.grandpaScore` to 1,
   plays `fireball` once, calls `Farm.addGrandpaCandles()`, the same three steps vanilla's
   `grandpaCandles` command performs with a computed count. The farm relights the candle from that
   field every time it loads, so it stays lit through the rest of Year 1. Grandpa's line as
   `message` (no portrait). Hold, `globalFadeToBlack`, `addMailReceived <EndingSeenMail>`, `end`.

Vanilla's Year 3 judgement only runs when `grandpaScore` is zero, so a lit candle silences it. Year 2
and 3 own the shrine from here (their spec). The reset must zero `grandpaScore` on loop again; verify
`WorldResetService` covers it and add it if not.

## 5. The continuation and the wall

After the seen mail lands and the fade clears:

1. `TryOpenShrineThenContinue(ShowEndingChoice)`: the JP spend first, because loop-again resets the
   moment it is picked and keeps are bought before a reset.
2. `ShowEndingChoice`: a `DialogueBox` with two responses and no cancel, replacing
   `ShowKeepPlayingChoice`.
   - **Loop again**: `ContinueAfterResetSpend()`, the same mid-year reset the fail path uses.
   - **Keep playing**: `VictoryAcknowledged = true`, `MetaState.Year2WallArmed = true`, add `191393`
     to `eventsSeen`, then the Junimo lines (prepare yourself, Spring 1 we free the townsfolk) as a
     plain dialogue. The rest of Year 1 runs as today after a keep-playing win.

**The wall.** On `OnDayStarted` when `Game1.year >= 2`, `Year2WallArmed` is true and the Year 2 content
flag (`MetaState.Year2Started`, reserved, never set by this version) is false: a Junimo `DialogueBox`
before the hub (the loop is broken, but the Junimos are not ready to lead the next fight; Year 2 is
coming in a future beta, stay tuned) with the single response **Loop again**, which runs the reset to
Spring 1 Year 1. Quitting re-shows it on the next load. Saves that chose keep-playing on an older
version never have `Year2WallArmed` and are untouched. The Year 2 update reads `Year2WallArmed` to
pick the story up on Spring 1.

## 6. Villager memory and the assembled line

### Data

`FamiliarityRollup` keeps its score and adds `MetaState.VillagerMemory`, a dictionary from villager
name to a record: `Talks`, `Gifts`, `BirthdayGifts`, `HeartEvents` (running counts) and `Loops`
(the set of loop numbers the villager was dealt with in). `FamiliarityGlue` supplies one new signal,
`BirthdayGift = GiftsToday > 0 && today is the NPC's birthday` (season and day from `NPC.Birthday_Season`
and `NPC.Birthday_Day`). The current loop number is `MetaState.CompletedResets + 1`. No new polling.

### Speaker

`EndingSpeaker.Pick(meta, config, isPresent, isSpouse, isChild)` (Core, pure): among villagers with
familiarity at or above `DejaVuThreshold`, present in the game, not the spouse, not a child, the
highest score; ties broken by most loops, then by name. None qualifies: no speaker, scene 3 is
omitted.

### Line

`EndingLine.Assemble(memory, voice)` (Core, pure) picks the strongest true fact, in order:

| Tier | Condition | Middle sentence |
|---|---|---|
| 1 | birthday gifts in 2+ loops | "I remember you bringing me a birthday present twice." |
| 2 | a heart event seen in 2+ loops, and the event is in the scene table | "I remember the day we [scene], and I remember it twice." |
| 3 | gifts in 2+ loops | "I remember you bringing me things you couldn't have known I liked." |
| 4 | anything else | "I remember talking with you before you ever arrived." |

Opener and closer are fixed: "I know something is going on here that I can't understand. I keep
remembering conversations with you that never happened. You've only been here a year, but ..." and
"I don't know how, but I want to help." One i18n entry per tier with `{{villager}}` and `{{scene}}`
tokens; a per-villager voice override table (Shane, George, Haley, Abigail, Wizard at minimum) for
voices the generic line would flatten. The heart-event scene table names events by id with a short
scene phrase; an event not in the table falls through to tier 3. Tier 2 needs per-event, per-loop
tracking: `HeartEventLoops`, event id to loop set, on the memory record.

Every line is reviewed by Jeff before it ships, in a lines file beside this spec, the déjà-vu pattern.

Saves from before this version hold scores only, so their first ending reaches tier 4 at most. The
counts start the day they update.

## 7. Debug and verification

- `tly_win` now arms the ending for tomorrow morning (the real path) instead of opening a menu.
- `tly_ending [speaker <villager>]` replays the event on the spot, optionally forcing the speaker so
  every voice line can be eyeballed; it does not run the continuation.
- `tly_year2wall` fires the wall dialog.
- Runbook section in `docs/HEADLESS_DRIVING.md`: arm, sleep, step out, watch; loop-again branch;
  keep-playing branch through to the Spring 1 wall.

Unit tests (Core, existing suite):

- Win night: fires on any date when the board completes; never twice; Winter 28 routes the same;
  festival tomorrow defers.
- Speaker: threshold, exclusions, tie-break.
- Line: each tier, fallthrough order, missing scene falls to tier 3, score-only save gives tier 4.
- Memory rollup: birthday detection, loop set deduplication, heart-event loop tracking.
- Wall: armed only by the new branch, exempt for legacy keep-playing saves; its loop-again clears
  `VictoryAcknowledged` and `Year2WallArmed`; `EndingSeen` keeps the event from replaying.

Live, on the throwaway save, before release: the whole flow once on Standard, and scene 1 and 6 once
on Meadowlands (porch offset and shrine tile). Jeff's playtest is the voice and pacing pass only.

## 8. Files

New: `Integration/EndingEventDriver.cs`, `Integration/EndingEventInjector.cs`,
`Integration/GrandpaCandleCommand.cs`, `Core/Ending/EndingCast.cs`, `Core/Ending/EndingSpeaker.cs`,
`Core/Ending/EndingLine.cs`, `Core/VillagerMemory.cs`, `assets/morris_dark.png`, i18n keys under
`event.ending.*`, `dialog.ending.*`, `dialog.year2wall.*`.

Changed: `RunController` (win night, continuation, wall), `RunState` (`EndingArmed`), `MetaState`
(`VillagerMemory`, `EndingSeen`, `Year2WallArmed`, `Year2Started`), `FamiliarityRollup` / `FamiliarityGlue`,
`EventSuppressionPatch` (191393), `WorldResetService` (grandpaScore), `Day28CutsceneDriver` (Win
branch removed), `ModEntry` (commands, asset), `HEADLESS_DRIVING.md`.

Deleted: `UI/VictoryMenu.cs`, `Day28Branch.Win`.

## 9. Open items carried to other specs

- Intro rework: foreshadow grandpa as the previous keeper.
- Year 2 spec: reads `Year2WallArmed`, owns the shrine candles 2 to 4, the town-in-the-loop mechanic
  (friendship and Perfection), Ginger Island and Skull Cavern content.
- Déjà-vu phase 2 (festival memories) can read `VillagerMemory` once it exists.
