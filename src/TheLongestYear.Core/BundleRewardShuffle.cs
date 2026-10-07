using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Randomizer option "Random bundle rewards": every bundle on a board gets a reward
/// drawn from the pool of rewards vanilla bundles can give. Fixed per board: the draw depends
/// only on the board seed and the bundle's "Room/Index" key, so a reload, a held board and the
/// manifest re-check all reproduce the same rewards.
///
/// Two forms that must agree for the same key: <see cref="Apply"/> on engine specs and
/// <see cref="ApplyToData"/> on a written Data/Bundles board (Vanilla and Remixed sources).</summary>
public static class BundleRewardShuffle
{
    private const int Salt = 0x3E71;

    /// <summary>Knuth's multiplicative constant. Multiplying the board seed spreads adjacent
    /// seeds apart before they reach <see cref="Random"/>, whose first draw is nearly the same
    /// for seeds that differ by one.</summary>
    private const uint SeedMultiplier = 2654435761u;

    private const uint FnvOffset = 2166136261u;
    private const uint FnvPrime = 16777619u;

    /// <summary>The one room the shuffle leaves alone, rewards and pool alike. The Vault is
    /// shuffled: its field 1 is an ordinary item reward ("O 220 3") the Junimo note menu grants
    /// like any other room's, and field 2 (the gold ask) is never touched.</summary>
    private const string SkippedRoom = "Abandoned Joja Mart";

    /// <summary>Whether the shuffle leaves this room's rewards alone and keeps them out of the pool.</summary>
    public static bool SkipsRoom(string room) => string.Equals(room, SkippedRoom, StringComparison.Ordinal);

    private const char FieldSeparator = '/';
    private const int RewardFieldIndex = 1;

    /// <summary>Distinct, non-empty, no '/' (it would break the bundle string), ordinal-sorted
    /// so the pool, and with it every pick, is the same on every machine.</summary>
    public static IReadOnlyList<string> CleanPool(IEnumerable<string> rewards)
        => rewards
            .Where(r => !string.IsNullOrWhiteSpace(r) && r.IndexOf(FieldSeparator) < 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(r => r, StringComparer.Ordinal)
            .ToList();

    /// <summary>The reward for one bundle on one board. <paramref name="pool"/> must not be empty.</summary>
    public static string RewardFor(int boardSeed, string bundleKey, IReadOnlyList<string> pool)
    {
        var rng = new Random(SeedFor(boardSeed, bundleKey));
        return pool[rng.Next(pool.Count)];
    }

    /// <summary>Engine form: returns the specs with each non-skipped bundle's RewardField
    /// replaced. An empty pool returns the input unchanged.</summary>
    public static IReadOnlyList<BundleSpec> Apply(
        IReadOnlyList<BundleSpec> specs, int boardSeed, IReadOnlyList<string> pool, Func<string, bool> skipRoom)
    {
        if (pool.Count == 0)
            return specs;
        return specs
            .Select(s => skipRoom(s.Room)
                ? s
                : s with { RewardField = RewardFor(boardSeed, $"{s.Room}/{s.Index}", pool) })
            .ToList();
    }

    /// <summary>Data form: board keyed "Room/Index" to the slash-delimited bundle string. Field 1
    /// (reward) is replaced on each non-skipped bundle; every other field stays byte-identical.
    /// An empty pool returns a copy of the input.</summary>
    public static IDictionary<string, string> ApplyToData(
        IReadOnlyDictionary<string, string> board, int boardSeed, IReadOnlyList<string> pool, Func<string, bool> skipRoom)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, string> entry in board)
        {
            int sep = entry.Key.IndexOf(FieldSeparator);
            string room = sep < 0 ? entry.Key : entry.Key.Substring(0, sep);
            string[] fields = entry.Value.Split(FieldSeparator);
            if (pool.Count == 0 || skipRoom(room) || fields.Length <= RewardFieldIndex)
            {
                result[entry.Key] = entry.Value;
                continue;
            }
            fields[RewardFieldIndex] = RewardFor(boardSeed, entry.Key, pool);
            result[entry.Key] = string.Join(FieldSeparator, fields);
        }
        return result;
    }

    private static int SeedFor(int boardSeed, string bundleKey)
        => unchecked((int)((uint)boardSeed * SeedMultiplier) ^ StableHash(bundleKey) ^ Salt);

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
