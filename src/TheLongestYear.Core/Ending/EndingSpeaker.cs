using System;
using System.Linq;

namespace TheLongestYear.Core.Ending;

/// <summary>Year One Ending (spec 2026-09-06 section 6): the villager who steps forward at the ceremony.
/// Highest familiarity at or above the deja-vu threshold, ties by most loops known, then by name.
/// <paramref name="isEligibleInGame"/> answers "present, not the spouse, not a child".</summary>
public static class EndingSpeaker
{
    public static string? Pick(MetaState meta, int threshold, Func<string, bool> isEligibleInGame)
    {
        if (threshold <= 0) threshold = 1;
        return meta.VillagerFamiliarity
            .Where(kv => kv.Value >= threshold && isEligibleInGame(kv.Key))
            .OrderByDescending(kv => kv.Value)
            .ThenByDescending(kv => meta.VillagerMemory.TryGetValue(kv.Key, out var mem) ? mem.Loops.Count : 0)
            .ThenBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => kv.Key)
            .FirstOrDefault();
    }
}
