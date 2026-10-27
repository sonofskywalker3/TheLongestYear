using System;
using System.Globalization;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>A <see cref="DayTable"/> as text: comma-separated maximal runs of equal landing value over
/// the start days 1..112, each "firstStart-lastStart:landing" with 0 meaning never
/// ("1-34:35,35-112:0").</summary>
public static class DayTableRuns
{
    private const char RunSeparator = ',';
    private const char RangeSeparator = '-';
    private const char ValueSeparator = ':';

    public static string Write(DayTable t)
    {
        byte[] lands = t.ToLandings();
        var sb = new StringBuilder();
        int start = 0;
        for (int i = 1; i <= lands.Length; i++)
        {
            if (i < lands.Length && lands[i] == lands[start]) continue;
            if (sb.Length > 0) sb.Append(RunSeparator);
            sb.Append(start + 1).Append(RangeSeparator).Append(i).Append(ValueSeparator).Append(lands[start]);
            start = i;
        }
        return sb.ToString();
    }

    /// <summary>Throws <see cref="FormatException"/> unless the runs cover start days 1..112 exactly once,
    /// in order, each landing 0 or a day in 1..112.</summary>
    public static DayTable Read(string runs)
    {
        if (string.IsNullOrWhiteSpace(runs)) throw new FormatException("Empty day table.");
        var lands = new byte[DayTable.Days];
        int next = 1;
        foreach (string run in runs.Split(RunSeparator))
        {
            string[] halves = run.Split(ValueSeparator);
            string[] range = halves[0].Split(RangeSeparator);
            if (halves.Length != 2 || range.Length != 2
                || !TryInt(range[0], out int first) || !TryInt(range[1], out int last) || !TryInt(halves[1], out int landing))
                throw new FormatException($"Malformed day table run '{run}'.");
            if (first != next || last < first || last > DayTable.Days)
                throw new FormatException($"Day table run '{run}' does not continue from day {next}.");
            if (landing > DayTable.Days)
                throw new FormatException($"Day table run '{run}' lands outside 0..{DayTable.Days}.");
            for (int d = first; d <= last; d++) lands[d - 1] = (byte)landing;
            next = last + 1;
        }
        if (next != DayTable.Days + 1) throw new FormatException($"Day table stops at day {next - 1}, not {DayTable.Days}.");
        return DayTable.FromLandings(lands);
    }

    private static bool TryInt(string text, out int value)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
