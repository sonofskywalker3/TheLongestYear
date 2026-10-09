# Deja-vu phase 2: festival memories

**Date:** 2026-10-09
**Status:** DRAFT for Jeff's review. Nothing is coded. Lines are drafts in
`2026-10-09-deja-vu-festival-lines.md` and need Jeff's yes before they go into i18n.
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

The villager does not remember the festival. He or she has a hunch about it, shaped by what the
player did there in an earlier loop.

## Non-goals

- No mechanical effect. A memory line never changes a festival outcome, a price, a friendship value
  or a score. (The two small familiarity bonuses under "Familiarity" are meta points only, and are an
  open question.)
- No loop explanation. Same rule as phase 1.
- No multiplayer. Every hook below returns early when `Game1.IsMultiplayer` (memory note: no
  multiplayer work yet).
- No new festivals content (no new events, maps or scripts). The lines ride on dialogue the
  festival already has.

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
```

### Where it lives

- `RunState.FestivalLog : Dictionary<string, FestivalMemory>`: this loop's festivals, written as they
  happen. RunState is saved with the farm, so a reload mid-loop keeps it.
- `MetaState.FestivalMemories : Dictionary<string, FestivalMemory>`: what earlier loops left behind.
  The only thing the lines read.

Writing this loop's outcomes into RunState, not straight into meta, is what keeps a memory from
being about the current loop. A line read on festival day always means "some earlier loop", and a
repeat visit to the same festival on the same day still finds the old memory (the meta record is not
touched until the rewind).

### Commit at the rewind

`FestivalMemoryStore.Commit(meta, run)` (Core, pure) runs in `RunController.FinalizeReset` just before
`Run.BeginNewRun(...)` (RunController.cs ~833), so every rewind path (failed gate, Day-28 voluntary
restart, `tly_reset`) goes through it. It merges per field:

- an entry in the log with `AttendedRun` set replaces the meta `AttendedRun`;
- an entry with an outcome replaces the meta outcome fields (`Outcome`, `ItemId`, `ItemQuality`,
  `Npc`, `Score`, `OutcomeRun`);
- a festival missing from the log (skipped, or the loop rewound before it) leaves the meta record
  alone. The old memory stays.

`BeginNewRun` then clears `FestivalLog` and the per-loop caps below.

Winning the year never rewinds, so nothing is committed; there is no next loop to remember in. The
Year 2 work on `story` can read the same meta later if it wants to.

### Cleared how

Never by a rewind (that is the point). Only by `tly_festmem clear` (debug) or a new farm, which starts
a new MetaState. Turning the feature off in GMCM stops recording and speaking but keeps the records.

### Outcomes per festival

| Festival (id) | Recorded | Outcome names | Memory keys spoken |
|---|---|---|---|
| Egg Festival (`spring13`) | eggs found, won or lost | `EggWon`, `EggLost` | `egghunt.won`, `egghunt.lost` |
| Flower Dance (`spring24`) | attended, partner (or none) | `Danced` (+`Npc`), `NoPartner` | `dance.partner`, `dance.attended` |
| Luau (`summer11`) | ingredient + governor's reaction | `LuauToxic`, `LuauBad`, `LuauOkay`, `LuauGood`, `LuauBest`, `LuauEmpty`, `LuauShorts` | `luau.bad` (Toxic, Bad), `luau.good` (Good, Best), `luau.shorts`; Okay and Empty are silent |
| Moonlight Jellies (`summer28`) | attended | none | `jellies.attended` |
| Fair (`fall16`) | grange score, place, shorts, empty display | `GrangeFirst`, `GrangeSecond`, `GrangeThird`, `GrangeLost`, `GrangeShorts`, `GrangeNone` | `fair.won` (First), `fair.lost` (Second, Third, Lost), `fair.shorts`; None is silent |
| Spirit's Eve (`fall27`) | golden pumpkin found | `Pumpkin` | `spirits.pumpkin`; attending without it is silent |
| Festival of Ice (`winter8`) | fish caught, won or lost | `IceWon`, `IceLost` | `icefish.won`, `icefish.lost` |
| Feast of the Winter Star (`winter25`) | recipient, gift, gift taste | `GiftLoved`, `GiftLiked`, `GiftNeutral`, `GiftDisliked`, `GiftHated` (+`Npc`, `ItemId`) | `winterstar.liked` (Loved, Liked), `winterstar.disliked` (Disliked, Hated); Neutral is silent |
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
| Winter Star gift | prefix + postfix `Event.chooseSecretSantaGift(Item i, Farmer who)` (12476) | the prefix saves `secretSantaRecipient.Name` (the method nulls it); the postfix records the item and `NPC.getGiftTasteForThisItem(i)` (NPC.cs 1405), mapped to loved/liked/neutral/disliked/hated |
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
- `MetaState.FestivalMemories` holds a speakable memory for that festival (the table above);
- the villager's familiarity is at least `DejaVuThreshold` (phase 1's tier 1, default 60). Tier 2
  adds nothing here; one line per memory per speaker;
- the villager has not spoken a festival memory this loop (`RunState.FestivalMemoryShownTo`);
- for `dance.partner` and the Winter Star memories: the villager is the one named in `Npc`.

`CompletedResets >= 1` is implied: loop 1 has no meta record, so there is nothing to say.

### Picking the speaker

Among the villagers who can speak it, prefer one with his or her own line for that memory (highest
familiarity first, ties by name so the choice is stable across repeat visits); else the eligible
villager with the highest familiarity speaks the fallback line. `dance.partner` and the Winter Star
memories have no fallback speaker: if the named villager is not eligible, nobody says it (the dance
then falls back to `dance.attended`).

### Festival day (Egg Festival, Luau, Jellies, Fair, Spirit's Eve, Festival of Ice)

**Hook:** the same postfix on `Event.setUpPlayerControlSequence`. By then every festival actor has
had its festival line pushed (`Event.addActor`, 5509, via `TryGetFestivalDialogueForYear` /
`setNewDialogue`). The postfix picks the speaker among `__instance.actors`, and pushes
`new Dialogue(npc, "TLY.festmem", text)` on top of that actor's `CurrentDialogue`. Talking at a
festival goes through `Event.checkAction` (11631), which draws the top of the stack; `DialogueBox
.closeDialogue` pops it (Menus/DialogueBox.cs 204), so the memory plays on the first talk and the
villager's own festival line on the second. This is the festival equivalent of phase 1's prepend.

One line per festival per loop, at most. The chance is `DejaVuFestivalDayChancePercent` (default
**100**: the player still has to walk up to the right villager, and there are only six of these a
year). Actors to skip, because `Event.checkAction` would replace or repeat the line:

- the festival host (`Event.festivalHost`, Lewis at every festival here): `checkAction` replaces a
  finished stack with `hostMessageKey` through `setNewDialogue`, which clears it (NPC.cs 4592);
- at `spring24`, any dance-capable villager (`datable` or `FlowerDanceCanDance`): the no-partner
  branch rebuilds the stack around the top line, so it would repeat. (No festival-day memory is
  planned for the Flower Dance anyway.)
- at `winter25`, `secretSantaRecipient`: talking to him or her opens the gift prompt first;
- the spouse (vanilla swaps in the `_spouse` line), as phase 1 already does.

**Heard, not just pushed.** The line is stamped heard (`RunState.FestivalMemoryHeard` += festival id,
`FestivalMemoryShownTo` += npc) in a postfix on `Game1.drawDialogue(NPC speaker)` when the top
dialogue's translation key is `TLY.festmem`. A line pushed but never heard (the player left, the auto
end fired) is pushed again on the next visit that day.

**Pre-contest only.** Each line is advice about the contest, so it is pulled when the contest starts:
on the `"yes"` in `Event.answerDialogueQuestion` (the same point `FestivalMainEventOncePatch` stamps),
and for the Luau also in a postfix on `Event.addItemToLuauSoup(Item, Farmer)` (12728). An unheard line
is removed from the speaker's stack and the festival counts as spent for the loop. Fair grange
judging also clears every actor's stack itself (`interpretGrangeResults` calls `setNewDialogue` on all
actors), which agrees with this rule.

### Lead-up week (Flower Dance, Winter Star, Night Market, Luau shorts for Lewis)

These memories need a villager who is not available on the day (the dance partner is mid-dance, the
gift recipient is behind the gift prompt, Lewis is the host, the Night Market has no festival
actors), so they play in ordinary dialogue in the days before.

**Hook:** phase 1's postfix on `NPC.checkForNewCurrentDialogue`. Before phase 1 rolls, it asks
`FestivalMemoryRules.TryPickLeadUp(meta, run, npc, date, config, rng)`. The window is the
`DejaVuFestivalLeadDays` days before the festival (default **7**, so it never crosses into the season
before; the Night Market window ends the day before its first night). On a hit it pushes
`new Dialogue(npc, "TLY.festmem", text)` the same way phase 1 does, with every phase 1 guard intact
(never over an Introduction or other `activeDialogueEvents` line, never on a festival day, never the
spouse). Chance per talk: `DejaVuFestivalLeadChancePercent` (default **15**). One lead-up line per
festival per loop (`RunState.FestivalLeadShown`).

### Interplay with phase 1's caps

- Festival memories have their own caps: one line per festival per loop, one festival memory per
  villager per loop. They do not use phase 1's "one line per villager per loop" slot.
- A lead-up line ignores phase 1's seven-day cap but sets it (`DejaVuLastDay`), so a generic deja-vu
  line never lands in the same week as a lead-up memory.
- Festival-day lines do not touch phase 1's caps. Phase 1 never runs inside a festival
  (`Game1.isFestival()` guard), so the two cannot collide.

Worst case per loop: eight festival-day and lead-up lines plus phase 1's weekly line. In practice
fewer, because each needs an eligible villager and a speakable memory.

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
key set like `DejaVuLines`, so adding a line is a JSON edit. `{{item}}` is filled at speak time from
`ItemRegistry.GetData(ItemId).DisplayName` (localised, and a removed mod item resolves to null, which
drops the line). The full draft, 51 lines, is in `2026-10-09-deja-vu-festival-lines.md`.

## Familiarity

The nightly rollup already counts festival talks: `Event.checkAction` calls
`NPC.grantConversationFriendship`, which sets `Friendship.TalkedToToday` (NPC.cs 2835). Two
festival interactions it misses, proposed as small additions to `FamiliarityRollup`:

- dancing with a villager at the Flower Dance: +10 (the same as a heart event);
- the Winter Star gift: +3 (a gift; vanilla calls `receiveGift` with `updateGiftLimitInfo: false`,
  so `GiftsToday` never sees it).

## Config (GMCM, "Features")

| Setting | Default |
|---|---|
| `EnableDejaVuFestivalMemories` | on (does nothing while `EnableDejaVuDialogue` is off) |
| `DejaVuFestivalDayChancePercent` | 100 |
| `DejaVuFestivalLeadChancePercent` | 15 |
| `DejaVuFestivalLeadDays` | 7 |

## Edge cases

- **First loop:** no meta records, no lines. Recording runs from day one.
- **Skipped festival, or the loop rewound before it:** nothing committed for it, so the older memory
  stays and can still be spoken. "Last year" in the lines is vague on purpose.
- **Entered, left before the contest:** attendance is updated, the old outcome stays.
- **Contest run with nothing to remember** (empty soup pot, empty grange display, neutral gift): the
  outcome is recorded and replaces the old one, and it is silent. The latest truth wins.
- **Repeat visit the same day:** attendance and outcomes are idempotent; an unheard festival-day line
  is pushed again; a heard one is not.
- **Auto end (`FestivalTimeFlow.ForceEnd`) or leaving mid-line:** the line was never drawn, so it is
  not stamped; nothing else to do.
- **Speaker absent** from this year's festival actors: the next candidate speaks, or nobody.
- **Dance partner or gift recipient now married to the player:** spouse is excluded; the dance falls
  back to `dance.attended` from another villager.
- **Mod-added NPCs:** use the fallback lines when eligible. Mod-added festivals are ignored (only the
  nine ids above are recorded).
- **Multiplayer:** every recorder and injector returns early. Nothing is recorded or spoken.
- **Feature off mid-loop:** recording stops; whatever the log holds still commits at the rewind.

## Debug commands

`tly_festmem`, registered beside `tly_dejavu`:

- `status`: meta records, this loop's log, heard/shown stamps, and for the next festival the speaker
  that would be picked and the line key;
- `set <festival> <outcome> [item-or-npc] [score]`: write a meta record (e.g. `set summer11 LuauBad
  (O)248`) with `AttendedRun`/`OutcomeRun` set to the previous loop;
- `clear [festival]`: drop one meta record, or all;
- `commit`: run the rewind merge on the current log without a reset (for checking the merge live);
- `force <festival>`: the next festival entry (or, for a lead-up memory, the next talk with the
  speaker) plays the line, skipping chance and caps but not the host, datable, recipient or spouse
  guards.

## Testing

Core (xUnit, `TheLongestYear.Tests`):

- `FestivalOutcomes`: governor level 0 to 6 to outcome; grange score bands, -666 and empty display;
  egg and fish win thresholds; gift taste constants to liked/neutral/disliked.
- `FestivalMemoryStore.Commit`: attendance-only entry keeps the old outcome; outcome entry replaces
  all outcome fields; missing festival keeps the record; a silent outcome replaces a speakable one.
- `RunState.BeginNewRun`: clears `FestivalLog`, `FestivalMemoryHeard`, `FestivalMemoryShownTo`,
  `FestivalLeadShown`, and phase 1's `DejaVuShownTo` / `DejaVuLastDay` (the prerequisite fix).
- `FestivalMemoryRules`: no record, no line; below threshold, no line; own-line speaker beats a higher
  familiarity fallback speaker; ties by name; partner-only memories ignore everyone else; one per
  festival and one per villager per loop; lead-up window bounds (day before inclusive, festival day
  exclusive, never in the previous season); lead-up sets but ignores the weekly cap; chance via an
  injected RNG.
- `FestivalMemoryLines`: pool discovery, fallback, `{{item}}` substitution, null item drops the line;
  the i18n guard covers every `festmem.*` key.

Live (Rodger save, loaded through `tly_loadsave`; game launches labelled whose they are):

1. `tly_dejavu set Gus 200`, `tly_festmem set summer11 LuauBad (O)248`, move the date to Summer 11
   with the SMAPI console inject, walk into the Luau, talk to Gus: his `luau.bad` line with Garlic
   plays, then his festival line. Add an item to the pot first on a second run: the line is gone.
2. Walk out and back in without talking to Gus: the line is pushed again. Talk, leave, return: not
   again.
3. Run the Luau soup normally, `tly_festmem status` shows the log entry; `tly_festmem commit` moves
   it to meta with the right outcome.
4. `tly_festmem set spring24 Danced Penny`, `tly_dejavu set Penny 200`, date to Spring 20, `force
   spring24`, talk to Penny: her partner line plays; on her Introduction day it does not.
5. Egg Festival and Fair once each with `force` to see the host and pre-contest guards hold.

## Docs

README + Nexus (identical content): extend the phase 1 Features bullet ("The town half-remembers")
with a clause about festivals. CHANGELOG Unreleased. No new credit line; Gribbleby is already credited.

## Open questions for Jeff

1. **How rare?** Festival memories get their own budget (up to one per festival per loop) on top of
   phase 1's weekly line. Is that too many, or should a festival line use up phase 1's weekly slot?
2. **Festival day at 100%:** guaranteed when an eligible speaker is there (the player still has to
   talk to him or her), or a roll like phase 1?
3. **Old memories:** a skipped festival keeps the memory from an older loop. Fine, or should
   memories fade after one loop?
4. **Lead-up week:** 7 days at 15% per talk. Right feel?
5. **Familiarity bonuses:** +10 for a Flower Dance partner, +3 for the Winter Star recipient. Yes or
   no?
6. **Lewis on festival day:** he is the host, and vanilla wipes anything put on his stack. He gets the
   shorts memory in the Luau lead-up week instead. Want a second small patch so he can speak at the
   festival itself?
7. **Which festivals:** the eight vanilla festivals plus the Night Market are in. The 1.6 Desert
   Festival, Trout Derby and Squid Fest are out. Add any?
8. **Silent outcomes:** okay Luau soup, empty pot, empty grange display, neutral Winter Star gift, and
   Spirit's Eve without the pumpkin say nothing. Want lines for any of them?
9. **The phase 1 cap bug** (caps never reset at the rewind): ship the fix now as a master bug fix, or
   with phase 2?
10. **The lines:** all 51 in the lines file, especially the 12 Flower Dance partner lines and which
    villagers get their own Luau lines.
