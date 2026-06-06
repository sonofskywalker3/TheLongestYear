# Vault payment + count-based vault gate — design

*2026-06-06. Fixes the "vault/money gate is invisible AND unpayable in normal play" bug
(TODO.md "ACTIVE BUG"). Master / release line. Single-player only.*

## Problem

Each season's gate ANDs in a vault requirement (`VaultRules.IsVaultGateSatisfied`). Two gaps:

1. **Unpayable in normal play.** The only writes to `Run.VaultBundlesPaid` were the debug
   command (`tly_payvault`) and `WorldResetService` (pre-paying all four when
   `keep_bus_unlocked` is owned). No gameplay code recorded a *real* in-game CC vault payment.
   So without the upgrade the vault term could never go true — and the upgrade itself needs
   run-reach `bus:1` (a paid vault), which was also debug-only → **deadlock**.
2. **Invisible in the UI.** `SeasonGoalsMenu` was built only from item bundles; the vault
   requirement appeared nowhere. (A first pass at the journal line shipped in 0.9.7 against the
   *old* exact-tier semantics — this spec revises it.)

## Decisions (from brainstorm)

- **Count-based, tier-agnostic gate.** A season's vault gate is satisfied when the player has
  paid **at least `seasonOrdinal` vault bundles this run** (Spring 1, Summer 2, Fall 3, Winter 4),
  regardless of *which* tiers. Paying all four in Spring pre-satisfies every season. This
  replaces the old "must pay the matching tier each season" rule. Total gold over a year is
  unchanged (42,500g for all four).
- **Vanilla CC is the source of truth.** Rather than only watching for live completion events,
  a **reconcile** step reads which vault bundles vanilla already marks complete and unions the
  missing indices into `Run.VaultBundlesPaid`. Additive only. This is what makes a player who
  paid on an older version (already-complete, no `false→true` transition to observe) carry over
  cleanly instead of being stuck unable to re-pay.
- **JP: item-only, no completion bonus.** The vault is a one-item (money) bundle. Paying it
  awards a single per-item JP (Common tier, week-scaled, JP-boost applied) and does **not**
  award the bundle-completion bonus.
- **Journal shows per-season sufficiency only.** The green-journal line shows the paid count vs.
  this season's requirement with a met / not-met badge. No "kept via upgrade" label, no
  toward-the-upgrade progress (when the upgrade is owned the count is already full, so it simply
  reads as met). The `keep_bus_unlocked` upgrade is a **shrine** concern, not a journal one.
- **Upgrade needs the whole bus.** Full restoration is all four bundles, so `keep_bus_unlocked`
  requires run-reach **`bus:4`** (all four paid this run), up from `bus:1`. The `bus` metric
  returns the paid count (0–4) instead of a 0/1 flag.

All new gameplay paths gate on `RunActivation.IsActive` + single-player.

## Components

### 1. `VaultRules` (Core) — count-based gate

- `SeasonOrdinal(Season)` → 1..4 (`(int)season + 1`).
- `IsVaultIndex(int)` → true for 34–37 (`Vault2500`..`Vault25000`).
- `VaultIndices` → the four indices (for reconcile/iteration).
- **Rewrite** `IsVaultGateSatisfied`:
  ```
  if (meta.HasUpgrade(KeepBusUnlockedId)) return true;
  return run.VaultBundlesPaid.Count >= SeasonOrdinal(season);
  ```
- `PaidCount(RunState)` → `run.VaultBundlesPaid.Count` (list is kept deduped on insert).
- **Remove** `GoldCostForSeason`, `VaultGateStatus`, and `DescribeGate` (added in 0.9.7 for the
  old tier-specific design; obsolete under count semantics). `BundleIndexForSeason` is also no
  longer used by the gate; keep it only if a test/`tly_payvault` still references it — otherwise
  remove. *(Check `tly_payvault` first.)*

### 2. `DonationService.OnVaultBundlePaid(int index)` — idempotent sink

```
if (!RunActivation.IsActive) return;                 // belt-and-suspenders; callers also gate
if (Run.VaultBundlesPaid.Contains(index)) return;    // idempotent / dedupe
Run.VaultBundlesPaid.Add(index);
long jp = JpBoostHelper.Apply(_store.State, _jp.PerItem(Rarity.Common, Run.WeekOfYear));
_store.State.JunimoPoints += jp;                     // item JP only — NO TryMarkBundleAwarded / bonus
_monitor.Log($"Vault bundle {index} paid -> +{jp} JP (now {_store.State.JunimoPoints}).", Info);
```

This is the single place that records a vault payment + awards JP. Two feeders call it:

### 3. `DonationObserver` — live path (immediate)

In the existing bundle-complete diff (`!wasComplete && b.complete`): if
`VaultRules.IsVaultIndex(b.bundleIndex)` → call `OnVaultBundlePaid(b.bundleIndex)` **instead of**
`OnBundleCompleted` (suppresses the completion bonus for vault). Non-vault bundles keep their
existing `OnBundleCompleted` path. The per-slot loop is unaffected — verified that
`completionAnimation` flips `Bundle.complete` but leaves `ingredients[].completed` false, so the
money "ingredient" never triggers a per-item award.

### 4. `VaultPaymentSync.Reconcile(RunState)` (Integration, new) — backstop

```
if (!RunActivation.IsActive || !Game1.IsMasterGame || Game1.IsMultiplayer) return;
var cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
if (cc == null) return;
foreach (int idx in VaultRules.VaultIndices)
    if (BundleDictHasKey(idx) && cc.isBundleComplete(idx))   // guard: KeyNotFound if missing
        DonationService.Active?.OnVaultBundlePaid(idx);       // idempotent
```

`BundleDictHasKey` checks `Game1.netWorldState.Value.Bundles.FieldDict.ContainsKey(idx)` so
`isBundleComplete` can't throw on a missing index (the documented `WorldResetService` crash
shape). Catches the **mid-run upgrade migration** (already-complete, no transition) and any
payment the observer missed.

**Reconcile call sites** (all mod-side glue with Game1 access):
- `RunController.OnDayEnding` — before computing `vaultGateSatisfied` (correctness for the gate).
- `MenuLauncher.OpenSeasonGoals` — before building the journal (accurate display).
- `MenuLauncher.OpenShrineShop` — before building the shrine (accurate `bus:4` run-reach).

### 5. `RunReachEvaluator` — `bus` returns count

```
"bus" => _runState?.Invoke()?.VaultBundlesPaid.Count ?? 0,   // 0–4, deduped on insert
```
`bus:4` is then met only when all four are paid. (Reconcile at shrine-open runs first.)

### 6. `UpgradeCatalog` — `keep_bus_unlocked` run-reach `bus:1` → `bus:4`.

### 7. `SeasonGoalsMenu` — count-based journal line

Replace the `DescribeGate`-based banner (0.9.7) with:
- `int paid = VaultRules.PaidCount(_run); int need = VaultRules.SeasonOrdinal(_season);`
- `bool met = VaultRules.IsVaultGateSatisfied(_season, _run, _meta);`
- Line: **`Vault (bus repair): {paid} of {need} paid`**, badge **`MET`** (green) / **`NOT MET`**
  (red), and a hint when not met: **`Pay any Vault bundle at the Community Center`**.

## Cross-reset / migration behavior

- **Reset:** `WorldResetService` zeroes every vanilla CC vault slot **and** `BeginNewRun` clears
  `VaultBundlesPaid` → reconcile finds nothing, both stay empty. No stuck state.
- **`keep_bus_unlocked` owned:** `WorldResetService` pre-fills all four into `VaultBundlesPaid`
  (count 4) and the gate short-circuits on the upgrade. Reconcile is additive so it never
  removes them. Journal reads "4 of N → MET". Player never touches the Vault.
- **Mid-run upgrade from an older version:** previously-paid vault bundles are vanilla-complete;
  first reconcile (day-end / journal / shrine) backfills them. No re-paying.

## Deadlock resolution

Paying a real vault bundle now records into `VaultBundlesPaid` → `bus` count rises → by Winter a
full run has `bus:4` → `keep_bus_unlocked` becomes purchasable in the shrine. The first-ever
unlock path no longer depends on the upgrade it gates.

## Tests

- **`VaultRulesTests`** — rewrite for count semantics: gate met at each season when
  `count >= ordinal`; not met when `count < ordinal`; pre-pay all four in Spring satisfies Winter;
  `keep_bus_unlocked` short-circuits with count 0. Remove the obsolete "different tier doesn't
  satisfy" and the 0.9.7 `GoldCostForSeason`/`DescribeGate` tests.
- **`DonationService`** — `OnVaultBundlePaid` records the index, awards item JP (asserts JP rose
  by the Common per-item amount), does **not** mark a bundle-completion award, and is idempotent
  (second call is a no-op).
- **`RunReachRequirementTests` / `RunReachEvaluator`** — `bus:4` parses; met iff count ≥ 4.
- **`UpgradeCatalogTests`** — `keep_bus_unlocked` run-reach string == `bus:4`.
- `VaultPaymentSync.Reconcile` reads `Game1`/`CommunityCenter`, so it isn't unit-tested (same as
  `RunReachEvaluator`); the testable decision (`IsVaultIndex` / which indices to add) lives in
  pure `VaultRules` and is covered above.

## Out of scope

- Whether the bus *physically* works on day 1 under `keep_bus_unlocked` (separate existing
  concern — reconcile is additive and doesn't touch it).
- The animated reset / real ending cutscenes (separate TODO items).
