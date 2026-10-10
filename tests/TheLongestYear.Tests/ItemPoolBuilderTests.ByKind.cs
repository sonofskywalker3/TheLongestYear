using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public partial class ItemPoolBuilderTests
{
    private static ItemPools BuildPoolsWithObjects(params (string id, string name, int cat)[] items)
        => BuildPoolsWithObjects(
            items.Select(i => (i.id, i.name, i.cat, tags: (string[]?)null)).ToArray());

    private static ItemPools BuildPoolsWithObjects(params (string id, string name, int cat, string[]? tags)[] items)
    {
        var objects = items.ToDictionary(
            i => i.id,
            i => new RawObjectEntry("Basic", i.cat, 50, false, i.tags ?? Array.Empty<string>(), i.name));
        return ItemPoolBuilder.Build(
            new List<RawCropEntry>(), objects, new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(), new List<RawFruitTreeEntry>(),
            new List<RawGeodeDropEntry>(), Tuning);
    }

    [Fact]
    public void Every_vetted_object_lands_in_its_kind_pool()
    {
        ItemPools pools = BuildPoolsWithObjects(
            ("388", "Wood", cat: -16), ("176", "Egg", cat: -5), ("184", "Milk", cat: -6),
            ("72", "Diamond", cat: -2), ("681", "Rain Totem", cat: 0), ("768", "Solar Essence", cat: -28));
        Assert.Contains(pools.ByKind[ItemKind.Resource], p => p.ItemId == "(O)388");
        Assert.Contains(pools.ByKind[ItemKind.Egg], p => p.ItemId == "(O)176");
        Assert.Contains(pools.ByKind[ItemKind.Gem], p => p.ItemId == "(O)72");
        Assert.Contains(pools.ByKind[ItemKind.Totem], p => p.ItemId == "(O)681");
        Assert.Contains(pools.ByKind[ItemKind.MonsterLoot], p => p.ItemId == "(O)768");
    }

    [Fact]
    public void Colour_tags_index_items_by_colour()
    {
        ItemPools pools = BuildPoolsWithObjects(("420", "Red Mushroom", cat: -81, tags: new[] { "color_red" }));
        Assert.Contains(pools.ColourTags["color_red"], p => p.ItemId == "(O)420");
    }

    [Fact]
    public void ByKind_Trophy_ComesFromGilTrophiesFixedList_NotDataObjects()
    {
        ItemPools pools = BuildPoolsWithObjects(("522", "Some Ring", cat: 0));
        var trophies = pools.ByKind[ItemKind.Trophy].Select(p => p.ItemId).ToList();
        foreach (string id in AuthoredBundleCatalog.GilTrophies)
            Assert.Contains(id, trophies);
        Assert.All(pools.ByKind[ItemKind.Trophy], p => Assert.Equal(3, p.Weight));
    }

    [Fact]
    public void WinterOnly_HoldsItemsWhoseCatalogSeasonsAreExactlyWinter()
    {
        var pools = Build(
            objects: Objects(("412", Obj(category: -81, price: 70)), ("420", Obj(category: -81))),
            forage: new[]
            {
                new RawSpawnEntry("(O)412", Season.Winter, null, "Forest"),
                new RawSpawnEntry("(O)420", Season.Spring, null, "Forest"),
            });
        Assert.Contains(pools.WinterOnly, p => p.ItemId == "(O)412");
        Assert.DoesNotContain(pools.WinterOnly, p => p.ItemId == "(O)420");
    }

    [Fact]
    public void ColourTags_skip_rings_so_one_never_reaches_the_Dye_recipe()
    {
        // Vanilla tags the Amethyst Ring color_purple (Nexus, 2026-09-14); a ring cannot be donated.
        ItemPools pools = BuildPoolsWithObjects(
            ("529", "Amethyst Ring", cat: 0, tags: new[] { "color_purple", "ring_item" }),
            ("254", "Melon", cat: -79, tags: new[] { "color_purple" }));
        Assert.Contains(pools.ColourTags["color_purple"], p => p.ItemId == "(O)254");
        Assert.DoesNotContain(pools.ColourTags["color_purple"], p => p.ItemId == "(O)529");
    }

    [Fact]
    public void ColourTags_skip_books_so_Dye_never_asks_for_one()
    {
        // The Queen of Sauce Cookbook (color_blue, 100 golden walnuts) reached a Dye bundle (SilviaVA,
        // Nexus, 2026-09-17), and a streamer's board asked for Woodcutter's Weekly (2026-09-29).
        ItemPools pools = BuildPoolsWithObjects(
            ("Book_QueenOfSauce", "Queen Of Sauce Cookbook", cat: -102, tags: new[] { "color_blue", "book_item" }),
            ("SkillBook_1", "Bait And Bobber", cat: -103, tags: new[] { "color_blue", "book_item" }),
            ("372", "Clam", cat: -23, tags: new[] { "color_blue" }));
        Assert.Contains(pools.ColourTags["color_blue"], p => p.ItemId == "(O)372");
        Assert.DoesNotContain(pools.ColourTags["color_blue"], p => p.ItemId == "(O)SkillBook_1");
        Assert.DoesNotContain(pools.ColourTags["color_blue"], p => p.ItemId == "(O)Book_QueenOfSauce");
    }

    /// <summary>Dye picks from the six vanilla Dye items, the six common gems, and coloured crops,
    /// fruit, flowers, forage and beach finds only (Jeff, 2026-09-29). Diamond, Prismatic Shard and
    /// the mineral-type gems (Quartz and the like) stay out. 60 boards had asked for dishes, artifacts,
    /// bombs, books, Joja Cola and Energy Tonic.</summary>
    [Fact]
    public void ColourTags_hold_only_vanilla_dye_items_and_grown_or_gathered_things()
    {
        ItemPools pools = BuildPoolsWithObjects(
            ("444", "Duck Feather", cat: -18, tags: new[] { "color_white" }),
            ("62", "Aquamarine", cat: -2, tags: new[] { "color_blue" }),
            ("190", "Cauliflower", cat: -75, tags: new[] { "color_white" }),
            ("613", "Apple", cat: -79, tags: new[] { "color_red" }),
            ("421", "Sunflower", cat: -80, tags: new[] { "color_yellow" }),
            ("18", "Daffodil", cat: -81, tags: new[] { "color_yellow" }),
            ("397", "Sea Urchin", cat: -23, tags: new[] { "color_purple" }),
            ("204", "Lucky Lunch", cat: -7, tags: new[] { "color_yellow" }),
            ("583", "Prehistoric Rib", cat: 0, tags: new[] { "color_white" }),
            ("286", "Cherry Bomb", cat: -8, tags: new[] { "color_red" }),
            ("446", "Rabbit's Foot", cat: -18, tags: new[] { "color_white" }),
            ("66", "Amethyst", cat: -2, tags: new[] { "color_purple" }),
            ("68", "Topaz", cat: -2, tags: new[] { "color_gold" }),
            ("72", "Diamond", cat: -2, tags: new[] { "color_white" }),
            ("80", "Quartz", cat: -2, tags: new[] { "color_white" }),
            ("84", "Frozen Tear", cat: -2, tags: new[] { "color_cyan" }),
            ("74", "Prismatic Shard", cat: -2, tags: new[] { "color_prismatic" }),
            ("86", "Earth Crystal", cat: -2, tags: new[] { "color_copper" }),
            ("130", "Tuna", cat: -4, tags: new[] { "color_blue" }));
        var all = pools.ColourTags.Values.SelectMany(l => l).Select(p => p.ItemId).ToHashSet();

        foreach (string kept in new[] { "(O)444", "(O)62", "(O)190", "(O)613", "(O)421", "(O)18", "(O)397", "(O)66", "(O)68", "(O)80", "(O)84" })
            Assert.Contains(kept, all);
        foreach (string dropped in new[] { "(O)204", "(O)583", "(O)286", "(O)446", "(O)72", "(O)74", "(O)86", "(O)130" })
            Assert.DoesNotContain(dropped, all);
    }

    /// <summary>Mystic Syrup comes only from a tapper on the Mystic Tree, whose seed is the Foraging
    /// Mastery reward (every skill at 10, then 10,000 XP). A one-year loop never reaches this; exclude it
    /// from every bundle pool (Jeff, 2026-09-30).</summary>
    [Fact]
    public void Mystic_Syrup_is_excluded_from_every_pool()
    {
        // Its seed is the Foraging Mastery crafting recipe (all five skills at 10, then 10,000 XP);
        // the syrup only comes from a tapper on that tree (Jeff, 2026-09-30: remove it).
        Assert.Contains("(O)MysticSyrup", ItemPoolBuilder.BuiltInExcludedItemIds);
    }
}
