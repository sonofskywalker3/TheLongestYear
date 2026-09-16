using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Obtainability;

/// <summary>Facts that live in game CODE, typed from the PC 1.6 decompile (never from any table of this
/// mod): mine fish, Moss, the season seed fishing treasure rolls, and the guild rewards' shape.</summary>
public static class CodeSources
{
    private const string GreenhouseUnlock = "mail:ccPantry";
    private const int SpringSwitchDay = 23;   // Utility.cs 254: Spring switches after day 23
    private const int OtherSwitchDay = 20;    // Utility.cs 254: other seasons after day 20

    /// <summary>MineShaft.getFish 1148-1172: area 0 and 10 give Stonefish, 40 Ice Pip, 80 Lava Eel, on a
    /// per-cast chance that grows with fishing level and depth. Any season, any weather.</summary>
    private static readonly (string ItemId, string Floor, string Note)[] MineFishTable =
    {
        ("(O)158", "mines:floor 1", "Stonefish, floors 1 to 39, 2% + 1% per level point (MineShaft.cs 1151-1157)"),
        ("(O)161", "mines:floor 40", "Ice Pip, floors 40 to 79, 1.5% + 0.9% per level point (1159-1165)"),
        ("(O)162", "mines:floor 80", "Lava Eel, floors 80 to 119, 1% + 0.8% per level point (1166-1172)"),
    };

    /// <summary>Utility.getRaccoonSeedForCurrentTimeOfYear 250-272, by season of the catch.</summary>
    private static readonly IReadOnlyDictionary<Season, string> SeasonSeedIds = new Dictionary<Season, string>
    {
        [Season.Spring] = "(O)CarrotSeeds", [Season.Summer] = "(O)SummerSquashSeeds",
        [Season.Fall] = "(O)BroccoliSeeds", [Season.Winter] = "(O)PowdermelonSeeds",
    };

    public static IEnumerable<(string ItemId, ObtainSource Source)> MineFish()
    {
        foreach ((string id, string floor, string note) in MineFishTable)
            yield return (id, new ObtainSource(SourceKind.Fish, DayTable.Always, Reliability.Dependable,
                // No skill gate: the roll only grows with fishing level, so naming the skill at level
                // 0 would print "Fishing 0" as though it were a requirement.
                ObtainConditions.None with { Requires = new[] { floor } }, note));
    }

    /// <summary>Tree.cs 843-846 (moss grows on GrowsMoss trees), 980-984 (no moss in Winter unless the
    /// greenhouse), 1274 (scraped for 1 to 2 Moss).</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> Moss()
    {
        WeekMask notWinter = WeekMask.All.Except(WeekMask.ForSeason(Season.Winter));
        yield return ("(O)Moss", new ObtainSource(SourceKind.Forage, DayTable.InWeeks(notWinter), Reliability.Dependable,
            ObtainConditions.None with { Requires = new[] { "trees:mature trees with moss" } }, "moss scraped from trees (Tree.cs 1274)"));
        yield return ("(O)Moss", new ObtainSource(SourceKind.GreenhouseCrop, DayTable.Always, Reliability.Dependable,
            ObtainConditions.None with { Requires = new[] { "trees:mature trees with moss", GreenhouseUnlock } }, "moss on greenhouse trees, all year"));
    }

    /// <summary>The seed a fishing treasure chest gives for the day it is opened (Utility.cs 250-272,
    /// reached from FishingRod.cs 2477 and the ordinary treasure roll).</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> SeasonSeeds()
    {
        foreach (Season season in Enum.GetValues<Season>())
        {
            string id = SeasonSeedIds[season];
            DayTable days = DayTable.Available(day => SeedSeasonOn(day) == season);
            yield return (id, new ObtainSource(SourceKind.FishingTreasure, days, Reliability.Chance,
                ObtainConditions.None with { Requires = new[] { "fishing:treasure chest" }, FewDays = true },
                $"season seed from a treasure chest ({season} window)"));
        }
    }

    /// <summary>Which season's seed a chest gives on a day of the year.</summary>
    public static Season SeedSeasonOn(int dayOfYear)
    {
        var season = (Season)((dayOfYear - 1) / Calendar.DaysPerMonth);
        int dayOfMonth = Calendar.DayOfMonthOf(dayOfYear);
        int switchDay = season == Season.Spring ? SpringSwitchDay : OtherSwitchDay;
        return dayOfMonth > switchDay ? (Season)(((int)season + 1) % Calendar.MonthsPerYear) : season;
    }

    /// <summary>Data/MonsterSlayerQuests rewards, handed over at the Adventure Guild once the count is
    /// reached. Dependable: killing is on purpose. The time the count takes is a condition for the
    /// consumer, not a number this model invents.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> GuildRewards(IEnumerable<SlayerQuestRow> quests)
    {
        foreach (SlayerQuestRow quest in quests)
        {
            if (string.IsNullOrWhiteSpace(quest.RewardItemId)) continue;
            var requires = new List<string> { $"guild:{quest.Id} {quest.Count} kills ({string.Join(", ", quest.Targets)})" };
            string? floor = quest.Targets.Select(MineSources.MonsterFloor).FirstOrDefault(f => f != null);
            if (floor != null) requires.Add(floor == MineSources.SkullCavernName ? MineSources.SkullCave : "mines:" + floor);
            yield return (BundleParsing.NormalizeItemId(quest.RewardItemId), new ObtainSource(SourceKind.Guild, DayTable.Always,
                Reliability.Dependable, ObtainConditions.None with { Requires = requires }, $"Adventure Guild reward for {quest.Id}"));
        }
    }

    /// <summary>Magic Bait: Mr. Qi's (QiGemShop barter, his island recipe), so a Ginger Island item and
    /// out of scope (Jeff's ruling 2026-09-16), not an unresolved one. QiGemShop stocks (O)908 per the
    /// Data/Shops export ("patch export/Data_Shops.json", QiGemShop entry, item (O)908, MinStack 20)
    /// and StardewValley/Utility.cs's QiGemShop setup in the decompile.</summary>
    public static IEnumerable<(string ItemId, ObtainSource Source)> MagicBait()
    {
        yield return ("(O)908", new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable,
            ObtainConditions.None with { GingerIsland = true, Requires = new[] { "shop:QiGemShop" } },
            "Mr. Qi's magic bait (island)"));
    }
}
