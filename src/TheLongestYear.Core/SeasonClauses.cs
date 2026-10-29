using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Clause-level reading of a game state query's season clauses, shared by every reader
/// that asks "which seasons does this Condition allow". A Condition is a comma-separated AND of
/// clauses (GameStateQuery.cs), and a leading '!' negates ONE clause, never the whole string.</summary>
public static class SeasonClauses
{
    private const string SeasonQuery = "SEASON";
    private const string LocationSeasonQuery = "LOCATION_SEASON";
    private const char NegationPrefix = '!';
    private const char Quote = '"';
    private const char ClauseSeparator = ',';

    /// <summary>Splits on commas outside double quotes (ANY "a, b" "c" keeps its arguments together).</summary>
    public static IEnumerable<string> Split(string condition)
    {
        int start = 0;
        bool quoted = false;
        for (int i = 0; i < condition.Length; i++)
        {
            if (condition[i] == Quote) quoted = !quoted;
            else if (condition[i] == ClauseSeparator && !quoted)
            {
                yield return condition.Substring(start, i - start);
                start = i + 1;
            }
        }
        yield return condition.Substring(start);
    }

    /// <summary>The seasons a <c>SEASON x [y...]</c> or <c>LOCATION_SEASON &lt;loc&gt; x [y...]</c>
    /// clause names, or null for any other clause. <paramref name="negated"/> reports a leading
    /// '!'. Only season NAMES count (IsDefined: Enum.TryParse accepts any integer).</summary>
    public static HashSet<Season>? SeasonsNamedBy(string clause, out bool negated)
    {
        string body = clause.Trim();
        negated = body.StartsWith(NegationPrefix);
        if (negated) body = body.Substring(1).TrimStart();
        string[] words = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return null;

        int firstSeason;
        if (string.Equals(words[0], LocationSeasonQuery, StringComparison.OrdinalIgnoreCase)) firstSeason = 2;
        else if (string.Equals(words[0], SeasonQuery, StringComparison.OrdinalIgnoreCase)) firstSeason = 1;
        else return null;

        var seasons = new HashSet<Season>();
        for (int i = firstSeason; i < words.Length; i++)
            if (TryParseSeason(words[i], out Season s))
                seasons.Add(s);
        return seasons;
    }

    /// <summary>True when <paramref name="token"/> is a season NAME. Enum.TryParse alone accepts any
    /// integer ("TIME 0600 1800" would read as two nonsense seasons).</summary>
    public static bool TryParseSeason(string token, out Season season)
        => Enum.TryParse(token, ignoreCase: true, out season) && Enum.IsDefined(typeof(Season), season);
}
