using System;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Maps the player cannot stand in on day 1, held to their opening week in the landing
/// tables. The Secret Woods sits behind the large stump, which needs the steel axe; the board already
/// opens it at week 4, and its data routes landed from day 1
/// until 2026-11-05 (Jeff: "fix this"). Deep Woods is entered only through the Secret Woods, so it
/// opens the same week.
/// <para>The gate goes into both tables of a route (<see cref="ObtainSource.Lands"/> and
/// <see cref="ObtainSource.UndelayedLands"/>): it is the calendar, not travel, so a consumer that
/// prices travel itself still cannot land the route before the map opens. It is applied once per
/// route, before any travel (<see cref="MineDepth.WithTravel"/>), so a Deep Woods depth wait overlaps
/// the wait for the woods instead of adding to it.</para></summary>
public static class MapWeekGate
{
    public const string SecretWoodsMap = "Woods";
    public const string DeepWoodsMap = "DeepWoods";
    private const string LocationPrefix = "location:";
    private static readonly string[] GatedMaps = { SecretWoodsMap, DeepWoodsMap };

    /// <summary>The week the Secret Woods opens: the same week the board gives it. This folder is built
    /// blind of the board's tables, so the number is kept here and a test holds the two equal.</summary>
    public const int WoodsOpeningWeek = 4;

    /// <summary>The first day the Secret Woods (and so Deep Woods) is open: the first day of its week.</summary>
    public static int WoodsOpeningDay => (WoodsOpeningWeek - 1) * Calendar.DaysPerWeek + 1;

    /// <summary>The day the latest gated map this route needs opens, or null when it needs none. Only an
    /// exact "location:Woods" or "location:DeepWoods" counts.</summary>
    public static int? OpeningDay(ObtainConditions conditions)
    {
        if (conditions is null) throw new ArgumentNullException(nameof(conditions));
        bool gated = conditions.Requires.Any(r =>
            r.StartsWith(LocationPrefix, StringComparison.Ordinal)
            && GatedMaps.Contains(r.Substring(LocationPrefix.Length), StringComparer.Ordinal));
        return gated ? WoodsOpeningDay : null;
    }

    /// <summary>The route with its map's opening day built into both tables (unchanged when it needs none).</summary>
    public static ObtainSource Apply(ObtainSource source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (OpeningDay(source.Conditions) is not int day) return source;
        return source with
        {
            Lands = source.Lands.StartNoEarlierThan(day),
            UndelayedLands = source.UndelayedLands?.StartNoEarlierThan(day),
        };
    }
}
