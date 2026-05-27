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
        Assert.Empty(run.SelectedThemesThisMonth);
        Assert.Null(run.CurrentSelection);
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
    public void Select_records_current_and_adds_to_month_set()
    {
        var run = new RunState();
        run.Select(Theme.Mining);
        Assert.Equal(Theme.Mining, run.CurrentSelection);
        Assert.True(run.IsSelected(Theme.Mining));
        run.Select(Theme.Mining); // re-selecting same theme does not duplicate
        Assert.Single(run.SelectedThemesThisMonth);
    }

    [Fact]
    public void BeginNewMonth_advances_season_and_clears_selections_only()
    {
        var run = new RunState();
        run.RecordDonation("Parsnip");
        run.Select(Theme.Mining);
        run.BeginNewMonth(Season.Summer);

        Assert.Equal(Season.Summer, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Empty(run.SelectedThemesThisMonth);
        Assert.Null(run.CurrentSelection);
        Assert.Contains("Parsnip", run.DonatedSet()); // donations are cumulative across months
    }

    [Fact]
    public void BeginNewRun_resets_everything_and_bumps_run_number()
    {
        var run = new RunState { RunNumber = 3 };
        run.RecordDonation("Parsnip");
        run.Select(Theme.Mining);
        run.Season = Season.Winter;
        run.DayOfMonth = 28;

        run.BeginNewRun(seed: 99);

        Assert.Equal(4, run.RunNumber);
        Assert.Equal(99, run.Seed);
        Assert.Equal(Season.Spring, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Empty(run.DonatedItemIds);
        Assert.Empty(run.SelectedThemesThisMonth);
        Assert.Null(run.CurrentSelection);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var run = new RunState
        {
            Seed = 42, RunNumber = 2, Season = Season.Fall, DayOfMonth = 15,
            DonatedItemIds = { "Parsnip", "CopperBar" },
            SelectedThemesThisMonth = { Theme.Mining },
            CurrentSelection = Theme.Mining
        };

        string json = JsonSerializer.Serialize(run);
        RunState restored = JsonSerializer.Deserialize<RunState>(json)!;

        Assert.Equal(42, restored.Seed);
        Assert.Equal(2, restored.RunNumber);
        Assert.Equal(Season.Fall, restored.Season);
        Assert.Equal(15, restored.DayOfMonth);
        Assert.Equal(new[] { "Parsnip", "CopperBar" }, restored.DonatedItemIds);
        Assert.Equal(new[] { Theme.Mining }, restored.SelectedThemesThisMonth);
        Assert.Equal(Theme.Mining, restored.CurrentSelection);
    }

    [Fact]
    public void TryMarkBundleAwarded_is_true_once_then_false()
    {
        var run = new RunState();
        Assert.True(run.TryMarkBundleAwarded(7));
        Assert.False(run.TryMarkBundleAwarded(7));
        Assert.True(run.TryMarkBundleAwarded(8));
    }

    [Fact]
    public void TryMarkRoomAwarded_is_true_once_then_false()
    {
        var run = new RunState();
        Assert.True(run.TryMarkRoomAwarded(0));
        Assert.False(run.TryMarkRoomAwarded(0));
    }

    [Fact]
    public void BeginNewRun_clears_completion_awards()
    {
        var run = new RunState();
        run.TryMarkBundleAwarded(1);
        run.TryMarkRoomAwarded(2);

        run.BeginNewRun(seed: 5);

        Assert.True(run.TryMarkBundleAwarded(1)); // awardable again in the fresh run
        Assert.True(run.TryMarkRoomAwarded(2));
    }

    [Fact]
    public void BeginNewRun_clears_vault_payments()
    {
        var run = new RunState();
        run.VaultBundlesPaid.Add(VaultRules.Vault2500);
        run.VaultBundlesPaid.Add(VaultRules.Vault5000);

        run.BeginNewRun(seed: 5);

        Assert.Empty(run.VaultBundlesPaid);
    }

    [Fact]
    public void OfferPresentedWeek_defaults_to_negative_one()
        => Assert.Equal(-1, new RunState().OfferPresentedWeek);

    [Fact]
    public void BeginNewRun_resets_OfferPresentedWeek()
    {
        var run = new RunState();
        run.OfferPresentedWeek = 5;
        run.BeginNewRun(seed: 1);
        Assert.Equal(-1, run.OfferPresentedWeek);
    }

    [Fact]
    public void OfferPresentedWeek_round_trips_through_json()
    {
        var run = new RunState { OfferPresentedWeek = 7 };
        string json = System.Text.Json.JsonSerializer.Serialize(run);
        RunState restored = System.Text.Json.JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(7, restored.OfferPresentedWeek);
    }

    [Fact]
    public void BeginNewMonth_clears_CurrentWeekBonusItems()
    {
        var run = new RunState { Season = Season.Spring };
        run.CurrentWeekBonusItems.Add("(O)24");
        run.BeginNewMonth(Season.Summer);
        Assert.Empty(run.CurrentWeekBonusItems);
    }

    [Fact]
    public void BeginNewMonth_consumes_NextMonthSelection_as_current()
    {
        // Day 28 pre-pick -> NextMonthSelection. Tomorrow's OnDayStarted calls BeginNewMonth,
        // which should promote it to CurrentSelection and clear NextMonthSelection.
        var run = new RunState { Season = Season.Spring };
        run.NextMonthSelection = Theme.Fishing;

        run.BeginNewMonth(Season.Summer);

        Assert.Equal(Theme.Fishing, run.CurrentSelection);
        Assert.Contains(Theme.Fishing, run.SelectedThemesThisMonth);
        Assert.Null(run.NextMonthSelection);
    }

    [Fact]
    public void BeginNewMonth_with_no_NextMonthSelection_leaves_current_null()
    {
        var run = new RunState { Season = Season.Spring };
        run.CurrentSelection = Theme.Foraging; // last month's selection
        run.BeginNewMonth(Season.Summer);
        Assert.Null(run.CurrentSelection);
    }

    [Fact]
    public void BeginNewRun_clears_NextMonthSelection()
    {
        var run = new RunState { NextMonthSelection = Theme.Mining };
        run.BeginNewRun(seed: 1);
        Assert.Null(run.NextMonthSelection);
    }

    [Fact]
    public void PeakMineFloor_defaults_to_zero_and_round_trips_through_json()
    {
        var fresh = new RunState();
        Assert.Equal(0, fresh.PeakMineFloor);

        var original = new RunState { PeakMineFloor = 65 };
        string json = System.Text.Json.JsonSerializer.Serialize(original);
        RunState restored = System.Text.Json.JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(65, restored.PeakMineFloor);
    }

    [Fact]
    public void BeginNewRun_resets_PeakMineFloor()
    {
        var run = new RunState { PeakMineFloor = 90 };
        run.BeginNewRun(seed: 42);
        Assert.Equal(0, run.PeakMineFloor);
    }

    [Fact]
    public void RecordMineFloor_takes_the_max_and_ignores_shallower_floors()
    {
        var run = new RunState();
        run.RecordMineFloor(20);
        Assert.Equal(20, run.PeakMineFloor);
        run.RecordMineFloor(10);
        Assert.Equal(20, run.PeakMineFloor);
        run.RecordMineFloor(45);
        Assert.Equal(45, run.PeakMineFloor);
    }
}
