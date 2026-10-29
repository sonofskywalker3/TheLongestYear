using TheLongestYear.Core;

namespace TheLongestYear.Tests;

public class TextCenteringTests
{
    [Fact]
    public void Ink_is_centred_in_the_box_not_the_line_height()
    {
        // A 48px box; ink starts 6px into the line and is 14px tall. Ink centre must be at 24.
        float y = TextCentering.InkCentredY(0f, 48f, 6f, 14f, 1f);
        Assert.Equal(24f, y + 6f + 14f / 2f);
    }

    [Fact]
    public void Scale_applies_to_the_ink_offset_and_height()
    {
        float y = TextCentering.InkCentredY(100f, 60f, 4f, 10f, 2f);
        Assert.Equal(130f, y + 4f * 2f + 10f * 2f / 2f);
    }

    [Fact]
    public void InkExtent_spans_the_highest_top_to_the_lowest_bottom_and_skips_blank_glyphs()
    {
        var extent = TextCentering.InkExtent(new[] { (5, 12), (3, 14), (10, 0), (6, 13) });
        Assert.Equal((3f, 16f), extent);
    }

    [Fact]
    public void InkExtent_of_nothing_drawn_is_null()
    {
        Assert.Null(TextCentering.InkExtent(new[] { (0, 0) }));
        Assert.Null(TextCentering.InkExtent(System.Array.Empty<(int, int)>()));
    }
}
