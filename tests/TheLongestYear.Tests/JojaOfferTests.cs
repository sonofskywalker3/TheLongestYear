using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Morris's offer (spec 2026-09-25-joja-offer-design): the scene, the letters, the blacklist.</summary>
public class JojaOfferTests
{
    private static (RunState run, MetaState meta) Fresh() => (new RunState(), new MetaState());

    [Fact]
    public void Scene_plays_once_a_loop_until_rejected()
    {
        var (run, meta) = Fresh();
        Assert.True(JojaOffer.ShouldPlayScene(run, meta, busy: false));
        JojaOffer.MarkSceneSeen(run, meta, dayOfYear: 10);
        Assert.False(JojaOffer.ShouldPlayScene(run, meta, busy: false));
        run.BeginNewRun(seed: 1);
        Assert.True(JojaOffer.ShouldPlayScene(run, meta, busy: false));
        JojaOffer.Reject(meta, runNumber: run.RunNumber);
        Assert.False(JojaOffer.ShouldPlayScene(run, meta, busy: false));
    }

    [Fact]
    public void Scene_waits_while_busy()
    {
        var (run, meta) = Fresh();
        Assert.False(JojaOffer.ShouldPlayScene(run, meta, busy: true));
    }

    [Fact]
    public void Scene_is_skippable_only_after_it_has_ever_been_seen()
    {
        var (run, meta) = Fresh();
        Assert.False(JojaOffer.SceneSkippable(meta));
        JojaOffer.MarkSceneSeen(run, meta, 3);
        run.BeginNewRun(2);
        Assert.True(JojaOffer.SceneSkippable(meta));
    }

    [Fact]
    public void Letter_days_are_two_per_season_inside_the_season_and_repeatable()
    {
        List<int> days = JojaOffer.PlanLetterDays(seed: 42);
        Assert.Equal(JojaOffer.ComeLetters, days.Count);
        for (int season = 0; season < 4; season++)
        {
            var inSeason = days.Where(d => (d - 1) / 28 == season).ToList();
            Assert.Equal(2, inSeason.Count);
            Assert.All(inSeason, d => Assert.InRange((d - 1) % 28 + 1, 2, 27));
            Assert.NotEqual(inSeason[0], inSeason[1]);
        }
        Assert.Equal(days, JojaOffer.PlanLetterDays(seed: 42));
        Assert.Equal(days.OrderBy(d => d), days);
    }

    [Fact]
    public void Come_letters_go_out_in_order_on_their_days_and_stop_once_the_scene_is_seen()
    {
        var (run, meta) = Fresh();
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        Assert.Equal(0, JojaOffer.ComeLetterDue(run, meta, 4));
        Assert.Equal(1, JojaOffer.ComeLetterDue(run, meta, 5));
        run.JojaLettersSent = 1;
        Assert.Equal(0, JojaOffer.ComeLetterDue(run, meta, 5));
        Assert.Equal(2, JojaOffer.ComeLetterDue(run, meta, 21));   // a missed day still delivers the next letter
        JojaOffer.MarkSceneSeen(run, meta, 22);
        Assert.Equal(0, JojaOffer.ComeLetterDue(run, meta, 33));
    }

    [Fact]
    public void No_come_letters_after_a_rejection_in_any_loop()
    {
        var (run, meta) = Fresh();
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        JojaOffer.Reject(meta, 1);
        run.BeginNewRun(9);
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        Assert.Equal(0, JojaOffer.ComeLetterDue(run, meta, 5));
    }

    [Fact]
    public void Decision_letters_fall_weekly_after_the_scene_and_stop_at_four()
    {
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 26);          // Spring 26
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 32));
        Assert.Equal(1, JojaOffer.DecisionLetterDue(run, meta, 33));   // crosses into Summer
        run.JojaDecisionLettersSent = 1;
        Assert.Equal(2, JojaOffer.DecisionLetterDue(run, meta, 40));
        run.JojaDecisionLettersSent = 4;
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 80));
    }

    [Fact]
    public void Decision_clock_past_winter_28_never_fires()
    {
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 110);
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 112));
    }

    [Fact]
    public void No_decision_letters_before_the_scene_or_after_a_rejection()
    {
        var (run, meta) = Fresh();
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 50));
        JojaOffer.MarkSceneSeen(run, meta, 10);
        JojaOffer.Reject(meta, run.RunNumber);
        Assert.Equal(0, JojaOffer.DecisionLetterDue(run, meta, 17));
    }

    [Fact]
    public void Morris_line_after_rejection_depends_on_the_loop()
    {
        var (run, meta) = Fresh();
        Assert.Equal(JojaMorrisLine.Ask, JojaOffer.MorrisLine(run, meta, run.RunNumber));
        JojaOffer.Reject(meta, run.RunNumber);
        Assert.Equal(JojaMorrisLine.RefuseAgain, JojaOffer.MorrisLine(run, meta, run.RunNumber));
        run.BeginNewRun(3);
        Assert.Equal(JojaMorrisLine.PositionFilled, JojaOffer.MorrisLine(run, meta, run.RunNumber));
    }

    [Fact]
    public void Cashier_refuses_until_answered_then_for_good_after_a_rejection()
    {
        var meta = new MetaState();
        Assert.Equal(JojaCashierLine.Undecided, JojaOffer.CashierLine(meta));
        JojaOffer.Reject(meta, 1);
        Assert.Equal(JojaCashierLine.Refused, JojaOffer.CashierLine(meta));
    }

    [Fact]
    public void BeginNewRun_clears_the_loop_state_but_not_the_rejection()
    {
        var (run, meta) = Fresh();
        run.JojaLetterDays = new List<int> { 5 };
        run.JojaLettersSent = 3;
        JojaOffer.MarkSceneSeen(run, meta, 40);
        run.JojaDecisionLettersSent = 2;
        JojaOffer.Reject(meta, run.RunNumber);
        run.BeginNewRun(7);
        Assert.Equal(-1, run.JojaSceneSeenDay);
        Assert.Empty(run.JojaLetterDays);
        Assert.Equal(0, run.JojaLettersSent);
        Assert.Equal(0, run.JojaDecisionLettersSent);
        Assert.True(JojaOffer.IsRejected(meta));
        Assert.True(meta.JojaOfferEverSeen);
    }

    [Fact]
    public void A_second_rejection_keeps_the_first_loop()
    {
        var meta = new MetaState();
        JojaOffer.Reject(meta, 2);
        JojaOffer.Reject(meta, 5);
        Assert.Equal(2, meta.JojaRejectedLoop);
    }

    [Fact]
    public void Yes_changes_no_state()
    {
        // Yes is handled entirely on the game side (bad ending, exit to title without saving).
        // Nothing in Core records it: a fresh state asks again.
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 10);
        Assert.Equal(JojaMorrisLine.Ask, JojaOffer.MorrisLine(run, meta, run.RunNumber));
        Assert.False(JojaOffer.IsRejected(meta));
    }

    [Fact]
    public void Old_saves_load_with_nothing_happened()
    {
        var run = Newtonsoft.Json.JsonConvert.DeserializeObject<RunState>("{\"RunNumber\":3}")!;
        var meta = Newtonsoft.Json.JsonConvert.DeserializeObject<MetaState>("{}")!;
        Assert.Equal(-1, run.JojaSceneSeenDay);
        Assert.NotNull(run.JojaLetterDays);
        Assert.False(JojaOffer.IsRejected(meta));
        Assert.False(meta.JojaOfferEverSeen);
    }
}
