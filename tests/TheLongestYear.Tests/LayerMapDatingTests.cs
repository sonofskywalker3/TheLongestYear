using System;
using System.Collections.Generic;
using System.Text.Json;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Seven Deadly Sins support (2026-11-05): a layer can say what the maps' own doors do not.
/// The mod lists Midnight Carp with no season in a cave whose door stands behind a paid bundle; read
/// from the door alone the cave was open in week 1, so a Summer goal could ask for the carp. And its
/// Eastern Forest is entered by touch-action warp tiles, which the walk does not see, so its fish
/// were never placed.</summary>
public class LayerMapDatingTests
{
    private const string Cave = "Custom_SDS.Cave7";
    private const string Inner = "Custom_SDS.Caveroom";
    private const string Forest = "Custom_SDS.EasternForest_main";

    private static readonly RawLocationLink[] Doors =
    {
        new("Farm", "Backwoods"), new("Backwoods", "Mountain"),
        new("Mountain", "Custom_SDS.Cavebase"), new("Custom_SDS.Cavebase", Cave), new(Cave, Inner),
        new("Farm", "Forest"), new("Forest", "Woods"),
    };

    private static ObtainSource Fish(params string[] requires)
        => new(SourceKind.Fish, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { Requires = requires }, "test");

    [Fact]
    public void A_map_reads_as_week_one_from_its_door_alone()
    {
        LocationWeeks walk = LocationWeeks.Build(Doors, _ => false);
        Assert.True(walk.TryGet(Cave, out PlaceWeek week));
        Assert.Equal(1, week.Week);
    }

    [Fact]
    public void A_layer_week_holds_the_map_and_every_map_behind_it()
    {
        LocationWeeks walk = LocationWeeks.Build(Doors, _ => false,
            openingWeeks: new Dictionary<string, int> { [Cave] = 10 });
        Assert.True(walk.TryGet(Cave, out PlaceWeek cave));
        Assert.Equal(new PlaceWeek(10, 10, 10), cave);
        Assert.True(walk.TryGet(Inner, out PlaceWeek inner));
        Assert.Equal(10, inner.Week);
        Assert.True(walk.TryGet("Custom_SDS.Cavebase", out PlaceWeek cavebase));
        Assert.Equal(1, cavebase.Week);
    }

    [Fact]
    public void A_layer_week_never_opens_a_map_earlier_than_its_own_gate()
    {
        LocationWeeks walk = LocationWeeks.Build(Doors, _ => false,
            openingWeeks: new Dictionary<string, int> { ["Woods"] = 2 });
        Assert.True(walk.TryGet("Woods", out PlaceWeek woods));
        Assert.Equal(LocationGating.WeekFor("Woods"), woods.Week);
    }

    [Fact]
    public void A_map_with_no_door_is_reached_through_a_layer_link()
    {
        Assert.False(LocationWeeks.Build(Doors, _ => false).TryGet(Forest, out _));
        var withLink = new List<RawLocationLink>(Doors) { new("Forest", Forest) };
        LocationWeeks walk = LocationWeeks.Build(withLink, _ => false,
            openingWeeks: new Dictionary<string, int> { [Forest] = 9 });
        Assert.True(walk.TryGet(Forest, out PlaceWeek week));
        Assert.Equal(9, week.Week);
    }

    [Fact]
    public void A_route_at_a_layer_dated_map_lands_no_earlier_than_its_week()
    {
        var weeks = new Dictionary<string, int> { [Cave] = 10 };
        int day = MapWeekGate.FirstDayOf(10);
        ObtainSource gated = MapWeekGate.Apply(Fish("location:" + Cave), weeks);
        Assert.Equal(day, gated.Lands.Lands(1));
        Assert.Equal(day + 5, gated.Lands.Lands(day + 5));
        // Without the layer, and for any other map, the route is untouched.
        Assert.Equal(1, MapWeekGate.Apply(Fish("location:" + Cave)).Lands.Lands(1));
        Assert.Equal(1, MapWeekGate.Apply(Fish("location:Town"), weeks).Lands.Lands(1));
    }

    [Fact]
    public void The_later_of_a_layer_week_and_the_built_in_gate_counts()
    {
        Assert.Equal(MapWeekGate.WoodsOpeningDay,
            MapWeekGate.Apply(Fish("location:Woods"), new Dictionary<string, int> { ["Woods"] = 2 }).Lands.Lands(1));
        Assert.Equal(MapWeekGate.FirstDayOf(9),
            MapWeekGate.Apply(Fish("location:Woods"), new Dictionary<string, int> { ["Woods"] = 9 }).Lands.Lands(1));
    }

    private const string Head = "{\"UniqueId\": \"ModA\", \"CheckedVersion\": \"1.0.0\", ";

    [Fact]
    public void An_inputs_file_carries_map_weeks_and_links_into_the_marks()
    {
        LayerInputs read = LayerFile.ReadInputs(Head +
            "\"MapWeeks\": {\"" + Cave + "\": 10}, \"MapLinks\": [{\"From\": \"Forest\", \"To\": \"" + Forest + "\"}]}");
        Assert.Equal(10, read.MapWeeks[Cave]);
        Assert.Equal(new RawLocationLink("Forest", Forest), Assert.Single(read.MapLinks));

        LayerSet set = LayerSet.Compose(null, new[] { read }, Array.Empty<LayerAnswers>(), _ => { });
        Assert.Equal(10, set.Marks.MapWeeks[Cave]);
        Assert.Equal(new RawLocationLink("Forest", Forest), Assert.Single(set.Marks.MapLinks));

        LayerInputs back = LayerFile.ReadInputs(LayerFile.WriteInputs(read));
        Assert.Equal(10, back.MapWeeks[Cave]);
        Assert.Single(back.MapLinks);
    }

    [Fact]
    public void An_inputs_file_without_them_reads_as_none()
    {
        LayerInputs read = LayerFile.ReadInputs("{\"UniqueId\": \"ModA\", \"CheckedVersion\": \"1.0.0\"}");
        Assert.Empty(read.MapWeeks);
        Assert.Empty(read.MapLinks);
        Assert.Empty(YearTwoMarks.None.MapWeeks);
        Assert.Empty(YearTwoMarks.None.MapLinks);
    }

    [Theory]
    [InlineData("\"MapWeeks\": {\"Custom_X\": 0}")]
    [InlineData("\"MapWeeks\": {\"Custom_X\": 17}")]
    [InlineData("\"MapLinks\": [{\"From\": \"Forest\"}]")]
    [InlineData("\"MapLinks\": [null]")]
    public void A_bad_map_week_or_link_is_refused(string member)
        => Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Head + member + "}"));
}

/// <summary>A map a layer calls year 2 closes its spawn rows whatever the walks say (Nexus bug
/// 1166150: the Midnight Carp row with no season in a cave that has a door and warps both ways).</summary>
public class LayerClosedMapSpawnTests
{
    private const string Cave = "Custom_SDS.Cave7";

    private static readonly RawLocationLink[] Links =
    {
        new("Farm", "Mountain"), new("Mountain", "Custom_SDS.Cavebase"), new("Custom_SDS.Cavebase", Cave),
        new(Cave, "Custom_SDS.Cavebase"),
    };

    private static readonly RawSpawnEntry[] CarpRows =
    {
        new("(O)269", Season.Fall, null, "Mountain"),
        new("(O)269", Season.Winter, null, "Mountain"),
        new("(O)269", null, null, Cave),
    };

    private static SpawnPlaceRule Rule(IReadOnlySet<string>? closed)
    {
        var all = new[] { "Farm", "Mountain", "Custom_SDS.Cavebase", Cave };
        return new SpawnPlaceRule(
            ReachabilityGraph.UnreachableLocations(Links, all, _ => false),
            LocationWeeks.Build(Links, name => closed != null && closed.Contains(name)),
            closed);
    }

    [Fact]
    public void Read_by_its_doors_the_cave_row_stays()
    {
        SpawnRowSplit split = Rule(null).Split(CarpRows);
        Assert.Equal(3, split.Kept.Count);
    }

    [Fact]
    public void A_layer_closed_map_drops_its_rows()
    {
        SpawnRowSplit split = Rule(new HashSet<string> { Cave }).Split(CarpRows);
        Assert.Equal(2, split.Kept.Count);
        Assert.All(split.Kept, row => Assert.Equal("Mountain", row.Location));
        Assert.Equal(Cave, Assert.Single(split.Dropped).Location);
    }
}
