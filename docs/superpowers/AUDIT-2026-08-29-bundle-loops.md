# Bundle and season-gate audit, 20 loops, 0.16.31 to 0.16.36

**Date:** 2026-08-28 (session 11:05 to 11:32), throwaway save lineage Rodger / `None_447554838`
(rotated to `None_447607703` by the end).
**Build under test:** 0.16.31 plus the diagnostics commit 0.16.32 (no generation change), then the
fixes 0.16.33 to 0.16.36 found by this audit, all committed locally, none pushed.
**Method:** every check below is backed by the game's own packed data (Data/Locations, Data/Fish,
Data/Objects, Data/Crops, Data/RandomBundles, Data/PassiveFestivals, dumped through the game DLLs;
memory `stardew-xnb-data-dump`) and by the decompile where the data is silent
(`GameLocation.CheckGenericFishRequirements`, `MineShaft` forage). The engine's pool rules were
replicated in Python against that data first (`pools.py`), then every generated board and every
weekly-goal sample was parsed out of the SMAPI log and checked item by item (`audit.py`,
`stats.py`, in the session scratchpad; logs in `test-output/log-archive/SMAPI-audit-20260829-*.txt`,
not committed).

## Summary

| | |
|---|---|
| Loops generated diagnostically (`tly_genbundles`) | 20 (loops 0 to 19), plus loops 57 to 63 to capture the live boards, plus loops 0 and 16 re-run on 0.16.35 |
| Boards generated | 31 (20 + 7 + 2 re-runs + the 2 repeats inside each determinism self-check not counted) |
| Real `tly_reset` cycles | 6 (Runs 59 to 63 on 0.16.32, Run 63 re-checked on 0.16.35 and 0.16.36); `tly_gatecheck` after each: **no impossible gates on any live board** |
| Determinism self-check | OK on all 29 `tly_genbundles` runs |
| Weekly goals audited | Spring/Summer/Fall/Winter for every live board (`tly_goals`), plus a real season advance on Run 62 (Summer 8, Fall 8, Winter 8 hub re-opened, screenshots `test-output/audit-*-hub-run62.png`) |
| Findings by category | 1: 2 (one confirmed by the engine's own audit, one judgement); 2: 5 distinct items (fixed); 3: 1 rule-level (ruling needed); 4: 0; 5: 0; 6: systematic (14 of 20 boards, ruling needed); 7: 1 (vanilla's Recycler's, observation) |
| Fixes from last night observed working | all five (section "Last night's fixes") |

**Caveat Jeff raised mid-session:** nothing was donated during the season advance. The gate was
skipped (`tly_setday 7`, `debug season <x>`, `debug sleep`), so every weekly-goal sample below comes
from a fully open board. Real play would have fewer open slots by Fall and Winter, which changes
which items get drawn, not the rules being checked. Every finding here is a rule failure, not a
draw-luck artefact, and each fix is unit-tested independently of the draw.

## Findings

### Category 1: impossible at its gate

| Loop | Bundle | Item | Gate season | Why | Data evidence |
|---|---|---|---|---|---|
| 16 (seed 1169836138) | Spring Foraging (Seasonal, all by Spring 28) | Red Mushroom (O)420 | Spring | The engine's own `tly_genbundles` gate audit reports `Spring: 4/3 IMPOSSIBLE`, blocked by "Red Mushroom (needs Summer) [season override to Summer]". The Spring forage pool contains Red Mushroom only through `PoolTuning.SeasonalForageAdditions.Spring` (config default and Jeff's live config both list `(O)420`), while `GameplayConfig.DefaultItemSeasonPins` pins `(O)420` to Summer (the Dye bundle row). The two tables disagree, exactly the Purple Mushroom incident of 2026-08-27. | Data/Locations forage rows for (O)420: Woods Summer, Woods Fall (IslandNorthCave1 rows are excluded). No Spring spawn row anywhere. The only Spring source is the mines: `MineShaft.cs` line 1434 spawns (O)420 (4 in 5) or (O)422 on "rainbow lights" mushroom floors inside mine area 40 (floors 41 to 79), any season. |
| 6 (seed 327223644) | Weatherman's (Percentage, ramp [1,2,3,4] of X=4) | slots Perch, White Algae, Albacore, Midnight Carp, Super Cucumber | Spring (demands 1) | Only White Algae counts as Spring-obtainable, and it spawns only in the Sewer and the mines, which the mod's own `LocationGating` floors at Summer for fish gates. The Percentage ramp clamp uses `DerivedSeasonPins` (seasons only, no location floors), so it did not clamp Spring to 0. Low severity: White Algae bites from mine floor 20 upward, so a real player can meet it; reported because the prompt's rule list names "mines Summer". | Data/Locations fish rows for (O)157: Sewer (no season), UndergroundMine (no season). Data/Fish 157: all seasons, 600 to 2600. |

Recommendation for loop 16 (not applied, balance ruling): follow the Purple Mushroom precedent and
make the tables agree. Either change the `(O)420` pin to Spring (mines floor 41+ any season; this
moves the Dye bundle's Red Mushroom deadline earlier, which is the balance part) or drop `(O)420`
from the Spring forage additions (Spring pool shrinks from 7 to 6 items, more repeats, see
category 6). A third, structural option is to make a Seasonal fill skip any item whose merged
earliest-season pin is later than the bundle's season, which fixes the gate without touching either
table. Jeff's call.

Recommendation for loop 6: fold `LocationGating` floors into the Percentage ramp clamp
(`ClampRampForObtainability` currently sees seasons only). Also a ruling: it can only make ramps
more lenient.

### Category 2: wrong-season weekly goal (all FIXED this session)

| Board | Goal | Item | Season offered | Why wrong | Data evidence | Fix |
|---|---|---|---|---|---|---|
| Run 59 (board seed 2119975264) | Fishing, Lake Fish | Sturgeon (O)698 | Fall | Lake Fish is PerItem; `BundleDeadlines` put Sturgeon's deadline in Fall (rank by effort), and `InPlayItemsFor(PerItem)` offered every item pinned to the current season with no obtainability check. | Data/Locations Mountain and Backwoods rows: `LOCATION_SEASON Here summer winter`. | 0.16.33: PerItem goals pass the same predicate Percentage uses. Test `PerItem_in_play_items_also_pass_the_obtainability_predicate`. |
| Run 59 | Fishing, Lake Fish | Rainbow Trout (O)138 | Winter | Same mechanism, deadline Winter. | Forest/Mountain/Town/Backwoods rows: Season Summer (Forest also `IS_PASSIVE_FESTIVAL_OPEN TroutDerby`, Summer). | 0.16.33 |
| Run 58 (legacy pre-0.16.26 board still on disk) | Foraging, Four Seasons Sampler | Chanterelle (O)281 | Summer | `SeasonResolver` built forage seasons from a raw Data/Locations scan with no location exclusions and no condition seasons, so Ginger Island's `IslandNorthCave1` row (no Season) made Chanterelle year-round. | Non-excluded rows: Woods, Season Fall only. | 0.16.34: forage seasons come from the engine forage pool via `SpawnSeasonMap.FromPools`. Test `FromPools_ForagePool_IncludedWithItsSeasons`. |
| Run 60 (seed -804988751) | Foraging, Exotic Foraging | Purple Mushroom (O)422 | Summer | Same raw-scan cause (IslandNorthCave1 rows). Observation-level in practice: the mines drop it any season from floor 81 and the mushroom cave grows it, but the mod's own forage pool says Fall/Winter and the goal side should agree with it. | Only forage rows are on IslandNorthCave1 (excluded). | 0.16.34 |
| Runs 59, 60, 61, 62 | Foraging, Exotic Foraging | Red Mushroom (O)420 | Winter | Same cause. Observation-level for the same reason (mines floor 41+ any season). | Woods Summer/Fall; IslandNorthCave1 excluded. | 0.16.34 |
| Run 63 (seed 1062087078, on 0.16.35) | Fishing, Master Fisher's | Scorpion Carp (O)165 | Summer | Master Fisher's is a kept-vanilla pick-some bundle; Percentage goals used the catalog's season set only, which has no location floors, while the gates already floor the Desert at Fall. | Data/Locations Desert TopPond row, no season; Data/Fish 165 all seasons. `LocationGating`: Desert = Fall. | 0.16.36: goals consult the derived item model's floor for derived items. Tests in `GoalObtainabilityTests`. |

Verified in-game after each deploy: Run 62 goals clean in all four seasons on 0.16.35; Run 63's
Scorpion Carp moved from Summer to Fall on 0.16.36. On the legacy Run 58 board the pre-0.16.26
Night Fishing also offered Herring q1 and Red Snapper, and Specialty Fish offered Pufferfish in
Winter; those slots do not exist on any 0.16.3x board, listed only for completeness.

### Category 3: wrong water

No ocean-only fish appeared in River Fish or Lake Fish and no fresh-water fish in Ocean Fish on any
of the 20 boards (see the per-fix table). One rule-level finding remains:

| Loops | Bundle | Item | Why | Data evidence |
|---|---|---|---|---|
| 0, 2, 3, 8, 10, 11, 12, 13, 14, 15, 17, 18 | Night Fishing | Sea Cucumber (O)154 | Catchable 6am to 7pm at the Beach in Fall/Winter; admitted by the "at most one Night Market fish" exception. | Data/Fish 154: `600 1900`. Beach row `LOCATION_SEASON Here fall winter`; Submarine row. |
| 6, 16 | Night Fishing | Octopus (O)149 | Catchable 6am to 1pm in Summer; same exception. | Data/Fish 149: `600 1300`, Beach Season Summer; Submarine row. |

Root cause: the exception was written for the market's own trio, but Midnight Squid (O)798,
Spook Fish (O)799 and Blobfish (O)800 carry `ExcludeFromRandomSale = true` in Data/Objects, and
`ItemPoolBuilder.Vets` drops every such item, so none of the three ever enters the fish pool. The
only fish left with a Submarine row that are not already night-only are Octopus and Sea Cucumber
(Sea Jelly has category 0, so it does not count as a fish), and one of them lands in 14 of 20
Night Fishing bundles. Ruling needed: exempt fish with a Submarine spawn row from the
`ExcludeFromRandomSale` vet (the trio becomes the market fish, as intended), or drop the exception
(Night Fishing then has exactly four candidates for three slots: Bream, Squid, Super Cucumber,
Midnight Carp, so the no-repeat rule would fall back often).

### Category 4: off-season item in a season-named bundle

None on 20 boards. Every Spring/Summer/Fall/Winter Foraging and Crops slot drew a season-specific
item; Mussel, Clam, Cockle, Oyster, Coconut and Cactus Fruit never appeared in a season-named
bundle (Mussel only in Crab Pot 12 times and Four Seasons Sampler 5 times, both allowed).

### Category 5: quality ask on an unstarrable item

None in any re-rolled bundle (algae, jellies, Fiber and trap fish were never given a star).
Kept-vanilla asks (Wine q1, Void Salmon q2 in The Missing) are vanilla's own and starrable.

### Category 6: repeats

The no-repeat rule (0.16.30) is working where the pools allow it and falling back where they do
not. Across the 20 boards: 14 have at least one item shared between a re-rolled or authored bundle
and another bundle; 4 have two re-rolled bundles sharing an item; 31 "allowing repeats" fallback
lines in the log. Most repeated: Purple Mushroom (11 boards), Blackberry (5), Common Mushroom (5),
Red Mushroom (3), Chanterelle, Holly, Nautilus Shell (2 each).

Root cause: the season-specific forage pools are small (Spring 7, Summer 6, Fall 7, Winter 7 items
in vanilla) and the fixed lists that draw from the same items are filled first or never re-rolled:
Four Seasons Sampler (authored, final before pass 2), Forager's (Blackberry x50, Wild Plum x15),
Field Research and Wild Medicine (Purple Mushroom), Exotic Foraging (Red and Purple Mushroom),
Winter Star (Holly), Dye (Red Mushroom), Chef's (Fiddlehead Fern). Example, loop 0: Fall Foraging
was left with one unasked candidate (Hazelnut) after the Sampler took Common Mushroom, Blackberry
and Chanterelle and Forager's held Blackberry and Wild Plum, so it repeated three of the Sampler's
items. Ruling needed: let a season-named forage bundle draw any-season forage before repeating,
let the Sampler avoid what the seasonal bundles need (it is composed first), or accept repeats
between a seasonal bundle and a fixed list.

### Category 7: junk

Recycler's (kept vanilla, Bulletin Board) appeared on 5 of 20 boards and its slots (Trash,
Driftwood, Broken Glasses, Broken CD, Soggy Newspaper) also surface as Mixed weekly goals on those
boards (Runs 60 and 63). This is vanilla's own bundle and the junk is its point, so it is recorded
as an observation, not a defect. No junk and no legendary fish appeared anywhere else; Trash is gone
from the fish pool (46 items, was 52 with the trash table).

## Last night's fixes, as observed on the 20 boards

| Fix | Evidence |
|---|---|
| 0.16.26 non-habitat keys ignored | Lake Fish drew only Backwoods/Mountain/Sewer/Woods fish (Largemouth Bass, Sturgeon, Bullhead, Walleye, Catfish, Woodskip, Chub, Carp, Lingcod, Perch, Rainbow Trout, Midnight Carp, River Jelly, Green and White Algae); River Fish only Forest/Town/Woods fish; Ocean Fish only Beach fish (Eel, Flounder, Tilapia, Red Snapper, Sardine, Herring, Halibut, Anchovy, Albacore, Tuna, Pufferfish, Red Mullet, Squid, Sea Cucumber, Super Cucumber, Seaweed). Zero wrong-water slots. Salmon reads Fall only: every Salmon gate was "by Fall" (River Fish loops 14, 15, 16) and no Spring goal named it. |
| 0.16.27 season-named bundles | Zero any-season items in 140 seasonal slots; Winter Foraging drew Winter Root (11) and Snow Yam (12). |
| 0.16.28 Night Fishing | 20 of 20 Night Fishing bundles: only Bream, Squid, Super Cucumber, Midnight Carp plus at most one Submarine fish; Flounder, Eel and Walleye never appeared. |
| 0.16.29 festival seasons | Sea Cucumber gates: never before Fall (e.g. loop 0 "Sea Cucumber by Fall"); Squid gates Winter; Octopus Summer or later; Super Cucumber Summer or later. No festival fish demanded before its season on any board. |
| 0.16.30 no repeats | 6 of 20 boards fully repeat-free; the rest fall back per category 6 (pool exhaustion, not a rule failure). |

## Fixes landed this session (local commits only, not pushed)

| Version | Commit | What |
|---|---|---|
| 0.16.32 | c324e2a | Diagnostics only: `tly_genbundles` lists every slot by name, logs each bundle's gates and runs the gate audit on the diagnostic board; new `tly_goals [season] [week]`. |
| 0.16.33 | e317f5f | PerItem weekly goals pass the obtainability predicate (Sturgeon in Fall, Rainbow Trout in Winter). |
| 0.16.34 | 2098587 | Weekly-goal forage seasons come from the engine forage pool (Chanterelle, Purple Mushroom in Summer). |
| 0.16.35 | 6f73729 | Jeff's request: at most one fruit-tree fruit per theme's weekly goal list (Data/FruitTrees group cap). Verified on 8 goal sets: never more than one. |
| 0.16.36 | 8a2fd12 | Weekly goals honour the derived item model's location floors (Scorpion Carp in Summer). |

Tests: 1200 passing. `docs/engine-bundle-catalogue.md` regenerated from `tly_dumpbundles` on
0.16.36 (Fish 46, Forage 29, Crops 43 on this save with the year-2 crops excluded).

## Open items for Jeff

1. Red Mushroom: pin to Spring, drop it from the Spring forage additions, or add the structural
   Seasonal-fill guard (category 1).
2. Night Fishing's market fish: exempt Submarine spawns from the `ExcludeFromRandomSale` vet, or
   drop the exception (category 3).
3. Repeats between seasonal forage bundles and the fixed lists (category 6).
4. Percentage ramp clamp ignoring location floors (category 1, low).
5. The final rotated save folder `None_447607703` was left in place (delete only on your say-so).
