using System;
using System.Collections.Generic;
using System.Text;

namespace TheLongestYear.Core;

/// <summary>What a Condition says about the calendar season.</summary>
public enum SeasonVerdict
{
    /// <summary>No clause the reader understands names a season: any season, as far as we know.</summary>
    Unknown,
    /// <summary>The row can only fire in <see cref="SeasonReading.Seasons"/>.</summary>
    Seasons,
    /// <summary>The season clauses rule out every season: the row never fires.</summary>
    Impossible,
}

/// <summary>A Condition's season verdict, with the allowed seasons (calendar order) when it is
/// <see cref="SeasonVerdict.Seasons"/>, and empty otherwise.</summary>
public readonly record struct SeasonReading(SeasonVerdict Verdict, IReadOnlyList<Season> Seasons)
{
    public static readonly SeasonReading Unknown = new(SeasonVerdict.Unknown, Array.Empty<Season>());
    public static readonly SeasonReading Impossible = new(SeasonVerdict.Impossible, Array.Empty<Season>());
}

/// <summary>Reads the season clauses of a game state query the way the game evaluates them
/// (GameStateQuery.cs): the string is a comma-separated AND split by ArgUtility.SplitQuoteAware,
/// each clause is split on spaces with quotes removed, and a leading '!' negates that one clause.
///
/// Only the real season queries are read; every other query, ANY included, says nothing about
/// seasons and is skipped:
/// <list type="bullet">
/// <item><c>SEASON a [b...]</c>: arguments parse as the Season enum (a name or 0-3), as
/// ArgUtility.TryGetEnum does. Negated, its seasons are removed.</item>
/// <item><c>SEASON_DAY season day [season day...]</c>: true on one day of the named seasons, so
/// positive it narrows to those seasons. Negated it only removes single days, so it is skipped.</item>
/// <item><c>LOCATION_SEASON &lt;loc&gt; a [b...]</c>, only for <c>Here</c> / <c>Target</c> (the
/// row's own location; another map's season can differ from the calendar, e.g. IslandSouth).
/// The game compares the arguments to the season KEY as strings, so only names count:
/// "LOCATION_SEASON Here 1" never matches.</item>
/// </list>
/// A clause the game would reject (a SEASON argument that is not a season, an odd SEASON_DAY
/// pair) and a positive LOCATION_SEASON naming no season are skipped rather than read as
/// impossible: we do not drop a row on a guess.</summary>
public static class SeasonClauses
{
    private const string SeasonQuery = "SEASON";
    private const string SeasonDayQuery = "SEASON_DAY";
    private const string LocationSeasonQuery = "LOCATION_SEASON";
    private const string HereLocation = "Here";
    private const string TargetLocation = "Target";
    private const char NegationPrefix = '!';
    private const char Quote = '"';
    private const char Escape = '\\';
    private const char ClauseSeparator = ',';
    private const char ArgSeparator = ' ';

    private static readonly Season[] AllSeasons = { Season.Spring, Season.Summer, Season.Fall, Season.Winter };

    /// <summary>Splits a Condition into its top-level clauses on commas outside double quotes,
    /// keeping quotes and escapes (GameStateQuery.SplitRaw). A backslash-escaped quote (\") does
    /// not open or close a quoted section, so a quoted ANY argument never leaks into the outer AND.</summary>
    public static IEnumerable<string> Split(string condition)
    {
        int start = 0;
        bool quoted = false;
        for (int i = 0; i < condition.Length; i++)
        {
            char c = condition[i];
            if (c == Escape && i + 1 < condition.Length && condition[i + 1] == Quote) { i++; continue; }
            if (c == Quote) quoted = !quoted;
            else if (c == ClauseSeparator && !quoted)
            {
                yield return condition.Substring(start, i - start);
                start = i + 1;
            }
        }
        yield return condition.Substring(start);
    }

    /// <summary>Splits one clause into its arguments on spaces outside quotes, removing the quotes
    /// and turning \" into a literal quote (ArgUtility.SplitBySpaceQuoteAware).</summary>
    public static IReadOnlyList<string> Args(string clause)
    {
        var args = new List<string>();
        var current = new StringBuilder();
        bool quoted = false, inArg = false;
        for (int i = 0; i < clause.Length; i++)
        {
            char c = clause[i];
            if (c == Escape && i + 1 < clause.Length && clause[i + 1] == Quote) { current.Append(Quote); inArg = true; i++; continue; }
            if (c == Quote) { quoted = !quoted; inArg = true; continue; }
            if (c == ArgSeparator && !quoted)
            {
                if (inArg && current.Length > 0) args.Add(current.ToString());
                current.Clear();
                inArg = false;
                continue;
            }
            current.Append(c);
            inArg = true;
        }
        if (current.Length > 0) args.Add(current.ToString());
        return args;
    }

    /// <summary>The season verdict of a whole Condition: positive season clauses intersect,
    /// negated SEASON / LOCATION_SEASON clauses subtract, everything else is skipped.</summary>
    public static SeasonReading Read(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return SeasonReading.Unknown;
        HashSet<Season>? allowed = null;
        foreach (string clause in Split(condition))
        {
            IReadOnlyList<string> args = Args(clause.Trim());
            if (args.Count == 0) continue;
            string key = args[0];
            bool negated = key.Length > 1 && key[0] == NegationPrefix;
            if (negated) key = key.Substring(1);

            HashSet<Season>? named = NamedSeasons(key, args, negated);
            if (named == null) continue;
            if (negated)
            {
                allowed ??= new HashSet<Season>(AllSeasons);
                allowed.ExceptWith(named);
            }
            else if (allowed == null) allowed = named;
            else allowed.IntersectWith(named);
        }
        if (allowed == null) return SeasonReading.Unknown;
        if (allowed.Count == 0) return SeasonReading.Impossible;
        var ordered = new List<Season>();
        foreach (Season s in AllSeasons)
            if (allowed.Contains(s)) ordered.Add(s);
        return new SeasonReading(SeasonVerdict.Seasons, ordered);
    }

    /// <summary>The seasons one season query names, or null when the clause is not one we read
    /// (see the class comment).</summary>
    private static HashSet<Season>? NamedSeasons(string key, IReadOnlyList<string> args, bool negated)
    {
        var seasons = new HashSet<Season>();
        if (string.Equals(key, SeasonQuery, StringComparison.OrdinalIgnoreCase))
        {
            for (int i = 1; i < args.Count; i++)
            {
                if (!TryParseSeasonEnum(args[i], out Season s)) return null;   // the game errors
                seasons.Add(s);
            }
            return seasons.Count > 0 ? seasons : null;
        }
        if (string.Equals(key, SeasonDayQuery, StringComparison.OrdinalIgnoreCase))
        {
            if (negated || args.Count < 3 || args.Count % 2 == 0) return null;
            for (int i = 1; i + 1 < args.Count; i += 2)
            {
                if (!TryParseSeasonEnum(args[i], out Season s) || !int.TryParse(args[i + 1], out _)) return null;
                seasons.Add(s);
            }
            return seasons;
        }
        if (string.Equals(key, LocationSeasonQuery, StringComparison.OrdinalIgnoreCase))
        {
            if (args.Count < 3) return null;
            if (!string.Equals(args[1], HereLocation, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(args[1], TargetLocation, StringComparison.OrdinalIgnoreCase))
                return null;
            for (int i = 2; i < args.Count; i++)
                if (TryParseSeasonName(args[i], out Season s))
                    seasons.Add(s);
            if (seasons.Count == 0) return null;   // never matches when positive, removes nothing when negated
            return seasons;
        }
        return null;
    }

    /// <summary>A SEASON argument as ArgUtility.TryGetEnum reads it: a name or a defined number
    /// (0-3). Enum.TryParse alone would also accept any other integer.</summary>
    private static bool TryParseSeasonEnum(string token, out Season season)
        => Enum.TryParse(token, ignoreCase: true, out season) && Enum.IsDefined(typeof(Season), season);

    /// <summary>True only for a season name (spring, summer, fall, winter, any case): the strings
    /// LOCATION_SEASON compares against.</summary>
    private static bool TryParseSeasonName(string token, out Season season)
    {
        foreach (Season s in AllSeasons)
            if (string.Equals(token, s.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                season = s;
                return true;
            }
        season = default;
        return false;
    }
}
