using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Bundle indices for the extra bundles the bundle-count dial adds to a room. Vanilla
/// 1.6.15 uses 0 to 36 and Tech's Cross-Mod Bundles reuses vanilla's keys, so the range starts far
/// above both; any index a room pool already uses is skipped as well. Handed out in call order,
/// so a generation that walks rooms in a fixed order gets the same indices every time.
/// Spec 2026-10-09-bundle-count-dial.</summary>
public sealed class ReservedBundleIndices
{
    public const int First = 9000;

    private readonly HashSet<int> _used;
    private int _next = First;

    public ReservedBundleIndices(IEnumerable<int> usedIndices)
    {
        _used = new HashSet<int>(usedIndices ?? Array.Empty<int>());
    }

    public static bool IsReserved(int index) => index >= First;

    public int Next()
    {
        while (_used.Contains(_next))
            _next++;
        _used.Add(_next);
        return _next++;
    }
}

/// <summary>Turns a room's standard picks (one per position, from <see cref="RemixSelector"/>)
/// into the number of bundles the bundle-count dial asks for. Spec 2026-10-09-bundle-count-dial.
///
/// Fewer: drops picks at random, never a seasonal Crops bundle
/// (<see cref="RemixSelector.IsAlwaysPicked"/>) while anything else can go; order and indices of
/// the kept picks are unchanged. More: each extra draws from every candidate the room offers (first
/// occurrence of each name), leaving out names already in the room, and takes a reserved index.
/// When no new name is left the room stops short and <c>shortfall</c> says by how many. An extra
/// also leaves out bundles whose theme (<see cref="BundleThemes"/>) the board or the room already
/// fields, unless only such bundles remain (Jeff, 2026-11-10: at most one bundle per theme).</summary>
public static class RoomBundleCountPlanner
{
    public static IReadOnlyList<BundleSpec> Plan(
        IReadOnlyList<BundleSpec> picks,
        IReadOnlyList<IReadOnlyList<BundleSpec>> positions,
        int target,
        Random rng,
        Func<int> nextReservedIndex,
        out int shortfall,
        IReadOnlySet<string>? boardThemes = null)
    {
        if (picks == null) throw new ArgumentNullException(nameof(picks));
        if (rng == null) throw new ArgumentNullException(nameof(rng));
        shortfall = 0;

        if (target == picks.Count)
            return picks;
        if (target < picks.Count)
            return Drop(picks, picks.Count - target, rng);
        return Add(picks, positions, target - picks.Count, rng, nextReservedIndex, out shortfall, boardThemes);
    }

    private static IReadOnlyList<BundleSpec> Drop(IReadOnlyList<BundleSpec> picks, int dropCount, Random rng)
    {
        var droppable = Enumerable.Range(0, picks.Count).Where(i => !RemixSelector.IsAlwaysPicked(picks[i].Name)).ToList();
        var protectedOnes = Enumerable.Range(0, picks.Count).Where(i => RemixSelector.IsAlwaysPicked(picks[i].Name)).ToList();
        var dropped = new HashSet<int>();
        for (int n = 0; n < dropCount; n++)
        {
            List<int> from = droppable.Count > 0 ? droppable : protectedOnes;
            if (from.Count == 0)
                break;
            int at = rng.Next(from.Count);
            dropped.Add(from[at]);
            from.RemoveAt(at);
        }
        return picks.Where((_, i) => !dropped.Contains(i)).ToList();
    }

    private static IReadOnlyList<BundleSpec> Add(
        IReadOnlyList<BundleSpec> picks,
        IReadOnlyList<IReadOnlyList<BundleSpec>> positions,
        int addCount,
        Random rng,
        Func<int> nextReservedIndex,
        out int shortfall,
        IReadOnlySet<string>? boardThemes)
    {
        if (nextReservedIndex == null) throw new ArgumentNullException(nameof(nextReservedIndex));
        var result = new List<BundleSpec>(picks);
        var names = new HashSet<string>(picks.Select(p => p.Name), StringComparer.Ordinal);
        var usedThemes = new HashSet<string>(boardThemes ?? new HashSet<string>(), StringComparer.Ordinal);
        foreach (BundleSpec pick in picks)
            BundleThemes.MarkUsed(usedThemes, pick.Name);
        var pool = new List<BundleSpec>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (IReadOnlyList<BundleSpec> candidates in positions ?? Array.Empty<IReadOnlyList<BundleSpec>>())
        foreach (BundleSpec candidate in candidates)
            if (seen.Add(candidate.Name))
                pool.Add(candidate);

        shortfall = 0;
        for (int n = 0; n < addCount; n++)
        {
            List<BundleSpec> available = pool.Where(c => !names.Contains(c.Name)).ToList();
            if (available.Count == 0)
            {
                shortfall = addCount - n;
                break;
            }
            IReadOnlyList<BundleSpec> free = BundleThemes.WithoutTakenThemes(available, usedThemes);
            BundleSpec chosen = free[rng.Next(free.Count)];
            names.Add(chosen.Name);
            BundleThemes.MarkUsed(usedThemes, chosen.Name);
            result.Add(chosen with { Index = nextReservedIndex() });
        }
        return result;
    }
}
