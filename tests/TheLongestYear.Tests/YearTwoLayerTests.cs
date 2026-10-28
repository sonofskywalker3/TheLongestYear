using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using TheLongestYear.Core.Lexicon;
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
}
