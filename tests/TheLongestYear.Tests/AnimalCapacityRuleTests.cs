using System.Linq;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class AnimalCapacityRuleTests
{
    private static MetaState Owning(params string[] ids)
    {
        var meta = new MetaState { JunimoPoints = 100_000 };
        foreach (string id in ids)
            meta.OwnedUpgrades.Add(id);
        return meta;
    }

    private static string[] HerdTiers(int upTo)
        => Enumerable.Range(1, upTo).Select(t => $"herdbook_{t}").ToArray();

    [Theory]
    [InlineData("keep_coop", "coop", 4)]
    [InlineData("keep_big_coop", "coop", 8)]
    [InlineData("keep_deluxe_coop", "coop", 12)]
    [InlineData("keep_barn", "barn", 4)]
    [InlineData("keep_big_barn", "barn", 8)]
    [InlineData("keep_deluxe_barn", "barn", 12)]
    public void Capacity_is_the_vanilla_room_of_the_kept_building(string keep, string family, int expected)
        => Assert.Equal(expected, AnimalCapacityRule.Capacity(Owning(keep), family));

    [Fact]
    public void Capacity_takes_the_highest_kept_tier_and_zero_when_none()
    {
        MetaState meta = Owning("keep_coop", "keep_big_coop");
        Assert.Equal(8, AnimalCapacityRule.Capacity(meta, AnimalHousing.CoopFamily));
        Assert.Equal(0, AnimalCapacityRule.Capacity(meta, AnimalHousing.BarnFamily));
    }

    [Fact]
    public void Demand_counts_the_free_chicken_slot()
    {
        MetaState meta = Owning();
        Assert.Equal(1, AnimalCapacityRule.Demand(meta, AnimalHousing.CoopFamily));
        Assert.Equal(0, AnimalCapacityRule.Demand(meta, AnimalHousing.BarnFamily));
    }

    [Fact]
    public void Demand_sums_start_rows_and_herd_slots_per_family()
    {
        // herdbook_1..4: slots Chicken, Chicken, Cow, Cow, Duck -> coop 3, barn 2.
        // start_chicken + start_duck -> coop +2; start_cow + start_ostrich -> barn +2.
        MetaState meta = Owning(HerdTiers(4).Concat(new[]
            { "start_chicken", "start_duck", "start_cow", "start_ostrich" }).ToArray());
        Assert.Equal(5, AnimalCapacityRule.Demand(meta, AnimalHousing.CoopFamily));
        Assert.Equal(4, AnimalCapacityRule.Demand(meta, AnimalHousing.BarnFamily));
    }

    [Theory]
    [InlineData("start_chicken", "coop")]
    [InlineData("start_void_chicken", "coop")]
    [InlineData("start_duck", "coop")]
    [InlineData("start_dinosaur", "coop")]
    [InlineData("start_rabbit", "coop")]
    [InlineData("start_ostrich", "barn")]
    [InlineData("start_cow", "barn")]
    [InlineData("start_goat", "barn")]
    [InlineData("start_sheep", "barn")]
    [InlineData("start_pig", "barn")]
    [InlineData("herdbook_1", "coop")]
    [InlineData("herdbook_2", "barn")]
    [InlineData("herdbook_4", "coop")]
    [InlineData("herdbook_16", "coop")]
    [InlineData("herdbook_17", "barn")]
    public void FamilyOf_maps_animal_rows_to_their_housing(string id, string family)
        => Assert.Equal(family, AnimalCapacityRule.FamilyOf(id));

    [Theory]
    [InlineData("keep_coop")]
    [InlineData("backpack_1")]
    [InlineData("herdbook_0")]
    [InlineData("herdbook_18")]
    public void FamilyOf_is_null_for_rows_that_add_no_animal(string id)
        => Assert.Null(AnimalCapacityRule.FamilyOf(id));

    [Fact]
    public void Buy_that_fills_the_coop_exactly_is_allowed()
    {
        // Free slot + herdbook_1 + start_chicken = 3; one more makes 4 = Coop room.
        MetaState meta = Owning("keep_coop", "herdbook_1", "start_chicken");
        Assert.False(AnimalCapacityRule.WouldOverflow(meta, "start_void_chicken"));
    }

    [Fact]
    public void Buy_past_the_coop_room_overflows()
    {
        MetaState meta = Owning("keep_coop", "herdbook_1", "start_chicken", "start_void_chicken");
        Assert.True(AnimalCapacityRule.WouldOverflow(meta, "start_duck"));
        meta.OwnedUpgrades.Add("keep_big_coop");
        Assert.False(AnimalCapacityRule.WouldOverflow(meta, "start_duck"));
    }

    [Fact]
    public void Herd_tier_with_no_kept_building_of_its_family_overflows()
    {
        MetaState meta = Owning("keep_coop", "herdbook_1");
        Assert.True(AnimalCapacityRule.WouldOverflow(meta, "herdbook_2"));   // Cow, no barn kept
        meta.OwnedUpgrades.Add("keep_barn");
        Assert.False(AnimalCapacityRule.WouldOverflow(meta, "herdbook_2"));
    }

    [Fact]
    public void Ostrich_counts_against_the_barn_not_the_coop()
    {
        MetaState meta = Owning("keep_coop", "keep_barn", "start_cow", "start_goat", "start_sheep", "start_pig");
        Assert.True(AnimalCapacityRule.WouldOverflow(meta, "start_ostrich"));
        Assert.False(AnimalCapacityRule.WouldOverflow(meta, "start_chicken"));
    }

    [Fact]
    public void Families_do_not_share_room()
    {
        MetaState meta = Owning("keep_deluxe_barn", "keep_big_barn", "keep_barn");
        Assert.True(AnimalCapacityRule.WouldOverflow(meta, "start_chicken"));
    }

    [Fact]
    public void Save_already_over_room_is_only_blocked_further()
    {
        // Bought before the rule: 6 coop animals in a 4-room Coop. Nothing is taken away, more
        // coop buys are refused, barn buys still work.
        MetaState meta = Owning("keep_coop", "keep_barn", "herdbook_1", "start_chicken",
            "start_void_chicken", "start_duck", "start_dinosaur");
        Assert.Equal(6, AnimalCapacityRule.Demand(meta, AnimalHousing.CoopFamily));
        Assert.True(AnimalCapacityRule.WouldOverflow(meta, "start_rabbit"));
        Assert.False(AnimalCapacityRule.WouldOverflow(meta, "start_cow"));
    }

    [Fact]
    public void Non_animal_rows_never_overflow()
        => Assert.False(AnimalCapacityRule.WouldOverflow(Owning(), "keep_big_coop"));

    [Fact]
    public void BlockReason_names_the_family_and_is_null_when_there_is_room()
    {
        MetaState meta = Owning("keep_coop", "herdbook_1");
        Assert.Null(AnimalCapacityRule.BlockReason(meta, "start_chicken"));
        Assert.Equal(Strings.Get("shrine.no-room.barn"), AnimalCapacityRule.BlockReason(meta, "herdbook_2"));
        Assert.Equal(Strings.Get("shrine.no-room.coop"), AnimalCapacityRule.BlockReason(Owning(), "start_chicken"));
        Assert.DoesNotContain("shrine.", AnimalCapacityRule.BlockReason(Owning(), "start_chicken"));
    }

    [Fact]
    public void TryPurchase_refuses_an_overflowing_buy_and_leaves_state_unchanged()
    {
        MetaState meta = Owning("keep_coop", "herdbook_1");
        long before = meta.JunimoPoints;
        UpgradePurchase.PurchaseResult result =
            UpgradePurchase.TryPurchase(meta, UpgradeCatalog.TryGet("herdbook_2"));
        Assert.Equal(UpgradePurchase.PurchaseResult.NoAnimalRoom, result);
        Assert.Equal(before, meta.JunimoPoints);
        Assert.DoesNotContain("herdbook_2", meta.OwnedUpgrades);
    }

    [Fact]
    public void TryPurchase_allows_a_buy_that_fits()
    {
        MetaState meta = Owning("keep_coop", "keep_barn", "herdbook_1");
        Assert.Equal(UpgradePurchase.PurchaseResult.Success,
            UpgradePurchase.TryPurchase(meta, UpgradeCatalog.TryGet("herdbook_2")));
    }

    [Fact]
    public void Shop_filter_moves_an_overflowing_row_from_buyable_to_room_blocked()
    {
        MetaState meta = Owning("keep_coop", "herdbook_1");
        UpgradeDefinition tier2 = UpgradeCatalog.TryGet("herdbook_2")!;
        Assert.False(KeepShopFilter.IsBuyable(tier2, meta, _ => true));
        Assert.Contains(tier2, KeepShopFilter.RoomBlockedInCategory(UpgradeCategory.Carryover, meta, _ => true));
        // The owned tier below still shows as the owned leaf.
        Assert.Contains(UpgradeCatalog.TryGet("herdbook_1")!,
            KeepShopFilter.OwnedLeavesInCategory(UpgradeCategory.Carryover, meta, _ => true));

        meta.OwnedUpgrades.Add("keep_barn");
        Assert.True(KeepShopFilter.IsBuyable(tier2, meta, _ => true));
        Assert.DoesNotContain(tier2, KeepShopFilter.RoomBlockedInCategory(UpgradeCategory.Carryover, meta, _ => true));
    }
}
