using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

public static partial class BundleSlotFiller
{
    /// <summary>One replacement candidate for a single slot of an EXISTING bundle, drawn from the
    /// same pool (and, for a Recipe bundle, the same PART) the slot came from, excluding everything
    /// already asked for on the board. Null when nothing suitable exists, which the caller must
    /// treat as "leave the slot alone", never as success.
    ///
    /// Deliberately NOT <see cref="Fill"/>. Fill rolls a WHOLE bundle: run on a live board it would
    /// re-roll slots a player has already donated, and run on a fabricated one-slot bundle it would
    /// lose the recipe-part identity that makes Dye ask for one item per colour and Field Research
    /// one of each of four things. This picks exactly one item for exactly one slot and touches
    /// nothing else (spec 2026-09-10-source-reachability, task 9).
    ///
    /// <paramref name="avoid"/> is a HARD veto here, unlike in <see cref="Fill"/>, where it yields
    /// once the pool runs short: a repair that hands back an id the board already asks for has not
    /// repaired anything. Legendary fish are out of every repair draw, because the board's
    /// legendary allowance was spent when the board was generated and this pass has no way to know
    /// what is left of it. The Prismatic Shard and Mystery Box (<see cref="CappedAsks"/>) are out
    /// for the same reason.
    ///
    /// <paramref name="tuning"/> is unused today. It is in the signature so a caller passes the
    /// same block <see cref="Fill"/> takes and a later rule that needs it (a stack or quality
    /// judgement made here rather than by the caller) costs no call-site change.</summary>
    public static PoolItem? ReplacementFor(
        BundleSpec spec, int slotIndex, DomainMatch match, ItemPools pools,
        BundleGenerationTuning tuning, Random rng, IReadOnlySet<string> avoid,
        ItemAvailabilityModel? availability, PoolRecipe? knownRecipe, Action<string>? log = null)
    {
        if (spec == null || pools == null || rng == null || match == null) return null;
        if (match.Domain == PoolDomain.None) return null;
        if (slotIndex < 0 || slotIndex >= spec.Slots.Count) return null;

        IReadOnlyList<PoolItem> candidates;
        if (match.Domain == PoolDomain.Recipe)
        {
            PoolRecipe recipe = knownRecipe
                ?? BundlePoolRecipes.For(spec.Name, VanillaIds(spec), pools, availability);
            List<IReadOnlyList<PoolItem>> parts = recipe.Parts
                .Select(part => part.Source(pools, availability)).ToList();
            int part = PartIndexForSlot(recipe, slotIndex, spec.Slots.Count);
            if (part >= 0 && part < parts.Count)
            {
                candidates = parts[part];
            }
            else
            {
                // The union, not a guessed part: a replacement from the WRONG part gives a
                // thematically odd bundle, while the union only gives a broader draw. Both stay
                // inside the recipe, which is what keeps the pick reachable.
                candidates = BundlePoolRecipes.Union(parts.ToArray());
                log?.Invoke(
                    $"'{spec.Name}': could not identify which recipe part slot {slotIndex} came from " +
                    $"({recipe.Parts.Count} part(s) against {spec.Slots.Count} slot(s)); drawing from the whole recipe.");
            }
        }
        else
        {
            candidates = Candidates(spec, match, pools, availability, knownRecipe);
        }

        // Nothing the bundle already asks for, including the very id being replaced.
        var taken = new HashSet<string>(StringComparer.Ordinal);
        foreach (BundleSlotSpec slot in spec.Slots)
        {
            if (string.IsNullOrEmpty(slot.ItemId) || slot.ItemId == MoneySlotId
                || BundleParsing.IsCategoryRef(slot.ItemId))
                continue;
            taken.Add(BundleParsing.NormalizeItemId(slot.ItemId));
        }

        List<PoolItem> pool = candidates
            .Where(p => !taken.Contains(p.ItemId)
                        && !(avoid != null && avoid.Contains(p.ItemId))
                        && !LegendaryFishRules.IsLegendary(p.ItemId)
                        && !CappedAsks.IsCapped(p.ItemId))
            .ToList();

        // Night Fishing's one-Night-Market-fish cap: if the bundle's other slots already hold as
        // many as it may, the replacement cannot be another one.
        (Func<PoolItem, bool>? capped, int cap) = CapFor(spec, match, pools);
        if (capped != null && cap < int.MaxValue)
        {
            string replaced = BundleParsing.NormalizeItemId(spec.Slots[slotIndex].ItemId ?? "");
            var others = new HashSet<string>(taken, StringComparer.Ordinal);
            others.Remove(replaced);
            int held = candidates.Count(p => capped(p) && others.Contains(p.ItemId));
            if (held >= cap)
                pool = pool.Where(p => !capped(p)).ToList();
        }

        if (pool.Count == 0) return null;
        List<PoolItem> picked = WeightedSampler.Sample(pool, 1, rng);
        return picked.Count > 0 ? picked[0] : null;
    }

    /// <summary>Which recipe part a given slot index belongs to, walking the parts in the same
    /// fixed order <see cref="SampleByParts"/> filled them in, so a repair draws from the part the
    /// original slot actually came from. -1 means "cannot be identified", and the caller must then
    /// draw from the whole recipe rather than from a guessed part.
    ///
    /// This RECONSTRUCTS the boundaries by assumption, because they are not recorded anywhere: the
    /// generation-time part of each slot is not written into BundleData and cannot be recovered
    /// from it. The assumption is that every part filled exactly the count it asked for, which
    /// holds for a board this engine generated with fully-filled parts, and fails in two ways:
    ///
    /// <list type="bullet">
    /// <item>A part that came up SHORT. <see cref="SampleByParts"/> fills through
    /// <c>WeightedSampler.Sample</c>, which can return fewer items than asked for even after the
    /// union fallback; <c>chosen.Count</c> then advances by less than the part wanted and every
    /// later boundary shifts. That shortfall leaves no trace in the finished bundle, so it cannot
    /// be detected here. What CAN be detected is the structural version of the same problem: parts
    /// whose counts do not add up to the bundle's slots at all, which is the check below.</item>
    /// <item>A board this engine never generated (a vanilla-preserved bundle, or another mod's),
    /// where slot order need not follow recipe-part order in the first place.</item>
    /// </list>
    ///
    /// So the answer is only trusted when the parts account for EVERY slot; anything else returns
    /// -1 and the caller falls back to the union, which is the conservative direction (a broader
    /// draw rather than a wrong-part one, both still inside the recipe).</summary>
    private static int PartIndexForSlot(PoolRecipe recipe, int slotIndex, int targetCount)
    {
        if (recipe == null || targetCount <= 0) return -1;
        int filled = 0;
        int answer = -1;
        for (int i = 0; i < recipe.Parts.Count && filled < targetCount; i++)
        {
            int remaining = targetCount - filled;
            int want = recipe.Parts[i].Count <= BundlePoolRecipes.RestOfTheSlots
                ? remaining
                : Math.Min(recipe.Parts[i].Count, remaining);
            filled += want;
            if (answer < 0 && slotIndex < filled) answer = i;
        }
        // The parts leave slots unaccounted for, so the boundaries this walk produced are not the
        // ones the bundle was actually filled with. Refuse to answer rather than answer wrongly.
        return filled == targetCount ? answer : -1;
    }
}
