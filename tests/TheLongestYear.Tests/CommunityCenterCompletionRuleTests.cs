using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The patched Farmer.hasCompletedCommunityCenter answer (Nexus bug 1130863: a finished
/// hall read as incomplete, so Willy's back-room letter never came).</summary>
public class CommunityCenterCompletionRuleTests
{
    [Fact]
    public void Inactive_run_defers_to_vanilla()
    {
        Assert.Equal(CcCompletionAnswer.Vanilla,
            CommunityCenterCompletionRule.Decide(runActive: false, allAreasRestored: false, ccIsCompleteMail: false, everyRoomDoneOnBoard: false));
    }

    [Fact]
    public void Rooms_open_on_the_board_fail_closed_even_with_gift_mail()
    {
        // Gifts of the Junimos restore the room mails at reset; vanilla's mail check would say
        // complete with five rooms still open. The board answer wins.
        Assert.Equal(CcCompletionAnswer.No,
            CommunityCenterCompletionRule.Decide(runActive: true, allAreasRestored: false, ccIsCompleteMail: false, everyRoomDoneOnBoard: false));
    }

    [Fact]
    public void Every_room_done_on_the_board_defers_to_vanilla()
    {
        Assert.Equal(CcCompletionAnswer.Vanilla,
            CommunityCenterCompletionRule.Decide(runActive: true, allAreasRestored: false, ccIsCompleteMail: false, everyRoomDoneOnBoard: true));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void A_restored_hall_is_complete_whatever_the_board_says(bool allAreasRestored, bool ccIsCompleteMail)
    {
        // ChaoticMindset's save: areasComplete all true, ccIsComplete set, yet the board walk said
        // no and vanilla's Willy trigger never fired. The hall itself is the authority once the
        // Junimos have said goodbye.
        Assert.Equal(CcCompletionAnswer.Yes,
            CommunityCenterCompletionRule.Decide(runActive: true, allAreasRestored, ccIsCompleteMail, everyRoomDoneOnBoard: false));
    }
}
