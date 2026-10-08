using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for crops (Data/Crops growth days, regrowth, trellis, seasons),
/// forage (how many places it spawns, whether only remote ones, first spawn week plus location
/// gating) and saplings (sold daily, week 1). Crop week = the season's first week plus the growth
/// weeks, never past the season's last week; seeds from a festival, the cart or the Oasis wait
/// for that source (AvailabilityWeeks.SeedSourceWeeks, whose rows carry a pacing Week and an
/// earlier Hard week). A crop with no seasons is unplaced.</summary>
public static class CropForageAvailability
{
    private const int BaseEffort = 1;
    private const int QuickGrowthDays = 6;
    private const int MediumGrowthDays = 12;
    private const int RegrowStep = 1;
    private const int SingleLocationStep = 1;
    private const int RemoteLocationStep = 1;
    private const int SaplingEffort = 2;
    private static readonly string[] RemoteMarkers = { "Woods", "Desert", "Island" };

    /// <param name="shopWeeks">When set, a crop whose seed has no <see cref="AvailabilityWeeks.SeedSourceWeeks"/>
    /// row and is first sold after week 1 (<see cref="ShopWeeks"/>) is planted no earlier than that
    /// week, in a season it can still finish in: Stardew Valley Expanded's Gold Carrot seed is sold
    /// only by the Desert Trader, and read from its seasons alone the crop was a week-1 Spring
    /// harvest (mod-support work, 2026-10-08). A seed sold from week 1, or by no walkable shop at
    /// all (Mixed Seeds, a mod's own framework), keeps the season arithmetic.</param>
    public static ItemEffort? DeriveCrop(string qualifiedId, IReadOnlyList<RawCropGrowth> crops, WeekMode mode = WeekMode.Pacing,
        ShopWeeks? shopWeeks = null)
    {
        if (crops == null) throw new ArgumentNullException(nameof(crops));
        ItemEffort? best = null;
        foreach (RawCropGrowth crop in crops)
        {
            if (crop.HarvestItemId != qualifiedId) continue;
            int growth = crop.GrowthDays <= QuickGrowthDays ? 0 : crop.GrowthDays <= MediumGrowthDays ? 1 : 2;
            int regrow = crop.Regrows || crop.Trellis ? RegrowStep : 0;
            int effort = BaseEffort + growth + regrow;
            int? week = null;
            int hardWeek = 0;
            string? sold = null;
            if (crop.Seasons.Count > 0)
            {
                Season first = crop.Seasons.Min();
                int growWeeks = crop.GrowthDays / Calendar.DaysPerWeek;   // planted day 1, harvest day 1 + days
                // The crop's own arithmetic is the base for BOTH weeks; a seed source can only
                // push them later, and it pushes the pacing week and the hard week by different
                // amounts (spec 2026-08-28-obtainable-board-4-boosts: a year-two crop's Boost
                // route lands earlier than the permanent buy the pacing week assumes).
                int grown = Math.Min(AvailabilityWeeks.FirstWeekOf(first) + growWeeks, AvailabilityWeeks.LastWeekOf(first));
                week = grown;
                hardWeek = grown;
                if (!AvailabilityWeeks.SeedSourceWeeks.ContainsKey(qualifiedId) && shopWeeks != null
                    && !string.IsNullOrEmpty(crop.SeedItemId) && shopWeeks.TryGet(crop.SeedItemId, out PlaceWeek seedShop)
                    && seedShop.Week > 1)
                {
                    int? fromShop = OasisHarvestWeek(crop, seedShop.Week);
                    if (fromShop == null) continue;   // no season of the year fits after the seed arrives
                    week = Math.Max(grown, fromShop.Value);
                    int seedHard = seedShop.HardFor(mode);
                    hardWeek = seedHard > 1 && OasisHarvestWeek(crop, seedHard) is int hardFromShop
                        ? Math.Max(grown, Math.Min(hardFromShop, week.Value))
                        : grown;
                    sold = $", seed sold from week {seedShop.Week}";
                }
                else if (AvailabilityWeeks.SeedSourceWeeks.TryGetValue(qualifiedId, out (int Week, int Hard) seed))
                {
                    week = Math.Max(grown, seed.Week);
                    hardWeek = Math.Max(grown, seed.Hard);
                    // Extreme opens the Oasis in Spring week 3 (Jeff, 2026-10-02): the hard week is
                    // the honest harvest from that date, never later than the row's own.
                    if (mode == WeekMode.HardAll && AvailabilityWeeks.OasisSeedCrops.Contains(qualifiedId)
                        && OasisHarvestWeek(crop, AvailabilityWeeks.DesertHardWeekFor(mode)) is int oasis)
                        hardWeek = Math.Max(grown, Math.Min(seed.Hard, oasis));
                }
            }
            bool better = best == null
                || (week ?? int.MaxValue) < (best.EarliestWeek ?? int.MaxValue)
                || (week == best.EarliestWeek && effort < best.Effort);
            if (better)
                best = new ItemEffort(effort,
                    $"crop, {crop.GrowthDays} days (+{growth}){(regrow > 0 ? ", regrows or trellis (+1)" : "")}{sold}, "
                    + $"week {(week?.ToString() ?? "unknown")}, effort {effort}"
                    + (week != null && hardWeek < week.Value ? $", hard week {hardWeek}" : ""),
                    week, week == null ? null : AvailabilityWeeks.SeasonOf(week.Value),
                    HardWeek: week == null ? null : hardWeek);
        }
        return best;
    }

    /// <summary>First harvest week of a crop whose seeds can first be bought in
    /// <paramref name="seedWeek"/>: planted on the first day of the earliest week that is both in
    /// one of the crop's seasons and not before the seeds, harvested its growth time later (the
    /// same whole-week arithmetic as the pacing week), and only if that harvest still lands inside
    /// the season. Null when no season of the year fits.</summary>
    public static int? OasisHarvestWeek(RawCropGrowth crop, int seedWeek)
    {
        if (crop == null) throw new ArgumentNullException(nameof(crop));
        int growWeeks = crop.GrowthDays / Calendar.DaysPerWeek;
        foreach (Season season in crop.Seasons.OrderBy(s => s))
        {
            int plant = Math.Max(AvailabilityWeeks.FirstWeekOf(season), seedWeek);
            int harvest = plant + growWeeks;
            if (plant <= AvailabilityWeeks.LastWeekOf(season) && harvest <= AvailabilityWeeks.LastWeekOf(season))
                return harvest;
        }
        return null;
    }

    /// <param name="weeks">When set, a row in a map the walked weeks cannot date is ignored and
    /// each row's location week comes from the walk (<see cref="LocationWeeks"/>): Stardew Valley
    /// Expanded's Grampleton Suburbs rows (no door from anywhere a player walks, no season) made
    /// Holly, Crocus and Crystal Fruit read as week-1 forage (mod-support work, 2026-10-08). Null
    /// reads <see cref="LocationGating"/>'s names for every row.</param>
    public static ItemEffort? DeriveForage(string qualifiedId, IReadOnlyList<RawSpawnEntry> spawns, WeekMode mode = WeekMode.Pacing,
        LocationWeeks? weeks = null)
    {
        if (spawns == null) throw new ArgumentNullException(nameof(spawns));
        List<RawSpawnEntry> rows = spawns.Where(s => s.ItemId == qualifiedId
            && (weeks == null || weeks.TryGet(s.Location ?? "", out _))).ToList();
        List<string> locations = rows
            .Select(s => s.Location ?? "")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (locations.Count == 0)
        {
            if (AvailabilityWeeks.BushBerryWeeks.TryGetValue(qualifiedId, out int bushWeek))
                return new ItemEffort(BaseEffort, $"bush berry, week {bushWeek}, effort {BaseEffort}",
                    bushWeek, AvailabilityWeeks.SeasonOf(bushWeek));
            return null;
        }
        int single = locations.Count == 1 ? SingleLocationStep : 0;
        int remote = locations.All(l => RemoteMarkers.Any(m => l.Contains(m, StringComparison.Ordinal))) ? RemoteLocationStep : 0;
        int effort = BaseEffort + single + remote;
        int week = rows
            .Select(s => Math.Max(AvailabilityWeeks.FirstWeekOf(s.Season ?? Season.Spring), PlaceWeekOf(s.Location, weeks)))
            .Min();
        int hardWeek = rows
            .Select(s => Math.Max(AvailabilityWeeks.FirstWeekOf(s.Season ?? Season.Spring), PlaceHardWeekOf(s.Location, weeks, mode)))
            .Min();
        return new ItemEffort(effort,
            $"forage, {locations.Count} location(s) (+{single}){(remote > 0 ? ", remote only (+1)" : "")}, week {week}, effort {effort}",
            week, AvailabilityWeeks.SeasonOf(week), HardWeek: hardWeek);
    }

    private static int PlaceWeekOf(string? location, LocationWeeks? weeks)
        => weeks != null && weeks.TryGet(location ?? "", out PlaceWeek place) ? place.Week : LocationGating.WeekFor(location ?? "");

    private static int PlaceHardWeekOf(string? location, LocationWeeks? weeks, WeekMode mode)
        => weeks != null && weeks.TryGet(location ?? "", out PlaceWeek place) ? place.HardFor(mode) : LocationGating.HardWeekFor(location ?? "", mode);

    public static ItemEffort? DeriveSapling(string qualifiedId, IReadOnlyList<PoolItem> saplings)
    {
        if (saplings == null) throw new ArgumentNullException(nameof(saplings));
        if (!saplings.Any(s => s.ItemId == qualifiedId)) return null;
        return new ItemEffort(SaplingEffort, $"sapling, sold daily, week {AvailabilityWeeks.SaplingWeek}, effort {SaplingEffort}",
            AvailabilityWeeks.SaplingWeek, Season.Spring);
    }
}
