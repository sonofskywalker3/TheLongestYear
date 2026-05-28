using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StashItemRecordTests
{
    [Fact]
    public void Round_trips_through_json()
    {
        var original = new StashItemRecord("(O)24", 3, 2);
        string json = JsonSerializer.Serialize(original);
        StashItemRecord restored = JsonSerializer.Deserialize<StashItemRecord>(json)!;

        Assert.Equal("(O)24", restored.ItemId);
        Assert.Equal(3, restored.Quantity);
        Assert.Equal(2, restored.Quality);
    }

    [Fact]
    public void Empty_list_round_trips_through_json()
    {
        var list = new System.Collections.Generic.List<StashItemRecord>();
        string json = JsonSerializer.Serialize(list);
        var restored = JsonSerializer.Deserialize<System.Collections.Generic.List<StashItemRecord>>(json)!;
        Assert.Empty(restored);
    }

    [Fact]
    public void Quality_zero_is_valid()
    {
        var r = new StashItemRecord("(O)1", 1, 0);
        Assert.Equal(0, r.Quality);
    }
}
