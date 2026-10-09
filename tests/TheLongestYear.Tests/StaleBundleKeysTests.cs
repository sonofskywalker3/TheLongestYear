using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-10-09-bundle-count-dial, "Removing stale bundles".</summary>
public class StaleBundleKeysTests
{
    private static Dictionary<string, string> Board(params string[] keys)
        => keys.ToDictionary(k => k, k => "x");

    [Fact]
    public void Same_keys_nothing_stale()
    {
        var board = Board("Pantry/0", "Pantry/1", "Vault/23");
        var result = StaleBundleKeys.Find(board, board.Keys);
        Assert.Empty(result.Keys);
        Assert.Empty(result.Indices);
    }

    [Fact]
    public void A_dropped_bundle_of_a_written_room_is_stale()
    {
        var board = Board("Pantry/0", "Pantry/1");
        var result = StaleBundleKeys.Find(board, new[] { "Pantry/0", "Pantry/1", "Pantry/4" });
        Assert.Equal(new[] { "Pantry/4" }, result.Keys);
        Assert.Equal(new[] { 4 }, result.Indices);
    }

    [Fact]
    public void Rooms_the_board_does_not_write_are_left_alone()
    {
        var board = Board("Pantry/0");
        var result = StaleBundleKeys.Find(board, new[] { "Pantry/0", "Some Mod Room/77" });
        Assert.Empty(result.Keys);
    }

    /// <summary>A reserved index can move from one room to another between loops: the old key is
    /// stale but its index is still in use, so its completion entry is rebuilt, not removed.</summary>
    [Fact]
    public void An_index_still_used_by_another_key_is_reported_as_reused()
    {
        var board = Board("Pantry/0", "Crafts Room/9000");
        var result = StaleBundleKeys.Find(board, new[] { "Pantry/0", "Pantry/9000", "Crafts Room/9000" });
        Assert.Equal(new[] { "Pantry/9000" }, result.Keys);
        Assert.Equal(new[] { 9000 }, result.Indices);
    }

    [Fact]
    public void Malformed_live_keys_are_ignored()
    {
        var board = Board("Pantry/0");
        var result = StaleBundleKeys.Find(board, new[] { "Pantry/0", "Pantry", "Pantry/x" });
        Assert.Empty(result.Keys);
    }

    [Fact]
    public void Area_lookup_maps_every_board_index_to_its_room()
    {
        var board = Board("Pantry/0", "Pantry/9000", "Crafts Room/13", "Vault/23");
        var map = StaleBundleKeys.IndexToRoom(board.Keys);
        Assert.Equal("Pantry", map[9000]);
        Assert.Equal("Crafts Room", map[13]);
        Assert.Equal(4, map.Count);
    }
}
