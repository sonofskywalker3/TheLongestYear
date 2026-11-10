using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One bundle's place in the reward allocation: its room, its absolute index, the reward
/// it carries now, and whether it is one of OUR authored bundles (known by identity before the
/// engine's name uniquification, never by its final name).</summary>
public readonly record struct RewardSlot(string Room, int Index, string Reward, bool Authored);

/// <summary>The one allocation routine behind every form of <see cref="BundleRewardShuffle"/>.
/// Rewards are handed out from a limited pool, marked off as they go (Jeff, 2026-11-10: "it
/// should stay vanilla, but it should draw from the same limited pool"). Slots are walked in
/// canonical order (absolute index, then room), never input order, so the spec form and the data
/// form agree whatever order their boards come in. Each mode draws from a seeded
/// <see cref="Random"/> of its own and so moves no other stream. The result is aligned to the
/// input: entry i is slot i's reward.</summary>
internal static class BundleRewardAllocation
{
    /// <summary>Random rewards on: each bundle draws from its own room's pool without replacement
    /// across the whole board; once its room's pool is used up it draws from the board-wide pool
    /// (every room pool together), still without replacement. Only once every reward on the board
    /// is used does a new cycle start over the same universe. An empty universe changes nothing.</summary>
    public static string[] Shuffled(
        IReadOnlyList<RewardSlot> slots, Random rng, IReadOnlyDictionary<string, IReadOnlyList<string>> roomPools)
    {
        string[] result = slots.Select(s => s.Reward).ToArray();
        IReadOnlyList<string> universe = BundleRewardShuffle.CleanPool(roomPools.Values.SelectMany(p => p));
        if (universe.Count == 0)
            return result;

        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (int i in CanonicalOrder(slots))
        {
            if (used.Count >= universe.Count)
                used.Clear(); // every reward handed out once: a new cycle
            IReadOnlyList<string> own = roomPools.TryGetValue(slots[i].Room, out IReadOnlyList<string>? pool)
                ? pool
                : Array.Empty<string>();
            List<string> open = Unused(own, used);
            if (open.Count == 0)
                open = Unused(universe, used);
            result[i] = open[rng.Next(open.Count)];
            used.Add(result[i]);
        }
        return result;
    }

    /// <summary>Random rewards off: every non-authored bundle keeps its reward and marks it used,
    /// board-wide, before any authored bundle is served. Each authored bundle then takes an unused
    /// reward from its room's standard set; if that set is used up, an unused reward from the other
    /// rooms' standard sets; if every one is used, it keeps its own reward. Two retained vanilla or
    /// remix bundles may still share a reward: this only stops OUR bundles adding repeats.</summary>
    public static string[] VanillaKept(
        IReadOnlyList<RewardSlot> slots, Random rng, IReadOnlyDictionary<string, IReadOnlyList<string>> standardRoomSets)
    {
        string[] result = slots.Select(s => s.Reward).ToArray();
        var used = new HashSet<string>(
            slots.Where(s => !s.Authored && !string.IsNullOrEmpty(s.Reward)).Select(s => s.Reward),
            StringComparer.Ordinal);
        IReadOnlyList<string> everyRoom = BundleRewardShuffle.CleanPool(standardRoomSets.Values.SelectMany(p => p));

        foreach (int i in CanonicalOrder(slots).Where(i => slots[i].Authored))
        {
            IReadOnlyList<string> own = standardRoomSets.TryGetValue(slots[i].Room, out IReadOnlyList<string>? set)
                ? set
                : Array.Empty<string>();
            List<string> open = Unused(own, used);
            if (open.Count == 0)
                open = Unused(everyRoom, used); // own room's set is used up, so this is the other rooms'
            if (open.Count > 0)
                result[i] = open[rng.Next(open.Count)];
            used.Add(result[i]);
        }
        return result;
    }

    /// <summary>Slot positions by absolute index, then room (ordinal), then input position.</summary>
    private static IEnumerable<int> CanonicalOrder(IReadOnlyList<RewardSlot> slots)
        => Enumerable.Range(0, slots.Count)
            .OrderBy(i => slots[i].Index)
            .ThenBy(i => slots[i].Room, StringComparer.Ordinal)
            .ThenBy(i => i);

    private static List<string> Unused(IReadOnlyList<string> pool, HashSet<string> used)
        => pool.Where(r => !used.Contains(r)).ToList();
}
