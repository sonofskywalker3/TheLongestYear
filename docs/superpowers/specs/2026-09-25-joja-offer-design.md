# Morris's offer: the Joja side quest and its game over

Approved in conversation with Jeff, 2026-09-25. Story branch.

## Why

In the opening, Morris tells the farmer to come see him at the store if they want to be his
assistant. Nothing follows that up today: the only Joja rule is `JojaMembershipBlock`, which refuses
the membership with a generic Junimo line. This side quest pays the line off, keeps Joja purely
hostile (the player never gets a single positive or helpful interaction with Joja anywhere in the
mod), and plants the first clue that Morris is being helped from outside: he remembers the player
across loops.

## The flow, one loop

1. **The offer scene.** The first time the player enters JojaMart in a loop, a scene plays: Morris
   repeats the assistant manager offer with details. Joja buys the farm for a huge sum to turn it
   into a meat processing plant; the job comes with company housing, transportation and excellent
   benefits, the whole package. "Think about it." Then the player is free.
   - It plays every loop as a reminder. After the first time it has ever been seen on this save,
     it is skippable (same rule as the other seen scenes: `MetaState.SeasonTurnsSeen` style set).
   - It never plays again once the player has been rejected (see "Rejected").
2. **Undecided (scene seen this loop, no answer yet).**
   - The cashier will not sell anything: "I'm not supposed to let you buy anything until you talk to
     the manager." (draft; final wording through the game-writing skill)
   - Talking to Morris (the `JoinJoja` counter tile) asks whether the player accepts: Yes / No.
3. **No.** Morris refuses the player service, for any reason, for good, and promises they will never
   work for Joja again. The player is now **Rejected**.
4. **Yes.** The bad ending (below). Nothing about the yes is saved.

## Letters

Both kinds are sent through the existing mail path (`Data/Mail` entries plus the mailbox, as
`OnboardingMailService` does). All counts and clocks are **per loop**: a rewind starts them over.

- **"Come see me" letters (scene not yet seen this loop).** Twice a season on random days, until the
  player walks into JojaMart this loop. Up to 8 a year, one text per slot (8 texts), each less
  friendly and more insistent than the last. The count and the escalation start over each loop.
- **"Make a decision" letters (scene seen, no answer).** One a week, 4 in all: expectant, annoyed,
  demanding, then the last one, which takes the silence as a rejection and delivers the same
  blacklisting by mail. That last letter makes the player **Rejected**. If a rewind lands before the
  fourth, the next loop starts over (the scene plays again on the next visit).
- **After Rejected: no letters ever again,** in this loop or any later one.

## Rejected (per save, permanent)

One flag on `MetaState` (survives every loop, never cleared by a reset or a voluntary restart).

- The offer scene never plays again.
- Cashier: refuses service (a short line in the "we reserve the right" spirit).
- Morris, same loop as the rejection: repeats the refusal.
- Morris, any later loop, even on Spring 1: "I'm sorry, the position I mentioned has already been
  filled. We've determined that you aren't to be trusted. Please leave immediately." (Jeff's line,
  verbatim.) This is the first clue he remembers the player across loops.
- The vanilla membership form is never reached: every Morris interaction goes through this quest.
  `JojaMembershipBlock` stays as a safety net for any other path to `JojaSignUp_Yes`.

Cost to the player: JojaMart is closed to them for the rest of the save (no Wednesday seed runs,
nothing Joja-only). Intended: Joja never helps.

## The bad ending (Yes)

A staged event, then a game-over screen, then back to the title **without saving**. The save on
disk is still the morning of that day, undecided, so reloading returns the player to before the
yes. Chosen over a permanent loss (Jeff, 2026-09-25): most players will say yes out of curiosity,
and wiping a deep run for that reads as mean rather than roguelike; SMAPI's daily save backups would
make a permanent loss undoable anyway.

**The scene** (we cannot build a real factory, so it is staged):
- The farm (reworked by Jeff, 2026-09-25, after watching the first build): the farmhouse torn down
  (dust and debris over it, then the building hidden for the rest of the scene), then the whole farm
  paved: real flooring (a concrete-looking floor) laid across the entire map, and **real** coops and
  barns (actual farm buildings, with their proper bases, not sprites drawn on top) in rows side by side
  across the whole farm. **Real** farm animals (cows, sheep, goats, chickens, ducks, pigs only; no
  rabbits, no exotic animals), each housed in one of those buildings, walk in the way they do in the
  evening: the game's own go-home behavior, not scripted lines. The camera pans across all the rows.
  All of it is in memory only: the scene always ends at the title without saving (fail-closed).
- The town: pollution, a green river with dead fish, driftwood all over the beach, a Closed sign
  across Pierre's door.
- Staging uses the mod's own event tools where they exist (`tlyChangeLocation`, `tlyFadeIn`,
  `tlyFadeOut`, the black overlay) plus temporary sprites for the buildings, debris, sign, fish and
  driftwood. Tile positions are taken from gridded screenshots of each map, never guessed (see
  memory `scene-paths-no-guessing`). Every walking route is checked the same way.

**The game-over screen:**
- "Game Over", centered, about a third of the way down.
- In the middle, centered: "The Junimos were forced to abandon the valley, and Joja was left to take
  over unopposed." (Jeff's line, two spelling fixes approved.)
- Bottom third: the farmer in a suit (no hat, black boots; Jeff 2026-09-25 dropped the fedora) standing by Morris, both with glowing red eyes. The
  dark, red-eyed Morris sprite already exists (`MorrisDarkSprite`); the farmer is drawn with borrowed
  clothes and red eyes for this screen only, never touching the save's real appearance.
- A button (and any key) returns to the title. No save is written.

## State

- **Per loop** (reset by every rewind, including a voluntary restart): offer scene seen this loop,
  "come see me" letters sent this loop and the days they fall on, the day the scene was seen (the
  4-week clock), decision letters sent.
- **Per save** (never reset): offer scene ever seen (for skippable), Rejected, the loop number the
  rejection happened in (to tell "same loop" from "later loop" for Morris's line).
- Old saves: all new fields default to "nothing happened yet", so an existing run simply starts
  getting letters.

## Words

Every player-facing line (the offer scene, the cashier, Morris's question and answers, all 12 letter
texts, the refusal) is drafted through the game-writing skill and shown to Jeff verbatim before it
goes in. Jeff's two supplied lines are used as given.

## Testing

- Unit tests (Core): the letter schedule (twice a season, random days, per-loop reset, stops when the
  scene is seen or the player is Rejected), the 4-week decision clock, which Morris line applies
  (undecided / rejected this loop / rejected in an earlier loop), and that a reset clears the per-loop
  state but never Rejected.
- Headless runs with frame capture, like the opening's: the offer scene on first entry, the cashier
  refusal, both answers to Morris, the bad-ending scene end to end, the game-over screen, and that
  the title is reached with no save written.
