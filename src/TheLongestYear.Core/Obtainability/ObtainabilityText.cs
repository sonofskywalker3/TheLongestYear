using System;
using System.Collections.Generic;
using System.Text;

namespace TheLongestYear.Core.Obtainability;

/// <summary>The one-item readout for tly_obtain, and the source line the comparison report reuses.</summary>
public static class ObtainabilityText
{
    public static string Describe(string itemId, ObtainabilityModel model, Func<string, string?> nameOf)
    {
        string id = BundleParsing.NormalizeItemId(itemId);
        string name = nameOf(id) ?? "?";
        IReadOnlyList<ObtainSource> sources = model.Sources(id);
        if (sources.Count == 0) return $"{id} {name}: no source in the obtainability model.";

        var sb = new StringBuilder();
        sb.Append($"{id} {name}: {sources.Count} source(s); dependable weeks {model.Weeks(id, ObtainFilter.DependableOnly)}, ")
          .Append($"any weeks {model.Weeks(id, ObtainFilter.Any)}");
        foreach (ObtainSource s in sources)
            sb.AppendLine().Append("  - ").Append(SourceLine(s));
        return sb.ToString();
    }

    public static string SourceLine(ObtainSource s)
    {
        var sb = new StringBuilder($"{s.Kind}, {s.Reliability}, weeks {s.Weeks}");
        ObtainConditions c = s.Conditions;
        if (c.Skill != null) sb.Append($", {c.Skill} {c.SkillLevel}");
        if (c.CatchLimit > 0) sb.Append($", catch limit {c.CatchLimit}");
        if (c.RainOnly) sb.Append(", rain only");
        if (c.FewDays) sb.Append(", few days");
        if (c.YearTwo) sb.Append(", year 2");
        if (c.GingerIsland) sb.Append(", Ginger Island");
        if (c.Unresolved) sb.Append(", UNRESOLVED");
        if (c.Requires.Count > 0) sb.Append($", needs {string.Join("; ", c.Requires)}");
        return sb.Append($" | {s.Detail}").ToString();
    }
}
