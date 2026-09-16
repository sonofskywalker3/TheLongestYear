using System.Collections.Generic;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

public class ObtainabilityCacheTests
{
    private static ObtainabilityInputs Inputs(string shopItem) => new()
    {
        Shops = new List<ShopRow> { new("SeedShop", shopItem, null, IsRecipe: false) },
    };

    [Fact]
    public void Equal_inputs_reuse_the_previous_build()
    {
        var cache = new ObtainabilityCache();

        ObtainabilityBuild first = cache.Get(Inputs("(O)472"), out bool firstReused);
        ObtainabilityBuild second = cache.Get(Inputs("(O)472"), out bool secondReused);

        Assert.False(firstReused);
        Assert.True(secondReused);
        Assert.Same(first, second);
    }

    [Fact]
    public void A_changed_row_rebuilds()
    {
        var cache = new ObtainabilityCache();

        ObtainabilityBuild first = cache.Get(Inputs("(O)472"), out _);
        ObtainabilityBuild second = cache.Get(Inputs("(O)473"), out bool reused);

        Assert.False(reused);
        Assert.NotSame(first, second);
        Assert.NotEmpty(second.Model.Sources("(O)473"));
    }

    [Fact]
    public void A_changed_nested_list_rebuilds()
    {
        var cache = new ObtainabilityCache();
        ObtainabilityInputs WithCrop(int growthDays) => new()
        {
            Crops = new List<CropRow> { new("(O)472", "(O)24", new[] { TheLongestYear.Core.Season.Spring }, growthDays, -1) },
        };

        cache.Get(WithCrop(4), out _);
        cache.Get(WithCrop(5), out bool reused);

        Assert.False(reused);
    }
}
