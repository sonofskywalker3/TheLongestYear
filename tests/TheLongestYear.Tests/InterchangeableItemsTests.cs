using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>White and brown eggs are the same thing to a player (Nexus, flutegir, 2026-10-10: a
/// 13-egg Animal slot took only white eggs).</summary>
public class InterchangeableItemsTests
{
    private const string Egg = "(O)176";
    private const string BrownEgg = "(O)180";
    private const string LargeEgg = "(O)174";
    private const string LargeBrownEgg = "(O)182";
    private const string DuckEgg = "(O)442";
    private const string VoidEgg = "(O)305";
    private const string DinosaurEgg = "(O)107";
    private const string GoldenEgg = "(O)928";

    [Theory]
    [InlineData(Egg, BrownEgg)]
    [InlineData(BrownEgg, Egg)]
    [InlineData(LargeEgg, LargeBrownEgg)]
    [InlineData(LargeBrownEgg, LargeEgg)]
    [InlineData("176", "(O)180")]   // vanilla bundle data writes bare ids
    [InlineData("(O)182", "174")]
    public void White_and_brown_of_one_size_stand_in_for_each_other(string slot, string offered)
        => Assert.True(InterchangeableItems.SameForDonation(slot, offered));

    [Fact]
    public void An_item_is_always_the_same_as_itself()
    {
        Assert.True(InterchangeableItems.SameForDonation(DuckEgg, DuckEgg));
        Assert.True(InterchangeableItems.SameForDonation("442", DuckEgg));
    }

    [Theory]
    [InlineData(Egg, LargeEgg)]          // sizes stay apart
    [InlineData(Egg, LargeBrownEgg)]
    [InlineData(BrownEgg, LargeEgg)]
    [InlineData(Egg, DuckEgg)]           // other eggs stay apart
    [InlineData(Egg, VoidEgg)]
    [InlineData(Egg, DinosaurEgg)]
    [InlineData(Egg, GoldenEgg)]
    [InlineData(LargeEgg, DuckEgg)]
    public void Other_eggs_and_the_other_size_do_not(string slot, string offered)
        => Assert.False(InterchangeableItems.SameForDonation(slot, offered));

    [Fact]
    public void Missing_ids_never_match()
    {
        Assert.False(InterchangeableItems.SameForDonation(null, Egg));
        Assert.False(InterchangeableItems.SameForDonation(Egg, null));
        Assert.False(InterchangeableItems.SameForDonation("", ""));
    }

    [Fact]
    public void Equivalents_list_the_item_and_its_other_colour()
    {
        Assert.Equal(new[] { Egg, BrownEgg }, InterchangeableItems.EquivalentsOf(BrownEgg).OrderBy(x => x));
        Assert.Equal(new[] { LargeEgg, LargeBrownEgg }, InterchangeableItems.EquivalentsOf("174").OrderBy(x => x));
        Assert.Equal(new[] { DuckEgg }, InterchangeableItems.EquivalentsOf("442"));
        Assert.Empty(InterchangeableItems.EquivalentsOf(null));
    }

    // ---- Shrine goals: the same rule, and the colours add up toward one goal ----

    [Fact]
    public void A_shrine_egg_goal_credits_either_colour()
    {
        var goal = new ShrineGoal { ItemId = Egg, Stack = 13 };
        Assert.True(ShrineGoalRules.CanCredit(goal, BrownEgg, 0, 13));
        Assert.True(ShrineGoalRules.CanCredit(goal, Egg, 0, 13));
        Assert.False(ShrineGoalRules.CanCredit(goal, LargeBrownEgg, 0, 13));
        Assert.False(ShrineGoalRules.CanCredit(goal, DuckEgg, 0, 13));
    }

    [Fact]
    public void Seven_white_and_six_brown_fill_a_thirteen_egg_goal_clicked_stack_first()
    {
        var goal = new ShrineGoal { ItemId = Egg, Stack = 13 };
        var inventory = new List<ShrineGoalRules.HeldStack>
        {
            new(Egg, 7), new(DuckEgg, 20), new(BrownEgg, 6),
        };
        Assert.Equal(13, ShrineGoalRules.MatchingCount(goal, inventory));

        IReadOnlyList<ShrineGoalRules.Take>? plan = ShrineGoalRules.TakePlan(goal, inventory, clickedSlot: 2);
        Assert.NotNull(plan);
        Assert.Equal(new[] { new ShrineGoalRules.Take(2, 6), new ShrineGoalRules.Take(0, 7) }, plan);
    }

    [Fact]
    public void A_take_plan_stops_at_the_goal_and_leaves_the_rest()
    {
        var goal = new ShrineGoal { ItemId = BrownEgg, Stack = 5 };
        var inventory = new List<ShrineGoalRules.HeldStack> { new(BrownEgg, 3), new(Egg, 9) };
        Assert.Equal(new[] { new ShrineGoalRules.Take(1, 5) },
            ShrineGoalRules.TakePlan(goal, inventory, clickedSlot: 1));
    }

    [Fact]
    public void A_take_plan_is_null_when_short_or_the_click_is_not_the_goal_item()
    {
        var goal = new ShrineGoal { ItemId = Egg, Stack = 13 };
        var shortInv = new List<ShrineGoalRules.HeldStack> { new(Egg, 7), new(BrownEgg, 5) };
        Assert.Null(ShrineGoalRules.TakePlan(goal, shortInv, clickedSlot: 0));

        var duckClick = new List<ShrineGoalRules.HeldStack> { new(DuckEgg, 20), new(Egg, 13) };
        Assert.Null(ShrineGoalRules.TakePlan(goal, duckClick, clickedSlot: 0));

        goal.Deposited = true;
        Assert.Null(ShrineGoalRules.TakePlan(goal, new List<ShrineGoalRules.HeldStack> { new(Egg, 13) }, 0));
    }

    [Fact]
    public void Shrine_candidates_skip_the_other_colour_of_a_board_egg()
    {
        var board = new Dictionary<string, string> { ["Pantry/0"] = "Animal/O 465 20/176 13 0/0/4" };
        ShrineGoalRules.BoardRefs refs = ShrineGoalRules.ReadBoard(board);
        IReadOnlyList<string> c = ShrineGoalRules.Candidates(new[] { BrownEgg, LargeBrownEgg, DuckEgg }, null, refs, _ => null);
        Assert.Equal(new[] { LargeBrownEgg, DuckEgg }.OrderBy(x => x, System.StringComparer.Ordinal), c);

        IReadOnlyList<string> c2 = ShrineGoalRules.Candidates(new[] { LargeEgg, DuckEgg },
            null, ShrineGoalRules.ReadBoard(new Dictionary<string, string>()), _ => null, alreadyTaken: new[] { "182" });
        Assert.Equal(new[] { DuckEgg }, c2);
    }
}
