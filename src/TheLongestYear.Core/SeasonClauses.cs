using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Clause-level reading of a game state query's season clauses, shared by every reader
/// that asks "which seasons does this Condition allow". A Condition is a comma-separated AND of
/// clauses (GameStateQuery.cs), and a leading '!' negates ONE clause, never the whole string.</summary>
public static class SeasonClauses
{
    private const string SeasonQuery = "SEASON";
    private const string LocationSeasonQuery = "LOCATION_SEASON";
    private const string AnyQuery = "ANY";
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
    /// '!'. Arguments are read like the game's Utility.TryParseEnum: a name or 0-3.</summary>
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

    /// <summary>True when <paramref name="token"/> is a season name or one of the season numbers
    /// 0-3, as the game's Utility.TryParseEnum reads a SEASON argument. The IsDefined check only
    /// rejects numbers outside 0-3 ("TIME 0600 1800"); "1" still reads as Summer. Use
    /// <see cref="TryParseSeasonName"/> where a number is not a season argument.</summary>
    public static bool TryParseSeason(string token, out Season season)
        => Enum.TryParse(token, ignoreCase: true, out season) && Enum.IsDefined(typeof(Season), season);

    /// <summary>True only for a season NAME (all letters): the bare-token scan of a clause that is
    /// not a season query, where "PLAYER_FISHING_LEVEL Current 3" or "YEAR 2" must not read as
    /// Winter or Fall.</summary>
    public static bool TryParseSeasonName(string token, out Season season)
    {
        season = default;
        return token.Length > 0 && token.All(char.IsLetter) && TryParseSeason(token, out season);
    }

    /// <summary>True for an ANY clause (negated or not). Its quoted sub-queries are an OR and do not
    /// split on spaces or commas, so its tokens say nothing reliable about seasons.</summary>
    public static bool IsAnyQuery(string clause)
    {
        string body = clause.Trim().TrimStart(NegationPrefix).TrimStart();
        int space = body.IndexOf(' ');
        string query = space < 0 ? body : body.Substring(0, space);
        return string.Equals(query, AnyQuery, StringComparison.OrdinalIgnoreCase);
    }
}
