# Handoff — 2026-05-29 end of day

Two playtest days (2026-05-28 evening + 2026-05-29 all day) landed 18
commits on top of the prior handoff. Most of them are playtest-driven
fine-tuning. The final commit (`40fd40e`) is the one to read first —
it explains a silent failure that quietly inert-ed three rounds of
bonus-drop work before we caught it.

The game is currently launched (SMAPI PID 27640) waiting on the user
to load the save and verify the freshly-actually-working patches.

## Branch state

- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: `40fd40e` (the critical Harmony fix)
- 358 tests passing, 0 warnings, 0 errors
- DLL deployed to `C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Mods\TheLongestYear\`
- Not pushed (no remote configured for TLY)

## **READ THIS FIRST — silent abort root cause (commit `40fd40e`)**

Round-8's `EventSuppressionPatch` declared:

```csharp
[HarmonyPatch(typeof(GameLocation), nameof(GameLocation.checkEventPrecondition))]
```

`GameLocation.checkEventPrecondition` has **two overloads on PC**:
`(string)` and `(string, bool check_seen)`. Patching by name alone
threw `AmbiguousMatchException` inside `harmony.PatchAll()`, and
**`PatchAll` aborts iteration on the first throw** — so every later
`[HarmonyPatch]` class in the assembly silently failed to apply.

Casualties (silently inert for the entire 2026-05-29 playtest):

- `MineOreDropBonus` (OnStoneDestroyed snapshot doubling)
- `AllDropsBonusPatch` (Object.performToolAction snapshot doubling)
- `TreeAllDropsBonusPatch`, `ResourceClumpAllDropsBonusPatch`
- `JunimoStashCapacityPatch` (the 4-slot UI cap — chest showed 36)
- `JunimoStashCapPatch` (the addItem-side cap)
- `JunimoStashShowMenuPatch` (the color-picker shift)
- `JunimoStashImmovablePatch` (pickaxe-proof chest)

We diagnosed it by adding instrumentation TRACE logs (round 9 + 10)
that the user then reported as totally absent from the log. That
absence was the smoking gun — the patches weren't just gated, they
weren't *attached*. The mod-loader log entry was buried at
`[14:14:06 ERROR The Longest Year] Mod crashed on entry...
HarmonyException: Ambiguous match for HarmonyMethod[...]`.

**Two fixes in `40fd40e`:**

1. **Pin the single-string `checkEventPrecondition` overload** with
   explicit `new System.Type[] { typeof(string) }` in the attribute.
2. **Replace `harmony.PatchAll()` with a per-class loop** that
   `try/catch`es each `PatchClassProcessor.Patch()` individually so
   one ambiguous-match can no longer crater everything downstream.
   Summary log line at the end shows applied/failed counts.

**Apply this lesson to every future `[HarmonyPatch]`** — if the
target method has any chance of having overloads (`performToolAction`,
`performAction`, `getDamageDoneByPlayer`, anything with a `bool` or
`Tool` parameter that vanilla sometimes adds), pin the signature
explicitly with `Type[]`.

## Commits since prior handoff (newest first)

```
40fd40e fix(critical): pin checkEventPrecondition overload; isolate Harmony failures
1819ee5 diag(stash): trace GetActualCapacity gate; JP HUD switches to RenderingHud
c1110f3 fix(playtest): diagnose mine-bonus silence, log restored theme, sweep stash before resolve
a022114 docs(todo): retune keep_pet JP cost — 50-100 (sentimental, not a run-saver)
1fb2e26 docs(todo): spec keep_pet JP upgrade — preserve pet + hearts across loops
e2da3ea fix(events): suppress vanilla event 191393 (Demetrius/Lewis CC intro)
7d3af70 fix(stash): anchor at (entry+3, +1) — one tile up from previous spot
fe63175 feat(bonus-drops): mine_drops_up fires on overworld rocks too; trees & clumps for Mixed
b3a63a1 feat(bonus-drops): snapshot-diff doubling so vanilla's full drop set gets bonused
a363ed0 fix(bonus-drops): never bonus on weeds or twigs
94423fa feat(playtest): bonus drop sparkle+chime, JP HUD scale 0.95, mine bonus diagnostic
ebb1eec fix(stash): anchor to (entry+3, +2) — between doormat path and mailbox
836f356 fix(playtest): stash anchor clears farmhouse exit, JP HUD drops theme line
c484300 fix(playtest): JP HUD bigger+higher, stash indicator off, bonus-drop logs, bundle JP via observer
fb9167c fix(debug): reroll picks no longer rejected by canonical offer check
395b205 feat(debug): re-roll themes button on the weekly hub for QA
56b5d4d fix(playtest): no forage sweep on reload, undo SO board patch, shift stash buttons
ca9cc08 fix(playtest): stash sweep, wizard letter, JP HUD, crop skip, mine entrance, sampler avoids
```

## What's actually shipped (as of this DLL)

### Bonus drops — snapshot-diff doubling

User spec evolution culminated in: a single 30%/10% roll either doubles
the FULL drop set vanilla produced, or nothing. Mirrors vanilla's exact
loot distribution instead of substituting a fixed item.

- **`mine_drops_up` (30%)** — fires on `GameLocation.OnStoneDestroyed`
  anywhere (mines, farm, quarry, backwoods). `MineOreDropBonus`
  snapshots `Location.debris.Count` in the prefix, iterates new debris
  in the postfix, clones each `Item` via `Game1.createItemDebris`. Only
  rocks/nodes — weeds and twigs intentionally do NOT bonus under Mining.
- **`all_drops_up` (10%)** — fires on:
  - `OnStoneDestroyed` (same snapshot path, shared with mine_drops_up
    in `MineOreDropBonus` — tier the roll, only one fires per event)
  - `Object.performToolAction` (weeds, twigs, machines — `AllDropsBonusPatch`)
  - `Tree.performToolAction` (`TreeAllDropsBonusPatch` in TerrainBonusPatches.cs)
  - `ResourceClump.performToolAction` (big stumps, boulders, clusters,
    meteorites — `ResourceClumpAllDropsBonusPatch`)
- **`forage_yield_up` (25%)** — unchanged from prior session. Drops a
  2nd forage clone adjacent to the spawned tile via `ForageYieldPatch`.
- **`BonusDropEffects.Play(loc, x, y)`** plays `"yoba"` chime +
  `Utility.sparkleWithinArea(rect, 5, Color.White)` on every bonus
  firing so the player can see/hear the bonus, not just check the log.
- **`PatchLog.Info(...)`** on every bonus fire so the next log pull
  shows exactly what doubled. Round-9 added an unconditional TRACE on
  the OnStoneDestroyed postfix entry so we can disambiguate "patch not
  firing" from "bonus inactive" from "debris diff empty."

### Stash

- **Anchor: `(entry+3, entry+1)`** — Standard farm = (67, 16). Path
  through several iterations (porch → exit corridor → mailbox front →
  one-tile-too-low → here). Comment at `JunimoStashService.AutoTile`
  documents the geometry.
- **Sweep BEFORE ResolveTile** (commit `c1110f3`). The prior order had
  ResolveTile seeing the old chest as a blocking object and falling
  through to the next ladder candidate.
- **No indicator bubble** (`WorldResetService` no longer calls
  `_stashService.RegisterIndicator`).
- **4-slot UI cap** comes back online with `40fd40e` — `JunimoStashCapacityPatch`
  postfixes `Chest.GetActualCapacity`, gated on the `tly.junimo.stash`
  modData tag. Round-10 added a TRACE log inside the postfix that will
  surface the failure mode if the cap still doesn't take.

### Theme picker / hub

- **Re-roll button** (`feat: 395b205`) on `WeeklyHubMenu` for QA.
  Salts the offer RNG with an incrementing counter, regenerates the
  candidate set excluding already-selected-this-month, re-runs
  `ResolvePerCardData` + layout. Controller DPad-down lands on it.
- **Reroll picks bypass canonical offer check** (`fb9167c`) — added
  `skipOfferCheck` parameter to `RunController.SelectByName`, passed
  `true` from `ConfirmSelection` whenever `_rerollCounter > 0`.

### JP HUD

- `dialogueFont` scaled to `0.95f` (5% smaller than the round-4 size)
- Padding 14, line gap 6
- Position: `boxTopY + hudBoxHeight + 24` (24px under the box, was 80)
- Theme line **removed** — quest log shows that state, HUD just shows
  banked JP.
- **`Display.RenderingHud`** (was RenderedHud) so the journal-icon
  hover tooltip lands on top of the box instead of behind it (round 10).

### Vanilla cutscene suppression

- `EventSuppressionPatch` prefixes `GameLocation.checkEventPrecondition(string)`
  (overload pinned per `40fd40e`) and returns `"-1"` for any precondition
  key whose leading id matches `SuppressedEventIds`. Current set:
  - `"191393"` — Demetrius + Lewis CC intro (Spring 5 Y1).

### Bonus item sampler

- `CcItemCatalog.EarlyGameAvoid` extended on 2026-05-28 to filter out:
  Cactus Fruit, Coconut, Morel, Chanterelle, Red Mushroom, Maple
  Syrup, Oak Resin, Pine Tar, Large Egg + variants, regular Egg +
  Brown Egg, Honey, Truffle Oil, Truffle.
- Spring weeks 1–2 should no longer surface day-1-impossible items
  like Large Egg, Honey, Cactus Fruit, Morel.

### Effects layer

- `crop_growth_down` is now a snapshot-restore (prefix captures
  phase+dayOfPhase, postfix restores) so a watered crop ends the
  modifier day exactly where it started — no advance, no regression.
- `mines_closed` blocks `performAction("Mine"/"NextMineLevel"/"MineElevator")`
  at the Mountain entrance, not just the elevator inside (`MinesEntranceClosedPatch`).
  Display: "Mine entrance closed all week."
- `forage_off` sweep no longer runs on save reload — only on fresh
  selection (`SelectByName`) and on `BeginNewMonth` pre-pick activation
  (commit `56b5d4d`). Reloading to re-pick the week no longer destroys
  saved forage.

### Bundle JP

- Moved into `DonationObserver` (commit `c484300`). Snapshot
  `Bundle.complete` at menu open, diff per tick, fire `OnBundleCompleted`
  on `false → true`. The vestigial Harmony postfix on
  `JunimoNoteMenu.checkIfBundleIsComplete` stays as an idempotent
  fallback via `Run.TryMarkBundleAwarded`.

### Other minor wins

- Wizard's `wizardJunimoNote` letter pre-marked in CommunityCenterUnlock
- Joja "boulder cleared" letter (`landslideDone`) silenced via
  `noLetter:true` in MountainUnlock
- Color picker swatch nulled and remaining right-column buttons shifted
  down so the toggle's old slot fills
- Stash duplicate sweep (sweep ALL tagged chests on every PlaceChest)

## What the user reported confirmed working

- Stash placement geometry (after the round-9 sweep-order fix)
- Theme quest entry shows in the player journal
- Reroll → Mixed pick works (round-1 silent-rejection bug fixed)
- Mine entrance hard block on Foraging week
- Wizard / Joja boulder mail both silenced
- Color picker layout (toggle icon gone, buttons shifted)
- Forage on reload (no destructive sweep)
- Only one stash on the save (sweep works)
- Reroll button itself
- Stash placement perfect at (67, 17) on their save (latest)
- Theme quest entry visible

## Test list pending verification (round 11+)

After the `40fd40e` Harmony fix, EVERY patch attached for the first
time. The user has the game launched but hasn't reported back yet.
Watch for these on the next log pull:

**Stash:**
1. **4-slot cap** — open stash, should see 4 slots (was 36 because cap
   patch wasn't applied)
2. **Immovable** — pickaxe on stash should bounce; chest not picked up

**Bonus drops** (Mining theme active per the user's save):
3. **Mine bonus on farm rocks** — chime + sparkle + doubled drop ~30%
4. **Mine bonus inside mines** — same on ore/coal/copper nodes
5. **Log lines** — every smash should add a TRACE
   `OnStoneDestroyed postfix: stoneId='...', newDebrisAdded=N, mineBonus=True`
   and (on a fire) an INFO `mine_drops_up: stone '...' destroyed → doubled N drop(s)`
6. **Capacity trace** — every menu-open should add a TRACE
   `GetActualCapacity postfix: tile=..., hasStashTag=..., probedCap=4, vanillaResult=36`

**UI:**
7. **JP HUD journal tooltip** — hover the journal icon; "Quest Log"
   tooltip should appear ON TOP of the JP box (RenderingHud fix)

**Cutscenes:**
8. **191393 suppressed** — Demetrius/Lewis CC intro should not fire

**Bundle JP:**
9. Complete any CC bundle, JP bumps via observer path

**Deferred (need different theme picks):**
10. `all_drops_up` doubling on weeds → fiber, trees → wood, big stumps
11. `all_sell_prices_down` — parsnip sells for 17g on Mixed week
12. `crop_growth_down` snapshot restore on Fishing week days 2 + 5

## Open spec items (TODO.md)

1. **Co-opted day-1 intro cutscene** — re-use 191393's Lewis-at-Town
   staging with new TLY dialogue (Joja stakes → Lewis walks off →
   player enters CC → Junimo explains the loop). Plays once per save,
   skippable, auto-skipped on subsequent loops via a `MetaState` flag.
2. **`keep_pet` JP upgrade** (50–100 JP, sentimental category) —
   preserves pet species + name + friendship hearts across loops via
   a new `MetaState` snapshot. Barn/coop animals must continue to
   start each loop at 0 hearts (the existing `WorldResetService.ApplyStartingAnimals`
   already does this — call this out in any future cleanup).
3. **`kept_kitchen`/`kept_basement`/`kept_shortcuts`** — already shipped
   (in `UpgradeCatalog.cs:154-159`), but the TODO entry was never
   moved to Resolved. Closing it is a one-line edit.

## Files most likely relevant to follow-up

| Area | Files |
|---|---|
| Bonus drops | `Loop/MineDropsPatch.cs`, `Loop/AllDropsPatch.cs`, `Loop/TerrainBonusPatches.cs`, `Loop/BonusDropEffects.cs`, `Loop/PatchLog.cs` |
| Stash | `Loop/JunimoStashService.cs`, `Loop/JunimoStashCapPatch.cs` |
| Hub + reroll | `UI/WeeklyHubMenu.cs`, `Loop/RunController.cs` (SelectByName + skipOfferCheck) |
| Effects | `Loop/CropGrowthPatch.cs`, `Loop/ForageOffPatch.cs`, `Loop/ForageYieldPatch.cs` |
| Cutscene block | `Loop/EventSuppressionPatch.cs` |
| Mod entry / Harmony pass | `ModEntry.cs` (the per-class try/catch loop at line ~99) |
| Bundle JP | `Donations/DonationObserver.cs` |

## Workflow reminders

- **Local commits only** — TLY has no remote. Don't `git push`.
- **Game-running deploys fail** — use `-p:EnableModDeploy=false
  -p:EnableModZip=false` if the user is mid-playtest, deploy + commit
  when they exit.
- **Standard farm only** — `StandardFarmEnforcer` scrubs the picker on
  new saves; `OnSaveLoaded` bails on existing non-Standard saves.
- **Bash tool with bash, PowerShell tool with PS syntax** — earlier
  exit 127 on a PS command sent to Bash; tool match matters.
- **No `/sdcard/` paths** — workspace-wide, even though TLY is PC-only.
- **Co-Authored-By footer** — every commit gets the `Claude Opus 4.7
  (1M context)` footer per project guidance.
- **EVERY Harmony patch needs an explicit Type[] in its attribute** if
  the target method has any chance of overloads. The `40fd40e` fix
  surfaces failures individually now, but the lesson is to avoid them
  in the first place.

## How to pull the log without killing the game

```powershell
Copy-Item "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" `
  "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\SMAPI-latest.txt" -Force
```

(PC SMAPI doesn't lock the log file the way Android does.)

To kill + pull at end of session:

```powershell
Get-Process -Name "Stardew Valley","StardewModdingAPI" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Copy-Item "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" `
  "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\SMAPI-latest.txt" -Force
```

## What's NOT done

The biggest near-term lift is just **verifying that round 11 actually
works** — the user is sitting at the title screen with the freshly
working DLL but hasn't tested yet. Almost the entire pending list
above maps to "did the round-8-killed patches come back to life now
that PatchAll doesn't abort?"

After that, fresh asks from the user. The TODO.md spec items (intro
cutscene, keep_pet) can be picked up if they want progression beyond
playtest cleanup.
