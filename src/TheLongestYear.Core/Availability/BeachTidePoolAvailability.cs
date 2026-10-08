using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Coral and Sea Urchin, which Data/Locations does not list: the game drops them from
/// code. Decompile (PC 1.6, StardewValley.Locations/Beach.cs DayUpdate, lines 84 to 95): every
/// day, any season, a halving chain starting at 1 drops one item per pass on the tide pools
/// (tiles 65..89 x 11..22), Sea Urchin 1 in 5 and Coral otherwise. The tide pools are east of the
/// broken bridge (Beach.fixBridge, tiles 58 to 60), which the player repairs with 300 Wood, so
/// the week is the later of the Beach's week and Wood's week. (The Summer 12 to 14 beach-wide
/// drop, line 119, is later than any bridge week and is not read.)</summary>
public static class BeachTidePoolAvailability
{
    public const string BeachLocation = "Beach";
    private const string WoodItemId = WildTreeAvailability.WoodItemId;
    private const int BridgeWood = 300;

    private const int BaseEffort = 2;
    private const int BridgeStep = 1;
    private const int MinorityStep = 1;
    private const double MajorityShare = 0.5;

    /// <summary>Beach.cs DayUpdate: Sea Urchin at NextDouble() &lt; 0.2, else Coral.</summary>
    private static readonly IReadOnlyDictionary<string, (double Share, string Name)> TidePoolItems =
        new Dictionary<string, (double, string)>(StringComparer.Ordinal)
        {
            ["(O)397"] = (0.2, "Sea Urchin"),
            ["(O)393"] = (0.8, "Coral"),
        };

    public static ItemEffort? Derive(string qualifiedId, LocationWeeks? weeks, Func<string, int?> weekOf, WeekMode mode = WeekMode.Pacing)
    {
        if (weekOf == null) throw new ArgumentNullException(nameof(weekOf));
        if (qualifiedId == null || !TidePoolItems.TryGetValue(qualifiedId, out (double Share, string Name) item)) return null;
        PlaceWeek beach;
        if (weeks == null) beach = PlaceWeek.OwnGate(BeachLocation);
        else if (!weeks.TryGet(BeachLocation, out beach)) return null;
        int? woodWeek = weekOf(WoodItemId);
        if (woodWeek == null) return null;
        int week = Math.Max(beach.Week, woodWeek.Value);
        int hard = Math.Max(beach.HardFor(mode), woodWeek.Value);
        int effort = BaseEffort + BridgeStep + (item.Share < MajorityShare ? MinorityStep : 0);
        return new ItemEffort(effort,
            $"beach tide pools, {item.Name} ({item.Share:0.#} of drops), past the bridge ({BridgeWood} Wood, wood week {woodWeek}), week {week}, effort {effort}",
            week, AvailabilityWeeks.SeasonOf(week), HardWeek: hard);
    }
}
