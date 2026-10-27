using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

/// <summary>A fingerprint of every answer a model gives: SHA-256 over one line per source,
/// <c>id|kind|reliability|detail|lands|undelayed|conditions|setup|inputs</c>, ordinally sorted, so any
/// change to a landing day, a source's tags, its setup steps or input groups (which the fairness rule
/// and the mine depth read) or the set of sources changes the hash.</summary>
internal static class ObtainabilityGolden
{
    public static string Hash(ObtainabilityModel model)
    {
        var lines = new List<string>();
        foreach (string id in model.ItemIds)
            foreach (ObtainSource s in model.Sources(id))
                lines.Add(Line(id, s));
        lines.Sort(StringComparer.Ordinal);
        byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>The same fingerprint over a snapshot's id-to-sources map (an answer file's <c>Obtain</c>).</summary>
    public static string Hash(IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> sources)
    {
        var lines = new List<string>();
        foreach (var (id, list) in sources)
            foreach (ObtainSource s in list)
                lines.Add(Line(id, s));
        lines.Sort(StringComparer.Ordinal);
        byte[] bytes = Encoding.UTF8.GetBytes(string.Join("\n", lines));
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    private static string Line(string id, ObtainSource s)
        => $"{id}|{s.Kind}|{s.Reliability}|{s.Detail}|{Days(s.Lands)}|{Days(s.Undelayed)}|{Conditions(s.Conditions)}"
           + $"|setup={string.Join(",", s.Setup.Select(step => $"{step.Name}:{step.Days}"))}"
           + $"|inputs={string.Join(";", s.Inputs.Select(group => string.Join("+", group)))}";

    /// <summary>The 112 landing days as hex bytes (00 = never).</summary>
    private static string Days(DayTable table)
    {
        var bytes = new byte[DayTable.Days];
        for (int d = 1; d <= DayTable.Days; d++) bytes[d - 1] = (byte)(table.Lands(d) ?? 0);
        return Convert.ToHexString(bytes);
    }

    private static string Conditions(ObtainConditions c)
        => string.Join(",",
            $"skill={c.Skill}:{c.SkillLevel}",
            $"requires={string.Join("+", c.Requires)}",
            $"rain={c.RainOnly}", $"few={c.FewDays}", $"y2={c.YearTwo}", $"island={c.GingerIsland}",
            $"catch={c.CatchLimit}", $"unresolved={c.Unresolved}", $"owned={c.OwnedOnly}");
}
