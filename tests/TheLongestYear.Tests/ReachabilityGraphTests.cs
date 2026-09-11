using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

public class ReachabilityGraphTests
{
    private static readonly string[] Everything =
    {
        "Farm", "Town", "Beach", "IslandSouth", "FishmongerShop", "Desert", "BusStop",
    };

    private static readonly RawLocationLink[] Links =
    {
        new("Farm", "Town"),
        new("Town", "Beach"),
        new("Town", "BusStop"),
        new("BusStop", "Desert"),
        new("Beach", "IslandSouth"),
        new("IslandSouth", "FishmongerShop"),
    };

    private static bool IslandForbidden(string name) => name.Contains("Island", StringComparison.Ordinal);

    [Fact]
    public void Forbidden_location_is_unreachable()
    {
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden);
        Assert.Contains("IslandSouth", unreachable);
    }

    [Fact]
    public void Location_reachable_only_through_a_forbidden_place_is_unreachable()
    {
        // The Fishmonger shop: its one door leads to IslandSouth, and its NAME matches no marker,
        // which is the whole point. The real map is called VoidWitchCult.TheFishmonger_Fishmonger_GI_Inside.
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden);
        Assert.Contains("FishmongerShop", unreachable);
    }

    [Fact]
    public void Ordinary_locations_stay_reachable()
    {
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden);
        Assert.DoesNotContain("Town", unreachable);
        Assert.DoesNotContain("Beach", unreachable);
    }

    [Fact]
    public void Location_behind_a_gate_that_opens_during_the_year_stays_reachable()
    {
        // The bus is broken on Spring 1, but the Desert is still connected to the world, so
        // Cactus Fruit must remain a legal board target (BundleCatalogBuilder's standing ruling).
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden);
        Assert.DoesNotContain("Desert", unreachable);
    }

    [Fact]
    public void Second_door_to_the_world_keeps_a_location_reachable()
    {
        var links = new List<RawLocationLink>(Links) { new("FishmongerShop", "Town") };
        var unreachable = ReachabilityGraph.UnreachableLocations(links, Everything, IslandForbidden);
        Assert.DoesNotContain("FishmongerShop", unreachable);
    }

    [Fact]
    public void Warps_are_followed_in_both_directions()
    {
        // Vanilla interiors often declare the warp only on one side.
        var links = new[] { new RawLocationLink("SeedShop", "Town"), new RawLocationLink("Farm", "Town") };
        var all = new[] { "Farm", "Town", "SeedShop" };
        var unreachable = ReachabilityGraph.UnreachableLocations(links, all, _ => false);
        Assert.Empty(unreachable);
    }

    [Fact]
    public void Cycles_terminate()
    {
        var links = new[]
        {
            new RawLocationLink("Farm", "Town"), new RawLocationLink("Town", "Farm"),
            new RawLocationLink("Town", "Beach"), new RawLocationLink("Beach", "Town"),
        };
        var unreachable = ReachabilityGraph.UnreachableLocations(links, new[] { "Farm", "Town", "Beach" }, _ => false);
        Assert.Empty(unreachable);
    }

    [Fact]
    public void Location_with_no_doors_at_all_is_unknown_not_unreachable()
    {
        // Verified in-game 2026-09-10: MovieTheater, WizardHouseBasement and LewisBasement are
        // loaded with zero warps. A map with no doors is evidence of our ignorance about how it
        // is entered, never proof that a player cannot get there.
        var all = new[] { "Farm", "Town", "MovieTheater" };
        var links = new[] { new RawLocationLink("Farm", "Town") };
        var unreachable = ReachabilityGraph.UnreachableLocations(links, all, _ => false);
        Assert.DoesNotContain("MovieTheater", unreachable);
    }

    [Fact]
    public void Island_only_world_does_not_strip_everything_when_start_is_missing()
    {
        // Fail open: an unknown start location must not mark the whole world unreachable.
        var unreachable = ReachabilityGraph.UnreachableLocations(Links, Everything, IslandForbidden, start: "NoSuchPlace");
        Assert.Empty(unreachable);
    }
}
