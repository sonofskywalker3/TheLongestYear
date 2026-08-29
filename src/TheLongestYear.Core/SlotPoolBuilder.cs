using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Builds the weekly-theme goal pool: every OPEN, in-play, concrete ingredient slot of the
/// theme's bundles. Pure — live game data comes in as plain inputs (bundle data dict + a
/// per-bundle slot-state accessor), so it unit-tests without Game1.
///
/// Rules (spec 2026-07-09, activity themes 2026-08-28):
///   - A room theme takes bundles whose BundleRequirement (matched by bundle name) carries that
///     theme. An activity theme (and Mixed, which means anything) takes single LINES from any
///     bundle on the board whose item kind matches (ThemeDomains) when a classifier is supplied.
///   - The bundle must have in-play items this season (BundleRequirement.InPlayItemsFor); each
///     emitted slot is flagged Due when the day-28 gate demands it this season (DueItemsFor).
///   - A bundle that already has NumberOfSlots completed ingredient lines is complete — its
///     remaining lines can no longer be donated and are excluded.
///   - Category refs and completed slots are excluded. Null slot state ⇒ all lines open.
///   - A stretch line (BundleRequirement.StretchLines) is forced in play and Due once
///     weekOfYear reaches AvailabilityWeeks.LastWeekOf(its stretch season), regardless of the
///     obtainability predicate; its emitted slot carries Stretch = true.
///
/// weekOfYear has no default on purpose: it used to default to 0, and a caller that forgot it got
/// a pool with every stretch line silently switched off (0 is never >= a season's last week).
/// </summary>
public static class SlotPoolBuilder
{
    private const string YearTwoSeedsRouteTag = "Boost: Year-Two Seeds";
    private const string SneakPeekRouteTag = "Boost: Sneak Peek";

    public static IReadOnlyList<BonusSlot> OpenSlotsForTheme(
        IReadOnlyDictionary<string, string> bundleData,
        Func<int, bool[]?> slotStateForBundle,
        IReadOnlyList<BundleRequirement> requirements,
        Theme theme, Season season,
        Func<string, bool> isObtainableInSeason,
        int weekOfYear,
        Func<string, ItemKind>? kindOf = null,
        Func<string, string?>? routeTagOf = null)
    {
        if (bundleData == null) throw new ArgumentNullException(nameof(bundleData));
        if (slotStateForBundle == null) throw new ArgumentNullException(nameof(slotStateForBundle));
        if (requirements == null) throw new ArgumentNullException(nameof(requirements));
        if (isObtainableInSeason == null) throw new ArgumentNullException(nameof(isObtainableInSeason));

        // Requirements by bundle name (first wins — names are unique per save in practice).
        var reqByName = new Dictionary<string, BundleRequirement>(StringComparer.Ordinal);
        foreach (BundleRequirement r in requirements)
            if (!reqByName.ContainsKey(r.Name))
                reqByName[r.Name] = r;

        bool perLine = kindOf != null && ThemeDomains.MatchesPerLine(theme);

        var pool = new List<BonusSlot>();
        foreach (KeyValuePair<string, string> kvp in bundleData)
        {
            ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
            if (!reqByName.TryGetValue(bundle.Name, out BundleRequirement? req)) continue;
            if (!perLine && req.Theme != theme) continue;

            var inPlay = new HashSet<string>(
                req.InPlayItemsFor(season, isObtainableInSeason), StringComparer.Ordinal);
            var due = new HashSet<string>(
                req.DueItemsFor(season, isObtainableInSeason), StringComparer.Ordinal);

            // Stretch lines (spec 2026-08-28-obtainable-board-2-stretch): once weekOfYear reaches
            // the stretch season's last week, the line is in play and due regardless of what the
            // obtainability predicate says (it would say no until the item's real pacing week).
            var stretchDue = new HashSet<string>(StringComparer.Ordinal);
            foreach (KeyValuePair<string, Season> kv in req.StretchLines)
                if (weekOfYear >= AvailabilityWeeks.LastWeekOf(kv.Value))
                    stretchDue.Add(kv.Key);
            if (stretchDue.Count > 0)
            {
                inPlay.UnionWith(stretchDue);
                due.UnionWith(stretchDue);
            }

            if (inPlay.Count == 0) continue;

            bool[]? state = slotStateForBundle(bundle.Index);

            // Bundle already complete (enough lines filled)? Remaining lines are dead.
            if (state != null)
            {
                int completed = 0;
                int lineCount = Math.Min(bundle.Ingredients.Count, state.Length);
                for (int i = 0; i < lineCount; i++)
                    if (state[i]) completed++;
                if (completed >= bundle.NumberOfSlots) continue;
            }

            for (int i = 0; i < bundle.Ingredients.Count; i++)
            {
                BundleIngredient ing = bundle.Ingredients[i];
                if (BundleParsing.IsCategoryRef(ing.ItemRef)) continue;
                string id = BundleParsing.NormalizeItemId(ing.ItemRef);
                if (!inPlay.Contains(id)) continue;
                if (perLine && !ThemeDomains.Matches(theme, kindOf!(id))) continue;
                if (state != null && i < state.Length && state[i]) continue;   // already donated

                pool.Add(new BonusSlot
                {
                    BundleIndex = bundle.Index,
                    IngredientIndex = i,
                    ItemId = id,
                    Stack = ing.Stack > 0 ? ing.Stack : 1,
                    Quality = ing.Quality,
                    BundleName = bundle.Name,
                    Due = due.Contains(id),
                    Stretch = stretchDue.Contains(id),
                    RouteTag = RouteTagFor(id, routeTagOf),
                });
            }
        }
        return pool;
    }

    /// <summary>A goal's Boost route (spec 2026-08-28-obtainable-board-4-boosts), or null when
    /// the item follows vanilla pacing. A year-2 crop always routes through Year-Two Seeds (or the
    /// permanent buy); a dish routes through Sneak Peek when its availability basis (routeTagOf)
    /// carries <see cref="Availability.CookedDishAvailability.SneakPeekBasisMarker"/>, the one
    /// place that note's text is defined.</summary>
    private static string? RouteTagFor(string id, Func<string, string?>? routeTagOf)
    {
        if (PoolAdditions.YearTwoCropIds.Contains(id)) return YearTwoSeedsRouteTag;
        string? basis = routeTagOf?.Invoke(id);
        return basis != null
               && basis.Contains(Availability.CookedDishAvailability.SneakPeekBasisMarker, StringComparison.Ordinal)
            ? SneakPeekRouteTag
            : null;
    }
}
