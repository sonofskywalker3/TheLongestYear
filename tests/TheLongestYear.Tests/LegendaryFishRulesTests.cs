using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff's ruling, 2026-09-04 (Nexus bug 1127469, gazumbrado: "2 silver Mutant Carps",
/// "2 glacier fish for Winter Star", "2 legend, 2 angler, 2 crimson fish"): a legendary fish is
/// asked for ONCE, at base quality, and a bundle holds at most one on Easy/Normal, two from two
/// different seasons on Hard, four (one per season) on Extreme.</summary>
public class LegendaryFishRulesTests
{
    private const string Legend = "(O)163";
    private const string Crimsonfish = "(O)159";
    private const string Angler = "(O)160";
    private const string Glacierfish = "(O)775";
    private const string MutantCarp = "(O)682";

    private static PoolItem Item(string id, int weight = 1, Season[]? seasons = null, string[]? locations = null)
        => new(id, 500, weight, seasons ?? Array.Empty<Season>(), locations ?? new[] { "Town" });

    /// <summary>The five legendaries exactly as PoolAdditions rows them, at a crushing weight so
    /// the raw roll is all but certain to over-pick them.</summary>
    private static readonly PoolItem[] Legendaries =
    {
        Item(Legend, 1000, new[] { Season.Spring }),
        Item(Crimsonfish, 1000, new[] { Season.Summer }),
        Item(Angler, 1000, new[] { Season.Fall }),
        Item(Glacierfish, 1000, new[] { Season.Winter }),
        Item(MutantCarp, 1000, Array.Empty<Season>()),
    };

    private static PoolItem[] Fillers(int count)
        => Enumerable.Range(0, count).Select(i => Item($"(O)f{i}", 1)).ToArray();

    private static BundleSpec FishSpec(int slots)
        => new("Fish Tank", 0, "River Fish", "River Fish", "O 685 30", 0, slots,
            // Vanilla ids outside the pool: a re-drawn vanilla id keeps vanilla's plain ask, which
            // would mask the quality assertion below.
            Enumerable.Range(0, slots).Select(i => new BundleSlotSpec($"(O)v{i}", 1, 0)).ToList());

    private static ItemAvailabilityModel Model(DifficultyStep step)
        => new(new Dictionary<string, ItemAvailability>(), mode: WeekModes.For(step), step: step);

    private static DifficultyProfile Profile(DifficultyStep stack, DifficultyStep quality = DifficultyStep.Normal)
        => DifficultyResolver.Resolve(new DifficultySettings { StackSize = stack, QualityAsks = quality }, new GameplayConfig());

    private static List<string> LegendariesIn(BundleSpec spec)
        => spec.Slots.Where(s => LegendaryFishRules.IsLegendary(s.ItemId)).Select(s => s.ItemId).ToList();

    [Theory]
    [InlineData(DifficultyStep.Easy, 1)]
    [InlineData(DifficultyStep.Normal, 1)]
    [InlineData(DifficultyStep.Hard, 2)]
    [InlineData(DifficultyStep.Extreme, 4)]
    public void The_per_bundle_cap_by_step(DifficultyStep step, int expected)
        => Assert.Equal(expected, LegendaryFishRules.MaxPerBundle(step));

    [Theory]
    [InlineData(DifficultyStep.Easy)]
    [InlineData(DifficultyStep.Normal)]
    [InlineData(DifficultyStep.Hard)]
    [InlineData(DifficultyStep.Extreme)]
    public void A_fish_bundle_never_holds_more_legendaries_than_the_step_allows(DifficultyStep step)
    {
        var pools = new ItemPools { Fish = Legendaries.Concat(Fillers(8)).ToList() };
        for (int seed = 0; seed < 40; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(FishSpec(5), new DomainMatch(PoolDomain.Fish, null), pools,
                new BundleGenerationTuning(), new Random(seed), availability: Model(step));
            Assert.NotSame(FishSpec(5), filled);
            List<string> found = LegendariesIn(filled);
            Assert.True(found.Count <= LegendaryFishRules.MaxPerBundle(step),
                $"seed {seed} on {step}: {found.Count} legendaries ({string.Join(", ", found)})");
            Assert.Equal(5, filled.Slots.Select(s => s.ItemId).Distinct().Count());
        }
    }

    [Theory]
    [InlineData(DifficultyStep.Hard)]
    [InlineData(DifficultyStep.Extreme)]
    public void No_two_legendaries_share_a_season_even_under_the_cap(DifficultyStep step)
    {
        // The vanilla five all sit in different seasons, so this can only bite through a pool
        // that rows two of them into one season (a season pin, a content pack). Angler is given
        // Spring here to make that happen.
        PoolItem legend = Item(Legend, 1000, new[] { Season.Spring });
        PoolItem springAngler = Item(Angler, 1000, new[] { Season.Spring });
        PoolItem filler = Item("(O)f0");
        var candidates = new List<PoolItem> { legend, springAngler, filler };
        var chosen = new List<PoolItem> { legend, springAngler };

        LegendaryFishRules.Enforce(chosen, candidates, step, new Random(1));

        Assert.Equal(new[] { Legend, "(O)f0" }, chosen.Select(c => c.ItemId));
    }

    [Fact]
    public void Mutant_carp_has_no_season_so_it_never_collides_but_still_counts()
    {
        PoolItem carp = Item(MutantCarp, 1000, Array.Empty<Season>());
        PoolItem legend = Item(Legend, 1000, new[] { Season.Spring });
        PoolItem crimson = Item(Crimsonfish, 1000, new[] { Season.Summer });
        PoolItem filler = Item("(O)f0");
        var candidates = new List<PoolItem> { carp, legend, crimson, filler };

        var hard = new List<PoolItem> { carp, legend, crimson };
        LegendaryFishRules.Enforce(hard, candidates, DifficultyStep.Hard, new Random(1));
        Assert.Equal(new[] { MutantCarp, Legend, "(O)f0" }, hard.Select(c => c.ItemId));

        var extreme = new List<PoolItem> { carp, legend, crimson };
        LegendaryFishRules.Enforce(extreme, candidates, DifficultyStep.Extreme, new Random(1));
        Assert.Equal(new[] { MutantCarp, Legend, Crimsonfish }, extreme.Select(c => c.ItemId));
    }

    [Fact]
    public void With_nothing_left_to_swap_in_the_surplus_legendary_is_dropped()
    {
        PoolItem legend = Item(Legend, 1000, new[] { Season.Spring });
        PoolItem crimson = Item(Crimsonfish, 1000, new[] { Season.Summer });
        var chosen = new List<PoolItem> { legend, crimson };

        LegendaryFishRules.Enforce(chosen, new List<PoolItem> { legend, crimson }, DifficultyStep.Normal, new Random(1));

        Assert.Equal(new[] { Legend }, chosen.Select(c => c.ItemId));
    }

    [Fact]
    public void A_legendary_slot_is_always_one_at_base_quality_whatever_the_fish_domain_rolls()
    {
        var tuning = new BundleGenerationTuning { GoldQualityChance = 1.0, SilverQualityChance = 1.0 };
        var pools = new ItemPools
        {
            Fish = Legendaries.Concat(Fillers(8)).ToList(),
            QualityEligibleIds = new HashSet<string>(Legendaries.Select(l => l.ItemId).Concat(Fillers(8).Select(f => f.ItemId)), StringComparer.Ordinal),
        };
        for (int seed = 0; seed < 40; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(FishSpec(5), new DomainMatch(PoolDomain.Fish, null), pools,
                tuning, new Random(seed), availability: Model(DifficultyStep.Extreme));
            foreach (BundleSlotSpec slot in filled.Slots.Where(s => LegendaryFishRules.IsLegendary(s.ItemId)))
            {
                Assert.Equal(1, slot.Stack);
                Assert.Equal(0, slot.Quality);
            }
            // The rule is about legendaries only: a filler still takes the gold ask the tuning forces.
            Assert.Contains(filled.Slots, s => !LegendaryFishRules.IsLegendary(s.ItemId) && s.Quality == 2);
        }
    }

    [Theory]
    [InlineData(DifficultyStep.Hard)]
    [InlineData(DifficultyStep.Extreme)]
    public void The_stack_modifier_leaves_a_legendary_at_one(DifficultyStep step)
    {
        var spec = new BundleSpec("Fish Tank", 1, "Lake Fish", "Lake Fish", "O 685 30", 0, 2,
            new List<BundleSlotSpec> { new(MutantCarp, 1, 0), new("(O)f0", 1, 0) });

        BundleSpec scaled = StackScaling.Apply(spec, Profile(step));

        Assert.Equal(1, scaled.Slots[0].Stack);
        Assert.True(scaled.Slots[1].Stack > 1, "the filler must still scale, or the dial is off");
    }

    [Fact]
    public void The_vanilla_board_pass_leaves_a_legendary_at_one_and_plain()
    {
        var data = new Dictionary<string, string>
        {
            ["Fish Tank/6"] = "Lake Fish/O 685 30/682 1 0 f0 1 0/6/2//Lake Fish",
        };
        var tuning = new BundleGenerationTuning { GoldQualityChance = 1.0, SilverQualityChance = 1.0 };
        var eligible = new HashSet<string>(StringComparer.Ordinal) { MutantCarp, "(O)f0" };

        IDictionary<string, string> result = VanillaBoardDifficultyPass.Apply(
            data, Profile(DifficultyStep.Extreme, DifficultyStep.Extreme), tuning, seed: 7, eligible);

        string ingredients = result["Fish Tank/6"].Split('/')[2];
        Assert.StartsWith("682 1 0 ", ingredients);
        Assert.NotEqual("682 1 0 f0 1 0", ingredients);   // the filler still moved
    }

    [Fact]
    public void An_authored_fish_bundle_holds_at_most_one_legendary_on_normal()
    {
        var def = new AuthoredBundleDef("Weatherman's", "Fish Tank", "O 681 2", 6,
            AuthoredSlotSource.Fish, 5, 4, new List<string>());
        var pools = new ItemPools { Fish = Legendaries.Concat(Fillers(8)).ToList() };
        for (int seed = 0; seed < 40; seed++)
        {
            BundleSpec? composed = AuthoredBundleComposer.Compose(def, 0, pools, new BundleGenerationTuning(), true, new Random(seed));
            Assert.NotNull(composed);
            Assert.True(LegendariesIn(composed!).Count <= 1, $"seed {seed}");
        }
    }

    [Fact]
    public void The_board_allowance_is_none_on_easy_a_quarter_of_boards_on_normal_two_on_hard_three_on_extreme()
    {
        Assert.Equal(0, LegendaryFishRules.BoardAllowance(DifficultyStep.Easy, new Random(1)));
        int normalBoards = Enumerable.Range(0, 2000).Count(seed => LegendaryFishRules.BoardAllowance(DifficultyStep.Normal, new Random(seed)) == 1);
        Assert.InRange(normalBoards, 400, 600);   // about one board in four
        Assert.All(Enumerable.Range(0, 50), seed => Assert.True(LegendaryFishRules.BoardAllowance(DifficultyStep.Normal, new Random(seed)) <= 1));
        Assert.Equal(2, LegendaryFishRules.BoardAllowance(DifficultyStep.Hard, new Random(1)));
        Assert.Equal(3, LegendaryFishRules.BoardAllowance(DifficultyStep.Extreme, new Random(1)));
    }

    [Fact]
    public void A_banned_id_never_reaches_a_slot_even_through_the_hard_item_swap()
    {
        // Legendaries at crushing weight AND as the only hard-effort items: without the ban both the
        // raw roll and the hard-item swap would put one in.
        var pools = new ItemPools { Fish = Legendaries.Concat(Fillers(8)).ToList() };
        var derived = new Dictionary<string, ItemAvailability>();
        foreach (PoolItem l in Legendaries) derived[l.ItemId] = new(Season.Spring, 9, "test", EarliestWeek: 4, HardWeek: 4);
        foreach (PoolItem f in Fillers(8)) derived[f.ItemId] = new(Season.Spring, 1, "test", EarliestWeek: 1, HardWeek: 1);
        var model = new ItemAvailabilityModel(derived, mode: WeekModes.For(DifficultyStep.Normal), step: DifficultyStep.Normal);
        for (int seed = 0; seed < 40; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(FishSpec(5), new DomainMatch(PoolDomain.Fish, null), pools,
                new BundleGenerationTuning(), new Random(seed), availability: model, banned: LegendaryFishRules.Ids);
            Assert.Empty(LegendariesIn(filled));
            Assert.Equal(5, filled.Slots.Count);
        }
        var def = new AuthoredBundleDef("Weatherman's", "Fish Tank", "O 681 2", 6, AuthoredSlotSource.Fish, 5, 4, new List<string>());
        BundleSpec? composed = AuthoredBundleComposer.Compose(def, 0, pools, new BundleGenerationTuning(), true, new Random(3), banned: LegendaryFishRules.Ids);
        Assert.NotNull(composed);
        Assert.Empty(LegendariesIn(composed!));
    }

    [Fact]
    public void A_bundle_never_takes_more_legendaries_than_the_board_has_left()
    {
        PoolItem legend = Item(Legend, 1000, new[] { Season.Spring });
        PoolItem crimson = Item(Crimsonfish, 1000, new[] { Season.Summer });
        PoolItem filler = Item("(O)f0");
        var chosen = new List<PoolItem> { legend, crimson };
        LegendaryFishRules.Enforce(chosen, new List<PoolItem> { legend, crimson, filler }, DifficultyStep.Extreme, new Random(1), boardBudget: 1);
        Assert.Equal(new[] { Legend, "(O)f0" }, chosen.Select(c => c.ItemId));
    }
}
