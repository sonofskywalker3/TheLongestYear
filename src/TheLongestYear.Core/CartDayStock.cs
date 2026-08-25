using System;
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
    private const string RecipeSuffix = "#Recipe";

    /// <summary>Mirrors vanilla ShopBuilder.TrackSeenItems' dedupe key: a recipe listing
    /// gets its own slot alongside the plain item, matching the game's own "#Recipe" suffix
    /// (F3: without this, an item and its recipe collapsed to one slot and the cart showed
    /// fewer items than its tier allows).</summary>
    public static string KeyFor(string qualifiedItemId, bool isRecipe)
        => isRecipe ? qualifiedItemId + RecipeSuffix : qualifiedItemId;

    public static IReadOnlyList<string> Select(RunState run, int day, IReadOnlyList<string> stockIds, int allowed)
    {
        if (allowed <= 0)
            return Array.Empty<string>();

        bool sameDay = run.CartStockDay == day && run.CartStockIds is { Count: > 0 };
        if (!sameDay)
        {
            var chosen = stockIds.Take(allowed).ToList();
            run.CartStockDay = chosen.Count > 0 ? day : NoDay;
            run.CartStockIds = chosen;
            return chosen;
        }

        var remembered = new HashSet<string>(run.CartStockIds, StringComparer.Ordinal);
        return stockIds.Where(remembered.Contains).ToList();
    }
}
