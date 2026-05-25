using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunStateTests
{
    [Fact]
    public void New_run_state_starts_at_spring_one_week_one()
    {
        var run = new RunState();
        Assert.Equal(Season.Spring, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Equal(1, run.WeekOfYear);
        Assert.Equal(1, run.RunNumber);
        Assert.Empty(run.DonatedItemIds);
        Assert.Empty(run.ChampionedThemesThisMonth);
        Assert.Null(run.CurrentChampion);
    }

    [Fact]
    public void WeekOfYear_combines_season_and_day()
    {
        var run = new RunState { Season = Season.Summer, DayOfMonth = 8 };
        Assert.Equal(6, run.WeekOfYear); // month index 1 -> 4 weeks + week 2 = 6
    }

    [Fact]
    public void RecordDonation_is_idempotent_per_item_id()
    {
        var run = new RunState();
        run.RecordDonation("Parsnip");
        run.RecordDonation("Parsnip");
        Assert.Single(run.DonatedItemIds);
        Assert.Contains("Parsnip", run.DonatedSet());
    }

    [Fact]
    public void Champion_records_current_and_adds_to_month_set()
    {
        var run = new RunState();
        run.Champion(Theme.Mining);
        Assert.Equal(Theme.Mining, run.CurrentChampion);
        Assert.True(run.IsChampioned(Theme.Mining));
        run.Champion(Theme.Mining); // re-championing same theme does not duplicate
        Assert.Single(run.ChampionedThemesThisMonth);
    }

    [Fact]
    public void BeginNewMonth_advances_season_and_clears_championing_only()
    {
        var run = new RunState();
        run.RecordDonation("Parsnip");
        run.Champion(Theme.Mining);
        run.BeginNewMonth(Season.Summer);

        Assert.Equal(Season.Summer, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Empty(run.ChampionedThemesThisMonth);
        Assert.Null(run.CurrentChampion);
        Assert.Contains("Parsnip", run.DonatedSet()); // donations are cumulative across months
    }

    [Fact]
    public void BeginNewRun_resets_everything_and_bumps_run_number()
    {
        var run = new RunState { RunNumber = 3 };
        run.RecordDonation("Parsnip");
        run.Champion(Theme.Mining);
        run.Season = Season.Winter;
        run.DayOfMonth = 28;

        run.BeginNewRun(seed: 99);

        Assert.Equal(4, run.RunNumber);
        Assert.Equal(99, run.Seed);
        Assert.Equal(Season.Spring, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Empty(run.DonatedItemIds);
        Assert.Empty(run.ChampionedThemesThisMonth);
        Assert.Null(run.CurrentChampion);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var run = new RunState
        {
            Seed = 42, RunNumber = 2, Season = Season.Fall, DayOfMonth = 15,
            DonatedItemIds = { "Parsnip", "CopperBar" },
            ChampionedThemesThisMonth = { Theme.Mining },
            CurrentChampion = Theme.Mining
        };

        string json = JsonSerializer.Serialize(run);
        RunState restored = JsonSerializer.Deserialize<RunState>(json)!;

        Assert.Equal(42, restored.Seed);
        Assert.Equal(2, restored.RunNumber);
        Assert.Equal(Season.Fall, restored.Season);
        Assert.Equal(15, restored.DayOfMonth);
        Assert.Equal(new[] { "Parsnip", "CopperBar" }, restored.DonatedItemIds);
        Assert.Equal(new[] { Theme.Mining }, restored.ChampionedThemesThisMonth);
        Assert.Equal(Theme.Mining, restored.CurrentChampion);
    }
}
