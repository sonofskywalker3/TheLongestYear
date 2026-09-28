using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Keep Fish Pond (elaineofshalott, Nexus 2026-09-27): one empty pond back in its spot.</summary>
public class FishPondKeepTests
{
    [Fact]
    public void Catalog_entry_is_a_buildings_keep_gated_on_building_a_fish_pond()
    {
        UpgradeDefinition? pond = UpgradeCatalog.TryGet(FishPondKeep.UpgradeId);
        Assert.NotNull(pond);
        Assert.Equal("keep_fish_pond", pond!.Id);
        Assert.Equal(UpgradeCategory.Buildings, pond.Category);
        Assert.Equal(750L, pond.Cost);
        Assert.Equal("building:Fish Pond", pond.RunReachRequirement);
        Assert.Null(pond.PrerequisiteId);
    }

    [Fact]
    public void Reach_gate_parses_as_a_building_requirement()
    {
        RunReachRequirement? r = RunReachRequirement.Parse("building:Fish Pond");
        Assert.NotNull(r);
        Assert.Equal("building", r!.Metric);
        Assert.Equal("Fish Pond", r.Key);
    }

    [Fact]
    public void Owned_keep_lands_in_kept_buildings()
    {
        var meta = new MetaState { OwnedUpgrades = { FishPondKeep.UpgradeId } };
        RunBaseline b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Contains(FishPondKeep.BuildingType, b.KeptBuildings);

        RunBaseline without = RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500);
        Assert.DoesNotContain(FishPondKeep.BuildingType, without.KeptBuildings);
    }

    [Fact]
    public void Pick_returns_null_when_there_is_no_pond()
    {
        Assert.Null(FishPondKeep.PickKeptPond(Array.Empty<int>()));
    }

    [Fact]
    public void Pick_prefers_the_pond_with_the_most_fish()
    {
        Assert.Equal(2, FishPondKeep.PickKeptPond(new[] { 3, 0, 7, 5 }));
    }

    [Fact]
    public void Pick_breaks_ties_on_the_first_pond()
    {
        Assert.Equal(0, FishPondKeep.PickKeptPond(new[] { 0, 0, 0 }));
        Assert.Equal(1, FishPondKeep.PickKeptPond(new[] { 2, 4, 4 }));
    }

    [Fact]
    public void Resolve_uses_the_remembered_spot_when_it_is_free()
    {
        BuildingSpot? spot = FishPondKeep.ResolveSpot(new BuildingSpot(20, 30), (_, _) => true);
        Assert.Equal(new BuildingSpot(20, 30), spot);
    }

    [Fact]
    public void Resolve_falls_back_when_no_spot_is_remembered()
    {
        BuildingSpot? spot = FishPondKeep.ResolveSpot(null, (_, _) => true);
        Assert.Equal(FishPondKeep.FallbackSpot, spot);
    }

    [Fact]
    public void Resolve_falls_back_when_the_remembered_spot_is_blocked_on_any_footprint_tile()
    {
        // One blocked tile in the bottom-right corner of the remembered 5x5.
        bool Free(int x, int y) => !(x == 24 && y == 34);
        BuildingSpot? spot = FishPondKeep.ResolveSpot(new BuildingSpot(20, 30), Free);
        Assert.Equal(FishPondKeep.FallbackSpot, spot);
    }

    [Fact]
    public void Resolve_searches_near_the_fallback_when_it_is_blocked_too()
    {
        BuildingSpot fb = FishPondKeep.FallbackSpot;
        // Everything left of fallback.X + 3 is blocked; the nearest free 5x5 starts at X + 3.
        bool Free(int x, int y) => x >= fb.X + 3;
        BuildingSpot? spot = FishPondKeep.ResolveSpot(null, Free);
        Assert.NotNull(spot);
        Assert.Equal(fb.X + 3, spot!.X);
        Assert.True(FishPondKeep.FootprintFree(spot, Free));
    }

    [Fact]
    public void Resolve_returns_null_when_nothing_fits()
    {
        Assert.Null(FishPondKeep.ResolveSpot(new BuildingSpot(20, 30), (_, _) => false));
    }

    [Fact]
    public void Footprint_is_five_by_five()
    {
        var seen = new List<(int, int)>();
        FishPondKeep.FootprintFree(new BuildingSpot(10, 10), (x, y) => { seen.Add((x, y)); return true; });
        Assert.Equal(25, seen.Count);
        Assert.Contains((14, 14), seen);
        Assert.DoesNotContain((15, 10), seen);
    }
}
