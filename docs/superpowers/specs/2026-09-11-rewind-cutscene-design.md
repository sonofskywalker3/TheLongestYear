# The Rewind

**Date:** 2026-09-11
**Status:** design approved in brainstorm (Jeff, 2026-09-10/11); spec pending Jeff's review, then plan
**Story spec:** `2026-06-06-tly1-story-and-cutscenes-design.md` §5.2 (the rewind). This spec replaces
that section: the void backdrop it describes is dropped in favour of a camera pan across the real Town.
**Related:** `2026-09-06-year-one-ending-design.md` (the win scene, whose event commands and cast
selector this reuses), `2026-09-09-darkness-pushback-design.md` (the adversary the Junimos name here)
**Decompile:** PC 1.6 at `Stardee Valoo/decompiled-pc/Stardew Valley`

## Problem

Missing a season gate is the mod's signature failure, and it currently plays as a static card:
`Day28CutsceneMenu` draws a black screen, one scaled Junimo sprite and paged text. TODO calls it "OK
but static" and asks for something that feels like the year actually rewinding.

The card is also the only place the year's undoing is ever shown. The player loses a year of work and
sees a paragraph about it.

## Goals

- The rewind is a staged sequence in real locations, not a card.
- The year visibly runs backward: seasons, light and weather all reverse across a single camera move.
- The cost is personal: a villager you bonded with forgets you on screen.
- The Junimos' power is finite and visibly spent, setting up the JP-cost idea in TODO.
- The player sees Spring 1 before they answer anything, and never sees a stale date again.

## Non-goals

- The day and time counter rolling backward in a corner with each day's real historical weather.
  Jeff parked this explicitly for a later update. Note that this spec's clock handling is the
  mechanism that feature needs, so it becomes a readout rather than a new system.
- Charging JP for the rewind (TODO, Open, un-costed).
- Touching the Continue (gate passed) branch. This spec is the Fail branch only.

## What plays

Fourteen beats, in order. Beats 1 to 9 are the bedroom, 10 is the pan, 11 onward is the return.

1. The farmer in bed at night.
2. The house lights go out.
3. The Junimos appear around the bed, each with its own small light radius.
4. "<farmer>, you have worked hard, but we have not gained enough power to proceed."
5. "Your efforts have attracted the attention of our adversary. It is coming for you now, and we
   cannot hold it back."
6. Darkness presses in on the edges.
7. "But the work must continue, we cannot fail here. So we will use what power we have to give you
   another chance."
8. "The year will begin again, and the darkness will sleep once more, until the light begins to grow."
   **The hold-or-reshuffle question is asked here** (see Ordering below).
9. The Junimo light pushes the darkness back and grows until the screen is white.
10. The rewind pan across Town (see The pan).
11. The farmhouse, Spring 1, sunny morning. The farmer still asleep in bed, Junimos still circling,
    no light aura now.
12. "We have bought you more time. Use it and what remains of our power wisely, and remember that
    every step you take is a small victory."
13. The pity dialogs.
14. The shrine.

Lines 4, 5, 7, 8 and 12 are Jeff's, to be used verbatim and moved into `i18n/default.json` under
`cutscene.rewind.*`. The existing `cutscene.day28.fail` blob is retired, except its closing question,
which becomes the beat 8 prompt.

## Where it plays in the existing flow

`Day28CutsceneDriver` opens the current scene on a deliberately chosen frame: after the shipping
tally, after the save, after the new-day sequence, after any overnight `FarmEvent` and any pending
`locationRequest`, but while the wake fade is still dark. That window was chosen in the 2026-06-03
playtest and is defended by a watchdog that re-arms if vanilla's `showEndOfNightStuff` steals the
frame. **This spec keeps that window unchanged.**

The world at that moment is still on the failed season's date. `OnCutsceneEnded` hides the day/time
HUD precisely because the pre-rewind date would otherwise show through the choice and the shrine, and
restores it once the world is back on Spring 1. Two consequences:

- The pan opens in the real current season. Nothing has to be faked to start.
- The reset itself still runs after the shrine, exactly as it does today. Nothing about its position
  in the flow moves.

## Paint versus state

Jeff, 2026-09-11: the reset does not have to happen during the cutscene, but the **paint** of Spring 1
must be on the last screen and must hold through the questions and the purchasing, until the player
gets control and the clock starts.

So beat 11 paints Spring 1 cosmetically:

- `Game1.season` set to Spring, so anything that reads it draws Spring.
- Weather cleared to sunny; the pan's wind and snow flags switched off.
- `timeOfDay` set to the ordinary wake time, so the light reads as morning.
- The HUD shows Spring 1 rather than being hidden.

That paint holds across beats 12, 13 and 14. The real reset lands underneath it and reconciles
everything for real. **This replaces the `Game1.displayHUD = false` hack in `OnCutsceneEnded`:** the
date is no longer hidden because it is wrong, it is shown because it is what the player is looking at.

Risk: `Game1.season` and the clock are global, and they are being set to something the run state does
not yet agree with. The window is short, scripted, and has no player control in it, but any TLY system
that reads season during it must be checked. Call this out in the plan.

## Ordering: the hold question

The prompt asking whether to hold the town's wishes steady or let time reshuffle them decides how the
new board is built. It cannot be asked after the reset, because by then the board exists. Jeff's beat
order put the keep and pity dialogs at 13; the pity half can live there, the hold half cannot.

It moves to beat 8, which is where the existing written line already puts it: the Junimos say they
will spend their power to give you another chance, and ask the question before they spend it.

## Section 1: the bedroom, beats 1 to 9

We are in `FarmHouse` on the dark wake frame, farmer in bed. The fade is never allowed to lift, so
what the player reads as night is the darkness the engine already handed us. No clock to fight, no
player placement to argue with. This is why the bedroom stays drawn by us rather than becoming a
vanilla `Event`: the June attempts died on exactly this frame (`fade` revealed the room, `globalFade`
blinked back, a `RenderedWorld` overlay painted over the event's own dialogue box).

**Lights out (beat 2)** removes the farmhouse's fireplace and lamp entries from
`Game1.currentLightSources` so nothing glows. It does not darken the room. Jeff, 2026-09-11: the
lights stay off afterwards as a reminder, but Spring 1 is daylit as normal, so only the glowing auras
are missing.

**The Junimos (beat 3)** are real `Junimo` instances, the same class the ending uses
(`new Junimo(pos, -1, temporary: true)` with the colour set by reflection through `JunimoPalette`).
They animate themselves: a four-frame idle at 100ms standing, eight-frame walk cycles per facing.
`stayPut` holds them at the bed so they sway in place instead of wandering the farmhouse. Each gets a
`LightSource` with its own small radius.

**The darkness (beat 6)** is two dials moving together: ambient toward black, and each Junimo's light
radius shrinking. The visible world closes to a few shrinking circles around the bed. A cloud-shaped
edge, which Jeff would prefer, needs a drawn mask over the lighting pass. Build the radius version
first, look at it, and only reach for the mask if the circles read as too tidy.

**The white (beat 9)** is the same dial reversed: radii grow past the size of the screen, colour
warming to white. The flash is the Junimos spending everything they have rather than an effect laid
on top.

Lines go through `EndingSpeechBox` with the Junimo portrait.

## Section 2: the pan, beat 10

Camera only. The farmer stays in bed, so nothing has to explain why they are outdoors at night.

Route: Clint's shop, panning up and left to the path out to the farm in the top left. **Both endpoints
need real tile coordinates measured off the Town map, not estimated**; the pan duration follows from
that distance. The camera uses the ending's existing `tlyPanTo`, which already does eased viewport
moves over a duration.

Four dials run along the route:

- **Seasons.** Swap points sit at even fractions of the route, divided by how many seasons are being
  unwound: a Fall failure swaps at one third and two thirds, a Winter failure at a quarter, a half and
  three quarters, a Spring failure not at all. Each swap is `GameLocation.updateSeasonalTileSheets()`,
  which disposes the map's tilesheets and reloads them under the current season key. Town is outdoors
  so it qualifies. **Call only that, never `seasonUpdate()`**, which mutates terrain, crops and
  features rather than just repainting.
- **The clock**, running backward the whole way. `Game1.UpdateGameClock` recomputes `outdoorLight`
  from `timeOfDay` every tick, lerping toward `eveningColor` past `getStartingToGetDarkTime` and
  deepening past `getTrulyDarkTime`. Driving the clock backward lights the valley backward for free.
  This is the beat that sells the reversal, and it is the same mechanism the parked day-counter
  feature will need.
- **Weather.** `isDebrisWeather` blows wind across the whole pan. Gusts land on each season swap.
- **The villager.** Walking the path toward the farm, stopping, a `?` emote, turning back.

**Seasons cannot crossfade.** The tilesheet swap is a dispose and reload against one map with one
tilesheet set; a true dissolve would mean rendering Town twice into render targets and blending them.
So it is a cut, hidden under a gust of `WeatherDebris` on the swap frame: the wind blows the season
away. Jeff accepted a white flash as the fallback; the gust is preferred because it is diegetic and
makes the wind he asked for load-bearing rather than decorative.

**The villager is chosen by the ending's existing cast selector**, which already picks the villager
the player is most bonded with and knows which shared scene they would half-remember. The one who
almost remembers you when you win is the one who forgets you when you fail, from the same code.

### A Spring failure

Nothing to unwind, so no season swaps. The pan still runs its full length with wind throughout, the
clock still runs backward with the light changing to match, and a few weather turns land along the
way. Time visibly comes undone even though the season does not.

Directional light is not an option: every character draws one `shadowTexture` blob centred under the
sprite with a fixed offset (`Character.cs:1708`). No sun angle, no length. The ambient light is the
indicator.

## Section 3: the return, beats 11 to 14

The pan ends, Spring 1 is painted per Paint versus state, and we are back in the farmhouse with the
farmer asleep and the Junimos still circling without their auras. Line 12 plays, then the pity
dialogs, then the shrine, all under the Spring 1 paint. The existing reset runs underneath and the
player takes control on a real Spring 1.

## Components

| Unit | Responsibility |
|---|---|
| `RewindBedroomScene` | Beats 1 to 9. Drawn. Owns the lights, the Junimos, the darkness dials, the white. |
| `RewindPanScene` | Beat 10. Owns the route, the four dials and the villager. |
| `RewindSpringPaint` | The cosmetic Spring 1 state and holding it across the menus. |
| `Day28CutsceneDriver` | Unchanged window and watchdog; opens the rewind sequence instead of the card. |
| `Day28CutsceneMenu` | Retired for Fail. Still serves Continue until that branch is redesigned. |

## Testing

Unit tests cover what is decidable without the engine: the season swap points for each failing season
(including Spring's zero), the backward clock's start and end values, the villager selection agreeing
with the ending's selector, and the beat ordering placing the hold question before the reset.

The rest is a live run per `docs/HEADLESS_DRIVING.md`, driven by `tly_failreset`, which already queues
the Fail branch from anywhere. Check on screen: the lights are out and stay out, the Junimos animate
and hold station, the darkness closes and the white opens, each season swap is hidden by its gust, the
villager turns back, and nothing between beat 11 and taking control ever shows a date that is not
Spring 1.

## Open questions for the plan

- Town route endpoints: measure Clint's and the farm path.
- Whether the cloud mask is needed, judged on the radius version.
- Which TLY systems read `Game1.season` during the paint window.
