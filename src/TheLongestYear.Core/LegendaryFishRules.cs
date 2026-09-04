using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What a bundle may ask of a legendary fish. Jeff's ruling, 2026-09-04, off Nexus bug
/// 1127469 (gazumbrado: "2 silver Mutant Carps for my lake fish bundle", "2 glacier fish for
/// Winter Star", ten Hard rerolls that every one asked for two or three of one legendary):
///
/// <list type="bullet">
/// <item>A legendary is asked for ONCE. The game lets it be caught once per loop
/// (CatchLimit 1; <see cref="CaughtFishReset"/> clears that on the rewind), so a stack of two is
/// not hard, it is impossible. The stack-size modifier rounds a plain x1 up to x2 on Hard, which is
/// exactly how every report above was produced.</item>
/// <item>A legendary is asked for at BASE quality, never silver or gold. One cast, one chance.</item>
/// <item>A bundle holds at most one legendary on Easy and Normal, two on Hard, four on Extreme,
/// and never two from the same season: a Hard bundle may want Legend and Crimsonfish (Spring and
/// Summer), an Extreme one all four seasons. Mutant Carp has no season, so it never collides, but
/// it counts against the cap like any other.</item>
/// </list>
///
/// The cap reads the ITEM RARITY step, the dial that already decides which items a board asks
/// for, so the number of legendaries on a bundle moves with the same setting as everything else
/// about what it asks.</summary>
public static class LegendaryFishRules
{
    /// <summary>The five vanilla legendaries: Legend, Crimsonfish, Angler, Glacierfish, Mutant
    /// Carp. The five Extended Family fish never reach a pool at all (they hang off a Qi order,
    /// see ItemPoolBuilder), so they need no entry here.</summary>
    public static readonly IReadOnlySet<string> Ids = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)163", "(O)159", "(O)160", "(O)775", "(O)682",
    };

    private const int OnePerBundle = 1;
    private const int TwoPerBundle = 2;
    private const int OnePerSeason = 4;

    public static bool IsLegendary(string? itemId)
        => itemId != null && Ids.Contains(BundleParsing.NormalizeItemId(itemId));

    /// <summary>How many legendaries one bundle may hold at this item-rarity step.</summary>
    public static int MaxPerBundle(DifficultyStep step)
        => step switch
        {
            DifficultyStep.Hard => TwoPerBundle,
            DifficultyStep.Extreme => OnePerSeason,
            _ => OnePerBundle,
        };

    /// <summary>Every legendary asks for one, whatever the stack roll or the stack-size dial says.</summary>
    public static int ClampStack(string itemId, int stack)
        => IsLegendary(itemId) ? 1 : stack;

    /// <summary>Every legendary asks for base quality, whatever the quality roll or dial says.</summary>
    public static int ClampQuality(string itemId, int quality)
        => IsLegendary(itemId) ? 0 : quality;

    /// <summary>Rewrites <paramref name="chosen"/> in place so it holds at most
    /// <see cref="MaxPerBundle"/> legendaries and no two of them share a season. Each surplus
    /// legendary is swapped for a weighted pick from the non-legendary candidates the bundle has
    /// not already taken, so the bundle keeps its slot count. When no replacement is left the
    /// surplus slot is dropped, and the caller's slot count shrinks by one; that is still better
    /// than an ask the game cannot satisfy. Deterministic for a given rng stream.</summary>
    public static void Enforce(
        List<PoolItem> chosen, IReadOnlyList<PoolItem> candidates, DifficultyStep step, Random rng,
        Action<string>? log = null, string? bundleName = null)
    {
        if (chosen == null) throw new ArgumentNullException(nameof(chosen));
        if (candidates == null) throw new ArgumentNullException(nameof(candidates));
        if (rng == null) throw new ArgumentNullException(nameof(rng));

        int cap = MaxPerBundle(step);
        var keptSeasons = new HashSet<Season>();
        var taken = new HashSet<string>(chosen.Select(c => c.ItemId), StringComparer.Ordinal);
        int kept = 0;

        // Walk in roll order so the earliest-rolled legendaries are the ones that stay: the
        // sampler's first picks are the ones the weights favoured.
        for (int i = 0; i < chosen.Count; i++)
        {
            PoolItem item = chosen[i];
            if (!IsLegendary(item.ItemId))
                continue;

            bool underCap = kept < cap;
            bool seasonFree = item.Seasons.All(s => !keptSeasons.Contains(s));
            if (underCap && seasonFree)
            {
                kept++;
                foreach (Season s in item.Seasons) keptSeasons.Add(s);
                continue;
            }

            string why = !underCap ? $"over the {step} cap of {cap}" : "shares a season with one already kept";
            List<PoolItem> replacements = candidates
                .Where(c => !IsLegendary(c.ItemId) && !taken.Contains(c.ItemId))
                .ToList();
            if (replacements.Count == 0)
            {
                log?.Invoke($"'{bundleName}': dropped legendary {item.ItemId} ({why}); nothing left to swap in.");
                chosen.RemoveAt(i);
                i--;
                continue;
            }
            PoolItem pick = WeightedSampler.Sample(replacements, 1, rng)[0];
            taken.Add(pick.ItemId);
            chosen[i] = pick;
            log?.Invoke($"'{bundleName}': swapped legendary {item.ItemId} ({why}) for {pick.ItemId}.");
        }
    }
}
