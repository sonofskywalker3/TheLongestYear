using System.Linq;
using System.Text.Json.Nodes;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using TheLongestYear.Core.Sabotage;
using Xunit;
using static TheLongestYear.Tests.FairnessFixtures;

namespace TheLongestYear.Tests;

/// <summary>Deep Woods support (2026-11-05): the Secret Woods and Deep Woods open at week 4 in the
/// landing tables, Deep Woods levels from 10 wait as the Skull Cavern does, and the rewind takes Deep
/// Woods' saved progress back to a fresh game.</summary>
public class DeepWoodsSupportTests
{
    private const int WoodsDay = 22;   // week 4's first day
    private static readonly int SkullCavernPrep = MineDepth.DaysToReach(MineDepth.SkullCavernFloor);

    private static ObtainSource Forage(params string[] requires)
        => new(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { Requires = requires }, "test");

    private static ObtainSource AsBuilt(ObtainSource s) => MineDepth.WithTravel(MapWeekGate.Apply(s));

    [Fact]
    public void The_woods_open_on_the_first_day_of_their_board_week()
    {
        Assert.Equal(TheLongestYear.Core.Availability.LocationGating.WeekFor(MapWeekGate.SecretWoodsMap), MapWeekGate.WoodsOpeningWeek);
        Assert.Equal(WoodsDay, MapWeekGate.WoodsOpeningDay);
    }

    [Fact]
    public void Start_no_earlier_than_waits_for_the_day_and_then_lands_as_before()
    {
        DayTable t = DayTable.Always.StartNoEarlierThan(WoodsDay);
        Assert.Equal(WoodsDay, t.Lands(1));
        Assert.Equal(WoodsDay, t.Lands(WoodsDay));
        Assert.Equal(50, t.Lands(50));
    }

    [Fact]
    public void Start_later_overlaps_a_wait_the_table_already_holds()
    {
        DayTable gated = DayTable.Always.StartNoEarlierThan(WoodsDay);
        DayTable t = gated.StartLater(12);
        Assert.Equal(WoodsDay, t.Lands(1));      // 1 + 12 = 13, still before the woods open
        Assert.Equal(42, t.Lands(30));          // past the gate, the preparation shows
        Assert.Null(t.Lands(DayTable.Days - 5));
    }

    [Fact]
    public void A_secret_woods_route_lands_from_week_4_in_both_tables()
    {
        ObtainSource built = AsBuilt(Forage("location:Woods"));
        Assert.Equal(WoodsDay, built.Lands.Lands(1));
        Assert.Equal(WoodsDay, built.Undelayed.Lands(1));
    }

    [Fact]
    public void Only_the_exact_woods_maps_are_gated()
    {
        Assert.Equal(1, AsBuilt(Forage("location:Forest")).Lands.Lands(1));
        Assert.Equal(1, AsBuilt(Forage("location:WoodsExtra")).Lands.Lands(1));
        Assert.Equal(WoodsDay, AsBuilt(Forage("location:DeepWoods")).Lands.Lands(1));
    }

    [Fact]
    public void A_shallow_deep_woods_route_opens_with_the_woods()
    {
        ObtainSource built = AsBuilt(Forage("location:DeepWoods", "deepwoods:level 2"));
        Assert.Equal(WoodsDay, built.Lands.Lands(1));
        Assert.Null(built.UndelayedLands);
    }

    [Fact]
    public void A_skull_cavern_tier_deep_woods_route_overlaps_its_preparation_with_the_woods_wait()
    {
        ObtainSource built = AsBuilt(Forage("location:DeepWoods", "deepwoods:level 30"));
        Assert.Equal(WoodsDay, built.Lands.Lands(1));
        Assert.Equal(30 + SkullCavernPrep, built.Lands.Lands(30));
        // The undelayed table keeps the calendar gate but not the preparation; the fairness rule prices that.
        Assert.Equal(30, built.Undelayed.Lands(30));
        Assert.Equal(WoodsDay, built.Undelayed.Lands(1));
    }

    [Fact]
    public void The_deepest_deep_woods_level_named_is_read()
    {
        var c = ObtainConditions.None with { Requires = new[] { "deepwoods:level 9", "deepwoods:level 30", "location:DeepWoods" } };
        Assert.Equal(30, MineDepth.DeepWoodsLevelOf(c));
        Assert.True(MineDepth.NeedsDeepWoodsSkullCavernTier(c));
        Assert.False(MineDepth.NeedsDeepWoodsSkullCavernTier(ObtainConditions.None with { Requires = new[] { "deepwoods:level 9" } }));
        Assert.Null(MineDepth.FloorOf(c));   // not a mine floor: no Delay
    }

    private static void AssertBest(FairnessVerdict verdict, bool counts, int? landing, int added)
    {
        Assert.Equal(counts, verdict.Counts);
        RouteVerdict route = Assert.Single(verdict.Routes);
        Assert.Equal(landing, route.LandingDay);
        Assert.Equal(added, route.AddedDays);
    }

    private static ObtainSource DeepRoute(int level) => Route(SourceKind.Forage, requires: new[] { "location:DeepWoods", $"deepwoods:level {level}" });

    [Fact]
    public void Fairness_counts_a_deep_woods_level_30_route_free_once_the_mines_are_cleared()
    {
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 120), Model(DeepRoute(30)));
        AssertBest(verdict, true, Hit + 1, 0);
    }

    [Fact]
    public void Fairness_adds_the_mine_gap_for_level_30_on_normal()
    {
        int gap = FairnessRule.MineGapDays(100, MineDepth.MinesBottomFloor);
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Normal, Save(floor: 100), Model(DeepRoute(30)));
        AssertBest(verdict, true, Hit + 1 + gap, gap);
    }

    [Fact]
    public void Fairness_rules_level_30_out_on_easy_before_the_mines_are_cleared()
    {
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Easy, Save(floor: 100), Model(DeepRoute(30)));
        Assert.False(verdict.Counts);
        Assert.Contains("Skull Cavern monsters", verdict.Routes[0].Reason);
    }

    [Fact]
    public void Fairness_needs_nothing_for_a_shallow_level()
    {
        FairnessVerdict verdict = FairnessRule.Judge(Item, Hit, Deadline, DifficultyStep.Easy, Save(floor: 0), Model(DeepRoute(5)));
        AssertBest(verdict, true, Hit + 1, 0);
    }

    private const long Host = 6980932027091496310;
    private const long Farmhand = 42;
    private const string Saved =
        "{\"PlayersWhoGotStardropFromUnicorn\":[6980932027091496310,42],\"WoodsObeliskLocations\":[{\"X\":1,\"Y\":2}]," +
        "\"WoodsObeliskNonFarmLocations\":{\"Town\":[{\"X\":3,\"Y\":4}]},\"LowestLevelReached\":31,\"OrbStonesSaved\":2,\"Future\":7}";

    [Fact]
    public void The_rewind_takes_deep_woods_progress_back_and_keeps_the_rest()
    {
        Assert.Equal(DeepWoodsStateReset.Outcome.Reset, DeepWoodsStateReset.Reset(Saved, Host, out string? json));
        var state = JsonNode.Parse(json!)!.AsObject();
        Assert.Equal(0, (int)state["LowestLevelReached"]!);
        Assert.Equal(0, (int)state["OrbStonesSaved"]!);
        Assert.Empty(state["WoodsObeliskLocations"]!.AsArray());
        Assert.Empty(state["WoodsObeliskNonFarmLocations"]!.AsObject());
        // Only the reset player's Unicorn Stardrop comes back; a farmhand's stays claimed.
        Assert.Equal(new[] { Farmhand }, state["PlayersWhoGotStardropFromUnicorn"]!.AsArray().Select(n => (long)n!).ToArray());
        Assert.Equal(7, (int)state["Future"]!);
    }

    [Fact]
    public void A_fresh_state_needs_no_rewrite()
    {
        const string fresh = "{\"PlayersWhoGotStardropFromUnicorn\":[],\"WoodsObeliskLocations\":[],\"WoodsObeliskNonFarmLocations\":{},\"LowestLevelReached\":0,\"OrbStonesSaved\":0}";
        Assert.Equal(DeepWoodsStateReset.Outcome.NothingToChange, DeepWoodsStateReset.Reset(fresh, Host, out string? json));
        Assert.Null(json);
        Assert.Equal(DeepWoodsStateReset.Outcome.NothingToChange, DeepWoodsStateReset.Reset(null, Host, out _));
    }

    [Fact]
    public void Unreadable_state_is_left_alone()
    {
        Assert.Equal(DeepWoodsStateReset.Outcome.Unexpected, DeepWoodsStateReset.Reset("[1,2]", Host, out _));
        Assert.Equal(DeepWoodsStateReset.Outcome.Unexpected, DeepWoodsStateReset.Reset("{not json", Host, out _));
    }

    [Fact]
    public void The_save_data_key_matches_smapi()
        => Assert.Equal("smapi/mod-data/maxmakesmods.deepwoodsmod/data", DeepWoodsStateReset.SaveDataKey);
}

/// <summary>Keep Stardrop (Unicorn) (Jeff, 2026-11-05: "yes that keep is good").</summary>
public class DeepWoodsUnicornKeepTests
{
    private const long Host = 6980932027091496310;
    private const string Claimed = "{\"PlayersWhoGotStardropFromUnicorn\":[6980932027091496310],\"LowestLevelReached\":12}";

    [Fact]
    public void The_row_is_a_stardrop_keep_at_the_stardrop_price()
    {
        WalletKeep keep = Assert.Single(WalletKeepTable.Entries, e => e.UpgradeId == WalletKeepTable.UnicornKeepId);
        Assert.Equal(WalletKeepKind.Stardrop, keep.Kind);
        Assert.Equal(WalletKeepTable.TryGet("keep_stardrop_museum")!.Cost, keep.Cost);
        Assert.Empty(keep.MailFlags);
        Assert.NotNull(RunReachRequirement.Parse(keep.Reach));
    }

    [Fact]
    public void The_row_is_offered_only_with_deep_woods_installed()
    {
        var before = UpgradeCatalog.IsModLoaded;
        try
        {
            UpgradeCatalog.IsModLoaded = _ => false;
            Assert.DoesNotContain(UpgradeCatalog.ByCategory(UpgradeCategory.Carryover), d => d.Id == WalletKeepTable.UnicornKeepId);
            UpgradeCatalog.IsModLoaded = id => id == DeepWoodsStateReset.DeepWoodsModId;
            Assert.Contains(UpgradeCatalog.ByCategory(UpgradeCategory.Carryover), d => d.Id == WalletKeepTable.UnicornKeepId);
        }
        finally
        {
            UpgradeCatalog.IsModLoaded = before;
        }
    }

    [Fact]
    public void A_kept_unicorn_stardrop_stays_claimed_through_the_rewind()
    {
        Assert.Equal(DeepWoodsStateReset.Outcome.Reset, DeepWoodsStateReset.Reset(Claimed, Host, out string? json, keepUnicornStardrop: true));
        Assert.True(DeepWoodsStateReset.HasUnicornStardrop(json, Host));
        DeepWoodsStateReset.Reset(Claimed, Host, out string? cleared);
        Assert.False(DeepWoodsStateReset.HasUnicornStardrop(cleared, Host));
    }

    [Fact]
    public void The_claim_reads_from_the_saved_state()
    {
        Assert.True(DeepWoodsStateReset.HasUnicornStardrop(Claimed, Host));
        Assert.False(DeepWoodsStateReset.HasUnicornStardrop(Claimed, 1));
        Assert.False(DeepWoodsStateReset.HasUnicornStardrop("not json", Host));
        Assert.False(DeepWoodsStateReset.HasUnicornStardrop(null, Host));
    }
}
