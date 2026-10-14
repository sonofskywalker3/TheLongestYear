using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One item the rewind dropped on the ground: where, and what (as a stash record).</summary>
public sealed record PendingGroundDrop(string Location, int X, int Y, StashItemRecord Item);

/// <summary>Ground debris is never saved, but the rewind's forced save happens right after it drops
/// overflow on the ground (Keep Farm Decor, Keep Farmhouse Furniture, a full stash). A player who
/// quit on Spring 1 before picking those up lost them. The meta remembers them until the first
/// night: a load before then drops them again, and the night forgets them (from then on they are
/// ordinary ground items, as before).</summary>
public static class ResetGroundDrops
{
    public static void Remember(MetaState meta, IEnumerable<PendingGroundDrop> drops)
        => meta.PendingResetDrops = new List<PendingGroundDrop>(drops);

    public static void ForgetAtNight(MetaState meta) => meta.PendingResetDrops.Clear();
}
