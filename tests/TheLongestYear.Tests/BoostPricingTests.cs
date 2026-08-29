using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoostPricingTests
{
    private static int[] Levels(int farming = 0, int mining = 0) => new[] { farming, 0, 0, mining, 0 };

    [Theory]
    [InlineData(1, 0, 10)]   // first buy of level 1: 0.2 x 50
    [InlineData(2, 1, 60)]   // second buy at level 2: 0.2 x 100 x 3
    [InlineData(1, 2, 90)]   // third buy of level 1: 0.2 x 50 x 9
    [InlineData(9, 0, 250)]  // 0.2 x 1250
    public void Crash_course_price_table(int target, int boughtSoFar, long expected)
        => Assert.Equal(expected, BoostPricing.CrashCourseCost(target, boughtSoFar));

    [Fact]
    public void Crash_course_caps_at_two_per_skill_and_never_reaches_ten()
    {
        var run = new RunState();
        var ctx = new BoostContext(1, false, Levels(farming: 3), 0, Skill: 0);
        Assert.True(BoostPricing.CrashCourseAvailable(run, ctx));
        run.SkillLevelsBoughtThisLoop[0] = 2;
        Assert.False(BoostPricing.CrashCourseAvailable(run, ctx));
        var nine = new BoostContext(1, false, Levels(farming: 9), 0, Skill: 0);
        Assert.False(BoostPricing.CrashCourseAvailable(new RunState(), nine));
        Assert.False(BoostPricing.CrashCourseAvailable(new RunState(), ctx with { Skill = -1 }));
    }

    [Fact]
    public void Crash_course_purchase_through_TryBuy_prices_from_the_run_counter()
    {
        var meta = new MetaState { JunimoPoints = 1000 }; var run = new RunState();
        var farming = new BoostContext(1, false, Levels(), 0, Skill: 0);
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.CrashCourse, farming));
        Assert.Equal(990, meta.JunimoPoints);
        var farming1 = farming with { SkillLevels = Levels(farming: 1) };
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.CrashCourse, farming1));
        Assert.Equal(930, meta.JunimoPoints);          // 0.2 x 100 x 3 = 60
        Assert.Equal(2, run.SkillLevelsBoughtThisLoop[0]);
        Assert.Equal(BoostPurchase.Result.NotAvailable, BoostPurchase.TryBuy(meta, run, BoostId.CrashCourse, farming1));
        var mining = farming with { Skill = 3 };
        Assert.Equal(BoostPurchase.Result.Success, BoostPurchase.TryBuy(meta, run, BoostId.CrashCourse, mining));
        Assert.Equal(840, meta.JunimoPoints);          // 0.2 x 50 x 9 = 90
        Assert.Equal(3, run.ActiveBoosts[^1].Skill);
        Assert.Equal(112, run.ActiveBoosts[^1].ExpiresAfterDay);
    }

    [Theory]
    [InlineData(35, 40)] [InlineData(40, 50)] [InlineData(1, 10)] [InlineData(119, 120)]
    public void Elevator_landing_is_the_next_multiple_of_ten(int floor, int landing)
        => Assert.Equal(landing, BoostPricing.ElevatorLanding(floor));

    [Theory]
    [InlineData(10, 15)] [InlineData(20, 35)] [InlineData(120, 235)]
    public void Elevator_pass_is_a_fifth_of_the_keep_row(int landing, long cost)
        => Assert.Equal(cost, BoostPricing.ElevatorPassCost(landing));

    [Fact]
    public void Elevator_pass_unavailable_before_the_mine_and_at_the_bottom()
    {
        Assert.False(BoostPricing.ElevatorPassAvailable(0));
        Assert.True(BoostPricing.ElevatorPassAvailable(5));
        Assert.False(BoostPricing.ElevatorPassAvailable(120));
    }

    [Fact]
    public void Elevator_pass_is_repeatable_and_priced_per_landing()
    {
        var meta = new MetaState { JunimoPoints = 1000 }; var run = new RunState();
        Assert.Equal(BoostPurchase.Result.Success,
            BoostPurchase.TryBuy(meta, run, BoostId.ElevatorPass, new BoostContext(1, false, Levels(), 35)));
        Assert.Equal(1000 - 75, meta.JunimoPoints);    // landing 40: 0.2 x 375
        Assert.Equal(BoostPurchase.Result.Success,
            BoostPurchase.TryBuy(meta, run, BoostId.ElevatorPass, new BoostContext(1, false, Levels(), 40)));
        Assert.Equal(2, run.ActiveBoosts.Count);
        Assert.Equal(BoostPurchase.Result.NotAvailable,
            BoostPurchase.TryBuy(meta, run, BoostId.ElevatorPass, new BoostContext(1, false, Levels(), 0)));
    }
}
