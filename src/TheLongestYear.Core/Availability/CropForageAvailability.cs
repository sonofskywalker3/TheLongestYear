using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort and first week for crops (Data/Crops growth days, regrowth, trellis, seasons),
/// forage (how many places it spawns, whether only remote ones, first spawn week plus location
/// gating) and saplings (sold daily, week 1). Crop week = the season's first week plus the growth
/// weeks, never past the season's last week; festival-only seeds wait for their festival
/// (AvailabilityWeeks.FestivalCropWeeks). A crop with no seasons is unplaced.</summary>
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

    public static ItemEffort? DeriveCrop(string qualifiedId, IReadOnlyList<RawCropGrowth> crops)
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
            if (crop.Seasons.Count > 0)
            {
                Season first = crop.Seasons.Min();
                int growWeeks = Math.Max(1, (crop.GrowthDays + Calendar.DaysPerWeek - 1) / Calendar.DaysPerWeek);
                week = Math.Min(AvailabilityWeeks.FirstWeekOf(first) + growWeeks - 1, AvailabilityWeeks.LastWeekOf(first));
                if (AvailabilityWeeks.FestivalCropWeeks.TryGetValue(qualifiedId, out int festival))
                    week = Math.Max(week.Value, festival);
            }
            bool better = best == null
                || (week ?? int.MaxValue) < (best.EarliestWeek ?? int.MaxValue)
                || (week == best.EarliestWeek && effort < best.Effort);
            if (better)
                best = new ItemEffort(effort,
                    $"crop, {crop.GrowthDays} days (+{growth}){(regrow > 0 ? ", regrows or trellis (+1)" : "")}, "
                    + $"week {(week?.ToString() ?? "unknown")}, effort {effort}",
                    week, week == null ? null : AvailabilityWeeks.SeasonOf(week.Value));
        }
        return best;
    }

    public static ItemEffort? DeriveForage(string qualifiedId, IReadOnlyList<RawSpawnEntry> spawns)
    {
        if (spawns == null) throw new ArgumentNullException(nameof(spawns));
        List<RawSpawnEntry> rows = spawns.Where(s => s.ItemId == qualifiedId).ToList();
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
            .Select(s => Math.Max(AvailabilityWeeks.FirstWeekOf(s.Season ?? Season.Spring), LocationGating.WeekFor(s.Location ?? "")))
            .Min();
        return new ItemEffort(effort,
            $"forage, {locations.Count} location(s) (+{single}){(remote > 0 ? ", remote only (+1)" : "")}, week {week}, effort {effort}",
            week, AvailabilityWeeks.SeasonOf(week));
    }

    public static ItemEffort? DeriveSapling(string qualifiedId, IReadOnlyList<PoolItem> saplings)
    {
        if (saplings == null) throw new ArgumentNullException(nameof(saplings));
        if (!saplings.Any(s => s.ItemId == qualifiedId)) return null;
        return new ItemEffort(SaplingEffort, $"sapling, sold daily, week {AvailabilityWeeks.SaplingWeek}, effort {SaplingEffort}",
            AvailabilityWeeks.SaplingWeek, Season.Spring);
    }
}
