using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-09: a held board keeps its count; the dial only changes the next new board.</summary>
public class BundleCountStampTests
{
    private static DifficultyProfile Fresh(DifficultyStep step)
        => DifficultyResolver.Resolve(new DifficultySettings { BundleCount = step }, new GameplayConfig());

    [Fact]
    public void A_new_board_takes_the_freshly_resolved_rule()
    {
        var fresh = Fresh(DifficultyStep.Hard);
        BundleCountStamp.ForReset(fresh, BundleCountRule.For(DifficultyStep.Easy), holdingBoard: false);
        Assert.Equal(BundleCountRule.For(DifficultyStep.Hard), fresh.BundleCount);
    }

    [Fact]
    public void A_held_board_keeps_the_rule_it_was_built_with()
    {
        var fresh = Fresh(DifficultyStep.Hard);
        BundleCountStamp.ForReset(fresh, BundleCountRule.For(DifficultyStep.Easy), holdingBoard: true);
        Assert.Equal(BundleCountRule.For(DifficultyStep.Easy), fresh.BundleCount);
    }

    /// <summary>A board built before the dial existed had no rule; holding it keeps it that way.</summary>
    [Fact]
    public void A_held_board_from_before_the_dial_stays_unchanged()
    {
        var fresh = Fresh(DifficultyStep.Extreme);
        BundleCountStamp.ForReset(fresh, previous: null, holdingBoard: true);
        Assert.Null(fresh.BundleCount);
    }
}
