using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>A RunReachRequirement in the player's words for the Plan tab's Locked section
/// (spec 2026-08-29 shrine tabs + JP Boosts, section 3). Keys reach.&lt;metric&gt;; tokens key and
/// value. Unknown metric or missing key: the raw requirement, so nothing is ever hidden.</summary>
public static class ReachText
{
    private static readonly IReadOnlyDictionary<string, string> ToolNames = new Dictionary<string, string>
    {
        ["hoe"] = "Hoe", ["pickaxe"] = "Pickaxe", ["axe"] = "Axe", ["watering_can"] = "Watering Can",
    };
    private static readonly string[] ToolTiers = { "", "Copper", "Steel", "Gold", "Iridium" };

    public static string Describe(string? requirement)
    {
        if (string.IsNullOrWhiteSpace(requirement)) return "";
        RunReachRequirement? r = RunReachRequirement.Parse(requirement);
        if (r == null) return requirement;
        var tokens = new Dictionary<string, string> { ["key"] = r.Key ?? "", ["value"] = r.Threshold.ToString() };
        switch (r.Metric)
        {
            case "skill":
                tokens["key"] = Capitalise(r.Key);
                break;
            case "tool":
                tokens["key"] = (r.Threshold >= 1 && r.Threshold < ToolTiers.Length ? ToolTiers[r.Threshold] + " " : "")
                    + (ToolNames.TryGetValue(r.Key ?? "", out string? n) ? n : r.Key ?? "");
                break;
        }
        string key = "reach." + r.Metric;
        string text = Strings.Get(key, tokens);
        return text == key ? requirement : text;
    }

    private static string Capitalise(string? s) => string.IsNullOrEmpty(s) ? "" : char.ToUpperInvariant(s[0]) + s.Substring(1);
}
