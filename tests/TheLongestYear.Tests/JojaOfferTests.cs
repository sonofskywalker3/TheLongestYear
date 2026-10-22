using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Joja;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Morris's offer (spec 2026-09-25-joja-offer-design): the scene, the letters, the blacklist.</summary>
[Collection("i18n")]
public class JojaOfferTests
{
    private readonly I18nFixture _fixture;
    public JojaOfferTests(I18nFixture fixture) => _fixture = fixture;

    private static (RunState run, MetaState meta) Fresh() => (new RunState(), new MetaState());

    private static readonly string[] MorrisQuestionKeys = { "joja.morris.ask", "joja.morris.accept", "joja.morris.decline" };

    /// <summary>The `$y` question built in JojaCounterPatch.Counter splits its answer/reply
    /// fields on '_', so the joja.morris.* question and answer text must never contain '_' or
    /// '\''. default.json must carry all three keys; every i18n file the mod ships is checked,
    /// and a translation that leaves a key out falls back to default.json.</summary>
    [Fact]
    public void Morris_question_and_answer_text_has_no_underscore_or_apostrophe()
    {
        foreach (string key in MorrisQuestionKeys)
            Assert.True(_fixture.Map.ContainsKey(key), $"default.json is missing {key}");

        string i18nDir = System.IO.Path.GetDirectoryName(I18nFixture.DefaultJsonPath)!;
        var options = new System.Text.Json.JsonDocumentOptions
        {
            CommentHandling = System.Text.Json.JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        };
        string[] files = System.IO.Directory.GetFiles(i18nDir, "*.json");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            using var doc = System.Text.Json.JsonDocument.Parse(System.IO.File.ReadAllText(file), options);
            foreach (string key in MorrisQuestionKeys)
            {
                if (!doc.RootElement.TryGetProperty(key, out System.Text.Json.JsonElement element)) continue;
                string value = element.GetString() ?? "";
                string where = $"{System.IO.Path.GetFileName(file)}: {key}";
                Assert.False(value.Contains('_'), $"{where} contains '_'");
                Assert.False(value.Contains('\''), $"{where} contains an apostrophe");
            }
        }
    }

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

    private const int SeedsToCheck = 5000;

    /// <summary>Note 6 (Jeff, 2026-10-02): any two letters at least a week apart, across a season
    /// boundary too, still two per season on days 2..27, over many seeds.</summary>
    [Fact]
    public void Letter_days_are_at_least_a_week_apart_for_every_seed()
    {
        for (int seed = 0; seed < SeedsToCheck; seed++)
        {
            List<int> days = JojaOffer.PlanLetterDays(seed);
            Assert.Equal(JojaOffer.ComeLetters, days.Count);
            for (int season = 0; season < 4; season++)
            {
                var inSeason = days.Where(d => (d - 1) / 28 == season).ToList();
                Assert.Equal(2, inSeason.Count);
                Assert.All(inSeason, d => Assert.InRange((d - 1) % 28 + 1, 2, 27));
            }
            for (int i = 1; i < days.Count; i++)
                Assert.True(days[i] - days[i - 1] >= JojaOffer.DaysPerWeek,
                    $"seed {seed}: letters on days {days[i - 1]} and {days[i]} are under a week apart");
        }
    }

    [Fact]
    public void Letter_days_still_vary_from_seed_to_seed()
    {
        var firstDays = new HashSet<int>();
        var lastDays = new HashSet<int>();
        for (int seed = 0; seed < SeedsToCheck; seed++)
        {
            List<int> days = JojaOffer.PlanLetterDays(seed);
            firstDays.Add(days[0]);
            lastDays.Add(days[^1]);
        }
        // Spring's first letter can land anywhere a later one still fits (2..20), Winter's second
        // anywhere a week after a first one (Winter 9..27).
        Assert.Equal(Enumerable.Range(2, 19).ToHashSet(), firstDays);
        Assert.Equal(Enumerable.Range(Calendar.DayOfYear(3, 9), 19).ToHashSet(), lastDays);
    }

    /// <summary>A save that rolled its days before the week rule (Jeff's: Spring 11 and 14) re-plans
    /// the letters it has not had yet, keeping the ones already sent, a week after the last one.</summary>
    [Fact]
    public void An_old_plan_that_breaks_the_week_rule_is_replanned_after_the_letters_already_sent()
    {
        var (run, meta) = Fresh();
        run.BeginNewRun(seed: 77);
        run.JojaLetterDays = new List<int> { 11, 14, 40, 45, 70, 80, 95, 97 };
        run.JojaLettersSent = 1;
        JojaLetter letter = JojaOffer.MorningLetter(run, meta, today: 12, rewindPending: false);
        Assert.Equal(JojaLetter.None, letter);

        Assert.Equal(11, run.JojaLetterDays[0]);
        Assert.InRange(run.JojaLetterDays.Count, 2, JojaOffer.ComeLetters);
        Assert.True(run.JojaLetterDays[1] >= 11 + JojaOffer.DaysPerWeek);
        for (int i = 1; i < run.JojaLetterDays.Count; i++)
            Assert.True(run.JojaLetterDays[i] - run.JojaLetterDays[i - 1] >= JojaOffer.DaysPerWeek);
        // The new days come from the loop's own plan, so a reload the same morning plans the same.
        List<int> loopPlan = JojaOffer.PlanLetterDays(JojaOffer.LetterSeed(77));
        Assert.All(run.JojaLetterDays.Skip(1), d => Assert.Contains(d, loopPlan));

        var again = new RunState();
        again.BeginNewRun(seed: 77);
        again.JojaLetterDays = new List<int> { 11, 14, 40, 45, 70, 80, 95, 97 };
        again.JojaLettersSent = 1;
        JojaOffer.MorningLetter(again, meta, today: 12, rewindPending: false);
        Assert.Equal(run.JojaLetterDays, again.JojaLetterDays);
    }

    [Fact]
    public void An_old_plan_that_keeps_the_week_rule_is_left_alone()
    {
        var (run, meta) = Fresh();
        var days = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        run.JojaLetterDays = new List<int>(days);
        JojaOffer.MorningLetter(run, meta, today: 3, rewindPending: false);
        Assert.Equal(days, run.JojaLetterDays);
    }

    [Fact]
    public void Letters_already_sent_too_close_together_do_not_force_a_replan()
    {
        var (run, meta) = Fresh();
        var days = new List<int> { 11, 14, 40, 50, 70, 80, 95, 105 };
        run.JojaLetterDays = new List<int>(days);
        run.JojaLettersSent = 2;
        JojaOffer.MorningLetter(run, meta, today: 20, rewindPending: false);
        Assert.Equal(days, run.JojaLetterDays);
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

    // ---- MorningLetter: the one place that decides this morning's Morris letter ----

    private const int FallTen = 66;   // Fall 10 = day of year 56 + 10

    [Fact]
    public void Morning_letter_is_the_next_come_letter_and_only_one_a_morning()
    {
        var (run, meta) = Fresh();
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        JojaLetter first = JojaOffer.MorningLetter(run, meta, today: 21, rewindPending: false);
        Assert.Equal(new JojaLetter(JojaLetterKind.Come, 1, Rejects: false), first);
        JojaOffer.Record(run, meta, first);
        Assert.Equal(1, run.JojaLettersSent);
        // Two slots were behind on day 21; the second waits for the next morning.
        Assert.Equal(new JojaLetter(JojaLetterKind.Come, 2, false), JojaOffer.MorningLetter(run, meta, 22, false));
    }

    [Fact]
    public void Morning_letter_turns_to_decision_letters_once_the_scene_is_seen()
    {
        var (run, meta) = Fresh();
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        JojaOffer.MarkSceneSeen(run, meta, 10);
        Assert.Equal(JojaLetter.None, JojaOffer.MorningLetter(run, meta, 16, false));
        Assert.Equal(new JojaLetter(JojaLetterKind.Decide, 1, false), JojaOffer.MorningLetter(run, meta, 20, false));
    }

    [Fact]
    public void Morning_letter_sends_nothing_and_plans_nothing_while_a_rewind_is_pending()
    {
        var (run, meta) = Fresh();
        Assert.Equal(JojaLetter.None, JojaOffer.MorningLetter(run, meta, 30, rewindPending: true));
        Assert.Empty(run.JojaLetterDays);
        run.JojaLetterDays = new List<int> { 5, 20, 33, 50, 60, 70, 90, 100 };
        Assert.Equal(JojaLetter.None, JojaOffer.MorningLetter(run, meta, 30, rewindPending: true));
    }

    [Fact]
    public void Fourth_decision_letter_rejects_only_when_no_rewind_is_pending()
    {
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 10);
        run.JojaDecisionLettersSent = 3;
        Assert.Equal(JojaLetter.None, JojaOffer.MorningLetter(run, meta, 38, rewindPending: true));
        Assert.False(JojaOffer.IsRejected(meta));

        JojaLetter fourth = JojaOffer.MorningLetter(run, meta, 38, rewindPending: false);
        Assert.Equal(new JojaLetter(JojaLetterKind.Decide, 4, Rejects: true), fourth);
        JojaOffer.Record(run, meta, fourth);
        Assert.Equal(4, run.JojaDecisionLettersSent);
        Assert.Equal(run.RunNumber, meta.JojaRejectedLoop);
    }

    [Fact]
    public void Earlier_decision_letters_do_not_reject()
    {
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 10);
        JojaLetter third = new(JojaLetterKind.Decide, 3, Rejects: false);
        run.JojaDecisionLettersSent = 2;
        Assert.Equal(third, JojaOffer.MorningLetter(run, meta, 31, false));
        JojaOffer.Record(run, meta, third);
        Assert.False(JojaOffer.IsRejected(meta));
    }

    [Fact]
    public void A_fresh_loop_plans_the_whole_year_from_the_loop_seed()
    {
        var (run, meta) = Fresh();
        run.BeginNewRun(seed: 77);
        JojaOffer.MorningLetter(run, meta, today: 1, rewindPending: false);
        Assert.Equal(JojaOffer.PlanLetterDays(JojaOffer.LetterSeed(77)), run.JojaLetterDays);
    }

    [Fact]
    public void A_first_plan_mid_year_starts_at_letter_one_on_the_next_future_slot_with_no_burst()
    {
        var (run, meta) = Fresh();
        run.BeginNewRun(seed: 77);
        List<int> wholeYear = JojaOffer.PlanLetterDays(JojaOffer.LetterSeed(77));
        List<int> ahead = wholeYear.Where(d => d >= FallTen).ToList();
        Assert.NotEmpty(ahead);   // the seed leaves Fall and Winter slots after Fall 10

        var deliveries = new List<(int day, JojaLetter letter)>();
        for (int day = FallTen; day <= Calendar.DayOfYear(3, 28); day++)
        {
            JojaLetter letter = JojaOffer.MorningLetter(run, meta, day, false);
            if (letter.Kind == JojaLetterKind.None) continue;
            JojaOffer.Record(run, meta, letter);
            deliveries.Add((day, letter));
        }
        Assert.Equal(ahead, deliveries.Select(d => d.day).ToList());
        Assert.Equal(Enumerable.Range(1, ahead.Count), deliveries.Select(d => d.letter.Number));
        Assert.All(deliveries, d => Assert.Equal(JojaLetterKind.Come, d.letter.Kind));
    }

    [Fact]
    public void A_first_plan_past_every_slot_sends_nothing()
    {
        var (run, meta) = Fresh();
        run.BeginNewRun(seed: 77);
        Assert.Equal(JojaLetter.None, JojaOffer.MorningLetter(run, meta, Calendar.DayOfYear(3, 28), false));
    }

    [Fact]
    public void A_slot_slept_through_still_arrives_the_next_morning()
    {
        var (run, meta) = Fresh();
        run.BeginNewRun(seed: 77);
        JojaOffer.MorningLetter(run, meta, today: 1, rewindPending: false);   // plans the year
        int slot = run.JojaLetterDays[0];
        // The morning of the slot never ran this code (a rewind-pending morning, say).
        Assert.Equal(JojaLetter.None, JojaOffer.MorningLetter(run, meta, slot, rewindPending: true));
        Assert.Equal(new JojaLetter(JojaLetterKind.Come, 1, false), JojaOffer.MorningLetter(run, meta, slot + 1, false));
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

    /// <summary>Note 7 (Jeff, 2026-10-02): after the fourth decision letter the player never heard
    /// Morris say no, so his first line is the letter one, once; then "You heard me. Leave."</summary>
    [Fact]
    public void A_rejection_by_letter_gets_the_letter_line_once_then_the_usual_one()
    {
        var (run, meta) = Fresh();
        JojaOffer.MarkSceneSeen(run, meta, 10);
        run.JojaDecisionLettersSent = 3;
        JojaOffer.Record(run, meta, new JojaLetter(JojaLetterKind.Decide, 4, Rejects: true));
        Assert.True(meta.JojaRejectedByLetter);
        Assert.Equal(JojaMorrisLine.RefuseAfterLetter, JojaOffer.MorrisLine(run, meta, run.RunNumber));

        JojaOffer.MarkMorrisLineShown(meta, JojaMorrisLine.RefuseAfterLetter);
        Assert.Equal(JojaMorrisLine.RefuseAgain, JojaOffer.MorrisLine(run, meta, run.RunNumber));
        run.BeginNewRun(4);
        Assert.Equal(JojaMorrisLine.PositionFilled, JojaOffer.MorrisLine(run, meta, run.RunNumber));
    }

    [Fact]
    public void A_letter_rejection_first_seen_in_a_later_loop_is_still_position_filled()
    {
        var (run, meta) = Fresh();
        JojaOffer.Reject(meta, run.RunNumber, byLetter: true);
        run.BeginNewRun(4);
        Assert.Equal(JojaMorrisLine.PositionFilled, JojaOffer.MorrisLine(run, meta, run.RunNumber));
    }

    [Fact]
    public void A_rejection_in_person_never_gets_the_letter_line()
    {
        var (run, meta) = Fresh();
        JojaOffer.Reject(meta, run.RunNumber);
        Assert.False(meta.JojaRejectedByLetter);
        Assert.Equal(JojaMorrisLine.RefuseAgain, JojaOffer.MorrisLine(run, meta, run.RunNumber));
        JojaOffer.Reject(meta, run.RunNumber, byLetter: true);   // the first rejection stands
        Assert.False(meta.JojaRejectedByLetter);
    }

    [Fact]
    public void A_save_rejected_before_the_route_was_recorded_counts_as_in_person()
    {
        var run = Newtonsoft.Json.JsonConvert.DeserializeObject<RunState>("{\"RunNumber\":2}")!;
        var meta = Newtonsoft.Json.JsonConvert.DeserializeObject<MetaState>("{\"JojaRejectedLoop\":2}")!;
        Assert.Equal(JojaMorrisLine.RefuseAgain, JojaOffer.MorrisLine(run, meta, run.RunNumber));
    }

    [Fact]
    public void Morris_letter_refusal_is_jeffs_line_verbatim()
    {
        Assert.Equal("You must not have read my letter. You're no longer welcome here. Leave.$u",
            _fixture.Map["joja.morris.refuse-letter"]);
    }

    /// <summary>Note 2 (Jeff, 2026-10-02): no smile after a No. Morris's sheet has four cells
    /// (0 smile, 1 pursed frown, 2 shocked, 3 scowl); untagged lines show cell 0, so every line
    /// after a rejection carries $u (cell 3).</summary>
    [Theory]
    [InlineData("joja.morris.refuse")]
    [InlineData("joja.morris.refuse-again")]
    [InlineData("joja.morris.refuse-letter")]
    [InlineData("joja.morris.position-filled")]
    public void Morris_scowls_on_every_line_after_a_no(string key)
    {
        Assert.EndsWith("$u", _fixture.Map[key]);
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
