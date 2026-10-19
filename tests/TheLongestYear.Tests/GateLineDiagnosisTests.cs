using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Why an unmet season-gate line could not be asked this week, for the Warn log a short
/// or empty offer writes (player report, 0.19.97), and the no-theme week's HUD key.</summary>
public class GateLineDiagnosisTests
{
    private static readonly Theme[] None = Array.Empty<Theme>();

    private static GateLineVerdict Classify(
        GoalBlock block, Theme[] pool, Theme[] sample, Theme[] offer, Theme[] picked)
        => GateLineDiagnosis.Classify(block, pool, sample, offer, picked);

    [Fact]
    public void A_line_an_offered_theme_asks_for_is_askable()
        => Assert.Equal(UnaskableReason.Askable,
            Classify(GoalBlock.None, new[] { Theme.Mixed }, new[] { Theme.Mixed }, new[] { Theme.Mixed }, new[] { Theme.Mixed }).Reason);

    [Fact]
    public void A_line_no_pool_holds_and_not_yet_obtainable_is_reported_as_such()
    {
        Assert.Equal(UnaskableReason.NotObtainableThisWeek, Classify(GoalBlock.NotYet, None, None, None, None).Reason);
        Assert.Equal(UnaskableReason.NotObtainableThisWeek, Classify(GoalBlock.OutOfSeason, None, None, None, None).Reason);
    }

    [Fact]
    public void A_line_the_availability_model_never_placed_is_unreachable()
        => Assert.Equal(UnaskableReason.Unreachable, Classify(GoalBlock.NotPlaced, None, None, None, None).Reason);

    [Fact]
    public void An_obtainable_line_no_theme_pools_is_not_covered()
        => Assert.Equal(UnaskableReason.NoThemeCovers, Classify(GoalBlock.None, None, None, new[] { Theme.Fishing }, None).Reason);

    [Fact]
    public void A_pooled_line_overrides_the_obtainability_signal()
    {
        // A stretch line is in the pool even while the model says "not yet".
        var verdict = Classify(GoalBlock.NotYet, new[] { Theme.Farming }, None, None, None);
        Assert.Equal(UnaskableReason.LeftOutOfList, verdict.Reason);
        Assert.Equal(new[] { Theme.Farming }, verdict.Themes);
    }

    [Fact]
    public void A_line_only_picked_themes_ask_for_names_them()
    {
        var verdict = Classify(GoalBlock.None, new[] { Theme.Mixed }, new[] { Theme.Mixed }, new[] { Theme.Fishing }, new[] { Theme.Mixed });
        Assert.Equal(UnaskableReason.ThemesPickedThisMonth, verdict.Reason);
        Assert.Equal(new[] { Theme.Mixed }, verdict.Themes);
    }

    [Fact]
    public void A_line_an_undealt_theme_asks_for_names_the_unpicked_theme()
    {
        var verdict = Classify(GoalBlock.None, new[] { Theme.Kitchen, Theme.Mixed }, new[] { Theme.Kitchen, Theme.Mixed },
            new[] { Theme.Fishing }, new[] { Theme.Mixed });
        Assert.Equal(UnaskableReason.ThemeNotDealt, verdict.Reason);
        Assert.Equal(new[] { Theme.Kitchen }, verdict.Themes);
    }

    [Fact]
    public void Describe_gives_a_readable_reason_for_every_verdict()
    {
        foreach (UnaskableReason reason in Enum.GetValues(typeof(UnaskableReason)))
            Assert.False(string.IsNullOrWhiteSpace(GateLineDiagnosis.Describe(new GateLineVerdict(reason, new[] { Theme.Mixed }))));
        Assert.Contains("Mixed", GateLineDiagnosis.Describe(new GateLineVerdict(UnaskableReason.ThemesPickedThisMonth, new[] { Theme.Mixed })));
    }

    [Fact]
    public void Blocker_matches_the_obtainability_rule()
    {
        var spring = new HashSet<Season> { Season.Spring };
        int summerWeek = AvailabilityWeeks.LastWeekOf(Season.Summer);
        Assert.Equal(GoalBlock.OutOfSeason, GoalObtainability.BlockerFor(spring, null, "(O)24", summerWeek));
        Assert.Equal(GoalBlock.None, GoalObtainability.BlockerFor(spring, null, "(O)24", 1));
        Assert.Equal(GoalBlock.None, GoalObtainability.BlockerFor(null, null, "(O)24", summerWeek));
    }

    [Fact]
    public void Items_due_sums_every_unmet_bundle_for_the_season()
    {
        var a = BundleRequirement.CreateSeasonal("A", Theme.Farming, new[] { "(O)24", "(O)188" }, Season.Spring, bundleIndex: 1);
        var b = BundleRequirement.CreateSeasonal("B", Theme.Fishing, new[] { "(O)128" }, Season.Spring, bundleIndex: 2);
        var later = BundleRequirement.CreateSeasonal("C", Theme.Mining, new[] { "(O)334" }, Season.Fall, bundleIndex: 3);
        var ledger = new SlotLedger();
        ledger.Add(1, 0, "(O)24");
        Assert.Equal(2, GateLineDiagnosis.ItemsDue(new[] { a, b, later }, Season.Spring, ledger));
        ledger.Add(1, 1, "(O)188");
        ledger.Add(2, 0, "(O)128");
        Assert.Equal(0, GateLineDiagnosis.ItemsDue(new[] { a, b, later }, Season.Spring, ledger));
    }

    [Theory]
    [InlineData(0, NoThemeWeekMessage.GateMet)]
    [InlineData(1, NoThemeWeekMessage.OneItemDue)]
    [InlineData(5, NoThemeWeekMessage.ItemsDue)]
    public void The_no_theme_week_message_fits_what_the_season_still_needs(int itemsDue, NoThemeWeekMessage expected)
        => Assert.Equal(expected, GateLineDiagnosis.NoThemeWeekMessageFor(itemsDue));
}
