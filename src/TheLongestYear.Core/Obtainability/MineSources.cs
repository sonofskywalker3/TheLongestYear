using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Mine nodes, monster drops and fishing treasure. These come from game CODE, not data, so
/// the facts are tables; each line cites the PC 1.6 decompile.</summary>
public static class MineSources
{
    private const string SkullCave = "location:SkullCave";
    private const string Treasure = "fishing:treasure chest";
    private const string GoldenTreasure = "fishing:golden treasure chest";
    private const int SkullCavernFloor = 121;
    private const int SkillBookCount = 5;

    /// <summary>What breaking stones yields, and from which floor (MineShaft.cs createLitterObject
    /// 4321-4658, getMineArea 3757-3827, gem nodes 3962-3975, geodes from stones 3642-3663, coal 3665-3673).</summary>
    private static readonly (string ItemId, string Where, Reliability Reliability, string Note)[] NodeTable =
    {
        ("(O)378", "mines:floor 1", Reliability.Dependable, "copper node 751, 2.9% per stone (4377)"),
        ("(O)380", "mines:floor 40", Reliability.Dependable, "iron node 290 (4428)"),
        ("(O)384", "mines:floor 80", Reliability.Dependable, "gold node 764 (4466)"),
        ("(O)386", SkullCave, Reliability.Dependable, "iridium node 765 (4549-4601)"),
        ("(O)382", "mines:floor 1", Reliability.Chance, "coal from stones, 5% then 25% (3665-3673)"),
        ("(O)66", "mines:floor 1", Reliability.Chance, "amethyst node 8 (3966)"),
        ("(O)68", "mines:floor 1", Reliability.Chance, "topaz node 10 (3966)"),
        ("(O)70", "mines:floor 40", Reliability.Chance, "jade node 6 (3970)"),
        ("(O)62", "mines:floor 40", Reliability.Chance, "aquamarine node 14 (3970)"),
        ("(O)64", "mines:floor 80", Reliability.Chance, "ruby node 4 (3975)"),
        ("(O)60", "mines:floor 80", Reliability.Chance, "emerald node 12 (3975)"),
        ("(O)72", "mines:floor 51", Reliability.Chance, "diamond node 2 above floor 50 (4607)"),
        ("(O)535", "mines:floor 1", Reliability.Chance, "geode from stones, floors 1-39 (3642)"),
        ("(O)536", "mines:floor 40", Reliability.Chance, "frozen geode from stones (3650)"),
        ("(O)537", "mines:floor 80", Reliability.Chance, "magma geode from stones (3655)"),
        ("(O)749", "mines:floor 21", Reliability.Chance, "omni geode above floor 20 and Skull Cavern (3660)"),
    };

    /// <summary>First floor each mine monster spawns on (MineShaft.cs getMonsterForThisLevel 3999-4318).
    /// Skull Cavern monsters use floor 121.</summary>
    private static readonly IReadOnlyDictionary<string, int> MonsterFloors = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Green Slime"] = 1, ["Big Slime"] = 1, ["Bug"] = 1, ["Duggy"] = 1, ["Rock Crab"] = 1,
        ["Fly"] = 15, ["Grub"] = 15, ["Bat"] = 31, ["Stone Golem"] = 31,
        ["Frost Bat"] = 40, ["Dust Spirit"] = 40, ["Ghost"] = 51, ["Skeleton"] = 70,
        ["Lava Bat"] = 80, ["Metal Head"] = 80, ["Shadow Brute"] = 80, ["Shadow Shaman"] = 80, ["Lava Crab"] = 80,
        ["Squid Kid"] = 90,
        ["Mummy"] = 121, ["Serpent"] = 121, ["Iridium Bat"] = 121, ["Carbon Ghost"] = 121,
        ["Pepper Rex"] = 126, ["Iridium Crab"] = 146,
    };

    /// <summary>Every fishing treasure outcome in FishingRod.openTreasureMenuEndFunction (2426-2730),
    /// with the gate the game checks. All chance. Line numbers per entry.</summary>
    private static readonly (string ItemId, string[] Requires, bool Island, string Note)[] TreasureTable = BuildTreasureTable();

    public static string? MonsterFloor(string monster)
        => MonsterFloors.TryGetValue(monster, out int floor) ? (floor >= SkullCavernFloor ? "Skull Cavern" : $"floor {floor}") : null;

    public static IEnumerable<(string ItemId, ObtainSource Source)> Nodes()
    {
        foreach ((string id, string where, Reliability reliability, string note) in NodeTable)
            yield return (id, new ObtainSource(
                SourceKind.MineNode, DayTable.Always, reliability,
                ObtainConditions.None with { Requires = new[] { where } }, note));
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> MonsterDrops(
        IEnumerable<MonsterDropRow> rows, IReadOnlyDictionary<string, ObjInfo> objects)
    {
        foreach (MonsterDropRow row in rows)
        {
            string requires = MonsterFloors.TryGetValue(row.Monster, out int floor)
                ? (floor >= SkullCavernFloor ? SkullCave : $"mines:floor {floor}")
                : "monster:" + row.Monster;
            var template = new ObtainSource(
                SourceKind.MonsterDrop, DayTable.Always, Reliability.Chance,
                ObtainConditions.None with { Requires = new[] { requires } },
                $"{row.Monster} drop, chance {row.Chance:0.###}");
            foreach (var emitted in ItemQueries.Emit(row.ItemId, objects, template))
                yield return emitted;
        }
    }

    public static IEnumerable<(string ItemId, ObtainSource Source)> FishingTreasure()
    {
        foreach ((string id, string[] requires, bool island, string note) in TreasureTable)
        {
            WeekMask weeks = id == "(O)273" ? WeekMask.ForSeason(Season.Spring) : WeekMask.All;
            yield return (id, new ObtainSource(
                SourceKind.FishingTreasure, DayTable.InWeeks(weeks), Reliability.Chance,
                ObtainConditions.None with { Requires = requires, GingerIsland = island }, note));
        }
    }

    private static (string, string[], bool, string)[] BuildTreasureTable()
    {
        var t = new List<(string, string[], bool, string)>();
        void Add(string id, string note, params string[] extra) => t.Add((id, new[] { Treasure }.Concat(extra).ToArray(), false, note));
        void Golden(string id, string note) => t.Add((id, new[] { GoldenTreasure }, false, note));

        Add("(O)273", "rice shoot, Spring, not the beach (2446-2448)");
        Add("(O)774", "wild bait (2450-2452, 2556-2558)", "recipe:Wild Bait");
        t.Add(("(O)890", new[] { Treasure, "special order:DROP_QI_BEANS" }, true, "Qi beans (2454-2456)"));
        Add("(O)MysteryBox", "mystery box roll (2458-2461)");
        Add("(O)GoldenMysteryBox", "golden mystery box (2460)", "mastery:2");
        Add("(O)GoldenAnimalCracker", "golden animal cracker (2462-2465)", "mastery:0");
        foreach (string id in new[] { "(O)337", "(O)213", "(O)872", "(O)687", "(O)ChallengeBait", "(O)703", "(O)StardropTea", "(O)797", "(O)733", "(O)728", "(O)SonarBobber" })
            Golden(id, "golden chest table (2466-2509)");
        for (int i = 0; i < SkillBookCount; i++) Golden($"(O)SkillBook_{i}", "golden chest table (2474)");
        Add("(O)378", "case 0 (2532)"); Add("(O)388", "case 0 (2536)"); Add("(O)390", "case 0 (2540)"); Add("(O)382", "case 0 (2542)");
        Add("(O)380", "case 0, distance 3 (2528)", "fishing:clear water 3");
        Add("(O)384", "case 0, distance 4 (2524)", "fishing:clear water 4");
        Add("(O)386", "case 0, distance 5 (2516-2518)", "fishing:clear water 5");
        Add("(O)687", "case 1 (2552-2554)", "skill:Fishing 6", "fishing:clear water 4");
        Add("(O)SonarBobber", "case 1 (2560-2562)", "skill:Fishing 6");
        Add("(O)DeluxeBait", "case 1 (2564-2566)", "skill:Fishing 6");
        Add("(O)685", "case 1 and the empty-chest fallback (2570, 2725)");
        Add("(O)102", "case 2 lost book (2574-2576)", "mail:lostBookFound");
        for (int id = 585; id <= 587; id++) Add($"(O){id}", "case 2 artifact (2580-2582)", "skill:Fishing 2", "artifact found");
        for (int id = 103; id <= 119; id++) Add($"(O){id}", "case 2 artifact (2584-2586)", "skill:Fishing 2", "artifact found");
        Add("(O)535", "case 2 and case 3 geode (2590, 2603)");
        Add("(O)536", "case 3 geode (2603)", "fishing:clear water 3");
        Add("(O)537", "case 3 geode (2603)", "fishing:clear water 4");
        foreach (string id in new[] { "(O)86", "(O)66", "(O)68" }) Add(id, "case 3 gem (2629)", "skill:Fishing 2");
        foreach (string id in new[] { "(O)84", "(O)70", "(O)62" }) Add(id, "case 3 gem (2625)", "skill:Fishing 2", "fishing:clear water 3");
        foreach (string id in new[] { "(O)82", "(O)64", "(O)60" }) Add(id, "case 3 gem (2621)", "skill:Fishing 2", "fishing:clear water 4");
        Add("(O)72", "case 3 diamond (2631-2634, 2706)", "skill:Fishing 2");
        Add("(O)770", "case 3 mixed seeds below Fishing 2 (2643-2646)");
        Add("(W)14", "case 3 Neptune's Glaive, once (2649-2654)", "skill:Fishing 2");
        Add("(W)51", "case 3 Broken Trident, once (2655-2660)", "skill:Fishing 2");
        foreach (string id in new[] { "(O)516", "(O)517", "(O)518", "(O)519" }) Add(id, "case 3 ring (2666-2669)", "skill:Fishing 2");
        for (int id = 529; id <= 534; id++) Add($"(O){id}", "case 3 ring (2672)", "skill:Fishing 2");
        Add("(O)166", "case 3 treasure chest (2676-2679)", "skill:Fishing 2");
        Add("(O)74", "case 3 prismatic shard (2680-2683)", "skill:Fishing 6");
        Add("(O)127", "case 3 (2684-2687)", "skill:Fishing 2");
        Add("(O)126", "case 3 (2688-2691)", "skill:Fishing 2");
        Add("(O)527", "case 3 ring (2692-2695)", "skill:Fishing 2");
        for (int id = 504; id <= 513; id++) Add($"(B){id}", "case 3 boots (2696-2699)", "skill:Fishing 2");
        Add("(O)928", "case 3 golden egg (2700-2703)", "skill:Fishing 2", "mail:Farm_Eternal");
        for (int i = 0; i < SkillBookCount; i++) Add($"(O)SkillBook_{i}", "case 3 skill book after 3 treasures (2708-2715)", "skill:Fishing 2");
        Add("(O)GoldenBobber", "Desert Festival quest on its third day (2727-2730)", "quest:98765", "festival:DesertFestival");
        Add("(O)812", "roe from the caught fish, once the Roe book is read (2732-2748)", "book:Book_Roe");
        Add("(O)Book_Roe", "the roe book, Fishing 5 and more than 2 treasures (2750-2754)", "skill:Fishing 5");
        Add("(O)TroutDerbyTag", "a trout derby tag caught during the derby (2756-2759)", "festival:TroutDerby");
        return t.ToArray();
    }
}
