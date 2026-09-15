using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Tests;

public class ObtainabilityCodeSourcesTests
{
    [Fact]
    public void Mine_fish_are_dependable_any_day_with_their_floor_as_a_condition()
    {
        var fish = CodeSources.MineFish().ToDictionary(s => s.ItemId, s => s.Source);
        Assert.Equal(new[] { "(O)158", "(O)161", "(O)162" }, fish.Keys.OrderBy(k => k).ToArray());
        Assert.All(fish.Values, s => { Assert.Equal(SourceKind.Fish, s.Kind); Assert.Equal(Reliability.Dependable, s.Reliability); Assert.Equal(1, s.Lands.Lands(1)); });
        // The roll only grows with fishing level; there is no level gate, so no skill is named.
        Assert.All(fish.Values, s => { Assert.Null(s.Conditions.Skill); Assert.Equal(0, s.Conditions.SkillLevel); });
        Assert.Contains("mines:floor 1", fish["(O)158"].Conditions.Requires);
        Assert.Contains("mines:floor 40", fish["(O)161"].Conditions.Requires);
        Assert.Contains("mines:floor 80", fish["(O)162"].Conditions.Requires);
    }

    [Fact]
    public void Moss_is_forage_outside_winter_and_all_year_in_the_greenhouse()
    {
        var moss = CodeSources.Moss().ToList();
        var outdoor = moss.Single(s => s.Source.Kind == SourceKind.Forage).Source;
        Assert.Equal("(O)Moss", moss[0].ItemId);
        Assert.Equal(1, outdoor.Lands.Lands(1));
        Assert.Null(outdoor.Lands.Lands(85));
        var indoor = moss.Single(s => s.Source.Kind == SourceKind.GreenhouseCrop).Source;
        Assert.Equal(85, indoor.Lands.Lands(85));
        Assert.Contains("mail:ccPantry", indoor.Conditions.Requires);
    }

    [Fact]
    public void Season_seeds_switch_to_the_next_season_late_in_the_month()
    {
        var seeds = CodeSources.SeasonSeeds().ToDictionary(s => s.ItemId, s => s.Source);
        Assert.Equal(1, seeds["(O)CarrotSeeds"].Lands.Lands(1));
        Assert.Equal(105, seeds["(O)CarrotSeeds"].Lands.Lands(24));    // Spring 24 onward gives Summer Squash; Carrot comes back from Winter 21
        Assert.Equal(24, seeds["(O)SummerSquashSeeds"].Lands.Lands(1));
        Assert.Equal(49, seeds["(O)BroccoliSeeds"].Lands.Lands(1));    // Summer 21
        Assert.Equal(77, seeds["(O)PowdermelonSeeds"].Lands.Lands(1)); // Fall 21
        Assert.Null(seeds["(O)PowdermelonSeeds"].Lands.Lands(105));    // Winter 21 onward gives Carrot
        Assert.Equal(105, seeds["(O)CarrotSeeds"].Lands.Lands(100));
        Assert.All(seeds.Values, s => { Assert.Equal(SourceKind.FishingTreasure, s.Kind); Assert.Equal(Reliability.Chance, s.Reliability); });
    }

    [Fact]
    public void Guild_rewards_come_from_the_slayer_rows()
    {
        var rows = new[] { new SlayerQuestRow("Duggies", new[] { "Duggy" }, 30, "(H)27") };
        var hat = CodeSources.GuildRewards(rows).Single().Source;
        Assert.Equal(SourceKind.Guild, hat.Kind);
        Assert.Equal(Reliability.Dependable, hat.Reliability);
        Assert.Equal(1, hat.Lands.Lands(1));
        Assert.Contains("guild:Duggies 30 kills (Duggy)", hat.Conditions.Requires);
        Assert.Contains("mines:floor 1", hat.Conditions.Requires);   // MineSources.MonsterFloor for the first target
    }
}
