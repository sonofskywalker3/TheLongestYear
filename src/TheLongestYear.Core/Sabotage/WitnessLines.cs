using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

public sealed class WitnessRecord
{
    public string Npc { get; set; } = "";
    public int SceneDayOfYear { get; set; }
    public bool Said { get; set; }

    /// <summary>The line was held back on a morning when the player had not met him yet
    /// (<see cref="WitnessLines.Decide"/>): the window gains the day the introduction took.</summary>
    public bool HeldForIntroduction { get; set; }
}

/// <summary>What a morning does with one witness line.</summary>
public enum WitnessMorning
{
    /// <summary>Said, or its window is over: forget it.</summary>
    Drop,
    /// <summary>The scene has not happened yet by the calendar: keep it, say nothing.</summary>
    Wait,
    /// <summary>He has not been met: vanilla's introduction plays alone today, the line waits a day.</summary>
    Hold,
    /// <summary>Put the line on top of his dialogue for today.</summary>
    Say,
}

/// <summary>A villager who saw a strike scene says so once, the next time the player talks to
/// him within a week (spec 2026-09-21). Every loop: the town forgets, so he is shaken afresh.</summary>
public static class WitnessLines
{
    public const int WindowDays = 7;

    /// <summary>The day the introduction took, added to the window of a line held for it.</summary>
    public const int IntroductionDays = 1;

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

    /// <summary>How many days after the scene the line can still be said: a week, plus the day of
    /// the introduction when the line was held for one.</summary>
    public static int WindowFor(WitnessRecord record)
        => WindowDays + (record != null && record.HeldForIntroduction ? IntroductionDays : 0);

    public static bool IsLive(WitnessRecord record, int today)
        => record != null && !record.Said && today > record.SceneDayOfYear && today - record.SceneDayOfYear <= WindowFor(record);

    /// <summary>The morning's call for one line (designer, 2026-10-09: "if they haven't talked to
    /// the villager yet, it can be pushed back a day"). On a morning when the player has not met
    /// the witness yet (<paramref name="notMetYet"/>: vanilla would play his first-meeting
    /// Introduction, which clears his dialogue and would throw the line away anyway), the line is
    /// held: the introduction plays alone, whole, however many talks it takes, and the line is
    /// offered from the next morning. A held line's window gains one day
    /// (<see cref="IntroductionDays"/>), the day he was met; every other held morning he was not
    /// talked to at all, so it cost nothing. Marks the record held.</summary>
    public static WitnessMorning Decide(WitnessRecord record, int today, bool notMetYet)
    {
        if (record == null || record.Said || today - record.SceneDayOfYear > WindowFor(record)) return WitnessMorning.Drop;
        if (today <= record.SceneDayOfYear) return WitnessMorning.Wait;
        if (notMetYet)
        {
            record.HeldForIntroduction = true;
            return WitnessMorning.Hold;
        }
        return WitnessMorning.Say;
    }

    public static IReadOnlyList<string> AllKeys { get; } = new[] { LineKey("Linus"), LineKey("Shane"), WhenLastNight, WhenOtherNight };
}
