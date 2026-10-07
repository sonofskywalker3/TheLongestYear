using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ShrineTabLayoutTests
{
    private const int Preferred = 220;
    private const int Gap = 8;
    private const int Min = 120;

    [Fact]
    public void Three_tabs_keep_the_preferred_width_even_when_tight()
        => Assert.Equal(Preferred, ShrineTabLayout.TabWidth(3, Preferred, Gap, 500, Min));

    [Fact]
    public void Four_tabs_keep_the_preferred_width_when_they_fit()
        => Assert.Equal(Preferred, ShrineTabLayout.TabWidth(4, Preferred, Gap, 952, Min));

    [Fact]
    public void Four_tabs_shrink_to_fit_the_room_left()
    {
        int w = ShrineTabLayout.TabWidth(4, Preferred, Gap, 900, Min);
        Assert.True(w < Preferred);
        Assert.True(ShrineTabLayout.StripWidth(4, w, Gap) <= 900);
    }

    [Fact]
    public void Four_tabs_never_go_below_the_minimum()
        => Assert.Equal(Min, ShrineTabLayout.TabWidth(4, Preferred, Gap, 200, Min));

    [Fact]
    public void Strip_width_counts_gaps_between_tabs_only()
    {
        Assert.Equal(3 * Preferred + 2 * Gap, ShrineTabLayout.StripWidth(3, Preferred, Gap));
        Assert.Equal(0, ShrineTabLayout.StripWidth(0, Preferred, Gap));
    }
}
