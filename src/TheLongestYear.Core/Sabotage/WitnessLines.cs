using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

public sealed class WitnessRecord
{
    public string Npc { get; set; } = "";
    public int SceneDayOfYear { get; set; }
    public bool Said { get; set; }
}

/// <summary>A villager who saw a strike scene says so once, the next time the player talks to
/// him within a week (spec 2026-09-21). Every loop: the town forgets, so he is shaken afresh.</summary>
public static class WitnessLines
{
    public const int WindowDays = 7;
    public const string WhenLastNight = "dialogue.witness.when-last-night";
    public const string WhenOtherNight = "dialogue.witness.when-other-night";

    public static string NpcFor(DarknessEvent e) => e switch
    {
        DarknessEvent.CropBlight => "Linus",
        DarknessEvent.Reversion => "Shane",
        _ => null,
    };

    public static string LineKey(string npc) => "dialogue.witness." + npc.ToLowerInvariant();

    public static string WhenKey(int sceneDay, int today) => today - sceneDay <= 1 ? WhenLastNight : WhenOtherNight;

    public static bool IsLive(WitnessRecord record, int today)
        => record != null && !record.Said && today > record.SceneDayOfYear && today - record.SceneDayOfYear <= WindowDays;

    /// <summary>Does the witness line follow the NPC's topic dialogue in the same conversation
    /// (designer, 2026-10-08)? On a first meeting vanilla clears the NPC's dialogue stack to push
    /// its Introduction (any unseen conversation topic does the same), which threw the line away:
    /// Linus played his introduction and kept the line for a later talk. A location line pushed on
    /// top of it would likewise put it off to a second talk. When the line was queued, vanilla just
    /// pushed a dialogue and the line is no longer on top (cleared away or buried), it is spoken
    /// right after that dialogue ends, in the same conversation.</summary>
    public static bool FollowsTopic(bool lineWasQueued, bool lineOnTop, bool topicPushed)
        => lineWasQueued && topicPushed && !lineOnTop;

    public static IReadOnlyList<string> AllKeys { get; } = new[] { LineKey("Linus"), LineKey("Shane"), WhenLastNight, WhenOtherNight };
}
