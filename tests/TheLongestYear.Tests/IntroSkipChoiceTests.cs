using TheLongestYear.Core.Intro;

namespace TheLongestYear.Tests;

public class IntroSkipChoiceTests
{
    [Fact]
    public void Default_is_not_pending()
    {
        var c = new IntroSkipChoice();
        Assert.False(c.Pending);
        Assert.False(c.Consume());
    }

    [Fact]
    public void Record_true_is_consumed_once()
    {
        var c = new IntroSkipChoice();
        c.Record(skip: true);
        Assert.True(c.Pending);
        Assert.True(c.Consume());
        Assert.False(c.Pending);
        Assert.False(c.Consume());
    }

    [Fact]
    public void Record_false_clears_an_earlier_true()
    {
        var c = new IntroSkipChoice();
        c.Record(skip: true);
        c.Record(skip: false);
        Assert.False(c.Consume());
    }

    [Fact]
    public void Skipping_means_the_cc_seen_flag_is_planted_and_the_picker_opens()
    {
        // A skipped intro plants CcSeen before the first settled frame; the decider then goes
        // straight to the picker on the fresh morning instead of starting the cutscene.
        var snap = new IntroSnapshot(HasSeenIntro: false, Season: TheLongestYear.Core.Season.Spring,
            DayOfMonth: 1, CcSeen: true, EventActive: false);
        Assert.Equal(IntroAction.OpenPicker, IntroSequenceDecider.Next(snap));
    }
}
