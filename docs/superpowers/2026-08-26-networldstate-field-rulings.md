# NetWorldState keep/wipe rulings - the complete field table

**Status:** closes the audit opened in `HANDOFF-2026-08-26-networldstate-audit.md`. Supersedes the
partial table in `specs/2026-07-13-networldstate-audit-design.md`, which ruled most of the class but
missed one field entirely and got two rulings wrong.

**Source:** `StardewValley.Network/NetWorldState.cs` in the Android decompile at
`C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android\decompiled`. The installed PC
build is also **1.6.15**, so for this class there is no PC/Android divergence to reason about; every
line reference below is to that decompile.

**Method:** every field declaration in the class, not a sample. The class declares **51 net fields**
plus **3 plain cache fields**; all 54 appear below.

**Philosophy:** wipe by default. Keep only for (a) meta-progression the mod owns, (b) a cross-loop
memory the mod deliberately maintains, or (c) wiping breaks vanilla worse than the leak - and (c)
has to say how.

---

## The table

`already handled` = the reset covers it in a dedicated step; the ruling is recorded here for
completeness, the code is elsewhere.

| # | Field | Ruling | Reason |
|---|---|---|---|
| 1 | `uniqueIDForThisGame` | **WIPE - new (step 2c)** | Step 0 re-seeds `Game1.uniqueIDForThisGame`; the netWorldState copy stayed stale until save. `WriteToGame1` has the reverse sync, and its `!IsServer` branch runs in single-player, copying the old value back over Game1 (called on every farmEvent finish, Game1.cs:4982) - which would undo the re-seed the new run's weather and forage RNG depend on. Now synced by `UpdateFromGame1`. |
| 2 | `serverPrivacy` | KEEP | Multiplayer session config, not progression. |
| 3 | `whichFarm` | KEEP | Save-level farm choice. |
| 4 | `whichModFarm` | KEEP | Save-level farm choice. |
| 5 | `_oldModFarmType` | KEEP (cache) | Plain field, not a net field. The `WriteToGame1` change-detection latch for #4; wiping it only forces one redundant farm-type lookup. |
| 6 | `shuffleMineChests` | KEEP | The remixed-mine-rewards new-game option - save-level config, same class as remixed bundles. |
| 7 | `minesDifficulty` | WIPE (1d, 2026-07-13) | Shrine of Challenge toggle - run-scoped power. |
| 8 | `skullCavesDifficulty` | WIPE (1d, 2026-07-13) | Shrine of Challenge toggle - run-scoped power. |
| 9 | `highestPlayerLimit` | KEEP | Multiplayer session config. |
| 10 | `currentPlayerLimit` | KEEP | Multiplayer session config. |
| 11 | `year` | already handled (step 2 + 2c) | Calendar rewind, then `UpdateFromGame1`. |
| 12 | `season` | already handled (step 2 + 2c) | Calendar rewind, then `UpdateFromGame1`. |
| 13 | `dayOfMonth` | already handled (step 2 + 2c) | Calendar rewind, then `UpdateFromGame1`. |
| 14 | `timeOfDay` | already handled (step 2 + 2c) | Calendar rewind, then `UpdateFromGame1`. |
| 15 | `daysPlayed` | already handled (step 2 + 2c) | `Game1.stats.DaysPlayed = 1`, then `UpdateFromGame1`. |
| 16 | `visitsUntilY1Guarantee` | **RE-ROLL - corrected (1d)** | Was set to -1 as the new-game sentinel. It is not: -1 means DISABLED, and both consumers gate on `>= 0` (Forest.cs:763, Game1.cs:9020). Vanilla rolls it once at save creation under the `YearOneCompletable` option (Game1.cs:4204-4219), and `loadForNewGame` cannot re-roll it during a reset because `newGameSetupOptions` is empty outside the new-game flow. So the old line permanently killed the red-cabbage guarantee on such a save at its first rewind. Now re-rolled with the vanilla formula, only when it was already armed. |
| 17 | `isPaused` | KEEP | Live session state. |
| 18 | `isTimePaused` | KEEP | Live session state. |
| 19 | `locationWeather` | already handled (step 2b) | Vanilla's day-start weather chain re-runs for the rewound date. |
| 20 | `isRaining` | already handled (step 2b) | Legacy mirror of the Default `LocationWeather`; `ApplyWeatherForNewDay` rewrites it. |
| 21 | `isSnowing` | already handled (step 2b) | As above. |
| 22 | `isLightning` | already handled (step 2b) | As above. |
| 23 | `isDebrisWeather` | already handled (step 2b + 2c) | As above; also written by `UpdateFromGame1`. |
| 24 | `weatherForTomorrow` | already handled (step 0 + 2b) | Set to Sun at step 0, then re-resolved by the weather chain. |
| 25 | `bundles` | already handled (step 1a) | CC completion rewinds; arrays zeroed in place so vanilla's `bundles[i]` lookups cannot throw. |
| 26 | `bundleRewards` | already handled (step 1a) | As above. |
| 27 | `netBundleData` | already handled (step 1a) | `loadForNewGame` regenerates the board; `SetBundleData` re-upserts. |
| 28 | `_bundleData` | KEEP (cache) | Plain field. Derived view of #27, rebuilt whenever `_bundleDataDirty` is set. |
| 29 | `_bundleDataDirty` | KEEP (cache) | Plain field. Set by `SetBundleData`, which step 1a calls. |
| 30 | `raccoonBundles` | WIPE (1d, 2026-07-13) | Raccoon request chain - run-scoped. |
| 31 | `seasonOfCurrentRacconBundle` | WIPE to -1 (1d, 2026-07-13) | As above; -1 is this field's real default. |
| 32 | `parrotPlatformsUnlocked` | WIPE (1d, 2026-07-13) | Ginger Island progression. |
| 33 | `goblinRemoved` | WIPE (1d, 2026-07-13) | Witch Swamp progression. |
| 34 | `submarineLocked` | WIPE (1d, 2026-07-13) | Night Market progression. |
| 35 | `lowestMineLevel` | already handled (step 6) | Pinned to the kept elevator floor (cap-not-grant). |
| 36 | `lowestMineLevelForOrder` | already handled (step 6) | As above. |
| 37 | `museumPieces` | already handled (step 1b) | Museum rewinds; otherwise the reward ladder re-arms every loop. |
| 38 | `lostBooksFound` | already handled (step 1c) | Library shelf rewinds with the museum. |
| 39 | `goldenWalnuts` | WIPE (1d, 2026-07-13) | Island currency - run-scoped. |
| 40 | `goldenWalnutsFound` | WIPE (1d, 2026-07-13) | Island progression tally. |
| 41 | `goldenCoconutCracked` | WIPE (1d, 2026-07-13) | One-time island unlock. |
| 42 | `foundBuriedNuts` | WIPE (1d, 2026-07-13) | Per-nut found markers. |
| 43 | `miniShippingBinsObtained` | WIPE (1d, 2026-07-13) | Perfection-adjacent counter. |
| 44 | `perfectionWaivers` | WIPE (1d, 2026-07-13) | Perfection-adjacent counter. |
| 45 | `timesFedRaccoons` | WIPE (1d, 2026-07-13) | Raccoon chain. |
| 46 | `treasureTotemsUsed` | WIPE (1d, 2026-07-13) | Progression counter. |
| 47 | `farmhandData` | KEEP | Multiplayer session data. Empty in single-player, which is what TLY supports. Flagged as the one KEEP that would need revisiting if the mod ever went multiplayer, since it holds whole `Farmer` objects with their own progression. |
| 48 | `locationsWithBuildings` | KEEP | Engine-maintained index of location NAMES, refreshed by `UpdateBuildingCache` on every add and remove. Its only consumers count live buildings through `getLocationFromName` (Game1.cs:6505/6515), so a stale entry resolves to zero rather than granting anything. |
| 49 | `builders` | WIPE (1d, 2026-07-13) | In-flight Robin/Wizard builds point at buildings the world wipe deleted - same class as Clint's `toolBeingUpgraded`. |
| 50 | `activePassiveFestivals` | WIPE (1d, 2026-07-13) | Daily ephemera the skipped day-start would refresh. |
| 51 | `worldStateIDs` | WIPE (1d, 2026-07-13) | One-time world flags (trash bear, map states). Both this and the `Game1.worldStateIDs` static are cleared so neither re-syncs into the other. |
| 52 | `islandVisitors` | WIPE (1d, 2026-07-13) | Island progression. |
| 53 | `checkedGarbage` | WIPE (1d, 2026-07-13) | Daily ephemera. |
| 54 | `dishOfTheDay` | **WIPE to null - corrected (1d)** | Was kept on the reasoning that `loadForNewGame` re-rolls it. It does not: `Game1.UpdateDishOfTheDay` (Game1.cs:9432) is reached only from the `_newDayAfterFade` night chain the rewind skips, so the Saloon opened Spring 1 still selling the dish from the day the reset fired. A real vanilla Spring 1 has none for the same reason, and every consumer null-checks (ItemQueryResolver.cs:145, DefaultPhoneHandler.cs:394). |
| 55 | `activatedGoldenParrot` | WIPE (1d, 2026-07-13) | Island endgame unlock. |
| 56 | `daysPlayedWhenLastRaccoonBundleWasFinished` | WIPE (1d, 2026-07-13) | Raccoon chain timing. |
| 57 | `canDriveYourselfToday` | WIPE (1d, 2026-07-13) | Daily ephemera. |
| 58 | `goldenClocksTurnedOff` | WIPE (1d, 2026-07-13) | Preference on a building the world wipe removed. |
| 59 | `netQuestOfTheDay` | **WIPE - new (step 2d)** | Missed entirely by the 2026-07-13 pass. `loadForNewGame` refreshes it (Game1.cs:4229) while the calendar is still the PRE-reset one, so the quest was seeded off the old run's `DaysPlayed` (`getQuestOfTheDay` seeds on `DaysPlayed * 777`) and, on the `SlayMonsterQuest` branch, gated on the old run's `MineShaft.lowestLevelReached` - which step 6 has not yet pinned back to the kept elevator floor. Nothing re-rolled it afterwards, so Spring 1 of every loop opened with a quest, and its gold reward, that a genuine day 1 cannot offer: vanilla returns null outright while `DaysPlayed <= 1`. Now refreshed after the calendar rewind. |

Row numbers run to 59 because the five weather mirrors and the three cache fields are listed
individually rather than grouped; the class itself declares 51 net fields plus 3 plain fields.

---

## What changed in code

Five commits, 0.14.4 through 0.14.8:

1. `fix: drop the no-op netWorldState.Date writes in the reset` - `NetWorldState.Date` is
   `=> WorldDate.Now()`, a computed property returning a fresh `WorldDate` built from the Game1
   statics, so the three `Date.X = ...` assignments in the calendar rewind wrote to a throwaway
   object and never reached netWorldState. Removed, with the reason recorded so they do not
   come back.
2. `fix: sync netWorldState from Game1 after the reset's calendar and weather rewind` - row 1.
3. `fix: re-roll the quest of the day after the rewind instead of carrying the old run's` - row 59.
4. `fix: clear the dish of the day at reset` - row 54.
5. `fix: re-roll the year-1 cart guarantee at reset instead of disabling it` - row 16.

865 tests pass, unchanged. Every one of these touches live `Game1` statics inside
`WorldResetService`, which the Core test project cannot construct, so the table plus a live smoke is
the real evidence - as the handoff anticipated.

---

## Verification status

**Smoked in game 2026-08-26 on the throwaway save (`None_447449779`), v0.14.9.** Log archived at
`log-archive/SMAPI-v0.14.10-20260826-211537.txt`; the probe used is `tly_netstate`, added for this
(read-only, rows labelled with their table numbers above).

The setup: load the save, `debug nd` five times to reach Spring 6 with `DaysPlayed=6`, probe, then
`tly_reset`, then probe again.

| Check | Before reset | After reset | Verdict |
|---|---|---|---|
| Row 59 `QuestOfTheDay` | `FishingQuest "Fishing: Catfish"` | `null` | PASS |
| Row 54 `DishOfTheDay` | `Tortilla x1` | `null` | PASS |
| Row 1 `uniqueIDForThisGame` | 447449779 | 447470044, and it stayed re-seeded | PASS |
| `DaysPlayed` | 6 | 1 | PASS |
| JP | 38 | 38 | PASS |
| Upgrades | 34 | same 34 | PASS |
| Stash | 3 items banked | `restored 3/3 items into stash chest` | PASS |
| Kept buildings | coop, barn, silo, stable | all replaced at their snapshotted tiles | PASS |
| Spring 1 weather | - | Sun, and the week-ahead forecast is a varied Sun/rain mix off the new seed | PASS |

The reset log line is `netWorldState leftovers wiped (... daily ephemera, dish of the day)`, and the
run came back as `Run 45 loaded (Spring 1). JP banked: 38.` No exceptions anywhere in the reset.

**The dish leak was caught in its natural state, before any of this ran.** The very first probe, on
the save as it sat at Spring 1 with `DaysPlayed=1` from a PREVIOUS reset made by the old code, read
`DishOfTheDay = Trout Soup x3`. A genuine vanilla Spring 1 has none. That is the bug, observed on a
real save rather than argued from the decompile.

**Still not verified:** row 16 (`visitsUntilY1Guarantee`). It reads -1 on this save, meaning the
guarantee was never armed, and arming it needs a save created with the `YearOneCompletable`
new-game option. The re-roll branch therefore never executed. Deliberately left unsmoked - it needs
a purpose-made save, and the wrong-in-the-old-code direction (permanently disabling the guarantee)
is not reachable on any save that has it at -1 already.

---

## Left open

- **`farmhandData` (row 47)** stays KEEP on the grounds that TLY is single-player. If multiplayer
  support is ever added, that field holds whole `Farmer` objects and becomes a progression leak.
  Carried into TODO.md as its own item rather than dropped.
