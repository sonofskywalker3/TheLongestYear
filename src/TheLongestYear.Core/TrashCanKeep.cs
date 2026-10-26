namespace TheLongestYear.Core;

/// <summary>The Keep Trash Can chain (keep_trash_can_1..4, Copper to Iridium). The can is not a
/// tool item: its tier is the plain Farmer.trashCanLevel field (0 = basic .. 4 = iridium), so the
/// reach gate reads that field live and the reset writes it back from the baseline.</summary>
public static class TrashCanKeep
{
    public const string Slug = "trash_can";
    public const string ReachMetric = "trash_can";
    public const int MaxTier = 4;
}
