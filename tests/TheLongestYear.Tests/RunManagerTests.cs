using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunManagerTests
{
    private static CcItem Item(string id, Theme theme, params Season[] seasons)
        => new CcItem(id, theme, Rarity.Common, new HashSet<Season>(seasons));

    /// <summary>Catalog with one single-season item per season + one year-round multi-season item.</summary>
    private static List<CcItem> Catalog() => new()
    {
        Item("spring-only", Theme.Foraging, Season.Spring),
        Item("summer-only", Theme.Farming,  Season.Summer),
        Item("fall-only",   Theme.Fishing,  Season.Fall),
        Item("winter-only", Theme.Mining,   Season.Winter),
        Item("year-round",  Theme.Mixed,    Season.Spring, Season.Summer, Season.Fall, Season.Winter),
    };

    private static RunManager Mgr() => new RunManager(new GateEvaluator());

    [Fact]
    public void Midweek_continues()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 3 };
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, Catalog()));
    }

    [Fact]
    public void Week_end_without_champion_now_continues()
    {
        // Spec change 2026-05-26: no weekly fail; an un-championed week 7/14/21 is fine.
        var run = new RunState { Season = Season.Spring, DayOfMonth = 7 };
        Assert.Equal(RunAction.Continue, Mgr().EvaluateDayEnd(run, Catalog()));
    }

    [Fact]
    public void Month_end_with_single_season_donated_advances()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        run.RecordDonation("spring-only");
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, Catalog()));
    }

    [Fact]
    public void Month_end_missing_single_season_fails()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        // "spring-only" not donated
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, Catalog()));
    }

    [Fact]
    public void Month_end_only_needs_this_season_items()
    {
        // Summer-only / Fall-only / Winter-only not donated yet — Spring shouldn't care.
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        run.RecordDonation("spring-only");
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, Catalog()));
    }

    [Fact]
    public void Month_end_does_not_require_multi_season_until_winter()
    {
        // "year-round" not donated — passes Spring/Summer/Fall gates.
        var run = new RunState { Season = Season.Fall, DayOfMonth = 28 };
        run.RecordDonation("spring-only");
        run.RecordDonation("summer-only");
        run.RecordDonation("fall-only");
        Assert.Equal(RunAction.AdvanceMonth, Mgr().EvaluateDayEnd(run, Catalog()));
    }

    [Fact]
    public void Winter_end_with_full_cc_wins()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        run.RecordDonation("spring-only");
        run.RecordDonation("summer-only");
        run.RecordDonation("fall-only");
        run.RecordDonation("winter-only");
        run.RecordDonation("year-round");
        Assert.Equal(RunAction.Win, Mgr().EvaluateDayEnd(run, Catalog()));
    }

    [Fact]
    public void Winter_end_missing_multi_season_fails()
    {
        // Winter passes its single-season gate but "year-round" was never donated -> no future
        // season for it to land in -> fail.
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        run.RecordDonation("spring-only");
        run.RecordDonation("summer-only");
        run.RecordDonation("fall-only");
        run.RecordDonation("winter-only");
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, Catalog()));
    }

    [Fact]
    public void Winter_end_missing_winter_only_fails()
    {
        var run = new RunState { Season = Season.Winter, DayOfMonth = 28 };
        run.RecordDonation("spring-only");
        run.RecordDonation("summer-only");
        run.RecordDonation("fall-only");
        run.RecordDonation("year-round");
        // "winter-only" missing
        Assert.Equal(RunAction.FailReset, Mgr().EvaluateDayEnd(run, Catalog()));
    }
}
