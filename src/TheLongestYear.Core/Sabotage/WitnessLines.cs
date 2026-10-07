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

    public static IReadOnlyList<string> AllKeys { get; } = new[] { LineKey("Linus"), LineKey("Shane"), WhenLastNight, WhenOtherNight };
}
