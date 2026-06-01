namespace TheLongestYear.Core;

/// <summary>
/// A parsed "what did the player reach THIS run" gate on a keep upgrade. Distinct from
/// <see cref="UpgradeDefinition.MetaRequirement"/> (which checks cross-run accumulators):
/// this compares a live in-run value against a threshold. Format "metric[:key]:threshold",
/// or the flag form "scythe:golden". Parsing + comparison live here (pure, testable); the
/// live value per metric is supplied by the glue RunReachEvaluator.
/// </summary>
public sealed class RunReachRequirement
{
    public string Metric { get; }
    public string? Key { get; }
    public int Threshold { get; }

    private RunReachRequirement(string metric, string? key, int threshold)
    {
        Metric = metric;
        Key = key;
        Threshold = threshold;
    }

    /// <summary>actual reach value ≥ the required threshold.</summary>
    public bool IsMet(int actualReach) => actualReach >= Threshold;

    /// <summary>Parse a requirement string, or null if empty/malformed.</summary>
    public static RunReachRequirement? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        string[] parts = raw.Split(':');
        // Keyed-flag form for metrics whose value is a name, not a number (the evaluator
        // supplies 0/1): scythe:golden, building:Coop, building:Big Coop, ...
        if (parts.Length == 2 && parts[1].Length > 0 && (parts[0] == "scythe" || parts[0] == "building"))
            return new RunReachRequirement(parts[0], parts[1], 1);
        // 2-part numeric: metric:threshold (rod / backpack / mine / mastery).
        if (parts.Length == 2 && parts[0].Length > 0 && int.TryParse(parts[1], out int t2))
            return new RunReachRequirement(parts[0], null, t2);
        // 3-part numeric: metric:key:threshold (tool / skill).
        if (parts.Length == 3 && parts[0].Length > 0 && parts[1].Length > 0
            && int.TryParse(parts[2], out int t3))
            return new RunReachRequirement(parts[0], parts[1], t3);
        return null;
    }
}
