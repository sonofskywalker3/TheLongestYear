using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SeasonPityTests
{
    private static GameplayConfig Cfg(bool enabled = true, int threshold = 5, double step = 0.10, double floor = 0.50, int trim = 2)
        => new() { PityEnabled = enabled, PityThreshold = threshold, PityQuotaStep = step, PityQuotaFloor = floor, PityTrimPerStep = trim };

    [Fact]
    public void RecordFail_increments_only_that_season_and_remembers_it()
    {
        var s = new MetaState();
        SeasonPity.RecordFail(s, Season.Summer);
        SeasonPity.RecordFail(s, Season.Summer);
        Assert.Equal(new List<int> { 0, 2, 0, 0 }, s.SeasonFailCounts);
        Assert.Equal((int)Season.Summer, s.LastFailSeason);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5, 0)]
    [InlineData(6, 1)]
    [InlineData(9, 4)]
    public void EaseSteps_is_fails_beyond_threshold(int fails, int expected)
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { fails, 0, 0, 0 } };
        Assert.Equal(expected, SeasonPity.EaseSteps(s, Season.Spring, Cfg()));
    }

    [Fact]
    public void EaseSteps_is_zero_when_disabled_but_counting_continues()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 8, 0, 0, 0 } };
        Assert.Equal(0, SeasonPity.EaseSteps(s, Season.Spring, Cfg(enabled: false)));
        SeasonPity.RecordFail(s, Season.Spring);
        Assert.Equal(9, s.SeasonFailCounts[0]);
    }

    [Fact]
    public void RecordPass_drops_to_threshold_never_raises()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 9, 2, 0, 0 } };
        SeasonPity.RecordPass(s, Season.Spring, Cfg());
        SeasonPity.RecordPass(s, Season.Summer, Cfg());
        Assert.Equal(5, s.SeasonFailCounts[0]);
        Assert.Equal(2, s.SeasonFailCounts[1]);
    }

    [Fact]
    public void QuotaFactor_steps_down_and_floors()
    {
        Assert.Equal(1.0, SeasonPity.QuotaFactor(0, Cfg()), 6);
        Assert.Equal(0.8, SeasonPity.QuotaFactor(2, Cfg()), 6);
        Assert.Equal(0.5, SeasonPity.QuotaFactor(9, Cfg()), 6);
    }

    [Fact]
    public void TrimUnits_scales_per_step()
    {
        Assert.Equal(0, SeasonPity.TrimUnits(0, Cfg()));
        Assert.Equal(6, SeasonPity.TrimUnits(3, Cfg()));
    }

    [Fact]
    public void StampReshuffleTrim_records_season_and_units_or_clears()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 0, 7, 0, 0 }, LastFailSeason = 1 };
        SeasonPity.StampReshuffleTrim(s, Cfg());
        Assert.Equal(1, s.BoardTrimSeason);
        Assert.Equal(4, s.BoardTrimSteps);   // (7-5) steps * 2 per step

        var none = new MetaState { SeasonFailCounts = new List<int> { 3, 0, 0, 0 }, LastFailSeason = 0 };
        SeasonPity.StampReshuffleTrim(none, Cfg());
        Assert.Equal(-1, none.BoardTrimSeason);
        Assert.Equal(0, none.BoardTrimSteps);
    }

    [Fact]
    public void Counts_pad_to_four_when_short_or_missing()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 2 } };
        Assert.Equal(0, SeasonPity.EaseSteps(s, Season.Winter, Cfg()));
        SeasonPity.RecordFail(s, Season.Winter);
        Assert.Equal(new List<int> { 2, 0, 0, 1 }, s.SeasonFailCounts);
    }

    [Fact]
    public void DisplaySteps_uses_quota_ease_stamp_else_board_trim()
    {
        var eased = new MetaState { BoardEaseSeason = (int)Season.Spring, BoardEaseSteps = 2, BoardTrimSeason = -1 };
        Assert.Equal(2, SeasonPity.DisplaySteps(eased, Cfg()));
        var shuffled = new MetaState { BoardEaseSeason = -1, BoardTrimSeason = 0, BoardTrimSteps = 4 };
        Assert.Equal(2, SeasonPity.DisplaySteps(shuffled, Cfg()));   // 4 units / 2 per step
        Assert.Equal(0, SeasonPity.DisplaySteps(new MetaState(), Cfg()));
    }

    [Fact]
    public void CurrentQuotaEase_is_null_without_a_stamp_or_when_disabled()
    {
        var cfg = Cfg();
        Assert.Null(SeasonPity.CurrentQuotaEase(new MetaState(), cfg));
        var stamped = new MetaState { BoardEaseSeason = (int)Season.Spring, BoardEaseSteps = 2 };
        Assert.Null(SeasonPity.CurrentQuotaEase(stamped, Cfg(enabled: false)));
        var ease = SeasonPity.CurrentQuotaEase(stamped, cfg);
        Assert.NotNull(ease);
        Assert.Equal(Season.Spring, ease!.Season);
        Assert.Equal(2, ease.Steps);
        Assert.Equal(0.8, ease.Factor, 6);
    }

    [Fact]
    public void CurrentQuotaEase_reads_the_stamp_not_live_counts()
    {
        // Fail counters have already dropped back to the threshold (RecordPass ran), but the
        // stamp from the keep choice still governs the requirements for this held board.
        var s = new MetaState
        {
            BoardEaseSeason = (int)Season.Spring,
            BoardEaseSteps = 2,
            SeasonFailCounts = new List<int> { 5, 0, 0, 0 },
        };
        var ease = SeasonPity.CurrentQuotaEase(s, Cfg());
        Assert.NotNull(ease);
        Assert.Equal(Season.Spring, ease!.Season);
        Assert.Equal(2, ease.Steps);
        Assert.Equal(0.8, ease.Factor, 6);
    }

    [Fact]
    public void CurrentQuotaEase_is_null_for_Winter_even_past_the_threshold()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 0, 0, 0, 8 }, LastFailSeason = (int)Season.Winter };
        SeasonPity.StampKeepEase(s, Cfg());
        Assert.Equal(-1, s.BoardEaseSeason);
        Assert.Equal(0, s.BoardEaseSteps);
        Assert.Null(SeasonPity.CurrentQuotaEase(s, Cfg()));
    }

    [Fact]
    public void StampKeepEase_records_or_clears()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 7, 0, 0, 0 }, LastFailSeason = 0 };
        SeasonPity.StampKeepEase(s, Cfg());
        Assert.Equal(0, s.BoardEaseSeason);
        Assert.Equal(2, s.BoardEaseSteps);

        var none = new MetaState { SeasonFailCounts = new List<int> { 3, 0, 0, 0 }, LastFailSeason = 0 };
        SeasonPity.StampKeepEase(none, Cfg());
        Assert.Equal(-1, none.BoardEaseSeason);
        Assert.Equal(0, none.BoardEaseSteps);
    }

    [Fact]
    public void StampReshuffleTrim_clears_the_ease_stamp()
    {
        var s = new MetaState
        {
            BoardEaseSeason = 1,
            BoardEaseSteps = 3,
            SeasonFailCounts = new List<int> { 0, 0, 0, 0 },
            LastFailSeason = -1,
        };
        SeasonPity.StampReshuffleTrim(s, Cfg());
        Assert.Equal(-1, s.BoardEaseSeason);
        Assert.Equal(0, s.BoardEaseSteps);
    }

    [Fact]
    public void Ease_survives_RecordPass_within_the_loop()
    {
        var s = new MetaState { SeasonFailCounts = new List<int> { 7, 0, 0, 0 }, LastFailSeason = 0 };
        SeasonPity.StampKeepEase(s, Cfg());
        SeasonPity.RecordPass(s, Season.Spring, Cfg());   // counter drops to threshold mid-loop
        var ease = SeasonPity.CurrentQuotaEase(s, Cfg());
        Assert.NotNull(ease);
        Assert.Equal(Season.Spring, ease!.Season);
        Assert.Equal(2, ease.Steps);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void IsValidSeasonIndex_is_0_to_3(int index, bool expected)
        => Assert.Equal(expected, SeasonPity.IsValidSeasonIndex(index));
}
