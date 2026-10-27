using System;
using System.Globalization;
using System.Text;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>A <see cref="DayTable"/> as text: comma-separated runs over the start days 1..112 of two
/// kinds. A plateau "=&lt;landing&gt;x&lt;count&gt;" (landing 0 = never) is a stretch of equal landings; a
/// shift "+&lt;k&gt;x&lt;count&gt;" is a stretch where landing minus start is the constant k (k = 0 is
/// "available every day"). Greedy left to right, the longer candidate wins and a run of one day is a
/// plateau: "=35x34,+0x78".</summary>
public static class DayTableRuns
{
    private const char RunSeparator = ',';
    private const char Count = 'x';
    private const char PlateauMark = '=';
    private const char ShiftMark = '+';

    public static string Write(DayTable t)
    {
        byte[] lands = t.ToLandings();
        var sb = new StringBuilder();
        int i = 0;
        while (i < lands.Length)
        {
            int plateau = 1;
            while (i + plateau < lands.Length && lands[i + plateau] == lands[i]) plateau++;
            int shift = 0;
            int k = lands[i] - (i + 1);
            if (lands[i] != 0 && k >= 0)
            {
                shift = 1;
                while (i + shift < lands.Length && lands[i + shift] != 0 && lands[i + shift] - (i + shift + 1) == k) shift++;
            }
            if (sb.Length > 0) sb.Append(RunSeparator);
            if (shift > plateau)
            {
                sb.Append(ShiftMark).Append(k).Append(Count).Append(shift);
                i += shift;
            }
            else
            {
                sb.Append(PlateauMark).Append(lands[i]).Append(Count).Append(plateau);
                i += plateau;
            }
        }
        return sb.ToString();
    }

    /// <summary>Throws <see cref="FormatException"/> unless the runs sum to exactly 112 start days, every
    /// landing is 0 (plateau only) or a day in 1..112, and none lands before its start day.</summary>
    public static DayTable Read(string runs)
    {
        if (string.IsNullOrWhiteSpace(runs)) throw new FormatException("Empty day table.");
        var lands = new byte[DayTable.Days];
        int next = 0;   // zero-based index of the next start day to fill
        foreach (string run in runs.Split(RunSeparator))
        {
            string[] halves = run.Length > 1 ? run.Substring(1).Split(Count) : Array.Empty<string>();
            if (halves.Length != 2 || !TryInt(halves[0], out int value) || !TryInt(halves[1], out int count) || count < 1)
                throw new FormatException($"Malformed day table run '{run}'.");
            if (next + count > DayTable.Days)
                throw new FormatException($"Day table run '{run}' runs past day {DayTable.Days}.");
            if (run[0] != PlateauMark && run[0] != ShiftMark)
                throw new FormatException($"Malformed day table run '{run}'.");
            bool shift = run[0] == ShiftMark;
            for (int j = 0; j < count; j++)
            {
                int start = next + j + 1;
                int landing = shift ? start + value : value;
                if (landing > DayTable.Days || (landing != 0 && landing < start) || (shift && landing == 0))
                    throw new FormatException($"Day table run '{run}' lands outside its start days.");
                lands[next + j] = (byte)landing;
            }
            next += count;
        }
        if (next != DayTable.Days) throw new FormatException($"Day table covers {next} days, not {DayTable.Days}.");
        return DayTable.FromLandings(lands);
    }

    private static bool TryInt(string text, out int value)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
