using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>The shipped vanilla answers with every active supported-mod layer laid over them.
/// <para><b>Compose order.</b> Vanilla, then each single-mod delta in the order given (file order),
/// then each interaction delta by mod count ascending (file order within a count). A layer's answers
/// apply only when every mod it names is active (has an inputs entry here). For every variant, a later
/// delta wins per item id; see <see cref="LayDelta"/> for the other
/// members. Row hashes, per variant: vanilla's <see cref="AnswerSet.RowHashesFor"/>, then each applied
/// delta's <see cref="LayerAnswers.VariantRowHashes"/> for that variant laid over, later winning. Rulings (<see cref="LayerInputs.YearRulings"/>,
/// <see cref="LayerInputs.WeekRulings"/>) go in inputs order, later winning; two layers that rule one
/// id differently are both named in a WARN.</para>
/// <para><b>Mapped.</b> A layer whose <see cref="LayerAnswers.Deltas"/> names variants maps only
/// those; a vanilla variant it does not name is dropped from the composed set (its answers with that
/// layer are unknown, so that save state is computed). An empty <see cref="LayerAnswers.Deltas"/>
/// maps every variant as unchanged. Every pair of active layers must have its interaction file
/// (<c>A+B.answers.json</c>, empty when the pair changes nothing): until it does, the pair is listed in
/// <see cref="UnmappedPairs"/> and <see cref="Composed"/> holds no variants, so the resolver never
/// serves answers that might miss the two mods' interaction.</para></summary>
public sealed class LayerSet
{
    private readonly IReadOnlyDictionary<string, YearRuling> _yearRulings;

    private LayerSet(
        AnswerSet? composed, AnswerSet? overlay, IReadOnlyList<string> layers, IReadOnlyList<string> keyLayers,
        IReadOnlyList<string> ids, IReadOnlyList<string> unmappedPairs,
        IReadOnlyDictionary<string, YearRuling> yearRulings, IReadOnlyDictionary<string, int> weekRulings,
        IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> extraDirect)
    {
        Composed = composed;
        Overlay = overlay;
        Layers = layers;
        KeyLayers = keyLayers;
        Ids = ids;
        UnmappedPairs = unmappedPairs;
        _yearRulings = yearRulings;
        WeekRulings = weekRulings;
        ExtraDirectSources = extraDirect;
    }

    /// <summary>The set the resolver is given as its shipped answers: <see cref="Overlay"/>, with no
    /// variants while any pair of active layers is unmapped. Its <see cref="AnswerSet.Layers"/> read
    /// <c>["vanilla", ids…, "A+B"…]</c> in compose order. Null with no vanilla set.</summary>
    public AnswerSet? Composed { get; }

    /// <summary>Every applied delta laid over vanilla, whether or not every pair is mapped: the
    /// baseline <c>tly_lexicon write</c> measures a new delta against. Null with no vanilla set.</summary>
    public AnswerSet? Overlay { get; }

    /// <summary><c>["vanilla", ids…, "A+B"…]</c> in compose order: the layers the cache key names
    /// (<see cref="Composed"/>'s <see cref="AnswerSet.Layers"/>, and set without a vanilla set too).</summary>
    public IReadOnlyList<string> Layers { get; }

    /// <summary>What the cache key names for the layers (<see cref="RowHashes.Key"/> only): <c>"vanilla"</c>,
    /// each active inputs file as <c>id#&lt;hash&gt;</c>, then each applied answers file as
    /// <c>name.answers#&lt;hash&gt;</c> in compose order, the hash being <see cref="LayerFile.HashText"/> of
    /// the file text (of its written form for a layer made in code). Editing a layer file changes the
    /// key, so a cache entry made with the old file is not reused; <see cref="Layers"/> stays plain ids.</summary>
    public IReadOnlyList<string> KeyLayers { get; }

    /// <summary>The active layers' mod ids, in inputs order.</summary>
    public IReadOnlyList<string> Ids { get; }

    /// <summary>Each pair of active layers with no interaction file, as its <see cref="LayerFile.Name"/>.</summary>
    public IReadOnlyList<string> UnmappedPairs { get; }

    /// <summary>Availability week rulings from every layer, by qualified id (the config's
    /// <c>AvailabilityWeekOverrides</c> win over these per id).</summary>
    public IReadOnlyDictionary<string, int> WeekRulings { get; }

    /// <summary>Every layer's <see cref="LayerInputs.ExtraSources"/>, concatenated per id in inputs
    /// order: the direct sources the builder adds (<see cref="ObtainabilityBuilder.Build"/>'s
    /// <c>extraDirect</c>). They are passed beside the inputs, never written into them, so the inputs
    /// (and their row hashes) stay the game's.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> ExtraDirectSources { get; }

    /// <summary>The layers' year ruling for a qualified id, or null when no layer rules it.</summary>
    public YearRuling? YearRuling(string id) => _yearRulings.TryGetValue(id, out YearRuling r) ? r : null;

    /// <summary>The message a layer checked against another mod version gets.</summary>
    public static string VersionMessage(string id, string checkedVersion, string? installed)
        => $"Lexicon layer {id}: checked against {checkedVersion}, installed {installed ?? "unknown"}; applying its routes, rows that changed are recomputed";

    /// <summary>WARNs when the installed version is not the one the layer was checked against. The layer
    /// applies either way (true): the row hashes decide which answers still hold.</summary>
    public static bool VersionCheck(LayerInputs layer, string? installedVersion, Action<string> warn)
    {
        if (!string.Equals(layer.CheckedVersion, installedVersion, StringComparison.Ordinal))
            warn(VersionMessage(layer.UniqueId, layer.CheckedVersion, installedVersion));
        return true;
    }

    /// <summary>The layers for the installed mods: inputs whose mod is loaded (version-checked), and
    /// answers whose every mod is among them, each in the order given.</summary>
    public static (IReadOnlyList<LayerInputs> Inputs, IReadOnlyList<LayerAnswers> Answers) Active(
        IReadOnlyList<LayerInputs> inputs, IReadOnlyList<LayerAnswers> answers,
        Func<string, bool> isLoaded, Func<string, string?> installedVersionOf, Action<string> warn)
    {
        var kept = inputs.Where(i => isLoaded(i.UniqueId) && VersionCheck(i, installedVersionOf(i.UniqueId), warn)).ToList();
        var ids = new HashSet<string>(kept.Select(i => i.UniqueId), StringComparer.Ordinal);
        return (kept, answers.Where(a => a.Mods.Count > 0 && a.Mods.All(ids.Contains)).ToList());
    }

    public static LayerSet Compose(
        AnswerSet? vanilla, IReadOnlyList<LayerInputs> inputs, IReadOnlyList<LayerAnswers> answers, Action<string> warn)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        if (answers == null) throw new ArgumentNullException(nameof(answers));
        if (warn == null) throw new ArgumentNullException(nameof(warn));

        List<string> ids = inputs.Select(i => i.UniqueId).Distinct(StringComparer.Ordinal).ToList();
        var active = new HashSet<string>(ids, StringComparer.Ordinal);
        List<LayerAnswers> applied = answers.Where(a => a.Mods.Count > 0 && a.Mods.All(active.Contains))
            .Select((a, i) => (a, i)).OrderBy(p => p.a.Mods.Distinct(StringComparer.Ordinal).Count()).ThenBy(p => p.i)
            .Select(p => p.a).ToList();

        var mappedPairs = new HashSet<string>(
            applied.Where(a => a.Mods.Distinct(StringComparer.Ordinal).Count() == 2).Select(a => LayerFile.Name(a.Mods)),
            StringComparer.Ordinal);
        var unmapped = new List<string>();
        for (int i = 0; i < ids.Count; i++)
            for (int j = i + 1; j < ids.Count; j++)
            {
                string pair = LayerFile.Name(new[] { ids[i], ids[j] });
                if (!mappedPairs.Contains(pair)) unmapped.Add(pair);
            }

        var years = Rulings(inputs, i => i.YearRulings, warn, "year");
        var weeks = Rulings(inputs, i => i.WeekRulings, warn, "week");
        var extra = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal);
        foreach (LayerInputs layer in inputs)
            foreach (var (id, sources) in layer.ExtraSources)
                extra[id] = extra.TryGetValue(id, out var had) ? had.Concat(sources).ToList() : sources.ToList();

        var layers = new List<string> { RowHashes.Vanilla };
        layers.AddRange(ids);
        layers.AddRange(applied.Where(a => a.Mods.Distinct(StringComparer.Ordinal).Count() > 1).Select(a => LayerFile.Name(a.Mods)));
        var keyLayers = new List<string> { RowHashes.Vanilla };
        keyLayers.AddRange(inputs.Select(i => $"{i.UniqueId}#{HashOf(i.TextHash, () => LayerFile.WriteInputs(i))}"));
        keyLayers.AddRange(applied.Select(a => $"{LayerFile.Name(a.Mods)}.answers#{HashOf(a.TextHash, () => LayerFile.WriteAnswers(a))}"));
        AnswerSet? overlay = null, composed = null;
        if (vanilla != null)
        {
            overlay = applied.Count == 0 ? vanilla with { Layers = layers } : Lay(vanilla, applied, layers);
            composed = unmapped.Count == 0
                ? overlay
                : overlay with { Variants = new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) };
        }
        return new LayerSet(composed, overlay, layers, keyLayers, ids, unmapped, years, weeks, extra);
    }

    private static string HashOf(string textHash, Func<string> written)
        => textHash.Length > 0 ? textHash : LayerFile.HashText(written());

    private static AnswerSet Lay(AnswerSet vanilla, IReadOnlyList<LayerAnswers> applied, IReadOnlyList<string> layers)
    {
        AnswerSet? result = null;
        // Base first, so AnswerSetMerge stores the others as deltas over it.
        foreach (string name in vanilla.Variants.Keys.OrderBy(k => k == AnswerSet.BaseVariant ? 0 : 1).ThenBy(k => k, StringComparer.Ordinal))
        {
            AnswerVariant? whole = vanilla.Resolve(name);
            if (whole == null) continue;
            var rows = new Dictionary<string, ulong>(vanilla.RowHashesFor(name), StringComparer.Ordinal);
            bool mapped = true;
            foreach (LayerAnswers layer in applied)
            {
                if (layer.Deltas.Count > 0)
                {
                    if (!layer.Deltas.TryGetValue(name, out AnswerVariant? delta)) { mapped = false; break; }
                    whole = LayDelta(whole, delta);
                }
                if (layer.VariantRowHashes.TryGetValue(name, out var own))
                    foreach (var (key, hash) in own) rows[key] = hash;
            }
            if (!mapped) continue;
            result = AnswerSetMerge.With(result, name, whole, rows, vanilla.GameVersion, vanilla.ModVersion, layers);
        }
        return result ?? new AnswerSet(vanilla.GameVersion, vanilla.ModVersion, layers, vanilla.RowHashes,
            new Dictionary<string, AnswerVariant>(StringComparer.Ordinal));
    }

    /// <summary>A delta laid over the variant below it: per id for <c>Obtain</c> and <c>YearSort</c>,
    /// per key for <c>MapWeeks</c>, <c>ShopWeeks</c> and <c>Availability</c> (a layer's availability
    /// entry replaces the one below it whole: the entry carries its own inputs hash), later winning;
    /// <c>Unresolved</c> and <c>UnreachedMaps</c> are the union (less any map now reached), <c>Passes</c>
    /// the larger.</summary>
    public static AnswerVariant LayDelta(AnswerVariant below, AnswerVariant delta)
    {
        var maps = Merge(below.MapWeeks, delta.MapWeeks);
        return new AnswerVariant(
            Merge(below.Obtain, delta.Obtain), Merge(below.YearSort, delta.YearSort),
            below.Unresolved.Union(delta.Unresolved, StringComparer.Ordinal).OrderBy(u => u, StringComparer.Ordinal).ToList(),
            Math.Max(below.Passes, delta.Passes),
            maps,
            below.UnreachedMaps.Union(delta.UnreachedMaps, StringComparer.Ordinal).Where(m => !maps.ContainsKey(m))
                .OrderBy(m => m, StringComparer.Ordinal).ToList(),
            Merge(below.ShopWeeks, delta.ShopWeeks))
        {
            Availability = Merge(below.Availability, delta.Availability),
        };
    }

    private static Dictionary<string, T> Merge<T>(IReadOnlyDictionary<string, T> below, IReadOnlyDictionary<string, T> over)
    {
        var all = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var (k, v) in below) all[k] = v;
        foreach (var (k, v) in over) all[k] = v;
        return all;
    }

    private static IReadOnlyDictionary<string, T> Rulings<T>(
        IReadOnlyList<LayerInputs> inputs, Func<LayerInputs, IReadOnlyDictionary<string, T>> of, Action<string> warn, string what)
        where T : notnull
    {
        var rulings = new Dictionary<string, (T Value, string By)>(StringComparer.Ordinal);
        foreach (LayerInputs layer in inputs)
            foreach (var (id, value) in of(layer))
            {
                if (rulings.TryGetValue(id, out var had) && !EqualityComparer<T>.Default.Equals(had.Value, value))
                    warn($"Lexicon layers {had.By} and {layer.UniqueId} give {id} different {what} rulings ({had.Value} and {value}); using {layer.UniqueId}'s.");
                rulings[id] = (value, layer.UniqueId);
            }
        return rulings.ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal);
    }
}
