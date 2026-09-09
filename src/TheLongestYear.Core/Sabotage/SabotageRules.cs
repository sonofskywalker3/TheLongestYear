using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>How many crops blight takes on a night it strikes: a share of the live crops,
/// clamped so a tiny patch loses one and a big field loses a handful, never the run.</summary>
public static class BlightRule
{
    public static int Count(int liveCrops, Season season)
    {
        if (liveCrops <= 0) return 0;
        double share = season == Season.Fall ? SabotageTuning.BlightShareFall : SabotageTuning.BlightShareSummer;
        int max = season == Season.Fall ? SabotageTuning.BlightMaxFall : SabotageTuning.BlightMaxSummer;
        int n = (int)Math.Ceiling(liveCrops * share);
        n = Math.Max(SabotageTuning.BlightMinPerNight, Math.Min(max, n));
        return Math.Min(n, liveCrops);
    }

    /// <summary>Which of the live crops die: <paramref name="count"/> distinct positions, uniform.</summary>
    public static IReadOnlyList<int> PickIndexes(int liveCrops, int count, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        count = Math.Max(0, Math.Min(count, liveCrops));
        var all = Enumerable.Range(0, liveCrops).ToList();
        for (int i = all.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (all[i], all[j]) = (all[j], all[i]);
        }
        return all.Take(count).ToList();
    }
}

/// <summary>A filled slot the darkness may empty: in an unwarded item room, in a bundle that is
/// not yet complete (a complete bundle keeps its reward and its room restoration intact).</summary>
public static class ReversionRule
{
    public static IReadOnlyList<DonatedSlot> Candidates(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, Func<string, bool> hasWard)
    {
        if (ledger is null) throw new ArgumentNullException(nameof(ledger));
        if (requirements is null) throw new ArgumentNullException(nameof(requirements));
        if (hasWard is null) throw new ArgumentNullException(nameof(hasWard));
        var result = new List<DonatedSlot>();
        foreach (BundleRequirement req in requirements)
        {
            if (req.BundleIndex < 0) continue;
            string? ward = WardIds.HallWardFor(req.Theme);
            if (ward is null || hasWard(ward)) continue;
            if (req.IsFullyComplete(ledger)) continue;
            foreach (DonatedSlot slot in ledger.Entries)
                if (slot.BundleIndex == req.BundleIndex)
                    result.Add(slot);
        }
        return result;
    }

    public static DonatedSlot? Pick(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, Func<string, bool> hasWard, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        IReadOnlyList<DonatedSlot> candidates = Candidates(ledger, requirements, hasWard);
        return candidates.Count == 0 ? null : candidates[rng.Next(candidates.Count)];
    }
}

/// <summary>One item the tamper roll may swap in: its id, room theme and effort, already
/// filtered by the glue to things the availability model places in Winter.</summary>
public sealed record TamperCandidate(string ItemId, Theme Theme, int Effort);

/// <summary>An unfilled slot the darkness may rewrite, with the bundle it sits in.</summary>
public sealed record TamperTarget(BundleRequirement Bundle, int IngredientIndex, string ItemId);

/// <summary>Winter's unavoidable front: pick an unfilled slot (the ones whose item the player is
/// holding first, that is the sting) and a replacement of similar effort the bundle does not
/// already ask for.</summary>
public static class TamperRule
{
    public static IReadOnlyList<TamperTarget> Targets(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements)
    {
        if (ledger is null) throw new ArgumentNullException(nameof(ledger));
        if (requirements is null) throw new ArgumentNullException(nameof(requirements));
        var result = new List<TamperTarget>();
        foreach (BundleRequirement req in requirements)
        {
            if (req.BundleIndex < 0) continue;
            if (WardIds.HallWardFor(req.Theme) is null) continue;   // not an item room
            if (req.IsFullyComplete(ledger)) continue;
            foreach (BundleSlot slot in req.Slots)
                if (!ledger.IsFilled(req.BundleIndex, slot.IngredientIndex))
                    result.Add(new TamperTarget(req, slot.IngredientIndex, slot.ItemId));
        }
        return result;
    }

    /// <summary>Slots whose item the player holds come first; within a tier the order is random.</summary>
    public static TamperTarget? PickTarget(
        IReadOnlyList<TamperTarget> targets, Func<string, bool> playerHolds, Random rng)
    {
        if (targets is null) throw new ArgumentNullException(nameof(targets));
        if (playerHolds is null) throw new ArgumentNullException(nameof(playerHolds));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (targets.Count == 0) return null;
        var held = targets.Where(t => playerHolds(t.ItemId)).ToList();
        var pool = held.Count > 0 ? held : targets.ToList();
        return pool[rng.Next(pool.Count)];
    }

    /// <summary>The replacement: same room theme (the Bulletin Board's Mixed takes any), not
    /// already in the bundle, then the closest few in effort to the original, one at random.</summary>
    public static TamperCandidate? PickReplacement(
        TamperTarget target, int originalEffort, IReadOnlyList<TamperCandidate> candidates, Random rng)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        var inBundle = new HashSet<string>(target.Bundle.Ingredients, StringComparer.Ordinal);
        List<TamperCandidate> eligible = candidates
            .Where(c => !inBundle.Contains(c.ItemId))
            .Where(c => target.Bundle.Theme == Theme.Mixed || c.Theme == target.Bundle.Theme)
            .OrderBy(c => Math.Abs(c.Effort - originalEffort))
            .ThenBy(c => c.ItemId, StringComparer.Ordinal)
            .Take(SabotageTuning.TamperCandidatePool)
            .ToList();
        return eligible.Count == 0 ? null : eligible[rng.Next(eligible.Count)];
    }
}
