# Task 12: Witness dialogue

Status: done. Commits 3faf399 and 00584ce on story, pushed (no GitHub 500). Manifest Version untouched (0.18.15).

## Built
- `WitnessLines` / `WitnessRecord` in Core, exactly per the brief. `RunState.WitnessLines` (List, serialises with the run, cleared in `BeginNewRun`).
- `WitnessDialogueService` (`OnScenePlayed`, `OnDayStarted`), wired in ModEntry: the Task 6 factory lambda calls `_witness?.OnScenePlayed(strike.Event)`, `OnDayStarted` runs right after `_boostEffects` (after the run controller synced the date).
- i18n: the designer's four lines verbatim; `I18nGuardTests` walks `WitnessLines.AllKeys`.
- Debug command `tly_witness list | peek <npc> | talk <npc> | click` (no talk command existed; `talk` is `Game1.drawDialogue(npc)`).

## Rulings
- Skipped scenes: the factory's `onFinished(shown)` is true for finished, skipped and failed-after-staging; the witness is recorded in that branch. Detection of "no scene": the lambda returns early when `shown` is false (scene never staged), and when no scene takes the slot (scripted event, fail night, `CanPlay` false) `SceneFor` is never consumed and the strike lands via `ApplyNow`, so no record. Skip path was not exercised live (it uses the same callback).
- `Dialogue(NPC, string, string)` and `Dialogue.onFinish` exist in 1.6 as in the brief. onFinish fires in `exitCurrentDialogue` on the final page.
- Hall line kept verbatim.

## Deviation
Brief says record N from `Run.DayOfMonth`; my first version used the game date, and the live run showed the game date has already rolled to N+1 during the overnight scene (recorded day 31, which would never be live). Fixed in 00584ce to read `run.Season/DayOfMonth` (log: scene day 30, today 31). The morning-after read still uses the game date.
I wrote the implementation before the tests for the pure parts (the tests are the brief's, plus reset-clears and JSON round-trip); the pure logic was verified by them afterwards.

## Tests
`dotnet build TheLongestYear.sln` 0 errors; `dotnet test tests/TheLongestYear.Tests --no-build`: Passed 3889, Failed 0 (14 witness tests).

## Live check (my launches, minimized, no mouse or keyboard; throwaway saves created and deleted)
Commands: `tools/deploy.ps1 -Minimized`, `tools/bridge.ps1` (`tly_newgame standard skipintro`, `tly_select Fishing`, `tly_sabotage arm blight crops` / `arm revert`, `tly_playseason`, `tly_reset`, `tly_witness ...`), `tools/send-smapi-command.ps1` (`debug warp/season/spreaddirt/spreadseeds 479/sleep`), PrintWindow capture script (scratchpad).
- Linus: crows scene Summer 2, "Witness: Linus saw the CropBlight scene on day 30". Summer 3 top dialogue: "I was out walking last night and ...". Talking closed it: `said=True`, next top is his normal rainy line.
- Linus, new loop, never talked: day 37 (7 days after) top = "...the other night..."; day 38 top = his normal line and the list is empty (gone after the window).
- Shane: after `tly_playseason`, season fall, `arm revert`: "Witness: Shane saw the Reversion scene on day 60"; day 63 and day 67 top = "When I left the bar the other night ...".
- No ERROR from the mod. Game closed; two throwaway saves deleted (Saves listing matches before); no config change; no tracked logs pruned.
- Not verified live: a skipped scene, the loop-reset clearing (unit-tested), Shane's "last night" (same code path as Linus).
- A dialogue box left open blocks `debug sleep` until closed.

## Frames (`test-output/scenes/witness/`, untracked; both viewed)
- `linus-morning-after.png`: Linus portrait box with the "last night" line, Mountain, Wed 3.
- `shane-other-night.png`: Shane box with the "the other night" line, farmhouse, Thu 11.
(The box is drawn via the debug `talk`, not a real conversation; the Mountain frame shows Linus's portrait while the farmer stands at his tent.)

## Open items
None blocking. Real conversation (checkAction) shows the same `CurrentDialogue.Peek()` as `drawDialogue`, but it was not exercised with input.
