namespace TheLongestYear.Core;

/// <summary>
/// Saves written by 0.18.118 or earlier banked a stashed dresser as a bare id, but the game's own
/// save still holds the real stash chest with the dresser's contents. On load, before the stale
/// chest is swept, each banked record whose live item carries instance data the record lacks is
/// replaced by a fresh record of the live item. Both are written by the same Saving event, so the
/// live item is the same item with more detail, never a different one.
/// </summary>
public static class StashLegacyRescue
{
    public static bool ShouldReplace(StashItemRecord stored, StashItemRecord live)
    {
        if (stored.ItemId != live.ItemId)
            return false;
        return (stored.Contents == null && live.Contents != null)
            || (stored.HeldObject == null && live.HeldObject != null)
            || (stored.Clothing == null && live.Clothing != null)
            || (stored.Boots == null && live.Boots != null)
            || (stored.InnerRings == null && live.InnerRings != null)
            || (stored.TrinketSeed == null && live.TrinketSeed != null);
    }
}
