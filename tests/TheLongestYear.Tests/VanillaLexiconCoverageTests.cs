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

    /// <summary>The shipped answers are served only to the mod version that wrote them, so every
    /// release must regenerate the file after bumping the manifest (<c>tly_lexicon write vanilla</c>).</summary>
    [Fact]
    public void ShippedModVersionIsTheManifestVersion()
    {
        string manifestPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "manifest.json");
        Assert.True(File.Exists(manifestPath), $"The mod manifest is missing from the test output: {manifestPath}");
        using var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath),
            new System.Text.Json.JsonDocumentOptions { CommentHandling = System.Text.Json.JsonCommentHandling.Skip, AllowTrailingCommas = true });
        string version = manifest.RootElement.GetProperty("Version").GetString()!;
        Assert.True(Shipped.Value.ModVersion == version,
            $"assets/lexicon/vanilla.json was written by TLY {Shipped.Value.ModVersion} but the manifest is {version}: "
            + "regenerate it (tly_lexicon write vanilla, both variants) after the version bump, or it is ignored in game.");
    }

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
    public void ShippedYearSortMatchesFixtureColdBuild()
        => Assert.Equal(
            ObtainabilityGolden.Hash(ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(VanillaInputsFixture.Load())).Model),
            ObtainabilityGolden.Hash(Variant(AnswerSet.BaseVariant).YearSort));

    /// <summary>The Pierre variant against a cold build of the inputs dumped from a save that owns the
    /// upgrade (TLY's Data/Shops edit is live only in the game, so it is in that fixture's shop rows).</summary>
    [Fact]
    public void PierreVariantMatchesPierreFixtureColdBuild()
    {
        ObtainabilityInputs pierre = VanillaInputsFixture.LoadPierre();
        AnswerVariant shipped = Variant(AnswerSet.PierreVariant);
        Assert.Equal(ObtainabilityGolden.Hash(ObtainabilityBuilder.Build(pierre).Model), ObtainabilityGolden.Hash(shipped.Obtain));
        Assert.Equal(
            ObtainabilityGolden.Hash(ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(pierre)).Model),
            ObtainabilityGolden.Hash(shipped.YearSort));
    }

    /// <summary>The fixtures are the data the shipped answers were checked against, row for row, so the
    /// cold-build comparisons here compare the same data.</summary>
    [Fact]
    public void FixtureRowsAreTheShippedRows()
    {
        static List<KeyValuePair<string, ulong>> Sorted(IReadOnlyDictionary<string, ulong> rows)
            => rows.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        Assert.Equal(Sorted(Shipped.Value.RowHashesFor(AnswerSet.BaseVariant)), Sorted(RowHashes.Of(VanillaInputsFixture.Load(), false)));
        Assert.Equal(Sorted(Shipped.Value.RowHashesFor(AnswerSet.PierreVariant)), Sorted(RowHashes.Of(VanillaInputsFixture.LoadPierre(), true)));
    }

    public static IEnumerable<object[]> AvailabilityCases()
        => new[] { false, true }.SelectMany(pierre => Enum.GetValues<DifficultyStep>()
            .SelectMany(step => new[] { new object[] { pierre, step, false }, new object[] { pierre, step, true } }));

    /// <summary>Every shipped availability answer equals the one rebuilt offline from the availability
    /// inputs fixture of the same save state: a rule change that moves an answer fails here until
    /// <c>vanilla.json</c> is written again. The stamp is checked too, so the fixture is the projection
    /// the live game hashes.</summary>
    [Theory]
    [MemberData(nameof(AvailabilityCases))]
    public void VanillaAvailabilityAnswersMatchFixtureBuild(bool pierre, DifficultyStep step, bool kitchen)
    {
        AvailabilityInputs inputs = VanillaInputsFixture.LoadAvailability(pierre);
        Assert.Equal(!pierre, inputs.YearTwoCapped);
        string key = AvailabilityAnswers.Key(step, kitchen);
        AnswerVariant variant = Variant(pierre ? AnswerSet.PierreVariant : AnswerSet.BaseVariant);
        Assert.True(variant.Availability.TryGetValue(key, out AvailabilityAnswers? shipped), $"no shipped availability answers for {key}");
        AvailabilityAnswers built = AvailabilityInputs.Answers(inputs, step, kitchen);
        Assert.Equal(shipped!.Inputs, built.Inputs);

        static AnswerVariant Only(string k, AvailabilityAnswers answers)
            => new(new Dictionary<string, IReadOnlyList<ObtainSource>>(), new Dictionary<string, IReadOnlyList<ObtainSource>>(),
                Array.Empty<string>(), 0, new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>())
            {
                Availability = new Dictionary<string, AvailabilityAnswers>(StringComparer.Ordinal) { [k] = answers },
            };
        IReadOnlyList<string> differences = LexiconDiff.Compare(Only(key, shipped), Only(key, built));
        Assert.True(differences.Count == 0, $"{differences.Count} difference(s), first: " + string.Join("; ", differences.Take(5)));
    }

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

    [Theory]
    [InlineData("g2", "m")]
    [InlineData("g", "m2")]
    public void MergeOverAnotherVersionKeepsOnlyTheWrittenVariant(string game, string mod)
    {
        static AnswerVariant Make(string detail)
        {
            var map = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal)
            {
                ["(O)1"] = new[] { new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None, detail) },
            };
            return new AnswerVariant(map, map, Array.Empty<string>(), 1,
                new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>());
        }
        var rows = new Dictionary<string, ulong> { ["meta|pierre_year2"] = 1 };
        string[] layers = { "vanilla" };
        AnswerSet set = AnswerSetMerge.With(null, AnswerSet.BaseVariant, Make("base"), rows, "g", "m", layers);
        set = AnswerSetMerge.With(set, AnswerSet.PierreVariant, Make("pierre"), rows, "g", "m", layers);

        // The same version keeps the other variant.
        var warnings = new List<string>();
        AnswerSet same = AnswerSetMerge.With(set, AnswerSet.BaseVariant, Make("base2"), rows, "g", "m", layers, warnings.Add);
        Assert.Equal(2, same.Variants.Count);
        Assert.Empty(warnings);

        // Another version drops it (it is stale by definition), with one WARN naming it.
        AnswerSet moved = AnswerSetMerge.With(set, AnswerSet.BaseVariant, Make("base2"), rows, game, mod, layers, warnings.Add);
        Assert.Equal(new[] { AnswerSet.BaseVariant }, moved.Variants.Keys);
        Assert.Equal("base2", moved.Resolve(AnswerSet.BaseVariant)!.Obtain["(O)1"].Single().Detail);
        Assert.Equal((game, mod), (moved.GameVersion, moved.ModVersion));
        Assert.Contains(AnswerSet.PierreVariant, Assert.Single(warnings), StringComparison.Ordinal);
    }

    [Fact]
    public void LoadVanillaIsNullWithoutAFile()
        => Assert.Null(ShippedLexicon.LoadVanilla(Path.Combine(Path.GetTempPath(), "tly-no-such-mod-folder-" + Guid.NewGuid())));
}
