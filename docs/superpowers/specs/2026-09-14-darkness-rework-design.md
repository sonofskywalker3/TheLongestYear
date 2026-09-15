# Darkness Rework

**Date:** 2026-09-14
**Status:** built in Part B, see `2026-09-15-darkness-obtainability-wiring-design.md`
**Branch:** `story`
**Replaces the scheduling in:** `2026-09-09-darkness-pushback-design.md` (effects, wards, letters,
scene and the Circle of Warding stay as that spec and its later rulings describe)
**Depends on:** `2026-09-14-item-obtainability-design.md`

## Problem

Live testing (2026-09-14) showed the three fronts rolling independently every night, so blight,
reversion and tampering could pile onto the same night and the same week. Jeff: "We can't have it
piling on like that." A blight night also hit crops and chests together; that was already changed to
one or the other (commit `c059373`). Separately, reversion and tampering take no account of whether
the player can recover, which on easier settings is "rage bait", and a tampered legendary fish could
ask for more than one.

## 1. One nightly roll with a decaying weekly chance

- Each ordinary night (never day 28, never the win night) rolls once: does the darkness strike?
- The chance starts each week at the season's value: **Summer 25%, Fall 35%, Winter 35%**. No
  darkness in Spring.
- Each strike lowers the chance by **5 percentage points** for the rest of that week. It resets to the
  season's value at the start of each week.
- The same chance on every Darkness level (Jeff, option A); only the hits scale.

## 2. One event per strike, split evenly

A strike picks exactly one event, evenly among what the season offers:

| Season | Options (each equally likely) |
|---|---|
| Summer | crop blight, chest blight |
| Fall | crop blight, chest blight, reversion |
| Winter | crop blight, chest blight, reversion, tampering |

The existing limits still apply: blight on at most 2 nights a week; reversion at most once a week and
never from day 25; tampering at most twice a Winter, at least 5 days apart, never from day 21.

If the picked option is capped or has nothing to act on (no live crops, nothing stored, no filled
slot in an unfinished bundle, no fair candidate), the pick moves evenly to the remaining options. If
none can act, there is no strike that night and the chance does not drop.

## 3. The Darkness difficulty dial

- An **11th dial, Darkness**, in the Difficulty section. The overall Difficulty lever sets it with the
  other ten; the lever itself stays a setup shortcut that no gameplay reads.
- Stamped into the save's difficulty profile at reset like the other dials, so a change takes effect
  at the next rewind.
- **Migration** (Jeff): existing configs and saves set the overall lever AND the Darkness dial to the
  **lowest** of the player's ten existing dials. A save stamped before the Darkness dial existed
  derives it the same way from that stamp.

## 4. Blight by Darkness level

Jeff's numbers are Normal; share moves 1 point per level, caps about 20% per level. Crop blight takes
the share of live crops; chest blight takes the share of stored units (Junimo Stash excluded). At
least 1 either way.

| Darkness | Share | Summer cap | Fall / Winter cap |
|---|---|---|---|
| Easy | 4% | 8 | 12 |
| Normal | 5% | 10 | 15 |
| Hard | 6% | 12 | 18 |
| Extreme | 7% | 14 | 21 |

## 5. Reversion fairness

- **Easy:** only empty a slot whose item can be obtained again, from tomorrow to the end of the year,
  through dependable sources, AND is a low-effort item. (The effort cut-off is set in the plan once
  the obtainability comparison is read.)
- **Normal:** only a slot whose item can be obtained again from tomorrow to the end of the year,
  through dependable sources.
- **Hard, Extreme:** any filled slot in an unfinished item-room bundle, as today. Jeff: players
  choosing these "are asking to get the wind knocked out of their sails"; they will hold spares.

## 6. Tampering

- **Guaranteed hit (Jeff, option B).** The first time a save reaches Winter, tampering strikes on
  Winter 1. Every later Winter it strikes on a random night of week 1. It counts as that week's first
  strike (the chance drops 5 points) and uses one of the two tampers.
- **Replacement pool by Darkness level:**
  - **Easy:** items obtainable in Winter through dependable sources from normal play. The Traveling
    Cart is not a source here. Not "items ONLY obtainable in Winter".
  - **Normal:** Easy's pool, plus anything Pierre or Sandy sells, plus crops that Mixed Seeds grow in
    the greenhouse.
  - **Hard, Extreme:** anything.
- **Quantity never exceeds what can really be obtained.** The tampered stack goes through the board's
  own limits: legendary fish are always 1 (`LegendaryFishRules`, as `QuantityAskPass` already does);
  never above the item's basis ceiling by the deadline; the gold-quality reduction when a quality is
  asked; an item with no basis asks for 1. Today's tamper path skips the legendary rule, which is a
  bug this fixes.
- The tamper stack's slice keeps scaling by level (Easy 0-10%, Normal 10-20%, Hard 20-30%, Extreme
  30-40%), now read from the Darkness dial.

## 7. Dropped

- A rule forcing at least one bundle of truly Winter-only items. The board just rolls; feedback
  decides (Jeff).

## Testing

Pure rules in Core with unit tests for the roll, the decay and weekly reset, the even split and its
fall-through, each level's blight numbers, the reversion and tamper fairness filters against a fake
obtainability model, the guaranteed Winter tamper, the stack limits (legendary = 1), and the
migration's lowest-dial rule. Live verification per front with `tly_sabotage arm`, staged so Jeff
sleeps into each one.
