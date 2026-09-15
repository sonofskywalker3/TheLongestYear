using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

public class SabotageScheduleTests
{
    [Theory]
    [InlineData(SabotageKind.Blight, Season.Spring, false)]
    [InlineData(SabotageKind.Blight, Season.Summer, true)]
    [InlineData(SabotageKind.Blight, Season.Fall, true)]
    [InlineData(SabotageKind.Blight, Season.Winter, true)]
    [InlineData(SabotageKind.Reversion, Season.Summer, false)]
    [InlineData(SabotageKind.Reversion, Season.Fall, true)]
    [InlineData(SabotageKind.Reversion, Season.Winter, true)]
    [InlineData(SabotageKind.Tampering, Season.Fall, false)]
    [InlineData(SabotageKind.Tampering, Season.Winter, true)]
    public void Each_front_opens_in_its_own_seasons(SabotageKind kind, Season season, bool open)
        => Assert.Equal(open, SabotageSchedule.IsOpen(kind, season));

    [Fact]
    public void A_closed_season_is_closed()
    {
        Assert.False(SabotageSchedule.IsOpen(SabotageKind.Blight, Season.Spring));
        Assert.False(SabotageSchedule.IsOpen(SabotageKind.Tampering, Season.Fall));
    }

    [Fact]
    public void Reversion_and_tampering_keep_quiet_at_the_end_of_a_season()
    {
        Assert.False(SabotageSchedule.IsQuietDay(SabotageKind.Reversion, 24));
        Assert.True(SabotageSchedule.IsQuietDay(SabotageKind.Reversion, 25));
        Assert.False(SabotageSchedule.IsQuietDay(SabotageKind.Tampering, 20));
        Assert.True(SabotageSchedule.IsQuietDay(SabotageKind.Tampering, 21));
    }

    [Fact]
    public void Blight_is_capped_per_week_and_the_cap_resets_next_week()
    {
        var run = new RunState();
        int week = Calendar.WeekOfYear((int)Season.Summer, 3);
        int day = Calendar.DayOfYear((int)Season.Summer, 3);
        for (int i = 0; i < SabotageTuning.BlightNightsPerWeek; i++)
        {
            Assert.True(SabotageSchedule.WithinCaps(SabotageKind.Blight, run, week, day));
            SabotageSchedule.RecordStrike(SabotageKind.Blight, run, week, day);
        }
        Assert.False(SabotageSchedule.WithinCaps(SabotageKind.Blight, run, week, day));
        Assert.True(SabotageSchedule.WithinCaps(SabotageKind.Blight, run, week + 1, day + 7));
    }

    [Fact]
    public void Reversion_is_once_a_week()
    {
        var run = new RunState();
        SabotageSchedule.RecordStrike(SabotageKind.Reversion, run, 10, 65);
        Assert.False(SabotageSchedule.WithinCaps(SabotageKind.Reversion, run, 10, 66));
        Assert.True(SabotageSchedule.WithinCaps(SabotageKind.Reversion, run, 11, 71));
    }

    [Fact]
    public void Tampering_is_twice_a_winter_and_spaced_out()
    {
        var run = new RunState();
        int day1 = Calendar.DayOfYear((int)Season.Winter, 3);
        SabotageSchedule.RecordStrike(SabotageKind.Tampering, run, 13, day1);
        Assert.False(SabotageSchedule.WithinCaps(SabotageKind.Tampering, run, 13, day1 + SabotageTuning.TamperMinDaysApart - 1));
        Assert.True(SabotageSchedule.WithinCaps(SabotageKind.Tampering, run, 13, day1 + SabotageTuning.TamperMinDaysApart));
        SabotageSchedule.RecordStrike(SabotageKind.Tampering, run, 14, day1 + 8);
        Assert.False(SabotageSchedule.WithinCaps(SabotageKind.Tampering, run, 15, day1 + 30));
    }

    [Fact]
    public void The_nights_die_is_fixed_by_seed_day_and_front()
    {
        double a = SabotageSchedule.Rng(42, 40, SabotageKind.Blight).NextDouble();
        double b = SabotageSchedule.Rng(42, 40, SabotageKind.Blight).NextDouble();
        double c = SabotageSchedule.Rng(42, 40, SabotageKind.Reversion).NextDouble();
        double d = SabotageSchedule.Rng(42, 41, SabotageKind.Blight).NextDouble();
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
        Assert.NotEqual(a, d);
    }

    [Fact]
    public void A_new_run_forgets_every_counter_and_report()
    {
        var run = new RunState();
        SabotageSchedule.RecordStrike(SabotageKind.Blight, run, 6, 40);
        SabotageSchedule.RecordStrike(SabotageKind.Reversion, run, 10, 66);
        SabotageSchedule.RecordStrike(SabotageKind.Tampering, run, 13, 90);
        run.Tampers.Add(new TamperRecord { BundleIndex = 1 });
        run.PendingSabotageReports.Add(new SabotageReport { Kind = SabotageKind.Blight, Count = 2 });
        run.EndingArmed = true;
        run.DarknessChanceWeek = 9;
        run.DarknessChance = 0.1;
        run.UnmoderatedReversionSpent = true;
        run.UnmoderatedTamperSpent = true;
        run.GuaranteedTamperDone = true;
        run.BeginNewRun(7);
        Assert.False(run.EndingArmed);
        Assert.Equal(-1, run.BlightWeek);
        Assert.Equal(0, run.BlightNightsThisWeek);
        Assert.Equal(-1, run.LastReversionWeek);
        Assert.Empty(run.TamperDays);
        Assert.Empty(run.Tampers);
        Assert.Empty(run.PendingSabotageReports);
        Assert.Equal(-1, run.DarknessChanceWeek);
        Assert.Equal(0.0, run.DarknessChance);
        Assert.False(run.UnmoderatedReversionSpent);
        Assert.False(run.UnmoderatedTamperSpent);
        Assert.False(run.GuaranteedTamperDone);
    }
}

public class BlightRuleTests
{
    [Fact]
    public void Count_and_spoil_count_are_now_by_level_and_pinned_in_DarknessLevelsTests()
    {
        Assert.Equal(5, BlightRule.Count(100, Season.Summer, DifficultyStep.Normal));
        Assert.Equal(5, BlightRule.SpoilCount(100, Season.Summer, DifficultyStep.Normal));
    }

    [Theory]
    [InlineData(-75, true)]   // vegetable
    [InlineData(-4, true)]    // fish
    [InlineData(-26, true)]   // artisan goods are food
    [InlineData(-2, false)]   // minerals go missing instead
    [InlineData(-15, false)]  // metal
    public void Food_spoils_and_the_rest_goes_missing(int category, bool perishable)
        => Assert.Equal(perishable, BlightRule.IsPerishableCategory(category));

    [Fact]
    public void A_blight_night_takes_crops_or_stock_never_both()
    {
        bool sawCrops = false, sawStock = false;
        for (int seed = 0; seed < 200; seed++)
        {
            (int crops, int spoil) = BlightRule.OneTarget(3, 5, null, new Random(seed));
            Assert.True(crops == 0 ^ spoil == 0);
            sawCrops |= crops == 3;
            sawStock |= spoil == 5;
        }
        Assert.True(sawCrops);
        Assert.True(sawStock);
    }

    [Theory]
    [InlineData(0, 5, 0, 5)]   // no crops: the stock
    [InlineData(3, 0, 3, 0)]   // nothing stored: the crops
    [InlineData(0, 0, 0, 0)]
    public void A_blight_night_goes_after_whichever_side_has_anything(int crops, int spoil, int expectCrops, int expectSpoil)
        => Assert.Equal((expectCrops, expectSpoil), BlightRule.OneTarget(crops, spoil, null, new Random(1)));

    [Fact]
    public void A_named_target_always_wins()
    {
        for (int seed = 0; seed < 20; seed++)
        {
            Assert.Equal((3, 0), BlightRule.OneTarget(3, 5, BlightTarget.Crops, new Random(seed)));
            Assert.Equal((0, 5), BlightRule.OneTarget(3, 5, BlightTarget.Chests, new Random(seed)));
        }
    }

    [Fact]
    public void Picks_distinct_positions_inside_the_field()
    {
        IReadOnlyList<int> picked = BlightRule.PickIndexes(20, 6, new Random(1));
        Assert.Equal(6, picked.Count);
        Assert.Equal(6, picked.Distinct().Count());
        Assert.All(picked, i => Assert.InRange(i, 0, 19));
        Assert.Equal(3, BlightRule.PickIndexes(3, 6, new Random(1)).Count);
    }
}

public class ReversionRuleTests
{
    private static BundleRequirement Bundle(string name, Theme theme, int index, int slots, params string[] ids)
        => BundleRequirement.CreatePercentage(name, theme, ids, slots, new[] { 0, 0, 0, slots }, bundleIndex: index);

    [Fact]
    public void Only_unfinished_item_room_bundles_are_candidates()
    {
        var pantry = Bundle("Spring Crops", Theme.Farming, 1, 2, "(O)24", "(O)188", "(O)190");
        var tank = Bundle("River Fish", Theme.Fishing, 2, 2, "(O)145", "(O)143");
        var vault = Bundle("2,500g", Theme.Artisan, 3, 1, "(O)-1");
        var ledger = new SlotLedger();
        ledger.Add(1, 0, "(O)24");
        ledger.Add(2, 0, "(O)145");
        ledger.Add(2, 1, "(O)143");   // River Fish complete: never touched

        var all = ReversionRule.Candidates(ledger, new[] { pantry, tank, vault });
        Assert.Single(all);
        Assert.Equal(1, all[0].BundleIndex);
        Assert.Equal(1, ReversionRule.Pick(ledger, new[] { pantry, tank, vault }, new Random(1))!.BundleIndex);
        Assert.Null(ReversionRule.Pick(new SlotLedger(), new[] { pantry, tank, vault }, new Random(1)));
    }

    [Fact]
    public void A_slot_whose_item_fails_the_fairness_test_is_never_picked()
    {
        // Build the same two-slot unfinished bundle the candidate test uses; call the ids A and B.
        (SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements) = TwoFilledSlotsInOneUnfinishedBundle();
        string a = ledger.Entries[0].ItemId, b = ledger.Entries[1].ItemId;

        for (int seed = 0; seed < 20; seed++)
        {
            DonatedSlot? pick = ReversionRule.Pick(ledger, requirements, id => id == b, new Random(seed));
            Assert.NotNull(pick);
            Assert.Equal(b, pick!.ItemId);
        }
        Assert.Null(ReversionRule.Pick(ledger, requirements, _ => false, new Random(1)));
        Assert.NotNull(ReversionRule.Pick(ledger, requirements, new Random(1)));
    }

    private static (SlotLedger Ledger, IReadOnlyList<BundleRequirement> Requirements) TwoFilledSlotsInOneUnfinishedBundle()
    {
        var pantry = Bundle("Spring Crops", Theme.Farming, 1, 3, "(O)24", "(O)188", "(O)190");
        var ledger = new SlotLedger();
        ledger.Add(1, 0, "(O)24");
        ledger.Add(1, 1, "(O)188");
        return (ledger, new[] { pantry });
    }
}

public class TamperRuleTests
{
    private static BundleRequirement Bundle(string name, Theme theme, int index, int slots, params string[] ids)
        => BundleRequirement.CreatePercentage(name, theme, ids, slots, new[] { 0, 0, 0, slots }, bundleIndex: index);

    [Fact]
    public void Targets_are_unfilled_slots_of_unfinished_bundles()
    {
        var pantry = Bundle("Spring Crops", Theme.Farming, 1, 2, "(O)24", "(O)188", "(O)190");
        var ledger = new SlotLedger();
        ledger.Add(1, 1, "(O)188");
        var targets = TamperRule.Targets(ledger, new[] { pantry });
        Assert.Equal(new[] { 0, 2 }, targets.Select(t => t.IngredientIndex).ToArray());
    }

    [Fact]
    public void The_slot_whose_item_the_player_holds_is_hit_first()
    {
        var pantry = Bundle("Spring Crops", Theme.Farming, 1, 3, "(O)24", "(O)188", "(O)190");
        var targets = TamperRule.Targets(new SlotLedger(), new[] { pantry });
        TamperTarget? pick = TamperRule.PickTarget(targets, id => id == "(O)190", new Random(3));
        Assert.NotNull(pick);
        Assert.Equal("(O)190", pick!.ItemId);
    }

    [Fact]
    public void Replacement_matches_the_room_theme_and_is_not_already_in_the_bundle()
    {
        var pantry = Bundle("Spring Crops", Theme.Farming, 1, 3, "(O)24", "(O)188", "(O)190");
        var target = new TamperTarget(pantry, 0, "(O)24");
        var candidates = new List<TamperCandidate>
        {
            new("(O)24", Theme.Farming, 1),   // already in the bundle
            new("(O)145", Theme.Fishing, 1),  // wrong room
            new("(O)414", Theme.Farming, 2),  // the only fit
        };
        TamperCandidate? pick = TamperRule.PickReplacement(target, 1, candidates, new Random(1));
        Assert.Equal("(O)414", pick!.ItemId);
    }

    [Theory]
    [InlineData(100, 1, DifficultyStep.Normal, 10, 20)]   // weeks 1 and 2 keep the whole count
    [InlineData(100, 2, DifficultyStep.Normal, 10, 20)]
    [InlineData(100, 3, DifficultyStep.Normal, 5, 10)]    // week 3: 50 left, 10 to 20% of it
    [InlineData(100, 4, DifficultyStep.Easy, 1, 3)]       // week 4: 33 left, 0 to 10% of it
    [InlineData(100, 3, DifficultyStep.Hard, 10, 15)]
    [InlineData(100, 3, DifficultyStep.Extreme, 15, 20)]
    [InlineData(0, 3, DifficultyStep.Extreme, 1, 1)]      // no known max count: one
    public void Tampered_stack_is_a_difficulty_slice_of_what_the_weeks_left(int max, int week, DifficultyStep step, int low, int high)
    {
        for (int seed = 0; seed < 50; seed++)
        {
            int stack = TamperRule.Stack(max, week, step, new Random(seed));
            Assert.InRange(stack, low, high);
        }
    }

    [Fact]
    public void The_bulletin_board_takes_any_theme_and_closest_effort_wins()
    {
        var board = Bundle("Fodder", Theme.Mixed, 5, 1, "(O)262");
        var target = new TamperTarget(board, 0, "(O)262");
        var candidates = new List<TamperCandidate>();
        for (int i = 0; i < 20; i++) candidates.Add(new($"(O){900 + i}", Theme.Fishing, 10 + i));
        candidates.Add(new("(O)1", Theme.Farming, 2));
        TamperCandidate? pick = TamperRule.PickReplacement(target, 2, candidates, new Random(1));
        Assert.NotNull(pick);
        Assert.True(pick!.Effort <= 14, $"picked effort {pick.Effort}, expected one of the five closest");
        Assert.Null(TamperRule.PickReplacement(target, 2, new List<TamperCandidate>(), new Random(1)));
    }
}

public class BundleDataTamperTests
{
    private const string Value = "Spring Crops/O 495 30/24 1 0 188 1 0 190 1 0/0/2//Spring Crops";

    [Fact]
    public void Rewrites_one_triple_and_nothing_else()
    {
        string? result = BundleDataTamper.Rewrite(Value, 1, "(O)414", 1, 0);
        Assert.Equal("Spring Crops/O 495 30/24 1 0 (O)414 1 0 190 1 0/0/2//Spring Crops", result);
        ParsedBundle parsed = BundleParsing.Parse("Pantry/1", result!);
        Assert.Equal("(O)414", parsed.Ingredients[1].ItemRef);
        Assert.Equal(3, parsed.Ingredients.Count);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void Out_of_range_slot_is_refused(int slot)
        => Assert.Null(BundleDataTamper.Rewrite(Value, slot, "(O)414", 1, 0));

    [Fact]
    public void Apply_copies_the_board_and_finds_the_key_by_index()
    {
        var board = new Dictionary<string, string> { ["Pantry/1"] = Value, ["Fish Tank/6"] = "River Fish/O 685 30/145 1 0/1/1//River Fish" };
        Assert.Equal("Fish Tank/6", BundleDataTamper.KeyForIndex(board, 6));
        Assert.Null(BundleDataTamper.KeyForIndex(board, 9));
        Dictionary<string, string>? tampered = BundleDataTamper.Apply(board, "Pantry/1", 0, "(O)414", 1, 0);
        Assert.NotNull(tampered);
        Assert.Equal(Value, board["Pantry/1"]);
        Assert.StartsWith("Spring Crops/O 495 30/(O)414 1 0 ", tampered!["Pantry/1"]);
        Assert.Equal(board["Fish Tank/6"], tampered["Fish Tank/6"]);
    }
}

public class WardIdsTests
{
    [Fact]
    public void Every_ward_is_a_catalog_row_in_the_wards_tab()
    {
        foreach (string id in WardIds.All)
        {
            UpgradeDefinition? def = UpgradeCatalog.TryGet(id);
            Assert.NotNull(def);
            Assert.Equal(UpgradeCategory.Wards, def!.Category);
            Assert.Null(def.RunReachRequirement);
        }
    }

    [Fact]
    public void Circles_chain_and_count()
    {
        Assert.Null(UpgradeCatalog.TryGet(WardIds.Circle1)!.PrerequisiteId);
        Assert.Equal(WardIds.Circle1, UpgradeCatalog.TryGet(WardIds.Circle2)!.PrerequisiteId);
        Assert.Equal(WardIds.Circle2, UpgradeCatalog.TryGet(WardIds.Circle3)!.PrerequisiteId);
        Assert.True(WardIds.Circle1Cost < WardIds.Circle2Cost && WardIds.Circle2Cost < WardIds.Circle3Cost);
        var owned = new HashSet<string> { WardIds.Circle1, WardIds.Circle2 };
        Assert.Equal(2, WardIds.CircleCount(owned.Contains));
        Assert.Equal(0, WardIds.CircleCount(_ => false));
    }

    [Fact]
    public void Wards_cover_every_blighted_season_and_nothing_else()
    {
        Assert.Null(WardIds.CropWardFor(Season.Spring));
        Assert.Equal(WardIds.CropsSummer, WardIds.CropWardFor(Season.Summer));
        Assert.Equal(WardIds.CropsFall, WardIds.CropWardFor(Season.Fall));
        Assert.Equal(WardIds.CropsWinter, WardIds.CropWardFor(Season.Winter));
        Assert.Equal(6, WardIds.All.Count);
        Assert.DoesNotContain(UpgradeCatalog.All, u => u.Id.StartsWith("ward_hall_"));
    }
}
