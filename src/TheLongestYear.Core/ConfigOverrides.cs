using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Merges a built-in default table with the player's config.json overrides: defaults
/// first, then every valid user entry wins on conflict, and every invalid user entry is reported
/// through <c>onInvalid</c> and skipped. Used for ThemeOverrides, ItemSeasonPins and BundleQuotas.</summary>
public static class ConfigOverrides
{
    /// <summary>Merge string-valued enum tables (Theme, Season...). Values parse case-insensitively.
    /// An unparseable default is dropped silently; an unparseable user value is reported as
    /// <c>onInvalid(key, value)</c>.</summary>
    public static Dictionary<string, TEnum> MergeEnum<TEnum>(
        IReadOnlyDictionary<string, string> defaults,
        IReadOnlyDictionary<string, string>? user,
        Action<string, string> onInvalid)
        where TEnum : struct, Enum
    {
        var merged = new Dictionary<string, TEnum>();
        foreach (KeyValuePair<string, string> kv in defaults)
            if (Enum.TryParse(kv.Value, ignoreCase: true, out TEnum parsed))
                merged[kv.Key] = parsed;

        if (user == null)
            return merged;
        foreach (KeyValuePair<string, string> kv in user)
        {
            if (Enum.TryParse(kv.Value, ignoreCase: true, out TEnum parsed))
                merged[kv.Key] = parsed;
            else
                onInvalid(kv.Key, kv.Value);
        }
        return merged;
    }

    /// <summary>Why a user quota array was skipped.</summary>
    public enum QuotaProblem
    {
        /// <summary>Missing, or not one cumulative count per season.</summary>
        WrongLength,
        /// <summary>A count below zero.</summary>
        Negative,
    }

    /// <summary>Merge cumulative per-season bundle quotas. Every array is copied, so callers can
    /// never edit the defaults. A user array is skipped and reported as
    /// <c>onInvalid(key, value, problem)</c> when it does not hold exactly
    /// <paramref name="length"/> entries or holds a negative one.</summary>
    public static Dictionary<string, int[]> MergeQuotas(
        IReadOnlyDictionary<string, int[]> defaults,
        IReadOnlyDictionary<string, int[]>? user,
        int length,
        Action<string, int[]?, QuotaProblem> onInvalid)
    {
        var merged = new Dictionary<string, int[]>();
        foreach (KeyValuePair<string, int[]> kv in defaults)
            merged[kv.Key] = (int[])kv.Value.Clone();

        if (user == null)
            return merged;
        foreach (KeyValuePair<string, int[]> kv in user)
        {
            int[]? quota = kv.Value;
            if (quota == null || quota.Length != length)
            {
                onInvalid(kv.Key, quota, QuotaProblem.WrongLength);
                continue;
            }
            if (Array.Exists(quota, n => n < 0))
            {
                onInvalid(kv.Key, quota, QuotaProblem.Negative);
                continue;
            }
            merged[kv.Key] = (int[])quota.Clone();
        }
        return merged;
    }
}
