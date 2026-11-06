using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Seven Deadly Sins support (2026-11-05). A player lost the Wolf Staff after the first
/// loop: the mod records the pickup as a seen-event id, the rewind put every seen id back, and the
/// staff itself went with the inventory.</summary>
public class SdsSupportTests
{
    [Fact]
    public void The_wolf_staff_pickup_flag_is_not_put_back_by_the_rewind()
        => Assert.Contains("ricehit.SevenDeadlySinsCP_Pickup_SDS_WolfStaff", SdsUnlocks.RewoundEventIds);

    [Fact]
    public void The_scenes_that_open_the_seasonal_store_and_the_hotel_rewind()
    {
        Assert.Contains("19215319", SdsUnlocks.RewoundEventIds);
        Assert.Contains("19215323", SdsUnlocks.RewoundEventIds);
        // The whole chain, or a later scene would wait on a first one that never replays.
        foreach (string id in SdsUnlocks.BooChain)
            Assert.Contains(id, SdsUnlocks.RewoundEventIds);
    }

    [Fact]
    public void The_travel_station_forgets_its_destinations()
    {
        Assert.Equal(7, SdsUnlocks.StationScenes.Count);
        foreach (string id in SdsUnlocks.StationScenes)
            Assert.Contains(id, SdsUnlocks.RewoundEventIds);
    }
}

/// <summary>The rewind takes Unlockable Bundles' saved progress back to a fresh game: a paid map
/// bundle stayed paid (its area free in every later loop), an item bundle stayed paid while its
/// reward went with the inventory, currencies carried over and prize machines kept their place.</summary>
public class UnlockableBundlesResetTests
{
    private const string Saved =
        "{\"UnlockableSaveData\":{" +
        "\"SDSCavebase.open\":{\"Custom_SDS.Cavebase\":{\"Purchased\":true,\"DayPurchased\":40}}," +
        "\"WoodsTree.eggshell2\":{\"Custom_Woods2\":{\"Purchased\":true,\"DayPurchased\":50}}," +
        "\"SDScliffroad.roadopen\":{\"Custom_SDS.cliffroad\":{\"Purchased\":false,\"AlreadyPaid\":{\"Money\":10000}}}}," +
        "\"FoundUniqueDigSpots\":{\"Town:1,2\":[77]}," +
        "\"SPRTriggerActionKeys\":[\"a\"]," +
        "\"WalletCurrencyData\":{\"SDS.Oldcoin\":{\"77\":12}}," +
        "\"WalletCurrencyTotal\":{\"SDS.Oldcoin\":{\"77\":30}}," +
        "\"PrizeMachineData\":{\"SDS.MoorePrizeMachine\":{\"77\":{\"PrizeLevel\":6}}}}";

    private static System.Text.Json.Nodes.JsonObject Parse(string json)
        => (System.Text.Json.Nodes.JsonObject)System.Text.Json.Nodes.JsonNode.Parse(json)!;

    [Fact]
    public void Every_bundle_currency_and_prize_machine_goes_back_to_a_fresh_game()
    {
        var outcome = UnlockableBundlesStateReset.Reset(Saved, new System.Collections.Generic.HashSet<string>(), out string? rewritten);
        Assert.Equal(UnlockableBundlesStateReset.Outcome.Reset, outcome);
        var state = Parse(rewritten!);
        Assert.Empty(state["UnlockableSaveData"]!.AsObject());
        Assert.Empty(state["WalletCurrencyData"]!.AsObject());
        Assert.Empty(state["WalletCurrencyTotal"]!.AsObject());
        Assert.Empty(state["PrizeMachineData"]!.AsObject());
        Assert.Empty(state["FoundUniqueDigSpots"]!.AsObject());
        Assert.Single(state["SPRTriggerActionKeys"]!.AsArray());
        Assert.False(UnlockableBundlesStateReset.IsPurchased(rewritten, "SDSCavebase.open"));
    }

    [Fact]
    public void A_kept_bundle_stays_paid()
    {
        var kept = new System.Collections.Generic.HashSet<string> { "SDSCavebase.open" };
        UnlockableBundlesStateReset.Reset(Saved, kept, out string? rewritten);
        Assert.True(UnlockableBundlesStateReset.IsPurchased(rewritten, "SDSCavebase.open"));
        Assert.False(UnlockableBundlesStateReset.IsPurchased(rewritten, "WoodsTree.eggshell2"));
        Assert.Empty(Parse(rewritten!)["WalletCurrencyData"]!.AsObject());
    }

    [Fact]
    public void Purchased_reads_the_saved_record()
    {
        Assert.True(UnlockableBundlesStateReset.IsPurchased(Saved, "SDSCavebase.open"));
        Assert.False(UnlockableBundlesStateReset.IsPurchased(Saved, "SDScliffroad.roadopen"));   // part paid only
        Assert.False(UnlockableBundlesStateReset.IsPurchased(Saved, "Unknown"));
        Assert.False(UnlockableBundlesStateReset.IsPurchased("not json", "SDSCavebase.open"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"UnlockableSaveData\":{},\"WalletCurrencyData\":{}}")]
    public void A_fresh_state_is_left_alone(string? json)
        => Assert.Equal(UnlockableBundlesStateReset.Outcome.NothingToChange,
            UnlockableBundlesStateReset.Reset(json, new System.Collections.Generic.HashSet<string>(), out _));

    [Fact]
    public void A_state_in_another_shape_is_left_alone()
        => Assert.Equal(UnlockableBundlesStateReset.Outcome.Unexpected,
            UnlockableBundlesStateReset.Reset("[1]", new System.Collections.Generic.HashSet<string>(), out _));

    [Fact]
    public void The_save_data_keys_are_the_ones_smapi_files_them_under()
    {
        Assert.Equal("smapi/mod-data/dlx.bundles/main", UnlockableBundlesStateReset.SaveDataKey);
        Assert.Equal("smapi/mod-data/flqzzzz.sevendeadlysins-smapi/cookingsavedata", SdsCookingStateReset.SaveDataKey);
    }

    [Fact]
    public void Cooking_progress_is_dropped_for_the_reset_player_only()
    {
        const string cooking = "{\"players\":{\"77\":{\"PlayerId\":77,\"Recipes\":[{\"Id\":\"SDS_Rice\",\"RewardClaimed\":true}]},\"88\":{\"PlayerId\":88}}}";
        Assert.Equal(SdsCookingStateReset.Outcome.Reset, SdsCookingStateReset.Reset(cooking, 77, out string? rewritten));
        var players = Parse(rewritten!)["players"]!.AsObject();
        Assert.False(players.ContainsKey("77"));
        Assert.True(players.ContainsKey("88"));
        Assert.Equal(SdsCookingStateReset.Outcome.NothingToChange, SdsCookingStateReset.Reset(rewritten, 77, out _));
        Assert.Equal(SdsCookingStateReset.Outcome.Unexpected, SdsCookingStateReset.Reset("[]", 77, out _));
    }
}
