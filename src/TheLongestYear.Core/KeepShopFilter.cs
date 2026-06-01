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

    /// <summary>
    /// The OWNED keeps in a category that should be shown as a green "you already have this"
    /// confirmation row (no cost). A keep qualifies when it is owned, it is the TOP owned tier
    /// in its chain (its successor isn't also owned), AND its successor isn't currently buyable
    /// (a buyable next-tier row already implies the lower tier is owned, so we don't double up).
    /// Standalone owned keeps (no successor) always qualify. Catalog order preserved.
    /// </summary>
    public static IReadOnlyList<UpgradeDefinition> OwnedLeavesInCategory(
        UpgradeCategory category, MetaState state, Func<string?, bool> reachMet)
    {
        var leaves = new List<UpgradeDefinition>();
        foreach (UpgradeDefinition def in UpgradeCatalog.ByCategory(category))
        {
            if (!state.HasUpgrade(def.Id))
                continue;
            UpgradeDefinition? successor = FindSuccessor(category, def.Id);
            if (successor != null && state.HasUpgrade(successor.Id))
                continue;                                       // a higher tier is owned — not the leaf
            if (successor != null && IsBuyable(successor, state, reachMet))
                continue;                                       // next tier already shown as buyable
            leaves.Add(def);
        }
        return leaves;
    }

    /// <summary>The next tier in a chain: the catalog entry whose prerequisite is <paramref name="id"/>.
    /// Chains are linear within a category, so the first match is the unique successor (null if none).</summary>
    private static UpgradeDefinition? FindSuccessor(UpgradeCategory category, string id)
    {
        foreach (UpgradeDefinition def in UpgradeCatalog.ByCategory(category))
            if (def.PrerequisiteId == id)
                return def;
        return null;
    }
}
