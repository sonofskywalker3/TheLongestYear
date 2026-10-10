using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

public static partial class BundleSlotFiller
{
    /// <summary>How many distinct items <see cref="Fill"/> could pick for this bundle before any
    /// avoid set (0 for a domain it does not re-roll). The engine fills the
    /// tightest bundles first so a small pool is not the one left holding the repeat fallback.
    ///
    /// Pass the same <paramref name="availability"/> the fill will get: a recipe part can read the
    /// model (The Missing's extreme band, Rare Crops' effort floor), so counting without it counts
    /// a different pool than the one that rolls, and mis-orders the fill passes.</summary>
    public static int CandidateCount(
        BundleSpec spec, DomainMatch match, ItemPools pools, ItemAvailabilityModel? availability = null,
        PoolRecipe? knownRecipe = null)
    {
        if (match.Domain == PoolDomain.None)
            return 0;
        (Func<PoolItem, bool>? capped, int cap) = CapFor(spec, match, pools);
        return WeightedSampler.Capacity(Candidates(spec, match, pools, availability, knownRecipe), capped, cap);
    }

    /// <summary>Night Fishing and Specialty Fish: at most one Night Market fish per bundle (see
    /// FishBundleCandidates.CapsNightMarketFish).</summary>
    private static (Func<PoolItem, bool>? Capped, int Cap) CapFor(BundleSpec spec, DomainMatch match, ItemPools pools)
        => match.Domain == PoolDomain.Fish && FishBundleCandidates.CapsNightMarketFish(spec)
            ? (p => FishBundleCandidates.IsNightMarketFish(p, pools.FishRows), FishBundleCandidates.NightMarketFishPerBundle)
            : (null, int.MaxValue);

    private static IReadOnlyList<PoolItem> Candidates(
        BundleSpec spec, DomainMatch match, ItemPools pools,
        ItemAvailabilityModel? availability = null, PoolRecipe? knownRecipe = null)
    {
        switch (match.Domain)
        {
            case PoolDomain.Recipe:
                return BundlePoolRecipes.Union(
                    (knownRecipe ?? BundlePoolRecipes.For(spec.Name, VanillaIds(spec), pools, availability))
                        .Parts.Select(part => part.Source(pools, availability)).ToArray());
            case PoolDomain.SeasonalCrops:
            case PoolDomain.QualityCrops:
                return FilterSeason(pools.Crops, match.Season, availability);
            case PoolDomain.SeasonalForage:
                return FilterSeason(pools.Forage, match.Season, availability);
            case PoolDomain.Fish:
            {
                IReadOnlyList<PoolItem> fish = FishBundleCandidates.WithoutJellies(pools);
                if (FishBundleCandidates.IsNightFishingBundle(spec))
                    return FishBundleCandidates.ForNightFishing(fish, pools.FishRows);
                return FishBundleCandidates.IsSpecialtyFishBundle(spec)
                    ? FishBundleCandidates.ForSpecialty(fish, pools.FishRows)
                    : FishBundleCandidates.ByHabitat(spec, fish);
            }
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
    /// foraging bundles). A season-less bundle (null) still draws from the whole pool.
    ///
    /// With a model, the bundle also leaves out anything the model dates after its own season.
    /// A season-named bundle is gated "all by its season" (BundleClassifier), and an item whose
    /// spawn season matches but whose source opens later (Rhubarb and Starfruit from the Oasis,
    /// Coffee Bean from week 5) made that gate impossible (player report 2026-10, gmastern1:
    /// Spring Crops asked for Rhubarb).</summary>
    private static IReadOnlyList<PoolItem> FilterSeason(
        IReadOnlyList<PoolItem> pool, Season? season, ItemAvailabilityModel? availability = null)
        => season == null
            ? pool
            : pool.Where(p => p.Seasons.Count > 0 && p.Seasons.Contains(season.Value)
                              && (availability == null || availability.For(p.ItemId).Gate <= season.Value))
                  .ToList();
}
