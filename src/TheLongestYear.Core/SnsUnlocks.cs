using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Sword and Sorcery's lasting unlocks and its keep row. The mod's Coastal Guild in East Scarp
/// replaces Mateo's tent once the player finishes Mateo's building order (500 Wood, 300 Stone,
/// 100 Hardwood, handed out by his 8-heart scene SnS.Ch1.Mateo.11). The order's reward is the mail
/// flag <see cref="GuildMail"/>, and every map patch that swaps the tent for the guild, its order
/// board and its ledger shop reads that flag (MapPatches.json). The rewind wipes the flag, so the
/// tent is back each loop unless the guild is kept.
/// </summary>
public static class SnsUnlocks
{
    /// <summary>The Content Patcher pack's id: the one that carries the maps, events and items.</summary>
    public const string ModId = "DN.SnS";

    /// <summary>The building order's reward mail; the guild stands while it is received.</summary>
    public const string GuildMail = "Mateo_SpecialOrders_BuildGuildMail";

    public const string KeepCoastalGuildId = "keep_sns_coastal_guild";
    /// <summary>Priced with Keep Cliff Road (Jeff, 2026-11-18): one building order saved, and what
    /// is behind the guild (the Underforge) still waits on Mateo's ten-heart scene.</summary>
    public const long KeepCoastalGuildCost = 350;

    /// <summary>The order Mateo's 8-heart scene hands out; its reward is <see cref="GuildMail"/>.</summary>
    public const string GuildOrder = "Mateo.SpecialOrders.BuildGuild";

    /// <summary>OrderType of the mod's story orders, each handed out by a scene (the guild, the
    /// river, the shield, the essence, Lion's Mane). Like Stardew Valley Expanded's story orders
    /// they rewind: the scene replays and hands the order out again.</summary>
    public const string EventOrderType = "SwordSorcery.Event";

    /// <summary>OrderType of the Coastal Guild's own order board.</summary>
    public const string BoardOrderType = "SwordSorcery";

    /// <summary>Completed orders the rewind leaves completed for this player's keeps. With Keep
    /// Coastal Guild the guild stands from day 1, so its building order stays done (Jeff,
    /// 2026-11-18): Mateo's scene still plays and vanilla's AddSpecialOrder skips a completed
    /// non-repeatable order.</summary>
    public static IReadOnlySet<string> KeptCompletedOrders(Func<string, bool> hasUpgrade)
    {
        if (hasUpgrade is null) throw new ArgumentNullException(nameof(hasUpgrade));
        var kept = new HashSet<string>(StringComparer.Ordinal);
        if (hasUpgrade(KeepCoastalGuildId))
            kept.Add(GuildOrder);
        return kept;
    }

    /// <summary>The upgrade ids offered only with Sword and Sorcery installed.</summary>
    public static bool IsSnsRow(string upgradeId) => upgradeId == KeepCoastalGuildId;
}
