namespace TheLongestYear.Core.Availability;

/// <summary>Trash (167 to 172) comes off the line from day 1 in any water, including the mine
/// floors (MineShaft.getFish falls through to Random.Next(167, 173)). Nothing in Data/Locations
/// says so, which let the fish-pond route place it at week 5 (review 2026-08-28).</summary>
public static class FishingTrashAvailability
{
    private const int FirstTrashId = 167;
    private const int LastTrashId = 172;
    private const int TrashEffort = 2;

    public static ItemEffort? Derive(string qualifiedId)
    {
        string bare = BundleParsing.StripQualifier(qualifiedId ?? "");
        if (!int.TryParse(bare, out int id) || id < FirstTrashId || id > LastTrashId) return null;
        return new ItemEffort(TrashEffort, $"fishing trash, any water from day 1, week 1, effort {TrashEffort}", 1, Season.Spring, 1);
    }
}
