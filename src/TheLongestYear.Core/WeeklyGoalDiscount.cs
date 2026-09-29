using System;

namespace TheLongestYear.Core;

/// <summary>The theme week discount (spec 2026-09-29-theme-week-discount): the week's goal lines
/// ask for less while that week's theme is picked. The discount comes from the run's Stack size
/// dial (<see cref="DifficultyProfile.WeeklyGoalStackDiscount"/>).</summary>
public static class WeeklyGoalDiscount
{
    /// <summary>Lines asking for this many or fewer are left alone, and no discount goes below it.</summary>
    public const int Floor = 10;

    /// <summary>The discounted ask: <c>max(10, floor(original x (1 - discount)))</c> for a line
    /// asking for more than 10, the original otherwise. Always rounds down.</summary>
    public static int Stack(int original, double discount)
    {
        if (original <= Floor || discount <= 0.0)
            return original;
        int lowered = (int)Math.Floor(original * (1.0 - discount));
        return Math.Max(Floor, lowered);
    }
}
