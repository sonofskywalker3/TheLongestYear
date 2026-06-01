using TheLongestYear.Core.Intro;

namespace TheLongestYear.Tests;

public class IntroEventKeysTests
{
    [Fact]
    public void Identifiers_are_stable()
    {
        // These strings persist in save data (eventsSeen / mailReceived); changing them would
        // strand existing saves, so pin them down.
        Assert.Equal("tly_intro", IntroEventKeys.IntroEventId);
        Assert.Equal("tly_intro_cc_seen", IntroEventKeys.CcSeenMail);
        Assert.Equal("tly_intro_done", IntroEventKeys.IntroDoneMail);
    }
}
