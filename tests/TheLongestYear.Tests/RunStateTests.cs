using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunStateTests
{
    // Nexus report gmastern1, 2026-09-12: slept on a failed Spring 28, the night save wrote
    // Summer 1, quit before the morning rewind, and the reload rolled the run into Summer.
    [Fact]
    public void Pending_day28_branch_survives_a_save_round_trip()
    {
        var run = new RunState { PendingDay28 = TheLongestYear.Core.Day28.Day28Branch.Fail };
        RunState back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(run))!;
        Assert.Equal(TheLongestYear.Core.Day28.Day28Branch.Fail, back.PendingDay28);
    }

    [Fact]
    public void A_pending_day28_outcome_blocks_the_load_time_month_rollover()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28, PendingDay28 = TheLongestYear.Core.Day28.Day28Branch.Fail };
        Assert.False(run.OwesMonthRolloverOnLoad(Season.Summer));
    }

    [Fact]
    public void Without_a_pending_outcome_a_new_calendar_month_still_rolls_over_on_load()
    {
        var run = new RunState { Season = Season.Spring, DayOfMonth = 28 };
        Assert.True(run.OwesMonthRolloverOnLoad(Season.Summer));
        Assert.False(run.OwesMonthRolloverOnLoad(Season.Spring));
    }

    [Fact]
    public void New_run_state_starts_at_spring_one_week_one()
    {
        var run = new RunState();
        Assert.Equal(Season.Spring, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Equal(1, run.WeekOfYear);
        Assert.Equal(1, run.RunNumber);
        Assert.Empty(run.DonatedItemIds);
        Assert.Empty(run.DonatedSlots);
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
    public void RecordDonation_is_idempotent_per_slot_and_keeps_two_slots_with_one_id()
    {
        var run = new RunState();
        Assert.True(run.RecordDonation(7, 0, "(O)388"));
        Assert.False(run.RecordDonation(7, 0, "(O)388"));   // same slot twice
        Assert.True(run.RecordDonation(7, 1, "(O)388"));    // Construction's second Wood slot
        Assert.Equal(2, run.DonatedSlots.Count);
        SlotLedger ledger = run.DonatedLedger();
        Assert.True(ledger.IsFilled(7, 0));
        Assert.True(ledger.IsFilled(7, 1));
        Assert.False(ledger.IsFilled(7, 2));
        Assert.Equal(2, ledger.FilledCount(7));
        Assert.Equal(0, ledger.FilledCount(8));
        Assert.Contains("(O)388", ledger.ItemIds);
    }

    [Fact]
    public void ReplaceDonations_replaces_the_whole_ledger()
    {
        var run = new RunState();
        run.RecordDonation(1, 0, "(O)24");
        run.ReplaceDonations(new[] { new DonatedSlot { BundleIndex = 2, IngredientIndex = 3, ItemId = "(O)190" } });
        Assert.Single(run.DonatedSlots);
        Assert.True(run.DonatedLedger().IsFilled(2, 3));
        Assert.False(run.DonatedLedger().IsFilled(1, 0));
    }

    [Fact]
    public void Legacy_DonatedItemIds_deserializes_but_is_not_the_ledger()
    {
        string json = "{\"DonatedItemIds\":[\"(O)24\"],\"Season\":0,\"DayOfMonth\":1}";
        RunState restored = JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(new[] { "(O)24" }, restored.DonatedItemIds);
        Assert.Empty(restored.DonatedSlots);
        Assert.Equal(0, restored.DonatedLedger().Count);
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
        run.RecordDonation(0, 0, "Parsnip");
        run.Select(Theme.Mining);
        run.BeginNewMonth(Season.Summer);

        Assert.Equal(Season.Summer, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Empty(run.SelectedThemesThisMonth);
        Assert.Null(run.CurrentSelection);
        Assert.Single(run.DonatedSlots); // donations are cumulative across months
    }

    [Fact]
    public void BeginNewRun_resets_everything_and_bumps_run_number()
    {
        var run = new RunState { RunNumber = 3 };
        run.RecordDonation(0, 0, "Parsnip");
        run.Select(Theme.Mining);
        run.Season = Season.Winter;
        run.DayOfMonth = 28;

        run.BeginNewRun(seed: 99);

        Assert.Equal(4, run.RunNumber);
        Assert.Equal(99, run.Seed);
        Assert.Equal(Season.Spring, run.Season);
        Assert.Equal(1, run.DayOfMonth);
        Assert.Empty(run.DonatedItemIds);
        Assert.Empty(run.DonatedSlots);
        Assert.Empty(run.SelectedThemesThisMonth);
        Assert.Null(run.CurrentSelection);
    }

    [Fact]
    public void Round_trips_through_json()
    {
        var run = new RunState
        {
            Seed = 42, RunNumber = 2, Season = Season.Fall, DayOfMonth = 15,
            DonatedSlots = { new DonatedSlot { BundleIndex = 3, IngredientIndex = 1, ItemId = "Parsnip" } },
            SelectedThemesThisMonth = { Theme.Mining },
            CurrentSelection = Theme.Mining
        };

        string json = JsonSerializer.Serialize(run);
        RunState restored = JsonSerializer.Deserialize<RunState>(json)!;

        Assert.Equal(42, restored.Seed);
        Assert.Equal(2, restored.RunNumber);
        Assert.Equal(Season.Fall, restored.Season);
        Assert.Equal(15, restored.DayOfMonth);
        Assert.Single(restored.DonatedSlots);
        Assert.Equal(3, restored.DonatedSlots[0].BundleIndex);
        Assert.Equal(1, restored.DonatedSlots[0].IngredientIndex);
        Assert.Equal("Parsnip", restored.DonatedSlots[0].ItemId);
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
    public void BeginNewRun_forgets_the_theme_week_discount()
    {
        var run = new RunState { DiscountWeek = 6 };

        run.BeginNewRun(seed: 5);

        Assert.Equal(-1, run.DiscountWeek);
    }

    [Fact]
    public void BeginNewRun_clears_the_voluntary_restart_menus_flag()
    {
        var run = new RunState { RestartMenusDone = true };

        run.BeginNewRun(seed: 5);

        Assert.False(run.RestartMenusDone);
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

    // ---- Per-week bonus slot list (CurrentWeekBonusSlots) ----
    // Backs the weekly theme quest's checklist: RunController samples live CC bundle slots for the
    // selected theme; a goal ticks when its exact slot flips complete. Cleared on Select/
    // BeginNewMonth/BeginNewRun so a re-picked or new week starts with no active goals (see the
    // BonusSlot-specific clearing tests below). DonatedThisWeekIds (the old id-only ledger) was
    // retired in the slot redesign (2026-07-09).

    [Fact]
    public void New_run_state_starts_with_empty_current_week_bonus_slots()
        => Assert.Empty(new RunState().CurrentWeekBonusSlots);

    [Fact]
    public void TryMarkVaultBundlePaid_is_true_once_then_false()
    {
        var run = new RunState();
        Assert.True(run.TryMarkVaultBundlePaid(34));
        Assert.False(run.TryMarkVaultBundlePaid(34));   // idempotent — no duplicate
        Assert.True(run.TryMarkVaultBundlePaid(35));
        Assert.Equal(2, run.VaultBundlesPaid.Count);
    }

    [Fact]
    public void BeginNewRun_clears_vault_bundles_paid()
    {
        var run = new RunState();
        run.TryMarkVaultBundlePaid(34);
        run.BeginNewRun(seed: 1);
        Assert.Empty(run.VaultBundlesPaid);
    }

    [Fact]
    public void BeginNewMonth_clears_current_week_bonus_slots()
    {
        var run = new RunState();
        run.CurrentWeekBonusSlots.Add(new BonusSlot { BundleIndex = 3, IngredientIndex = 1, ItemId = "(O)24", Stack = 5, Quality = 2, BundleName = "Quality Crops" });
        run.BeginNewMonth(Season.Summer);
        Assert.Empty(run.CurrentWeekBonusSlots);
    }

    [Fact]
    public void BeginNewRun_clears_current_week_bonus_slots()
    {
        var run = new RunState();
        run.CurrentWeekBonusSlots.Add(new BonusSlot { BundleIndex = 3, IngredientIndex = 1, ItemId = "(O)24" });
        run.BeginNewRun(seed: 42);
        Assert.Empty(run.CurrentWeekBonusSlots);
    }

    [Fact]
    public void Select_clears_current_week_bonus_slots()
    {
        var run = new RunState();
        run.CurrentWeekBonusSlots.Add(new BonusSlot { BundleIndex = 3, IngredientIndex = 1, ItemId = "(O)24" });
        run.Select(Theme.Farming);
        Assert.Empty(run.CurrentWeekBonusSlots);
    }

    [Fact]
    public void Cart_day_stock_round_trips_and_resets_on_new_run()
    {
        var run = new RunState { CartStockDay = 4, CartStockIds = new System.Collections.Generic.List<string> { "(O)1" } };
        string json = System.Text.Json.JsonSerializer.Serialize(run);
        RunState back = System.Text.Json.JsonSerializer.Deserialize<RunState>(json)!;
        Assert.Equal(4, back.CartStockDay);
        Assert.Equal(new[] { "(O)1" }, back.CartStockIds);
        back.BeginNewRun(7);
        Assert.Equal(-1, back.CartStockDay);
        Assert.Empty(back.CartStockIds);
    }

    // Nijah, Nexus 2026-09-28: a rerolled offer is kept for the week when the hub closes.
    private static RunState WithReroll(int week)
    {
        var run = new RunState();
        run.RecordReroll(week, new[] { Theme.Farming, Theme.Kitchen }, 2);
        run.RerollSeenPairs.Add("Farming|Kitchen");
        return run;
    }

    [Fact]
    public void A_rerolled_offer_survives_a_save_round_trip()
    {
        RunState back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(WithReroll(6)))!;
        Assert.Equal(6, back.RerollWeek);
        Assert.Equal(2, back.RerollCount);
        Assert.Equal(new[] { Theme.Farming, Theme.Kitchen }, back.RerolledOffer);
        Assert.Equal(new[] { "Farming|Kitchen" }, back.RerollSeenPairs);
        Assert.Equal(new[] { Theme.Farming, Theme.Kitchen }, back.RerolledOfferFor(6));
    }

    [Fact]
    public void A_rerolled_offer_from_another_week_is_ignored()
    {
        var run = WithReroll(6);
        Assert.Null(run.RerolledOfferFor(7));
        Assert.Null(new RunState().RerolledOfferFor(-1));
    }

    [Fact]
    public void A_rerolled_offer_that_holds_a_theme_picked_since_is_ignored()
    {
        var run = WithReroll(6);
        Assert.Null(run.RerolledOfferFor(6, new[] { Theme.Kitchen }));
        Assert.NotNull(run.RerolledOfferFor(6, new[] { Theme.Mining }));
    }

    [Fact]
    public void Reroll_state_is_cleared_by_a_pick_a_new_month_and_a_new_loop()
    {
        var picked = WithReroll(6);
        picked.Select(Theme.Mining);
        var month = WithReroll(6);
        month.BeginNewMonth(Season.Summer);
        var loop = WithReroll(6);
        loop.BeginNewRun(9);

        foreach (RunState run in new[] { picked, month, loop })
        {
            Assert.Equal(-1, run.RerollWeek);
            Assert.Equal(0, run.RerollCount);
            Assert.Empty(run.RerolledOffer);
            Assert.Empty(run.RerollSeenPairs);
        }
    }

    [Fact]
    public void SelectSecond_adds_to_the_months_themes_and_Select_BeginNewMonth_BeginNewRun_clear_it()
    {
        RunState Fresh()
        {
            var r = new RunState();
            r.Select(Theme.Mining);
            r.SelectSecond(Theme.Fishing);
            r.SecondLiabilityId = "x"; r.SecondGoalMultiplier = 1.4; r.SecondLiabilitySuppressedThisWeek = true;
            r.SecondWeekBonusSlots.Add(new BonusSlot());
            return r;
        }
        void AssertCleared(RunState r)
        {
            Assert.Null(r.SecondSelection); Assert.False(r.IsDoubleWeekSelection);
            Assert.Empty(r.SecondWeekBonusSlots); Assert.Null(r.SecondLiabilityId);
            Assert.Equal(1.0, r.SecondGoalMultiplier); Assert.False(r.SecondLiabilitySuppressedThisWeek);
        }
        RunState a = Fresh();
        Assert.True(a.IsDoubleWeekSelection);
        Assert.Contains(Theme.Fishing, a.SelectedThemesThisMonth);
        a.Select(Theme.Farming); AssertCleared(a);
        RunState b = Fresh(); b.BeginNewMonth(Season.Summer); AssertCleared(b);
        RunState c = Fresh(); c.BeginNewRun(5); AssertCleared(c);
    }

    [Fact]
    public void Shrine_goals_clear_per_list_and_round_trip()
    {
        RunState Fresh()
        {
            var r = new RunState();
            r.Select(Theme.Mining); r.SelectSecond(Theme.Fishing);
            r.CurrentWeekShrineGoals.Add(new ShrineGoal { ItemId = "(O)1", Stack = 2, ListIndex = 0, Deposited = true });
            r.CurrentWeekShrineGoals.Add(new ShrineGoal { ItemId = "(O)2", Stack = 3, ListIndex = 1, Paid = true });
            return r;
        }
        var rt = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(Fresh()))!;
        Assert.Equal(2, rt.CurrentWeekShrineGoals.Count);
        Assert.True(rt.CurrentWeekShrineGoals[0].Deposited);
        Assert.True(rt.CurrentWeekShrineGoals[1].Paid);
        Assert.Empty(new RunState().CurrentWeekShrineGoals);

        var a = Fresh(); a.SelectSecond(Theme.Farming);
        Assert.Single(a.CurrentWeekShrineGoals); Assert.Equal(0, a.CurrentWeekShrineGoals[0].ListIndex);
        var b = Fresh(); b.Select(Theme.Farming); Assert.Empty(b.CurrentWeekShrineGoals);
        var c = Fresh(); c.BeginNewMonth(Season.Summer); Assert.Empty(c.CurrentWeekShrineGoals);
        var d = Fresh(); d.BeginNewRun(9); Assert.Empty(d.CurrentWeekShrineGoals);
    }

    [Fact]
    public void Second_selection_survives_a_json_round_trip_and_an_old_save_has_none()
    {
        var r = new RunState();
        r.Select(Theme.Mining);
        r.SelectSecond(Theme.Fishing);
        r.SecondLiabilityId = "x"; r.SecondGoalMultiplier = 1.4; r.SecondLiabilitySuppressedThisWeek = true;
        RunState back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(r))!;
        Assert.Equal(Theme.Fishing, back.SecondSelection);
        Assert.Equal("x", back.SecondLiabilityId);
        Assert.Equal(1.4, back.SecondGoalMultiplier);
        Assert.True(back.SecondLiabilitySuppressedThisWeek);
        RunState old = JsonSerializer.Deserialize<RunState>("{}")!;
        Assert.Null(old.SecondSelection);
        Assert.Equal(1.0, old.SecondGoalMultiplier);
    }

    /// <summary>The empty-offer backstop skips a week with no theme, no bonus and no drawback:
    /// last week's pick must not carry on into it.</summary>
    [Fact]
    public void SkipWeek_clears_last_weeks_pick_goals_and_drawback_but_keeps_the_months_picks()
    {
        var run = new RunState();
        run.Select(Theme.Mining);
        run.SelectSecond(Theme.Fishing);
        run.CurrentLiabilityId = "forage_off";
        run.CurrentGoalMultiplier = 1.5;
        run.LiabilitySuppressedThisWeek = true;
        run.CurrentWeekBonusSlots.Add(new BonusSlot { ItemId = "(O)24" });
        run.CurrentWeekBonusItems.Add("(O)24");
        run.CurrentWeekShrineGoals.Add(new ShrineGoal { ItemId = "(O)24", ListIndex = 0 });

        run.SkipWeek();

        Assert.Null(run.CurrentSelection);
        Assert.Null(run.SecondSelection);
        Assert.Null(run.CurrentLiabilityId);
        Assert.Equal(1.0, run.CurrentGoalMultiplier);
        Assert.False(run.LiabilitySuppressedThisWeek);
        Assert.Empty(run.CurrentWeekBonusSlots);
        Assert.Empty(run.CurrentWeekBonusItems);
        Assert.Empty(run.CurrentWeekShrineGoals);
        Assert.Contains(Theme.Mining, run.SelectedThemesThisMonth);
    }
}
