# Difficulty Modifiers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ten independent Easy/Normal/Hard/Extreme difficulty modifiers in GMCM, defaulting to Normal (today's exact balance), applying at the next loop reset.

**Architecture:** A pure `DifficultyResolver` turns ten step enums into a `DifficultyProfile` of concrete numbers. The profile is stamped into `MetaState` at every reset, and consumers read the stamp rather than live config, which is what makes "applies at the next reset" real. Three of the four ask-side modifiers are implemented as **pre-transforms on the generator's inputs** (a scaled `BundleGenerationTuning`, a rarity-biased `ItemPools`) rather than as changes to generation logic, so `BundleSlotFiller` and `AuthoredBundleComposer` need no edits at all. A new pure Core pass gives the Vanilla board the same stack / quality / required-slot treatment without ever changing which item a slot asks for.

**Tech Stack:** C# / .NET 6, xUnit, SMAPI, Harmony, GenericModConfigMenu.

**Spec:** `docs/superpowers/specs/2026-08-26-difficulty-modifiers-design.md`

## Global Constraints

- **Normal must be a byte-exact no-op.** Every resolver output at all-Normal equals today's config value. This is the regression guard protecting every existing save, and it gets its own test.
- **Branch rule:** this is a feature branch (`feat/difficulty-modifiers`). Do NOT bump `manifest.json`'s `Version`. The release line owns version bumps. Integrate by merge, never rebase.
- **Never push.** Local commits only. Pushing requires Jeff's explicit "yes, push."
- **No em dashes** in any string, doc, comment, or commit message. Workspace rule.
- **No `/sdcard/` paths** anywhere. Use `/storage/emulated/0/`.
- **Quality eligibility is never overridden by a difficulty step.** The built-in never-quality set (Seaweed `(O)152`, Green Algae `(O)153`, White Algae `(O)157`), `PoolTuning.QualityIneligibleItemIds`, and `ItemPools.QualityEligibleIds` all still govern at Extreme. Reintroducing gold-star Fiber is Nexus bug 1122358 and is unacceptable.
- **Money slots (`ItemId == "-1"`) are never touched** by any modifier.
- **Test command:** `dotnet test tests/TheLongestYear.Tests/TheLongestYear.Tests.csproj -v q --nologo`. Baseline before this work: **865 passing, 0 failing.**
- **Build command:** `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo`.
- Commit after every task.

---

## File Structure

**Created (all pure, all in `src/TheLongestYear.Core/`):**

| File | Responsibility |
|---|---|
| `DifficultyStep.cs` | The four-step enum plus parse/normalize helpers. |
| `DifficultySettings.cs` | The ten configured steps. Serialized into `GameplayConfig.Difficulty`. |
| `DifficultyProfile.cs` | The resolved effective values. Serialized into `MetaState.Difficulty`. |
| `DifficultyResolver.cs` | The entire balance table, as one pure function. |
| `DifficultyTuning.cs` | Produces a stack/quality-scaled clone of `BundleGenerationTuning`. |
| `RarityBias.cs` | Produces a hardness-biased clone of `ItemPools`. |
| `RequiredSlots.cs` | Adjusts a `BundleSpec`'s `NumberOfSlots`. |
| `VanillaBoardDifficultyPass.cs` | Pure `Data/Bundles` string-dictionary transform for the Vanilla board. |
| `UpgradePricing.cs` | The single home of effective upgrade cost. |

**Modified:**

| File | Change |
|---|---|
| `GameplayConfig.cs` | Add `Difficulty` property. |
| `MetaState.cs` | Add `Difficulty` stamp property. |
| `BundleClassifier.cs` | Clamp quotas to the live `NumberOfSlots` before `CreatePercentage`. |
| `JpCalculator.cs` | Accept and apply an earned multiplier. |
| `CartSlotRules.cs` | Take the starting-slot floor as a parameter. |
| `BundleHoldPricing.cs` | Accept and apply a price multiplier. |
| `SeasonPity.cs` | Read the pity profile rather than raw config. |
| `RunBaselineBuilder.cs` | Starting gold multiplier. |
| `Loop/BundleEngine.cs` | Apply rarity bias to pools; apply required-slot adjustment to generated specs. |
| `Loop/WorldResetService.cs` | Stamp the profile; pass scaled inputs to the engine; run the Vanilla post-pass. |
| `Loop/CartSlotLimitPatch.cs` | Pass the profile's starting-slot count. |
| `Donations/UpgradePurchaseService.cs`, `UI/JunimoShrineMenu.cs`, `UI/ShrinePreviewMenu.cs`, `Core/UpgradePurchase.cs` | Route cost through `UpgradePricing`. |
| `ModEntry.cs` | GMCM Difficulty section; `tly_difficulty` command. |
| `i18n/default.json` | Twenty-plus new strings. |
| `README.md`, `docs/nexus-description.bbcode` | Difficulty section, content-identical. |

---

### Task 1: The resolver and its balance table

The foundation. Everything else consumes `DifficultyProfile`.

**Files:**
- Create: `src/TheLongestYear.Core/DifficultyStep.cs`, `DifficultySettings.cs`, `DifficultyProfile.cs`, `DifficultyResolver.cs`
- Modify: `src/TheLongestYear.Core/GameplayConfig.cs`
- Test: `tests/TheLongestYear.Tests/DifficultyResolverTests.cs`

**Interfaces produced:**
```csharp
public enum DifficultyStep { Easy, Normal, Hard, Extreme }

public sealed class DifficultySettings
{
    public DifficultyStep StackSize { get; set; } = DifficultyStep.Normal;
    public DifficultyStep QualityAsks { get; set; } = DifficultyStep.Normal;
    public DifficultyStep RequiredSlots { get; set; } = DifficultyStep.Normal;
    public DifficultyStep ItemRarity { get; set; } = DifficultyStep.Normal;
    public DifficultyStep JpEarned { get; set; } = DifficultyStep.Normal;
    public DifficultyStep ShrinePrices { get; set; } = DifficultyStep.Normal;
    public DifficultyStep StartingGold { get; set; } = DifficultyStep.Normal;
    public DifficultyStep CartSlots { get; set; } = DifficultyStep.Normal;
    public DifficultyStep HoldPrices { get; set; } = DifficultyStep.Normal;
    public DifficultyStep SeasonPity { get; set; } = DifficultyStep.Normal;
    public bool IsAllNormal { get; }          // fast path for skipping passes
    public bool AsksAllNormal { get; }        // StackSize + QualityAsks + RequiredSlots
}

public sealed class PityProfile
{
    public bool Enabled { get; set; }
    public int Threshold { get; set; }
    public double QuotaStep { get; set; }
    public double QuotaFloor { get; set; }
    public int TrimPerStep { get; set; }
}

public sealed class DifficultyProfile
{
    public double StackFactor { get; set; }
    public double QualityFactor { get; set; }
    public int RequiredSlotsDelta { get; set; }   // Extreme uses RequireAllSlots instead
    public bool RequireAllSlots { get; set; }
    public double RarityBias { get; set; }
    public double JpEarnedFactor { get; set; }
    public double ShrinePriceFactor { get; set; }
    public int StartingGold { get; set; }
    public int StartingCartSlots { get; set; }
    public double HoldPriceFactor { get; set; }
    public PityProfile Pity { get; set; }
    public DifficultySettings Steps { get; set; }   // stamped for diagnostics only
    public static DifficultyProfile Normal(GameplayConfig config);
}

public static class DifficultyResolver
{
    public static DifficultyProfile Resolve(DifficultySettings settings, GameplayConfig config);
}
```

**Balance table (copy verbatim from spec section 2):**

| Modifier | Easy | Normal | Hard | Extreme |
|---|---|---|---|---|
| `StackFactor` | 0.75 | 1.0 | 1.5 | 2.0 |
| `QualityFactor` | 0.5 | 1.0 | 2.0 | 3.0 |
| `RequiredSlotsDelta` | -1 | 0 | +1 | (RequireAllSlots) |
| `RarityBias` | 0.5 | 1.0 | 1.6 | 2.4 |
| `JpEarnedFactor` | 1.5 | 1.0 | 0.75 | 0.5 |
| `ShrinePriceFactor` | 0.75 | 1.0 | 1.25 | 1.5 |
| `StartingGold` | `cfg.StartingMoney * 2.0` | `* 1.0` | `* 0.5` | `0` |
| `StartingCartSlots` | 3 | 1 | 0 | 0 |
| `HoldPriceFactor` | 0.5 | 1.0 | 2.0 | 4.0 |

Pity (from `cfg.PityThreshold` / `PityQuotaStep` / `PityQuotaFloor` / `PityTrimPerStep`, and gated by `cfg.PityEnabled`):

| | Threshold | QuotaStep | QuotaFloor | TrimPerStep | Enabled |
|---|---|---|---|---|---|
| Easy | `round(base*0.6)` | `base*1.5` | `1-(1-base)*1.2` | `round(base*1.5)` | `cfg.PityEnabled` |
| Normal | `base` | `base` | `base` | `base` | `cfg.PityEnabled` |
| Hard | `round(base*1.6)` | `base*0.5` | `1-(1-base)*0.5` | `max(1, round(base*0.5))` | `cfg.PityEnabled` |
| Extreme | `base` | `base` | `base` | `base` | **false** |

`QuotaFloor` clamps to `[0.0, 1.0]`. `Threshold` and `TrimPerStep` floor at 0 and 1 respectively.

- [ ] **Step 1: Write the failing test** in `DifficultyResolverTests.cs`

```csharp
[Fact]
public void Normal_Resolves_To_Todays_Config_Values()
{
    var cfg = new GameplayConfig();
    var p = DifficultyResolver.Resolve(new DifficultySettings(), cfg);

    Assert.Equal(1.0, p.StackFactor);
    Assert.Equal(1.0, p.QualityFactor);
    Assert.Equal(0, p.RequiredSlotsDelta);
    Assert.False(p.RequireAllSlots);
    Assert.Equal(1.0, p.RarityBias);
    Assert.Equal(1.0, p.JpEarnedFactor);
    Assert.Equal(1.0, p.ShrinePriceFactor);
    Assert.Equal(cfg.StartingMoney, p.StartingGold);
    Assert.Equal(1, p.StartingCartSlots);
    Assert.Equal(1.0, p.HoldPriceFactor);
    Assert.Equal(cfg.PityThreshold, p.Pity.Threshold);
    Assert.Equal(cfg.PityQuotaStep, p.Pity.QuotaStep);
    Assert.Equal(cfg.PityQuotaFloor, p.Pity.QuotaFloor);
    Assert.Equal(cfg.PityTrimPerStep, p.Pity.TrimPerStep);
    Assert.True(p.Pity.Enabled);
}

[Fact]
public void Extreme_Pity_Is_Disabled_But_Baselines_Are_Preserved()
{
    var cfg = new GameplayConfig();
    var p = DifficultyResolver.Resolve(
        new DifficultySettings { SeasonPity = DifficultyStep.Extreme }, cfg);
    Assert.False(p.Pity.Enabled);
    Assert.Equal(cfg.PityThreshold, p.Pity.Threshold);
}

[Fact]
public void Pity_Disabled_In_Config_Stays_Disabled_At_Easy()
{
    var cfg = new GameplayConfig { PityEnabled = false };
    var p = DifficultyResolver.Resolve(
        new DifficultySettings { SeasonPity = DifficultyStep.Easy }, cfg);
    Assert.False(p.Pity.Enabled);
}

[Theory]
[InlineData(DifficultyStep.Easy,    1000)]
[InlineData(DifficultyStep.Normal,   500)]
[InlineData(DifficultyStep.Hard,     250)]
[InlineData(DifficultyStep.Extreme,    0)]
public void StartingGold_Scales_From_Config(DifficultyStep step, int expected)
{
    var p = DifficultyResolver.Resolve(
        new DifficultySettings { StartingGold = step }, new GameplayConfig());
    Assert.Equal(expected, p.StartingGold);
}

[Fact]
public void Hard_Pity_Starts_Later_And_Eases_Less()
{
    var p = DifficultyResolver.Resolve(
        new DifficultySettings { SeasonPity = DifficultyStep.Hard }, new GameplayConfig());
    Assert.Equal(8, p.Pity.Threshold);
    Assert.Equal(0.05, p.Pity.QuotaStep, 3);
    Assert.Equal(0.75, p.Pity.QuotaFloor, 3);
    Assert.Equal(1, p.Pity.TrimPerStep);
}

[Fact]
public void AsksAllNormal_Ignores_Economy_Steps()
{
    var s = new DifficultySettings { JpEarned = DifficultyStep.Extreme };
    Assert.True(s.AsksAllNormal);
    Assert.False(s.IsAllNormal);
}
```

- [ ] **Step 2: Run it and confirm it fails to compile** (`DifficultyResolver` not defined).
- [ ] **Step 3: Implement the four files.** `GameplayConfig` gains `public DifficultySettings Difficulty { get; set; } = new();` with an XML doc naming the "defaults to Normal, applies at the next reset" contract.
- [ ] **Step 4: Run the full suite.** Expected: 865 + 6 passing.
- [ ] **Step 5: Commit** `feat(difficulty): DifficultyResolver and the ten-step balance table`

---

### Task 2: Clamp bundle quotas to the live slot count

**Prerequisite for modifier 3, and a latent-bug fix in its own right.** `BundleRequirement.CreatePercentage` throws `ArgumentOutOfRangeException` when any quota entry exceeds `numberOfSlots`. Easy's `-1` on required slots can drive `X` below a configured quota (for example `Artisan` has a Winter quota of 6 against `X=6`; at Easy `X` becomes 5 and the construction throws, taking the reset down with it). SVE-edited save data can do the same today.

**Files:**
- Modify: `src/TheLongestYear.Core/BundleClassifier.cs` (the `CreatePercentage` call around line 96)
- Test: `tests/TheLongestYear.Tests/BundleClassifierTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Quota_Above_SlotCount_Is_Clamped_Not_Thrown()
{
    // X=2, Y=4, quota asks for 3 by Winter: impossible, must clamp to 2.
    var parsed = BundleParsing.Parse("Pantry/5", "Artisan/O 12 1/348 1 0 424 1 0 426 1 0 428 1 0/2/2//Artisan");
    var quotas = new Dictionary<string, int[]> { ["Artisan"] = new[] { 0, 1, 2, 3 } };

    BundleRequirement? req = BundleClassifier.Classify(
        parsed, Theme.Farming, new Dictionary<string, Season>(), quotas);

    Assert.NotNull(req);
    Assert.Equal(BundleKind.Percentage, req!.Kind);
    Assert.All(req.CumulativeRequiredBySeason!, n => Assert.True(n <= req.NumberOfSlots));
    Assert.Equal(2, req.CumulativeRequiredBySeason![3]);
}
```

- [ ] **Step 2: Run it.** Expected: FAIL with `ArgumentOutOfRangeException`.
- [ ] **Step 3: Implement.** Immediately before `CreatePercentage`, clamp a copy of the array:

```csharp
// A configured quota may exceed this board's X: the difficulty "required slots" modifier
// can lower X below the table value, and SVE-edited save data can do the same. An
// unsatisfiable quota would brick the run, and CreatePercentage throws on it outright.
int[] clampedQuota = quota.Select(n => Math.Clamp(n, 0, parsed.NumberOfSlots)).ToArray();
```

Pass `clampedQuota` instead of `quota`. Add `using System.Linq;` if absent.

- [ ] **Step 4: Run the full suite.** Expected: all green.
- [ ] **Step 5: Commit** `fix(bundles): clamp a configured quota to the board's live slot count`

---

### Task 3: Stack and quality scaling on the Engine board

No changes to `BundleSlotFiller`. The engine already receives a `BundleGenerationTuning`; hand it a scaled clone.

**Files:**
- Create: `src/TheLongestYear.Core/DifficultyTuning.cs`
- Test: `tests/TheLongestYear.Tests/DifficultyTuningTests.cs`

**Interfaces produced:**
```csharp
public static class DifficultyTuning
{
    /// <summary>A clone of <paramref name="tuning"/> with stack numbers scaled by
    /// profile.StackFactor and quality chances by profile.QualityFactor. Returns the SAME
    /// reference when both factors are 1.0.</summary>
    public static BundleGenerationTuning Scale(BundleGenerationTuning tuning, DifficultyProfile profile);
}
```

Rules:
- Scaled stack fields: `QualityCropStack`, `CheapMinStack`, `CheapMaxStack`, `MidMinStack`, `MidMaxStack`, `DearMinStack`, `DearMaxStack`, `LargeQuantityMinStack`, `LargeQuantityMaxStack`. Each `Math.Clamp(round(v * factor), 1, 99)`.
- `LargeQuantityForageChance` scales by the same factor, clamped to `[0.0, 1.0]`.
- `SilverQualityChance` and `GoldQualityChance` scale by `QualityFactor`. Then clamp: if their sum exceeds 0.90, scale both down proportionally so the sum is exactly 0.90.
- Every other field is copied by reference (the dictionaries and lists are not mutated).
- `CheapPriceCeiling` / `MidPriceCeiling` are price bands, not stacks. Do NOT scale them.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Normal_Returns_The_Same_Instance()
{
    var t = new BundleGenerationTuning();
    var p = DifficultyResolver.Resolve(new DifficultySettings(), new GameplayConfig());
    Assert.Same(t, DifficultyTuning.Scale(t, p));
}

[Fact]
public void Hard_Scales_Stacks_And_Leaves_Price_Bands_Alone()
{
    var t = new BundleGenerationTuning();
    var p = DifficultyResolver.Resolve(
        new DifficultySettings { StackSize = DifficultyStep.Hard }, new GameplayConfig());
    var s = DifficultyTuning.Scale(t, p);

    Assert.Equal(8, s.QualityCropStack);              // 5 * 1.5 = 7.5 -> 8
    Assert.Equal(30, s.CheapMinStack);                // 20 * 1.5
    Assert.Equal(99, s.CheapMaxStack);                // 99 * 1.5 capped at 99
    Assert.Equal(t.CheapPriceCeiling, s.CheapPriceCeiling);
    Assert.Equal(t.MidPriceCeiling, s.MidPriceCeiling);
}

[Fact]
public void Stacks_Never_Fall_Below_One()
{
    var t = new BundleGenerationTuning { DearMinStack = 1 };
    var p = DifficultyResolver.Resolve(
        new DifficultySettings { StackSize = DifficultyStep.Easy }, new GameplayConfig());
    Assert.Equal(1, DifficultyTuning.Scale(t, p).DearMinStack);
}

[Fact]
public void Extreme_Quality_Is_Clamped_So_A_Plain_Ask_Stays_Possible()
{
    var t = new BundleGenerationTuning { SilverQualityChance = 0.5, GoldQualityChance = 0.5 };
    var p = DifficultyResolver.Resolve(
        new DifficultySettings { QualityAsks = DifficultyStep.Extreme }, new GameplayConfig());
    var s = DifficultyTuning.Scale(t, p);
    Assert.Equal(0.90, s.SilverQualityChance + s.GoldQualityChance, 3);
}

[Fact]
public void Hard_Quality_Doubles_The_Default_Chances()
{
    var p = DifficultyResolver.Resolve(
        new DifficultySettings { QualityAsks = DifficultyStep.Hard }, new GameplayConfig());
    var s = DifficultyTuning.Scale(new BundleGenerationTuning(), p);
    Assert.Equal(0.20, s.SilverQualityChance, 3);
    Assert.Equal(0.10, s.GoldQualityChance, 3);
}
```

- [ ] **Step 2: Run it.** Expected: FAIL, `DifficultyTuning` not defined.
- [ ] **Step 3: Implement `DifficultyTuning.Scale`.**
- [ ] **Step 4: Run the full suite.**
- [ ] **Step 5: Commit** `feat(difficulty): stack and quality scaling as a tuning pre-transform`

---

### Task 4: Rarity bias on the Engine pools

Also needs no generator changes: bias the `PoolItem.Weight` values the sampler already reads.

**Files:**
- Create: `src/TheLongestYear.Core/RarityBias.cs`
- Test: `tests/TheLongestYear.Tests/RarityBiasTests.cs`

**Interfaces produced:**
```csharp
public static class RarityBias
{
    /// <summary>Reweights every pool toward (bias &gt; 1) or away from (bias &lt; 1) harder
    /// items: weight becomes round(weight * bias^(ItemHardness.Score - 1)), floor 1.
    /// Returns the SAME reference when bias is 1.0.</summary>
    public static ItemPools Apply(ItemPools pools, double bias, RarityThresholds thresholds);
}
```

Each pool is scored with the `PoolDomain` that matches it (`Crops` -> `SeasonalCrops`, `Fish` -> `Fish`, `ArtisanGoods` -> `ArtisanGoods`, and so on) so `ItemHardness.NeedsStation` fires for artisan goods. Pools with no natural domain (`Artifacts`, `Books`, `Saplings`, `GeodeMinerals`, `Cooking`, `TapperGoods`) score with `PoolDomain.None`. `DerivedSeasonPins` and `QualityEligibleIds` are carried across unchanged.

- [ ] **Step 1: Write the failing test**

```csharp
private static PoolItem Item(string id, int price) =>
    new(id, price, 3, new List<Season>(), new List<string>());

[Fact]
public void Bias_Of_One_Returns_The_Same_Instance()
{
    var pools = new ItemPools { Crops = new List<PoolItem> { Item("(O)24", 35) } };
    Assert.Same(pools, RarityBias.Apply(pools, 1.0, new RarityThresholds()));
}

[Fact]
public void Hard_Bias_Raises_A_Rare_Items_Weight_Above_A_Common_Items()
{
    var pools = new ItemPools
    {
        Crops = new List<PoolItem> { Item("(O)cheap", 10), Item("(O)dear", 5000) }
    };
    var biased = RarityBias.Apply(pools, 1.6, new RarityThresholds());
    int cheap = biased.Crops.Single(p => p.ItemId == "(O)cheap").Weight;
    int dear  = biased.Crops.Single(p => p.ItemId == "(O)dear").Weight;
    Assert.True(dear > cheap, $"expected dear ({dear}) > cheap ({cheap})");
}

[Fact]
public void Easy_Bias_Lowers_A_Rare_Items_Weight_But_Never_Below_One()
{
    var pools = new ItemPools
    {
        Crops = new List<PoolItem> { Item("(O)cheap", 10), Item("(O)dear", 5000) }
    };
    var biased = RarityBias.Apply(pools, 0.5, new RarityThresholds());
    Assert.All(biased.Crops, p => Assert.True(p.Weight >= 1));
    Assert.True(biased.Crops.Single(p => p.ItemId == "(O)dear").Weight
              < biased.Crops.Single(p => p.ItemId == "(O)cheap").Weight);
}

[Fact]
public void Eligibility_Data_Survives_The_Rebuild()
{
    var pools = new ItemPools
    {
        Crops = new List<PoolItem> { Item("(O)24", 35) },
        QualityEligibleIds = new HashSet<string> { "(O)24" },
        DerivedSeasonPins = new Dictionary<string, Season> { ["(O)24"] = Season.Fall },
    };
    var biased = RarityBias.Apply(pools, 2.4, new RarityThresholds());
    Assert.Contains("(O)24", biased.QualityEligibleIds!);
    Assert.Equal(Season.Fall, biased.DerivedSeasonPins["(O)24"]);
}
```

- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement `RarityBias.Apply`.**
- [ ] **Step 4: Run the full suite.**
- [ ] **Step 5: Commit** `feat(difficulty): rarity bias as an ItemPools pre-transform`

---

### Task 5: Required-slot adjustment

**Files:**
- Create: `src/TheLongestYear.Core/RequiredSlots.cs`
- Test: `tests/TheLongestYear.Tests/RequiredSlotsTests.cs`

**Interfaces produced:**
```csharp
public static class RequiredSlots
{
    /// <summary>Adjusts a generated bundle's pick-X count without changing which slots are
    /// shown. Extreme requires every shown slot. Always clamped to [1, Slots.Count].
    /// Returns the SAME reference when the profile is Normal for this modifier.</summary>
    public static BundleSpec Apply(BundleSpec spec, DifficultyProfile profile);
}
```

Vault bundles must be skipped: a money bundle has a single money slot and its `NumberOfSlots` is structural. Guard on `spec.Room == "Vault"` and on any slot whose `ItemId == "-1"`.

- [ ] **Step 1: Write the failing test**

```csharp
private static BundleSpec Spec(int required, int shown, string room = "Pantry")
{
    var slots = Enumerable.Range(0, shown)
        .Select(i => new BundleSlotSpec($"(O){100 + i}", 1, 0)).ToList();
    return new BundleSpec(room, 1, "Test", "Test", "O 12 1", 0, required, slots);
}

[Fact]
public void Normal_Returns_The_Same_Instance()
{
    var s = Spec(4, 6);
    var p = DifficultyResolver.Resolve(new DifficultySettings(), new GameplayConfig());
    Assert.Same(s, RequiredSlots.Apply(s, p));
}

[Fact]
public void Hard_Requires_One_More() =>
    Assert.Equal(5, RequiredSlots.Apply(Spec(4, 6), Profile(DifficultyStep.Hard)).NumberOfSlots);

[Fact]
public void Easy_Requires_One_Fewer() =>
    Assert.Equal(3, RequiredSlots.Apply(Spec(4, 6), Profile(DifficultyStep.Easy)).NumberOfSlots);

[Fact]
public void Extreme_Requires_Every_Shown_Slot() =>
    Assert.Equal(6, RequiredSlots.Apply(Spec(4, 6), Profile(DifficultyStep.Extreme)).NumberOfSlots);

[Fact]
public void Hard_Cannot_Exceed_The_Shown_Slot_Count() =>
    Assert.Equal(3, RequiredSlots.Apply(Spec(3, 3), Profile(DifficultyStep.Hard)).NumberOfSlots);

[Fact]
public void Easy_Never_Drops_Below_One() =>
    Assert.Equal(1, RequiredSlots.Apply(Spec(1, 4), Profile(DifficultyStep.Easy)).NumberOfSlots);

[Fact]
public void Vault_Money_Bundles_Are_Untouched()
{
    var vault = new BundleSpec("Vault", 34, "2,500g", "2,500g", "", 0, 1,
        new List<BundleSlotSpec> { new("-1", 2500, 2500) });
    Assert.Same(vault, RequiredSlots.Apply(vault, Profile(DifficultyStep.Extreme)));
}

private static DifficultyProfile Profile(DifficultyStep step) =>
    DifficultyResolver.Resolve(new DifficultySettings { RequiredSlots = step }, new GameplayConfig());
```

- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement `RequiredSlots.Apply`.**
- [ ] **Step 4: Run the full suite.**
- [ ] **Step 5: Commit** `feat(difficulty): required-slot adjustment for generated bundles`

---

### Task 6: The Vanilla board post-pass

The highest-risk change in the plan, and the one that makes a modifier mean the same thing on a Standard or Remixed board. Pure, so it is fully testable without the game.

**Files:**
- Create: `src/TheLongestYear.Core/VanillaBoardDifficultyPass.cs`
- Test: `tests/TheLongestYear.Tests/VanillaBoardDifficultyPassTests.cs`

**Interfaces produced:**
```csharp
public static class VanillaBoardDifficultyPass
{
    /// <summary>Applies the stack, quality, and required-slot modifiers to a live Data/Bundles
    /// dictionary WITHOUT ever changing which item a slot asks for. Returns a new dictionary,
    /// or the SAME reference when every ask-side modifier is Normal.</summary>
    public static IDictionary<string, string> Apply(
        IDictionary<string, string> bundleData,
        DifficultyProfile profile,
        BundleGenerationTuning tuning,
        int seed,
        IReadOnlySet<string>? qualityEligibleIds = null);
}
```

Rules, per spec sections 3.1, 3.2, 3.3 and 4:
- Return the input reference unchanged when `profile.Steps.AsksAllNormal`.
- Iterate keys in **ordinal sort order** so the RNG stream is deterministic regardless of dictionary iteration order. Seed a `Random` per bundle from `seed` combined with a stable hash of the key, mirroring `BundleEngine`'s `SlotSaltPrime` idiom.
- Parse with `BundleParsing.Parse`, rebuild the value by hand in the `BundleDataWriter` field layout, preserving the reward, color, sprite, and display-name fields verbatim. **Do not round-trip through `BundleSpec`**, because that would discard the sprite field.
- **Stacks:** multiply, `MidpointRounding.AwayFromZero`, clamp to `[1, 99]`. Skip any slot whose `ItemRef` is `-1`.
- **Quality:** skip a slot whose normalized id is in the never-quality set or absent from `qualityEligibleIds` (when supplied). On Hard/Extreme roll gold at `GoldQualityChance * (factor - 1)` then silver at `SilverQualityChance * (factor - 1)`, only for slots currently at quality 0. On Easy strip a star with probability `1 - factor`.
- **Required slots:** same rule as Task 5, clamped to `[1, distinct ingredient count]`. Skip Vault rooms and money bundles entirely.

- [ ] **Step 1: Write the failing test**

```csharp
private const string ArtisanKey = "Pantry/5";
private const string Artisan =
    "Artisan/O 12 1/348 1 0 424 1 0 426 1 0 428 1 0 344 1 0 807 1 0/1/4//Artisan";

private static DifficultyProfile P(DifficultySettings s) =>
    DifficultyResolver.Resolve(s, new GameplayConfig());

[Fact]
public void All_Normal_Returns_The_Same_Instance()
{
    var data = new Dictionary<string, string> { [ArtisanKey] = Artisan };
    Assert.Same(data, VanillaBoardDifficultyPass.Apply(
        data, P(new DifficultySettings()), new BundleGenerationTuning(), 123));
}

[Fact]
public void Hard_Stacks_Scale_And_Item_Ids_Are_Untouched()
{
    var data = new Dictionary<string, string>
    {
        ["Pantry/6"] = "Fodder/O 12 1/262 10 0 178 10 0 613 3 0/2/3//Fodder"
    };
    var outp = VanillaBoardDifficultyPass.Apply(
        data, P(new DifficultySettings { StackSize = DifficultyStep.Hard }),
        new BundleGenerationTuning(), 123);

    var parsed = BundleParsing.Parse("Pantry/6", outp["Pantry/6"]);
    Assert.Equal(new[] { "262", "178", "613" }, parsed.Ingredients.Select(i => i.ItemRef));
    Assert.Equal(new[] { 15, 15, 5 }, parsed.Ingredients.Select(i => i.Stack));
}

[Fact]
public void Stacks_Are_Capped_At_99()
{
    var data = new Dictionary<string, string>
    {
        ["Crafts Room/1"] = "Construction/O 12 1/388 99 0/1/1//Construction"
    };
    var outp = VanillaBoardDifficultyPass.Apply(
        data, P(new DifficultySettings { StackSize = DifficultyStep.Extreme }),
        new BundleGenerationTuning(), 7);
    Assert.Equal(99, BundleParsing.Parse("Crafts Room/1", outp["Crafts Room/1"])
        .Ingredients.Single().Stack);
}

[Fact]
public void Money_Bundles_Are_Never_Touched()
{
    const string vault = "2,500g/O 12 1/-1 2500 2500/4/1//2,500g";
    var data = new Dictionary<string, string> { ["Vault/34"] = vault };
    var outp = VanillaBoardDifficultyPass.Apply(
        data, P(new DifficultySettings
        {
            StackSize = DifficultyStep.Extreme,
            RequiredSlots = DifficultyStep.Extreme,
        }),
        new BundleGenerationTuning(), 7);
    Assert.Equal(vault, outp["Vault/34"]);
}

[Fact]
public void Extreme_Required_Slots_Demands_Every_Ingredient()
{
    var data = new Dictionary<string, string> { [ArtisanKey] = Artisan };
    var outp = VanillaBoardDifficultyPass.Apply(
        data, P(new DifficultySettings { RequiredSlots = DifficultyStep.Extreme }),
        new BundleGenerationTuning(), 7);
    Assert.Equal(6, BundleParsing.Parse(ArtisanKey, outp[ArtisanKey]).NumberOfSlots);
}

[Fact]
public void Quality_Is_Never_Added_To_An_Ineligible_Item()
{
    // (O)152 Seaweed is in the built-in never-quality set.
    var data = new Dictionary<string, string>
    {
        ["Fish Tank/4"] = "Specialty Fish/O 12 1/152 1 0/1/1//Specialty Fish"
    };
    var outp = VanillaBoardDifficultyPass.Apply(
        data, P(new DifficultySettings { QualityAsks = DifficultyStep.Extreme }),
        new BundleGenerationTuning(), 7);
    Assert.Equal(0, BundleParsing.Parse("Fish Tank/4", outp["Fish Tank/4"])
        .Ingredients.Single().Quality);
}

[Fact]
public void Easy_Strips_Some_Existing_Quality_Stars()
{
    var slots = string.Join(" ", Enumerable.Range(0, 40).Select(i => $"{200 + i} 1 2"));
    var data = new Dictionary<string, string>
    {
        ["Pantry/7"] = $"Quality Crops/O 12 1/{slots}/3/40//Quality Crops"
    };
    var outp = VanillaBoardDifficultyPass.Apply(
        data, P(new DifficultySettings { QualityAsks = DifficultyStep.Easy }),
        new BundleGenerationTuning(), 7);
    int stillGold = BundleParsing.Parse("Pantry/7", outp["Pantry/7"])
        .Ingredients.Count(i => i.Quality == 2);
    Assert.InRange(stillGold, 1, 39);   // some stripped, not all
}

[Fact]
public void The_Pass_Is_Deterministic_For_A_Given_Seed()
{
    var settings = new DifficultySettings
    {
        StackSize = DifficultyStep.Hard,
        QualityAsks = DifficultyStep.Hard,
    };
    var a = VanillaBoardDifficultyPass.Apply(
        new Dictionary<string, string> { [ArtisanKey] = Artisan },
        P(settings), new BundleGenerationTuning(), 4242);
    var b = VanillaBoardDifficultyPass.Apply(
        new Dictionary<string, string> { [ArtisanKey] = Artisan },
        P(settings), new BundleGenerationTuning(), 4242);
    Assert.Equal(a[ArtisanKey], b[ArtisanKey]);
}
```

- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement `VanillaBoardDifficultyPass`.**
- [ ] **Step 4: Run the full suite.**
- [ ] **Step 5: Commit** `feat(difficulty): a Vanilla-board post-pass for stacks, quality and pick-X`

---

### Task 7: The MetaState stamp

**Files:**
- Modify: `src/TheLongestYear.Core/MetaState.cs`
- Test: `tests/TheLongestYear.Tests/MetaStateTests.cs`

**Interfaces produced:**
```csharp
// on MetaState
public DifficultyProfile? Difficulty { get; set; }

/// <summary>The profile this loop runs under: the stamp when present, otherwise resolved
/// live from config. Legacy saves have no stamp and resolve to all-Normal, which is
/// identical to pre-difficulty behavior.</summary>
public DifficultyProfile EffectiveDifficulty(GameplayConfig config)
    => Difficulty ?? DifficultyResolver.Resolve(config.Difficulty, config);
```

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void A_Legacy_Save_Resolves_Live_From_Config()
{
    var meta = new MetaState();
    var cfg = new GameplayConfig();
    Assert.Null(meta.Difficulty);
    Assert.Equal(cfg.StartingMoney, meta.EffectiveDifficulty(cfg).StartingGold);
}

[Fact]
public void The_Stamp_Wins_Over_A_Changed_Config()
{
    var cfg = new GameplayConfig();
    var meta = new MetaState
    {
        Difficulty = DifficultyResolver.Resolve(new DifficultySettings(), cfg)
    };
    cfg.Difficulty.StartingGold = DifficultyStep.Extreme;   // changed mid-run
    Assert.Equal(500, meta.EffectiveDifficulty(cfg).StartingGold);
}
```

- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the full suite.**
- [ ] **Step 5: Commit** `feat(difficulty): stamp the resolved profile into MetaState`

---

### Task 8: Wire the Engine generation path

**Files:**
- Modify: `src/TheLongestYear/Loop/BundleEngine.cs`, `src/TheLongestYear/Loop/WorldResetService.cs`

`BundleEngine`'s constructor gains a `DifficultyProfile` parameter, stored as `_difficulty`. Inside `Generate`, immediately after `ItemPools itemPools = new GameDataPools(_monitor).Build(...)` (line 134):

```csharp
itemPools = RarityBias.Apply(itemPools, _difficulty.RarityBias, _thresholds);
```

Every composed spec passes through `RequiredSlots.Apply(spec, _difficulty)` before it enters the `GeneratedBundleSet`.

`WorldResetService` (the `else` branch at line 509) resolves and stamps FIRST, then builds the engine with the scaled tuning:

```csharp
_meta.Difficulty = DifficultyResolver.Resolve(_config.Difficulty, _config);
var scaledTuning = DifficultyTuning.Scale(_config.PoolTuning, _meta.Difficulty);
var engine = new BundleEngine(_monitor, scaledTuning, _config.EnableNonObjectDonations,
    _config.RarityThresholds, YearTwoCrops.ExcludedFor(_meta.HasUpgrade), _meta.Difficulty);
```

**Critical:** the stamp must be written before the board is generated, and the `SaveLoaded` engine-mode re-derivation path in `ModEntry` must use `_meta.Difficulty` (the stamp), never `_config.Difficulty`. If the reload resolved live, a GMCM change mid-loop would re-derive a different board than the one in the save.

- [ ] **Step 1: Add the constructor parameter and the two call sites.**
- [ ] **Step 2: Build.** Run: `dotnet build src/TheLongestYear/TheLongestYear.csproj -v q --nologo`. Expected: no errors.
- [ ] **Step 3: Grep for every other `new BundleEngine(` and update it.** Run: `grep -rn "new BundleEngine(" --include=*.cs src`. Every hit must pass a profile.
- [ ] **Step 4: Run the full suite.**
- [ ] **Step 5: Commit** `feat(difficulty): apply the profile to Engine board generation`

---

### Task 9: Wire the Vanilla generation path

**Files:**
- Modify: `src/TheLongestYear/Loop/WorldResetService.cs` (the `if (vanillaBoard)` branch, around line 500)

After the existing "no engine write" logging, run the post-pass over the live bundle data when the ask-side modifiers are not all Normal. Read from `Game1.netWorldState.Value.BundleData`, apply, write back through `SetBundleData`.

```csharp
_meta.Difficulty = DifficultyResolver.Resolve(_config.Difficulty, _config);
if (!_meta.Difficulty.Steps.AsksAllNormal)
{
    int seed = BundleEngineSeed.For(
        unchecked((ulong)Game1.player.UniqueMultiplayerID), _meta.EffectiveBundleSeedLoop);
    var current = new Dictionary<string, string>(Game1.netWorldState.Value.BundleData);
    var adjusted = VanillaBoardDifficultyPass.Apply(
        current, _meta.Difficulty, _config.PoolTuning, seed);
    Game1.netWorldState.Value.SetBundleData(new Dictionary<string, string>(adjusted));
    _monitor.Log(
        $"Reset: Vanilla board adjusted for difficulty (stacks {_meta.Difficulty.Steps.StackSize}, " +
        $"quality {_meta.Difficulty.Steps.QualityAsks}, required slots {_meta.Difficulty.Steps.RequiredSlots}).",
        LogLevel.Info);
}
```

- [ ] **Step 1: Verify the netWorldState API.** Run: `grep -rn "SetBundleData\|netWorldState.Value.BundleData" --include=*.cs src`. Match the existing call shape exactly; if `SetBundleData` is not what the engine path uses, use whatever `BundleEngine.WriteToWorld` uses.
- [ ] **Step 2: Implement.**
- [ ] **Step 3: Build.** Expected: no errors.
- [ ] **Step 4: Run the full suite.**
- [ ] **Step 5: Commit** `feat(difficulty): run the ask-side pass over a Vanilla board at reset`

---

### Task 10: JP earned

**Files:**
- Modify: `src/TheLongestYear.Core/JpCalculator.cs`
- Test: `tests/TheLongestYear.Tests/JpCalculatorTests.cs`

Add an optional constructor parameter `double earnedMultiplier = 1.0`, applied inside the private `Scale` and inside `VaultPayment`. Both keep their existing minimum-of-1 behavior where it exists today.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void Earned_Multiplier_Scales_Per_Item_Jp()
{
    var s = new JpSettings();
    Assert.Equal(new JpCalculator(s).PerItem(Rarity.Rare, 1) / 2,
                 new JpCalculator(s, 0.5).PerItem(Rarity.Rare, 1));
}

[Fact]
public void Earned_Multiplier_Scales_Completion_Bonuses()
{
    var s = new JpSettings();
    Assert.Equal(new JpCalculator(s).RoomBonus(1) * 3 / 2,
                 new JpCalculator(s, 1.5).RoomBonus(1));
}

[Fact]
public void A_Vault_Payment_Still_Awards_At_Least_One_Jp()
{
    Assert.True(new JpCalculator(new JpSettings(), 0.5).VaultPayment(100) >= 1);
}

[Fact]
public void The_Default_Multiplier_Changes_Nothing()
{
    var s = new JpSettings();
    Assert.Equal(new JpCalculator(s).PerItem(Rarity.VeryRare, 13),
                 new JpCalculator(s, 1.0).PerItem(Rarity.VeryRare, 13));
}
```

- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement, then update every `new JpCalculator(` construction site** to pass `meta.EffectiveDifficulty(config).JpEarnedFactor`. Run `grep -rn "new JpCalculator(" --include=*.cs src` to find them all.
- [ ] **Step 4: Run the full suite.**
- [ ] **Step 5: Commit** `feat(difficulty): JP earned multiplier`

---

### Task 11: Shrine prices

Six call sites read `UpgradeDefinition.Cost`. All must go through one helper so the shown price and the charged price cannot disagree. This is the 0.14.2 Shop Discount bug class.

**Files:**
- Create: `src/TheLongestYear.Core/UpgradePricing.cs`
- Modify: `src/TheLongestYear.Core/UpgradePurchase.cs`, `src/TheLongestYear/Donations/UpgradePurchaseService.cs`, `src/TheLongestYear/UI/JunimoShrineMenu.cs`, `src/TheLongestYear/UI/ShrinePreviewMenu.cs`, `src/TheLongestYear/ModEntry.cs`
- Test: `tests/TheLongestYear.Tests/UpgradePricingTests.cs`

**Interfaces produced:**
```csharp
public static class UpgradePricing
{
    /// <summary>The JP price actually charged and displayed for an upgrade. Rounded away from
    /// zero, floor 0. A free upgrade (Cost 0) stays free at every step.</summary>
    public static long EffectiveCost(UpgradeDefinition def, double factor);
    public static long EffectiveCost(UpgradeDefinition def, DifficultyProfile profile);
}
```

`UpgradePurchase.TryPurchase` gains an optional `double priceFactor = 1.0` and uses `UpgradePricing.EffectiveCost` for both the affordability check and the deduction.

- [ ] **Step 1: Write the failing test**

```csharp
private static UpgradeDefinition Def(long cost) =>
    new("test_upgrade", UpgradeCategory.Economy, cost);

[Theory]
[InlineData(1.0, 100)]
[InlineData(0.75, 75)]
[InlineData(1.25, 125)]
[InlineData(1.5, 150)]
public void Cost_Scales_By_The_Factor(double factor, long expected) =>
    Assert.Equal(expected, UpgradePricing.EffectiveCost(Def(100), factor));

[Fact]
public void A_Free_Upgrade_Stays_Free() =>
    Assert.Equal(0, UpgradePricing.EffectiveCost(Def(0), 1.5));

[Fact]
public void Purchase_Charges_Exactly_What_It_Checks()
{
    var meta = new MetaState { JunimoPoints = 125 };
    var result = UpgradePurchase.TryPurchase(meta, Def(100), 1.25);
    Assert.Equal(UpgradePurchase.PurchaseResult.Success, result);
    Assert.Equal(0, meta.JunimoPoints);
}

[Fact]
public void Purchase_Is_Refused_When_The_Scaled_Price_Is_Unaffordable()
{
    var meta = new MetaState { JunimoPoints = 100 };
    Assert.Equal(UpgradePurchase.PurchaseResult.NotEnoughJp,
        UpgradePurchase.TryPurchase(meta, Def(100), 1.25));
    Assert.Equal(100, meta.JunimoPoints);
}
```

- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement, then update all six read sites.** Run `grep -rn "\.Cost" --include=*.cs src | grep -v "/obj/\|/bin/"` and confirm every hit either routes through `UpgradePricing` or is provably not an upgrade price.
- [ ] **Step 4: Run the full suite, then build the SMAPI project.**
- [ ] **Step 5: Commit** `feat(difficulty): shrine price multiplier through one pricing chokepoint`

---

### Task 12: Starting gold and cart slots

**Files:**
- Modify: `src/TheLongestYear.Core/CartSlotRules.cs`, `src/TheLongestYear.Core/RunBaselineBuilder.cs`, `src/TheLongestYear/Loop/WorldResetService.cs`, `src/TheLongestYear/Loop/CartSlotLimitPatch.cs`
- Test: `tests/TheLongestYear.Tests/CartSlotRulesTests.cs`

`CartSlotRules.VisibleSlots` gains an optional `int startingSlots = MinSlots` parameter used as the floor when no tier is owned; `MinSlots` stays as the default so existing callers and tests are unaffected. `WorldResetService` line 378 passes `_meta.EffectiveDifficulty(_config).StartingGold` instead of `_config.StartingMoney`.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void No_Upgrades_Shows_The_Configured_Starting_Slots() =>
    Assert.Equal(3, CartSlotRules.VisibleSlots(0, startingSlots: 3));

[Fact]
public void Zero_Starting_Slots_Means_An_Empty_Cart() =>
    Assert.Equal(0, CartSlotRules.VisibleSlots(0, startingSlots: 0));

[Fact]
public void An_Owned_Tier_Still_Wins_Over_A_Lower_Starting_Floor() =>
    Assert.Equal(5, CartSlotRules.VisibleSlots(5, startingSlots: 0));

[Fact]
public void An_Owned_Tier_Below_The_Starting_Floor_Does_Not_Shrink_The_Cart() =>
    Assert.Equal(3, CartSlotRules.VisibleSlots(2, startingSlots: 3));

[Fact]
public void The_Default_Is_Unchanged() =>
    Assert.Equal(1, CartSlotRules.VisibleSlots(0));
```

- [ ] **Step 2: Run it.** Expected: FAIL.
- [ ] **Step 3: Implement and wire both consumers.**
- [ ] **Step 4: Run the full suite, then build.**
- [ ] **Step 5: Commit** `feat(difficulty): starting gold and starting cart slots`

---

### Task 13: Hold and pity prices, and the pity profile

**Files:**
- Modify: `src/TheLongestYear.Core/BundleHoldPricing.cs`, `src/TheLongestYear.Core/SeasonPity.cs`, `src/TheLongestYear.Core/BundleHold.cs`
- Test: `tests/TheLongestYear.Tests/BundleHoldPricingTests.cs`, `tests/TheLongestYear.Tests/SeasonPityTests.cs`

`BundleHoldPricing.CostFor` gains an optional `double factor = 1.0`, applied after the curve lookup, rounded away from zero, floor 0. `SeasonPity` reads `PityProfile` values instead of the raw `GameplayConfig` fields, with the profile threaded from the caller.

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void The_First_Hold_Is_Free_At_Every_Step()
{
    var curve = new List<long> { 0, 50, 100 };
    Assert.Equal(0, BundleHoldPricing.CostFor(0, curve, 4.0));
}

[Fact]
public void Later_Holds_Scale()
{
    var curve = new List<long> { 0, 50, 100 };
    Assert.Equal(200, BundleHoldPricing.CostFor(1, curve, 4.0));
    Assert.Equal(25, BundleHoldPricing.CostFor(1, curve, 0.5));
}

[Fact]
public void A_Factor_Of_One_Changes_Nothing()
{
    var curve = new List<long> { 0, 50, 100 };
    Assert.Equal(100, BundleHoldPricing.CostFor(5, curve, 1.0));
}

[Fact]
public void Extreme_Pity_Never_Eases_A_Season()
{
    var meta = new MetaState { SeasonFailCounts = new List<int> { 99, 0, 0, 0 } };
    var cfg = new GameplayConfig();
    var profile = DifficultyResolver.Resolve(
        new DifficultySettings { SeasonPity = DifficultyStep.Extreme }, cfg);
    Assert.Equal(0, SeasonPity.EaseSteps(meta, Season.Spring, profile.Pity));
}

[Fact]
public void Pity_Counting_Still_Runs_At_Extreme()
{
    var meta = new MetaState { SeasonFailCounts = new List<int> { 99, 0, 0, 0 } };
    Assert.Equal(99, meta.SeasonFailCounts[0]);
}
```

- [ ] **Step 2: Run it.** Expected: FAIL. Adjust the `SeasonPity` assertions to whatever its real entry-point signature is; the requirement is that an Extreme profile produces zero easing while the fail counts keep incrementing.
- [ ] **Step 3: Implement.**
- [ ] **Step 4: Run the full suite, then build.**
- [ ] **Step 5: Commit** `feat(difficulty): hold and pity price scaling, and the pity profile`

---

### Task 14: The GMCM Difficulty section

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs` (after the "Features" section, before "Season pity"), `src/TheLongestYear/i18n/default.json`

Ten `AddTextOption` rows with `allowedValues: new[] { "Easy", "Normal", "Hard", "Extreme" }` and a `formatAllowedValue` that resolves `gmcm.difficulty.step.<lowercase>`. A section title plus a paragraph stating the two contracts: everything defaults to Normal, and changes apply at the next loop.

Required strings (all must exist in `default.json` or `I18nGuardTests` fails):

```
gmcm.difficulty.section
gmcm.difficulty.blurb
gmcm.difficulty.step.easy / .normal / .hard / .extreme
gmcm.difficulty.stack-size.name / .tooltip
gmcm.difficulty.quality-asks.name / .tooltip
gmcm.difficulty.required-slots.name / .tooltip
gmcm.difficulty.item-rarity.name / .tooltip
gmcm.difficulty.jp-earned.name / .tooltip
gmcm.difficulty.shrine-prices.name / .tooltip
gmcm.difficulty.starting-gold.name / .tooltip
gmcm.difficulty.cart-slots.name / .tooltip
gmcm.difficulty.hold-prices.name / .tooltip
gmcm.difficulty.season-pity.name / .tooltip
```

Copy requirements from the spec:
- `item-rarity.name` must itself read "Item rarity (TLY Custom bundles only)". The name, not just the tooltip, because a setting that silently does nothing on a Vanilla board is a bug report waiting to happen.
- `cart-slots.tooltip` must say the cart is empty until Cart Stall I is bought.
- `starting-gold.tooltip` must say it scales the Starting money value above.
- `blurb` must say changes apply at the next loop, not immediately.
- No em dashes in any of them.

- [ ] **Step 1: Add the strings to `default.json`.**
- [ ] **Step 2: Add the ten GMCM rows.**
- [ ] **Step 3: Build, then run the full suite** (the i18n guard test covers the strings).
- [ ] **Step 4: Commit** `feat(difficulty): GMCM Difficulty section with all ten modifiers`

---

### Task 15: The `tly_difficulty` diagnostics command

**Files:**
- Modify: `src/TheLongestYear/ModEntry.cs`

Read-only, shaped like `tly_netstate`. Prints the ten configured steps, the ten stamped steps, and every resolved value, plus a line saying whether the stamp or the live config is in effect. This is what a balance report from a stream viewer needs attached.

- [ ] **Step 1: Register the command** next to the other `tly_` registrations, with a description naming it read-only.
- [ ] **Step 2: Implement the handler.**
- [ ] **Step 3: Build.**
- [ ] **Step 4: Commit** `feat(difficulty): tly_difficulty read-only diagnostics command`

---

### Task 16: Player-facing copy

**Files:**
- Modify: `README.md`, `docs/nexus-description.bbcode`

Workspace rule: the README and the Nexus description must be **content-identical**, differing only in markup. Same sections, same order, same wording. Add a Difficulty section in the Configuration area of both, in the same commit.

Content, per spec section 7:
- Ten independent modifiers, four steps each, all defaulting to Normal.
- Changing nothing changes nothing.
- Changes apply at the next loop, not immediately.
- Item rarity affects TLY Custom bundles only; stack size, quality asks, and required slots work on vanilla Standard and Remixed boards too.

- [ ] **Step 1: Back up the current README and description** into `release-notes/` so Jeff can revert.
- [ ] **Step 2: Write both sections.**
- [ ] **Step 3: Diff them against each other and confirm the prose matches word for word.**
- [ ] **Step 4: Commit** `docs: difficulty modifiers in the README and the Nexus description`

**Do NOT publish.** Uploading the description to Nexus requires Jeff's explicit "yes, push" and is done by driving his signed-in Chrome, never Playwright.

---

## Self-Review

**Spec coverage:**

| Spec section | Task |
|---|---|
| 2.1 modifier 1 stack size | 3 (Engine), 6 (Vanilla) |
| 2.1 modifier 2 quality asks | 3 (Engine), 6 (Vanilla) |
| 2.1 modifier 3 required slots | 5 (Engine), 6 (Vanilla), 2 (quota clamp) |
| 2.1 modifier 4 item rarity | 4 |
| 2.2 modifier 5 JP earned | 10 |
| 2.2 modifier 6 shrine prices | 11 |
| 2.2 modifier 7 starting gold | 12 |
| 2.2 modifier 8 cart slots | 12 |
| 2.2 modifier 9 hold and pity prices | 13 |
| 2.3 modifier 10 season pity | 1 (profile), 13 (application) |
| 4 Vanilla post-pass | 6, 9 |
| 5.1 types | 1 |
| 5.2 the stamp and legacy fallback | 7, 8, 9 |
| 5.4 diagnostics | 15 |
| 6 testing | every task |
| 7 player copy | 16 |

No gaps.

**Known deviation from the spec, recorded deliberately:** spec section 3.4 describes the rarity bias as applying inside the sampler. This plan applies it to `ItemPools` before generation instead. Same effect on the sampled distribution, and it means `BundleSlotFiller` and `AuthoredBundleComposer` need no edits, which removes the risk of breaking the existing generation tests. Same reasoning applies to stacks and quality, which the spec already describes as tuning-number scaling.

**Type consistency:** `DifficultyProfile`, `DifficultySettings`, `DifficultyStep`, `PityProfile`, `DifficultyResolver.Resolve`, `DifficultyTuning.Scale`, `RarityBias.Apply`, `RequiredSlots.Apply`, `VanillaBoardDifficultyPass.Apply`, `UpgradePricing.EffectiveCost`, `MetaState.EffectiveDifficulty` are each defined once and used with the same signature everywhere.

**Ordering constraint:** Task 2 must land before Task 8 or 9, because Easy's `-1` on required slots will otherwise throw out of `CreatePercentage` during a reset.
