using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One unowned tier of a keep chain and what it costs today.</summary>
public sealed record KeepLadderTier(UpgradeDefinition Def, long Cost);

/// <summary>A tiered keep's ladder for its tooltip: how many tiers are owned, the next few unowned
/// tiers with today's price, and what every unowned tier costs together.</summary>
public sealed record KeepLadderView(int Owned, int Total, IReadOnlyList<KeepLadderTier> Next, long LeftToBuy);

/// <summary>
/// Keep chains read from the catalog. Prerequisites can branch (Keep Coop leads to Keep Big Coop and
/// to Start with Chicken), so a keep's ladder is its own linear path: every prerequisite back to the
/// first tier, the keep itself, then forward only along the chain's main line
/// (<see cref="NextInChain"/>). A branch off the main line is its own ladder, rooted at the branch row
/// (<see cref="PinnedKeeps.ChainRoot"/>). Pure, shared by the tier ladder in the shrine tooltips and
/// the pinned budget line (<see cref="PinnedBudget"/>).
/// </summary>
public static class KeepLadder
{
    /// <summary>Unowned tiers the tooltip names before the "left to buy" total.</summary>
    public const int ShownTiers = 3;

    /// <summary>Longest successor walk before giving up (guards a malformed cycle).</summary>
    private const int MaxChainLength = 64;

    /// <summary>A meta requirement with this prefix marks a "Start with [animal]" row: it hangs off a
    /// housing tier as a branch and is never the next tier of the housing chain.</summary>
    private const string SideRowRequirementPrefix = "species:";

    /// <summary>Catalog entries by prerequisite id, catalog order kept.</summary>
    private static readonly IReadOnlyDictionary<string, List<UpgradeDefinition>> ByPrerequisite =
        UpgradeCatalog.All
            .Where(d => d.PrerequisiteId != null)
            .GroupBy(d => d.PrerequisiteId!, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

    /// <summary>Every tier of <paramref name="id"/>'s ladder, first tier first: its prerequisites back
    /// to the start, the keep itself, then its main-line successors. Empty for an unknown id or a
    /// ladder with a tier that is not offered (its mod is missing).</summary>
    public static IReadOnlyList<UpgradeDefinition> Tiers(string id)
    {
        var tiers = new List<UpgradeDefinition>();
        UpgradeDefinition? current = UpgradeCatalog.TryGet(id);
        while (current != null && tiers.Count < MaxChainLength)
        {
            if (!UpgradeCatalog.IsOffered(current))
                return Array.Empty<UpgradeDefinition>();
            tiers.Add(current);
            current = current.PrerequisiteId == null ? null : UpgradeCatalog.TryGet(current.PrerequisiteId);
        }
        tiers.Reverse();
        current = tiers.Count == 0 ? null : NextInChain(id);
        while (current != null && tiers.Count < MaxChainLength)
        {
            tiers.Add(current);
            current = NextInChain(current.Id);
        }
        return tiers;
    }

    /// <summary>The main-line successor of <paramref name="id"/>: its only offered successor that is
    /// not a side row. Null at the end of a chain and at a fork with no single main line (Keep Horse
    /// leads to two separate keeps), where every successor starts its own ladder.</summary>
    public static UpgradeDefinition? NextInChain(string id)
    {
        if (!ByPrerequisite.TryGetValue(id, out List<UpgradeDefinition>? next))
            return null;
        List<UpgradeDefinition> main = next.Where(d => UpgradeCatalog.IsOffered(d) && !IsSideRow(d)).ToList();
        return main.Count == 1 ? main[0] : null;
    }

    /// <summary>The first unowned tier of <paramref name="id"/>'s chain, or null when the chain is
    /// fully owned or unknown.</summary>
    public static UpgradeDefinition? NextUnowned(string id, MetaState state)
        => Tiers(id).FirstOrDefault(d => !state.HasUpgrade(d.Id));

    /// <summary>The ladder for a tiered keep, or null for a single keep (no ladder to show).
    /// Prices are today's (<see cref="UpgradePricing.EffectiveCost(UpgradeDefinition, double, MetaState)"/>).</summary>
    public static KeepLadderView? For(string id, MetaState state, double priceFactor)
    {
        IReadOnlyList<UpgradeDefinition> tiers = Tiers(id);
        if (tiers.Count < 2)
            return null;
        var unowned = tiers.Where(d => !state.HasUpgrade(d.Id))
            .Select(d => new KeepLadderTier(d, UpgradePricing.EffectiveCost(d, priceFactor, state)))
            .ToList();
        return new KeepLadderView(tiers.Count - unowned.Count, tiers.Count,
            unowned.Take(ShownTiers).ToList(), unowned.Sum(t => t.Cost));
    }

    /// <summary>The tooltip lines: "Owned 2 of 4 tiers", up to <see cref="ShownTiers"/> next tiers,
    /// then the total left to buy when anything is left.</summary>
    public static IEnumerable<string> Lines(KeepLadderView ladder)
    {
        yield return Strings.Get("menu.shrine.ladder.owned", new Dictionary<string, string>
        {
            ["owned"] = ladder.Owned.ToString(),
            ["total"] = ladder.Total.ToString(),
        });
        foreach (KeepLadderTier tier in ladder.Next)
            yield return Strings.Get("menu.shrine.ladder.tier", new Dictionary<string, string>
            {
                ["name"] = tier.Def.DisplayName,
                ["jp"] = tier.Cost.ToString(),
            });
        if (ladder.Next.Count > 0)
            yield return Strings.Get("menu.shrine.ladder.left", new Dictionary<string, string> { ["jp"] = ladder.LeftToBuy.ToString() });
    }

    /// <summary>The ladder tooltip block for <paramref name="id"/>, starting with a line break, or
    /// "" for a single keep. Appended after the keep's description.</summary>
    public static string TooltipBlock(string? id, MetaState state, double priceFactor)
    {
        if (id == null || For(id, state, priceFactor) is not KeepLadderView ladder)
            return "";
        return "\n" + string.Join("\n", Lines(ladder));
    }

    private static bool IsSideRow(UpgradeDefinition def)
        => def.MetaRequirement?.StartsWith(SideRowRequirementPrefix, StringComparison.Ordinal) == true;
}
