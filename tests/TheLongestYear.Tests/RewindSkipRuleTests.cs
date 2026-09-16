using System.Collections.Generic;
using TheLongestYear.Core.Rewind;
using Xunit;

namespace TheLongestYear.Tests;

public class RewindSkipRuleTests
{
    [Fact]
    public void A_first_rewind_on_a_fresh_save_cannot_be_skipped()
        => Assert.False(RewindSkipRule.IsSkippable(new HashSet<string>(), completedResets: 0));

    [Fact]
    public void A_watched_rewind_can_be_skipped()
        => Assert.True(RewindSkipRule.IsSkippable(new HashSet<string> { RewindSkipRule.SeenName }, completedResets: 0));

    [Fact]
    public void A_save_that_already_finished_a_loop_counts_as_having_seen_it()
        => Assert.True(RewindSkipRule.IsSkippable(new HashSet<string>(), completedResets: 1));

    [Fact]
    public void A_missing_seen_set_is_treated_as_empty()
        => Assert.False(RewindSkipRule.IsSkippable(null, completedResets: 0));

    [Fact]
    public void The_seen_name_does_not_collide_with_a_season_turn()
    {
        foreach (TheLongestYear.Core.SeasonTurnKind kind in System.Enum.GetValues<TheLongestYear.Core.SeasonTurnKind>())
            Assert.NotEqual(TheLongestYear.Core.SeasonTurn.SeenName(kind), RewindSkipRule.SeenName);
    }
}
