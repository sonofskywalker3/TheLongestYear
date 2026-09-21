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
- The morning popups stay exactly as they are.
- The morning Junimo tamper scene ("@, the {{old}} are tainted now. All of them.") stays as it is.
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
scene is due, or the scene is skipped, cannot play, or throws, Apply runs at once, as today. Apply
is idempotent per night so a skip mid-scene cannot double it. The morning report does not change.

### The overnight slot

`FarmEventSuppressionPatch` already postfixes `Utility.pickFarmEvent`. A sibling postfix returns
our `FarmEvent` when a scene is due tonight:

- A vanilla random event picked for the same night (fairy, witch, meteorite, owl, capsule) is
  dropped. They are random and come round again.
- A wedding, a `WorldChangeEvent` (the Community Center's own repairs, the Joja ones) or any event
  the patch does not recognise as random wins. Our scene is not played; the strike applies at once
  and that kind's scene waits for its next strike this loop.
- Fail nights are already suppressed and stay so. The strike itself never runs on day 28.

Each scene is a class implementing vanilla's `FarmEvent` (`setUp`, `tickUpdate`, `draw`,
`drawAboveEverything`, `makeChangesToLocation`), the way `WitchEvent` and `FairyEvent` are. They
are not event scripts: they need to be driven by the night's picked targets and drawn over the
sleeping farm.

### Scene 1: the crows (crop blight), about 8 seconds

- The Farm at night. Camera on the largest cluster of the picked crop tiles; a scarecrow in range
  is framed in.
- Crows fly in from the top of the screen, eyes glowing red (the vanilla crow sprite with a
  two-pixel red eye overlay and a small red light). One crow per picked crop, six on screen at
  most; crops outside the frame die at the same beat.
- One crow lands beside the scarecrow. Nothing happens to it.
- Linus walks into the edge of the frame, stops, takes a step back, turns and hurries off the way
  he came.
- The crows peck. Apply: each picked crop becomes a dead crop.
- The crows lift off together. Fade.
- Sound: the vanilla crow caw pitched down. No music.

### Scene 2: the thief (chest blight), about 8 seconds

- Wherever the picked chest is: the Farm, a shed, the cellar, the farmhouse. Any other map the
  chest could be on (**mine**: a chest off the farm plays no scene; the strike applies at once and
  the scene waits for a chest on the farm. The thief walking into the mines or the desert is not
  worth staging).
- **One chest per strike (Jeff, 2026-09-21; a rule change).** Chest blight used to take units one
  at a time across every unwarded chest and placed machine on every map. Now the first unit that
  lands on a chest makes it the night's chest, and no other chest loses anything that night.
  Placed machines are as before: on the levels where they are at stake, several can go in one
  night inside the same budget.
- The scene is staged at one thing: the night's chest if there is one, else one of the machines
  taken. It plays when that thing is on the farm's own maps (the Farm, a shed, the cellar, the
  farmhouse); anything else taken that night goes at the same beat, off screen. A target
  anywhere else gets no scene and the thief waits for his next strike.
- A Shadow Brute walks in from the nearest door or map edge to the chest. The lid opens with the
  vanilla animation and sound. Beat. Apply: the units vanish.
- The Brute turns, looks toward the camera for half a second, eyes red, and runs out the way it
  came. Fade.
- In the farmhouse the farmer is asleep in bed in the shot. The spouse is placed in the bed and the
  children in their beds or crib for the scene, wherever they were standing when the player went
  to sleep. The new day repositions everyone anyway.
- The thief is one of Krobus's people. Krobus is not in the scene. He is the first item for the
  in-season beats spec, which will go over his vanilla beats (and the roommate case).

### Scene 3: the hall (reversion), about 7 seconds

- The Community Center from outside (Town), at night. The windows glow like firelight; shadows
  cross them.
- Shane walks in from the saloon side, stops dead, a small jump, backs away, runs.
- Apply at the jump. The glow holds a beat. Fade.
- The player learns nothing about which room or slot. The popup and the board tell the rest.

### Scene 4: the cloud (tampering, Winter), about 10 seconds

- The world map fills the screen, in its Winter art.
- A dark cloud pours in from the mountain side, spreads over the whole valley, and settles
  thickest over the farm. The Community Center gets no special treatment: the strike is on the
  things the farmer was saving to donate, not on the hall.
- The map dims under it. The low `shadowDie` from the Winter gate scene. Hold. Apply. Fade.
- No witness.

**The dark aura.** From that morning until the loop ends, every item of the tainted type (the
tamper record's old item id) is drawn with a dark aura wherever an item is drawn: inventory,
chests, shop and bundle menus, held overhead. A postfix on `Object.drawInMenu` (and the held-item
draw) keyed on `RunState.TamperRecords`; a dim purple pulse under the sprite (**mine**: the visual;
a first version for Jeff to look at). Cleared with the run at reset.

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
  slot collision with a forced vanilla event and with a Community Center repair night.
- Jeff's pass: sleep into each of the four with `tly_sabotage arm`, talk to Linus and Shane the
  morning after and again three days later, and watch the three gate scenes with the new lines.

## Out of scope

- The rewind costing JP and what losing looks like (back in the 1.0 story update as of 2026-09-21;
  its own spec).
- Story beats inside the seasons, including Krobus (the next spec).
- Any change to strike chances, hit sizes, wards or fairness rules beyond the guarantee.
