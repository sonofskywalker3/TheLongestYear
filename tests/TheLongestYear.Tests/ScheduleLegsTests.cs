using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class ScheduleLegsTests
{
    // A tiny valley: HaleyHouse -> Town -> Saloon, and Town -> Beach.
    private static string[]? Route(string from, string to) => (from, to) switch
    {
        ("HaleyHouse", "Saloon") => new[] { "HaleyHouse", "Town", "Saloon" },
        ("Saloon", "HaleyHouse") => new[] { "Saloon", "Town", "HaleyHouse" },
        ("HaleyHouse", "Town") => new[] { "HaleyHouse", "Town" },
        ("Town", "Saloon") => new[] { "Town", "Saloon" },
        ("HaleyHouse", "Beach") => new[] { "HaleyHouse", "Town", "Beach" },
        _ => null,
    };

    private static (int X, int Y)? WarpTo(string location, string next) => (location, next) switch
    {
        ("HaleyHouse", "Town") => (2, 23),
        ("Town", "Saloon") => (45, 70),
        ("Town", "HaleyHouse") => (20, 89),
        ("Town", "Beach") => (54, 109),
        ("Saloon", "Town") => (14, 24),
        _ => null,
    };

    private static (int X, int Y) WarpTarget(string location, (int X, int Y) warp) => (location, warp) switch
    {
        ("HaleyHouse", (2, 23)) => (20, 90),
        ("Saloon", (14, 24)) => (45, 71),
        _ => (0, 0),
    };

    private static List<ScheduleLegs.Leg> Legs(string home, (int, int) homeTile, params (string, int, int)[] stops)
    {
        var entries = new List<ScheduleLegs.Stop>();
        foreach ((string loc, int x, int y) in stops) entries.Add(new ScheduleLegs.Stop(loc, (x, y)));
        return ScheduleLegs.In("Town", home, homeTile, entries, Route, WarpTo, WarpTarget);
    }

    [Fact]
    public void A_walk_through_town_runs_from_the_door_it_entered_to_the_door_it_leaves()
    {
        var legs = Legs("HaleyHouse", (5, 5), ("Saloon", 10, 10));

        Assert.Equal(new[] { new ScheduleLegs.Leg((20, 90), (45, 70), true) }, legs);
    }

    [Fact]
    public void A_stop_in_town_ends_the_leg_on_that_tile()
    {
        var legs = Legs("HaleyHouse", (5, 5), ("Town", 30, 60));

        Assert.Equal(new[] { new ScheduleLegs.Leg((20, 90), (30, 60), true) }, legs);
    }

    [Fact]
    public void Each_stop_starts_where_the_last_one_ended()
    {
        var legs = Legs("HaleyHouse", (5, 5), ("Town", 30, 60), ("Saloon", 10, 10), ("HaleyHouse", 5, 5));

        Assert.Equal(new[]
        {
            new ScheduleLegs.Leg((20, 90), (30, 60), true),
            new ScheduleLegs.Leg((30, 60), (45, 70), false),
            new ScheduleLegs.Leg((45, 71), (20, 89), true),
        }, legs);
    }

    [Fact]
    public void A_day_that_never_crosses_town_has_no_legs()
    {
        Assert.Empty(Legs("HaleyHouse", (5, 5), ("HaleyHouse", 7, 7)));
    }

    [Fact]
    public void An_unknown_route_or_missing_warp_skips_that_stop_only()
    {
        var legs = Legs("HaleyHouse", (5, 5), ("Mars", 1, 1), ("Beach", 3, 3));

        // Mars has no route, so the Beach stop still starts from home; Town -> Beach has a warp.
        Assert.Equal(new[] { new ScheduleLegs.Leg((20, 90), (54, 109), true) }, legs);
    }

    [Fact]
    public void A_leg_that_starts_and_ends_on_one_tile_is_dropped()
    {
        Assert.Empty(Legs("Town", (30, 60), ("Town", 30, 60)));
    }

    private static List<(int X, int Y)> Line(int fromX, int toX, int y)
    {
        var route = new List<(int X, int Y)>();
        for (int x = fromX; x <= toX; x++) route.Add((x, y));
        return route;
    }

    [Fact]
    public void A_route_far_from_the_camera_is_not_used()
    {
        Assert.Null(ScheduleLegs.ForCamera(Line(0, 20, 50), (0, 0), (20, 0), maxDistance: 6, lead: 4, minTiles: 3));
    }

    [Fact]
    public void A_route_is_kept_from_its_door_to_a_few_tiles_past_the_camera()
    {
        // Runs x 0..20 on row 3; the camera line is a point-like segment at x 5 row 0.
        var kept = ScheduleLegs.ForCamera(Line(0, 20, 3), (5, 0), (5, 0), maxDistance: 6, lead: 4, minTiles: 3);

        Assert.NotNull(kept);
        Assert.Equal((0, 3), kept![0]);
        Assert.Equal((9, 3), kept[kept.Count - 1]);
    }

    [Fact]
    public void A_route_crossing_the_camera_near_its_end_is_kept_whole()
    {
        var kept = ScheduleLegs.ForCamera(Line(0, 20, 0), (19, 0), (30, 0), maxDistance: 6, lead: 4, minTiles: 3);

        Assert.Equal(21, kept!.Count);
    }

    [Fact]
    public void Too_short_a_cut_is_not_used()
    {
        Assert.Null(ScheduleLegs.ForCamera(Line(0, 20, 0), (0, 0), (0, 0), maxDistance: 6, lead: 1, minTiles: 3));
    }
}
