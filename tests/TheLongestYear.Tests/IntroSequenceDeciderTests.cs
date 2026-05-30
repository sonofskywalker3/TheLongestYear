using TheLongestYear.Core;
using TheLongestYear.Core.Intro;

namespace TheLongestYear.Tests;

public class IntroSequenceDeciderTests
{
    private static IntroSnapshot Fresh(bool porch = false, bool cc = false, bool eventActive = false) =>
        new IntroSnapshot(
            HasSeenIntro: false, Season: Season.Spring, DayOfMonth: 1,
            PorchSeen: porch, CcSeen: cc, EventActive: eventActive);

    [Fact]
    public void IsFreshIntroMorning_true_only_on_spring1_unseen()
    {
        Assert.True(IntroGate.IsFreshIntroMorning(hasSeenIntro: false, Season.Spring, 1));
        Assert.False(IntroGate.IsFreshIntroMorning(hasSeenIntro: true, Season.Spring, 1));
        Assert.False(IntroGate.IsFreshIntroMorning(hasSeenIntro: false, Season.Spring, 2));
        Assert.False(IntroGate.IsFreshIntroMorning(hasSeenIntro: false, Season.Summer, 1));
    }

    [Fact]
    public void AlreadySeen_yields_None()
    {
        var s = Fresh() with { HasSeenIntro = true };
        Assert.Equal(IntroAction.None, IntroSequenceDecider.Next(s));
    }

    [Fact]
    public void Fresh_no_flags_starts_porch()
        => Assert.Equal(IntroAction.StartPorch, IntroSequenceDecider.Next(Fresh()));

    [Fact]
    public void Event_active_waits()
        => Assert.Equal(IntroAction.Waiting, IntroSequenceDecider.Next(Fresh(eventActive: true)));

    [Fact]
    public void Porch_seen_warps_to_cc()
        => Assert.Equal(IntroAction.WarpToCc, IntroSequenceDecider.Next(Fresh(porch: true)));

    [Fact]
    public void Both_seen_opens_picker()
        => Assert.Equal(IntroAction.OpenPicker, IntroSequenceDecider.Next(Fresh(porch: true, cc: true)));
}
