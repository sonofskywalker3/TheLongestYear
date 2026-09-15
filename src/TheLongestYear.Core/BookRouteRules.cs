using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Which books the Book bundle may draw at a difficulty step. Every book in the pool has
/// a year-1 route, but two of them are slow: The Alleyway Buffet needs a gold axe and a gold
/// pickaxe, Mapping Cave Systems needs 1,000 monster kills. Easy leaves both out; every other
/// step keeps them and lets their week pin the season gate (Jeff, 2026-09-15).</summary>
public static class BookRouteRules
{
    /// <summary>Book ids to keep out of the Book bundle at this step. Empty above Easy.</summary>
    public static IReadOnlySet<string> BannedFor(DifficultyStep step)
        => step == DifficultyStep.Easy
            ? AvailabilityWeeks.LateBookIds
            : new HashSet<string>(StringComparer.Ordinal);
}
