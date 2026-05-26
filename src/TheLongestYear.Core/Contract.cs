using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// One (season, theme) contract. Spec 2026-05-26 round 5: gate is now pool-count based, not
/// per-item.
/// <list type="bullet">
///   <item><see cref="RequiredItemIds"/> is the POOL of donatable items for this contract (the
///         per-item pinning from <see cref="ContractGenerator"/>). Donating any of them counts
///         toward the gate.</item>
///   <item><see cref="GateRequirement"/> is HOW MANY pool items the player must donate to clear
///         the contract by season-end (typically 2-4 by season).</item>
///   <item><see cref="BonusItemIds"/> is a per-run random subset of the pool shown on the
///         planning hub. Donating a bonus item during its championed week pays bonus JP via
///         the championship multiplier — NOT a gate requirement.</item>
/// </list>
/// </summary>
public sealed class Contract
{
    public Season Season { get; }
    public Theme Theme { get; }

    /// <summary>The full item pool for this contract (every CC item pinned to this season+theme).</summary>
    public IReadOnlyList<string> RequiredItemIds { get; }

    /// <summary>How many pool items the player must donate by season-end to clear the gate.
    /// Capped at <c>RequiredItemIds.Count</c> so a pool smaller than the configured N
    /// stays satisfiable.</summary>
    public int GateRequirement { get; }

    /// <summary>A random subset of the pool shown on the planning hub as bonus-JP items.
    /// Re-rolls per run (the generator runs on every reset).</summary>
    public IReadOnlyList<string> BonusItemIds { get; }

    public string BonusId { get; }
    public string LiabilityId { get; }

    public Contract(
        Season season,
        Theme theme,
        IEnumerable<string> poolItemIds,
        int gateRequirement,
        IEnumerable<string> bonusItemIds,
        string bonusId,
        string liabilityId)
    {
        Season = season;
        Theme = theme;
        RequiredItemIds = poolItemIds?.ToList() ?? throw new ArgumentNullException(nameof(poolItemIds));
        BonusItemIds = (bonusItemIds ?? Array.Empty<string>()).ToList();
        BonusId = bonusId ?? throw new ArgumentNullException(nameof(bonusId));
        LiabilityId = liabilityId ?? throw new ArgumentNullException(nameof(liabilityId));

        if (gateRequirement < 0)
            throw new ArgumentOutOfRangeException(nameof(gateRequirement), gateRequirement, "Must be non-negative.");
        // Don't ask for more donations than the pool has — protects tiny pools (e.g. Winter Mining=2).
        GateRequirement = Math.Min(gateRequirement, RequiredItemIds.Count);
    }

    /// <summary>True when at least <see cref="GateRequirement"/> distinct pool items are present
    /// in the player's donation ledger. Empty pool / zero requirement is trivially satisfied.</summary>
    public bool IsSatisfiedBy(ISet<string> donatedItemIds)
    {
        if (GateRequirement == 0) return true;
        int count = 0;
        foreach (string id in RequiredItemIds)
            if (donatedItemIds.Contains(id) && ++count >= GateRequirement)
                return true;
        return false;
    }
}
