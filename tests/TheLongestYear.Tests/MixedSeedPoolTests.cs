using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The Mixed Seeds pool another mod keeps, read as Cornucopia More Crops 1.6.3 fills it
/// (data/mixedseeds.json with its options on), and the share of each seed per bag.</summary>
public class MixedSeedPoolTests
{
    private const string NotIsland = "TRUE, !LOCATION_NAME Here IslandWest, ";
    private const double Tolerance = 0.0005;

    private static string In(string seasons)
        => NotIsland + $"ANY \"LOCATION_SEASON Here {seasons}\" \"LOCATION_IS_INDOORS Here\"";

    /// <summary>Cornucopia's valley entries, in file order, with the crop's seasons.</summary>
    private static readonly (string Seed, string Condition, Season[] Grows)[] Cornucopia =
    {
        ("(O)Cornucopia_BasilSeeds", In("spring"), new[] { Season.Spring }),
        ("(O)Cornucopia_OnionSeeds", In("spring"), new[] { Season.Spring }),
        ("(O)Cornucopia_SugarBeetSeeds", In("spring"), new[] { Season.Spring }),
        ("(O)Cornucopia_SpinachSeeds", In("spring"), new[] { Season.Spring }),
        ("(O)Cornucopia_CanolaFlowerSeeds", In("spring summer"), new[] { Season.Spring, Season.Summer }),
        ("(O)Cornucopia_AsparagusSeeds", In("spring"), new[] { Season.Spring }),
        ("(O)Cornucopia_BellPepperSeeds", In("summer"), new[] { Season.Summer }),
        ("(O)Cornucopia_WatermelonSeeds", In("summer"), new[] { Season.Summer }),
        ("(O)Cornucopia_LettuceSeeds", In("summer"), new[] { Season.Summer }),
        ("(O)Cornucopia_GroundcherriesSeeds", In("summer fall"), new[] { Season.Summer, Season.Fall }),
        ("(O)Cornucopia_ChickpeaSeeds", In("summer"), new[] { Season.Summer }),
        ("(O)Cornucopia_TurnipSeeds", In("fall"), new[] { Season.Fall }),
        ("(O)Cornucopia_ZucchiniSeeds", In("fall"), new[] { Season.Fall }),
        ("(O)Cornucopia_OatsSeeds", In("fall"), new[] { Season.Fall }),
        // The mod's own file leaves this entry's last quote open.
        ("(O)Cornucopia_BarleySeeds", NotIsland + "ANY \"LOCATION_SEASON Here fall\" \"LOCATION_IS_INDOORS Here", new[] { Season.Fall }),
        ("(O)Cornucopia_CelerySeeds", In("fall"), new[] { Season.Fall }),
        ("(O)Cornucopia_SugarcaneSeeds", "LOCATION_NAME Here IslandWest", new[] { Season.Summer }),
    };

    private static readonly Dictionary<string, Season[]> Grows = Cornucopia.ToDictionary(c => c.Seed, c => c.Grows)
        .Concat(new Dictionary<string, Season[]>
        {
            ["(O)472"] = new[] { Season.Spring }, ["(O)474"] = new[] { Season.Spring }, ["(O)475"] = new[] { Season.Spring },
            ["(O)476"] = new[] { Season.Spring },
        }).ToDictionary(p => p.Key, p => p.Value);

    private static IReadOnlyList<Season>? SeasonsOf(string seed) => Grows.TryGetValue(seed, out Season[]? s) ? s : null;

    private static List<MixedSeedRow> Pool()
    {
        List<MixedSeedRow> rows = Cornucopia.Select(c => new MixedSeedRow(c.Seed, c.Condition, 1)).ToList();
        rows.Add(new MixedSeedRow(MixedSeedPool.MixedSeedsQualifiedId, null, 1));
        return rows;
    }

    private static IReadOnlyDictionary<string, double> Outdoors(Season season, IReadOnlyList<MixedSeedSubstitution>? subs = null)
        => MixedSeedPool.Shares(season, indoors: false, Pool(), SeasonsOf, subs);

    [Theory]
    [InlineData("LOCATION_SEASON Here spring", Season.Spring, false, true)]
    [InlineData("LOCATION_SEASON Here spring", Season.Fall, false, false)]
    [InlineData("LOCATION_SEASON Here spring summer", Season.Summer, false, true)]
    [InlineData("!LOCATION_NAME Here IslandWest", Season.Spring, false, true)]
    [InlineData("LOCATION_NAME Here IslandWest", Season.Spring, false, false)]
    [InlineData("ANY \"LOCATION_SEASON Here fall\" \"LOCATION_IS_INDOORS Here\"", Season.Spring, true, true)]
    [InlineData("ANY \"LOCATION_SEASON Here fall\" \"LOCATION_IS_INDOORS Here\"", Season.Spring, false, false)]
    [InlineData("false, LOCATION_SEASON Here spring", Season.Spring, false, false)]
    [InlineData("true, SEASON spring", Season.Spring, false, true)]
    [InlineData(null, Season.Fall, false, true)]
    public void ConditionIsReadForAValleyPlanting(string? condition, Season season, bool indoors, bool expected)
        => Assert.Equal(expected, MixedSeedPool.Admits(condition, season, indoors));

    /// <summary>The game keeps escaped quotes through its comma split, so an ANY inside an ANY reads.</summary>
    [Fact]
    public void ANestedAnyIsRead()
    {
        const string nested = "ANY \"ANY \\\"SEASON spring\\\" \\\"SEASON summer\\\"\" \"FALSE\"";
        Assert.True(MixedSeedPool.Admits(nested, Season.Spring, indoors: false));
        Assert.True(MixedSeedPool.Admits(nested, Season.Summer, indoors: false));
        Assert.False(MixedSeedPool.Admits(nested, Season.Fall, indoors: false));
    }

    [Fact]
    public void WithCropsAnytimeAnywhereACropsSeasonIsNotAsked()
    {
        var pool = new List<MixedSeedRow>
        {
            new("(O)Cornucopia_ZucchiniSeeds", null, 1), new("(O)Cornucopia_BasilSeeds", null, 1),
            new(MixedSeedPool.AnySeasonKey, null, 0), new(MixedSeedPool.MixedSeedsQualifiedId, null, 1),
        };
        IReadOnlyDictionary<string, double> shares = MixedSeedPool.Shares(Season.Spring, false, pool, SeasonsOf);
        Assert.Equal(1.0 / 3, shares["(O)Cornucopia_ZucchiniSeeds"], 6);
        Assert.Equal(1.0 / 3, shares["(O)Cornucopia_BasilSeeds"], 6);
        Assert.False(shares.ContainsKey(MixedSeedPool.AnySeasonKey));
    }

    [Theory]
    [InlineData("PLAYER_HAS_MAIL Current someFlag")]
    [InlineData("LOCATION_SEASON Target spring")]
    [InlineData("ANY \"WEATHER Here Rain\" \"SOMETHING_NEW\"")]
    public void AnUnknownClauseIsNotJudged(string condition)
        => Assert.Null(MixedSeedPool.Admits(condition, Season.Spring, indoors: false));

    [Fact]
    public void AFalseClauseSettlesItWhateverElseIsUnknown()
        => Assert.False(MixedSeedPool.Admits("PLAYER_HAS_MAIL Current someFlag, FALSE", Season.Spring, indoors: false));

    [Fact]
    public void SpringGivesEachCornucopiaSeedOneBagInSeven()
    {
        IReadOnlyDictionary<string, double> shares = Outdoors(Season.Spring);
        foreach (string seed in new[] { "Basil", "Onion", "SugarBeet", "Spinach", "CanolaFlower", "Asparagus" })
            Assert.Equal(1.0 / 7, shares[$"(O)Cornucopia_{seed}Seeds"], 6);
        // The vanilla slot keeps vanilla's own odds: Parsnip half of it.
        Assert.Equal(0.5 / 7, shares["(O)472"], 6);
        Assert.Equal(0.25 / 7, shares["(O)474"], 6);
        Assert.Equal(1.0, shares.Values.Sum(), 6);
        Assert.False(shares.ContainsKey("(O)Cornucopia_ZucchiniSeeds"));
        Assert.False(shares.ContainsKey("(O)Cornucopia_SugarcaneSeeds"));
    }

    [Fact]
    public void SummerHoldsSixSeedsAndTheVanillaSlot()
    {
        IReadOnlyDictionary<string, double> shares = Outdoors(Season.Summer);
        foreach (string seed in new[] { "CanolaFlower", "BellPepper", "Watermelon", "Lettuce", "Groundcherries", "Chickpea" })
            Assert.Equal(1.0 / 7, shares[$"(O)Cornucopia_{seed}Seeds"], 6);
        Assert.Equal(0.25 / 7, shares["(O)487"], 6);
        Assert.Equal(1.0, shares.Values.Sum(), 6);
    }

    /// <summary>Measured in game on 2026-11-06 (tly_mixedseeds, Fall): Artichoke Seeds 25%, each
    /// Cornucopia seed about 10.7%, Corn, Eggplant and Pumpkin about 3.6% each.</summary>
    [Fact]
    public void FallKeepsArtichokeAtAQuarterAndSharesTheRest()
    {
        IReadOnlyDictionary<string, double> shares = Outdoors(Season.Fall);
        Assert.Equal(0.25, shares[MixedSeedPool.ArtichokeSeedsQualifiedId], 6);
        foreach (string seed in new[] { "Groundcherries", "Turnip", "Zucchini", "Oats", "Barley", "Celery" })
            Assert.InRange(shares[$"(O)Cornucopia_{seed}Seeds"], 0.75 / 7 - Tolerance, 0.75 / 7 + Tolerance);
        foreach (string vanilla in new[] { "(O)487", "(O)488", "(O)490" })
            Assert.Equal(0.25 / 7, shares[vanilla], 6);
        Assert.Equal(1.0, shares.Values.Sum(), 6);
    }

    [Fact]
    public void ACultivationUpgradeTakesItsTenthFirst()
    {
        var garlic = new[] { new MixedSeedSubstitution("(O)476", 0.10) };
        IReadOnlyDictionary<string, double> shares = Outdoors(Season.Spring, garlic);
        Assert.Equal(0.10, shares["(O)476"], 6);
        Assert.Equal(0.90 / 7, shares["(O)Cornucopia_AsparagusSeeds"], 6);
        Assert.Equal(1.0, shares.Values.Sum(), 6);
    }

    [Fact]
    public void TheBoostOverridesTheUpgradeWhenBothRoll()
    {
        var both = new[] { new MixedSeedSubstitution("(O)476", 0.10), new MixedSeedSubstitution("(O)476", 0.05) };
        IReadOnlyDictionary<string, double> shares = Outdoors(Season.Spring, both);
        Assert.Equal(1 - 0.90 * 0.95, shares["(O)476"], 6);
        Assert.Equal(0.90 * 0.95 / 7, shares["(O)Cornucopia_BasilSeeds"], 6);
    }

    [Fact]
    public void AnEmptyPoolIsVanillasRoll()
    {
        IReadOnlyDictionary<string, double> fall = MixedSeedPool.Shares(Season.Fall, false, new List<MixedSeedRow>(), SeasonsOf);
        Assert.Equal(4, fall.Count);
        Assert.All(fall.Values, v => Assert.Equal(0.25, v, 6));
        IReadOnlyDictionary<string, double> spring = MixedSeedPool.Shares(Season.Spring, false, new List<MixedSeedRow>(), SeasonsOf);
        Assert.Equal(0.5, spring["(O)472"], 6);
    }

    [Fact]
    public void WinterOutdoorsPlantsNothing()
        => Assert.Empty(Outdoors(Season.Winter));

    [Fact]
    public void TheGreenhouseTakesEveryValleyEntryInAnySeason()
    {
        IReadOnlyDictionary<string, double> shares = MixedSeedPool.Shares(Season.Winter, indoors: true, Pool(), SeasonsOf);
        // 16 valley entries and the vanilla slot; Winter's roll is one of three seasons', so Artichoke is a twelfth.
        double artichoke = 0.25 / 3;
        Assert.Equal(artichoke, shares[MixedSeedPool.ArtichokeSeedsQualifiedId], 6);
        Assert.Equal((1 - artichoke) / 17, shares["(O)Cornucopia_ZucchiniSeeds"], 6);
        Assert.Equal((1 - artichoke) / 17, shares["(O)Cornucopia_AsparagusSeeds"], 6);
        Assert.False(shares.ContainsKey("(O)Cornucopia_SugarcaneSeeds"));
        Assert.Equal(1.0, shares.Values.Sum(), 6);
    }

    [Fact]
    public void WeightAddsSlots()
    {
        var pool = new List<MixedSeedRow>
        {
            new("(O)Cornucopia_BasilSeeds", null, 3), new("(O)Cornucopia_OnionSeeds", null, 1),
            new(MixedSeedPool.MixedSeedsQualifiedId, null, 1),
        };
        IReadOnlyDictionary<string, double> shares = MixedSeedPool.Shares(Season.Spring, false, pool, SeasonsOf);
        Assert.Equal(3.0 / 5, shares["(O)Cornucopia_BasilSeeds"], 6);
        Assert.Equal(1.0 / 5, shares["(O)Cornucopia_OnionSeeds"], 6);
    }

    [Fact]
    public void ASeedWithNoCropRowIsSkippedAndOneOutOfSeasonToo()
    {
        var pool = new List<MixedSeedRow>
        {
            new("(O)NoSuchSeeds", null, 1), new("(O)Cornucopia_ZucchiniSeeds", null, 1), new("(O)Cornucopia_BasilSeeds", null, 1),
            new(MixedSeedPool.MixedSeedsQualifiedId, null, 1),
        };
        IReadOnlyDictionary<string, double> shares = MixedSeedPool.Shares(Season.Spring, false, pool, SeasonsOf);
        Assert.Equal(0.5, shares["(O)Cornucopia_BasilSeeds"], 6);
        Assert.False(shares.ContainsKey("(O)NoSuchSeeds"));
        Assert.False(shares.ContainsKey("(O)Cornucopia_ZucchiniSeeds"));
    }

    [Fact]
    public void AnEntryThatCannotBeJudgedTakesItsSlotAndGivesNoRoute()
    {
        var pool = new List<MixedSeedRow>
        {
            new("(O)Cornucopia_BasilSeeds", "PLAYER_HAS_MAIL Current someFlag", 1), new("(O)Cornucopia_OnionSeeds", null, 1),
            new(MixedSeedPool.MixedSeedsQualifiedId, null, 1),
        };
        IReadOnlyDictionary<string, double> shares = MixedSeedPool.Shares(Season.Spring, false, pool, SeasonsOf);
        Assert.Equal(1.0 / 3, shares[MixedSeedPool.Unreadable], 6);
        Assert.Equal(1.0 / 3, shares["(O)Cornucopia_OnionSeeds"], 6);
        Assert.False(shares.ContainsKey("(O)Cornucopia_BasilSeeds"));
        Assert.DoesNotContain("(O)Cornucopia_BasilSeeds", MixedSeedPool.FarmSeeds(pool, SeasonsOf));
    }

    [Fact]
    public void TheVanillaSlotCanBeSwitchedOffByTheOtherMod()
    {
        var pool = new List<MixedSeedRow>
        {
            new("(O)Cornucopia_BasilSeeds", null, 1), new("(O)Cornucopia_OnionSeeds", null, 1),
            new(MixedSeedPool.MixedSeedsQualifiedId, null, 0),
        };
        IReadOnlyDictionary<string, double> shares = MixedSeedPool.Shares(Season.Spring, false, pool, SeasonsOf);
        Assert.Equal(0.5, shares["(O)Cornucopia_BasilSeeds"], 6);
        Assert.False(shares.ContainsKey("(O)472"));
    }

    [Fact]
    public void TheVanillaCropsKeyAddsVanillasSeedsOnceEach()
    {
        var pool = new List<MixedSeedRow>
        {
            new("$vanilla_crops", null, 1), new("(O)Cornucopia_BasilSeeds", null, 1),
            new(MixedSeedPool.MixedSeedsQualifiedId, null, 0),
        };
        IReadOnlyDictionary<string, double> shares = MixedSeedPool.Shares(Season.Spring, false, pool, SeasonsOf);
        Assert.Equal(0.25, shares["(O)Cornucopia_BasilSeeds"], 6);
        Assert.Equal(0.25, shares["(O)472"], 6);
    }

    [Fact]
    public void FarmSeedsAreThePoolsOwnSeedsAYearOneFarmCanGet()
    {
        IReadOnlySet<string> seeds = MixedSeedPool.FarmSeeds(Pool(), SeasonsOf);
        Assert.Equal(16, seeds.Count);
        Assert.Contains("(O)Cornucopia_CelerySeeds", seeds);
        Assert.DoesNotContain("(O)Cornucopia_SugarcaneSeeds", seeds);
        Assert.DoesNotContain("(O)472", seeds);
        Assert.Empty(MixedSeedPool.FarmSeeds(new List<MixedSeedRow>(), SeasonsOf));
    }
}
