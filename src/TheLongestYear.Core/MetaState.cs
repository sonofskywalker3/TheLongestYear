using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>
/// Everything that survives a loop reset ("banked forever"): banked Junimo Points,
/// purchased upgrades, the Junimo Stash tier, and meta-condition accumulators
/// (e.g. animal species the player has ever owned across runs). Stored as per-save data and
/// committed with the game's own save (see MetaStore) — scoped to one playthrough.
/// </summary>
public sealed class MetaState
{
    public long JunimoPoints { get; set; }

    /// <summary>IDs of permanently purchased upgrades.</summary>
    public List<string> OwnedUpgrades { get; set; } = new();

    /// <summary>Tier of the Junimo Stash capacity upgrade (0 = base).</summary>
    public int StashCapacityTier { get; set; }

    /// <summary>True once the one-time pre-first-reset save backup has been taken (banked forever).</summary>
    public bool BackupDone { get; set; }

    /// <summary>
    /// Animal species the player has ever owned across all runs in this playthrough.
    /// Drives "Start with [animal]" upgrade availability via the species: meta-requirement
    /// prefix on <see cref="UpgradeDefinition.MetaRequirement"/>. Game-side hookup that
    /// adds to this list when an animal joins a coop/barn is part of a later plan.
    /// </summary>
    public List<string> AnimalSpeciesEverOwned { get; set; } = new();

    public bool HasUpgrade(string id) => OwnedUpgrades.Contains(id);

    /// <summary>
    /// Evaluate a meta-requirement string against current banked state. Format is "ns:value";
    /// only "species:&lt;name&gt;" is recognised today. Unknown namespaces return false (treated
    /// as "requirement not met") so future-added requirements default-deny on older code paths.
    /// </summary>
    public bool MeetsMetaRequirement(string? requirement)
    {
        if (string.IsNullOrEmpty(requirement))
            return true;
        int colon = requirement.IndexOf(':');
        if (colon <= 0)
            return false;
        string ns = requirement.Substring(0, colon);
        string value = requirement.Substring(colon + 1);
        return ns switch
        {
            "species" => AnimalSpeciesEverOwned.Contains(value, StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }
}
