using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The lexicon's files: the shipped load and the disk cache never throw on an unreadable
/// or unwritable file (a denied Android storage permission included); they log and miss.</summary>
public sealed class LexiconDiskCacheTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "tly-lexicon-tests-" + Guid.NewGuid().ToString("N"));
    private readonly List<string> _warns = new();
    private readonly List<string> _traces = new();

    public void Dispose()
    {
        if (!Directory.Exists(_dir)) return;
        foreach (string f in Directory.GetFiles(_dir)) File.SetAttributes(f, FileAttributes.Normal);
        Directory.Delete(_dir, recursive: true);
    }

    private LexiconDiskCache Cache(int max = 3) => new(_dir, max, _warns.Add, _traces.Add);

    private static AnswerSet Set(string tag)
    {
        var sources = new Dictionary<string, IReadOnlyList<ObtainSource>> { [tag] = Array.Empty<ObtainSource>() };
        var variant = new AnswerVariant(sources, sources, Array.Empty<string>(), 1,
            new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>());
        return new AnswerSet("1.6.15", "1.1.0", new[] { RowHashes.Vanilla }, new Dictionary<string, ulong>(),
            new Dictionary<string, AnswerVariant> { [AnswerSet.BaseVariant] = variant });
    }

    [Theory]
    [InlineData(typeof(UnauthorizedAccessException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(SecurityException))]
    [InlineData(typeof(NotSupportedException))]
    [InlineData(typeof(System.Text.Json.JsonException))]
    public void ShippedLoadFailureIsAWarnAndNoShippedAnswers(Type exception)
    {
        AnswerSet? shipped = LexiconDiskCache.LoadShipped(() => throw (Exception)Activator.CreateInstance(exception)!, _warns.Add, _traces.Add);
        Assert.Null(shipped);
        Assert.Single(_warns);
    }

    [Fact]
    public void WithoutShippedAnswersTheResolverFallsThroughToTheCache()
    {
        AnswerSet? shipped = LexiconDiskCache.LoadShipped(() => throw new UnauthorizedAccessException("denied"), _warns.Add, _traces.Add);
        ObtainabilityInputs live = VanillaInputsFixture.Load();
        LexiconDiskCache cache = Cache();
        string key = RowHashes.Key(RowHashes.Of(live, false), new[] { RowHashes.Vanilla }, "1.6.15", "1.1.0");
        cache.Write(key, Set("cached"));
        int builds = 0;
        var resolver = new LexiconResolver(shipped, cache.Read, cache.Write, (_, _) => { builds++; return Set("built").Variants[AnswerSet.BaseVariant]; });
        Resolved r = resolver.Resolve(live, false, "1.6.15", "1.1.0", new[] { RowHashes.Vanilla });
        Assert.Equal(AnswerOrigin.Cache, r.Origin);
        Assert.True(r.Variant.Obtain.ContainsKey("cached"));
        Assert.Equal(0, builds);
    }

    [Fact]
    public void WriteThenReadRoundTrips()
    {
        LexiconDiskCache cache = Cache();
        cache.Write("abcdef0123", Set("one"));
        Assert.True(cache.Read("abcdef0123")!.Resolve(AnswerSet.BaseVariant)!.Obtain.ContainsKey("one"));
        Assert.Null(cache.Read("0000000000"));
        Assert.Empty(_warns);
    }

    /// <summary>An entry whose write time cannot be touched (a denied permission) is still served.</summary>
    [Fact]
    public void AFailedTouchStillServesTheEntry()
    {
        var cache = new LexiconDiskCache(_dir, 3, _warns.Add, _traces.Add, _ => throw new UnauthorizedAccessException("denied"));
        cache.Write("abcdef0123", Set("one"));
        Assert.NotNull(cache.Read("abcdef0123"));
        Assert.Empty(_warns);
        Assert.Single(_traces);
    }

    [Fact]
    public void ACorruptEntryIsOneWarnAndAMiss()
    {
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "abcdef0123.json"), "{ not json");
        Assert.Null(Cache().Read("abcdef0123"));
        Assert.Single(_warns);
    }

    [Fact]
    public void PruneKeepsTheNewest()
    {
        LexiconDiskCache cache = Cache(max: 2);
        for (int i = 0; i < 4; i++)
        {
            cache.Write($"key{i}000000", Set("s" + i));
            File.SetLastWriteTimeUtc(Path.Combine(_dir, $"key{i}000000.json"), DateTime.UtcNow.AddMinutes(i - 10));
        }
        cache.Write("key9000000", Set("last"));
        var left = Directory.GetFiles(_dir).Select(Path.GetFileNameWithoutExtension).OrderBy(n => n).ToList();
        Assert.Equal(new[] { "key3000000", "key9000000" }, left);
    }

    /// <summary>A prune failure is reported as a prune failure, not as a failed write.</summary>
    [Fact]
    public void APruneFailureIsNotAWriteFailure()
    {
        LexiconDiskCache cache = Cache(max: 1);
        cache.Write("old0000000", Set("old"));
        string old = Path.Combine(_dir, "old0000000.json");
        File.SetLastWriteTimeUtc(old, DateTime.UtcNow.AddHours(-1));
        File.SetAttributes(old, FileAttributes.ReadOnly);   // Delete of a read-only file throws
        cache.Write("new0000000", Set("new"));
        Assert.NotNull(cache.Read("new0000000"));
        Assert.DoesNotContain(_warns.Concat(_traces), m => m.Contains("could not write", StringComparison.Ordinal));
        Assert.Contains(_warns.Concat(_traces), m => m.Contains("prune", StringComparison.OrdinalIgnoreCase));
    }
}
