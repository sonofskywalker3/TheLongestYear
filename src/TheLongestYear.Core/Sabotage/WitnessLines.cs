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

    /// <summary>Where the line goes when it follows a topic: straight under the topic, which stays
    /// on top. Takes and returns the NPC's dialogue stack top first, with the line removed from
    /// wherever it was (cleared stacks simply do not have it).
    ///
    /// Why under and not after (2026-10-08, the second pass): the first fix opened the line 200 ms
    /// after the topic's last page from the topic's <c>onFinish</c>. That fires while the closing
    /// box is still animating out, so the line was pushed on top of the not-yet-popped topic, and
    /// <c>DialogueBox.closeDialogue</c> then popped the top of the stack: the line, not the topic.
    /// Under the topic, vanilla's own pop takes the topic off and leaves the line next.</summary>
    public static List<T> PlaceUnderTop<T>(IReadOnlyList<T> topFirst, T line) where T : class
    {
        var rest = new List<T>();
        foreach (T d in topFirst)
            if (!ReferenceEquals(d, line)) rest.Add(d);
        rest.Insert(rest.Count == 0 ? 0 : 1, line);
        return rest;
    }

    /// <summary>When the topic's box has closed, does the line open at once, in the same
    /// conversation? Only when that box is what just closed, nothing else took the screen
    /// (vanilla's <c>afterDialogues</c> can open a menu), the line is now the NPC's next dialogue,
    /// no event is up and the player is still where the NPC is. Otherwise it waits on his stack for
    /// the next talk, which vanilla shows on its own.</summary>
    public static bool OpensAfterTopic(bool closedBoxWasTopic, bool screenIsFree, bool lineIsNext, bool eventUp, bool sameLocation)
        => closedBoxWasTopic && screenIsFree && lineIsNext && !eventUp && sameLocation;

    /// <summary>The topic's box closed but the topic is still the NPC's top dialogue: vanilla's
    /// <c>$e</c> ended this talk part way through it and the rest is said on the next talk (Linus's
    /// Introduction is two talks, live 2026-10-08). The line keeps waiting under the topic and opens
    /// when the topic's last box closes, so it still comes straight after the whole introduction.</summary>
    public static bool WaitsForTopicToFinish(bool closedBoxWasTopic, bool topicStillOnTop)
        => closedBoxWasTopic && topicStillOnTop;

    public static IReadOnlyList<string> AllKeys { get; } = new[] { LineKey("Linus"), LineKey("Shane"), WhenLastNight, WhenOtherNight };
}
