using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-10-08-custom-board-vanilla-only: TLY Custom boards ask only for vanilla
/// items and give only vanilla rewards; with the filter off (null) every path returns exactly what
/// it returned before.</summary>
public class VanillaOnlyBoardTests
{
    private static readonly BundleGenerationTuning Tuning = new();

    // A prefixed mod id (the SMAPI "Author.Mod_Item" shape) and an unprefixed one, which a
    // prefix rule would wrongly read as vanilla.
    private const string PrefixedModId = "FlashShifter.SVE_Fir_Wax";
    private const string UnprefixedModId = "CornucopiaPeanut";

    private static readonly IReadOnlyDictionary<string, string> EmptyBoard = new Dictionary<string, string>();

    private static RawObjectEntry Obj(int category, string type = "Basic", int price = 50)
        => new(type, category, price, false, Array.Empty<string>());

    /// <summary>Two vanilla crops (Parsnip, Cauliflower) and two modded ones, as Data/Objects and
    /// Data/Crops would carry them with a crop mod installed.</summary>
    private static ItemPools BuildMixed(IReadOnlySet<string>? vanillaOnly)
    {
        var objects = new Dictionary<string, RawObjectEntry>
        {
            ["24"] = Obj(-75), ["190"] = Obj(-75),
            [PrefixedModId] = Obj(-75), [UnprefixedModId] = Obj(-75),
            ["Moss"] = Obj(-16),
        };
        var crops = new List<RawCropEntry>
        {
            new("24", new[] { Season.Spring }, 2, "472"),
            new("190", new[] { Season.Spring }, 2, "474"),
            new(PrefixedModId, new[] { Season.Spring }, 2, "mod1"),
            new(UnprefixedModId, new[] { Season.Spring }, 2, "mod2"),
        };
        return ItemPoolBuilder.Build(
            crops, objects, new List<RawSpawnEntry>(), new List<RawSpawnEntry>(), new HashSet<string>(),
            new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            Tuning, vanillaOnlyIds: vanillaOnly);
    }

    private static IEnumerable<string> AllPoolIds(ItemPools p)
        => p.Crops.Concat(p.Fish).Concat(p.Forage).Concat(p.CrabPot).Concat(p.MonsterDrops).Concat(p.Metals)
            .Concat(p.ArtisanGoods).Concat(p.Artifacts).Concat(p.Books).Concat(p.Saplings).Concat(p.GeodeMinerals)
            .Concat(p.Cooking).Concat(p.TapperGoods).Concat(p.WinterOnly).Concat(p.ByKind.Values.SelectMany(v => v))
            .Select(i => i.ItemId);

    private static string Shape(ItemPools p)
        => string.Join("|", new[]
        {
            string.Join(",", p.Crops.Select(i => $"{i.ItemId}:{i.Weight}")),
            string.Join(",", p.ByKind.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + string.Join(";", kv.Value.Select(i => i.ItemId)))),
            string.Join(",", p.ExcludedIds.OrderBy(i => i, StringComparer.Ordinal)),
            string.Join(",", p.QualityEligibleIds.OrderBy(i => i, StringComparer.Ordinal)),
            string.Join(",", p.DerivedSeasonPins.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => kv.Key + kv.Value)),
        });

    // ---- (a) item pools ----

    [Fact]
    public void Pools_VanillaOnly_LeaveOutPrefixedAndUnprefixedModIds()
    {
        ItemPools pools = BuildMixed(VanillaItemIds.All);
        var ids = AllPoolIds(pools).ToHashSet();
        Assert.DoesNotContain("(O)" + PrefixedModId, ids);
        Assert.DoesNotContain("(O)" + UnprefixedModId, ids);
        Assert.Contains("(O)24", ids);
        Assert.Contains("(O)190", ids);
        // Exported, so a bundle's own items and flavored-slot inputs cannot re-admit them.
        Assert.Contains("(O)" + PrefixedModId, pools.ExcludedIds);
        Assert.Contains("(O)" + UnprefixedModId, pools.ExcludedIds);
    }

    [Fact]
    public void Pools_FilterOff_KeepModItems_ExactlyAsBefore()
    {
        ItemPools off = BuildMixed(null);
        var ids = AllPoolIds(off).ToHashSet();
        Assert.Contains("(O)" + PrefixedModId, ids);
        Assert.Contains("(O)" + UnprefixedModId, ids);
        Assert.DoesNotContain("(O)" + UnprefixedModId, off.ExcludedIds);
    }

    [Fact]
    public void Pools_VanillaOnly_OnAnUnmoddedGame_IsANoOp()
    {
        var objects = new Dictionary<string, RawObjectEntry> { ["24"] = Obj(-75), ["190"] = Obj(-75), ["Moss"] = Obj(-16) };
        var crops = new List<RawCropEntry> { new("24", new[] { Season.Spring }, 2, "472"), new("190", new[] { Season.Fall }, 2, "474") };
        ItemPools Run(IReadOnlySet<string>? filter) => ItemPoolBuilder.Build(
            crops, objects, new List<RawSpawnEntry>(), new List<RawSpawnEntry>(), new HashSet<string>(),
            new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            Tuning, vanillaOnlyIds: filter);
        Assert.Equal(Shape(Run(null)), Shape(Run(VanillaItemIds.All)));
    }

    [Fact]
    public void Pools_VanillaOnly_AreDeterministic()
        => Assert.Equal(Shape(BuildMixed(VanillaItemIds.All)), Shape(BuildMixed(VanillaItemIds.All)));

    // ---- (b) rewards ----

    [Theory]
    [InlineData("O 465 20", "(O)465")]
    [InlineData("R 518 1", "(O)518")]
    [InlineData("BO 10 1", "(BC)10")]
    [InlineData("BBL 13 1", "(BC)13")]
    [InlineData("F 1226 1", "(F)1226")]
    [InlineData("H 27 1", "(H)27")]
    [InlineData("W 13 1", "(W)13")]
    [InlineData("B 504 1", "(B)504")]
    [InlineData("C 1000 1", "(S)1000")]
    [InlineData("C 0 1", "(P)0")]
    public void RewardItemId_ReadsEveryRewardCode(string reward, string expected)
        => Assert.Equal(expected, VanillaOnlyBoard.RewardItemId(reward));

    [Theory]
    [InlineData("")]
    [InlineData("Q 1 1")]
    [InlineData("O")]
    public void RewardItemId_NamesNoItem_ForEmptyOrUnknown(string reward)
        => Assert.Null(VanillaOnlyBoard.RewardItemId(reward));

    [Fact]
    public void RewardPool_VanillaOnly_LeavesOutModdedRewards()
    {
        var rewards = new[] { "O 465 20", "BO 10 1", $"O {PrefixedModId} 5", $"O {UnprefixedModId} 1", "Q 1 1", "" };
        IReadOnlyList<string> pool = BundleRewardShuffle.CleanPool(rewards, VanillaItemIds.All);
        Assert.Equal(new[] { "BO 10 1", "O 465 20" }, pool);
    }

    [Fact]
    public void RewardPool_FilterOff_IsTodaysPool()
    {
        var rewards = new[] { "O 465 20", "BO 10 1", $"O {PrefixedModId} 5", "O 465 20" };
        Assert.Equal(new[] { "BO 10 1", "O 465 20", $"O {PrefixedModId} 5" }, BundleRewardShuffle.CleanPool(rewards));
        Assert.Equal(BundleRewardShuffle.CleanPool(rewards), BundleRewardShuffle.CleanPool(rewards, null));
    }

    // ---- templates ----

    private static BundleSpec Spec(string room, int index, string name, string reward, int numberOfSlots, params string[] ids)
        => new(room, index, name, name, reward, 0, numberOfSlots, ids.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());

    private static IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> Pools(params (string Room, BundleSpec[][] Positions)[] rooms)
        => rooms.ToDictionary(
            r => r.Room,
            r => (IReadOnlyList<IReadOnlyList<BundleSpec>>)r.Positions.Select(p => (IReadOnlyList<BundleSpec>)p.ToList()).ToList(),
            StringComparer.Ordinal);

    [Fact]
    public void Templates_AllVanilla_AreTheSameInstances()
    {
        BundleSpec spring = Spec("Pantry", 0, "Spring Crops", "O 465 20", 4, "24", "188", "190", "192");
        BundleSpec money = Spec("Vault", 23, "2,500g", "O 220 3", 1, "-1");
        BundleSpec anyFish = Spec("Fish Tank", 9, "Night Fishing", "R 516 1", 3, "-4", "(O)798");
        var pools = Pools(("Pantry", new[] { new[] { spring } }), ("Vault", new[] { new[] { money } }), ("Fish Tank", new[] { new[] { anyFish } }));

        var filtered = VanillaOnlyBoard.FilterRoomPools(pools, VanillaItemIds.All, VanillaBundleBoard.Standard, out int changed);

        Assert.Equal(0, changed);
        Assert.Same(spring, filtered["Pantry"][0][0]);
        Assert.Same(money, filtered["Vault"][0][0]);
        Assert.Same(anyFish, filtered["Fish Tank"][0][0]);
    }

    [Fact]
    public void Templates_FilterOff_ReturnsThePoolsThemselves()
    {
        var pools = Pools(("Pantry", new[] { new[] { Spec("Pantry", 0, "Spring Crops", "O 465 20", 4, PrefixedModId) } }));
        Assert.Same(pools, VanillaOnlyBoard.FilterRoomPools(pools, null, VanillaBundleBoard.Standard, out int changed));
        Assert.Equal(0, changed);
    }

    [Fact]
    public void Templates_ModdedSlotsDropped_CountsClamped_ModdedRewardSwappedForVanillas()
    {
        BundleSpec mixed = Spec("Pantry", 0, "Spring Crops", $"O {UnprefixedModId} 5", 4, "24", PrefixedModId, UnprefixedModId, "190")
            with { PickCount = 4 };
        var pools = Pools(("Pantry", new[] { new[] { mixed } }));

        var filtered = VanillaOnlyBoard.FilterRoomPools(pools, VanillaItemIds.All, VanillaBundleBoard.Standard, out int changed);

        BundleSpec stripped = Assert.Single(Assert.Single(filtered["Pantry"]));
        Assert.Equal(1, changed);
        Assert.Equal(new[] { "24", "190" }, stripped.Slots.Select(s => s.ItemId));
        Assert.Equal(2, stripped.NumberOfSlots);
        Assert.Equal(2, stripped.PickCount);
        // Vanilla's own Spring Crops reward (Pantry/0 on the baked standard board).
        Assert.Equal(VanillaOnlyBoard.VanillaRewardFor("Pantry", 0, VanillaBundleBoard.Standard), stripped.RewardField);
        Assert.True(VanillaOnlyBoard.IsVanillaReward(stripped.RewardField, VanillaItemIds.All));
    }

    [Fact]
    public void Templates_AllModdedCandidate_Dropped_WhenThePositionHasAnother()
    {
        BundleSpec modded = Spec("Pantry", 0, "Mod Crops", "O 465 20", 2, PrefixedModId, UnprefixedModId);
        BundleSpec vanilla = Spec("Pantry", 0, "Spring Crops", "O 465 20", 4, "24", "188", "190", "192");
        var pools = Pools(("Pantry", new[] { new[] { modded, vanilla } }));

        var filtered = VanillaOnlyBoard.FilterRoomPools(pools, VanillaItemIds.All, VanillaBundleBoard.Standard, out int changed);

        Assert.Same(vanilla, Assert.Single(Assert.Single(filtered["Pantry"])));
        Assert.Equal(1, changed);
    }

    [Fact]
    public void Templates_PositionLeftEmpty_TakesVanillasOwnBundleForThatKey()
    {
        BundleSpec modded = Spec("Pantry", 0, "Mod Crops", "O 465 20", 2, PrefixedModId, UnprefixedModId);
        var pools = Pools(("Pantry", new[] { new[] { modded } }));

        var filtered = VanillaOnlyBoard.FilterRoomPools(pools, VanillaItemIds.All, VanillaBundleBoard.Standard, out _);

        BundleSpec fallback = Assert.Single(Assert.Single(filtered["Pantry"]));
        BundleSpec expected = BundleParsing.ToSpec("Pantry/0", VanillaBundleBoard.Standard["Pantry/0"]);
        Assert.Equal(expected.Name, fallback.Name);
        Assert.Equal(expected.Slots, fallback.Slots);
        Assert.All(fallback.Slots, s => Assert.True(VanillaOnlyBoard.IsVanillaIngredient(s.ItemId, VanillaItemIds.All)));
    }

    [Fact]
    public void Templates_PositionLeftEmpty_OnAModAddedKey_IsDropped()
    {
        BundleSpec modded = Spec("Mod Room", 900, "Mod Crops", "O 465 20", 1, PrefixedModId);
        var pools = Pools(("Mod Room", new[] { new[] { modded } }));
        var filtered = VanillaOnlyBoard.FilterRoomPools(pools, VanillaItemIds.All, EmptyBoard, out _);
        Assert.False(filtered.ContainsKey("Mod Room"));
    }

    [Fact]
    public void Templates_ModdedReward_OnAModAddedKey_GetsTheFallback()
    {
        BundleSpec spec = Spec("Mod Room", 900, "Crops", $"BO {PrefixedModId} 1", 1, "24");
        BundleSpec? stripped = VanillaOnlyBoard.Strip(spec, VanillaItemIds.All, EmptyBoard);
        Assert.Equal(VanillaOnlyBoard.FallbackReward, stripped!.RewardField);
        Assert.True(VanillaOnlyBoard.IsVanillaReward(VanillaOnlyBoard.FallbackReward, VanillaItemIds.All));
    }

    [Fact]
    public void Templates_Filtered_AreDeterministic()
    {
        var pools = Pools(("Pantry", new[]
        {
            new[] { Spec("Pantry", 0, "Spring Crops", $"O {PrefixedModId} 1", 4, "24", PrefixedModId, "190") },
            new[] { Spec("Pantry", 1, "Mod", "O 465 20", 1, UnprefixedModId) },
        }));
        string Render(IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> p)
            => string.Join("\n", p.OrderBy(r => r.Key, StringComparer.Ordinal).SelectMany(r => r.Value).SelectMany(c => c)
                .Select(s => $"{s.Room}/{s.Index}/{s.Name}/{s.RewardField}/{s.NumberOfSlots}/{string.Join(" ", s.Slots.Select(x => x.ItemId))}"));
        Assert.Equal(
            Render(VanillaOnlyBoard.FilterRoomPools(pools, VanillaItemIds.All, VanillaBundleBoard.Standard, out _)),
            Render(VanillaOnlyBoard.FilterRoomPools(pools, VanillaItemIds.All, VanillaBundleBoard.Standard, out _)));
    }

    // ---- the baked data agrees with what the mod already treats as vanilla ----

    [Fact]
    public void BakedSet_CoversTheStandardBoard_ItsRewards_AndTheAuthoredBundles()
    {
        Assert.NotEmpty(VanillaBundleBoard.Standard);
        foreach (KeyValuePair<string, string> entry in VanillaBundleBoard.Standard)
        {
            BundleSpec spec = BundleParsing.ToSpec(entry.Key, entry.Value);
            Assert.Same(spec, VanillaOnlyBoard.Strip(spec, VanillaItemIds.All, VanillaBundleBoard.Standard));
        }
        foreach (AuthoredBundleDef def in AuthoredBundleCatalog.All)
        {
            Assert.True(VanillaOnlyBoard.IsVanillaReward(def.RewardField, VanillaItemIds.All), def.Name);
            foreach (string id in def.FixedItemIds ?? Array.Empty<string>())
                Assert.True(VanillaItemIds.Contains(id), $"{def.Name}: {id}");
        }
        foreach (string id in ItemPoolBuilder.BuiltInExcludedItemIds.Where(id => id.StartsWith("(O)", StringComparison.Ordinal) && !id.Contains('.')))
            Assert.True(VanillaItemIds.Contains(id), id);
    }

    [Theory]
    [InlineData("(O)Moss")]
    [InlineData("(O)Goby")]
    [InlineData("(O)SmokedFish")]
    [InlineData("(O)DriedFruit")]
    [InlineData("(BC)Dehydrator")]
    [InlineData("(H)27")]
    [InlineData("(W)13")]
    [InlineData("24")]
    public void BakedSet_Knows16StringIds_AndBareObjectIds(string id)
        => Assert.True(VanillaItemIds.Contains(id));

    [Theory]
    [InlineData("(O)" + PrefixedModId)]
    [InlineData("(O)" + UnprefixedModId)]
    [InlineData(UnprefixedModId)]
    public void BakedSet_RejectsModIds(string id)
        => Assert.False(VanillaItemIds.Contains(id));
}
