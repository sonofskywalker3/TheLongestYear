# Deja-vu phase 2: festival memories

**Date:** 2026-10-09
**Status:** DRAFT. Jeff's rulings of 2026-10-09 are applied (see "Jeff's rulings"). Nothing is coded.
Lines are drafts in `2026-10-09-deja-vu-festival-lines.md` and need Jeff's yes before they go into
i18n.
**Builds on:** phase 1, `2026-08-27-deja-vu-dialogue-design.md` (shipped 0.16.17).
**Credit:** u/Gribbleby (r/StardewValley beta thread) for the idea; already in README Credits from
phase 1. Jeff's own example lines are used verbatim.
**Scope:** PC 1.6, single player. Decompile references are to
`decompiled-pc/Stardew Valley/StardewValley/Event.cs` unless noted.

## Problem

Phase 1 lets a villager the player has invested in across loops say something faintly familiar. It
knows only how much the player talked to someone, never what happened. Festivals are the best
material the loop has: the same eight days come round every year, and the player did something
specific at each one. Jeff's examples (TODO.md, 2026-08-27):

- "Did you come to town for the dance last year?"
- At the Luau: "Just don't put X in the pot this year. Wait... why did I think that?"
- At the Luau, when last loop's ingredient was good: "For some reason I think Y would be amazing if
  you have any."
- (2026-10-09) From a first dance partner four loops later: "I swear we've danced before, but this is
  your first time right?"

The villager does not remember the festival. He or she has a hunch about it, shaped by what the
player did there in an earlier loop.

## Jeff's rulings (2026-10-09)

Answers to the first draft's open questions. Quoted text is Jeff's.

1. **Frequency.** "They're so rare to actually see one right now I think every 2nd loop and beyond
   festival should have like a 20% chance per villager, but once you've seen one that year the
   others get dropped." From the second loop on, every eligible villager rolls 20% at each festival
   (festival day) or on each talk (lead-up week). The first festival memory the player hears for a
   festival spends that festival for the loop: every other villager's line for it is dropped. Festival
   lines have their own budget (one per festival per loop) and do not use phase 1's weekly cap.
   Replaces the draft's 100% festival-day and 15% lead-up chances.
2. **Guaranteed vs rolled.** Rolled, except the special cases in 3.
3. **Familiarity bonuses: yes** (+10 dance partner, +3 Winter Star recipient). Plus:
   - "Make winning the egg hunt ensure that someone remembers you won." The Egg Festival after a win
     always carries the `egghunt.won` memory (see "The egg hunt guarantee").
   - "add a higher percentage chance for a secret santa or dance partner to remember": **50%**.
   - "since those change each loop you need to retain every villager this effects through all loops
     so you can get a 'I swear we've danced before, but this is your first time right?' type response
     from your first dance partner 4 loops later." Meta keeps every dance partner and every Winter
     Star recipient from every loop. Any of them, in any later loop, can remember: in the lead-up,
     at the dance (asked again, or watching you dance with someone else), when the player is his or
     her Secret Santa again, or at the Winter Star itself.
4. **Lewis:** "don't worry about it." No festival-day patch. He speaks only in the Luau lead-up, as
   drafted.
5. **Neutral outcomes stay silent:** "only memorable stuff would leak through."
6. **Out of scope stays out:** the 1.6 Desert Festival, Trout Derby and Squid Fest.

Decisions made here to fit the rulings (flagged for Jeff in "Open questions"):

- The phase 1 familiarity threshold (60) still gates the rolled 20% lines. A stranger should not
  remember you. Dance partners and Winter Star recipients skip it: the shared moment is the
  familiarity, and a random recipient would otherwise almost never qualify.
- The per-festival budget covers the lead-up week and the festival day together, so "once you've
  seen one that year" holds across both.
- The draft's "one festival memory per villager per loop" cap is dropped. The per-festival budget is
  the only cap.

## Non-goals

- No mechanical effect. A memory line never changes a festival outcome, a price, a friendship value
  or a score. (The two familiarity bonuses under "Familiarity" are meta points only.)
- No loop explanation. Same rule as phase 1.
- No multiplayer. Every hook below returns early when `Game1.IsMultiplayer` (memory note: no
  multiplayer work yet).
- No new festivals content (no new events, maps or scripts). The lines ride on dialogue the
  festival already has.
- No 1.6 Desert Festival, Trout Derby or Squid Fest (ruling 6).

## What is recorded

### The record (Core, pure)

```
FestivalMemory
  string Festival      // festival id: "spring13", "spring24", "summer11", "summer28",
                       // "fall16", "fall27", "winter8", "winter25", or "NightMarket"
  int AttendedRun      // RunState.RunNumber of the loop the player last entered it; -1 = never
  int OutcomeRun       // RunNumber the outcome fields came from; -1 = no outcome yet
  string Outcome       // one FestivalOutcome name, see the table below; "" = none
  string ItemId        // qualified item id (Luau ingredient, Winter Star gift); "" = none
  int ItemQuality
  string Npc           // internal name (dance partner, Winter Star recipient); "" = none
  int Score            // eggs, fish or grange points; 0 = none
  int HeardRun         // NEW: RunNumber of the last loop a memory of this festival was heard; -1 = never

BondMemory             // NEW: one per loop per bond, never overwritten
  string Npc           // internal name
  int Run              // RunNumber of the loop it happened in
  string Outcome       // "Danced", or the Winter Star gift outcome (GiftLoved ... GiftHated)
  string ItemId        // Winter Star gift; "" for a dance
  int ItemQuality
```

### Where it lives

- `RunState.FestivalLog : Dictionary<string, FestivalMemory>`: this loop's festivals, written as they
  happen. RunState is saved with the farm, so a reload mid-loop keeps it.
- `MetaState.FestivalMemories : Dictionary<string, FestivalMemory>`: the latest memory of each
  festival from earlier loops.
- **NEW** `MetaState.DancePartners : List<BondMemory>` and `MetaState.WinterStarRecipients :
  List<BondMemory>`: every Flower Dance partner and every Winter Star recipient from every earlier
  loop, oldest first. Nothing is ever removed or replaced, so the first loop's partner is still there
  in loop five. At most one entry per loop each, so the lists stay tiny.

Writing this loop's outcomes into RunState, not straight into meta, is what keeps a memory from
being about the current loop. A line always means "some earlier loop", and a repeat visit to the same
festival on the same day still finds the old memory (meta is not touched until the rewind).

Bond queries (Core, pure, `FestivalBonds`):

- `IsPastPartner(meta, npc)`: any `DancePartners` entry for that villager.
- `IsPastRecipient(meta, npc)`: any `WinterStarRecipients` entry.
- `LatestGift(meta, npc)`: that villager's most recent `WinterStarRecipients` entry (its item and
  outcome pick the liked or disliked line).

### Commit at the rewind

`FestivalMemoryStore.Commit(meta, run)` (Core, pure) runs in `RunController.FinalizeReset` just before
`Run.BeginNewRun(...)` (RunController.cs ~833), so every rewind path (failed gate, Day-28 voluntary
restart, `tly_reset`) goes through it. In this order:

1. **Heard stamps:** for every festival in `RunState.FestivalMemoryHeard`, set the meta record's
   `HeardRun` to this loop. (First, so a new outcome below re-arms the egg guarantee.)
2. **Attendance:** an entry in the log with `AttendedRun` set replaces the meta `AttendedRun`.
3. **Outcome:** an entry with an outcome replaces the meta outcome fields (`Outcome`, `ItemId`,
   `ItemQuality`, `Npc`, `Score`, `OutcomeRun`).
4. **Bonds:** a `spring24` entry with `Danced` appends a `BondMemory` to `DancePartners`; a `winter25`
   entry with a gift outcome appends one to `WinterStarRecipients`. Appended, never merged, so the
   same villager can appear for several loops.
5. A festival missing from the log (skipped, or the loop rewound before it) leaves the meta record
   alone. The old memory stays.

`BeginNewRun` then clears `FestivalLog` and the per-loop stamps below.

Winning the year never rewinds, so nothing is committed; there is no next loop to remember in. The
Year 2 work on `story` can read the same meta later if it wants to.

### Cleared how

Never by a rewind (that is the point). Only by `tly_festmem clear` (debug) or a new farm, which starts
a new MetaState. Turning the feature off in GMCM stops recording and speaking but keeps the records.

### Outcomes per festival

| Festival (id) | Recorded | Outcome names | Memory keys spoken |
|---|---|---|---|
| Egg Festival (`spring13`) | eggs found, won or lost | `EggWon`, `EggLost` | `egghunt.won`, `egghunt.lost` |
| Flower Dance (`spring24`) | attended, partner (or none) | `Danced` (+`Npc`), `NoPartner` | `dance.attended`; from `DancePartners`: `dance.partner` (lead-up), `dance.again`, `dance.other` (festival day) |
| Luau (`summer11`) | ingredient + governor's reaction | `LuauToxic`, `LuauBad`, `LuauOkay`, `LuauGood`, `LuauBest`, `LuauEmpty`, `LuauShorts` | `luau.bad` (Toxic, Bad), `luau.good` (Good, Best), `luau.shorts`; Okay and Empty are silent |
| Moonlight Jellies (`summer28`) | attended | none | `jellies.attended` |
| Fair (`fall16`) | grange score, place, shorts, empty display | `GrangeFirst`, `GrangeSecond`, `GrangeThird`, `GrangeLost`, `GrangeShorts`, `GrangeNone` | `fair.won` (First), `fair.lost` (Second, Third, Lost), `fair.shorts`; None is silent |
| Spirit's Eve (`fall27`) | golden pumpkin found | `Pumpkin` | `spirits.pumpkin`; attending without it is silent |
| Festival of Ice (`winter8`) | fish caught, won or lost | `IceWon`, `IceLost` | `icefish.won`, `icefish.lost` |
| Feast of the Winter Star (`winter25`) | recipient, gift, gift taste | `GiftLoved`, `GiftLiked`, `GiftNeutral`, `GiftDisliked`, `GiftHated` (+`Npc`, `ItemId`) | from `WinterStarRecipients`: `winterstar.liked` (Loved, Liked), `winterstar.disliked` (Disliked, Hated), `winterstar.again` (lead-up); `winterstar.seen` (festival day); a Neutral gift is silent in the lead-up |
| Night Market (`NightMarket`) | attended any of its three nights | none | `nightmarket.attended` |

The classification from raw values (governor level, grange score, gift taste) is a pure Core function,
`FestivalOutcomes.Classify...`, so it is unit-tested without the game.

## Vanilla hooks (recording)

All are Harmony postfixes in one glue class, `FestivalMemoryRecorder`, each guarded by
`RunActivation.IsActive`, the config switch, `!Game1.IsMultiplayer` and `__instance.isFestival`.
Private methods are patched by name through `AccessTools.Method`.

| What | Hook | Reads |
|---|---|---|
| Attendance (every festival) | postfix `Event.setUpPlayerControlSequence(string id)` (line 10947) when `id` is a festival set-up id: `eggFestival`, `flowerFestival`, `luau`, `jellies`, `fair`, `halloween`, `christmas`, `iceFestival` (the `case` labels at 10947 onward; not `eggHunt` / `iceFishing`, which are the contests) | festival id = `__instance.id` minus the `festival_` prefix, as `FestivalTimeFlow.TryGetFestivalEndTime` already does |
| Egg hunt | postfix private `Event.eggHuntWinner()` (12824) | `Game1.player.festivalScore`; won when it is at least 9, vanilla's single-player `numberOfEggsToWin` (12826) |
| Flower Dance partner | postfix `Event.setUpFestivalMainEvent()` (11148), only when `isSpecificFestival("spring24")` | `Game1.player.dancePartner.Value?.TryGetVillager()?.Name`; set when the NPC accepts in `Event.answerDialogueQuestion` (11522 / 11542) |
| Luau soup | postfix private `Event.governorTaste()` (12772) | the governor level from `eventCommands[CurrentCommand + 1]`, which the method sets to `switchEvent governorReaction<N>` (12817), N 0 to 6; the ingredient is `Game1.player.team.luauIngredients[0]` (qualified id, quality). N: 0 toxic, 1 bad, 2 okay, 3 good, 4 best, 5 nothing usable, 6 shorts (`IsItemMayorShorts`) |
| Grange | postfix `Event.interpretGrangeResults()` (11410) | public field `grangeScore` (set by private `judgeGrange`, 11315): 90+ first, 75+ second, 60+ third, else lost; `-666` = shorts; `Game1.player.team.grangeDisplay.Count == 0` = no display (same tests vanilla uses at 11418 to 11436) |
| Golden pumpkin | SMAPI `Player.InventoryChanged`, an added `(O)373` while `Game1.CurrentEvent?.isSpecificFestival("fall27")` | vanilla puts it in a chest at maze tile 33,13 (11028); the inventory event avoids patching the chest |
| Ice fishing | postfix private `Event.iceFishingWinner()` (12889) | `Game1.player.festivalScore`; won when at least 5, vanilla's `numberOfFishToWin` |
| Winter Star gift | prefix + postfix `Event.chooseSecretSantaGift(Item i, Farmer who)` (12476) | the prefix saves `secretSantaRecipient.Name` (the method nulls it at 12499); the postfix records the item and `NPC.getGiftTasteForThisItem(i)` (NPC.cs 1405), mapped to loved/liked/neutral/disliked/hated |
| Night Market | SMAPI `Player.Warped` into `BeachNightMarket` (StardewValley.Locations/BeachNightMarket.cs) while `Utility.IsPassiveFestivalDay("NightMarket")` | attendance only |

Repeat visits: TLY festivals are re-enterable for the rest of their window (FestivalTimeFlow), and
`FestivalMainEventOncePatch` blocks a second main event the same day. So attendance is idempotent,
and egg hunt, ice fishing, dance and grange outcomes can only be recorded once per day. The Luau
taste only runs inside the main event, so it is covered by the same guard. `RunState.BeginNewRun`
already clears the main-event stamp at a rewind, so the next loop's festivals run and record fresh.

Vanilla's own conversation topics (`wonEggHunt`, `wonGrange`, `wonIceFishing`, set through
`autoGenerateActiveDialogueEvent`) belong to the current loop and play after the festival. They do
not conflict: memory lines play only before the festival or before its contest, and the 0.16.8
`FarmerReset` clears active dialogue events at every rewind, so last loop's topics never leak.

## Who says what, and when

### Eligibility (Core, pure: `FestivalMemoryRules`)

A villager may speak a festival memory when all of these hold:

- the feature is on (`EnableDejaVuFestivalMemories`) and phase 1 is on (`EnableDejaVuDialogue`);
- `RunState.FestivalMemoryHeard` does not hold this festival yet (the per-festival budget);
- there is a speakable memory for him or her (the table above);
- **ordinary memories:** the villager's familiarity is at least `DejaVuThreshold` (phase 1's tier 1,
  default 60). Tier 2 adds nothing here;
- **bond memories** (`dance.partner`, `dance.again`, `dance.other`, every `winterstar.*`): the
  villager is in `DancePartners` (dance) or `WinterStarRecipients` (Winter Star). No familiarity
  threshold;
- not the spouse (vanilla swaps in the `_spouse` line), as phase 1 already does.

`CompletedResets >= 1` is implied: loop 1 has no meta record, so there is nothing to say. That is
ruling 1's "every 2nd loop and beyond".

Familiarity is meta and cumulative (phase 1: +1 per talk day, +3 per gift, +10 per heart event), so
60 means roughly two seasons of near-daily talks across all loops so far. In loop 2 only the
villagers the player sees most will clear it. That keeps "a stranger doesn't remember you" true and
is the main reason ordinary lines stay rare; see open question 1.

### The rolls

| Memory | Chance | Who rolls |
|---|---|---|
| Ordinary (every memory not listed below) | `DejaVuFestivalChancePercent`, **20** | every eligible villager |
| Bond (`dance.*` for a past partner, every `winterstar.*`) | `DejaVuFestivalBondChancePercent`, **50** | every past partner or past recipient |
| `egghunt.won` after a win not yet heard | **100** (the guarantee) | every eligible villager |

A villager who could speak both a bond memory and an ordinary one for the same festival (a past
partner in the Flower Dance lead-up) rolls only the bond memory. A past partner never says
`dance.attended`.

**Which line:** the villager's own pool for that memory if there is one, else the fallback pool.
Bond memories with no own line and no fallback stay silent.

**Festival day, one roll per villager per festival per loop.** The roll is made once, when the
festival is set up, from a seed of (`RunNumber`, festival id, villager name), so walking out and back
in the same day gives the same result. A villager who rolls a hit gets the line pushed (below). One
who misses says nothing about it that loop.

**Lead-up week, one roll per talk.** Same rule as the draft, now 20% (or 50% for a bond memory).

### First heard spends the festival

Several villagers can carry a line for the same festival at once. The moment one is heard, the
festival is stamped in `RunState.FestivalMemoryHeard`, and every other villager's unheard line for it
is pulled from his or her stack (`FestivalMemoryInjector.DropUnheard(festival)` walks
`Game1.CurrentEvent.actors` on festival day, or every location's characters in the lead-up). No other
memory for that festival plays again this loop, lead-up or festival day. The stamp is set in a postfix
on `Game1.drawDialogue(NPC speaker)` when the top dialogue's translation key is `TLY.festmem`; each
pushed line carries its festival id so the postfix knows which one to stamp. A line pushed but never
heard (the player left, the auto end fired) does not spend anything.

### The egg hunt guarantee

When the meta `spring13` record is `EggWon` and `HeardRun < OutcomeRun` (that win has not been
remembered yet), the next Egg Festival the player enters pushes `egghunt.won` onto **every** eligible
villager present, no roll. Whoever the player talks to first says it, and the rest are dropped.
Pushing it to everyone, rather than picking one speaker, is what makes it certain: the player does not
have to find the right villager.

- Heard: `HeardRun` is set at the rewind, so the guarantee is used up. A later loop's Egg Festival
  rolls 20% as normal, unless the player wins again, which writes a new `OutcomeRun` and re-arms it.
- Not heard (no eligible villager present, the player skipped the festival or started the hunt
  without talking to anyone): it stays armed for the next loop's Egg Festival.

### Festival day (Egg Festival, Flower Dance, Luau, Jellies, Fair, Spirit's Eve, Festival of Ice, Winter Star)

**Hook:** the same postfix on `Event.setUpPlayerControlSequence`. By then every festival actor has
had its festival line pushed (`Event.addActor`, 5509, via `TryGetFestivalDialogueForYear` /
`setNewDialogue`). The postfix rolls each eligible actor in `__instance.actors` and pushes
`new Dialogue(npc, "TLY.festmem", text)` on top of each winner's `CurrentDialogue`. Talking at a
festival goes through `Event.checkAction` (11631), which draws the top of the stack; `DialogueBox
.closeDialogue` pops it (Menus/DialogueBox.cs 204), so the memory plays on the first talk and the
villager's own festival line on the second. This is the festival equivalent of phase 1's prepend.

Actors never given a line at set-up, because `Event.checkAction` would replace or repeat it:

- the festival host (`Event.festivalHost`, Lewis at every festival here): `checkAction` replaces a
  finished stack with `hostMessageKey` through `setNewDialogue`, which clears it (NPC.cs 4592). Lewis
  speaks only in the Luau lead-up (ruling 4);
- at `spring24`, any dance-capable villager (`datable` or `FlowerDanceCanDance`): while the player has
  no partner, the no-partner branch (11996 to 12100) redraws the top line and rebuilds the stack around
  it, so it would repeat. The two Flower Dance day memories go in at other points (below);
- at `winter25`, `secretSantaRecipient`: talking to him or her opens the gift prompt first (12014);
- the spouse.

**Flower Dance, festival day (NEW).** Both hook a postfix on `Event.answerDialogueQuestion` for the
`danceAsk` answer, only when it set `Game1.player.dancePartner` (11522 / 11542; not the spouse branch):

- `dance.again`: the villager who just said yes is a past partner. On a 50% hit, the memory is appended
  as a second page of the acceptance: `who.CurrentDialogue.Peek().dialogues.Add(new DialogueLine(text))`
  (Dialogue.cs 176). The accept line was drawn by `Game1.drawDialogue(who)` inside the method, and the
  open `DialogueBox` reads the list as it pages, so the memory plays right after "yes". Verify live; if
  the box does not pick it up, push it under the accept line instead so it plays on the next talk.
- `dance.other`: every other past partner among the actors, now that the player has a partner. Once
  `dancePartner` is set, `checkAction` takes the normal path for dance-capable villagers (the
  `else` at 12099 clears only a "..." top, then 12014 onward draws the stack), so a pushed line plays
  once and pops cleanly. Each past partner rolls 50%. `{{partner}}` is the partner's display name.

Both spend the Flower Dance budget when heard. Neither can play if a lead-up line already spent it.

**Winter Star, festival day (NEW).** `winterstar.seen`: every past recipient among the actors except
this year's `secretSantaRecipient` rolls 50% at set-up and gets the line pushed like any festival-day
memory.

**Pre-contest only.** Each contest line is advice about the contest, so it is pulled when the contest
starts: on the `"yes"` in `Event.answerDialogueQuestion` (the same point `FestivalMainEventOncePatch`
stamps), and for the Luau also in a postfix on `Event.addItemToLuauSoup(Item, Farmer)` (12728). An
unheard line is removed from every speaker's stack and the festival counts as spent for the loop.
Fair grange judging also clears every actor's stack itself (`interpretGrangeResults` calls
`setNewDialogue` on all actors), which agrees with this rule. Jellies, Spirit's Eve, Flower Dance and
Winter Star lines are not contest advice and stay until heard or until the player leaves.

### Lead-up week (Flower Dance, Winter Star, Night Market, Luau shorts for Lewis)

These memories need a villager who is not available on the day, or a moment before it (the Night
Market has no festival actors, Lewis is the host, the Secret Santa letter comes before the feast), so
they play in ordinary dialogue in the days before.

**Hook:** phase 1's postfix on `NPC.checkForNewCurrentDialogue`. Before phase 1 rolls, it asks
`FestivalMemoryRules.TryPickLeadUp(meta, run, npc, date, config, rng)`. The window is the
`DejaVuFestivalLeadDays` days before the festival (default **7**, so it never crosses into the season
before; the Night Market window ends the day before its first night). On a hit it pushes
`new Dialogue(npc, "TLY.festmem", text)` the same way phase 1 does, with every phase 1 guard intact
(never over an Introduction or other `activeDialogueEvents` line, never on a festival day, never the
spouse). Chance per talk from "The rolls". The first one heard spends the festival.

Lead-up memories:

- Flower Dance: `dance.partner` (a past partner, 50%), `dance.attended` (anyone else eligible, 20%).
- Winter Star, for a past recipient (50%), first match wins:
  1. `winterstar.again` (NEW): he or she is this loop's recipient. The recipient is known in advance:
     `Utility.GetRandomWinterStarParticipant()` (Utility.cs 5791) is seeded on the game id, year and
     player, and is the same call vanilla uses for the Secret Santa letter (LetterViewerMenu.cs 220) and
     the feast (Event.cs 11032). Only after the player has read the letter
     (`mailReceived` holds `"sawSecretSanta" + Game1.year`, LetterViewerMenu.cs 221), so the line never
     gives the draw away.
  2. `winterstar.liked` / `winterstar.disliked`: from `LatestGift(npc)`. A neutral latest gift is
     silent here.
- Night Market: `nightmarket.attended` (20%).
- Luau: `luau.shorts` from Lewis (20%).

### Interplay with phase 1's caps

- Festival memories have their own budget: one per festival per loop, lead-up and festival day
  together (ruling 1). They do not use phase 1's "one line per villager per loop" slot, and there is
  no separate per-villager cap for festival memories.
- A lead-up line ignores phase 1's seven-day cap but sets it (`DejaVuLastDay`), so a generic deja-vu
  line never lands in the same week as a lead-up memory.
- Festival-day lines do not touch phase 1's caps. Phase 1 never runs inside a festival
  (`Game1.isFestival()` guard), so the two cannot collide.

Worst case per loop: nine festival memories (the eight festivals and the Night Market) plus phase 1's
weekly line. In practice far fewer: with a 20% roll per villager, a festival with three eligible
villagers present carries at least one line about half the time, and the player still has to talk to
that villager.

## Prerequisite: phase 1's per-loop caps never reset (bug found while writing this)

Phase 1's spec says `DejaVuShownTo` and `DejaVuLastDay` reset because "RunState is rebuilt every
loop". It is not: `MetaStore.Run` is created once and the rewind calls `RunState.BeginNewRun`, which
clears neither field (only the debug `tly_dejavu reset` does). Two effects:

1. `DejaVuShownTo` is effectively "one line per villager, ever", not per loop.
2. `DejaVuLastDay` is a `Game1.stats.DaysPlayed` stamp, and DaysPlayed restarts at 1 every loop
   (MountainUnlock.cs comment). After a rewind, `daysPlayed - DejaVuLastDay` is negative, so the
   weekly cap blocks every line until the new loop passes the old loop's last line day plus 7.

Fix (one small master bug-fix commit, ahead of phase 2): clear both in `BeginNewRun`, with a test
that a rewind resets them. Phase 2's new per-loop fields go in the same place.

## Lines and tokens

Keys: `festmem.<memory>.<npc>.<n>` for a villager's own lines, `festmem.<memory>.default.<n>` for the
fallback (memory names as in the outcomes table, e.g. `festmem.luau.bad.gus.1`). A new prefix, not
`dejavu.`, so phase 1's pool discovery never sees them. `FestivalMemoryLines` resolves pools from the
key set like `DejaVuLines`, so adding a line is a JSON edit. Tokens, filled at speak time:

- `{{item}}`: `ItemRegistry.GetData(ItemId).DisplayName` (localised; a removed mod item resolves to
  null, which drops the line);
- `{{partner}}` (NEW, `dance.other` only): this loop's dance partner's display name.

The full draft, 80 lines, is in `2026-10-09-deja-vu-festival-lines.md`.

## Familiarity

Approved by Jeff (ruling 3). The nightly rollup already counts festival talks: `Event.checkAction`
calls `NPC.grantConversationFriendship`, which sets `Friendship.TalkedToToday` (NPC.cs 2835). Two
festival interactions it misses, added to `FamiliarityRollup`:

- dancing with a villager at the Flower Dance: +10 (the same as a heart event);
- the Winter Star gift: +3 (a gift; vanilla calls `receiveGift` with `updateGiftLimitInfo: false`,
  so `GiftsToday` never sees it).

These feed phase 1 and the ordinary festival memories. Bond memories do not need them, since they
skip the threshold.

## Config (GMCM, "Features")

| Setting | Default |
|---|---|
| `EnableDejaVuFestivalMemories` | on (does nothing while `EnableDejaVuDialogue` is off) |
| `DejaVuFestivalChancePercent` | 20 (replaces the draft's `DejaVuFestivalDayChancePercent` 100 and `DejaVuFestivalLeadChancePercent` 15) |
| `DejaVuFestivalBondChancePercent` | 50 |
| `DejaVuFestivalLeadDays` | 7 |

The egg hunt guarantee has no setting.

## Edge cases

- **First loop:** no meta records, no lines. Recording runs from day one.
- **Skipped festival, or the loop rewound before it:** nothing committed for it, so the older memory
  stays and can still be spoken. "Last year" in the lines is vague on purpose.
- **Entered, left before the contest:** attendance is updated, the old outcome stays.
- **Contest run with nothing to remember** (empty soup pot, empty grange display, neutral gift): the
  outcome is recorded and replaces the old one, and it is silent (ruling 5). The latest truth wins.
  A neutral gift still adds the recipient to `WinterStarRecipients`, so `winterstar.again` and
  `winterstar.seen` can still play for him or her.
- **Same partner several loops running:** one `DancePartners` entry per loop. The lines do not count
  loops, so it makes no difference to what is said.
- **Past partner who is also this loop's partner, in the lead-up:** he or she may say `dance.partner`.
  If it is heard, the festival is spent and `dance.again` will not play at the dance.
- **Repeat visit the same day:** attendance and outcomes are idempotent; the seeded roll gives the same
  winners; an unheard festival-day line is pushed again; once one is heard, nothing is pushed.
- **Auto end (`FestivalTimeFlow.ForceEnd`) or leaving mid-line:** the line was never drawn, so it is
  not stamped; nothing else to do.
- **Speaker absent** from this year's festival actors: he or she does not roll that day.
- **Dance partner or gift recipient now married to the player:** spouse is excluded from every memory.
- **Egg win, no eligible villager at the next Egg Festival:** the guarantee waits for the next loop.
- **Mod-added NPCs:** use the fallback lines when eligible. Mod-added festivals are ignored (only the
  nine ids above are recorded).
- **Multiplayer:** every recorder and injector returns early. Nothing is recorded or spoken.
- **Feature off mid-loop:** recording stops; whatever the log holds still commits at the rewind.

## Debug commands

`tly_festmem`, registered beside `tly_dejavu`:

- `status`: meta records (with `HeardRun`), both bond lists, this loop's log, heard stamps, and for the
  next festival each eligible villager, his or her chance and the line key;
- `set <festival> <outcome> [item-or-npc] [score]`: write a meta record (e.g. `set summer11 LuauBad
  (O)248`) with `AttendedRun`/`OutcomeRun` set to the previous loop and `HeardRun` cleared;
- `partner <npc> [run]` (NEW): append a `DancePartners` entry (default: the previous loop);
- `recipient <npc> <item> <outcome> [run]` (NEW): append a `WinterStarRecipients` entry;
- `clear [festival|partners|recipients]`: drop one meta record or bond list, or everything;
- `commit`: run the rewind merge on the current log without a reset (for checking the merge live);
- `force <festival>`: the next festival entry (or, for a lead-up memory, the next talk with an eligible
  villager) treats every roll as a hit, skipping chance and the budget but not the host, datable,
  recipient or spouse guards.

## Testing

Core (xUnit, `TheLongestYear.Tests`):

- `FestivalOutcomes`: governor level 0 to 6 to outcome; grange score bands, -666 and empty display;
  egg and fish win thresholds; gift taste constants to liked/neutral/disliked.
- `FestivalMemoryStore.Commit`: attendance-only entry keeps the old outcome; outcome entry replaces
  all outcome fields; missing festival keeps the record; a silent outcome replaces a speakable one;
  heard stamps set `HeardRun` before the outcome merge (a heard win followed by a new win leaves the
  guarantee armed); a `Danced` entry appends to `DancePartners` and a gift appends to
  `WinterStarRecipients`; five commits with five different partners keep all five, oldest first; the
  same partner twice gives two entries.
- `FestivalBonds`: past partner from loop 1 still found in loop 5; `LatestGift` returns the newest of
  two gifts to the same villager.
- `RunState.BeginNewRun`: clears `FestivalLog`, `FestivalMemoryHeard`, the per-festival pushed-line
  record, and phase 1's `DejaVuShownTo` / `DejaVuLastDay` (the prerequisite fix). Does not touch the
  bond lists (they are meta).
- `FestivalMemoryRules`, with an injected RNG:
  - no record, no line; loop 1, no line;
  - ordinary memory below threshold, no line; at threshold, rolls at 20% (hit at 0.19, miss at 0.20);
  - bond memory below threshold still rolls, at 50%;
  - past partner rolls `dance.partner`, never `dance.attended`;
  - egg guarantee: `EggWon` with `HeardRun < OutcomeRun` gives every eligible villager the line with
    a 0.99 roll; `HeardRun == OutcomeRun` falls back to 20%;
  - festival-day roll is stable for the same (run, festival, villager) and differs across runs;
  - budget: after the festival is stamped heard, no villager gets a line, lead-up or festival day;
  - `winterstar.again` only for this loop's recipient and only after `sawSecretSanta<year>`; else
    liked/disliked from `LatestGift`; a neutral latest gift is silent in the lead-up;
  - `winterstar.seen` never for this loop's recipient;
  - lead-up window bounds (day before inclusive, festival day exclusive, never in the previous
    season); lead-up sets but ignores the weekly cap.
- `FestivalMemoryLines`: pool discovery, fallback, `{{item}}` and `{{partner}}` substitution, null
  item drops the line; the i18n guard covers every `festmem.*` key.

Live (Rodger save, loaded through `tly_loadsave`; game launches labelled whose they are):

1. `tly_dejavu set Gus 200`, `tly_festmem set summer11 LuauBad (O)248`, `force summer11`, move the
   date to Summer 11 with the SMAPI console inject, walk into the Luau, talk to Gus: his `luau.bad`
   line with Garlic plays, then his festival line. Talk to Pierre (also above threshold): no memory,
   the festival is spent. Second run: add an item to the pot first, the line is gone.
2. Without `force`: `tly_festmem status` on festival day lists each eligible villager's roll. Walk out
   and back in: the same villagers carry a line.
3. Egg guarantee: `tly_festmem set spring13 EggWon`, two villagers set above threshold, enter the Egg
   Festival with no `force`: both carry the line, the first one talked to says it, the second does
   not. `commit`, then `status` shows `HeardRun` set and the guarantee off.
4. Dance: `tly_festmem partner Penny 1`, `tly_festmem partner Abigail 2`, Penny and Abigail at 1000+
   friendship, `force spring24`, date to Spring 24. Ask Penny: her accept line, then her `dance.again`
   line on the next page. Second run, ask Abigail: talk to Penny afterwards, her `dance.other` line
   names Abigail.
5. Winter Star: `tly_festmem recipient <this loop's recipient> (O)395 GiftLiked 1`, date to Winter 18,
   read the Secret Santa letter, `force winter25`, talk to him or her: `winterstar.again`. Before
   reading the letter: no line.
6. Run the Luau soup normally, `tly_festmem status` shows the log entry; `tly_festmem commit` moves it
   to meta with the right outcome.

## Docs

README + Nexus (identical content): extend the phase 1 Features bullet ("The town half-remembers")
with a clause about festivals. CHANGELOG Unreleased. No new credit line; Gribbleby is already credited.

## Open questions for Jeff

Answered 2026-10-09 (see "Jeff's rulings"): how rare, guaranteed vs rolled, familiarity bonuses,
Lewis, which festivals, silent outcomes. Still open:

1. **Familiarity threshold.** Ordinary lines still need familiarity 60, bonds skip it. If loop 2 feels
   empty, the fix is a lower festival-only threshold (say 30) rather than a higher chance. Keep 60 for
   now?
2. **One budget for lead-up and festival day.** A past partner who says something in the lead-up week
   uses up the Flower Dance, so he or she will not also say `dance.again` at the dance. Fine, or should
   the dance day have its own budget?
3. **Old memories:** a skipped festival keeps the memory from an older loop (dance and Winter Star
   bonds never fade, by ruling 3). Fine for the others, or should they fade after one loop?
4. **The phase 1 cap bug** (caps never reset at the rewind): ship the fix now as a master bug fix, or
   with phase 2?
5. **The lines:** all 80 in the lines file, especially the 26 new Flower Dance lines.
