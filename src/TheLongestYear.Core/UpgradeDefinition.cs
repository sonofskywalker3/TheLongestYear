using System;
using System.Collections.Generic;

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
    public long Cost { get; }

    /// <summary>Id of the upgrade that must already be owned, or null if no prerequisite.</summary>
    public string? PrerequisiteId { get; }

    /// <summary>Meta-state condition ("species:Chicken") the player must satisfy, or null if none.</summary>
    public string? MetaRequirement { get; }

    /// <summary>Live in-run reach gate (e.g. "tool:watering_can:2"), or null if the upgrade is
    /// not reach-gated. Parsed/evaluated separately (see RunReachRequirement + RunReachEvaluator).</summary>
    public string? RunReachRequirement { get; }

    private readonly string? _nameKey;
    private readonly string? _descKey;
    private readonly IReadOnlyDictionary<string, string>? _tokens;

    // Interim shim for UpgradeCatalogGenerators — removed in the next task once the
    // generators are converted to template keys+tokens (Task 4).
    private readonly string? _literalDisplayName;
    private readonly string? _literalDescription;

    /// <summary>Resolved lazily so locale changes take effect without a rebuild of the catalog.
    /// Token values prefixed "i18n:" are themselves resolved as translation keys at read time
    /// (used by generator template rows — see Task 4).</summary>
    public string DisplayName => _literalDisplayName ??
        (_tokens == null ? Strings.Get(_nameKey!) : Strings.Get(_nameKey!, ResolveTokens(_tokens)));

    public string Description => _literalDescription ??
        (_tokens == null ? Strings.Get(_descKey!) : Strings.Get(_descKey!, ResolveTokens(_tokens)));

    /// <summary>Resolves any token value prefixed "i18n:" as a translation key
    /// (<c>Strings.Get(value.Substring(5))</c>); other token values pass through unchanged.</summary>
    private static IReadOnlyDictionary<string, string> ResolveTokens(IReadOnlyDictionary<string, string> tokens)
    {
        const string I18nTokenPrefix = "i18n:";
        var resolved = new Dictionary<string, string>(tokens.Count);
        foreach (var kv in tokens)
            resolved[kv.Key] = kv.Value.StartsWith(I18nTokenPrefix, StringComparison.Ordinal)
                ? Strings.Get(kv.Value.Substring(I18nTokenPrefix.Length))
                : kv.Value;
        return resolved;
    }

    /// <summary>Hand-authored row: keys derive from the id — upgrade.{id}.name / upgrade.{id}.desc.</summary>
    public UpgradeDefinition(
        string id, UpgradeCategory category, long cost,
        string? prerequisiteId = null, string? metaRequirement = null, string? runReachRequirement = null)
        : this(id, category, $"upgrade.{id}.name", $"upgrade.{id}.desc", tokens: null, cost,
               prerequisiteId, metaRequirement, runReachRequirement)
    { }

    /// <summary>Template row (generators): explicit template keys + tokens.</summary>
    public UpgradeDefinition(
        string id, UpgradeCategory category, string nameKey, string descKey,
        IReadOnlyDictionary<string, string>? tokens, long cost,
        string? prerequisiteId = null, string? metaRequirement = null, string? runReachRequirement = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Id must be non-empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(nameKey))
            throw new ArgumentException("Name key must be non-empty.", nameof(nameKey));
        if (cost < 0)
            throw new ArgumentOutOfRangeException(nameof(cost), cost, "Cost must be non-negative.");
        Id = id; Category = category; Cost = cost;
        _nameKey = nameKey; _descKey = descKey; _tokens = tokens;
        PrerequisiteId = prerequisiteId; MetaRequirement = metaRequirement; RunReachRequirement = runReachRequirement;
    }

    /// <summary>
    /// Interim shim for UpgradeCatalogGenerators — removed in the next task. Matches the old
    /// (string displayName, string description) signature and returns the literals directly,
    /// bypassing key resolution entirely, so the generator call sites keep compiling and
    /// byte-identical while Task 3 converts only the hand-authored rows in UpgradeCatalog.cs.
    /// Task 4 replaces every generator call site with the template-key constructor above and
    /// deletes this shim.
    /// </summary>
    [Obsolete("Interim shim for UpgradeCatalogGenerators — removed in the next task (Task 4).")]
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
        _literalDisplayName = displayName;
        _literalDescription = description ?? "";
        Cost = cost;
        PrerequisiteId = prerequisiteId;
        MetaRequirement = metaRequirement;
        RunReachRequirement = runReachRequirement;
    }
}
