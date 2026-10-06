using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class StashNestingTests
{
    private static StashItemRecord R(string id, List<StashItemRecord>? contents = null, StashItemRecord? held = null)
        => new(id, 1, 0, Contents: contents, HeldObject: held);

    [Theory]
    [InlineData("(H)0")]                 // hat
    [InlineData("(S)1000")]              // shirt
    [InlineData("(P)0")]                 // pants
    [InlineData("(F)704")]               // furniture (a dresser)
    [InlineData("(F)Mod.TallBeechDresser")]
    [InlineData("(WP)12")]               // wallpaper
    [InlineData("(FL)3")]                // flooring
    public void Cosmetic_types_are_cosmetic(string id) => Assert.True(StashNesting.IsCosmetic(id));

    [Theory]
    [InlineData("(B)504")]               // boots
    [InlineData("(O)529")]               // ring (rings are objects)
    [InlineData("(O)880")]               // combined ring
    [InlineData("(TR)ParrotEgg")]        // trinket
    [InlineData("(O)128")]               // fish
    [InlineData("(O)405")]               // wood path item (craftable decoration)
    [InlineData("(O)93")]                // torch
    [InlineData("(BC)130")]              // chest
    [InlineData("(BC)152")]              // lamp-post
    [InlineData("(T)IridiumRod")]        // tool
    [InlineData("(W)4")]                 // weapon
    [InlineData("(M)Mannequin")]         // mannequin
    [InlineData("(F)sonofskywalker3.TheLongestYear_PlanningShrine")]   // TLY's own furniture
    [InlineData("")]
    public void Everything_else_is_not_cosmetic(string id) => Assert.False(StashNesting.IsCosmetic(id));

    [Fact]
    public void Empty_dresser_can_be_stashed() => Assert.True(StashNesting.CanStash(R("(F)704")));

    [Fact]
    public void Dresser_of_hats_and_clothes_can_be_stashed()
        => Assert.True(StashNesting.CanStash(R("(F)704", new() { R("(H)0"), R("(S)1000"), R("(P)0"), R("(WP)12") })));

    [Fact]
    public void Dresser_with_a_ring_is_refused_and_names_it()
    {
        var dresser = R("(F)704", new() { R("(H)0"), R("(O)529") });
        Assert.False(StashNesting.CanStash(dresser));
        Assert.Equal(new[] { "(O)529" }, StashNesting.NonCosmeticNested(dresser));
    }

    [Fact]
    public void Depth_two_is_checked()
    {
        var inner = R("(F)709", new() { R("(B)504") });
        Assert.Equal(new[] { "(B)504" }, StashNesting.NonCosmeticNested(R("(F)704", new() { R("(H)0"), inner })));
        Assert.True(StashNesting.CanStash(R("(F)704", new() { R("(F)709", new() { R("(H)0") }) })));
    }

    [Fact]
    public void Held_object_is_checked()
    {
        Assert.False(StashNesting.CanStash(R("(F)1120", held: R("(O)24"))));
        Assert.True(StashNesting.CanStash(R("(F)1120", held: R("(F)1376"))));
    }

    [Fact]
    public void Rod_attachments_are_exempt()
    {
        var rod = new StashItemRecord("(T)IridiumRod", 1, 0,
            Attachments: new List<StashItemRecord?> { new("(O)685", 20, 0), new("(O)686", 1, 0) });
        Assert.True(StashNesting.CanStash(rod));
    }

    [Fact]
    public void Combined_ring_inner_rings_are_exempt()
    {
        var ring = new StashItemRecord("(O)880", 1, 0,
            InnerRings: new List<StashItemRecord> { new("(O)529", 1, 0), new("(O)530", 1, 0) });
        Assert.True(StashNesting.CanStash(ring));
    }

    [Fact]
    public void A_non_container_top_level_item_is_never_refused()
    {
        Assert.True(StashNesting.CanStash(R("(O)529")));
        Assert.True(StashNesting.CanStash(R("(B)504")));
        Assert.True(StashNesting.CanStash(R("(BC)130")));
    }

    [Fact]
    public void Trim_keeps_cosmetics_and_ejects_the_rest()
    {
        var ejected = new List<StashItemRecord>();
        StashItemRecord trimmed = StashNesting.Trim(R("(F)704", new() { R("(H)0"), R("(O)529"), R("(S)1000") }), ejected);

        Assert.Equal(new[] { "(H)0", "(S)1000" }, trimmed.Contents!.Select(c => c.ItemId));
        Assert.Equal(new[] { "(O)529" }, ejected.Select(e => e.ItemId));
    }

    [Fact]
    public void Trim_recurses_into_ejected_and_kept_children()
    {
        // Dresser > [ inner dresser > [hat, boots], chest item > [hat, fish] ]
        var inner = R("(F)709", new() { R("(H)0"), R("(B)504") });
        var chest = R("(BC)130", new() { R("(H)2"), R("(O)128") });
        var ejected = new List<StashItemRecord>();

        StashItemRecord trimmed = StashNesting.Trim(R("(F)704", new() { inner, chest }), ejected);

        Assert.Single(trimmed.Contents!);                                       // only the inner dresser stays
        Assert.Equal(new[] { "(H)0" }, trimmed.Contents![0].Contents!.Select(c => c.ItemId));
        Assert.Equal(new[] { "(B)504", "(O)128", "(BC)130" }.OrderBy(x => x), ejected.Select(e => e.ItemId).OrderBy(x => x));
        StashItemRecord ejectedChest = ejected.Single(e => e.ItemId == "(BC)130");
        Assert.Equal(new[] { "(H)2" }, ejectedChest.Contents!.Select(c => c.ItemId));   // the chest keeps its hat
        Assert.True(StashNesting.CanStash(trimmed));
        Assert.All(ejected, e => Assert.True(StashNesting.CanStash(e)));
    }

    [Fact]
    public void Trim_ejects_a_non_cosmetic_held_object()
    {
        var ejected = new List<StashItemRecord>();
        StashItemRecord trimmed = StashNesting.Trim(R("(F)1120", held: R("(O)24")), ejected);
        Assert.Null(trimmed.HeldObject);
        Assert.Equal("(O)24", ejected.Single().ItemId);
    }

    [Fact]
    public void Trim_leaves_a_plain_record_untouched()
    {
        var ejected = new List<StashItemRecord>();
        StashItemRecord rod = new("(T)IridiumRod", 1, 0, Attachments: new List<StashItemRecord?> { null, null });
        StashItemRecord trimmed = StashNesting.Trim(rod, ejected);
        Assert.Null(trimmed.Contents);
        Assert.Equal(2, trimmed.Attachments!.Count);
        Assert.Empty(ejected);
    }
}

public class StashLegacyRescueTests
{
    [Fact]
    public void Replaces_a_legacy_record_whose_live_item_has_contents()
    {
        var stored = new StashItemRecord("(F)704", 1, 0);
        var live = new StashItemRecord("(F)704", 1, 0, Contents: new() { new("(H)0", 1, 0) });
        Assert.True(StashLegacyRescue.ShouldReplace(stored, live));
    }

    [Fact]
    public void Replaces_a_legacy_record_missing_dye_or_trinket_seed()
    {
        Assert.True(StashLegacyRescue.ShouldReplace(new("(S)1000", 1, 0),
            new("(S)1000", 1, 0, Clothing: new StashClothingRecord(1u, true))));
        Assert.True(StashLegacyRescue.ShouldReplace(new("(TR)ParrotEgg", 1, 0),
            new("(TR)ParrotEgg", 1, 0, TrinketSeed: 5)));
    }

    [Fact]
    public void Leaves_a_different_item_or_a_complete_record_alone()
    {
        var live = new StashItemRecord("(F)704", 1, 0, Contents: new() { new("(H)0", 1, 0) });
        Assert.False(StashLegacyRescue.ShouldReplace(new("(F)709", 1, 0), live));
        Assert.False(StashLegacyRescue.ShouldReplace(live, live));
        Assert.False(StashLegacyRescue.ShouldReplace(new("(O)388", 50, 0), new("(O)388", 50, 0)));
    }
}
