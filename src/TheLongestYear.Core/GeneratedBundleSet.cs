using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>A complete engine-generated bundle set for one loop, plus its requirement
/// MANIFEST. The manifest is built by round-tripping each spec through the same
/// writer→parser pair the game will see and classifying with the existing
/// BundleClassifier — so classification can never drift from the written data, and
/// (because the engine authored every bundle) nothing themed is ever skipped. The
/// season-gate clamp then guarantees no season's cumulative quota demands more slots
/// than are obtainable by that season's end (spec 2026-07-14, user-ruled safety rule).</summary>
public sealed class GeneratedBundleSet
{
    public IReadOnlyList<BundleSpec> Bundles { get; }

    /// <summary>The input each flavored slot names, keyed <see cref="FlavoredSlotPass.KeyFor"/>
    /// ("bundleIndex:slotIndex") to a qualified item id. Empty when no slot on the board is a
    /// Dried Fruit, Dried Mushrooms or Smoked Fish ask.
    ///
    /// This rides ALONGSIDE the bundle data rather than inside it: vanilla's ingredients are
    /// (id, stack, quality) triples with nowhere to put a flavor, and putting one there would
    /// break the byte-for-byte manifest check. The mod persists this map next to
    /// MetaState.WrittenBoard and sets it on the live bundle at load.</summary>
    public IReadOnlyDictionary<string, string> Flavors { get; }

    public GeneratedBundleSet(IReadOnlyList<BundleSpec> bundles, IReadOnlyDictionary<string, string>? flavors = null)
    {
        Bundles = bundles;
        Flavors = flavors ?? new Dictionary<string, string>();
    }

    public IReadOnlyDictionary<string, string> ToBundleData() =>
        Bundles.ToDictionary(BundleDataWriter.Key, BundleDataWriter.Value);

    /// <summary>Classify every generated bundle into a requirement manifest.</summary>
    /// <param name="availability">Derived item model, forwarded to the classifier so PerItem
    /// bundles get a computed deadline per ingredient. Null keeps the legacy pin-table path;
    /// see <see cref="BundleClassifier.Classify"/>. The obtainability clamp below still uses
    /// <paramref name="itemSeasonPins"/>, which is a separate question from the due dates.</param>
    public IReadOnlyList<BundleRequirement> BuildRequirements(
        IReadOnlyDictionary<string, Season> itemSeasonPins,
        IReadOnlyDictionary<string, int[]> bundleQuotas,
        ItemAvailabilityModel? availability = null)
    {
        // One classification loop for the generator and the stored-board path: the set is
        // reduced to the exact bundle strings the game will see (writer -> parser), so what is
        // classified can never drift from what was written.
        return BoardRequirements.Build(ToBundleData(), itemSeasonPins, bundleQuotas, availability);
    }

    /// <summary>ramp[s] may never exceed the number of ingredient slots obtainable by the
    /// end of season s (un-pinned ingredients count as Spring-obtainable). The Winter value
    /// keeps demanding min(numberOfSlots, obtainable-ever) so completion is still required.
    /// Result stays monotonic non-decreasing.</summary>
    /// <param name="stretchLines">Stretch lines (spec 2026-08-28-obtainable-board-2-stretch): an id
    /// with a stretch line counts as obtainable from its stretch season, exactly as
    /// <see cref="BundleClassifier.RampFromItems"/> reads them. Without this the clamp reads the
    /// pin table alone and flattens the very bump the stretch line was placed to create.</param>
    public static int[] ClampRampForObtainability(
        int[] cumulativeRamp, IReadOnlyList<string> ingredients, int numberOfSlots,
        IReadOnlyDictionary<string, Season> pins,
        IReadOnlyDictionary<string, Season>? stretchLines = null)
    {
        var clamped = new int[cumulativeRamp.Length];
        for (int s = 0; s < cumulativeRamp.Length; s++)
        {
            int obtainable = ingredients.Count(id =>
                !pins.TryGetValue(id, out Season pinned) || (int)pinned <= s
                || (stretchLines != null && stretchLines.TryGetValue(id, out Season stretch) && (int)stretch <= s));
            // Two separate ceilings, and BOTH matter. How many ingredients are obtainable by this
            // season is one of them; how many the bundle can physically take is the other. A
            // pick-X-of-Y bundle has Y ingredients but only X slots, so clamping by obtainable
            // ingredients alone can leave a quota of Y standing against X fillable slots. That is
            // Nexus 1137357: a pick-8-of-9 bundle whose gate wanted 9, so a fully green bundle
            // still failed its season because there was nowhere to put the ninth donation.
            clamped[s] = Math.Min(Math.Min(cumulativeRamp[s], obtainable), numberOfSlots);
        }
        int last = clamped.Length - 1;
        int obtainableEver = ingredients.Count; // by Winter every pin has passed
        clamped[last] = Math.Max(clamped[last], Math.Min(numberOfSlots, obtainableEver));
        for (int s = 1; s < clamped.Length; s++)
            clamped[s] = Math.Max(clamped[s], clamped[s - 1]);
        return clamped;
    }
}
