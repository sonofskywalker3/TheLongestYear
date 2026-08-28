using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Night Fishing re-roll (Jeff, 2026-08-28): only fish that are NOT catchable before
/// 6pm anywhere, plus the Night Market's fish, at most one of those per bundle. Flounder
/// (6am to 8pm) was landing in Night Fishing because the bundle's vanilla ingredients span
/// every water, so the habitat rule let the whole ocean in.</summary>
public class NightFishingCandidatesTests
{
    private static string Row(string name, string spans)
        => $"{name}/50/mixed/12/30/{spans}/spring summer fall winter/both/690 .4 1 .1/5/1/1/0";

    private static PoolItem Fish(string id, int category = -4, params string[] locations)
        => new(id, 50, 3, Array.Empty<Season>(), locations, category);

    private static readonly string[] NightMarketIds = { "(O)800", "(O)799", "(O)798", "(O)149" };

    private static ItemPools Pools() => new()
    {
        Fish = new[]
        {
            Fish("(O)132", -4, "Forest", "Town"),           // Bream 1800-2600
            Fish("(O)151", -4, "Beach"),                    // Squid 1800-2600
            Fish("(O)269", -4, "Forest", "Mountain"),       // Midnight Carp 2200-2600
            Fish("(O)155", -4, "Beach", "Submarine"),       // Super Cucumber 1800-2600
            Fish("(O)267", -4, "Beach"),                    // Flounder 600-2000: day fish
            Fish("(O)148", -4, "Beach"),                    // Eel 1600-2600: catchable before 6pm
            Fish("(O)140", -4, "Forest"),                   // Walleye 1200-2600
            Fish("(O)800", -4, "Beach", "Submarine"),       // Blobfish: Night Market
            Fish("(O)799", -4, "Beach", "Submarine"),       // Spook Fish: Night Market
            Fish("(O)798", -4, "Beach", "Submarine"),       // Midnight Squid: Night Market
            Fish("(O)149", -4, "Beach", "Submarine"),       // Octopus: day fish also sold at the market
            Fish("(O)152", 0, "Beach", "Submarine"),        // Seaweed: not a fish (category 0)
        },
        FishRows = new Dictionary<string, RawFishEntry>
        {
            ["132"] = RawFishEntry.Parse("132", Row("Bream", "1800 2600")),
            ["151"] = RawFishEntry.Parse("151", Row("Squid", "1800 2600")),
            ["269"] = RawFishEntry.Parse("269", Row("Midnight Carp", "2200 2600")),
            ["155"] = RawFishEntry.Parse("155", Row("Super Cucumber", "1800 2600")),
            ["267"] = RawFishEntry.Parse("267", Row("Flounder", "600 2000")),
            ["148"] = RawFishEntry.Parse("148", Row("Eel", "1600 2600")),
            ["140"] = RawFishEntry.Parse("140", Row("Walleye", "1200 2600")),
            ["800"] = RawFishEntry.Parse("800", Row("Blobfish", "600 2600")),
            ["799"] = RawFishEntry.Parse("799", Row("Spook Fish", "600 2600")),
            ["798"] = RawFishEntry.Parse("798", Row("Midnight Squid", "600 2600")),
            ["149"] = RawFishEntry.Parse("149", Row("Octopus", "600 1300")),
            ["152"] = RawFishEntry.Parse("152", Row("Seaweed", "600 2600")),
        },
    };

    // The originals do not matter for the night rule; the slot count sets how many fish the fill needs.
    private static BundleSpec NightFishing(int slots) => new("Fish Tank", 9, "Night Fishing", "Night Fishing",
        "O 242 10", 0, slots, Enumerable.Range(0, slots).Select(i => new BundleSlotSpec((900 + i).ToString(), 1, 0)).ToList());

    [Theory]
    [InlineData("1800 2600", true)]
    [InlineData("2200 2600", true)]
    [InlineData("600 2600", false)]
    [InlineData("1600 2600", false)]
    [InlineData("600 1100 1800 2600", false)]   // Albacore: morning window too
    [InlineData("", false)]                      // no window = open all day
    public void RawFishEntry_IsNightOnly_WhenEveryWindowOpensAtOrAfterSixPm(string spans, bool expected)
        => Assert.Equal(expected, RawFishEntry.Parse("1", Row("X", spans)).IsNightOnly());

    [Fact]
    public void NightFishing_OnlyNightFish_AtMostOneNightMarketFish_NeverAlgae()
    {
        var pools = Pools();
        bool marketSeen = false, breamSeen = false;
        for (int seed = 0; seed < 80; seed++)
        {
            var filled = BundleSlotFiller.Fill(NightFishing(3),
                new DomainMatch(PoolDomain.Fish, null), pools, new BundleGenerationTuning(), new Random(seed));
            var ids = filled.Slots.Select(s => s.ItemId).ToList();
            Assert.Equal(3, ids.Count);
            Assert.DoesNotContain(ids, id => id is "(O)267" or "(O)148" or "(O)140" or "(O)152");
            Assert.True(ids.Count(NightMarketIds.Contains) <= 1, string.Join(",", ids));
            marketSeen |= ids.Any(NightMarketIds.Contains);
            breamSeen |= ids.Contains("(O)132");
        }
        Assert.True(marketSeen);
        Assert.True(breamSeen);
    }

    [Fact]
    public void NightFishing_KeepsVanillaSlots_WhenNightFishPlusOneMarketFishCannotFillIt()
    {
        var pools = Pools();
        // Five slots: four true night fish plus one market fish is exactly enough...
        Assert.NotSame(NightFishing(5), BundleSlotFiller.Fill(NightFishing(5),
            new DomainMatch(PoolDomain.Fish, null), pools, new BundleGenerationTuning(), new Random(1)));
        // ...six is not, however many Night Market fish are in the pool.
        var six = NightFishing(6);
        Assert.Same(six, BundleSlotFiller.Fill(six,
            new DomainMatch(PoolDomain.Fish, null), pools, new BundleGenerationTuning(), new Random(1)));
    }

    [Fact]
    public void OtherFishBundles_StillUseHabitat()
    {
        var pools = Pools();
        var ocean = new BundleSpec("Fish Tank", 8, "Ocean Fish", "Ocean Fish", "O 275 3", 0, 3,
            new[] { "267" }.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());
        var filled = BundleSlotFiller.Fill(ocean,
            new DomainMatch(PoolDomain.Fish, null), pools, new BundleGenerationTuning(), new Random(2));
        Assert.All(filled.Slots, s => Assert.Contains("Beach", pools.Fish.Single(p => p.ItemId == s.ItemId).Locations));
    }
}
