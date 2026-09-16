using System;
using System.Collections.Generic;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Sabotage;

/// <summary>How many days of from-nothing mine travel (<see cref="MineDepth"/>) a made route's table
/// inherited from its inputs, so the fairness rule can take them back out and price the save's real
/// depth instead.
/// <para>The made table was derived from each input item's COMBINED table (every route of the item under
/// the filter the made route was read with), so the credit is read the same way: per input item, the
/// combined landing as built (N) minus the combined landing rebuilt from each route's table before the
/// travel (O: a direct route's <see cref="ObtainSource.UndelayedLands"/>, a made route's own landing less
/// its own credit, recursively). Across one group the earliest item serves, across groups the latest
/// is waited for, and the credit is the latest N minus the latest O. It does not depend on the save.</para>
/// <para>Exact for chains that pass an input's landing straight through (a machine's processing days, a
/// recipe's ingredients). Approximate, by a few days, through a chain that rounds to a later window (a
/// weekly or seasonal gate). Where a route's O cannot be rebuilt (a made input route whose table has no
/// landing past the end of the year, a chance half the builder dropped) the route is left out of O, which
/// can only shrink the credit, so the made route lands later, never earlier, than the rule said before
/// the travel was added. An input already being read further up a cycle is left out of both N and O,
/// which is approximate.</para></summary>
internal static class MineTravelCredit
{
    private const int NoDays = 0;
    /// <summary>The same depth the rule follows inputs to.</summary>
    private const int MaxDepth = 6;

    public static int Inherited(ObtainSource made, int startDay, ObtainabilityModel model)
        => Inherited(made, startDay, model, new HashSet<string>(StringComparer.Ordinal), 0);

    private static int Inherited(
        ObtainSource made, int startDay, ObtainabilityModel model, HashSet<string> reading, int depth)
    {
        if (made.Inputs.Count == 0 || depth >= MaxDepth) return NoDays;
        ObtainFilter filter = FilterFor(made);
        int? latestBuilt = null;
        int? latestReached = null;
        foreach (IReadOnlyList<string> group in made.Inputs)
        {
            if (group.Count == 0) continue;
            int? groupBuilt = null;
            int? groupReached = null;
            foreach (string id in group)
            {
                if (!reading.Add(id)) continue;   // already being read up the chain: left out
                try
                {
                    groupBuilt = Earliest(groupBuilt, model.Table(id, filter).Lands(startDay));
                    foreach (ObtainSource route in model.Sources(id))
                        if (filter.Accepts(route))
                            groupReached = Earliest(groupReached, Reached(route, startDay, model, reading, depth + 1));
                }
                finally { reading.Remove(id); }
            }
            if (groupBuilt is null || groupReached is null) return NoDays;
            latestBuilt = Latest(latestBuilt, groupBuilt.Value);
            latestReached = Latest(latestReached, groupReached.Value);
        }
        return latestBuilt is int n && latestReached is int o && n > o ? n - o : NoDays;
    }

    /// <summary>A route's landing with every mine floor already reached, or null when it cannot be rebuilt.</summary>
    private static int? Reached(ObtainSource route, int startDay, ObtainabilityModel model, HashSet<string> reading, int depth)
    {
        if (route.UndelayedLands != null) return route.UndelayedLands.Lands(startDay);
        int? built = route.Lands.Lands(startDay);
        if (built is null || route.Inputs.Count == 0) return built;
        return built.Value - Math.Min(Inherited(route, startDay, model, reading, depth), built.Value - startDay);
    }

    /// <summary>The filter the builder read this route's inputs with: its reliability's table, and the
    /// island, year 2 and owned-only routes its own flags admit (Derived.Of's eight variants).</summary>
    private static ObtainFilter FilterFor(ObtainSource made)
        => (made.Reliability == Reliability.Dependable ? ObtainFilter.DependableOnly : ObtainFilter.Any) with
        {
            IncludeGingerIsland = made.Conditions.GingerIsland,
            IncludeYearTwo = made.Conditions.YearTwo,
            IncludeOwnedOnly = made.Conditions.OwnedOnly,
        };

    private static int? Earliest(int? a, int? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);

    private static int Latest(int? a, int b) => a is null ? b : Math.Max(a.Value, b);
}
