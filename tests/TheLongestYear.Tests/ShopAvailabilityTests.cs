using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ShopAvailabilityTests
{
    [Theory]
    [InlineData("(O)245", 1)]   // Sugar
    [InlineData("(O)216", 1)]   // Bread, Saloon
    [InlineData("(H)27", 2)]    // Hard Hat
    [InlineData("(O)523", 5)]   // Savage Ring
    [InlineData("(O)PrizeTicket", 1)]
    public void Shop_and_reward_items_have_weeks(string id, int week)
        => Assert.Equal(week, ShopAvailability.Derive(id)!.EarliestWeek);

    [Fact]
    public void Savage_ring_gates_in_spring_like_the_deep_mine()
        => Assert.Equal(Season.Spring, ShopAvailability.Derive("(O)523")!.GateSeason);

    [Fact]
    public void Unclaimed_item_is_null() => Assert.Null(ShopAvailability.Derive("(O)24"));

    [Theory]
    [InlineData("(O)78", 1)]     // Cave Carrot
    [InlineData("(O)342", 4)]    // Pickles
    [InlineData("(O)635", 5)]    // Orange
    [InlineData("(O)638", 13)]   // Cherry, second year
    public void Other_and_fruit_tables(string id, int week) => Assert.Equal(week, ShopAvailability.Derive(id)!.EarliestWeek);

    [Fact]
    public void Plural_category_tags_match_the_games_machine_rules()
    {
        var objects = new Dictionary<string, RawObjectEntry> { ["400"] = new RawObjectEntry("Basic", -79, 120, false, new string[0], "Strawberry") };
        Assert.Equal(new[] { "(O)400" }, ContextTagMatcher.IdsMatchingAll(objects, new[] { "category_fruits" }));
    }

    [Fact]
    public void Hats_and_weapons_from_the_guild_table_are_placed_by_the_composer()
    {
        var composer = new EffortComposer(new EffortData(), new Dictionary<string, ItemAvailability>(), hasKitchen: false);
        Assert.True(composer.DeriveAll().ContainsKey("(H)27"));
    }

    [Fact]
    public void Late_floor_table_beats_a_rule_that_answers_too_early()
    {
        var data = new EffortData
        {
            ArtifactSpots = new List<RawArtifactSpot> { new("Default", "(O)412", 0.5) },
        };
        var composer = new EffortComposer(data, new Dictionary<string, ItemAvailability>(), hasKitchen: false);
        Assert.Equal(13, composer.Derive("(O)412")!.EarliestWeek);   // Winter Root
    }

    [Theory]
    [InlineData("(O)746", 12)]   // Jack-O-Lantern, Spirit's Eve Fall 27
    [InlineData("(O)373", 12)]   // Golden Pumpkin, the maze
    [InlineData("(O)634", 13)]   // Apricot
    public void Table_rows(string id, int week) => Assert.Equal(week, ShopAvailability.Derive(id)!.EarliestWeek);

    [Fact]
    public void Pool_book_is_week_2()
    {
        var composer = new EffortComposer(new EffortData(), new Dictionary<string, ItemAvailability>(), hasKitchen: false,
            books: new List<PoolItem> { new("(O)Book_Diamonds", 5000, 1, new List<Season>(), new List<string>()) });
        Assert.Equal(AvailabilityWeeks.BookWeek, composer.Derive("(O)Book_Diamonds")!.EarliestWeek);
    }

    [Fact]
    public void Pool_artifact_without_a_spot_row_is_week_1()
    {
        var composer = new EffortComposer(new EffortData(), new Dictionary<string, ItemAvailability>(), hasKitchen: false,
            artifacts: new List<PoolItem> { new("(O)103", 300, 1, new List<Season>(), new List<string>()) });
        Assert.Equal(1, composer.Derive("(O)103")!.EarliestWeek);
    }

    [Fact]
    public void Composer_takes_the_earliest_week_across_rules()
    {
        // Red Mushroom: a Mushroom Box machine rule (quest unlock, week 9) and a mine forage row (week 1).
        var data = new EffortData
        {
            MachineRules = new List<RawMachineRule> { new("(BC)128", null, new string[0], new[] { "(O)420" }, 1440, -1) },
            MachineUnlocks = new Dictionary<string, string> { ["(BC)128"] = "null" },
            ForageSpawns = new List<RawSpawnEntry> { new("(O)420", null, null, "UndergroundMine20") },
        };
        var composer = new EffortComposer(data, new Dictionary<string, ItemAvailability>(), hasKitchen: false);
        Assert.Equal(1, composer.Derive("(O)420")!.EarliestWeek);
    }

    [Fact]
    public void Builder_places_every_trap_fish_in_the_data()
    {
        var pools = new ItemPools { TrapFishIds = new HashSet<string> { "(O)715", "(O)716" } };
        var model = ItemAvailabilityBuilder.Build(pools);
        Assert.True(model.IsPlaced("(O)715"));
        Assert.Equal(AvailabilityWeeks.TrapFishWeek, model.For("(O)716").Week);
    }
}
