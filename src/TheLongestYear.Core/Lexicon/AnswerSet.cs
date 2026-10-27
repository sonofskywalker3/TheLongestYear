using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>One composed answer: the obtainability model's routes for every item, the year-sort
/// model's, and the map and shop weeks they were built against.</summary>
public sealed record AnswerVariant(
    IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> Obtain,     // every id in inputs.Objects has a key (empty list = no route)
    IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> YearSort,
    IReadOnlyList<string> Unresolved, int Passes,
    IReadOnlyDictionary<string, PlaceWeek> MapWeeks, IReadOnlyList<string> UnreachedMaps,
    IReadOnlyDictionary<string, PlaceWeek> ShopWeeks)
{
    /// <summary>The distinct day-table run strings, ordinally sorted; the file refers to them by index.
    /// Filled by <see cref="LexiconJson.Read"/> (and written by <see cref="LexiconJson.Write"/>, which
    /// builds its own pool); empty on a freshly built variant.</summary>
    public IReadOnlyList<string> Tables { get; init; } = Array.Empty<string>();

    /// <summary>The item availability answers for this data, by <see cref="AvailabilityAnswers.Key"/>
    /// (<c>"{step}|kitchen"</c> / <c>"{step}|no_kitchen"</c>). Filled as they are first computed (and
    /// for every key by <c>tly_lexicon write vanilla</c>); empty on a freshly built variant. Stored
    /// whole in a delta variant, like the other non-route members.</summary>
    public IReadOnlyDictionary<string, AvailabilityAnswers> Availability { get; init; }
        = new Dictionary<string, AvailabilityAnswers>(StringComparer.Ordinal);

    /// <summary>The obtainability model these answers stand for. Ids with no route (the empty
    /// lists the snapshot keeps for every object) are left out, as the builder leaves them out, so
    /// the model's <see cref="ObtainabilityModel.ItemIds"/> and <see cref="ObtainabilityModel.Count"/>
    /// equal the cold build's.</summary>
    public ObtainabilityModel ObtainModel() => ModelOf(Obtain);

    /// <summary>How many ids have at least one route: <see cref="ObtainModel"/>'s count, without building it.</summary>
    public int RoutedCount() => Obtain.Where(p => p.Value.Count > 0)
        .Select(p => BundleParsing.NormalizeItemId(p.Key)).Distinct(StringComparer.Ordinal).Count();

    /// <summary>The year-sort model these answers stand for (see <see cref="ObtainModel"/>).</summary>
    public ObtainabilityModel YearSortModel() => ModelOf(YearSort);

    private static ObtainabilityModel ModelOf(IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> sources)
        => new(sources.Where(p => p.Value.Count > 0).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal));
}

/// <summary>The shipped or cached answer file: which game, mod and layers it was made for, the
/// row hashes it was checked against, and its variants ("base", "pierre_year2_seeds").
/// <para><b>Storage rule.</b> When the set holds a <see cref="BaseVariant"/>, every other variant is
/// stored as a delta over it: its <c>Obtain</c> and <c>YearSort</c> hold only the ids whose source
/// list differs from base's (compared as their <see cref="LexiconJson.Options"/> JSON, so
/// <see cref="ObtainSource.Inputs"/> counts too; an id base has and the variant lacks is stored as an
/// empty list); its other members are stored whole. Read a variant through <see cref="Resolve"/>,
/// never straight out of <see cref="Variants"/>. With no base variant, every variant is stored whole.
/// <see cref="AnswerSetMerge.With"/> keeps this rule.</para>
/// <para><see cref="RowHashes"/> are the base variant's input rows; every other variant's own row
/// hashes, whole, are in <see cref="VariantRowHashes"/> (read them through
/// <see cref="RowHashesFor"/>).</para></summary>
public sealed record AnswerSet(
    string GameVersion, string ModVersion, IReadOnlyList<string> Layers,          // layer ids composed in, "vanilla" first
    IReadOnlyDictionary<string, ulong> RowHashes,
    IReadOnlyDictionary<string, AnswerVariant> Variants)
{
    public const string BaseVariant = "base";
    /// <summary>The variant for a save that owns the <c>pierre_year2_seeds</c> upgrade.</summary>
    public const string PierreVariant = "pierre_year2_seeds";

    /// <summary>Per non-base variant, every row hash its inputs had. Empty when the set holds only base.</summary>
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, ulong>> VariantRowHashes { get; init; }
        = new Dictionary<string, IReadOnlyDictionary<string, ulong>>();

    /// <summary>The named variant whole (a delta laid over base), or null when the set lacks it.</summary>
    public AnswerVariant? Resolve(string name)
    {
        if (!Variants.TryGetValue(name, out AnswerVariant? stored)) return null;
        if (name == BaseVariant || !Variants.TryGetValue(BaseVariant, out AnswerVariant? baseline)) return stored;
        return AnswerVariantDelta.Apply(baseline, stored);
    }

    /// <summary>The row hashes the named variant was built from.</summary>
    public IReadOnlyDictionary<string, ulong> RowHashesFor(string name)
        => name != BaseVariant && VariantRowHashes.TryGetValue(name, out var own) ? own : RowHashes;
}

/// <summary>The delta form non-base variants are stored in (see <see cref="AnswerSet"/>).</summary>
public static class AnswerVariantDelta
{
    /// <summary><paramref name="full"/> with <c>Obtain</c> and <c>YearSort</c> cut to the ids that
    /// differ from <paramref name="baseline"/>.</summary>
    public static AnswerVariant Diff(AnswerVariant baseline, AnswerVariant full)
        => full with
        {
            Obtain = Cut(baseline.Obtain, full.Obtain), YearSort = Cut(baseline.YearSort, full.YearSort),
            Tables = Array.Empty<string>(),
        };

    /// <summary>The whole variant a delta stands for.</summary>
    public static AnswerVariant Apply(AnswerVariant baseline, AnswerVariant delta)
        => delta with
        {
            Obtain = Lay(baseline.Obtain, delta.Obtain), YearSort = Lay(baseline.YearSort, delta.YearSort),
            Tables = Array.Empty<string>(),
        };

    private static IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> Cut(
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> baseline,
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> full)
    {
        var delta = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal);
        foreach (var (id, sources) in full)
            if (!baseline.TryGetValue(id, out var was) || Text(was) != Text(sources)) delta[id] = sources;
        foreach (string id in baseline.Keys)
            if (!full.ContainsKey(id)) delta[id] = Array.Empty<ObtainSource>();
        return delta;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> Lay(
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> baseline,
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> delta)
    {
        var all = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal);
        foreach (var (id, sources) in baseline) all[id] = sources;
        foreach (var (id, sources) in delta) all[id] = sources;
        return all;
    }

    private static string Text(IReadOnlyList<ObtainSource> sources)
        => System.Text.Json.JsonSerializer.Serialize(sources, LexiconJson.Options);
}

/// <summary>Writes one variant into an answer set, keeping its other variants and the storage rule.</summary>
public static class AnswerSetMerge
{
    /// <summary>The set <paramref name="existing"/> (or a new one) with <paramref name="name"/> set to
    /// <paramref name="full"/>, built from input rows hashing to <paramref name="rowHashes"/>; the game
    /// and mod versions and the layers are the new ones. When <paramref name="existing"/> was made for
    /// another game or mod version, its other variants were built by other rules or from other data,
    /// so they are dropped (each named to <paramref name="warn"/>) rather than relabelled as this
    /// version's.</summary>
    public static AnswerSet With(
        AnswerSet? existing, string name, AnswerVariant full, IReadOnlyDictionary<string, ulong> rowHashes,
        string gameVersion, string modVersion, IReadOnlyList<string> layers, Action<string>? warn = null)
    {
        var variants = new Dictionary<string, AnswerVariant>(StringComparer.Ordinal);
        var hashes = new Dictionary<string, IReadOnlyDictionary<string, ulong>>(StringComparer.Ordinal);
        if (existing != null && (existing.GameVersion != gameVersion || existing.ModVersion != modVersion))
        {
            foreach (string other in existing.Variants.Keys.Where(k => k != name).OrderBy(k => k, StringComparer.Ordinal))
                warn?.Invoke($"dropping variant '{other}': it was written for game {existing.GameVersion}, mod {existing.ModVersion}, not game {gameVersion}, mod {modVersion}; write it again on this version.");
            existing = null;
        }
        if (existing != null)
            foreach (string other in existing.Variants.Keys.Where(k => k != name))
            {
                variants[other] = existing.Resolve(other)!;
                hashes[other] = existing.RowHashesFor(other);
            }
        variants[name] = full;
        hashes[name] = rowHashes;

        if (!variants.TryGetValue(AnswerSet.BaseVariant, out AnswerVariant? baseline))
            return new AnswerSet(gameVersion, modVersion, layers, new Dictionary<string, ulong>(), variants)
            {
                VariantRowHashes = hashes,
            };

        var stored = new Dictionary<string, AnswerVariant>(StringComparer.Ordinal);
        foreach (var (key, variant) in variants)
            stored[key] = key == AnswerSet.BaseVariant ? variant : AnswerVariantDelta.Diff(baseline, variant);
        IReadOnlyDictionary<string, ulong> baseHashes = hashes[AnswerSet.BaseVariant];
        hashes.Remove(AnswerSet.BaseVariant);
        return new AnswerSet(gameVersion, modVersion, layers, baseHashes, stored) { VariantRowHashes = hashes };
    }
}

public static class AnswerVariantFactory
{
    /// <summary>Snapshots a built model. <c>Obtain</c> and <c>YearSort</c> carry a key for every object
    /// id in <paramref name="inputs"/> (an empty list when nothing yields it) plus every other id the
    /// model holds (hats, weapons, big craftables). Null <paramref name="maps"/> or
    /// <paramref name="shops"/> give empty weeks.</summary>
    public static AnswerVariant From(
        ObtainabilityBuild model, ObtainabilityBuild yearSort, ObtainabilityInputs inputs,
        LocationWeeks? maps, IReadOnlyCollection<string> allMaps, ShopWeeks? shops)
    {
        IReadOnlyDictionary<string, PlaceWeek> reached = maps?.Reached ?? new Dictionary<string, PlaceWeek>();
        return new AnswerVariant(
            Snapshot(model.Model, inputs), Snapshot(yearSort.Model, inputs),
            model.Unresolved.ToList(), model.Passes,
            new Dictionary<string, PlaceWeek>(reached),
            allMaps.Where(m => !reached.ContainsKey(m)).OrderBy(m => m, StringComparer.Ordinal).ToList(),
            new Dictionary<string, PlaceWeek>(shops?.Weeks ?? new Dictionary<string, PlaceWeek>()));
    }

    private static Dictionary<string, IReadOnlyList<ObtainSource>> Snapshot(ObtainabilityModel model, ObtainabilityInputs inputs)
    {
        var all = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal);
        foreach (string id in inputs.Objects.Keys) all[id] = model.Sources(id);
        foreach (string id in model.ItemIds) all[id] = model.Sources(id);
        return all;
    }
}
