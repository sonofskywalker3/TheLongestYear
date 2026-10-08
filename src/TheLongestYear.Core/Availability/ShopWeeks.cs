using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>The earliest week some shop the player can walk to sells an item (mod-support work,
/// 2026-10-08), read from Data/Shops the way the reachability rule reads it (item queries,
/// RandomItemId, year-2 lines closed) and dated by where each shop opens.
///
/// A shop is placed by its owners' home and current maps, by an "OpenShop" tile, or, for the few
/// vanilla shops the game opens from code at a fixed spot, by <see cref="CodeOpenedShops"/>. Each
/// placement is dated by <see cref="LocationWeeks"/>; a placement the walk cannot date, and a shop
/// with no placement at all (the Traveling Cart, festival stalls, a mod's dialogue shop), proves
/// nothing, so it gives no week. Recipe lines teach, they do not sell, and are skipped. Seasonal
/// conditions on a line are not read: this is a floor on when the item can first be bought, and
/// the rules that use it (fruit trees, crop seeds) apply the item's own seasons on top.</summary>
public sealed class ShopWeeks
{
    /// <summary>Vanilla shops opened by game code at a fixed map rather than by data
    /// (decompile: Desert.OnDesertTrader, IslandNorth.checkAction, the Volcano dwarf, the resort
    /// bar). Data/Shops gives them no owner and no map tile, so without this a mod item sold only at
    /// the Desert Trader had no place at all.</summary>
    public static readonly IReadOnlyDictionary<string, string> CodeOpenedShops =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["DesertTrade"] = "Desert",
            ["IslandTrade"] = "IslandNorth",
            ["VolcanoShop"] = "Caldera",
            ["ResortBar"] = "IslandSouth",
        };

    private readonly IReadOnlyDictionary<string, PlaceWeek> _weeks;

    private ShopWeeks(IReadOnlyDictionary<string, PlaceWeek> weeks) => _weeks = weeks;

    /// <summary>Every item some walkable shop sells, with its earliest weeks. For the log and tests.</summary>
    public IReadOnlyDictionary<string, PlaceWeek> Weeks => _weeks;

    public static ShopWeeks Build(
        IReadOnlyList<RawShopListing> listings,
        IReadOnlyList<RawShopPlacement> placements,
        LocationWeeks locationWeeks)
    {
        if (listings == null) throw new ArgumentNullException(nameof(listings));
        if (placements == null) throw new ArgumentNullException(nameof(placements));
        if (locationWeeks == null) throw new ArgumentNullException(nameof(locationWeeks));

        var shopWeek = new Dictionary<string, PlaceWeek>(StringComparer.Ordinal);
        void Place(string shopId, string location)
        {
            if (string.IsNullOrEmpty(shopId) || !locationWeeks.TryGet(location ?? "", out PlaceWeek week)) return;
            shopWeek[shopId] = shopWeek.TryGetValue(shopId, out PlaceWeek? known) ? known.EarlierOf(week) : week;
        }
        foreach (RawShopPlacement placement in placements)
            if (placement != null) Place(placement.ShopId, placement.LocationName);
        foreach (KeyValuePair<string, string> fixedShop in CodeOpenedShops)
            Place(fixedShop.Key, fixedShop.Value);

        var weeks = new Dictionary<string, PlaceWeek>(StringComparer.Ordinal);
        foreach (RawShopListing listing in listings)
        {
            if (listing == null || listing.IsRecipe || listing.LockedAfterYearOne || string.IsNullOrEmpty(listing.ItemId)) continue;
            if (!shopWeek.TryGetValue(listing.ShopId ?? "", out PlaceWeek? week)) continue;
            string id = BundleParsing.NormalizeItemId(listing.ItemId);
            weeks[id] = weeks.TryGetValue(id, out PlaceWeek? known) ? known.EarlierOf(week) : week;
        }
        return new ShopWeeks(weeks);
    }

    public bool TryGet(string qualifiedItemId, out PlaceWeek week)
    {
        week = null!;
        if (string.IsNullOrEmpty(qualifiedItemId)) return false;
        if (!_weeks.TryGetValue(BundleParsing.NormalizeItemId(qualifiedItemId), out PlaceWeek? found)) return false;
        week = found;
        return true;
    }
}
