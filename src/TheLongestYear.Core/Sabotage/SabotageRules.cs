using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>What a blight night goes after: the crops in the ground, or what is stored in chests.</summary>
public enum BlightTarget { Crops, Chests }

/// <summary>How many crops blight takes on a night it strikes: a share of the live crops,
/// clamped so a tiny patch loses one and a big field loses a handful, never the run.</summary>
public static class BlightRule
{
    /// <summary>ONE OR THE OTHER. A blight night used to take crops and chest stock together; "I
    /// want it to be one or the other, not both" (Jeff, 2026-09-14). Given what each side would
    /// lose, returns the pair with one of them zeroed: <paramref name="forced"/> when a playtest
    /// named a target, otherwise whichever side has anything to take, and a coin flip when both
    /// do.</summary>
    public static (int Crops, int Spoil) OneTarget(int crops, int spoil, BlightTarget? forced, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (forced == BlightTarget.Crops) return (crops, 0);
        if (forced == BlightTarget.Chests) return (0, spoil);
        if (crops <= 0) return (0, spoil);
        if (spoil <= 0) return (crops, 0);
        return rng.Next(2) == 0 ? (crops, 0) : (0, spoil);
    }

    /// <summary>How many crops die on a strike: the level's share of the live crops, at least one,
    /// capped by season and level (spec 2026-09-15 Part B, section 2.4).</summary>
    public static int Count(int liveCrops, Season season, DifficultyStep level)
        => Take(liveCrops, season, level);

    /// <summary>How many stored units go on a chest strike, from the total in unwarded chests (the
    /// Junimo Stash excluded by the caller). Same share and caps as crops.</summary>
    public static int SpoilCount(int storedUnits, Season season, DifficultyStep level)
        => Take(storedUnits, season, level);

    /// <summary>Decimal digits kept before rounding up: enough to tell 100 * 0.07 = 7 apart from a
    /// real fraction, without a double's binary rounding (7.000000000000001) forcing an extra
    /// unit.</summary>
    private const int SharePrecision = 6;

    private static int Take(int have, Season season, DifficultyStep level)
    {
        if (have <= 0) return 0;
        double raw = Math.Round(have * DarknessLevels.BlightShare(level), SharePrecision);
        int n = (int)Math.Ceiling(raw);
        n = Math.Max(SabotageTuning.BlightMinPerNight, Math.Min(DarknessLevels.BlightCap(level, season), n));
        return Math.Min(n, have);
    }

    /// <summary>Vanilla object categories that are food and so "spoil": vegetables, fruit,
    /// flowers, forage greens, fish, eggs, milk and other animal products, cooked dishes,
    /// artisan goods. Anything else taken from a chest "goes missing" instead.</summary>
    public static bool IsPerishableCategory(int category) => category switch
    {
        -75 => true,   // vegetable
        -79 => true,   // fruit
        -80 => true,   // flower
        -81 => true,   // greens / forage
        -4 => true,    // fish
        -5 => true,    // egg
        -6 => true,    // milk
        -18 => true,   // animal product
        -7 => true,    // cooking
        -26 => true,   // artisan goods
        _ => false,
    };

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

/// <summary>A filled slot the darkness may empty: in an item room, in a bundle that is not yet
/// complete (a complete bundle keeps its reward and its room restoration intact). No ward: the
/// Junimos protect crops, not the hall (Jeff, 2026-09-09).</summary>
public static class ReversionRule
{
    /// <summary>Room themes (see RoomThemeMap): the five item rooms, never the Vault or Joja.</summary>
    public static bool IsItemRoomTheme(Theme theme) => theme is Theme.Farming or Theme.Foraging or Theme.Fishing or Theme.Mining or Theme.Mixed;

    public static IReadOnlyList<DonatedSlot> Candidates(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements)
    {
        if (ledger is null) throw new ArgumentNullException(nameof(ledger));
        if (requirements is null) throw new ArgumentNullException(nameof(requirements));
        var result = new List<DonatedSlot>();
        foreach (BundleRequirement req in requirements)
        {
            if (req.BundleIndex < 0) continue;
            if (!IsItemRoomTheme(req.Theme)) continue;
            if (req.IsFullyComplete(ledger)) continue;
            foreach (DonatedSlot slot in ledger.Entries)
                if (slot.BundleIndex == req.BundleIndex)
                    result.Add(slot);
        }
        return result;
    }

    public static DonatedSlot? Pick(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, Random rng)
        => Pick(ledger, requirements, _ => true, rng);

    /// <summary>Uniform among the candidates whose item passes <paramref name="fair"/> (the
    /// FairnessRule, spec 2026-09-15 Part B, 1.6). Null when none passes.</summary>
    public static DonatedSlot? Pick(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, Func<string, bool> fair, Random rng)
    {
        if (fair is null) throw new ArgumentNullException(nameof(fair));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        List<DonatedSlot> candidates = Candidates(ledger, requirements).Where(s => fair(s.ItemId)).ToList();
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
            if (!ReversionRule.IsItemRoomTheme(req.Theme)) continue;
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

    /// <summary>The most the hall may ask for: legendary fish are always one (LegendaryFishRules,
    /// the rule QuantityAskPass already applies and the old tamper path skipped); otherwise the
    /// board's own ceiling, <see cref="AskBands.Ceiling"/> of the item's basis by Winter 28; 0 when
    /// the item has no basis, which <see cref="Stack"/> turns into an ask of one.</summary>
    public static int MaxCount(string itemId, double? basisByWinter)
    {
        if (LegendaryFishRules.IsLegendary(itemId)) return 1;
        if (basisByWinter is not double basis || basis <= 0) return 0;
        return (int)Math.Ceiling(basis * AskBands.Ceiling);
    }

    /// <summary>How many of the replacement the hall asks for (Jeff, 2026-09-09). Start from the
    /// item's normal max count, divide by the Winter weeks already passed (week 3 halves it, week
    /// 4 thirds it; weeks 1 and 2 keep it whole), then roll a 10%-wide slice of what is left by
    /// difficulty: Easy 0 to 10%, Normal 10 to 20%, Hard 20 to 30%, Extreme 30 to 40%. Never
    /// below one. An item with no known max count asks for one.</summary>
    public static int Stack(int maxCount, int weekOfWinter, DifficultyStep step, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (maxCount <= 0) return 1;
        int weeksPassed = Math.Max(1, weekOfWinter - 1);
        double remainder = (double)maxCount / weeksPassed;
        double low = (int)step * SabotageTuning.TamperSliceWidth;
        double high = low + SabotageTuning.TamperSliceWidth;
        double fraction = low + rng.NextDouble() * (high - low);
        return Math.Max(1, (int)Math.Round(remainder * fraction, MidpointRounding.AwayFromZero));
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
