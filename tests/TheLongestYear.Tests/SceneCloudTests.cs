using System;
using System.Linq;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21, Scene 4: a dark cloud pours over the world map from the mountain side,
/// covers the valley, and settles thickest on the farm. The split, the timing and the map-to-screen
/// mapping are pure arithmetic, so they are tested here.</summary>
public class SceneCloudTests
{
    // The 1.6 Valley map at the map tab's scale: 300 by 180 art pixels, times four, and the Farm
    // area's PixelArea from Data/WorldMap (68,62 50x34), times four.
    private const int MapW = 1200;
    private const int MapH = 720;
    private const int FarmX = 272, FarmY = 248, FarmW = 200, FarmH = 136;

    private static SceneCloud.Blob[] Cloud(int seed = 7)
        => SceneCloud.Plan(SceneCloud.BlobCount, MapW, MapH, FarmX, FarmY, FarmW, FarmH, new Random(seed)).ToArray();

    // ------------------------------------------------------------------ the split

    [Fact]
    public void Forty_blobs_split_seventy_five_twenty_five_between_the_valley_and_the_farm()
    {
        // Jeff, 2026-10-07: the first cut's 60/40 piled the farm too high and left the valley bare.
        SceneCloud.Blob[] cloud = Cloud();
        Assert.Equal(40, cloud.Length);
        Assert.Equal(10, cloud.Count(b => b.OnFarm));
        Assert.Equal(30, cloud.Count(b => !b.OnFarm));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(99)]
    public void Every_farm_blob_rests_inside_the_farm(int seed)
    {
        foreach (SceneCloud.Blob b in Cloud(seed).Where(b => b.OnFarm))
        {
            Assert.InRange(b.RestX, FarmX, FarmX + FarmW);
            Assert.InRange(b.RestY, FarmY, FarmY + FarmH);
        }
    }

    [Fact]
    public void Scattered_blobs_rest_on_the_map_and_spread_over_it()
    {
        SceneCloud.Blob[] scattered = Cloud().Where(b => !b.OnFarm).ToArray();
        foreach (SceneCloud.Blob b in scattered)
        {
            Assert.InRange(b.RestX, 0, MapW);
            Assert.InRange(b.RestY, 0, MapH);
        }
        // Some on each side of the map: the cloud covers the valley, not one corner of it.
        Assert.Contains(scattered, b => b.RestX < MapW / 2.0);
        Assert.Contains(scattered, b => b.RestX > MapW / 2.0);
    }

    [Fact]
    public void The_farm_is_where_the_cloud_lies_thickest()
    {
        // Blob area per map pixel, over the farm against over the whole map.
        SceneCloud.Blob[] cloud = Cloud();
        double Area(SceneCloud.Blob b) => b.Diameter * b.Diameter;
        double onFarm = cloud.Where(b => b.RestX >= FarmX && b.RestX <= FarmX + FarmW && b.RestY >= FarmY && b.RestY <= FarmY + FarmH).Sum(Area) / (FarmW * FarmH);
        double everywhere = cloud.Sum(Area) / (MapW * (double)MapH);
        // Still thickest on the farm, but by a smaller margin than the first cut's.
        Assert.True(onFarm > everywhere * 1.2, $"farm {onFarm:0.00} against map {everywhere:0.00}");
    }

    [Theory]
    [InlineData(7)]
    [InlineData(11)]
    public void The_scattered_veil_leaves_no_part_of_the_map_bare(int seed)
    {
        // Jeff, 2026-10-07: the cloud is "sort of everywhere", forest, mountains, water and the
        // margins too, never clustered on the town. Every cell of a 6 by 5 grid over the whole map
        // holds exactly one scattered blob's rest.
        SceneCloud.Blob[] scattered = Cloud(seed).Where(b => !b.OnFarm).ToArray();
        var cells = scattered.Select(b => ((int)(b.RestX / (MapW / 6.0)), (int)(b.RestY / (MapH / 5.0)))).ToList();
        Assert.Equal(30, cells.Distinct().Count());
        Assert.All(cells, c => { Assert.InRange(c.Item1, 0, 5); Assert.InRange(c.Item2, 0, 4); });
    }

    [Theory]
    [InlineData(7)]
    [InlineData(3)]
    [InlineData(99)]
    public void Every_blob_starts_wholly_above_the_map_and_comes_in_from_the_north(int seed)
    {
        // Jeff, 2026-10-07: "all the clouds come in from the north, not start on the map".
        foreach (SceneCloud.Blob b in Cloud(seed))
        {
            Assert.True(b.SpawnY + b.Diameter / 2 < 0, $"blob disc reaches y {b.SpawnY + b.Diameter / 2} at its start");
            Assert.True(b.SpawnY < b.RestY, "it drifts south");
            // Roughly above its own rest: a little sideways drift only.
            Assert.InRange(b.SpawnX - b.RestX, -MapW * 0.041, MapW * 0.041);
        }
    }

    [Fact]
    public void The_starts_are_spread_across_the_whole_width_not_one_corner()
    {
        SceneCloud.Blob[] cloud = Cloud();
        Assert.Contains(cloud, b => b.SpawnX < MapW / 4.0);
        Assert.Contains(cloud, b => b.SpawnX > MapW * 3 / 4.0);
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

    [Fact]
    public void The_same_seed_plans_the_same_cloud()
    {
        Assert.Equal(Cloud(5).Select(b => (b.RestX, b.RestY, b.StartMs)), Cloud(5).Select(b => (b.RestX, b.RestY, b.StartMs)));
    }

    // ------------------------------------------------------------------ the timing

    [Fact]
    public void The_drift_starts_at_1500_and_the_starts_are_spread_to_6000()
    {
        SceneCloud.Blob[] cloud = Cloud();
        Assert.Equal(1500, cloud.Min(b => b.StartMs));
        Assert.Equal(6000, cloud.Max(b => b.StartMs));
        Assert.All(cloud.Where(b => !b.OnFarm), b => Assert.InRange(b.StartMs, 1500, 5500));
        Assert.All(cloud.Where(b => b.OnFarm), b => Assert.InRange(b.StartMs, 4500, 6000));
    }

    [Fact]
    public void Every_farm_blob_is_at_rest_at_9000_and_nothing_is_still_moving_after()
    {
        SceneCloud.Blob[] cloud = Cloud();
        Assert.All(cloud.Where(b => b.OnFarm), b => Assert.Equal(9000, b.ArriveMs));
        Assert.All(cloud, b => Assert.True(b.ArriveMs <= 9000));
        Assert.All(cloud, b => Assert.Equal(1.0, SceneCloud.Progress(b, 9000)));
    }

    [Fact]
    public void Blobs_drift_in_over_seconds_rather_than_dart()
    {
        // Jeff, 2026-10-07: "drift in slowly instead of darting in so quickly". A farm blob takes
        // 3000 to 4500 ms; a scattered one the same, cut short only where the farm settles first.
        SceneCloud.Blob[] cloud = Cloud();
        Assert.All(cloud.Where(b => b.OnFarm), b => Assert.InRange(b.ArriveMs - b.StartMs, 3000, 4500));
        Assert.All(cloud.Where(b => !b.OnFarm), b => Assert.InRange(b.ArriveMs - b.StartMs, 3000, 4500));
    }

    [Fact]
    public void A_blob_waits_on_the_spawn_line_then_lands_on_its_rest()
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
        {
            for (int ms = b.ArriveMs; ms < 12200; ms += 50)
            {
                (double X, double Y) at = SceneCloud.Position(b, ms);
                Assert.InRange(at.X - b.RestX, -12.01, 12.01);
                Assert.InRange(at.Y - b.RestY, -6.01, 6.01);
            }
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
    public void A_blob_is_at_full_darkness_when_it_crosses_the_maps_top_edge(int seed)
    {
        // Jeff, 2026-10-07: "blowing in transparent and then darkened already halfway down". The
        // first instant any of its disc is on the map, it is already at full darkness.
        foreach (SceneCloud.Blob b in Cloud(seed))
        {
            int crossing = -1;
            for (int ms = b.StartMs; ms <= b.ArriveMs; ms++)
            {
                (double _, double y) = SceneCloud.Position(b, ms);
                if (y + b.Diameter / 2 >= 0) { crossing = ms; break; }
            }
            Assert.True(crossing > b.StartMs, "it starts wholly off the map");
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

    // ------------------------------------------------------------------ the farm rect on screen

    [Fact]
    public void An_art_rectangle_is_scaled_by_four_like_MapRegion()
    {
        Assert.Equal((272, 248, 200, 136), SceneCloud.ArtToMap(68, 62, 50, 34, 4));
    }

    [Fact]
    public void At_1280x720_the_map_fills_the_height_at_a_whole_four()
    {
        (double scale, int ox, int oy) = SceneCloud.Fit(1280, 720, MapW, MapH, 4);
        Assert.Equal(1.0, scale, 6);   // four screen pixels per art pixel
        Assert.Equal((40, 0), (ox, oy));
        Assert.Equal((312, 248, 200, 136), SceneCloud.MapToPaint(FarmX, FarmY, FarmW, FarmH, ox, oy, scale));
    }

    [Fact]
    public void At_4k_the_map_fills_the_screen_at_a_whole_scale()
    {
        // 3840x2130 (the native capture's client area): the fractional fit is 11.83 per art pixel,
        // and 11 fills 93 percent of it, so the art stays on a whole-pixel grid.
        (double scale, int ox, int oy) = SceneCloud.Fit(3840, 2130, MapW, MapH, 4);
        Assert.Equal(11 / 4.0, scale, 6);
        Assert.Equal((270, 75), (ox, oy));
        Assert.Equal((270 + 748, 75 + 682, 550, 374), SceneCloud.MapToPaint(FarmX, FarmY, FarmW, FarmH, ox, oy, scale));
    }

    [Fact]
    public void A_screen_a_whole_scale_would_leave_half_empty_gets_the_fractional_fit()
    {
        // 500x300: the fit is 1.67 per art pixel; 1 would fill only 60 percent of it.
        (double scale, int ox, int oy) = SceneCloud.Fit(500, 300, MapW, MapH, 4);
        Assert.Equal(300 / 180.0 / 4, scale, 6);
        Assert.Equal((0, 0), (ox, oy));
    }

    [Theory]
    [InlineData(1280, 720)]
    [InlineData(1920, 1080)]
    [InlineData(3840, 2160)]
    [InlineData(2560, 1440)]
    [InlineData(1366, 768)]
    [InlineData(1024, 768)]
    public void The_whole_map_always_fits_the_screen(int w, int h)
    {
        (double scale, int ox, int oy) = SceneCloud.Fit(w, h, MapW, MapH, 4);
        (int x, int y, int mw, int mh) = SceneCloud.MapToPaint(0, 0, MapW, MapH, ox, oy, scale);
        Assert.True(x >= 0 && y >= 0 && x + mw <= w && y + mh <= h, $"{x},{y} {mw}x{mh} on {w}x{h}");
        // And it fills one side to within the whole-number allowance.
        Assert.True(mw >= w * 0.9 || mh >= h * 0.9, $"{mw}x{mh} on {w}x{h}");
    }
}
