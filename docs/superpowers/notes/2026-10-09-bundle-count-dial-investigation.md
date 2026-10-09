# Bundle-count dial: investigation notes (2026-10-09)

Read-only investigation for the ThirteenRedCats request (TODO.md, sweep of 2026-09-16): a
difficulty dial for the NUMBER of bundles per Community Center room. Easy = one or two fewer,
Normal = standard, Hard = one or two more, Extreme = every bundle the room's pool can offer.
Jeff's reply to the player: "if the graphical side of things isn't hard-coded I think I could do
that". Nothing was prototyped. PC 1.6 only.

Paths: `decomp/` = `decompiled-pc/Stardew Valley/`; `Core/` = `src/TheLongestYear.Core/`;
`Mod/` = `src/TheLongestYear/`.

## Verdict in one paragraph

The graphical side is only half hard-coded. The room page (the parchment with the bundle bags) uses
one shared background for every room and places bags from a fixed table of **9 positions**. Any
room can show **up to 9 bundles with no UI work**. Fewer than the vanilla count also draws fine (the
unused spots are just empty parchment). The ingredient page is per bundle and does not care how many
bundles the room has. So Easy and Hard (one or two either way) need **no UI work at all**. Only
"every bundle" needs UI work, because three rooms have more than 9 possible bundles. The real cost is
not graphics but **data plumbing**: the game never deletes a bundle key, TLY's whole board relies on
every run writing the exact same set of keys, and the CC location caches which bundle belongs to
which room. Changing the count between runs needs a new "remove stale bundles and refresh the CC"
step, and that step is the bulk of the work for every tier.

## 1. Vanilla: what is and is not hard-coded

### Room page layout (JunimoNoteMenu)

- Menu is a fixed 1280 x 720 panel (`decomp/StardewValley.Menus/JunimoNoteMenu.cs:105`).
- Background: the SAME 320 x 180 region of `LooseSprites/JunimoNote` for every room
  (`JunimoNoteMenu.cs:1332`). There is no per-room art and no drawn placeholder per bag, so an empty
  position leaves no visual hole.
- Bags are built by walking every `BundleData` key whose name contains the room name, in dictionary
  order, and placing the Nth one at `getBundleLocationFromNumber(N)` (`JunimoNoteMenu.cs:358-375`).
  Positions are a switch over **0 to 8** (`JunimoNoteMenu.cs:1780-1823`):
  0 top centre, 1 bottom left, 2 bottom right, 3 middle left, 4 middle right, 5 centre,
  6 centre low, 7 upper left, 8 upper right. Anything 9 or above falls through to the panel's
  top-left corner (no case, returns the origin), so a 10th bag sits on top of the back button and
  all extras stack on one spot.
- The same function is used again on window resize (`JunimoNoteMenu.cs:1516-1522`), so one Harmony
  postfix on it covers both call sites.
- Bag size is 64 x 64 (`decomp/StardewValley.Menus/Bundle.cs:95`). Occupied area spans x 304 to 956,
  y 136 to 452; the present (reward) button sits at (592, 512) (`JunimoNoteMenu.cs:1148`). There
  looks to be room for a bottom row of about four bags either side of the present button, but that
  must be confirmed with a screenshot before anyone builds on it.
- Controller navigation between bags is a nearest-neighbour search that only runs when the current
  bag's id is one of the first 10 (`oldID - 5000 >= 10` bails, `JunimoNoteMenu.cs:236`). Bags 11+
  would be unreachable by stick from the bag grid. Mouse is fine.
- Room completion: when the page opens, if every bag on it is complete the area is marked complete
  (`JunimoNoteMenu.cs:380-399`). This works for any count.
- The browse-from-inventory mode cycles areas 0 to 5 (`JunimoNoteMenu.cs:846`); count-independent.

### Ingredient page

- Per bundle, built from that bundle's own slot count via `addRectangleRowsToList`, which lays out 1
  to 12 slots (`JunimoNoteMenu.cs:1686-1730`). Unaffected by how many bundles the room has.

### CommunityCenter and NetWorldState

- `Data/Bundles` keys are `"Room/index"`; the index is a GLOBAL number. Vanilla 1.6.15 indices
  (`Core/VanillaBundleBoard.Generated.cs:14-46`): Pantry 0-5 (6), Fish Tank 6-11 (6), Crafts Room
  13-17 and 19 (6), Boiler Room 20-22 (3), Vault 23-26 (4), Bulletin Board 31-35 (5), Abandoned
  Joja Mart 36 (1). Unused: 12, 18, 27-30, 37+.
- `NetWorldState.SetBundleData` upserts via `netBundleData.CopyFrom` and only ever adds or grows
  `Bundles[index]` / `BundleRewards[index]`; it never removes a key
  (`decomp/StardewValley.Network/NetWorldState.cs:653-682`; `CopyFrom` is upsert-only at
  `decomp/Netcode/NetDictionary.cs:471-477`, while `Set` at `:479-483` clears first).
- `CommunityCenter.initAreaBundleConversions` builds the private `areaToBundleDictionary` and
  `bundleToAreaDictionary` from BundleData, and only in the constructor
  (`decomp/StardewValley.Locations/CommunityCenter.cs:108-126, 186-203`). It uses
  `bundleToAreaDictionary.Add`, so a numeric index shared by two rooms throws and the CC fails to
  load (TLY already documents this, `Mod/Loop/BundleEngine.cs:52-62`). Re-calling it is not safe: it
  also adds mutexes and net fields each time (`CommunityCenter.cs:190-196`).
- Consequences of a stale cache after a mid-session board write with a different key set:
  - `shouldNoteAppearInArea` reads `areaToBundleDictionary` (`CommunityCenter.cs:444-460`), so a new
    bundle would be ignored when deciding whether a room's note shows.
  - `checkForMissedRewards` does `bundleToAreaDictionary[key]` for every key in `bundleRewards`
    (`CommunityCenter.cs:584-586`): a reward key with no area entry throws KeyNotFoundException.
- Area name to number is a fixed switch over 7 areas (`CommunityCenter.cs:205-231`, names at
  `:1273-1286`). Fine: the dial changes bundles per area, not areas.
- Hard-coded bundle indices in vanilla are rare: `AbandonedJojaMart.cs:76` (36, The Missing) and the
  1.6 save migration's `"Pantry/4"` (`SaveMigrator_1_6.cs:682`). Nothing else assumes a room's count.
- `numberOfCompleteBundles` counts every `Bundles` entry including stale ones
  (`CommunityCenter.cs:292-306`); it only gates the Bulletin Board note (> 2, `:371-374`).

### 1.6 remix (Data/RandomBundles)

- `BundleGenerator.Generate`: per area, the count is the number of tokens in that area's `Keys`
  string; one bundle per key, a `BundleSets` entry first, then indexed bundles, then wildcard
  (`Index == -1`) bundles (`decomp/StardewValley/BundleGenerator.cs:21-80`), written as
  `AreaName + "/" + Keys[i]` (`:141`). Vanilla remix keeps the standard count. TLY replaces this
  with its own engine anyway.

### Joja and the Vault

- Joja membership is blocked on TLY saves (`Mod/Loop/JojaMembershipBlock.cs:20-26`), so the Joja
  path is not in play.
- The Vault page is purchase buttons, not ingredient slots (`JunimoNoteMenu.cs:1567-1582`).

## 2. TLY's board today

- **The per-room count is not a constant.** It is the number of `Data/Bundles` keys per room:
  `VanillaBundlePool.BuildRoomPools` makes one position per key (`Mod/Loop/VanillaBundlePool.cs:72-91`);
  `Data/RandomBundles` only adds candidates to existing positions (`:93-119`, out-of-range dropped at
  `:224`); authored bundles are appended as candidates at every existing position, never as new
  positions (`Mod/Loop/BundleEngine.cs:701-751`).
- `RemixSelector.PickForRoom` picks exactly one bundle per position, keeps the pick's real index,
  and avoids repeating a name in the room (`Core/RemixSelector.cs:31-49, 68-79`).
- **The key invariant:** the set of "Room/index" keys written must be identical every generation
  (`BundleEngine.cs:64-67`); `WriteToWorld` relies on it to skip clearing and zeroes completion in
  place (`BundleEngine.cs:631-669`). TLY never removes a key anywhere; the reset re-runs
  `SetBundleData` and zeroes arrays (`Mod/Loop/WorldResetService.cs:347-364`).
- Board repair rewrites only ingredient fields of existing keys and never changes count
  (`Mod/Loop/BoardRepairService.cs:317-337`).
- Season gate: `BundleGate.IsSatisfied` requires EVERY bundle's own season requirement plus the vault
  gate (`Core/BundleGate.cs:14-21`). "Any N of Y" lives inside each bundle (its ramp/quota,
  `Core/BundleRequirement.cs:164-250`, clamped to the bundle's slot count in
  `Core/GeneratedBundleSet.cs:158-183`). So the gate load scales directly with the number of bundles:
  two more bundles = two more things due at some season end.
- Per-slot ledger, weekly theme goals and bonus slots are keyed by (bundleIndex, ingredientIndex) and
  read the live board (`Core/SlotLedger.cs:13-41`, `Core/SlotPoolBuilder.cs:37-112`,
  `Mod/Loop/WeeklyThemeQuestService.cs:289-293`). None assume a count.
- Each completed bundle pays a JP bonus once per run (`Core/RunState.cs:213-214`), so the count dial
  also moves the JP economy.
- Vault: four bundles assumed (`Core/VaultRules.cs:29, 49-54`, Winter needs 4;
  `Mod/Integration/VaultBundleMap.cs:62-67`). Side finding: `VaultRules` hard-codes indices 34-37
  (`Core/VaultRules.cs:22-25`) while vanilla 1.6.15 uses 23-26; it is only the fallback when the live
  map is missing, but it is stale.
- Difficulty: `DifficultySettings` holds nine per-dial steps (`Core/DifficultySettings.cs:22-111`;
  `SetAll`, `Clone`, `IsAllNormal` would each need the new dial), resolved in
  `Core/DifficultyResolver.cs:37-68`, stamped per reset (`WorldResetService.cs:157`, `MetaState.cs:140`).
  Closest precedent: RequiredSlots (Easy -1, Hard +1, Extreme all; `DifficultyResolver.cs:48-55`,
  `Core/RequiredSlots.cs:21-44`). Normal/Remixed boards never change which bundles appear
  (`Core/VanillaBoardDifficultyPass.cs:11-14`), so this dial is naturally **Custom (engine) boards only**.
- Held boards re-stamp difficulty from live config at reset and rebuild from the pinned seed
  (`WorldResetService.cs:157, 724-739`). A count dial must keep the old stamp on a held board, or the
  held board changes shape.
- Load check `EngineManifestCheck.Matches` only checks generated keys are live; extra live keys pass
  silently (`Core/EngineManifestCheck.cs:27-51`).
- Precedent for the stale-key risk already in the code: `VanillaOnlyBoard.FilterRoomPools` can drop a
  mod-added position (`Core/VanillaOnlyBoard.cs:154-167`), which would leave a live key behind.
- Tech's Cross-Mod Bundles: TLY reflects into Tech's generator on Normal/Remixed resets and restores
  Tech's board by upserting keys, never removing (`Mod/Loop/TechCrossModBundlesTarget.cs:28-39`,
  `Core/TechBoardOfRecord.cs:61-92`). New TLY indices must not collide with Tech's.

## 3. Other mods

No bundle-count mod source is available locally (the Mods folder holds only CartCatalog, Content
Patcher, Farm Type Manager, GMCM, NapTime, SaveBackup, StardewDeliveryService, TLY). Nothing in the
workspace patches `getBundleLocationFromNumber`. So there is no local example of how another mod lays
out more than 9 bags; nothing was downloaded. From the code alone: a Content Patcher pack that adds
`Data/Bundles` keys gets the 9-position limit and the stale-key behaviour for free.

## 4. Pool sizes ("every bundle")

From the local catalogue `docs/engine-bundle-catalogue.md` (git-ignored, `tly_dumpbundles`, dated
2026-09-30, unmodded): distinct bundle names offered per room vs vanilla positions:

| Room | Vanilla count | Distinct in pool | Fits 9 positions? |
|---|---|---|---|
| Pantry | 6 | 13 | no |
| Crafts Room | 6 | 11 | no |
| Fish Tank | 6 | 9 | yes |
| Boiler Room | 3 | 9 | yes |
| Bulletin Board | 5 | 14 | no |
| Vault | 4 | 4 | (excluded) |
| Abandoned Joja Mart | 1 | 1 | (excluded) |

Total for the five themed rooms: 56 bundles vs 26 today. With other bundle mods installed the pool
grows further, so "every bundle" has no fixed upper bound.

## 5. Per tier: what breaks and the effort

**Shared foundation (needed by every tier except Normal): medium.**
1. Engine: pick K positions per room instead of all (fewer), or add extra positions with fresh
   indices drawn from the room's name-distinct union (more). Fresh indices need a reserved range that
   cannot hit vanilla (0-36) or Tech's keys.
2. A key-removal step on write: delete stale keys from `netBundleData` (protected field, reflection),
   `Bundles` and `BundleRewards`, then rebuild the CC's private `areaToBundleDictionary` /
   `bundleToAreaDictionary` by reflection (not by re-calling `initAreaBundleConversions`). Without it:
   fewer = stale bags stay on the page and the room can never complete; more then back to normal =
   same; new keys without the cache rebuild = KeyNotFound in `checkForMissedRewards`.
3. Dial plumbing: new `DifficultySettings` field (SetAll/Clone/IsAllNormal), resolver, GMCM/menu
   row, old saves read as "no change", held boards keep their stamp, manifest check notices extra live
   keys.
4. Tests and a live round trip: Normal, Easy, Hard, back to Normal across rewinds on one save, plus a
   save/load in between.

| Tier | UI work | Effort | What breaks without care |
|---|---|---|---|
| Fewer (Easy, -1/-2) | None. Gaps are blank parchment. Optional: re-centre bags (Boiler at 2 = top centre + bottom left looks lopsided). | Medium (foundation is all of it) | Stale bags on the page, room never completes. Boiler at 3 -1/-2 goes to 2 or 1: needs a floor. |
| More (Hard, +1/+2) | None. Max is 8 (Pantry, Crafts, Fish), 7 (Bulletin), 5 (Boiler), all under 9. | Medium (foundation + index range + extra-position fill; pool repeats with "no item twice") | Index collisions crash the CC load; missing cache rebuild throws on missed rewards. |
| Every bundle (Extreme) | Yes: Harmony postfix on `getBundleLocationFromNumber` for positions 9-13 (or a computed grid when count > 9), lift the 10-bag controller limit (`JunimoNoteMenu.cs:236`), screenshot-check at several UI scales. | Large | Pantry 13, Crafts 11, Bulletin 14 overflow the 9 spots. "No item asked twice" across 56 bundles will drain thin pools (fish, crab pot 3, monster drops 5); the gate would demand ~2x the work; JP from bundle completions roughly doubles. Needs sims. |

A cheaper Easy alternative to deleting keys: keep the key space fixed and write the dropped bundles
as already complete ("the Junimos did this one"). It avoids the removal step, but the bag still shows
on the page, the run state would need to leave it out of the gate and the JP award, and players may
read it as a bug. Worth a design call, not recommended by default.

## 6. Questions for Jeff

1. **Board type:** Custom (engine) boards only? Normal/Remixed promise vanilla's bundles today.
2. **Season gates on a shorter or longer board:** the gate needs every bundle's own season ask.
   Should an extra bundle just add its asks to the season it naturally falls in, or should Hard's
   extra bundles be held to the later seasons so Spring does not get heavier? On Easy, is it fine that
   Spring may lose one of its due bundles?
3. **Which bundles go on Easy:** random, or the hardest by effort first? Should Spring/Summer/Fall
   Crops (always picked today, `Core/RemixSelector.cs:25-29`) stay protected?
4. **Floors:** Boiler Room has 3. Is 1 bundle in a room acceptable on Easy -2, or floor at 2?
5. **Vault and The Missing:** leave out of the dial (they are fixed by the vault gate and the
   Joja Mart)? Recommended yes.
6. **"Every bundle" meaning:** every name-distinct bundle in the room's pool (56 today on an unmodded
   game, more with Tech or other packs), or a cap such as "fill all 9 spots"? A 9-cap gives Extreme
   with no UI work at all (Pantry 9, Crafts 9, Fish 9, Boiler 9, Bulletin 9 = 45).
7. **Same bundle twice:** may Extreme repeat a theme (two Spring Crops variants), or names stay unique
   per room as today?
8. **JP economy:** bundle completion pays JP. Scale the per-bundle bonus by count so Easy is not
   poorer and Extreme not richer, or let it ride?
9. **Mid-run changes:** the dial takes effect at the next rewind only, and a held board keeps its
   count. OK?
10. **Easy method:** remove the bundles (clean, medium work) or show them as pre-completed (cheaper,
    looks odd)?
