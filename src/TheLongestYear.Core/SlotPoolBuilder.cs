using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Builds the weekly-theme goal pool: every OPEN, in-play, concrete ingredient slot of the
/// theme's bundles. Pure — live game data comes in as plain inputs (bundle data dict + a
/// per-bundle slot-state accessor), so it unit-tests without Game1.
///
/// Rules (spec 2026-07-09):
///   - Bundle must classify to a BundleRequirement (matched by bundle name) with the requested
///     theme, and have in-play items this season (same gating as BundleRequirement.InPlayItemsFor:
///     Seasonal in its season / PerItem pins / Percentage non-zero quota + obtainability).
///   - A bundle that already has NumberOfSlots completed ingredient lines is complete — its
///     remaining lines can no longer be donated and are excluded.
///   - Category refs and completed slots are excluded. Null slot state ⇒ all lines open.
/// </summary>
public static class SlotPoolBuilder
{
    public static IReadOnlyList<BonusSlot> OpenSlotsForTheme(
        IReadOnlyDictionary<string, string> bundleData,
        Func<int, bool[]?> slotStateForBundle,
        IReadOnlyList<BundleRequirement> requirements,
        Theme theme, Season season,
        Func<string, bool> isObtainableInSeason)
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

        var pool = new List<BonusSlot>();
        foreach (KeyValuePair<string, string> kvp in bundleData)
        {
            ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
            if (!reqByName.TryGetValue(bundle.Name, out BundleRequirement? req)) continue;
            if (req.Theme != theme) continue;

            var inPlay = new HashSet<string>(
                req.InPlayItemsFor(season, isObtainableInSeason), StringComparer.Ordinal);
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
                if (state != null && i < state.Length && state[i]) continue;   // already donated

                pool.Add(new BonusSlot
                {
                    BundleIndex = bundle.Index,
                    IngredientIndex = i,
                    ItemId = id,
                    Stack = ing.Stack > 0 ? ing.Stack : 1,
                    Quality = ing.Quality,
                    BundleName = bundle.Name,
                });
            }
        }
        return pool;
    }
}
