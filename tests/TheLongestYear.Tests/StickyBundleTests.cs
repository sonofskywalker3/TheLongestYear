using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The Sticky bundle (Jeff, 2026-09-30): anything sticky a year can reach, shown six and
/// needed four so the Required Slots dial reads 3/4/5/6, with asks sized like the other created
/// bundles and read against the season the slot is due.</summary>
public class StickyBundleTests
{
    private const string Sap = "(O)92";
    private const string Sugar = "(O)245";
    private const string IceCream = "(O)233";
    private const string MinersTreat = "(O)243";
    private const string CranberrySauce = "(O)238";

    private static readonly string[] Sticky =
    {
        "(O)92", "(O)724", "(O)725", "(O)726", "(O)340", "(O)344",
        "(O)245", "(O)766", "(O)233", "(O)731", "(O)238", "(O)243",
    };

    private static DifficultyProfile Profile(DifficultySettings settings)
        => DifficultyResolver.Resolve(settings, new GameplayConfig());

    private static BundleSpec VanillaSticky()
        => new("Crafts Room", 4, "Sticky", "Sticky", "BO 114 1", 0, 1,
            new List<BundleSlotSpec> { new(Sap, 500, 0) });

    private static BundleSpec Filled()
    {
        var pools = new ItemPools
        {
            ByKind = new Dictionary<ItemKind, IReadOnlyList<PoolItem>>
            {
                [ItemKind.Other] = Sticky.Select(id => new PoolItem(id, 50, 3, Array.Empty<Season>(), Array.Empty<string>())).ToList(),
            },
        };
        return BundleSlotFiller.Fill(VanillaSticky(), new DomainMatch(PoolDomain.Recipe, null), pools,
            new BundleGenerationTuning(), new Random(7));
    }

    [Fact]
    public void Sticky_shows_six_and_needs_four()
    {
        BundleSpec filled = Filled();
        Assert.Equal(6, filled.Slots.Count);
        Assert.Equal(6, filled.Slots.Select(s => s.ItemId).Distinct().Count());
        Assert.Equal(4, filled.NumberOfSlots);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 3)]
    [InlineData(DifficultyStep.Normal, 4)]
    [InlineData(DifficultyStep.Hard, 5)]
    [InlineData(DifficultyStep.Extreme, 6)]
    public void The_required_slots_dial_reads_three_to_six(DifficultyStep step, int needs)
    {
        BundleSpec dialed = RequiredSlots.Apply(Filled(), Profile(new DifficultySettings { RequiredSlots = step }));
        Assert.Equal(needs, dialed.NumberOfSlots);
    }

    [Fact]
    public void Ice_cream_has_no_spring_basis_and_is_plentiful_from_summer()
    {
        Assert.Null(QuantityAskPass.BasisByDeadline(IceCream, Season.Spring));   // the stand is its first route
        Assert.Equal(25, QuantityAskPass.BasisByDeadline(IceCream, Season.Summer));
        Assert.Equal(25, QuantityAskPass.BasisByDeadline(IceCream, Season.Winter));   // bought in Summer, kept
    }

    [Fact]
    public void Seasonal_rows_read_the_best_season_up_to_the_deadline()
    {
        Assert.Equal(20, QuantityAskPass.BasisByDeadline(Sugar, Season.Spring));
        Assert.Equal(40, QuantityAskPass.BasisByDeadline(Sugar, Season.Fall));
        Assert.Null(QuantityAskPass.BasisByDeadline(CranberrySauce, Season.Summer));   // a Fall crop
        Assert.Null(QuantityAskPass.BasisByDeadline("(O)731", Season.Spring));          // Maple Bar
        Assert.Equal(5, QuantityAskPass.BasisByDeadline(CranberrySauce, Season.Fall));
        Assert.Equal(8, QuantityAskPass.BasisByDeadline(CranberrySauce, Season.Winter));
        Assert.Equal(4, QuantityAskPass.BasisByDeadline(MinersTreat, Season.Spring));   // Mummy drop beats cooking 3
        Assert.Equal(6, QuantityAskPass.BasisByDeadline(MinersTreat, Season.Summer));
    }

    [Fact]
    public void Every_sticky_item_carries_a_banded_ask()
    {
        Assert.All(Sticky, id => Assert.True(QuantityAskPass.Covers(id), id));
    }
}
