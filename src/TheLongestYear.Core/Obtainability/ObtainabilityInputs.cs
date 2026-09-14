using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

// ---- Plain input records. The glue (Loop/GameObtainabilityData) fills these from the live game
// ---- data assets at save load, so every rule here is testable without the game. Item ids are
// ---- QUALIFIED ("(O)24") except category references ("-75") and item queries, which travel as
// ---- their raw text and are expanded by ItemQueries (Task 4).

/// <summary>A festival's dates: passive festivals (Data/PassiveFestivals: Night Market, Squid Fest,
/// Trout Derby, Desert Festival) and day festivals (Data/Festivals/FestivalDates: Egg Festival...).</summary>
public sealed record FestivalDates(string Id, Season Season, int StartDay, int EndDay)
{
    public WeekMask Weeks => WeekMask.ForDays(
        Calendar.DayOfYear((int)Season, StartDay), Calendar.DayOfYear((int)Season, EndDay));
}
