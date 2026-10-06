using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RandomizerSettingsTests
{
    [Fact]
    public void Every_option_is_off_by_default()
    {
        var r = new GameplayConfig().Randomizer;
        Assert.Equal(RerollMode.Off, r.Rerolls);
        Assert.False(r.RandomThemeItems || r.RandomPairings || r.RandomMultiplier
            || r.MysteryCard || r.RandomBundleRewards || r.RandomCartDays
            || r.DoubleThemeWeek || r.WildcardDays || r.RandomShrineDonations);
    }

    [Fact]
    public void An_old_reroll_switch_that_was_on_becomes_free()
    {
        var c = new GameplayConfig { EnableThemeReroll = true };
        Assert.True(RandomizerMigration.Apply(c));
        Assert.Equal(RerollMode.Free, c.Randomizer.Rerolls);
        Assert.False(c.EnableThemeReroll);
        Assert.False(RandomizerMigration.Apply(c)); // one shot
    }

    [Fact]
    public void An_old_reroll_switch_that_was_off_changes_nothing()
    {
        var c = new GameplayConfig();
        Assert.False(RandomizerMigration.Apply(c));
        Assert.Equal(RerollMode.Off, c.Randomizer.Rerolls);
    }

    [Fact]
    public void A_setting_flipped_mid_week_waits_for_the_next_week()
    {
        var run = new RunState();
        var live = new RandomizerSettings();
        Assert.False(run.RandomizerFor(5, live).RandomPairings);
        live.RandomPairings = true;
        Assert.False(run.RandomizerFor(5, live).RandomPairings); // same week: snapshot holds
        Assert.True(run.RandomizerFor(6, live).RandomPairings);  // next week: new snapshot
    }

    [Fact]
    public void The_snapshot_survives_a_save_round_trip()
    {
        var run = new RunState();
        run.RandomizerFor(3, new RandomizerSettings { RandomMultiplier = true, Rerolls = RerollMode.CostsJp,
            DoubleThemeWeek = true, WildcardDays = true, RandomShrineDonations = true });
        var back = JsonSerializer.Deserialize<RunState>(JsonSerializer.Serialize(run))!;
        Assert.Equal(3, back.RandomizerWeek);
        Assert.True(back.RandomizerFor(3, new RandomizerSettings()).RandomMultiplier);
        Assert.Equal(RerollMode.CostsJp, back.RandomizerSnapshot!.Rerolls);
        Assert.True(back.RandomizerSnapshot.DoubleThemeWeek);
        Assert.True(back.RandomizerSnapshot.WildcardDays);
        Assert.True(back.RandomizerSnapshot.RandomShrineDonations);
    }

    [Fact]
    public void An_old_save_without_a_snapshot_takes_the_live_settings()
    {
        var back = JsonSerializer.Deserialize<RunState>("{}")!;
        Assert.True(back.RandomizerFor(1, new RandomizerSettings { RandomCartDays = true }).RandomCartDays);
    }

    [Fact]
    public void A_new_run_forgets_the_snapshot()
    {
        var run = new RunState();
        run.RandomizerFor(9, new RandomizerSettings { MysteryCard = true });
        run.BeginNewRun(123);
        Assert.Equal(-1, run.RandomizerWeek);
        Assert.Null(run.RandomizerSnapshot);
    }

    // Final review M1: a save made mid-week before the update has no snapshot for the week.
    private static RunState MidWeekOldSave(int offerPresented)
    {
        var run = JsonSerializer.Deserialize<RunState>("{}")!;
        run.Season = Season.Summer;
        run.DayOfMonth = 10;
        run.OfferPresentedWeek = offerPresented < 0 ? -1 : run.WeekOfYear;
        return run;
    }

    [Fact]
    public void An_old_mid_week_save_whose_offer_was_shown_keeps_the_week_all_off()
    {
        var run = MidWeekOldSave(offerPresented: 1);
        Assert.True(run.SnapshotOffIfWeekAlreadyOffered());
        var live = new RandomizerSettings { RandomCartDays = true, RandomPairings = true };
        Assert.False(run.RandomizerFor(run.WeekOfYear, live).RandomCartDays);
        Assert.True(run.RandomizerFor(run.WeekOfYear + 1, live).RandomCartDays); // next week takes live
    }

    [Fact]
    public void An_old_mid_week_save_with_this_weeks_pick_keeps_the_week_all_off()
    {
        var run = MidWeekOldSave(offerPresented: -1);
        run.CurrentSelection = Theme.Fishing;
        run.DiscountWeek = run.WeekOfYear;
        Assert.True(run.SnapshotOffIfWeekAlreadyOffered());
        Assert.False(run.RandomizerFor(run.WeekOfYear, new RandomizerSettings { RandomCartDays = true }).RandomCartDays);
    }

    [Fact]
    public void A_week_not_yet_offered_takes_the_live_settings_at_the_offer()
    {
        var run = MidWeekOldSave(offerPresented: -1);
        Assert.False(run.SnapshotOffIfWeekAlreadyOffered());
        Assert.True(run.RandomizerFor(run.WeekOfYear, new RandomizerSettings { RandomCartDays = true }).RandomCartDays);
    }

    [Fact]
    public void A_save_with_this_weeks_snapshot_is_left_alone()
    {
        var run = MidWeekOldSave(offerPresented: 1);
        run.RandomizerFor(run.WeekOfYear, new RandomizerSettings { RandomCartDays = true });
        Assert.False(run.SnapshotOffIfWeekAlreadyOffered());
        Assert.True(run.RandomizerSnapshot!.RandomCartDays);
    }
}
