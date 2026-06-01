# Keep System v2 — in-run reach gating (Spec A)

**Date:** 2026-06-01
**Status:** Approved design — ready for implementation plan
**Branch context:** `feat/v1-plan-07-junimo-stash` (Spec B / Placeable Interactables shipped first)

## Problem

The Junimo Shrine sells permanent "keep" upgrades (start each run with a tool tier,
skill level, mine-elevator depth, etc.). Both the purchase menu
(`JunimoShrineMenu.VisibleCatalogForActiveCategory`) and the read-only planning
preview (`ShrinePreviewMenu`) currently gate a keep on only:

1. not already owned,
2. its chain prerequisite is owned (`PrerequisiteId`), and
3. its cross-run `MetaRequirement` is met (`species:` / `upgrade:` / `quest:` /
   `mail:` / `season:` — all *persistent, ever-happened* accumulators).

None of these models **what the player actually reached during the current run**.
Consequences seen in playtest:

- **Un-earned keeps are offered.** Loadout tier-1s (backpack, Copper tools) have no
  prerequisite, so they show as buyable even when the player never reached them this
  run. The keep descriptions already *promise* "capped at your in-run reach", but that
  cap is only applied at grant time — the shop never enforces it.
- **Reached tiers don't surface usefully.** Higher tiers are chain-locked behind
  buying the lower ones, so the shop never shows that a Gold tier is now within reach.

The keep descriptions and the reset baseline already assume an in-run reach concept;
this spec implements it as a first-class gate.

## The gating rule (authoritative)

> **For each chain, the shop offers only the lowest un-owned tier — and only if the
> player has reached at least that tier this run.** Buying it reveals the next tier
> *iff* that tier was also reached this run; otherwise the chain disappears from the
> shop until a future run reaches higher.

This is the existing chain prerequisite **plus one new gate**: `in-run reach ≥ this
tier`. No other change to the chain model.

### Worked example (user's, canonical)

Player owns no keeps. This run they reached a **Steel watering can** and a **Copper
hoe**.

- Hoe chain → lowest un-owned = Copper; reach(hoe)=1 ≥ 1 ⇒ **show Keep Copper Hoe**.
- Watering-can chain → lowest un-owned = Copper; reach(can)=2 ≥ 1 ⇒ **show Keep Copper
  Watering Can**. Steel is chain-locked behind Copper, so it is *not* shown yet.

At the post-run purchase shrine (run failed at Summer 1):

- Only **Keep Copper Watering Can** shows.
- Buy it → it is immediately replaced by **Keep Steel Watering Can** (reach 2 ≥ 2).
- Buy Steel → **no** watering-can keep shows (Gold not reached: reach 2 ≥ 3 is false).
- A later run that obtains a **Gold watering can** → **Keep Gold Watering Can** appears
  with its JP cost, so the player knows what to save toward.

### When reach is measured

Reach is read **live, at the moment the menu is built**:

- Read-only preview (placed shrine furniture) → current value, mid-run.
- Purchase shrine (opens at loop boundary: reset or win) → end-of-run value, taken
  **before** the reset wipe.

Tool tier, rod tier, backpack size, skill level, deepest mine floor, mastery level,
and golden-scythe possession are all **monotonic within a run** (they only go up), so
"current value at open" already equals "max reached this run" — no separate max
tracking is needed.

## Approach (chosen: A)

Add an optional `RunReachRequirement` string to `UpgradeDefinition`, mirroring the
existing `MetaRequirement` design. Core owns the *format + threshold comparison*
(parseable and unit-testable); a thin glue-layer evaluator supplies the *live* value
per metric. Both menus add a single reach check. Rejected alternatives: passing a full
reach snapshot into Core (extra plumbing for marginal test gain), and inline
`switch`-on-id checks in each menu (scatters logic, not data-driven).

## §1 — Requirement format + Core parser

New optional field on `UpgradeDefinition`: `string? RunReachRequirement`.

Format (`:`-delimited, same spirit as `MetaRequirement`):

| Requirement              | Meaning (reach ≥ threshold)                          |
|--------------------------|------------------------------------------------------|
| `tool:<kind>:<tier>`     | kind ∈ hoe/pickaxe/axe/watering_can; tier 1–4        |
| `rod:<tier>`             | 1 bamboo, 2 fiberglass, 3 iridium                    |
| `backpack:<tier>`        | 1 = Big (24 slots), 2 = Deluxe (36 slots)            |
| `skill:<name>:<level>`   | name ∈ farming/mining/foraging/fishing/combat; 1–10  |
| `mine:<floor>`           | deepest mine floor, 10–120                            |
| `mastery:<level>`        | 1–5                                                  |
| `scythe:golden`          | flag — player has obtained the Golden Scythe          |

Core adds a `RunReachRequirement` value type:
- `static RunReachRequirement? Parse(string?)` → `(Metric, Key, Threshold)`; flag
  metrics (`scythe`) parse with `Threshold = 1` and a `IsFlag` marker.
- `bool IsMet(int actualReach)` → `actualReach >= Threshold`.
- Null/empty input ⇒ no requirement (treated as always met by callers).

Pure and fully unit-tested (parse round-trips, threshold comparisons, malformed
strings → null/unmet).

## §2 — `RunReachEvaluator` (glue / Integration layer)

A small class (e.g. `Integration/RunReachEvaluator.cs`) with one public method
`bool Meets(string? requirement)`:

1. `Parse` the requirement (null/empty ⇒ `true`).
2. Resolve the **live actual reach** for the metric from `Game1.player` / game APIs:
   - `tool` → the player's tool of that kind, `Tool.UpgradeLevel`.
   - `rod` → player's `FishingRod.UpgradeLevel` (0 training, 1 bamboo, 2 fiberglass,
     3 iridium).
   - `backpack` → `Game1.player.MaxItems` mapped (24 ⇒ 1, 36 ⇒ 2; 12 ⇒ 0).
   - `skill` → the raw skill level (`Game1.player.farmingLevel` etc., unbuffed).
   - `mine` → `Game1.player.deepestMineLevel`.
   - `mastery` → `MasteryTrackerMenu.getCurrentMasteryLevel()`.
   - `scythe:golden` → `Game1.player.mailReceived.Contains("gotGoldenScythe")`.
3. Return `requirement.IsMet(actual)` (or the flag check).

Unknown metric ⇒ `false` (fail closed, logged). This is the only non-unit-testable
piece; verified via the SMAPI log + in-game.

## §3 — Catalog changes (`UpgradeCatalog` + `UpgradeCatalogGenerators`)

Stamp `RunReachRequirement` on every reach-based keep:

- **Tools** (16 rows): `tool:<slug>:<tier>` on each `keep_<slug>_<tier>`.
- **Skill levels** (50 rows): `skill:<slug>:<level>` on each `keep_<slug>_level_<level>`.
- **Mine elevator** (12 rows): `mine:<floor>` on each `keep_mine_elevator_<floor>`.
- **Backpack** (existing Loadout keeps): `backpack:<tier>` on each.
- **Fishing rod** — restructure to a 3-tier chain:
  - `keep_fishing_rod_0` — "Keep Bamboo Pole", **25 JP**, `rod:1`, no prerequisite.
  - `keep_fishing_rod_1` — "Keep Fiberglass Rod", 150 JP, prereq `keep_fishing_rod_0`,
    `rod:2`.
  - `keep_fishing_rod_2` — "Keep Iridium Rod", 425 JP, prereq `keep_fishing_rod_1`,
    `rod:3`.
  Existing ids `_1`/`_2` are unchanged (no save migration); only `_0` is added and
  `_1` gains a prerequisite.
- **Mastery** — NEW chain `keep_mastery_1..5` (Carryover category), chain prereq
  `keep_mastery_<n-1>`, `mastery:<n>`. Owning it persists the mastery level across loops
  (mastery is post-level-10 global progression). Suggested cost ramp (end-game, steep):
  `1000, 1500, 2000, 2750, 3500` JP for levels 1–5 — tunable in the plan.
- **Golden Scythe** — NEW single keep `keep_golden_scythe` (Loadout), `scythe:golden`,
  no chain prerequisite. Suggested cost `250` JP (a convenience tool, not a power
  spike) — tunable in the plan.

## §4 — Filter parity, grant swap, preview layout

- `JunimoShrineMenu.VisibleCatalogForActiveCategory` and `ShrinePreviewMenu` both add
  `if (!reach.Meets(def.RunReachRequirement)) continue;` after the existing checks, so
  the purchase shrine and the planning preview filter identically. The preview keeps
  showing owned keeps (`[x]`) plus reach-gated buyable keeps (`[ ]`).
- `FarmerReset.EnsureBasicTools`: when `keep_golden_scythe` is owned, grant the Golden
  Scythe (`(W)53`) **instead of** the basic scythe each loop (suppress the basic-scythe
  grant). When a `keep_mastery_<n>` is owned, restore mastery to that level on reset
  (set mastery exp to the level threshold), the way skill-level keeps restore skills.
- `ShrinePreviewMenu` layout: move the "Junimo Shrine — Planning" title inside the
  dialogue box (it currently draws above the visible box top), and accommodate the
  now-longer list (scroll, or a higher box / clamp with an indicator).
- The reset baseline (`MetaState.HighestKeptTier` + tier grant) already caps a kept tier
  at the player's in-run reach; with the shop now reach-gated this is largely redundant
  but is retained as a safety net.

## §5 — Testing

- **Core (unit):** `RunReachRequirement.Parse` round-trips and rejects malformed input;
  `IsMet` threshold boundaries; generator output asserts each reach-based keep carries
  the correct `RunReachRequirement` string and that counts are right (tools 16, skills
  50, mine 12, rod 3 incl. bamboo root, mastery 5). The watering-can worked example is
  encoded as a table-driven test over the filter predicate (a pure helper that takes
  owned-set + reach values and returns the visible set), independent of `Game1`.
- **Glue (log-verified):** `RunReachEvaluator` live reads, the Golden-Scythe grant
  swap, and the mastery restore are verified in-game via the SMAPI log (the test
  project references only `TheLongestYear.Core`).

## Out of scope

- No change to non-reach upgrades (Stash, Buildings, Efficiency, Foresight,
  Obtainability, flat Loadout perks like starting gold/seeds) — they keep their current
  `PrerequisiteId` + `MetaRequirement` gating.
- No new purchasing flow; purchasing stays at the loop-boundary shrine popup. The placed
  shrine remains read-only.
- The effects of keeps at run-start (actually granting the tier/level/etc.) are existing
  behavior; this spec only changes what the shop **offers**, plus the two explicitly new
  grants (Golden Scythe swap, mastery restore).
