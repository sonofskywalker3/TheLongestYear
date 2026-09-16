# The Expanded Opening (design)

Date: 2026-09-16. Branch: `story`. Status: APPROVED by Jeff (sections 1 and 2, 2026-09-16); ready for the
implementation plan.

Replaces section 5.1 ("Opening montage") of `2026-06-06-tly1-story-and-cutscenes-design.md`. That
outline's premise (Joja sent you to sell a farm you did not know was yours) is dropped: vanilla's letter
already hands you the deed and says grandpa left the city for the same reason you are leaving it. The
opening now builds on vanilla instead of fighting it.

## 0. Facts the design rests on

- Vanilla's opening is three parts: the `GrandpaStory` minigame (deathbed speech, then the Joja cubicle
  and the letter), the `Intro` minigame (bus ride), and event `60367` in `Data/Events/BusStop` (Robin
  meets you at the bus, walks you to the farm, Lewis at the door). The deathbed lines and the letter are
  strings in `Strings/StringsFromCSFiles` (`GrandpaStory.cs.12026` to `12040`, letter `12051` and
  `12055`), so a mod can replace the words and keep the pictures.
- **Today the mod forces the vanilla opening off** for every Longest Year farm (`SkipIntroChoicePatch`
  sets `skipIntro = true` whatever the checkbox says) and plays its own porch and Junimo event on the
  first morning (`IntroEventInjector.BuildIntroEvent`, started by `IntroSequenceDriver`). Players have
  never seen grandpa, the letter or Robin under this mod.
- Vanilla's own Skip intro does what Jeff wants of a skip: `TitleMenu.createdNewCharacter(true)` marks
  `60367` seen and wakes the player in bed on Spring 1.
- Grandpa's deathbed speech already says the player's "bright spirit will fade before a growing
  emptiness". In this mod's fiction that emptiness is the darkness, so vanilla hands us the hook.
- The stash chest and the planning statue stand outside the farmhouse by the porch; the three books
  (Cookbook, Craftbook, Bundle Log) are carried furniture items granted at run start (`BookKit`).
- Farm events written for the Standard farm land in front of the house on every farm type (the game
  shifts them by the farmhouse offset), which is how the current intro works on Meadowlands.

## 1. What plays (approved)

**Skip intro off, new farm:**

1. **Deathbed.** Vanilla scene, its eight lines rewritten. Grandpa's "growing emptiness" speech keeps its
   shape and gains one notch of warning: the emptiness is something he knows, not a figure of speech. No
   names, no hall, no spirits.
2. **Cubicle and letter.** Vanilla scene, letter rewritten. The deed and the "I dropped everything" story
   stay. One added paragraph: the old hall in town meant a great deal to him, he did not finish what he
   started there, if it ever needs you, go. Reads as an old man's regret until the Junimos give it a
   second meaning.
3. **Bus stop.** Robin greets you and gets nostalgic about your grandfather holding the town together,
   and how things have frayed since. Morris steps off the same bus behind you, polite and superior, asks
   for the mayor. Robin points him up the road.
4. **The walk.** Robin's vanilla tour lines trimmed, Morris trailing.
5. **Farmhouse.** Lewis welcomes you. Morris states his business in company voice: the Community Center
   is a registered landmark fallen into disrepair, Joja holds an option to buy it that closes Winter 28
   unless the building is restored, and he is the newly assigned manager of the local JojaMart, here to
   see the contract through. Lewis is angry and has no money. Morris, leaving, recognizes you from your
   file: you walked out, the door stays open, he expects to see you back. No red eyes, no menace. A man
   doing a job.
6. **Ask Lewis.** Because of the letter, you ask to see the hall. He takes you.
7. **Community Center.** Lewis says it is beyond saving, leaves you the key, leaves. The Junimos appear:
   who they are, the hall as the town's heart, the loop and its rules. Not one word about grandpa.
8. **Farm tour.** The Junimos on the porch: the stash, the statue, the three books handed over one by
   one with what each does, then "each week the town needs something most" and the theme picker opens.
   Control returns on Spring 1.

**Skip intro on:** vanilla's own skip. No cinematic, no arrival, no tour. The player wakes in bed on
Spring 1 and the picker opens. The existing pop-up at character creation carries the warning that the
opening holds story and mechanics.

**Morris's arc (Jeff, 2026-09-16):** he arrives an unwitting pawn with a contract to enforce and
becomes a willing thrall only at the end, after the town and the farmer thwart the job he was sent to
do. The opening plays him straight. Any glimpse of the darkness on him comes from later scenes, not
this one.

**Grandpa (Jeff, 2026-09-16):** the opening drops three hints (deathbed, letter, Robin) and no more.
Later story beats add a few. The revelation is grandpa's spirit at his shrine in the Year One Ending,
already built on this branch (`2026-09-07-year-one-ending-script.md`, the shrine pan, grandpa's
message, the candle). Year 2 opens with the farmer asking the Junimos what he meant (recorded in
`docs/story-ideas.md`; Year 2 is not on this branch).

## 2. How it is built (approved)

### 2.1 Let vanilla play

`SkipIntroChoicePatch` stops forcing `skipIntro = true`. It still records the player's choice in
`IntroSkipChoice` (the new-game load plants the cc-seen flag on a skip, as today) and passes the
checkbox value through unchanged. Off: `GrandpaStory`, `Intro` and the arrival event play. On:
vanilla's skip path, bed, Spring 1, cc-seen flag, picker.

### 2.2 Grandpa's words

An `AssetRequested` editor on `Strings/StringsFromCSFiles`, active when the mod is enabled (every new
farm is a Longest Year farm while it is), replaces:

- `GrandpaStory.cs.12026`, `12028` (the "grandson" / "granddaughter" openers), `12029`, `12030`,
  `12031`, `12034`, `12035`, `12036`, `12038`, `12040`: the deathbed speech.
- `GrandpaStory.cs.12051`, `12055`: the letter (male and female variants; `{0}` is the player's name,
  `{1}` the farm name, `^` a line break, exactly as vanilla).

The pictures, timing and music of both scenes are untouched. The strings live in the mod's `i18n` under
new keys (`opening.grandpa-1` to `-8`, `opening.letter`) so translations follow the usual path.

### 2.3 One arrival event

An `AssetRequested` editor on `Data/Events/BusStop` replaces the value of the vanilla key `60367/u 0`
with the mod's script, keeping the vanilla key so the game's own seen-list, skip path and reset
bookkeeping (`FarmerReset` already marks `60367` seen) keep working. The script is one event that
carries itself through four maps with `changeLocation`, as the current intro and the ending already do:

- **BusStop:** vanilla's bus arrival, Robin's greeting rewritten, Morris added as a temporary actor
  (`addTemporaryActor Morris`, the plain sprite, never `Morris_Dark`) stepping off after the player.
- **Farm:** Robin's walk with trimmed lines and Morris following; Lewis at the door (vanilla tiles, so
  the farmhouse offset places it on every farm type); Morris's business and exit toward town; the
  player's question; Lewis leads.
- **CommunityCenter:** Lewis's lines and exit, then the Junimo scene on the current intro's tiles.
- **Farm again:** the tour on the porch tiles, Junimos as temporary actors beside the stash chest and
  the statue, the three books named in turn, the closing line, `addMailReceived <cc-seen>`, `end`.

The event is not skippable in-scene, as the current intro is not (a skipped event never reaches the
flag). Skipping is the checkbox.

Every speak payload goes through `EndingEventInjector.EventText`, which strips the two characters that
would break the `/`-joined script. `IntroEventInjector.BuildIntroEvent` and its `event.intro.*` keys are
deleted; the new script lives in its own injector class beside the ending's.

### 2.4 The driver

`IntroSequenceDriver` no longer starts an event. It keeps one job: on the first morning of a fresh run,
once the cc-seen flag is present (planted by the event's end or by the skip), open the theme picker.
`IntroGate` / `IntroSequenceDecider` keep their pure logic; the "start the event" branch goes.
`HasSeenIntro` still promotes at first save and suppresses everything on later loops (a rewind never
replays the opening). `tly_replayintro` clears the flags and, in place of "reset to Spring 1", starts
the event in place from the bus stop so the whole opening can be watched without a new farm.

### 2.5 Placement before the tour

The stash chest and statue are placed on save load (`JunimoStashService.PlaceChest`,
`PlanningShrineService.Place`), which under the vanilla chain happens when `Intro` calls
`loadForNewGame` during the bus ride, before the arrival event starts. The plan verifies this order in
the log on a real new farm. The books are already in the pack at run start; the tour names them and
their slot counts. Handing them over visibly (remove and re-add during the event) is a plan-level
choice and must keep `BookKit`'s "exactly one copy" invariant.

### 2.6 Dependency on master

The tour's book lines say each book starts with four free slots. That is the Cookbook and Craftbook
slot rework on master's TODO (4 / 8 / 12 / 16 like the stash), which Jeff wants released before the
story update. The opening's text assumes it; the merge of master into story brings the code.

### 2.7 Out of scope

Mid-year hint scenes, Morris's fall, the Year 2 opener, a custom-drawn cinematic,
any change to vanilla's pictures or music, multiplayer farmhands (the opening is host-side, as today).

## 3. Writing rules

All lines through the game-writing skill in Jeff's register; drafts to Jeff before they land.

- Grandpa, the letter and Robin never say keeper, spirit, darkness, Junimo, loop, hall's heart, or
  anything a first-time player could read as supernatural. Grandpa's warning is about the emptiness he
  felt; the letter's paragraph is regret about an unfinished project; Robin's is a neighbour's memory.
- Morris: courteous, precise, faintly superior, never threatening. The Winter 28 date is contract
  language. His line to the farmer is recruiting, not scorn.
- Lewis: frustrated and broke, no speeches about landmarks; the player asks, he answers.
- Junimos: the current intro's explanation of the loop, trimmed to what the player must know before
  day one (season shares, the unwind, what carries over), and the tour's plain "this is what this does"
  for the stash, the statue and each book. No grandpa.
- Male and female variants only where vanilla has them (the two grandpa openers, the letter's "my boy"
  / "my dear").

## 4. Testing

- Unit: the skip patch passes the checkbox through and still records the choice; every replaced string
  key has a value and keeps vanilla's `{0}` / `{1}` placeholders; the event script contains no `"` or
  `/` inside a speak payload, visits BusStop, Farm, CommunityCenter, Farm in that order, and ends with
  the cc-seen flag; the driver opens the picker on the flag and never starts an event; a reset never
  re-arms the opening. The blind guard and i18n guard tests keep applying.
- Headless: `tools/farmtype-intro.ps1` extended to the full opening on Standard and Meadowlands with the
  checkbox off (deathbed and cubicle are minigames, so the script waits for the bus-stop event to start,
  then steps it with `tly_eventstep`), and once with the checkbox on (bed, picker, no event). Log checks:
  the stash and statue placed before the event starts; Morris present at the bus stop and gone before
  the Community Center; the picker opens once; a `tly_reset` after the opening shows no intro.
- Jeff's own pass on a real new farm before the story release, with a female farmer once for the letter
  variant.

## 5. Open items carried elsewhere

- Mid-year hint scenes and Morris's fall: future story spec.
- The Year 2 opening question (the farmer asks the Junimos what grandpa meant): `docs/story-ideas.md`.
  The revelation itself is built: the ending's shrine scene.
- Cookbook and Craftbook slots: master TODO (release first).
