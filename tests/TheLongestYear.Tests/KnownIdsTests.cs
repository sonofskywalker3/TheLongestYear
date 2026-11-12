using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Lexicon;
using Xunit;

namespace TheLongestYear.Tests;

public class KnownIdsTests
{
    // Shaped like the game's data classes: public fields, rows with an Id property, a list inside a row.
    public sealed class Row
    {
        public string Id { get; set; } = "";
        public string ItemId = "";
        public List<Row> Inner = new();
    }

    public sealed class Place
    {
        public string Name = "";
        public List<Row> Fish = new();
        public Dictionary<string, Place> Areas = new();
    }

    private const string Asset = "Data/Locations";

    private static Dictionary<string, Place> Game() => new()
    {
        ["Forest"] = new Place
        {
            Name = "Forest",
            Fish =
            {
                new Row { Id = "Carp", ItemId = "(O)142", Inner = { new Row { Id = "Deep" }, new Row { Id = "ModDeep" } } },
                new Row { Id = "Mod_Carp", ItemId = "(O)269" },
            },
            Areas = { ["Pond"] = new Place { Fish = { new Row { Id = "Chub" }, new Row { Id = "Mod_Chub" } } } },
        },
        ["Custom_ModMine"] = new Place { Fish = { new Row { Id = "Carp", ItemId = "(O)269" } } },
    };

    private static readonly string[] VanillaIds =
    {
        "Data/Locations|Forest",
        "Data/Locations|Forest|Fish/Carp",
        "Data/Locations|Forest|Fish/Carp/Inner/Deep",
        "Data/Locations|Forest|Areas/Pond",
        "Data/Locations|Forest|Areas/Pond/Fish/Chub",
    };

    [Fact]
    public void IdsListsEveryEntryAndRow()
    {
        var ids = AssetIds.Ids(Asset, Game()).ToList();
        Assert.Equal(10, ids.Count);
        Assert.Superset(new HashSet<string>(VanillaIds), new HashSet<string>(ids));
        Assert.Contains("Data/Locations|Custom_ModMine|Fish/Carp", ids);
        Assert.Contains("Data/Locations|Forest|Fish/Carp/Inner/ModDeep", ids);
    }

    [Fact]
    public void FilterKeepsOnlyKnownEntriesAndRowsAndLeavesTheGameDataAlone()
    {
        Dictionary<string, Place> game = Game();
        var dropped = new List<string>();
        Dictionary<string, Place> kept = AssetIds.Filter(Asset, game, new KnownIds(VanillaIds), dropped.Add);

        Assert.Equal(new[] { "Forest" }, kept.Keys);
        Place forest = kept["Forest"];
        Assert.Equal("Forest", forest.Name);
        Assert.Equal(new[] { "Carp" }, forest.Fish.Select(r => r.Id));
        Assert.Equal(new[] { "Deep" }, forest.Fish[0].Inner.Select(r => r.Id));
        Assert.Equal(new[] { "Chub" }, forest.Areas["Pond"].Fish.Select(r => r.Id));
        Assert.Equal(4, dropped.Count);

        Assert.Equal(2, game["Forest"].Fish.Count);
        Assert.Equal(2, game["Forest"].Fish[0].Inner.Count);
        Assert.Equal(2, game["Forest"].Areas["Pond"].Fish.Count);
    }

    [Fact]
    public void FilterHandsBackAnUntouchedEntryAndTheDataItselfWithNoList()
    {
        Dictionary<string, Place> game = Game();
        Dictionary<string, Place> all = AssetIds.Filter(Asset, game, new KnownIds(AssetIds.Ids(Asset, game)));
        Assert.Same(game["Forest"], all["Forest"]);
        Assert.Same(game, AssetIds.Filter(Asset, game, null));
    }

    [Fact]
    public void ARowWithNoIdIsKnownByWhatItSaysWhereverItSits()
    {
        var shipped = new List<Row> { new() { Id = "", ItemId = "(O)1" } };
        var known = new KnownIds(AssetIds.Ids("Data/FishPondData", shipped));
        // A mod puts a named row and an unnamed one in front of the known unnamed row.
        var live = new List<Row> { new() { Id = "Mod" }, new() { Id = "", ItemId = "(O)Mod" }, new() { Id = "", ItemId = "(O)1" } };
        Assert.Equal(new[] { "(O)1" }, AssetIds.Filter("Data/FishPondData", live, known).Select(r => r.ItemId));

        var place = new Place { Fish = { new Row { Id = "", ItemId = "(O)Mod" }, new Row { Id = "", ItemId = "(O)1" } } };
        var knownPlace = new KnownIds(AssetIds.Ids("A", new Dictionary<string, Place> { ["P"] = new Place { Fish = { new Row { Id = "", ItemId = "(O)1" } } } }));
        Assert.Equal(new[] { "(O)1" }, AssetIds.Filter("A", new Dictionary<string, Place> { ["P"] = place }, knownPlace)["P"].Fish.Select(r => r.ItemId));
    }

    [Fact]
    public void AStringAssetIsKnownByItsKey()
    {

        var fish = new Dictionary<string, string> { ["128"] = "Pufferfish/80", ["Mod_Fish"] = "x" };
        Dictionary<string, string> kept = AssetIds.Filter("Data/Fish", fish, new KnownIds(new[] { "Data/Fish|128" }));
        Assert.Equal(new[] { "128" }, kept.Keys);
    }

    [Fact]
    public void AnObjectAssetIsOneEntryWithAnEmptyKey()
    {
        var cans = new Place { Areas = { ["Can1"] = new Place { Fish = { new Row { Id = "Base" }, new Row { Id = "Mod" } } } } };
        var known = new KnownIds(new[] { "Data/GarbageCans|", "Data/GarbageCans||Areas/Can1", "Data/GarbageCans||Areas/Can1/Fish/Base" });
        Place kept = AssetIds.Filter("Data/GarbageCans", cans, known);
        Assert.Equal(new[] { "Base" }, kept.Areas["Can1"].Fish.Select(r => r.Id));
        Assert.Equal(2, cans.Areas["Can1"].Fish.Count);
    }
}
