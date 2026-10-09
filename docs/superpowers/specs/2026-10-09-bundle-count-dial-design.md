# Bundle-count dial: design (2026-10-09)

Requested by ThirteenRedCats (Nexus post, 16 Sep; TODO.md sweep of 2026-09-16). Investigation:
`docs/superpowers/notes/2026-10-09-bundle-count-dial-investigation.md`. PC 1.6 only.

## Jeff's rulings (2026-10-09)

1. The setting applies only when a new loop starts: the next board picks a different number of
   bundles. Nothing is ever removed mid-loop. A held board keeps its count.
2. TLY Custom (engine) boards only. Normal and Remixed stay vanilla. The Vault and The Missing
   Bundle are not affected.
3. Counts per room, floor 2:
   - **Easy:** one or two fewer than the room's standard count, never below 2.
   - **Normal:** the standard count, capped at 6.
   - **Hard:** one or two more than standard, capped at 8.
   - **Extreme:** always 9. If a room's pool cannot supply 9 bundles with different names, it
     takes as many as it can and the log says so.
4. Extra bundles roll like any other bundle and keep their own natural season. Bundle names stay
   unique within a room.
5. JP rewards are not scaled. Harder means you donate more, earn more JP and loop more.
6. It is one more difficulty dial: GMCM, the `tly_difficulty` report, the overall lever, stamped
   per save like the others.

## Design

### The dial

`DifficultySettings.BundleCount` (a `DifficultyStep`, default Normal), included in `SetAll`,
`Clone` and `IsAllNormal`. Not part of `AsksAllNormal`: it never applies to a vanilla board.

The resolver stamps a resolved rule, not the step (same principle as the other dials), as
`DifficultyProfile.BundleCount`, a `BundleCountRule`:

| Step | Change | Floor | Cap |
|---|---|---|---|
| Easy | -2 or -1 (rolled per room) | 2 | 6 |
| Normal | 0 | 2 | 6 |
| Hard | +1 or +2 (rolled per room) | 2 | 8 |
| Extreme | exactly 9 | | |

`Target(standard, rng)`: Extreme returns 9. Otherwise `standard + roll`, capped, then floored at
`min(2, standard)` (a room that only ever had one bundle is not grown by the floor). The rng is
consumed only when the change is a range, so Normal consumes nothing. A profile stamped before
the dial existed has `BundleCount == null`, which means "leave every room exactly as it is";
such a save regenerates its board unchanged.

"Standard count" is the number of positions the engine builds for the room today (one per
`Data/Bundles` key, after the vanilla-only template filter).

### Picking

Per themed room, in the engine's existing pass 1 (rooms in ordinal order):

1. `RemixSelector.PickForRoom` picks one bundle per standard position exactly as today. Normal
   with a vanilla board therefore writes the same board as before the dial.
2. The room's target is rolled from its own stream (`seed ^ BundleCountSalt`, salted by room), so
   no existing stream moves.
3. **Fewer:** drop picks at random (same per-room stream), never a seasonal Crops bundle while
   another pick can go. Dropped positions simply are not written.
4. **More:** each extra position draws from the room's whole candidate pool (every candidate of
   every position, authored bundles included, first occurrence of each name), leaving out every
   name already picked in the room. When no new name is left, the room stops short and logs it.
   Each extra gets an index from a reserved range (below).

Everything after pass 1 (fill, required slots, stack scaling, flavors, rewards, season gate,
weekly themes, JP) works per bundle already and needs no change. Extra bundles fall into whatever
season their own asks put them in.

### Reserved bundle indices

Vanilla 1.6.15 uses 0 to 36; Tech's Cross-Mod Bundles reuses vanilla's room/index keys. Extra
positions take indices from **9000 upward**, allocated in room order, skipping any index any room
pool already uses. Deterministic for a given seed and pool shape, so the load-time re-derivation
reproduces them.

### Removing stale bundles (the real work)

`SetBundleData` never removes a key, and the game re-adds `Data/Bundles`' default keys on every
load (the CommunityCenter constructor reads `BundleData` while it is still empty). So a board with
fewer bundles than the defaults gets its dropped bundles back on each load, and a board with
reserved indices is missing from the CC's private bundle-to-room lookup, which throws in
`checkForMissedRewards`.

`StaleBundleKeys` (Core, pure) lists the live keys in a room the board writes that the board does
not contain, and the indices no longer used by any board key. `BundleKeySync` (Mod) removes those
keys from `netBundleData`, `Bundles` and `BundleRewards` (reflection on the protected field), then
rebuilds the CC's `areaToBundleDictionary` and `bundleToAreaDictionary` by reflection (never by
re-calling `initAreaBundleConversions`, which also adds mutexes) and refreshes its ingredient
cache. It runs:

- in `BundleEngine.WriteToWorld`, before the write (every reset and the fresh run), and
- on every save load of an engine board, right after Tech's board-of-record restore and before
  the board repair and catalog, against the stored board of record.

Rooms the board does not write, and keys of rooms the board does write that it still contains,
are never touched.

### Held boards

A held board (Fail night "keep") re-stamps the difficulty at reset from live config. The count
rule is carried over from the previous stamp instead, so a held board keeps its shape.

## Out of scope

- More than 9 bags on a room page (the 9 fixed spots are the cap; Extreme is 9).
- Normal and Remixed boards, the Vault, The Missing Bundle.
- Scaling JP or anything else by the count.

## Testing

Unit: the rule table and floors/caps, the planner (fewer, more, unique names, crops protected,
short pool), reserved index allocation, stale-key finding, held-board carry, resolver and settings
plumbing, Normal = no change. Live: each step across a rewind on a throwaway farm, room pages
screenshotted, a reload in between, no KeyNotFound in the log.

## Amendment 2026-10-09: the Vault joins the dial

Jeff, 2026-10-09: "We need to change the vault. for easy drop the highest cost bundle, for hard add 1 more that's
double the current highest cost, and for extreme add another beyond that with another like x1.5-1.75 of the hard
bundle". This replaces "The Vault ... not affected" in ruling 2 and in Out of scope.

**Where.** TLY Custom (engine) boards only. The engine already owns the Vault on those boards (it writes the four
money bundles back with `VaultAmountMultiplier`, default 1.25, applied by `VaultAmountScaler`). Normal and Remixed
boards keep vanilla's Vault, as they keep every other room.

**The rule.** `BundleCountRule.VaultDelta`, stamped with the rest of the rule: Easy -1, Normal 0, Hard +1,
Extreme +2. A rule stamped before this amendment has 0, so a board in flight keeps four Vault bundles, and a held
board carries its whole rule (Vault included) as before. `VaultBundleCount` (Core, pure):

- **Kept:** a negative delta drops the priciest bundle(s), never below one.
- **Extras:** the first costs 2x the board's priciest Vault bundle, each next one 1.6x the one before (inside
  Jeff's 1.5 to 1.75), rounded to the nearest 500g. They copy the priciest bundle's reward and layout, take their
  own bag tints (5 and 6) and an index from the reserved range.
- **Prices are the board's own, after the multiplier**, so the ratios hold whatever the multiplier is:

| Step | Vanilla base prices | The board at the default +25% |
|---|---|---|
| Easy | 2,500 / 5,000 / 10,000 | 3,125 / 6,250 / 12,500 |
| Normal | 2,500 / 5,000 / 10,000 / 25,000 | 3,125 / 6,250 / 12,500 / 31,250 |
| Hard | + 50,000 | + 62,500 |
| Extreme | + 50,000 + 80,000 | + 62,500 + 100,000 |

**Indices.** The Vault extras are numbered after every themed room's extras (the engine allocates them once pass 1
is done), so the themed rooms' reserved indices never move. Easy's dropped bundle (Vault/26 on vanilla data) is
removed by the stale-key sync at the write and again on every load, like any dropped bundle.

**The season gate.** The Vault gate asks for the season ordinal (1 by Spring ... 4 by Winter) but never more than
the board has (`VaultRules.RequiredPaid`): an Easy board asks for 3 by Winter. Hard and Extreme still ask for 4 by
Winter; the extra bundles are needed only to finish the room (the bus). The keep_bus_unlocked reach ("bus:4")
counts an Easy board's full Vault (all 3 paid) as 4 (`VaultRules.BusReachValue`); on Hard and Extreme it still
unlocks at 4 paid, as before.

**Diagnostics.** `tly_gatecheck` prints this board's Vault prices and per-season counts from the live board
(`VaultBundleMap.PriceLadder`), not the vanilla 2,500 / 5,000 / 10,000 / 25,000 constants it used to print.
`tly_bundlecount buy [index|cheapest]` pays a Vault bundle through the page's own purchase button (open the page
in one batch, buy in the next, `close` after).

**Live check (2026-10-09, throwaway farm, game minimized, log only).** Each step across a `tly_reset` and a reload:
Easy 3 bags (3,125 / 6,250 / 12,500g), Normal 4, Hard 5 (+62,500g at 9008), Extreme 6 (+62,500g and 100,000g at
9019 and 9020). The Vault page drew every bag at its own spot; `checkForMissedRewards` clean; the CC lookup knew
every bundle. Paying through the purchase button took the gold, TLY recorded each payment and its JP, and the room
completed (ccVault queued, room JP paid) only on the last bundle: the 3rd on Easy, 4th on Normal, 5th on Hard,
6th on Extreme.
