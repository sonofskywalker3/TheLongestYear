using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Pure derivation of "which concrete ingredient ids has the player actually deposited into the
/// CC bundles", read straight from the vanilla bundle definitions + per-slot completion state.
///
/// <para>
/// This is the day-end backstop for the live <c>DonationObserver</c>, which only records donations
/// it witnesses while the <c>JunimoNoteMenu</c> is open and can miss a deposit (menu closed on the
/// same tick, a path it didn't scan). A missed donation never enters <see cref="RunState"/>'s
/// ledger, so the season gate can read "failed" even though vanilla shows the bundle complete
/// (beta report, khauser13: completed every goal but the run reset). Reconciling from the game's
/// own authoritative slot state fixes that — mirroring how <c>VaultPaymentSync</c> already reconciles
/// the vault ledger from <c>isBundleComplete</c>.
/// </para>
///
/// <para>
/// Id derivation matches <c>BundleCatalogBuilder</c> exactly so the produced ids line up with the
/// gate's <c>BundleRequirement.Ingredients</c>: non-item rooms (Vault / Abandoned Joja) are skipped,
/// category ingredients are skipped (the gate skips them too), and ids are normalized via
/// <see cref="BundleParsing.NormalizeItemId"/>. Crucially, slots are walked positionally against the
/// completion array, so skipping a category slot does NOT shift the concrete slots' ids.
/// </para>
/// </summary>
public static class CcDonationReconciler
{
    /// <summary>
    /// Yield the normalized concrete ingredient id of every completed slot across all themed
    /// (item-room) bundles. <paramref name="slotCompletionForIndex"/> maps a bundle's global index
    /// to its per-slot completion array (1:1 with the bundle's ingredient list), or null if absent.
    /// </summary>
    public static IEnumerable<string> DonatedConcreteIds(
        IReadOnlyDictionary<string, string>? bundleData,
        Func<int, bool[]?>? slotCompletionForIndex)
    {
        if (bundleData is null || slotCompletionForIndex is null)
            yield break;

        foreach (KeyValuePair<string, string> kvp in bundleData)
        {
            ParsedBundle bundle = BundleParsing.Parse(kvp.Key, kvp.Value);
            if (!RoomThemeMap.TryGetTheme(bundle.Room, out _))
                continue; // Vault / Joja / unknown room — not an item gate.

            bool[]? completed = slotCompletionForIndex(bundle.Index);
            if (completed is null)
                continue;

            IReadOnlyList<BundleIngredient> ingredients = bundle.Ingredients;
            for (int i = 0; i < ingredients.Count; i++)
            {
                if (i >= completed.Length || !completed[i])
                    continue;
                string itemRef = ingredients[i].ItemRef;
                if (BundleParsing.IsCategoryRef(itemRef))
                    continue; // category slot — not represented in the gate's ingredient ids.
                yield return BundleParsing.NormalizeItemId(itemRef);
            }
        }
    }
}
