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
    private readonly IReadOnlySet<string> _unplaced;

    private ShopWeeks(IReadOnlyDictionary<string, PlaceWeek> weeks, IReadOnlySet<string> unplaced)
    {
        _weeks = weeks;
        _unplaced = unplaced;
    }

    /// <summary>The shop weeks and unplaced items as recorded (<see cref="Weeks"/>,
    /// <see cref="UnplacedItems"/>), for an offline rebuild of the availability inputs
    /// (<see cref="Lexicon.AvailabilityInputs"/>). Null unplaced = none.</summary>
    internal static ShopWeeks FromWeeks(IReadOnlyDictionary<string, PlaceWeek> weeks, IEnumerable<string>? unplaced = null)
        => new(new Dictionary<string, PlaceWeek>(weeks ?? throw new ArgumentNullException(nameof(weeks)), StringComparer.Ordinal),
            new HashSet<string>(unplaced ?? Array.Empty<string>(), StringComparer.Ordinal));

    /// <summary>Every item some walkable shop sells, with its earliest weeks. For the log and tests.</summary>
    public IReadOnlyDictionary<string, PlaceWeek> Weeks => _weeks;

    /// <summary>Items (normalized ids) a non-recipe, year-1 listing sells only at shops that are
    /// unplaced or flagged, so they have no <see cref="Weeks"/> entry: vanilla's Pineapple Seeds
    /// (Island Trader), SVE's Joja Berry and Joja Veggie seeds (Joja-route vendors). An item no shop
    /// lists at all is not here: nothing says where it comes from, so the rules keep their own
    /// arithmetic for it.</summary>
    public IReadOnlySet<string> UnplacedItems => _unplaced;

    /// <param name="flaggedShops">Shop ids a supported mod's layer marks as year-2 or Ginger Island
    /// content (<see cref="Obtainability.YearTwoMarks.Shops"/>): their listings are skipped, so a
    /// shop standing on a reached map still sells nothing in year 1. Null = none.</param>
    public static ShopWeeks Build(
        IReadOnlyList<RawShopListing> listings,
        IReadOnlyList<RawShopPlacement> placements,
        LocationWeeks locationWeeks,
        IReadOnlySet<string>? flaggedShops = null)
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
        var unplaced = new HashSet<string>(StringComparer.Ordinal);
        foreach (RawShopListing listing in listings)
        {
            if (listing == null || listing.IsRecipe || listing.LockedAfterYearOne || string.IsNullOrEmpty(listing.ItemId)) continue;
            string id = BundleParsing.NormalizeItemId(listing.ItemId);
            if ((flaggedShops != null && flaggedShops.Contains(listing.ShopId ?? ""))
                || !shopWeek.TryGetValue(listing.ShopId ?? "", out PlaceWeek? week))
            {
                unplaced.Add(id);
                continue;
            }
            weeks[id] = weeks.TryGetValue(id, out PlaceWeek? known) ? known.EarlierOf(week) : week;
        }
        unplaced.ExceptWith(weeks.Keys);
        return new ShopWeeks(weeks, unplaced);
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
