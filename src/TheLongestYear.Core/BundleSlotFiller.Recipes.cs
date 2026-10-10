using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

public static partial class BundleSlotFiller
{
    /// <summary>Rolls a recipe bundle part by part, in the recipe's own fixed order so the rng
    /// stream is deterministic: each part draws from its own candidates minus the avoid set and
    /// minus what earlier parts already took, filling its Count slots (or, at Count 0, the rest).
    /// A part that cannot fill falls back to the whole recipe's candidates, which already carry
    /// the bundle's own vanilla items.</summary>
    private static List<PoolItem> SampleByParts(
        BundleSpec spec, PoolRecipe recipe, IReadOnlyList<IReadOnlyList<PoolItem>> parts,
        IReadOnlyList<PoolItem> union, int targetCount, Random rng,
        IReadOnlySet<string>? avoid, Action<string>? log)
    {
        var chosen = new List<PoolItem>(targetCount);
        var taken = new HashSet<string>(StringComparer.Ordinal);

        void Take(IReadOnlyList<PoolItem> from, int count)
        {
            foreach (PoolItem pick in WeightedSampler.Sample(from, count, rng))
            {
                chosen.Add(pick);
                taken.Add(pick.ItemId);
            }
        }

        List<PoolItem> Free(IEnumerable<PoolItem> from, bool dropAvoided)
            => from.Where(p => !taken.Contains(p.ItemId)
                               && !(dropAvoided && avoid != null && avoid.Contains(p.ItemId))).ToList();

        for (int i = 0; i < parts.Count && chosen.Count < targetCount; i++)
        {
            int remaining = targetCount - chosen.Count;
            int want = recipe.Parts[i].Count <= BundlePoolRecipes.RestOfTheSlots
                ? remaining
                : Math.Min(recipe.Parts[i].Count, remaining);

            List<PoolItem> available = Free(parts[i], dropAvoided: true);
            if (available.Count < want)
                available = Free(parts[i], dropAvoided: false); // no fresh item left: allow repeats
            if (available.Count < want)
            {
                log?.Invoke($"'{spec.Name}': part {recipe.Parts[i].Label} short by {want - available.Count}; falling back to the vanilla items");
                available = Free(union, dropAvoided: false);
            }
            Take(available, want);
        }

        // Fewer parts than slots, or a fixed-count part that ran dry: the rest of the bundle comes
        // from the recipe's whole candidate list.
        if (chosen.Count < targetCount)
        {
            List<PoolItem> rest = Free(union, dropAvoided: true);
            if (rest.Count < targetCount - chosen.Count)
                rest = Free(union, dropAvoided: false);
            Take(rest, targetCount - chosen.Count);
        }
        return chosen;
    }

    /// <summary>The domain a Recipe bundle rolls stack and quality with: the one its dominant
    /// part maps to, when that part is fish, crops or forage. Everything else (gems, artifacts,
    /// cooking, books, trash...) asks for one plain item, which is exactly what
    /// <see cref="PoolDomain.None"/> gives <see cref="RollStack"/> and <see cref="RollQuality"/>.
    ///
    /// The dominant part is the one filling the most slots; a "rest of the slots" part takes
    /// whatever the fixed-count parts leave. Ties keep the earlier part, so the choice is
    /// deterministic in the recipe's own fixed order.</summary>
    public static PoolDomain RecipeRollDomain(PoolRecipe recipe, int targetCount)
    {
        if (recipe == null || recipe.Parts.Count == 0)
            return PoolDomain.None;

        int fixedSlots = recipe.Parts
            .Where(p => p.Count > BundlePoolRecipes.RestOfTheSlots)
            .Sum(p => p.Count);
        int rest = Math.Max(0, targetCount - Math.Min(fixedSlots, targetCount));

        PoolPart? dominant = null;
        int best = -1;
        foreach (PoolPart part in recipe.Parts)
        {
            int size = part.Count <= BundlePoolRecipes.RestOfTheSlots ? rest : part.Count;
            if (size > best) { best = size; dominant = part; }
        }
        return dominant == null ? PoolDomain.None : DomainForLabel(dominant.Label);
    }

    /// <summary>The pool domain a recipe part's label names, for the stack/quality roll only.
    /// Anything else is None: a plain single item. The Crop arm is unreached today (no recipe part
    /// carries that label); it stands so a crop part added later rolls crop quality by default.</summary>
    private static PoolDomain DomainForLabel(string label)
        => label switch
        {
            "Fish" => PoolDomain.Fish,
            "Forage" => PoolDomain.SeasonalForage,
            "Crop" or "Crops" => PoolDomain.SeasonalCrops,
            _ => PoolDomain.None,
        };

    /// <summary>The recipe a bundle re-rolls from, for the engine's diagnostics
    /// (<c>tly_genbundles</c> prints its name and parts). Same call <see cref="Fill"/> makes, so
    /// the report cannot drift from what actually rolled.</summary>
    public static PoolRecipe RecipeFor(BundleSpec spec, ItemPools pools, ItemAvailabilityModel? availability)
        => BundlePoolRecipes.For(spec.Name, VanillaIds(spec), pools, availability);

    /// <summary>The bundle's own item ids, money and category refs left out.</summary>
    private static IReadOnlyList<string> VanillaIds(BundleSpec spec)
        => spec.Slots
            .Where(s => !string.IsNullOrEmpty(s.ItemId) && s.ItemId != MoneySlotId
                        && !BundleParsing.IsCategoryRef(s.ItemId))
            .Select(s => BundleParsing.NormalizeItemId(s.ItemId))
            .ToList();
}
