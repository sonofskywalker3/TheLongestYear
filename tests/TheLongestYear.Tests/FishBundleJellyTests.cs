using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Jeff, 2026-10-05: jellies are an ingredient, not a fish. No fish bundle asks for Sea,
/// River or Cave Jelly (or a modded jelly); they stay in the fish pool for everything else.</summary>
public class FishBundleJellyTests
{
    private const string FishNonFish = "fish_nonfish";
    private const string CountsAsFishCatch = "counts_as_fish_catch";

    private static PoolItem Fish(string id, int category, params string[] locations)
        => new(id, 50, 3, Array.Empty<Season>(), locations, category);

    private static BundleSpec Bundle(string name, int slots, params string[] originals)
        => new("Fish Tank", 7, name, name, "O 1 1", 0, slots,
            originals.Select(o => new BundleSlotSpec(o, 1, 0)).ToList());

    private static string Row(string name, int difficulty, string spans)
        => $"{name}/{difficulty}/mixed/12/30/{spans}/spring summer fall winter/both/690 .4 1 .1/5/1/1/0";

    // ---- Jellies --------------------------------------------------------------------------------

    [Fact]
    public void Builder_marks_jellies_by_their_context_tags_and_keeps_them_in_the_fish_pool()
    {
        RawObjectEntry Obj(params string[] tags) => new("Fish", 0, 50, false, tags);
        var objects = new Dictionary<string, RawObjectEntry>
        {
            ["SeaJelly"] = Obj(FishNonFish, CountsAsFishCatch),
            ["Author.Mod_Glowfin"] = Obj(FishNonFish, CountsAsFishCatch),   // a modded jelly
            ["152"] = Obj(FishNonFish),                                      // Seaweed: not a jelly
            ["131"] = new("Fish", -4, 30, false, new[] { "fish_ocean" }),   // Sardine
        };
        var spawns = objects.Keys.Select(id => new RawSpawnEntry(id, null, null, "Beach")).ToList();

        ItemPools pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects, new List<RawSpawnEntry>(), spawns,
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            new BundleGenerationTuning());

        Assert.Equal(new[] { "(O)Author.Mod_Glowfin", "(O)SeaJelly" }, pools.JellyIds.OrderBy(i => i, StringComparer.Ordinal));
        Assert.Contains(pools.Fish, p => p.ItemId == "(O)SeaJelly");
    }

    private static ItemPools JellyPools() => new()
    {
        Fish = new[]
        {
            Fish("(O)SeaJelly", 0, "Beach", "Submarine"),
            Fish("(O)RiverJelly", 0, "Forest", "Mountain", "Town"),
            Fish("(O)CaveJelly", 0, "UndergroundMine"),
            Fish("(O)Author.Mod_Glowfin", 0, "Beach", "Mountain", "UndergroundMine"),
            Fish("(O)131", -4, "Beach"), Fish("(O)130", -4, "Beach"), Fish("(O)150", -4, "Beach"),
            Fish("(O)701", -4, "Beach"), Fish("(O)147", -4, "Beach"),
            Fish("(O)136", -4, "Mountain"), Fish("(O)700", -4, "Mountain"), Fish("(O)698", -4, "Mountain"),
            Fish("(O)142", -4, "Mountain"), Fish("(O)702", -4, "Mountain"),
            Fish("(O)156", -4, "UndergroundMine"), Fish("(O)158", -4, "UndergroundMine"),
            Fish("(O)164", -4, "Desert"), Fish("(O)165", -4, "Desert"), Fish("(O)734", -4, "Woods"),
        },
        JellyIds = new HashSet<string>(StringComparer.Ordinal) { "(O)Author.Mod_Glowfin" },
    };

    private static readonly string[] Jellies = { "(O)SeaJelly", "(O)RiverJelly", "(O)CaveJelly", "(O)Author.Mod_Glowfin" };

    [Theory]
    [InlineData("Ocean Fish", "131", "130", "150", "701")]
    [InlineData("Lake Fish", "136", "700", "698", "142")]
    [InlineData("Specialty Fish", "156", "164", "734", "158")]
    [InlineData("Quality Fish", "136", "131", "156", "164")]
    public void No_fish_bundle_asks_for_a_jelly(string name, params string[] originals)
    {
        ItemPools pools = JellyPools();
        BundleSpec spec = Bundle(name, 4, originals);
        for (int seed = 0; seed < 40; seed++)
        {
            BundleSpec filled = BundleSlotFiller.Fill(spec, new DomainMatch(PoolDomain.Fish, null), pools,
                new BundleGenerationTuning(), new Random(seed));
            Assert.NotSame(spec, filled);
            Assert.DoesNotContain(filled.Slots, s => Jellies.Contains(s.ItemId));
        }
    }

    [Fact]
    public void Weathermans_never_asks_for_a_jelly()
    {
        ItemPools pools = JellyPools();
        var def = new AuthoredBundleDef("Weatherman's", "Fish Tank", "O 681 2", 6, AuthoredSlotSource.Fish, 5, 4, new List<string>());
        for (int seed = 0; seed < 40; seed++)
        {
            BundleSpec? composed = AuthoredBundleComposer.Compose(def, 0, pools, new BundleGenerationTuning(), true, new Random(seed));
            Assert.NotNull(composed);
            Assert.DoesNotContain(composed!.Slots, s => Jellies.Contains(s.ItemId));
        }
    }
}
