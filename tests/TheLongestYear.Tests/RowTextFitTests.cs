using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Room-finding for the Herd Book keep note: it must never land on the hearts, pet or
/// cracker icons that vanilla draws in an Animals-tab row.</summary>
public class RowTextFitTests
{
    // A Herd Book row laid out as drawn: kind label at 140, cracker-lifted hearts at 468..628 from
    // y 26, the pet icons at 660..700 from y 22.
    private static readonly PixelBox Label = new(140, 6, 200, 30);
    private static readonly PixelBox LiftedHearts = new(468, 26, 160, 24);
    private static readonly PixelBox PetIcons = new(660, 22, 40, 80);

    [Fact]
    public void An_empty_line_is_one_span()
    {
        var spans = RowTextFit.FreeSpans(new PixelBox[0], 6, 36, 140, 768, 8);
        Assert.Equal(new[] { new FreeSpan(140, 768) }, spans);
    }

    [Fact]
    public void Obstacles_on_the_line_split_it_with_padding()
    {
        var spans = RowTextFit.FreeSpans(new[] { Label, LiftedHearts, PetIcons }, 6, 36, 140, 768, 8);
        Assert.Equal(new[] { new FreeSpan(348, 460), new FreeSpan(636, 652), new FreeSpan(708, 768) }, spans);
    }

    [Fact]
    public void Obstacles_off_the_line_do_not_block_it()
    {
        var lowHearts = new PixelBox(468, 50, 160, 24);
        var spans = RowTextFit.FreeSpans(new[] { Label, lowHearts }, 6, 36, 140, 768, 8);
        Assert.Equal(new[] { new FreeSpan(348, 768) }, spans);
    }

    [Fact]
    public void A_note_too_wide_for_any_gap_on_the_top_line_does_not_fit_there()
    {
        var spans = RowTextFit.FreeSpans(new[] { Label, LiftedHearts, PetIcons }, 6, 36, 140, 768, 8);
        Assert.Null(RowTextFit.FirstFitting(spans, 300));
        Assert.Equal(new FreeSpan(348, 460), RowTextFit.Widest(spans));
    }

    [Fact]
    public void First_and_last_fitting_pick_the_outermost_span_wide_enough()
    {
        var spans = new[] { new FreeSpan(0, 50), new FreeSpan(100, 300), new FreeSpan(400, 700) };
        Assert.Equal(new FreeSpan(100, 300), RowTextFit.FirstFitting(spans, 150));
        Assert.Equal(new FreeSpan(400, 700), RowTextFit.LastFitting(spans, 150));
        Assert.Null(RowTextFit.LastFitting(spans, 400));
    }

    [Fact]
    public void Overlapping_obstacles_leave_no_phantom_gap()
    {
        var a = new PixelBox(200, 0, 100, 30);
        var b = new PixelBox(250, 0, 20, 30);
        var spans = RowTextFit.FreeSpans(new[] { a, b }, 0, 30, 100, 500, 0);
        Assert.Equal(new[] { new FreeSpan(100, 200), new FreeSpan(300, 500) }, spans);
    }
}
