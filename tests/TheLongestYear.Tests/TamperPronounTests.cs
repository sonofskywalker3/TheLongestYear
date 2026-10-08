using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-08: after "It has tainted all the X", the Junimos say "The darkness
/// has touched it" when X reads as uncountable (a mass noun, bulk stuff, a container good), and
/// keep "touched them" for anything counted, fish and shellfish included.</summary>
public class TamperPronounTests
{
    private const int NoCat = AskPhrases.NoCategory;
    private const int Fish = FlavoredSlotRules.FishCategory;

    [Theory]
    // bulk stuff with no container
    [InlineData("Wood", "(O)388", NoCat)]
    [InlineData("Hardwood", "(O)709", NoCat)]
    [InlineData("Clay", "(O)330", NoCat)]
    [InlineData("Hay", "(O)178", NoCat)]
    [InlineData("Wool", "(O)440", NoCat)]
    [InlineData("Cloth", "(O)428", NoCat)]
    [InlineData("Fiber", "(O)771", NoCat)]
    [InlineData("Sap", "(O)92", NoCat)]
    [InlineData("Moss", "(O)Moss", NoCat)]
    [InlineData("Copper Ore", "(O)378", NoCat)]
    [InlineData("Refined Quartz", "(O)338", NoCat)]
    [InlineData("Seaweed", "(O)152", NoCat)]
    [InlineData("Driftwood", "(O)169", NoCat)]
    // container goods
    [InlineData("Holly", "(O)283", NoCat)]
    [InlineData("Honey", "(O)340", NoCat)]
    [InlineData("Wine", "(O)348", NoCat)]
    [InlineData("Beer", "(O)346", NoCat)]
    [InlineData("Milk", "(O)184", NoCat)]
    [InlineData("Oil", "(O)247", NoCat)]
    [InlineData("Maple Syrup", "(O)724", NoCat)]
    [InlineData("Mayonnaise", "(O)306", NoCat)]
    [InlineData("Bread", "(O)216", NoCat)]
    [InlineData("Pumpkin Soup", "(O)236", NoCat)]
    [InlineData("Roe", "(O)812", NoCat)]
    [InlineData("Caviar", "(O)445", NoCat)]
    public void An_uncountable_item_is_it(string name, string id, int category)
        => Assert.True(ItemPlurals.TaintedReadsAsMass(name, id, flavored: false, ItemPluralsTests.VanillaPlural, category));

    [Theory]
    [InlineData("Blueberry Wine", "(O)348")]
    [InlineData("Wild Honey", "(O)340")]
    [InlineData("Apple Juice", "(O)350")]
    [InlineData("Blueberry Jelly", "(O)344")]
    [InlineData("Pickled Beet", "(O)342")]
    public void A_flavoured_container_good_is_it(string name, string id)
        => Assert.True(ItemPlurals.TaintedReadsAsMass(name, id, flavored: true, ItemPluralsTests.VanillaPlural));

    [Theory]
    // normal plurals
    [InlineData("Parsnip", "(O)24", NoCat)]
    [InlineData("Potato", "(O)192", NoCat)]
    [InlineData("Salad", "(O)196", NoCat)]
    // the game's phrase plurals read as counted too ("all the lumps of Coal")
    [InlineData("Coal", "(O)382", NoCat)]
    [InlineData("Wheat", "(O)262", NoCat)]
    // fish keep the same word but are counted
    [InlineData("Pike", "(O)144", Fish)]
    [InlineData("Salmon", "(O)139", Fish)]
    [InlineData("Bream", "(O)132", Fish)]
    [InlineData("Largemouth Bass", "(O)136", Fish)]
    [InlineData("Shrimp", "(O)720", Fish)]
    [InlineData("Crayfish", "(O)716", Fish)]
    // shellfish and jellies take their plurals
    [InlineData("Oyster", "(O)723", Fish)]
    [InlineData("Sea Jelly", "(O)SeaJelly", Fish)]
    // already plural names
    [InlineData("Cookies", "(O)223", NoCat)]
    [InlineData("Raisins", "(O)Raisins", NoCat)]
    [InlineData("Pickles", "(O)342", NoCat)]
    [InlineData("Hops", "(O)304", NoCat)]
    [InlineData("Tea Leaves", "(O)815", NoCat)]
    [InlineData("Dried Mushrooms", "(O)DriedMushrooms", NoCat)]
    // counted dishes that keep the same word
    [InlineData("Smoked Fish", "(O)SmokedFish", NoCat)]
    [InlineData("Baked Fish", "(O)198", NoCat)]
    public void A_counted_item_is_them(string name, string id, int category)
        => Assert.False(ItemPlurals.TaintedReadsAsMass(name, id, flavored: false, ItemPluralsTests.VanillaPlural, category));

    [Theory]
    [InlineData("Dried Apples", "(O)DriedFruit")]
    [InlineData("Smoked Salmon", "(O)SmokedFish")]
    [InlineData("Dried Chanterelles", "(O)DriedMushrooms")]
    public void A_flavoured_counted_good_is_them(string name, string id)
        => Assert.False(ItemPlurals.TaintedReadsAsMass(name, id, flavored: true, ItemPluralsTests.VanillaPlural));
}
