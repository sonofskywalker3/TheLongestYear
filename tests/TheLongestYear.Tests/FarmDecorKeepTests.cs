using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class FarmDecorKeepTests
{
    [Theory]
    [InlineData(FarmThingKind.Flooring, "(FL)ignored")]
    [InlineData(FarmThingKind.Fence, "(O)322")]
    [InlineData(FarmThingKind.Fence, "(O)325")]          // gate
    [InlineData(FarmThingKind.Torch, "(O)93")]
    [InlineData(FarmThingKind.Torch, "(BC)146")]         // campfire
    [InlineData(FarmThingKind.Sign, "(BC)37")]
    [InlineData(FarmThingKind.BigCraftable, "(BC)152")]  // wood lamp-post
    [InlineData(FarmThingKind.BigCraftable, "(BC)153")]  // iron lamp-post
    [InlineData(FarmThingKind.BigCraftable, "(BC)108")]  // tub o' flowers
    [InlineData(FarmThingKind.BigCraftable, "(BC)TextSign")]
    [InlineData(FarmThingKind.BigCraftable, "(BC)94")]   // singing stone (Jeff: may stay)
    public void Spec_decor_is_kept(FarmThingKind kind, string id) => Assert.True(FarmDecorKeep.IsKeptDecor(kind, id));

    [Theory]
    [InlineData(FarmThingKind.BigCraftable, "(BC)12")]   // keg (machine)
    [InlineData(FarmThingKind.BigCraftable, "(BC)117")]  // soda machine (Data/Machines)
    [InlineData(FarmThingKind.BigCraftable, "(BC)8")]    // scarecrow
    [InlineData(FarmThingKind.BigCraftable, "(BC)110")]  // rarecrow
    [InlineData(FarmThingKind.BigCraftable, "(BC)130")]  // chest
    [InlineData(FarmThingKind.BigCraftable, "(BC)62")]   // garden pot (grows crops)
    [InlineData(FarmThingKind.BigCraftable, "(BC)127")]  // statue of endless fortune (produces)
    [InlineData(FarmThingKind.Object, "(O)599")]         // sprinkler
    [InlineData(FarmThingKind.Object, "(O)24")]          // a crop item left on the ground
    [InlineData(FarmThingKind.Furniture, "(F)sonofskywalker3.TheLongestYear_PlanningShrine")]
    public void Machines_crops_sprinklers_scarecrows_chests_are_never_kept(FarmThingKind kind, string id)
        => Assert.False(FarmDecorKeep.IsKeptDecor(kind, id));

    // Jeff, 2026-10-01: Keep Farm Decor keeps no furniture of any kind.
    [Theory]
    [InlineData("(F)1120")]   // outdoor table
    [InlineData("(F)0")]      // oak chair
    [InlineData("(F)1376")]   // furniture lamp
    [InlineData("(F)704")]    // dresser
    [InlineData("(F)sonofskywalker3.TheLongestYear_PlanningShrine")]
    public void Furniture_is_never_kept(string id) => Assert.False(FarmDecorKeep.IsKeptDecor(FarmThingKind.Furniture, id));

    // Round 2 audit (Jeff, 2026-10-01): big craftables with a function are not decor.
    [Theory]
    [InlineData("(BC)56")]    // slime ball: breaks for slime
    [InlineData("(BC)83")]    // wicked statue: keeps the witch out
    [InlineData("(BC)118")]   // barrel: breakable loot container
    [InlineData("(BC)119")]   // crate
    [InlineData("(BC)120")]
    [InlineData("(BC)121")]
    [InlineData("(BC)122")]
    [InlineData("(BC)123")]
    [InlineData("(BC)124")]
    [InlineData("(BC)125")]
    [InlineData("(BC)174")]
    [InlineData("(BC)175")]
    [InlineData("(BC)262")]
    [InlineData("(BC)263")]
    [InlineData("(BC)141")]   // prairie king arcade
    [InlineData("(BC)159")]   // junimo kart arcade
    [InlineData("(BC)219")]   // cursed p.k. arcade
    public void Big_craftables_with_a_function_are_never_kept(string id)
        => Assert.False(FarmDecorKeep.IsKeptDecor(FarmThingKind.BigCraftable, id));

    // ----- clump rules: tool tier x debris type -----

    [Theory]
    [InlineData(600, 0, 0, false)]
    [InlineData(600, 1, 0, true)]     // copper axe: stump
    [InlineData(602, 1, 4, false)]
    [InlineData(602, 2, 0, true)]     // steel axe: hollow log
    [InlineData(672, 4, 1, false)]
    [InlineData(672, 0, 2, true)]     // steel pickaxe: boulder
    [InlineData(622, 0, 2, false)]
    [InlineData(622, 0, 3, true)]
    [InlineData(752, 0, 0, true)]
    [InlineData(44, 4, 4, false)]     // unknown clump: never broken
    public void CanBreak_follows_the_kept_tool_tier(int index, int axe, int pick, bool expected)
        => Assert.Equal(expected, FarmDecorKeep.CanBreak(index, axe, pick));

    [Theory]
    [InlineData(600, 2)]
    [InlineData(602, 8)]
    [InlineData(672, 0)]
    public void Hardwood_drops_match_vanilla(int index, int hardwood)
        => Assert.Equal(hardwood, FarmDecorKeep.RuleFor(index)!.HardwoodDrop);

    // ----- planner -----

    private static IReadOnlyList<DecorTile> T(params (int x, int y)[] tiles) => tiles.Select(t => new DecorTile(t.x, t.y)).ToList();
    private static DecorClump Clump(int id, int index, int x, int y) => new(id, index, T((x, y), (x + 1, y), (x, y + 1), (x + 1, y + 1)));
    private static TileBlock Open(int x, int y) => TileBlock.None;

    [Fact]
    public void Path_under_a_stump_with_a_copper_axe_clears_it_for_two_hardwood()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((10, 10)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 10, 10) }, Open, axeTier: 1, pickaxeTier: 0);
        Assert.Equal(new[] { 1 }, plan.Placed);
        Assert.Equal(new[] { new ClearedClump(7, 2) }, plan.ClearedClumps);
    }

    [Fact]
    public void Path_under_a_stump_with_the_basic_axe_goes_to_the_stash_and_the_stump_stays()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((10, 10)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 10, 10) }, Open, axeTier: 0, pickaxeTier: 0);
        Assert.Empty(plan.Placed);
        Assert.Equal(new[] { 1 }, plan.Displaced);
        Assert.Empty(plan.ClearedClumps);
    }

    [Fact]
    public void Boulder_cleared_with_a_steel_pickaxe_drops_nothing()
    {
        var fence = new DecorPiece(1, DecorLayer.Object, T((5, 6)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { fence }, new[] { Clump(3, 672, 5, 5) }, Open, 0, 2);
        Assert.Equal(new[] { new ClearedClump(3, 0) }, plan.ClearedClumps);
    }

    [Fact]
    public void Clumps_away_from_decor_are_left_alone()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((0, 0)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 20, 20) }, Open, 4, 4);
        Assert.Empty(plan.ClearedClumps);
        Assert.Equal(new[] { 1 }, plan.Placed);
    }

    [Fact]
    public void One_clump_under_two_pieces_is_cleared_once()
    {
        var a = new DecorPiece(1, DecorLayer.Ground, T((10, 10)));
        var b = new DecorPiece(2, DecorLayer.Ground, T((11, 11)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { a, b }, new[] { Clump(7, 600, 10, 10) }, Open, 1, 0);
        Assert.Single(plan.ClearedClumps);
        Assert.Equal(new[] { 1, 2 }, plan.Placed);
    }

    [Fact]
    public void A_breakable_clump_is_not_cleared_for_a_piece_the_stash_takes_anyway()
    {
        // A 2x1 bench over a stump (breakable) and a hollow log (not breakable with a copper axe).
        var bench = new DecorPiece(1, DecorLayer.Object, T((10, 10), (12, 10)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { bench },
            new[] { Clump(7, 600, 10, 10), Clump(8, 602, 12, 10) }, Open, axeTier: 1, pickaxeTier: 0);
        Assert.Equal(new[] { 1 }, plan.Displaced);
        Assert.Empty(plan.ClearedClumps);
    }

    [Fact]
    public void A_clump_overlapping_only_on_a_non_anchor_tile_still_counts()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((11, 11)));
        DecorPlan cleared = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 10, 10) }, Open, 1, 0);
        Assert.Equal(new[] { new ClearedClump(7, 2) }, cleared.ClearedClumps);
        DecorPlan kept = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(7, 600, 10, 10) }, Open, 0, 0);
        Assert.Equal(new[] { 1 }, kept.Displaced);
    }

    [Fact]
    public void A_high_axe_tier_does_not_break_a_boulder()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((5, 5)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new[] { Clump(3, 672, 5, 5) }, Open, axeTier: 4, pickaxeTier: 1);
        Assert.Equal(new[] { 1 }, plan.Displaced);
        Assert.Empty(plan.ClearedClumps);
    }

    [Fact]
    public void A_kept_building_wins_over_decor()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((3, 3)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new DecorClump[0],
            (x, y) => x == 3 && y == 3 ? TileBlock.Building : TileBlock.None, 0, 0);
        Assert.Equal(new[] { 1 }, plan.Displaced);
    }

    [Fact]
    public void Off_map_tiles_send_decor_to_the_stash()
    {
        var torch = new DecorPiece(1, DecorLayer.Object, T((-1, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { torch }, new DecorClump[0],
            (x, y) => x < 0 ? TileBlock.OffMap : TileBlock.None, 0, 0);
        Assert.Equal(new[] { 1 }, plan.Displaced);
    }

    [Fact]
    public void Another_object_blocks_an_object_but_not_a_path_under_it()
    {
        // e.g. the stash chest or planning shrine now stands on that tile.
        var path = new DecorPiece(1, DecorLayer.Ground, T((4, 4)));
        var sign = new DecorPiece(2, DecorLayer.Object, T((4, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path, sign }, new DecorClump[0],
            (x, y) => TileBlock.OtherObject, 0, 0);
        Assert.Equal(new[] { 1 }, plan.Placed);
        Assert.Equal(new[] { 2 }, plan.Displaced);
    }

    [Fact]
    public void Small_debris_under_kept_decor_is_listed_for_clearing_once()
    {
        // Weeds, twigs, stones, grass, saplings, trees and bushes all report SmallDebris.
        var path = new DecorPiece(1, DecorLayer.Ground, T((4, 4)));
        var fence = new DecorPiece(2, DecorLayer.Object, T((4, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path, fence }, new DecorClump[0],
            (x, y) => TileBlock.SmallDebris, 0, 0);
        Assert.Equal(new[] { 1, 2 }, plan.Placed);
        Assert.Equal(new[] { new DecorTile(4, 4) }, plan.DebrisTilesToClear);
    }

    [Fact]
    public void Same_fresh_fence_is_swapped_not_displaced()
    {
        // Meadowlands and mod farms spawn their own Wood Fences: the kept fence replaces the fresh
        // one instead of going to the stash and leaving a duplicate on the farm every loop.
        var fence = new DecorPiece(1, DecorLayer.Object, T((4, 4)), "(O)322");
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { fence }, new DecorClump[0],
            (x, y) => TileBlock.FreshObject, 0, 0, (x, y) => "(O)322");
        Assert.Equal(new[] { 1 }, plan.Placed);
        Assert.Empty(plan.Displaced);
        Assert.Equal(new[] { new DecorTile(4, 4) }, plan.SameObjectTilesToSwap);
    }

    [Fact]
    public void Different_fresh_object_still_displaces()
    {
        var fence = new DecorPiece(1, DecorLayer.Object, T((4, 4)), "(O)298");
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { fence }, new DecorClump[0],
            (x, y) => TileBlock.FreshObject, 0, 0, (x, y) => "(O)322");
        Assert.Empty(plan.Placed);
        Assert.Equal(new[] { 1 }, plan.Displaced);
        Assert.Empty(plan.SameObjectTilesToSwap);
    }

    [Fact]
    public void Same_id_but_furniture_or_stash_on_the_tile_still_displaces()
    {
        // OtherObject (furniture, the stash chest) is never swapped, even with a matching fresh id.
        var fence = new DecorPiece(1, DecorLayer.Object, T((4, 4)), "(O)322");
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { fence }, new DecorClump[0],
            (x, y) => TileBlock.FreshObject | TileBlock.OtherObject, 0, 0, (x, y) => "(O)322");
        Assert.Equal(new[] { 1 }, plan.Displaced);
        Assert.Empty(plan.SameObjectTilesToSwap);
    }

    [Fact]
    public void Same_id_swap_is_dropped_when_the_piece_is_displaced_by_unbreakable_debris()
    {
        var fence = new DecorPiece(1, DecorLayer.Object, T((5, 5)), "(O)322");
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { fence }, new[] { Clump(3, 672, 5, 5) },
            (x, y) => TileBlock.FreshObject, 0, 0, (x, y) => "(O)322");
        Assert.Equal(new[] { 1 }, plan.Displaced);
        Assert.Empty(plan.SameObjectTilesToSwap);
    }

    [Fact]
    public void Fresh_object_without_ids_blocks_like_any_object()
    {
        var fence = new DecorPiece(1, DecorLayer.Object, T((4, 4)));
        var path = new DecorPiece(2, DecorLayer.Ground, T((4, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { fence, path }, new DecorClump[0],
            (x, y) => TileBlock.FreshObject, 0, 0);
        Assert.Equal(new[] { 2 }, plan.Placed);
        Assert.Equal(new[] { 1 }, plan.Displaced);
    }

    [Fact]
    public void Debris_under_displaced_decor_is_left_alone()
    {
        var path = new DecorPiece(1, DecorLayer.Ground, T((4, 4)));
        DecorPlan plan = FarmDecorPlanner.Plan(new[] { path }, new DecorClump[0],
            (x, y) => TileBlock.SmallDebris | TileBlock.Building, 0, 0);
        Assert.Empty(plan.DebrisTilesToClear);
    }
}

[Collection("i18n")]
public class FarmDecorKeepCatalogTests
{
    [Fact]
    public void Catalog_row_is_one_level_500_jp_in_buildings()
    {
        UpgradeDefinition row = UpgradeCatalog.All.Single(u => u.Id == FarmDecorKeep.UpgradeId);
        Assert.Equal(500L, row.Cost);
        Assert.Equal(UpgradeCategory.Buildings, row.Category);
        Assert.Null(row.PrerequisiteId);
        Assert.Null(row.RunReachRequirement);
    }
}
