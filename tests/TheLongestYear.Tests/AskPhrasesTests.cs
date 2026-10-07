using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-07: "I prefer jars of wild honey, and sea jellies, and bottles of
/// blueberry wine." The counted ask in "Bring us {{new}} instead" names a container for a liquid or
/// spread, a proper plural for a countable thing, and the bare word for bulk stuff. The game's
/// pluralizer is <see cref="ItemPluralsTests.VanillaPlural"/>, the 1.6 makePlural in English.</summary>
public class AskPhrasesTests
{
    private static string Ask(int count, string id, string name)
        => AskPhrases.Ask(count, id, name, ItemPluralsTests.VanillaPlural);

    [Theory]
    // Confirmed by the designer.
    [InlineData("(O)346", "Beer", "3 mugs of Beer")]
    [InlineData("(O)303", "Pale Ale", "3 glasses of Pale Ale")]
    [InlineData("(O)395", "Coffee", "3 cups of Coffee")]
    [InlineData("(O)445", "Caviar", "3 tins of Caviar")]
    [InlineData("(O)812", "Salmon Roe", "3 clusters of Salmon Roe")]
    [InlineData("(O)812", "Roe", "3 clusters of Roe")]
    [InlineData("(O)447", "Aged Salmon Roe", "3 jars of Aged Salmon Roe")]
    [InlineData("(O)348", "Blueberry Wine", "3 bottles of Blueberry Wine")]
    [InlineData("(O)348", "Wine", "3 bottles of Wine")]
    [InlineData("(O)340", "Wild Honey", "3 jars of Wild Honey")]
    [InlineData("(O)340", "Honey", "3 jars of Honey")]
    [InlineData("(O)184", "Milk", "3 bottles of Milk")]
    [InlineData("(O)186", "Large Milk", "3 bottles of Large Milk")]
    [InlineData("(O)436", "Goat Milk", "3 bottles of Goat Milk")]
    [InlineData("(O)438", "L. Goat Milk", "3 bottles of L. Goat Milk")]
    [InlineData("(O)724", "Maple Syrup", "3 bottles of Maple Syrup")]
    [InlineData("(O)247", "Oil", "3 bottles of Oil")]
    [InlineData("(O)432", "Truffle Oil", "3 bottles of Truffle Oil")]
    [InlineData("(O)SeaJelly", "Sea Jelly", "3 Sea Jellies")]
    // Picked from the sprite, for the designer to check.
    [InlineData("(O)342", "Pickled Beets", "3 jars of Pickled Beets")]
    [InlineData("(O)342", "Pickles", "3 jars of Pickles")]
    [InlineData("(O)344", "Blueberry Jelly", "3 jars of Blueberry Jelly")]
    [InlineData("(O)350", "Apple Juice", "3 bottles of Apple Juice")]
    [InlineData("(O)459", "Mead", "3 jugs of Mead")]
    [InlineData("(O)614", "Green Tea", "3 cups of Green Tea")]
    [InlineData("(O)306", "Mayonnaise", "3 jars of Mayonnaise")]
    [InlineData("(O)807", "Dinosaur Mayonnaise", "3 jars of Dinosaur Mayonnaise")]
    [InlineData("(O)419", "Vinegar", "3 bottles of Vinegar")]
    [InlineData("(O)725", "Oak Resin", "3 bottles of Oak Resin")]
    [InlineData("(O)726", "Pine Tar", "3 jars of Pine Tar")]
    [InlineData("(O)MysticSyrup", "Mystic Syrup", "3 bottles of Mystic Syrup")]
    [InlineData("(O)814", "Squid Ink", "3 bottles of Squid Ink")]
    [InlineData("(O)DriedFruit", "Dried Fruit", "3 jars of Dried Fruit")]
    [InlineData("(O)DriedFruit", "Dried Apples", "3 jars of Dried Apples")]
    [InlineData("(O)DriedMushrooms", "Dried Mushrooms", "3 bags of Dried Mushrooms")]
    [InlineData("(O)Raisins", "Raisins", "3 boxes of Raisins")]
    [InlineData("(O)236", "Pumpkin Soup", "3 bowls of Pumpkin Soup")]
    [InlineData("(O)456", "Algae Soup", "3 bowls of Algae Soup")]
    [InlineData("(O)232", "Rice Pudding", "3 bowls of Rice Pudding")]
    [InlineData("(O)238", "Cranberry Sauce", "3 bowls of Cranberry Sauce")]
    [InlineData("(O)253", "Triple Shot Espresso", "3 cups of Triple Shot Espresso")]
    [InlineData("(O)903", "Ginger Ale", "3 bottles of Ginger Ale")]
    [InlineData("(O)224", "Spaghetti", "3 plates of Spaghetti")]
    [InlineData("(O)216", "Bread", "3 loaves of Bread")]
    [InlineData("(O)245", "Sugar", "3 bags of Sugar")]
    [InlineData("(O)167", "Joja Cola", "3 cans of Joja Cola")]
    public void A_liquid_or_spread_takes_its_container(string id, string name, string ask)
        => Assert.Equal(ask, Ask(3, id, name));

    [Theory]
    [InlineData("(O)SeaJelly", "Sea Jelly", "Sea Jellies")]
    [InlineData("(O)RiverJelly", "River Jelly", "River Jellies")]
    [InlineData("(O)CaveJelly", "Cave Jelly", "Cave Jellies")]
    [InlineData("(O)24", "Parsnip", "Parsnips")]
    [InlineData("(O)873", "Piña Colada", "Piña Coladas")]
    [InlineData("(O)233", "Ice Cream", "Ice Creams")]
    [InlineData("(O)223", "Cookies", "Cookies")]
    [InlineData("(O)304", "Hops", "Hops")]
    public void A_countable_thing_takes_a_plural(string id, string name, string plural)
        => Assert.Equal("3 " + plural, Ask(3, id, name));

    [Theory]
    [InlineData("(O)330", "Clay", "3 Clay")]
    [InlineData("(O)382", "Coal", "3 lumps of Coal")]     // the game's own phrase
    [InlineData("(O)178", "Hay", "3 Hay")]
    [InlineData("(O)440", "Wool", "3 Wool")]
    [InlineData("(O)428", "Cloth", "3 Cloth")]
    [InlineData("(O)771", "Fiber", "3 Fiber")]
    [InlineData("(O)92", "Sap", "3 Sap")]
    [InlineData("(O)Moss", "Moss", "3 Moss")]
    [InlineData("(O)709", "Hardwood", "3 Hardwood")]
    [InlineData("(O)388", "Wood", "3 Wood")]
    [InlineData("(O)684", "Bug Meat", "3 Bug Meat")]
    [InlineData("(O)378", "Copper Ore", "3 Copper Ore")]
    [InlineData("(O)338", "Refined Quartz", "3 Refined Quartz")]
    [InlineData("(O)SmokedFish", "Smoked Fish", "3 Smoked Fish")]
    [InlineData("(O)198", "Baked Fish", "3 Baked Fish")]
    public void Bulk_stuff_with_no_container_stays_bare(string id, string name, string ask)
        => Assert.Equal(ask, Ask(3, id, name));

    // Designer, 2026-10-07: every fish keeps the same word ("7 Pike", "7 Salmon"), except the
    // jellies he already chose ("Sea Jellies") and the fish goods with a container or a bare word.
    [Theory]
    [InlineData(7, "(O)144", "Pike", "7 Pike")]
    [InlineData(7, "(O)139", "Salmon", "7 Salmon")]
    [InlineData(3, "(O)136", "Largemouth Bass", "3 Largemouth Bass")]
    [InlineData(5, "(O)132", "Bream", "5 Bream")]
    [InlineData(4, "(O)128", "Pufferfish", "4 Pufferfish")]
    [InlineData(2, "(O)149", "Octopus", "2 Octopus")]
    [InlineData(3, "(O)SeaJelly", "Sea Jelly", "3 Sea Jellies")]
    [InlineData(3, "(O)RiverJelly", "River Jelly", "3 River Jellies")]
    [InlineData(3, "(O)CaveJelly", "Cave Jelly", "3 Cave Jellies")]
    public void A_fish_keeps_the_same_word(int count, string id, string name, string ask)
        => Assert.Equal(ask, AskPhrases.Ask(count, id, name, ItemPluralsTests.VanillaPlural, FlavoredSlotRules.FishCategory));

    [Theory]
    [InlineData("(O)812", "Salmon Roe", "3 clusters of Salmon Roe")]
    [InlineData("(O)447", "Aged Salmon Roe", "3 jars of Aged Salmon Roe")]
    [InlineData("(O)445", "Caviar", "3 tins of Caviar")]
    [InlineData("(O)SmokedFish", "Smoked Salmon", "3 Smoked Salmon")]
    public void The_fish_goods_keep_their_containers(string id, string name, string ask)
        => Assert.Equal(ask, AskPhrases.Ask(3, id, name, ItemPluralsTests.VanillaPlural, FlavoredSlotRules.FishCategory));

    [Fact]
    public void A_thing_that_is_not_a_fish_still_takes_its_plural()
        => Assert.Equal("7 Parsnips", AskPhrases.Ask(7, "(O)24", "Parsnip", ItemPluralsTests.VanillaPlural, -75));

    [Fact]
    public void Holly_comes_in_sprigs()
        => Assert.Equal("8 sprigs of Holly", Ask(8, "(O)283", "Holly"));

    [Theory]
    [InlineData("(O)346", "Beer")]
    [InlineData("(O)340", "Wild Honey")]
    [InlineData("(O)SeaJelly", "Sea Jelly")]
    [InlineData("(O)24", "Parsnip")]
    public void A_count_of_one_is_the_bare_name(string id, string name)
    {
        Assert.Equal(name, Ask(1, id, name));
        Assert.Equal(name, Ask(0, id, name));
    }

    [Fact]
    public void A_bare_id_finds_its_container()
        => Assert.Equal("2 mugs of Beer", Ask(2, "346", "Beer"));

    [Fact]
    public void An_item_the_tables_do_not_know_keeps_the_name_rules()
    {
        Assert.Equal("3 Cherry Wine", Ask(3, "Modder.Cellar_CherryWine", "Cherry Wine"));
        Assert.Equal("3 Turnips", Ask(3, "Modder.Garden_Turnip", "Turnip"));
    }

    [Fact]
    public void A_container_reads_as_plural()
        => Assert.True(ItemPlurals.AskIsPlural(3));

    [Fact]
    public void The_dried_and_smoked_goods_have_a_name_for_the_ask()
    {
        Assert.Equal("item-name.dried-fruit", FlavorlessBundleSlots.AskNameKeyFor("DriedFruit"));
        Assert.Equal("item-name.dried-mushrooms", FlavorlessBundleSlots.AskNameKeyFor("(O)DriedMushrooms"));
        Assert.Equal("item-name.smoked-fish", FlavorlessBundleSlots.AskNameKeyFor("(O)SmokedFish"));
        Assert.Null(FlavorlessBundleSlots.AskNameKeyFor("(O)24"));
        Assert.Equal(3, System.Linq.Enumerable.Count(FlavorlessBundleSlots.AllAskNameKeys));
    }
}
