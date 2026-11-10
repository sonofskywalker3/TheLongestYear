using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Bundle rewards on a board, from a limited pool marked off as it is handed out
/// (Jeff, 2026-11-10). Two modes, both fixed per board: they depend only on the board seed and
/// the bundles on it, so a reload, a held board and the manifest re-check all reproduce the same
/// rewards. Both run last, on their own seeded RNG, and change only reward fields.
///
/// Randomizer option "Random bundle rewards" on: <see cref="Apply"/> on engine specs and
/// <see cref="ApplyToData"/> on a written Data/Bundles board (Vanilla and Remixed sources) share
/// one allocation (<see cref="BundleRewardAllocation.Shuffled"/>) and must agree for the same board.
/// Off: <see cref="KeepVanillaRewards"/> (engine boards, the only ones holding our authored bundles).</summary>
public static class BundleRewardShuffle
{
    private const int Salt = 0x3E71;

    /// <summary>Knuth's multiplicative constant. Multiplying the board seed spreads adjacent
    /// seeds apart before they reach <see cref="Random"/>, whose first draw is nearly the same
    /// for seeds that differ by one.</summary>
    private const uint SeedMultiplier = 2654435761u;

    private const uint FnvOffset = 2166136261u;
    private const uint FnvPrime = 16777619u;

    /// <summary>Seed keys of the two modes' own RNG streams.</summary>
    private const string ShuffleStreamKey = "#random-rewards";
    private const string VanillaKeptStreamKey = "#authored-rewards";

    /// <summary>The one room the shuffle leaves alone, rewards and pool alike. The Vault is
    /// shuffled: its field 1 is an ordinary item reward ("O 220 3") the Junimo note menu grants
    /// like any other room's, and field 2 (the gold ask) is never touched.</summary>
    private const string SkippedRoom = "Abandoned Joja Mart";

    /// <summary>With random rewards off the Vault is left alone too, and its rewards are never
    /// handed to an authored bundle (Jeff, 2026-11-10: the 5,000g Vault keeps its 30 Quality
    /// Fertilizer, and Mineral no longer gives the 25,000g Vault's Crystalarium).</summary>
    private const string VaultRoom = "Vault";

    /// <summary>Whether the shuffle leaves this room's rewards alone and keeps them out of the pool.</summary>
    public static bool SkipsRoom(string room) => string.Equals(room, SkippedRoom, StringComparison.Ordinal);

    /// <summary>Whether random-rewards-off leaves this room's rewards alone and out of its sets.</summary>
    public static bool VanillaKeptSkipsRoom(string room)
        => SkipsRoom(room) || string.Equals(room, VaultRoom, StringComparison.Ordinal);

    private const char FieldSeparator = '/';
    private const int RewardFieldIndex = 1;

    /// <summary>Distinct, non-empty, no '/' (it would break the bundle string), ordinal-sorted
    /// so the pool, and with it every pick, is the same on every machine.</summary>
    /// <param name="vanillaOnlyIds">TLY Custom boards pass <see cref="VanillaItemIds.All"/>, and
    /// a reward that gives anything outside it is left out (spec 2026-10-08-custom-board-vanilla-only).
    /// Null keeps every reward, as Normal and Remixed always have.</param>
    public static IReadOnlyList<string> CleanPool(IEnumerable<string> rewards, IReadOnlySet<string>? vanillaOnlyIds = null)
        => rewards
            .Where(r => !string.IsNullOrWhiteSpace(r) && r.IndexOf(FieldSeparator) < 0)
            .Where(r => vanillaOnlyIds == null || VanillaOnlyBoard.IsVanillaReward(r, vanillaOnlyIds))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

    /// <summary>Each room's cleaned reward pool from (room, reward) pairs, the rooms
    /// <paramref name="skipRoom"/> names left out.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> RoomPools(
        IEnumerable<(string Room, string Reward)> rewards, Func<string, bool> skipRoom, IReadOnlySet<string>? vanillaOnlyIds = null)
        => rewards
            .Where(r => !skipRoom(r.Room))
            .GroupBy(r => r.Room, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => CleanPool(g.Select(r => r.Reward), vanillaOnlyIds), StringComparer.Ordinal);

    /// <summary>Each themed room's standard reward set for random-rewards-off: the rewards of a
    /// "Room/Index" board (<see cref="VanillaBundleBoard.Standard"/>), Vault and Joja left out.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> StandardRoomSets(
        IReadOnlyDictionary<string, string> board, IReadOnlySet<string>? vanillaOnlyIds = null)
        => RoomPools(
            board.Select(entry => (RoomOf(entry.Key), RewardOf(entry.Value))),
            VanillaKeptSkipsRoom, vanillaOnlyIds);

    /// <summary>Random rewards on, engine form: every non-skipped bundle's RewardField drawn from
    /// <paramref name="roomPools"/> (see <see cref="BundleRewardAllocation.Shuffled"/>). Output
    /// order is the input's. An empty pool returns the rewards unchanged.</summary>
    public static IReadOnlyList<BundleSpec> Apply(
        IReadOnlyList<BundleSpec> specs, int boardSeed,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roomPools, Func<string, bool> skipRoom)
    {
        List<int> open = Enumerable.Range(0, specs.Count).Where(i => !skipRoom(specs[i].Room)).ToList();
        string[] rewards = BundleRewardAllocation.Shuffled(
            open.Select(i => new RewardSlot(specs[i].Room, specs[i].Index, specs[i].RewardField, false)).ToList(),
            StreamFor(boardSeed, ShuffleStreamKey), Cleaned(roomPools));
        return WithRewards(specs, open, rewards);
    }

    /// <summary>Random rewards on, data form: board keyed "Room/Index" to the slash-delimited
    /// bundle string. Field 1 (reward) is replaced on each non-skipped bundle; every other field
    /// stays byte-identical. Agrees with <see cref="Apply"/> on the same board. An empty pool
    /// returns a copy of the input.</summary>
    public static IDictionary<string, string> ApplyToData(
        IReadOnlyDictionary<string, string> board, int boardSeed,
        IReadOnlyDictionary<string, IReadOnlyList<string>> roomPools, Func<string, bool> skipRoom)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        var openKeys = new List<string>();
        var slots = new List<RewardSlot>();
        foreach (KeyValuePair<string, string> entry in board)
        {
            result[entry.Key] = entry.Value;
            string room = RoomOf(entry.Key);
            if (skipRoom(room) || entry.Value.Split(FieldSeparator).Length <= RewardFieldIndex)
                continue;
            openKeys.Add(entry.Key);
            slots.Add(new RewardSlot(room, IndexOf(entry.Key), RewardOf(entry.Value), false));
        }
        string[] rewards = BundleRewardAllocation.Shuffled(slots, StreamFor(boardSeed, ShuffleStreamKey), Cleaned(roomPools));
        for (int i = 0; i < openKeys.Count; i++)
        {
            string[] fields = result[openKeys[i]].Split(FieldSeparator);
            fields[RewardFieldIndex] = rewards[i];
            result[openKeys[i]] = string.Join(FieldSeparator, fields);
        }
        return result;
    }

    /// <summary>Random rewards off, engine form (see <see cref="BundleRewardAllocation.VanillaKept"/>):
    /// vanilla and remix bundles keep their rewards; each authored bundle
    /// (<paramref name="isAuthored"/>, by identity) takes an unused reward from
    /// <paramref name="standardRoomSets"/>. Vault and Joja untouched. Output order is the input's.</summary>
    public static IReadOnlyList<BundleSpec> KeepVanillaRewards(
        IReadOnlyList<BundleSpec> specs, int boardSeed,
        IReadOnlyDictionary<string, IReadOnlyList<string>> standardRoomSets, Func<BundleSpec, bool> isAuthored)
    {
        List<int> open = Enumerable.Range(0, specs.Count).Where(i => !VanillaKeptSkipsRoom(specs[i].Room)).ToList();
        string[] rewards = BundleRewardAllocation.VanillaKept(
            open.Select(i => new RewardSlot(specs[i].Room, specs[i].Index, specs[i].RewardField, isAuthored(specs[i]))).ToList(),
            StreamFor(boardSeed, VanillaKeptStreamKey), Cleaned(standardRoomSets));
        return WithRewards(specs, open, rewards);
    }

    private static IReadOnlyList<BundleSpec> WithRewards(IReadOnlyList<BundleSpec> specs, List<int> open, string[] rewards)
    {
        var result = specs.ToList();
        for (int k = 0; k < open.Count; k++)
            if (!string.Equals(result[open[k]].RewardField, rewards[k], StringComparison.Ordinal))
                result[open[k]] = result[open[k]] with { RewardField = rewards[k] };
        return result;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> Cleaned(IReadOnlyDictionary<string, IReadOnlyList<string>> pools)
        => pools.ToDictionary(kv => kv.Key, kv => CleanPool(kv.Value), StringComparer.Ordinal);

    private static string RoomOf(string key)
    {
        int sep = key.IndexOf(FieldSeparator);
        return sep < 0 ? key : key.Substring(0, sep);
    }

    /// <summary>The absolute index of a "Room/Index" key; a key without one sorts last.</summary>
    private static int IndexOf(string key)
    {
        int sep = key.IndexOf(FieldSeparator);
        return sep >= 0 && int.TryParse(key.Substring(sep + 1), out int index) ? index : int.MaxValue;
    }

    private static string RewardOf(string value)
    {
        string[] fields = value.Split(FieldSeparator);
        return fields.Length > RewardFieldIndex ? fields[RewardFieldIndex] : string.Empty;
    }

    private static Random StreamFor(int boardSeed, string streamKey)
        => new(unchecked((int)((uint)boardSeed * SeedMultiplier) ^ StableHash(streamKey) ^ Salt));

    /// <summary>FNV-1a over the key's chars. <see cref="string.GetHashCode()"/> is randomised per
    /// process in .NET Core, which would make a reload produce different rewards.</summary>
    private static int StableHash(string text)
    {
        unchecked
        {
            uint hash = FnvOffset;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= FnvPrime;
            }
            return (int)hash;
        }
    }
}
