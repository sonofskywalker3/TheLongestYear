using System.Collections.Generic;

namespace TheLongestYear.Core.Availability;

/// <summary>Trash (167 to 172) comes off the line from day 1 in any water, including the mine
/// floors (MineShaft.getFish falls through to Random.Next(167, 173)). Nothing in Data/Locations
/// says so, which let the fish-pond route place it at week 5 (review 2026-08-28), and which also
/// left it invisible to SourceReachability's positive-proof set: a mod that lists a trash id
/// (e.g. Driftwood, 169) in a shop it also puts somewhere unreachable used to get it wrongly
/// condemned, since nothing else on the reachable side ever spoke up for it (review 2026-09-10,
/// The Fishmonger live-verification run).</summary>
public static class FishingTrashAvailability
{
    public const int FirstTrashId = 167;
    public const int LastTrashId = 172;
    private const int TrashEffort = 2;

    public static ItemEffort? Derive(string qualifiedId)
    {
        string bare = BundleParsing.StripQualifier(qualifiedId ?? "");
        if (!int.TryParse(bare, out int id) || id < FirstTrashId || id > LastTrashId) return null;
        return new ItemEffort(TrashEffort, $"fishing trash, any water from day 1, week 1, effort {TrashEffort}", 1, Season.Spring, 1);
    }

    /// <summary>Every trash id (167 to 172), fully qualified. The one place that range is stated as
    /// a set rather than a bound check, for SourceReachability's positive-proof set to consume
    /// without restating the literals.</summary>
    public static IEnumerable<string> QualifiedIds()
    {
        for (int id = FirstTrashId; id <= LastTrashId; id++)
            yield return $"(O){id}";
    }
}
