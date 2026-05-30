# Handoff — 2026-05-29 resume point

Continuation handoff from the same calendar day as
[`2026-05-29-handoff-eod.md`](2026-05-29-handoff-eod.md). The earlier
"eod" handoff covered the round-8 silent-abort fix and the
Debris.itemId.Value root cause. Between then and now, **15 more
commits** landed — a full rebalance + several new feature chains +
two carryover-feature flagship implementations + a cleanup pass.

The user is mid-playtest, currently on Spring week 2 of Run 1 (a
self-selected Mixed week). SMAPI is running (PID 36936). The deployed
DLL is at commit `2014027`. **Eight commits are staged in `bin/` and
will land the moment the user exits the game** (deploy on exit, never
mid-session — game holds the DLL file lock).

## Branch state

- Branch: `feat/v1-plan-07-junimo-stash`
- Tip: `e5e8e6a` (fireplace intro quest)
- 359 tests passing, 0 warnings, 0 errors across the build chain
- Not pushed (no remote configured for TLY)
- Deployed DLL: through `2014027` (loaded by SMAPI PID 36936 at 16:32)
- bin/ DLL: through `e5e8e6a` (waiting on game exit)

## Commits since the prior handoff (newest first)

```
e5e8e6a feat(quests): intro quest for the Season Goals fireplace board
af622ab refactor: remove IndicatorRegistry; close forage_off carryover
ce26c25 docs(todo): audit + clean up stale entries
e0251d4 feat(upgrades): keep_pet — pet survives loop resets with hearts
5959de0 feat(loop): JP-spend popup on reset + win; continue-after-victory choice
d8f57a1 feat(bonus-drops): all_drops_up covers fish (treasure deferred)
44f27bd feat(bonus-drops): all_drops_up covers tree shake + monster drops
74c221e fix(stash): draw-prefix guard kills 1-frame color picker flash
2014027 fix(upgrades): cult_starfruit cost 1125 → 750
2a0077f fix: stash color picker scrub + Quality Crops gold star + Quick Bite chain
e574992 feat(upgrades): JP boost, shop discount + starter gold chains, theme rebalance
208653a feat(upgrades): tiered passive accelerators (Green Thumb / Coal Vein / Forager's Eye)
64cca5b fix(bonus-drops): +1 from rolled set, not full set doubled
526862f fix(bonus-drops): read Debris.itemId.Value — vanilla Object debris leaves .item null
5d5640f diag(mine-bonus): log roll value + wrap bonus path in try/catch
```

## What shipped this session

### Bonus-drops layer (rounds 12-14)

- **Read path fix** (`526862f`) — vanilla `Object` debris has `.item == null`
  and stores the id in `Debris.itemId.Value`. The prior `d?.item != null`
  filter dropped 100% of vanilla mining drops. Now reads `itemId.Value`
  and clones via `Game1.createObjectDebris(string id, …)`.
  See memory [`debris_item_null_for_objects.md`].
- **+1 from rolled set, not full doubling** (`64cca5b`) — user feedback:
  "you're doubling, don't want doubling, that's OP, want +1." Snapshot diff
  collects all rolled itemIds, picks ONE uniformly at random per swing.
- **Threshold rebalance** (`e574992`, `BonusDropResolver`):
  `mine_drops_up` 30% → 20%, `forage_yield_up` 25% → 20%, fish bite kept
  at 30%, `all_drops_up` (Mixed) kept at 10% (generalist).
- **Crop growth probabilistic** (`e574992`, `CropGrowthPatch`) — switched
  from deterministic days-2-and-5 model to 20% per crop per day for both
  bonus and liability paths.
- **Coverage expansion** (`44f27bd`, `d8f57a1`) — `all_drops_up` (10%)
  also fires on `Tree.shake` (walking into a tree drops a seed),
  `GameLocation.monsterDrop` (loot from kills), and
  `FishingRod.pullFishFromWater` (fish caught). Fishing treasure
  intentionally NOT covered — already a rare-roll bonus, doubling would
  double-dip on the rarity premium.

### Upgrade catalog (now 161 entries, ~95k JP for the complete set)

Three new 5-tier passive accelerator chains in **Obtainability**
(`208653a` + `2a0077f` for fish):
- **Green Thumb I-V** — 5/10/15/20/25% chance per watered crop per day
  to gain an extra growth tick.
- **Coal Vein I-V** — 5/10/15/20/25% chance per stone to drop +1 coal.
- **Forager's Eye I-V** — 5/10/15/20/25% chance per overnight forage
  spawn to be doubled.
- **Quick Bite I-V** — 5/10/15/20/25% faster fish bite. Stacks
  multiplicatively with the Fishing theme bonus.

All four accelerator chains use the same cost curve: **50 / 125 / 250 /
425 / 650 JP** (sum 1500/chain). T1 at 50 JP is now the cheapest entry
in the catalog. Curve picked specifically against user balance feedback
("13 JP after a week, hoping for ~100 by end of month" — wanted tier 1
to be a sure thing after first run).

Two re-tiered existing chains (`e574992`):
- **Seed Money I-V** (was 2-tier, now 5) — +1k / +2.5k / +5k / +10k /
  +25k g starting bonus. Costs 75/175/350/600/900.
- **Shop Discount I-V** (was single tier "5"). Now 5/10/15/20/25%, all
  PERMANENT (prior single tier was wired to no effect at all). New
  `ShopDiscountPatch` prefixes static `ShopMenu.chargePlayer` —
  currency-type-0 only (gold), amount>0 only (selling untouched).

One new compound-effect chain (`e574992`):
- **Junimo Favor I-V** — 5/10/15/20/25% boost to ALL JP sources
  (donations, bundle/room completions, weekly quest, interim awards).
  Premium pricing (100/250/500/850/1300) because the effect compounds
  with itself. Wired via `JpBoostHelper.Apply(meta, base)` at every
  `+=JunimoPoints` site EXCEPT `tly_addjp` (debug stays raw).

Decoupled cultivations (`d8f57a1` + `2014027`):
- `cult_starfruit` no longer requires `cult_red_cabbage` as a prereq
  (also dropped from MixedSeedsPatch's AND-chain). Cost dropped from
  1125 → 750 to match `cult_red_cabbage`.

### Continue-after-victory mode (`5959de0`)

JP-spend popup pops at loop boundaries — see memory
[`jp_store_only_at_loop_boundaries.md`]. Two trigger paths:

1. **Reset path** — when `OnDayStarted` fires with `_pendingReset` set
   (i.e. the morning after a MonthlyFail at Winter 28), opens the
   `JunimoShrineMenu` first. Menu's `exitFunction` continues with
   `PerformReset` + day-start sync. If the menu can't open
   (cutscene / existing menu), falls through to immediate reset — UX
   miss but never a lost reset.

2. **Win path** — `RunAction.Win` in `OnDayEnding` sets
   `_pendingWinChoice` (only on the FIRST win — subsequent Winter 28
   wins after Keep-playing are silent). Next `OnDayStarted` opens the
   shrine menu; on close, pops a vanilla `createQuestionDialogue`:
     - "Start a new loop" → triggers the same reset path
     - "Keep playing this run" → sets `MetaState.VictoryAcknowledged`
       (persisted immediately via `_store.Save()` — save-scum-proof)
       and continues with no reset

Manual `tly_reset` intentionally NOT routed through the popup — debug
commands stay raw.

### Continue spec — keep_pet (`e0251d4`)

`PetSnapshot` record (Core) + `MetaState.PetState` field +
`PetCarryoverService` (mod). 75 JP, Buildings category — cheapest Keep
because the upgrade is sentimental (no Large Milk equivalent). Pet
type, breed, name, and friendship (0-1000) all carry across resets.
Barn/coop animals explicitly do NOT — `ApplyStartingAnimals` is
untouched.

Snapshot fires BEFORE `loadForNewGame` in `WorldResetService.PerformReset`;
restore fires after `ApplyStartingAnimals` (step 10a). Sets the
`MarniePetAdoption` mail flag on restore so vanilla's day-1 adoption
offer doesn't double up.

### IndicatorRegistry deleted (`af622ab`)

User: "you never got the indicator right, so just remove it and close
it." 217 lines removed. The four `IndicatorRegistry.Dismiss(id)` callers
(`CookbookMenu`, `CraftbookMenu`, `JunimoStashShowMenuPatch`, and now
`SeasonGoalsMenu`) write directly to `MetaState.DismissedIndicators`
instead. Field name kept despite being a slight misnomer post-rename
(it's now strictly a "shown intro quests" tracker) — preserved so
in-progress playthrough saves don't lose state on JSON deserialise.

`WorldResetService.RegisterIndicators` method gone. SMAPI
`Display.RenderedWorld += IndicatorRegistry.OnRenderedWorld` hook gone.
`JunimoStashService.RegisterIndicator` helper gone.

### Fireplace intro quest (`e5e8e6a`)

Quest `tly.-9004` "A gift from the Junimos" → "There's a notice board
by the Community Center fireplace that tracks this season's goals — go
have a look." Gated on `DismissedIndicators.Contains("tly.fireplace")`,
dismissed via `cleanupBeforeExit`/`emergencyShutDown` on
`SeasonGoalsMenu`. `SeasonGoalsMenu` constructor now takes `MetaState`;
`MenuLauncher` passes `_store.State`.

**Backwards-compat:** `FireBookQuestIntros` is now `internal` and
called from `ModEntry.OnSaveLoaded` too. `AddIntroQuest` is idempotent
against the questLog, so calling on every save load is safe. This
means the user's CURRENT save will get the fireplace quest on next
load instead of having to wait a full year to roll over.

### Other small fixes

- **Quality Crops gold star** (`2a0077f`) — `BundleCatalogBuilder.BuildIngredientQualities()`
  pulls the MAX quality per id across every bundle that uses it.
  `RunController.GetQualityForIngredient(id)` exposes it.
  `WeeklyHubMenu.ResolveBonusItemsForTheme` now passes `quality` to
  `ItemRegistry.Create` instead of hardcoded 0. Parsnips for the
  Quality bundle now render with gold star on the hub card.
- **Stash color picker flash kill** (`2a0077f` + `74c221e`) — vanilla's
  `Chest.grabItemFromInventory` calls `ShowMenu()` after every item
  transfer, recreating the menu (picker included). The `ShowMenu`
  postfix strips it, but `update`-postfix is too late (vanilla draws
  the recreated menu BEFORE the next update tick). Belt-and-suspenders:
  `Chest.ShowMenu` postfix + `ItemGrabMenu.update` postfix +
  `ItemGrabMenu.draw` PREFIX. The draw-prefix runs synchronously
  immediately before render, so even a 1-frame flash is impossible.

## TODO state (post-audit clean-up — `ce26c25`)

`TODO.md` was full of stale entries from prior sessions. After the
audit, the **actually-open** list is:

1. **Co-opted day-1 intro cutscene** (v1.1 narrative tier) — replaces
   vanilla event 191393. Lewis stages the threat, Junimo pops up in
   the CC to explain the loop. Needs custom event scripting +
   `MetaState.HasSeenIntro` flag. Chunky.
2. **Festival exit to host map** — ~20 lines. `Event.endBehaviors`
   currently warps to the farm entry; should land on the festival's
   host map (Town for Egg Fair, Beach for Luau, etc).
3. **`fortune_rare_fish` exact rarity rewire** — currently a 0.75×
   bite-rate approximation via Curiosity Lure piggyback. True rarity
   intercept needs patching whatever rolls the fish-rarity table.

**Recently closed (in TODO.md Resolved section):**
- Continue-after-victory mode (this session)
- `keep_pet` (this session)
- `keep_kitchen` / `keep_basement` / `keep_shortcuts` (audit confirmed
  already wired — TODO entry was just stale)
- IndicatorRegistry visual indicator (removed)
- `forage_off` over-suppression (user closed: "it's not an issue")
- Quality Crops gold star
- Stash color picker flash

## What the user has confirmed working

From the live playtest log:
- Mining theme: rocks → +1 picked drop on Mining week
- Mixed theme: half sell prices, 9 weed hits (+1 fiber each), 2 twig
  hits (+1 wood), 1 stone hit, tree chops (4 hits in log)
- Stash: 4-slot cap, immovable, color picker no longer flashes
- JP HUD: tooltip layering correct
- Quality Crops: gold star renders on week-2 farming card
- Weekly hub: theme reroll works, Mixed pick works
- Mine entrance hard block on Foraging weeks

User reported a "tree fell over in Town" mystery — most likely just a
vigorous vanilla `Tree.shake` animation interpreted dramatically. The
shake patch can't cause a tree to actually fall (postfix only
snapshot-diffs debris).

## Pending tests (not on the live DLL yet — staged in bin/)

The DLL the user is currently playing on is `2014027`. The following
need an exit + redeploy + reload to surface:

- Stash color picker draw-guard (`74c221e`)
- Tree shake / monster / fish bonus drops on Mixed week (`44f27bd` + `d8f57a1`)
- Continue-after-victory popups (`5959de0`) — not testable until
  Winter 28 anyway
- `keep_pet` (`e0251d4`) — not testable until reset
- Fireplace intro quest (`e5e8e6a`) — fires on the next save load

## Memories saved this session

- [`feedback_dont_re_offer_closed_tests.md`] — once a test is confirmed,
  retire it from the active list permanently.
- [`debris_item_null_for_objects.md`] — vanilla Object debris has
  `.item == null`; read `itemId.Value` and clone via
  `createObjectDebris(string, …)`.
- [`jp_store_only_at_loop_boundaries.md`] — JP shrine menu only opens
  at reset or win; no in-world tile, no NPC, no hotkey.

## Workflow reminders (unchanged from prior handoff)

- **Local commits only** — TLY has no remote. Don't `git push`.
- **Game-running deploys fail** — use `-p:EnableModDeploy=false
  -p:EnableModZip=false` if the user is mid-playtest; deploy on exit.
- **Standard farm only** — `StandardFarmEnforcer` scrubs the picker on
  new saves; `OnSaveLoaded` bails on existing non-Standard saves.
- **Bash for bash, PowerShell for PS syntax.**
- **No `/sdcard/` paths** (workspace-wide rule — irrelevant for
  TLY/PC but the workspace memory still flags violations).
- **Co-Authored-By footer** — every commit gets the `Claude Opus 4.7
  (1M context)` footer.
- **Explicit Type[] in every `[HarmonyPatch]`** if the target method
  has any chance of overloads. The round-8 fix surfaces failures
  individually now via the per-class try/catch loop in `ModEntry`,
  but the lesson is to avoid the failure in the first place.

## How to pull the log without killing the game

```powershell
Copy-Item "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" `
  "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\SMAPI-latest.txt" -Force
```

PC SMAPI doesn't lock the log the way Android does, so this is safe
mid-session.

To kill + pull at end of session:

```powershell
Get-Process -Name "Stardew Valley","StardewModdingAPI" -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Seconds 2
Copy-Item "$env:APPDATA\StardewValley\ErrorLogs\SMAPI-latest.txt" `
  "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\SMAPI-latest.txt" -Force
```

## How to refresh the upgrade catalog HTML view

```powershell
& "C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear\scripts\view-upgrades.ps1"
```

Reflects over `TheLongestYear.Core.dll`, drops a styled HTML at
`%TEMP%\tly-upgrades.html`, opens in the default browser. Current
output: **161 upgrades, ~95k JP for the complete set**.

## What's NOT done

The actually-open TODO list (above) is the punch list. The biggest
near-term lift would be the **co-opted day-1 intro cutscene** — that's
the only chunky narrative item left. Festival exit + fortune_rare_fish
are both small isolated cleanups available whenever.

When the user exits the current playtest:
1. Kill SMAPI + pull final log.
2. Build with deploy on (game is dead).
3. Relaunch.
4. Verify the staged work surfaces: fireplace quest pops on save load;
   stash color picker no longer flashes on add/remove.
