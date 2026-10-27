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
}
