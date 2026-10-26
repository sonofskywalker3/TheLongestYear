using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Tests;

public class BundleLogTabRulesTests
{
    [Theory]
    [InlineData(true, false, true)]    // run active, no tab yet -> add
    [InlineData(true, true, false)]    // already added -> never twice
    [InlineData(false, false, false)]  // non-TLY save -> no tab
    public void ShouldAdd_only_on_an_active_run_once(bool active, bool present, bool expected)
        => Assert.Equal(expected, BundleLogTabRules.ShouldAdd(active, present));

    [Fact]
    public void NextTabX_sits_one_tab_right_of_the_last_tab()
    {
        // Vanilla's ten tabs start at x+64 and step 64.
        var xs = Enumerable.Range(0, 10).Select(i => 100 + 64 + i * 64).ToList();
        Assert.Equal(100 + 64 + 10 * 64, BundleLogTabRules.NextTabX(xs, 0));
    }

    [Fact]
    public void NextTabX_uses_the_right_most_tab_not_the_last_listed()
        => Assert.Equal(700 + 64, BundleLogTabRules.NextTabX(new[] { 200, 700, 300 }, 0));

    [Fact]
    public void NextTabX_falls_back_on_an_empty_row()
        => Assert.Equal(42, BundleLogTabRules.NextTabX(Array.Empty<int>(), 42));

    [Theory]
    [InlineData(9, 10, 9, 10)]     // vanilla's exit tab -> right trigger reaches the log
    [InlineData(8, 10, 9, null)]   // inside vanilla's range: vanilla moves itself
    [InlineData(10, 10, 9, null)]  // already on the log
    [InlineData(10, 11, 9, 11)]    // another mod's tab sits before the log
    [InlineData(9, 11, 9, null)]   // ... then that tab is the next one, not ours
    [InlineData(9, -1, 9, null)]   // no log tab
    public void RightTrigger_steps_onto_the_log_from_the_tab_before_it(int current, int log, int vanillaCount, int? expected)
        => Assert.Equal(expected, BundleLogTabRules.RightTriggerTarget(current, log, vanillaCount));

    [Fact]
    public void Tab_label_is_short()
    {
        I18nFixture.InstallGlobalProvider();
        Assert.Equal("Bundle Log", TheLongestYear.Core.Strings.Get("menu.goals.tab-label"));
    }
}
