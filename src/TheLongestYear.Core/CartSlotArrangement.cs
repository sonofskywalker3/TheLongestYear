using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The order the Traveling Cart's stock is shown in, before the cart-slot cap keeps the
/// first N (CartSlotLimitPatch). Two rules (Jeff, 2026-10-27):
///
/// - <b>Extras take a random slot.</b> Vanilla lists its 10 random objects first and its fixed
///   extras (Rare Seed, Coffee Bean, the year-1 Red Cabbage guarantee, catalogues...) after them,
///   so a capped cart never showed an extra at all. Each extra is now inserted at a random
///   position, so it is as likely as any random object to land in a shown slot.
/// - <b>A Year 2 slot.</b> Each cart day has a <see cref="YearTwoChance"/> chance that one shown
///   slot holds an item no shop sells before year 2 (vanilla or modded, <see cref="YearTwoShopItems"/>),
///   one roll per visit however many slots are bought. On the day vanilla's year-1 guarantee fires,
///   that guaranteed item takes the slot without a roll.
///
/// The Year 2 roll and pick use their own random stream that does not read the stock, so every
/// rebuild of the same day (reopening the cart after a purchase, the shrine preview) picks the
/// same item: <see cref="CartDayStock"/> remembers the first build's ids and would drop a
/// different pick.</summary>
public static class CartSlotArrangement
{
    public const double YearTwoChance = 0.10;

    /// <param name="stock">Stock keys in vanilla order (<see cref="CartDayStock.KeyFor"/>).</param>
    /// <param name="mainDrawCount">How many leading keys are the cart's main random draw; the rest
    /// are extras.</param>
    /// <param name="guaranteed">Keys of extras that are a guarantee when present (the year-1 Red
    /// Cabbage visit).</param>
    /// <param name="yearTwoPool">Year 2 keys in a stable order.</param>
    /// <param name="allowed">How many slots the cart shows.</param>
    /// <param name="shuffleRng">Places the extras.</param>
    /// <param name="yearTwoRng">Rolls and picks the Year 2 slot; never reads the stock.</param>
    public static CartArrangement Arrange(IReadOnlyList<string> stock, int mainDrawCount,
        IReadOnlySet<string> guaranteed, IReadOnlyList<string> yearTwoPool, int allowed,
        Random shuffleRng, Random yearTwoRng)
    {
        if (stock == null) throw new ArgumentNullException(nameof(stock));
        int main = Math.Clamp(mainDrawCount, 0, stock.Count);
        var order = stock.Take(main).ToList();
        string? guarantee = stock.Skip(main).FirstOrDefault(guaranteed.Contains);
        foreach (string extra in stock.Skip(main))
        {
            if (extra == guarantee) continue;
            order.Insert(shuffleRng.Next(order.Count + 1), extra);
        }

        string? yearTwo = PickYearTwo(guarantee, yearTwoPool, yearTwoRng);
        if (yearTwo == null || allowed <= 0)
        {
            if (guarantee != null) order.Add(guarantee);
            return new CartArrangement(order, null);
        }

        int at = order.IndexOf(yearTwo);
        if (at >= 0 && at < allowed)
            return new CartArrangement(order, null);   // already shown
        if (at >= 0) order.RemoveAt(at);
        order.Insert(shuffleRng.Next(Math.Min(allowed, order.Count + 1)), yearTwo);
        return new CartArrangement(order, stock.Contains(yearTwo) ? null : yearTwo);
    }

    /// <summary>The guarantee when it is in today's stock, else a <see cref="YearTwoChance"/>
    /// roll for a pool item. Always draws the same two numbers so the stream stays aligned.</summary>
    private static string? PickYearTwo(string? guarantee, IReadOnlyList<string> pool, Random rng)
    {
        double roll = rng.NextDouble();
        int index = rng.Next(Math.Max(1, pool.Count));
        if (guarantee != null) return guarantee;
        if (roll >= YearTwoChance || pool.Count == 0) return null;
        return pool[index];
    }
}

/// <summary>The cart's display order, and the Year 2 key the caller must add to the stock
/// because vanilla did not stock it today (null when nothing is added).</summary>
public sealed record CartArrangement(IReadOnlyList<string> Order, string? Added);
