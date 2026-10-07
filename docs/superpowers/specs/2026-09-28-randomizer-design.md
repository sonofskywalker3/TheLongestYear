# Randomizer settings section: design

Status: DRAFT, design agreed in chat (Jeff, 2026-09-28). Not scheduled: target release 1.1, after the
story release, while players wait for Year 2 (2.0).
Origin: Nijah, Nexus posts tab, 28 Sep (theme reroll repeats, fixed items, fixed pairings). Jeff turned
the thread into a Randomizer section, 2026-09-28 (TODO.md, "RANDOMIZER settings section").
Branch: `randomizer` (off `master`). Do not bump `manifest.json` here; `master` owns the version.

## What it is

A new **Randomizer** section in the settings menu (GMCM). Every option trades the shipped balance for
variety, and every option is **off by default**, so a player who never opens the section plays the
game exactly as balanced. The one exception is an existing switch: a player who already turned on
"Allow re-rolling the weekly themes" moves to rerolls = Free.

Ten options:

1. Theme rerolls (Off / Costs JP / Free), including the reroll bug fix
2. Random theme items
3. Random buff/debuff pairings
4. Random weekly JP multiplier
5. Mystery card
6. Double theme week
7. Random shrine donations
8. Wildcard days
9. Random bundle rewards
10. Random cart days

## Timing rules (all options)

- Weekly options (1 to 8, 10) take effect from the **next weekly offer**. Whatever the current week
  rolled stays as rolled, so nobody flips a setting mid-week to dodge a goal.
- Random bundle rewards (9) take effect at the **next loop**, when the board is built.
- Everything a week rolls (offer, rerolls seen, multipliers, mystery card, pairings, shrine goals,
  wildcard day, cart days) is persisted on `RunState` so a reload reproduces the week exactly.
- All rolls are seeded from the run seed and the week, like the current offer, so they are
  deterministic per save.

## 1. Theme rerolls

Replaces the on/off `EnableThemeReroll` with a three-way setting: **Off**, **Costs JP**, **Free**.
Migration: `EnableThemeReroll = true` becomes Free; false becomes Off.

- **Cost** (Costs JP): 50 JP for the first reroll of the week, doubling each time (50, 100, 200, ...),
  back to 50 each week. Flat on purpose, not season-scaled (Jeff): in Spring one reroll costs more than
  the whole week's bonus, so rerolls are not a pick-anything-every-week option unless the player sets
  them to Free. The button shows the price and is greyed out when the player cannot afford it.
- **Which themes a reroll may offer**: any theme not picked this month that can ask for at least
  **one** goal. The normal offer's floor is two; the reroll drops it to one so a thin week still has
  something else to show. It never drops to zero: a theme with no goals lifts the drawback for free.
- **No repeats**: a reroll never shows a pair already shown this week until every pair of the allowed
  themes has been shown; then the cycle starts over.
- **Rerolls stick**: the rerolled offer and the list of pairs seen are saved for the week. Closing the
  hub no longer throws away a pair the player paid for.
- **The bug this fixes** (Nijah): the reroll shuffled only the themes that qualified. In a week with
  exactly two qualifiers it could only ever show the same pair.
- The normal weekly offer (weighted by askable goals, floor two) is unchanged.

## 2. Random theme items

The weekly goals are drawn **evenly** from every open line the theme can ask for: no gate-due-first
tier, no effort weighting by season. Applies to every roll, normal and reroll.

Kept, because they prevent impossible goals rather than set difficulty:
- an item must be obtainable by that week (the availability model);
- a bundle is never asked for more lines than it can still take;
- group caps (at most one fruit-tree fruit, capped crab-pot catches);
- the week's goal budget (how many goals).

## 3. Random buff/debuff pairings

Each offered card keeps its theme's **buff** but draws a random **drawback** from the eight drawbacks,
shown on the card and fixed once offered.

A theme never draws a drawback that blocks its own goals:

| Theme | Excluded drawbacks |
|---|---|
| Foraging | foraging off |
| Farming | crops grow slower |
| Fishing | fish bite slower |
| Mining | mines closed |
| Spelunking | mines closed |
| Artisan | machines slower |
| Kitchen, Mixed | none |

The exclusion table lives in Core next to `ThemeModifiers` and is unit-tested.

## 4. Random weekly JP multiplier

Each card shows its own multiplier, **0.5x to 1.5x** in steps of 0.05. It multiplies **only the JP
from that theme's goals**: the weekly bonus and donations into goal slots (CC and shrine). Other JP
(ordinary donations, bundle and room bonuses) is untouched. With the option off, cards show 1x (the
line is hidden unless a mystery card is on offer).

## 5. Mystery card

About **1 week in 4** (seeded roll per week), one of the two cards is dealt **face down**. It shows only
its multiplier, always **1.25x to 1.75x**, to reward the risk. Theme, buff, drawback and goals are
revealed when the player picks it.

- Same card size and position as a normal card; only the card back and multiplier are new drawing.
  No third card (Jeff: GUI risk).
- The sealed theme is drawn by the same rules as a normal card.
- A reroll on a mystery week keeps one card sealed.
- Never on a double week.
- With the random multiplier off, the face-up card shows 1x.

## 6. Double theme week

**Once a season**, in week 2 or week 3 (seeded), the hub says "Double week" and the player takes
**both** cards.

- Both buffs, both drawbacks, and two goal lists in the quest log.
- Each list lifts its own drawback when done.
- Both themes count as used for the month.
- Not in week 1 (thin goal pools) or week 4 (the gate crunch, where two drawbacks could fail the season).

Needs real rework: `ActiveEffectsProvider` holds one bonus and one liability today, and `RunState` one
current selection. Both become lists (of at most two); every caller that reads the single selection is
updated. The quest service shows two sections.

## 7. Random shrine donations

Tops the week's goal list up with items that have **no Community Center slot**, donated at the farm
statue.

- **Target size** by the **Required Slots** difficulty dial: Easy 3, Normal 4, Hard 5, Extreme 6 goals
  in total. Shrine goals fill the gap between the CC goals and the target; none if the CC goals already
  reach it.
- **Item pool** by the **Item Rarity** dial: items of the theme's kind (`ThemeDomains`) that have no
  slot on this board.
  - Easy and Normal: obtainable **this week**.
  - Hard and Extreme: obtainable this week **or at any earlier point this loop** (the item's first
    possible week, `ItemAvailability.HardWeek`, is at or before this week). Missed the rainy-day catfish
    in week 2? You should have stocked up.
- **Stack** by the same rules a bundle slot for that item would use, scaled by the Stack Size dial.
- **Pays** the same JP a CC donation of that item would (`JpCalculator.PerItem`, rarity from price),
  times the goal-slot bonus and the card's multiplier.
- **Donate tab**: a fourth tab on the farm statue's menu (`ShrinePreviewMenu`) showing this week's
  shrine goals as slots with the inventory below; click an item to donate, like a CC bundle page.
- **Drawback**: lifts only when every goal (CC and shrine) is done. Missing shrine goals is never a
  fail: the player earns less and keeps the drawback.

Groundwork: today's item pool (`ItemAvailabilityModel`) covers only bundle-pool items. The shrine pool
needs availability data for off-board items of each kind; build it from the same model inputs, and log
any item it cannot place so it never becomes a goal.

## 8. Wildcard days

One random day a week gets a twist for that day only.

- Never a festival day, never the last day of the season.
- The **day** shows in the quest log from the start of the week; the **twist** is revealed that
  morning with a HUD message and shown in the quest log and on the statue's Active tab.
- Pool:
  - **Good**: double forage, faster fish bites, crops grow an extra day overnight, shops 25% off, max luck.
  - **Bad**: mines closed, slower fish bites, sell prices down 25%, energy drains faster.
  - **Odd** (Jeff: things the game already does, in the wrong way):
    - Out-of-season snow: snow outside Winter. Crops do not grow that night; animals stay inside and lose
      happiness without a heater. Nothing dies.
    - A night event forced: one of the game's own random night events (crop fairy, witch, meteorite,
      stone owl, strange capsule).
    - Farm debris returns: large stumps, boulders and logs respawn on the farm.
    - Rockslide: the path to the mines is blocked for the day; only the minecart gets there. Before the
      minecarts are repaired it is not drawn (it would duplicate "mines closed").
- Good and bad effects reuse the existing effect ids where one exists (the theme buff and drawback
  patches), so a wildcard day is a one-day effect layered on the week's effects.

## 9. Random bundle rewards

Each loop, every bundle's reward is replaced with a random reward drawn from **any** bundle's reward
list (every vanilla and remixed bundle), with **no value matching** (Jeff: swingy on purpose).
Applied when the board is built, through `BundleSpec.RewardField`, on both board sources.

## 10. Random cart days

At the start of each week, every day gets its own seeded chance of a Traveling Cart visit.

- Averages about two visits a week, never zero (if the roll gives none, one day is forced).
- Festival days are skipped. The Night Market is unchanged.
- The Cart Stall preview shows the week's cart days.
- New Harmony patch on `Forest.ShouldTravelingMerchantVisitToday`. The three places that hard-code
  Friday and Sunday (`CartStockPreview.CartVisitDaysInWeek`, `ShrinePreviewMenu.TravelingCartVisitsToday`
  and `NextCartVisitDay`) move to one shared Core schedule.

## Config shape

A nested `Randomizer` block on `GameplayConfig` (not flat fields), one property per option, all off by
default, with its own GMCM section title. Rerolls is an enum (Off, CostsJp, Free). Migration of
`EnableThemeReroll` runs once at config load, like the existing stash-tile migration.

## Build order

Each step is releasable on its own behind its switch:

1. Rerolls (fix, setting, cost, persistence)
2. Random theme items, random pairings
3. Random multiplier, mystery card
4. Random bundle rewards
5. Random cart days
6. Wildcard days
7. Double theme week
8. Random shrine donations (largest: new tab, new goal credit, wider item pool)

## Out of scope

- Random starting kit (rejected by Jeff, 2026-09-28).
- Any change to the default, non-randomized game.

## Testing

- Core unit tests: reroll pair cycling and the floor-of-one rule, pairing exclusions, multiplier ranges
  and steps, mystery-week frequency, double-week placement, shrine target sizes per dial and pool
  filters per dial, cart-day rolls (average, never zero, festival skip), wildcard day placement,
  reward shuffle source list, config migration.
- Headless game driving for each option in turn (see `docs/HEADLESS_DRIVING.md`).
- A live pass before release, launches labelled as Jeff's or Claude's, asked for before taking the desktop.
