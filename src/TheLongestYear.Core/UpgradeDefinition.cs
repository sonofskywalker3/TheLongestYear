using System;

namespace TheLongestYear.Core;

/// <summary>
/// One row in the upgrade shop. Effects of an upgrade at run-start (e.g. starting backpack tier,
/// retained XP, Mixed Seeds injection) are applied in a later plan; Plan 05 only records
/// the purchase into <see cref="MetaState.OwnedUpgrades"/>.
///
/// Prerequisites are two-tier:
///   - <see cref="PrerequisiteId"/>: a specific upgrade that must already be owned
///     (e.g. "Keep Big Coop" requires "Keep Coop").
///   - <see cref="MetaRequirement"/>: an arbitrary meta-state condition the player must satisfy.
///     Format is "namespace:value"; currently only "species:&lt;name&gt;" is recognised, gating
///     "Start with [animal]" upgrades on having ever owned that species across runs. The format
///     is intentionally generic so Longest Year 2/3 expansions can add new conditions
///     (e.g. "completed:Ginger Island") without schema changes.
/// </summary>
public sealed class UpgradeDefinition
{
    public string Id { get; }
    public UpgradeCategory Category { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public long Cost { get; }

    /// <summary>Id of the upgrade that must already be owned, or null if no prerequisite.</summary>
    public string? PrerequisiteId { get; }

    /// <summary>Meta-state condition ("species:Chicken") the player must satisfy, or null if none.</summary>
    public string? MetaRequirement { get; }

    /// <summary>Live in-run reach gate (e.g. "tool:watering_can:2"), or null if the upgrade is
    /// not reach-gated. Parsed/evaluated separately (see RunReachRequirement + RunReachEvaluator).</summary>
    public string? RunReachRequirement { get; }

    public UpgradeDefinition(
        string id,
        UpgradeCategory category,
        string displayName,
        string description,
        long cost,
        string? prerequisiteId = null,
        string? metaRequirement = null,
        string? runReachRequirement = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id must be non-empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("DisplayName must be non-empty.", nameof(displayName));
        if (cost < 0)
            throw new ArgumentOutOfRangeException(nameof(cost), cost, "Cost must be non-negative.");

        Id = id;
        Category = category;
        DisplayName = displayName;
        Description = description ?? "";
        Cost = cost;
        PrerequisiteId = prerequisiteId;
        MetaRequirement = metaRequirement;
        RunReachRequirement = runReachRequirement;
    }
}
