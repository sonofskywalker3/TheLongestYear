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

    private const double SpringBags = 763 * 0.025;
    /// <summary>Fall's share: Artichoke Seeds keep their quarter.</summary>
    private const double FallShare = 0.75 / 7;
    /// <summary>Bags that turn up before Fall in this pool, where the only Summer crop is none: Summer 1,
    /// the Railroad, Krobus twice and the green rain.</summary>
    private static readonly double SummerBags = (15 + 45) * 0.025 + 20 + 1443 * 0.025 * 0.9;
    /// <summary>What Fall's own bags yield of Zucchini: Fall 1's weeds get 7 harvests, Krobus' bags of
    /// Fall 4 get 6, of Fall 11 get 2, and the later ones none.</summary>
    private static readonly double FallOwnZucchini = FallShare * (MixedSeedSupply.FallStartWeeds * MixedSeedSupply.BagsPerWeed * 7 + 10 * 6 + 10 * 2);

    private static double Basis(string crop, string combination, Season season)
        => MixedSeedSupply.BoardBases(Inputs())[crop][combination][(int)season];

    [Fact]
    public void ABoardThatAsksOnlyInSpringGetsAllOfSpringsBags()
    {
        // 19.075 bags, 1 in 7 of them Asparagus Seeds, one harvest of one each.
        Assert.Equal(SpringBags / 7, Basis(Asparagus, "Spring", Season.Spring), 6);
        Assert.False(MixedSeedSupply.BoardBases(Inputs())[Asparagus].ContainsKey("Fall"));
    }

    [Fact]
    public void ABoardThatAsksOnlyInFallGetsEverySavedBagOnFallsFirstDay()
    {
        // This pool has no Summer crop, so every bag from before Fall is planted on Fall 1: 7 harvests.
        double expected = FallShare * (SpringBags + SummerBags) * 7 + FallOwnZucchini;
        Assert.Equal(expected, Basis(Zucchini, "Fall", Season.Fall), 6);
    }

    [Fact]
    public void ABagWantedInTwoSeasonsIsSharedBetweenThem()
    {
        // Spring's bags are halved between Spring and Fall; Summer's are wanted only in Fall.
        Assert.Equal(SpringBags / 2 / 7, Basis(Asparagus, "Spring+Fall", Season.Spring), 6);
        double fall = FallShare * (SpringBags / 2 + SummerBags) * 7 + FallOwnZucchini;
        Assert.Equal(fall, Basis(Zucchini, "Spring+Fall", Season.Fall), 6);
        // No bag is counted twice: what Spring gives up is exactly what Fall gains.
        double springLoss = SpringBags / 2;
        double fallGain = (Basis(Zucchini, "Fall", Season.Fall) - fall) / (FallShare * 7);
        Assert.Equal(springLoss, fallGain, 6);
    }

    [Fact]
    public void ABagTooLateForItsOwnSeasonGoesOnWhole()
    {
        // With a Summer crop of 10 days, the green rain (Summer 23) and Krobus on Summer 25 cannot be
        // harvested in Summer: they are Fall's whole, even on a board that asks in Summer too.
        MixedSeedInputs inputs = Inputs();
        var pool = inputs.Pool.ToList();
        pool.Insert(0, new MixedSeedRow("(O)ChickSeeds", In("summer"), 1));
        var crops = inputs.Crops.ToList();
        crops.Add(new MixedSeedCrop("(O)ChickSeeds", "(O)Chick", new[] { Season.Summer }, 10, 5, 2, OnlyRoute: true));
        var bases = MixedSeedSupply.BoardBases(new MixedSeedInputs(pool, crops));
        double late = 1443 * 0.025 * 0.9 + 10;
        double early = (15 + 45) * 0.025 + 10;   // Summer 1, the Railroad, Krobus on Summer 18
        double fall = FallShare * (SpringBags / 2 + early / 2 + late) * 7 + FallOwnZucchini;
        Assert.Equal(fall, bases[Zucchini]["Summer+Fall"][(int)Season.Fall], 6);
        // Summer alone: Spring's bags from Summer 1 (4 harvests of 2), the early bags the day they come.
        double summerShare = 1.0 / 2;   // one Summer entry and the vanilla slot
        double summer = summerShare * 2 * (SpringBags * 4 + 15 * 0.025 * 4 + 45 * 0.025 * 4 + 10 * 1);
        Assert.Equal(summer, bases["(O)Chick"]["Summer"][(int)Season.Summer], 6);
    }

    [Fact]
    public void AnAskOutsideABoardReadsTheEverySeasonBasis()
    {
        IReadOnlyDictionary<string, double[]> bases = MixedSeedSupply.Bases(Inputs());
        Assert.Equal(new[] { Asparagus, Zucchini }, bases.Keys.OrderBy(k => k, StringComparer.Ordinal));
        // This pool has crops in Spring and Fall only, so those are the seasons that share a bag.
        Assert.Equal("Spring+Fall", MixedSeedSupply.EverySeason(Inputs()));
        Assert.Equal(Basis(Asparagus, "Spring+Fall", Season.Spring), bases[Asparagus][(int)Season.Spring], 6);
        Assert.Empty(MixedSeedSupply.Bases(null));
    }

    // ---- The board pass ----

    private static ItemAvailabilityModel Model()
    {
        return new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>(StringComparer.Ordinal))
            { MixedSeedBoardBases = MixedSeedSupply.BoardBases(Inputs()), MixedSeedCropBases = MixedSeedSupply.Bases(Inputs()) };
    }

    private static BundleSpec Bundle(int index, params (string Id, int Stack)[] slots)
        => new("Pantry", index, "B" + index, "B" + index, "O 495 30", 0, slots.Length,
            slots.Select(s => new BundleSlotSpec(s.Id, s.Stack, 0)).ToList());

    private static DifficultyProfile Profile(DifficultyStep step)
        => DifficultyResolver.Resolve(new DifficultySettings { StackSize = step }, new GameplayConfig());

    [Fact]
    public void TheBoardsSeasonsAreReadOffItsSlots()
    {
        ItemAvailabilityModel model = Model();
        Assert.Empty(MixedSeedBoardPass.AskedSeasons(new[] { Bundle(1, ("(O)24", 5)) }, model));
        Assert.Equal(new[] { Season.Fall }, MixedSeedBoardPass.AskedSeasons(new[] { Bundle(1, (Zucchini, 1)) }, model));
        Assert.Equal(new[] { Season.Spring, Season.Fall },
            MixedSeedBoardPass.AskedSeasons(new[] { Bundle(1, (Zucchini, 1)), Bundle(2, (Asparagus, 1)) }, model));
        // An Artichoke grows from the same bags: it makes Fall an asked season beside a pool crop,
        Assert.Equal(new[] { Season.Spring, Season.Fall },
            MixedSeedBoardPass.AskedSeasons(new[] { Bundle(1, (Asparagus, 1), (YearTwoCrops.Artichoke, 2)) }, model));
        // and alone it is not this pass's business.
        Assert.Empty(MixedSeedBoardPass.AskedSeasons(new[] { Bundle(1, (YearTwoCrops.Artichoke, 2)) }, model));
    }

    [Fact]
    public void ALoneFallCropIsSizedFromEverySavedBag()
    {
        ItemAvailabilityModel model = Model();
        double basis = Basis(Zucchini, "Fall", Season.Fall);
        var board = new[] { Bundle(1, ("(O)24", 5), (Zucchini, 1)) };
        IReadOnlyList<BundleSpec> sized = MixedSeedBoardPass.Apply(board, model, Profile(DifficultyStep.Normal), seed: 42);
        int whole = (int)Math.Round(basis, MidpointRounding.AwayFromZero);
        Assert.InRange(sized[0].Slots[1].Stack, (int)Math.Ceiling(whole * 0.20), (int)Math.Ceiling(whole * 0.50));
        Assert.Equal(5, sized[0].Slots[0].Stack);
        // The same board and seed give the same ask.
        Assert.Equal(sized[0].Slots[1].Stack, MixedSeedBoardPass.Apply(board, model, Profile(DifficultyStep.Normal), seed: 42)[0].Slots[1].Stack);
    }

    [Fact]
    public void AnArtichokeOnTheBoardHalvesWhatSpringKeeps()
    {
        ItemAvailabilityModel model = Model();
        var board = new[] { Bundle(1, (Asparagus, 9), (YearTwoCrops.Artichoke, 3)) };
        IReadOnlyList<BundleSpec> sized = MixedSeedBoardPass.Apply(board, model, Profile(DifficultyStep.Extreme), seed: 7);
        // Spring keeps half its bags: about 1.4 Asparagus, so even Extreme asks one.
        Assert.Equal(1, sized[0].Slots[0].Stack);
        Assert.Equal(3, sized[0].Slots[1].Stack);
    }

    [Fact]
    public void ABoardWithoutSuchCropsOrAModelWithoutAPoolIsLeftAsItIs()
    {
        var board = new[] { Bundle(1, ("(O)24", 5)) };
        Assert.Same(board, MixedSeedBoardPass.Apply(board, Model(), Profile(DifficultyStep.Normal), seed: 1));
        var poolBoard = new[] { Bundle(1, (Zucchini, 3)) };
        var noPool = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>(StringComparer.Ordinal));
        Assert.Same(poolBoard, MixedSeedBoardPass.Apply(poolBoard, noPool, Profile(DifficultyStep.Normal), seed: 1));
        Assert.Same(poolBoard, MixedSeedBoardPass.Apply(poolBoard, null, Profile(DifficultyStep.Normal), seed: 1));
    }

    [Fact]
    public void TheAskIsTheDifficultysBandOfTheBasis()
    {
        var model = new ItemAvailabilityModel(new Dictionary<string, ItemAvailability>(StringComparer.Ordinal))
            { MixedSeedCropBases = MixedSeedSupply.Bases(Inputs()) };
        Assert.True(QuantityAskPass.Covers(Asparagus, model));
        Assert.False(QuantityAskPass.Covers(Asparagus));
        // Outside a board Spring keeps half its bags here (this pool has no Summer crop): about 1.4.
        Assert.Equal(SpringBags / 2 / 7, QuantityAskPass.BasisByDeadline(Asparagus, Season.Spring, model)!.Value, 6);
        double basis = Basis(Asparagus, "Spring", Season.Spring);
        // A board that asks only in Spring: about 2.7 Asparagus. Easy asks 1, Normal 1 or 2, Hard 2, Extreme 2 or 3.
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
        Assert.Equal(SpringBags / 2 / 7, back.MixedSeedCropBases![Asparagus][0], 6);
        Assert.DoesNotContain("MixedSeedBoardBases", System.Text.Json.JsonSerializer.Serialize(answers, LexiconJson.Options));
        string board = System.Text.Json.JsonSerializer.Serialize(answers with { MixedSeedBoardBases = MixedSeedSupply.BoardBases(Inputs()) }, LexiconJson.Options);
        AvailabilityAnswers boardBack = System.Text.Json.JsonSerializer.Deserialize<AvailabilityAnswers>(board, LexiconJson.Options)!;
        Assert.Equal(SpringBags / 7, boardBack.MixedSeedBoardBases![Asparagus]["Spring"][0], 6);
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
