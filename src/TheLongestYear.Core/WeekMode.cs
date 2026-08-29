namespace TheLongestYear.Core;

/// <summary>Which week the availability model answers with (spec 2026-08-28-obtainable-board,
/// section 1). Pacing: Easy and Normal, gates and goals use the pacing week. HardGates: Hard,
/// gates use the hard week, cards stay on pacing. HardAll: Extreme, both use the hard week.</summary>
public enum WeekMode { Pacing, HardGates, HardAll }

/// <summary>Maps a difficulty ramp step to the <see cref="WeekMode"/> the availability model
/// answers with (spec 2026-08-28-obtainable-board, section 1: "Easy and Normal use Week. Hard
/// and Extreme gates use HardWeek. Hard cards stay on Week; Extreme cards use HardWeek").</summary>
public static class WeekModes
{
    public static WeekMode For(DifficultyStep step) => step switch
    {
        DifficultyStep.Hard => WeekMode.HardGates,
        DifficultyStep.Extreme => WeekMode.HardAll,
        _ => WeekMode.Pacing,
    };
}
