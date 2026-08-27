using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One power book the shrine can keep across a reset.</summary>
public sealed record BookKeep(string StatKey, string UpgradeId, long Cost, string? PrerequisiteId);

/// <summary>
/// The single source of truth for the Keep &lt;book&gt; rows (spec 2026-08-27 keep-power-books).
/// Feeds both the catalog generator (what is sold) and RunBaselineBuilder (what is re-granted),
/// so the two can never drift. Stat keys are vanilla StatKeys.Book_* (decompile StatKeys.cs);
/// each is a binary flag set by Object.readBook. Prices are three bands by the power's value
/// over a year, not the book's gold price: Convenience 150, Yield 350, Power 500..750.
/// </summary>
public static class BookKeepTable
{
    public const string StatKeyPrefix = "Book_";
    public const string UpgradeIdPrefix = "keep_book_";
    public const string ReachMetric = "book";

    private const long Convenience = 150;
    private const long Yield = 350;

    private static readonly (string StatKey, long Cost, string? PrereqStatKey)[] Rows =
    {
        // Convenience.
        ("Book_PriceCatalogue",  Convenience, null),
        ("Book_AnimalCatalogue", Convenience, null),
        ("Book_Trash",           Convenience, null),
        ("Book_Grass",           Convenience, null),
        ("Book_Horse",           Convenience, null),
        // Yield.
        ("Book_Woodcutting", Yield, null),
        ("Book_WildSeeds",   Yield, null),
        ("Book_Roe",         Yield, null),
        ("Book_Crabbing",    Yield, null),
        ("Book_Diamonds",    Yield, null),
        ("Book_Mystery",     Yield, null),
        ("Book_Artifact",    Yield, null),
        ("Book_Void",        Yield, null),
        ("Book_Marlon",      Yield, null),
        ("Book_Friendship",  Yield, null),
        // Power.
        ("Book_Bombs",   500, null),
        ("Book_Defense", 600, null),
        ("Book_Speed",   750, null),
        ("Book_Speed2",  750, "Book_Speed"),   // vanilla sells pt. 2 only after pt. 1
    };

    public static IReadOnlyList<BookKeep> Entries { get; } = Rows
        .Select(r => new BookKeep(r.StatKey, UpgradeIdFor(r.StatKey), r.Cost,
            r.PrereqStatKey == null ? null : UpgradeIdFor(r.PrereqStatKey)))
        .ToList();

    /// <summary>keep_book_&lt;stat key after "Book_", lower-cased&gt;.</summary>
    public static string UpgradeIdFor(string statKey)
    {
        if (!statKey.StartsWith(StatKeyPrefix, StringComparison.Ordinal))
            throw new ArgumentException($"Not a Book_* stat key: {statKey}", nameof(statKey));
        return UpgradeIdPrefix + statKey.Substring(StatKeyPrefix.Length).ToLowerInvariant();
    }

    public static string ReachFor(string statKey) => $"{ReachMetric}:{statKey}";
}
