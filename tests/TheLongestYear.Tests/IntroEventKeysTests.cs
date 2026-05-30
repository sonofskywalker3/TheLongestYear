using TheLongestYear.Core.Intro;

namespace TheLongestYear.Tests;

public class IntroEventKeysTests
{
    [Fact]
    public void PorchKey_uses_valid_preconditions()
    {
        // u=DayOfMonth, Season=spring, n=has-mail, !n=not-mail. Must NOT use D/s/m (Dating/Shipped/EarnedMoney).
        Assert.Equal(
            "tly_intro_porch/u 1/Season spring/!n tly_intro_porch_seen/!n tly_intro_done",
            IntroEventKeys.PorchKey);
    }

    [Fact]
    public void CcKey_gates_on_porch_seen_via_mail()
    {
        Assert.Equal(
            "tly_intro_cc/n tly_intro_porch_seen/!n tly_intro_cc_seen/!n tly_intro_done",
            IntroEventKeys.CcKey);
    }

    [Fact]
    public void Keys_contain_no_legacy_letter_preconditions()
    {
        foreach (var key in new[] { IntroEventKeys.PorchKey, IntroEventKeys.CcKey })
        {
            Assert.DoesNotContain("/D ", key);
            Assert.DoesNotContain("/s ", key);
            Assert.DoesNotContain("m " + IntroEventKeys.PorchSeenMail, key);
        }
    }
}
