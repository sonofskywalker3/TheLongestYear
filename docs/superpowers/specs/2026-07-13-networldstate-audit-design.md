# NetWorldState keep/wipe audit — one-time full field pass (v0.11.45)

**Why:** bundles (0.9.x), museum pieces (0.11.25), and lost books (0.11.37) were each caught
ONE REPORT AT A TIME from the same survival class: fields on `Game1.netWorldState` that the
reset's `loadForNewGame` path never rebuilds. `NetWorldState` is a finite class — this audit
enumerates every field once (decompile `StardewValley.Network/NetWorldState.cs`), rules each
keep/wipe against the full-reset philosophy (wipe-by-default; keep only save-level config and
session state), and implements the wipes in `WorldResetService.ResetNetWorldStateLeftovers()`.
Companion to the 0.11.38 StatResetRules flip (same philosophy: enumerate the exemptions).

## Ruling table

| Field | Ruling | Why |
|---|---|---|
| year / season / dayOfMonth / timeOfDay / daysPlayed | **already reset** (step 2) | calendar rewind |
| locationWeather / isRaining / isSnowing / isLightning / isDebrisWeather / weatherForTomorrow | **already reset** (0.11.43, step 2b) | vanilla day-start chain re-runs |
| bundles / bundleRewards / netBundleData | **already reset** (step 1a) | CC rewinds; loadForNewGame regenerates bundle data |
| museumPieces | **already reset** (0.11.25, step 1b) | museum rewinds |
| lostBooksFound | **already reset** (0.11.37, step 1c) | library rewinds |
| lowestMineLevel / lowestMineLevelForOrder | **already reset** (0.9.38, step 6) | pinned to kept elevator floor |
| goldenWalnuts / goldenWalnutsFound / goldenCoconutCracked / foundBuriedNuts / islandVisitors / parrotPlatformsUnlocked / activatedGoldenParrot / goblinRemoved / submarineLocked | **WIPE (new, 1d)** | Ginger Island & endgame progression — run-scoped |
| miniShippingBinsObtained / perfectionWaivers / treasureTotemsUsed | **WIPE (new, 1d)** | progression counters |
| timesFedRaccoons / seasonOfCurrentRacconBundle / daysPlayedWhenLastRaccoonBundleWasFinished / raccoonBundles | **WIPE (new, 1d)** | raccoon-request chain — run-scoped |
| minesDifficulty / skullCavesDifficulty | **WIPE (new, 1d)** | Shrine of Challenge toggle — run-scoped |
| builders | **WIPE (new, 1d)** | in-flight Robin/Wizard builds reference wiped buildings (same class as Clint's toolBeingUpgraded) |
| worldStateIDs (mirrors static Game1.worldStateIDs) | **WIPE (new, 1d)** | one-time world flags (trash bear, map states) — run-scoped; both sides cleared so nothing re-syncs |
| activePassiveFestivals / checkedGarbage / canDriveYourselfToday | **WIPE (new, 1d)** | daily ephemera the skipped vanilla day-start would refresh |
| goldenClocksTurnedOff | **WIPE (new, 1d)** | preference on a building the wipe removed |
| visitsUntilY1Guarantee | **WIPE → -1 (new, 1d)** | Traveling Cart year-1 red-cabbage guarantee — every loop gets the same window |
| whichFarm / whichModFarm | **KEEP** | save-level farm choice |
| shuffleMineChests | **KEEP** | new-game "remixed mine rewards" option — save-level config, like remixed bundles |
| serverPrivacy / highestPlayerLimit / currentPlayerLimit / farmhandData | **KEEP** | multiplayer/session config, not progression |
| isPaused / isTimePaused | **KEEP** | live session state |
| locationsWithBuildings | **KEEP** | engine-maintained index; rebuilt as buildings register |
| dishOfTheDay | **KEEP** | re-rolled by loadForNewGame during the reset |

## Verification
Unit tests unaffected (546 pass; the method is shell-side over live `Game1`). Live check
rides any future reset: the Trace line `netWorldState leftovers wiped` plus, on a save that
had progressed, walnut/raccoon/difficulty state visibly rewound. No dedicated playtest
requested — the wiped surfaces are all unreachable-or-rare in a year-1 loop; this pass is
insurance against future reports, not a response to one.
