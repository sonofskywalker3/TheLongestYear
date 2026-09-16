# Crab pot measurement, 2026-09-16 (TODO item D)

My automated run over the headless bridge, build 0.18.12, throwaway save `None_449262005`
(Run 183), Spring. `tly_crabpots place 10` on Spring 1 put 10 baited pots in each of the four
zones (Beach = ocean; Forest, Town, Mountain = fresh water), then a daily `tly_crabpots` collect
and re-bait followed by `debug sleep`, stopping on Spring 23 so the day-28 night never came.
Four mornings collected nothing because the collect ran twice on the same day (the sleep wait
matched early), so the sample is **22 real collection days**, every one of them 40 items from
40 pots. Raw chest: 880 items, 15 distinct.

## Raw counts (22 days, 10 ocean pots + 30 fresh pots)

| Item | Count | Water |
|---|---|---|
| Periwinkle | 145 | fresh |
| Crayfish | 133 | fresh |
| Snail | 122 | fresh |
| Shrimp | 29 | ocean |
| Clam | 26 | ocean |
| Mussel | 22 | ocean |
| Cockle | 22 | ocean |
| Oyster | 17 | ocean |
| Crab | 15 | ocean |
| Lobster | 10 | ocean |
| Junk (Newspaper, Trash, CD, Glasses, Driftwood) | 339 | both |

Junk share: 36% of ocean pot-days, 39% of fresh pot-days.

## Against the modelled basis (`QuantityBasisTables.CrabPot`, per week at 10 pots in the right zone)

Ocean rows scale by 7/22 (10 pots); fresh rows by 7/22/3 (30 pots).

| Item | Measured per week, 10 pots | Modelled | Ratio |
|---|---|---|---|
| Lobster | 3.2 | 2.8 | 1.14 |
| Crab | 4.8 | 5.3 | 0.90 |
| Oyster | 5.4 | 7.2 | 0.75 |
| Clam | 8.3 | 6.1 | 1.36 |
| Shrimp | 9.2 | 6.9 | 1.34 |
| Cockle | 7.0 | 8.3 | 0.84 |
| Mussel | 7.0 | 6.8 | 1.03 |
| Periwinkle | 15.4 | 15.0 | 1.03 |
| Crayfish | 14.1 | 14.7 | 0.96 |
| Snail | 12.9 | 14.0 | 0.92 |

## Reading

The hand table from `CrabPot.DayUpdate` holds. Every fresh-water row is within 8% of the
measurement, and the ocean rows sit within the noise of ten pots over three weeks (the ocean
counts are 10 to 29 items each, so a swing of a third is one or two pots' luck). Nothing is off
by the 2.3x that sank the forage expected-value pass. Jeff's 2026-09-04 rule "pots do not change
with the season, so one season extrapolates" was the premise of running Spring only.

**Recommendation: keep the modelled crab pot bases as they are; no code change.** Cockle, Mussel,
Oyster and Clam already add this basis to their measured forage mean in `QuantityAskPass`, so
the "still unclamped" note in the TODO is stale: they are banded like everything else. Jeff to
confirm, then the D item closes.
