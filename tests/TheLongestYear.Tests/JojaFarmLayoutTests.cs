using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Joja;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The bad ending's rows of coops and barns and its seeded litter scatter (spec
/// 2026-09-25-joja-offer-design, "The bad ending (Yes)").</summary>
public class JojaFarmLayoutTests
{
    private static readonly (int Width, int Height)[] BarnCoop = { (7, 4), (6, 3) };
    private const int Lane = 4, SpriteRows = 7, Gap = 1;

    private static Func<int, int, bool> Open(int w, int h, Func<int, int, bool>? blocked = null)
        => (x, y) => x >= 0 && y >= 0 && x < w && y < h && (blocked == null || !blocked(x, y));

    private static IReadOnlyList<FarmBuildingRow> Layout(Func<int, int, bool> free, int w, int h, int maxRows = 10)
        => JojaFarmLayout.Rows(free, w, h, BarnCoop, Lane, SpriteRows, Gap, minPerRow: 2, maxRows: maxRows);

    private static IEnumerable<(int X, int Y)> Footprint(FarmBuildingSpot s)
    {
        var (w, h) = BarnCoop[s.Kind];
        for (int y = s.Y; y < s.Y + h; y++)
            for (int x = s.X; x < s.X + w; x++)
                yield return (x, y);
    }

    [Fact]
    public void Open_field_fills_rows_side_by_side_alternating_kinds()
    {
        var rows = Layout(Open(80, 65), 80, 65);
        Assert.NotEmpty(rows);
        var first = rows[0];
        Assert.Equal(new[] { 0, 1, 0, 1 }, first.Spots.Take(4).Select(s => s.Kind));
        // side by side: each next building starts one gap after the previous one's right edge
        for (int i = 1; i < first.Spots.Count; i++)
        {
            var prev = first.Spots[i - 1];
            Assert.Equal(prev.X + BarnCoop[prev.Kind].Width + Gap, first.Spots[i].X);
        }
        // every building in a row stands on the row's bottom tile
        foreach (var row in rows)
            foreach (var s in row.Spots)
                Assert.Equal(row.Bottom, s.Y + BarnCoop[s.Kind].Height - 1);
    }

    [Fact]
    public void Rows_are_far_enough_apart_that_no_sprite_covers_the_lane_above()
    {
        var rows = Layout(Open(80, 65), 80, 65);
        Assert.True(rows.Count >= 4);
        for (int i = 1; i < rows.Count; i++)
            Assert.True(rows[i].Bottom - SpriteRows + 1 > rows[i - 1].Bottom + Lane,
                $"row {i} sprite top {rows[i].Bottom - SpriteRows + 1} overlaps lane ending {rows[i - 1].Bottom + Lane}");
    }

    [Fact]
    public void Nothing_is_placed_on_or_lanes_over_a_blocked_tile()
    {
        // a pond in the middle and a blocked strip down the left side
        Func<int, int, bool> blocked = (x, y) => (x >= 30 && x < 45 && y >= 20 && y < 35) || x < 3;
        var free = Open(80, 65, blocked);
        var rows = Layout(free, 80, 65);
        Assert.NotEmpty(rows);
        foreach (var row in rows)
            foreach (var s in row.Spots)
            {
                foreach (var t in Footprint(s)) Assert.True(free(t.X, t.Y), $"footprint on blocked {t}");
                var (w, _) = BarnCoop[s.Kind];
                for (int y = row.Bottom + 1; y <= row.Bottom + Lane; y++)
                    for (int x = s.X; x < s.X + w; x++)
                        Assert.True(free(x, y), $"lane on blocked {x},{y}");
            }
    }

    [Fact]
    public void No_two_buildings_overlap()
    {
        var rows = Layout(Open(80, 65), 80, 65);
        var seen = new HashSet<(int, int)>();
        foreach (var s in rows.SelectMany(r => r.Spots))
            foreach (var t in Footprint(s))
                Assert.True(seen.Add(t), $"overlap at {t}");
    }

    [Fact]
    public void Max_rows_and_min_per_row_are_respected()
    {
        Assert.Equal(2, Layout(Open(80, 65), 80, 65, maxRows: 2).Count);
        // a map only 10 wide fits one building per row: below the minimum of two, so no rows
        Assert.Empty(Layout(Open(10, 65), 10, 65));
    }

    [Fact]
    public void Same_input_same_layout()
    {
        Func<int, int, bool> blocked = (x, y) => (x * 7 + y * 3) % 23 == 0;
        var a = Layout(Open(80, 65, blocked), 80, 65);
        var b = Layout(Open(80, 65, blocked), 80, 65);
        Assert.Equal(a.SelectMany(r => r.Spots), b.SelectMany(r => r.Spots));
    }

    [Fact]
    public void Lane_spots_sit_in_the_lane_under_the_building_and_are_spread_out()
    {
        var spots = JojaFarmLayout.LaneSpots(x: 10, width: 7, bottom: 20, lane: 4, count: 3, spacing: 2, seed: 5);
        Assert.Equal(3, spots.Count);
        foreach (var (x, y) in spots)
        {
            Assert.InRange(x, 10, 16);
            Assert.InRange(y, 22, 24);
        }
        for (int i = 0; i < spots.Count; i++)
            for (int j = i + 1; j < spots.Count; j++)
            {
                double dx = spots[i].X - spots[j].X, dy = spots[i].Y - spots[j].Y;
                Assert.True(dx * dx + dy * dy >= 4);
            }
    }

    [Fact]
    public void Scatter_is_seeded_spaced_and_order_independent()
    {
        var cands = new List<(int, int)>();
        for (int y = 0; y < 10; y++) for (int x = 0; x < 60; x++) cands.Add((x, y));
        var a = JojaScatter.Pick(cands, 12, 3.5, 42);
        var reversed = Enumerable.Reverse(cands).ToList();
        var b = JojaScatter.Pick(reversed, 12, 3.5, 42);
        Assert.Equal(a, b);
        Assert.Equal(12, a.Count);
        for (int i = 0; i < a.Count; i++)
            for (int j = i + 1; j < a.Count; j++)
            {
                double dx = a[i].X - a[j].X, dy = a[i].Y - a[j].Y;
                Assert.True(dx * dx + dy * dy >= 3.5 * 3.5);
            }
        Assert.NotEqual(a, JojaScatter.Pick(cands, 12, 3.5, 43));
    }

    [Fact]
    public void Scatter_is_not_a_grid_or_a_row()
    {
        var cands = new List<(int, int)>();
        for (int y = 0; y < 8; y++) for (int x = 0; x < 60; x++) cands.Add((x, y));
        var a = JojaScatter.Pick(cands, 10, 4, 7);
        Assert.True(a.Select(p => p.Item2).Distinct().Count() >= 3, "all on one or two rows");
        var xs = a.Select(p => p.Item1).OrderBy(x => x).ToList();
        var steps = xs.Zip(xs.Skip(1), (p, q) => q - p).Distinct().Count();
        Assert.True(steps >= 3, "evenly spaced like a grid");
    }

    [Fact]
    public void Scatter_stops_when_spacing_leaves_no_room()
    {
        var cands = new List<(int, int)> { (0, 0), (1, 0), (2, 0) };
        Assert.Single(JojaScatter.Pick(cands, 5, 5, 1));
    }
}
