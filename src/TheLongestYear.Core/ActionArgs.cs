using System;
using System.Collections.Generic;
using System.Text;

namespace TheLongestYear.Core;

/// <summary>Splits an action string the way vanilla's <c>ArgUtility.SplitBySpaceQuoteAware</c> does
/// before running it (TriggerActionManager.TryRunAction): spaces separate arguments, a quoted span
/// keeps its spaces and loses its quotes (so <c>""</c> is one empty argument), <c>\"</c> is a literal
/// quote, and runs of spaces outside quotes yield no empty arguments.</summary>
public static class ActionArgs
{
    private const char Delimiter = ' ';
    private const char Quote = '"';
    private const char Escape = '\\';

    public static string[] SplitBySpaceQuoteAware(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return Array.Empty<string>();
        if (input.IndexOf(Quote) < 0)
            return input.Split(Delimiter, StringSplitOptions.RemoveEmptyEntries);

        var args = new List<string>();
        var current = new StringBuilder();
        bool started = false;
        bool quoted = false;
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (c == Escape && i + 1 < input.Length && input[i + 1] == Quote)
            {
                current.Append(Quote);
                started = true;
                i++;
            }
            else if (c == Quote)
            {
                quoted = !quoted;
                started = true;
            }
            else if (c == Delimiter && !quoted)
            {
                if (started)
                    args.Add(current.ToString());
                current.Clear();
                started = false;
            }
            else
            {
                current.Append(c);
                started = true;
            }
        }
        if (started)
            args.Add(current.ToString());
        return args.ToArray();
    }
}
