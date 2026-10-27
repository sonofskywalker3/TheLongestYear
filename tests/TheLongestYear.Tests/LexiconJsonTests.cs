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

        Assert.Equal("=0x112", DayTableRuns.Write(DayTable.None));
        Assert.Equal("+0x112", DayTableRuns.Write(DayTable.Always));
        Assert.Equal("=35x35,+0x77", DayTableRuns.Write(DayTable.Available(d => d >= 35)));
        Assert.Equal("+5x107,=0x5", DayTableRuns.Write(DayTable.Exact(d => d + 5 <= 112 ? d + 5 : (int?)null)));
        DayTable single = DayTable.Always.Except(DayTable.Exact(d => d == 50 ? 50 : (int?)null));   // a one-day gap
        Assert.Equal("+0x49,=0x1,+0x62", DayTableRuns.Write(single));
        Assert.True(DayTableRuns.Read(DayTableRuns.Write(single)).Equals(single));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a-b:c")]
    [InlineData("=0x111")]
    [InlineData("=0x113")]
    [InlineData("=113x112")]
    [InlineData("=5x112")]
    [InlineData("+0x112,=0x1")]
    [InlineData("+1x112")]
    [InlineData("=0x0,=0x112")]
    [InlineData("?3x112")]
    [InlineData("=0x1,=0x2147483647")]
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
        Assert.True(back.Variants["base"].Tables.Count > 0);
        Assert.Equal(back.Variants["base"].Tables.OrderBy(x => x, StringComparer.Ordinal), back.Variants["base"].Tables);
        AnswerVariant b = back.Variants["base"];
        Assert.Equal(set.GameVersion, back.GameVersion);
        Assert.Equal(set.Layers, back.Layers);
        Assert.Equal(set.RowHashes.OrderBy(p => p.Key), back.RowHashes.OrderBy(p => p.Key));
        Assert.Equal(variant.Obtain.Keys.OrderBy(k => k, StringComparer.Ordinal), b.Obtain.Keys.OrderBy(k => k, StringComparer.Ordinal));
        foreach (string id in variant.Obtain.Keys)
            Assert.True(variant.Obtain[id].SequenceEqual(b.Obtain[id]), id);
        foreach (string id in variant.Obtain.Keys)
            Assert.True(variant.Obtain[id].Select(s => s.Inputs).SequenceEqual(b.Obtain[id].Select(s => s.Inputs),
                new InputsComparer()), "Inputs " + id);
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

    private sealed class InputsComparer : IEqualityComparer<IReadOnlyList<IReadOnlyList<string>>>
    {
        public bool Equals(IReadOnlyList<IReadOnlyList<string>>? x, IReadOnlyList<IReadOnlyList<string>>? y)
            => x!.Count == y!.Count && x.Zip(y).All(p => p.First.SequenceEqual(p.Second));

        public int GetHashCode(IReadOnlyList<IReadOnlyList<string>> obj) => obj.Count;
    }

    [Fact]
    public void HostileDetailKeepsOneSourcePerLineAndRoundTrips()
    {
        const string detail = "line1\nline2 \"quoted\" back\\slash \u00e9\u4e2d\r\ttab";
        var source = new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None, detail)
        {
            Inputs = new[] { new[] { "(O)1", "(O)2" } },
            Setup = new[] { new SetupStep("building:Coop", 3) },
        };
        var variant = new AnswerVariant(
            new Dictionary<string, IReadOnlyList<ObtainSource>> { ["(O)1"] = new[] { source, source with { Detail = "second" } } },
            new Dictionary<string, IReadOnlyList<ObtainSource>>(),
            Array.Empty<string>(), 1,
            new Dictionary<string, TheLongestYear.Core.Availability.PlaceWeek>(), Array.Empty<string>(),
            new Dictionary<string, TheLongestYear.Core.Availability.PlaceWeek>());
        var set = new AnswerSet("1", "1", new[] { "vanilla" }, new Dictionary<string, ulong>(),
            new Dictionary<string, AnswerVariant> { ["base"] = variant });

        string json = LexiconJson.Write(set);
        Assert.Equal(2, json.Split('\n').Count(l => l.TrimStart().StartsWith("{\"Conditions\"")));

        AnswerSet back = LexiconJson.Read(json);
        IReadOnlyList<ObtainSource> got = back.Variants["base"].Obtain["(O)1"];
        Assert.Equal(detail, got[0].Detail);
        Assert.True(got.SequenceEqual(variant.Obtain["(O)1"]));
        Assert.Equal(new[] { "(O)1", "(O)2" }, got[0].Inputs[0]);
    }
}
