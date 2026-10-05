using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Keep Special Orders Board (Jeff, 2026-10-05; elaineofshalott asked on Nexus): the town
/// board outside Mayor Lewis's house is open from Spring 1 of every loop. Unlocks once a run reached the day
/// the vanilla board opens (Fall 2, DaysPlayed 58).</summary>
public class SpecialOrdersBoardKeepTests
{
    public SpecialOrdersBoardKeepTests() => I18nFixture.InstallGlobalProvider();

    [Fact]
    public void Catalog_entry_is_a_flat_1500_buildings_keep_gated_on_the_board_opening()
    {
        UpgradeDefinition? row = UpgradeCatalog.TryGet(SpecialOrdersBoardKeep.UpgradeId);
        Assert.NotNull(row);
        Assert.Equal("keep_special_orders_board", row!.Id);
        Assert.Equal(UpgradeCategory.Buildings, row.Category);
        Assert.Equal(1500L, row.Cost);
        Assert.Equal("special_orders", row.RunReachRequirement);
        Assert.Null(row.PrerequisiteId);
        Assert.Null(row.MetaRequirement);
        Assert.False(GiftLadder.IsGift(row));
    }

    [Fact]
    public void Reach_gate_parses_as_a_bare_flag()
    {
        RunReachRequirement? r = RunReachRequirement.Parse(SpecialOrdersBoardKeep.ReachMetric);
        Assert.NotNull(r);
        Assert.Equal("special_orders", r!.Metric);
        Assert.Null(r.Key);
        Assert.Equal(1, r.Threshold);
    }

    [Theory]
    [InlineData(1u, 0)]     // Spring 1
    [InlineData(56u, 0)]    // Summer 28
    [InlineData(57u, 0)]    // Fall 1: the morning a Summer fail opens the perk screen; the board never opened
    [InlineData(58u, 1)]    // Fall 2: vanilla opens the board
    [InlineData(85u, 1)]    // Winter 1: the morning a Fall fail opens the perk screen
    [InlineData(113u, 1)]   // Spring 1 of year 2: a won run
    public void Reach_counts_once_the_run_reached_fall_2(uint daysPlayed, int expected)
        => Assert.Equal(expected, SpecialOrdersBoardKeep.ReachValue(daysPlayed));

    [Theory]
    [InlineData(false, 1u, false)]
    [InlineData(false, 57u, false)]
    [InlineData(false, 58u, true)]
    [InlineData(true, 1u, true)]
    [InlineData(true, 57u, true)]
    public void Board_is_open_when_owned_or_vanilla_has_opened_it(bool owned, uint daysPlayed, bool expected)
        => Assert.Equal(expected, SpecialOrdersBoardKeep.IsBoardOpen(owned, daysPlayed));

    [Fact]
    public void Plan_tab_locked_text_names_fall_2()
        => Assert.Equal("unlocked once the Special Orders board opens (Fall 2)",
            ReachText.Describe(SpecialOrdersBoardKeep.ReachMetric));

    [Fact]
    public void Strings_are_plain_and_have_no_em_dashes()
    {
        UpgradeDefinition row = UpgradeCatalog.TryGet(SpecialOrdersBoardKeep.UpgradeId)!;
        Assert.Equal("Keep Special Orders Board", row.DisplayName);
        Assert.DoesNotContain("—", row.Description);
        Assert.DoesNotContain("—", ReachText.Describe(SpecialOrdersBoardKeep.ReachMetric));
        Assert.Contains("Spring 1", row.Description);
    }
}
