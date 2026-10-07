using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>One chest in the storage draw, as <see cref="BlightRule.ChestHosts"/> sees it: which
/// inventory it shows (equal ids share one, like every Junimo Chest), whether it stands on one of the
/// farm maps the thief scene can show, and whether it sits on a Circle of Warding.</summary>
public readonly record struct ChestSeat(int InventoryId, bool OnFarm, bool Warded);

/// <summary>Whether a chest brings its inventory to the draw (<see cref="Host"/>), and whether that
/// inventory is warded.</summary>
public readonly record struct ChestHost(bool Host, bool Warded);

/// <summary>The chest side of the storage draw: which chests are in it, and which chests share one
/// stock (every Junimo Chest).</summary>
public static partial class BlightRule
{
    /// <summary>Is a chest in the storage draw at all? A Mini-Shipping Bin is emptied (shipped)
    /// overnight between the plan at day end and the strike, so a hit there would point at nothing.
    /// Every other chest is in: plain, big, hopper, and Junimo Chests (Jeff, 2026-10-07: their
    /// shared stock is a target, see <see cref="ChestHosts"/>).</summary>
    public static bool ChestInDraw(bool shipsOvernight) => !shipsOvernight;

    /// <summary>Which chests bring their stock to the draw, given every chest in it in walk order.
    /// Chests that show one shared inventory (every Junimo Chest shows the same one) are ONE chest
    /// in the night: their stock is drawn once, so the unit-weighted pick never counts it twice,
    /// and a unit taken there is gone from all of them (Jeff, 2026-10-07). The host that stands for
    /// the group is its first chest on the farm maps, else its first chest, so the thief scene can
    /// be staged at it whenever one is on the farm. The group is warded when any of its chests sits
    /// on a Circle of Warding: the player sees those items inside a warded chest. A chest with an
    /// inventory of its own is its own host with its own ward.</summary>
    public static IReadOnlyList<ChestHost> ChestHosts(IReadOnlyList<ChestSeat> chests)
    {
        if (chests is null) throw new ArgumentNullException(nameof(chests));
        var host = new Dictionary<int, int>();
        var warded = new Dictionary<int, bool>();
        for (int i = 0; i < chests.Count; i++)
        {
            ChestSeat c = chests[i];
            warded[c.InventoryId] = (warded.TryGetValue(c.InventoryId, out bool w) && w) || c.Warded;
            if (!host.TryGetValue(c.InventoryId, out int h)) host[c.InventoryId] = i;
            else if (!chests[h].OnFarm && c.OnFarm) host[c.InventoryId] = i;
        }
        var result = new ChestHost[chests.Count];
        for (int i = 0; i < chests.Count; i++)
        {
            int id = chests[i].InventoryId;
            result[i] = new ChestHost(host[id] == i, warded[id]);
        }
        return result;
    }
}
