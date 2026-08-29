using System;
using System.Collections.Generic;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

/// <summary>Every judgement number behind "the first week of the year an item can exist" (spec
/// 2026-08-28-even-year-availability). Facts read from game data live in the rules; what lives
/// here is the pacing Jeff ruled on: 30 mine floors a week, Skull Cavern from Fall, a kitchen by
/// Summer, and so on. Weeks are 1 to 16 (Calendar.WeekOfYear).</summary>
public static class AvailabilityWeeks
{
    /// <summary>An item no rule placed. Winter, the safe direction for a deadline.</summary>
    public const int UnknownWeek = 13;
    public const int KitchenWeek = 6;
    /// <summary>Cookies unlock at Evelyn's Saloon event 19; no unlock condition means Cookies.</summary>
    public const int CookiesWeek = 5;
    /// <summary>Queen of Sauce episodes air weekly; year 1 covers episodes 1 to 16.</summary>
    public const int YearOneEpisodes = 16;
    /// <summary>Year 2 covers episodes 17 to 32 (the Sneak Peek Boost route: spec
    /// 2026-08-28-obtainable-board-4-boosts).</summary>
    public const int YearTwoEpisodesLast = 32;
    /// <summary>A dish the Saloon or a Cookout Kit can supply without a kitchen.</summary>
    public const int ShopDishWeek = 3;
    public const int PondDelayWeeks = 4;
    public const int SaplingWeek = 1;
    public const int ArtifactWeek = 1;
    public const int SalmonberryWeek = 3;
    public const int BlackberryWeek = 10;
    public const int SkullCavernWeek = 9;
    public const int SewerWeek = 7;
    public const int SwampWeek = 13;
    /// <summary>Jeff, 2026-08-28: 30 floors a week for the theme goals.</summary>
    public const int MineFloorsPerWeek = 30;
    /// <summary>Desert hard week (Jeff): a Spring bus is possible but not fun; Hard may ask from
    /// Summer week 2.</summary>
    public const int DesertHardWeek = 6;

    /// <summary>Crops whose seeds come from a festival, the cart, the Oasis or another source
    /// with its own week, rather than Pierre's day-1 shelf: the harvest cannot come before the
    /// source week plus the crop's own growth time.
    ///
    /// Week is the pacing answer. Hard is the earliest a player who goes looking can have it, and
    /// for the three year-two crops that is the Year-Two Seeds Boost route (spec
    /// 2026-08-28-obtainable-board-4-boosts): a Mixed Seeds roll in the crop's own season, which
    /// lands before the permanent buy the pacing week assumes. For every other row the two are the
    /// same number, because a festival or Oasis date is a calendar fact, not a pacing judgement.</summary>
    public static readonly IReadOnlyDictionary<string, (int Week, int Hard)> SeedSourceWeeks =
        new Dictionary<string, (int, int)>(StringComparer.Ordinal)
        {
            ["(O)400"] = (3, 3),      // Strawberry, Egg Festival Spring 13
            ["(O)417"] = (12, 12),    // Sweet Gem Berry, Rare Seed from the cart, 24 days
            ["(O)433"] = (5, 5),      // Coffee Bean, Dust Sprite seed plus 10 days
            ["(O)284"] = (10, 10),    // Beet, Oasis week 9 plus 6 days
            ["(O)252"] = (11, 11),    // Rhubarb, Oasis seeds in a garden pot, Garden Pot recipe keep
            ["(O)268"] = (11, 11),    // Starfruit, Oasis seeds in a garden pot, Garden Pot recipe keep (Summer crop kept through Fall)
            [YearTwoCrops.Garlic] = (4, 2),        // year-two crop: permanent buy, or the Boost from Spring week 2
            [YearTwoCrops.RedCabbage] = (7, 6),    // year-two crop: permanent buy, or the Boost from Summer week 6
            [YearTwoCrops.Artichoke] = (11, 10),   // year-two crop: permanent buy, or the Boost from Fall week 10
        };

    /// <summary>Winter dig-spot forage whose artifact-spot row carries a Winter condition the
    /// glue does not read. Later floors that beat the rules' own answer.</summary>
    public static readonly IReadOnlyDictionary<string, (int Week, string Note)> LateFloors =
        new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["(O)412"] = (13, "Winter Root, Winter dig spots"),
            ["(O)416"] = (13, "Snow Yam, Winter dig spots"),
        };

    /// <summary>Bush berries have no spawn rows; their weeks are calendar facts.</summary>
    public static readonly IReadOnlyDictionary<string, int> BushBerryWeeks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["(O)296"] = SalmonberryWeek,
            ["(O)410"] = BlackberryWeek,
        };

    /// <summary>Mine fish spawn by floor, which the location key cannot see now that the mines
    /// open in Spring: Stonefish and Ghostfish floor 20 (week 1), Ice Pip floor 60 (week 2),
    /// Lava Eel and Cave Jelly floor 100 (week 4, a Spring gate like the rest of area 80).</summary>
    public static readonly IReadOnlyDictionary<string, (int Week, Season Gate)> MineFishWeeks =
        new Dictionary<string, (int, Season)>(StringComparer.Ordinal)
        {
            ["(O)158"] = (1, Season.Spring),        // Stonefish
            ["(O)156"] = (1, Season.Spring),        // Ghostfish
            ["(O)161"] = (2, Season.Spring),        // Ice Pip
            ["(O)162"] = (4, Season.Spring),        // Lava Eel
            ["(O)CaveJelly"] = (4, Season.Spring),  // Cave Jelly
        };

    /// <summary>Legendary fish pacing weeks (Jeff, spec 2026-08-28-obtainable-board section 3):
    /// Legend rains in Spring, Crimsonfish Summer, Angler Fall, Glacierfish Winter, Mutant Carp
    /// waits on the sewer's Fishing 3 gate. Applied as a floor over the season/location week so
    /// the hard week is unchanged.</summary>
    public static readonly IReadOnlyDictionary<string, int> LegendaryPacingWeeks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["(O)163"] = 4,   // Legend
            ["(O)159"] = 5,   // Crimsonfish
            ["(O)160"] = 9,   // Angler
            ["(O)775"] = 13,  // Glacierfish
            ["(O)682"] = 7,   // Mutant Carp
        };

    /// <summary>Things a shop sells from day 1 (Pierre's staples, the Saloon's menu): week 1.</summary>
    public static readonly IReadOnlyDictionary<string, string> ShopStaples =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["(O)245"] = "Sugar, Pierre's daily",
            ["(O)246"] = "Wheat Flour, Pierre's daily",
            ["(O)247"] = "Oil, Pierre's daily",
            ["(O)423"] = "Rice, Pierre's daily",
            ["(O)419"] = "Vinegar, Pierre's daily",
            ["(O)196"] = "Salad, the Saloon's menu",
            ["(O)216"] = "Bread, the Saloon's menu",
            ["(O)224"] = "Spaghetti, the Saloon's menu",
            ["(O)206"] = "Pizza, the Saloon's menu",
            ["(O)395"] = "Coffee, the Saloon's menu",
        };

    /// <summary>Adventurer's Guild rewards for slaying a count of a monster: the monster's mine
    /// area sets the week (30 floors a week), the count pushes it later.</summary>
    public static readonly IReadOnlyDictionary<string, (int Week, Season Gate, string Note)> GuildRewardWeeks =
        new Dictionary<string, (int, Season, string)>(StringComparer.Ordinal)
        {
            ["(H)27"] = (2, Season.Spring, "Hard Hat, 30 Duggies, floors 1 to 39"),
            ["(O)810"] = (2, Season.Spring, "Crabshell Ring, 60 Rock Crabs, floors 1 to 39"),
            ["(W)13"] = (3, Season.Spring, "Insect Head, 125 cave insects, floors 1 to 39"),
            ["(H)8"] = (3, Season.Spring, "Skeleton Mask, 50 Skeletons, floors 41 to 79"),
            ["(O)522"] = (4, Season.Spring, "Vampire Ring, 200 Bats"),
            ["(O)523"] = (5, Season.Spring, "Savage Ring, 150 Void Spirits, floors 81 to 119"),
            ["(O)526"] = (6, Season.Summer, "Burglar's Ring, 500 Dust Sprites"),
            ["(O)520"] = (8, Season.Summer, "Slime Charmer Ring, 1000 Slimes"),
            ["(O)811"] = (12, Season.Fall, "Napalm Ring, 250 Serpents, Skull Cavern"),
        };

    /// <summary>Rewards from the Help Wanted board and the like.</summary>
    public static readonly IReadOnlyDictionary<string, (int Week, int Hard, string Note)> QuestRewardWeeks =
        new Dictionary<string, (int, int, string)>(StringComparer.Ordinal)
        {
            ["(O)PrizeTicket"] = (2, 1, "every 3rd Help Wanted quest, Quest.cs"),
            ["(O)MysteryBox"] = (3, 2, "Qi plane after the 6th Help Wanted quest or day 50, Utility.cs"),
        };

    /// <summary>Items no data table places, with the week I believe is right. Every row is shown
    /// to Jeff in tly_dumpavailability's Placed column as "rule" with this note, and he rules on
    /// them like the unknowns.</summary>
    public static readonly IReadOnlyDictionary<string, (int Week, string Note)> OtherPlacements =
        new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["(O)78"] = (1, "Cave Carrot, mine dirt from floor 1"),
            ["(O)Moss"] = (1, "Moss, from trees in any season"),
            ["(O)815"] = (4, "Tea Leaves, Caroline's tea sapling recipe plus 20 days"),
            ["(O)746"] = (12, "Jack-O-Lantern, Spirit's Eve Fall 27"),
            ["(O)373"] = (12, "Golden Pumpkin, Spirit's Eve maze"),
            ["(O)772"] = (7, "Oil of Garlic, Combat 6 crafting recipe, garlic plus oil"),
            ["(O)342"] = (4, "Pickles, Preserves Jar (Farming 4) plus a Spring vegetable; the jar rule names no pickle id"),
            ["(O)233"] = (5, "Ice Cream, the Summer ice cream stand"),
            ["(O)178"] = (1, "Hay, Marnie's shop from the first Wednesday (closed Mon and Tue), Jeff 2026-08-29"),
            ["(O)Book_Artifact"] = (6, "Treasure Appraisal Guide, artifact spots and fishing treasure, Jeff 2026-08-29"),
        };

    /// <summary>Hard weeks for <see cref="OtherPlacements"/> rows whose earliest possible week is
    /// earlier than the pacing week. Rows absent here use the pacing week as the hard week.</summary>
    public static readonly IReadOnlyDictionary<string, int> OtherHardWeeks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["(O)Book_Artifact"] = 2,   // an artifact spot can drop it from week 2, Jeff 2026-08-29
        };

    /// <summary>Fish with no Data/Fish row the parser reads (the 1.6 jellies): effort by hand so the
    /// absolute bands do not call a trivial catch Extreme.</summary>
    public static readonly IReadOnlyDictionary<string, int> FishEffortRows =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["(O)CaveJelly"] = 3, ["(O)SeaJelly"] = 1, ["(O)RiverJelly"] = 1,
        };

    /// <summary>Fruit tree fruit: a sapling planted in week 1 matures in 28 days, so a tree
    /// fruits in its own season only when that season starts after week 4. Spring fruit (Cherry,
    /// Apricot) is a second-year item or a Traveling Cart buy: week 13, Jeff confirmed. Island
    /// fruit (Banana, Mango) waits for the island; excluded from this table (Task 12).</summary>
    public static readonly IReadOnlyDictionary<string, (int Week, string Note)> FruitTreeFruitWeeks =
        new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["(O)634"] = (13, "Apricot, Spring tree: second year or the cart"),
            ["(O)638"] = (13, "Cherry, Spring tree: second year or the cart"),
            ["(O)635"] = (5, "Orange, Summer tree from a week-1 sapling"),
            ["(O)636"] = (5, "Peach, Summer tree from a week-1 sapling"),
            ["(O)613"] = (9, "Apple, Fall tree"),
            ["(O)637"] = (9, "Pomegranate, Fall tree"),
        };

    /// <summary>Books with a year-1 route (Data/Shops and code, review 2026-08-28). The Bookseller's
    /// eleven story books are YEAR 3 in his stock; the ones here have a free gift box, a shop, or a
    /// prize-machine route. Everything else is drop-only and stays out of the Book pool.</summary>
    public static readonly IReadOnlyDictionary<string, int> BookWeeks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["(O)Book_PriceCatalogue"] = 2,   // Bookseller, 3,000g, always
            ["(O)SkillBook_0"] = 3, ["(O)SkillBook_1"] = 3, ["(O)SkillBook_2"] = 3, ["(O)SkillBook_3"] = 3, ["(O)SkillBook_4"] = 3,  // 5,000g and up
            ["(O)Book_Speed"] = 5,            // Way of the Wind pt. 1, 15,000g
            ["(O)PurpleBook"] = 5,            // Book of Stars, 15,000g at 25 percent
            ["(O)Book_Trash"] = 1,            // gift box in Town
            ["(O)Book_Marlon"] = 1,           // gift box in the Adventurer's Guild
            ["(O)Book_Bombs"] = 3,            // the Dwarf, 4,000g
            ["(O)Book_Friendship"] = 5,       // prize ticket machine
        };

    /// <summary>Crab pots need Fishing 3 for the recipe (or Willy's shop at 1,500g).</summary>
    public const int TrapFishWeek = 2;

    /// <summary>Machines with a route no recipe field shows. Dehydrator: choosing mushrooms for the
    /// farm cave places one (FarmCave.cs:273), Demetrius comes at 25,000g earned, about week 6.</summary>
    public static readonly IReadOnlyDictionary<string, (int Week, string Note)> MachineRouteWeeks =
        new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["(BC)Dehydrator"] = (6, "Dehydrator, mushroom cave"),
        };

    /// <summary>A machine with no skill, friendship or price gate found: mail after a special order.</summary>
    public const int SpecialOrderMachineWeek = 9;

    /// <summary>Basis prefixes emitted by a rule that reads one of Jeff's hand-ruled week tables
    /// rather than a game-data fact. Every table in this file is a judgement, so every rule that
    /// reads one produces a judgement row: OtherPlacements ("table, "), SeedSourceWeeks, BookWeeks
    /// (EffortComposer.PoolBook), FruitTreeFruitWeeks, GuildRewardWeeks, QuestRewardWeeks
    /// ("reward, ") and MachineRouteWeeks. Matched with StartsWith, so "guild reward" needs its own
    /// entry even though "reward" is listed.</summary>
    private static readonly string[] JudgementBasisPrefixes =
    {
        "table,",
        "seed source",
        "book",
        "fruit tree",
        "guild reward",
        "reward",
        "machine route",
    };

    /// <summary>True when a Phase 2 basis string names Jeff's own placement rather than a game-data
    /// fact: any rule that read one of the hand-ruled week tables (see
    /// <see cref="JudgementBasisPrefixes"/>), or a late-floor note still awaiting his sign-off
    /// ("(for Jeff to confirm)"). tly_dumpavailability shows these rows as `judgement` instead of
    /// `rule` so he can find every one without reading every basis.</summary>
    public static bool IsJudgementBasis(string basis)
    {
        if (basis == null) return false;
        if (basis.Contains("(for Jeff to confirm)", StringComparison.Ordinal)) return true;
        foreach (string prefix in JudgementBasisPrefixes)
            if (basis.StartsWith(prefix, StringComparison.Ordinal)) return true;
        return false;
    }

    public static Season SeasonOf(int week)
    {
        int clamped = Math.Clamp(week, 1, Calendar.WeeksPerYear);
        return (Season)((clamped - 1) / Calendar.WeeksPerMonth);
    }

    public static int FirstWeekOf(Season season) => (int)season * Calendar.WeeksPerMonth + 1;
    public static int LastWeekOf(Season season) => ((int)season + 1) * Calendar.WeeksPerMonth;

    /// <summary>30 floors a week (Jeff): floor 1 to 30 week 1, 31 to 60 week 2, and so on.</summary>
    public static int MineFloorWeek(int floor) => Math.Max(1, (Math.Max(1, floor) - 1) / MineFloorsPerWeek + 1);

    /// <summary>Theme-goal week for a mine area: floors 1 to 39 week 1, 41 to 79 week 2,
    /// 81 to 119 week 3, Skull Cavern Fall week 9.</summary>
    public static int MineAreaWeek(int area) => area switch
    {
        MineAreas.Area0 or MineAreas.Area10 => MineFloorWeek(1),
        MineAreas.Area40 => MineFloorWeek(41),
        MineAreas.Area80 => MineFloorWeek(81),
        _ => SkullCavernWeek,
    };

    /// <summary>Every mine area gates in Spring now (Jeff, 2026-08-28: 30 floors a week means
    /// area 80 is reachable well inside Spring); only the Skull Cavern, behind the bus repair,
    /// waits for Fall.</summary>
    public static Season MineAreaGateSeason(int area) => area == MineAreas.SkullCavern ? Season.Fall : Season.Spring;

    /// <summary>Hard week for a mine area: the same floors, Skull Cavern at the Desert hard week.</summary>
    public static int MineAreaHardWeek(int area) => area == MineAreas.SkullCavern ? DesertHardWeek : MineAreaWeek(area);

    /// <summary>Week a machine unlocked at a skill level is realistically running.</summary>
    public static int MachineLevelWeek(int level) => level switch
    {
        <= 2 => 2,
        3 => 3,
        4 or 5 => 4,
        6 or 7 => 6,
        8 or 9 => 7,
        _ => 9,
    };

    /// <summary>Week an animal building tier is realistically up: base coop or barn week 2,
    /// big week 5, deluxe week 9. links = upgrades above the base building.</summary>
    public static int HousingTierWeek(int links) => links switch
    {
        0 => 2,
        1 => 5,
        _ => 9,
    };
}
