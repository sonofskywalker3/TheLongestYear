using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Seven Deadly Sins' lasting unlocks that the mod records as "this scene was seen", and how they
/// rewind: like Stardew Valley Expanded's (<see cref="SveUnlocks"/>), they wipe each loop. None of
/// these ids is flagged by the unlock-grant scan (no mail, recipe or quest command, no friendship
/// in the key), so the seen re-seed carried each one into every later loop:
/// <list type="bullet">
/// <item>The Wolf Staff in the woods below the Secret Woods. The mod's code adds
/// <see cref="WolfStaffPickupFlag"/> to the seen events when the staff is picked up and hides its
/// tile while the flag is there (WolfStaffPickup.cs, Pickup.json, 3.14.2). It is no scene at all,
/// so it was re-seeded while the staff went with the inventory: gone for good after the first loop.</item>
/// <item>The Boo chain (a farmhouse scene from day 16, then five more): the last two open the
/// seasonal store in town and the Unmanned Hotel with its prize machine (MapWarps.json).</item>
/// <item>The arrival scenes the mod's travel station reads for its destinations
/// (TransferData.json): left seen, the station still sold a ride past a road the loop had closed again.</item>
/// </list>
/// </summary>
public static class SdsUnlocks
{
    /// <summary>The Content Patcher pack's id: the one that carries the events and items.</summary>
    public const string ModId = "ricehit.SevenDeadlySinsCP";

    /// <summary>The seen-event id the mod's code writes when the Wolf Staff is picked up.</summary>
    public const string WolfStaffPickupFlag = "ricehit.SevenDeadlySinsCP_Pickup_SDS_WolfStaff";

    /// <summary>The Boo chain, first scene to last; 19215319 opens the seasonal store and 19215323
    /// the Unmanned Hotel.</summary>
    public static IReadOnlyList<string> BooChain { get; } = new[]
    {
        "19215318", "19215319", "19215320", "19215321", "19215322", "19215323",
    };

    /// <summary>The travel station's destinations, each unlocked by having seen its arrival scene:
    /// the town church, the city centre, the woods, the sea, the cliff, the cave room and the
    /// Eastern Forest. (The Lucas lift scene 19215275 needs friendship, so it rewinds already.)</summary>
    public static IReadOnlyList<string> StationScenes { get; } = new[]
    {
        "19215238", "19215239", "19215240", "19215241", "19215242", "19215324", "19215325",
    };

    /// <summary>Seen-event ids the rewind must not put back.</summary>
    public static IReadOnlySet<string> RewoundEventIds { get; } = BuildRewoundEventIds();

    private static HashSet<string> BuildRewoundEventIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { WolfStaffPickupFlag };
        ids.UnionWith(BooChain);
        ids.UnionWith(StationScenes);
        return ids;
    }

    /// <summary>The mod's Unlockable Bundles records the rewind leaves paid for this player's keeps
    /// (<see cref="UnlockableBundlesStateReset"/>). Every other bundle is paid again each loop.</summary>
    public static IReadOnlySet<string> KeptBundles(Func<string, bool> hasUpgrade)
    {
        if (hasUpgrade is null) throw new ArgumentNullException(nameof(hasUpgrade));
        var kept = new HashSet<string>(StringComparer.Ordinal);
        if (hasUpgrade(KeepCliffRoadId))
        {
            kept.Add(CliffRoadBundle);
            kept.Add(CliffBundle);
        }
        if (hasUpgrade(KeepOldMineId))
            kept.Add(OldMineBundle);
        return kept;
    }

    /// <summary>Run-reach metric of a keep that needs a bundle paid this loop: "bundle:&lt;id&gt;".</summary>
    public const string BundleReachMetric = "bundle";

    /// <summary>The road up the cliff north of the Mountain (10,000g).</summary>
    public const string CliffRoadBundle = "SDScliffroad.roadopen";
    /// <summary>The way on from the cliff to the angels' hall (20 Solar Essence).</summary>
    public const string CliffBundle = "SDScliff.open";
    /// <summary>The sealed door in the old mine west of the Mountain (99 each of Solar Essence, Void
    /// Essence, Slime and Bat Wing): the ten cave floors behind it.</summary>
    public const string OldMineBundle = "SDSCavebase.open";

    public const string KeepCliffRoadId = "keep_sds_cliff_road";
    /// <summary>Priced with the yield wallet keeps: two cheap payments, and what is behind them is
    /// people and scenes, not a supply.</summary>
    public const long KeepCliffRoadCost = 350;

    public const string KeepOldMineId = "keep_sds_old_mine";
    /// <summary>Priced with Keep Bus Unlocked: a second dungeon, harder than the Skull Cavern, with
    /// its own ore, loot and prize machine. Above Keep Skull Key (750).</summary>
    public const long KeepOldMineCost = 1000;

    /// <summary>The upgrade ids offered only with Seven Deadly Sins installed.</summary>
    public static bool IsSdsRow(string upgradeId) => upgradeId == KeepCliffRoadId || upgradeId == KeepOldMineId;
}
