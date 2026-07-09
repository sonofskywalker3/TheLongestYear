# Slot-based weekly theme checklist — design

**Date:** 2026-07-09
**Status:** approved pending user spec review
**Source reports:** khauser13 (Nexus, 11 Jun — 1x parsnip cleared a "5 crops" goal; mining theme
impossible), Tutorem / emmainthealps / xsansara / Dusklight7 (goal asks for already-donated items).
**User decisions this session:** slot-based redesign (over display-only fix); exact-slot tick;
already-donated slots ineligible; empty pool → no quest + drawback lifted; same-item slot
collision → seeded-random pick.

## Problem

The weekly theme quest samples bare **item ids** and ticks a checklist entry when that id lands
in ANY Community Center slot (`RunState.DonatedThisWeekIds` is id-only). But the quest text and
hub icons display the **MAX** stack/quality across every bundle containing the item. Three
user-visible bugs fall out:

1. **Quantity lie** — "Parsnip x5" (gold) ticks when 1 basic parsnip goes into Spring Crops.
2. **Already-donated asks** — the sampler doesn't know what's been donated, so goals can name
   items with no open slot left (previously ruled "by design", overturned by this redesign —
   4 reporters).
3. **Impossible themes** — a theme can be offered whose goals cannot all be completed (mining
   theme asking 3 items with 2 open slots).

## Design

### 1. The pool: open slots, not item ids

A **slot** = one concrete ingredient line of one bundle: `(bundleIndex, ingredientIndex,
qualifiedItemId, stack, quality, bundleName)`. A slot is **open** iff the live CC state
(`CommunityCenter.bundles[bundleIndex][ingredientIndex]`) says it is not completed.

The weekly bonus pool for a theme = all open slots of that theme's bundles, with the existing
bundle-level gating preserved unchanged:

- Seasonal bundles: in play only during their season.
- PerItem bundles: only slots whose item is pinned to the current season.
- Percentage bundles: only when the season's cumulative quota is non-zero.
- Obtainability predicate + rarity weighting (inverse-rarity draw) + week 1–2
  `EarlyGameAvoid` filter: all carried over, applied per slot via the slot's item id.
- Category-ref slots (e.g. "-5 any egg") and money slots are excluded, as today.

**Already-donated slots are never sampled** ("picking 4 of 9 with 3 donated → picks only from
the remaining 6").

**Same item in multiple open slots:** sampling stays keyed by item id (rarity weighting per id,
one checklist entry per id); the entry's slot is then chosen **seeded-random** among that item's
open slots (same `(seed, week, theme)` determinism as the draw itself). Determinism note: the
draw is a pure function of `(seed, week, theme, pool)`; the pool is snapshotted when the offer
is built, and the picked slots are **persisted at selection** — mid-week donations shrink future
pools, not the committed checklist.

### 2. Checklist display + exact-slot tick

Quest lines name the real requirement and its bundle:

```
[ ] Parsnip x5 (gold) — Quality Crops
[X] Sunfish — River Fish
```

(Stack suffix only when >1; quality tag only when >0; egg-color disambiguation kept.)

An entry **ticks only when its exact slot flips complete** — and vanilla only completes a slot
when the full stack at the required quality is deposited, so multi-item goals now require all
items. Because every sampled slot was open at selection time, "slot is now complete" ⇒ "it was
completed this week": the tick reads the **live CC slot state directly**, which makes it
self-reconciling (no missed-observer drift, no separate per-week donated ledger for slots).

The **1.5× selection-bonus JP multiplier** and the **drawback-lift / weekly JP completion
bonus** follow the same rule: the multiplier applies when the donation that just landed
completed a sampled slot; the quest completes when all sampled slots are complete.
Idempotency of the completion reward stays on `RunState.LiabilitySuppressedThisWeek`.

### 3. Shrinking and empty pools

- Pool smaller than the season's usual count (4–7): the checklist is simply **shorter**;
  completing it still clears the drawback and pays the weekly JP bonus.
- Pool **empty** (everything for the theme already donated): **no quest is created, the
  drawback is auto-lifted immediately** (`LiabilitySuppressedThisWeek = true` +
  `ActiveEffectsProvider.SuppressLiability`), **no weekly JP bonus**, HUD message
  "Nothing left to donate for this theme — drawback lifted." + INFO log.

### 4. Persistence + migration

- `RunState.CurrentWeekBonusSlots` (new): the sampled slot records. Replaces the *use* of
  `CurrentWeekBonusItems`; the old property remains for JSON back-compat reading.
- Migration: on run load, if `CurrentWeekBonusItems` is non-empty but `CurrentWeekBonusSlots`
  is empty (mid-week save from an older version), **re-sample once** with the new sampler,
  rebuild the quest, log INFO. One-time re-roll, beta-acceptable.
- No per-week slot-donation ledger needed (see §2 — live CC state is the source of truth).
  `DonatedThisWeekIds` remains only if other consumers still need it; audit and remove if dead.

### 5. Messaging ("don't rush donations" visibility)

- Hub theme-pick screen gains one line: *"Banking items for a matching theme week pays 1.5× JP."*
- Quest tip reworded to state plainly: *hold donations until their theme week for 1.5× JP and
  the drawback lift.*

## Architecture

- **Core (pure, unit-tested):**
  - `BonusSlot` record + `BonusSlotSampler` (evolves `BonusItemSampler`): takes the open-slot
    pool, returns sampled slots. Same salts/determinism; per-id weighting; seeded slot pick.
  - Quest-objective formatting helper (pure string building) if practical.
- **Mod side:**
  - `SlotPoolBuilder` (Donations/): builds the open-slot pool from live `BundleData` +
    `CommunityCenter.bundles` + the classified `BundleRequirement` list (theme + in-play gating).
  - `DonationObserver`: on slot flip, pass slot identity `(bundleIndex, ingredientIndex)` through
    to `DonationService` alongside the item id (JP multiplier check).
  - `WeeklyThemeQuestService`: renders from `CurrentWeekBonusSlots`, ticks from live CC state.
  - `WeeklyHubMenu` bonus cards: render each sampled slot's own stack/quality (drop the global
    MAX maps for this surface; `SeasonGoalsMenu` and other consumers keep them).
- **Dormant-gate rule:** all new paths run only under `RunActivation.IsActive` (existing pattern).

## Testing

- Core unit tests: open-slot filtering; already-donated exclusion; seeded-random slot choice
  determinism; shrink/empty pool behavior; per-id weighting preserved; early-game filter;
  migration trigger condition.
- Mod-side: build + existing suite green.
- One PC playtest for feel: pick a theme, donate a partial stack (no tick), complete the slot
  (tick + 1.5×), verify shorter/empty-pool weeks late in a test run.

## Out of scope

- Season-checkpoint / win-gate semantics (fixed separately in v0.11.11).
- Balance retuning of quotas or JP prices (0.12.0/0.13.0).
- The "cheese: reset-farming JP" incentive redesign (0.13.0).
