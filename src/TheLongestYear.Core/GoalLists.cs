using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The two goal lists of a double theme week (spec section 6). Activity themes and Mixed
/// take single lines across any bundle, so both cards can sample the same Community Center line.</summary>
public static class GoalLists
{
    /// <summary>The second card's goals without any (bundle, ingredient) line the first card already
    /// owns, in their original order. The first card always keeps a shared line.</summary>
    public static List<BonusSlot> Dedupe(IReadOnlyList<BonusSlot>? first, IReadOnlyList<BonusSlot>? second)
    {
        var owned = new HashSet<(int, int)>();
        if (first != null)
            foreach (BonusSlot s in first)
                if (s != null) owned.Add((s.BundleIndex, s.IngredientIndex));

        var kept = new List<BonusSlot>();
        if (second == null) return kept;
        foreach (BonusSlot s in second)
            if (s != null && owned.Add((s.BundleIndex, s.IngredientIndex)))
                kept.Add(s);
        return kept;
    }
}
