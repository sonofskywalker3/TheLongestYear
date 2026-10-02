using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What a board may ask of the two items a player cannot farm: the Prismatic Shard and
/// the Mystery Box. Jeff's ruling, 2026-09-30, from the quantity-rules spec
/// (docs/superpowers/specs/2026-09-30-quantity-rules-design.md):
///
/// <list type="bullet">
/// <item>A capped item is asked for ONCE per slot, whatever the stack-size modifier says.</item>
/// <item>A whole board may ask for a capped item only so many times, by the STACK SIZE step:
/// Easy none, Normal one, Hard two, Extreme three. The count is the sum of stacks across the
/// board's bundles (<see cref="CountOnBoard"/>).</item>
/// </list></summary>
public static class CappedAsks
{
    public const string PrismaticShard = "(O)74";
    public const string MysteryBox = "(O)MysteryBox";

    public static readonly IReadOnlySet<string> Ids = new HashSet<string>(StringComparer.Ordinal)
    {
        PrismaticShard, MysteryBox,
    };

    private const int EasyBoardAllowance = 0;
    private const int NormalBoardAllowance = 1;
    private const int HardBoardAllowance = 2;
    private const int ExtremeBoardAllowance = 3;
    private const int CappedSlotStack = 1;

    public static bool IsCapped(string? itemId)
        => itemId != null && Ids.Contains(BundleParsing.NormalizeItemId(itemId));

    /// <summary>How many times one board may ask for each capped item at this stack-size step.</summary>
    public static int BoardAllowance(DifficultyStep stackSize)
        => stackSize switch
        {
            DifficultyStep.Easy => EasyBoardAllowance,
            DifficultyStep.Normal => NormalBoardAllowance,
            DifficultyStep.Hard => HardBoardAllowance,
            _ => ExtremeBoardAllowance,
        };

    /// <summary>A capped id always asks for one; any other id keeps its stack.</summary>
    public static int ClampStack(string? itemId, int stack)
        => IsCapped(itemId) ? CappedSlotStack : stack;

    /// <summary>Repairs one live BundleData value: every capped ask above one comes back as one,
    /// every other field survives byte for byte. Null when nothing needed changing. This only
    /// lowers stacks; it cannot hold the per-board count on a board the engine did not build
    /// (see <see cref="VanillaBoardDifficultyPass"/>).</summary>
    public static string? RepairBundleValue(string value)
        => BundleAskRewrite.LowerAsks(value, (id, stack) => Math.Min(stack, ClampStack(id, stack)));

    /// <summary>Lowers every capped slot to a stack of one. Returns the same reference when nothing
    /// needed lowering, so callers can tell a no-op by reference.</summary>
    public static BundleSpec ClampBundle(BundleSpec spec)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        if (!spec.Slots.Any(s => ClampStack(s.ItemId, s.Stack) != s.Stack))
            return spec;
        var slots = spec.Slots
            .Select(s => ClampStack(s.ItemId, s.Stack) == s.Stack ? s : s with { Stack = CappedSlotStack })
            .ToList();
        return spec with { Slots = slots };
    }

    /// <summary>Sum of the stacks that ask for <paramref name="itemId"/> across the board.</summary>
    public static int CountOnBoard(string itemId, IEnumerable<BundleSpec> board)
    {
        if (itemId == null) throw new ArgumentNullException(nameof(itemId));
        if (board == null) throw new ArgumentNullException(nameof(board));
        string wanted = BundleParsing.NormalizeItemId(itemId);
        return board
            .SelectMany(b => b.Slots)
            .Where(s => string.Equals(BundleParsing.NormalizeItemId(s.ItemId), wanted, StringComparison.Ordinal))
            .Sum(s => s.Stack);
    }

    /// <summary>Rewrites <paramref name="chosen"/> in place so it holds at most
    /// <c>remaining[id]</c> of each capped id (an id missing from the map has none left). Walks in
    /// roll order so the earliest rolls stay; each surplus is swapped for a weighted pick from the
    /// candidates that are neither capped nor already chosen, or dropped when none is left.
    /// Deterministic for a given rng stream.</summary>
    public static void Enforce(
        List<PoolItem> chosen, IReadOnlyList<PoolItem> candidates,
        IReadOnlyDictionary<string, int> remaining, Random rng,
        Action<string>? log = null, string? bundleName = null)
    {
        if (chosen == null) throw new ArgumentNullException(nameof(chosen));
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        if (remaining == null) throw new ArgumentNullException(nameof(remaining));
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        var taken = new HashSet<string>(chosen.Select(c => c.ItemId), StringComparer.Ordinal);
        var kept = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < chosen.Count; i++)
        {
            PoolItem item = chosen[i];
            if (!IsCapped(item.ItemId))
                continue;

            string id = BundleParsing.NormalizeItemId(item.ItemId);
            int left = remaining.TryGetValue(id, out int r) ? r : 0;
            kept.TryGetValue(id, out int have);
            if (have < left)
            {
                kept[id] = have + 1;
                continue;
            }

            List<PoolItem> replacements = candidates
                .Where(c => !IsCapped(c.ItemId) && !taken.Contains(c.ItemId))
                .ToList();
            if (replacements.Count == 0)
            {
                log?.Invoke($"'{bundleName}': dropped capped ask {item.ItemId} (board has {left} left); nothing left to swap in.");
                chosen.RemoveAt(i);
                i--;
                continue;
            }
            PoolItem pick = WeightedSampler.Sample(replacements, 1, rng)[0];
            taken.Add(pick.ItemId);
            chosen[i] = pick;
            log?.Invoke($"'{bundleName}': swapped capped ask {item.ItemId} (board has {left} left) for {pick.ItemId}.");
        }
    }
}
