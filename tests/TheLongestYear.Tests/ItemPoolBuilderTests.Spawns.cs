using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public partial class ItemPoolBuilderTests
{
    /// <summary>Jeff, 2026-08-28: Midnight Squid, Spook Fish and Blobfish "should be valid
    /// options" for Night Fishing. They carry ExcludeFromRandomSale in Data/Objects (the game keeps
    /// them out of random shop stock), and the vet dropped them with everything else so flagged.
    /// A fish with a Night Market (Submarine) spawn row is a market fish: it stays in the pool,
    /// and only its festival rows count, because its Beach rows are gated in code, not data, and
    /// would otherwise read as an all-year beach catch.</summary>
    [Fact]
    public void Fish_NightMarketFish_KeptDespiteExcludeFromRandomSale_SeasonWinter_SubmarineOnly()
    {
        var pools = Build(
            fish: new[]
            {
                new RawSpawnEntry("(O)800", null, null, "Beach"),
                new RawSpawnEntry("(O)800", null, null, "Submarine"),
                new RawSpawnEntry("(O)898", null, "PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", "Beach"),
            },
            objects: Objects(
                ("800", Obj(category: -4, type: "Fish", excludeFromRandomSale: true)),   // Blobfish
                ("898", Obj(category: -4, type: "Fish", excludeFromRandomSale: true))));  // Son of Crimsonfish: no market row, stays out
        var blob = Assert.Single(pools.Fish);
        Assert.Equal("(O)800", blob.ItemId);
        Assert.Equal(new[] { Season.Winter }, blob.Seasons);
        Assert.Equal(new[] { "Submarine" }, blob.Locations);
        Assert.Equal(Season.Winter, pools.DerivedSeasonPins["(O)800"]);
    }

    /// <summary>Nexus bug, spenderg, 2026-08-30: a river fish bundle asked for Ms. Angler, which
    /// can only be caught while Mr. Qi's Extended Family order is running - post-Community-Center
    /// content a one-year loop never reaches. Data/Objects gives all five Extended Family fish
    /// ExcludeFromRandomSale=false (unlike the vanilla legendaries), and their spawn maps are the
    /// ordinary Town/Beach/Mountain/Forest/Sewer, so only the spawn row's Condition betrays them.</summary>
    [Theory]
    [InlineData("(O)898")]  // Son of Crimsonfish, Beach
    [InlineData("(O)899")]  // Ms. Angler, Town
    [InlineData("(O)900")]  // Legend II, Mountain
    [InlineData("(O)901")]  // Radioactive Carp, Sewer
    [InlineData("(O)902")]  // Glacierfish Jr., Forest
    public void Fish_ExtendedFamily_StaysOutOfPool(string qualifiedId)
    {
        string bare = qualifiedId.Substring(3);
        var pools = Build(
            fish: new[]
            {
                new RawSpawnEntry(qualifiedId, null, "PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", "Town"),
            },
            objects: Objects((bare, Obj(category: -4, type: "Fish"))));
        Assert.Empty(pools.Fish);
        Assert.DoesNotContain(qualifiedId, pools.QualityEligibleIds);
    }

    /// <summary>The trap this fix had to avoid: vanilla writes the PARENT legendaries as negated
    /// rows of the very same query ("!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY"
    /// is Angler's only row, because the order swaps parent for child). A negated clause means
    /// reachable WITHOUT the order, which is exactly the state a loop is in, so the row must
    /// count.</summary>
    [Fact]
    public void Fish_NegatedSpecialOrderRow_StaysInPool()
    {
        var pools = Build(
            fish: new[]
            {
                new RawSpawnEntry("(O)160", null, "!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", "Town"),
            },
            objects: Objects(("160", Obj(category: -4, type: "Fish"))));
        Assert.Equal("(O)160", Assert.Single(pools.Fish).ItemId);
    }

    /// <summary>Only the gated ROW is dropped, not the item: a fish that also spawns normally
    /// keeps that spawn's season and location.</summary>
    [Fact]
    public void Fish_GatedRowDropped_UngatedRowKept()
    {
        var pools = Build(
            fish: new[]
            {
                new RawSpawnEntry("(O)128", Season.Fall, "PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", "Sewer"),
                new RawSpawnEntry("(O)128", Season.Spring, null, "Beach"),
            },
            objects: Objects(("128", Obj(category: -4, type: "Fish"))));
        PoolItem pufferfish = Assert.Single(pools.Fish);
        Assert.Equal(new[] { Season.Spring }, pufferfish.Seasons);
        Assert.Equal(new[] { "Beach" }, pufferfish.Locations);
    }

    /// <summary>Forage rows carry the same gate - Qi Beans drop from DROP_QI_BEANS - so the
    /// forage pool has to read the Condition too.</summary>
    [Fact]
    public void Forage_SpecialOrderGated_StaysOutOfPool()
    {
        var pools = Build(
            forage: new[]
            {
                new RawSpawnEntry("(O)890", null, "!IS_FESTIVAL_DAY, PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current DROP_QI_BEANS", "Default"),
            },
            objects: Objects(("890", Obj(category: -74, type: "Seeds"))));
        Assert.Empty(pools.Forage);
    }

    /// <summary>Nexus post Thrippa, 2026-09-25: Visit Mount Vapius spawns eggs on the ground, so
    /// Spring Foraging asked for six Large Brown Eggs. Animal products stay out of the forage
    /// pool; the Animal and Chef's bundles still reach them. Plants beside them stay in.</summary>
    [Fact]
    public void Forage_AnimalProducts_StayOutOfPool()
    {
        var pools = Build(
            forage: new[]
            {
                new RawSpawnEntry("(O)182", Season.Spring, null, "Custom_MtVapius"),   // Large Brown Egg
                new RawSpawnEntry("(O)186", Season.Spring, null, "Custom_MtVapius"),   // Large Milk
                new RawSpawnEntry("(O)440", Season.Spring, null, "Custom_MtVapius"),   // Wool
                new RawSpawnEntry("(O)16", Season.Spring, null, "Custom_MtVapius"),    // Wild Horseradish
            },
            objects: Objects(
                ("182", Obj(category: -5)),
                ("186", Obj(category: -6)),
                ("440", Obj(category: -18)),
                ("16", Obj(category: -81))));
        Assert.Equal(new[] { "(O)16" }, pools.Forage.Select(p => p.ItemId).ToArray());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("SEASON spring", false)]
    [InlineData("PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", true)]
    [InlineData("PLAYER_SPECIAL_ORDER_ACTIVE Current Gunther", true)]
    [InlineData("!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY", false)]
    [InlineData("!IS_FESTIVAL_DAY, PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current DROP_QI_BEANS", true)]
    [InlineData("!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY, WEATHER Here Rain", false)]
    public void IsSpecialOrderGated_ReadsClausesAndRespectsNegation(string? condition, bool expected)
        => Assert.Equal(expected, ItemPoolBuilder.IsSpecialOrderGated(condition));

    [Fact]
    public void FishPool_RequiresObjectTypeFish_JunkSpawnsExcluded()
    {
        var pools = Build(
            objects: Objects(("128", Obj(category: -4, type: "Fish")), ("388", Obj(category: -16))),
            fish: new[]
            {
                new RawSpawnEntry("(O)128", null, null, "Beach"),
                new RawSpawnEntry("(O)388", null, null, "Town"), // wood in a fish table — junk
            });
        Assert.Equal(new[] { "(O)128" }, pools.Fish.Select(p => p.ItemId));
    }

    [Fact]
    public void MonsterPool_RequiresMonsterLootCategory_OtherDropsExcluded()
    {
        var pools = Build(
            objects: Objects(("768", Obj(category: -28)), ("80", Obj(category: -2))),
            drops: new[] { new RawMonsterDropEntry("768"), new RawMonsterDropEntry("80") });
        Assert.Equal(new[] { "(O)768" }, pools.MonsterDrops.Select(p => p.ItemId));
    }

    [Fact]
    public void Fish_SeasonsUnionAcrossSpawns_LocationsCollected_TrapSeparated()
    {
        var pools = Build(
            objects: Objects(("128", Obj(category: -4, type: "Fish")), ("715", Obj(category: -4, type: "Fish"))),
            fish: new[]
            {
                new RawSpawnEntry("(O)128", Season.Summer, null, "Beach"),
                new RawSpawnEntry("(O)128", Season.Fall, null, "Forest"),
                new RawSpawnEntry("(O)715", null, null, "Beach"),
            },
            trap: new HashSet<string> { "715" });
        var fish = Assert.Single(pools.Fish);
        Assert.Equal("(O)128", fish.ItemId);
        Assert.Equal(new[] { Season.Summer, Season.Fall }, fish.Seasons);
        Assert.Equal(new[] { "Beach", "Forest" }, fish.Locations);
        Assert.Equal("(O)715", Assert.Single(pools.CrabPot).ItemId);
    }

    [Fact]
    public void SeasonsFromSpawn_ConditionTokensParsed_NoSignalMeansAllSeasons()
    {
        Assert.Equal(new[] { Season.Winter }, ItemPoolBuilder.SeasonsFromSpawn(Season.Winter, null));
        Assert.Equal(new[] { Season.Spring, Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "LOCATION_SEASON Here spring summer"));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, null));               // empty = any
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "PLAYER_HAS_MAIL x")); // no season signal
    }

    /// <summary>Player report 2026-08-28: a Sea Cucumber (Fall/Winter at the beach) was demanded
    /// before Summer 1. Its Submarine row carries no season because the game gates the Night
    /// Market by date in code, so the pool read it as all-year. Rows for a passive festival's
    /// own maps (Submarine, BeachNightMarket) and rows conditioned on
    /// IS_PASSIVE_FESTIVAL_OPEN take that festival's season from Data/PassiveFestivals.</summary>
    [Fact]
    public void SeasonsFromSpawn_PassiveFestivalRows_TakeTheFestivalSeason()
    {
        var festivals = new Dictionary<string, Season> { ["NightMarket"] = Season.Winter, ["SquidFest"] = Season.Winter, ["TroutDerby"] = Season.Summer };
        Assert.Equal(new[] { Season.Winter },
            ItemPoolBuilder.SeasonsFromSpawn(null, null, "Submarine", festivals));
        Assert.Equal(new[] { Season.Winter },
            ItemPoolBuilder.SeasonsFromSpawn(null, null, "BeachNightMarket", festivals));
        Assert.Equal(new[] { Season.Winter },
            ItemPoolBuilder.SeasonsFromSpawn(null, "IS_PASSIVE_FESTIVAL_OPEN SquidFest, TIME 0600 1800", "Beach", festivals));
        Assert.Equal(new[] { Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "IS_PASSIVE_FESTIVAL_OPEN TroutDerby", "Forest", festivals));
        // An explicit season or season tokens still win; an unknown festival is no signal.
        Assert.Equal(new[] { Season.Fall },
            ItemPoolBuilder.SeasonsFromSpawn(Season.Fall, "IS_PASSIVE_FESTIVAL_OPEN SquidFest", "Beach", festivals));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "IS_PASSIVE_FESTIVAL_OPEN ModFest", "Beach", festivals));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, null, "Beach", festivals));
        // Without festival data the Night Market maps still read as Winter (built-in fallback).
        Assert.Equal(new[] { Season.Winter }, ItemPoolBuilder.SeasonsFromSpawn(null, null, "Submarine"));
    }

    [Fact]
    public void FishPool_NightMarketFish_IsWinterNotAnySeason_AndPinnedFall()
    {
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(("154", Obj(category: -4, price: 75, type: "Fish")), ("800", Obj(category: -4, price: 500, type: "Fish"))),
            new List<RawSpawnEntry>(),
            new[]
            {
                new RawSpawnEntry("154", null, "LOCATION_SEASON Here fall winter", "Beach"),
                new RawSpawnEntry("154", null, null, "Submarine"),
                new RawSpawnEntry("800", null, null, "Submarine"),
            },
            new HashSet<string>(), new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(),
            new List<RawGeodeDropEntry>(), Tuning,
            festivalSeasons: new Dictionary<string, Season> { ["NightMarket"] = Season.Winter });
        Assert.Equal(new[] { Season.Fall, Season.Winter }, pools.Fish.Single(p => p.ItemId == "(O)154").Seasons); // Sea Cucumber
        Assert.Equal(new[] { Season.Winter }, pools.Fish.Single(p => p.ItemId == "(O)800").Seasons);              // Blobfish
        Assert.Equal(Season.Fall, pools.DerivedSeasonPins["(O)154"]);
        Assert.Equal(Season.Winter, pools.DerivedSeasonPins["(O)800"]);
    }

    /// <summary>Player reports 2026-10-29: Rainbow Trout (Summer) and Squid (Winter) asked for in
    /// Spring weeks 1-2 on a save with map mods. A '!' anywhere in a row's Condition used to mean
    /// "no season signal" = any season. A Condition is a comma-separated AND, read clause by clause.</summary>
    [Fact]
    public void SeasonsFromSpawn_NegatedClauses_ReadClauseByClause()
    {
        // A negated clause that names no season leaves the season clause in charge.
        Assert.Equal(new[] { Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "LOCATION_SEASON Here summer, !WEATHER Here rain"));
        Assert.Equal(new[] { Season.Winter },
            ItemPoolBuilder.SeasonsFromSpawn(null, "!IS_PASSIVE_FESTIVAL_OPEN SquidFest, SEASON winter"));
        // A negated season clause removes its seasons.
        Assert.Equal(new[] { Season.Spring, Season.Summer, Season.Fall },
            ItemPoolBuilder.SeasonsFromSpawn(null, "!LOCATION_SEASON Here winter"));
        Assert.Equal(new[] { Season.Spring },
            ItemPoolBuilder.SeasonsFromSpawn(null, "SEASON spring summer, !SEASON summer"));
        Assert.Equal(new[] { Season.Spring },
            ItemPoolBuilder.SeasonsFromSpawn(null, "!SEASON summer, SEASON spring summer"));
        // Positive season clauses intersect.
        Assert.Equal(new[] { Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "SEASON spring summer, LOCATION_SEASON Here summer fall"));
        // Negated clauses alone with no season are still no signal; so is no condition.
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "!WEATHER Here rain"));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, null));
        // The explicit Season field still wins over the condition.
        Assert.Equal(new[] { Season.Fall },
            ItemPoolBuilder.SeasonsFromSpawn(Season.Fall, "!LOCATION_SEASON Here fall"));
    }

    /// <summary>Only the real season queries say anything about seasons, read as the game reads
    /// them: SEASON parses the enum (names or 0-3), LOCATION_SEASON compares season-name strings
    /// and only means the calendar for Here / Target, and no other query's words are seasons.</summary>
    [Fact]
    public void SeasonsFromSpawn_OnlyRealSeasonQueriesCount()
    {
        // Numbers outside SEASON are not seasons ("3" is a fishing level, "2" a year).
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "!WEATHER Here rain, PLAYER_FISHING_LEVEL Current 3"));
        Assert.Equal(new[] { Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "LOCATION_SEASON Here summer, PLAYER_FISHING_LEVEL Current 3"));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "!WEATHER Here rain, YEAR 2"));
        // SEASON reads the enum, numbers included; LOCATION_SEASON compares names only.
        Assert.Equal(new[] { Season.Summer }, ItemPoolBuilder.SeasonsFromSpawn(null, "SEASON 1"));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "!LOCATION_SEASON Here 1"));
        // Another map's season can differ from the calendar (the island is always summer).
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "!LOCATION_SEASON IslandSouth winter"));
        Assert.Equal(new[] { Season.Fall },
            ItemPoolBuilder.SeasonsFromSpawn(null, "LOCATION_SEASON Target fall"));
        // A word that happens to be a season name is not a season query.
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "PLAYER_HAS_MAIL Current spring, !WEATHER Here rain"));
        // SEASON_DAY narrows to its seasons; negated it only removes a day.
        Assert.Equal(new[] { Season.Spring, Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "SEASON_DAY spring 5 summer 10"));
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "!SEASON_DAY winter 25"));
        // Quoted arguments are unquoted.
        Assert.Equal(new[] { Season.Spring, Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "SEASON \"spring\" summer, !WEATHER Here rain"));
    }

    [Fact]
    public void SeasonsFromSpawn_AnyClause_IsNoSeasonSignal_AndNeverLeaksInnerClauses()
    {
        Assert.Empty(ItemPoolBuilder.SeasonsFromSpawn(null, "ANY \"!SEASON winter fall\" \"WEATHER Here rain\""));
        Assert.Equal(new[] { Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "SEASON summer, ANY \"SEASON spring\" \"WEATHER Here rain\""));
        // An escaped quote inside a quoted ANY argument does not close it, so the inner
        // ", SEASON winter" stays inside the ANY instead of joining the outer AND.
        Assert.Equal(new[] { Season.Summer },
            ItemPoolBuilder.SeasonsFromSpawn(null, "ANY \"PLAYER_HAS_MAIL Current \\\"x, SEASON winter\" \"WEATHER Here rain\", SEASON summer"));
    }

    [Fact]
    public void ReadSpawnSeasons_TellsUnknownSeasonsAndImpossibleApart()
    {
        Assert.Equal(SeasonVerdict.Unknown, ItemPoolBuilder.ReadSpawnSeasons(null, "!WEATHER Here rain", "Beach").Verdict);
        Assert.Equal(SeasonVerdict.Seasons, ItemPoolBuilder.ReadSpawnSeasons(null, "SEASON spring", "Beach").Verdict);
        Assert.Equal(SeasonVerdict.Impossible, ItemPoolBuilder.ReadSpawnSeasons(null, "SEASON spring, !SEASON spring", "Beach").Verdict);
        Assert.Equal(SeasonVerdict.Impossible, ItemPoolBuilder.ReadSpawnSeasons(null, "SEASON spring, LOCATION_SEASON Here winter", "Beach").Verdict);
        Assert.True(ItemPoolBuilder.IsSeasonImpossible(null, "SEASON spring, !SEASON spring"));
        Assert.False(ItemPoolBuilder.IsSeasonImpossible(Season.Spring, "SEASON spring, !SEASON spring"));   // the field wins
        Assert.False(ItemPoolBuilder.IsSeasonImpossible(null, null));
    }

    /// <summary>A row whose season clauses rule out every season never fires: it is dropped, so it
    /// neither makes the fish any-season nor adds its map.</summary>
    [Fact]
    public void FishPool_ImpossibleRow_IsDropped()
    {
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(("138", Obj(category: -4, price: 65, type: "Fish")), ("151", Obj(category: -4, price: 80, type: "Fish"))),
            new List<RawSpawnEntry>(),
            new[]
            {
                new RawSpawnEntry("138", Season.Summer, null, "Mountain"),
                new RawSpawnEntry("138", null, "SEASON spring, !SEASON spring", "ModdedLake"),
                new RawSpawnEntry("151", null, "SEASON summer, SEASON winter", "ModdedLake"),
            },
            new HashSet<string>(), new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(),
            new List<RawGeodeDropEntry>(), Tuning);
        PoolItem trout = pools.Fish.Single(p => p.ItemId == "(O)138");
        Assert.Equal(new[] { Season.Summer }, trout.Seasons);
        Assert.Equal(new[] { "Mountain" }, trout.Locations);
        Assert.DoesNotContain(pools.Fish, p => p.ItemId == "(O)151");
    }

    [Fact]
    public void FishPool_ModdedRowWithNegatedWeather_KeepsTheFishInItsSeason()
    {
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(("138", Obj(category: -4, price: 65, type: "Fish"))),
            new List<RawSpawnEntry>(),
            new[]
            {
                new RawSpawnEntry("138", Season.Summer, null, "Mountain"),
                new RawSpawnEntry("138", null, "LOCATION_SEASON Here summer, !WEATHER Here rain", "ModdedLake"),
            },
            new HashSet<string>(), new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(),
            new List<RawGeodeDropEntry>(), Tuning);
        Assert.Equal(new[] { Season.Summer }, pools.Fish.Single(p => p.ItemId == "(O)138").Seasons); // Rainbow Trout
    }

    /// <summary>Winter Root and Snow Yam are dug up, not picked, so they have no Data/Locations
    /// forage row and never reached the Winter pool; vanilla's own Winter Foraging bundle asks
    /// for both. Built-in (not a tuning default) because a saved config.json replaces the
    /// SeasonalForageAdditions dictionary wholesale.</summary>
    [Fact]
    public void BuiltInWinterAdditions_WinterRootAndSnowYam_JoinWinterPool_EvenWithEmptiedTuning()
    {
        var emptied = new BundleGenerationTuning { SeasonalForageAdditions = new() };
        var pools = ItemPoolBuilder.Build(
            new List<RawCropEntry>(),
            Objects(("412", Obj(category: -81, price: 70)), ("416", Obj(category: -81, price: 100))),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(), new HashSet<string>(),
            new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(),
            emptied);
        Assert.Equal(new[] { Season.Winter }, pools.Forage.Single(p => p.ItemId == "(O)412").Seasons);
        Assert.Equal(new[] { Season.Winter }, pools.Forage.Single(p => p.ItemId == "(O)416").Seasons);
        Assert.Equal(Season.Winter, pools.DerivedSeasonPins["(O)412"]);
    }

    [Fact]
    public void ForageAdditions_FromTuning_JoinTheSeasonPool()
    {
        // Default tuning adds (O)404/(O)420 to Spring; give them object entries so they vet in.
        var pools = Build(
            objects: Objects(("404", Obj(category: -81)), ("420", Obj(category: -81)),
                             ("16", Obj(category: -81))),
            forage: new[] { new RawSpawnEntry("(O)16", Season.Spring, null, "Forest") });
        var ids = pools.Forage.Select(p => p.ItemId).ToList();
        Assert.Contains("(O)16", ids);
        Assert.Contains("(O)404", ids);
        Assert.Contains("(O)420", ids);
        Assert.Contains(Season.Spring, pools.Forage.First(p => p.ItemId == "(O)404").Seasons);
    }

    [Fact]
    public void DerivedSeasonPins_LaterThanSpringOnly()
    {
        var pools = Build(
            objects: Objects(("128", Obj(category: -4, type: "Fish")), ("129", Obj(category: -4, type: "Fish")),
                             ("130", Obj(category: -4, type: "Fish"))),
            fish: new[]
            {
                new RawSpawnEntry("(O)128", Season.Fall, null, "Beach"),   // earliest Fall -> pinned
                new RawSpawnEntry("(O)129", Season.Spring, null, "Beach"), // Spring -> no pin
                new RawSpawnEntry("(O)130", null, null, "Beach"),          // any season -> no pin
            });
        Assert.Equal(Season.Fall, pools.DerivedSeasonPins["(O)128"]);
        Assert.False(pools.DerivedSeasonPins.ContainsKey("(O)129"));
        Assert.False(pools.DerivedSeasonPins.ContainsKey("(O)130"));
    }

    [Fact]
    public void DerivedSeasonPins_IncludeSeasonLimitedTrapFish()
    {
        var pools = Build(
            objects: Objects(("715", Obj(category: -4, type: "Fish"))),
            fish: new[] { new RawSpawnEntry("(O)715", Season.Fall, null, "Beach") },
            trap: new HashSet<string> { "715" });
        Assert.Equal(Season.Fall, pools.DerivedSeasonPins["(O)715"]);
    }
}
