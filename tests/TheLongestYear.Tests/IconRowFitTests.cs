using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class IconRowFitTests
{
    // The Collections page's Bundle Log rows: 616 wide inside, 64px icons 88 apart; "+N more" ~96 wide.
    private const int Room = 616, Icon = 64, Pitch = 88;
    private static int Note(int hidden) => 96;

    [Fact]
    public void Shows_every_icon_when_they_all_fit()
    {
        Assert.Equal(7, IconRowFit.Shown(7, Room, Icon, Pitch, Note));   // 6 * 88 + 64 = 592
        Assert.Equal(3, IconRowFit.Shown(3, Room, Icon, Pitch, Note));
    }

    [Fact]
    public void Leaves_room_for_the_note_when_some_are_hidden()
    {
        // Nine missing: seven icons plus the note would end at 7 * 88 + 96 = 712, past 616.
        int shown = IconRowFit.Shown(9, Room, Icon, Pitch, Note);
        Assert.Equal(5, shown);                                        // 5 * 88 + 96 = 536
        Assert.True(shown * Pitch + Note(9 - shown) <= Room);
    }

    [Fact]
    public void A_wider_note_takes_another_icon()
        => Assert.Equal(4, IconRowFit.Shown(9, Room, Icon, Pitch, _ => 200));   // 4 * 88 + 200 = 552

    [Fact]
    public void Nothing_to_show_or_no_room()
    {
        Assert.Equal(0, IconRowFit.Shown(0, Room, Icon, Pitch, Note));
        Assert.Equal(0, IconRowFit.Shown(5, 100, Icon, Pitch, Note));
    }
}
