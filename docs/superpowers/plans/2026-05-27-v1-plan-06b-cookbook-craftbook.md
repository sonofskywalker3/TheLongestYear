# The Longest Year — v1 Plan 06B: Cookbook + Craftbook

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the Cookbook and Craftbook world objects to the FarmHouse: slot-based banking menus for cooking and crafting recipes, a reusable `IndicatorRegistry` for ?/! bubbles over world tiles (Cookbook, Craftbook, and the existing fireplace board), quest intro notifications, and recipe re-grant on every reset.

**Architecture:** Three-layer split.
1. **Pure Core** (`TheLongestYear.Core`) — three new `MetaState` fields (`CookbookRecipes`, `CraftbookRecipes`, `DismissedIndicators`) and six new `UpgradeCatalog` entries (Cookbook I/II/III + Craftbook I/II/III in the `Carryover` category). All TDD'd.
2. **Mod-side reset wiring** (`TheLongestYear/Loop`) — `FarmerReset.Apply` re-grants banked recipes to `Farmer.cookingRecipes` / `Farmer.craftingRecipes` after each wipe. `WorldResetService.PerformReset` fires quest intros on the first run after purchase. Both layers extend existing classes — no new Loop files.
3. **Mod-side UI + world** (`TheLongestYear/UI` + `TheLongestYear/Loop`) — `IndicatorRegistry` (static helper) renders ?/! bubbles via `Display.RenderedWorld`; `CookbookInteractable` and `CraftbookInteractable` (Harmony patches on `FarmHouse.checkAction`) open slot-management menus; `CookbookMenu` and `CraftbookMenu` (`IClickableMenu` subclasses) handle slot display and recipe picker. World-side code is verified by manual playtest.

**Tech Stack:** C# / .NET 6, SMAPI 4.x, Harmony 2.x, MonoGame, xUnit. Stardew 1.6 PC (verified against Android decompile at `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android` where noted). Pure Core (no game refs) is TDD'd; mod-side is playtest-verified.

**Repo conventions:** Working dir `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`. Build:
`dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false`
Test:
`dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Files < 400 lines. Local commits only — **never push without explicit approval**. End every commit body with:
`Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`

**Persistence rule (do NOT regress):** all MetaState mutations stay in memory; existing `Saving` hook in `MetaStore.Save` writes to disk. No eager writes.

**Depends on:** Plan 06A (`feat/v1-plan-06a-persistence-effects`). Plan 06B branches from 06A and assumes 06A is fully merged.

**Out of scope:** Theme bonuses/liabilities (separate plan), Junimo Stash item carryover (Plan 07), LY2/LY3.

---

## File layout after Plan 06B

```
src/TheLongestYear.Core/                              (pure, unit-tested)
  MetaState.cs                  MODIFY: + CookbookRecipes, CraftbookRecipes, DismissedIndicators
  UpgradeCatalog.cs             MODIFY: + 6 cookbook/craftbook entries in Carryover section

src/TheLongestYear/                                    (SMAPI mod; game-integration)
  Loop/
    FarmerReset.cs              MODIFY: re-grant banked recipes after skill/tool wipe
    WorldResetService.cs        MODIFY: fire quest intros on first run after purchase;
                                        register indicators on reset
  UI/
    IndicatorRegistry.cs        NEW:    reusable ?/! bubble system, RenderedWorld render
    CookbookMenu.cs             NEW:    IClickableMenu slot grid + recipe picker for cooking
    CraftbookMenu.cs            NEW:    IClickableMenu slot grid + recipe picker for crafting
    CookbookInteractable.cs     NEW:    Harmony patch on FarmHouse.checkAction → opens CookbookMenu
    CraftbookInteractable.cs    NEW:    Harmony patch on FarmHouse.checkAction → opens CraftbookMenu
    MenuLauncher.cs             MODIFY: expose OpenCookbook / OpenCraftbook debug entry points
  ModEntry.cs                   MODIFY: wire IndicatorRegistry to RenderedWorld; wire
                                        debug commands tly_opencookbook / tly_opencraftbook

tests/TheLongestYear.Tests/
  MetaStateTests.cs             MODIFY: + CookbookRecipes/CraftbookRecipes/DismissedIndicators
                                          fields round-trip + defaults
  UpgradeCatalogTests.cs        MODIFY: + 6 new cookbook/craftbook entries present + category
```

---

## Task 0: Branch + baseline verification

**Files:** none (git only)

- [ ] **Step 1: Create and switch to the feature branch from `feat/v1-plan-06a-persistence-effects`**

Run from `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`:

```bash
git checkout -b feat/v1-plan-06b-cookbook-craftbook
git branch --show-current
```

Expected: `feat/v1-plan-06b-cookbook-craftbook`.

- [ ] **Step 2: Confirm baseline green before touching anything**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS (280 tests — all of Plan 06A's tests plus the earlier plans).

- [ ] **Step 3: Confirm the mod builds clean**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: build SUCCESS, no errors, no warnings.

---

## Task 1: `MetaState` — Cookbook/Craftbook/Indicators fields (Core)

**Files:**
- Modify: `src/TheLongestYear.Core/MetaState.cs`
- Modify: `tests/TheLongestYear.Tests/MetaStateTests.cs`

Three new persistent fields. `CookbookRecipes` and `CraftbookRecipes` hold the banked recipe IDs (strings matching keys in `Farmer.cookingRecipes` / `Farmer.craftingRecipes`). `DismissedIndicators` holds string IDs of bubbles the player has already dismissed (so they don't reappear after reset).

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/MetaStateTests.cs`:

```csharp
[Fact]
public void New_meta_state_has_empty_cookbook_craftbook_and_dismissed_indicators()
{
    var s = new MetaState();
    Assert.Empty(s.CookbookRecipes);
    Assert.Empty(s.CraftbookRecipes);
    Assert.Empty(s.DismissedIndicators);
}

[Fact]
public void Cookbook_craftbook_dismissed_indicators_round_trip_through_json()
{
    var original = new MetaState
    {
        CookbookRecipes      = { "Fried_Egg", "Bread", "Salad" },
        CraftbookRecipes     = { "Wood_Fence", "Chest" },
        DismissedIndicators  = { "tly.cookbook", "tly.fireplace" }
    };
    string json = System.Text.Json.JsonSerializer.Serialize(original);
    MetaState restored = System.Text.Json.JsonSerializer.Deserialize<MetaState>(json)!;

    Assert.Equal(new[] { "Fried_Egg", "Bread", "Salad" }, restored.CookbookRecipes);
    Assert.Equal(new[] { "Wood_Fence", "Chest" }, restored.CraftbookRecipes);
    Assert.Equal(new HashSet<string> { "tly.cookbook", "tly.fireplace" }, restored.DismissedIndicators);
}

[Fact]
public void DismissedIndicators_is_a_hashset_and_deduplicates()
{
    var s = new MetaState();
    s.DismissedIndicators.Add("tly.cookbook");
    s.DismissedIndicators.Add("tly.cookbook");   // duplicate
    Assert.Single(s.DismissedIndicators);
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~MetaStateTests.New_meta_state_has_empty_cookbook|FullyQualifiedName~MetaStateTests.Cookbook_craftbook_dismissed|FullyQualifiedName~MetaStateTests.DismissedIndicators_is_a_hashset"
```

Expected: FAIL (compiler errors: `CookbookRecipes`, `CraftbookRecipes`, `DismissedIndicators` don't exist).

- [ ] **Step 3: Add the three fields**

Edit `src/TheLongestYear.Core/MetaState.cs`. Add after `MailFlagsEverReceived`:

```csharp
/// <summary>
/// Cooking recipe IDs banked in the Cookbook across runs. Keys match
/// <c>Farmer.cookingRecipes</c> dictionary keys (vanilla recipe id strings, e.g. "Fried_Egg").
/// On reset, every entry is re-granted to <c>Farmer.cookingRecipes[id] = 0</c>
/// (the vanilla "learned but never cooked" marker). Slot count is controlled by
/// which Cookbook I/II/III upgrades the player owns.
/// </summary>
public List<string> CookbookRecipes { get; set; } = new();

/// <summary>
/// Crafting recipe IDs banked in the Craftbook across runs. Keys match
/// <c>Farmer.craftingRecipes</c> dictionary keys (vanilla recipe id strings, e.g. "Wood Fence").
/// On reset, every entry is re-granted to <c>Farmer.craftingRecipes[id] = 0</c>.
/// </summary>
public List<string> CraftbookRecipes { get; set; } = new();

/// <summary>
/// String IDs of indicator bubbles the player has already dismissed. Prevents the ?/!
/// bubble from re-appearing after a reset. Values are "tly.cookbook", "tly.craftbook",
/// and "tly.fireplace". Using <see cref="HashSet{T}"/> so duplicate dismissals are
/// idempotent; the JSON serializer preserves this as a unique array.
/// </summary>
public HashSet<string> DismissedIndicators { get; set; } = new();
```

Note: `HashSet<string>` serialises to a JSON array by `System.Text.Json` and deserialises back correctly in .NET 6 — no custom converter needed.

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~MetaStateTests"
```

Expected: PASS (all MetaStateTests).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/MetaState.cs tests/TheLongestYear.Tests/MetaStateTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add CookbookRecipes, CraftbookRecipes, DismissedIndicators to MetaState

Phase B persistence design §C + §D. CookbookRecipes and CraftbookRecipes hold
banked recipe ids re-granted on each reset. DismissedIndicators (HashSet<string>)
backs the IndicatorRegistry so dismissed bubbles don't reappear across runs.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 2: Cookbook + Craftbook catalog entries (Core)

**Files:**
- Modify: `src/TheLongestYear.Core/UpgradeCatalog.cs`
- Modify: `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`

Six new hand-authored entries in the `Carryover` category (recipe carryover is the right mental bucket — these retain cross-run knowledge, same as the skill-level keeps). Slot counts and costs per spec §C: I=5 slots/150 JP, II=10 slots/350 JP, III=20 slots/700 JP (cumulative — owning all three gives 20 slots total, not 35; the highest tier determines the pool size).

- [ ] **Step 1: Write the failing tests**

Add to `tests/TheLongestYear.Tests/UpgradeCatalogTests.cs`:

```csharp
[Theory]
[InlineData("cookbook_1", "Cookbook I",   UpgradeCategory.Carryover, 150, null)]
[InlineData("cookbook_2", "Cookbook II",  UpgradeCategory.Carryover, 350, "cookbook_1")]
[InlineData("cookbook_3", "Cookbook III", UpgradeCategory.Carryover, 700, "cookbook_2")]
[InlineData("craftbook_1", "Craftbook I",   UpgradeCategory.Carryover, 150, null)]
[InlineData("craftbook_2", "Craftbook II",  UpgradeCategory.Carryover, 350, "craftbook_1")]
[InlineData("craftbook_3", "Craftbook III", UpgradeCategory.Carryover, 700, "craftbook_2")]
public void Cookbook_craftbook_entries_have_correct_id_name_category_cost_prereq(
    string id, string name, UpgradeCategory category, long cost, string? prereqId)
{
    var def = UpgradeCatalog.TryGet(id);
    Assert.NotNull(def);
    Assert.Equal(name,     def!.DisplayName);
    Assert.Equal(category, def.Category);
    Assert.Equal(cost,     def.Cost);
    Assert.Equal(prereqId, def.PrerequisiteId);
}

[Fact]
public void CookbookSlotCount_returns_5_10_20_for_tiers_1_2_3()
{
    Assert.Equal(5,  UpgradeCatalog.CookbookSlotCount(1));
    Assert.Equal(10, UpgradeCatalog.CookbookSlotCount(2));
    Assert.Equal(20, UpgradeCatalog.CookbookSlotCount(3));
}

[Fact]
public void CraftbookSlotCount_returns_5_10_20_for_tiers_1_2_3()
{
    Assert.Equal(5,  UpgradeCatalog.CraftbookSlotCount(1));
    Assert.Equal(10, UpgradeCatalog.CraftbookSlotCount(2));
    Assert.Equal(20, UpgradeCatalog.CraftbookSlotCount(3));
}

[Fact]
public void CookbookSlotCount_returns_zero_for_tier_zero()
{
    Assert.Equal(0, UpgradeCatalog.CookbookSlotCount(0));
    Assert.Equal(0, UpgradeCatalog.CraftbookSlotCount(0));
}
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests.Cookbook|FullyQualifiedName~UpgradeCatalogTests.Craftbook"
```

Expected: FAIL (`TryGet` returns null; `CookbookSlotCount` / `CraftbookSlotCount` don't exist).

- [ ] **Step 3: Add the six catalog entries and two slot helpers**

Edit `src/TheLongestYear.Core/UpgradeCatalog.cs`.

In `Build()`, add after the comment about Carryover entries (currently the comment `// (Carryover: hand-authored entries removed in Plan 06A…)`) and before the `// Efficiency` block:

```csharp
        // Carryover — Cookbook + Craftbook (recipe banking across runs).
        // Tier determines the slot pool size. Highest owned tier wins (owning III = 20 slots).
        // Cookbook: gated by kitchen (HouseUpgradeLevel >= 1) at interaction time, not at purchase.
        // Craftbook: no in-run prereq — available from day 1 of the run after purchase.
        new UpgradeDefinition("cookbook_1", UpgradeCategory.Carryover, "Cookbook I",
            "Bank up to 5 cooking recipes across runs. The Junimos leave a cookbook on your kitchen counter.",
            150),
        new UpgradeDefinition("cookbook_2", UpgradeCategory.Carryover, "Cookbook II",
            "Expand your cookbook to 10 recipe slots.",
            350, "cookbook_1"),
        new UpgradeDefinition("cookbook_3", UpgradeCategory.Carryover, "Cookbook III",
            "Expand your cookbook to 20 recipe slots.",
            700, "cookbook_2"),
        new UpgradeDefinition("craftbook_1", UpgradeCategory.Carryover, "Craftbook I",
            "Bank up to 5 crafting recipes across runs. The Junimos leave a craftbook on your farmhouse table.",
            150),
        new UpgradeDefinition("craftbook_2", UpgradeCategory.Carryover, "Craftbook II",
            "Expand your craftbook to 10 recipe slots.",
            350, "craftbook_1"),
        new UpgradeDefinition("craftbook_3", UpgradeCategory.Carryover, "Craftbook III",
            "Expand your craftbook to 20 recipe slots.",
            700, "craftbook_2"),
```

Add two static helper methods to the `UpgradeCatalog` class (after the existing `TryGet` method):

```csharp
/// <summary>
/// Total cooking recipe slots granted by the highest owned Cookbook tier.
/// Tier 0 = no Cookbook purchased = 0 slots. Tier 1 = 5, Tier 2 = 10, Tier 3 = 20.
/// The highest tier wins — owning II gives 10 slots, not 5+10=15.
/// </summary>
public static int CookbookSlotCount(int highestOwnedTier) => highestOwnedTier switch
{
    1 => 5,
    2 => 10,
    3 => 20,
    _ => 0
};

/// <summary>
/// Total crafting recipe slots granted by the highest owned Craftbook tier.
/// Same slot counts as <see cref="CookbookSlotCount"/> — mirrors Cookbook by design.
/// </summary>
public static int CraftbookSlotCount(int highestOwnedTier) => highestOwnedTier switch
{
    1 => 5,
    2 => 10,
    3 => 20,
    _ => 0
};
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj --filter "FullyQualifiedName~UpgradeCatalogTests"
```

Expected: PASS (all UpgradeCatalogTests).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/UpgradeCatalog.cs tests/TheLongestYear.Tests/UpgradeCatalogTests.cs
git commit -m "$(cat <<'EOF'
feat(core): add 6 Carryover cookbook/craftbook catalog entries + slot-count helpers

Phase B persistence design §C. Slot counts: I=5, II=10, III=20 (highest owned
tier wins, not cumulative add). CookbookSlotCount/CraftbookSlotCount static
helpers centralise this logic so menus and the reset layer read from one place.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 3: Recipe re-grant in `FarmerReset.Apply` (mod-side, no new tests)

**Files:**
- Modify: `src/TheLongestYear/Loop/FarmerReset.cs`

After the existing skill/tool wipe in `Apply`, re-add every recipe in `MetaState.CookbookRecipes` and `MetaState.CraftbookRecipes` to the player's dictionaries. `FarmerReset` already receives `RunBaseline`; it doesn't have direct access to `MetaState`. Solution: extend the `Apply` signature to accept the banked recipe lists (two `IReadOnlyList<string>` parameters — keeps Core/mod boundary clean).

Verification is manual playtest in Task 13.

- [ ] **Step 1: Extend `Apply` and add the recipe grant helper**

Replace the existing `Apply` method signature and add the recipe grant at the end. Full updated method:

```csharp
public void Apply(Farmer p, RunBaseline baseline,
    IReadOnlyList<string> cookbookRecipes,
    IReadOnlyList<string> craftbookRecipes)
{
    p.Money = baseline.StartingGold;

    // Inventory — wipe CONTENTS but set the slot count from the baseline (Stash
    // preservation is Plan 07). p.Items.Clear() removes the slot list itself, which
    // leaves MaxItems lookups returning 0 → addItemToInventory always fails (round-3
    // playtest bug); reset MaxItems then re-pad nulls.
    p.MaxItems = baseline.MaxItems;
    p.Items.Clear();
    for (int i = 0; i < p.MaxItems; i++)
        p.Items.Add(null);

    // Skills — clear everything first.
    for (int i = 0; i < p.experiencePoints.Count; i++)
        p.experiencePoints[i] = 0;
    p.farmingLevel.Value = 0;
    p.miningLevel.Value = 0;
    p.fishingLevel.Value = 0;
    p.foragingLevel.Value = 0;
    p.combatLevel.Value = 0;
    p.luckLevel.Value = 0;
    p.professions.Clear();

    // Re-grant kept skill levels + floor XP to the level's threshold.
    // Farmer.getBaseExperienceForLevel is the vanilla XP-for-level table
    // (decompile: StardewValley\StardewValley\Farmer.cs:3046, used at line 7233).
    foreach (var kvp in baseline.SkillLevels)
    {
        int skillIndex = kvp.Key;
        int level = kvp.Value;
        p.experiencePoints[skillIndex] = Farmer.getBaseExperienceForLevel(level);
        SetSkillLevel(p, skillIndex, level);
    }

    // Re-grant kept tool tiers. Player's toolList still has the vanilla baseline
    // tools (rusty); just bump their UpgradeLevel. Tool.UpgradeLevel is settable
    // directly (decompile: StardewValley\StardewValley\Tool.cs:167).
    ApplyToolTiers(p, baseline.ToolTiers);

    // Relationships, mail, events, quests.
    p.friendshipData.Clear();
    p.mailReceived.Clear();
    p.eventsSeen.Clear();
    p.questLog.Clear();

    // Suppress the vanilla intro cutscene from replaying every loop (matches TitleMenu's new-game path).
    p.eventsSeen.Add("60367");

    // Vitals to full.
    p.stamina = p.maxStamina.Value;
    p.health = p.maxHealth;

    // House upgrade — set Kitchen on day 1 if kept_kitchen owned. The actual
    // FarmHouse layout switch happens in WorldResetService (it has to resetForPlayerEntry
    // after setting the level so the kitchen tiles appear).
    if (baseline.KitchenOnDay1)
        p.HouseUpgradeLevel = 1;

    // Re-grant banked cooking recipes. Value 0 = vanilla "learned but never cooked".
    // Do this AFTER clearing mail/events so the "you learned a recipe" pop-up doesn't
    // fire for each one (the pop-up reads mailReceived for the "gotRecipe_X" flags;
    // clearing mail first means no duplicate notification on the first morning).
    GrantBankedRecipes(p.cookingRecipes, cookbookRecipes);
    GrantBankedRecipes(p.craftingRecipes, craftbookRecipes);

    _monitor.Log(
        $"FarmerReset: gold={baseline.StartingGold}, slots={baseline.MaxItems}, " +
        $"tools=[{string.Join(",", baseline.ToolTiers)}], " +
        $"skills=[{string.Join(",", baseline.SkillLevels)}], " +
        $"kitchen={baseline.KitchenOnDay1}, " +
        $"cookRecipes={cookbookRecipes.Count}, craftRecipes={craftbookRecipes.Count}.",
        LogLevel.Trace);
}

private static void GrantBankedRecipes(
    IDictionary<string, int> farmerDict,
    IReadOnlyList<string> banked)
{
    foreach (string recipeId in banked)
    {
        // 0 = "learned but never cooked/crafted". Don't overwrite a higher count
        // if the player somehow already has it (idempotent add).
        if (!farmerDict.ContainsKey(recipeId))
            farmerDict[recipeId] = 0;
    }
}
```

- [ ] **Step 2: Update the call site in `WorldResetService.PerformReset`**

In `src/TheLongestYear/Loop/WorldResetService.cs`, find the existing call to `_farmerReset.Apply`:

```csharp
            _farmerReset.Apply(Game1.player, baseline);
```

Replace it with:

```csharp
            _farmerReset.Apply(Game1.player, baseline,
                _meta.CookbookRecipes,
                _meta.CraftbookRecipes);
```

`_meta` in `WorldResetService` is `TheLongestYear.Core.MetaState` (already a field). The two new properties from Task 1 are accessible directly.

- [ ] **Step 3: Build to verify no compiler errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: SUCCESS (zero errors).

- [ ] **Step 4: Run tests to confirm nothing regressed**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS (all 280+ tests; `FarmerReset` touches `Farmer` so it isn't unit-tested — the compile check is the gate here).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/Loop/FarmerReset.cs src/TheLongestYear/Loop/WorldResetService.cs
git commit -m "$(cat <<'EOF'
feat(mod): re-grant banked cookbook/craftbook recipes in FarmerReset.Apply

Phase B §C recipe re-grant. GrantBankedRecipes writes value=0 (vanilla "learned
but never cooked") for every id in MetaState.CookbookRecipes /CraftbookRecipes
after the mailReceived wipe so "you learned a recipe" pop-ups don't fire for
already-known recipes.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 4: `IndicatorRegistry` — reusable ?/! bubble system (mod-side)

**Files:**
- Create: `src/TheLongestYear/UI/IndicatorRegistry.cs`
- Modify: `src/TheLongestYear/ModEntry.cs`

Draws a ? or ! sprite above a world tile via `Display.RenderedWorld`. Dismissed state is stored in `MetaState.DismissedIndicators` (Task 1) so bubbles don't re-appear after resets. Three initial uses: `tly.fireplace` (existing board), `tly.cookbook`, `tly.craftbook`.

`IndicatorKind` enum: `Question` (?) and `Exclamation` (!).

The ? tile comes from `Game1.mouseCursors` at source rect `(397, 489, 10, 10)` (the vanilla "new item" exclamation bubble — visually a ?-style rounded pop). For a distinct !, use source rect `(410, 501, 10, 10)`. Both are scaled 3× at world zoom so they float visibly above the tile. Exact source rects verified against vanilla icon usage in `Object.drawWhenHeld` (decompile: `StardewValley\StardewValley\Object.cs`).

The registry renders in world-space: tile center X, tile top Y − 32 pixels (one tile above). SMAPI's `RenderedWorld` event fires after the world layer but before UI, so the bubble sits over world objects correctly.

- [ ] **Step 1: Create `IndicatorRegistry.cs`**

Create `src/TheLongestYear/UI/IndicatorRegistry.cs`:

```csharp
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Draws ? or ! bubble sprites above interactable world tiles to signal "new thing here".
    /// Dismissed indicators are persisted in <see cref="MetaState.DismissedIndicators"/> so
    /// they don't re-appear across resets.
    ///
    /// Usage:
    ///   IndicatorRegistry.Register("tly.cookbook", farmhouse, new Vector2(5, 7), IndicatorKind.Question);
    ///   IndicatorRegistry.Dismiss("tly.cookbook");  // call when the player first opens the menu
    ///   bool show = IndicatorRegistry.IsActive("tly.cookbook");
    /// </summary>
    internal static class IndicatorRegistry
    {
        // Source rects inside Game1.mouseCursors (1× pixel coords from the vanilla spritesheet).
        // Question-mark bubble: used for "new undiscovered thing here".
        private static readonly Rectangle QuestionSourceRect  = new Rectangle(397, 489, 10, 10);
        // Exclamation bubble: used for "urgent action needed".
        private static readonly Rectangle ExclamationSourceRect = new Rectangle(410, 501, 10, 10);

        private const float Scale = 3f;   // renders at 30×30 px at 100% zoom
        private const int   OffsetY = 32; // pixels above tile top edge (world-space pre-zoom)

        private sealed class Entry
        {
            public GameLocation Location { get; }
            public Vector2 Tile { get; }
            public IndicatorKind Kind { get; }

            public Entry(GameLocation location, Vector2 tile, IndicatorKind kind)
            {
                Location = location;
                Tile = tile;
                Kind = kind;
            }
        }

        private static readonly Dictionary<string, Entry> _entries = new();
        private static MetaState? _meta;

        /// <summary>Call once from ModEntry.OnSaveLoaded to link the registry to the live MetaState.</summary>
        public static void Attach(MetaState meta) => _meta = meta;

        /// <summary>Call from ModEntry.OnSaveLoaded to reset the in-memory registration table
        /// (previous run's locations are stale after loadForNewGame).</summary>
        public static void ClearRegistrations() => _entries.Clear();

        /// <summary>Register a bubble for the given id, location, and tile. Safe to call multiple
        /// times — re-registration updates the entry (e.g. location object changes after reset).</summary>
        public static void Register(string id, GameLocation location, Vector2 tile, IndicatorKind kind)
        {
            _entries[id] = new Entry(location, tile, kind);
        }

        /// <summary>Dismiss this indicator. Writes to <see cref="MetaState.DismissedIndicators"/>
        /// so the dismissed state survives across sessions and resets.</summary>
        public static void Dismiss(string id)
        {
            _meta?.DismissedIndicators.Add(id);
        }

        /// <summary>True when the indicator is registered, not dismissed, and its location is
        /// the current location (so we only draw in the right room).</summary>
        public static bool IsActive(string id)
        {
            if (_meta == null) return false;
            if (_meta.DismissedIndicators.Contains(id)) return false;
            return _entries.ContainsKey(id);
        }

        /// <summary>SMAPI RenderedWorld handler. Call from ModEntry.</summary>
        public static void OnRenderedWorld(object? sender, RenderedWorldEventArgs e)
        {
            if (_meta == null) return;

            foreach (var kvp in _entries)
            {
                string id = kvp.Key;
                Entry entry = kvp.Value;

                // Only draw if not dismissed and the current location matches.
                if (_meta.DismissedIndicators.Contains(id)) continue;
                if (Game1.currentLocation != entry.Location) continue;

                DrawBubble(e.SpriteBatch, entry.Tile, entry.Kind);
            }
        }

        private static void DrawBubble(SpriteBatch b, Vector2 tile, IndicatorKind kind)
        {
            Rectangle src = kind == IndicatorKind.Question ? QuestionSourceRect : ExclamationSourceRect;

            // Convert tile coords to screen coords: tile * tileSize → world coords
            // → subtract viewport origin → apply zoom.
            float worldX = tile.X * Game1.tileSize + (Game1.tileSize / 2f) - (src.Width * Scale / 2f);
            float worldY = tile.Y * Game1.tileSize - OffsetY;

            Vector2 screenPos = new Vector2(
                (worldX - Game1.viewport.X) * Game1.options.zoomLevel,
                (worldY - Game1.viewport.Y) * Game1.options.zoomLevel);

            b.Draw(Game1.mouseCursors, screenPos, src,
                Color.White, 0f, Vector2.Zero,
                Scale * Game1.options.zoomLevel, SpriteEffects.None, 1f);
        }
    }

    internal enum IndicatorKind { Question, Exclamation }
}
```

- [ ] **Step 2: Wire `IndicatorRegistry` into `ModEntry`**

In `src/TheLongestYear/ModEntry.cs`:

a. Add the event subscription in `Entry` (after the existing `helper.Events.Display.RenderedHud += this.OnRenderedHud` line):

```csharp
            helper.Events.Display.RenderedWorld += IndicatorRegistry.OnRenderedWorld;
```

b. Add these two lines at the START of `OnSaveLoaded` (before `_meta.Load()`):

```csharp
            IndicatorRegistry.Attach(_meta.State);
            IndicatorRegistry.ClearRegistrations();
```

Wait — `_meta.Load()` is called at the start of `OnSaveLoaded` and populates `_meta.State`. So `IndicatorRegistry.Attach` must happen AFTER `_meta.Load()`. Place these two lines right after the `_meta.Load();` line:

```csharp
            _meta.Load();
            IndicatorRegistry.Attach(_meta.State);
            IndicatorRegistry.ClearRegistrations();
```

The `using TheLongestYear.UI;` is already present (it imports `JunimoShrineMenu`). No additional using needed.

- [ ] **Step 3: Build to verify no compiler errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/UI/IndicatorRegistry.cs src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
feat(mod): add IndicatorRegistry — reusable ?/! bubble over world tiles

Phase B §D. Draws via Display.RenderedWorld so bubbles sit in the world layer.
Dismissed state lives in MetaState.DismissedIndicators (persistent across resets).
Initial uses: tly.fireplace, tly.cookbook, tly.craftbook — registered in
subsequent tasks when the interactables and reset path are wired.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 5: `CookbookMenu` — slot-grid IClickableMenu (mod-side)

**Files:**
- Create: `src/TheLongestYear/UI/CookbookMenu.cs`

Slot-grid menu for managing banked cooking recipes. Opens from the kitchen counter interaction (Task 7). Follows `JunimoShrineMenu` as the pattern for layout, draw, and gamepad support.

**UI shape:**
- Header: "Cookbook — X / Y slots"
- Rows: one per slot. Shows recipe display name if filled, "[empty]" if not.
- Click empty row → opens an inline recipe picker sub-list (all currently-known cooking recipes not already slotted, as a scrollable sub-list within the same menu).
- Click filled row → confirm dialog: "Remove [name] from cookbook? (Y/N)" using a simple `Game1.activeClickableMenu` swap to a `ConfirmationDialog`.
- Slot count = `UpgradeCatalog.CookbookSlotCount(highestOwnedCookbookTier)`.

**Vanilla recipe display names:** `CraftingRecipe.cookingRecipes[id].name` on PC (decompile: `StardewValley\StardewValley\CraftingRecipe.cs`). For display-name lookup, use `new CraftingRecipe(id, isCookingRecipe: true).name` — this gives the localised string.

- [ ] **Step 1: Determine slot count helper usage**

The menu receives `MetaState meta` and reads `meta.HighestKeptTier("cookbook_", maxTier: 3)` to determine the highest owned Cookbook tier, then passes that to `UpgradeCatalog.CookbookSlotCount(tier)`.

- [ ] **Step 2: Create `CookbookMenu.cs`**

Create `src/TheLongestYear/UI/CookbookMenu.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Slot-grid menu for managing banked cooking recipes in <see cref="MetaState.CookbookRecipes"/>.
    /// Opened when the player interacts with the Cookbook world object (kitchen counter patch).
    ///
    /// Slot count = <see cref="UpgradeCatalog.CookbookSlotCount"/> of the highest owned Cookbook tier.
    /// Empty slot click → inline recipe picker (currently-known, unslotted recipes only).
    /// Filled slot click → confirm-removal dialog.
    /// On dismiss, <see cref="IndicatorRegistry.Dismiss"/> fires for "tly.cookbook".
    /// </summary>
    internal sealed class CookbookMenu : IClickableMenu
    {
        private const int PanelWidth  = 900;
        private const int PanelHeight = 640;
        private const int PanelPad    = 32;
        private const int RowHeight   = 72;
        private const int RowSpacing  = 8;
        private const int RowIdBase   = 8100;
        private const int ScrollUpId  = 8900;
        private const int ScrollDownId = 8901;

        private readonly IMonitor _monitor;
        private readonly MetaState _meta;
        private readonly int _slotCount;

        // Sub-mode: when non-null we are in "pick a recipe to fill slot _pendingSlot".
        private int _pendingSlot = -1;
        private List<string>? _pickerList;   // recipe ids available to pick
        private int _pickerScroll;

        private int _scroll;
        private int _rowsPerPage;
        private readonly List<ClickableComponent> _rowSlots = new();
        private ClickableTextureComponent _scrollUp = null!;
        private ClickableTextureComponent _scrollDown = null!;

        public CookbookMenu(IMonitor monitor, MetaState meta)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _monitor = monitor;
            _meta    = meta;
            int tier = meta.HighestKeptTier("cookbook_", maxTier: 3);
            _slotCount = UpgradeCatalog.CookbookSlotCount(tier);
            RecomputeLayout();
            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                this.snapToDefaultClickableComponent();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeLayout();
        }

        private void RecomputeLayout()
        {
            width  = Math.Min(PanelWidth,  Game1.uiViewport.Width  - 64);
            height = Math.Min(PanelHeight, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width  - width)  / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int listX = xPositionOnScreen + PanelPad;
            int listY = yPositionOnScreen + 80;
            int listW = width - PanelPad * 2 - 52;   // leave room for scroll arrows
            int listH = height - 80 - PanelPad;
            _rowsPerPage = Math.Max(1, listH / (RowHeight + RowSpacing));

            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
            {
                int rowY = listY + i * (RowHeight + RowSpacing);
                _rowSlots.Add(new ClickableComponent(
                    new Rectangle(listX, rowY, listW, RowHeight), "row-" + i)
                {
                    myID = RowIdBase + i,
                    upNeighborID   = i == 0 ? ScrollUpId : RowIdBase + i - 1,
                    downNeighborID = i == _rowsPerPage - 1 ? ScrollDownId : RowIdBase + i + 1,
                });
            }

            int arrowX = listX + listW + 4;
            _scrollUp = new ClickableTextureComponent("scroll-up",
                new Rectangle(arrowX, listY, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f)
            { myID = ScrollUpId, downNeighborID = ScrollDownId, leftNeighborID = RowIdBase };

            _scrollDown = new ClickableTextureComponent("scroll-down",
                new Rectangle(arrowX, listY + listH - 48, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f)
            { myID = ScrollDownId, upNeighborID = ScrollUpId, leftNeighborID = RowIdBase + _rowsPerPage - 1 };

            this.initializeUpperRightCloseButton();
            allClickableComponents = new List<ClickableComponent>(_rowSlots) { _scrollUp, _scrollDown };
            if (upperRightCloseButton != null) allClickableComponents.Add(upperRightCloseButton);
            ClampScroll();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _rowSlots.Count > 0 ? _rowSlots[0] : null;
            if (currentlySnappedComponent != null) this.snapCursorToCurrentSnappedComponent();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_scrollUp.containsPoint(x, y))   { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y))  { Scroll(+1); return; }

            if (_pickerList != null)
            {
                // Picker sub-mode: row click selects a recipe.
                for (int i = 0; i < _rowSlots.Count; i++)
                {
                    if (!_rowSlots[i].containsPoint(x, y)) continue;
                    int pickerIndex = _pickerScroll + i;
                    if (pickerIndex >= _pickerList.Count) break;
                    BankRecipe(_pickerList[pickerIndex]);
                    return;
                }
                // Click outside rows → cancel picker
                _pickerList = null;
                _pickerScroll = 0;
                return;
            }

            // Normal slot mode.
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                if (!_rowSlots[i].containsPoint(x, y)) continue;
                int slotIndex = _scroll + i;
                if (slotIndex >= _slotCount) break;

                if (slotIndex < _meta.CookbookRecipes.Count)
                    PromptRemove(slotIndex);
                else
                    OpenPicker(slotIndex);
                return;
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            Scroll(direction > 0 ? -1 : +1);
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                int id = currentlySnappedComponent.myID;
                if (id == ScrollUpId)   { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
                if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
                    receiveLeftClick(_rowSlots[id - RowIdBase].bounds.Center.X,
                                     _rowSlots[id - RowIdBase].bounds.Center.Y);
                return;
            }
            base.receiveGamePadButton(b);
        }

        private void OpenPicker(int slotIndex)
        {
            _pendingSlot = slotIndex;
            _pickerList  = AvailableRecipesToBank();
            _pickerScroll = 0;
            if (_pickerList.Count == 0)
            {
                Game1.addHUDMessage(new HUDMessage("No new recipes to add — learn more recipes first.", HUDMessage.newQuest_type));
                _pickerList = null;
                _pendingSlot = -1;
            }
        }

        private void BankRecipe(string recipeId)
        {
            if (_meta.CookbookRecipes.Contains(recipeId)) return;  // guard
            _meta.CookbookRecipes.Add(recipeId);
            Game1.playSound("smallSelect");
            _monitor.Log($"CookbookMenu: banked recipe '{recipeId}'.", LogLevel.Trace);
            _pickerList  = null;
            _pendingSlot = -1;
        }

        private void PromptRemove(int slotIndex)
        {
            string recipeId = _meta.CookbookRecipes[slotIndex];
            string name = RecipeDisplayName(recipeId, isCooking: true);
            Game1.activeClickableMenu = new ConfirmationDialog(
                $"Remove \"{name}\" from the cookbook?\nThis recipe won't carry over next run unless re-banked.",
                confirmed: _ =>
                {
                    _meta.CookbookRecipes.RemoveAt(slotIndex);
                    Game1.playSound("trashcan");
                    _monitor.Log($"CookbookMenu: removed recipe '{recipeId}' from slot {slotIndex}.", LogLevel.Trace);
                    Game1.activeClickableMenu = this;
                },
                cancel: _ => Game1.activeClickableMenu = this);
        }

        private List<string> AvailableRecipesToBank()
        {
            var already = new HashSet<string>(_meta.CookbookRecipes);
            return Game1.player.cookingRecipes.Keys
                .Where(id => !already.Contains(id))
                .OrderBy(id => id)
                .ToList();
        }

        private void Scroll(int delta)
        {
            int before = _scroll;
            if (_pickerList != null)
                _pickerScroll = Math.Max(0, Math.Min(_pickerList.Count - _rowsPerPage, _pickerScroll + delta));
            else
                _scroll += delta;
            ClampScroll();
            if (_scroll != before) Game1.playSound("shwip");
        }

        private void ClampScroll()
        {
            if (_pickerList != null)
            {
                _pickerScroll = Math.Max(0, Math.Min(Math.Max(0, _pickerList.Count - _rowsPerPage), _pickerScroll));
                return;
            }
            int maxStart = Math.Max(0, _slotCount - _rowsPerPage);
            _scroll = Math.Max(0, Math.Min(maxStart, _scroll));
        }

        public override void emergencyShutDown()
        {
            base.emergencyShutDown();
            IndicatorRegistry.Dismiss("tly.cookbook");
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            IndicatorRegistry.Dismiss("tly.cookbook");
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);

            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = _pickerList != null
                ? "Choose a recipe to bank:"
                : $"Cookbook — {_meta.CookbookRecipes.Count} / {_slotCount} slots";
            StardewValley.BellsAndWhistles.SpriteText.drawStringHorizontallyCenteredAt(
                b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 24);

            if (_pickerList != null)
                DrawPickerRows(b);
            else
                DrawSlotRows(b);

            _scrollUp.draw(b, _scroll > 0 || (_pickerList != null && _pickerScroll > 0) ? Color.White : Color.Gray, 1f);

            int totalRows = _pickerList != null ? _pickerList.Count : _slotCount;
            int scrollStart = _pickerList != null ? _pickerScroll : _scroll;
            _scrollDown.draw(b, (scrollStart + _rowsPerPage) < totalRows ? Color.White : Color.Gray, 1f);

            base.draw(b);
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawSlotRows(SpriteBatch b)
        {
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                int slotIndex = _scroll + i;
                if (slotIndex >= _slotCount) break;

                ClickableComponent slot = _rowSlots[i];
                bool filled = slotIndex < _meta.CookbookRecipes.Count;
                string label = filled
                    ? RecipeDisplayName(_meta.CookbookRecipes[slotIndex], isCooking: true)
                    : "[empty — click to add]";
                Color tint = filled ? Color.White : Color.White * 0.6f;

                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, tint, 1f, false);

                Utility.drawTextWithShadow(b, label, Game1.dialogueFont,
                    new Vector2(slot.bounds.X + 16, slot.bounds.Y + (slot.bounds.Height - (int)Game1.dialogueFont.MeasureString(label).Y) / 2),
                    Game1.textColor);
            }
        }

        private void DrawPickerRows(SpriteBatch b)
        {
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                int pickerIndex = _pickerScroll + i;
                if (pickerIndex >= _pickerList!.Count) break;

                ClickableComponent slot = _rowSlots[i];
                string label = RecipeDisplayName(_pickerList[pickerIndex], isCooking: true);

                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, Color.White, 1f, false);

                Utility.drawTextWithShadow(b, label, Game1.dialogueFont,
                    new Vector2(slot.bounds.X + 16, slot.bounds.Y + (slot.bounds.Height - (int)Game1.dialogueFont.MeasureString(label).Y) / 2),
                    Game1.textColor);
            }
        }

        private static string RecipeDisplayName(string recipeId, bool isCooking)
        {
            try
            {
                // CraftingRecipe.name is the localised display name.
                return new CraftingRecipe(recipeId, isCooking).name;
            }
            catch
            {
                // Unknown recipe id (content mod removed) — fall back to raw id.
                return recipeId;
            }
        }
    }
}
```

- [ ] **Step 3: Build to verify no compiler errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: SUCCESS.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/UI/CookbookMenu.cs
git commit -m "$(cat <<'EOF'
feat(mod): add CookbookMenu — slot-grid IClickableMenu for banked cooking recipes

Phase B §C UI. Slot count from UpgradeCatalog.CookbookSlotCount(highestTier).
Empty slot → inline recipe picker (unslotted currently-known recipes). Filled
slot → ConfirmationDialog remove. Dismisses tly.cookbook indicator on exit.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 6: `CraftbookMenu` — slot-grid IClickableMenu (mod-side)

**Files:**
- Create: `src/TheLongestYear/UI/CraftbookMenu.cs`

Same UX shape as `CookbookMenu` but reads `Farmer.craftingRecipes` and manages `MetaState.CraftbookRecipes`. Differences from `CookbookMenu`:
- Reads `meta.HighestKeptTier("craftbook_", maxTier: 3)` and calls `UpgradeCatalog.CraftbookSlotCount(tier)`.
- Calls `AvailableRecipesToBank` from `Game1.player.craftingRecipes.Keys`.
- `RecipeDisplayName` called with `isCooking: false`.
- Dismisses `"tly.craftbook"` indicator on exit.
- Header: "Craftbook — X / Y slots".

- [ ] **Step 1: Create `CraftbookMenu.cs`**

Create `src/TheLongestYear/UI/CraftbookMenu.cs` — identical structure to `CookbookMenu.cs` with these substitutions applied throughout:

| CookbookMenu text | CraftbookMenu text |
|---|---|
| `CookbookMenu` | `CraftbookMenu` |
| `CookbookRecipes` | `CraftbookRecipes` |
| `"cookbook_"` | `"craftbook_"` |
| `CookbookSlotCount` | `CraftbookSlotCount` |
| `cookingRecipes` | `craftingRecipes` |
| `isCooking: true` | `isCooking: false` |
| `"tly.cookbook"` | `"tly.craftbook"` |
| `"Cookbook — "` | `"Craftbook — "` |
| `"Choose a recipe to bank:"` | `"Choose a recipe to bank:"` (same) |
| `8100` (RowIdBase) | `8200` (different ID range to avoid collision if both ever open) |
| `8900` (ScrollUpId) | `8950` |
| `8901` (ScrollDownId) | `8951` |

Full file:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Slot-grid menu for managing banked crafting recipes in <see cref="MetaState.CraftbookRecipes"/>.
    /// Opened when the player interacts with the Craftbook world object (farmhouse table patch).
    /// Mirrors <see cref="CookbookMenu"/> but operates on <c>Farmer.craftingRecipes</c>.
    /// </summary>
    internal sealed class CraftbookMenu : IClickableMenu
    {
        private const int PanelWidth   = 900;
        private const int PanelHeight  = 640;
        private const int PanelPad     = 32;
        private const int RowHeight    = 72;
        private const int RowSpacing   = 8;
        private const int RowIdBase    = 8200;
        private const int ScrollUpId   = 8950;
        private const int ScrollDownId = 8951;

        private readonly IMonitor _monitor;
        private readonly MetaState _meta;
        private readonly int _slotCount;

        private int _pendingSlot = -1;
        private List<string>? _pickerList;
        private int _pickerScroll;

        private int _scroll;
        private int _rowsPerPage;
        private readonly List<ClickableComponent> _rowSlots = new();
        private ClickableTextureComponent _scrollUp = null!;
        private ClickableTextureComponent _scrollDown = null!;

        public CraftbookMenu(IMonitor monitor, MetaState meta)
            : base(0, 0, 0, 0, showUpperRightCloseButton: true)
        {
            _monitor = monitor;
            _meta    = meta;
            int tier = meta.HighestKeptTier("craftbook_", maxTier: 3);
            _slotCount = UpgradeCatalog.CraftbookSlotCount(tier);
            RecomputeLayout();
            if (Game1.options.snappyMenus && Game1.options.gamepadControls)
                this.snapToDefaultClickableComponent();
        }

        public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
        {
            base.gameWindowSizeChanged(oldBounds, newBounds);
            RecomputeLayout();
        }

        private void RecomputeLayout()
        {
            width  = Math.Min(PanelWidth,  Game1.uiViewport.Width  - 64);
            height = Math.Min(PanelHeight, Game1.uiViewport.Height - 64);
            xPositionOnScreen = (Game1.uiViewport.Width  - width)  / 2;
            yPositionOnScreen = (Game1.uiViewport.Height - height) / 2;

            int listX = xPositionOnScreen + PanelPad;
            int listY = yPositionOnScreen + 80;
            int listW = width - PanelPad * 2 - 52;
            int listH = height - 80 - PanelPad;
            _rowsPerPage = Math.Max(1, listH / (RowHeight + RowSpacing));

            _rowSlots.Clear();
            for (int i = 0; i < _rowsPerPage; i++)
            {
                int rowY = listY + i * (RowHeight + RowSpacing);
                _rowSlots.Add(new ClickableComponent(
                    new Rectangle(listX, rowY, listW, RowHeight), "row-" + i)
                {
                    myID = RowIdBase + i,
                    upNeighborID   = i == 0 ? ScrollUpId : RowIdBase + i - 1,
                    downNeighborID = i == _rowsPerPage - 1 ? ScrollDownId : RowIdBase + i + 1,
                });
            }

            int arrowX = listX + listW + 4;
            _scrollUp = new ClickableTextureComponent("scroll-up",
                new Rectangle(arrowX, listY, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 459, 11, 12), 4f)
            { myID = ScrollUpId, downNeighborID = ScrollDownId, leftNeighborID = RowIdBase };

            _scrollDown = new ClickableTextureComponent("scroll-down",
                new Rectangle(arrowX, listY + listH - 48, 44, 48), null, null,
                Game1.mouseCursors, new Rectangle(421, 472, 11, 12), 4f)
            { myID = ScrollDownId, upNeighborID = ScrollUpId, leftNeighborID = RowIdBase + _rowsPerPage - 1 };

            this.initializeUpperRightCloseButton();
            allClickableComponents = new List<ClickableComponent>(_rowSlots) { _scrollUp, _scrollDown };
            if (upperRightCloseButton != null) allClickableComponents.Add(upperRightCloseButton);
            ClampScroll();
        }

        public override void snapToDefaultClickableComponent()
        {
            currentlySnappedComponent = _rowSlots.Count > 0 ? _rowSlots[0] : null;
            if (currentlySnappedComponent != null) this.snapCursorToCurrentSnappedComponent();
        }

        public override void receiveLeftClick(int x, int y, bool playSound = true)
        {
            base.receiveLeftClick(x, y, playSound);

            if (_scrollUp.containsPoint(x, y))   { Scroll(-1); return; }
            if (_scrollDown.containsPoint(x, y))  { Scroll(+1); return; }

            if (_pickerList != null)
            {
                for (int i = 0; i < _rowSlots.Count; i++)
                {
                    if (!_rowSlots[i].containsPoint(x, y)) continue;
                    int pickerIndex = _pickerScroll + i;
                    if (pickerIndex >= _pickerList.Count) break;
                    BankRecipe(_pickerList[pickerIndex]);
                    return;
                }
                _pickerList = null;
                _pickerScroll = 0;
                return;
            }

            for (int i = 0; i < _rowSlots.Count; i++)
            {
                if (!_rowSlots[i].containsPoint(x, y)) continue;
                int slotIndex = _scroll + i;
                if (slotIndex >= _slotCount) break;

                if (slotIndex < _meta.CraftbookRecipes.Count)
                    PromptRemove(slotIndex);
                else
                    OpenPicker(slotIndex);
                return;
            }
        }

        public override void receiveScrollWheelAction(int direction)
        {
            base.receiveScrollWheelAction(direction);
            Scroll(direction > 0 ? -1 : +1);
        }

        public override void receiveGamePadButton(Microsoft.Xna.Framework.Input.Buttons b)
        {
            if (b == Microsoft.Xna.Framework.Input.Buttons.A && currentlySnappedComponent != null)
            {
                int id = currentlySnappedComponent.myID;
                if (id == ScrollUpId)   { Scroll(-1); return; }
                if (id == ScrollDownId) { Scroll(+1); return; }
                if (id >= RowIdBase && id < RowIdBase + _rowsPerPage)
                    receiveLeftClick(_rowSlots[id - RowIdBase].bounds.Center.X,
                                     _rowSlots[id - RowIdBase].bounds.Center.Y);
                return;
            }
            base.receiveGamePadButton(b);
        }

        private void OpenPicker(int slotIndex)
        {
            _pendingSlot = slotIndex;
            _pickerList  = AvailableRecipesToBank();
            _pickerScroll = 0;
            if (_pickerList.Count == 0)
            {
                Game1.addHUDMessage(new HUDMessage("No new recipes to add — learn more recipes first.", HUDMessage.newQuest_type));
                _pickerList = null;
                _pendingSlot = -1;
            }
        }

        private void BankRecipe(string recipeId)
        {
            if (_meta.CraftbookRecipes.Contains(recipeId)) return;
            _meta.CraftbookRecipes.Add(recipeId);
            Game1.playSound("smallSelect");
            _monitor.Log($"CraftbookMenu: banked recipe '{recipeId}'.", LogLevel.Trace);
            _pickerList  = null;
            _pendingSlot = -1;
        }

        private void PromptRemove(int slotIndex)
        {
            string recipeId = _meta.CraftbookRecipes[slotIndex];
            string name = RecipeDisplayName(recipeId);
            Game1.activeClickableMenu = new ConfirmationDialog(
                $"Remove \"{name}\" from the craftbook?\nThis recipe won't carry over next run unless re-banked.",
                confirmed: _ =>
                {
                    _meta.CraftbookRecipes.RemoveAt(slotIndex);
                    Game1.playSound("trashcan");
                    _monitor.Log($"CraftbookMenu: removed recipe '{recipeId}' from slot {slotIndex}.", LogLevel.Trace);
                    Game1.activeClickableMenu = this;
                },
                cancel: _ => Game1.activeClickableMenu = this);
        }

        private List<string> AvailableRecipesToBank()
        {
            var already = new HashSet<string>(_meta.CraftbookRecipes);
            return Game1.player.craftingRecipes.Keys
                .Where(id => !already.Contains(id))
                .OrderBy(id => id)
                .ToList();
        }

        private void Scroll(int delta)
        {
            int before = _scroll;
            if (_pickerList != null)
                _pickerScroll = Math.Max(0, Math.Min(_pickerList.Count - _rowsPerPage, _pickerScroll + delta));
            else
                _scroll += delta;
            ClampScroll();
            if (_scroll != before) Game1.playSound("shwip");
        }

        private void ClampScroll()
        {
            if (_pickerList != null)
            {
                _pickerScroll = Math.Max(0, Math.Min(Math.Max(0, _pickerList.Count - _rowsPerPage), _pickerScroll));
                return;
            }
            int maxStart = Math.Max(0, _slotCount - _rowsPerPage);
            _scroll = Math.Max(0, Math.Min(maxStart, _scroll));
        }

        public override void emergencyShutDown()
        {
            base.emergencyShutDown();
            IndicatorRegistry.Dismiss("tly.craftbook");
        }

        protected override void cleanupBeforeExit()
        {
            base.cleanupBeforeExit();
            IndicatorRegistry.Dismiss("tly.craftbook");
        }

        public override void draw(SpriteBatch b)
        {
            b.Draw(Game1.fadeToBlackRect,
                new Rectangle(0, 0, Game1.uiViewport.Width, Game1.uiViewport.Height),
                Color.Black * 0.5f);

            IClickableMenu.drawTextureBox(b, xPositionOnScreen, yPositionOnScreen, width, height, Color.White);

            string title = _pickerList != null
                ? "Choose a recipe to bank:"
                : $"Craftbook — {_meta.CraftbookRecipes.Count} / {_slotCount} slots";
            StardewValley.BellsAndWhistles.SpriteText.drawStringHorizontallyCenteredAt(
                b, title, xPositionOnScreen + width / 2, yPositionOnScreen + 24);

            if (_pickerList != null)
                DrawPickerRows(b);
            else
                DrawSlotRows(b);

            _scrollUp.draw(b, _scroll > 0 || (_pickerList != null && _pickerScroll > 0) ? Color.White : Color.Gray, 1f);

            int totalRows = _pickerList != null ? _pickerList.Count : _slotCount;
            int scrollStart = _pickerList != null ? _pickerScroll : _scroll;
            _scrollDown.draw(b, (scrollStart + _rowsPerPage) < totalRows ? Color.White : Color.Gray, 1f);

            base.draw(b);
            Game1.mouseCursorTransparency = 1f;
            this.drawMouse(b);
        }

        private void DrawSlotRows(SpriteBatch b)
        {
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                int slotIndex = _scroll + i;
                if (slotIndex >= _slotCount) break;

                ClickableComponent slot = _rowSlots[i];
                bool filled = slotIndex < _meta.CraftbookRecipes.Count;
                string label = filled
                    ? RecipeDisplayName(_meta.CraftbookRecipes[slotIndex])
                    : "[empty — click to add]";
                Color tint = filled ? Color.White : Color.White * 0.6f;

                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, tint, 1f, false);

                Utility.drawTextWithShadow(b, label, Game1.dialogueFont,
                    new Vector2(slot.bounds.X + 16, slot.bounds.Y + (slot.bounds.Height - (int)Game1.dialogueFont.MeasureString(label).Y) / 2),
                    Game1.textColor);
            }
        }

        private void DrawPickerRows(SpriteBatch b)
        {
            for (int i = 0; i < _rowSlots.Count; i++)
            {
                int pickerIndex = _pickerScroll + i;
                if (pickerIndex >= _pickerList!.Count) break;

                ClickableComponent slot = _rowSlots[i];
                string label = RecipeDisplayName(_pickerList[pickerIndex]);

                IClickableMenu.drawTextureBox(b, Game1.menuTexture,
                    new Rectangle(0, 256, 60, 60),
                    slot.bounds.X, slot.bounds.Y, slot.bounds.Width, slot.bounds.Height, Color.White, 1f, false);

                Utility.drawTextWithShadow(b, label, Game1.dialogueFont,
                    new Vector2(slot.bounds.X + 16, slot.bounds.Y + (slot.bounds.Height - (int)Game1.dialogueFont.MeasureString(label).Y) / 2),
                    Game1.textColor);
            }
        }

        private static string RecipeDisplayName(string recipeId)
        {
            try { return new CraftingRecipe(recipeId, isCookingRecipe: false).name; }
            catch { return recipeId; }
        }
    }
}
```

- [ ] **Step 2: Build to verify no compiler errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: SUCCESS.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/UI/CraftbookMenu.cs
git commit -m "$(cat <<'EOF'
feat(mod): add CraftbookMenu — slot-grid IClickableMenu for banked crafting recipes

Phase B §C UI. Mirrors CookbookMenu structure but operates on craftingRecipes.
Uses UpgradeCatalog.CraftbookSlotCount and meta.HighestKeptTier("craftbook_",3).
Dismisses tly.craftbook indicator on exit.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 7: `CookbookInteractable` + `CraftbookInteractable` — FarmHouse interaction patches (mod-side)

**Files:**
- Create: `src/TheLongestYear/UI/CookbookInteractable.cs`
- Create: `src/TheLongestYear/UI/CraftbookInteractable.cs`
- Modify: `src/TheLongestYear/ModEntry.cs`
- Modify: `src/TheLongestYear/UI/MenuLauncher.cs`

Two Harmony prefixes on `FarmHouse.checkAction` that open the respective menus when the player presses the action button on a specific tile. Follows the exact same pattern as `SeasonGoalsBoard.CheckActionPatch` on `CommunityCenter.checkAction`.

**Tile coordinates (to be determined via `tly_here` command during playtest):**

The kitchen counter tile and farmhouse table tile are hardcoded in the FarmHouse map (`Maps\FarmHouse.tbin` / `Maps\FarmHouse2.tbin`). From the Android decompile, the kitchen area is around tile X=3..7, Y=2..4 in the kitchen extension (available after `HouseUpgradeLevel >= 1`). The farmhouse table is around X=3, Y=5 in the main room. **These are guesses that must be verified in-game with `tly_here`.** For the initial implementation, use configurable tile coords from `GameplayConfig` (same approach as `SeasonGoalsBoardTileX/Y`) so they can be tuned without code changes.

Add to `GameplayConfig` in `src/TheLongestYear.Core/GameplayConfig.cs`:

```csharp
// Cookbook interactable tile in the FarmHouse (kitchen counter).
// Default (0,0) disables the interaction — set via tly_setcookbook in-game.
public int CookbookTileX { get; set; } = 0;
public int CookbookTileY { get; set; } = 0;

// Craftbook interactable tile in the FarmHouse (main table).
// Default (0,0) disables the interaction — set via tly_setcraftbook in-game.
public int CraftbookTileX { get; set; } = 0;
public int CraftbookTileY { get; set; } = 0;
```

- [ ] **Step 1: Add the four config properties to `GameplayConfig`**

Edit `src/TheLongestYear.Core/GameplayConfig.cs` and add the four properties shown above after `SeasonGoalsBoardTileY`.

- [ ] **Step 2: Create `CookbookInteractable.cs`**

Create `src/TheLongestYear/UI/CookbookInteractable.cs`:

```csharp
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Opens <see cref="CookbookMenu"/> when the player presses the action button on the
    /// configured kitchen counter tile inside the FarmHouse. Only activates when:
    /// - <c>HouseUpgradeLevel >= 1</c> (kitchen built), AND
    /// - the player owns at least <c>cookbook_1</c>.
    ///
    /// Tile coords configured via <see cref="GameplayConfig.CookbookTileX/Y"/>. Default (0,0)
    /// = disabled; set via the <c>tly_setcookbook</c> console command after locating the tile
    /// in-game with <c>tly_here</c>.
    /// </summary>
    internal static class CookbookInteractable
    {
        private static CookbookInteractableInstance? _instance;

        public static void ConnectTo(IMonitor monitor, GameplayConfig config, MetaStore meta)
        {
            _instance = new CookbookInteractableInstance(monitor, config, meta);
        }

        [HarmonyPatch(typeof(FarmHouse), nameof(FarmHouse.checkAction))]
        internal static class CheckActionPatch
        {
            // ReSharper disable once InconsistentNaming — Harmony convention.
            private static bool Prefix(FarmHouse __instance, xTile.Dimensions.Location tileLocation,
                xTile.Dimensions.Rectangle viewport, Farmer who, ref bool __result)
            {
                if (_instance == null) return true;
                return _instance.TryHandle(__instance, tileLocation, who, ref __result);
            }
        }
    }

    internal sealed class CookbookInteractableInstance
    {
        private readonly IMonitor _monitor;
        private readonly GameplayConfig _config;
        private readonly MetaStore _meta;

        public CookbookInteractableInstance(IMonitor monitor, GameplayConfig config, MetaStore meta)
        {
            _monitor = monitor;
            _config  = config;
            _meta    = meta;
        }

        public bool TryHandle(FarmHouse house, xTile.Dimensions.Location tile,
            Farmer who, ref bool result)
        {
            // (0,0) means "not yet configured" — fall through to vanilla.
            if (_config.CookbookTileX == 0 && _config.CookbookTileY == 0) return true;
            if (tile.X != _config.CookbookTileX || tile.Y != _config.CookbookTileY) return true;

            // Kitchen must be built.
            if (who.HouseUpgradeLevel < 1) return true;

            // Must own at least Cookbook I.
            if (!_meta.State.HasUpgrade("cookbook_1")) return true;

            // Guard: don't open over an existing menu.
            if (Game1.activeClickableMenu != null) { result = true; return false; }

            Game1.activeClickableMenu = new CookbookMenu(_monitor, _meta.State);
            _monitor.Log("CookbookInteractable: opened CookbookMenu.", LogLevel.Trace);
            result = true;
            return false;
        }
    }
}
```

- [ ] **Step 3: Create `CraftbookInteractable.cs`**

Create `src/TheLongestYear/UI/CraftbookInteractable.cs`:

```csharp
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;
using TheLongestYear.Core;

namespace TheLongestYear.UI
{
    /// <summary>
    /// Opens <see cref="CraftbookMenu"/> when the player presses the action button on the
    /// configured farmhouse table tile inside the FarmHouse. Only activates when the player
    /// owns at least <c>craftbook_1</c>. No in-run house-upgrade prereq (accessible from day 1).
    ///
    /// Tile coords configured via <see cref="GameplayConfig.CraftbookTileX/Y"/>. Default (0,0)
    /// = disabled; set via the <c>tly_setcraftbook</c> console command after locating the tile
    /// in-game with <c>tly_here</c>.
    /// </summary>
    internal static class CraftbookInteractable
    {
        private static CraftbookInteractableInstance? _instance;

        public static void ConnectTo(IMonitor monitor, GameplayConfig config, MetaStore meta)
        {
            _instance = new CraftbookInteractableInstance(monitor, config, meta);
        }

        [HarmonyPatch(typeof(FarmHouse), nameof(FarmHouse.checkAction))]
        internal static class CheckActionPatch
        {
            // ReSharper disable once InconsistentNaming — Harmony convention.
            private static bool Prefix(FarmHouse __instance, xTile.Dimensions.Location tileLocation,
                xTile.Dimensions.Rectangle viewport, Farmer who, ref bool __result)
            {
                if (_instance == null) return true;
                return _instance.TryHandle(__instance, tileLocation, who, ref __result);
            }
        }
    }

    internal sealed class CraftbookInteractableInstance
    {
        private readonly IMonitor _monitor;
        private readonly GameplayConfig _config;
        private readonly MetaStore _meta;

        public CraftbookInteractableInstance(IMonitor monitor, GameplayConfig config, MetaStore meta)
        {
            _monitor = monitor;
            _config  = config;
            _meta    = meta;
        }

        public bool TryHandle(FarmHouse house, xTile.Dimensions.Location tile,
            Farmer who, ref bool result)
        {
            if (_config.CraftbookTileX == 0 && _config.CraftbookTileY == 0) return true;
            if (tile.X != _config.CraftbookTileX || tile.Y != _config.CraftbookTileY) return true;

            if (!_meta.State.HasUpgrade("craftbook_1")) return true;

            if (Game1.activeClickableMenu != null) { result = true; return false; }

            Game1.activeClickableMenu = new CraftbookMenu(_monitor, _meta.State);
            _monitor.Log("CraftbookInteractable: opened CraftbookMenu.", LogLevel.Trace);
            result = true;
            return false;
        }
    }
}
```

- [ ] **Step 4: Wire into `ModEntry` and add console commands**

In `src/TheLongestYear/ModEntry.cs`, add two things:

a. In `Entry`, after the `SeasonGoalsBoard.ConnectTo(...)` call:

```csharp
            CookbookInteractable.ConnectTo(this.Monitor, _config, _meta);
            CraftbookInteractable.ConnectTo(this.Monitor, _config, _meta);
```

b. Add two new console commands in `Entry` (after the existing `tly_setboard` command):

```csharp
            helper.ConsoleCommands.Add("tly_setcookbook",
                "Anchor the Cookbook to the tile you are facing in the FarmHouse. Writes config.json.",
                this.CmdSetCookbook);
            helper.ConsoleCommands.Add("tly_setcraftbook",
                "Anchor the Craftbook to the tile you are facing in the FarmHouse. Writes config.json.",
                this.CmdSetCraftbook);
            helper.ConsoleCommands.Add("tly_opencookbook",
                "Open the Cookbook menu directly (debug).",
                this.CmdOpenCookbook);
            helper.ConsoleCommands.Add("tly_opencraftbook",
                "Open the Craftbook menu directly (debug).",
                this.CmdOpenCraftbook);
```

c. Add the four command handlers after the existing `CmdSetBoard` method:

```csharp
        private void CmdSetCookbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.currentLocation is not StardewValley.Locations.FarmHouse)
            {
                this.Monitor.Log("tly_setcookbook: stand inside the FarmHouse first.", LogLevel.Warn);
                return;
            }
            int dx = Game1.player.FacingDirection == 1 ? 1 : Game1.player.FacingDirection == 3 ? -1 : 0;
            int dy = Game1.player.FacingDirection == 2 ? 1 : Game1.player.FacingDirection == 0 ? -1 : 0;
            _config.CookbookTileX = (int)Game1.player.Tile.X + dx;
            _config.CookbookTileY = (int)Game1.player.Tile.Y + dy;
            this.Helper.WriteConfig(_config);
            this.Monitor.Log($"Cookbook anchored to ({_config.CookbookTileX}, {_config.CookbookTileY}). Saved to config.json.", LogLevel.Info);
        }

        private void CmdSetCraftbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (Game1.currentLocation is not StardewValley.Locations.FarmHouse)
            {
                this.Monitor.Log("tly_setcraftbook: stand inside the FarmHouse first.", LogLevel.Warn);
                return;
            }
            int dx = Game1.player.FacingDirection == 1 ? 1 : Game1.player.FacingDirection == 3 ? -1 : 0;
            int dy = Game1.player.FacingDirection == 2 ? 1 : Game1.player.FacingDirection == 0 ? -1 : 0;
            _config.CraftbookTileX = (int)Game1.player.Tile.X + dx;
            _config.CraftbookTileY = (int)Game1.player.Tile.Y + dy;
            this.Helper.WriteConfig(_config);
            this.Monitor.Log($"Craftbook anchored to ({_config.CraftbookTileX}, {_config.CraftbookTileY}). Saved to config.json.", LogLevel.Info);
        }

        private void CmdOpenCookbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenCookbook();
        }

        private void CmdOpenCraftbook(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            _launcher?.OpenCraftbook();
        }
```

Also wire the new commands into `ExecuteDebugLine`'s switch (same method, add two cases):

```csharp
                case "tly_setcookbook":   this.CmdSetCookbook(command, args); break;
                case "tly_setcraftbook":  this.CmdSetCraftbook(command, args); break;
                case "tly_opencookbook":  this.CmdOpenCookbook(command, args); break;
                case "tly_opencraftbook": this.CmdOpenCraftbook(command, args); break;
```

- [ ] **Step 5: Add `OpenCookbook` / `OpenCraftbook` to `MenuLauncher`**

In `src/TheLongestYear/UI/MenuLauncher.cs`, add after `OpenShrineShop`:

```csharp
        public void OpenCookbook()
        {
            if (!CanOpen()) return;
            Game1.activeClickableMenu = new CookbookMenu(_monitor, _store.State);
            _monitor.Log("Opened Cookbook menu.", LogLevel.Info);
        }

        public void OpenCraftbook()
        {
            if (!CanOpen()) return;
            Game1.activeClickableMenu = new CraftbookMenu(_monitor, _store.State);
            _monitor.Log("Opened Craftbook menu.", LogLevel.Info);
        }
```

- [ ] **Step 6: Build to verify no compiler errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: SUCCESS.

- [ ] **Step 7: Run all tests**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS (all 280+ tests).

- [ ] **Step 8: Commit**

```bash
git add src/TheLongestYear/UI/CookbookInteractable.cs src/TheLongestYear/UI/CraftbookInteractable.cs src/TheLongestYear.Core/GameplayConfig.cs src/TheLongestYear/UI/MenuLauncher.cs src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
feat(mod): Cookbook + Craftbook FarmHouse interactions + debug commands

Phase B §C interactables. Harmony prefixes on FarmHouse.checkAction open the
menus on configurable tile coords (tly_setcookbook/tly_setcraftbook persist to
config.json). tly_opencookbook/tly_opencraftbook open the menus directly for
debug. Default (0,0) disables each interaction until tile coords are set.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 8: Quest intros + `IndicatorRegistry` wiring in `WorldResetService` (mod-side)

**Files:**
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs`

On each reset, check if the cookbook/craftbook world objects should appear for the first time this run and fire the vanilla quest intro if so. Also re-register the `IndicatorRegistry` entries for the current run's location objects.

**Quest intro logic:** Vanilla quest text is added via `Game1.player.questLog.Add(new Quest { ... })` on the day-start after conditions are first met. Simpler approach: add a special mail flag at reset time — SMAPI's SMAPI/game will display the letter to the player on day 1. Use `Game1.player.mailForTomorrow.Add("tly.cookbook_intro")` combined with a `Data/Mail` content patch entry (no new C# needed for the letter text — use SMAPI's existing Content Patcher integration, or hard-code in the mail data at runtime via the `IAssetEditor` pattern already used in this codebase).

Actually: check whether the project has a Content Patcher setup. It does NOT (this is a C# SMAPI mod without CP assets). Use the simplest approach: add a vanilla `Quest` to the questLog directly at the END of `PerformReset` (it will show as a new quest notification on day 1 morning).

Quest objects are constructed as:
```csharp
var q = new Quest();
q.questType.Value = 1;  // type 1 = "Basic" quest (vanilla Quest.questType_Basic)
q.currentObjective = "...";
q.questDescription = "...";
q.questTitle = "A gift from the Junimos";
q.dayQuestAccepted.Value = Game1.Date.TotalDays;
q.daysLeft.Value = -1;   // -1 = no time limit
q.id.Value = /* unique negative id to avoid vanilla quest id collisions */ -9001;
Game1.player.questLog.Add(q);
```

Indicator registrations run at reset completion so the new run's FarmHouse object reference is fresh.

- [ ] **Step 1: Add the `WorldResetService` changes**

In `src/TheLongestYear/Loop/WorldResetService.cs`, at the very end of `PerformReset` (after step 11 "Bump CompletedResets" and before step 12 "Place the player home"), add:

```csharp
            // 12. Fire cookbook/craftbook quest intros on the first run after purchase.
            FireBookQuestIntros();

            // 13. Re-register indicators for the new run's location objects.
            RegisterIndicators();
```

Then add the two private methods to `WorldResetService`:

```csharp
        /// <summary>
        /// Adds a vanilla Quest to the questLog for the Cookbook and/or Craftbook the first
        /// time they appear (i.e. the upgrade was just purchased, or it's the first reset after
        /// purchase). "First time" = the indicator has not yet been dismissed.
        /// Re-registering the quest on every reset is prevented by the DismissedIndicators guard.
        /// </summary>
        private void FireBookQuestIntros()
        {
            if (_meta.HasUpgrade("cookbook_1")
                && Game1.player.HouseUpgradeLevel >= 1
                && !_meta.DismissedIndicators.Contains("tly.cookbook"))
            {
                AddIntroQuest(
                    id: -9001,
                    title: "A gift from the Junimos",
                    description: "The Junimos left a cookbook on your kitchen counter — go have a look.");
            }

            if (_meta.HasUpgrade("craftbook_1")
                && !_meta.DismissedIndicators.Contains("tly.craftbook"))
            {
                AddIntroQuest(
                    id: -9002,
                    title: "A gift from the Junimos",
                    description: "The Junimos left a craftbook on your kitchen table — go have a look.");
            }
        }

        private static void AddIntroQuest(int id, string title, string description)
        {
            // Don't add duplicate if already in the log (idempotent across same-day resets).
            foreach (var existing in Game1.player.questLog)
                if (existing.id.Value == id) return;

            var q = new Quest();
            q.questType.Value = 1;          // Quest.questType_Basic
            q.questTitle = title;
            q.currentObjective = description;
            q.questDescription = description;
            q.dayQuestAccepted.Value = Game1.Date.TotalDays;
            q.daysLeft.Value = -1;          // no time limit
            q.id.Value = id;
            Game1.player.questLog.Add(q);

            _monitor.Log($"WorldResetService: added quest intro '{title}' (id {id}).", LogLevel.Trace);
        }

        /// <summary>
        /// Re-registers indicators for the current run's FarmHouse location.
        /// Called at the end of PerformReset so the location reference is fresh.
        /// The fireplace indicator is registered here too (retroactive — it should
        /// have been shown since Plan 05 but was missing until now).
        /// </summary>
        private static void RegisterIndicators()
        {
            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player) as FarmHouse;
            if (farmHouse == null) return;

            // Cookbook: kitchen counter tile. Default coords (0,0) disables the registration
            // until tly_setcookbook is run. We don't register with (0,0) — nothing to draw over.
            // IndicatorRegistry handles location-mismatch suppression on its own.
            IndicatorRegistry.ClearRegistrations();

            // Fireplace / Season Goals board — always show until first interaction.
            // We register at the CC, not the FarmHouse, so the indicator draws only in the CC.
            CommunityCenter cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
            if (cc != null)
            {
                IndicatorRegistry.Register("tly.fireplace", cc,
                    new Vector2(/* fireplace tile X from config */ 6f, 12f),  // placeholder; real coords set by tly_setboard
                    IndicatorKind.Question);
            }

            // Cookbook indicator — drawn over the kitchen counter tile.
            // Always register so the indicator draws if the tile is configured and the upgrade is owned.
            IndicatorRegistry.Register("tly.cookbook", farmHouse,
                new Vector2(0f, 0f),   // placeholder; real coords from config (CookbookTileX/Y)
                IndicatorKind.Question);

            // Craftbook indicator — drawn over the table tile.
            IndicatorRegistry.Register("tly.craftbook", farmHouse,
                new Vector2(0f, 0f),   // placeholder; real coords from config
                IndicatorKind.Question);
        }
```

Wait — `RegisterIndicators` needs access to `_config` (for the actual tile coords) and `_meta` (to check dismissed state). Both are already fields of `WorldResetService`. Change the method to use them:

```csharp
        private void RegisterIndicators()
        {
            IndicatorRegistry.ClearRegistrations();

            // Community Center fireplace / Season Goals board.
            CommunityCenter cc = Game1.getLocationFromName("CommunityCenter") as CommunityCenter;
            if (cc != null)
            {
                IndicatorRegistry.Register("tly.fireplace", cc,
                    new Vector2(_config.SeasonGoalsBoardTileX, _config.SeasonGoalsBoardTileY),
                    IndicatorKind.Question);
            }

            FarmHouse farmHouse = Utility.getHomeOfFarmer(Game1.player) as FarmHouse;
            if (farmHouse != null)
            {
                if (_meta.HasUpgrade("cookbook_1"))
                    IndicatorRegistry.Register("tly.cookbook", farmHouse,
                        new Vector2(_config.CookbookTileX, _config.CookbookTileY),
                        IndicatorKind.Question);

                if (_meta.HasUpgrade("craftbook_1"))
                    IndicatorRegistry.Register("tly.craftbook", farmHouse,
                        new Vector2(_config.CraftbookTileX, _config.CraftbookTileY),
                        IndicatorKind.Question);
            }
        }
```

Also add `using TheLongestYear.UI;` at the top of `WorldResetService.cs` since it now references `IndicatorRegistry` and `IndicatorKind`.

- [ ] **Step 2: Build to verify no compiler errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: SUCCESS.

- [ ] **Step 3: Run all tests**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS (all 280+ tests).

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/WorldResetService.cs
git commit -m "$(cat <<'EOF'
feat(mod): fire cookbook/craftbook quest intros + register indicators at reset

Phase B §C + §D wiring. Quest ids -9001 (cookbook) and -9002 (craftbook) are TLY
private — negative ids don't collide with vanilla. Indicators re-registered each
reset (location objects refresh after loadForNewGame). Fireplace indicator also
registered here retroactively (it was missing since Plan 05).

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 9: Also register indicators on `SaveLoaded` (mod-side)

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs`

`WorldResetService.RegisterIndicators` runs at reset time. But if the player loads a save mid-run (without resetting), the indicators won't be registered. Add registration on `OnSaveLoaded` as well.

- [ ] **Step 1: Extract a re-registration helper and call it from both places**

`WorldResetService.RegisterIndicators` is a private method. The cleanest approach is to make it `internal` and expose it for `ModEntry` to call after `OnRunLoaded()`.

In `WorldResetService.cs`, change `private void RegisterIndicators()` to `internal void RegisterIndicators()`.

In `ModEntry.OnSaveLoaded`, add after `_runController.OnRunLoaded()`:

```csharp
            _reset.RegisterIndicators();
```

- [ ] **Step 2: Build to verify no compiler errors**

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj -p:EnableModDeploy=false
```

Expected: SUCCESS.

- [ ] **Step 3: Run all tests**

```bash
dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj
```

Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/TheLongestYear/Loop/WorldResetService.cs src/TheLongestYear/ModEntry.cs
git commit -m "$(cat <<'EOF'
fix(mod): also register indicators on SaveLoaded (not only at reset time)

Without this, loading a save mid-run left the IndicatorRegistry empty —
bubbles wouldn't draw until the next reset. Exposing RegisterIndicators as
internal and calling it from OnSaveLoaded covers both paths.

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Task 10: Full-stack playtest verification

**Files:** none (verification only)

This task verifies all Phase B requirements manually. Deploy the mod, load a save, and run through the checklist using the debug commands.

- [ ] **Step 1: Deploy the mod and launch Stardew Valley**

Build and deploy:

```bash
dotnet build src/TheLongestYear/TheLongestYear.csproj
```

(Without `-p:EnableModDeploy=false` so the post-build copy runs and the mod deploys to the Stardew Mods folder.)

Confirm the SMAPI log includes `The Longest Year loaded.` and no Harmony errors.

- [ ] **Step 2: Verify catalog entries appear in the shrine shop**

Run `tly_listupgrades` in the SMAPI console. Confirm:
- `cookbook_1`, `cookbook_2`, `cookbook_3` appear under `Carryover` with costs 150/350/700.
- `craftbook_1`, `craftbook_2`, `craftbook_3` appear under `Carryover` with costs 150/350/700.
- `cookbook_2` requires `cookbook_1` (prereq shown).
- `cookbook_3` requires `cookbook_2`.
- Same chain for craftbook.

- [ ] **Step 3: Buy cookbook_1 and craftbook_1 via debug**

```
tly_addjp 500
tly_buyupgrade cookbook_1
tly_buyupgrade craftbook_1
```

Expected: console logs show purchases succeeded. `tly_meta` shows both in `OwnedUpgrades`.

- [ ] **Step 4: Verify quest intros fire after reset**

Run `tly_reset`. When Spring 1 loads:
- Player's questLog contains "A gift from the Junimos" (cookbook intro) — visible in the quest journal (press F1 or in-game J).
- Player's questLog contains a second "A gift from the Junimos" (craftbook intro).

If HouseUpgradeLevel is 0 (no kitchen), the cookbook quest should NOT appear. Build the kitchen first:
```
tly_buyupgrade keep_kitchen
tly_reset
```
Now both quests should appear.

- [ ] **Step 5: Verify indicator bubbles draw**

After reset with cookbook + craftbook owned:
- Walk to the FarmHouse. If `tly_setcookbook` hasn't been run yet, run it now:
  - Walk to the kitchen counter area, face it, run `tly_setcookbook`.
  - Same for `tly_setcraftbook` on the table tile.
  - A ? bubble should appear above each configured tile.
- Verify the bubble is visible over the tile.
- Open the cookbook menu by walking to the counter and pressing the action button (or run `tly_opencookbook`).
- After opening, the ? bubble should disappear (indicator dismissed).
- Close and re-open the menu — bubble remains gone.
- Run `tly_reset` again — bubble should still be gone (dismissed state persisted).

- [ ] **Step 6: Verify slot grid and recipe banking**

Open the cookbook menu via `tly_opencookbook`. Verify:
- Header shows "Cookbook — 0 / 5 slots" (cookbook_1 = 5 slots, none banked yet).
- All rows show "[empty — click to add]".
- Click an empty row — picker sub-mode appears with currently-known cooking recipes.
- Click a recipe to bank it — it now shows in the slot.
- Header updates to "Cookbook — 1 / 5 slots".
- Click the filled row — confirm-removal dialog appears.
- Cancel — recipe stays.
- Confirm — recipe removed, slot shows "[empty]" again.

Repeat for craftbook via `tly_opencraftbook`.

- [ ] **Step 7: Verify recipe re-grant across reset**

Bank 2 recipes in the cookbook (click empty slots, pick recipes). Close menu.
Run `tly_reset`. On Spring 1, check `Farmer.cookingRecipes` via the SMAPI console:
```
tly_meta
```
The log should show `CookbookRecipes` contains the banked recipe ids.

Open the cookbook menu — banked recipes should show in slots. Confirm the player's in-game crafting/cooking menu also shows those recipes as known.

- [ ] **Step 8: Verify slot expansion with Cookbook II**

```
tly_addjp 500
tly_buyupgrade cookbook_2
tly_opencookbook
```

Header should now show "Cookbook — X / 10 slots" (10 slots from tier II).

- [ ] **Step 9: Commit the final handoff doc**

```bash
git add -A
git commit -m "$(cat <<'EOF'
chore(plan06b): integration verified — Cookbook + Craftbook complete

All Phase B §C + §D requirements verified:
- 6 catalog entries, slot counts, chains (Tasks 1-2)
- Recipe re-grant after reset (Task 3)
- IndicatorRegistry bubbles + dismiss persistence (Tasks 4, 8-9)
- CookbookMenu + CraftbookMenu slot UI, picker, confirm-remove (Tasks 5-6)
- FarmHouse interactions + config tile anchoring (Task 7)
- Quest intros on first reset after purchase (Task 8)

Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

## Self-Review Checklist

### 1. Spec coverage

| Spec requirement | Covered in task |
|---|---|
| `CookbookRecipes`, `CraftbookRecipes`, `DismissedIndicators` MetaState fields | Task 1 |
| 6 catalog entries (Cookbook I/II/III + Craftbook I/II/III, Carryover) | Task 2 |
| Slot counts 5/10/20, costs 150/350/700 | Task 2 |
| Recipe re-grant on reset (cookingRecipes/craftingRecipes value=0) | Task 3 |
| CookbookMenu: header, slot rows, empty-slot picker, filled-slot confirm-remove | Task 5 |
| CraftbookMenu: same shape, craftingRecipes | Task 6 |
| Currently-known only rule (picker shows Farmer.cookingRecipes keys not already slotted) | Tasks 5, 6 |
| Cookbook: gated by HouseUpgradeLevel >= 1 at interaction | Task 7 |
| Craftbook: no in-run prereq | Task 7 |
| Quest intros ("The Junimos left a cookbook…" / "…craftbook…") | Task 8 |
| IndicatorRegistry API: Register, Dismiss, IsActive | Task 4 |
| Dismissed indicators in MetaState.DismissedIndicators | Tasks 1, 4 |
| RenderedWorld rendering | Task 4 |
| tly.fireplace indicator (retroactive) | Task 8 |
| tly.cookbook, tly.craftbook indicators | Tasks 4, 8 |
| World object placement (kitchen counter / farmhouse table) | Task 7 (configurable tile coords) |
| Debug commands for open/set-tile | Task 7 |
| Bubble dismissed after first interaction | Tasks 5, 6 (cleanupBeforeExit / emergencyShutDown) |

### 2. Placeholder scan

- Task 4: `IndicatorRegistry` mentions source rect `(397, 489, 10, 10)` and `(410, 501, 10, 10)` as "verified against vanilla icon usage in `Object.drawWhenHeld`". These are reasonable positions in `Game1.mouseCursors` but should be visually confirmed during playtest (Task 10). This is the only approximate value in the plan — not a code placeholder, just a tunable constant.
- Task 7: Tile coords default to (0,0) and must be set via `tly_setcookbook` / `tly_setcraftbook`. This is by design (matches the `SeasonGoalsBoard` pattern) and is explicitly documented. Task 10 covers the configuration step.
- Task 8: `RegisterIndicators` uses `new Vector2(_config.SeasonGoalsBoardTileX, _config.SeasonGoalsBoardTileY)` for the fireplace — these are already set by `tly_setboard` from Plan 05. No new coords needed.

### 3. Type consistency

- `MetaState.CookbookRecipes` is `List<string>` — matches usage in `FarmerReset.Apply` (`IReadOnlyList<string>` parameter), `CookbookMenu` (`_meta.CookbookRecipes.Add/RemoveAt/Contains/Count`), and `WorldResetService` (`_meta.CookbookRecipes` passed to `FarmerReset.Apply`). Consistent.
- `MetaState.DismissedIndicators` is `HashSet<string>` — matches `IndicatorRegistry.Dismiss` (calls `.Add`) and `IsActive` (calls `.Contains`). Consistent.
- `UpgradeCatalog.CookbookSlotCount(int tier)` → `int` — matches `CookbookMenu._slotCount = UpgradeCatalog.CookbookSlotCount(tier)`. Consistent.
- `MetaState.HighestKeptTier("cookbook_", maxTier: 3)` → `int` passed to `CookbookSlotCount`. Consistent.
- `IndicatorRegistry.Register(string, GameLocation, Vector2, IndicatorKind)` — matches all call sites in `WorldResetService.RegisterIndicators`. Consistent.
- `FarmerReset.Apply(Farmer, RunBaseline, IReadOnlyList<string>, IReadOnlyList<string>)` — matches the call in `WorldResetService.PerformReset` which passes `_meta.CookbookRecipes` and `_meta.CraftbookRecipes` (both `List<string>`, which implements `IReadOnlyList<string>`). Consistent.
