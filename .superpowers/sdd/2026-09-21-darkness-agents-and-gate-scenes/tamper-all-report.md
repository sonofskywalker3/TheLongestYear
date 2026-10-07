# Follow-up: the tamper only takes an item asked in one slot (designer ruling 2026-10-07)

Status: DONE. Branch `story`. Code commit `da61fda` (pushed). Manifest version untouched (0.18.15).

## What changed

Built to the SIMPLIFIED ruling: one strike still rewrites ONE slot, and that slot's exact item must be
asked in exactly one slot on the whole board.

- **Target rule (Core, `TamperRule.Targets`).** A slot is a target only when it is open, in an
  unfinished item-room bundle (as before), and `TamperRule.SlotsAsking` finds exactly one slot on the
  board asking for its exact item. Every slot counts, filled or open, in every bundle (the Vault and
  Joja included, though they never share an item). A doubled id inside one bundle is two slots.
  Quality is ignored.
- **Exact item = id + flavour.** `TamperTarget` gained `Flavor`. The flavour is read from
  `MetaState.WrittenBoardFlavors` (key `bundle:slot`) the way `FlavoredSlotPatch` applies it: only on
  an id `FlavoredSlotRules.IsFlavored` accepts (Dried Fruit, Smoked Fish; those are the only flavoured
  kinds the board writes), so a stale map entry under a plain item names nothing. Flavours compare
  bare or qualified alike (`(O)613` = `613`).
- **A slot naming no flavour overlaps every flavour of its id.** Vanilla's
  `Bundle.IsValidItemForThisIngredientDescription` takes any flavour when `preservesId` is null, so an
  "Any Dried Fruit" slot and a "Dried Apples" slot both want Dried Apples. Neither is a target. This is
  what keeps "it has tainted all the X" true for the rare mixed board.
- **`TamperRecord.OldFlavor` / `SabotageReport.OldFlavor`** (new, nullable). One record per strike is
  unchanged, since a strike still writes one slot.
- **The rewritten slot forgets its flavour** (`TamperRule.ClearFlavor`, called in `WriteTamper`).
- **The aura keys on id + flavour** (`TaintedItems.IsTainted(qualifiedId, flavor)`). The patch passes
  `item.QualifiedItemId` and `item.preservedParentSheetIndex.Value` (the bare id of the input, set by
  `ObjectDataDefinition.CreateFlavoredDriedFruit` / `CreateFlavoredSmokedFish` in the 1.6 decompile).
  A flavoured record marks only that flavour. A record with no flavour marks every copy of its id,
  which is what the slot accepted and exactly what the aura did before, so old saves are unchanged.
  A plain Potato's taint never marks Pickled Potato (another id, `(O)342`) and the reverse.
  Allocation-free: a `HashSet` lookup on the id, then a loop over the few records comparing the
  flavour in place (`string.CompareOrdinal` with an offset past any qualifier, no substring).
  `TaintedAuraPatch.Tainted` is now `Func<TaintedItems>`; ModEntry's closure refreshes and returns the
  cache (no allocation).
- **The Junimo line names the exact item, pluralised.** `SabotageService.ExactName` builds the item the
  way the bundle menu does (`Utility.CreateFlavoredItem`), so the name is the game's own ("Dried
  Apples"). `ItemPlurals.Tainted` keeps a flavoured Dried Fruit / Smoked Fish name as the game wrote
  it: the game already runs Dried Fruit names through `makePlural` (Object.cs 1011), so pluralising
  again gave "Dried Appleses", and "all the Smoked Salmon" is how the fish is said. Anything else goes
  through `ItemPlurals.Plural`, which now also checks the last word of a name against the mass nouns
  (and Jelly joined the list): "all the Blueberry Jelly", "all the Wild Honey" (was "Wild Honeys").
  No dialogue was written: the designer's lines are unchanged, only the {{old}} token's value.
- **Debug readout.** `tly_sabotage status` adds two lines: the target count with the first twelve
  exact names, and "not targets (asked in 2+ slots ... multi-slot items are never tampered)" with each
  open multi-slot item and its slot count. Each `tampered:` line shows the exact name and
  `[flavour X]`. `tly_sabotage fair <id>` ends with one "tamper target (...)" line per flavour the board
  names: "yes, asked in exactly one slot" or "no, asked in N slots (multi-slot items are never targets)".
- **`tly_sabotage aurachest`** builds each tainted item exactly (flavoured when it has a flavour) and,
  for a flavoured one, adds another flavour of the same good beside it (Apple, else Apricot; Sunfish,
  else Carp) so the frame shows one marked and one not.
- Specs: `2026-09-15-darkness-obtainability-wiring-design.md` (Tampering paragraph: the target rule)
  and `2026-09-21-darkness-agents-and-gate-scenes-design.md` (Junimo scene {{old}} bullet, plurals,
  the dark aura). The rework spec (2026-09-14) does not state the target rule, so it is unchanged.

Items 2, 4's slot list, 6's several-replacements note and 7's every-slot note were superseded by the
ruling and are not built. Item 7 still holds trivially: the one rewritten slot is removed from
`CurrentWeekBonusSlots` as before.

## No target: what happens

The existing no-fair-target path, unchanged. `PlanTamper` returns null, so `CanAct(Tampering)` is
false: the night's roll (`NightRoll.Pick`) takes another event that can act, or nothing strikes and the
chance does not drop; an armed tamper logs "armed but cannot act tonight"; the guaranteed Winter tamper
logs "had no fair target ... retrying tomorrow" and retries the next night through week 1; the every-loop
guarantee keeps the kind owed. The log line is now Info and says why: "tampering has no target: no open
slot in an unfinished bundle asks for an item that no other slot on the board asks for." I chose it
because it is what the code already does for "nothing fair", it never forces a strike that would break
the ruling, and the guarantee machinery already handles a kind that could not act.

## FlavoredSlotPatch, before and after

The patch postfixes the `Bundle` constructor and, for each ingredient whose id `IsFlavored` and whose
`bundle:slot` key is in the map, sets `preservesId` to the stored input.

- **Before:** `WriteTamper` left the map entry. If the replacement was plain (the usual case) the patch
  skipped it (`IsFlavored` false), so nothing showed, but the stale entry stayed. If the replacement
  was itself a flavoured kind (Dried Fruit / Smoked Fish are in the artisan pool), the patch pinned the
  OLD input on it: a Smoked Fish replacing Dried Apples would ask for "Smoked Apple", which nothing
  makes.
- **After:** `WriteTamper` removes the entry. A plain replacement is unaffected; a flavoured-kind
  replacement shows as "Any Dried Fruit" / "Any Smoked Fish" through `FlavorlessSlotLabelPatch`, as an
  unflavoured slot always has. Live: `tly_flavors` after the strike lists only `3:1 -> (O)267`; the
  `5:3 -> (O)613` entry is gone.

## Tests

TDD: `tests/TheLongestYear.Tests/TamperExactItemTests.cs` written first and failed to compile against
the old Core (no `Flavor`, `SlotsAsking`, `ClearFlavor`, `OldFlavor`, `IsTainted`, `ItemPlurals.Tainted`).
22 tests (15 facts, one 7-case theory):

- An item in three bundles (one slot filled) is never a target, and 40 seeded `PickTarget` runs with the
  player holding it never pick it; an item in one slot is the only target; a filled copy elsewhere
  makes it multi-slot; a doubled id in one bundle is two slots.
- Dried Apple and Dried Cucumber in two bundles are each a target and carry their flavour; the same
  flavour in two slots (qualified and bare spellings) is never a target; an any-flavour slot overlaps a
  flavoured one; a stale flavour under a plain item is ignored; `SlotsAsking` counts.
- `ClearFlavor` removes only that slot's entry, is false when absent and on a null map.
- Aura: tainted Dried Apples mark Dried Apples only (bare and qualified flavour), not Dried Cucumbers,
  not an unflavoured Dried Fruit, not the Apple itself; a plain Potato taint does not mark Pickled
  Potato and a Pickled Potato taint does not mark Potato or Pickled Parsnip; an unflavoured record marks
  every flavour of its id; two records of one base mark both flavours and nothing else.
- Old save: a `TamperRecord` JSON without `OldFlavor` (and a `SabotageReport` without it) deserialises
  with null and the aura behaves as before.
- Line: "Dried Apples", "Dried Cranberries", "Smoked Salmon" stay; "Potato" -> "Potatoes",
  "Parsnip" -> "Parsnips", "Blueberry Jelly" and "Wild Honey" stay.

Full suite once before committing:

```
dotnet build TheLongestYear.sln          -> Build succeeded.
dotnet test tests/TheLongestYear.Tests --no-build
  Passed!  - Failed: 0, Passed: 3911, Skipped: 0, Total: 3911
```

## Live check

All launches mine, minimized, no mouse or keyboard. `tools/deploy.ps1 -Minimized`; `tools/bridge.ps1`
(`tly_newgame standard skipintro`, `tly_select`, `tly_reset`, `tly_flavors`, `tly_additem query
FLAVORED_ITEM DriedFruit 613` and `... 634`, `tly_sabotage tamper|status|fair|aurachest`);
`tools/send-smapi-command.ps1` (`debug season winter`, `debug warp FarmHouse 8 8`, `debug warp Farm 64
16`, `tly_eventstep`); my scratchpad PrintWindow script (restores without activating, re-minimises).
Harmony: 93 patch classes applied, 0 failed; no ERROR from the mod.

- Throwaway farm `standard_451077373` (renamed `standard_451077415` by the in-place reset). The first
  board had no flavoured slot; one `tly_reset` gave Dried Apples (Brewer's slot 3) and Smoked Flounder
  (Preserver's slot 1).
- Before the strike, `tly_sabotage status`: "tamper targets ...: 111: Opal, Orpiment, ..." and
  "not targets (asked in 2+ slots, filled or open; multi-slot items are never tampered): Common
  Mushroom x2, Hazelnut x2, Mudstone x2, Nekoite x2, Oyster x2, Wild Plum x2". `fair (O)DriedFruit`:
  "tamper target (Dried Apples): yes, asked in exactly one slot".
- With Dried Apples and Dried Apricots in the inventory (held items go first), `tly_sabotage tamper`:
  "Brewer's slot 3 no longer names a flavour (was (O)613)" and "Brewer's slot 3 now asks for 2 Beer
  instead of Dried Apples". Status after: "tampered: Brewer's slot 3: Dried Apples [flavour (O)613] ->
  2 Beer"; Beer now shows in the not-targets list (x2, see Open). `fair (O)408`: "tamper target
  (Hazelnut): no, asked in 2 slots (multi-slot items are never targets)"; same for Beer.
- Stepping onto the Farm started the Junimo scene: "(Dried Apples -> 2 Beer, skippable=False)".
- Cleanup: game closed; save deleted, Saves folder matches the listing from before; `config.json` and
  window size never changed; `git status` clean after the run (deploy pruned only untracked archives,
  so there was nothing to `git checkout --`).

### Frames (`test-output/scenes/tamper-exact/`, git-ignored; I looked at each one listed)

| File | What it shows |
| --- | --- |
| `junimo-01-line1.png` | The scene at the door: "Rodger, last night the darkness struck! It has tainted all the Dried Apples." |
| `junimo-02.png` | "Bring us 2 Beer instead. They remain pure." |
| `junimo-04.png` | After the scene, control back; hotbar slot 10 Dried Apples, slot 11 Dried Apricots. |
| `aurachest-1..3.png` | `tly_sabotage aurachest`: chest row Dried Apples, Dried Apricots, Parsnip; inventory below with the same two jars. Only the Dried Apples carry the purple glow, in both rows. |
| `aurachest-crop-chest.png` | 4x crop of the chest row: purple halo round the Dried Apples; Dried Apricots (same base item, another flavour) and Parsnip plain. |
| `aurachest-crop-inventory.png` | 4x crop of the inventory pair, same result. |

`junimo-03.png` is the fade back to the normal view as the scene ends; `junimo-05.png` repeats the
after-scene view of `junimo-04.png`. The hotbar glow on the Dried Apples is faint at full size; the
4x crops show it plainly.

## Open items

1. **The replacement can be a multi-slot item.** `PickReplacement` only avoids items already in the
   same bundle, so the live strike asked for Beer, which another bundle also asks for (Beer x2). That
   is fine for the ruling as given (it is about what may be tainted), but it means a later tamper can
   never take that slot. Designer question: should a replacement also avoid anything already on the
   board?
2. **Unflavoured slots of goods that come in flavours** (a plain Roe, Honey or Jelly slot, or a legacy
   "Any Dried Fruit" slot) taint every flavour, because that slot accepted every flavour. Brief item 5's
   "marks the plain item only" read literally would leave those auras on nothing at all (every Roe has
   a flavour), so I kept the id-only behaviour for an unflavoured record. The Pickled Potato case the
   brief names is covered (different id). Flag if the designer wants otherwise.
3. **Held-first and flavours.** "The player holds it" compares ids, so holding any Dried Fruit puts every
   Dried Fruit target in the first tier. Unchanged behaviour; harmless.
4. **Not run live:** a Smoked Fish strike (the same code path as Dried Fruit), and an old save with
   pre-change records (covered by the deserialisation test).
