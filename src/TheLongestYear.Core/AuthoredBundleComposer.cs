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
    private const int AllSeasonsCoverage = 4;
    private const int SlotStack = 1;

    public static BundleSpec? Compose(
        AuthoredBundleDef def,
        int absoluteIndex,
        ItemPools pools,
        BundleGenerationTuning tuning,
        bool nonObjectDonationsEnabled,
        Random rng,
        DifficultyStep step = DifficultyStep.Normal,
        IReadOnlySet<string>? banned = null)
    {
        bool isTrophies = def.Name == GilTrophiesBundleName;

        IReadOnlyList<PoolItem> candidates = isTrophies
            ? TrophyCandidates(nonObjectDonationsEnabled)
            : SourceCandidates(def, pools);
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

    private static List<PoolItem> TrophyCandidates(bool nonObjectDonationsEnabled)
    {
        IReadOnlyList<string> ids = nonObjectDonationsEnabled
            ? AuthoredBundleCatalog.GilTrophies
            : AuthoredBundleCatalog.GilTrophyRingsOnly;
        return ids
            .Select(id => new PoolItem(id, 0, 1, Array.Empty<Season>(), Array.Empty<string>()))
            .ToList();
    }

    private static IReadOnlyList<PoolItem> SourceCandidates(AuthoredBundleDef def, ItemPools pools)
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
            case AuthoredSlotSource.Cooking:
                return pools.Cooking;
            case AuthoredSlotSource.TapperGoods:
                return pools.TapperGoods;
            case AuthoredSlotSource.ArtisanGoods:
                return pools.ArtisanGoods;
            case AuthoredSlotSource.Forage:
                return pools.Forage;
            case AuthoredSlotSource.Fish:
                return pools.Fish;
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

    /// <summary>An any-season item (empty Seasons list) counts as covering all four
    /// seasons on its own.</summary>
    private static int SeasonCoverage(IReadOnlyList<PoolItem> chosen)
    {
        var seasons = new HashSet<Season>();
        foreach (PoolItem item in chosen)
        {
            if (item.Seasons.Count == 0)
                return AllSeasonsCoverage;
            foreach (Season season in item.Seasons)
                seasons.Add(season);
        }
        return seasons.Count;
    }
}
