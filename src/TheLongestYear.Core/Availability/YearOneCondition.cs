using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Whether a game state query can hold in year 1, judged ONLY by its top-level
/// <c>YEAR</c> clauses. A loop never leaves year 1, so a shop line or spawn row gated to year 2 is
/// not a source this run (Nexus post Thrippa, 2026-09-25: Cornucopia sells its rare seeds behind
/// "SEASON spring, YEAR 2"). Every other clause is left alone and counts as possible: this rule
/// only ever removes a route whose year makes it impossible, it never invents one.
///
/// Game syntax (GameStateQuery): clauses are comma-separated and all must hold; a leading "!"
/// negates; quoted arguments group, so the clauses inside an <c>ANY "..." "..."</c> are an OR and
/// are not read here. <c>YEAR min [max]</c> holds when min &lt;= year &lt;= max.</summary>
public static class YearOneCondition
{
    private const string YearKey = "YEAR";
    private const int LoopYear = 1;

    public static bool Allows(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        foreach (string clause in TopLevelClauses(condition))
        {
            if (!YearClauseHolds(clause.Trim(), out bool holds)) continue;
            if (!holds) return false;
        }
        return true;
    }

    /// <summary>False when the clause is not a YEAR clause (nothing to judge).</summary>
    private static bool YearClauseHolds(string clause, out bool holds)
    {
        holds = true;
        bool negated = clause.StartsWith("!", StringComparison.Ordinal);
        if (negated) clause = clause.Substring(1).TrimStart();
        string[] tokens = clause.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2 || !tokens[0].Equals(YearKey, StringComparison.OrdinalIgnoreCase)) return false;
        if (!int.TryParse(tokens[1], out int min)) return false;
        int? max = tokens.Length > 2 && int.TryParse(tokens[2], out int m) ? m : null;
        bool inRange = LoopYear >= min && (max == null || LoopYear <= max.Value);
        holds = negated ? !inRange : inRange;
        return true;
    }

    private static IEnumerable<string> TopLevelClauses(string condition)
    {
        int start = 0;
        bool quoted = false;
        for (int i = 0; i < condition.Length; i++)
        {
            char c = condition[i];
            if (c == '"') quoted = !quoted;
            else if (c == ',' && !quoted)
            {
                yield return condition.Substring(start, i - start);
                start = i + 1;
            }
        }
        yield return condition.Substring(start);
    }
}
