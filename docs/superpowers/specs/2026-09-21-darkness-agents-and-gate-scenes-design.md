# Darkness Agents and Gate Scenes: design

**Date:** 2026-09-21
**Status:** draft for Jeff's review. Every line and ruling here is Jeff's from the 2026-09-21
session unless marked **mine**.
**Branch:** `story`
**Changes:** `2026-09-07-season-turn-beats-design.md` (lines, Fall cast, Winter glow),
`2026-09-09-darkness-pushback-design.md` (first-strike letters removed, presentation),
`2026-09-14-darkness-rework-design.md` (a per-loop guarantee added to the roll).
**Session notes:** `docs/superpowers/notes/2026-09-21-story-session-notes.md`

## The tone this serves

The Light is not the good or the kind, and it is not soft. It does what must be done for the most
people and for its own side. The Junimos used up the farmer's grandfather to buy time and will use
the farmer up too. They know what it costs him and hate it, and they do not stop. No line promises
that any of it counts.

Until Winter the darkness cannot touch the valley itself. It sends agents: crows, a thief from the
mines, shadows in the hall. In Winter the cold, the snow and the lack of sun subdue nature, and the
darkness acts directly.

**Writing rule that came out of this session (game-writing rule 11):** no character tells the
player what he just watched. The strike scenes are silent. A line after an event carries only what
the player could not see.

## Part 1: the gate scenes

The three porch scenes keep their place (the morning of Summer 1, Fall 1, Winter 1 after a passed
gate), their staging and their skip rule. The lines change, the Fall scene loses a line, and the
Winter glow holds.

**Amendment, 2026-10-08 (Jeff, Winter save pass item 16): the scenes play on the first Farm arrival,
not on waking.** "It's weird it happens and then I get out of bed." The Continue morning now runs the
normal day start straight away, so the planning hub opens in the farmhouse on waking as on any
week-start morning, and the porch scene is owed (`RunState.PendingSeasonTurn`, persisted). The first
time the farmer then arrives on the Farm by any route (the farmhouse door, the Bus Stop, Forest or
Backwoods edge, a building door, a totem), the scene plays with the tamper scene's trigger and
staging: on the porch behind black while the Junimos are placed, a fade in, and at the end (or a
skip) `tlyReturnTo` puts him back on the tile and facing he arrived at. The rules
(`SeasonTurnArrival`): an arrival while an event, a farm event, a menu or another porch scene is up
keeps it for the next arrival; it is consumed when it starts, so it plays once; a day spent off the
Farm keeps it owed into the next day; a tamper scene owed on the same arrival waits for the next one;
a newer gate's scene replaces an unwatched older one; the armed Year One Ending drops it. A load that
replays a pending day-28 Continue follows the same rule. Why the hub first: the hub was only ever
after the scene because both ran on waking; it does not depend on the scene, and the hub opening on
waking matches every other week-start morning, while the scene waiting would otherwise keep the
hub (and the day) from starting until he stepped out. `tly_seasonturn` replays the scene staged at
the porch when run on the Farm, and moves to the Farm as before from anywhere else.

**Amendment, 2026-10-09 (designer: a Junimo was hidden behind something in a porch scene): the
Junimos stand only on clear tiles.** The marks were fixed offsets from the door ((0,3), (-2,3),
(2,3), (-4,4), the pair shifted one right), and the planning shrine is auto-placed on (-2,3): it hid
the Winter scene's second Junimo whole (the "Junimo under the star statue" in the screenshots was
the shrine itself, a Junimo holding a star), and the stash on (3,3) stood against the right one.
Now every porch scene (the three season turns and the tamper scene) reads the Farm round the porch
when it is staged (`PorchOcclusion`): objects, a big craftable and the tile above it (it is drawn
two tall), furniture with its sprite's full height, buildings other than the farmhouse with their
roofs, resource clumps, bushes and the row above them, a grown tree's canopy, a crop, map walls,
water and Front or AlwaysFront tiles. A Junimo's sprite is 48 pixels, inside its own tile at rest,
and its hops lift it into the tile above, so a mark needs its tile and the tile above free. The
choice (`PorchMarks`, first that fits): the scene's own layout; an even row centred on the
farmer (equal gaps, the same distance either side) on row 3, then row 4, at gaps 2, 3, 1, 4; for
an even count, the same row half a tile off centre with equal gaps and Junimo 0 under the farmer
(four Junimos cannot be centred on whole tiles at gap 1); then each mark to its nearest clear tile.
Never below row 4: at the scene's viewport the speech box covers row 5 (live: a pair moved there
lost its feet under the box). On a fresh Standard farm (shrine on -2, stash on +3) the pair keeps
(+1,3) (-1,3), Fall stands at (0,3) (-1,3) (1,3), Winter at (0,3) (1,3) (-1,3) (2,3); with an extra
chest on (1,3), Summer and the tamper pair stand at (0,3) (-1,3), Fall at (0,3) (-4,3) (4,3), and
Winter falls back to (0,3) (-1,3) (2,3) (-4,4). The log line `Porch scene: door (x,y), N Junimos
at ...` gives the tiles.

### Spring to Summer

The warning scene: the first strike can land that night.

| Who | Line |
| --- | --- |
| Green | Thank you @. You've done well. But something dark has noticed our work here. |
| Orange | We have enough power to hold it back thanks to your gifts, but some attacks may slip through our guard. |
| Green, the save has never been rewound | Prepare yourself, we know you can do this. |
| Green, the save has been rewound at least once | Prepare yourself. We have taken much from you. We must take more, but know that it will end. |

"Rewound at least once" is `MetaState.CompletedResets > 0` (**mine**; any reset counts, a fail
night or a chosen loop-again). This is the only scene with a changed closer; Jeff did not want one
in Fall or Winter.

### Summer to Fall

Three lines, one per Junimo, so green does not speak twice. Three Junimos as today.

| Who | Line |
| --- | --- |
| Green | You have been working hard. We are growing stronger. |
| Orange | It grows stronger as well. As the days grow shorter, it will reach further. |
| Turquoise | You must continue. We will hold back what we can, but the work is yours alone. |

`stopMusic` moves to before the orange line (**mine**; it sat before line 2 and still does).

### Fall to Winter

| Who | Line |
| --- | --- |
| Green | This is the last season, @. If it is to end, it ends here. |
| Orange | The agents of the enemy will not stop, but with the cold and dark of Winter, it may attack directly. |
| Turquoise | It will corrupt whatever it can. To go on, you must mend what it breaks. |
| Green | If you win here, this cycle will be broken. But I fear our war will continue. |

"our war" over "our fight" is **mine**, Jeff to confirm.

Staging change: the purple glow and `shadowDie` still come up before line 2, but the glow is not
released before the last line. It holds through the leave hops and into the fade-out.

### Code

`SeasonTurn.Lines` takes the rewound flag and returns the Summer closer's key
(`event.turn.summer-3` or `event.turn.summer-3-again`); the Fall row list drops to three. The i18n
key `event.turn.fall-4` is removed. `SeasonTurnEventInjector` drops the `stopGlowing` before the
Winter closer. Core tests for both; `I18nGuardTests` walks the new key.

## Part 2: the strike scenes

### What changes and what stays

- The three first-strike villager letters are removed (`SabotageMailService`, `mail.darkness.*`,
  `MetaState.SabotageLettersSent`).
- The morning popups stay exactly as they are. **Amended 2026-10-08 (designer, save 1 pass):** they
  move from the HUD one-liner with the X icon into vanilla's corner message box, the one "The
  spreading weeds have caused damage to the farm" uses (`HUDMessage.ForCornerTextbox`: big text,
  wrapped, no icon, gone on its own after about five seconds, longer for a long one). Several on
  one morning show one after another, never stacked. None shows while a menu (the planning hub, a
  level-up), a dialogue, an event, a strike scene, the Junimos' tamper scene or a fade holds the
  screen; one already up when such a thing starts is taken down and shown again, in full, after
  it. The lines (keys `morning.sabotage.*`; the `hud.sabotage.*` keys are retired):
  - Crows, the designer's own words: "Some strange crows visited the farm last night. They didn't
    eat your crops, but {{count}} have withered away." (one: "but 1 has withered away").
  - Thief: "Some of your things have gone missing overnight: {{items}}." {{items}} lists what was
    taken with counts, the biggest first, five kinds at most and then "N other things" ("6
    Parsnips, 1 bottle of Wine and 3 other things"). Counts and containers come from `AskPhrases`
    (`AskPhrases.Counted`; one reads "1 Parsnip", "1 bottle of Wine").
  - Hall (reversion, or a tamper with no scene to tell it): unchanged, "You awaken with a feeling
    that something is wrong at the Community Center."
    **Amended 2026-10-08 (designer, second pass; replaces a Junimo scene for the hall):** each
    reversion adds one sentence after it in the same box, naming the item and its bundle, no
    quantity: "The {{item}} is gone from the {{bundle}} bundle." For a slot that asked for more
    than one, the bare plural with "are" and no count or container ("The Parsnips are gone from the
    Spring Crops bundle."); a mass noun or container good stays singular with "is" ("The Wild Honey
    is gone ..."), by the tainted line's "all the X" rules (`MorningLines.RevertedItem`). The item
    is the game's display name (flavour included); the bundle is its name as the bundle menu shows
    it (the display name field), and a name already ending in "Bundle" is not doubled ("... from
    the Abigail's Bundle."). Several reversions on one morning: one sentence each. Keys
    `morning.sabotage.reverted.one|other|one-named|other-named`. A tamper with no scene still says
    the hall line alone.
- The Junimo tamper scene keeps its staging and its popup, with two changes (Jeff, 2026-10-07):
  - It no longer plays on waking (that version blinked and warped the farmer to his doorstep). It
    plays the first time the farmer arrives on the Farm by any route while the tamper report waits
    (Jeff, 2026-10-07: "however you get to the farm map, just show the scene, vanilla does this
    too"): the farmhouse door, the Bus Stop, Forest or Backwoods edge, a building door, a totem. Like
    vanilla's Community Center cutscene on entering Town, it is staged at the farmhouse porch (the
    tile the house's own door warp puts him on), behind black while the two Junimos are put on their
    marks, then a fade in; when it ends the farmer is back on the exact tile and facing he arrived
    at (vanilla's event end restores the position recorded when the event started). An arrival
    while another event, a menu, a farm event or a season turn is up keeps it for the next arrival.
    It plays once. The night's other reports still show after it.
  - Its first line is "@, last night the darkness struck! It has tainted all the {{old}}."
  - {{old}} names the exact item the tamper took, flavour included: "all the Dried Apples", not
    "all the Dried Fruit" (designer, 2026-10-07). The tamper only ever takes an item one bundle
    asks for (wiring spec 2026-09-15, Tampering; per bundle, Jeff 2026-10-07: Construction's double
    Wood qualifies). When that bundle asks for it in several slots, every open one is rewritten to
    the same new item and stack and filled ones stay; the line names the item and the per-slot
    count. A later tamper never picks a
    tainted item, in any flavour, as its replacement, so the line stays true: nothing on the board
    wants the tainted thing. The replacement is always new to the board: no slot anywhere asks for
    it, in any flavour.
    **Amended 2026-10-08 (designer, Winter save: the weekly quest still asked for the tainted
    Super Cucumbers):** a weekly goal on a rewritten slot follows it, on both lists of a double
    week: it names the new item, stack and quality, and the quest checklist is rebuilt at once
    (`TamperRule.FollowGoals`). Its theme week discount is dropped, so the week-end revert cannot
    put the old item's full ask back onto the new item's slot.
  - Plurals are right: {{old}} is the item's plural (the game's own pluralizer, corrected for
    mass nouns like Beer, Wool, Hay and Honey, also as the last word of a name such as Blueberry
    Jelly; a flavoured Dried Fruit or Smoked Fish keeps the game's own name, which already reads
    right: "Dried Apples", "Smoked Salmon"). {{new}} is the bare name for one. For more than one,
    a liquid or spread names the container its sprite shows, before the full name ("3 bottles of
    Blueberry Wine", "3 jars of Wild Honey", "3 mugs of Beer", "3 glasses of Pale Ale", "3 cups of
    Coffee", "3 tins of Caviar", "3 clusters of Salmon Roe", "3 bowls of Pumpkin Soup"); a
    countable thing takes its plural ("3 Parsnips", "3 Sea Jellies"); bulk stuff with no container
    stays bare ("3 Clay", "3 Wool", "3 Hay"), or keeps the game's own phrase ("3 lumps of Coal").
    Every fish (Object category -4) keeps the same word (designer, 2026-10-07): "7 Pike", "7
    Salmon", "3 Largemouth Bass", and "all the Pike" in the tainted line; the jellies still count
    ("Sea Jellies") and Roe, Aged Roe, Caviar and Smoked Fish keep their containers or bare word.
    The crab-pot and beach shellfish take their plurals ("7 Oysters", "7 Clams", "7 Crabs", "7
    Lobsters", "7 Snails", "7 Mussels", "7 Cockles", "7 Periwinkles", "all the Mussels"); Shrimp
    and Crayfish keep the same word. Amended 2026-10-08: Super Cucumber and Sea Cucumber are fish
    but take normal plurals too ("all the Super Cucumbers", "all the Sea Cucumbers", "7 Sea
    Cucumbers"); live, the tamper line had said "all the Super Cucumber". Holly comes in sprigs: "8 sprigs of Holly", "all the Holly".
    The table lives in `AskPhrases`. A plural ask, container phrases included, ends "They remain
    pure." (key `event.darkness.tamper-3-plural`) instead of "It remains pure.".
    **Amended 2026-10-09 (designer: "Bring us Nautilus Shell instead" should read "Bring us a
    Nautilus Shell instead").** One countable item takes "a" or "an" (by its first letter: "an"
    before a vowel letter; no vanilla Object name needs an exception, the one U name, Unmilled
    Rice, being a mass noun). A mass noun stays bare ("Bring us Clay instead", "Wool", "Hay",
    "Honey", "Blueberry Wine", "Pumpkin Soup": anything with a container, the bulk-stuff table and
    the mass-noun names), and so does a name that is already plural ("Hops", "Pickles", "Dried
    Apples") and a word the game counts in pieces ("Coal", "Wheat"). A fish, shellfish or jelly is
    counted ("a Pike", "an Oyster", "a Sea Jelly"), and so are Smoked Fish, Baked Fish and Dish O'
    The Sea ("a Smoked Salmon"). In a language the game does not pluralize, one item stays bare.
    `AskPhrases.One`; {{new}} is only used by the tamper scene's last line.
  - No purple screen glow under the middle line; the low sound stays.
  It names the item, which the player could not know.
- New: an overnight scene per kind of strike, in the slot vanilla uses for the fairy and the witch.

### When a scene plays

The first strike of each kind in a loop plays its scene. Later strikes of that kind in the same
loop are popup only. Four scenes a loop at most.

A scene cannot be skipped the first time that kind has ever played on the save. After that a click
skips it. `MetaState.StrikeScenesSeen` (a set of kind names, permanent) and
`RunState.StrikeScenesPlayed` (the same, cleared at reset).

A second tamper in one Winter (possible on the harder dials) gets the popup and the Junimo scene,
not the cloud.

### The guarantee: every kind strikes at least once every loop

A guarantee, not a rising chance (a rising chance bunches strikes late in the season, which is the
pile-on the rework removed).

Each kind has a debut season: crow blight and chest blight in Summer, reversion in Fall. If a kind
has not struck by the start of week 3 of its debut season, it is forced in the first half of week
3 (Jeff: it gives the player more time to pivot). The night of day 15 is the first forced night.
If the kind cannot act that night (see below), or two kinds are owed and only one event can strike
a night, the force carries to the next night it can act (**mine**), so crows and thief both owed
land on days 15 and 16. A forced strike is that week's strike like any other: the chance drops 5 points and the weekly caps
count it. Tampering keeps its own rule (Winter 1 the first time, a random night of week 1 after).

A guarantee never overrides a rule that says the kind cannot act:

- Crops warded for the season (Ward of the Fields): no crows that season. The ward buys a season
  without them.
- Nothing stored outside a Circle of Warding or the Junimo Stash: no thief.
- No filled slot in an unfinished bundle, or no fair candidate on Easy and Normal: no reversion.

In those cases the kind simply does not debut that loop. Pure rule in `SabotageSchedule`, unit
tested.

### Picking at dusk, striking in the scene

Today `SabotageService.RunNight` picks and applies at day end, before the overnight slot, so the
crops would already be gone when the crows land. `NightPlan.Execute` splits in two:

- **Pick** (day end): choose the event and its exact targets (the crop tiles, the chest and the
  units, the slot, the tamper plan). Stored on the service for the night, not persisted.
- **Apply**: do the damage and write the morning report.

When a scene is due, the scene calls Apply at its beat (the crow pecks, the lid opens). When no
scene is due (its kind already played this loop), Apply runs at once, as today: that strike has no
scene by design. Apply is idempotent per night so a skip mid-scene cannot double it. The morning
report does not change.

**Conflict or broken (Jeff, 2026-10-08: "If there's a CONFLICT and a different scene runs, we push
back one day. If the SYSTEM is broken and a scene CAN'T run ever, then they miss out on the cool
scene, but still get hit.").** Two different things can keep a due scene from playing, and they
are handled differently:

- **A conflict pushes the strike back.** Another event owns tonight's overnight slot (a Wildcard
  night_event twist, a `WorldChangeEvent`, a wedding, a birth or other personal farm event, another
  mod's `farmEventOverride`; see the overnight slot below), or another mod replaces our scene after
  it was handed the slot, so our `setUp` never runs. The strike is postponed: neither its effect nor
  its scene happens that night, and nothing is recorded, so the week's chance does not drop, no cap
  slot or tamper spacing is spent, and the every-loop guarantee still owes the kind. It is queued
  and fires with its scene on the next free night.
- **A broken scene lands the strike bare.** The scene cannot run: at the pick its staging check
  fails (the crows need the Farm map, the hall needs Town, the cloud needs the farm on a world map
  region with a base texture), or it cannot show tonight's pick, or a check throws; or later its
  `Stage()` says no, or `setUp` throws before the scene is staged (a missing sprite, texture or map
  data). The strike's effect lands now, with no scene, and is recorded like any landed strike
  (chance drop, cap or spacing, `StruckEvents`, the every-loop guarantee, the guaranteed Winter
  tamper). It is never queued. The scene was not shown, so it stays due for a later strike of that
  kind. The morning HUD lines are the usual ones; a tamper that landed without its cloud still gets
  its Junimo scene at the porch, which is a separate scene.

A scene that cannot stage never stops its kind from acting: the night's "can act" test does not ask
about the scene. The one exception is the thief's draw, which the designer chose separately: while
his scene is due he draws only chests he can be filmed at (Scene 2). If the chosen chest still
cannot be staged, the theft lands from that chest with no scene.

The run records a strike only when it commits: its scene stages, or it lands now (no scene by
design, or bare). Each strike lands exactly once on every path, and nothing lands bare on a
conflict. The pure rules are `StrikeStaging.AtPick` (wait for the scene, land by design, or land
bare), `StrikeLifecycle` (handed the slot, setUp ran, committed, applied, postponed) and its
`AtNet` for the nets under the save, the morning and the next night pass: a scene that staged but
ended before its beat lands there; a setUp that ran but never staged lands bare; a scene handed the
slot whose setUp never ran was replaced (a conflict, queued); a strike never handed the slot (a fail
or restart night, or the first night of a save, when vanilla runs no `pickFarmEvent`) is postponed
without queuing.

**The queue (designer, 2026-10-07).** Only a conflict queues. Its kind goes on a queue kept on the
run (`RunState.QueuedStrikes`, cleared at the loop reset, empty on older saves). Nothing about the
pick is kept: on the night it fires it is planned afresh and fairly. On every later night pass the
oldest queued kind that can act tonight fires instead of the normal roll, with its scene (or bare,
if its scene has since broken), as that night's one strike, recorded like any strike. It honours
everything the kind's own "can act" test does: caps, the spacing between tampers, wards, quiet
days, nothing fair to take. A queued kind that cannot act tonight stays queued and the night rolls
normally. It leaves the queue only when it commits; a night whose slot is taken again postpones it
and it stays queued. A debug arm still takes precedence; the every-loop guarantee waits behind the
queue. A guaranteed Winter tamper postponed by a conflict keeps its own carry instead: it stays
owed past week 1, every night until it lands; when it commits it also clears a queued Tampering.
The order of the night's sources is `NightPrecedence`; the queue is `StrikeQueue`; the slot
decision is `StrikeSlot.Decide`; the record is `StrikeLedger.Record`.

### The overnight slot

`FarmEventSuppressionPatch` already postfixes `Utility.pickFarmEvent`. A sibling postfix returns
our `FarmEvent` when a scene is due tonight:

- A vanilla random event picked for the same night (fairy, witch, meteorite, owl, capsule) is
  dropped. They are random and come round again.
- A wedding, a `WorldChangeEvent` (the Community Center's own repairs, the Joja ones), another
  mod's farm event override, a personal farm event (a birth, a pregnancy question), a Wildcard
  night_event twist, or any event the patch does not recognise as random wins. That is a conflict:
  the strike is postponed, effect and scene both (see above), and queued: it fires on the next free
  night with its scene.
- Fail nights are already suppressed and stay so. The strike itself never runs on day 28.

Each scene is a class implementing vanilla's `FarmEvent` (`setUp`, `tickUpdate`, `draw`,
`drawAboveEverything`, `makeChangesToLocation`), the way `WitchEvent` and `FairyEvent` are. They
are not event scripts: they need to be driven by the night's picked targets and drawn over the
sleeping farm.

### Scene 1: the crows (crop blight), about 8 seconds

- The Farm at night. Camera on the largest cluster of the picked crop tiles; a scarecrow in range
  is framed in.
- Crows fly in from the top of the screen, eyes glowing red (the vanilla crow sprite with a
  two-pixel red eye overlay and a small red light). **Amended 2026-10-08 (designer: "they dropped
  straight down onto the field. They must swoop in from the sides"):** each crow enters off the
  left or right edge of the frame, a few tiles above its crop, the flock alternating edges in its
  stagger, and flies a curve that dives and then levels out, fast on entry and braking to land
  (`CrowSwoop`). It faces the way it flies, then turns to the field once down. The scarecrow crow
  flies in toward the scarecrow. One crow per picked crop, six on screen at
  most; crops outside the frame die at the same beat.
- One crow lands beside the scarecrow. Nothing happens to it. **Amended 2026-10-08 (designer,
  second pass):** it lands ON the scarecrow, perched on its hat, facing the crops, with no ground
  shadow (`CrowPerch.OnHat`). However many scarecrows stand near, only ONE gets a crow: the nearest
  to the patch centre that still fits in the frame with the patch (`CrowPerch.ChooseScarecrow`).
- Linus walks into the edge of the frame, stops, takes a step back, turns and hurries off the way
  he came.
- The crows peck. Apply: each picked crop becomes a dead crop. **Amended 2026-10-08 (designer,
  second pass):** each crop crow pecks on its own random timing for the whole time it is down
  (`CrowPerch.PeckTimes`: a short look about after landing, then single pecks and quick doubles
  with random pauses), never in step with the others; the scarecrow crow just stands. The crops
  still die at the strike beat and the flock still lifts off together.
- The crows lift off together. Fade.
- Sound: the vanilla crow caw pitched down. No music.

### Scene 2: the thief (chest blight), about 8 seconds

- Wherever the picked chest is: the Farm, a shed, the cellar, the farmhouse. **While the thief
  scene is still due this loop, the chest draw only holds chests on those maps (designer,
  2026-10-07, option a)**, so the first theft always has its scene; a barn, coop, greenhouse or
  island chest is safe until then, and so is a chest with no tile beside it he could stand on (a
  Junimo group counts only through a chest he can reach; a placed machine likewise). If no chest
  there has anything to take, the thief cannot act
  that night (the every-loop guarantee and the queue keep him owed). Once his scene has played,
  every chest is in the draw again and later thefts there land with no scene, by design.
- **Junimo Chests are chests (Jeff, 2026-10-07).** Every Junimo Chest shows one shared inventory,
  so they are ONE chest in the draw: the stock is weighted once, a unit taken is gone from all of
  them, and the scene is staged at a Junimo Chest on the farm's maps when there is one. While the
  scene is due the shared stock is in the draw only when one of its chests stands on those maps. A
  ward on any of them protects the shared stock. Mini-Shipping Bins stay out (they ship overnight).
- **One chest per strike (Jeff, 2026-09-21; a rule change).** Chest blight used to take units one
  at a time across every unwarded chest and placed machine on every map. Now the first unit that
  lands on a chest makes it the night's chest, and no other chest loses anything that night.
  Placed machines are as before: on the levels where they are at stake, several can go in one
  night inside the same budget.
- The scene is staged at one thing: the night's chest if there is one, else one of the machines
  taken. It plays when that thing is on the farm's own maps (the Farm, a shed, the cellar, the
  farmhouse); anything else taken that night goes at the same beat, off screen. Placed machines
  are only ever at stake on the Farm itself, which the scene shows.
- A Shadow Brute walks in from the nearest door or map edge to the chest. The lid opens with the
  vanilla animation and sound. Beat. Apply: the units vanish.
- **Nothing spoils (designer, 2026-10-08: a thief steals).** Chest blight used to split what it took
  into food that "spoiled" and the rest that "went missing". Every unit it takes is now stolen,
  food included, and the morning box lists it. Nothing else in the mod spoiled things.
- **Nothing hides the chest (designer, 2026-10-08: on save 1 it was behind a tree).** For the scene,
  anything drawn in front of the chest, its lid or the thief's walk (and his head above it) goes
  see-through the way vanilla fades a tree with the farmer behind it: grown trees and fruit trees
  and buildings through their own alpha, bushes through a tint on their draw. The regions are
  vanilla's own fade boxes (`SceneSeeThrough`). All back to full opacity when the scene ends.
  `tly_sabotage fixture` also clears any tree or bush in front of its chest.
- The Brute turns, looks toward the camera for half a second, eyes red, and runs out the way it
  came. Fade.
- In the farmhouse the farmer is asleep in bed in the shot. The spouse is placed in the bed and the
  children in their beds or crib for the scene, wherever they were standing when the player went
  to sleep. The new day repositions everyone anyway.
- The thief is one of Krobus's people. Krobus is not in the scene. He is the first item for the
  in-season beats spec, which will go over his vanilla beats (and the roommate case).

### Scene 3: the hall (reversion), about 7 seconds

- The Community Center from outside (Town), at night. The windows glow like firelight, and the
  silhouettes of men cross them.
- Shane walks in from the saloon side, stops dead, a small jump, backs away, runs.
- Apply at the jump. The glow holds a beat. Fade.
- The player learns nothing about which room or slot. The popup and the board tell the rest.
- **Amended 2026-10-08 (designer, second pass):** no Junimo scene for the hall. The morning box
  names the item and bundle (see the morning popups above), and the emptied slot is marked in the
  bundle menu: its icon in the page's ingredient list sits in the tainted aura's purple glow until
  the slot is filled again. The marks are `RunState.DarkenedSlots` (rules `DarkenedSlots`, cleared
  at the loop reset, a filled slot's mark dropped at day start and never drawn once the ingredient
  is completed on the open page). Vanilla and TLY boards both use the vanilla bundle menu, whose
  icons are numbered by ingredient index.

**Changed by Jeff on 2026-09-23, after seeing the first version.** These override the lines above
where they differ.

- The shadows were too big. Each one reads as a man's silhouette: head, shoulders and body, roughly
  human proportioned for the window, and smaller than before.
- The light was too bright. Both the painted glow on the glass and the pools of light it casts
  are turned down.
- Shane is not going into or up to the Community Center. It is night and he is walking past it to
  clear his head before he heads home. He walks the town's real paths (the game's own pathfinding
  over Town, not a hand-drawn line) along the front of the hall, notices it, and the stop, the
  jump and the backing away all happen on the path, not at the door. Then he carries on toward
  home, the direction of Marnie's ranch, and out of the frame. The camera may move if the facade
  and his path past it need it to share the frame.

**Changed again, 2026-09-25 to 2026-10-07 (Jeff, and the controller's rulings on them).** These
override everything above where they differ.

- No made up paths. Every tile Shane steps on is a path tile on Town's own map (Back layer Type
  Dirt, Stone or Wood, nothing standing on it), checked against an export of the map in the unit
  tests and again against the live map before the scene uses the route. A tile that fails is
  re-routed over path tiles only; if no path route exists he is left out of the scene.
- His route: up from the riverside path and over the wooden bridge east of the hall, west along
  the dirt road below the hall (row 28), stopping level with the hall below its right hand window
  at (57,28). He never sets foot on the cobbled approach to the door or the lawn. After the jump
  he backs two tiles down the road, then hurries home down the road's south west arm toward the
  square, which is his way back through town to Marnie's ranch, where he lives.
- The scene runs ten seconds (the cap). The camera opens on the whole facade and tilts down to the
  road, keeping the windows in the top of the frame. He walks at vanilla's walking pace (533 ms a
  tile) and hurries off at 320 ms a tile. After he has gone the shot holds on the lit windows.
- The silhouettes of men are gone (Jeff: "about as intimidating as a mens room silhouette").
  There is ONE figure, a Shadow Brute from the mines (the controller's pick; the Shadow Shaman was
  the fallback and was not needed), drawn solid black with no interior detail and cut at the glass.
  It is NOT looking out at Shane: it walks into the right hand window side-on and then works
  there, turned from the street, leaning in and reaching with its hands in a slow loop. The
  reversion is what it is doing. Shane's fright is at seeing a dark shape busy inside, not at being
  watched.
- The firelight is unchanged: same colour, same dimness, same fill on the glass's texel grid.

### Scene 4: the cloud (tampering, Winter), about 12 seconds

- The world map in its Winter art, at the size the in-game map tab draws it and inside the map
  tab's own frame, centred on a very dark night backdrop (Jeff, 2026-10-07: the map blown up to
  fill the screen "looks bad"; the filled version stays behind one switch).
- A dark cloud drifts in slowly from the north, every blob entering from above the top of the
  screen (none starts on screen), and settles sort of everywhere: evenly over the whole screen,
  frame and backdrop included, forest, mountains, water, the desert edge, empty land and the
  margins alike, never clustered on the town or any settlement. It lies a little thicker on the
  farm, which is the darkest place on the map. The Community Center gets no special treatment:
  the strike is on the things the farmer was saving to donate, not on the hall. (Jeff,
  2026-10-07, after the first frames: less piled on the farm, more everywhere else, and a slow
  drift rather than a dart.)
- Everything dims under it. The low `shadowDie` from the Winter gate scene. Hold. Apply. Fade.
- No witness.

**The dark aura.** From that morning until the loop ends, every copy of the exact item tainted
(the tamper record's old item id and its flavour, `TamperRecord.OldFlavor`) is drawn with a dark
aura wherever an item is drawn: inventory, chests, shop and bundle menus, held overhead. Tainted
Dried Apples glow; Dried Cucumbers, another flavour of the same item, do not (designer,
2026-10-07). The flavour is matched against the item's `preservedParentSheetIndex`. A record with
no flavour (a slot that named none and took any, or a record saved before the field existed) marks
every copy of its item id, as the aura always did. A plain Potato's taint never marks Pickled
Potato: that is another item id. A prefix on `Object.drawInMenu` (and the held-item draw) keyed on
`RunState.Tampers` through `TaintedItems`, allocation-free per draw; a dim purple pulse under the
sprite (**mine**: the visual; a first version for Jeff to look at). Cleared with the run at reset.

A rewritten slot forgets its flavour: the tamper removes the slot's entry from
`MetaState.WrittenBoardFlavors`, so `FlavoredSlotPatch` cannot pin the old fruit onto the new ask.

### The witnesses

Linus (crows) and Shane (hall) only. Nobody but the player sees the thief or the cloud; Lewis's
letter is dropped.

For 7 days after the scene, the first time the player talks to the witness he says his line in
place of his normal dialogue, once. It happens every loop the scene plays, because the town forgets
and the witness is shaken afresh. If the scene was skipped it still counts; if no scene played
(popup-only strike), there is no witness line.

`{{when}}` is "last night" on the morning after the scene and "the other night" on any later day.

| Who | Line |
| --- | --- |
| Linus | I was out walking {{when}} and I passed your farm. I've never seen crows like that before. I haven't slept well since. |
| Shane | When I left the bar {{when}} I could swear I saw something moving in the old Community Center. I'd had a few drinks, but not that many. |

Mechanics: `RunState.WitnessLines` holds (npc, scene day, said). On day start inside the window,
an unsaid line is pushed onto the top of the NPC's dialogue stack; saying it marks it said; the
window closing drops it. Keys `dialogue.witness.linus`, `dialogue.witness.shane`,
`dialogue.witness.when-last-night`, `dialogue.witness.when-other-night`.

**Amended 2026-10-08 (designer, save 1: Linus had never been talked to, his introduction played and
the line did not).** Vanilla's Introduction topic (and any unseen conversation topic) clears the
NPC's dialogue stack before it pushes itself, which threw the line away. Now the line follows
whatever dialogue displaced it, in the same conversation: the introduction, then the witness line.
A location line pushed on top of it is handled the same way. Rule `WitnessLines.FollowsTopic`; a
postfix on `NPC.checkForNewCurrentDialogue` does it.

**Amended 2026-10-08 (designer, second pass: the log said the line followed the introduction, and
it never showed).** The first fix opened the line 200 ms after the introduction's `onFinish`. That
fires on the last page while the box is still shrinking away; the line was pushed on top of the
not yet popped introduction, and `DialogueBox.closeDialogue` pops the TOP of the speaker's stack,
so it popped the line. Now the postfix puts the line straight UNDER the introduction on the stack
(`WitnessLines.PlaceUnderTop`): vanilla's close pops the introduction and leaves the line next.
When the introduction's box closes (`Display.MenuChanged`, after `closeDialogue` has run), the line
opens at once if nothing else took the screen, no event is up and the player is still there
(`WitnessLines.OpensAfterTopic`); otherwise it waits on his stack and vanilla shows it on the next
talk. Headless check: `tly_witness fresh <npc>` (marks him unmet), `tly_witness arm <npc>`,
`tly_witness talk <npc>` (his real `checkAction`), `tly_witness click` to page on.

**Amended 2026-10-09 (designer: "I told you to just leave it, if they haven't talked to the
villager yet, it can be pushed back a day").** Both follow-up fixes above are removed (the
`checkForNewCurrentDialogue` patch, the MenuChanged opener, `FollowsTopic`, `PlaceUnderTop`,
`OpensAfterTopic`, `WaitsForTopicToFinish`). The rule now: on a morning when the player has not
met the witness yet (never talked to, or his Introduction still owed: the `Introduction` dialogue
event active and his `<Name>_Introduction` flag missing), the line is not pushed that day, and
vanilla's introduction plays alone, however many talks it takes (Linus's is two). From the next
morning the line is offered as before, once. The window: a line held for an introduction gets
one more day (7 + 1 after the scene), the day he was met; on the other held mornings the player
did not talk to him at all, so they cost nothing (`WitnessLines.Decide`, `WitnessRecord.
HeldForIntroduction`). The other unseen conversation topics that clear a stack still clear the
line for that day; it comes back the next morning while the window lasts. Live check 2026-10-09,
throwaway Standard farm: never-met Linus with an armed line, first talk "A stranger?... Hello.",
second talk "Don't mind me. I just live out here alone.", nothing more that day; next morning his
first talk is the crows line. `tly_witness arm` makes the morning's call at once (Hold or Say).

### Debug

`tly_sabotage scene <crows|thief|hall|cloud>` plays a scene now against a fresh pick (or a staged
fixture when there is nothing to pick), without sleeping. `tly_sabotage arm` keeps working and now
leads into the real scene. `tly_sabotage status` lists scenes played this loop, scenes seen on the
save, and pending witness lines.

## Testing

- Core: the guarantee (each debut season, the week-3 force (day 15, carrying forward), every can't-act exemption, the chance
  drop and caps counting a forced strike), the scene-due rule (first per loop, skippable after
  first per save), the pick and apply split being idempotent, the witness window and the `{{when}}`
  switch, the Summer closer by rewound flag.
- Live, my automated runs: each scene through `tly_sabotage scene` on a developed throwaway farm,
  screenshots at each beat; the thief in the farmhouse on a married save with a child; the overnight
  slot collision with a forced vanilla event and with a Community Center repair night (the strike
  is postponed and queued, then fires with its scene on the next free night with nothing armed); a
  scene broken on purpose (`tly_sabotage breakscene`) at the pick and in setUp lands its strike with
  no scene, with the usual morning HUD line, and the scene stays due;
  a thief armed with a farm chest and a barn chest while his scene is due takes from the farm chest.
- Jeff's pass: sleep into each of the four with `tly_sabotage arm`, talk to Linus and Shane the
  morning after and again three days later, and watch the three gate scenes with the new lines.

## Out of scope

- The rewind costing JP and what losing looks like (back in the 1.0 story update as of 2026-09-21;
  its own spec).
- Story beats inside the seasons, including Krobus (the next spec).
- Any change to strike chances, hit sizes, wards or fairness rules beyond the guarantee.
