using System;
using System.Linq;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21, Scene 4: the world map at the map tab's size in the map tab's frame,
/// and a dark cloud that rolls in from above the top of the screen, covers the whole screen and
/// settles thickest on the farm. The split, the grid, the timing and the fits are pure arithmetic,
/// so they are tested here.</summary>
public class SceneCloudTests
{
    // The 1.6 Valley map in map pixels: 300 by 180 art pixels, times four, and the Farm area's
    // PixelArea from Data/WorldMap (68,62 50x34), times four.
    private const int MapW = 1200;
    private const int MapH = 720;
    private const int FarmX = 272, FarmY = 248, FarmW = 200, FarmH = 136;

    /// <summary>A screen in map pixels, for the map framed at the map tab's size (UI scale 1, zoom 1).</summary>
    private sealed record Screen(int ViewW, int ViewH, double X, double Y, double W, double H);

    private static Screen Framed(int viewW, int viewH)
    {
        (double scale, int ox, int oy) = SceneMapFit.MapTab(viewW, viewH, MapW, MapH, 4, 1.0);
        (double x, double y, double w, double h) = SceneMapFit.ScreenInMap(viewW, viewH, ox, oy, scale);
        return new Screen(viewW, viewH, x, y, w, h);
    }

    private static readonly Screen At720 = Framed(1280, 720);
    private static readonly Screen At4k = Framed(3840, 2130);

    private static SceneCloud.Blob[] Cloud(int seed = 7, Screen? screen = null)
    {
        Screen s = screen ?? At720;
        return SceneCloud.Plan(MapW, MapH, FarmX, FarmY, FarmW, FarmH, s.X, s.Y, s.W, s.H, new Random(seed)).ToArray();
    }

    public static TheoryData<int, int> Screens => new() { { 1280, 720 }, { 1920, 1080 }, { 3840, 2130 }, { 2560, 1440 } };

    // ------------------------------------------------------------------ the split and the count

    [Fact]
    public void On_a_map_sized_screen_a_quarter_of_the_cloud_packs_over_the_farm()
    {
        SceneCloud.Blob[] cloud = Cloud(screen: At720);
        Assert.InRange(cloud.Count(b => b.OnFarm) / (double)cloud.Length, 0.22, 0.28);
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void The_farm_gets_the_same_ten_blobs_on_any_screen(int w, int h)
    {
        // The farm is the same size on the map whatever the screen; a bigger screen's extra
        // blobs scatter over the extra screen round the map.
        Assert.Equal(10, Cloud(screen: Framed(w, h)).Count(b => b.OnFarm));
    }

    [Fact]
    public void A_screen_the_size_of_the_map_gets_about_thirty_scattered_blobs_and_a_big_one_stays_bounded()
    {
        (int c720, int r720) = SceneCloud.ScatterGrid(At720.W, At720.H, MapW, MapH);
        Assert.InRange(c720 * r720, 28, 34);
        (int c4k, int r4k) = SceneCloud.ScatterGrid(At4k.W, At4k.H, MapW, MapH);
        Assert.InRange(c4k * r4k, 50, 66);
        (int c8k, int r8k) = SceneCloud.ScatterGrid(7680, 4320, MapW, MapH);
        Assert.InRange(c8k * r8k, 50, 66);
    }

    [Fact]
    public void A_big_screen_past_the_cap_gets_bigger_blobs_so_the_cover_matches()
    {
        double Cover(Screen s)
        {
            SceneCloud.Blob[] scattered = Cloud(screen: s).Where(b => !b.OnFarm).ToArray();
            return scattered.Sum(b => b.Diameter * b.Diameter) / (s.W * s.H);
        }
        Assert.InRange(Cover(At4k) / Cover(At720), 0.75, 1.33);
    }

    // ------------------------------------------------------------------ where they rest

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public void Every_farm_blob_rests_inside_the_farm(int seed)
    {
        foreach (Screen s in new[] { At720, At4k })
            foreach (SceneCloud.Blob b in Cloud(seed, s).Where(b => b.OnFarm))
            {
                Assert.InRange(b.RestX, FarmX, FarmX + FarmW);
                Assert.InRange(b.RestY, FarmY, FarmY + FarmH);
            }
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void The_scattered_rests_cover_the_whole_screen_one_to_a_cell(int w, int h)
    {
        // Jeff, 2026-10-07: the cloud is "sort of everywhere", and with the framed map that means
        // the whole screen, frame and backdrop included.
        Screen s = Framed(w, h);
        (int columns, int rows) = SceneCloud.ScatterGrid(s.W, s.H, MapW, MapH);
        SceneCloud.Blob[] scattered = Cloud(screen: s).Where(b => !b.OnFarm).ToArray();
        var cells = scattered.Select(b => ((int)((b.RestX - s.X) / (s.W / columns)), (int)((b.RestY - s.Y) / (s.H / rows)))).ToList();
        Assert.Equal(columns * rows, cells.Distinct().Count());
        Assert.All(cells, c => { Assert.InRange(c.Item1, 0, columns - 1); Assert.InRange(c.Item2, 0, rows - 1); });
    }

    [Fact]
    public void The_farm_is_where_the_cloud_lies_thickest()
    {
        foreach (Screen s in new[] { At720, At4k })
        {
            SceneCloud.Blob[] cloud = Cloud(screen: s);
            double Area(SceneCloud.Blob b) => b.Diameter * b.Diameter;
            double onFarm = cloud.Where(b => b.RestX >= FarmX && b.RestX <= FarmX + FarmW && b.RestY >= FarmY && b.RestY <= FarmY + FarmH).Sum(Area) / (FarmW * FarmH);
            double everywhere = cloud.Sum(Area) / (s.W * s.H);
            Assert.True(onFarm > everywhere * 1.2, $"farm {onFarm:0.00} against screen {everywhere:0.00}");
        }
    }

    [Fact]
    public void The_same_seed_plans_the_same_cloud()
        => Assert.Equal(Cloud(5).Select(b => (b.RestX, b.RestY, b.StartMs)), Cloud(5).Select(b => (b.RestX, b.RestY, b.StartMs)));

    // ------------------------------------------------------------------ where they come from

    [Theory]
    [InlineData(7)]
    [InlineData(3)]
    [InlineData(99)]
    public void Every_blob_starts_wholly_above_the_top_of_the_screen_and_drifts_south(int seed)
    {
        // Jeff, 2026-10-07: "all the clouds come in from the north"; with the framed map, from
        // above the top of the screen.
        foreach (Screen s in new[] { At720, At4k })
            foreach (SceneCloud.Blob b in Cloud(seed, s))
            {
                Assert.True(b.SpawnY + b.Diameter / 2 < s.Y, $"blob disc reaches y {b.SpawnY + b.Diameter / 2} against the screen top {s.Y}");
                Assert.True(b.SpawnY < b.RestY, "it drifts south");
                Assert.InRange(b.SpawnX - b.RestX, -s.W * 0.041, s.W * 0.041);
            }
    }

    [Fact]
    public void The_starts_are_spread_across_the_whole_screen_width()
    {
        foreach (Screen s in new[] { At720, At4k })
        {
            SceneCloud.Blob[] cloud = Cloud(screen: s);
            Assert.Contains(cloud, b => b.SpawnX < s.X + s.W / 4);
            Assert.Contains(cloud, b => b.SpawnX > s.X + s.W * 3 / 4);
        }
    }

    [Fact]
    public void A_blob_resting_further_south_drifts_for_longer_and_sets_out_earlier()
    {
        SceneCloud.Blob[] scattered = Cloud().Where(b => !b.OnFarm).ToArray();
        SceneCloud.Blob south = scattered.OrderByDescending(b => b.RestY).First();
        SceneCloud.Blob north = scattered.OrderBy(b => b.RestY).First();
        Assert.True(south.ArriveMs - south.StartMs > north.ArriveMs - north.StartMs);
        Assert.True(south.StartMs < north.StartMs);
    }

    // ------------------------------------------------------------------ the timing

    [Fact]
    public void The_drift_starts_at_1500_and_the_starts_are_spread_to_6000()
    {
        foreach (Screen s in new[] { At720, At4k })
        {
            SceneCloud.Blob[] cloud = Cloud(screen: s);
            Assert.Equal(1500, cloud.Min(b => b.StartMs));
            Assert.Equal(6000, cloud.Max(b => b.StartMs));
            Assert.All(cloud.Where(b => !b.OnFarm), b => Assert.InRange(b.StartMs, 1500, 5500));
            Assert.All(cloud.Where(b => b.OnFarm), b => Assert.InRange(b.StartMs, 4500, 6000));
        }
    }

    [Fact]
    public void Every_farm_blob_is_at_rest_at_9000_and_nothing_is_still_moving_after()
    {
        foreach (Screen s in new[] { At720, At4k })
        {
            SceneCloud.Blob[] cloud = Cloud(screen: s);
            Assert.All(cloud.Where(b => b.OnFarm), b => Assert.Equal(9000, b.ArriveMs));
            Assert.All(cloud, b => Assert.True(b.ArriveMs <= 9000));
            Assert.All(cloud, b => Assert.Equal(1.0, SceneCloud.Progress(b, 9000)));
        }
    }

    [Fact]
    public void Blobs_drift_in_over_seconds_rather_than_dart()
    {
        // Jeff, 2026-10-07: "drift in slowly instead of darting in so quickly".
        foreach (Screen s in new[] { At720, At4k })
            Assert.All(Cloud(screen: s), b => Assert.InRange(b.ArriveMs - b.StartMs, 3000, 4500));
    }

    [Fact]
    public void A_blob_waits_above_the_screen_then_lands_on_its_rest()
    {
        SceneCloud.Blob b = Cloud().First(x => x.OnFarm);
        (double X, double Y) before = SceneCloud.Position(b, b.StartMs - 100);
        Assert.Equal(b.SpawnX, before.X, 3);
        Assert.Equal(b.SpawnY, before.Y, 3);
        (double X, double Y) arrived = SceneCloud.Position(b, b.ArriveMs);
        Assert.Equal(b.RestX, arrived.X, 3);
        Assert.Equal(b.RestY, arrived.Y, 3);
    }

    [Fact]
    public void A_settled_blob_drifts_only_a_few_pixels()
    {
        foreach (SceneCloud.Blob b in Cloud())
            for (int ms = b.ArriveMs; ms < 12200; ms += 50)
            {
                (double X, double Y) at = SceneCloud.Position(b, ms);
                Assert.InRange(at.X - b.RestX, -12.01, 12.01);
                Assert.InRange(at.Y - b.RestY, -6.01, 6.01);
            }
    }

    [Fact]
    public void Progress_never_goes_backwards()
    {
        SceneCloud.Blob b = Cloud()[0];
        double last = 0;
        for (int ms = 0; ms <= 9500; ms += 10)
        {
            double p = SceneCloud.Progress(b, ms);
            Assert.True(p >= last);
            last = p;
        }
    }

    [Fact]
    public void A_blob_is_unseen_before_it_sets_out_and_full_from_then_on()
    {
        foreach (SceneCloud.Blob b in Cloud())
        {
            Assert.Equal(0f, SceneCloud.Alpha(b, b.StartMs));
            for (int ms = b.StartMs + 1; ms <= 12200; ms += 50)
                Assert.Equal(0.7f, SceneCloud.Alpha(b, ms), 4);
        }
    }

    [Theory]
    [InlineData(7)]
    [InlineData(3)]
    [InlineData(99)]
    public void A_blob_is_at_full_darkness_when_it_comes_over_the_top_of_the_screen(int seed)
    {
        // Jeff, 2026-10-07: "blowing in transparent and then darkened already halfway down".
        foreach (Screen s in new[] { At720, At4k })
            foreach (SceneCloud.Blob b in Cloud(seed, s))
            {
                int crossing = -1;
                for (int ms = b.StartMs; ms <= b.ArriveMs; ms++)
                {
                    (double _, double y) = SceneCloud.Position(b, ms);
                    if (y + b.Diameter / 2 >= s.Y) { crossing = ms; break; }
                }
                Assert.True(crossing > b.StartMs, "it starts wholly off the screen");
                Assert.Equal(0.7f, SceneCloud.Alpha(b, crossing), 4);
            }
    }

    [Fact]
    public void The_dim_eases_in_with_the_cloud_and_is_full_at_9000()
    {
        Assert.Equal(0f, SceneCloud.Dim(0));
        Assert.Equal(0f, SceneCloud.Dim(1500));
        Assert.InRange(SceneCloud.Dim(5000), 0.01f, 0.34f);
        Assert.Equal(0.35f, SceneCloud.Dim(9000), 4);
        Assert.Equal(0.35f, SceneCloud.Dim(12000), 4);
    }

    // ------------------------------------------------------------------ the map tab's size and frame

    [Fact]
    public void An_art_rectangle_is_scaled_by_four_like_MapRegion()
        => Assert.Equal((272, 248, 200, 136), SceneMapFit.ArtToMap(68, 62, 50, 34, 4));

    [Fact]
    public void At_1280x720_the_map_is_the_map_tabs_1200x720_centred()
    {
        (double scale, int ox, int oy) = SceneMapFit.MapTab(1280, 720, MapW, MapH, 4, 1.0);
        Assert.Equal(1.0, scale, 6);
        Assert.Equal((40, 0), (ox, oy));
        Assert.Equal((312, 248, 200, 136), SceneMapFit.MapToPaint(FarmX, FarmY, FarmW, FarmH, ox, oy, scale));
    }

    [Fact]
    public void At_4k_the_map_stays_the_map_tabs_size_in_the_middle()
    {
        (double scale, int ox, int oy) = SceneMapFit.MapTab(3840, 2130, MapW, MapH, 4, 1.0);
        Assert.Equal(1.0, scale, 6);
        Assert.Equal((1320, 705), (ox, oy));
    }

    [Theory]
    [InlineData(1.0, 4)]
    [InlineData(1.5, 6)]
    [InlineData(2.0, 8)]
    [InlineData(1.2, 5)]
    [InlineData(0.75, 3)]
    [InlineData(0.1, 1)]
    public void The_map_tabs_scale_is_always_a_whole_number_of_pixels_per_art_pixel(double uiToPaint, int perArt)
    {
        (double scale, int _, int _) = SceneMapFit.MapTab(1920, 1080, MapW, MapH, 4, uiToPaint);
        Assert.Equal(perArt, scale * 4, 6);
    }

    [Fact]
    public void The_frame_sits_where_MapPage_draws_its_dialogue_box()
    {
        // MapPage.drawMap: drawDialogueBox(mapX - 32, mapY - 96, (300 + 16) * 4, (180 + 32) * 4);
        // drawDialogueBox then draws everything a tile lower for a box with no questions, so the
        // visible frame runs from (mapX - 32, mapY - 32) to 32 past the map's far corner.
        Assert.Equal((8, -32, 1264, 784, 64), SceneMapFit.Frame(40, 0, 1200, 720, 1.0));
        Assert.Equal((12, -48, 1896, 1176, 96), SceneMapFit.Frame(60, 0, 1800, 1080, 1.5));
    }

    [Fact]
    public void The_screen_in_map_pixels_runs_from_the_map_corner_back_to_the_screen_corner()
    {
        Assert.Equal((-40.0, 0.0, 1280.0, 720.0), SceneMapFit.ScreenInMap(1280, 720, 40, 0, 1.0));
        Assert.Equal((-40.0, 0.0, 1280.0, 720.0), SceneMapFit.ScreenInMap(1920, 1080, 60, 0, 1.5));
    }

    // ------------------------------------------------------------------ the filled fit, kept behind a switch

    [Fact]
    public void Filled_at_4k_the_map_fills_the_screen_at_a_whole_scale()
    {
        (double scale, int ox, int oy) = SceneMapFit.Fill(3840, 2130, MapW, MapH, 4);
        Assert.Equal(11 / 4.0, scale, 6);
        Assert.Equal((270, 75), (ox, oy));
    }

    [Fact]
    public void Filled_a_screen_a_whole_scale_would_leave_half_empty_gets_the_fractional_fit()
    {
        (double scale, int ox, int oy) = SceneMapFit.Fill(500, 300, MapW, MapH, 4);
        Assert.Equal(300 / 180.0 / 4, scale, 6);
        Assert.Equal((0, 0), (ox, oy));
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(1024, 768)]
    public void Filled_the_whole_map_always_fits_the_screen(int w, int h)
    {
        (double scale, int ox, int oy) = SceneMapFit.Fill(w, h, MapW, MapH, 4);
        (int x, int y, int mw, int mh) = SceneMapFit.MapToPaint(0, 0, MapW, MapH, ox, oy, scale);
        Assert.True(x >= 0 && y >= 0 && x + mw <= w && y + mh <= h, $"{x},{y} {mw}x{mh} on {w}x{h}");
        Assert.True(mw >= w * 0.9 || mh >= h * 0.9, $"{mw}x{mh} on {w}x{h}");
    }
}
