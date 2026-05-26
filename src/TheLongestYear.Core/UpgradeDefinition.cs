using System;

namespace TheLongestYear.Core;

/// <summary>
/// One row in the upgrade shop. Effects of an upgrade at run-start (e.g. starting backpack tier,
/// retained XP, Mixed Seeds injection) are applied in a later plan; Plan 05 only records
/// the purchase into <see cref="MetaState.OwnedUpgrades"/>.
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

    public UpgradeDefinition(string id, UpgradeCategory category, string displayName, string description, long cost, string? prerequisiteId = null)
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
    }
}
