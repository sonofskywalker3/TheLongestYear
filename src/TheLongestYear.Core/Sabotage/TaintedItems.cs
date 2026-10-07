using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Cache of the qualified item ids the darkness tampered away this loop, read by the
/// draw patches every frame. Refresh is a no-op unless the list changed, so a draw call never
/// allocates in the steady state.</summary>
public sealed class TaintedItems
{
    private const string DefaultItemPrefix = "(O)";
    private const char QualifierOpen = '(';

    private readonly HashSet<string> _ids = new();
    private IReadOnlyList<TamperRecord>? _source;
    private int _count = -1;

    public ISet<string> Ids => _ids;

    /// <summary>Rebuilds the set when the record list is a different list or its length changed
    /// (a reset clears the list in place, so a count of 0 empties the set).</summary>
    public ISet<string> Refresh(IReadOnlyList<TamperRecord>? tampers)
    {
        int count = tampers?.Count ?? 0;
        if (ReferenceEquals(tampers, _source) && count == _count) return _ids;
        _source = tampers;
        _count = count;
        _ids.Clear();
        for (int i = 0; i < count; i++)
        {
            string id = tampers![i].OldItemId;
            if (string.IsNullOrEmpty(id)) continue;
            _ids.Add(id[0] == QualifierOpen ? id : DefaultItemPrefix + id);
        }
        return _ids;
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
