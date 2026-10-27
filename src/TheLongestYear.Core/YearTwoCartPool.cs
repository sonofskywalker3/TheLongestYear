using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One registered item, as much of it as the cart pool needs.</summary>
/// <param name="QualifiedId">"(O)830", "(W)57", "(O)Mod.Item".</param>
/// <param name="TypeId">The item type prefix, "(O)", "(W)"...</param>
/// <param name="CartSellsKind">The cart's own random-stock line would sell this kind of item: it passes
/// that line's PerItemCondition, has a price and is not ExcludeFromRandomSale. Vanilla's cart only
/// sells objects with a real category, never weapons, boots, rings, minerals, artifacts or quest
/// items, so its Year 2 slot keeps to the same kinds.</param>
public sealed record CartCandidate(string QualifiedId, string TypeId, bool CartSellsKind, YearTwoReason Reason);

/// <summary>The items the Traveling Cart's Year 2 slot can offer (Jeff, 2026-10-27): every item TLY
/// sorts as year 2 (<see cref="YearTwoItems"/>) of a kind the cart already sells in year 1.
///
/// A positive year-2 reason (a YEAR 2 or CC-complete route, an island route, an owned-only route) is
/// read from real data. "No route at all" only means something for objects: the model reads object
/// sources thoroughly, but not how tools, weapons, hats, clothing or furniture are handed out. A mod's
/// object with no route is year 2 by the same rule (its source arrives only after the CC, or never).
/// <see cref="VanillaYearRulings"/> settles the vanilla items the model cannot read either way.</summary>
public static class YearTwoCartPool
{
    private const string ObjectTypeId = "(O)";

    public static IReadOnlyList<string> Select(IEnumerable<CartCandidate> candidates)
        => (candidates ?? Array.Empty<CartCandidate>())
            .Where(Offers)
            .Select(c => c.QualifiedId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

    public static bool Offers(CartCandidate c)
    {
        if (!c.CartSellsKind) return false;
        if (VanillaYearRulings.Ruling(c.QualifiedId) is YearRuling ruling)
            return ruling == YearRuling.YearTwo;
        return c.Reason switch
        {
            YearTwoReason.YearOne => false,
            YearTwoReason.NoRoute => c.TypeId == ObjectTypeId,
            _ => true,
        };
    }
}
