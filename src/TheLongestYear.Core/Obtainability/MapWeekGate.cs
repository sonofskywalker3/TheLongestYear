using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Maps the player cannot stand in on day 1, held to their opening week in the landing
/// tables. The board already opens each at that week; their data routes landed from day 1 until
/// 2026-11-05 (Jeff: "fix this", then "fix the sewer and swamp too"):
/// <list type="bullet">
/// <item>The Secret Woods sits behind the large stump, which needs the steel axe: week 4. Deep Woods
/// is entered only through it, so it opens the same week.</item>
/// <item>The Sewer needs the Rusty Key (60 museum donations), and the Mutant Bug Lair is inside it:
/// week 7.</item>
/// <item>The Witch's Swamp needs the Dark Talisman, after the Sewer and the Bug Lair quest: week 13,
/// and the Witch's Hut is inside it.</item>
/// </list>
/// <para>The gate goes into both tables of a route (<see cref="ObtainSource.Lands"/> and
/// <see cref="ObtainSource.UndelayedLands"/>): it is the calendar, not travel, so a consumer that
/// prices travel itself still cannot land the route before the map opens. It is applied once per
/// route, before any travel (<see cref="MineDepth.WithTravel"/>), so a Deep Woods depth wait overlaps
/// the wait for the woods instead of adding to it.</para>
/// <para>This folder is built blind of the board's tables, so the weeks are kept here and a test holds
/// each equal to the board's.</para></summary>
public static class MapWeekGate
{
    public const string SecretWoodsMap = "Woods";
    public const string DeepWoodsMap = "DeepWoods";
    public const int WoodsOpeningWeek = 4;
    public const int SewerOpeningWeek = 7;
    public const int SwampOpeningWeek = 13;
    private const string LocationPrefix = "location:";

    /// <summary>Each gated map (its exact name) and the week it opens.</summary>
    public static readonly IReadOnlyDictionary<string, int> OpeningWeeks = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [SecretWoodsMap] = WoodsOpeningWeek,
        [DeepWoodsMap] = WoodsOpeningWeek,
        ["Sewer"] = SewerOpeningWeek,
        ["BugLand"] = SewerOpeningWeek,
        ["WitchSwamp"] = SwampOpeningWeek,
        ["WitchHut"] = SwampOpeningWeek,
    };

    private const string ShopPrefix = "shop:";

    /// <summary>Shops inside a gated map, which their routes name only as "shop:&lt;id&gt;": Krobus's
    /// shop is in the Sewer, so it opens with it.</summary>
    public static readonly IReadOnlyDictionary<string, int> ShopOpeningWeeks = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["ShadowShop"] = SewerOpeningWeek,
    };

    /// <summary>The gated map each shop of <see cref="ShopOpeningWeeks"/> stands in, so a back door into
    /// the map opens the shop with it.</summary>
    public static readonly IReadOnlyDictionary<string, string> ShopMaps = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ShadowShop"] = "Sewer",
    };

    /// <summary>The first day of a week.</summary>
    public static int FirstDayOf(int week) => (week - 1) * Calendar.DaysPerWeek + 1;

    /// <summary>The first day the Secret Woods (and so Deep Woods) is open.</summary>
    public static int WoodsOpeningDay => FirstDayOf(WoodsOpeningWeek);

    /// <summary>The day the latest gated map this route needs opens, or null when it needs none. Only an
    /// exact "location:&lt;map&gt;" for a map in <see cref="OpeningWeeks"/>, or "shop:&lt;id&gt;" for a
    /// shop in <see cref="ShopOpeningWeeks"/>, counts.</summary>
    /// <param name="layerWeeks">Maps a supported mod's layer holds to a week
    /// (<see cref="YearTwoMarks.MapWeeks"/>); the later of it and this table's week counts. Null = none.</param>
    /// <param name="openedEarly">Maps of this table a supported mod opens sooner
    /// (<see cref="YearTwoMarks.OpenedEarly"/>): the earlier of it and this table's week is the map's
    /// week, before <paramref name="layerWeeks"/>. Deep Woods follows the Secret Woods. Null = none.</param>
    public static int? OpeningDay(ObtainConditions conditions, IReadOnlyDictionary<string, int>? layerWeeks = null,
        IReadOnlyDictionary<string, int>? openedEarly = null)
    {
        if (conditions is null) throw new ArgumentNullException(nameof(conditions));
        int? latest = null;
        foreach (string r in conditions.Requires)
        {
            int week;
            if (r.StartsWith(LocationPrefix, StringComparison.Ordinal))
            {
                string map = r.Substring(LocationPrefix.Length);
                bool known = OpeningWeeks.TryGetValue(map, out week);
                if (known && openedEarly != null
                    && openedEarly.TryGetValue(map == DeepWoodsMap ? SecretWoodsMap : map, out int early))
                    week = Math.Min(week, early);
                if (layerWeeks != null && layerWeeks.TryGetValue(map, out int layerWeek))
                    week = known ? Math.Max(week, layerWeek) : layerWeek;
                else if (!known) continue;
            }
            else if (r.StartsWith(ShopPrefix, StringComparison.Ordinal))
            {
                string shop = r.Substring(ShopPrefix.Length);
                if (!ShopOpeningWeeks.TryGetValue(shop, out week)) continue;
                if (openedEarly != null && ShopMaps.TryGetValue(shop, out string? shopMap)
                    && openedEarly.TryGetValue(shopMap, out int earlyShop))
                    week = Math.Min(week, earlyShop);
            }
            else continue;
            int day = FirstDayOf(week);
            if (latest is null || day > latest.Value) latest = day;
        }
        return latest;
    }

    /// <summary>The route with its map's opening day built into both tables (unchanged when it needs none).</summary>
    public static ObtainSource Apply(ObtainSource source, IReadOnlyDictionary<string, int>? layerWeeks = null,
        IReadOnlyDictionary<string, int>? openedEarly = null)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (OpeningDay(source.Conditions, layerWeeks, openedEarly) is not int day) return source;
        return source with
        {
            Lands = source.Lands.StartNoEarlierThan(day),
            UndelayedLands = source.UndelayedLands?.StartNoEarlierThan(day),
        };
    }
}
