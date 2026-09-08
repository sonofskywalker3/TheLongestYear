using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Which recipes the Cookbook and Craftbook should offer to bank, and whether the loop
/// boundary should put the book in front of the player before the reset wipes their recipes.
///
/// Nexus post ada113, 2026-09-07: the books start at 0 slots, the first tier is bought at the
/// loop-boundary shrine, and the reset that follows wipes every learned recipe. So a book bought
/// at the shrine had nothing to bank by the time the player first opened it, and the perk was
/// dead for the loop it was bought in. The reset flow now opens each book after the shrine when
/// it has a free slot and there is something worth putting in it.</summary>
public static class RecipeBanking
{
    /// <summary>Recipes worth banking: known, not banked yet, and not one the game hands out on a
    /// new save anyway (those come back after every reset, so a slot spent on one is wasted).
    /// Sorted so the picker is stable.</summary>
    public static List<string> Bankable(
        IEnumerable<string> known, IReadOnlyCollection<string> banked, Func<string, bool> isDefault)
    {
        if (known == null) throw new ArgumentNullException(nameof(known));
        if (banked == null) throw new ArgumentNullException(nameof(banked));
        if (isDefault == null) throw new ArgumentNullException(nameof(isDefault));
        var already = new HashSet<string>(banked, StringComparer.Ordinal);
        return known
            .Where(id => !already.Contains(id) && !isDefault(id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Open the book at the loop boundary only when it can take something: a free slot
    /// and at least one bankable recipe. A full book, an unbought one (0 slots) or a player who
    /// only knows the starter recipes gets no interruption.</summary>
    public static bool ShouldOfferAtReset(int slotCount, int bankedCount, int bankableCount)
        => slotCount > bankedCount && bankableCount > 0;
}
