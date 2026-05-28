# Junimo Stash Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Junimo Stash — a real `Chest` world-object on the Farm that persists banked items across runs, with a configurable tile location, a slot cap enforced by a Harmony patch, item serialization into `MetaState`, and full reset/restore wiring.

**Architecture:** Three-layer split.
1. **Pure Core** (`TheLongestYear.Core`) — new `StashItemRecord` POCO and `MetaState.StashItems` field. JSON round-trip tested. `MetaState.StashSlotCount` computed from `HighestKeptTier("stash_", 2)` → maps tier 1→4, tier 2→8.
2. **Mod-side world** (`TheLongestYear/Loop`) — `JunimoStashService` places and re-populates the stash `Chest` on every reset, banks items from the chest into `MetaState.StashItems` on the `Saving` event, registers the indicator bubble, and provides a `tly_setstash` console command (same pattern as `tly_setcookbook`).
3. **Mod-side cap enforcement** (`TheLongestYear/Loop`) — `JunimoStashCapPatch` is a Harmony postfix on `Chest.addItem`. When the chest is marked as the Junimo Stash (via `modData["tly.junimo.stash"] == "1"`) and the current non-null item count already meets the cap, the postfix forces the return value to the item (rejected), playing a HUD message.

**Chest lifecycle:** The stash chest is NOT a permanent world object — the Farm is wiped on every `loadForNewGame`. `JunimoStashService.RestoreStash` re-places the chest after each reset by adding a `Chest` instance to `Farm.objects` at the configured tile. Items are repopulated from `MetaState.StashItems` using `ItemRegistry.Create`. The chest is identified at runtime by `chest.modData["tly.junimo.stash"] == "1"` — this tag is set at placement time and persists in the vanilla save (modData survives the normal save/load cycle, just not a reset, which is why we re-place).

**Slot cap design:** Harmony postfix on `Chest.addItem(Item item)` (virtual, on `StardewValley.Objects.Chest`). The postfix checks: if `__instance.modData` contains `"tly.junimo.stash"` AND `__result == null` (item was accepted by vanilla) AND the non-null item count now exceeds the cap — then set `__result = item` (reject) and remove the just-added item, then show a HUD message. This is "post-accept correction": simpler than a prefix because it lets vanilla handle stacking first (stacking onto an existing stack is always allowed regardless of cap).

**Anti-save-scum:** `JunimoStashService.BankToMeta` is called from `MetaStore.Save`, not from chest-close, matching the existing MetaState write discipline.

**Tech Stack:** C# / .NET 6, SMAPI 4.x, Harmony 2.x, MonoGame, xUnit. Pure Core tested with xUnit. Mod-side verified by playtest.

**Repo conventions:** Working dir `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`. Build:
`dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false`
Test:
`dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Files < 400 lines. Local commits only — **never push without explicit approval**. End every commit body with:
`Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`

**Depends on:** Plan 06 (`feat/v1-plan-06-theme-effects`). Branch from there.

---

## File Structure

### New Core files
| File | Responsibility |
|------|----------------|
| `src/TheLongestYear.Core/StashItemRecord.cs` | POCO record: `string ItemId, int Quantity, int Quality`. JSON round-trip. |

### Modified Core files
| File | Change |
|------|--------|
| `src/TheLongestYear.Core/MetaState.cs` | Add `StashItems` (`List<StashItemRecord>`) field + `StashSlotCount` computed property (reads `HighestKeptTier("stash_", 2)` → 0/4/8). |
| `src/TheLongestYear.Core/GameplayConfig.cs` | Add `StashTileX` / `StashTileY` (both `int`, default 0 = disabled). |

### New mod-side files
| File | Responsibility |
|------|----------------|
| `src/TheLongestYear/Loop/JunimoStashService.cs` | Owns the stash chest lifecycle: `PlaceChest`, `PopulateFromMeta`, `BankToMeta`, `StashSlotCount`, `FindStashChest`. Called by `WorldResetService.PerformReset` (place + populate) and `MetaStore.Save` (bank). Also owns the indicator registration call. |
| `src/TheLongestYear/Loop/JunimoStashCapPatch.cs` | Harmony postfix on `Chest.addItem`. Enforces slot cap for the tagged stash chest; plays HUD message on rejection. |

### Modified mod-side files
| File | Change |
|------|--------|
| `src/TheLongestYear/Loop/WorldResetService.cs` | Step 14b (after `RegisterIndicators`): call `_stashService.PlaceChest()` then `_stashService.PopulateFromMeta()`. |
| `src/TheLongestYear/MetaStore.cs` | `Save()` calls `_stashService?.BankToMeta()` before writing. |
| `src/TheLongestYear/ModEntry.cs` | Construct `JunimoStashService`; wire `tly_setstash` + `tly_openstash` + `tly_stashclear` debug commands; pass service to `MetaStore` and `WorldResetService`. |

### Test files
| File | Change |
|------|--------|
| `tests/TheLongestYear.Tests/MetaStateTests.cs` | Add: `StashItems` defaults empty, round-trips JSON, `StashSlotCount` returns 0/4/8 for tier 0/1/2. |
| `tests/TheLongestYear.Tests/StashItemRecordTests.cs` | NEW: JSON round-trip for `StashItemRecord`; null-safety of empty list. |

---

## Task 0: Branch + baseline

**Files:** none (git only)

- [ ] **Step 1: Create and switch to the feature branch**

Run from `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`:

```bash
git checkout -b feat/v1-plan-07-junimo-stash
git branch --show-current
```

Expected output: `feat/v1-plan-07-junimo-stash`

- [ ] **Step 2: Confirm 320 tests pass before touching anything**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 320`

- [ ] **Step 3: Confirm mod builds cleanly**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit baseline**

```bash
git add .
git commit -m "$(cat <<'EOF'
chore: branch feat/v1-plan-07-junimo-stash from Plan 06

320 tests green, mod builds clean.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 1: StashItemRecord POCO + MetaState.StashItems (Core, TDD)

**Files:**
- Create: `src/TheLongestYear.Core/StashItemRecord.cs`
- Modify: `src/TheLongestYear.Core/MetaState.cs`
- Create: `tests/TheLongestYear.Tests/StashItemRecordTests.cs`
- Modify: `tests/TheLongestYear.Tests/MetaStateTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/StashItemRecordTests.cs`:

```csharp
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StashItemRecordTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var original = new StashItemRecord("(O)24", 3, 2);
        string json = JsonSerializer.Serialize(original);
        StashItemRecord restored = JsonSerializer.Deserialize<StashItemRecord>(json)!;

        Assert.Equal("(O)24", restored.ItemId);
        Assert.Equal(3, restored.Quantity);
        Assert.Equal(2, restored.Quality);
    }

    [Fact]
    public void Empty_list_round_trips_through_json()
    {
        var list = new System.Collections.Generic.List<StashItemRecord>();
        string json = JsonSerializer.Serialize(list);
        var restored = JsonSerializer.Deserialize<System.Collections.Generic.List<StashItemRecord>>(json)!;
        Assert.Empty(restored);
    }

    [Fact]
    public void Quality_zero_is_valid()
    {
        var r = new StashItemRecord("(O)1", 1, 0);
        Assert.Equal(0, r.Quality);
    }
}
```

Add to `tests/TheLongestYear.Tests/MetaStateTests.cs` (append before the closing `}`):

```csharp
    [Fact]
    public void StashItems_defaults_empty_and_round_trips_through_json()
    {
        var s = new MetaState();
        Assert.Empty(s.StashItems);

        var original = new MetaState
        {
            StashItems =
            {
                new StashItemRecord("(O)24", 2, 0),
                new StashItemRecord("(O)698", 1, 4)
            }
        };
        string json = JsonSerializer.Serialize(original);
        MetaState restored = JsonSerializer.Deserialize<MetaState>(json)!;

        Assert.Equal(2, restored.StashItems.Count);
        Assert.Equal("(O)24", restored.StashItems[0].ItemId);
        Assert.Equal(2, restored.StashItems[0].Quantity);
        Assert.Equal(0, restored.StashItems[0].Quality);
        Assert.Equal("(O)698", restored.StashItems[1].ItemId);
        Assert.Equal(1, restored.StashItems[1].Quantity);
        Assert.Equal(4, restored.StashItems[1].Quality);
    }

    [Fact]
    public void StashSlotCount_returns_zero_when_no_stash_upgrade_owned()
    {
        var s = new MetaState();
        Assert.Equal(0, s.StashSlotCount);
    }

    [Fact]
    public void StashSlotCount_returns_4_when_stash_1_owned()
    {
        var s = new MetaState { OwnedUpgrades = { "stash_1" } };
        Assert.Equal(4, s.StashSlotCount);
    }

    [Fact]
    public void StashSlotCount_returns_8_when_stash_2_owned()
    {
        var s = new MetaState { OwnedUpgrades = { "stash_1", "stash_2" } };
        Assert.Equal(8, s.StashSlotCount);
    }
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: compile errors or test failures referencing `StashItemRecord` and `StashItems` not found.

- [ ] **Step 3: Create StashItemRecord.cs**

Create `src/TheLongestYear.Core/StashItemRecord.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>
/// Serializable snapshot of a single item in the Junimo Stash. Round-trips through
/// System.Text.Json via <see cref="MetaState.StashItems"/>. Only stores what is
/// needed to recreate the item via ItemRegistry.Create on restore.
/// </summary>
public sealed record StashItemRecord(string ItemId, int Quantity, int Quality);
```

- [ ] **Step 4: Add StashItems + StashSlotCount to MetaState.cs**

Open `src/TheLongestYear.Core/MetaState.cs`. Add after the `DismissedIndicators` property (line ~76):

```csharp
    /// <summary>
    /// Items banked in the Junimo Stash across runs. Serialized as part of MetaState
    /// on the game's Saving event (never eagerly). Restored into the world chest on
    /// every reset by <c>JunimoStashService.PopulateFromMeta</c>.
    /// </summary>
    public List<StashItemRecord> StashItems { get; set; } = new();

    /// <summary>
    /// Current slot capacity of the Junimo Stash, derived from the highest owned
    /// stash upgrade tier: 0 = not unlocked, 4 = stash_1, 8 = stash_2.
    /// </summary>
    public int StashSlotCount => HighestKeptTier("stash_", 2) switch
    {
        1 => 4,
        2 => 8,
        _ => 0
    };
```

- [ ] **Step 5: Run tests — expect PASS**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 327` (320 + 7 new tests).

- [ ] **Step 6: Commit**

```bash
git add src/TheLongestYear.Core/StashItemRecord.cs \
        src/TheLongestYear.Core/MetaState.cs \
        tests/TheLongestYear.Tests/StashItemRecordTests.cs \
        tests/TheLongestYear.Tests/MetaStateTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add StashItemRecord + MetaState.StashItems + StashSlotCount

Pure Core: StashItemRecord POCO round-trips JSON; MetaState.StashItems
stores banked items; StashSlotCount reads HighestKeptTier("stash_", 2)
to return 0/4/8 based on owned upgrades. 7 new tests, 327 total.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: GameplayConfig stash tile coords (Core, TDD)

**Files:**
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs`
- Modify: `tests/TheLongestYear.Tests/MetaStateTests.cs` (or a new GameplayConfigTests.cs)

- [ ] **Step 1: Write the failing test**

Append to `tests/TheLongestYear.Tests/MetaStateTests.cs` (before closing `}`):

```csharp
    [Fact]
    public void GameplayConfig_stash_tile_defaults_to_zero_zero()
    {
        var c = new GameplayConfig();
        Assert.Equal(0, c.StashTileX);
        Assert.Equal(0, c.StashTileY);
    }
```

- [ ] **Step 2: Run tests — expect FAIL**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: compile error — `GameplayConfig` has no `StashTileX` / `StashTileY`.

- [ ] **Step 3: Add StashTileX / StashTileY to GameplayConfig.cs**

Open `src/TheLongestYear.Core/GameplayConfig.cs`. Add after the `CraftbookTileY` property (near line 187):

```csharp
    /// <summary>X tile coordinate on the Farm where the Junimo Stash chest is placed.
    /// Default (0,0) disables placement — set via tly_setstash in-game.</summary>
    public int StashTileX { get; set; } = 0;

    /// <summary>Y tile coordinate on the Farm where the Junimo Stash chest is placed.</summary>
    public int StashTileY { get; set; } = 0;
```

- [ ] **Step 4: Run tests — expect PASS**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 328`

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/GameplayConfig.cs \
        tests/TheLongestYear.Tests/MetaStateTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add GameplayConfig.StashTileX/Y for Junimo Stash placement

Default (0,0) disables placement, same pattern as CookbookTileX/Y.
1 new test, 328 total.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: JunimoStashService — chest placement + MetaState bank/restore

**Files:**
- Create: `src/TheLongestYear/Loop/JunimoStashService.cs`

This is mod-side (game references OK). No unit tests — verified by playtest in Task 10.

- [ ] **Step 1: Confirm build is green before adding the new file**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: `Build succeeded.`

- [ ] **Step 2: Create JunimoStashService.cs**

Create `src/TheLongestYear/Loop/JunimoStashService.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;
using TheLongestYear.UI;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Manages the Junimo Stash chest lifecycle across runs.
    ///
    /// The Farm is wiped on every reset (loadForNewGame), so the chest cannot rely
    /// on normal world persistence. Instead:
    ///   - <see cref="PlaceChest"/> creates a fresh Chest at the configured tile after each reset.
    ///   - <see cref="PopulateFromMeta"/> fills the newly placed chest from <see cref="MetaState.StashItems"/>.
    ///   - <see cref="BankToMeta"/> reads the chest's current contents and serialises them
    ///     back into <see cref="MetaState.StashItems"/> (called from MetaStore.Save on the game's
    ///     Saving event — never eagerly, to match the anti-save-scum invariant).
    ///
    /// The chest is identified by <c>modData["tly.junimo.stash"] == "1"</c>.
    /// </summary>
    internal sealed class JunimoStashService
    {
        internal const string StashModDataKey = "tly.junimo.stash";

        private readonly IMonitor _monitor;
        private readonly MetaState _meta;
        private readonly GameplayConfig _config;

        public JunimoStashService(IMonitor monitor, MetaState meta, GameplayConfig config)
        {
            _monitor = monitor;
            _meta    = meta;
            _config  = config;
        }

        /// <summary>
        /// Place a fresh stash Chest on the Farm at the configured tile.
        /// No-ops if the tile is (0,0) (disabled) or the stash upgrade has not been purchased.
        /// Idempotent: removes any existing tagged chest at that tile before placing a new one.
        /// </summary>
        public void PlaceChest()
        {
            if (_config.StashTileX == 0 && _config.StashTileY == 0)
                return;

            if (_meta.StashSlotCount == 0)
                return;

            Farm farm = Game1.getFarm();
            Vector2 tile = new Vector2(_config.StashTileX, _config.StashTileY);

            // Remove any stale tagged chest (from a prior reset) to avoid duplication.
            if (farm.objects.ContainsKey(tile)
                && farm.objects[tile] is Chest existing
                && existing.modData.ContainsKey(StashModDataKey))
            {
                farm.objects.Remove(tile);
                _monitor.Log(
                    $"JunimoStashService: removed stale stash chest at ({_config.StashTileX}, {_config.StashTileY}).",
                    LogLevel.Trace);
            }

            // Place a new player chest (playerChest = true enables the vanilla 36-slot layout;
            // we cap via JunimoStashCapPatch, not by changing the chest's internal size).
            var chest = new Chest(playerChest: true, tile);
            chest.modData[StashModDataKey] = "1";
            farm.objects[tile] = chest;

            _monitor.Log(
                $"JunimoStashService: placed stash chest at ({_config.StashTileX}, {_config.StashTileY}), " +
                $"cap={_meta.StashSlotCount} slots.",
                LogLevel.Info);
        }

        /// <summary>
        /// Fill the placed stash chest from <see cref="MetaState.StashItems"/>.
        /// Call after <see cref="PlaceChest"/> on each reset. No-op if no chest is placed.
        /// </summary>
        public void PopulateFromMeta()
        {
            Chest chest = FindStashChest();
            if (chest == null)
                return;

            int restored = 0;
            foreach (StashItemRecord record in _meta.StashItems)
            {
                Item item = ItemRegistry.Create(record.ItemId, record.Quantity, record.Quality,
                    allowNull: true);
                if (item == null)
                {
                    _monitor.Log(
                        $"JunimoStashService: could not recreate item '{record.ItemId}' (unknown id) — skipping.",
                        LogLevel.Warn);
                    continue;
                }
                chest.Items.Add(item);
                restored++;
            }

            _monitor.Log(
                $"JunimoStashService: restored {restored}/{_meta.StashItems.Count} items into stash chest.",
                LogLevel.Trace);
        }

        /// <summary>
        /// Read the stash chest's current contents and write them into
        /// <see cref="MetaState.StashItems"/>. Called by MetaStore.Save on the Saving event.
        /// Overwrites whatever was previously in StashItems — the chest is the authoritative source.
        /// No-op if no stash upgrade is owned or the tile is not configured.
        /// </summary>
        public void BankToMeta()
        {
            Chest chest = FindStashChest();
            if (chest == null)
            {
                // No chest to read from (e.g. player hasn't purchased stash_1 yet, or tile not set).
                return;
            }

            _meta.StashItems.Clear();
            foreach (Item item in chest.Items)
            {
                if (item == null) continue;
                _meta.StashItems.Add(new StashItemRecord(
                    item.QualifiedItemId,
                    item.Stack,
                    (item as StardewValley.Object)?.quality.Value ?? 0));
            }

            _monitor.Log(
                $"JunimoStashService: banked {_meta.StashItems.Count} items into MetaState.StashItems.",
                LogLevel.Trace);
        }

        /// <summary>
        /// Register the stash indicator bubble on the Farm.
        /// Called from WorldResetService.RegisterIndicators after the chest is placed.
        /// </summary>
        public void RegisterIndicator()
        {
            if (_config.StashTileX == 0 && _config.StashTileY == 0)
                return;
            if (_meta.StashSlotCount == 0)
                return;

            Farm farm = Game1.getFarm();
            IndicatorRegistry.Register("tly.stash", farm,
                new Vector2(_config.StashTileX, _config.StashTileY),
                IndicatorKind.Question);
        }

        /// <summary>
        /// Find the stash Chest in the current Farm's object layer. Returns null if not found.
        /// </summary>
        public Chest FindStashChest()
        {
            if (_config.StashTileX == 0 && _config.StashTileY == 0)
                return null;

            Vector2 tile = new Vector2(_config.StashTileX, _config.StashTileY);
            Farm farm = Game1.getFarm();
            if (farm == null)
                return null;

            if (farm.objects.TryGetValue(tile, out StardewValley.Object obj)
                && obj is Chest chest
                && chest.modData.ContainsKey(StashModDataKey))
            {
                return chest;
            }

            return null;
        }
    }
}
```

- [ ] **Step 3: Build to confirm no errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/JunimoStashService.cs
git commit -m "$(cat <<'EOF'
feat(mod): add JunimoStashService for stash chest lifecycle

PlaceChest re-creates the tagged Chest on the Farm after each reset.
PopulateFromMeta restores banked items via ItemRegistry.Create.
BankToMeta serialises chest contents into MetaState.StashItems on Save.
RegisterIndicator hooks the ?-bubble into IndicatorRegistry.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: JunimoStashCapPatch — slot cap enforcement

**Files:**
- Create: `src/TheLongestYear/Loop/JunimoStashCapPatch.cs`

- [ ] **Step 1: Understand the patch target**

From the Android decompile at `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android\decompiled\StardewValley\StardewValley.Objects\Chest.cs` line 888:

```csharp
public virtual Item addItem(Item item)
{
    item.resetState();
    clearNulls();
    createSlotsForCapacity();
    IInventory itemsForPlayer = GetItemsForPlayer();
    for (int i = 0; i < itemsForPlayer.Count; i++)
    {
        if (itemsForPlayer[i] != null && itemsForPlayer[i].canStackWith(item))
        {
            int amount = item.Stack - itemsForPlayer[i].addToStack(item);
            if (item.ConsumeStack(amount) == null)
                return null;  // fully stacked — consumed
        }
        else if (itemsForPlayer[i] == null)
        {
            itemsForPlayer[i] = item;
            return null;  // placed in empty slot
        }
    }
    if (itemsForPlayer.Count < GetActualCapacity())
    {
        itemsForPlayer.Add(item);
        return null;
    }
    return item;  // rejected (chest full)
}
```

The postfix runs after this. `__result` is null when vanilla accepted the item, non-null when vanilla rejected it. We intercept the null case to enforce our lower cap.

- [ ] **Step 2: Create JunimoStashCapPatch.cs**

Create `src/TheLongestYear/Loop/JunimoStashCapPatch.cs`:

```csharp
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TheLongestYear.Core;

namespace TheLongestYear.Loop
{
    /// <summary>
    /// Enforces the Junimo Stash slot cap on <see cref="Chest.addItem"/>.
    ///
    /// The patch is a postfix: vanilla runs first and handles stacking (stacking onto an
    /// existing stack is always allowed because it doesn't consume a new slot). After vanilla
    /// returns null (item accepted), we check whether the stash's non-null item count now
    /// exceeds the cap. If it does, we remove the just-added item, restore it to the return
    /// value, and show a HUD message.
    ///
    /// Gate: only fires when <c>chest.modData["tly.junimo.stash"] == "1"</c>.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.addItem))]
    internal static class JunimoStashCapPatch
    {
        private static IMonitor _monitor;
        private static MetaState _meta;

        public static void Connect(IMonitor monitor, MetaState meta)
        {
            _monitor = monitor;
            _meta    = meta;
        }

        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance, Item item, ref Item __result)
        {
            // Only act on the stash chest, and only when vanilla accepted the item (result null).
            if (__result != null)
                return;
            if (!__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                return;
            if (_meta == null)
                return;

            int cap = _meta.StashSlotCount;
            if (cap == 0)
                return;

            // Count non-null items in the player's inventory for this chest.
            // GetItemsForPlayer() returns the right IInventory (single-player = Items).
            var inv = __instance.GetItemsForPlayer();
            int nonNullCount = 0;
            for (int i = 0; i < inv.Count; i++)
            {
                if (inv[i] != null) nonNullCount++;
            }

            if (nonNullCount <= cap)
                return;  // within cap — accept as-is

            // Slot cap exceeded. Undo vanilla's acceptance: remove the item from the inventory.
            // The item landed in one of: a null slot (set by reference), or was added via .Add().
            // We find it by scanning for the item reference itself.
            for (int i = inv.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(inv[i], item))
                {
                    inv[i] = null;
                    break;
                }
            }

            // Return the item (rejected).
            __result = item;

            // HUD message.
            Game1.showRedMessage(
                $"Junimo Stash is full! ({cap} slot{(cap == 1 ? "" : "s")} maximum)");

            _monitor?.Log(
                $"JunimoStashCapPatch: rejected item '{item.QualifiedItemId}' — stash at cap ({cap}).",
                LogLevel.Trace);
        }
    }
}
```

- [ ] **Step 3: Build to confirm no errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/JunimoStashCapPatch.cs
git commit -m "$(cat <<'EOF'
feat(mod): add JunimoStashCapPatch — enforce slot cap on stash chest

Harmony postfix on Chest.addItem. Gates on modData["tly.junimo.stash"].
Undo vanilla acceptance when non-null count exceeds MetaState.StashSlotCount.
Plays red HUD message on rejection. Stacking onto existing stacks bypasses
cap (vanilla consumed the stack before our postfix runs).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: Wire JunimoStashService into WorldResetService + MetaStore

**Files:**
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs`
- Modify: `src/TheLongestYear/MetaStore.cs`

- [ ] **Step 1: Add JunimoStashService field + constructor parameter to WorldResetService**

Open `src/TheLongestYear/Loop/WorldResetService.cs`.

Add to fields (after `private readonly FarmerReset _farmerReset;`):

```csharp
        private readonly JunimoStashService _stashService;
```

Update the constructor signature (after `ProfessionPickerScheduler professionPicker`):

```csharp
        public WorldResetService(
            IMonitor monitor,
            TheLongestYear.Core.MetaState meta,
            TheLongestYear.Core.RunState run,
            TheLongestYear.Core.GameplayConfig config,
            CommunityCenterUnlock ccUnlock,
            string modDirectory,
            FarmerReset farmerReset,
            ProfessionPickerScheduler professionPicker,
            JunimoStashService stashService)
        {
            _monitor = monitor;
            _meta = meta;
            _run = run;
            _config = config;
            _ccUnlock = ccUnlock;
            _modDirectory = modDirectory;
            _farmerReset = farmerReset;
            _professionPicker = professionPicker;
            _stashService = stashService;
        }
```

In `PerformReset()`, after step 13 (`RegisterIndicators()`) and before step 14 (`resetForPlayerEntry`), insert step 13b:

```csharp
            // 13b. Place the Junimo Stash chest on the Farm and populate from MetaState.
            //      Must run after RegisterIndicators (so the farm location is fully resolved)
            //      and before the player is warped home.
            _stashService?.PlaceChest();
            _stashService?.PopulateFromMeta();
```

In `RegisterIndicators()`, at the end (before the closing `}`), add:

```csharp
            // Stash chest indicator — registered here so it re-appears correctly on each reset.
            _stashService?.RegisterIndicator();
```

- [ ] **Step 2: Wire BankToMeta into MetaStore.Save**

Open `src/TheLongestYear/MetaStore.cs`.

Add a nullable field:

```csharp
        private JunimoStashService _stashService;
```

Add a wiring method:

```csharp
        /// <summary>Connect the stash service so BankToMeta fires before each save.</summary>
        public void AttachStashService(JunimoStashService service)
            => _stashService = service;
```

Update `Save()`:

```csharp
        /// <summary>Commit banked progress and run-state into the save. Call from the game's Saving event.</summary>
        public void Save()
        {
            // Capture chest contents into MetaState before serialising.
            _stashService?.BankToMeta();
            _data.WriteSaveData(MetaDataKey, State);
            _data.WriteSaveData(RunDataKey, Run);
        }
```

- [ ] **Step 3: Build to confirm no errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: **compile error** — ModEntry still constructs WorldResetService without the new parameter. That's expected; we fix it in Task 6.

- [ ] **Step 4: Note the expected error — do NOT commit yet**

The error will be something like:
`error CS7036: There is no argument given that corresponds to the required parameter 'stashService'`

This is intentional — ModEntry wiring comes next in Task 6.

---

## Task 6: Wire everything into ModEntry

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs`

- [ ] **Step 1: Construct JunimoStashService and wire it into ModEntry**

Open `src/TheLongestYear/ModEntry.cs`.

Add field (after `private PeakMineFloorTracker _peakMineFloorTracker;`):

```csharp
        private JunimoStashService _stashService;
```

In `Entry()`, after `CraftbookInteractable.ConnectTo(...)`:

```csharp
            // JunimoStashCapPatch needs the live MetaState reference and monitor.
            // MetaState isn't fully loaded until OnSaveLoaded, but the patch only
            // fires in-game; JunimoStashCapPatch.Connect is safe to call here because
            // it only sets module-level references used lazily.
            // The _stashService is constructed in OnSaveLoaded where config + meta are live.
```

Add console commands in `Entry()` after the `tly_activeeffects` command:

```csharp
            helper.ConsoleCommands.Add("tly_setstash",
                "Anchor the Junimo Stash chest to the tile you are facing on the Farm. Writes config.json.",
                this.CmdSetStash);
            helper.ConsoleCommands.Add("tly_openstash",
                "Open the Junimo Stash chest directly (debug).",
                this.CmdOpenStash);
            helper.ConsoleCommands.Add("tly_stashclear",
                "Clear all items from the Junimo Stash MetaState (debug — DESTRUCTIVE).",
                this.CmdStashClear);
```

In `OnSaveLoaded`, after `_reset = new WorldResetService(...)` add the stash service construction:

```csharp
            _stashService = new JunimoStashService(this.Monitor, _meta.State, _config);
            _meta.AttachStashService(_stashService);
            JunimoStashCapPatch.Connect(this.Monitor, _meta.State);
```

Update the `WorldResetService` constructor call to include `_stashService` as the final argument:

```csharp
            _reset = new WorldResetService(
                this.Monitor, _meta.State, _meta.Run, _config, _ccUnlock,
                this.Helper.DirectoryPath, farmerReset, professionPicker,
                _stashService);
```

Also add a stash chest placement call in `OnSaveLoaded` at the very end of the method (after `_reset.RegisterIndicators()`), so items are restored when a save is loaded mid-run (not just after a reset):

```csharp
            // Restore stash chest on every save load (not just after reset), so a
            // save-and-reload mid-run re-places the chest correctly.
            _stashService.PlaceChest();
            _stashService.PopulateFromMeta();
```

Add the three command handlers (after `CmdOpenCraftbook`):

```csharp
        private void CmdSetStash(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.currentLocation is not StardewValley.Locations.Farm)
            {
                this.Monitor.Log("tly_setstash: stand on the Farm first.", LogLevel.Warn);
                return;
            }
            int dx = Game1.player.FacingDirection == 1 ? 1 : Game1.player.FacingDirection == 3 ? -1 : 0;
            int dy = Game1.player.FacingDirection == 2 ? 1 : Game1.player.FacingDirection == 0 ? -1 : 0;
            _config.StashTileX = (int)Game1.player.Tile.X + dx;
            _config.StashTileY = (int)Game1.player.Tile.Y + dy;
            this.Helper.WriteConfig(_config);
            this.Monitor.Log(
                $"Junimo Stash anchored to ({_config.StashTileX}, {_config.StashTileY}). Saved to config.json.",
                LogLevel.Info);
            // Immediately re-place the chest at the new tile.
            _stashService?.PlaceChest();
            _stashService?.PopulateFromMeta();
        }

        private void CmdOpenStash(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            var chest = _stashService?.FindStashChest();
            if (chest == null)
            {
                this.Monitor.Log("No stash chest found. Own stash_1 and run tly_setstash first.", LogLevel.Warn);
                return;
            }
            chest.ShowMenu();
        }

        private void CmdStashClear(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _meta.State.StashItems.Clear();
            var chest = _stashService?.FindStashChest();
            if (chest != null)
                chest.Items.Clear();
            this.Monitor.Log("Junimo Stash MetaState cleared (in memory — persists on next save).", LogLevel.Warn);
        }
```

Also add to `ExecuteDebugLine` switch (after `tly_activeeffects` case):

```csharp
                case "tly_setstash":  this.CmdSetStash(command, args); break;
                case "tly_openstash": this.CmdOpenStash(command, args); break;
                case "tly_stashclear": this.CmdStashClear(command, args); break;
```

- [ ] **Step 2: Build to confirm no errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: `Build succeeded.`

- [ ] **Step 3: Confirm tests still pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: `Passed! - Failed: 0, Passed: 328`

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/WorldResetService.cs \
        src/TheLongestYear/MetaStore.cs \
        src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
feat(mod): wire JunimoStashService into WorldResetService, MetaStore, ModEntry

PerformReset now calls PlaceChest + PopulateFromMeta after RegisterIndicators.
MetaStore.Save calls BankToMeta before writing (anti-save-scum invariant).
OnSaveLoaded constructs JunimoStashService, wires cap patch, and re-places
the chest on mid-run save loads. tly_setstash / tly_openstash / tly_stashclear
debug commands added.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: tly_meta output — include stash summary

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs`

The existing `tly_meta` command prints `StashTier` but not slot count or item count. Update it.

- [ ] **Step 1: Update PrintMeta in ModEntry.cs**

Find the `PrintMeta` method. Replace the `Monitor.Log` call:

```csharp
        private void PrintMeta(string command, string[] args)
        {
            if (!Context.IsWorldReady)
            {
                this.Monitor.Log("Load a save first.", LogLevel.Warn);
                return;
            }

            MetaState s = _meta.State;
            int stashSlots = s.StashSlotCount;
            int stashItems = s.StashItems.Count;
            string stashTile = (_config.StashTileX == 0 && _config.StashTileY == 0)
                ? "not configured"
                : $"({_config.StashTileX}, {_config.StashTileY})";

            this.Monitor.Log(
                $"JP={s.JunimoPoints}, " +
                $"StashTier={s.HighestKeptTier("stash_", 2)} ({stashSlots} slots, {stashItems} items banked, tile {stashTile}), " +
                $"Upgrades=[{string.Join(", ", s.OwnedUpgrades)}]",
                LogLevel.Info);
        }
```

- [ ] **Step 2: Build + test**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false && dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: `Build succeeded.` and `Passed! - Failed: 0, Passed: 328`

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
feat(mod): tly_meta includes stash slot count, item count, and tile

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Quest intro for stash (first run after purchase)

**Files:**
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs`

Same pattern as `FireBookQuestIntros`: add a quest intro the first time the stash chest appears (indicator not yet dismissed).

- [ ] **Step 1: Extend FireBookQuestIntros in WorldResetService.cs**

Find `FireBookQuestIntros()`. After the `craftbook_1` block, add:

```csharp
            if (_meta.HasUpgrade("stash_1")
                && (_config.StashTileX != 0 || _config.StashTileY != 0)
                && !_meta.DismissedIndicators.Contains("tly.stash"))
            {
                AddIntroQuest(
                    id: "tly.-9003",
                    title: "A gift from the Junimos",
                    description: "The Junimos placed a special chest on your farm — it will survive the seasons. " +
                                 "Find it and use it wisely; it has very limited space.");
            }
```

- [ ] **Step 2: Build + test**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false && dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: `Build succeeded.` and `Passed! - Failed: 0, Passed: 328`

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/WorldResetService.cs
git commit -m "$(cat <<'EOF'
feat(mod): add quest intro for Junimo Stash first run after purchase

Fires "A gift from the Junimos" quest (tly.-9003) on the first reset
after stash_1 is purchased and a tile is configured. Dismissed after
the player first opens the stash chest (same DismissedIndicators guard
as Cookbook/Craftbook).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Dismiss indicator on first stash open

**Files:**
- Modify: `src/TheLongestYear/Loop/JunimoStashService.cs`
- Modify: `src/TheLongestYear/ModEntry.cs` (CmdOpenStash)

The `?` bubble over the stash chest should disappear the first time the player opens it. Vanilla chest interaction goes through `Chest.ShowMenu()` (called when the player presses the action button on the chest object). We intercept this via a patch on `Chest.ShowMenu`.

However, patching `Chest.ShowMenu` broadly would affect all chests. A cleaner approach: wrap the call site. `CmdOpenStash` already calls `chest.ShowMenu()` directly. For the world interaction path (player walks up and presses action), the chest handles it via `Chest.checkForAction`. We can add a simple `Chest.ShowMenu` postfix gated on `modData`.

- [ ] **Step 1: Add ShowMenu postfix to JunimoStashCapPatch.cs**

Open `src/TheLongestYear/Loop/JunimoStashCapPatch.cs`. Add a second patch class in the same file (after the closing `}` of `JunimoStashCapPatch`):

```csharp
    /// <summary>
    /// Dismisses the "tly.stash" indicator the first time the player opens the stash chest.
    /// </summary>
    [HarmonyPatch(typeof(Chest), nameof(Chest.ShowMenu))]
    internal static class JunimoStashShowMenuPatch
    {
        // ReSharper disable once InconsistentNaming — Harmony convention.
        private static void Postfix(Chest __instance)
        {
            if (__instance.modData.ContainsKey(JunimoStashService.StashModDataKey))
                IndicatorRegistry.Dismiss("tly.stash");
        }
    }
```

Add `using TheLongestYear.UI;` to the top of `JunimoStashCapPatch.cs` (after `using TheLongestYear.Core;`).

- [ ] **Step 2: Build + test**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false && dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: `Build succeeded.` and `Passed! - Failed: 0, Passed: 328`

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Loop/JunimoStashCapPatch.cs
git commit -m "$(cat <<'EOF'
feat(mod): dismiss stash indicator on first open via ShowMenu postfix

JunimoStashShowMenuPatch: Harmony postfix on Chest.ShowMenu gated by
modData["tly.junimo.stash"], calls IndicatorRegistry.Dismiss("tly.stash").
? bubble disappears permanently after the first open.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Full-stack build + playtest verification

**Files:** none (verification only)

- [ ] **Step 1: Full build + all tests**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: `Build succeeded.` and `Passed! - Failed: 0, Passed: 328`

- [ ] **Step 2: Deploy and launch via sync.ps1**

```powershell
# From C:\Users\Jeff\Documents\Projects\Stardee Valoo\SyncdewValley
.\sync.ps1 deploy
.\sync.ps1 launch
```

Expected: game launches, SMAPI log shows no errors from TLY.

- [ ] **Step 3: Playtest verification sequence**

Perform these steps in-game (hand to user once deployed):

**A. Stash not owned:**
- `tly_openstash` → should log "No stash chest found."
- No chest visible anywhere on the farm.

**B. Buy stash_1 (4 slots), configure tile:**
- `tly_buyupgrade stash_1`
- Stand on the farm, face an empty tile.
- `tly_setstash` → logs "Junimo Stash anchored to (X, Y). Saved to config.json."
- A chest should appear at the faced tile immediately.
- A `?` bubble should appear above it.
- Quest log should show "A gift from the Junimos — ...special chest..."

**C. Open the stash:**
- Walk up to the chest and press action (or `tly_openstash`).
- The vanilla `ItemGrabMenu` opens (standard chest UI).
- `?` bubble disappears.
- Put 4 items in. A 5th item should be rejected with the red "Junimo Stash is full! (4 slots maximum)" message.
- `tly_meta` → shows `StashTier=1 (4 slots, 4 items banked, tile (X,Y))`

**D. Save + reload:**
- Save the game. Reload.
- The chest is at the same tile with the same 4 items. Open it to confirm.
- `tly_meta` → still shows 4 items banked.

**E. Reset:**
- `tly_reset`
- After reset: chest is re-placed at the configured tile with the same items.
- No duplicate chest at the old tile.
- `?` bubble does NOT re-appear (already dismissed).

**F. Upgrade to stash_2 (8 slots):**
- `tly_buyupgrade stash_2`
- Open the chest — now 8 slots. Can add 4 more items.
- A 9th item should be rejected with "Junimo Stash is full! (8 slots maximum)".

**G. stash_clear debug:**
- `tly_stashclear`
- Open chest — empty.
- `tly_meta` → 0 items banked.

- [ ] **Step 4: Pull SMAPI log and confirm clean**

```powershell
.\sync.ps1 logs
```

Inspect `AndroidConsolizer/test-output/SMAPI-latest.txt`. Confirm:
- No `[ERROR]` from TLY.
- `JunimoStashService: placed stash chest` line visible in Trace output.
- `JunimoStashService: banked N items` line on save.

- [ ] **Step 5: Final commit**

```bash
git add .
git commit -m "$(cat <<'EOF'
chore: Plan 07 playtest verification complete

Stash chest: placement, slot cap, MetaState banking, reset restore,
upgrade tier expansion, and indicator dismissal all verified in-game.
328 tests passing.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review

### 1. Spec coverage

| Requirement | Covered by |
|---|---|
| One specific Chest world-object | Task 3: `JunimoStashService.PlaceChest` places a real `Chest` in `Farm.objects` |
| Limited slots (4/8 from stash_1/stash_2 catalog entries) | Task 1: `MetaState.StashSlotCount`; Task 4: `JunimoStashCapPatch` enforces cap |
| Anytime interaction | Vanilla `Chest` handles player interaction via `checkForAction` → `ShowMenu` |
| Items survive reset — banked into MetaState on close/save | Task 3: `BankToMeta` called from `MetaStore.Save`; Task 5 wires it |
| Items restored on next run start | Task 3: `PopulateFromMeta`; Task 5 wires into `PerformReset` |
| Configurable tile with `tly_setstash` debug command | Task 2 (config fields); Task 6 (command) |
| Indicator bubble (`tly.stash`) | Task 3: `RegisterIndicator`; Task 5 wires into `RegisterIndicators` |
| Dismiss indicator on first open | Task 9: `JunimoStashShowMenuPatch` |
| Quest intro first run after purchase | Task 8 |
| `StashItemRecord` JSON round-trip | Task 1 |
| `MetaState.StashItems` + `StashSlotCount` | Task 1 |
| `tly_meta` output includes stash summary | Task 7 |
| `tly_openstash` / `tly_stashclear` debug commands | Task 6 |

All spec requirements have a task. No gaps.

### 2. Placeholder scan

No "TBD", "TODO", "implement later", "handle edge cases" found. All code steps show complete implementations.

### 3. Type consistency

- `StashItemRecord(string ItemId, int Quantity, int Quality)` — used consistently in Tasks 1, 3.
- `MetaState.StashItems` is `List<StashItemRecord>` — consistent across Tasks 1, 3, 5.
- `MetaState.StashSlotCount` (computed property) — used in Tasks 1, 4.
- `JunimoStashService.StashModDataKey` (`const string`) — used in Tasks 3, 4, 9.
- `JunimoStashCapPatch.Connect(IMonitor, MetaState)` — called in Task 6.
- `WorldResetService` constructor — updated in Task 5, called in Task 6. Argument order matches.
- `MetaStore.AttachStashService(JunimoStashService)` — defined in Task 5, called in Task 6.

All types consistent.

---

## Execution options

Plan complete and saved to `docs/superpowers/plans/2026-05-27-v1-plan-07-junimo-stash.md`. Two execution options:

**1. Subagent-Driven (recommended)** — fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** — execute tasks in this session using executing-plans, batch execution with checkpoints.

Which approach?
