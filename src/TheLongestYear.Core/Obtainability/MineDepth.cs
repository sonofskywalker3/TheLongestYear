using System;

namespace TheLongestYear.Core.Obtainability;

/// <summary>How long a mine floor takes to reach from nothing (Jeff's ruling 2026-09-16, "Mine depth in
/// the landing week"): 10 floors a day, and floors 1 to 10 are cleared on the day the player starts, so
/// a route needing floor N lands <see cref="DaysToReach"/> days after its start day.
/// <para>The builder builds the wait into every direct route's table once (<see cref="DaysBuiltIn"/>),
/// and a route made from a mine item inherits it through its input's table. A consumer that knows the
/// player's real depth reads <see cref="DaysBuiltIn"/> to take the from-nothing wait back out.</para></summary>
public static class MineDepth
{
    public const int FloorsPerDay = 10;
    public const string FloorPrefix = "mines:floor ";
    private const int NoDays = 0;

    /// <summary>max(0, ceil(floor / 10) - 1): floor 1 to 10 the same day, floor 80 seven days later.</summary>
    public static int DaysToReach(int floor)
        => floor <= FloorsPerDay ? NoDays : (floor + FloorsPerDay - 1) / FloorsPerDay - 1;

    /// <summary>The deepest floor any "mines:floor N" requirement names, or null when none does.</summary>
    public static int? FloorOf(ObtainConditions conditions)
    {
        if (conditions is null) throw new ArgumentNullException(nameof(conditions));
        int? deepest = null;
        foreach (string r in conditions.Requires)
        {
            if (!r.StartsWith(FloorPrefix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(r.Substring(FloorPrefix.Length), out int floor)) continue;
            if (deepest is null || floor > deepest.Value) deepest = floor;
        }
        return deepest;
    }

    /// <summary>The days of mine travel the builder puts into this route's own table: the wait for its
    /// floor on a route made from no item (a direct route), and none on a route made from items, whose
    /// wait arrives through its inputs' tables instead. Every direct source has no inputs and every
    /// derived source that could name a floor has at least one, so this one test is what the builder
    /// applies and what a consumer takes back out.</summary>
    public static int DaysBuiltIn(ObtainSource source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (source.Inputs.Count > 0) return NoDays;
        return FloorOf(source.Conditions) is int floor ? DaysToReach(floor) : NoDays;
    }

    /// <summary>The source with its floor's wait built into its table (unchanged when it has none).</summary>
    public static ObtainSource WithTravel(ObtainSource source)
    {
        int days = DaysBuiltIn(source);
        return days == NoDays ? source : source with { Lands = source.Lands.Delay(days) };
    }
}
