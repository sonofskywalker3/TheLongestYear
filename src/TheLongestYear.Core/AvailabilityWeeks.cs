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
    public const int KitchenWeek = 5;
    /// <summary>A dish the Saloon or a Cookout Kit can supply without a kitchen.</summary>
    public const int ShopDishWeek = 3;
    public const int PondDelayWeeks = 4;
    public const int SaplingWeek = 1;
    public const int ArtifactWeek = 1;
    public const int SalmonberryWeek = 3;
    public const int BlackberryWeek = 10;
    public const int SkullCavernWeek = 9;
    public const int SewerWeek = 5;
    public const int SwampWeek = 13;
    /// <summary>Jeff, 2026-08-28: 30 floors a week for the theme goals.</summary>
    public const int MineFloorsPerWeek = 30;
    /// <summary>Desert hard week (Jeff): a Spring bus is possible but not fun; Hard may ask from
    /// Summer week 2.</summary>
    public const int DesertHardWeek = 6;

    /// <summary>Crops whose seeds come from a festival, the cart, the Oasis or another source
    /// with its own week, rather than Pierre's day-1 shelf: the harvest cannot come before the
    /// source week plus the crop's own growth time.</summary>
    public static readonly IReadOnlyDictionary<string, int> SeedSourceWeeks =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["(O)400"] = 3,    // Strawberry, Egg Festival Spring 13
            ["(O)417"] = 12,   // Sweet Gem Berry, Rare Seed from the cart, 24 days
            ["(O)433"] = 5,    // Coffee Bean, Dust Sprite seed plus 10 days
            ["(O)284"] = 10,   // Beet, Oasis week 9 plus 6 days
            ["(O)252"] = 11,   // Rhubarb, Oasis seeds in a garden pot, Garden Pot recipe keep
            ["(O)268"] = 11,   // Starfruit, Oasis seeds in a garden pot, Garden Pot recipe keep (Summer crop kept through Fall)
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
    public static readonly IReadOnlyDictionary<string, (int Week, string Note)> QuestRewardWeeks =
        new Dictionary<string, (int, string)>(StringComparer.Ordinal)
        {
            ["(O)PrizeTicket"] = (1, "Prize Ticket, Help Wanted board"),
            ["(O)MysteryBox"] = (2, "Mystery Box, from rocks, fishing and the board once the meteor lands"),
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

    /// <summary>Books: the Bookseller's first visit is Spring, and the mines, fishing and dig
    /// spots drop them from day 1 at low odds (for Jeff to confirm).</summary>
    public const int BookWeek = 2;

    /// <summary>Crab pots need Fishing 3 for the recipe (or Willy's shop at 1,500g).</summary>
    public const int TrapFishWeek = 2;

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
