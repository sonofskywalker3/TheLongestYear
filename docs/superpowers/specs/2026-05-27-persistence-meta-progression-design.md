# Persistence + meta-progression — LY1 design

**Status:** approved 2026-05-27
**Scope:** LY1 (Year 1) only. LY2 / LY3 each get their own JP economy + upgrade
catalog, potentially as separate mods — not designed here.

## Problem

`FarmerReset.ToBaseline` wipes everything on every `tly_reset`: tools, money,
skills, mail, events, friendships, quests. The only things that survive are
banked JP and the `OwnedUpgrades` list in `MetaState` — but the *effects* of
those owned upgrades aren't applied during reset yet (the effects layer is
deferred to Plan 06). So even after buying `backpack_1` or `keep_coop`, the
player still wakes up with 12 inventory slots and no coop.

The user playtest pain that triggered this design: *"I'm tired of having no
tools when I test a reset."* Resolving that and the rest of the persistence
question together so the meta-progression layer ships coherently rather than
piecemeal.

## Player-facing carryover model

What carries across runs, and how.

| Category | Model | Catalog entries |
|---|---|---|
| Money | Existing `starter_gold_1/2` — wire effect | 0 new |
| Backpack | Existing `backpack_1/2` — wire effect | 0 new |
| Tools (base 5) | **Always present** at run start (rusty tier). No upgrade. Vanilla baseline. Scythe always rusty (no upgrade path exists). | 0 new |
| Tool tiers | Per-tool per-tier "Keep [Tool] at [Tier]" — chained, cap-not-grant | 16 (4 tools × 4 tiers) |
| Fishing rod | "Keep Fiberglass Rod" → "Keep Iridium Rod" — chained, cap-not-grant | 2 |
| Skill levels | Per-skill per-level "Keep [Skill] Level N" — chained 1→2→…→10, cap-not-grant. XP floored to kept level on reset. Profession picker re-triggered for kept L5/L10. | 50 (5 skills × 10 levels) |
| Mine elevator | "Keep Mine Elevator Floor 10/20/…/120" — chained, cap-not-grant | 12 (floors 10–120) |
| Friendships | **Full reset** every run. Defer per-NPC carryover to LY3 perfection chase. | 0 |
| Cooking recipes | **Cookbook** (slot-based banking) — kitchen counter sprite + quest intro + menu UI. Gated by kitchen built (`HouseUpgradeLevel >= 1`). | 3 (Cookbook I/II/III) |
| Crafting recipes | **Craftbook** (slot-based banking) — farmhouse table sprite (replaces vanilla bowl) + quest intro + menu UI. No in-run prereq. | 3 (Craftbook I/II/III) |
| Buildings | Existing `keep_coop` / `keep_barn` / `keep_kitchen` / variants — wire effects | 0 new |
| Starter animals | Existing `start_chicken` etc. — wire effects + track `AnimalSpeciesEverOwned` | 0 new |
| Vault bus | Existing `keep_bus_unlocked` — wire effect | 0 new |
| Inventory items | Junimo Stash (Plan 07) — player picks N items to bank | 0 new (covered by Plan 07) |
| Wallet flags (Skull Key, Magic Ink, etc.) | **Full reset** for v1. None are required for any CC bundle. Defer per-item keep upgrades to LY3 perfection chase. | 0 |

**Net new catalog additions:** ~86 entries.

### Core model: "cap-not-grant"

The retention upgrades (tools, skills, mine elevator) all share one mental
model: **buying an upgrade is permission to retain a peak achievement, not a
grant of that achievement.** Examples:

- Player buys `Keep Iron Watering Can` → resets with whatever tier their
  watering can actually is. If they're still on copper, they keep copper.
  If they reach iron in-run, they keep iron.
- Player buys `Keep Farming Level 5` → if they only hit Level 3 in-run, they
  keep Level 3.
- Player buys `Keep Mine Elevator Floor 80` → if they reached Floor 60 in-run,
  they keep elevator access to Floor 60 next run.

This eliminates the bootstrapping problem (don't need JP to access tools to
earn JP) while making the buy-with-JP decision still meaningful — you only
benefit from the upgrade when you do the in-run work that triggers it.

### Chained prerequisites

Every keep-chain upgrade requires the previous tier to be owned, matching
the existing `backpack_2` requires `backpack_1` pattern:

- `Keep Copper Hoe` → no prereq
- `Keep Steel Hoe` → requires `Keep Copper Hoe`
- `Keep Gold Hoe` → requires `Keep Steel Hoe`
- `Keep Iridium Hoe` → requires `Keep Gold Hoe`

Same for skill levels (Keep L1 → Keep L2 → …) and mine elevator (Keep Floor
10 → 20 → … → 120). The shrine shop UI **hides** entries whose prerequisite
isn't yet owned — players see only the next tier they can buy, not the
whole locked-out chain.

### Skill XP flooring + profession re-pick

When a skill is restored to a kept level on reset:
- XP is set to the **exact threshold** for that level (no half-progress
  preserved). E.g. `Keep Level 2` with 380 XP banked → reset to 100 XP
  (the L2 threshold), not 380.
- For every skill at L5 or L10 where the player owns the matching
  `Keep Level 5/10` upgrade, **re-trigger the vanilla profession picker
  dialog** so the player can re-choose their professions every loop.

### Recipe banking: "currently-known only" rule

When the player opens the Cookbook/Craftbook UI:
- **Currently-known recipes only**: the picker shows only recipes in the
  current `Farmer.cookingRecipes` / `Farmer.craftingRecipes` dictionary.
- Empty slot → click → list of currently-known unslotted recipes →
  player picks one to bank.
- Filled slot → click → option to remove (recipe lost next run unless re-banked).
- This avoids the "save a recipe I don't actually know" contradiction:
  if you could remember a recipe well enough to slot it, you wouldn't
  need to save it.

On reset, banked recipes are re-granted to the player's dictionaries.

## New components

### A. Effects layer for existing upgrades (Plan 06 work, no new UI)

Wire `MetaState.OwnedUpgrades` checks into `FarmerReset.ToBaseline` and the
world rebuild so existing catalog entries actually do something. Currently
they're catalog-only.

Specific wiring required:
- `backpack_1` → MaxItems = 24 (currently hardcoded to 12)
- `backpack_2` → MaxItems = 36
- `starter_gold_1` → +500g over baseline
- `starter_gold_2` → +1500g (replaces +500)
- `keep_coop/_big_coop/_deluxe_coop` → pre-build the structure on the farm
- `keep_barn` variants → same
- `keep_kitchen` → set `HouseUpgradeLevel >= 1`
- `keep_bus_unlocked` → pay all 4 Vault bundles via existing `VaultRules`
- `early_horse` → spawn horse + stable
- `start_<animal>` entries → put the animal in the matching housing
- `carry_xp_25/50` → DEPRECATED; replaced by per-level skill keeps
- `cult_*`, `fortune_*`, `shop_discount_*`, `weather_sage_*`, `cart_whisper_*` —
  Plan 06 hook points, not in this design's scope

### B. Per-stat keep upgrades

New catalog entries + reset-time logic:

**Tool tiers** (16 entries — chained per tool):
```
Keep Copper Hoe → Keep Steel Hoe → Keep Gold Hoe → Keep Iridium Hoe
Keep Copper Watering Can → Keep Steel … → Keep Gold … → Keep Iridium …
Keep Copper Axe → … (same chain)
Keep Copper Pickaxe → … (same chain)
```

Plus fishing rod (2 entries):
```
Keep Fiberglass Rod → Keep Iridium Rod
```

**Skill levels** (50 entries — chained per skill, L1 → L10):
```
Keep Farming Level 1 → 2 → 3 → … → 10
Keep Mining Level 1 → 2 → … → 10
Keep Foraging Level 1 → 2 → … → 10
Keep Fishing Level 1 → 2 → … → 10
Keep Combat Level 1 → 2 → … → 10
```

**Mine elevator** (12 entries — chained):
```
Keep Mine Elevator Floor 10 → 20 → 30 → … → 120
```

Reset-time logic:
- For each tool: look up highest "Keep [Tool] at [Tier]" owned. The player
  starts with that tool at `min(owned_keep_tier, in-run_peak_tier)`.
- For each skill: look up highest "Keep [Skill] Level N" owned.
  XP = threshold for `min(owned_keep_level, in-run_peak_level)`.
- Mine elevator: store deepest reached floor as a per-run high-water mark in
  `RunState`. On reset, restore mine state to `min(owned_keep_floor, run_peak_floor)`.

### C. Cookbook + Craftbook

Two parallel components with the same UX shape, different host objects and
prerequisites.

**Cookbook:**
- World object: replaces a tile on the kitchen counter (sprite TBD — art
  task).
- Visible only when both `HouseUpgradeLevel >= 1` AND `OwnedUpgrades`
  contains `cookbook_1` or higher.
- Quest fires the first day-start after both conditions are met. Quest
  text: *"The Junimos left a cookbook on your kitchen counter — go have
  a look."*
- ? indicator bubble draws above the counter until first interaction.
- Interaction → opens Cookbook menu (modal). Slot count = sum of owned
  Cookbook tier slot allotments.
- Menu UI:
  - Header: "Cookbook — X / Y slots"
  - Slot rows: for each slot, show the banked recipe (or "[empty]")
  - Empty slot click → recipe picker (list of currently-known cooking
    recipes that aren't already slotted)
  - Filled slot click → confirm "Remove from cookbook?" (recipe lost next
    run unless re-banked before reset)
- Banked recipes stored in `MetaState.CookbookRecipes` (List<string>).

**Craftbook:**
- World object: replaces the default bowl on the FarmHouse main table.
- Visible whenever `OwnedUpgrades` contains `craftbook_1` or higher.
  No in-run prerequisite.
- Quest fires day 1 of the first run after purchase. Quest text: *"The
  Junimos left a craftbook on your kitchen table — go have a look."*
- Same indicator + interaction flow as Cookbook.
- Banked recipes stored in `MetaState.CraftbookRecipes` (List<string>).

**Slot counts** (initial proposal, tunable):

| Upgrade | Slots | Cost |
|---|---|---|
| Cookbook I | 5 | 150 JP |
| Cookbook II | 10 | 350 JP |
| Cookbook III | 20 | 700 JP |
| Craftbook I | 5 | 150 JP |
| Craftbook II | 10 | 350 JP |
| Craftbook III | 20 | 700 JP |

Total = 20 banked recipes of each type at max.

**Recipe re-grant on reset:** after FarmerReset clears `Farmer.cookingRecipes`
and `Farmer.craftingRecipes`, re-add every recipe in `MetaState.CookbookRecipes`
and `MetaState.CraftbookRecipes` to the corresponding dictionaries with
`value = 0` (the vanilla "learned but never cooked" marker).

### D. Indicator bubble system

Reusable system for drawing ? / ! over interactable world objects to signal
"new thing here, take a look."

API sketch:
```csharp
internal static class IndicatorRegistry
{
    void Register(string id, GameLocation loc, Vector2 tile, IndicatorKind kind);
    void Dismiss(string id);
    bool IsActive(string id);
}
```

Dismissed indicators stored in `MetaState.DismissedIndicators` (HashSet<string>)
so they don't re-show across reset.

Initial uses:
- `tly.fireplace` — drawn over the Season Goals fireplace before first
  interaction. (Existing fireplace board, missing indicator today.)
- `tly.cookbook` — drawn over the kitchen counter before first cookbook
  interaction.
- `tly.craftbook` — drawn over the farmhouse table before first craftbook
  interaction.

Renders via SMAPI's `Display.RenderedWorld` event so the bubble sits in the
world layer correctly.

### E. Generalize `MetaRequirement`

Today `MetaState.MeetsMetaRequirement(string)` handles `species:<name>` only.
Generalize the dispatch to support more namespaces without breaking the
existing format:

| Namespace | Meaning |
|---|---|
| `species:` | Player has ever owned this animal species (existing) |
| `upgrade:<id>` | Player owns this upgrade — same as existing prerequisite chain |
| `quest:<id>` | Player has completed this vanilla / TLY quest |
| `mail:<flag>` | Player has received this mail flag (in current run OR ever, depending on suffix) |
| `season:<n>` | Player has reached this many resets / has been to season N at least once |

Format remains `<ns>:<value>`. Unknown namespaces return false (default-deny,
forward-compatible).

`UpgradeDefinition.MetaRequirement` already accepts the string; the dispatcher
in `MetaState.MeetsMetaRequirement` is the only thing that needs updating.

### F. Shrine UI: hide chain-locked entries

`UpgradeCatalog` already declares `PrerequisiteId` on chained entries. The
shrine shop UI currently shows locked entries with a "(req X)" hint. Change
to **hide** entries whose `PrerequisiteId` isn't owned, so the player only
sees what they can buy next.

Locked-by-`MetaRequirement` entries (e.g. `start_chicken` needs
`species:Chicken`) likewise hidden until the meta-requirement is satisfied.

## Architecture notes

- **`MetaState` additions** (persist forever):
  - `List<string> CookbookRecipes` — banked cooking recipes
  - `List<string> CraftbookRecipes` — banked crafting recipes
  - `HashSet<string> DismissedIndicators` — ?/! bubbles already dismissed

- **`RunState` additions** (per-run, reset on `BeginNewRun`):
  - `int PeakMineFloor` — deepest mine floor reached this run, used as the in-run cap for elevator restoration on next reset.

- **`FarmerReset.ToBaseline` is no longer a static method that takes only
  the player and starting money.** It needs to read `MetaState` to apply
  all the wired effects. Either pass `MetaState` in or move the reset
  logic onto an instance class.

- **`UpgradeCatalog` is hand-authored and growing.** With ~86 new entries
  spread across 4 chain types (tools / skills / mine / books), consider:
  - Programmatic generation for the per-tier and per-level chains
    (loops over tools × tiers, skills × levels, floors). Cuts catalog
    boilerplate from ~200 lines to ~50.
  - Keep one-off entries (Cookbook I/II/III, Craftbook I/II/III,
    existing entries) hand-authored.

## Out of scope (deferred)

- **LY2 / LY3 transition mechanics.** Separate JP economy, separate themes,
  potentially separate mods with hard-coded starting state. Noted from
  user roadmap discussion 2026-05-27; not designed here.
- **Friendship per-NPC retention.** Deferred to LY3 perfection chase.
- **Wallet flag per-item retention.** Deferred to LY3 perfection chase.
  None are bundle-critical.
- **Plan 06 effects (theme bonuses/liabilities).** Separate plan.
- **Plan 07 Junimo Stash item carryover.** Separate plan; this design
  references but doesn't supersede it.

## Implementation phasing

The design splits cleanly into two implementation plans:

### Phase A — Effects layer + per-stat keep upgrades (first plan)

The work that solves the user's immediate testing pain. No new UI surface
beyond shrine filter changes.

- Wire effects for all existing catalog entries (backpack, gold, buildings,
  animals, vault bus, horse).
- Add the 16 + 2 tool/rod keep entries (programmatically generated).
- Add the 50 skill level keep entries (programmatically generated).
- Add the 12 mine elevator keep entries (programmatically generated).
- Update `FarmerReset` to read `MetaState` and apply tier-cap logic.
- Skill XP flooring + profession picker re-trigger.
- Generalize `MetaState.MeetsMetaRequirement` (add `upgrade:`, `quest:`,
  `mail:`, `season:` namespaces; preserve `species:`).
- Filter shrine UI to hide chain-locked + meta-locked entries.
- Track `RunState.PeakMineFloor` during play; restore mine state on reset.
- Deprecate `carry_xp_25/50` (replaced by per-level keeps); leave defs
  in the catalog as "obsolete" or remove cleanly with no migration since
  no one has banked them yet.

### Phase B — Cookbook + Craftbook (follow-up plan)

Self-contained UI plan, depends on Phase A's `MetaState` extensions.

- Cookbook + Craftbook world objects (sprites, placement).
- Cookbook + Craftbook menu UI (slot management).
- Indicator bubble system (reusable for fireplace etc.).
- Quest intros (vanilla `Quest` system).
- Recipe re-grant on `FarmerReset`.

### Phase C — Future (LY3)

Wallet flag per-item retention + friendship per-NPC retention. Designed
when LY3 perfection chase is being scoped.

## Success criteria

For Phase A:
- `tly_reset` followed by sleeping through day 1 leaves the player with:
  - Tool tiers matching their highest-purchased "Keep [Tool] at [Tier]"
    keeps (capped at peak in-run tier).
  - Skill levels + XP matching the highest-purchased "Keep [Skill] Level N"
    keeps.
  - Mine elevator accessible to the highest "Keep Mine Elevator Floor N"
    threshold.
  - Backpack at the right size per owned upgrade.
  - Starter gold at the right amount per owned upgrade.
  - Buildings + starting animals matching owned upgrades.
  - Vault bus restored (if `keep_bus_unlocked` owned).
- Profession picker fires on day 1 for every skill at L5 / L10 with the
  matching keep upgrade.
- Shrine shop UI hides locked-prereq entries.
- 223 existing tests still pass; new tests cover per-tier / per-level
  cap logic.

For Phase B (when planned):
- Cookbook + Craftbook sprites render at the right tiles when owned.
- ? indicator draws before first interaction; dismissed after.
- Slot menu enforces slot count, shows only currently-known recipes.
- Banked recipes restored to player after reset.
