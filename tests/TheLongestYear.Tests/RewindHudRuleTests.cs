using TheLongestYear.Core.Rewind;
using Xunit;

namespace TheLongestYear.Tests;

public class RewindHudRuleTests
{
    [Fact]
    public void A_held_hud_that_something_switched_back_on_is_forced_off()
        => Assert.False(RewindHudRule.Correction(hudHeld: true, displayHud: true));

    [Fact]
    public void A_held_hud_that_is_already_off_is_left_alone()
        => Assert.Null(RewindHudRule.Correction(hudHeld: true, displayHud: false));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Outside_the_hold_the_hud_is_never_touched(bool displayHud)
        => Assert.Null(RewindHudRule.Correction(hudHeld: false, displayHud: displayHud));

    [Fact]
    public void Release_always_gives_the_hud_back()
        => Assert.True(RewindHudRule.ValueOnRelease);
}
