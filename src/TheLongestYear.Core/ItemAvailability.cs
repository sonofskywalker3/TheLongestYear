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
public sealed record ItemAvailability(
    Season EarliestSeason, int Effort, string Basis, EffortSource Source = EffortSource.Derived,
    int EarliestWeek = 0, Season? GateSeason = null)
{
    /// <summary>First week of the year the item can exist; a record built from a season alone
    /// reads as that season's first week.</summary>
    public int Week => EarliestWeek > 0 ? EarliestWeek : AvailabilityWeeks.FirstWeekOf(EarliestSeason);
    /// <summary>Season a day-28 gate may first demand the item.</summary>
    public Season Gate => GateSeason ?? EarliestSeason;
}

/// <summary>Where an item's effort number came from: a derivation rule, the price bucket
/// fallback (no rule claimed the id), or the curated effort override table.</summary>
public enum EffortSource { Derived, Price, Override }

/// <summary>Effort without a season floor. Phase 2 rules (gems, geodes, monster drops,
/// artifacts, artisan goods, animal products, dishes, crops, forage) produce these: they say how
/// much work an item is, never when it first exists, so a gate is never moved by them.</summary>
public sealed record ItemEffort(int Effort, string Basis, int? EarliestWeek = null, Season? GateSeason = null);

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
    private const string EffortOnlyFloorNote = "floor not derived (Winter)";

    private readonly IReadOnlyDictionary<string, ItemAvailability> _derived;
    private readonly IReadOnlyDictionary<string, Season> _seasonOverrides;
    private readonly IReadOnlyDictionary<string, int> _effortOverrides;
    private readonly IReadOnlyDictionary<string, ItemEffort> _effortDerived;
    private readonly HashSet<string> _unrecognised = new(StringComparer.Ordinal);
    private readonly HashSet<string> _rejectedSeasonOverrides = new(StringComparer.Ordinal);
    private readonly IReadOnlyDictionary<string, int> _weekOverrides;
    private readonly HashSet<string> _unknown = new(StringComparer.Ordinal);

    public ItemAvailabilityModel(
        IReadOnlyDictionary<string, ItemAvailability> derived,
        IReadOnlyDictionary<string, Season>? seasonOverrides = null,
        IReadOnlyDictionary<string, int>? effortOverrides = null,
        IReadOnlyDictionary<string, ItemEffort>? effortDerived = null,
        IReadOnlyDictionary<string, int>? weekOverrides = null)
    {
        _derived = derived ?? throw new ArgumentNullException(nameof(derived));
        _seasonOverrides = seasonOverrides ?? new Dictionary<string, Season>(StringComparer.Ordinal);
        _effortOverrides = effortOverrides ?? new Dictionary<string, int>(StringComparer.Ordinal);
        _effortDerived = effortDerived ?? new Dictionary<string, ItemEffort>(StringComparer.Ordinal);
        _weekOverrides = weekOverrides ?? new Dictionary<string, int>(StringComparer.Ordinal);
        // Validated once here rather than per lookup, so the count is meaningful the moment the
        // model exists and a caller can log it at build time without waiting for traffic. Compared
        // in weeks (spec 2026-08-28-even-year): an override may only move a placed floor later.
        foreach (KeyValuePair<string, Season> pin in _seasonOverrides)
        {
            int? floor = PlacedWeek(pin.Key);
            if (floor != null && AvailabilityWeeks.FirstWeekOf(pin.Value) < floor.Value)
                _rejectedSeasonOverrides.Add(pin.Key);
        }
        foreach (KeyValuePair<string, int> pin in _weekOverrides)
        {
            int? floor = PlacedWeek(pin.Key);
            if (floor != null && pin.Value < floor.Value)
                _rejectedSeasonOverrides.Add(pin.Key);
        }
    }

    /// <summary>The week a rule placed the id at, or null when no rule did.</summary>
    private int? PlacedWeek(string id)
    {
        if (_derived.TryGetValue(id, out ItemAvailability? d)) return d.Week;
        if (_effortDerived.TryGetValue(id, out ItemEffort? e) && e.EarliestWeek != null) return e.EarliestWeek;
        return null;
    }

    /// <summary>True when a rule or an accepted override says when the item first exists.</summary>
    public bool IsPlaced(string qualifiedItemId)
        => qualifiedItemId != null
           && (PlacedWeek(qualifiedItemId) != null
               || (_seasonOverrides.ContainsKey(qualifiedItemId) && !_rejectedSeasonOverrides.Contains(qualifiedItemId))
               || (_weekOverrides.ContainsKey(qualifiedItemId) && !_rejectedSeasonOverrides.Contains(qualifiedItemId)));

    /// <summary>Every id For() has been asked about that nothing placed. The list Jeff reads
    /// after every sim (memory tly-sim-list-unknowns-each-run).</summary>
    public IReadOnlyCollection<string> UnknownIds => _unknown;

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

    /// <summary>True when either a season rule (fish, crab-pot, metals) or an effort-only rule
    /// placed this id. The goal sampler tiers such ids by effort; the rest use the price bucket.</summary>
    public bool HasDerivedEffort(string qualifiedItemId)
        => qualifiedItemId != null
           && (_derived.ContainsKey(qualifiedItemId) || _effortDerived.ContainsKey(qualifiedItemId));

    /// <summary>How many ids the effort-only rules placed (Phase 2 of the model).</summary>
    public int DerivedEffortCount => _effortDerived.Count;

    public ItemAvailability For(string qualifiedItemId)
    {
        if (qualifiedItemId == null) throw new ArgumentNullException(nameof(qualifiedItemId));

        bool known = _derived.TryGetValue(qualifiedItemId, out ItemAvailability? derived);
        bool effortKnown = _effortDerived.TryGetValue(qualifiedItemId, out ItemEffort? effortOnly);
        bool hasSeasonOverride = _seasonOverrides.TryGetValue(qualifiedItemId, out Season overrideSeason);
        bool hasEffortOverride = _effortOverrides.TryGetValue(qualifiedItemId, out int overrideEffort);

        if (!known && !effortKnown && !hasSeasonOverride && !hasEffortOverride && !_weekOverrides.ContainsKey(qualifiedItemId))
        {
            _unrecognised.Add(qualifiedItemId);
            _unknown.Add(qualifiedItemId);
            return new ItemAvailability(Season.Winter, UnrecognisedEffort, UnrecognisedBasis, EffortSource.Price,
                AvailabilityWeeks.UnknownWeek, Season.Winter);
        }

        int week = derived?.Week ?? effortOnly?.EarliestWeek ?? AvailabilityWeeks.UnknownWeek;
        Season gate = derived?.Gate ?? effortOnly?.GateSeason ?? AvailabilityWeeks.SeasonOf(week);
        bool placed = PlacedWeek(qualifiedItemId) != null;
        int effort = derived?.Effort ?? effortOnly?.Effort ?? UnrecognisedEffort;
        string basis = derived?.Basis
            ?? (effortOnly != null ? effortOnly.Basis + (placed ? "" : "; " + EffortOnlyFloorNote) : UnrecognisedBasis);
        EffortSource source = known || effortKnown ? EffortSource.Derived : EffortSource.Price;
        bool rejected = _rejectedSeasonOverrides.Contains(qualifiedItemId);

        if (hasSeasonOverride)
        {
            if (rejected)
            {
                basis = $"season override to {overrideSeason} REJECTED, earlier than derived floor "
                    + $"week {week} (derived: {basis})";
            }
            else
            {
                basis = $"season override to {overrideSeason} (derived: {basis})";
                week = AvailabilityWeeks.FirstWeekOf(overrideSeason);
                gate = overrideSeason;
                placed = true;
            }
        }
        if (_weekOverrides.TryGetValue(qualifiedItemId, out int overrideWeek))
        {
            if (rejected)
            {
                basis = $"week override to {overrideWeek} REJECTED, earlier than derived floor "
                    + $"week {week} (derived: {basis})";
            }
            else
            {
                basis = $"week override to {overrideWeek} (derived: {basis})";
                week = overrideWeek;
                gate = AvailabilityWeeks.SeasonOf(week);
                placed = true;
            }
        }
        if (hasEffortOverride)
        {
            basis = $"{basis}; effort override to {overrideEffort}";
            effort = overrideEffort;
            source = EffortSource.Override;
        }
        if (!placed) _unknown.Add(qualifiedItemId);
        return new ItemAvailability(AvailabilityWeeks.SeasonOf(week), effort, basis, source, week, gate);
    }
}
