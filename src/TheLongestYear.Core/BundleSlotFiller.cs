using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Seeded re-roll of a picked bundle's slot contents from its domain's item
/// pool (spec "expanded-pool remix"): weighted sample without replacement (no duplicate
/// items per bundle), season filtering for seasonal domains, habitat / night filtering for
/// fish (<see cref="FishBundleCandidates"/>), stack/quality rolls from the
/// BundleGenerationTuning block, and the large-quantity forage ask. An optional
/// <c>avoid</c> set (every item other bundles on this board already ask for) is left out
/// while the pool can still fill every slot without it. Returns the input spec
/// UNCHANGED (reference-equal) when the domain is
/// None or the filtered pool cannot fill every slot with distinct items — the safe
/// fallback the caller logs.</summary>
public static class BundleSlotFiller
{
    private const int QualityGold = 2;
    private const int QualitySilver = 1;
    private const string MoneySlotId = "-1";

    /// <summary>Items that fish out at base quality only, whatever the roll says —
    /// see <see cref="RollQuality"/>. Public because <see cref="VanillaBoardDifficultyPass"/>
    /// must honour exactly the same set when the quality-asks modifier adds a star to a
    /// vanilla-authored board (Nexus 1122358: a quality ask on an item the game never stars is
    /// an impossible slot).</summary>
    public static readonly IReadOnlySet<string> BuiltInQualityIneligibleItemIds =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "(O)152", // Seaweed
            "(O)153", // Green Algae
            "(O)157", // White Algae
        };

    /// <summary>The hard-item rule (spec 2026-08-28-obtainable-board-2-stretch, section 3) only
    /// applies to a bundle rolling at least this many slots.</summary>
    public const int MinSlotsForHardItem = 4;

    /// <summary>Whether the hard-item rule is on for this model: never on Easy, every other step.
    ///
    /// Deliberately NOT <see cref="StretchRule.Applies"/>. The two rules used to share that one
    /// gate, and once the stretch rule became pacing-mode-only the shared gate would have switched
    /// the hard-item rule off on Hard and Extreme as well, leaving those boards easier than
    /// Normal. The hard-item rule has nothing to do with which week a gate reads.</summary>
    public static bool HardItemRuleApplies(ItemAvailabilityModel model)
        => model != null && model.Step != DifficultyStep.Easy;

    public static BundleSpec Fill(
        BundleSpec spec, DomainMatch match, ItemPools pools,
        BundleGenerationTuning tuning, Random rng,
        PityTrim? trim = null, RarityThresholds? thresholds = null, Action<string>? log = null,
        IReadOnlySet<string>? avoid = null, ItemAvailabilityModel? availability = null)
    {
        if (match.Domain == PoolDomain.None)
            return spec;

        // Recipe bundles roll part by part (Dye: one item per colour; Field Research: one of each
        // of four things), so the parts are resolved once here, and every later pass runs on their
        // union: the pity trim, the avoid set, the stretch swap and the hard-item swap.
        PoolRecipe? recipe = match.Domain == PoolDomain.Recipe
            ? BundlePoolRecipes.For(spec.Name, VanillaIds(spec), pools, availability)
            : null;
        List<IReadOnlyList<PoolItem>> parts = recipe == null
            ? new List<IReadOnlyList<PoolItem>>()
            : recipe.Parts.Select(part => part.Source(pools, availability)).ToList();
        IReadOnlyList<PoolItem> candidates = recipe == null
            ? Candidates(spec, match, pools)
            : BundlePoolRecipes.Union(parts.ToArray());
        int targetCount = spec.PickCount > 0
            ? Math.Min(spec.PickCount, spec.Slots.Count)
            : spec.Slots.Count;

        // Season pity, reshuffle path (spec 2026-08-25): quality-off costs one unit for the whole
        // bundle when the domain rolls quality; the rest remove the hardest candidates, never
        // below what this bundle needs to fill.
        bool qualityOff = false;
        if (TrimApplies(match, trim))
        {
            int before = candidates.Count;
            int units = trim!.Units;
            if (DomainRollsQuality(match.Domain) && units > 0)
            {
                qualityOff = true;
                units -= 1;
            }
            candidates = ItemHardness.Trim(candidates, units, targetCount, match.Domain, thresholds ?? new RarityThresholds());
            int after = candidates.Count;
            if (log != null)
            {
                int removed = before - after;
                string guardNote = after == targetCount && removed < units ? " (guard stopped early)" : "";
                log($"pity trim '{spec.Name}': {before} candidates -> {after} (units {trim.Units}, quality off {qualityOff}, need {targetCount}){guardNote}");
            }
            // Carry the trim into the parts: a part keeps only what survived, unless nothing of
            // it did, in which case the part stands as it was rather than becoming unfillable.
            if (recipe != null)
            {
                var kept = new HashSet<string>(candidates.Select(p => p.ItemId), StringComparer.Ordinal);
                for (int i = 0; i < parts.Count; i++)
                {
                    IReadOnlyList<PoolItem> trimmed = parts[i].Where(p => kept.Contains(p.ItemId)).ToList();
                    if (trimmed.Count > 0)
                        parts[i] = trimmed;
                }
            }
        }

        (Func<PoolItem, bool>? capped, int cap) = CapFor(spec, match, pools);

        // No item asked twice across the board (2026-08-28): drop what other bundles already
        // ask for, unless that would leave this bundle unable to fill.
        if (avoid != null && avoid.Count > 0)
        {
            IReadOnlyList<PoolItem> fresh = candidates.Where(p => !avoid.Contains(p.ItemId)).ToList();
            if (WeightedSampler.Capacity(fresh, capped, cap) >= targetCount)
                candidates = fresh;
            else
                log?.Invoke($"'{spec.Name}': only {fresh.Count} candidates no other bundle asks for (need {targetCount}); allowing repeats.");
        }

        if (WeightedSampler.Capacity(candidates, capped, cap) < targetCount)
            return spec;

        List<PoolItem> chosen = recipe == null
            ? WeightedSampler.Sample(candidates, targetCount, rng, capped, cap)
            : SampleByParts(spec, recipe, parts, candidates, targetCount, rng, avoid, log);
        // Stretch swap and hard-item swap (spec 2026-08-28-obtainable-board-2-stretch, sections 2
        // and 3), replacing the Spring foothold: never on Easy, never on a season-named bundle
        // (it gates its own season by nature).
        if (availability != null && match.Season == null && HardItemRuleApplies(availability))
        {
            // Never on Easy, and never on a season-named bundle (it gates its own season by
            // nature). The stretch pass has the extra condition of a pacing-mode model; the
            // hard-item rule below runs on every step above Easy.
            bool stretches = StretchRule.Applies(availability);
            var chosenIds = new HashSet<string>(chosen.Select(c => c.ItemId), StringComparer.Ordinal);

            // Stretch swap (spec section 2): for each season the chosen list gains nothing in, hold a
            // stretch item; swap the last non-reachable slot for one from the pool when it holds none.
            // <paramref name="keep"/> is the index the hard-item swap just filled: the re-run below
            // must not take the hard item straight back out again.
            void StretchPass(int keep)
            {
                foreach (Season season in StretchRule.StretchSeasons)
                {
                    bool gains = chosen.Any(c => Gains(availability.For(c.ItemId), season));
                    bool holdsStretch = chosen.Any(c => StretchRule.IsStretchFor(availability.For(c.ItemId), season));
                    if (gains || holdsStretch) continue;
                    List<PoolItem> stretchPool = candidates
                        .Where(c => !chosenIds.Contains(c.ItemId) && StretchRule.IsStretchFor(availability.For(c.ItemId), season))
                        .ToList();
                    if (stretchPool.Count == 0) { log?.Invoke($"'{spec.Name}': no stretch item for {season} in its pool."); continue; }
                    int victim = -1;
                    for (int i = chosen.Count - 1; i >= 0; i--)
                        if (i != keep && !StretchRule.IsReachable(availability.For(chosen[i].ItemId), season)) { victim = i; break; }
                    if (victim < 0) continue;
                    PoolItem pick = WeightedSampler.Sample(stretchPool, 1, rng)[0];
                    chosenIds.Remove(chosen[victim].ItemId);
                    chosen[victim] = pick;
                    chosenIds.Add(pick.ItemId);
                    log?.Invoke($"'{spec.Name}': swapped in {pick.ItemId} as a {season} stretch.");
                }
            }

            if (stretches) StretchPass(-1);
            // Hard-item rule (spec section 3): one effort-6-or-more item per bundle of 4 or more slots.
            if (targetCount >= MinSlotsForHardItem && !chosen.Any(c => EffortTiers.IsHard(availability.For(c.ItemId).Effort)))
            {
                List<PoolItem> hardPool = candidates.Where(c => !chosenIds.Contains(c.ItemId) && EffortTiers.IsHard(availability.For(c.ItemId).Effort)).ToList();
                if (hardPool.Count == 0) log?.Invoke($"'{spec.Name}': no hard item in its pool.");
                else
                {
                    // Swap the easiest slot that is not a stretch line, so the stretch swap above survives.
                    int victim = chosen.Select((c, i) => (c, i))
                        .Where(p => !StretchRule.StretchSeasons.Any(s => StretchRule.IsStretchFor(availability.For(p.c.ItemId), s)))
                        .OrderBy(p => availability.For(p.c.ItemId).Effort).Select(p => p.i).DefaultIfEmpty(-1).First();
                    if (victim >= 0)
                    {
                        PoolItem pick = WeightedSampler.Sample(hardPool, 1, rng)[0];
                        chosenIds.Remove(chosen[victim].ItemId);
                        chosen[victim] = pick;
                        chosenIds.Add(pick.ItemId);
                        log?.Invoke($"'{spec.Name}': swapped in {pick.ItemId} as the hard item (effort {availability.For(pick.ItemId).Effort}).");
                        // The hard swap can be the very thing that empties a season: it takes out the
                        // easiest slot, which is often the only item reachable early. Re-run the
                        // stretch pass over the post-swap list so no season is left with nothing
                        // reachable AND no stretch line.
                        if (stretches) StretchPass(victim);
                    }
                }
            }
        }
        var slots = chosen.Select(item => new BundleSlotSpec(
            item.ItemId,
            RollStack(match.Domain, item, tuning, rng),
            qualityOff ? 0 : RollQuality(match.Domain, item, pools, tuning, rng))).ToList();

        if (match.Domain == PoolDomain.SeasonalForage
            && rng.NextDouble() < tuning.LargeQuantityForageChance)
        {
            int slotIndex = rng.Next(slots.Count);
            int stack = rng.Next(tuning.LargeQuantityMinStack, tuning.LargeQuantityMaxStack + 1);
            slots[slotIndex] = slots[slotIndex] with { Stack = stack, Quality = 0 };
        }

        return spec with
        {
            Slots = slots,
            NumberOfSlots = Math.Min(spec.NumberOfSlots, slots.Count),
        };
    }

    /// <summary>Rolls a recipe bundle part by part, in the recipe's own fixed order so the rng
    /// stream is deterministic: each part draws from its own candidates minus the avoid set and
    /// minus what earlier parts already took, filling its Count slots (or, at Count 0, the rest).
    /// A part that cannot fill falls back to the whole recipe's candidates, which already carry
    /// the bundle's own vanilla items.</summary>
    private static List<PoolItem> SampleByParts(
        BundleSpec spec, PoolRecipe recipe, IReadOnlyList<IReadOnlyList<PoolItem>> parts,
        IReadOnlyList<PoolItem> union, int targetCount, Random rng,
        IReadOnlySet<string>? avoid, Action<string>? log)
    {
        var chosen = new List<PoolItem>(targetCount);
        var taken = new HashSet<string>(StringComparer.Ordinal);

        void Take(IReadOnlyList<PoolItem> from, int count)
        {
            foreach (PoolItem pick in WeightedSampler.Sample(from, count, rng))
            {
                chosen.Add(pick);
                taken.Add(pick.ItemId);
            }
        }

        List<PoolItem> Free(IEnumerable<PoolItem> from, bool dropAvoided)
            => from.Where(p => !taken.Contains(p.ItemId)
                               && !(dropAvoided && avoid != null && avoid.Contains(p.ItemId))).ToList();

        for (int i = 0; i < parts.Count && chosen.Count < targetCount; i++)
        {
            int remaining = targetCount - chosen.Count;
            int want = recipe.Parts[i].Count <= BundlePoolRecipes.RestOfTheSlots
                ? remaining
                : Math.Min(recipe.Parts[i].Count, remaining);

            List<PoolItem> available = Free(parts[i], dropAvoided: true);
            if (available.Count < want)
                available = Free(parts[i], dropAvoided: false); // no fresh item left: allow repeats
            if (available.Count < want)
            {
                log?.Invoke($"'{spec.Name}': part {recipe.Parts[i].Label} short by {want - available.Count}; falling back to the vanilla items");
                available = Free(union, dropAvoided: false);
            }
            Take(available, want);
        }

        // Fewer parts than slots, or a fixed-count part that ran dry: the rest of the bundle comes
        // from the recipe's whole candidate list.
        if (chosen.Count < targetCount)
        {
            List<PoolItem> rest = Free(union, dropAvoided: true);
            if (rest.Count < targetCount - chosen.Count)
                rest = Free(union, dropAvoided: false);
            Take(rest, targetCount - chosen.Count);
        }
        return chosen;
    }

    /// <summary>The bundle's own item ids, money and category refs left out.</summary>
    private static IReadOnlyList<string> VanillaIds(BundleSpec spec)
        => spec.Slots
            .Where(s => !string.IsNullOrEmpty(s.ItemId) && s.ItemId != MoneySlotId
                        && !BundleParsing.IsCategoryRef(s.ItemId))
            .Select(s => BundleParsing.NormalizeItemId(s.ItemId))
            .ToList();

    /// <summary>How many distinct items <see cref="Fill"/> could pick for this bundle before any
    /// pity trim or avoid set (0 for a domain it does not re-roll). The engine fills the
    /// tightest bundles first so a small pool is not the one left holding the repeat fallback.</summary>
    public static int CandidateCount(BundleSpec spec, DomainMatch match, ItemPools pools)
    {
        if (match.Domain == PoolDomain.None)
            return 0;
        (Func<PoolItem, bool>? capped, int cap) = CapFor(spec, match, pools);
        return WeightedSampler.Capacity(Candidates(spec, match, pools), capped, cap);
    }

    /// <summary>Night Fishing: at most one Night Market fish per bundle (see FishBundleCandidates).</summary>
    private static (Func<PoolItem, bool>? Capped, int Cap) CapFor(BundleSpec spec, DomainMatch match, ItemPools pools)
        => match.Domain == PoolDomain.Fish && FishBundleCandidates.IsNightFishingBundle(spec)
            ? (p => FishBundleCandidates.IsNightMarketFish(p, pools.FishRows), FishBundleCandidates.NightMarketFishPerBundle)
            : (null, int.MaxValue);

    /// <summary>A trim applies to bundles feeding the trimmed season's gate: season-agnostic
    /// pools (Metals, ArtisanGoods, Fish, CrabPot, MonsterDrops, generic crops) feed every
    /// season, so they count; season-named bundles count only for their own season.</summary>
    public static bool TrimApplies(DomainMatch match, PityTrim? trim)
        => trim != null && trim.Units > 0 && match.Domain != PoolDomain.None
           && (match.Season == null || match.Season == trim.Season);

    /// <summary>Mirrors the domains <see cref="RollQuality"/> can give a silver/gold ask.</summary>
    public static bool DomainRollsQuality(PoolDomain domain)
        => domain is PoolDomain.QualityCrops or PoolDomain.SeasonalCrops or PoolDomain.SeasonalForage or PoolDomain.Fish;

    private static IReadOnlyList<PoolItem> Candidates(
        BundleSpec spec, DomainMatch match, ItemPools pools,
        ItemAvailabilityModel? availability = null)
    {
        switch (match.Domain)
        {
            case PoolDomain.Recipe:
                return BundlePoolRecipes.Union(
                    BundlePoolRecipes.For(spec.Name, VanillaIds(spec), pools, availability)
                        .Parts.Select(part => part.Source(pools, availability)).ToArray());
            case PoolDomain.SeasonalCrops:
            case PoolDomain.QualityCrops:
                return FilterSeason(pools.Crops, match.Season);
            case PoolDomain.SeasonalForage:
                return FilterSeason(pools.Forage, match.Season);
            case PoolDomain.Fish:
                return FishBundleCandidates.IsNightFishingBundle(spec)
                    ? FishBundleCandidates.ForNightFishing(pools.Fish, pools.FishRows)
                    : FishBundleCandidates.ByHabitat(spec, pools.Fish);
            case PoolDomain.CrabPot:
                return pools.CrabPot;
            case PoolDomain.MonsterDrops:
                return pools.MonsterDrops;
            case PoolDomain.Metals:
                return pools.Metals;
            case PoolDomain.ArtisanGoods:
                return pools.ArtisanGoods;
            default:
                return Array.Empty<PoolItem>();
        }
    }

    /// <summary>A season-named bundle asks only for items specific to that season, like
    /// vanilla's own Spring/Summer/Fall/Winter bundles. Any-season items (beach shellfish,
    /// desert fruit, an all-year modded crop) would otherwise sit in all four pools at full
    /// weight and crowd out the season's real forage (player report 2026-08-28, Mussel in four
    /// foraging bundles). A season-less bundle (null) still draws from the whole pool.</summary>
    private static IReadOnlyList<PoolItem> FilterSeason(IReadOnlyList<PoolItem> pool, Season? season)
        => season == null
            ? pool
            : pool.Where(p => p.Seasons.Count > 0 && p.Seasons.Contains(season.Value)).ToList();

    private static int RollStack(
        PoolDomain domain, PoolItem item, BundleGenerationTuning tuning, Random rng)
    {
        switch (domain)
        {
            case PoolDomain.QualityCrops:
                return tuning.QualityCropStack;
            case PoolDomain.MonsterDrops:
                if (item.Price < tuning.CheapPriceCeiling)
                    return rng.Next(tuning.CheapMinStack, tuning.CheapMaxStack + 1);
                if (item.Price < tuning.MidPriceCeiling)
                    return rng.Next(tuning.MidMinStack, tuning.MidMaxStack + 1);
                return rng.Next(tuning.DearMinStack, tuning.DearMaxStack + 1);
            default:
                return 1;
        }
    }

    private static int RollQuality(
        PoolDomain domain, PoolItem item, ItemPools pools, BundleGenerationTuning tuning, Random rng)
    {
        // Items that can never carry a quality star (algae/seaweed) must not get a
        // silver/gold ask — the slot would be impossible to donate (Nexus 1122358).
        // Built-in set + config extension list (built-in because an existing config.json
        // overrides serialized list defaults wholesale — see ItemPoolBuilder.BuiltInExcludedItemIds).
        if (BuiltInQualityIneligibleItemIds.Contains(item.ItemId)
            || tuning.QualityIneligibleItemIds.Contains(item.ItemId))
            return 0;

        // Structural rule (2026-08-25): only items the game itself gives quality to may carry
        // a quality ask. Null = no eligibility data (hand-built pools), keep legacy behaviour.
        if (pools.QualityEligibleIds != null && !pools.QualityEligibleIds.Contains(item.ItemId))
            return 0;
        switch (domain)
        {
            case PoolDomain.QualityCrops:
                return QualityGold;
            case PoolDomain.SeasonalCrops:
            case PoolDomain.SeasonalForage:
            case PoolDomain.Fish:
                if (rng.NextDouble() < tuning.GoldQualityChance) return QualityGold;
                if (rng.NextDouble() < tuning.SilverQualityChance) return QualitySilver;
                return 0;
            default:
                return 0;
        }
    }

    /// <summary>True when an item's reach newly extends into <paramref name="s"/>: reachable by
    /// season's end, and (for anything past Spring) not already reachable a season earlier. Spring
    /// has no "earlier" season, so any item reachable by Spring's end counts as gaining it.</summary>
    private static bool Gains(ItemAvailability a, Season s)
        => StretchRule.IsReachable(a, s) && (s == Season.Spring || !StretchRule.IsReachable(a, s - 1));
}
