using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Tests;

public class BundleLogButtonRulesTests
{
    [Theory]
    [InlineData(true, false, true)]    // run active, no button yet -> add
    [InlineData(true, true, false)]    // already added -> never twice
    [InlineData(false, false, false)]  // non-TLY save -> no button
    public void ShouldAdd_only_on_an_active_run_once(bool active, bool present, bool expected)
        => Assert.Equal(expected, BundleLogButtonRules.ShouldAdd(active, present));

    [Fact]
    public void ButtonY_sits_right_under_the_bag()
        // Vanilla 1.6 at a menu top of 0: Junimo note 96-160, bag 170-234, trash from 392.
        => Assert.Equal(234 + BundleLogButtonRules.ButtonGap,
            BundleLogButtonRules.ButtonY(234, new[] { (96, 160), (392, 496) }));

    [Fact]
    public void ButtonY_leaves_room_above_the_vanilla_trash_can()
    {
        int y = BundleLogButtonRules.ButtonY(234, new[] { (96, 160), (392, 496) });
        Assert.True(y + BundleLogButtonRules.ButtonSize <= 392);
    }

    [Fact]
    public void ButtonY_goes_under_a_button_already_below_the_bag()
        => Assert.Equal(306 + BundleLogButtonRules.ButtonGap,
            BundleLogButtonRules.ButtonY(234, new[] { (242, 306) }));

    [Fact]
    public void ButtonY_keeps_stepping_past_a_stack_of_buttons()
        => Assert.Equal(380 + BundleLogButtonRules.ButtonGap,
            BundleLogButtonRules.ButtonY(234, new[] { (316, 380), (242, 306) }));

    [Fact]
    public void ButtonY_without_a_column_list_is_right_under_the_bag()
        => Assert.Equal(234 + BundleLogButtonRules.ButtonGap, BundleLogButtonRules.ButtonY(234, null!));

    [Fact]
    public void NearestSlot_picks_the_right_column_slot_level_with_the_button()
    {
        // The grid's right-most column: slots 11, 23, 35 with centres 168, 236, 304.
        var column = new[] { (11, 168), (23, 236), (35, 304) };
        Assert.Equal(35, BundleLogButtonRules.NearestSlot(column, 242 + 32));
        Assert.Equal(23, BundleLogButtonRules.NearestSlot(column, 240));
    }

    [Fact]
    public void NearestSlot_is_none_without_slots()
    {
        Assert.Equal(-1, BundleLogButtonRules.NearestSlot(Array.Empty<(int, int)>(), 100));
        Assert.Equal(-1, BundleLogButtonRules.NearestSlot(null!, 100));
    }

    [Fact]
    public void Button_label_is_short()
    {
        I18nFixture.InstallGlobalProvider();
        Assert.Equal("Bundle Log", TheLongestYear.Core.Strings.Get("menu.goals.tab-label"));
    }
}
