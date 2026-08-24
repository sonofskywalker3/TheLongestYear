using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace TheLongestYear.Core;

/// <summary>Inspects a delegate's closure to find out what it captured. Used by
/// BundleOptionPatch to locate the vanilla Advanced-Game-Options apply callback that
/// belongs to the CC-bundles dropdown: positional heuristics broke (AGO header rows use
/// the Default element style, so a "count the non-label options" index was off by one —
/// Nexus 1122619, the Remixed pick soft-locking OK), but the callback's compiler-generated
/// closure holds a reference to its own dropdown, which identifies it unambiguously.</summary>
public static class DelegateClosures
{
    private const int MaxDepth = 3;

    /// <summary>True when <paramref name="del"/>'s closure (its Target object, nested
    /// compiler-generated closure objects, and captured delegates' closures, up to a small
    /// depth) holds a reference to <paramref name="needle"/>.</summary>
    public static bool References(Delegate del, object needle)
    {
        if (del?.Target == null || needle == null)
            return false;
        return Scan(del.Target, needle, MaxDepth, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private static bool Scan(object target, object needle, int depth, HashSet<object> seen)
    {
        if (target == null || depth < 0 || !seen.Add(target))
            return false;

        for (Type type = target.GetType(); type != null && type != typeof(object); type = type.BaseType)
        {
            foreach (FieldInfo field in type.GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
            {
                object value;
                try { value = field.GetValue(target); }
                catch { continue; }

                if (ReferenceEquals(value, needle))
                    return true;
                if (value is Delegate nested && nested.Target != null
                    && Scan(nested.Target, needle, depth - 1, seen))
                    return true;
                if (value != null && IsCompilerGeneratedClosure(value.GetType())
                    && Scan(value, needle, depth - 1, seen))
                    return true;
            }
        }
        return false;
    }

    private static bool IsCompilerGeneratedClosure(Type type)
        => type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);
}
