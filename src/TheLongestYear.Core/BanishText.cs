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
        string joined = names.Count == 2
            ? Strings.Get("banish.and", new Dictionary<string, string> { ["a"] = names[0], ["b"] = names[1] })
            : string.Join(", ", names);
        long jp = refunded.Sum(p => p.Paid);
        return Strings.Get("banish.refund", new Dictionary<string, string>
        {
            ["names"] = joined,
            ["jp"] = jp.ToString(),
        });
    }
}
