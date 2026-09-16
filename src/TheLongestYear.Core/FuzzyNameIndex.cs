using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// The game's <c>Utility.fuzzyItemSearch</c> match rule over a name table that is built and
/// formatted ONCE. The game's own call rebuilds the whole item-name table and re-formats every
/// name on each lookup (about 4 ms); the loop reset's bundle pool build made 487 of them, which
/// was the two-second freeze on closing the post-loop shrine (log, 2026-09-16).
///
/// Match rule copied from the decompile (<c>Utility.fuzzySearch</c> / <c>fuzzyCompare</c>):
/// trimmed equality scores 0, equality after formatting 1, formatted prefix 2, formatted
/// substring 3; the lowest score wins and a tie keeps the earliest name. Answers are memoized per
/// query, so the index must be rebuilt whenever the item data it was built from can change.
/// </summary>
public sealed class FuzzyNameIndex
{
    private const int ExactScore = 0;
    private const int FormattedScore = 1;
    private const int PrefixScore = 2;
    private const int ContainsScore = 3;

    private readonly List<(string Trimmed, string Formatted, string Id)> _names = new();
    private readonly Dictionary<string, string?> _answers = new(System.StringComparer.Ordinal);

    /// <param name="namesToIds">Name to qualified item id, in the game's table order. A repeated
    /// name keeps its first id, as the game's table does.</param>
    public FuzzyNameIndex(IEnumerable<KeyValuePair<string, string>> namesToIds)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in namesToIds)
        {
            if (entry.Key == null || !seen.Add(entry.Key))
                continue;
            _names.Add((entry.Key.Trim(), Format(entry.Key), entry.Value));
        }
    }

    /// <summary>The qualified id of the best match for <paramref name="query"/>, or null.</summary>
    public string? Find(string query)
    {
        if (_answers.TryGetValue(query, out string? known))
            return known;

        string trimmed = query.Trim();
        string formatted = Format(query);
        int? bestScore = null;
        string? bestId = null;
        foreach ((string Trimmed, string Formatted, string Id) name in _names)
        {
            int? score = Score(trimmed, formatted, name.Trimmed, name.Formatted);
            if (score.HasValue && (!bestScore.HasValue || score < bestScore))
            {
                bestScore = score;
                bestId = name.Id;
                if (score == ExactScore)
                    break; // nothing scores lower, and a tie keeps the earlier name
            }
        }

        _answers[query] = bestId;
        return bestId;
    }

    private static int? Score(string queryTrimmed, string queryFormatted, string termTrimmed, string termFormatted)
    {
        if (queryTrimmed == termTrimmed)
            return ExactScore;
        if (queryFormatted == termFormatted)
            return FormattedScore;
        if (termFormatted.StartsWith(queryFormatted, System.StringComparison.CurrentCulture))
            return PrefixScore;
        if (termFormatted.Contains(queryFormatted))
            return ContainsScore;
        return null;
    }

    /// <summary><c>Utility.fuzzyCompare</c>'s local FormatForFuzzySearch, verbatim.</summary>
    private static string Format(string value)
    {
        string minimal = value.Trim().ToLowerInvariant().Replace(" ", "");
        string formatted = minimal.Replace("(", "").Replace(")", "").Replace("'", "")
            .Replace(".", "")
            .Replace("!", "")
            .Replace("?", "")
            .Replace("-", "");
        return formatted.Length != 0 ? formatted : minimal;
    }
}
