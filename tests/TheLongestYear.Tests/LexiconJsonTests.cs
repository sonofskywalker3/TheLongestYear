using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;
using Xunit.Abstractions;

namespace TheLongestYear.Tests;

public sealed class LexiconJsonTests
{
    private readonly ITestOutputHelper _output;

    public LexiconJsonTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void DayTableRunsRoundTrip()
    {
        DayTable[] tables =
        {
            DayTable.None,
            DayTable.Always,
            DayTable.InWeeks(WeekMask.ForSeason(Season.Spring)),
            DayTable.Exact(d => d is >= 10 and <= 20 || d >= 60 ? d + 3 : (int?)null),
        };
        foreach (DayTable t in tables)
            Assert.True(DayTableRuns.Read(DayTableRuns.Write(t)).Equals(t), DayTableRuns.Write(t));

        Assert.Equal("1-112:0", DayTableRuns.Write(DayTable.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1-50:0")]
    [InlineData("1-50:0,52-112:0")]
    [InlineData("1-60:0,50-112:0")]
    [InlineData("1-112:200")]
    [InlineData("1-112")]
    [InlineData("a-b:c")]
    [InlineData("1-113:0")]
    public void DayTableRunsRejectsMalformedText(string text)
        => Assert.Throws<FormatException>(() => DayTableRuns.Read(text));

    [Fact]
    public void AnswerSetRoundTripsFixtureModel()
    {
        ObtainabilityInputs inputs = VanillaInputsFixture.Load();
        ObtainabilityBuild build = ObtainabilityBuilder.Build(inputs);
        ObtainabilityBuild yearSort = ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(inputs));
        var maps = new Dictionary<string, TheLongestYear.Core.Availability.PlaceWeek>
        {
            ["Town"] = new(1, 1, 1), ["Desert"] = new(9, 11, 13),
        };
        AnswerVariant variant = AnswerVariantFactory.From(build, yearSort, inputs, null, new[] { "Town", "Desert" }, null)
            with { MapWeeks = maps, ShopWeeks = new Dictionary<string, TheLongestYear.Core.Availability.PlaceWeek> { ["Pierre"] = new(1, 1, 2) } };
        var set = new AnswerSet("1.6.15", "1.1.0", new[] { "vanilla" },
            new Dictionary<string, ulong> { ["objects"] = ulong.MaxValue, ["shops"] = 7 },
            new Dictionary<string, AnswerVariant> { ["base"] = variant });

        foreach (string id in inputs.Objects.Keys)
            Assert.True(variant.Obtain.ContainsKey(id), id);

        string json = LexiconJson.Write(set);
        string path = Path.Combine(Path.GetTempPath(), "tly-vanilla-answers.json");
        File.WriteAllText(path, json);
        _output.WriteLine($"serialised vanilla answer set: {new FileInfo(path).Length} bytes ({variant.Obtain.Count} obtain ids)");

        AnswerSet back = LexiconJson.Read(json);
        AnswerVariant b = back.Variants["base"];
        Assert.Equal(set.GameVersion, back.GameVersion);
        Assert.Equal(set.Layers, back.Layers);
        Assert.Equal(set.RowHashes.OrderBy(p => p.Key), back.RowHashes.OrderBy(p => p.Key));
        Assert.Equal(variant.Obtain.Keys.OrderBy(k => k, StringComparer.Ordinal), b.Obtain.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string id in variant.Obtain.Keys)
            Assert.True(variant.Obtain[id].SequenceEqual(b.Obtain[id]), id);
        foreach (string id in variant.YearSort.Keys)
            Assert.True(variant.YearSort[id].SequenceEqual(b.YearSort[id]), id);
        Assert.Equal(variant.Passes, b.Passes);
        Assert.Equal(variant.Unresolved, b.Unresolved);
        Assert.Equal(maps.OrderBy(p => p.Key), b.MapWeeks.OrderBy(p => p.Key));
        Assert.Equal(variant.ShopWeeks.OrderBy(p => p.Key), b.ShopWeeks.OrderBy(p => p.Key));
        Assert.Equal(variant.UnreachedMaps, b.UnreachedMaps);

        // Both the undelayed-set and undelayed-null shapes are exercised by the vanilla model.
        var all = variant.Obtain.Values.SelectMany(l => l).ToList();
        Assert.Contains(all, s => s.UndelayedLands != null);
        Assert.Contains(all, s => s.UndelayedLands == null);
        Assert.Equal(all.Count(s => s.UndelayedLands == null),
            b.Obtain.Values.SelectMany(l => l).Count(s => s.UndelayedLands == null));

        string hashBefore = ObtainabilityGolden.Hash(new ObtainabilityModel(variant.Obtain));
        string hashAfter = ObtainabilityGolden.Hash(new ObtainabilityModel(b.Obtain));
        Assert.Equal(hashBefore, hashAfter);
        _output.WriteLine($"ids compared: {variant.Obtain.Count} + {variant.YearSort.Count}; hash {hashAfter}");
    }

    [Fact]
    public void WriteIsOrdinallySortedAndLineStable()
    {
        ObtainabilityInputs inputs = VanillaInputsFixture.Load();
        ObtainabilityBuild build = ObtainabilityBuilder.Build(inputs);
        AnswerVariant variant = AnswerVariantFactory.From(build, build, inputs, null, Array.Empty<string>(), null);
        var set = new AnswerSet("1.6.15", "1.1.0", new[] { "vanilla" },
            new Dictionary<string, ulong> { ["zeta"] = 1, ["Alpha"] = 2, ["beta"] = 3 },
            new Dictionary<string, AnswerVariant> { ["pierre_year2_seeds"] = variant, ["base"] = variant });

        string a = LexiconJson.Write(set), b = LexiconJson.Write(set);
        Assert.Equal(a, b);

        string[] lines = a.Split('\n');
        int hashes = Array.FindIndex(lines, l => l.Contains("\"RowHashes\""));
        string[] keys = lines.Skip(hashes + 1).TakeWhile(l => !l.TrimStart().StartsWith("}"))
            .Select(l => l.Trim().Split('"')[1]).ToArray();
        Assert.Equal(new[] { "Alpha", "beta", "zeta" }, keys);
        Assert.True(a.IndexOf("\"base\"", StringComparison.Ordinal) < a.IndexOf("\"pierre_year2_seeds\"", StringComparison.Ordinal));

        // Obtain ids sit in ordinal order, one per line start.
        int obtain = Array.FindIndex(lines, l => l.TrimStart().StartsWith("\"Obtain\""));
        var ids = new List<string>();
        for (int i = obtain + 1; i < lines.Length && !lines[i].StartsWith("      }"); i++)
            if (lines[i].StartsWith("        \"")) ids.Add(lines[i].Trim().Split('"')[1]);
        Assert.True(ids.Count > 100);
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal), ids);
    }
}
