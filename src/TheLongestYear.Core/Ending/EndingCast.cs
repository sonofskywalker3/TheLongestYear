using System.Collections.Generic;

namespace TheLongestYear.Core.Ending;

/// <summary>Everything the ending script needs that depends on the save. Built by the driver each
/// morning it starts the event.</summary>
public sealed record EndingCast(
    string? Speaker,
    string? SpeakerMiddleKey,
    string? SceneKey,
    IReadOnlyList<string> Crowd,
    int ShrineX,
    int ShrineY)
{
    /// <summary>The townsfolk on the hall steps, in placement order. The driver drops anyone the game
    /// cannot find. The speaker is placed separately and removed from this list if present.</summary>
    public static readonly IReadOnlyList<string> DefaultCrowd = new[]
    {
        "Lewis", "Robin", "Pierre", "Caroline", "Marnie", "Gus", "Emily", "Evelyn", "Penny", "Jas", "Vincent", "Linus",
    };
}
