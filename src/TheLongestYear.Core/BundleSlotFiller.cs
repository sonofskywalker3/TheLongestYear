using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Seeded re-roll of a picked bundle's slot contents from its domain's item
/// pool (spec "expanded-pool remix"): weighted sample without replacement (no duplicate
/// items per bundle), season filtering for seasonal domains, habitat / night filtering for
/// fish (<see cref="FishBundleCandidates"/>), and stack/quality rolls from the
/// BundleGenerationTuning block (fish and forage quantities are set later by QuantityAskPass). An optional
/// <c>avoid</c> set (every item other bundles on this board already ask for) is left out
/// while the pool can still fill every slot without it. Returns the input spec
/// UNCHANGED (reference-equal) when the domain is
/// None or the filtered pool cannot fill every slot with distinct items — the safe
/// fallback the caller logs.</summary>
public static partial class BundleSlotFiller
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
        Action<string>? log = null,
        IReadOnlySet<string>? avoid = null, ItemAvailabilityModel? availability = null,
        PoolRecipe? knownRecipe = null, IReadOnlySet<string>? banned = null, int legendaryBudget = int.MaxValue,
        IReadOnlyDictionary<string, int>? cappedBudget = null)
    {
        if (match.Domain == PoolDomain.None)
            return spec;

        // Recipe bundles roll part by part (Dye: one item per colour; Field Research: one of each
        // of four things), so the parts are resolved once here, and every later pass runs on their
        // union: the avoid set, the stretch swap and the hard-item swap.
        // <paramref name="knownRecipe"/> is the caller's cached recipe for this same bundle (the
        // engine already builds one for its diagnostics): pass it and BundlePoolRecipes.For runs
        // once per bundle per generation instead of three times.
        PoolRecipe? recipe = match.Domain != PoolDomain.Recipe
            ? null
            : knownRecipe ?? BundlePoolRecipes.For(spec.Name, VanillaIds(spec), pools, availability);
        List<IReadOnlyList<PoolItem>> parts = recipe == null
            ? new List<IReadOnlyList<PoolItem>>()
            : recipe.Parts.Select(part => part.Source(pools, availability)).ToList();
        IReadOnlyList<PoolItem> candidates = recipe == null
            ? Candidates(spec, match, pools, availability)
            : BundlePoolRecipes.Union(parts.ToArray());
        // A banned id is out of every draw this bundle makes (the raw roll, the stretch swap, the
        // hard-item swap, a recipe part), unlike <paramref name="avoid"/>, which yields when the
        // pool is short. The engine bans the legendaries once the board's allowance is used up.
        if (banned != null && banned.Count > 0)
        {
            candidates = candidates.Where(p => !banned.Contains(p.ItemId)).ToList();
            for (int i = 0; i < parts.Count; i++)
                parts[i] = parts[i].Where(p => !banned.Contains(p.ItemId)).ToList();
        }
        (int Shown, int Needs)? shape = BundleShapes.For(spec.Name);
        int targetCount = shape?.Shown
            ?? (spec.PickCount > 0 ? Math.Min(spec.PickCount, spec.Slots.Count) : spec.Slots.Count);

        // The domain this bundle's stack and quality roll with. A Recipe bundle has no domain of
        // its own, so it borrows the one its dominant part maps to (see RecipeRollDomain).
        PoolDomain rollDomain = recipe == null
            ? match.Domain
            : RecipeRollDomain(recipe, targetCount);

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
        // Legendary cap (LegendaryFishRules): runs after every swap above, because the hard-item
        // rule is exactly the kind of pass that puts a legendary in, and the cap has to hold on
        // what actually leaves this method.
        LegendaryFishRules.Enforce(chosen, candidates, availability?.Step ?? DifficultyStep.Normal, rng, log, spec.Name, legendaryBudget);
        // Prismatic Shard / Mystery Box board allowance (CappedAsks), for the same reason and in the
        // same place. Null means the caller keeps no board count, so nothing is capped here.
        if (cappedBudget != null)
            CappedAsks.Enforce(chosen, candidates, cappedBudget, rng, log, spec.Name);

        // Stack and quality (rollDomain decided above). A vanilla id the roll drew again
        // keeps the stack and quality the vanilla slot carried, so a re-roll that lands on the
        // bundle's own item reproduces vanilla's ask. That holds on EVERY domain, not only Recipe:
        // a legacy-domain roll can land on one of the bundle's own items just as easily, and there
        // is no reason for the same item to keep vanilla's x5-gold ask in one bundle and get a
        // fresh roll in the next (final review, 2026-08-29).
        IReadOnlyDictionary<string, BundleSlotSpec> vanillaSlots = VanillaSlots(spec);

        var slots = new List<BundleSlotSpec>(chosen.Count);
        foreach (PoolItem item in chosen)
        {
            if (vanillaSlots.TryGetValue(item.ItemId, out BundleSlotSpec? kept))
            {
                slots.Add(new BundleSlotSpec(item.ItemId,
                    kept.Stack,
                    LegendaryFishRules.ClampQuality(item.ItemId, kept.Quality)));
                continue;
            }
            slots.Add(new BundleSlotSpec(
                item.ItemId,
                RollStack(rollDomain, item, tuning, rng),
                LegendaryFishRules.ClampQuality(item.ItemId, RollQuality(rollDomain, item, pools, tuning, rng))));
        }

        // The old 40-99 "big ask" roll on one forage slot is gone (2026-09-04): every fish and
        // forage slot now takes basis x band in QuantityAskPass, on the finished board.

        return spec with
        {
            Slots = slots,
            NumberOfSlots = Math.Min(shape?.Needs ?? spec.NumberOfSlots, slots.Count),
        };
    }

    /// <summary>The season this slot will be due, as BundleClassifier will later decide it: a
    /// season-named bundle is due in its season; a per-item bundle (every slot required) gets the
    /// BundleDeadlines spread over the same ids and model; a pick-X-of-Y bundle runs on a ramp with
    /// no per-item deadline, so null. The required-slots dial can still turn a per-item bundle into
    /// pick-X-of-Y after this, which only loosens the deadline, so the clamp taken here can only be
    /// stricter than the board that ships, never impossible.</summary>
    public static Season? DeadlineFor(
        BundleSpec spec, DomainMatch match, IReadOnlyList<BundleSlotSpec> slots, string itemId,
        ItemAvailabilityModel? availability)
    {
        if (match.Season != null) return match.Season;
        if (availability == null) return null;
        List<string> ids = slots.Select(s => s.ItemId).Distinct(StringComparer.Ordinal).ToList();
        if (spec.NumberOfSlots < ids.Count) return null;
        return BundleDeadlines.For(ids, availability, StretchRule.Lines(ids, availability))
            .TryGetValue(itemId, out Season due) ? due : null;
    }

    /// <summary>The bundle's own slots keyed by normalized item id (first wins), so a re-drawn
    /// vanilla id keeps the stack and quality vanilla asked for. Built for every domain, not just
    /// Recipe.</summary>
    private static IReadOnlyDictionary<string, BundleSlotSpec> VanillaSlots(BundleSpec spec)
    {
        var byId = new Dictionary<string, BundleSlotSpec>(StringComparer.Ordinal);
        foreach (BundleSlotSpec slot in spec.Slots)
        {
            if (string.IsNullOrEmpty(slot.ItemId) || slot.ItemId == MoneySlotId
                || BundleParsing.IsCategoryRef(slot.ItemId))
                continue;
            string id = BundleParsing.NormalizeItemId(slot.ItemId);
            if (!byId.ContainsKey(id))
                byId[id] = slot;
        }
        return byId;
    }

    private static int RollStack(
        PoolDomain domain, PoolItem item, BundleGenerationTuning tuning, Random rng)
    {
        switch (domain)
        {
            // Every domain rolls one here. Fish, forage, crops (Quality Crops included), monster
            // drops, crab pot, station and mineral asks take basis x band later in
            // QuantityAskPass, on the finished board (2026-09-04); the old price-banded monster
            // roll and the fixed x5 gold crop went with it. The tuning fields stay for config
            // compatibility.
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
