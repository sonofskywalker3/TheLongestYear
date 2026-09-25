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
    public void HerdDemand_counts_the_free_chicken_slot()
    {
        MetaState meta = Owning();
        Assert.Equal(1, AnimalCapacityRule.HerdDemand(meta, AnimalHousing.CoopFamily));
        Assert.Equal(0, AnimalCapacityRule.HerdDemand(meta, AnimalHousing.BarnFamily));
    }

    [Fact]
    public void Herd_and_start_demand_are_counted_separately_per_family()
    {
        // herdbook_1..4: slots Chicken, Chicken, Cow, Cow, Duck -> coop 3, barn 2.
        // start_chicken + start_duck -> coop 2; start_cow + start_ostrich -> barn 2.
        MetaState meta = Owning(HerdTiers(4).Concat(new[]
            { "start_chicken", "start_duck", "start_cow", "start_ostrich" }).ToArray());
        Assert.Equal(3, AnimalCapacityRule.HerdDemand(meta, AnimalHousing.CoopFamily));
        Assert.Equal(2, AnimalCapacityRule.HerdDemand(meta, AnimalHousing.BarnFamily));
        Assert.Equal(2, AnimalCapacityRule.StartDemand(meta, AnimalHousing.CoopFamily));
        Assert.Equal(2, AnimalCapacityRule.StartDemand(meta, AnimalHousing.BarnFamily));
    }

    [Fact]
    public void DemandFor_a_herd_tier_counts_only_herd_slots_and_a_start_row_counts_both()
    {
        MetaState meta = Owning(HerdTiers(4).Concat(new[] { "start_chicken", "start_duck" }).ToArray());
        Assert.Equal(3, AnimalCapacityRule.DemandFor(meta, "herdbook_5"));
        Assert.Equal(5, AnimalCapacityRule.DemandFor(meta, "start_rabbit"));
        Assert.Equal(0, AnimalCapacityRule.DemandFor(meta, "keep_coop"));
    }

    [Fact]
    public void Herd_tier_ignores_start_rows()
    {
        // Free slot + three coop start rows fill 4 of the Coop's 4 in the old count; the Herd
        // Book only counts its own slots (free slot + tier 1 = 2), so the tier is allowed.
        MetaState meta = Owning("keep_coop", "start_chicken", "start_void_chicken", "start_duck");
        Assert.False(AnimalCapacityRule.WouldOverflow(meta, "herdbook_1"));
    }

    [Fact]
    public void Herd_tier_is_refused_when_herd_slots_alone_fill_the_room()
    {
        // herdbook_1..7: coop slots 0, 1, 4, 5 = 4 = Coop room; herdbook_8 is a Rabbit (coop).
        MetaState meta = Owning(HerdTiers(7).Concat(new[] { "keep_coop", "keep_barn" }).ToArray());
        Assert.Equal(4, AnimalCapacityRule.HerdDemand(meta, AnimalHousing.CoopFamily));
        Assert.True(AnimalCapacityRule.WouldOverflow(meta, "herdbook_8"));
        meta.OwnedUpgrades.Add("keep_big_coop");
        Assert.False(AnimalCapacityRule.WouldOverflow(meta, "herdbook_8"));
    }

    [Fact]
    public void Start_row_counts_the_herd_slots_too()
    {
        // Herd coop slots 0, 1, 4, 5 already fill the Coop; a coop start row has no room left.
        MetaState meta = Owning(HerdTiers(7).Concat(new[] { "keep_coop" }).ToArray());
        Assert.True(AnimalCapacityRule.WouldOverflow(meta, "start_chicken"));
    }

    [Fact]
    public void Full_ladder_always_fits_deluxe_buildings_even_with_every_start_row()
    {
        MetaState meta = Owning(RunBaselineBuilder.StartingAnimalIds
            .Concat(new[] { "keep_deluxe_coop", "keep_deluxe_barn" }).ToArray());
        for (int tier = 1; tier <= UpgradeCatalog.HerdBookMaxTier; tier++)
        {
            Assert.False(AnimalCapacityRule.WouldOverflow(meta, $"herdbook_{tier}"));
            meta.OwnedUpgrades.Add($"herdbook_{tier}");
        }
    }

    [Fact]
    public void Later_herd_tier_may_squeeze_owned_start_rows_without_blocking()
    {
        // Free slot + three coop start rows = 4 = Coop room. Buying herdbook_1 is still allowed
        // (herd only counts herd), leaving 5 coop animals owed a 4-room Coop; that is accepted.
        // A further coop start row is refused, a further herd tier still checks herd slots only.
        MetaState meta = Owning("keep_coop", "keep_barn", "start_chicken", "start_void_chicken", "start_duck");
        Assert.Equal(UpgradePurchase.PurchaseResult.Success,
            UpgradePurchase.TryPurchase(meta, UpgradeCatalog.TryGet("herdbook_1")));
        Assert.Contains("start_duck", meta.OwnedUpgrades);
        Assert.True(AnimalCapacityRule.WouldOverflow(meta, "start_rabbit"));
        Assert.False(AnimalCapacityRule.WouldOverflow(meta, "herdbook_2"));
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
        Assert.Equal(2, AnimalCapacityRule.HerdDemand(meta, AnimalHousing.CoopFamily));
        Assert.Equal(4, AnimalCapacityRule.StartDemand(meta, AnimalHousing.CoopFamily));
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
