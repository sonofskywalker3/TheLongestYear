using TheLongestYear.Core.Interactables;

namespace TheLongestYear.Tests;

public class BundleLogSideTabRulesTests
{
    private const int N = BundleLogSideTabRules.SpriteSize * BundleLogSideTabRules.SpriteSize;
    private const uint Edge = 0xFF_10_20_30, Inside = 0xFF_C8_E3_F7, Clear = 0x00_00_00_00;

    [Theory]
    [InlineData(true, false, true)]    // run active, no tab yet -> add
    [InlineData(true, true, false)]    // already added -> never twice
    [InlineData(false, false, false)]  // non-TLY save -> no tab
    public void ShouldAdd_only_on_an_active_run_once(bool active, bool present, bool expected)
        => Assert.Equal(expected, BundleLogSideTabRules.ShouldAdd(active, present));

    [Fact]
    public void Tab_name_is_its_key_so_vanilla_can_read_it_back()
        => Assert.Equal(BundleLogSideTabRules.TabKey, int.Parse(BundleLogSideTabRules.TabName));

    [Fact]
    public void Key_and_id_stay_clear_of_vanilla()
    {
        Assert.NotInRange(BundleLogSideTabRules.TabKey, 0, 7);
        Assert.NotInRange(BundleLogSideTabRules.TabComponentId, 7001, 7008);
        Assert.NotInRange(BundleLogSideTabRules.TabComponentId, 12340, 12349);
    }

    [Fact]
    public void TabY_is_one_slot_under_the_lowest_tab()
        // Vanilla at a page top of 0: seven tabs at 128, 192, ... 512 (no secret notes yet).
        => Assert.Equal(576, BundleLogSideTabRules.TabY(new[] { 128, 192, 256, 320, 384, 448, 512 }, 128));

    [Fact]
    public void TabY_ignores_the_order_tabs_were_added_in()
        => Assert.Equal(640, BundleLogSideTabRules.TabY(new[] { 128, 576, 512, 192 }, 128));

    [Fact]
    public void TabY_without_tabs_takes_the_first_slot()
    {
        Assert.Equal(128, BundleLogSideTabRules.TabY(Array.Empty<int>(), 128));
        Assert.Equal(128, BundleLogSideTabRules.TabY(null!, 128));
    }

    [Fact]
    public void Fits_on_a_720p_screen_without_secret_notes_but_not_with_them()
    {
        // 720 tall: the game menu box is 680 tall, so its top is at 20 and its bottom at 700.
        Assert.True(BundleLogSideTabRules.Fits(20 + 576, 700, 720));    // no Secret Notes tab: the log is the 8th tab
        Assert.False(BundleLogSideTabRules.Fits(20 + 640, 700, 720));   // after Secret Notes: the 9th tab
    }

    [Fact]
    public void Pitch_keeps_vanilla_spacing_while_the_column_fits()
    {
        // Page top 20 on a 720 screen: box bottom 700, first tab at 148. Eight tabs end at 660.
        Assert.Equal(64, BundleLogSideTabRules.Pitch(8, 148, 700));
        Assert.Equal(64, BundleLogSideTabRules.Pitch(1, 148, 700));
    }

    [Fact]
    public void Pitch_closes_up_nine_tabs_to_end_inside_the_box()
    {
        int pitch = BundleLogSideTabRules.Pitch(9, 148, 700);
        Assert.Equal(61, pitch);
        int[] tops = BundleLogSideTabRules.ColumnTops(9, 148, 700);
        Assert.Equal(148, tops[0]);
        Assert.True(tops[8] + BundleLogSideTabRules.TabSize <= 700);
        Assert.Equal(pitch, tops[1] - tops[0]);
    }

    [Fact]
    public void Pitch_never_squeezes_past_the_floor()
        => Assert.Equal(BundleLogSideTabRules.MinPitch, BundleLogSideTabRules.Pitch(20, 148, 700));

    [Fact]
    public void ColumnTops_handles_an_empty_column()
        => Assert.Empty(BundleLogSideTabRules.ColumnTops(0, 148, 700));

    private static uint[] Tab(int pictureX, int pictureY, uint picture)
    {
        var t = new uint[N];
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
                t[y * 16 + x] = x == 0 || y == 0 || x == 15 || y == 15 ? Edge : Inside;
        for (int y = pictureY; y < pictureY + 4; y++)
            for (int x = pictureX; x < pictureX + 4; x++)
                t[y * 16 + x] = picture;
        return t;
    }

    [Fact]
    public void BlankFrame_keeps_the_frame_and_fills_where_the_pictures_were()
    {
        uint[] frame = BundleLogSideTabRules.BlankFrame(new[] { Tab(4, 4, 0xFF_00_00_FF), Tab(6, 6, 0xFF_00_FF_00) });
        Assert.NotNull(frame);
        Assert.Equal(Edge, frame![0]);
        Assert.Equal(Edge, frame[15 * 16 + 15]);
        Assert.All(frame.Where((_, i) => i % 16 is > 0 and < 15 && i / 16 is > 0 and < 15), p => Assert.Equal(Inside, p));
    }

    [Fact]
    public void BlankFrame_needs_two_full_sprites()
    {
        Assert.Null(BundleLogSideTabRules.BlankFrame(new[] { Tab(4, 4, Edge) }));
        Assert.Null(BundleLogSideTabRules.BlankFrame(new[] { Tab(4, 4, Edge), new uint[3] }));
        Assert.Null(BundleLogSideTabRules.BlankFrame(null!));
    }

    [Fact]
    public void Compose_doubles_the_frame_and_centres_the_cover()
    {
        uint[] frame = Tab(0, 0, Edge);
        var cover = new uint[N];
        cover[0] = 0xFF_00_00_FF;          // top-left cover pixel
        // every other cover pixel is clear and leaves the frame showing
        uint[] icon = BundleLogSideTabRules.Compose(frame, cover);
        int size = 16 * BundleLogSideTabRules.IconScale;
        Assert.Equal(size * size, icon.Length);
        Assert.Equal(Edge, icon[0]);
        Assert.Equal(Edge, icon[1 * size + 1]);                // frame pixel (0,0) covers a 2x2 block
        Assert.Equal(0xFF_00_00_FFu, icon[8 * size + 8]);       // cover starts 8px in
        Assert.Equal(Inside, icon[8 * size + 9]);
        Assert.Equal(Clear, BundleLogSideTabRules.Compose(new uint[N], null)[0]);
    }

    [Fact]
    public void Tab_label_is_short()
    {
        I18nFixture.InstallGlobalProvider();
        Assert.Equal("Bundle Log", TheLongestYear.Core.Strings.Get("menu.goals.tab-label"));
    }
}
