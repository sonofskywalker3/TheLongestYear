using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>What the engine knows about one item, derived from the game's own data tables.
///
/// Two values, deliberately separate. Before this existed, PerItem bundles gated on a single
/// hand written table (GameplayConfig.DefaultItemSeasonPins) whose entries conflated the two:
/// Sunfish was pinned Spring and Shad Summer although both are catchable year round, so those
/// dates were pacing choices wearing the costume of availability facts. Reading a pacing choice
/// as an availability fact is what made a Fall Foraging bundle unsatisfiable at its own gate
/// (the Purple Mushroom incident, 2026-08-27).</summary>
/// <param name="EarliestSeason">Hard floor: before this season the item cannot exist at all.</param>
/// <param name="Effort">Derived judgement of how much work the item is. Higher is harder.</param>
/// <param name="Basis">Human readable derivation, for tly_itemmodel and the generated model doc.</param>
public sealed record ItemAvailability(Season EarliestSeason, int Effort, string Basis);

/// <summary>Every item's <see cref="ItemAvailability"/>, plus the override layers.
///
/// Precedence, lowest to highest: derived value, then curated season/effort overrides
/// (GameplayConfig defaults merged with the user's config, merged by the caller before it gets
/// here). An id with no derived entry and no override floors at WINTER, which is the safe
/// direction: BundleDeadlines clamps a deadline UPWARD to the floor, so a floor guessed too
/// early permits a gate the world cannot satisfy and bricks the run, while a floor guessed too
/// late only makes the gate lenient.</summary>
public sealed class ItemAvailabilityModel
{
    /// <summary>Effort assigned to an item no rule recognised. Mid scale, so an unrecognised item
    /// neither leads nor trails the effort ranking of a bundle it appears in.</summary>
    public const int UnrecognisedEffort = 6;

    private const string UnrecognisedBasis = "no derivation rule matched this item";

    private readonly IReadOnlyDictionary<string, ItemAvailability> _derived;
    private readonly IReadOnlyDictionary<string, Season> _seasonOverrides;
    private readonly IReadOnlyDictionary<string, int> _effortOverrides;
    private readonly HashSet<string> _unrecognised = new(StringComparer.Ordinal);

    public ItemAvailabilityModel(
        IReadOnlyDictionary<string, ItemAvailability> derived,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null)
    {
        _derived = derived ?? throw new ArgumentNullException(nameof(derived));
        _seasonOverrides = seasonOverrides ?? new Dictionary<string, Season>(StringComparer.Ordinal);
        _effortOverrides = effortOverrides ?? new Dictionary<string, int>(StringComparer.Ordinal);
    }

    /// <summary>Ids that fell through to the unrecognised default during this session's lookups.
    /// Surfaced by tly_itemmodel so a modded item the engine cannot place is visible rather than
    /// silently ungated.</summary>
    public IReadOnlyCollection<string> UnrecognisedIds => _unrecognised;

    public ItemAvailability For(string qualifiedItemId)
    {
        if (qualifiedItemId == null) throw new ArgumentNullException(nameof(qualifiedItemId));

        bool known = _derived.TryGetValue(qualifiedItemId, out ItemAvailability? derived);
        bool hasSeasonOverride = _seasonOverrides.TryGetValue(qualifiedItemId, out Season overrideSeason);
        bool hasEffortOverride = _effortOverrides.TryGetValue(qualifiedItemId, out int overrideEffort);

        if (!known && !hasSeasonOverride && !hasEffortOverride)
        {
            _unrecognised.Add(qualifiedItemId);
            return new ItemAvailability(Season.Winter, UnrecognisedEffort, UnrecognisedBasis);
        }

        Season season = derived?.EarliestSeason ?? Season.Winter;
        int effort = derived?.Effort ?? UnrecognisedEffort;
        string basis = derived?.Basis ?? UnrecognisedBasis;

        if (hasSeasonOverride)
        {
            basis = $"season override to {overrideSeason} (derived: {basis})";
            season = overrideSeason;
        }
        if (hasEffortOverride)
        {
            basis = $"{basis}; effort override to {overrideEffort}";
            effort = overrideEffort;
        }

        return new ItemAvailability(season, effort, basis);
    }
}
