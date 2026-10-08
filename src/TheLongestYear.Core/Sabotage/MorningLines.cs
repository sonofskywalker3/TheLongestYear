using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>The darkness's morning messages (designer, 2026-10-08): each shows in vanilla's corner
/// message box, the one the spreading weeds use, one after another. This class holds the parts that
/// need no game: the thief's list of what he took, and how long a box stays up.</summary>
public static class MorningLines
{
    /// <summary>The most item kinds the thief's list names; the rest are counted as "other things".</summary>
    public const int MaxStolenKinds = 5;

    /// <summary>Vanilla's time for a corner box (HUDMessage.ForCornerTextbox): 5.25 seconds.</summary>
    public const int VanillaBoxMs = 5250;

    /// <summary>Lines a box holds in vanilla's time; each line past these adds <see cref="ExtraLineMs"/>.</summary>
    public const int VanillaBoxLines = 3;

    /// <summary>Reading time for each line past <see cref="VanillaBoxLines"/>.</summary>
    public const int ExtraLineMs = 1200;

    /// <summary>How long a box of <paramref name="lines"/> wrapped lines stays up: vanilla's time,
    /// longer for a long list so it can be read.</summary>
    public static int BoxDurationMs(int lines)
        => VanillaBoxMs + Math.Max(0, lines - VanillaBoxLines) * ExtraLineMs;

    /// <summary>One entry per item (same id and name), counts added, the biggest first; ties keep
    /// the order they were taken in.</summary>
    public static List<StolenStack> Merge(IEnumerable<StolenStack> taken)
    {
        if (taken is null) throw new ArgumentNullException(nameof(taken));
        var merged = new List<StolenStack>();
        foreach (StolenStack s in taken)
        {
            if (s == null || s.Count <= 0) continue;
            StolenStack? same = merged.Find(m => m.ItemId == s.ItemId && m.Name == s.Name);
            if (same != null) same.Count += s.Count;
            else merged.Add(new StolenStack { ItemId = s.ItemId, Name = s.Name, Category = s.Category, Count = s.Count });
        }
        return merged.Select((s, i) => (s, i)).OrderByDescending(p => p.s.Count).ThenBy(p => p.i).Select(p => p.s).ToList();
    }

    /// <summary>The thief's list: up to <see cref="MaxStolenKinds"/> counted items ("3 Parsnips",
    /// "1 bottle of Wine"), and how many units of the rest go unnamed (0 when none). Expects
    /// <see cref="Merge"/>'s output.</summary>
    public static (List<string> Named, int OtherUnits) StolenPhrases(IReadOnlyList<StolenStack> merged, Func<string, string> gamePlural)
    {
        if (merged is null) throw new ArgumentNullException(nameof(merged));
        var named = new List<string>();
        int other = 0;
        for (int i = 0; i < merged.Count; i++)
        {
            StolenStack s = merged[i];
            if (i < MaxStolenKinds) named.Add(AskPhrases.Counted(s.Count, s.ItemId, s.Name, gamePlural, s.Category));
            else other += s.Count;
        }
        return (named, other);
    }

    /// <summary>An English list: "a", "a and b", "a, b and c".</summary>
    public static string JoinList(IReadOnlyList<string> parts)
    {
        if (parts is null || parts.Count == 0) return "";
        if (parts.Count == 1) return parts[0];
        return string.Join(", ", parts.Take(parts.Count - 1)) + " and " + parts[parts.Count - 1];
    }

    /// <summary>Total units the thief took.</summary>
    public static int Units(IEnumerable<StolenStack> taken) => taken?.Where(s => s != null).Sum(s => Math.Max(0, s.Count)) ?? 0;
}
