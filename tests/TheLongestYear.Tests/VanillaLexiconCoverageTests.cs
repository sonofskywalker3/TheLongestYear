using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The shipped vanilla lexicon (<c>src/TheLongestYear/assets/lexicon/vanilla.json</c>, linked
/// into the test output as <c>Fixtures/lexicon/vanilla.json</c>) answers for every vanilla object and
/// map, matches the offline cold build, and carries the Pierre year-2 seeds variant.</summary>
public sealed class VanillaLexiconCoverageTests
{
    private static readonly string[] PierreYear2Seeds = { "(O)476", "(O)485", "(O)489" };
    private static readonly Lazy<AnswerSet> Shipped = new(Load);

    private static AnswerSet Load()
    {
        string folder = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        string path = Path.Combine(folder, "lexicon", "vanilla.json");
        Assert.True(File.Exists(path), $"The shipped lexicon is missing from the test output: {path}");
        return LexiconJson.Read(File.ReadAllText(path));
    }

    private static AnswerVariant Variant(string name)
        => Shipped.Value.Resolve(name) ?? throw new Xunit.Sdk.XunitException($"The shipped lexicon has no '{name}' variant.");

    [Fact]
    public void EveryVanillaObjectHasAnEntry()
    {
        AnswerVariant baseline = Variant(AnswerSet.BaseVariant);
        var missing = VanillaItemIds.All.Where(id => id.StartsWith("(O)", StringComparison.Ordinal))
            .Where(id => !baseline.Obtain.ContainsKey(id)).OrderBy(id => id, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "No entry for: " + string.Join(", ", missing));
    }

    [Fact]
    public void EveryVanillaMapIsRecorded()
    {
        AnswerVariant baseline = Variant(AnswerSet.BaseVariant);
        var missing = VanillaLocationKeys.All
            .Where(key => !baseline.MapWeeks.ContainsKey(key) && !baseline.UnreachedMaps.Contains(key))
            .OrderBy(key => key, StringComparer.Ordinal).ToList();
        Assert.True(missing.Count == 0, "Maps in neither MapWeeks nor UnreachedMaps: " + string.Join(", ", missing));
    }

    [Fact]
    public void BaseVariantMatchesFixtureColdBuild()
        => Assert.Equal(ObtainabilityGoldenTests.VanillaHash, ObtainabilityGolden.Hash(Variant(AnswerSet.BaseVariant).Obtain));

    [Fact]
    public void PierreVariantHasOwnEntries()
    {
        AnswerVariant baseline = Variant(AnswerSet.BaseVariant);
        AnswerVariant pierre = Variant(AnswerSet.PierreVariant);
        foreach (string id in PierreYear2Seeds)
        {
            Assert.True(YearOneShop(pierre, id), $"{id}: no year-1 shop source in the Pierre variant");
            Assert.False(YearOneShop(baseline, id), $"{id}: base already has a year-1 shop source");
        }
    }

    private static bool YearOneShop(AnswerVariant variant, string id)
        => variant.Obtain.TryGetValue(id, out var sources)
           && sources.Any(s => s.Kind == SourceKind.Shop && !s.Conditions.YearTwo);

    [Fact]
    public void MergeKeepsTheOtherVariantAndStoresItAsADelta()
    {
        static ObtainSource Shop(string detail) => new(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None, detail);
        static AnswerVariant Make(params (string Id, ObtainSource[] Sources)[] entries)
        {
            var map = entries.ToDictionary(e => e.Id, e => (IReadOnlyList<ObtainSource>)e.Sources, StringComparer.Ordinal);
            return new AnswerVariant(map, map, Array.Empty<string>(), 1,
                new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>());
        }
        AnswerVariant baseline = Make(("(O)1", new[] { Shop("a") }), ("(O)2", new[] { Shop("b") }), ("(O)3", new[] { Shop("c") }));
        AnswerVariant pierre = Make(("(O)1", new[] { Shop("a") }), ("(O)2", new[] { Shop("b"), Shop("y1") }));
        var baseRows = new Dictionary<string, ulong> { ["meta|pierre_year2"] = 1 };
        var pierreRows = new Dictionary<string, ulong> { ["meta|pierre_year2"] = 2 };
        string[] layers = { "vanilla" };

        // Pierre first, then base: the order the two writes come in must not matter.
        AnswerSet set = AnswerSetMerge.With(null, AnswerSet.PierreVariant, pierre, pierreRows, "g", "m", layers);
        set = AnswerSetMerge.With(set, AnswerSet.BaseVariant, baseline, baseRows, "g", "m", layers);
        set = LexiconJson.Read(LexiconJson.Write(set));

        AnswerVariant stored = set.Variants[AnswerSet.PierreVariant];
        Assert.Equal(new[] { "(O)2", "(O)3" }, stored.Obtain.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Empty(stored.Obtain["(O)3"]);
        AnswerVariant resolved = set.Resolve(AnswerSet.PierreVariant)!;
        Assert.Equal(3, resolved.Obtain.Count);
        Assert.Equal(2, resolved.Obtain["(O)2"].Count);
        Assert.Equal(3, set.Resolve(AnswerSet.BaseVariant)!.Obtain.Count);
        Assert.Equal(1UL, set.RowHashes["meta|pierre_year2"]);
        Assert.Equal(2UL, set.RowHashesFor(AnswerSet.PierreVariant)["meta|pierre_year2"]);

        // Rewriting base keeps the Pierre variant whole.
        set = AnswerSetMerge.With(set, AnswerSet.BaseVariant, baseline, baseRows, "g", "m", layers);
        Assert.Equal(2, set.Resolve(AnswerSet.PierreVariant)!.Obtain["(O)2"].Count);
    }

    [Fact]
    public void LoadVanillaIsNullWithoutAFile()
        => Assert.Null(ShippedLexicon.LoadVanilla(Path.Combine(Path.GetTempPath(), "tly-no-such-mod-folder-" + Guid.NewGuid())));
}
