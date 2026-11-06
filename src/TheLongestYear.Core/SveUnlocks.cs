using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One Stardew Valley Expanded story order: SVE adds it once <paramref name="TriggerEvent"/>
/// has been seen and takes it away once <paramref name="DoneEvent"/> has.</summary>
public sealed record SveStoryOrder(string OrderKey, string TriggerEvent, string DoneEvent);

/// <summary>
/// Stardew Valley Expanded's lasting unlocks, and how they rewind (Jeff, 2026-11-05: they wipe each
/// loop like vanilla's, with keeps sold). SVE records almost every unlock as "this scene was seen":
/// its map patches, shop rows and powers read the event id, and the scene's script carries no mail,
/// recipe or quest command, so the unlock-grant scan never flags it and the seen re-seed carried the
/// unlock into every later loop for free.
/// <para>
/// Story orders: SVE's code (AddSpecialOrdersAfterEvents, 1.15.11) adds an order while its trigger
/// scene is seen, its done scene is not, and the order is not in the completed list. The rewind
/// therefore un-sees both scenes and forgets the completion (<see cref="SpecialOrderResetRules"/>);
/// SVE adds the order back once the trigger scene has played again. The order's reward mail is
/// wiped with the rest of the mail.
/// </para>
/// </summary>
public static class SveUnlocks
{
    /// <summary>The Content Patcher pack's id: the one that carries the events, powers and orders.</summary>
    public const string ModId = "FlashShifter.StardewValleyExpandedCP";

    /// <summary>Data/SpecialOrders OrderType of SVE's story orders (never on the town board).</summary>
    public const string StoryOrderType = "StardewValleyExpanded";

    public const string CheeseMasteryEvent = "1000026";
    public const string CraftMasteryEvent = "1000083";
    public const string BrewingMasteryEvent = "5407917";

    /// <summary>Robin's "all done" scene on the farm; SVE's GrandpaShedComplete token reads it.</summary>
    public const string ShedCompleteEvent = "2554906";
    private const string ShedOrderKey = "Robin3";
    /// <summary>Robin looks the ruins over on the farm; the order scene's key requires it, so it
    /// rewinds with that scene (EventGatingTables.AddDirectPrerequisites).</summary>
    private const string ShedInspectionEvent = "2554902";
    private const string ShedTriggerEvent = "2554903";
    private const string ShedFirstVisitEvent = "2554907";
    private const string ShedRepairedMail = "ShedRepaired";

    /// <summary>Susan's Summit rail shortcut: no order, opens on a scene after 30 days played.</summary>
    private const string SummitShortcutEvent = "1000077";

    /// <summary>The scene that plays on the first step into the Secret Woods. SVE's western forest has
    /// a second way into the woods, shut by a large log that Farm Type Manager places only while this
    /// scene is unseen (area "Forest West Log", one spawn a save). Put back as seen, the log never came
    /// back, so from the second loop on the Secret Woods stood open on day 1 with no steel axe. The
    /// same flag holds SVE's one Rare Seed in the woods.</summary>
    public const string SecretWoodsFirstVisitEvent = "7134028";

    public const string KeepShedId = "keep_grandpas_shed";
    /// <summary>Priced with Keep Basement: the repaired shed is a second building with its own
    /// greenhouse room, well above a Fish Pond (750).</summary>
    public const long KeepShedCost = 1800;

    /// <summary>SVE 1.15.11's own table (AddSpecialOrdersAfterEvents.SpecialOrders).</summary>
    public static IReadOnlyList<SveStoryOrder> StoryOrders { get; } = new List<SveStoryOrder>
    {
        new("Clint2", "8050108", "8050109"),
        new("Clint3", "2551994", "2554911"),
        new("Lewis2", "8033859", "8033861"),
        new("Morris", "9033859", "8033861"),
        new(ShedOrderKey, ShedTriggerEvent, ShedFirstVisitEvent),
        new("Robin4", "2554928", "2554909"),
        new("Apples", "7775926", "7775927"),
        new("MarlonFay2", "65360183", "65360184"),
        new("MorrisTod2", "6663407", "6663408"),
        new("MorrisTod3", "746153083", "746153084"),
        new("Lance", "65360186", "65360187"),
        new("Krobus", "1090506", "1090507"),
        new("CamillaBridge", "1337426", "1337427"),
        new("DwarfCaveShortcut", "1337438", "1337439"),
        new("GilMinecarts", "1337430", "1337431"),
        new("LegendaryTrio", "1337930", "1337931"),
        new("HenchmanHouseRepaired", "1337730", "1337731"),
    };

    /// <summary>Event ids that must not be re-seeded as seen: every story order's two scenes, the
    /// shed's completion scene, the Summit shortcut and the Secret Woods first-visit scene. The powers are found from Data/Powers
    /// (<see cref="PowerEventMarkers"/>).</summary>
    public static IReadOnlySet<string> RewoundEventIds { get; } = BuildRewoundEventIds();

    private static HashSet<string> BuildRewoundEventIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal) { ShedCompleteEvent, SummitShortcutEvent, SecretWoodsFirstVisitEvent };
        foreach (SveStoryOrder order in StoryOrders)
        {
            ids.Add(order.TriggerEvent);
            ids.Add(order.DoneEvent);
        }
        return ids;
    }

    /// <summary>Keep Grandpa's Shed: the scenes put back as seen, so the shed stays repaired, Robin
    /// does not come to inspect the ruins again, and SVE does not hand the order out again.</summary>
    public static IReadOnlyList<string> ShedKeptEvents { get; } =
        new[] { ShedInspectionEvent, ShedTriggerEvent, ShedCompleteEvent, ShedFirstVisitEvent };

    /// <summary>Keep Grandpa's Shed: the order's reward mail, which gates the completion scene.</summary>
    public static IReadOnlyList<string> ShedKeptMail { get; } = new[] { ShedRepairedMail };

    /// <summary>Completed story orders the rewind leaves completed for this player's keeps.</summary>
    public static IReadOnlySet<string> KeptCompletedOrders(Func<string, bool> hasUpgrade)
    {
        var kept = new HashSet<string>(StringComparer.Ordinal);
        if (hasUpgrade(KeepShedId))
            kept.Add(ShedOrderKey);
        return kept;
    }

    /// <summary>The upgrade ids offered only with SVE installed.</summary>
    public static bool IsSveRow(string upgradeId)
        => upgradeId == KeepShedId
           || upgradeId == WalletKeepTable.CheeseMasteryKeepId
           || upgradeId == WalletKeepTable.CraftMasteryKeepId
           || upgradeId == WalletKeepTable.BrewingMasteryKeepId;
}
