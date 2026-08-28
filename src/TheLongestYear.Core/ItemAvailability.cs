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
/// late only makes the gate lenient.
///
/// A season override may only move a floor LATER. An override that demands an item EARLIER than
/// the derived floor is claiming the item exists before the game can produce it, which is the
/// Purple Mushroom failure: the deadline is unsatisfiable, the year is lost on every loop, and
/// the loop is permanently unwinnable. Such an override is rejected at construction, the derived
/// floor stands, and the id is listed in <see cref="RejectedSeasonOverrides"/>.</summary>
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
    private readonly HashSet<string> _rejectedSeasonOverrides = new(StringComparer.Ordinal);

    public ItemAvailabilityModel(
        IReadOnlyDictionary<string, ItemAvailability> derived,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null)
    {
        _derived = derived ?? throw new ArgumentNullException(nameof(derived));
        _seasonOverrides = seasonOverrides ?? new Dictionary<string, Season>(StringComparer.Ordinal);
        _effortOverrides = effortOverrides ?? new Dictionary<string, int>(StringComparer.Ordinal);

        // Validated once here rather than per lookup, so the count is meaningful the moment the
        // model exists and a caller can log it at build time without waiting for traffic.
        foreach (KeyValuePair<string, Season> pin in _seasonOverrides)
        {
            if (_derived.TryGetValue(pin.Key, out ItemAvailability? entry)
                && pin.Value < entry.EarliestSeason)
                _rejectedSeasonOverrides.Add(pin.Key);
        }
    }

    /// <summary>Ids that fell through to the unrecognised default during this session's lookups.
    /// Surfaced by tly_itemmodel so a modded item the engine cannot place is visible rather than
    /// silently ungated.</summary>
    public IReadOnlyCollection<string> UnrecognisedIds => _unrecognised;

    /// <summary>Ids whose curated season override was thrown out for demanding the item earlier
    /// than the derived floor says it can exist. Populated at construction, so it is complete
    /// before the first lookup.</summary>
    public IReadOnlyCollection<string> RejectedSeasonOverrides => _rejectedSeasonOverrides;

    /// <summary>How many ids the derivation rules actually placed. The useful build time number:
    /// unlike the unrecognised count it is not zero until lookups start happening.</summary>
    public int DerivedCount => _derived.Count;

    /// <summary>True when a derivation rule placed this id (fish, crab-pot, metals), so its
    /// floor is a fact about the world rather than the unrecognised default.</summary>
    public bool IsDerived(string qualifiedItemId)
        => qualifiedItemId != null && _derived.ContainsKey(qualifiedItemId);

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
            if (_rejectedSeasonOverrides.Contains(qualifiedItemId))
            {
                basis = $"season override to {overrideSeason} REJECTED, earlier than derived floor "
                    + $"{season} (derived: {basis})";
            }
            else
            {
                basis = $"season override to {overrideSeason} (derived: {basis})";
                season = overrideSeason;
            }
        }
        if (hasEffortOverride)
        {
            basis = $"{basis}; effort override to {overrideEffort}";
            effort = overrideEffort;
        }

        return new ItemAvailability(season, effort, basis);
    }
}
