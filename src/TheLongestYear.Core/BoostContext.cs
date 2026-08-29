using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>The live facts a boost purchase needs that Core cannot read itself. The mod fills
/// it from Game1 at the moment of the click (BoostContextBuilder); tests hand-build it.</summary>
/// <param name="DayOfYear">Today, 1..112.</param>
/// <param name="TomorrowIsFestival">Utility.isFestivalDay for tomorrow (weather rows refuse).</param>
/// <param name="SkillLevels">Current levels indexed Farming 0, Fishing 1, Foraging 2, Mining 3, Combat 4.</param>
/// <param name="MineFloor">netWorldState.LowestMineLevel (0 = mine not entered).</param>
/// <param name="Skill">Crash Course: the skill being bought; -1 otherwise.</param>
public sealed record BoostContext(
    int DayOfYear,
    bool TomorrowIsFestival,
    IReadOnlyList<int> SkillLevels,
    int MineFloor,
    int Skill = -1)
{
    public Season Season => Calendar.SeasonOfDay(DayOfYear);

    public static BoostContext Simple(int dayOfYear) =>
        new(dayOfYear, false, new[] { 0, 0, 0, 0, 0 }, 0);
}
