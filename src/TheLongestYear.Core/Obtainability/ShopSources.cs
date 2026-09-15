using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Sources that are bought or handed out: shop rows, the Traveling Cart, festival shops,
/// Night Market boats, and the Squid Fest and Trout Derby rewards.</summary>
public static class ShopSources
{
    private const string CartShopId = "Traveler";
    private const string FestivalShopPrefix = "Festival_";
    private const string NightMarketId = "NightMarket";
    private const string TravelingMerchantSuffix = "_TravelingMerchant";
    private static readonly string[] IslandShopMarkers = { "Island", "Volcano", "Resort", "QiGem" };

    /// <summary>Festival key to the shop it opens (Event.cs 11818-11838). Keys are Data/Festivals/FestivalDates keys.</summary>
    private static readonly IReadOnlyDictionary<string, string> FestivalShopKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Festival_EggFestival_Pierre"] = "spring13",
        ["Festival_FlowerDance_Pierre"] = "spring24",
        ["Festival_Luau_Pierre"] = "summer11",
        ["Festival_DanceOfTheMoonlightJellies_Pierre"] = "summer28",
        ["Festival_SpiritsEve_Pierre"] = "fall27",
        ["Festival_FestivalOfIce_TravelingMerchant"] = "winter8",
        ["Festival_FeastOfTheWinterStar_Pierre"] = "winter25",
    };

    /// <summary>Squid Fest rewards (GameLocation.cs 11458-11499: score tiers, with a 50/50 between Winter
    /// Seeds and Mystery Boxes, and Mystery Boxes plus 265 only once the book is owned) and Trout Derby
    /// rewards (11524-11562: the first tag always gives a Tent Kit, later tags spin a wheel).</summary>
    private static readonly (string Festival, string ItemId, Reliability Reliability)[] RewardTable =
    {
        ("SquidFest", "(O)DeluxeBait", Reliability.Dependable), ("SquidFest", "(O)498", Reliability.Chance),
        ("SquidFest", "(O)MysteryBox", Reliability.Chance), ("SquidFest", "(O)242", Reliability.Dependable),
        ("SquidFest", "(O)797", Reliability.Dependable), ("SquidFest", "(O)395", Reliability.Dependable),
        ("SquidFest", "(F)SquidKid_Painting", Reliability.Dependable), ("SquidFest", "(O)Book_Crabbing", Reliability.Dependable),
        ("SquidFest", "(O)265", Reliability.Chance), ("SquidFest", "(O)694", Reliability.Dependable),
        ("SquidFest", "(O)166", Reliability.Dependable), ("SquidFest", "(O)253", Reliability.Dependable),
        ("SquidFest", "(H)SquidHat", Reliability.Dependable),
        ("TroutDerby", "(O)TentKit", Reliability.Dependable), ("TroutDerby", "(H)BucketHat", Reliability.Chance),
        ("TroutDerby", "(O)710", Reliability.Chance), ("TroutDerby", "(O)MysteryBox", Reliability.Chance),
        ("TroutDerby", "(O)72", Reliability.Chance), ("TroutDerby", "(F)MountedTrout_Painting", Reliability.Chance),
        ("TroutDerby", "(O)DeluxeBait", Reliability.Chance), ("TroutDerby", "(O)253", Reliability.Chance),
        ("TroutDerby", "(O)621", Reliability.Chance), ("TroutDerby", "(O)688", Reliability.Chance),
        ("TroutDerby", "(O)749", Reliability.Chance),
    };

    public static bool IsIslandShop(string shopId)
        => IslandShopMarkers.Any(m => shopId.Contains(m, StringComparison.Ordinal));

    public static IEnumerable<(string ItemId, ObtainSource Source)> Stock(
        IEnumerable<ShopRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach (ShopRow row in rows)
        {
            if (row.IsRecipe || row.TradeItemId != null) continue;
            if (Template(row, festivals) is not ObtainSource template) continue;
            foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                yield return emitted;
        }
    }

    /// <summary>Barter rows (ShopItemData.TradeItemId): paid for with another item, so, counting weeks in
    /// isolation, only had in weeks the trade item can be had.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> Barter(
        IEnumerable<ShopRow> rows, IReadOnlyDictionary<string, ObjInfo> objects,
        IReadOnlyDictionary<string, FestivalDates> festivals, ObtainabilityModel snapshot)
    {
        foreach (ShopRow row in rows.Where(r => !r.IsRecipe && r.TradeItemId != null))
        {
            if (Template(row, festivals) is not ObtainSource template) continue;
            string trade = BundleParsing.NormalizeItemId(row.TradeItemId!);
            Derived.Input paid = Derived.Of(snapshot, trade);
            ObtainConditions conditions = template.Conditions with { Requires = template.Conditions.Requires.Append("trade:" + trade).ToList() };
            if (paid.IsEmpty)
            {
                // The trade item has no source at all, so the barter cannot be made. Recorded as a
                // diagnostic rather than silently dropped, so the drop shows in the unresolved list.
                yield return (ItemQueries.UnresolvedPrefix + "trade " + trade, new ObtainSource(
                    SourceKind.Other, DayTable.Always, Reliability.Chance, conditions with { Unresolved = true },
                    $"{template.Detail} barter for {row.ItemId}: trade item {trade} has no source"));
                continue;
            }
            foreach (ObtainSource s in paid.Emit(template.Kind, t => template.Lands.Latest(t), conditions,
                template.Detail + " (barter)", luck: template.Reliability != Reliability.Dependable))
                foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, s))
                    yield return emitted;
        }
    }

    public static IReadOnlyDictionary<string, WeekMask> RecipeWeeks(
        IEnumerable<ShopRow> rows, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        var result = new Dictionary<string, WeekMask>(StringComparer.Ordinal);
        foreach (ShopRow row in rows.Where(r => r.IsRecipe))
        {
            if (RawTemplate(row, festivals) is not (WeekMask weeks, ObtainSource template)) continue;
            ObtainConditions c = template.Conditions;
            if (c.YearTwo || c.GingerIsland || c.Unresolved) continue;
            string id = BundleParsing.NormalizeItemId(row.ItemId);
            result[id] = result.TryGetValue(id, out WeekMask existing) ? existing | weeks : weeks;
        }
        return result;
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FestivalRewards(
        IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        foreach ((string festivalId, string itemId, Reliability reliability) in RewardTable)
        {
            if (!festivals.TryGetValue(festivalId, out FestivalDates? festival)) continue;
            yield return (itemId, new ObtainSource(
                SourceKind.Festival, FestivalDayTable(festival), reliability,
                ObtainConditions.None with { FewDays = true, Requires = new[] { "festival:" + festivalId } },
                $"{festivalId} reward"));
        }
    }

    /// <summary>The exact days a festival is open, day by day rather than by week.</summary>
    private static DayTable FestivalDayTable(FestivalDates festival) => DayTable.Available(d => IsFestivalDay(festival, d));

    private static bool IsFestivalDay(FestivalDates festival, int day)
    {
        int startDoy = Calendar.DayOfYear((int)festival.Season, festival.StartDay);
        int endDoy = Calendar.DayOfYear((int)festival.Season, festival.EndDay);
        return day >= startDoy && day <= endDoy;
    }

    /// <summary>The source every item of a row shares, or null when the row can never be stocked in year 1.</summary>
    private static ObtainSource? Template(ShopRow row, IReadOnlyDictionary<string, FestivalDates> festivals)
        => RawTemplate(row, festivals) is (_, ObtainSource template) ? template : null;

    /// <summary>Same as <see cref="Template"/>, but also returns the raw week mask: <see cref="RecipeWeeks"/>
    /// still speaks in weeks (readers use it as a season mask), so it needs the mask before it is wrapped
    /// into a same-day table.</summary>
    private static (WeekMask Weeks, ObtainSource Template)? RawTemplate(ShopRow row, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        ConditionReading reading = ConditionSeasons.Read(row.Condition, festivals);
        (SourceKind kind, WeekMask placeWeeks, bool fewDays, bool unplaced, FestivalDates? festival) = Placement(row.ShopId, festivals);
        WeekMask weeks = reading.Weeks & placeWeeks;
        if (weeks.IsEmpty) return null;
        // A true intersection when a festival gates the shop: a day only counts when both the row's
        // own condition and the festival's exact dates agree, computed in one predicate rather than
        // combining two independently-computed tables afterward (DayTable.Latest is not an AND).
        DayTable table = festival is FestivalDates f
            ? DayTable.Available(d => IsFestivalDay(f, d) && ConditionSeasons.IsAvailableOn(reading, placeWeeks, d))
            : ConditionSeasons.Availability(reading, placeWeeks);
        if (table.IsEmpty) return null;
        bool chance = reading.Chance || row.IsRandom || kind == SourceKind.Cart
                      || row.ShopId.EndsWith(TravelingMerchantSuffix, StringComparison.Ordinal);
        ObtainConditions conditions = ConditionSeasons.Apply(
            ObtainConditions.None with
            {
                Requires = new[] { "shop:" + row.ShopId },
                FewDays = fewDays,
                GingerIsland = IsIslandShop(row.ShopId),
                Unresolved = unplaced,
            },
            reading);
        var template = new ObtainSource(kind, table, chance ? Reliability.Chance : Reliability.Dependable, conditions, $"shop {row.ShopId}");
        return (weeks, template);
    }

    private static (SourceKind Kind, WeekMask Weeks, bool FewDays, bool Unplaced, FestivalDates? Festival) Placement(
        string shopId, IReadOnlyDictionary<string, FestivalDates> festivals)
    {
        if (shopId == CartShopId) return (SourceKind.Cart, WeekMask.All, false, false, null);
        if (shopId.StartsWith(FestivalShopPrefix, StringComparison.Ordinal))
        {
            string name = shopId.Substring(FestivalShopPrefix.Length).Split('_')[0];
            SourceKind kind = KindOf(name);
            if (FestivalShopKeys.TryGetValue(shopId, out string? key) && festivals.TryGetValue(key, out FestivalDates? day))
                return (kind, day.Weeks, true, false, day);
            if (festivals.TryGetValue(name, out FestivalDates? passive))
                return (kind, passive.Weeks, true, false, passive);
            return (kind, WeekMask.All, true, true, null);
        }

        // A festival shop that does not carry the "Festival_" prefix: the Desert Festival stalls are
        // named "DesertFestival_Pam", "DesertFestival_EggShop" and so on (DesertFestival.cs 777 opens
        // "DesertFestival_EggShop"). The segment before the first "_" is a Data/PassiveFestivals key
        // whenever the shop belongs to that festival, so those stalls only open on its exact days.
        string head = shopId.Split('_')[0];
        if (head.Length < shopId.Length && festivals.TryGetValue(head, out FestivalDates? own))
            return (KindOf(head), own.Weeks, true, false, own);
        return (SourceKind.Shop, WeekMask.All, false, false, null);
    }

    private static SourceKind KindOf(string festivalName)
        => festivalName == NightMarketId ? SourceKind.NightMarket : SourceKind.Festival;
}
