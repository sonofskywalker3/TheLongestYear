using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Seeded composition of one authored bundle's slots (Plan 3 "authored bundles"
/// spec) from its <see cref="AuthoredBundleDef"/> and the generation-time item pools:
/// weighted sample without replacement (<see cref="WeightedSampler"/>), Gil's Trophies'
/// enable/fallback pool switch, and the Four Seasons Sampler's season-spread retry.
/// Determinism is entirely a function of the caller-supplied <paramref name="rng"/> — no
/// other entropy source is consulted, so callers must salt/seed per bundle def name
/// themselves to get independent streams across bundles in the same generation pass.
/// Returns null when the source pool can't fill the shown-slot count with distinct items,
/// or (SeasonSpread defs only) when no attempt reaches the season-spread minimum within
/// the retry budget — the safe fallback the caller skips/logs.</summary>
public static class AuthoredBundleComposer
{
    private const string GilTrophiesBundleName = "Gil's Trophies";
    private const int TrophyStack = 1;
    private const int TrophyQuality = 0;
    private const int MaxSeasonSpreadAttempts = 8;
    private const int MinSeasonSpreadCoverage = 3;
    private const int SlotStack = 1;

    public static BundleSpec? Compose(
        AuthoredBundleDef def,
        int absoluteIndex,
        ItemPools pools,
        BundleGenerationTuning tuning,
        bool nonObjectDonationsEnabled,
        Random rng,
        DifficultyStep step = DifficultyStep.Normal,
        IReadOnlySet<string>? banned = null,
        ItemAvailabilityModel? availability = null)
    {
        bool isTrophies = def.Name == GilTrophiesBundleName;

        IReadOnlyList<PoolItem> candidates = isTrophies
            ? TrophyCandidates(nonObjectDonationsEnabled)
            : SourceCandidates(def, pools, availability);
        if (banned != null && banned.Count > 0)
            candidates = candidates.Where(p => !banned.Contains(p.ItemId)).ToList();

        int shown = isTrophies ? tuning.TrophyShownCount : def.SlotCount;
        int required = isTrophies
            ? Math.Min(tuning.TrophyRequiredCount, shown)
            : def.NumberOfSlots;

        if (candidates.Count < shown)
            return null;

        List<PoolItem>? chosen = def.SeasonSpread
            ? SampleWithSeasonSpread(candidates, shown, rng)
            : WeightedSampler.Sample(candidates, shown, rng);
        if (chosen == null)
            return null;

        // The fish source carries the five legendaries (PoolAdditions); the same per-bundle cap
        // and base-quality rule the engine's filler applies hold here (LegendaryFishRules).
        LegendaryFishRules.Enforce(chosen, candidates, step, rng, bundleName: def.Name);

        int quality = isTrophies ? TrophyQuality : def.QualityAsk;
        int stack = isTrophies ? TrophyStack : SlotStack;
        var slots = chosen
            .Select(item => new BundleSlotSpec(item.ItemId, stack, LegendaryFishRules.ClampQuality(item.ItemId, quality)))
            .ToList();

        return new BundleSpec(
            Room: def.Room,
            Index: absoluteIndex,
            Name: def.Name,
            DisplayName: def.Name,
            RewardField: def.RewardField,
            Color: def.Color,
            NumberOfSlots: required,
            Slots: slots);
    }

    /// <summary>Every item one slot of a composed authored bundle could ask for instead, in a
    /// weighted order drawn from <paramref name="rng"/>, for the board-wide no-repeat pass
    /// (<see cref="BoardNoRepeats"/>): the def's own source, minus <paramref name="banned"/>, the
    /// legendaries, the capped items and whatever the bundle already asks for. A season-spread
    /// bundle (Four Seasons Sampler) only takes an item that covers every season the old one did,
    /// so a swap can never thin out its spread.</summary>
    public static IReadOnlyList<string> ReplacementOptions(
        AuthoredBundleDef def, BundleSpec composed, int slotIndex, ItemPools pools,
        bool nonObjectDonationsEnabled, IReadOnlySet<string>? banned, Random rng,
        ItemAvailabilityModel? availability = null)
    {
        if (def == null || composed == null || pools == null || rng == null) return Array.Empty<string>();
        if (slotIndex < 0 || slotIndex >= composed.Slots.Count) return Array.Empty<string>();
        IReadOnlyList<PoolItem> candidates = def.Name == GilTrophiesBundleName
            ? TrophyCandidates(nonObjectDonationsEnabled)
            : SourceCandidates(def, pools, availability);
        var asked = new HashSet<string>(composed.Slots.Select(s => BundleParsing.NormalizeItemId(s.ItemId ?? "")), StringComparer.Ordinal);
        string current = BundleParsing.NormalizeItemId(composed.Slots[slotIndex].ItemId ?? "");
        PoolItem? was = candidates.FirstOrDefault(p => p.ItemId == current);
        bool mayThin = def.SeasonSpread && SpareSlots(composed, candidates).Contains(slotIndex);
        List<PoolItem> pool = candidates
            .Where(p => !asked.Contains(p.ItemId)
                        && !(banned != null && banned.Contains(p.ItemId))
                        && !LegendaryFishRules.IsLegendary(p.ItemId)
                        && !CappedAsks.IsCapped(p.ItemId)
                        && (!def.SeasonSpread || was == null || mayThin || KeepsSeasonsOf(p, was)))
            .ToList();
        return WeightedSampler.Sample(pool, pool.Count, rng).Select(p => p.ItemId).ToList();
    }

    /// <summary>The candidate names every season the old item did, so the swap cannot thin the
    /// spread. An any-season old item names none, so anything may replace it.</summary>
    private static bool KeepsSeasonsOf(PoolItem candidate, PoolItem old)
        => old.Seasons.All(s => candidate.Seasons.Contains(s));

    /// <summary>The slots allowed a swap that drops their seasons: each one, in turn, that the
    /// rest of the bundle still names the minimum number of seasons without. The no-repeat pass
    /// applies every slot's swap together and judges each alone, so "the others cover it" per slot
    /// is not enough: two slots could each count on the other. The slots left out keep their
    /// seasons, so the spread holds however many spare slots move. The beach's shells and the
    /// desert's fruit have no season and can only take a spare slot.</summary>
    private static HashSet<int> SpareSlots(BundleSpec composed, IReadOnlyList<PoolItem> candidates)
    {
        IReadOnlyList<Season> SeasonsAt(int i)
        {
            string id = BundleParsing.NormalizeItemId(composed.Slots[i].ItemId ?? "");
            return candidates.FirstOrDefault(p => p.ItemId == id)?.Seasons ?? Array.Empty<Season>();
        }
        var spare = new HashSet<int>();
        // Widest item first: a Red Mushroom left holding three seasons has nothing to swap to.
        foreach (int i in Enumerable.Range(0, composed.Slots.Count).OrderByDescending(i => SeasonsAt(i).Count).ThenBy(i => i))
        {
            int kept = Enumerable.Range(0, composed.Slots.Count)
                .Where(j => j != i && !spare.Contains(j))
                .SelectMany(SeasonsAt)
                .Distinct()
                .Count();
            if (kept >= MinSeasonSpreadCoverage)
                spare.Add(i);
        }
        return spare;
    }

    private static List<PoolItem> TrophyCandidates(bool nonObjectDonationsEnabled)
    {
        IReadOnlyList<string> ids = nonObjectDonationsEnabled
            ? AuthoredBundleCatalog.GilTrophies
            : AuthoredBundleCatalog.GilTrophyRingsOnly;
        return ids
            .Select(id => new PoolItem(id, 0, 1, Array.Empty<Season>(), Array.Empty<string>()))
            .ToList();
    }

    /// <param name="availability">The board's availability model, or null for none (the pools pass
    /// through unfiltered).</param>
    private static IReadOnlyList<PoolItem> SourceCandidates(AuthoredBundleDef def, ItemPools pools, ItemAvailabilityModel? availability)
    {
        switch (def.Source)
        {
            case AuthoredSlotSource.FixedList:
            case AuthoredSlotSource.Trash:
                return def.FixedItemIds
                    .Select(id => new PoolItem(id, 0, 1, Array.Empty<Season>(), Array.Empty<string>()))
                    .ToList();
            case AuthoredSlotSource.Artifacts:
                return pools.Artifacts;
            case AuthoredSlotSource.Books:
                return pools.Books;
            case AuthoredSlotSource.Saplings:
                return pools.Saplings;
            case AuthoredSlotSource.GeodeMinerals:
                return pools.GeodeMinerals;
            case AuthoredSlotSource.Minerals:
                // Rockhound's (Jeff, 2026-09-04): rocks only. The geode pool carries the artifacts
                // geodes also drop (Dwarf Gadget, Rusty Spoon...), which the first roll served up.
                return pools.GeodeMinerals.Where(p => p.Category == ItemKindClassifier.MineralCategory).ToList();
            case AuthoredSlotSource.Cooking:
                // Home Cook's Feast takes the same placed-only filter every rolled cooking part
                // uses (Jeff, 2026-11-10, Nijah's report). Without it an Easy board, whose model
                // leaves the year-2 dishes unplaced, asked for Pink Cake or Crispy Bass, and the
                // unplaced dish then fell back to a Winter deadline.
                return BundlePoolRecipes.Placeable(pools.Cooking, availability);
            case AuthoredSlotSource.TapperGoods:
                return pools.TapperGoods;
            case AuthoredSlotSource.ArtisanGoods:
                return pools.ArtisanGoods;
            case AuthoredSlotSource.Forage:
                return pools.Forage;
            case AuthoredSlotSource.Fish:
                // Weatherman's: a fish bundle, so no jellies (Jeff, 2026-10-05).
                return FishBundleCandidates.WithoutJellies(pools);
            default:
                return Array.Empty<PoolItem>();
        }
    }

    /// <summary>Retries the weighted sample against the SAME rng stream (spec: "re-sample
    /// with the same rng stream, up to 8 attempts total") until the chosen slots' pooled
    /// Seasons cover at least 3 distinct seasons, or the attempt budget runs out.</summary>
    private static List<PoolItem>? SampleWithSeasonSpread(
        IReadOnlyList<PoolItem> candidates, int shown, Random rng)
    {
        for (int attempt = 0; attempt < MaxSeasonSpreadAttempts; attempt++)
        {
            List<PoolItem> chosen = WeightedSampler.Sample(candidates, shown, rng);
            if (SeasonCoverage(chosen) >= MinSeasonSpreadCoverage)
                return chosen;
        }
        return null;
    }

    /// <summary>The distinct seasons the chosen items name. An any-season item (empty Seasons
    /// list, the desert's Coconut and Cactus Fruit) adds nothing: it says nothing about when the
    /// bundle can be worked on, and it used to pass the check alone.</summary>
    private static int SeasonCoverage(IReadOnlyList<PoolItem> chosen)
        => chosen.SelectMany(item => item.Seasons).Distinct().Count();
}
