using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Player-facing banish text that is built from data (the refund notice).</summary>
public static class BanishText
{
    /// <summary>One line naming every refunded banish and the JP that came back.</summary>
    public static string RefundNotice(IReadOnlyList<BanishPick> refunded)
    {
        List<string> names = refunded.Select(p => string.IsNullOrEmpty(p.Label) ? p.Key : p.Label).ToList();
        // "A", "A and B", "A, B and C": the separator and the "and" are both translatable.
        string joined = names.Count == 0 ? "" : names[^1];
        if (names.Count >= 2)
        {
            string head = string.Join(Strings.Get("banish.list-separator"), names.Take(names.Count - 1));
            joined = Strings.Get("banish.and", new Dictionary<string, string> { ["a"] = head, ["b"] = names[^1] });
        }
        long jp = refunded.Sum(p => p.Paid);
        return Strings.Get("banish.refund", new Dictionary<string, string>
        {
            ["names"] = joined,
            ["jp"] = jp.ToString(),
        });
    }
}
