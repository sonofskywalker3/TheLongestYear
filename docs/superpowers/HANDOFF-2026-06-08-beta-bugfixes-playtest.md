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

## Confirmed bugs reproduced/diagnosed but NOT yet fixed

### 🔴🔴 Double-pick theme on reset — ROOT CAUSE CONFIRMED, safe fix known. **DO THIS FIRST.**
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

### ⏳ #3 "keep tool upgrades" missing — REAL bug still UNCONFIRMED.
The diagnostic caught a **grant artifact**, not khauser13's bug: with a granted copper pickaxe the bag
had TWO pickaxes (`Pickaxe(L0) Pickaxe(L1)`), and `RunReachEvaluator.ToolLevel` returns the **first**
match (the basic, L0), masking the copper → row hidden. But a *real* Clint upgrade replaces the
pickaxe in place (ONE pickaxe), where this wouldn't trigger. So khauser13's real case (single upgraded
pickaxe missing from the spend menu at the day-28 boundary) is **still unreproduced**. To test: grant
5 copper bars, do a real Clint upgrade, hit a reset boundary, read the `[reach-snapshot]` line. A
`ToolLevel`→return-highest fix is correct hygiene but is NOT khauser13's bug; don't conflate.

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
