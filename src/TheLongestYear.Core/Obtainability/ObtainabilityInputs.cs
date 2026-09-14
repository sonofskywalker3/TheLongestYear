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

/// <summary>Data/Objects essentials. <see cref="ContextTags"/> are the item's BASE tags as the game
/// computes them (ItemContextTagManager.GetBaseContextTags), including generated ones such as
/// "category_fruits" and "id_o_24", which machine and pond rules match on.</summary>
public sealed record ObjInfo(
    string QualifiedId, string Name, int Category, int Price, IReadOnlyList<string> ContextTags, bool ExcludeFromRandomSale);

/// <summary>One Data/Locations Forage or Fish row. <see cref="ItemId"/> may be an item query.
/// <see cref="Season"/> null means any season unless <see cref="Condition"/> names one.
/// <see cref="MinFishingLevel"/> is SpawnFishData.MinFishingLevel (0 for forage). <see cref="IsRandom"/>
/// marks one entry of a RandomItemId list: the game picks one of them (ItemQueryResolver.cs 804-817).</summary>
public sealed record LocationSpawn(
    string Location, string ItemId, Season? Season, string? Condition, double Chance, int CatchLimit,
    bool RequireMagicBait, int MinFishingLevel, bool IsRandom = false);

/// <summary>One Data/Fish row, reduced: field 1 difficulty or "trap", field 5 time spans
/// ("600 1200 1800 2000"), field 7 weather ("sunny", "rainy", "both"), field 12 minimum level.</summary>
public sealed record FishRow(string ItemId, bool IsTrap, string Weather, int MinFishingLevel, string TimeSpans);

public sealed record ArtifactSpotRow(string Location, string ItemId, string? Condition, double Chance);

/// <summary>One Data/GarbageCans item (from a can's Items, or BeforeAll/AfterAll with CanId "*").</summary>
public sealed record GarbageRow(string CanId, string ItemId, string? Condition);
