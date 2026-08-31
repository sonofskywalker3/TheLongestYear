# What the forage sweep counted that a loop cannot reach

`forage-sweep-results.csv` holds three full-year runs (loops 120/121/122) of `tly_sweepforage`.
The counts are real harvests, but they were taken on the throwaway save, which has **everything
already unlocked**. A measured count is only as honest as the access the measuring save had, and
that has now caught us three times.

## 1. Calico Desert (fixed v0.16.175)

Cactus Fruit and Coconut were counted from Spring 1, about 38 a season, on a save where the bus
already ran. A loop pays the Vault bundle first (`AvailabilityWeeks.SkullCavernWeek`, week 9).
Their Spring and Summer rows are dropped; the ceiling comes from Fall and Winter only.

## 2. Ginger Island (found 2026-08-30)

`tly_sweepforage` walks `Game1.locations`, and the throwaway save has the island created
(`IslandNorthCave1` is in the save file). That cave is the **only** island map with
`Data/Locations` forage rows, and it spawns four mushrooms at chance 0.9 in **every season**:

| Item | Every mainland forage row in the game | Verdict |
|---|---|---|
| Purple Mushroom `(O)422` | none at all | rows deleted; ruled ceiling 5 |
| Chanterelle `(O)281` | Woods, Fall, 0.5 | all four rows halved |
| Red Mushroom `(O)420` | Woods Summer 0.25, Woods Fall 0.2 | all three rows halved |
| Common Mushroom `(O)404` | Woods Spring 0.25; Mountain/Forest/Woods/Backwoods Fall | halved, but exempt anyway (Wild Seeds) |

Purple Mushroom is the proof: no mainland forage row exists anywhere in the game, yet the sweep
credited it 17-19 a season. Jeff's ruling, 2026-08-30: cap Purple Mushroom at 5 (the mines'
mushroom floors give about that, and it is farmable if you know what you are doing), and halve the
other mushrooms. The halving is a ruling, not a measurement, and is marked `island-halved` on every
row it touched in `ForageAskLimits`.

## 3. Secret Woods (STILL OPEN)

The Woods is behind the Steel Axe, which `LocationGating` puts at week 4.

- **Morel `(O)257`** is the sharp case: Woods-only and Spring-only, so its ceiling of 9 assumes a
  whole Spring of access when a loop gets at most the last week of one. Not yet corrected.
- **Fiddlehead Fern `(O)259`** is Woods-only too, but Summer is weeks 5-8, entirely after the gate,
  so access is not its problem. (Its stray Spring/Fall/Winter counts are a separate matter, and it
  sits under `MinMeasuredAverage` regardless.)

## What a re-run needs before it is worth doing

`tly_sweepforage` logs the day's item counts but **not which map each came from**, so the chest
totals cannot be attributed after the fact. Any future run should first teach the sweep to record
per-map, per-item counts, and to skip maps a loop cannot reach. Until then, treat every number in
the CSV as an upper bound taken with full access.
