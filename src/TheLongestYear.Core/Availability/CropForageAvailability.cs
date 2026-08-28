using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Effort for crops (Data/Crops growth days, regrowth, trellis) and forage (how many
/// places it spawns, and whether only remote ones). Used for rule E weighting of the room
/// themes; season floors for these items come from the pools and are untouched here.</summary>
public static class CropForageAvailability
{
    private const int BaseEffort = 1;
    private const int QuickGrowthDays = 6;
    private const int MediumGrowthDays = 12;
    private const int RegrowStep = 1;
    private const int SingleLocationStep = 1;
    private const int RemoteLocationStep = 1;
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
            if (best == null || effort < best.Effort)
                best = new ItemEffort(effort,
                    $"crop, {crop.GrowthDays} days (+{growth}){(regrow > 0 ? ", regrows or trellis (+1)" : "")}, effort {effort}");
        }
        return best;
    }

    public static ItemEffort? DeriveForage(string qualifiedId, IReadOnlyList<RawSpawnEntry> spawns)
    {
        if (spawns == null) throw new ArgumentNullException(nameof(spawns));
        List<string> locations = spawns
            .Where(s => s.ItemId == qualifiedId)
            .Select(s => s.Location ?? "")
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (locations.Count == 0) return null;
        int single = locations.Count == 1 ? SingleLocationStep : 0;
        int remote = locations.All(l => RemoteMarkers.Any(m => l.Contains(m, StringComparison.Ordinal))) ? RemoteLocationStep : 0;
        int effort = BaseEffort + single + remote;
        return new ItemEffort(effort,
            $"forage, {locations.Count} location(s) (+{single}){(remote > 0 ? ", remote only (+1)" : "")}, effort {effort}");
    }
}
