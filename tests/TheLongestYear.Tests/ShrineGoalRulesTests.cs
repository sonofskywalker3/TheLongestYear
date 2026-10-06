using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ShrineGoalRulesTests
{
    private static readonly Dictionary<string, string> Board = new()
    {
        ["Pantry/0"] = "Spring Crops/O 465 20/24 1 0 188 1 0 -5 2 0/0/4",
        ["Vault/23"] = "2,500g/O 220 3/-1 2500 0/4",
    };

    private static int? CategoryOf(string id) => id switch
    {
        "(O)176" => -5,   // egg: the board asks any animal product (-5)
        "(O)400" => -79,
        _ => null,
    };

    [Fact]
    public void ReadBoard_takes_qualified_ids_and_category_refs()
    {
        ShrineGoalRules.BoardRefs refs = ShrineGoalRules.ReadBoard(Board);
        Assert.Contains("(O)24", refs.ItemIds);
        Assert.Contains("(O)188", refs.ItemIds);
        Assert.Contains(-5, refs.Categories);
        Assert.DoesNotContain("(O)-5", refs.ItemIds);
    }

    [Fact]
    public void Candidates_drop_board_ids_category_matches_excluded_legendaries_and_taken()
    {
        ShrineGoalRules.BoardRefs refs = ShrineGoalRules.ReadBoard(Board);
        var theme = new[] { "(O)24", "188", "(O)176", "(O)159", "(O)400", "(O)90", "(O)18", "(O)20", "(O)20" };
        var excluded = new HashSet<string> { "(O)90" };
        IReadOnlyList<string> c = ShrineGoalRules.Candidates(theme, excluded, refs, CategoryOf, alreadyTaken: new[] { "18" });
        Assert.Equal(new[] { "(O)20", "(O)400" }, c);
    }

    [Theory]
    [InlineData(DifficultyStep.Easy, 0, 3)]
    [InlineData(DifficultyStep.Normal, 1, 3)]
    [InlineData(DifficultyStep.Hard, 5, 0)]
    [InlineData(DifficultyStep.Extreme, 9, 0)]
    [InlineData(DifficultyStep.Extreme, 2, 4)]
    public void Count_fills_the_gap_to_the_target(DifficultyStep step, int cc, int expected)
        => Assert.Equal(expected, ShrineGoalRules.CountFor(step, cc));

    [Fact]
    public void CanCredit_needs_the_item_enough_stack_and_an_open_goal()
    {
        var goal = new ShrineGoal { ItemId = "(O)20", Stack = 5 };
        Assert.True(ShrineGoalRules.CanCredit(goal, "(O)20", 0, 5));
        Assert.True(ShrineGoalRules.CanCredit(goal, "(O)20", 4, 9));
        Assert.False(ShrineGoalRules.CanCredit(goal, "(O)20", 0, 4));
        Assert.False(ShrineGoalRules.CanCredit(goal, "(O)21", 0, 9));
        Assert.False(ShrineGoalRules.CanCredit(goal, null, 0, 9));
        Assert.False(ShrineGoalRules.CanCredit(null, "(O)20", 0, 9));
        goal.Deposited = true;
        Assert.False(ShrineGoalRules.CanCredit(goal, "(O)20", 0, 9));
    }

    [Fact]
    public void DonationJp_is_per_item_times_stack_times_bonus_times_card()
    {
        Assert.Equal(150, ShrineGoalRules.DonationJp(10, 10, 1.5, 1.0));
        Assert.Equal(225, ShrineGoalRules.DonationJp(10, 10, 1.5, 1.5));
        Assert.Equal(8, ShrineGoalRules.DonationJp(5, 1, 1.5, 1.0)); // 7.5 rounds away from zero
    }

    [Fact]
    public void Tally_and_paid_span_both_kinds_per_list()
    {
        var goals = new List<ShrineGoal>
        {
            new() { ItemId = "(O)1", ListIndex = 0, Deposited = true },
            new() { ItemId = "(O)2", ListIndex = 0 },
            new() { ItemId = "(O)3", ListIndex = 1, Deposited = true },
        };
        IReadOnlyList<ShrineGoal> first = ShrineGoalRules.OfList(goals, 0);
        Assert.Equal((3, 5), ShrineGoalRules.Tally(2, 3, first));
        Assert.Equal(1, ShrineGoalRules.MarkPaid(first));
        Assert.Equal(0, ShrineGoalRules.MarkPaid(first));
        Assert.False(goals[2].Paid);
    }

    [Fact]
    public void A_list_is_empty_only_without_either_kind()
    {
        var none = new List<ShrineGoal>();
        var one = new List<ShrineGoal> { new() { ItemId = "(O)1" } };
        Assert.True(ShrineGoalRules.ListIsEmpty(0, none));
        Assert.False(ShrineGoalRules.ListIsEmpty(0, one));
        Assert.False(ShrineGoalRules.ListIsEmpty(2, none));
    }

    [Fact]
    public void Build_rolls_stacks_then_applies_the_week_discount()
    {
        var profile = new DifficultyProfile();
        List<ShrineGoal> goals = ShrineGoalRules.Build(new[] { "(O)1", "(O)2" }, 1, profile, new Random(3),
            id => id == "(O)1" ? null : 40.0, (id, s) => id == "(O)2" ? 40 : s, weekDiscount: 0.5);
        Assert.Equal(2, goals.Count);
        Assert.All(goals, g => Assert.Equal(1, g.ListIndex));
        Assert.Equal(StackScaling.ScaleStack(1, 1.0), goals[0].Stack);
        Assert.Equal(20, goals[1].Stack);
        Assert.All(goals, g => Assert.False(g.Deposited || g.Paid));
    }
}
