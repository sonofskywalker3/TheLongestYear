using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class TimerTopicEventsTests
{
    private static (string id, string key, string script) Ev(string key, string script)
    {
        int slash = key.IndexOf('/');
        return (slash < 0 ? key : key.Substring(0, slash), key, script);
    }

    [Fact]
    public void Flags_the_scene_that_starts_a_topic_another_scene_waits_out()
    {
        // Defense Division's stay timer: a hidden scene starts a 162-day topic, and the goodbye
        // scene waits for that topic to run out.
        var events = new[]
        {
            Ev("90305264/e 90205264/A Time.A.02", "none/1 1/farmer 1 1 1/addConversationTopic Time.A.03 162/end"),
            Ev("99805264/!UpcomingFestival 3/e 90305264/A Time.A.03", "none/1 1/farmer 1 1 1/addConversationTopic Time.C.01 0/end"),
        };

        HashSet<string> ids = TimerTopicEvents.EventIds(events);

        Assert.Contains("90305264", ids);
    }

    [Fact]
    public void Leaves_alone_a_topic_no_scene_condition_reads()
    {
        // A flavour topic (villagers gossip about it) that no scene waits on: wiping it at the
        // rewind breaks nothing, so its scene stays seen.
        var events = new[]
        {
            Ev("611439/j 4", "none/1 1/farmer 1 1 1/addConversationTopic cc_Begin 4/end"),
            Ev("200/e 611439", "none/1 1/farmer 1 1 1/end"),
        };

        Assert.Empty(TimerTopicEvents.EventIds(events));
    }

    [Theory]
    [InlineData("A Stay")]
    [InlineData("NotActiveDialogueEvent Stay")]
    [InlineData("!ActiveDialogueEvent Stay")]
    [InlineData("ActiveDialogueEvent Stay")]
    public void Reads_every_spelling_of_the_topic_condition(string condition)
    {
        var events = new[]
        {
            Ev("1", "none/1 1/farmer 1 1 1/addConversationTopic Stay 30/end"),
            Ev("2/e 1/" + condition, "none/1 1/farmer 1 1 1/end"),
        };

        Assert.Contains("1", TimerTopicEvents.EventIds(events));
    }

    [Fact]
    public void A_topic_named_in_spoken_text_is_not_a_command()
    {
        var events = new[]
        {
            Ev("1", "none/1 1/farmer 1 1 1/speak Lewis \"We could addConversationTopic Stay 30 someday\"/end"),
            Ev("2/A Stay", "none/1 1/farmer 1 1 1/end"),
        };

        Assert.Empty(TimerTopicEvents.EventIds(events));
    }

    [Fact]
    public void A_slash_inside_spoken_text_does_not_start_a_command()
    {
        var events = new[]
        {
            Ev("1", "none/1 1/farmer 1 1 1/speak Lewis \"Example/addConversationTopic Stay 30/example\"/end"),
            Ev("2/A Stay", "none/1 1/farmer 1 1 1/end"),
        };

        Assert.Empty(TimerTopicEvents.EventIds(events));
    }

    [Fact]
    public void A_quoted_topic_matches_the_unquoted_one()
    {
        var events = new[]
        {
            Ev("1", "none/1 1/farmer 1 1 1/addConversationTopic Stay 30/end"),
            Ev("2/e 1/A \"Stay\"", "none/1 1/farmer 1 1 1/end"),
        };

        Assert.Contains("1", TimerTopicEvents.EventIds(events));
    }

    [Fact]
    public void Long_condition_names_ignore_case_but_topics_and_the_short_name_do_not()
    {
        var longName = new[]
        {
            Ev("1", "none/1 1/farmer 1 1 1/addConversationTopic Stay 30/end"),
            Ev("2/activedialogueevent Stay", "none/1 1/farmer 1 1 1/end"),
        };
        var otherTopic = new[]
        {
            Ev("1", "none/1 1/farmer 1 1 1/addConversationTopic Stay 30/end"),
            Ev("2/A stay", "none/1 1/farmer 1 1 1/end"),
        };
        var tileCondition = new[]
        {
            Ev("1", "none/1 1/farmer 1 1 1/addConversationTopic 5 30/end"),
            Ev("2/a 5 7", "none/1 1/farmer 1 1 1/end"),
        };

        Assert.Contains("1", TimerTopicEvents.EventIds(longName));
        Assert.Empty(TimerTopicEvents.EventIds(otherTopic));
        Assert.Empty(TimerTopicEvents.EventIds(tileCondition));
    }

    [Fact]
    public void Every_link_that_starts_a_waited_topic_is_flagged()
    {
        var events = new[]
        {
            Ev("10", "none/1 1/farmer 1 1 1/addConversationTopic T0 4/end"),
            Ev("11/e 10/A T0", "none/1 1/farmer 1 1 1/addConversationTopic T1 0/end"),
            Ev("12/e 11/A T1", "none/1 1/farmer 1 1 1/addConversationTopic T2 162/end"),
            Ev("13/e 12/A T2", "none/1 1/farmer 1 1 1/mail Goodbye/end"),
        };

        Assert.Equal(new HashSet<string> { "10", "11", "12" }, TimerTopicEvents.EventIds(events));
    }

    [Fact]
    public void Empty_and_missing_input_gives_nothing()
    {
        Assert.Empty(TimerTopicEvents.EventIds(null));
        Assert.Empty(TimerTopicEvents.EventIds(new[] { ("", "", ""), ("5", null!, null!) }));
    }
}
