# The Longest Year — v1 Plan 04: Donations & JP Wiring (real Community Center)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Junimo Points accrue from *real play*: hook the vanilla Community Center so every item donated awards rarity-scaled JP (plus bundle/room completion bonuses), record donations in the run ledger so the gates respond to real progress, and replace the hand-authored `CcItemCatalog` with contracts derived from the **real `Data/Bundles`** (using vanilla item ids so donations and contracts share one id-namespace).

**Architecture:** Pure, testable logic goes in `TheLongestYear.Core` (price→rarity tiers, bundle-string parsing, room→theme map, run completion-award tracking). Game-integration lives in `TheLongestYear` (the mod): Harmony postfixes on the CC donation methods feed a `DonationService` that awards JP + updates the ledger; a `BundleCatalogBuilder` reads the live `BundleData` and resolves each concrete-item requirement into a `CcItem` (rarity from price, season from crop data else year-round). Donations consume-on-submit and the room rewards are handled by vanilla — we only observe.

**Decision — donation surface:** the player donates through the **real vanilla CC** (`JunimoNoteMenu`); we hook it. The spec's "custom panel" (§15) is the weekly *planning/championing* screen, which is Plan 05 — not the donation surface. Donating to the real CC is the actual ground-truth mechanism (spec §6, §10), it already does consume-on-donate + the in-run room power-ups (spec §7), and it avoids building UI here.

**v1 simplifications (deliberate, refined later — call them out at review):**
- **Category ingredients skipped in contracts.** Bundle ingredients that are categories (negative ids, e.g. "any fish") are *not* turned into contract items (we can't pin one id). The player still donates them to the real CC for real bundle completion **and still earns JP** (the donation hook works on the actual item donated). Concrete-item requirements (the majority) become contracts. → revisit when contract fairness is tuned (Plan 06).
- **Choice bundles → first-N.** A bundle that needs *N of M* listed items contributes its **first N** items as contract requirements. → revisit Plan 06.
- **Fish/forage seasons = year-round.** Crops get accurate seasons from `Data/Crops`; minerals/bars/artisan are year-round; fish/forage are treated as year-round for now (deriving them is per-location and messy). This keeps the generator solvable but can schedule a season-locked fish off-season. Since the loop isn't experientially playtested until Plan 06/07, season accuracy is a Plan 06 fairness refinement. → revisit Plan 06.

**Tech Stack:** C# / .NET 6, SMAPI 4.x, **HarmonyLib** (already enabled via `EnableHarmony` since Plan 01, first used here), xUnit. Stardew 1.6 (PC), verified against the 1.6 decompile.

**Repo conventions:** Branch `feat/v1-plan-04-donations-jp`. Local commits only — **never push without explicit approval**. End every commit body with:
`Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>`
Files < 400 lines; tuning in `GameplayConfig`; Core references no game assemblies.

**Persistence rule (do NOT regress):** JP is added to `MetaState.JunimoPoints` in memory and committed **only** in the game's `Saving` event (already wired in `MetaStore`/`ModEntry`). Donation hooks mutate in-memory state; it persists with the next save. No eager writes.

---

## File layout after Plan 04

```
src/TheLongestYear.Core/                  (pure, unit-tested)
  RarityThresholds.cs     — NEW: price cutoffs for rarity tiers (config)
  RarityTiers.cs          — NEW: price -> Rarity
  BundleParsing.cs        — NEW: parse Data/Bundles strings; id normalize/category test
  RoomThemeMap.cs         — NEW: CC room name -> Theme (skips Vault / Joja)
  RunState.cs             — MODIFY: track already-awarded bundle/room completions
  GameplayConfig.cs       — MODIFY: add RarityThresholds
src/TheLongestYear/                        (SMAPI mod, runtime-verified)
  Donations/
    ItemRarityResolver.cs — NEW: item id -> price -> Rarity (uses ItemRegistry)
    SeasonResolver.cs     — NEW: item id -> obtainable seasons (crops else year-round)
    BundleCatalogBuilder.cs — NEW: live BundleData -> List<CcItem> (vanilla ids)
    DonationService.cs    — NEW: award JP + update ledger on donate/complete
    DonationPatches.cs    — NEW: Harmony postfixes -> DonationService
  ModEntry.cs             — MODIFY: Harmony init, build catalog on load, wire service, debug cmds
  Loop/RunController.cs   — MODIFY: generate contracts from the injected real catalog
tests/TheLongestYear.Tests/
  RarityTiersTests.cs     — NEW
  BundleParsingTests.cs   — NEW
  RoomThemeMapTests.cs    — NEW
  RunStateTests.cs        — MODIFY: completion-award tracking tests
```

---

## Task 0: Branch

- [ ] **Step 1: Create and switch to the feature branch**

From repo root `C:\Users\Jeff\Documents\Projects\Stardee Valoo\TheLongestYear`:

```bash
git checkout -b feat/v1-plan-04-donations-jp
git branch --show-current
```

Expected: `feat/v1-plan-04-donations-jp`.

- [ ] **Step 2: Confirm baseline green**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS (82 existing tests).

---

## Task 1: Core — `RarityThresholds` + `RarityTiers`

**Files:**
- Create: `src/TheLongestYear.Core/RarityThresholds.cs`
- Create: `src/TheLongestYear.Core/RarityTiers.cs`
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs`
- Test: `tests/TheLongestYear.Tests/RarityTiersTests.cs`

Rarity for JP is derived from an item's sale price via tunable cutoffs.

- [ ] **Step 1: Write the failing tests**

Create `tests/TheLongestYear.Tests/RarityTiersTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RarityTiersTests
{
    private static readonly RarityThresholds Default = new RarityThresholds();

    [Theory]
    [InlineData(0, Rarity.Common)]
    [InlineData(49, Rarity.Common)]
    [InlineData(50, Rarity.Uncommon)]
    [InlineData(199, Rarity.Uncommon)]
    [InlineData(200, Rarity.Rare)]
    [InlineData(599, Rarity.Rare)]
    [InlineData(600, Rarity.VeryRare)]
    [InlineData(5000, Rarity.VeryRare)]
    public void FromPrice_maps_price_to_tier(int price, Rarity expected)
        => Assert.Equal(expected, RarityTiers.FromPrice(price, Default));

    [Fact]
    public void FromPrice_respects_custom_thresholds()
    {
        var t = new RarityThresholds { UncommonAtLeast = 10, RareAtLeast = 20, VeryRareAtLeast = 30 };
        Assert.Equal(Rarity.Common, RarityTiers.FromPrice(9, t));
        Assert.Equal(Rarity.Uncommon, RarityTiers.FromPrice(10, t));
        Assert.Equal(Rarity.VeryRare, RarityTiers.FromPrice(99, t));
    }

    [Fact]
    public void FromPrice_null_thresholds_throws()
        => Assert.Throws<System.ArgumentNullException>(() => RarityTiers.FromPrice(1, null!));
}
```

- [ ] **Step 2: Run tests; expect FAIL** (types don't exist).

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`

- [ ] **Step 3: Create `RarityThresholds`**

Create `src/TheLongestYear.Core/RarityThresholds.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Sale-price cutoffs (gold) that bucket an item into a <see cref="Rarity"/>. All tunable.</summary>
public sealed class RarityThresholds
{
    public int UncommonAtLeast { get; set; } = 50;
    public int RareAtLeast { get; set; } = 200;
    public int VeryRareAtLeast { get; set; } = 600;
}
```

- [ ] **Step 4: Create `RarityTiers`**

Create `src/TheLongestYear.Core/RarityTiers.cs`:

```csharp
using System;

namespace TheLongestYear.Core;

/// <summary>Maps an item's sale price to a <see cref="Rarity"/> using <see cref="RarityThresholds"/>.</summary>
public static class RarityTiers
{
    public static Rarity FromPrice(int price, RarityThresholds thresholds)
    {
        if (thresholds is null)
            throw new ArgumentNullException(nameof(thresholds));

        if (price >= thresholds.VeryRareAtLeast) return Rarity.VeryRare;
        if (price >= thresholds.RareAtLeast) return Rarity.Rare;
        if (price >= thresholds.UncommonAtLeast) return Rarity.Uncommon;
        return Rarity.Common;
    }
}
```

- [ ] **Step 5: Add `RarityThresholds` to `GameplayConfig`**

In `src/TheLongestYear.Core/GameplayConfig.cs`, add inside the class:

```csharp
    /// <summary>Price cutoffs used to derive an item's rarity (and thus its JP value).</summary>
    public RarityThresholds RarityThresholds { get; set; } = new RarityThresholds();
```

- [ ] **Step 6: Run tests; expect PASS.**

- [ ] **Step 7: Commit**

```bash
git add src/TheLongestYear.Core/RarityThresholds.cs src/TheLongestYear.Core/RarityTiers.cs src/TheLongestYear.Core/GameplayConfig.cs tests/TheLongestYear.Tests/RarityTiersTests.cs
git commit -m "feat(core): add price-based RarityTiers + RarityThresholds config"
```

---

## Task 2: Core — `RunState` completion-award tracking

**Files:**
- Modify: `src/TheLongestYear.Core/RunState.cs`
- Test: `tests/TheLongestYear.Tests/RunStateTests.cs`

Completion bonuses (per bundle, per room) must be awarded exactly once per run. Track which have been awarded; clear on a new run (the world reset wipes the real CC, so per-run scope is correct).

- [ ] **Step 1: Add failing tests (append to `RunStateTests` class)**

Append to `tests/TheLongestYear.Tests/RunStateTests.cs` (inside the existing `RunStateTests` class):

```csharp
    [Fact]
    public void TryMarkBundleAwarded_is_true_once_then_false()
    {
        var run = new RunState();
        Assert.True(run.TryMarkBundleAwarded(7));
        Assert.False(run.TryMarkBundleAwarded(7));
        Assert.True(run.TryMarkBundleAwarded(8));
    }

    [Fact]
    public void TryMarkRoomAwarded_is_true_once_then_false()
    {
        var run = new RunState();
        Assert.True(run.TryMarkRoomAwarded(0));
        Assert.False(run.TryMarkRoomAwarded(0));
    }

    [Fact]
    public void BeginNewRun_clears_completion_awards()
    {
        var run = new RunState();
        run.TryMarkBundleAwarded(1);
        run.TryMarkRoomAwarded(2);

        run.BeginNewRun(seed: 5);

        Assert.True(run.TryMarkBundleAwarded(1)); // awardable again in the fresh run
        Assert.True(run.TryMarkRoomAwarded(2));
    }
```

- [ ] **Step 2: Run tests; expect FAIL** (methods don't exist).

- [ ] **Step 3: Add the fields + helpers to `RunState`**

In `src/TheLongestYear.Core/RunState.cs`, add these properties after `ChampionedThemesThisMonth`:

```csharp
    /// <summary>Bundle indices whose completion JP bonus has already been awarded this run.</summary>
    public List<int> AwardedBundleCompletions { get; set; } = new();

    /// <summary>Room/area numbers whose completion JP bonus has already been awarded this run.</summary>
    public List<int> AwardedRoomCompletions { get; set; } = new();
```

Add these methods to `RunState` (after `IsChampioned`):

```csharp
    /// <summary>Record a bundle-completion award; returns false if it was already awarded this run.</summary>
    public bool TryMarkBundleAwarded(int bundleIndex)
    {
        if (AwardedBundleCompletions.Contains(bundleIndex))
            return false;
        AwardedBundleCompletions.Add(bundleIndex);
        return true;
    }

    /// <summary>Record a room-completion award; returns false if it was already awarded this run.</summary>
    public bool TryMarkRoomAwarded(int area)
    {
        if (AwardedRoomCompletions.Contains(area))
            return false;
        AwardedRoomCompletions.Add(area);
        return true;
    }
```

In `BeginNewRun`, add these two clears alongside the existing clears:

```csharp
        AwardedBundleCompletions.Clear();
        AwardedRoomCompletions.Clear();
```

- [ ] **Step 4: Run tests; expect PASS.**

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear.Core/RunState.cs tests/TheLongestYear.Tests/RunStateTests.cs
git commit -m "feat(core): track per-run bundle/room completion awards (dedupe bonuses)"
```

---

## Task 3: Core — `BundleParsing` + `RoomThemeMap`

**Files:**
- Create: `src/TheLongestYear.Core/BundleParsing.cs`
- Create: `src/TheLongestYear.Core/RoomThemeMap.cs`
- Test: `tests/TheLongestYear.Tests/BundleParsingTests.cs`
- Test: `tests/TheLongestYear.Tests/RoomThemeMapTests.cs`

Pure parsing of the `Data/Bundles` format, item-id normalization (so bundle ids match donated `QualifiedItemId`), category detection, and the room→theme mapping.

- [ ] **Step 1: Write the failing `BundleParsing` tests**

Create `tests/TheLongestYear.Tests/BundleParsingTests.cs`:

```csharp
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleParsingTests
{
    [Fact]
    public void Parse_extracts_room_index_name_slots_and_ingredients()
    {
        // key "Room/index", value: name/reward/ingredients/color/numSlots/sprite/displayName
        var b = BundleParsing.Parse("Pantry/0", "Spring Crops/O 465 20/24 1 0 188 1 0 190 1 0 192 1 0/0/4//Spring Crops");

        Assert.Equal("Pantry", b.Room);
        Assert.Equal(0, b.Index);
        Assert.Equal("Spring Crops", b.Name);
        Assert.Equal(4, b.NumberOfSlots);
        Assert.Equal(4, b.Ingredients.Count);
        Assert.Equal("24", b.Ingredients[0].ItemRef);
        Assert.Equal(1, b.Ingredients[0].Stack);
        Assert.Equal(0, b.Ingredients[0].Quality);
    }

    [Fact]
    public void Parse_defaults_slots_to_ingredient_count_when_field_blank()
    {
        var b = BundleParsing.Parse("Crafts Room/13", "Spring Foraging/O 495 30/16 1 0 18 1 0 20 1 0 22 1 0///");
        Assert.Equal(4, b.NumberOfSlots); // blank slot field -> all ingredients
    }

    [Fact]
    public void ParseIngredients_reads_id_stack_quality_triples()
    {
        var list = BundleParsing.ParseIngredients("24 5 0 -5 1 0 (O)128 1 2").ToList();
        Assert.Equal(3, list.Count);
        Assert.Equal("24", list[0].ItemRef);
        Assert.Equal(5, list[0].Stack);
        Assert.Equal("-5", list[1].ItemRef);
        Assert.Equal("(O)128", list[2].ItemRef);
        Assert.Equal(2, list[2].Quality);
    }

    [Theory]
    [InlineData("-5", true)]
    [InlineData("-777", true)]
    [InlineData("24", false)]
    [InlineData("(O)24", false)]
    public void IsCategoryRef_true_only_for_negative_numbers(string raw, bool expected)
        => Assert.Equal(expected, BundleParsing.IsCategoryRef(raw));

    [Theory]
    [InlineData("24", "(O)24")]
    [InlineData("(O)24", "(O)24")]
    [InlineData("(BC)10", "(BC)10")]
    public void NormalizeItemId_qualifies_bare_object_ids(string raw, string expected)
        => Assert.Equal(expected, BundleParsing.NormalizeItemId(raw));
}
```

- [ ] **Step 2: Write the failing `RoomThemeMap` tests**

Create `tests/TheLongestYear.Tests/RoomThemeMapTests.cs`:

```csharp
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RoomThemeMapTests
{
    [Theory]
    [InlineData("Pantry", Theme.Farming)]
    [InlineData("Crafts Room", Theme.Foraging)]
    [InlineData("CraftsRoom", Theme.Foraging)]
    [InlineData("Fish Tank", Theme.Fishing)]
    [InlineData("Boiler Room", Theme.Mining)]
    [InlineData("Bulletin Board", Theme.Mixed)]
    public void TryGetTheme_maps_known_rooms(string room, Theme expected)
    {
        Assert.True(RoomThemeMap.TryGetTheme(room, out Theme theme));
        Assert.Equal(expected, theme);
    }

    [Theory]
    [InlineData("Vault")]
    [InlineData("Abandoned Joja Mart")]
    [InlineData("Nonsense")]
    public void TryGetTheme_rejects_non_item_rooms(string room)
        => Assert.False(RoomThemeMap.TryGetTheme(room, out _));
}
```

- [ ] **Step 3: Run tests; expect FAIL.**

- [ ] **Step 4: Create `BundleParsing`**

Create `src/TheLongestYear.Core/BundleParsing.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One ingredient requirement: an item id or category ref, a stack, and a min quality.</summary>
public readonly record struct BundleIngredient(string ItemRef, int Stack, int Quality);

/// <summary>A parsed Data/Bundles entry.</summary>
public sealed class ParsedBundle
{
    public string Room { get; }
    public int Index { get; }
    public string Name { get; }
    public IReadOnlyList<BundleIngredient> Ingredients { get; }
    public int NumberOfSlots { get; }

    public ParsedBundle(string room, int index, string name, IReadOnlyList<BundleIngredient> ingredients, int numberOfSlots)
    {
        Room = room;
        Index = index;
        Name = name;
        Ingredients = ingredients;
        NumberOfSlots = numberOfSlots;
    }
}

/// <summary>
/// Pure parsing of the vanilla Data/Bundles format. Key: "Room/index". Value (slash-delimited):
/// name / reward / ingredients(space-separated id-stack-quality triples) / color / numberOfSlots / sprite / displayName.
/// </summary>
public static class BundleParsing
{
    public static ParsedBundle Parse(string key, string value)
    {
        if (key is null) throw new ArgumentNullException(nameof(key));
        if (value is null) throw new ArgumentNullException(nameof(value));

        string[] keyParts = key.Split('/');
        string room = keyParts[0];
        int index = keyParts.Length > 1 && int.TryParse(keyParts[1], out int i) ? i : 0;

        string[] fields = value.Split('/');
        string name = fields.Length > 0 ? fields[0] : "";
        var ingredients = ParseIngredients(fields.Length > 2 ? fields[2] : "");

        int slots = ingredients.Count;
        if (fields.Length > 4 && int.TryParse(fields[4], out int parsedSlots))
            slots = parsedSlots;

        return new ParsedBundle(room, index, name, ingredients, slots);
    }

    public static IReadOnlyList<BundleIngredient> ParseIngredients(string ingredientField)
    {
        var result = new List<BundleIngredient>();
        if (string.IsNullOrWhiteSpace(ingredientField))
            return result;

        string[] parts = ingredientField.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i + 2 < parts.Length; i += 3)
        {
            int stack = int.TryParse(parts[i + 1], out int s) ? s : 1;
            int quality = int.TryParse(parts[i + 2], out int q) ? q : 0;
            result.Add(new BundleIngredient(parts[i], stack, quality));
        }
        return result;
    }

    /// <summary>A category requirement is a bare negative number (e.g. "-5" = any animal product).</summary>
    public static bool IsCategoryRef(string itemRef)
        => int.TryParse(itemRef, out int n) && n < 0;

    /// <summary>Qualify a bare object id ("24" -> "(O)24"); leave already-qualified ids ("(O)24", "(BC)10") as-is.</summary>
    public static string NormalizeItemId(string itemRef)
    {
        if (string.IsNullOrEmpty(itemRef)) return itemRef;
        return itemRef[0] == '(' ? itemRef : "(O)" + itemRef;
    }
}
```

- [ ] **Step 5: Create `RoomThemeMap`**

Create `src/TheLongestYear.Core/RoomThemeMap.cs`:

```csharp
namespace TheLongestYear.Core;

/// <summary>Maps a Community Center item-room name to its contract <see cref="Theme"/>.
/// Vault (gold) and Abandoned Joja Mart are not item rooms and are rejected.</summary>
public static class RoomThemeMap
{
    public static bool TryGetTheme(string room, out Theme theme)
    {
        switch ((room ?? "").Replace(" ", ""))
        {
            case "Pantry": theme = Theme.Farming; return true;
            case "CraftsRoom": theme = Theme.Foraging; return true;
            case "FishTank": theme = Theme.Fishing; return true;
            case "BoilerRoom": theme = Theme.Mining; return true;
            case "Bulletin":
            case "BulletinBoard": theme = Theme.Mixed; return true;
            default: theme = default; return false;
        }
    }
}
```

- [ ] **Step 6: Run tests; expect PASS.**

- [ ] **Step 7: Commit**

```bash
git add src/TheLongestYear.Core/BundleParsing.cs src/TheLongestYear.Core/RoomThemeMap.cs tests/TheLongestYear.Tests/BundleParsingTests.cs tests/TheLongestYear.Tests/RoomThemeMapTests.cs
git commit -m "feat(core): add pure Data/Bundles parsing + room->theme map"
```

---

## Task 4: Mod — `ItemRarityResolver`

**Files:**
- Create: `src/TheLongestYear/Donations/ItemRarityResolver.cs`

Resolve an item id to a `Rarity` via its sale price. Verified at runtime (touches `ItemRegistry`).

- [ ] **Step 1: Implement `ItemRarityResolver`**

Create `src/TheLongestYear/Donations/ItemRarityResolver.cs`:

```csharp
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>Resolves a (qualified) item id to a <see cref="Rarity"/> from its sale price.</summary>
    internal static class ItemRarityResolver
    {
        public static Rarity Resolve(string qualifiedItemId, RarityThresholds thresholds)
        {
            int price = 0;
            Item item = ItemRegistry.Create(qualifiedItemId, 1, 0, allowNull: true);
            if (item != null)
                price = item.salePrice();

            return RarityTiers.FromPrice(price, thresholds);
        }
    }
}
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: "Build succeeded". (If the game is running, the post-build deploy *copy* will fail with an IOException — that is fine; only the compile matters.)

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Donations/ItemRarityResolver.cs
git commit -m "feat(mod): add ItemRarityResolver (item id -> price -> Rarity)"
```

---

## Task 5: Mod — `SeasonResolver`

**Files:**
- Create: `src/TheLongestYear/Donations/SeasonResolver.cs`

Resolve an item id to the seasons it can be obtained: harvested crops use `Data/Crops` seasons; everything else is treated as year-round (v1 simplification — fish/forage accuracy is deferred to Plan 06). Verified at runtime.

- [ ] **Step 1: Implement `SeasonResolver`**

Create `src/TheLongestYear/Donations/SeasonResolver.cs`:

```csharp
using System.Collections.Generic;
using StardewValley;
using StardewValley.GameData.Crops;
using TheLongestYear.Core;
using CoreSeason = TheLongestYear.Core.Season;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Maps an item id to its obtainable seasons. Crops are derived from Data/Crops (harvest item -> seasons).
    /// All other items (fish, forage, minerals, bars, artisan) are treated as year-round for v1; fish/forage
    /// season accuracy is a Plan 06 fairness refinement.
    /// </summary>
    internal sealed class SeasonResolver
    {
        private static readonly IReadOnlySet<CoreSeason> AllSeasons =
            new HashSet<CoreSeason> { CoreSeason.Spring, CoreSeason.Summer, CoreSeason.Fall, CoreSeason.Winter };

        private readonly Dictionary<string, IReadOnlySet<CoreSeason>> _cropSeasonsByHarvestId;

        public SeasonResolver()
        {
            _cropSeasonsByHarvestId = BuildCropSeasonMap();
        }

        public IReadOnlySet<CoreSeason> SeasonsFor(string qualifiedItemId)
            => _cropSeasonsByHarvestId.TryGetValue(qualifiedItemId, out var seasons) ? seasons : AllSeasons;

        private static Dictionary<string, IReadOnlySet<CoreSeason>> BuildCropSeasonMap()
        {
            var map = new Dictionary<string, IReadOnlySet<CoreSeason>>();
            foreach (KeyValuePair<string, CropData> kvp in Game1.cropData)
            {
                CropData crop = kvp.Value;
                if (crop?.HarvestItemId == null || crop.Seasons == null || crop.Seasons.Count == 0)
                    continue;

                string harvestId = BundleParsing.NormalizeItemId(crop.HarvestItemId);
                var seasons = new HashSet<CoreSeason>();
                foreach (StardewValley.Season s in crop.Seasons)
                    seasons.Add((CoreSeason)(int)s);

                if (seasons.Count > 0)
                    map[harvestId] = seasons;
            }
            return map;
        }
    }
}
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: "Build succeeded" (deploy-copy lock OK if the game is running).

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Donations/SeasonResolver.cs
git commit -m "feat(mod): add SeasonResolver (crops from Data/Crops; else year-round)"
```

---

## Task 6: Mod — `BundleCatalogBuilder`

**Files:**
- Create: `src/TheLongestYear/Donations/BundleCatalogBuilder.cs`

Read the live `BundleData` and build `List<CcItem>` from concrete-item requirements (vanilla qualified ids), skipping category ingredients and non-item rooms. Verified at runtime.

- [ ] **Step 1: Implement `BundleCatalogBuilder`**

Create `src/TheLongestYear/Donations/BundleCatalogBuilder.cs`:

```csharp
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Builds the run's CC ground truth from the live vanilla bundle definitions. Each concrete-item
    /// requirement becomes one <see cref="CcItem"/> (vanilla qualified id, so it matches donated item ids),
    /// tagged with its room's theme, price-derived rarity, and crop-derived (else year-round) seasons.
    /// Category ingredients and non-item rooms (Vault, Joja) are skipped — see plan v1 simplifications.
    /// </summary>
    internal sealed class BundleCatalogBuilder
    {
        private readonly RarityThresholds _thresholds;
        private readonly SeasonResolver _seasons;
        private readonly IMonitor _monitor;

        public BundleCatalogBuilder(RarityThresholds thresholds, SeasonResolver seasons, IMonitor monitor)
        {
            _thresholds = thresholds;
            _seasons = seasons;
            _monitor = monitor;
        }

        public IReadOnlyList<CcItem> Build()
        {
            var items = new List<CcItem>();
            var seen = new HashSet<string>();
            int categorySkipped = 0;

            Dictionary<string, string> bundleData = Game1.netWorldState.Value.BundleData;
            foreach (KeyValuePair<string, string> kvp in bundleData)
            {
                ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
                if (!RoomThemeMap.TryGetTheme(bundle.Room, out Theme theme))
                    continue;

                int take = System.Math.Min(bundle.NumberOfSlots, bundle.Ingredients.Count);
                for (int i = 0; i < take; i++)
                {
                    string itemRef = bundle.Ingredients[i].ItemRef;
                    if (BundleParsing.IsCategoryRef(itemRef))
                    {
                        categorySkipped++;
                        continue;
                    }

                    string id = BundleParsing.NormalizeItemId(itemRef);
                    if (!seen.Add(id))
                        continue;

                    Rarity rarity = ItemRarityResolver.Resolve(id, _thresholds);
                    IReadOnlySet<Season> seasons = _seasons.SeasonsFor(id);
                    items.Add(new CcItem(id, theme, rarity, seasons));
                }
            }

            _monitor.Log(
                $"Bundle catalog built: {items.Count} concrete CC items ({categorySkipped} category ingredients skipped).",
                LogLevel.Info);
            return items;
        }
    }
}
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: "Build succeeded" (deploy-copy lock OK if the game is running).

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Donations/BundleCatalogBuilder.cs
git commit -m "feat(mod): build CcItem catalog from live vanilla BundleData (real ids)"
```

---

## Task 7: Mod — `DonationService`

**Files:**
- Create: `src/TheLongestYear/Donations/DonationService.cs`

Central handler the Harmony patches call: award per-item JP, record the donation in the ledger, and award bundle/room completion bonuses (deduped). Verified at runtime.

- [ ] **Step 1: Implement `DonationService`**

Create `src/TheLongestYear/Donations/DonationService.cs`:

```csharp
using StardewModdingAPI;
using TheLongestYear.Core;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Applies the JP economy to real Community Center activity: rarity-scaled JP per donated item,
    /// the donated id into the run ledger (so contracts/gates respond), and one-time bundle/room
    /// completion bonuses. JP lands in MetaState (committed with the next save — never eagerly).
    /// </summary>
    internal sealed class DonationService
    {
        /// <summary>Set on save load so the static Harmony patches can reach the live service.</summary>
        internal static DonationService Active;

        private readonly IMonitor _monitor;
        private readonly MetaStore _store;
        private readonly GameplayConfig _config;
        private readonly JpCalculator _jp;

        public DonationService(IMonitor monitor, MetaStore store, GameplayConfig config)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _jp = new JpCalculator(config.Jp);
        }

        private RunState Run => _store.Run;

        /// <summary>A successful donation of <paramref name="count"/> of an item to the CC.</summary>
        public void OnItemDonated(string qualifiedItemId, int count)
        {
            if (string.IsNullOrEmpty(qualifiedItemId) || count <= 0)
                return;

            Rarity rarity = ItemRarityResolver.Resolve(qualifiedItemId, _config.RarityThresholds);
            long jp = _jp.PerItem(rarity, Run.WeekOfYear) * count;
            _store.State.JunimoPoints += jp;
            Run.RecordDonation(qualifiedItemId);

            _monitor.Log(
                $"Donated {count}x {qualifiedItemId} ({rarity}) -> +{jp} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>A bundle just completed — award its one-time completion bonus.</summary>
        public void OnBundleCompleted(int bundleIndex)
        {
            if (!Run.TryMarkBundleAwarded(bundleIndex))
                return;

            _store.State.JunimoPoints += _config.Jp.BundleCompletionBonus;
            _monitor.Log(
                $"Bundle {bundleIndex} complete -> +{_config.Jp.BundleCompletionBonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }

        /// <summary>A room/area just completed — award its one-time completion bonus.</summary>
        public void OnRoomCompleted(int area)
        {
            if (!Run.TryMarkRoomAwarded(area))
                return;

            _store.State.JunimoPoints += _config.Jp.RoomCompletionBonus;
            _monitor.Log(
                $"Room {area} complete -> +{_config.Jp.RoomCompletionBonus} JP (now {_store.State.JunimoPoints}).",
                LogLevel.Info);
        }
    }
}
```

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: "Build succeeded" (deploy-copy lock OK if the game is running).

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Donations/DonationService.cs
git commit -m "feat(mod): add DonationService (per-item JP + ledger + completion bonuses)"
```

---

## Task 8: Mod — `DonationPatches` (Harmony)

**Files:**
- Create: `src/TheLongestYear/Donations/DonationPatches.cs`

Harmony postfixes that observe the real CC and forward to `DonationService.Active`. Verified at runtime.

- [ ] **Step 1: Implement `DonationPatches`**

Create `src/TheLongestYear/Donations/DonationPatches.cs`:

```csharp
using HarmonyLib;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace TheLongestYear.Donations
{
    /// <summary>
    /// Harmony postfixes on the vanilla Community Center donation path. We only observe — vanilla still
    /// consumes the item, marks the bundle, and grants the in-run room reward.
    /// </summary>
    internal static class DonationPatches
    {
        /// <summary>After an item is deposited into a bundle slot: award per-item JP + ledger.</summary>
        [HarmonyPatch(typeof(Bundle), nameof(Bundle.tryToDepositThisItem))]
        internal static class DepositPatch
        {
            // Capture the stack before deposit so the postfix can compute how many were consumed.
            private static void Prefix(Item item, out int __state) => __state = item?.Stack ?? 0;

            private static void Postfix(Item item, int __state)
            {
                if (DonationService.Active == null || item == null)
                    return;

                int consumed = __state - item.Stack;
                if (consumed > 0)
                    DonationService.Active.OnItemDonated(item.QualifiedItemId, consumed);
            }
        }

        /// <summary>After the menu confirms a bundle is complete: award the bundle bonus.</summary>
        [HarmonyPatch(typeof(JunimoNoteMenu), "checkIfBundleIsComplete")]
        internal static class BundleCompletePatch
        {
            private static void Postfix(JunimoNoteMenu __instance)
            {
                if (DonationService.Active == null)
                    return;

                Bundle bundle = __instance.currentPageBundle;
                if (bundle != null && bundle.complete)
                    DonationService.Active.OnBundleCompleted(bundle.bundleIndex);
            }
        }

        /// <summary>After a room/area is marked complete: award the room bonus.</summary>
        [HarmonyPatch(typeof(CommunityCenter), nameof(CommunityCenter.markAreaAsComplete))]
        internal static class AreaCompletePatch
        {
            private static void Postfix(int area)
                => DonationService.Active?.OnRoomCompleted(area);
        }
    }
}
```

> Note for the implementer: `checkIfBundleIsComplete` is a private method, so the patch targets it by string name (as written). If Harmony reports it cannot find a member, verify the exact name/signature against the 1.6 decompile at `C:\Users\Jeff\Documents\Projects\decompiler\stardew-valley-android\decompiled\StardewValley\StardewValley.Menus\JunimoNoteMenu.cs` (research found it at line 985) and adjust — do not guess silently.

- [ ] **Step 2: Build to confirm it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: "Build succeeded" (deploy-copy lock OK if the game is running). If a member name fails to resolve, stop and check the decompile per the note above.

- [ ] **Step 3: Commit**

```bash
git add src/TheLongestYear/Donations/DonationPatches.cs
git commit -m "feat(mod): Harmony postfixes on CC donate/bundle/room -> DonationService"
```

---

## Task 9: Mod — wire it into `ModEntry` + `RunController`

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs`
- Modify: `src/TheLongestYear/Loop/RunController.cs`

Apply Harmony, build the real catalog on save load, feed it to the contract generator, construct the donation service, and add debug commands. Verified at runtime.

- [ ] **Step 1: Let `RunController` use an injected catalog**

In `src/TheLongestYear/Loop/RunController.cs`:

Add a field alongside the other readonly fields:

```csharp
        private readonly System.Collections.Generic.IReadOnlyList<CcItem> _catalog;
```

Change the constructor to accept the catalog (add the parameter and assignment; keep the existing parameters/assignments):

```csharp
        public RunController(IMonitor monitor, MetaStore store, GameplayConfig config, WorldResetService reset,
            System.Collections.Generic.IReadOnlyList<CcItem> catalog)
        {
            _monitor = monitor;
            _store = store;
            _config = config;
            _reset = reset;
            _jp = new JpCalculator(config.Jp);
            _catalog = (catalog != null && catalog.Count > 0) ? catalog : CcItemCatalog.Items;
        }
```

Replace both `new ContractGenerator().Generate(CcItemCatalog.Items, ...)` calls (in `OnRunLoaded` and in `OnDayStarted`'s pending-reset branch) with `_catalog`:

```csharp
            _plan = new ContractGenerator().Generate(_catalog, Run.Seed);
```

(There are two such calls — update both.)

- [ ] **Step 2: Wire `ModEntry`: Harmony, catalog, donation service, commands**

In `src/TheLongestYear/ModEntry.cs`:

Add usings:

```csharp
using HarmonyLib;
using System.Collections.Generic;
using TheLongestYear.Donations;
```

Add fields alongside the others:

```csharp
        private SeasonResolver _seasonResolver;
        private IReadOnlyList<CcItem> _catalog = new List<CcItem>();
```

In `Entry`, after the existing event subscriptions, apply Harmony:

```csharp
            var harmony = new Harmony(this.ModManifest.UniqueID);
            harmony.PatchAll();
```

In `Entry`, register two debug commands (with the other `ConsoleCommands.Add` lines):

```csharp
            helper.ConsoleCommands.Add("tly_catalog", "Print the bundle-derived CC catalog summary.", this.CmdCatalog);
            helper.ConsoleCommands.Add("tly_testdonate", "Simulate a CC donation through the JP service. Usage: tly_testdonate <qualifiedId> [count]", this.CmdTestDonate);
```

In `OnSaveLoaded`, build the catalog and donation service **before** constructing the controller, and pass the catalog into the controller. Replace the controller-construction block so `OnSaveLoaded` reads:

```csharp
            _meta.Load();
            _reset = new WorldResetService(this.Monitor, _meta.State);

            _seasonResolver = new SeasonResolver();
            _catalog = new BundleCatalogBuilder(_config.RarityThresholds, _seasonResolver, this.Monitor).Build();
            DonationService.Active = new DonationService(this.Monitor, _meta, _config);

            _runController = new RunController(this.Monitor, _meta, _config, _reset, _catalog);
            _runController.OnRunLoaded();
            this.Monitor.Log(
                $"Run {_meta.Run.RunNumber} loaded ({_meta.Run.Season} {_meta.Run.DayOfMonth}). JP banked: {_meta.State.JunimoPoints}.",
                LogLevel.Info);
```

Add the two command handlers to the class:

```csharp
        private void CmdCatalog(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }

            var byTheme = new Dictionary<TheLongestYear.Core.Theme, int>();
            foreach (CcItem item in _catalog)
                byTheme[item.Theme] = byTheme.TryGetValue(item.Theme, out int n) ? n + 1 : 1;

            this.Monitor.Log($"CC catalog: {_catalog.Count} items.", LogLevel.Info);
            foreach (var kvp in byTheme)
                this.Monitor.Log($"  {kvp.Key}: {kvp.Value}", LogLevel.Info);
        }

        private void CmdTestDonate(string command, string[] args)
        {
            if (!Context.IsWorldReady) { this.Monitor.Log("Load a save first.", LogLevel.Warn); return; }
            if (args.Length < 1) { this.Monitor.Log("Usage: tly_testdonate <qualifiedId> [count]", LogLevel.Warn); return; }

            int count = args.Length > 1 && int.TryParse(args[1], out int c) ? c : 1;
            DonationService.Active?.OnItemDonated(args[0], count);
        }
```

- [ ] **Step 3: Build to confirm it compiles**

Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: "Build succeeded" (deploy-copy lock OK if the game is running).

- [ ] **Step 4: Run the full unit suite (no regressions)**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS (Plan 01–03 tests + the new Task 1/2/3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/TheLongestYear/ModEntry.cs src/TheLongestYear/Loop/RunController.cs
git commit -m "feat(mod): wire Harmony, real bundle catalog, and DonationService into ModEntry"
```

---

## Task 10: Engineering verification (solo, in-game)

**Files:** none (verification)

This confirms wiring at runtime. The developer drives it via the command-file bridge (write to `Mods\TheLongestYear\tly_commands.txt`) and reads `%APPDATA%\StardewValley\ErrorLogs\SMAPI-latest.txt`. Requires the game closed to deploy first.

- [ ] **Step 1: Deploy**

With the game closed: `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: "Build succeeded" and the mod copies to `...\Stardew Valley\Mods\TheLongestYear`.

- [ ] **Step 2: Confirm Harmony patched cleanly + catalog built**

Launch via `StardewModdingAPI.exe`, load a save. In the log, confirm:
- No Harmony patch errors at startup.
- On save load: `Bundle catalog built: N concrete CC items (...category ingredients skipped).` with a plausible N (vanilla ≈ 60–90 concrete items across the five item rooms; higher with a content mod like SVE).
- `tly_catalog` lists a non-zero count under each of the five themes.

- [ ] **Step 3: Confirm the JP service awards correctly**

Drive `tly_testdonate (O)24 5` (5 parsnips) and `tly_testdonate (O)74 1` (a Prismatic Shard — very rare). Confirm the log shows rarity-scaled JP added and `JP banked` increasing, and that `tly_runstate` shows the donated ids in the ledger.

- [ ] **Step 4: Confirm the real Harmony hook fires (one real donation)**

Walk to the Community Center in-game and donate one item to a bundle. Confirm the log shows `Donated 1x (O)... -> +N JP` from the deposit postfix (proving the Harmony hook fires on real play), and — if that donation completes a bundle/room — the corresponding completion-bonus line. (This is the one step that needs an actual in-game donation; everything else is driven solo.)

- [ ] **Step 5: Note any deviations**

If the catalog count is implausible, JP doesn't move, or the hook doesn't fire, fix forward against the log before merging. Re-deploy and re-check.

---

## Task 11: Full verification + finish the branch

**Files:** none (verification + git)

- [ ] **Step 1: Unit suite green**

Run: `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj`
Expected: PASS (Plan 01–03 + RarityTiers, BundleParsing, RoomThemeMap, RunState completion-award tests).

- [ ] **Step 2: Mod compiles + deploys clean**

Run (game closed): `dotnet build src/TheLongestYear/TheLongestYear.csproj`
Expected: "Build succeeded", deploys.

- [ ] **Step 3: Finish the development branch**

Use the `superpowers:finishing-a-development-branch` skill. Default: merge to `master` with `--no-ff` once the unit suite is green and Task 10's runtime checks pass. Do **not** push.

```bash
git checkout master
git merge --no-ff feat/v1-plan-04-donations-jp -m "merge: v1 Plan 04 — donations & JP wiring (real CC)"
```

- [ ] **Step 4: Update project memory**

Update `C:\Users\Jeff\.claude\projects\C--Users-Jeff-Documents-Projects-Stardee-Valoo\memory\project_the_longest_year.md`: Plan 04 done (real CC donation hooks → JP + ledger; bundle catalog from live `BundleData` with vanilla ids; rarity from price; crops-accurate seasons, fish/forage year-round). Note the v1 simplifications (category ingredients skipped in contracts, choice-bundle first-N, fish/forage seasons) as Plan 06 refinements. Next: Plan 05 (UI — ContractPickMenu + JunimoShrineMenu).

---

## Done criteria for Plan 04

- `dotnet test` green: `RarityTiers`, `BundleParsing`, `RoomThemeMap`, `RunState` completion-award tracking.
- `dotnet build` of the mod succeeds and deploys; Harmony patches apply with no errors.
- In-game: donating to the real CC awards rarity-scaled JP (per-item) + bundle/room completion bonuses; donated ids enter the run ledger (so gates respond); JP persists with the save.
- Contracts are generated from the **real** CC bundle data using vanilla item ids (so a donated item ticks the contract that needs it).
- Core still references no game assemblies.

## Self-review notes (spec coverage)

- **Per-item JP on donation, rarity-scaled** (spec §8) → `DonationService.OnItemDonated` + `RarityTiers`/`ItemRarityResolver` + `JpCalculator.PerItem` (Tasks 1, 4, 7).
- **Bundle/room completion bonuses, no contract reward** (spec §8) → `OnBundleCompleted`/`OnRoomCompleted` (deduped via `RunState`) on the real bundle/area hooks (Tasks 2, 7, 8). Contracts/gates never feed JP.
- **Cumulative donations tick the CC + contracts; consumed on submit; never refunded** (spec §6) → vanilla consume-on-donate (unchanged) + `RunState.RecordDonation` from the deposit hook; the world reset wipes the CC so a failed run keeps nothing (Plan 03).
- **Vanilla CC ground truth, contracts as a themed/scheduled view** (spec §10) → `BundleCatalogBuilder` over live `BundleData` feeding `ContractGenerator`; room→theme via `RoomThemeMap` (Tasks 3, 6, 9).
- **In-run room rewards fire** (spec §7) → handled by vanilla (`CommunityCenter.areaCompleteReward`); we only observe (Task 8).
- **JP banked, committed only on Saving** (spec §7) → JP into `MetaState`, persisted by existing `MetaStore`/`OnSaving` (no change needed).
- **Config in one place** (workspace rule) → `RarityThresholds` in `GameplayConfig` (Task 1).
- Deferred by design: category-ingredient contracts, choice-bundle full modelling, fish/forage season accuracy (all Plan 06 fairness pass); donation/planning UI (Plan 05); upgrades/obtainability/foresight (Plan 06); stash + narrative (Plan 07).
```
