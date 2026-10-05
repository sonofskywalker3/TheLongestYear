using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// What the loop reset does to special orders. loadForNewGame never touches
/// <c>Game1.player.team</c>, so before this rule a town order taken in one loop stayed accepted
/// in the next (its due date counted from the old calendar), the board kept offering last loop's
/// pair, and every completed non-repeatable town order was gone for good.
/// <para>
/// Rule: every town order (OrderType "", the board outside Pierre's) is dropped, in progress or
/// finished-but-unclaimed; the board's offer and its "already took one this week" mark are cleared
/// so it re-rolls; completed town orders are forgotten so they can come back. Mr. Qi's orders and
/// any other board type (the Desert Festival's Marlon board) are left as they are. A completed id
/// with no Data/SpecialOrders row (a removed mod) is left too: it can never be offered either way.
/// </para>
/// </summary>
public static class SpecialOrderResetRules
{
    /// <summary>Vanilla's OrderType for the town board (SpecialOrder.UpdateAvailableSpecialOrders).</summary>
    public const string TownOrderType = "";

    /// <summary>True for a town-board order. Data/SpecialOrders leaves OrderType blank for them.</summary>
    public static bool IsTownOrder(string? orderType) => string.IsNullOrEmpty(orderType);

    /// <summary>The completed order ids to remove from <c>team.completedSpecialOrders</c>.</summary>
    /// <param name="completed">Every id in <c>completedSpecialOrders</c>.</param>
    /// <param name="orderTypeById">OrderType per Data/SpecialOrders row id.</param>
    public static List<string> CompletedToForget(
        IEnumerable<string> completed, IReadOnlyDictionary<string, string?> orderTypeById)
    {
        var forget = new List<string>();
        foreach (string id in completed ?? Array.Empty<string>())
        {
            if (orderTypeById.TryGetValue(id, out string? type) && IsTownOrder(type))
                forget.Add(id);
        }
        return forget;
    }
}
