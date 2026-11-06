using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>How many bags of Mixed Seeds a year-1 farmer can gather, what that yields of a crop only
/// another mod's pool gives, and how that reaches the lexicon rows, the layer variants and the ask.</summary>
public class MixedSeedSupplyTests
{
    private const string Cornucopia = "Cornucopia.MoreCrops";
    private const string Game = "1.6.15";
    private const string Mod = "1.1.0";
    private const string Asparagus = "(O)Cornucopia_Asparagus";
    private const string AsparagusSeeds = "(O)Cornucopia_AsparagusSeeds";
    private const string Zucchini = "(O)Cornucopia_Zucchini";
    private const string ZucchiniSeeds = "(O)Cornucopia_ZucchiniSeeds";
    private const string BasilSeeds = "(O)Cornucopia_BasilSeeds";
    private static readonly string PoolVariant = AnswerSet.BaseVariant + AnswerSet.DoorJoiner + AnswerSet.MixedSeedPoolTag;

    private static string In(string season) => $"ANY \"LOCATION_SEASON Here {season}\" \"LOCATION_IS_INDOORS Here\"";

    /// <summary>Six Spring entries and six Fall ones, as Cornucopia fills a season.</summary>
    private static List<MixedSeedRow> Pool()
    {
        var rows = new List<MixedSeedRow> { new(AsparagusSeeds, In("spring"), 1), new(BasilSeeds, In("spring"), 1) };
        rows.AddRange(Enumerable.Range(1, 4).Select(i => new MixedSeedRow($"(O)Spring{i}Seeds", In("spring"), 1)));
        rows.Add(new MixedSeedRow(ZucchiniSeeds, In("fall"), 1));
        rows.AddRange(Enumerable.Range(1, 5).Select(i => new MixedSeedRow($"(O)Fall{i}Seeds", In("fall"), 1)));
        rows.Add(new MixedSeedRow(MixedSeedPool.MixedSeedsQualifiedId, null, 1));
        return rows;
    }

    private static MixedSeedInputs Inputs()
    {
        var crops = new List<MixedSeedCrop>
        {
            new(AsparagusSeeds, Asparagus, new[] { Season.Spring }, 5, 0, 1, OnlyRoute: true),
            new(BasilSeeds, "(O)Cornucopia_Basil", new[] { Season.Spring }, 3, 4, 1, OnlyRoute: false),
            new(ZucchiniSeeds, Zucchini, new[] { Season.Fall }, 14, 2, 1, OnlyRoute: true),
        };
        crops.AddRange(Enumerable.Range(1, 4).Select(i => new MixedSeedCrop($"(O)Spring{i}Seeds", $"(O)Spring{i}", new[] { Season.Spring }, 4, 0, 1, false)));
        crops.AddRange(Enumerable.Range(1, 5).Select(i => new MixedSeedCrop($"(O)Fall{i}Seeds", $"(O)Fall{i}", new[] { Season.Fall }, 4, 0, 1, false)));
        return new MixedSeedInputs(Pool(), crops);
    }

    [Fact]
    public void SpringsBagsAreTheWeedsStandingOnTheFirstDay()
    {
        // 763 weeds at 2.5% each: about 19 bags, all there from Spring 1.
        Assert.Equal(763 * 0.025, MixedSeedSupply.BagsInSeasonBy(Season.Spring, 1), 6);
        Assert.Equal(MixedSeedSupply.BagsInSeasonBy(Season.Spring, 1), MixedSeedSupply.BagsInSeasonBy(Season.Spring, 28), 6);
    }

    [Fact]
    public void KrobusSellsTenEachThursdayOnceTheSewerIsOpen()
    {
        List<MixedSeedArrival> krobus = MixedSeedSupply.Arrivals.Where(a => a.Source.StartsWith("Krobus", StringComparison.Ordinal)).ToList();
        // Summer 18 and 25, then Fall 4, 11, 18 and 25.
        Assert.Equal(new[] { 46, 53, 60, 67, 74, 81 }, krobus.Select(a => a.DayOfYear));
        Assert.All(krobus, a => Assert.Equal(10, a.Bags));
    }

    /// <summary>The Mutant Bug Lair opens with the Dark Talisman quest, after the Community Center.</summary>
    [Fact]
    public void NoBagComesFromAMapYearOneDoesNotReach()
        => Assert.DoesNotContain(MixedSeedSupply.Arrivals, a => a.Source.Contains("Bug Lair", StringComparison.Ordinal));

    [Fact]
    public void ASeedNothingElseIsKnownToGiveHasNoKnownSource()
    {
        var none = new HashSet<string>();
        var reach = new SourceReachability(
            none, new[] { new RawShopListing("(O)SoldSeeds", "SeedShop", LockedAfterYearOne: true) }, new[] { new RawShopPlacement("SeedShop", "Town") },
            new RawCropEntry[0], new RawRecipeEntry[0], none);
        // Sold only from year 2: a known, closed route. Listed nowhere: no known route at all.
        Assert.True(reach.IsUnreachable("(O)SoldSeeds"));
        Assert.False(reach.KnowsNoSource("(O)SoldSeeds"));
        Assert.False(reach.IsUnreachable("(O)PoolOnlySeeds"));
        Assert.True(reach.KnowsNoSource("(O)PoolOnlySeeds"));
    }

    [Fact]
    public void ABagIsCountedOnceInTheSeasonItTurnsUp()
    {
        double spring = MixedSeedSupply.BagsInSeasonBy(Season.Spring, 112);
        double summer = MixedSeedSupply.BagsInSeasonBy(Season.Summer, 112);
        double fall = MixedSeedSupply.BagsInSeasonBy(Season.Fall, 112);
        Assert.Equal(MixedSeedSupply.Arrivals.Sum(a => a.Bags), spring + summer + fall, 6);
        Assert.Equal(0, MixedSeedSupply.BagsInSeasonBy(Season.Fall, MixedSeedSupply.LastDay(Season.Summer)), 6);
    }

    [Theory]
    [InlineData(1, 5, 0, 1)]      // planted Spring 1, 5 days, no regrowth: one harvest
    [InlineData(24, 5, 0, 0)]     // planted Spring 24: the harvest would be Summer 1
    [InlineData(23, 5, 0, 1)]     // planted Spring 23: harvested on the 28th
    [InlineData(1, 14, 2, 7)]     // Zucchini from the first day: the 15th, then every 2 days to the 27th
    [InlineData(4, 14, 2, 6)]     // from the 4th: the 18th to the 28th
    [InlineData(11, 14, 2, 2)]    // from the 11th: the 25th and the 27th
    [InlineData(18, 14, 2, 0)]
    public void HarvestsStopAtTheSeasonsEnd(int plantDay, int growth, int regrow, int expected)
        => Assert.Equal(expected, MixedSeedSupply.Harvests(plantDay, Season.Spring, growth, regrow));

    [Fact]
    public void ASpringCropYieldsItsShareOfSpringsBags()
    {
        IReadOnlyDictionary<string, double[]> bases = MixedSeedSupply.Bases(Inputs());
        // 19.075 bags, 1 in 7 of them Asparagus Seeds, one harvest of one each.
        Assert.Equal(763 * 0.025 / 7, bases[Asparagus][(int)Season.Spring], 6);
        Assert.Equal(0, bases[Asparagus][(int)Season.Fall]);
    }

    [Fact]
    public void AFallCropYieldsFromFallsBagsAndTheHarvestsEachStillGets()
    {
        IReadOnlyDictionary<string, double[]> bases = MixedSeedSupply.Bases(Inputs());
        // Fall's share is 0.75 / 7 (Artichoke Seeds keep their quarter). Fall 1's weeds get 7 harvests,
        // Krobus' bags of Fall 4 get 6, of Fall 11 get 2, and the later ones none.
        double share = 0.75 / 7;
        double expected = share * (MixedSeedSupply.FallStartWeeds * MixedSeedSupply.BagsPerWeed * 7 + 10 * 6 + 10 * 2);
        Assert.Equal(expected, bases[Zucchini][(int)Season.Fall], 6);
    }

    [Fact]
    public void OnlyACropNothingElseGivesGetsABasis()
    {
        IReadOnlyDictionary<string, double[]> bases = MixedSeedSupply.Bases(Inputs());
        Assert.Equal(new[] { Asparagus, Zucchini }, bases.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Empty(MixedSeedSupply.Bases(null));
    }

    [Fact]
    public void TheAskIsTheDifficultysBandOfTheBasis()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>(StringComparer.Ordinal))
            { MixedSeedCropBases = MixedSeedSupply.Bases(Inputs()) };
        Assert.True(QuantityAskPass.Covers(Asparagus, model));
        Assert.False(QuantityAskPass.Covers(Asparagus));
        double basis = QuantityAskPass.BasisByDeadline(Asparagus, Season.Spring, model)!.Value;
        Assert.Equal(763 * 0.025 / 7, basis, 6);
        // About 2.7 Asparagus in a Spring: Easy asks 1, Normal 1 or 2, Hard 2, Extreme 2 or 3.
        (int Low, int High) Range(DifficultyStep step)
        {
            DifficultyProfile profile = DifficultyResolver.Resolve(new DifficultySettings { StackSize = step }, new GameplayConfig());
            var rolls = Enumerable.Range(0, 200).Select(seed => AskBands.Roll(basis, profile, new Random(seed))).ToList();
            return (rolls.Min(), rolls.Max());
        }
        Assert.Equal((1, 1), Range(DifficultyStep.Easy));
        Assert.Equal((1, 2), Range(DifficultyStep.Normal));
        Assert.Equal((2, 2), Range(DifficultyStep.Hard));
        Assert.Equal((2, 3), Range(DifficultyStep.Extreme));
        // A Fall crop asked by a Spring deadline has no basis yet.
        Assert.Null(QuantityAskPass.BasisByDeadline(Zucchini, Season.Spring, model));
    }

    [Fact]
    public void AnEmptyPoolAddsNoRowAndAFilledOneAddsOneEach()
    {
        ObtainabilityInputs vanilla = VanillaInputsFixture.Load();
        IReadOnlyDictionary<string, ulong> before = RowHashes.Of(vanilla, false);
        Assert.DoesNotContain(before.Keys, k => k.StartsWith("mixedseed|", StringComparison.Ordinal));
        IReadOnlyDictionary<string, ulong> after = RowHashes.Of(vanilla with { MixedSeedPool = Pool() }, false);
        Assert.Equal(before.Count + Pool().Count, after.Count);
        Assert.All(before, row => Assert.Equal(row.Value, after[row.Key]));
        Assert.Empty(AnswerSet.DataTags(vanilla));
        Assert.Equal(new[] { AnswerSet.MixedSeedPoolTag }, AnswerSet.DataTags(vanilla with { MixedSeedPool = Pool() }));
    }

    [Fact]
    public void AGameWithAPoolReadsItsOwnVariant()
    {
        Assert.Equal("base+mixed_seed_pool", AnswerSet.VariantName(false, null, new[] { AnswerSet.MixedSeedPoolTag }));
        Assert.Equal("pierre_year2_seeds+keep_minecarts+mixed_seed_pool",
            AnswerSet.VariantName(true, new[] { "keep_minecarts" }, new[] { AnswerSet.MixedSeedPoolTag }));
        Assert.Equal("base", AnswerSet.StemOf("base+mixed_seed_pool"));
    }

    [Fact]
    public void AvailabilityInputsWithoutAPoolHashAsTheyDidBefore()
    {
        var pools = new ItemPools();
        string without = AvailabilityInputs.From(pools, null, null, null, null, true).Hash();
        string emptyPool = AvailabilityInputs.From(pools with { MixedSeeds = new MixedSeedInputs(new List<MixedSeedRow>(), new List<MixedSeedCrop>()) }, null, null, null, null, true).Hash();
        string filled = AvailabilityInputs.From(pools with { MixedSeeds = Inputs() }, null, null, null, null, true).Hash();
        Assert.Equal(without, emptyPool);
        Assert.NotEqual(without, filled);
        Assert.DoesNotContain("MixedSeeds", System.Text.Json.JsonSerializer.Serialize(AvailabilityInputs.From(pools, null, null, null, null, true), LexiconJson.Options));
    }

    [Fact]
    public void AnswersWithoutABasisWriteNoMember()
    {
        var answers = new AvailabilityAnswers(
            new Dictionary<string, ItemAvailability>(), new Dictionary<string, ItemEffort>(),
            new Dictionary<string, double[]>(), new Dictionary<string, int>());
        Assert.DoesNotContain("MixedSeedCropBases", System.Text.Json.JsonSerializer.Serialize(answers, LexiconJson.Options));
        string with = System.Text.Json.JsonSerializer.Serialize(answers with { MixedSeedCropBases = MixedSeedSupply.Bases(Inputs()) }, LexiconJson.Options);
        AvailabilityAnswers back = System.Text.Json.JsonSerializer.Deserialize<AvailabilityAnswers>(with, LexiconJson.Options)!;
        Assert.Equal(763 * 0.025 / 7, back.MixedSeedCropBases![Asparagus][0], 6);
    }

    // ---- The layer variant ----

    private const string Item = "(O)257";
    private const string Layer = "{\"UniqueId\": \"" + Cornucopia + "\", \"CheckedVersion\": \"1.0.0\", \"FillsMixedSeedPool\": true}";
    private const string PlainLayer = "{\"UniqueId\": \"Other.Mod\", \"CheckedVersion\": \"1.0.0\"}";

    private static AnswerVariant Variant(string detail)
    {
        var obtain = new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal)
        {
            [Item] = new[] { new ObtainSource(SourceKind.Forage, DayTable.Always, Reliability.Dependable, ObtainConditions.None, detail) },
        };
        return new AnswerVariant(obtain, obtain, Array.Empty<string>(), 1,
            new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>());
    }

    private static AnswerSet Vanilla()
        => new(Game, Mod, new[] { RowHashes.Vanilla }, new Dictionary<string, ulong> { ["obj|(O)1"] = 1 },
            new Dictionary<string, AnswerVariant>(StringComparer.Ordinal) { [AnswerSet.BaseVariant] = Variant("vanilla") });

    private static LayerAnswers Answers(string mod, params (string Variant, string Detail)[] deltas)
        => new(new[] { mod }, mod + "@1.0.0",
            deltas.ToDictionary(d => d.Variant, d => (IReadOnlyDictionary<string, ulong>)new Dictionary<string, ulong> { [$"row|{mod}|{d.Variant}"] = 7 }, StringComparer.Ordinal),
            deltas.ToDictionary(d => d.Variant, d => Variant(d.Detail), StringComparer.Ordinal));

    private static string DetailOf(AnswerSet set, string variant) => set.Resolve(variant)!.Obtain[Item].Single().Detail;

    [Fact]
    public void ALayerThatFillsThePoolIsReadFromItsInputsFile()
    {
        Assert.True(LayerFile.ReadInputs(Layer).FillsMixedSeedPool);
        Assert.False(LayerFile.ReadInputs(PlainLayer).FillsMixedSeedPool);
        Assert.DoesNotContain("FillsMixedSeedPool", LayerFile.WriteInputs(LayerFile.ReadInputs(PlainLayer)));
    }

    [Fact]
    public void TheLayerServesItsPoolVariantOverVanillasStem()
    {
        LayerSet set = LayerSet.Compose(Vanilla(), new[] { LayerFile.ReadInputs(Layer) },
            new[] { Answers(Cornucopia, (AnswerSet.BaseVariant, "no pool"), (PoolVariant, "pool")) }, _ => { });
        Assert.Equal("no pool", DetailOf(set.Composed!, AnswerSet.BaseVariant));
        Assert.Equal("pool", DetailOf(set.Composed!, PoolVariant));
        Assert.Contains($"row|{Cornucopia}|{PoolVariant}", set.Composed!.RowHashesFor(PoolVariant).Keys);
        Assert.DoesNotContain($"row|{Cornucopia}|{AnswerSet.BaseVariant}", set.Composed!.RowHashesFor(PoolVariant).Keys);
    }

    [Fact]
    public void WithoutThePoolVariantThoseGamesAreNotServedTheLayersOtherAnswers()
    {
        LayerSet set = LayerSet.Compose(Vanilla(), new[] { LayerFile.ReadInputs(Layer) },
            new[] { Answers(Cornucopia, (AnswerSet.BaseVariant, "no pool")) }, _ => { });
        Assert.NotNull(set.Composed!.Resolve(AnswerSet.BaseVariant));
        Assert.Null(set.Composed!.Resolve(PoolVariant));
    }

    [Fact]
    public void AnotherModsPoolLeavesALayerThatDoesNotFillItWithoutAVariant()
    {
        // The pool's rows are in the data, so nothing shipped may match: there is no pool variant at all.
        LayerSet set = LayerSet.Compose(Vanilla(), new[] { LayerFile.ReadInputs(PlainLayer) },
            new[] { Answers("Other.Mod", (AnswerSet.BaseVariant, "other")) }, _ => { });
        Assert.Null(set.Composed!.Resolve(PoolVariant));
    }

    [Fact]
    public void WritingThePoolVariantMeasuresItAgainstVanillasStem()
    {
        var liveRows = new Dictionary<string, ulong> { ["obj|(O)1"] = 1, ["mixedseed|(O)x||0"] = 5 };
        LayerAnswers written = LayerDelta.Write(
            Cornucopia, Vanilla(), new[] { LayerFile.ReadInputs(Layer) }, Array.Empty<LayerAnswers>(),
            PoolVariant, Variant("pool"), liveRows, _ => "1.0.0", existing: null);
        Assert.Equal(new[] { PoolVariant }, written.Deltas.Keys);
        Assert.Equal(new[] { "mixedseed|(O)x||0" }, written.VariantRowHashes[PoolVariant].Keys);
    }
}
