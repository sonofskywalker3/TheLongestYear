using System;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core;

/// <summary>Why an item is a year-2 item, or <see cref="YearTwoReason.YearOne"/> when it is not.</summary>
public enum YearTwoReason
{
    /// <summary>A loop can get it before the Community Center is complete.</summary>
    YearOne,
    /// <summary>The obtainability model has no route at all: nothing in the loaded data hands it out.</summary>
    NoRoute,
    /// <summary>Its routes need year 2 or a finished CC (YEAR 2, IS_COMMUNITY_CENTER_COMPLETE...).</summary>
    YearTwoOnly,
    /// <summary>Its routes are on Ginger Island, which opens only once the CC is complete.</summary>
    IslandOnly,
    /// <summary>Its routes need year 2 or the island, mixed.</summary>
    YearTwoOrIsland,
    /// <summary>Its only routes need something a loop cannot get from nothing (an animal no shop sells).</summary>
    OwnedOnly,
}

/// <summary>TLY's year 1 / year 2 line for every item, base game and mods alike (Jeff, 2026-10-27):
/// an item is year 1 when a loop can get it before the Community Center is complete, and year 2
/// when it must wait for that. Read from an <see cref="ObtainabilityModel"/> built from the installed
/// game data at save load, so a mod's items are sorted by the same rule. That model must be built
/// from <see cref="WithoutCartRandomStock"/>: the Traveling Cart's random draw can roll almost any
/// object, so counting it as a route would make nearly every object year 1 (Garlic Seeds, and the
/// Garlic a Seed Maker turns back into seeds), and the cart is what this sort feeds.
///
/// A content pack that adds a source only once the CC is done (a Content Patcher <c>When</c> on
/// <c>IsCommunityCenterComplete</c> or a CC mail flag) has no source in the data during a loop, so
/// its item reads <see cref="YearTwoReason.NoRoute"/>: year 2, as it should be. The opposite slip is
/// possible: a route the model counts but a loop cannot really reach (an area a mod locks behind a
/// map warp the model does not read) keeps its item in year 1.</summary>
public static class YearTwoItems
{
    private const int FirstDay = 1;
    private const string CartShopId = "Traveler";

    /// <summary>The inputs without the cart's item-query lines (its random objects and furniture),
    /// whether read as the query or listed by the game from it (<see cref="ShopRow.Query"/>).
    /// The cart's fixed lines (Rare Seed, Coffee Bean...) stay: they are real year-1 routes.</summary>
    public static ObtainabilityInputs WithoutCartRandomStock(ObtainabilityInputs inputs)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        return inputs with
        {
            Shops = inputs.Shops.Where(r => !(r.ShopId == CartShopId && (r.Query != null || ItemQueries.IsQuery(r.ItemId)))).ToList(),
        };
    }

    /// <summary>The year-1 question: every route except year-2, island and owned-only ones. Unread
    /// conditions still count, so a doubtful route keeps an item in year 1.</summary>
    public static readonly ObtainFilter YearOneRoutes = new()
    {
        IncludeYearTwo = false, IncludeGingerIsland = false, IncludeOwnedOnly = false, IncludeUnresolved = true,
    };

    public static YearTwoReason Classify(ObtainabilityModel model, string itemId)
    {
        if (model == null) throw new ArgumentNullException(nameof(model));
        if (Lands(model, itemId, YearOneRoutes)) return YearTwoReason.YearOne;
        if (model.Sources(itemId).Count == 0) return YearTwoReason.NoRoute;
        bool viaYearTwo = Lands(model, itemId, YearOneRoutes with { IncludeYearTwo = true });
        bool viaIsland = Lands(model, itemId, YearOneRoutes with { IncludeGingerIsland = true });
        if (viaYearTwo) return YearTwoReason.YearTwoOnly;
        if (viaIsland) return YearTwoReason.IslandOnly;
        if (Lands(model, itemId, YearOneRoutes with { IncludeYearTwo = true, IncludeGingerIsland = true }))
            return YearTwoReason.YearTwoOrIsland;
        return Lands(model, itemId, YearOneRoutes with { IncludeOwnedOnly = true })
            ? YearTwoReason.OwnedOnly
            : YearTwoReason.NoRoute;
    }

    public static bool IsYearTwo(ObtainabilityModel model, string itemId)
        => Classify(model, itemId) != YearTwoReason.YearOne;

    private static bool Lands(ObtainabilityModel model, string itemId, ObtainFilter filter)
        => model.Table(itemId, filter).Lands(FirstDay) != null;
}
