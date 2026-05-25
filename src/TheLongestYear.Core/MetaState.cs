using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Everything that survives a loop reset ("banked forever").
/// Persisted via SMAPI global data, so it lives outside any single save.
/// </summary>
public sealed class MetaState
{
    public long JunimoPoints { get; set; }

    /// <summary>IDs of permanently purchased upgrades.</summary>
    public List<string> OwnedUpgrades { get; set; } = new();

    /// <summary>Tier of the Junimo Stash capacity upgrade (0 = base).</summary>
    public int StashCapacityTier { get; set; }

    public bool HasUpgrade(string id) => OwnedUpgrades.Contains(id);
}
