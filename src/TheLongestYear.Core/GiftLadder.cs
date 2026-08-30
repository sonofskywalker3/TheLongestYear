using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Gifts of the Junimos (Jeff, 2026-08-29): the five keeps that carry a Community Center room's
/// world reward across loops. They share one price ladder: the first Gift costs
/// <see cref="BaseCost"/>, and every Gift already owned raises the price of the rest by another
/// <see cref="BaseCost"/>, up to <see cref="MaxStep"/> steps (1,000 ... 5,000 JP). Each Gift maps
/// to the vanilla completion mail that makes the reward real; the reset restores that mail and
/// nothing else, so the room's bundles stay on the board and still have to be paid.
/// </summary>
public static class GiftLadder
{
    public const long BaseCost = 1000;
    public const int MaxStep = 5;

    public const string KeepGreenhouseId = "keep_greenhouse";
    public const string KeepQuarryBridgeId = "keep_quarry_bridge";
    public const string KeepBoulderClearedId = "keep_boulder_cleared";
    public const string KeepMinecartsId = "keep_minecarts";
    // The bus row is VaultRules.KeepBusUnlockedId ("keep_bus_unlocked").

    /// <summary>Gift upgrade id to the vanilla completion mail it restores.</summary>
    public static readonly IReadOnlyDictionary<string, string> MailByGift = new Dictionary<string, string>
    {
        [KeepGreenhouseId] = "ccPantry",
        [KeepQuarryBridgeId] = "ccCraftsRoom",
        [KeepBoulderClearedId] = "ccFishTank",
        [KeepMinecartsId] = "ccBoilerRoom",
        [VaultRules.KeepBusUnlockedId] = "ccVault",
    };

    public static bool IsGift(UpgradeDefinition definition) => definition.Category == UpgradeCategory.Gifts;

    public static int OwnedCount(MetaState state)
    {
        int n = 0;
        foreach (string id in MailByGift.Keys)
            if (state.HasUpgrade(id)) n++;
        return n;
    }

    /// <summary>The next Gift's base price for this player: BaseCost x (owned + 1), capped at MaxStep.</summary>
    public static long CostFor(MetaState state) => BaseCost * Math.Min(MaxStep, OwnedCount(state) + 1);

    /// <summary>Completion mails to put back after a reset, one per owned Gift.</summary>
    public static List<string> KeptMails(MetaState state)
    {
        var mails = new List<string>();
        foreach (KeyValuePair<string, string> pair in MailByGift)
            if (state.HasUpgrade(pair.Key)) mails.Add(pair.Value);
        return mails;
    }
}
