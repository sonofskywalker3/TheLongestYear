# Handoff — TLY beta bug-fix + playtest session (2026-06-08)

Dev version at handoff: **v0.9.24** on `master`. Game was running (live playtest) on **Spring 1,
Run 16** (seed -725868728) after a `tly_failreset`. Smoked Legend is in the stash.

## What shipped this session (committed, on master)

| Ver | Change |
|-----|--------|
| 0.9.18 | Guarantee one special-weather day in week 1 of each season (u/Tutorem) |
| 0.9.19 | **Junimo Stash preserves flavored-good identity + value** (#2 — smoked Legend) |
| 0.9.20 | **Reconcile item-donation ledger from vanilla CC at day-end** (#1a — khauser13 "completed but reset") |
| 0.9.21 | **Keep villagers out of the CC during a run** (#5 — NPC schedule routing) |
| 0.9.22 | Expose `tly_setday` as a console command (test tooling) |
| 0.9.23 | Cart preview requires Cart Catalog mod installed (user pref reversal) |
| 0.9.24 | **#3 tool-reach snapshot diagnostic** (RunReachEvaluator.LogToolSnapshot) |

## Confirmed in playtest ✅
- **#1a (v0.9.20)** — met the Spring gate by hand-donating real items → game **advanced to Summer**
  (did NOT falsely reset). The reconcile works.
- **#2 (v0.9.19)** — Smoked Legend survived a real loop reset with full value (user-confirmed).
- **#5 (v0.9.21)** — Clint not in the CC furnace room on a Friday (looked good; the final
  "is-he-at-the-blacksmith" confirm wasn't formally closed, but no NPCs seen in the CC).

## Confirmed in playtest ✅ (cont.)
- **In-flight Clint tool upgrade survived loop reset — FIXED + CONFIRMED (v0.9.30).** NEW bug found
  this session (not in original list). A tool being upgraded at Clint's lives in
  `Farmer.toolBeingUpgraded`, not `p.Items`, so `FarmerReset`'s `p.Items.Clear()` missed it; a
  finished-but-uncollected Copper Hoe survived a `tly_failreset` and Clint handed it back (free
  upgrade, bypassing revert-to-baseline). Fix: clear `toolBeingUpgraded` + `daysLeftForToolUpgrade`
  in `FarmerReset`. Proven from the post-reset save (`toolBeingUpgraded` empty, hoe back to lvl 0).
- **Vault indices remix-aware — FIXED + CONFIRMED (v0.9.26).** Verified via the v0.9.27 save-load
  diagnostic: `Vault bundles (this save): 23=2,500g, 24=5,000g, 25=10,000g, 26=25,000g` (derived from
  live bundle data, not the hardcoded 34-37). Season Goals vault line also restyled into a real list
  row with a coin icon (v0.9.28-29).
- **Double-pick theme on reset — FIXED + CONFIRMED (v0.9.25).** Added a second `_store.Save()` right
  after `DoDayStartSeasonAndHub()` in `ContinueAfterResetSpend` (`RunController.cs`). Playtest
  `tly_failreset` (Run 17, 2026-06-08): exactly ONE `Week 1 selection offer` line + ONE `Selected`
  line; pick stuck, no re-roll. See struck-through section below for the original diagnosis.

## Confirmed bugs reproduced/diagnosed but NOT yet fixed

### 🟠 Cart Catalog porch crate survives the loop reset (cross-mod loop pollution). NEW this session.
Player saw a Cart Catalog delivery crate on the porch after a reset. Mechanism (confirmed by code):
Cart Catalog delivers on `GameLoop.DayStarted → PorchDelivery.PlaceIfDue()` reading its OWN persistent
`OrderStore` (mod save data), with **no TLY/RunActivation gate** (`CartCatalog` has zero references to
TheLongestYear). TLY's `WorldResetService` resets the world but cannot/doesn't clear Cart Catalog's
private OrderStore, so a pending order placed in a prior run re-delivers post-reset. Fix is cross-mod
and needs design — options: (a) TLY clears Cart Catalog's pending orders on reset (requires a CC API
or direct mod-data poke), or (b) CC gates `PlaceIfDue` on a TLY "run active + same run" check. Both
couple the mods; (b) is cleaner (CC opts into TLY awareness). NOT fixed — logged for a design pass.
Note the crate OBJECT also needs clearing if it was already placed pre-reset (farm `objects` dict).

### ~~🔴🔴 Double-pick theme on reset~~ — FIXED in v0.9.25 (see above).
Reproduced live via `tly_failreset`. The reset presents the Week-1 offer, but `OfferPresentedWeek`
is set in `DoDayStartSeasonAndHub()` (`RunController.cs:~368`) **after** the post-reset
`_store.Save()`/`ForceFullSave()` (~365-366). The deferred `SaveLoaded → MetaStore.Load()` reloads
`Run` with `OfferPresentedWeek` reverted, so the day-start path re-presents; because the first pick is
now in `SelectedThemesThisMonth`, `SelectionService.OfferForWeek` re-rolls a different pair and the
**second pick overwrites the first** (log: picked Farming → forced Fishing/Mixed → Fishing).
**KEY FINDING from the log:** the reset-time hub **survives the reload** (the player picks from it),
so the safe fix is to **persist `OfferPresentedWeek` before the deferred reload** — e.g. add a
`_store.Save()` right after `DoDayStartSeasonAndHub()` so the reload reads the marker back as set and
the day-start guard skips the re-present. (Earlier worry about a "no picker" regression is disproven —
the hub survives.) Verify with a real `tly_failreset` (single pick expected).

### 🔴 Vault bundle indices wrong on REMIXED saves — needs verify + real fix.
This save (remixed) has Vault bundles at **23-26**; `VaultRules` hardcodes **34-37** (and `GoldForIndex`
too). So `IsVaultIndex(23..26)=false` → `DonationObserver` misclassifies a real vault payment as a
normal bundle completion → `VaultBundlesPaid` stays empty → the season gate can never satisfy (except
`keep_bus_unlocked`). **Verify** whether 34-37 is non-remixed-only (does the vault gate work on a
non-remixed save?). **Real fix:** derive both the vault indices AND their gold values from the live
`Game1.netWorldState.Value.BundleData` (room == "Vault") instead of hardcoding. Usages to update:
`VaultPaymentSync.cs:33`, `DonationObserver.cs:152`, `DonationService.GoldForIndex`, `VaultRules`.
NOTE: a `tly_payvault` workaround was used mid-test and the user (rightly) objected — see
memory `feedback_no_test_workarounds`. The reset clears that fake payment.

### 🟠 Bus-repair (Vault) Season Goals UI is inconsistent with other goals.
User: restyle the Vault/bus-repair entry in `SeasonGoalsMenu.cs` to match the bundle goals' layout.
Pairs with the vault fix.

### ✅ #3 "keep tool upgrades" — CONFIRMED WORKING with a faithful test (2026-06-08). NOT a bug.
The earlier diagnostic caught a **grant artifact**, not khauser13's bug: with a granted copper pickaxe
the bag had TWO pickaxes (`Pickaxe(L0) Pickaxe(L1)`), and `RunReachEvaluator.ToolLevel` returns the
**first** match (L0), masking the copper. A *real* Clint upgrade replaces the tool in place (ONE tool),
so that never triggers. Faithful retest this session: real Clint upgrades (copper axe + steel watering
can), hit the Spring-28 reset boundary — the keep options showed in **both** the planning shrine AND
the JP shop, were purchased, and **persisted into the next run**. khauser13's "keeps missing" does not
reproduce on a genuine upgrade. OPTIONAL hygiene only: make `ToolLevel` return the HIGHEST match
instead of the first, so a freak duplicate-tool bag can't mask an upgrade (no player can create that;
defensive only — not done).

### ⏳ #1b — Vault-finish-on-day-28 boundary race — UNREPRODUCED.
`tly_failreset` does NOT trigger the vanilla CC-completion cutscene, so #1b didn't fire. Needs a real
"complete the whole CC incl. Vault on day 28, then sleep" scenario; the SMAPI log captures the frame
ordering (EvaluateDayEnd branch, `eventUp`/menu blocking the shrine, ForceFullSave skip).

## Testing tooling (use these)
- **SMAPI console injector:** `tools/send-smapi-command.ps1` (drives `tly_` commands into the running
  game; see memory `smapi-console-input-injection`). Usage:
  `pwsh -NoProfile -File tools/send-smapi-command.ps1 "tly_setday 28" "tly_openstash"`.
- **Deploy cycle:** the game LOCKS the mod DLL — to load new code you must CLOSE the game, rebuild
  (`dotnet build src/TheLongestYear/TheLongestYear.csproj -c Release` auto-deploys), then relaunch
  `StardewModdingAPI.exe`. A save-reload does NOT reload the DLL. Pull logs from
  `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt` (overwritten each launch — archive first).
- Useful commands: `tly_setday <n>`, `tly_additem <(O)id> [n]`, `tly_openstash`, `tly_openshop`,
  `tly_failreset`, `tly_addmoney <n>`, `tly_runstate`. `tly_additem` grants quality-0 only.
- **Reading this save's bundles:** parse `<bundleData>` out of the save XML at
  `%APPDATA%\StardewValley\Saves\<name>\<name>` (use the Windows path in Python). Spring gate logic +
  quotas are in `GameplayConfig.DefaultBundleQuotas` / `DefaultItemSeasonPins` / `BundleClassifier`.

## Hard rules reaffirmed this session (see memory)
- **No test workarounds without asking** (`feedback_no_test_workarounds`): never use a debug shortcut
  that bypasses the bug under test or that a player can't do — reproduce or fix, or ASK first.
- Commit + bump version every change; one change per commit. Don't push/release without explicit OK.

## Recommended next order
1. **Fix the double-pick** (confirmed cause + safe fix) → rebuild → user `tly_failreset` to verify.
2. **Verify + fix the vault index bug** (derive from BundleData) + restyle the bus-repair Season Goals.
3. Set up the faithful **#3** test (real Clint upgrade) and the **#1b** Vault-finish repro for logs.
