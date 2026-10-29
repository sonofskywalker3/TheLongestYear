using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The three past-season boosts (Jeff, 2026-09-24): for a week, a season that has already
/// passed this loop adds its fish and forage to today's. Past seasons only, so the Spring boost is
/// sold from Summer on, the Summer boost from Fall on, the Fall boost in Winter. They replace the
/// last-season deadline cap (0.18.45): a player who missed a Summer-only Pufferfish can buy the
/// Summer boost and still catch it before the Winter deadline.</summary>
public static class PastSeasonBoosts
{
    private static readonly IReadOnlyDictionary<BoostId, Season> SeasonByBoost = new Dictionary<BoostId, Season>
    {
        [BoostId.SpringReturns] = Season.Spring,
        [BoostId.SummerReturns] = Season.Summer,
        [BoostId.FallReturns] = Season.Fall,
    };

    /// <summary>The season a boost brings back, or null when it is not a past-season boost.</summary>
    public static Season? SeasonOf(BoostId id) => SeasonByBoost.TryGetValue(id, out Season s) ? s : null;

    /// <summary>A past-season boost is on sale only once its season has passed this loop.</summary>
    public static bool Available(BoostId id, Season today) => SeasonOf(id) is Season s && s < today;

    /// <summary>The seasons brought back today by the run's active past-season boosts.</summary>
    public static IReadOnlySet<Season> Active(RunState run, int dayOfYear)
    {
        var active = new HashSet<Season>();
        foreach (KeyValuePair<BoostId, Season> pair in SeasonByBoost)
            if (BoostState.IsActive(run, pair.Key, dayOfYear))
                active.Add(pair.Value);
        return active;
    }
}

/// <summary>Which Data/Locations fish and forage rows a past-season boost copies, and how. A row
/// tied to a boosted season (its Season field, or a LOCATION_SEASON / SEASON condition naming it)
/// gets a copy with the season restriction taken off and every other rule kept (spot, time,
/// weather, catch limit). Rows that already spawn today are never copied, so today's pool is added
/// to, never doubled or replaced.</summary>
public static class PastSeasonSpawn
{
    /// <summary>Id prefix of every copied row. The mod's own Data/Locations readers skip rows with
    /// this prefix, so the availability model never mistakes a copy for a year-round spawn.</summary>
    public const string IdPrefix = "TLY_PastSeason_";

    private const string LocationSeasonQuery = "LOCATION_SEASON";
    private const string SeasonQuery = "SEASON";
    private const string NegationPrefix = "!";

    /// <summary>True for a row this feature added.</summary>
    public static bool IsCopy(string? id) => id != null && id.StartsWith(IdPrefix, StringComparison.Ordinal);

    /// <summary>The copy's condition when the row should be copied (null means "no condition"), or
    /// <c>Copy = false</c> when it should not. <paramref name="seasonField"/> is the row's Season
    /// field (null when unset).</summary>
    public static (bool Copy, string? Condition) For(
        Season? seasonField, string? condition, Season today, IReadOnlySet<Season> boosted)
    {
        if (boosted == null || boosted.Count == 0)
            return (false, null);

        if (seasonField.HasValue)
            return seasonField.Value != today && boosted.Contains(seasonField.Value)
                ? (true, condition)
                : (false, null);

        if (string.IsNullOrWhiteSpace(condition))
            return (false, null);

        List<string> clauses = condition.Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToList();
        int seasonClause = -1;
        HashSet<Season>? listed = null;
        for (int i = 0; i < clauses.Count; i++)
        {
            HashSet<Season>? seasons = SeasonsNamedBy(clauses[i], out bool negated);
            if (seasons == null) continue;
            if (negated || seasonClause >= 0) return (false, null);   // too unusual to copy safely
            seasonClause = i;
            listed = seasons;
        }
        if (listed == null || listed.Contains(today) || !listed.Overlaps(boosted))
            return (false, null);

        clauses.RemoveAt(seasonClause);
        return (true, clauses.Count == 0 ? null : string.Join(", ", clauses));
    }

    /// <summary>The seasons a LOCATION_SEASON or SEASON clause names, or null for any other clause.</summary>
    private static HashSet<Season>? SeasonsNamedBy(string clause, out bool negated)
    {
        string[] words = clause.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        string query = words.Length > 0 ? words[0] : "";
        negated = query.StartsWith(NegationPrefix, StringComparison.Ordinal);
        if (negated) query = query.Substring(NegationPrefix.Length);

        int firstSeason;
        if (string.Equals(query, LocationSeasonQuery, StringComparison.OrdinalIgnoreCase)) firstSeason = 2;
        else if (string.Equals(query, SeasonQuery, StringComparison.OrdinalIgnoreCase)) firstSeason = 1;
        else return null;

        var seasons = new HashSet<Season>();
        for (int i = firstSeason; i < words.Length; i++)
            if (Enum.TryParse(words[i], ignoreCase: true, out Season s))
                seasons.Add(s);
        return seasons;
    }
}
