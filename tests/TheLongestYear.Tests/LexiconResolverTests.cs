using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The resolver's order: the shipped answers when the live rows equal theirs, else the disk
/// cache by key, else a build that is written to the cache.</summary>
public sealed class LexiconResolverTests
{
    private const string Game = "1.6.15";
    private const string Mod = "1.1.0";
    private static readonly string[] Vanilla = { RowHashes.Vanilla };
    private static readonly Lazy<ObtainabilityInputs> Inputs = new(VanillaInputsFixture.Load);
    private static readonly string ShippedPath = Path.Combine(AppContext.BaseDirectory, "Fixtures", "lexicon", "vanilla.json");

    /// <summary>A recognisable variant: one obtain id named after where it came from.</summary>
    private static AnswerVariant Marker(string tag)
    {
        var sources = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal) { [tag] = Array.Empty<ObtainSource>() };
        return new AnswerVariant(sources, sources, Array.Empty<string>(), 1,
            new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>());
    }

    private static AnswerSet SetOf(IReadOnlyDictionary<string, ulong> rows, string tag, string game = Game)
        => new(game, Mod, Vanilla, rows,
            new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = Marker(tag) });

    private static ObtainabilityInputs WithShopChanged(ObtainabilityInputs inputs)
    {
        var shops = inputs.Shops.ToList();
        shops[0] = shops[0] with { IsRandom = !shops[0].IsRandom };
        return inputs with { Shops = shops };
    }

    private sealed class Harness
    {
        public int Builds;
        public readonly Dictionary<string, AnswerSet> Written = new(StringComparer.Ordinal);
        public Func<string, AnswerSet?> Read = _ => null;

        public LexiconResolver Resolver(AnswerSet? shipped)
            => new(shipped, key => Read(key), (key, set) => Written[key] = set, (_, _) => { Builds++; return Marker("built"); });
    }

    [Fact]
    public void ShippedWinsWhenRowsMatch()
    {
        ObtainabilityInputs live = Inputs.Value;
        var h = new Harness { Read = _ => throw new Xunit.Sdk.XunitException("the cache must not be read") };
        Resolved r = h.Resolver(SetOf(RowHashes.Of(live, false), "shipped")).Resolve(live, false, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Shipped, r.Origin);
        Assert.True(r.Variant.Obtain.ContainsKey("shipped"));
        Assert.Equal(0, h.Builds);
        Assert.Empty(h.Written);
    }

    [Fact]
    public void ShippedPierreVariantIsResolvedOverBase()
    {
        ObtainabilityInputs live = Inputs.Value;
        AnswerSet shipped = AnswerSetMerge.With(null, AnswerSet.BaseVariant, Marker("base"), RowHashes.Of(live, false), Game, Mod, Vanilla);
        shipped = AnswerSetMerge.With(shipped, AnswerSet.PierreVariant, Marker("pierre"), RowHashes.Of(live, true), Game, Mod, Vanilla);
        var h = new Harness();
        Resolved r = h.Resolver(shipped).Resolve(live, true, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Shipped, r.Origin);
        Assert.True(r.Variant.Obtain.ContainsKey("pierre"));
        Assert.Empty(r.Variant.Obtain["base"]);   // the delta's empty entry stands over base's own id
        Assert.Equal(0, h.Builds);
    }

    [Fact]
    public void ShippedIsSkippedForAnotherGameVersion()
    {
        ObtainabilityInputs live = Inputs.Value;
        var h = new Harness();
        Resolved r = h.Resolver(SetOf(RowHashes.Of(live, false), "shipped", game: "1.6.14")).Resolve(live, false, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Computed, r.Origin);
        Assert.Equal(1, h.Builds);
    }

    [Fact]
    public void ShippedIsSkippedWhenTheVariantIsMissing()
    {
        ObtainabilityInputs live = Inputs.Value;
        var h = new Harness();
        // Base only, with Pierre's rows: the rows "match" only through the base fallback of RowHashesFor.
        Resolved r = h.Resolver(SetOf(RowHashes.Of(live, true), "shipped")).Resolve(live, true, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Computed, r.Origin);
    }

    [Fact]
    public void CacheHitWhenRowsDiffer()
    {
        ObtainabilityInputs live = WithShopChanged(Inputs.Value);
        IReadOnlyDictionary<string, ulong> rows = RowHashes.Of(live, false);
        string key = RowHashes.Key(rows, Vanilla, Game, Mod);
        var h = new Harness();
        h.Read = k => k == key ? SetOf(rows, "cached") : null;
        Resolved r = h.Resolver(SetOf(RowHashes.Of(Inputs.Value, false), "shipped")).Resolve(live, false, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Cache, r.Origin);
        Assert.Equal(key, r.Key);
        Assert.True(r.Variant.Obtain.ContainsKey("cached"));
        Assert.Equal(0, h.Builds);
        Assert.Empty(h.Written);
    }

    [Fact]
    public void MissBuildsAndWrites()
    {
        ObtainabilityInputs live = WithShopChanged(Inputs.Value);
        IReadOnlyDictionary<string, ulong> rows = RowHashes.Of(live, false);
        string key = RowHashes.Key(rows, Vanilla, Game, Mod);
        var h = new Harness();
        Resolved r = h.Resolver(SetOf(RowHashes.Of(Inputs.Value, false), "shipped")).Resolve(live, false, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Computed, r.Origin);
        Assert.Equal(1, h.Builds);
        Assert.True(r.Variant.Obtain.ContainsKey("built"));
        AnswerSet written = Assert.Single(h.Written, p => p.Key == key).Value;
        Assert.Equal(Game, written.GameVersion);
        Assert.Equal(Mod, written.ModVersion);
        Assert.Equal(Vanilla, written.Layers);
        Assert.Equal(rows.OrderBy(p => p.Key), written.RowHashes.OrderBy(p => p.Key));
        Assert.True(written.Resolve(AnswerSet.BaseVariant)!.Obtain.ContainsKey("built"));
        Assert.Equal(new[] { RowHashes.Vanilla }, r.DirtyOwners);
        Assert.Equal(1, r.DirtyCount);
    }

    [Fact]
    public void CorruptCacheFallsBackToBuild()
    {
        ObtainabilityInputs live = WithShopChanged(Inputs.Value);
        var h = new Harness { Read = _ => throw new JsonException("truncated") };
        Resolved r = h.Resolver(null).Resolve(live, false, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Computed, r.Origin);
        Assert.Equal(1, h.Builds);
        Assert.Single(h.Written);
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(System.Security.SecurityException))]
    [InlineData(typeof(NotSupportedException))]
    public void DeniedCacheReadFallsBackToBuild(Type exception)
    {
        ObtainabilityInputs live = WithShopChanged(Inputs.Value);
        var h = new Harness { Read = _ => throw (Exception)Activator.CreateInstance(exception)! };
        Resolved r = h.Resolver(null).Resolve(live, false, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Computed, r.Origin);
        Assert.Equal(1, h.Builds);
    }

    /// <summary>A build that says how many items it recomputed (the incremental one) is reported with
    /// that count, and is told which variant the save needs.</summary>
    [Fact]
    public void MissReportsTheBuildsDirtyCountAndVariant()
    {
        ObtainabilityInputs live = WithShopChanged(Inputs.Value);
        string? asked = null;
        var resolver = new LexiconResolver(SetOf(RowHashes.Of(Inputs.Value, false), "shipped"), _ => null, (_, _) => { },
            (_, _, variant) => { asked = variant; return new LexiconBuild(Marker("built"), 7); });
        Resolved r = resolver.Resolve(live, true, Game, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Computed, r.Origin);
        Assert.Equal(7, r.DirtyCount);
        Assert.Equal(AnswerSet.PierreVariant, asked);
    }

    [Fact]
    public void KeyChangesWhenShopRowChanges()
    {
        var h = new Harness();
        LexiconResolver resolver = h.Resolver(null);
        string before = resolver.Resolve(Inputs.Value, false, Game, Mod, Vanilla).Key;
        string after = resolver.Resolve(WithShopChanged(Inputs.Value), false, Game, Mod, Vanilla).Key;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void KeyIgnoresNothingButInputsLayersVersions()
    {
        var h = new Harness();
        LexiconResolver resolver = h.Resolver(null);
        string Key(string game, string mod, IReadOnlyList<string> layers)
            => resolver.Resolve(Inputs.Value, false, game, mod, layers).Key;
        string key = Key(Game, Mod, Vanilla);
        Assert.Equal(key, Key(Game, Mod, new[] { RowHashes.Vanilla }));
        Assert.NotEqual(key, Key(Game, Mod, new[] { RowHashes.Vanilla, "SomeMod.Layer" }));
        Assert.NotEqual(key, Key("1.6.16", Mod, Vanilla));
        Assert.NotEqual(key, Key(Game, "1.1.1", Vanilla));
        Assert.NotEqual(key, resolver.Resolve(Inputs.Value, true, Game, Mod, Vanilla).Key);
    }

    /// <summary>The shipped file's base rows are the fixture's rows: an unmodded load is served
    /// from the shipped answers, never built.</summary>
    [Fact]
    public void ShippedFileServesTheVanillaFixture()
    {
        AnswerSet shipped = LexiconJson.Read(File.ReadAllText(ShippedPath));
        var h = new Harness();
        Resolved r = h.Resolver(shipped).Resolve(Inputs.Value, false, shipped.GameVersion, Mod, Vanilla);
        Assert.Equal(AnswerOrigin.Shipped, r.Origin);
        Assert.Equal(0, h.Builds);
    }

    /// <summary>The models the load builds from a variant answer exactly as the cold build's do,
    /// through a JSON round trip: the same ids, count and sources.</summary>
    [Fact]
    public void ModelsFromAVariantEqualTheColdBuild()
    {
        ObtainabilityInputs live = Inputs.Value;
        ObtainabilityBuild model = ObtainabilityBuilder.Build(live);
        ObtainabilityBuild yearSort = ObtainabilityBuilder.Build(YearTwoItems.WithoutCartRandomStock(live));
        AnswerVariant variant = AnswerVariantFactory.From(model, yearSort, live, null, Array.Empty<string>(), null);
        AnswerSet set = new(Game, Mod, Vanilla, RowHashes.Of(live, false),
            new Dictionary<string, AnswerVariant> { [AnswerSet.BaseVariant] = variant });
        AnswerVariant read = LexiconJson.Read(LexiconJson.Write(set)).Resolve(AnswerSet.BaseVariant)!;

        Assert.Equal(model.Model.Count, read.RoutedCount());   // the lexicon INFO line's item count
        AssertSameModel(model.Model, read.ObtainModel());
        AssertSameModel(yearSort.Model, read.YearSortModel());
        Assert.Equal(model.Unresolved, read.Unresolved);
        Assert.Equal(model.Passes, read.Passes);
    }

    private static void AssertSameModel(ObtainabilityModel expected, ObtainabilityModel actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(expected.ItemIds.OrderBy(i => i, StringComparer.Ordinal), actual.ItemIds.OrderBy(i => i, StringComparer.Ordinal));
        foreach (string id in expected.ItemIds)
            Assert.True(expected.Sources(id).SequenceEqual(actual.Sources(id)), $"sources differ for {id}");
    }
}
