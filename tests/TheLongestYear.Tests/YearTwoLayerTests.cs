using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The year-2 sections of a layer inputs file: week/effort rulings and the maps and shops
/// whose data routes count as year 2 or Ginger Island.</summary>
public sealed class YearTwoLayerTests
{
    private const string Id = "(O)X";

    private static string Doc(string sections)
        => "{\"UniqueId\": \"ModC\", \"CheckedVersion\": \"1.0.0\"" + (sections.Length == 0 ? "" : ", " + sections) + "}";

    private static string Ruling(int week, int effort, string basis)
        => $"\"YearTwoRulings\": {{\"{Id}\": {{\"Week\": {week}, \"Effort\": {effort}, \"Basis\": \"{basis}\"}}}}";

    [Fact]
    public void ReadInputs_reads_year_two_sections()
    {
        LayerInputs read = LayerFile.ReadInputs(Doc(
            Ruling(21, 7, "b") + ", \"YearTwoMaps\": {\"Custom_Highlands\": \"GingerIsland\"}, \"YearTwoShops\": {\"Shop1\": \"YearTwo\"}"));
        Assert.Equal(new YearTwoRuling(21, 7, "b"), Assert.Single(read.YearTwoRulings).Value);
        Assert.Equal(Id, read.YearTwoRulings.Keys.Single());
        Assert.Equal(YearTwoFlag.GingerIsland, Assert.Single(read.YearTwoMaps).Value);
        Assert.Equal("Custom_Highlands", read.YearTwoMaps.Keys.Single());
        Assert.Equal(YearTwoFlag.YearTwo, Assert.Single(read.YearTwoShops).Value);
        Assert.Equal("Shop1", read.YearTwoShops.Keys.Single());
    }

    [Fact]
    public void ReadInputs_missing_year_two_sections_read_as_empty()
    {
        LayerInputs read = LayerFile.ReadInputs("{\"UniqueId\": \"ModC\"}");
        Assert.Empty(read.YearTwoRulings);
        Assert.Empty(read.YearTwoMaps);
        Assert.Empty(read.YearTwoShops);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(33)]
    public void ReadInputs_refuses_week_outside_year_two(int week)
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc(Ruling(week, 5, "b"))));
        Assert.Contains(Id, e.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(13)]
    public void ReadInputs_refuses_effort_outside_scale(int effort)
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc(Ruling(20, effort, "b"))));
        Assert.Contains(Id, e.Message);
    }

    [Fact]
    public void ReadInputs_refuses_blank_basis()
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc(Ruling(20, 5, " "))));
        Assert.Contains(Id, e.Message);
    }

    [Fact]
    public void ReadInputs_refuses_null_ruling()
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc($"\"YearTwoRulings\": {{\"{Id}\": null}}")));
        Assert.Contains(Id, e.Message);
    }

    [Fact]
    public void ReadInputs_refuses_null_map_flag()
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc("\"YearTwoMaps\": {\"M\": null}")));
        Assert.Contains("M", e.Message);
    }

    [Fact]
    public void ReadInputs_refuses_null_shop_flag()
    {
        var e = Assert.Throws<JsonException>(() => LayerFile.ReadInputs(Doc("\"YearTwoShops\": {\"S\": null}")));
        Assert.Contains("S", e.Message);
    }

    [Fact]
    public void WriteInputs_round_trips_year_two_sections()
    {
        LayerInputs read = LayerFile.ReadInputs(Doc(
            Ruling(32, 12, "b") + ", \"YearTwoMaps\": {\"M\": \"GingerIsland\"}, \"YearTwoShops\": {\"S\": \"YearTwo\"}"));
        LayerInputs back = LayerFile.ReadInputs(LayerFile.WriteInputs(read));
        Assert.Equal(read.YearTwoRulings.OrderBy(p => p.Key), back.YearTwoRulings.OrderBy(p => p.Key));
        Assert.Equal(read.YearTwoMaps.OrderBy(p => p.Key), back.YearTwoMaps.OrderBy(p => p.Key));
        Assert.Equal(read.YearTwoShops.OrderBy(p => p.Key), back.YearTwoShops.OrderBy(p => p.Key));
        Assert.Single(back.YearTwoRulings);
    }

    private const string Olive = "(O)ModA.Olive";

    private static ObtainSource Route(params string[] requires)
        => new(SourceKind.Forage, DayTable.Always, Reliability.Dependable,
            ObtainConditions.None with { Requires = requires }, "route");

    private static IReadOnlyDictionary<string, YearTwoFlag> Flags(string key, YearTwoFlag flag)
        => new Dictionary<string, YearTwoFlag>(StringComparer.Ordinal) { [key] = flag };

    private static readonly IReadOnlyDictionary<string, YearTwoFlag> NoFlags = new Dictionary<string, YearTwoFlag>(StringComparer.Ordinal);

    [Fact]
    public void Mark_sets_island_flag_for_route_on_named_map()
    {
        var marks = new YearTwoMarks(Flags("Custom_Highlands", YearTwoFlag.GingerIsland), NoFlags);
        ObtainSource marked = marks.Mark(Route("location:Custom_Highlands"));
        Assert.True(marked.Conditions.GingerIsland);
        Assert.False(marked.Conditions.YearTwo);
    }

    [Fact]
    public void Mark_sets_year_two_flag_for_route_at_named_shop()
    {
        var marks = new YearTwoMarks(NoFlags, Flags("S1", YearTwoFlag.YearTwo));
        ObtainSource marked = marks.Mark(Route("shop:S1"));
        Assert.True(marked.Conditions.YearTwo);
        Assert.False(marked.Conditions.GingerIsland);
    }

    [Fact]
    public void Mark_matches_whole_name_only()
    {
        var marks = new YearTwoMarks(Flags("Custom_Highlands", YearTwoFlag.GingerIsland), Flags("S1", YearTwoFlag.YearTwo));
        ObtainSource cavern = Route("location:Custom_HighlandsCavern");
        ObtainSource shop = Route("shop:S10");
        Assert.Same(cavern, marks.Mark(cavern));
        Assert.Same(shop, marks.Mark(shop));
    }

    private static ObtainabilityBuild BuildOlive(YearTwoMarks? marks)
    {
        var extra = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal)
        {
            [Olive] = new[] { Route("location:Custom_Highlands") },
        };
        return ObtainabilityBuilder.Build(VanillaInputsFixture.Load(), extraDirect: extra, marks: marks);
    }

    [Fact]
    public void Build_flags_data_route_and_keeps_it_out_of_year_one_fold()
    {
        var marks = new YearTwoMarks(Flags("Custom_Highlands", YearTwoFlag.GingerIsland), NoFlags);
        ObtainabilityModel model = BuildOlive(marks).Model;
        Assert.True(model.Sources(Olive).Single().Conditions.GingerIsland);
        Assert.Null(model.Table(Olive, ObtainFilter.DependableOnly).LandingWeek(1));
        Assert.Equal(1, model.Table(Olive, ObtainFilter.DependableOnly with { IncludeGingerIsland = true }).LandingWeek(1));
    }

    [Fact]
    public void Build_without_marks_is_unchanged()
    {
        ObtainabilityModel model = BuildOlive(null).Model;
        Assert.False(model.Sources(Olive).Single().Conditions.GingerIsland);
        Assert.Equal(1, model.Table(Olive, ObtainFilter.DependableOnly).LandingWeek(1));
    }

    [Fact]
    public void Compose_merges_maps_and_shops_later_wins_with_warn()
    {
        LayerInputs a = LayerFile.ReadInputs("{\"UniqueId\": \"ModA\", \"YearTwoMaps\": {\"M\": \"YearTwo\"}, \"YearTwoShops\": {\"S\": \"YearTwo\"}}");
        LayerInputs b = LayerFile.ReadInputs("{\"UniqueId\": \"ModB\", \"YearTwoMaps\": {\"M\": \"GingerIsland\"}}");
        var warns = new List<string>();
        LayerSet set = LayerSet.Compose(null, new[] { a, b }, Array.Empty<LayerAnswers>(), warns.Add);
        Assert.Equal(YearTwoFlag.GingerIsland, set.Marks.Maps["M"]);
        Assert.Equal(YearTwoFlag.YearTwo, set.Marks.Shops["S"]);
        string warn = Assert.Single(warns);
        Assert.Contains("ModA", warn);
        Assert.Contains("ModB", warn);
    }
}
