using System;

namespace TheLongestYear.Core.Obtainability;

/// <summary>How long a mine floor takes to reach from nothing (Jeff's ruling 2026-09-16, "Mine depth in
/// the landing week"): 10 floors a day, and floors 1 to 10 are cleared on the day the player starts, so
/// a route needing floor N lands <see cref="DaysToReach"/> days after its start day. A route that needs
/// Skull Cavern (<see cref="SkullCavernRequirement"/>) needs the mines cleared first: the Skull Key is
/// the chest reward for reaching floor <see cref="MinesBottomFloor"/>, the mine's bottom (MineShaft.cs
/// 2188-2196: `case 120:` completes quest "18", then adds `new SpecialItem(4)`, the Skull Key, when the
/// player does not already have one). So <see cref="FloorOf"/> treats that requirement as floor
/// <see cref="SkullCavernFloor"/> and a direct Skull Cavern route lands on day 13 from nothing (Jeff's
/// ruling 2026-09-16, "Skull Cavern needs the mines cleared").
/// <para>The builder builds the wait into every direct route's table once (<see cref="WithTravel"/>),
/// keeping the table it had before in <see cref="ObtainSource.UndelayedLands"/>, and a route made from
/// a mine item inherits the wait through its input's table. Every derived source also carries an
/// undelayed table of its own: <see cref="Derived"/> reads each input twice, once over the routes'
/// tables and once over their undelayed tables, and applies the same chain to both (a machine's days, a
/// recipe's other ingredients, a TV episode or a shop week). So a made route's undelayed table is
/// exactly the table the model would give it with no travel anywhere, gates included, and a consumer
/// that knows the player's real depth judges every route, direct or made, on its undelayed table and
/// prices the depth itself. A made route whose delayed table ran past the end of the year is still
/// emitted when its undelayed table lands.</para></summary>
public static class MineDepth
{
    public const int FloorsPerDay = 10;
    public const string FloorPrefix = "mines:floor ";
    /// <summary>The mine's bottom, and the floor the Skull Key's chest sits on (MineShaft.cs 2188).</summary>
    public const int MinesBottomFloor = 120;
    /// <summary>The floor a "location:SkullCave" requirement is priced at: one past the mine's bottom,
    /// so it lands the day after the mines are cleared.</summary>
    public const int SkullCavernFloor = MinesBottomFloor + 1;
    public const string SkullCavernRequirement = "location:SkullCave";
    /// <summary>A Deep Woods route names its level as "deepwoods:level N" (the Deep Woods layer).</summary>
    public const string DeepWoodsLevelPrefix = "deepwoods:level ";
    /// <summary>The first Deep Woods level whose monsters are the Skull Cavern's (iridium crab, purple
    /// slime: Deep Woods' DeepWoodsSettings.Monsters). Jeff, 2026-11-05: depth is matched by monsters, so
    /// levels 1 to 9 (nothing past mines floor 119) open with the woods, and level 10 and below waits as
    /// the Skull Cavern does, for the mines to be cleared.</summary>
    public const int DeepWoodsSkullCavernTierLevel = 10;
    private const int NoDays = 0;

    /// <summary>max(0, ceil(floor / 10) - 1): floor 1 to 10 the same day, floor 80 seven days later.</summary>
    public static int DaysToReach(int floor)
        => floor <= FloorsPerDay ? NoDays : (floor + FloorsPerDay - 1) / FloorsPerDay - 1;

    /// <summary>The deepest floor any "mines:floor N" requirement names, or <see cref="SkullCavernFloor"/>
    /// for a "location:SkullCave" requirement (whichever is deeper when a route names both), or null
    /// when neither appears.</summary>
    public static int? FloorOf(ObtainConditions conditions)
    {
        if (conditions is null) throw new ArgumentNullException(nameof(conditions));
        int? deepest = null;
        foreach (string r in conditions.Requires)
        {
            int? floor = FloorNamedBy(r);
            if (floor is int f && (deepest is null || f > deepest.Value)) deepest = f;
        }
        return deepest;
    }

    private static int? FloorNamedBy(string requirement)
    {
        if (requirement == SkullCavernRequirement) return SkullCavernFloor;
        if (!requirement.StartsWith(FloorPrefix, StringComparison.Ordinal)) return null;
        return int.TryParse(requirement.Substring(FloorPrefix.Length), out int floor) ? floor : null;
    }

    /// <summary>The days of mine travel the builder puts into this route's own table: the wait for its
    /// floor on a route made from no item (a direct route), and none on a route made from items, whose
    /// wait arrives through its inputs' tables instead. Every direct source has no inputs and every
    /// derived source that could name a floor has at least one, so this one test is what the builder
    /// applies.</summary>
    public static int DaysBuiltIn(ObtainSource source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        if (source.Inputs.Count > 0) return NoDays;
        return FloorOf(source.Conditions) is int floor ? DaysToReach(floor) : NoDays;
    }

    /// <summary>The deepest Deep Woods level any "deepwoods:level N" requirement names, or null.</summary>
    public static int? DeepWoodsLevelOf(ObtainConditions conditions)
    {
        if (conditions is null) throw new ArgumentNullException(nameof(conditions));
        int? deepest = null;
        foreach (string r in conditions.Requires)
        {
            if (!r.StartsWith(DeepWoodsLevelPrefix, StringComparison.Ordinal)) continue;
            if (!int.TryParse(r.Substring(DeepWoodsLevelPrefix.Length), out int level)) continue;
            if (deepest is null || level > deepest.Value) deepest = level;
        }
        return deepest;
    }

    /// <summary>True when a route needs a Deep Woods level that holds Skull Cavern monsters.</summary>
    public static bool NeedsDeepWoodsSkullCavernTier(ObtainConditions conditions)
        => DeepWoodsLevelOf(conditions) is int level && level >= DeepWoodsSkullCavernTierLevel;

    /// <summary>The source with its floor's wait built into its table (unchanged when it has none).
    /// A mine floor adds its days after the landing (<see cref="DayTable.Delay"/>). A Deep Woods level in
    /// the Skull Cavern tier needs what a direct Skull Cavern route needs, the mines cleared
    /// (<see cref="SkullCavernFloor"/>), but that preparation overlaps the wait for the woods to open
    /// (<see cref="DayTable.StartLater"/>): started on day 1 it lands when the woods open, not 12 days
    /// after. The table must already hold the woods' opening day (<see cref="MapWeekGate"/>).</summary>
    public static ObtainSource WithTravel(ObtainSource source)
    {
        int days = DaysBuiltIn(source);
        if (days != NoDays)
            return source with { Lands = source.Lands.Delay(days), UndelayedLands = source.Lands };
        if (source.Inputs.Count == 0 && NeedsDeepWoodsSkullCavernTier(source.Conditions))
            return source with { Lands = source.Lands.StartLater(DaysToReach(SkullCavernFloor)), UndelayedLands = source.Lands };
        return source;
    }
}
