using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Per-day Traveling Cart selection. The first time the cart's stock is built on a
/// day, the first <c>allowed</c> ids are remembered on the RunState; every later build that
/// day is filtered to those ids, so a purchase leaves a gap for the rest of the day instead
/// of the next item sliding into view.</summary>
public static class CartDayStock
{
    private const int NoDay = -1;

    public static IReadOnlyList<string> Select(RunState run, int day, IReadOnlyList<string> stockIds, int allowed)
    {
        if (allowed <= 0)
            return System.Array.Empty<string>();
        run.CartStockIds ??= new List<string>();

        bool sameDay = run.CartStockDay == day && run.CartStockIds.Count > 0;
        if (!sameDay)
        {
            var chosen = stockIds.Take(allowed).ToList();
            run.CartStockDay = chosen.Count > 0 ? day : NoDay;
            run.CartStockIds = chosen;
            return chosen;
        }

        var remembered = new HashSet<string>(run.CartStockIds, System.StringComparer.Ordinal);
        return stockIds.Where(remembered.Contains).ToList();
    }
}
