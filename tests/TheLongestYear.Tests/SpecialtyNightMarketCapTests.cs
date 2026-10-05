using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-05: Specialty Fish holds at most one Night Market fish, the cap Night
/// Fishing already has. The legendary cap is separate and unchanged.</summary>
public class SpecialtyNightMarketCapTests
{
    private static PoolItem Fish(string id, int category, params string[] locations)
        => new(id, 50, 3, Array.Empty<Season>(), locations, category);

    private static BundleSpec Bundle(string name, int slots, params string[] originals)
        => new("Fish Tank", 7, name, name, "O 1 1", 0, slots,
            originals.Select(o => new BundleSlotSpec(o, 1, 0)).ToList());

    private static string Row(string name, int difficulty, string spans)
        => $"{name}/{difficulty}/mixed/12/30/{spans}/spring summer fall winter/both/690 .4 1 .1/5/1/1/0";

    private static ItemPools MarketPools(int ordinarySpecialties) => new()
    {
        Fish = new[]
            {
                Fish("(O)798", -4, "Submarine"), Fish("(O)799", -4, "Submarine"),
                Fish("(O)800", -4, "Submarine"), Fish("(O)149", -4, "Beach", "Submarine"),
            }
            .Concat(new[] { "(O)156", "(O)158", "(O)164", "(O)165", "(O)734" }
                .Take(ordinarySpecialties).Select(id => Fish(id, -4, "UndergroundMine")))
            .ToList(),
        FishRows = new Dictionary<string, RawFishEntry>
        {
            ["798"] = RawFishEntry.Parse("798", Row("Midnight Squid", 55, "600 2600")),
            ["799"] = RawFishEntry.Parse("799", Row("Spook Fish", 60, "600 2600")),
            ["800"] = RawFishEntry.Parse("800", Row("Blobfish", 75, "600 2600")),
            ["149"] = RawFishEntry.Parse("149", Row("Octopus", 95, "600 1300")),
        },
    };

    private static readonly string[] MarketIds = { "(O)798", "(O)799", "(O)800", "(O)149" };

    [Fact]
    public void Specialty_fish_holds_at_most_one_night_market_fish()
    {
        ItemPools pools = MarketPools(ordinarySpecialties: 5);
        BundleSpec spec = Bundle("Specialty Fish", 4, "128", "156", "164", "734");
        bool marketSeen = false;
        for (int seed = 0; seed < 60; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Fish, null), pools,
                new BundleGenerationTuning(), new Random(seed));
            var ids = filled.Slots.Select(s => s.ItemId).ToList();
            Assert.Equal(4, ids.Count);
            Assert.True(ids.Count(MarketIds.Contains) <= 1, string.Join(",", ids));
            marketSeen |= ids.Any(MarketIds.Contains);
        }
        Assert.True(marketSeen);
    }

    [Fact]
    public void Specialty_fish_counts_only_one_market_fish_towards_filling_its_slots()
    {
        ItemPools pools = MarketPools(ordinarySpecialties: 2);
        BundleSpec spec = Bundle("Specialty Fish", 4, "128", "156", "164", "734");
        Assert.Equal(3, BundleSlotFiller.CandidateCount(spec, new DomainMatch(PoolDomain.Fish, null), pools));
    }
}
