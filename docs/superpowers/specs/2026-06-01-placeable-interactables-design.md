# Design — Placeable Interactables (Spec B) — 2026-06-01

Part of a two-spec batch. The sibling spec (**Keep System v2** — in-run purchase
gating, Keep Golden Scythe, Keep Mastery 1–5) is independent and tracked
separately. This spec covers the world-interaction surfaces only.

## Problem

The mod's interactables are anchored to fixed map tiles via config coordinates
(`CookbookTileX/Y`, `CraftbookTileX/Y`, `SeasonGoalsBoardTileX/Y`). This is
fragile and bad for a hands-off beta:

- The default tiles don't match every farmhouse, so a fresh player gets a book
  that opens on the wrong spot (or, on the farmhouse table, just grabs the bowl
  furniture because the configured tile isn't actually the table).
- The table is movable furniture, so any tile we pick can drift.
- Setup currently requires debug commands (`tly_setcookbook`, etc.).

## Goal

Make the surfaces robust and placeable, with no debug setup:

- **Cookbook, Craftbook, Bundle-log** become **place-and-click furniture** the
  player drops wherever they like (like a Catalogue). The player always starts a
  loop holding all three; they can never be duplicated or permanently lost.
- The **Junimo Stash** gets a distinct purple/gold sprite (so it's never confused
  with vanilla 1.6 craftable Junimo Chests) and is **home-anchored** — it respawns
  at its farm spot every loop, so it can't be stranded in the desert/quarry.
- A **view-only planning shrine** sits next to the farmhouse from loop 1, showing
  what's unlocked and what's buyable next reset, so the player can plan ahead.

## Components

### 1. Three book furniture items (Cookbook, Craftbook, Bundle-log)

- Defined as **custom furniture** via a SMAPI `Data/Furniture` asset edit (ids
  namespaced, e.g. `sonofskywalker3.TheLongestYear_Cookbook` / `_Craftbook` /
  `_BundleLog`), each backed by a shipped sprite.
- Placeable like any furniture. A Harmony patch on `Furniture.checkForAction`
  detects our ids and opens the matching menu — `CookbookMenu`, `CraftbookMenu`,
  `SeasonGoalsMenu` — instead of vanilla furniture behavior.
- **Replaces** the tile-anchored `CookbookInteractable`, `CraftbookInteractable`,
  and `SeasonGoalsBoard` (and removes their `*TileX/Y` config + the
  `tly_set{cookbook,craftbook,board}` debug commands + the fireplace hit-area
  logic). The `SeasonGoalsMenu` itself is unchanged — only how it's opened.
- **Loop-start guarantee (no duplicates, never lost):** on `PerformReset` (and as
  a safety check on `OnSaveLoaded`), for each of the three book ids: sweep **every
  instance** — furniture placed in any `GameLocation` plus any copy in the player's
  inventory — remove them all, then grant **exactly one** of each to the inventory.
  So a loop always begins with the three books in the bag, regardless of where
  they were left, with no accumulation. (Destroy-and-regrant-exactly-one.)

### 2. Junimo Stash — recolor + home anchor

- Ship a **purple/gold recolored** Junimo-chest sprite for the stash so it's
  visually distinct from vanilla craftable Junimo Chests.
- Keep the existing **home-anchored auto-placement** (`JunimoStashService` already
  places it at a farm tile near the farmhouse and re-places on reset). Confirm it
  **respawns at the home spot every loop** — the "persistent player-positioned
  stash" idea is explicitly dropped; the stash always comes home, so it can't be
  stranded. Within a loop the player may shuffle it, but reset returns it home.
- The cap/tagging/carryover behavior is unchanged.

### 3. View-only planning shrine

- A new auto-placed world object near the farmhouse: **same row as the stash
  chest, ~4–5 tiles to its left** (left of the porch stairs). Placed by a small
  service mirroring `JunimoStashService`'s auto-place, present from loop 1.
- On action, opens a **read-only** menu: the upgrades already owned, and the
  upgrades that would be buyable on the next reset (preview), grouped by category,
  for planning. **No JP is spent here** — actual purchasing stays on the
  reset/win shrine popup. Implemented as a read-only mode of `JunimoShrineMenu`
  (or a thin `ShrinePreviewMenu` reusing the catalog + `MetaState`).
- Backed by a shipped shrine sprite.

### 4. Quests

- **Remove** the now-obsolete **book** intro quests — the cookbook and craftbook
  quests and the Season-Goals/fireplace-board quest (`tly.-9004`, which becomes the
  Bundle-log book). The books are carried items, so "go find the board/book"
  guidance no longer applies.
- **Keep** the **stash** quest (`tly.-9003`) — the stash is still a real world
  object the player should be pointed to.
- **Add** one intro quest: *"Check out the Junimo Shrine"*, pointing the player at
  the planning shrine.

### 5. Art (produced as part of this work)

Simple, clean placeholder sprites generated programmatically: three book items, a
planning shrine, and the purple/gold chest recolor. Final/polished art is
deferred; the build does not block on it. The user is not authoring any art.

## Approach

Custom furniture (`Data/Furniture` edit + texture + `checkForAction` patch) is the
right fit for "drop it anywhere and click it," and matches how the vanilla
Catalogue works. Rejected alternatives: tile-anchored interactables (the current
fragile approach we're replacing); use-from-inventory item (the user wants
place-and-click, not bag-use).

## Data / state changes

- `MetaState`/config: drop the `*TileX/Y` coordinates for cookbook/craftbook/board.
  Add the planning-shrine's resolved tile if it needs persisting (or auto-derive
  from the stash tile each load).
- No change to upgrade/JP/run data.

## Testing

- **Pure/Core (unit-testable):** the "exactly one of each book in inventory" target
  logic can be expressed as a pure helper (given counts of placed + held → how
  many to remove / add) and unit-tested.
- **Runtime (SMAPI log + playtest):** place each book → click → correct menu opens;
  after a reset, exactly one of each book is in the inventory regardless of where
  they were left (including if placed in another location); stash is recolored and
  back at its home tile each loop; planning shrine appears left of the stash and
  opens the read-only view; the "check out the shrine" quest points there; the old
  book/board quests are gone.

## Out of scope

- **Keep System v2** (sibling spec): in-run purchase gating, Keep Golden Scythe,
  Keep Mastery 1–5.
- Final/polished art (placeholders ship for the beta).
- Multiplayer.
