using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Pure visibility rules for the Junimo Shrine keep shop, shared by the purchase menu and the
/// read-only preview. A keep is BUYABLE when it is not owned, its chain prerequisite is owned,
/// its cross-run MetaRequirement is met, AND its in-run RunReachRequirement is met. The reach
/// check is injected as a delegate so this stays pure/testable (the glue passes a live evaluator).
/// </summary>
public static class KeepShopFilter
{
    /// <summary>True if <paramref name="def"/> should be offered for purchase right now.</summary>
    public static bool IsBuyable(UpgradeDefinition def, MetaState state, Func<string?, bool> reachMet)
    {
        if (state.HasUpgrade(def.Id))
            return false;
        if (def.PrerequisiteId != null && !state.HasUpgrade(def.PrerequisiteId))
            return false;
        if (!state.MeetsMetaRequirement(def.MetaRequirement))
            return false;
        if (def.RunReachRequirement != null && !reachMet(def.RunReachRequirement))
            return false;
        return true;
    }

    /// <summary>The buyable keeps in a category, catalog order preserved.</summary>
    public static IReadOnlyList<UpgradeDefinition> BuyableInCategory(
        UpgradeCategory category, MetaState state, Func<string?, bool> reachMet)
    {
        var visible = new List<UpgradeDefinition>();
        foreach (UpgradeDefinition def in UpgradeCatalog.ByCategory(category))
            if (IsBuyable(def, state, reachMet))
                visible.Add(def);
        return visible;
    }
}
