using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Lexicon;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

public class RowHashesTests
{
    private static readonly IReadOnlyList<string> Layers = new[] { "vanilla" };

    [Fact]
    public void SameInputsSameKey()
    {
        var a = RowHashes.Of(VanillaInputsFixture.Load(), false);
        var b = RowHashes.Of(VanillaInputsFixture.Load(), false);
        Assert.Equal(a.OrderBy(p => p.Key, System.StringComparer.Ordinal), b.OrderBy(p => p.Key, System.StringComparer.Ordinal));
        Assert.True(a.Count > 1000);
        Assert.Equal(RowHashes.Key(a, Layers, "1.6.15", "1.1.0"), RowHashes.Key(b, Layers, "1.6.15", "1.1.0"));
        Assert.NotEqual(RowHashes.Key(a, Layers, "1.6.15", "1.1.0"), RowHashes.Key(a, Layers, "1.6.15", "1.1.1"));
        Assert.NotEqual(RowHashes.Key(a, Layers, "1.6.15", "1.1.0"), RowHashes.Key(a, new[] { "vanilla", "x" }, "1.6.15", "1.1.0"));
    }

    [Fact]
    public void ShopRowChangeChangesOnlyThatRow()
    {
        var inputs = VanillaInputsFixture.Load();
        var shops = inputs.Shops.ToList();
        shops[0] = shops[0] with { TradeItemId = "(O)74" };
        var diff = RowHashes.Diff(RowHashes.Of(inputs, false), RowHashes.Of(inputs with { Shops = shops }, false));
        Assert.Empty(diff.Added);
        Assert.Empty(diff.Removed);
        string changed = Assert.Single(diff.Changed);
        Assert.StartsWith("shop|", changed);
    }

    [Fact]
    public void ShopConditionIsPartOfTheKey()
    {
        var inputs = VanillaInputsFixture.Load();
        var shops = inputs.Shops.ToList();
        shops[0] = shops[0] with { Condition = "PLAYER_HAS_MAIL Current Test" };
        var diff = RowHashes.Diff(RowHashes.Of(inputs, false), RowHashes.Of(inputs with { Shops = shops }, false));
        Assert.Empty(diff.Changed);
        Assert.StartsWith("shop|", Assert.Single(diff.Added));
        Assert.StartsWith("shop|", Assert.Single(diff.Removed));
    }

    [Fact]
    public void NewObjectIsAdded()
    {
        var inputs = VanillaInputsFixture.Load();
        var objects = inputs.Objects.ToDictionary(p => p.Key, p => p.Value);
        objects["(O)Cornucopia_Avocado"] = new ObjInfo("(O)Cornucopia_Avocado", "Avocado", -79, 90, new[] { "category_fruits" }, false);
        var diff = RowHashes.Diff(RowHashes.Of(inputs, false), RowHashes.Of(inputs with { Objects = objects }, false));
        Assert.Equal(new[] { "obj|(O)Cornucopia_Avocado" }, diff.Added);
        Assert.Empty(diff.Removed);
        Assert.Empty(diff.Changed);
    }

    [Theory]
    [InlineData("obj|(O)24", "vanilla")]
    [InlineData("obj|(O)Cornucopia_Avocado", "Cornucopia")]
    [InlineData("shop|Pierre|(O)24||0", "vanilla")]
    [InlineData("shop|Pierre|(O)Mod.Thing||0", "Mod")]
    [InlineData("pond|Mod_Pond", "Mod")]
    [InlineData("pond|Coral", "vanilla")]
    [InlineData("shop|Pierre|ALL_ITEMS (O)||0", "vanilla")]
    [InlineData("shop|Mod_Shop|ALL_ITEMS (O)||0", "Mod")]   // the shop id prefix is the owner of a mod shop with query stock
    [InlineData("meta|pierre_year2", "vanilla")]
    public void OwnerOfVanillaAndModIds(string key, string owner)
        => Assert.Equal(owner, RowHashes.Owner(key));

    [Fact]
    public void PierreFlagChangesKey()
    {
        var rows = RowHashes.Of(VanillaInputsFixture.Load(), false);
        var flagged = RowHashes.Of(VanillaInputsFixture.Load(), true);
        Assert.NotEqual(RowHashes.Key(rows, Layers, "g", "m"), RowHashes.Key(flagged, Layers, "g", "m"));
        Assert.Equal(new[] { "meta|pierre_year2" }, RowHashes.Diff(rows, flagged).Changed);
    }

    [Fact]
    public void DuplicateIdsKeepBothRowsWithStableKeys()
    {
        var cooking = new RecipeRow("Salad", new[] { "(O)24" }, "(O)196", "default", true);
        var crafting = cooking with { IsCooking = false, OutputId = "(O)197" };
        var inputs = new ObtainabilityInputs { Recipes = new[] { cooking, crafting } };
        var a = RowHashes.Of(inputs, false);
        var b = RowHashes.Of(inputs, false);
        Assert.Equal(new[] { "recipe|Salad", "recipe|Salad|1" }, a.Keys.Where(k => k.StartsWith("recipe|")).OrderBy(k => k, System.StringComparer.Ordinal));
        Assert.NotEqual(a["recipe|Salad"], a["recipe|Salad|1"]);
        Assert.Equal(a.OrderBy(p => p.Key, System.StringComparer.Ordinal), b.OrderBy(p => p.Key, System.StringComparer.Ordinal));
    }

    [Fact]
    public void RepeatedListKeysStillGetZeroBasedSuffix()
    {
        var drop = new MonsterDropRow("Slime", "(O)24", 0.5);
        var rows = RowHashes.Of(new ObtainabilityInputs { MonsterDrops = new[] { drop, drop with { Chance = 1 } } }, false);
        Assert.Equal(new[] { "drop|Slime|(O)24|0", "drop|Slime|(O)24|1" }, rows.Keys.Where(k => k.StartsWith("drop|")).OrderBy(k => k, System.StringComparer.Ordinal));
    }

    [Fact]
    public void EveryRecordKindYieldsKeys()
    {
        var keys = RowHashes.Of(VanillaInputsFixture.Load(), false).Keys.ToList();
        string[] prefixes = { "obj|", "fest|", "forage|", "fish|", "fishrow|", "spot|", "garbage|", "shop|", "drop|", "crop|", "tree|",
            "machine|", "recipe|", "animal|", "pond|", "tap|", "geode|", "geodedefault|", "slayer|", "building|", "tv|", "meta|" };
        foreach (string prefix in prefixes)
            Assert.True(keys.Any(k => k.StartsWith(prefix, System.StringComparison.Ordinal)), "no rows for " + prefix);
    }

    /// <summary>Every public property of <see cref="ObtainabilityInputs"/>, mapped by hand to the key
    /// prefix <see cref="RowHashes.Of"/> gives its rows. A property added to the inputs without rows
    /// here (and in <see cref="RowHashes.Of"/>) would let its data change without changing the rows,
    /// so the shipped answers would be served for data they were not built from.</summary>
    private static readonly IReadOnlyDictionary<string, string> PrefixByProperty = new Dictionary<string, string>
    {
        ["Objects"] = "obj|", ["Festivals"] = "fest|", ["Forage"] = "forage|", ["LocationFish"] = "fish|",
        ["FishRows"] = "fishrow|", ["ArtifactSpots"] = "spot|", ["Garbage"] = "garbage|", ["Shops"] = "shop|",
        ["MonsterDrops"] = "drop|", ["Crops"] = "crop|", ["FruitTrees"] = "tree|", ["Machines"] = "machine|",
        ["Recipes"] = "recipe|", ["Animals"] = "animal|", ["Ponds"] = "pond|", ["TapItems"] = "tap|", ["TreeDrops"] = "treedrop|",
        ["GeodeDrops"] = "geode|", ["GeodesUsingDefaultTable"] = "geodedefault|", ["SlayerQuests"] = "slayer|",
        ["Buildings"] = "building|", ["CookingChannel"] = "tv|",
    };

    [Fact]
    public void EveryInputsPropertyHasRows()
    {
        var properties = typeof(ObtainabilityInputs)
            .GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Select(p => p.Name).OrderBy(n => n, System.StringComparer.Ordinal).ToList();
        Assert.Equal(PrefixByProperty.Keys.OrderBy(n => n, System.StringComparer.Ordinal), properties);

        var keys = RowHashes.Of(VanillaInputsFixture.Load(), false).Keys.ToList();
        foreach (var (property, prefix) in PrefixByProperty)
            Assert.True(keys.Any(k => k.StartsWith(prefix, System.StringComparison.Ordinal)), $"{property}: no rows under {prefix}");
    }

    [Fact]
    public void CodeStampChangesKey()
    {
        var rows = RowHashes.Of(VanillaInputsFixture.Load(), false);
        Assert.NotEqual(RowHashes.Key(rows, Layers, "g", "m", "a"), RowHashes.Key(rows, Layers, "g", "m", "b"));
        Assert.NotEqual(RowHashes.Key(rows, Layers, "g", "m"), RowHashes.Key(rows, Layers, "g", "m", "a"));
    }
}
