using System.Collections.Generic;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Everyday routes that live in game CODE rather than data (Jeff's ruling 2026-09-16):
/// weeds, trees, bushes, tilling and the mine floor finds. Typed from the PC 1.6 decompile; each row
/// cites its lines.</summary>
public static class WorldSources
{
    private const int SpringSalmonberryFirst = 15, SpringSalmonberryLast = 18;   // Bush.cs 229-235
    private const int FallBlackberryFirst = 8, FallBlackberryLast = 11;          // Bush.cs 236-241

    private static readonly (string ItemId, SourceKind Kind, Reliability Reliability, string Requires, string Note)[] AnyDay =
    {
        ("(O)771", SourceKind.Forage, Reliability.Dependable, "weeds:any", "fiber from cutting weeds, 50% a weed (Object.cs 1395-1397)"),
        ("(O)309", SourceKind.Forage, Reliability.Dependable, "trees:mature wild trees", "acorn from shaking or chopping an oak (Tree.cs 574-576, 940)"),
        ("(O)310", SourceKind.Forage, Reliability.Dependable, "trees:mature wild trees", "maple seed from shaking or chopping a maple (Tree.cs 574-576, 940)"),
        ("(O)311", SourceKind.Forage, Reliability.Dependable, "trees:mature wild trees", "pine cone from shaking or chopping a pine (Tree.cs 574-576, 940)"),
        ("(O)330", SourceKind.Forage, Reliability.Dependable, "tilling:outdoors", "clay from tilling outdoors, 3% a tile by default (GameLocation.cs 14222)"),
        ("(O)330", SourceKind.MineNode, Reliability.Dependable, "mines:floor 1", "clay from tilling a mine floor (MineShaft.cs 3101-3103, 3149-3152)"),
        ("(O)78", SourceKind.MineNode, Reliability.Dependable, "mines:floor 1", "cave carrot from tilling a mine floor (MineShaft.cs 3179-3181)"),
        ("(O)80", SourceKind.MineNode, Reliability.Dependable, "mines:floor 1", "quartz, a mine floor find on any floor (MineShaft.cs 3849-3851, 3936-3938)"),
        ("(O)86", SourceKind.MineNode, Reliability.Dependable, "mines:floor 1", "earth crystal, floors 1 to 39 (MineShaft.cs 3895-3897)"),
        ("(O)84", SourceKind.MineNode, Reliability.Dependable, "mines:floor 40", "frozen tear, floors 40 to 79 (MineShaft.cs 3918-3920)"),
        ("(O)82", SourceKind.MineNode, Reliability.Dependable, "mines:floor 80", "fire quartz, floors 80 to 119 (MineShaft.cs 3923-3925)"),
        ("(O)420", SourceKind.MineNode, Reliability.Dependable, "mines:floor 21", "red mushroom above floor 20, 10% a find (MineShaft.cs 3856-3858)"),
        ("(O)422", SourceKind.MineNode, Reliability.Dependable, "mines:floor 81", "purple mushroom above floor 80, 5% a find (MineShaft.cs 3852-3854)"),
        ("(O)107", SourceKind.MineNode, Reliability.Chance, MineSources.SkullCave, "dinosaur egg on the cavern's dinosaur floors, 6% a spawn (MineShaft.cs 3939-3944)"),
    };

    public static IEnumerable<(string ItemId, ObtainSource Source)> All()
    {
        foreach ((string id, SourceKind kind, Reliability reliability, string requires, string note) in AnyDay)
            yield return (id, Make(kind, DayTable.Always, reliability, requires, note));

        yield return ("(O)296", Make(SourceKind.Forage, Window(Season.Spring, SpringSalmonberryFirst, SpringSalmonberryLast),
            Reliability.Dependable, "bushes:wild", "salmonberry bushes, Spring 15 to 18 (Bush.cs 229-235, 451)"));
        yield return ("(O)410", Make(SourceKind.Forage, Window(Season.Fall, FallBlackberryFirst, FallBlackberryLast),
            Reliability.Dependable, "bushes:wild", "blackberry bushes, Fall 8 to 11 (Bush.cs 236-241, 452)"));

        DayTable winter = DayTable.InWeeks(WeekMask.ForSeason(Season.Winter));
        foreach (string id in new[] { "(O)412", "(O)416" })
            yield return (id, Make(SourceKind.Forage, winter, Reliability.Dependable, "tilling:outdoors off the farm",
                "winter root or snow yam from tilling off the farm in Winter, 8% a tile (GameLocation.cs 14212-14214)"));
    }

    private static DayTable Window(Season season, int firstDay, int lastDay)
    {
        int first = Calendar.DayOfYear((int)season, firstDay);
        int last = Calendar.DayOfYear((int)season, lastDay);
        return DayTable.Available(day => day >= first && day <= last);
    }

    private static ObtainSource Make(SourceKind kind, DayTable days, Reliability reliability, string requires, string note)
        => new(kind, days, reliability, ObtainConditions.None with { Requires = new[] { requires } }, note);
}
