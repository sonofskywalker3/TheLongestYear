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

    [Fact]
    public void Preserve_fields_default_to_null_for_plain_items()
    {
        // The 3-arg form (plain ore/wood/sprinkler) carries no preserve identity.
        var r = new StashItemRecord("(O)378", 5, 0);
        Assert.Null(r.PreservedParentSheetIndex);
        Assert.Null(r.Preserve);
        Assert.Null(r.Price);
    }

    [Fact]
    public void Preserve_fields_round_trip_through_json()
    {
        // A Smoked Legend: source-fish id 163, PreserveType.SmokedFish, baked price ~21000.
        var original = new StashItemRecord("(O)SmokedFish", 1, 0,
            PreservedParentSheetIndex: "163", Preserve: 7, Price: 21000);
        string json = JsonSerializer.Serialize(original);
        StashItemRecord restored = JsonSerializer.Deserialize<StashItemRecord>(json)!;

        Assert.Equal("163", restored.PreservedParentSheetIndex);
        Assert.Equal(7, restored.Preserve);
        Assert.Equal(21000, restored.Price);
    }

    [Fact]
    public void Legacy_three_field_json_deserializes_with_null_preserve_fields()
    {
        // Saves written before the preserve fields existed must still load (forward-compat).
        const string legacyJson = "{\"ItemId\":\"(O)24\",\"Quantity\":3,\"Quality\":2}";
        StashItemRecord restored = JsonSerializer.Deserialize<StashItemRecord>(legacyJson)!;

        Assert.Equal("(O)24", restored.ItemId);
        Assert.Equal(3, restored.Quantity);
        Assert.Equal(2, restored.Quality);
        Assert.Null(restored.PreservedParentSheetIndex);
        Assert.Null(restored.Preserve);
        Assert.Null(restored.Price);
    }
}
