using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Cache of what the darkness tampered away this loop, read by the draw patches every
/// frame. Refresh is a no-op unless the list changed, so a draw call never allocates in the
/// steady state.
///
/// The taint is the exact item (designer, 2026-10-07: tainting the Dried Apples must not make the
/// Dried Cucumbers another bundle needs glow too). A record with a flavour marks only that
/// flavour of its item. A record with none marks every copy of its item: the slot it came from
/// named no flavour and took any, and that is also what every record saved before the flavour
/// field existed means.</summary>
public sealed class TaintedItems
{
    private const string DefaultItemPrefix = "(O)";
    private const char QualifierOpen = '(';
    private const char QualifierClose = ')';

    private readonly HashSet<string> _ids = new();
    /// <summary>One entry per record: the qualified id, and the unqualified flavour or null for any.</summary>
    private readonly List<(string Id, string? Flavor)> _taints = new();
    private IReadOnlyList<TamperRecord>? _source;
    private int _count = -1;

    /// <summary>The qualified ids with any taint at all, for a quick first check.</summary>
    public ISet<string> Ids => _ids;

    /// <summary>Rebuilds the cache when the record list is a different list or its length changed
    /// (a reset clears the list in place, so a count of 0 empties it).</summary>
    public ISet<string> Refresh(IReadOnlyList<TamperRecord>? tampers)
    {
        int count = tampers?.Count ?? 0;
        if (ReferenceEquals(tampers, _source) && count == _count) return _ids;
        _source = tampers;
        _count = count;
        _ids.Clear();
        _taints.Clear();
        for (int i = 0; i < count; i++)
        {
            string id = tampers![i].OldItemId;
            if (string.IsNullOrEmpty(id)) continue;
            string qualified = id[0] == QualifierOpen ? id : DefaultItemPrefix + id;
            string? flavor = tampers[i].OldFlavor;
            _ids.Add(qualified);
            _taints.Add((qualified, string.IsNullOrEmpty(flavor) ? null : BundleParsing.StripQualifier(flavor!)));
        }
        return _ids;
    }

    /// <summary>Is this item tainted? <paramref name="flavor"/> is the item's own flavour (a
    /// ColoredObject's preservedParentSheetIndex, the bare id in vanilla), null or empty when it
    /// has none. Allocation-free: the draw patches call it for every item drawn.</summary>
    public bool IsTainted(string qualifiedItemId, string? flavor)
    {
        if (_ids.Count == 0 || qualifiedItemId == null || !_ids.Contains(qualifiedItemId)) return false;
        for (int i = 0; i < _taints.Count; i++)
        {
            (string id, string? wanted) = _taints[i];
            if (!string.Equals(id, qualifiedItemId, StringComparison.Ordinal)) continue;
            if (wanted == null || FlavorIs(flavor, wanted)) return true;
        }
        return false;
    }

    /// <summary>Does the item's flavour, bare or qualified, name <paramref name="wanted"/> (bare)?
    /// Compared in place, without building a substring.</summary>
    private static bool FlavorIs(string? flavor, string wanted)
    {
        if (string.IsNullOrEmpty(flavor)) return false;
        int start = 0;
        if (flavor![0] == QualifierOpen)
        {
            int close = flavor.IndexOf(QualifierClose);
            if (close >= 0) start = close + 1;
        }
        return flavor.Length - start == wanted.Length
               && string.CompareOrdinal(flavor, start, wanted, 0, wanted.Length) == 0;
    }

    /// <summary>Alpha of the aura at a game time: a slow sine around a dim base.</summary>
    public static float Pulse(double totalMilliseconds)
    {
        const float Base = 0.45f;
        const float Swing = 0.20f;
        const double PeriodDivisor = 450.0;
        return Base + Swing * (float)Math.Sin(totalMilliseconds / PeriodDivisor);
    }

    /// <summary>Opacity of the aura's soft edge at a normalized distance from its centre
    /// (0 = centre, 1 = rim): a smooth falloff to nothing at the rim, 0 beyond it.</summary>
    public static float Falloff(float normalizedRadius)
    {
        if (normalizedRadius >= 1f) return 0f;
        float r = Math.Max(0f, normalizedRadius);
        return 1f - r * r;
    }
}
