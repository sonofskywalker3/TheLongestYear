namespace TheLongestYear.Core;

/// <summary>The " (gold)"-style quality suffix, previously duplicated as switch blocks in
/// WeeklyThemeQuestService and SeasonGoalsMenu. Leading space is part of the value so the
/// empty no-quality case composes cleanly.</summary>
public static class QualityTags
{
    public static string For(int quality) => quality switch
    {
        1 => Strings.Get("quality.silver"),
        2 => Strings.Get("quality.gold"),
        4 => Strings.Get("quality.iridium"),
        _ => ""
    };
}
