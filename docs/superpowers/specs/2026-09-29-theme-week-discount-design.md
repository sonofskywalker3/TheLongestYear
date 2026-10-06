# Theme week discount (design)

Date: 2026-09-29. Status: approved by Jeff in chat (numbers and rules below), pending his read of this doc.

## Why

A weekly goal is a whole bundle line, so a third of all goals ask for 20 or more (up to 49), from
week 1 on (8-board sweep, 2026-09-29). Nijah called 31 Cauliflower in week 1 unrealistic. Jeff
rejected partial donations (players expect a slot to take the whole stack at once). His rule
instead: **the theme you pick lowers what the bundle asks for, for that week only.** The theme
choice gets a bigger impact, the week's goals get easier, and so does the season gate.

## The rule

For each goal line committed for the current week (the `BonusSlot`s in
`RunState.CurrentWeekBonusSlots`):

```
if original stack > 10:
    discounted = max(10, floor(original × (1 − d)))
else:
    discounted = original          (unchanged)
```

| Difficulty | d | 31 becomes | 14 becomes | 11 becomes |
|---|---|---|---|---|
| Easy | 0.50 | 15 | 10 | 10 |
| Normal | 0.25 | 23 | 10 | 10 |
| Hard, Extreme | 0 | 31 | 14 | 11 |

- Always round DOWN, never below 10 (Jeff). Quality asks are not touched.
- **Difficulty source:** there is no overall gameplay difficulty (`DifficultySettings.Overall` is
  a setup shortcut, "nothing reads Overall"). Key the discount on the run's stamped **Stack size**
  dial (`MetaState.Difficulty` / `BoardDifficulty`, `Steps.StackSize`), resolved in
  `DifficultyResolver` into a new `DifficultyProfile.WeeklyGoalStackDiscount` (0.5 / 0.25 / 0 / 0),
  same pattern as the other balance numbers. Note: an Easy board is already scaled to 0.75, so
  Easy's discount stacks on that.
- Only the week's goal lines. Other lines of the same bundle keep their full ask.

## Lifecycle

1. **Apply** when goal slots become the current week's goals: after
   `PopulateBonusSlotsForCurrentSelection()` in `RunController.SelectByName` (`RunController.cs`
   ~1045), and the other commit paths through `PopulateBonusSlotsForCurrentSelection` (month
   rollover on load ~149, slot migration ~167, the day-28 pre-pick committed in
   `DoDayStartSeasonAndHub` ~852-867). Host only (`Context.IsMainPlayer`).
2. **Donated during the week:** the slot is complete at the discounted stack. It stays complete.
3. **Revert** every un-donated discounted line to its original stack:
   - at the start of the next week, before the hub offer (`DoDayStartSeasonAndHub`, before
     `PresentOffer` at ~894 and before `BeginNewMonth` at ~852);
   - before a mid-week re-pick or reroll clears the slots (`RunState.Select`, ~302);
   - `BeginNewMonth` (~317) as above.
   - A rewind needs no revert: `WorldResetService` rewrites the whole board.
   - Revert a line only if it is not donated AND its live stack still equals the discounted
     value (another mod, e.g. Challenging CC Bundles, may have swapped the board).
4. `CurrentSelection` stays set past the week (`RunController` ~897 comment), so "slots present"
   does not mean "this week": track the discount explicitly (below).

## State

- `BonusSlot` gains `OriginalStack` (int, 0 = not discounted) next to `Stack`. On apply,
  `OriginalStack = Stack` and `Stack = discounted`; on revert, `Stack = OriginalStack` and
  `OriginalStack = 0`. `Stack` is what the quest text ("xN"), the hub icons and `tly_goals`
  already read, so they show the discounted ask with no display change.
- RunState records the week the discount was applied for (e.g. `DiscountWeek`), so the revert
  knows which slots to restore after save/load.

## Writing the board

- Rewrite the stack in `Game1.netWorldState.Value.BundleData` with `BundleAskRewrite`
  (`Core/BundleAskRewrite.cs`, rewrites field 2 stacks only) or a per-ingredient-index variant,
  then `SetBundleData`, then `cc.refreshBundlesIngredientsInfo()`.
- **Mirror every write into `MetaState.WrittenBoard[key]`**, exactly like
  `BoardRepairService.ClampUnstackableAsks` (~230-266). Otherwise the load-time check
  (`EngineManifestCheck.MatchesIgnoringDisplayName`, `ModEntry.cs` ~5199) fails on a mid-week
  save and the save drops to the legacy read-and-classify path.
- Vanilla bundle mode: update the board fingerprint (`_boardFingerprint`, `ModEntry.cs` ~650)
  after a write so `ReclassifyIfBoardChanged` does not rebuild needlessly.
- `BundleRequirement.IngredientStacks` only feeds Season Goals icons. Refreshing it is optional;
  the gate counts slots, not stacks.

## JP

No change. A completed line pays the rarity JP of one item regardless of stack
(`DonationObserver` ~162-166), weekly goal payouts and bundle completion are flat. So a
discounted line already pays the same as the full line, which is what Jeff asked for.

## Tests (Core, TDD)

- Pure `WeeklyGoalDiscount.Stack(original, d)`: 31/0.5→15, 31/0.25→23, 14→10, 11→10, 10→10,
  9→9, 49/0.5→24, 43/0.25→32, d=0 unchanged.
- `DifficultyResolver`: StackSize Easy→0.5, Normal→0.25, Hard/Extreme→0.
- Apply/revert on a `BonusSlot` list plus bundle data string: only goal lines change, quality
  untouched, other lines untouched, revert restores exactly, revert skips donated lines and lines
  whose live stack changed underneath.
- `WrittenBoard` mirrored so `EngineManifestCheck` still matches after apply and after revert.

## In-game verification (headless, per docs/HEADLESS_DRIVING.md)

1. Normal save: `tly_select <theme>`; `tly_goals` shows discounted stacks; bundle menu data shows
   them; log shows the apply.
2. Save + reload mid-week: log says "Requirements source: stored engine board", not the legacy
   fallback.
3. Advance to the next week start (`tly_setday 7`, `debug sleep`): un-donated lines back to full.
4. Donate one discounted line mid-week (`tly_playseason goals`): it stays complete after the week.
5. Hard save: no stack changes.

## Open for Jeff

- Should the hub's theme preview (before picking) show the discounted numbers, so the player sees
  the benefit when choosing? Default in this spec: no, the preview shows full stacks and the
  discount appears once picked.
