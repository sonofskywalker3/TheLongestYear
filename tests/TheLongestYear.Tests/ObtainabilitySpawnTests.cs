using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilitySpawnTests
{
    private static readonly Dictionary<string, FestivalDates> Festivals = new()
    {
        ["NightMarket"] = new FestivalDates("NightMarket", Season.Winter, 15, 17),
    };
    private static readonly Dictionary<string, FestivalDates> NoFestivals = new();
    private static readonly Dictionary<string, ObjInfo> Objects = new();
    private static readonly ObtainabilityModel NoSources = new(new Dictionary<string, IReadOnlyList<ObtainSource>>());
    private static readonly ObtainabilityModel BaitAvailable = new(new Dictionary<string, IReadOnlyList<ObtainSource>>
    {
        ["(O)908"] = new[] { new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None, "shop GeneralStore") },
    });

    [Fact]
    public void Forage_takes_its_season_and_its_location()
    {
        var rows = new[] { new LocationSpawn("Forest", "(O)16", Season.Spring, null, 0.5, 0, false, 0) };
        var (id, source) = SpawnSources.Forage(rows, Objects, Festivals).Single();
        Assert.Equal("(O)16", id);
        Assert.Equal(SourceKind.Forage, source.Kind);
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Spring)), source.Lands);
        Assert.Equal(Reliability.Dependable, source.Reliability);
        Assert.Contains("location:Forest", source.Conditions.Requires);
    }

    [Fact]
    public void A_condition_season_narrows_a_seasonless_row_and_the_island_is_flagged()
    {
        var rows = new[]
        {
            new LocationSpawn("Beach", "(O)392", null, "SEASON Winter", 1.0, 0, false, 0),
            new LocationSpawn("IslandWest", "(O)829", null, null, 1.0, 0, false, 0),
            new LocationSpawn("Town", "(O)20", null, "FALSE", 1.0, 0, false, 0),
        };
        var sources = SpawnSources.Forage(rows, Objects, Festivals).ToList();
        Assert.Equal(2, sources.Count);                                  // FALSE row yields nothing
        Assert.Equal(DayTable.InWeeks(WeekMask.ForSeason(Season.Winter)), sources[0].Source.Lands);
        Assert.True(sources[1].Source.Conditions.GingerIsland);
    }

    [Fact]
    public void A_random_alternative_is_chance()
    {
        var rows = new[] { new LocationSpawn("Forest", "(O)16", Season.Spring, null, 1.0, 0, false, 0, IsRandom: true) };
        Assert.Equal(Reliability.Chance, SpawnSources.Forage(rows, Objects, Festivals).Single().Source.Reliability);
    }

    [Fact]
    public void A_fish_listed_through_a_query_keeps_its_fish_data()
    {
        var objects = new Dictionary<string, ObjInfo> { ["(O)142"] = new ObjInfo("(O)142", "Carp", -4, 30, new string[0], false) };
        var rows = new[] { new LocationSpawn("Mountain", "RANDOM_ITEMS (O) 142 142", Season.Fall, null, 1.0, 0, false, 0) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)142"] = new FishRow("(O)142", false, "rainy", 3, "600 2600") };
        var (id, source) = SpawnSources.LocationFish(rows, fishRows, objects, Festivals, NoSources).Single();
        Assert.Equal("(O)142", id);
        Assert.Equal(3, source.Conditions.SkillLevel);
        Assert.True(source.Conditions.RainOnly);
        Assert.Equal(Reliability.Chance, source.Reliability);
    }

    [Fact]
    public void Fish_carry_the_higher_level_weather_time_and_catch_limit()
    {
        var rows = new[] { new LocationSpawn("Forest", "(O)775", Season.Winter, null, 1.0, 1, true, 8) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)775"] = new FishRow("(O)775", false, "rainy", 6, "600 1200 1800 2000") };
        var source = SpawnSources.LocationFish(rows, fishRows, Objects, Festivals, BaitAvailable).Single().Source;
        Assert.Equal(SourceKind.Fish, source.Kind);
        Assert.Equal("Fishing", source.Conditions.Skill);
        Assert.Equal(8, source.Conditions.SkillLevel);
        Assert.True(source.Conditions.RainOnly);
        Assert.Equal(1, source.Conditions.CatchLimit);
        Assert.Contains("item:(O)908 Magic Bait", source.Conditions.Requires);
        Assert.Contains("time 600-1200 1800-2000", source.Detail);
    }

    [Fact]
    public void Trap_fish_come_from_crab_pots_all_year()
    {
        var rows = new[] { new FishRow("(O)717", true, "", 0, ""), new FishRow("(O)142", false, "sunny", 0, "600 2600") };
        var (id, source) = SpawnSources.CrabPot(rows).Single();
        Assert.Equal("(O)717", id);
        Assert.Equal(SourceKind.CrabPot, source.Kind);
        Assert.Equal(DayTable.Always, source.Lands);
        Assert.Contains("crafting:Crab Pot", source.Conditions.Requires);
    }

    [Fact]
    public void A_magic_bait_row_routes_through_the_bait_and_inherits_its_island_flag()
    {
        var bait = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)908"] = new[] { new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None with { GingerIsland = true }, "shop QiGemShop") },
        });
        var rows = new[] { new LocationSpawn("Beach", "(O)798", null, "SEASON Winter", 1.0, 0, true, 0) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)798"] = new FishRow("(O)798", false, "both", 0, "600 2600") };
        var squid = SpawnSources.LocationFish(rows, fishRows, new Dictionary<string, ObjInfo>(), NoFestivals, bait).Single(s => s.ItemId == "(O)798").Source;
        Assert.True(squid.Conditions.GingerIsland);
        Assert.Contains("item:(O)908 Magic Bait", squid.Conditions.Requires);
        Assert.Equal(85, squid.Lands.Lands(1));
        var none = SpawnSources.LocationFish(rows, fishRows, new Dictionary<string, ObjInfo>(), NoFestivals, new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>())).ToList();
        Assert.Empty(none);   // no bait anywhere: the row cannot be fished
    }

    [Fact]
    public void A_random_magic_bait_row_stays_chance_even_when_the_bait_is_fully_dependable()
    {
        var bait = new ObtainabilityModel(new Dictionary<string, IReadOnlyList<ObtainSource>>
        {
            ["(O)908"] = new[] { new ObtainSource(SourceKind.Shop, DayTable.Always, Reliability.Dependable, ObtainConditions.None, "shop QiGemShop") },
        });
        var rows = new[] { new LocationSpawn("Beach", "(O)798", null, "SEASON Winter", 1.0, 0, true, 0, IsRandom: true) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)798"] = new FishRow("(O)798", false, "both", 0, "600 2600") };
        var sources = SpawnSources.LocationFish(rows, fishRows, new Dictionary<string, ObjInfo>(), NoFestivals, bait)
            .Where(s => s.ItemId == "(O)798").ToList();
        Assert.NotEmpty(sources);
        Assert.All(sources, s => Assert.Equal(Reliability.Chance, s.Source.Reliability));
    }

    [Fact]
    public void A_sparse_row_condition_inside_a_festival_window_is_a_true_intersection()
    {
        var rows = new[] { new LocationSpawn("Submarine", "(O)798", null, "DAY_OF_MONTH 16", 1.0, 0, false, 0) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)798"] = new FishRow("(O)798", false, "both", 0, "600 2600") };
        var source = SpawnSources.LocationFish(rows, fishRows, Objects, Festivals, NoSources).Single().Source;
        Assert.Equal(100, source.Lands.Lands(1));      // Winter 16 is the only day both the row and the market are open
        Assert.Null(source.Lands.Lands(101));
    }

    [Fact]
    public void A_night_market_fish_lands_on_the_first_market_day()
    {
        var festivals = new Dictionary<string, FestivalDates> { ["NightMarket"] = new FestivalDates("NightMarket", Season.Winter, 15, 17) };
        var rows = new[] { new LocationSpawn("Submarine", "(O)798", null, null, 1.0, 0, false, 0) };
        var fishRows = new Dictionary<string, FishRow> { ["(O)798"] = new FishRow("(O)798", false, "both", 0, "600 2600") };
        var src = SpawnSources.LocationFish(rows, fishRows, new Dictionary<string, ObjInfo>(), festivals, NoSources).Single().Source;
        Assert.Equal(99, src.Lands.Lands(1));
        Assert.Equal(101, src.Lands.Lands(101));
        Assert.Null(src.Lands.Lands(102));
        Assert.True(src.Conditions.FewDays);
    }

    [Fact]
    public void Luck_sources_are_chance()
    {
        var spots = SpawnSources.ArtifactSpots(new[] { new ArtifactSpotRow("Town", "(O)107", null, 0.05) }, Objects, Festivals).Single().Source;
        var cans = SpawnSources.GarbageCans(new[] { new GarbageRow("JoshHouse", "(O)168", null) }, Objects, Festivals).Single().Source;
        Assert.Equal(Reliability.Chance, spots.Reliability);
        Assert.Equal(SourceKind.GarbageCan, cans.Kind);
        Assert.Equal(new[] { "(O)167", "(O)168", "(O)169", "(O)170", "(O)171", "(O)172" },
            SpawnSources.FishingTrash().Select(t => t.ItemId).ToArray());
    }
}
