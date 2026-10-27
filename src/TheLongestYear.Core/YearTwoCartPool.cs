using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One registered item, as much of it as the cart pool needs.</summary>
/// <param name="QualifiedId">"(O)830", "(W)57", "(O)Mod.Item".</param>
/// <param name="TypeId">The item type prefix, "(O)", "(W)"...</param>
/// <param name="Price">What the cart's pricing starts from (salePrice); zero or less is unsellable.</param>
/// <param name="ObjectType">Data/Objects Type ("Basic", "Litter", "Quest"...), null for other types.</param>
public sealed record CartCandidate(string QualifiedId, string TypeId, int Price, string? ObjectType, int Category, YearTwoReason Reason);

/// <summary>The items the Traveling Cart's Year 2 slot can offer (Jeff, 2026-10-27): every item TLY
/// sorts as year 2 (<see cref="YearTwoItems"/>) that a shop could sensibly sell.
///
/// The model's verdict is trusted in two different ways. A positive year-2 reason (a YEAR 2 or
/// CC-complete route, an island route, an owned-only route) is read from real data, so it counts for
/// every item type. "No route at all" only means something for objects: the model reads object
/// sources thoroughly, but not how tools, weapons, hats, clothing or furniture are handed out (Clint,
/// mine chests, the Hat Mouse, tailoring, catalogues), so for those types it says nothing. A mod's
/// object with no route is year 2 by the same rule (its source arrives only after the CC, or never).
/// <see cref="VanillaYearRulings"/> settles the vanilla items the model cannot read either way.</summary>
public static class YearTwoCartPool
{
    private const string ObjectTypeId = "(O)";
    private const string ToolTypeId = "(T)";
    private const int LitterCategory = -999;
    private static readonly IReadOnlySet<string> UnsoldObjectTypes =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Litter", "Quest" };

    public static IReadOnlyList<string> Select(IEnumerable<CartCandidate> candidates)
        => (candidates ?? Array.Empty<CartCandidate>())
            .Where(Offers)
            .Select(c => c.QualifiedId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

    public static bool Offers(CartCandidate c)
    {
        if (c.Price <= 0 || c.TypeId == ToolTypeId) return false;
        if (c.TypeId == ObjectTypeId
            && (c.Category == LitterCategory || (c.ObjectType != null && UnsoldObjectTypes.Contains(c.ObjectType))))
            return false;
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
