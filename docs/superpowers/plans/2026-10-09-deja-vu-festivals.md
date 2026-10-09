# Deja-vu Phase 2 (Festival Memories) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan
> task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Villagers at a festival sometimes half-remember what the player did at that festival in an
earlier loop (the egg hunt, the dance, the soup, the grange, the ice fishing, the secret gift).

**Spec:** `docs/superpowers/specs/2026-10-09-deja-vu-festivals-design.md`. Both "Jeff's rulings" sections
override the body. Lines: `docs/superpowers/specs/2026-10-09-deja-vu-festival-lines.md` (72 lines, approved
2026-10-09, copied into i18n verbatim).

**Branch:** `deja-vu-festivals` off `origin/master`. Feature branch: the manifest `Version` stays at the
branch-point value. Commit small, push the branch after each commit, never merge to master here.

**Tech Stack:** C# / .NET 6, SMAPI 4, Harmony, xUnit (Core only).

## Rulings that shape the build (from the spec)

- Festival day only. No lead-up week, so no `DejaVuFestivalLeadDays`, no `dance.partner`, no Lewis line,
  no Night Market lines (and no Night Market recording, since nothing would ever speak it).
- No friendship gate at festivals. Every eligible villager at the festival rolls 20%
  (`DejaVuFestivalChancePercent`); past dance partners and past secret gift recipients roll 50%
  (`DejaVuFestivalBondChancePercent`). Phase 1's threshold applies to phase 1 only.
- One memory per festival per loop: the first heard stamps the festival in
  `RunState.FestivalMemoryHeard`, and every other unheard line for it is pulled. A post-result line counts
  as that festival's memory.
- Egg hunt guarantee: a win not yet remembered (`HeardRun < OutcomeRun`) makes the next Egg Festival push
  `egghunt.won` on every eligible villager, no roll.
- Bonds are kept for every loop: `MetaState.DancePartners` and `MetaState.WinterStarRecipients`, appended
  at each rewind, never replaced.
- Familiarity bonuses: +10 to the dance partner, +3 to the secret friend.
- Neutral outcomes (okay or empty soup, empty grange, attending Spirit's Eve without the pumpkin) are
  recorded and silent.

## Decisions for the open build questions

1. **Post-result lines roll on their own** (20%, 50% for `winterstar.again`, 100% while the egg guarantee
   is armed), at the result. They are skipped when the festival is already spent.
2. **"Before" lines are pulled when the contest starts** (the host's "yes" for the Egg Festival, Luau, Fair
   and Festival of Ice, and for the Luau also the moment an item goes into the pot). Pulling does NOT spend
   the festival, so the post-result line can still play. A repeat visit the same day after the contest
   pushes no "before" lines (`FestivalMainEvent.AlreadyPlayed`).
3. **Where post-result lines play.** The Egg Festival, Luau and Festival of Ice end in a script with no free
   roam after the result (dumped `Data/Festivals`: `afterEggHunt`, `governorReaction<N>`, `afterIceFishing`
   all run to `end`). So those lines go into the script as a `speak <npc> "<line>"` command:
   - Egg hunt / ice fishing: inserted right after the `null` that follows `cutscene eggHuntWinner` /
     `cutscene iceFishingWinner`, so the speaker reacts the moment the winner is named (before the
     `AbbyWin` / `DickWin` fork, which would replace the command list).
   - Luau: `governorTaste` only writes `switchEvent governorReaction<N>`, so the line is queued and a postfix
     on `Event.DefaultCommands.SwitchEvent` inserts it before the reaction script's last `globalFade`, after
     Lewis's closing line.
   - Fair: free roam continues after judging, so Pierre's line is pushed on his stack after
     `interpretGrangeResults` rebuilt it, and plays on the next talk.
   - Winter Star: appended as a second page of the secret friend's thank-you line (the open `DialogueBox`
     pages through `Dialogue.dialogues` live), the same trick as `dance.again`.
   A scripted or appended line is stamped heard when it is placed (it cannot be missed).
4. **Keys.** `festmem.<memory>.<npc>.<n>` and `festmem.<memory>.default.<n>` for "before" and any-time lines;
   `festmem.<memory>.<npc>.after.<n>` for post-result lines (`winterstar.again` uses
   `festmem.winterstar.again.default.after.1`). The Winter Star gift variants are separate memories:
   `winterstar.seen` (neutral), `winterstar.seen.liked`, `winterstar.seen.disliked`.
5. **Stable rolls.** FNV-1a of (run seed, run number, festival, salt, villager), so a repeat visit gives the
   same villagers the same lines. `string.GetHashCode` is randomised per process and is not used.
6. **Speakers.** Festival actors that are villagers who can socialise, never the host (`festivalHost`,
   Lewis), the spouse, a child, at the Flower Dance anyone who can dance (they get `dance.again` /
   `dance.other` through the dance hook instead), or at the Winter Star this year's secret friend.

## Files

Core (pure, tested):
- `src/TheLongestYear.Core/FestivalMemory.cs`: `FestivalMemory`, `BondMemory`, `FestivalIds`,
  `FestivalOutcome` name constants, `FestivalMemoryKeys` memory-name constants.
- `src/TheLongestYear.Core/FestivalOutcomes.cs`: classification (governor level, grange score, egg and fish
  thresholds, gift taste) and outcome to memory mapping.
- `src/TheLongestYear.Core/FestivalMemoryStore.cs`: run-log writes (`RecordAttendance`, `RecordOutcome`),
  `Commit(meta, run)`, and `FestivalBonds` queries.
- `src/TheLongestYear.Core/FestivalMemoryRules.cs`: `PlanFestivalDay`, `PlanAfter`, `PlanDanceAgain`,
  `PlanDanceOther`, `PlanWinterStarAgain`, `MarkHeard`, `StableRoll`.
- `src/TheLongestYear.Core/FestivalMemoryLines.cs`: pool discovery, pick, tokens, `AllKeys`.
- Edits: `RunState.cs` (`FestivalLog`, `FestivalMemoryHeard`, cleared in `BeginNewRun`), `MetaState.cs`
  (`FestivalMemories`, `DancePartners`, `WinterStarRecipients`), `GameplayConfig.cs` (three settings),
  `FamiliarityRollup.cs` (`AddBonus`, two constants).

Glue:
- `src/TheLongestYear/Loop/FestivalMemoryRecorder.cs`: Harmony postfixes that record attendance and outcomes.
- `src/TheLongestYear/Loop/FestivalMemoryInjector.cs`: festival-day push, heard stamp (`Game1.drawDialogue`
  postfix), pull at contest start, post-result placement, dance and Winter Star hooks.
- `src/TheLongestYear/Debug/FestivalMemoryCommand.cs`: `tly_festmem`.
- Edits: `RunController.FinalizeReset` (commit before `BeginNewRun`), `ModEntry` (connect, GMCM toggle,
  command registration and bridge dispatch, golden pumpkin `InventoryChanged`), `i18n/default.json`.

Tests: `FestivalOutcomesTests`, `FestivalMemoryStoreTests`, `FestivalMemoryRulesTests`,
`FestivalMemoryLinesTests`, `RunStateTests` additions, `I18nGuardTests` walks `festmem.*`.

## Tasks

- [ ] **1. Spec and lines marked APPROVED** (2026-10-09), plan committed.
- [ ] **2. Records and outcomes.** Tests first: governor level 0..6 to outcome; grange 90/75/60 bands, -666
  shorts, empty display; egg win at 9, fish win at 5; gift taste love/like/neutral/dislike/hate; outcome to
  memory (`Okay`, `Empty`, `GrangeNone` silent). Then the types.
- [ ] **3. Store and commit.** Tests first: attendance-only keeps the old outcome; outcome replaces all
  outcome fields; missing festival keeps the record; silent outcome replaces a speakable one; heard stamps
  before the outcome merge (heard win then new win stays armed); `Danced` appends a partner, a gift appends a
  recipient; five loops keep five partners oldest first; same partner twice gives two entries;
  `LatestGift` newest; `BeginNewRun` clears log and heard, keeps meta bonds. Then RunState/MetaState fields,
  `FestivalMemoryStore`, `FestivalBonds`.
- [ ] **4. Lines.** i18n block (72 lines verbatim), `FestivalMemoryLines` with tests (own pool, fallback,
  after pool, `{{item}}`, `{{partner}}`, null item drops the line), guard walk.
- [ ] **5. Rules.** Tests first with injected rolls: no record no line; ordinary 20% (hit 19, miss 20); bond
  50%; egg guarantee 100% while `HeardRun < OutcomeRun`, 20% after; budget blocks everything once heard;
  host/spouse/child/dance-capable/secret friend skipped; `dance.attended` only for non-dancers;
  `winterstar.seen` variant from the latest gift and never for this year's secret friend; post-result
  lines need the matching result now; stable roll is stable per run and differs across runs; force skips
  chance and budget.
- [ ] **6. Config + familiarity bonus.** `EnableDejaVuFestivalMemories`, chance settings, `AddBonus`.
- [ ] **7. Recorder glue** (attendance, egg, dance partner, Luau, grange, pumpkin, ice, gift) and the commit
  in `FinalizeReset`.
- [ ] **8. Injector glue** (festival-day push, heard stamp and drop, contest pull, scripted post-result
  lines, Fair push, dance hooks, Winter Star append).
- [ ] **9. `tly_festmem`** (`status`, `set`, `partner`, `recipient`, `clear`, `commit`, `force <festival>|off`)
  and GMCM toggle.
- [ ] **10. Docs.** CHANGELOG Unreleased, README + Nexus description Features bullet (identical wording).
- [ ] **11. Live check** (headless, throwaway farm): record a festival, `tly_reset`, next loop's festival
  shows a memory with `tly_festmem force`, screenshot of a line, one post-result line. Owed if the game stays
  busy.

## Verification

- `dotnet test tests/TheLongestYear.Tests` green after every task.
- `dotnet build src/TheLongestYear -c Release` clean (this deploys to Mods; rebuild master's build afterwards
  so Mods ends on master).
