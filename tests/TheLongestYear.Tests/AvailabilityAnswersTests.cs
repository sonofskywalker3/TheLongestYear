using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The availability model served from stored answers (spec 2026-10-27-path-lexicon,
/// Task 6b) answers exactly as the one <see cref="ItemAvailabilityBuilder.Build"/> makes, after the
/// answers have been through the lexicon file format.</summary>
public sealed class AvailabilityAnswersTests
{
    private const string Fish = "(O)128";
    private const string Metal = "(O)384";
    private const string TrapFish = "(O)372";
    private const string BatWing = "(O)767";
    private const string ArtichokeDip = "(O)605";
    private const string Milk = "(O)184";
    private const string Modded = "(O)999";
    private static readonly Func<string, bool> NoUpgrades = _ => false;

    private static RawObjectEntry Obj(int category, string name) => new("Basic", category, 50, false, new string[0], name);

    /// <summary>A fish with its Data/Fish row, a metal and a trap fish (the EffortTiersAndComposerTests
    /// and ItemAvailabilityTests shapes), so the season rules, the trap rewrite and the composer all run.</summary>
    private static ItemPools Pools() => new()
    {
        Fish = new List<PoolItem> { new(Fish, 100, 1, new List<Season> { Season.Summer }, new List<string>()) },
        Metals = new List<PoolItem> { new(Metal, 100, 1, new List<Season>(), new List<string>()) },
        TrapFishIds = new HashSet<string>(StringComparer.Ordinal) { TrapFish },
        FishRows = new Dictionary<string, RawFishEntry> { ["128"] = new RawFishEntry("128", false, 80, "1200 1600", "sunny", 5, 0) },
    };

    /// <summary>A monster drop, an Artichoke dish (a year-two crop, so the dish units are filled) and
    /// a flour dish (so the dish table is).</summary>
    private static EffortData Data() => new()
    {
        Objects = new Dictionary<string, RawObjectEntry>
        {
            ["767"] = Obj(-28, "Bat Wing"), ["274"] = Obj(-75, "Artichoke"), ["184"] = Obj(-6, "Milk"),
            ["605"] = Obj(-7, "Artichoke Dip"), ["999"] = Obj(0, "Modded Thing"),
            ["246"] = Obj(-25, "Wheat Flour"), ["9001"] = Obj(-7, "Flat Bread"),
        },
        MonsterDrops = new List<RawMonsterDrop> { new("Bat", BatWing, 0.9) },
        CookingRecipes = new List<RawCookingRecipe>
        {
            new("Artichoke Dip", new[] { YearTwoCrops.Artichoke, Milk }, ArtichokeDip, "default"),
            // The DishAskBasisTests dish: a friendship unlock, so the kitchen moves its effort.
            new("Flat Bread", new[] { "(O)246" }, "(O)9001", "f Emily 3"),
        },
    };

    /// <summary>One accepted pin, one rejected for being earlier than the fish's floor, one week pin.</summary>
    private static readonly IReadOnlyDictionary<string, Season> SeasonPins = new Dictionary<string, Season>
    {
        [Metal] = Season.Fall, [Fish] = Season.Spring, [Modded] = Season.Winter,
    };

    private static readonly IReadOnlyDictionary<string, int> WeekPins = new Dictionary<string, int> { [BatWing] = 9 };

    public static IEnumerable<object[]> Cases()
        => Enum.GetValues<DifficultyStep>().SelectMany(step => new[] { new object[] { step, false }, new object[] { step, true } });

    [Theory]
    [MemberData(nameof(Cases))]
    public void AvailabilityFromAnswersEqualsBuild(DifficultyStep step, bool kitchen)
    {
        WeekMode mode = WeekModes.For(step);
        ItemAvailabilityModel built = ItemAvailabilityBuilder.Build(Pools(), seasonOverrides: SeasonPins, effortData: Data(),
            hasKitchen: kitchen, weekOverrides: WeekPins, mode: mode, step: step, hasUpgrade: NoUpgrades);
        AvailabilityAnswers answers = ItemAvailabilityBuilder.BuildWithAnswers(Pools(), seasonOverrides: SeasonPins, effortData: Data(),
            hasKitchen: kitchen, weekOverrides: WeekPins, mode: mode, step: step, hasUpgrade: NoUpgrades).Answers;

        AvailabilityAnswers read = RoundTrip(AvailabilityAnswers.Key(step, kitchen), answers);
        ItemAvailabilityModel served = ItemAvailabilityBuilder.FromAnswers(read, SeasonPins, WeekPins, mode, step);

        Assert.NotEmpty(read.DishBases);
        Assert.NotEmpty(read.YearTwoCropDishUnits);
        AssertSame(built, served);
    }

    [Fact]
    public void AvailabilityFromAnswersEqualsBuildWithoutEffortData()
    {
        ItemAvailabilityModel built = ItemAvailabilityBuilder.Build(Pools(), seasonOverrides: SeasonPins);
        AvailabilityAnswers answers = ItemAvailabilityBuilder.BuildWithAnswers(Pools(), seasonOverrides: SeasonPins).Answers;
        AssertSame(built, ItemAvailabilityBuilder.FromAnswers(RoundTrip("Normal|no_kitchen", answers), SeasonPins, null, WeekMode.Pacing, DifficultyStep.Normal));
    }

    [Fact]
    public void KeyNamesStepAndKitchen()
    {
        Assert.Equal("Hard|kitchen", AvailabilityAnswers.Key(DifficultyStep.Hard, true));
        Assert.Equal("Easy|no_kitchen", AvailabilityAnswers.Key(DifficultyStep.Easy, false));
    }

    [Fact]
    public void InputsHashFollowsEveryInput()
    {
        string Hash(ItemPools pools, EffortData data, IReadOnlyDictionary<string, Season> pins, IReadOnlyDictionary<string, int> weeks, bool capped)
            => AvailabilityAnswers.InputsHash(pools, data, pins, null, weeks, capped);
        string baseline = Hash(Pools(), Data(), SeasonPins, WeekPins, true);
        Assert.Equal(baseline, Hash(Pools(), Data(), SeasonPins, WeekPins, true));
        Assert.NotEqual(baseline, Hash(Pools() with { TrapFishIds = new HashSet<string>() }, Data(), SeasonPins, WeekPins, true));
        Assert.NotEqual(baseline, Hash(Pools(), new EffortData(), SeasonPins, WeekPins, true));
        Assert.NotEqual(baseline, Hash(Pools(), Data(), new Dictionary<string, Season>(), WeekPins, true));
        Assert.NotEqual(baseline, Hash(Pools(), Data(), SeasonPins, new Dictionary<string, int>(), true));
        Assert.NotEqual(baseline, Hash(Pools(), Data(), SeasonPins, WeekPins, false));
    }

    /// <summary>The shipped file carries the answers for every step and kitchen state in both variants,
    /// each stamped with the inputs it was made from.</summary>
    [Fact]
    public void ShippedFileHasEveryAvailabilityKey()
    {
        string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", "lexicon", "vanilla.json");
        AnswerSet shipped = LexiconJson.Read(System.IO.File.ReadAllText(path));
        var keys = Cases().Select(c => AvailabilityAnswers.Key((DifficultyStep)c[0], (bool)c[1])).OrderBy(k => k, StringComparer.Ordinal).ToList();
        foreach (string name in new[] { AnswerSet.BaseVariant, AnswerSet.PierreVariant })
        {
            AnswerVariant variant = shipped.Resolve(name)!;
            Assert.Equal(keys, variant.Availability.Keys.OrderBy(k => k, StringComparer.Ordinal));
            Assert.All(variant.Availability.Values, a => Assert.False(string.IsNullOrEmpty(a.Inputs)));
            Assert.All(variant.Availability.Values, a => Assert.NotEmpty(a.Derived));
        }
        // Pierre's seeds lift the year-two dish cap: only base lists capped dishes.
        Assert.NotEmpty(shipped.Resolve(AnswerSet.BaseVariant)!.Availability[AvailabilityAnswers.Key(DifficultyStep.Normal, false)].YearTwoCropDishUnits);
        Assert.Empty(shipped.Resolve(AnswerSet.PierreVariant)!.Availability[AvailabilityAnswers.Key(DifficultyStep.Normal, false)].YearTwoCropDishUnits);
    }

    /// <summary>The answers through the answer file: stored on a variant, written and read back.</summary>
    private static AvailabilityAnswers RoundTrip(string key, AvailabilityAnswers answers)
    {
        var empty = new Dictionary<string, IReadOnlyList<ObtainSource>>();
        var variant = new AnswerVariant(empty, empty, Array.Empty<string>(), 1,
            new Dictionary<string, PlaceWeek>(), Array.Empty<string>(), new Dictionary<string, PlaceWeek>())
        {
            Availability = new Dictionary<string, AvailabilityAnswers> { [key] = answers with { Inputs = "abc" } },
        };
        var set = new AnswerSet("1.6.15", "1.1.0", new[] { RowHashes.Vanilla }, new Dictionary<string, ulong>(),
            new Dictionary<string, AnswerVariant> { [AnswerSet.BaseVariant] = variant });
        AvailabilityAnswers read = LexiconJson.Read(LexiconJson.Write(set)).Resolve(AnswerSet.BaseVariant)!.Availability[key];
        Assert.Equal("abc", read.Inputs);
        return read;
    }

    private static void AssertSame(ItemAvailabilityModel expected, ItemAvailabilityModel actual)
    {
        Assert.Equal(expected.Mode, actual.Mode);
        Assert.Equal(expected.Step, actual.Step);
        Assert.Equal(expected.DerivedCount, actual.DerivedCount);
        Assert.Equal(expected.DerivedEffortCount, actual.DerivedEffortCount);
        Assert.Equal(expected.RejectedSeasonOverrides.OrderBy(i => i), actual.RejectedSeasonOverrides.OrderBy(i => i));
        Assert.Equal(expected.KnownIds, actual.KnownIds);
        foreach (string id in expected.KnownIds.Concat(new[] { Modded, "(O)24", "(O)nothing", "(O)9001" }))
        {
            Assert.Equal(expected.IsPlaced(id), actual.IsPlaced(id));
            Assert.Equal(expected.IsDerived(id), actual.IsDerived(id));
            Assert.Equal(expected.HasDerivedEffort(id), actual.HasDerivedEffort(id));
            Assert.Equal(expected.For(id), actual.For(id));
        }
        Assert.Equal(expected.UnknownIds.OrderBy(i => i), actual.UnknownIds.OrderBy(i => i));
        Assert.Equal(expected.DishBases.Keys.OrderBy(i => i), actual.DishBases.Keys.OrderBy(i => i));
        foreach (var (id, bases) in expected.DishBases) Assert.Equal(bases, actual.DishBases[id]);
        Assert.Equal(expected.YearTwoCropDishUnits.OrderBy(p => p.Key), actual.YearTwoCropDishUnits.OrderBy(p => p.Key));
    }
}
