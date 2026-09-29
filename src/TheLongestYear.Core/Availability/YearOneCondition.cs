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
    private const string SpecialOrderRuleKey = "PLAYER_SPECIAL_ORDER_RULE_ACTIVE";
    private const string QiOrderType = "Qi";
    private const int RuleNameToken = 2;

    /// <param name="closedRules">Special-order rules no loop can activate (see
    /// <see cref="RulesOnlyQiGrants"/>). A top-level <c>PLAYER_SPECIAL_ORDER_RULE_ACTIVE</c> clause
    /// naming one closes the row: the Default artifact spot's Qi Bean row made Qi Beans look like a
    /// year-1 drop (2026-09-29). Null judges YEAR clauses only.</param>
    public static bool Allows(string? condition, IReadOnlySet<string>? closedRules = null)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        foreach (string clause in TopLevelClauses(condition))
        {
            string trimmed = clause.Trim();
            if (closedRules != null && NamesClosedRule(trimmed, closedRules)) return false;
            if (!YearClauseHolds(trimmed, out bool holds)) continue;
            if (!holds) return false;
        }
        return true;
    }

    /// <summary>Rules that only Mr. Qi's special orders grant. His board is in the Walnut Room on
    /// Ginger Island, which a loop never reaches, so none of these can be active. Read from
    /// Data/SpecialOrders so a mod's orders count the same way.</summary>
    public static IReadOnlySet<string> RulesOnlyQiGrants(IEnumerable<RawSpecialOrder> orders)
    {
        var qi = new HashSet<string>(StringComparer.Ordinal);
        var open = new HashSet<string>(StringComparer.Ordinal);
        foreach (RawSpecialOrder order in orders ?? Array.Empty<RawSpecialOrder>())
        {
            bool isQi = string.Equals(order.OrderType, QiOrderType, StringComparison.OrdinalIgnoreCase);
            foreach (string rule in order.Rules ?? Array.Empty<string>())
                (isQi ? qi : open).Add(rule);
        }
        qi.ExceptWith(open);
        return qi;
    }

    /// <summary>True for an un-negated rule clause naming a closed rule. A negated clause asks
    /// for the rule to be OFF, which always holds in a loop, so it is left alone.</summary>
    private static bool NamesClosedRule(string clause, IReadOnlySet<string> closedRules)
    {
        if (clause.StartsWith("!", StringComparison.Ordinal)) return false;
        string[] tokens = clause.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length > RuleNameToken
               && tokens[0].Equals(SpecialOrderRuleKey, StringComparison.OrdinalIgnoreCase)
               && closedRules.Contains(tokens[RuleNameToken]);
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

/// <summary>One Data/SpecialOrders entry: its OrderType ("Qi" or blank for the town board) and
/// the special rules it switches on.</summary>
public sealed record RawSpecialOrder(string Id, string? OrderType, IReadOnlyList<string> Rules);
