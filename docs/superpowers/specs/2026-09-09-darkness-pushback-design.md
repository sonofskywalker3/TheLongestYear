# Darkness Pushback (the sabotage mechanic)

**Date:** 2026-09-09
**Status:** built from the story spec's section 6 on Jeff's "go ahead and implement" (2026-09-09);
every number and every rule not already locked in that section is an ASSUMPTION for Jeff to
retune, marked below. Branch `story`.
**Story spec:** `2026-06-06-tly1-story-and-cutscenes-design.md` section 6 (forms, onsets,
counterplay) and section 7 (hooks). The season-turn lines in
`2026-09-07-season-turn-beats-design.md` already announce each front and are the fiction this
spec makes true.

## What it is

The darkness acts on the world the way the Junimos do. From Summer it withers crops in the
night; from Fall it also empties donated bundle slots; in Winter it also rewrites what an unfilled
slot asks for. Each front is telegraphed the next morning by a HUD line only the player sees. The
first two fronts can be bought off at the shrine, one ward per season of crops and one per hall
room. The Winter front cannot.

Every front has its own switch in GMCM so a player who hates it can turn that one off.

| Front | Seasons | What happens overnight | Counter |
| --- | --- | --- | --- |
| Blight | Summer, Fall | A few live crops on the farm die (vanilla `dead`, drawn withered). | Ward of the Fields, one per season (Summer, Fall) |
| Reversion | Fall, Winter | One filled slot in an unfinished bundle empties; the item is gone. | Ward of the Hall, one per item room (five) |
| Tampering | Winter | One unfilled slot in an unfinished bundle asks for a different item. | none, by design |

Spring is clean. Winter blight is absent: the Fall-to-Winter turn line says "It will not rot your
crops" (spec 2026-09-07, `winter-3`), which settles the story spec's open question.

## When it runs

All three fronts roll at bedtime, in `RunController.OnDayEnding`, after the ledger mirror and
after the gate has evaluated, and only when the gate said Continue. So:

- A day 28 never rolls: the season's gate judges what the player actually had, and the day-28
  morning (cutscene early return) never has to carry a report.
- The win night never rolls (`EndingArmed`).
- Host only, single player or master game, `RunActivation.IsActive`, same as the ledger mirror.

The effects land before vanilla's overnight pass and before the save, so a quit-to-title after
sleeping keeps them. The morning report is persisted on the run (`PendingSabotageReports`) and
shown as HUD lines from `OnDayStarted`, then cleared.

**Deterministic rolls (assumption).** Each night's random stream is seeded from the run seed, the
day of the year and the front, so re-sleeping the same night gives the same outcome. Nothing to
save-scum.

## Blight

Per night in an open, unwarded season, with the front switched on:

| | Summer | Fall |
| --- | --- | --- |
| Chance the darkness strikes tonight | 25% | 35% |
| Crops killed when it does | ceil(4% of live crops), clamped 1 to 6 | ceil(6% of live crops), clamped 1 to 10 |
| Nights per week it can strike | 2 | 2 |

(All ASSUMPTIONS; `SabotageTuning` in Core holds them in one place.)

Targets: `HoeDirt` with a live, non-dead crop on the Farm location only, chosen uniformly. The
greenhouse, indoor pots, Ginger Island and every other map are exempt (the darkness poisons the
land, and the greenhouse is the Junimos' gift). Nothing else rots: the story spec's "stored items"
clause is left out for now (assumption: chests are the player's planning, and the Junimo Stash
is the cross-loop bank; rotting either is a feel-bad with no counter yet).

Morning line: `hud.sabotage.blight` with the count.

## Reversion

Per night in an open season, with the front switched on:

| | Fall | Winter |
| --- | --- | --- |
| Chance tonight | 20% | 30% |
| Slots emptied when it strikes | 1 | 1 |
| Cap | 1 per week | 1 per week |
| Quiet days | 25 to 28 | 25 to 28 |

(ASSUMPTIONS.) The quiet days leave time to redo a slot before the gate.

Candidates: filled, non-category slots in item rooms (Pantry, Crafts Room, Fish Tank, Boiler
Room, Bulletin Board) whose room has no ward and whose bundle is not fully complete. A complete
bundle is never touched, so vanilla's reward, room restoration and the mod's completion awards
stay consistent without an un-complete path. If there is no candidate, nothing happens.

The recipe (per the code map): flip `netWorldState.Bundles[bundle]` slot to false with a
clone-assign, then `ItemDonationSync.Reconcile`. The item is consumed. Re-donating pays donation
JP again (the observer sees a fresh false-to-true); that is the darkness's tax, not an exploit,
and it is deliberate.

Morning line: `hud.sabotage.reversion` with the item and bundle names.

## Tampering

Winter only, unavoidable, with the front switched on:

| Chance per night, Winter 1 to 20 | 15% |
| Cap | 2 per Winter, at least 5 days apart |
| Quiet days | 21 to 28 |

(ASSUMPTIONS.)

Target slot: an unfilled, non-category slot in an unfinished item-room bundle. Slots whose item
the player currently holds (inventory or any chest) come first, then the rest at random: the
sting is that the thing you were saving is no longer asked for.

Replacement item: from the mod's item catalog, same room theme as the bundle (the Bulletin
Board's Mixed accepts any theme), not already an ingredient of that bundle, placed by the
availability model with a pacing week in Winter (13 to the current week). Among those, the five
closest in effort to the original item, one at random. Stack 1, quality basic (assumption: the
quantity-realism ruling of 2026-08-30 says never roll a number that was not measured; a swap the
player could not plan for gets the gentlest ask). If no candidate exists, nothing happens.

The write: rewrite that ingredient triple in the live `BundleData` entry, `SetBundleData`, update
`MetaState.WrittenBoard` in lockstep (else the next load fails the manifest check and silently
demotes to the legacy path), rebuild the catalog and the requirements through the existing
`ReplaceCatalog` / `ReplaceRequirements`, and drop the slot from `CurrentWeekBonusSlots` if it
was this week's goal. A `TamperRecord` (bundle, slot, old id, new id, day) is kept on the run for
`tly_sabotage status` and the log.

Morning line: `hud.sabotage.tamper` with the bundle, old and new item names.

## Wards (shrine)

New `UpgradeCategory.Wards`, a new tab, permanent like every other upgrade (the story spec puts
the counters in the upgrade catalog, and TLY's upgrade philosophy is granular, buy-once).

| Id | Name | Cost (assumption) |
| --- | --- | --- |
| `ward_crops_summer` | Ward of the Fields: Summer | 250 |
| `ward_crops_fall` | Ward of the Fields: Fall | 300 |
| `ward_hall_pantry` | Ward of the Hall: Pantry | 200 |
| `ward_hall_craftsroom` | Ward of the Hall: Crafts Room | 200 |
| `ward_hall_fishtank` | Ward of the Hall: Fish Tank | 200 |
| `ward_hall_boilerroom` | Ward of the Hall: Boiler Room | 200 |
| `ward_hall_bulletin` | Ward of the Hall: Bulletin Board | 200 |

No prerequisites, no reach requirement. Shrine prices scale with the difficulty dial like every
other row. The story spec listed a Spring crop ward; Spring is clean so there is none.

## Config

Three bools on `GameplayConfig`, default true, in the GMCM Features section (applies straight
away, read live from the config instance): `EnableBlight`, `EnableBundleReversion`,
`EnableRequirementTampering`. Wards stay purchasable with a front off; they simply have nothing
to stop.

## Reset and hold

`RunState.BeginNewRun` clears the counters, the tamper records and any pending reports. A board
kept on a Fail night keeps its tampered entries (they are part of `WrittenBoard`), which is
right: the darkness changed the hall, and the hall is what the player chose to keep.

## Code shape

- `Core/Sabotage/SabotageTuning.cs`: every number above.
- `Core/Sabotage/SabotageSchedule.cs` (pure): which fronts are open in a season, the nightly
  roll, the caps, the ward checks, the seeded rng.
- `Core/Sabotage/BlightRule.cs`, `ReversionRule.cs`, `TamperRule.cs` (pure): pick counts, slots
  and replacements from ledgers, requirements and candidate lists.
- `Core/Sabotage/BundleDataTamper.cs` (pure): rewrite one ingredient triple in a bundle value.
- `Core/RunState.cs`: `BlightWeek`, `BlightNightsThisWeek`, `LastReversionWeek`, `TamperDays`,
  `Tampers`, `PendingSabotageReports`.
- `Core/UpgradeCatalog.cs` + `UpgradeCategory.Wards` + `WardIds`.
- `Loop/SabotageService.cs` (glue): the night pass and the morning report; `Loop/BlightPass.cs`
  for the crop walk; `Integration/CcSlotWriter.TryUnfill`; a tamper writer that calls back into
  `ModEntry` to rebuild catalog and requirements.
- `RunController.OnDayEnding` calls the night pass on Continue; `OnDayStarted` shows reports.
- Debug: `tly_sabotage status | blight [n] | revert | tamper`, console and file bridge.
- i18n: `hud.sabotage.*`, `upgrade.ward_*.name/.desc`, `upgrade-category.wards`,
  `gmcm.blight.*`, `gmcm.reversion.*`, `gmcm.tampering.*`.

## Testing

Core rules get xUnit tests (schedule, caps, candidate filters, the triple rewrite, run-state
reset). Live: `tly_sabotage blight 3` on a Summer save, `tly_sabotage revert` and `tamper` on a
Fall or Winter save, then reload and confirm the requirements source is still the engine
manifest, not the legacy path.

## Open for Jeff

- Every number in the tables.
- Whether blight should also touch stored items in Fall.
- Ward prices and whether hall wards should share a ladder like the Gifts.
- Whether the Wards tab should hide when all three fronts are off.
