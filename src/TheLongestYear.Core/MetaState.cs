using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Everything that survives a loop reset ("banked forever"): banked Junimo Points,
/// purchased upgrades, and the Junimo Stash tier. Stored as per-save data and
/// committed with the game's own save (see MetaStore) — scoped to one playthrough.
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
