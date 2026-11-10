using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What the banked JP buys of the pinned keeps: <see cref="Affordable"/> of the
/// <see cref="Total"/> unlocked next tiers, <see cref="ShortfallJp"/> more JP for the next one
/// (0 when all are affordable), and <see cref="Locked"/> pinned chains whose next tier is gated.</summary>
public readonly record struct PinnedBudgetResult(int Affordable, int Total, int Locked, long ShortfallJp);

/// <summary>
/// The pinned budget line on both shrine menus (spec 2026-11-10 section 4, amendment H). Each pinned
/// chain offers its next unowned tier from the catalog (not the visible rows). A tier that fails any
/// gate but JP (prerequisite, meta requirement, run reach, animal room) counts as locked; a fully
/// owned chain and an unknown id drop out. The unlocked tiers are bought on paper, cheapest first at
/// <see cref="UpgradePricing.EffectiveCost(UpgradeDefinition, double, MetaState)"/>, against the
/// banked JP, re-pricing after each buy so Gifts of the Junimos climb their shared ladder. The real
/// state is never changed: the paper buys go into a scratch copy of the owned list.
/// </summary>
public static class PinnedBudget
{
    public static PinnedBudgetResult Compute(MetaState state, double priceFactor, Func<string?, bool> reachMet)
    {
        var candidates = new List<UpgradeDefinition>();
        int locked = 0;
        var seenRoots = new HashSet<string>(StringComparer.Ordinal);
        foreach (string pin in state.PinnedUpgrades)
        {
            if (UpgradeCatalog.TryGet(pin) == null || !seenRoots.Add(PinnedKeeps.ChainRoot(pin)))
                continue;
            UpgradeDefinition? next = KeepLadder.NextUnowned(pin, state);
            if (next == null)
                continue;
            if (KeepShopFilter.IsBuyable(next, state, reachMet)) candidates.Add(next);
            else locked++;
        }

        var paper = new MetaState { OwnedUpgrades = new List<string>(state.OwnedUpgrades) };
        long jp = state.JunimoPoints;
        int affordable = 0;
        long shortfall = 0;
        var left = candidates.OrderBy(CatalogIndex).ToList();
        while (left.Count > 0)
        {
            // Cheapest at today's paper prices; ties keep catalog order (OrderBy is stable).
            UpgradeDefinition cheapest = left.OrderBy(d => UpgradePricing.EffectiveCost(d, priceFactor, paper)).First();
            long cost = UpgradePricing.EffectiveCost(cheapest, priceFactor, paper);
            if (cost > jp)
            {
                shortfall = cost - jp;
                break;
            }
            jp -= cost;
            paper.OwnedUpgrades.Add(cheapest.Id);
            left.Remove(cheapest);
            affordable++;
        }
        return new PinnedBudgetResult(affordable, candidates.Count, locked, shortfall);
    }

    /// <summary>The line's text, one entry per drawn line: nothing when no pin is left to buy.</summary>
    public static IReadOnlyList<string> Lines(PinnedBudgetResult budget)
    {
        var lines = new List<string>();
        if (budget.Total == 0)
        {
            if (budget.Locked > 0)
                lines.Add(Strings.Get(Key("menu.shrine.budget.all-locked", budget.Locked), new Dictionary<string, string> { ["locked"] = budget.Locked.ToString() }));
            return lines;
        }
        lines.Add(budget.Affordable == budget.Total
            ? Strings.Get(Key("menu.shrine.budget.all", budget.Total), new Dictionary<string, string> { ["total"] = budget.Total.ToString() })
            : Strings.Get(Key("menu.shrine.budget.some", budget.Total), new Dictionary<string, string>
            {
                ["count"] = budget.Affordable.ToString(),
                ["total"] = budget.Total.ToString(),
                ["jp"] = budget.ShortfallJp.ToString(),
            }));
        if (budget.Locked > 0)
            lines.Add(Strings.Get(Key("menu.shrine.budget.locked", budget.Locked), new Dictionary<string, string> { ["locked"] = budget.Locked.ToString() }));
        return lines;
    }

    /// <summary>The ".one" wording when the count is one: "your pinned upgrade", not "1 pinned upgrades".</summary>
    private static string Key(string key, int count) => count == SingleCount ? key + SingularSuffix : key;

    private const int SingleCount = 1;
    private const string SingularSuffix = ".one";

    private static int CatalogIndex(UpgradeDefinition def)
    {
        IReadOnlyList<UpgradeDefinition> all = UpgradeCatalog.All;
        for (int i = 0; i < all.Count; i++)
            if (ReferenceEquals(all[i], def)) return i;
        return int.MaxValue;
    }
}
