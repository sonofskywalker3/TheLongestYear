# The Longest Year — Status

**Last updated:** 2026-05-27 (after Plan 06A landed)
**Branch:** `feat/v1-plan-06a-persistence-effects`
**Tests:** 280 passing, 0 failing
**Build:** clean (0 warnings, 0 errors)

## What v1 means

Per the original design spec §14, v1 = "MVP — prove it's fun & stable on PC." Everything below
either ships in v1 or is explicitly deferred.

## Done

| Plan | Branch / commits | Shipped |
|---|---|---|
| **Plan 01 — Foundation** | merged | Core types: `MetaState`, `RunState`, `MetaStore`, `GameplayConfig`, `Calendar`, `Theme`/`Season`/`Rarity` enums, `JpSettings`. |
| **Plan 02 — Contracts** | merged | `RunManager`, `GateEvaluator`, `SelectionService`, `BundleCatalogBuilder`, `BundleGate`, theme/season classification, solvable-partition contract generator. |
| **Plan 03 — Lifecycle / reset** | merged | `WorldResetService` (in-place reset via `Game1.loadForNewGame`), `SaveBackup`, `WorldStateProbe` (leak test), `CommunityCenterUnlock`, `CcLocationAccessiblePatch`. |
| **Plan 04 — Donations + JP** | merged | `DonationService`, `DonationObserver` (Harmony-patched), `BundleCatalogBuilder` (catalog from `Data/Bundles`), `JpCalculator`, `UpgradePurchase` rule, `VaultRules`. |
| **Plan 05 — UI** | `feat/v1-plan-05-ui` | `WeeklyHubMenu` (planning hub), `JunimoShrineMenu` (upgrade shop), `MenuLauncher`, `SeasonGoalsBoard` (CC interactable), `UpgradeCatalog` + `UpgradePurchaseService`. |
| **Festival fixes** | `feat/v1-plan-05-ui` | Time flows during festivals, exit at real in-game time, auto-eject at festival end, HUD redraw during festivals, "Are you sure" suppression, day-8 hub unblock, day-3 forced rain removed, RNG re-seed on reset, Joja root-cause fix. |
| **Plan 06A — Persistence effects + per-stat keep upgrades** | `feat/v1-plan-06a-persistence-effects` | Wires `OwnedUpgrades` into reset effects (backpack, gold, kept coops/barns, kitchen, vault bus, horse, starting animals). Adds 80 chained keep entries (16 tool tiers + 2 rods + 50 skill levels + 12 mine elevator floors). Cap-not-grant via `PlayerSnapshot` (in-run peak captured pre-wipe) + `RunState.PeakMineFloor`. Profession picker re-fires for kept L5/L10 skills. Shrine UI hides locked entries. Generalised `MeetsMetaRequirement` (upgrade/quest/mail/season). |

## Pending for v1

These three blocks remain. After they ship, v1 is ready for a meaningful playtest.

### Plan 06B — Cookbook + Craftbook (next; signed off, ready to plan)

Phase B of the persistence + meta-progression design (`docs/superpowers/specs/2026-05-27-persistence-meta-progression-design.md`).

- World objects: Cookbook on the kitchen counter, Craftbook on the farmhouse table.
- Slot-based recipe banking with the "currently-known only" rule.
- Indicator bubble system (?/! over interactable world objects).
- Quest intros via vanilla `Quest`.
- Recipe re-grant on `FarmerReset`.
- New `MetaState` fields: `CookbookRecipes`, `CraftbookRecipes`, `DismissedIndicators`.

Status: spec signed off; implementation plan not yet written.

### Plan 06 — Theme effects layer (designed, awaiting sign-off)

Bonus/liability mapping per theme (see `TODO.md`):

| Theme | Bonus | Liability |
|---|---|---|
| Foraging | 25% chance for +1 on any forage drop | **Mines Closed** |
| Farming | +25% Crop Growth | -30% Fish Bite Rate |
| Fishing | +30% Fish Bite Rate | -25% Crop Growth |
| Mining | 30% chance for +1 on any mine drop | **Forage Off** |
| Mixed | 10% chance for +1 on any drop | -50% All Sell Prices |

Plus obtainability effects (Cultivation: Red Cabbage / Starfruit / Mixed Seeds injection,
Fortune: Rare Fish catch boost) and foresight data delivery (Weather Sage actual data,
Cart Whisperer actual data).

Status: catalog entries exist (Plan 05 + Plan 06A), but no gameplay effect wired. Design table needs user sign-off before implementation plan can be written.

### Plan 07 — Junimo Stash (designed in §9, needs implementation plan)

Per design spec §9: scarce slots, expandable via tier upgrades, cross-run item carryover.
The "strategic engine" — the meta-skill of opportunistic hoarding.

Status: high-level design exists; UI layout, content-selection rules, end-of-run interaction, item picker need spec sign-off.

## Deferred beyond v1

- **Cookbook/Craftbook Phase C (LY3)** — friendship per-NPC + wallet-flag per-item retention.
- **Cutscenes / full narrative** — placeholder text only in v1.
- **Endless victory-lap mode** — single-win run for v1.
- **Android port** — PC first.
- **Deep balancing pass** — calibrate numbers after v1 has been played.
- **Advanced contract modifiers** — per-run "blessings" etc.
- **SVE compatibility pass** — most pieces are SVE-safe already (see future-expansions notes).
- **LY2 / LY3** — Year 2/3 ultimate-perfection content, separate JP economies, possibly separate mods.

## Small follow-ups (not blocking v1, can land any time)

- **Festival exit to host map.** Currently `Event.endBehaviors` warps to the farm entry; should land on the festival's host map (Town for Egg/Fair/Spirit's Eve; Beach for Luau/Jellies; Forest for Flower Dance). ~20 lines (`endBehaviors` postfix or transpiler).
- **Seed-driven weather scheduler** with per-season minimums. Spec'd in `TODO.md`.
- **Wipe-meta debug command** (`tly_wipemeta`). Trivial — replace `_meta.State` with `new MetaState()` + `_meta.Save()`.
- **Weekly Theme Journal entry.** Player-facing reminder + bonus-item completion tracking → liability suppression on completion. Spec'd in `TODO.md`.

## Workflow rules in effect

- Local commits only. Never push without explicit "yes, push".
- Co-Authored-By footer on every commit.
- Build/test/deploy: I do, user plays, I pull logs.
- Reserve playtests for MEANINGFUL feedback opportunities. Don't request a playtest just to confirm wiring fires — verify that solo.
- Run with `-p:EnableModDeploy=false` while Stardew is open (file-lock on the deployed DLL).
