using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Availability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Mod-support work, 2026-10-08: SVE's Boomerang, Old Coin, Rusty Shield and Stone of
/// Yoba are artifacts whose dig spots live in their own Data/Objects ArtifactSpotChances, and they
/// carry ExcludeFromRandomSale, so the catalog-pool fallback skipped them too: unknown, week 13.</summary>
public class OwnDigSpotArtifactTests
{
    private const string Boomerang = "(O)FlashShifter.StardewValleyExpandedCP_Boomerang";
    private const string Shield = "(O)FlashShifter.StardewValleyExpandedCP_Rusty_Shield";

    private static LocationWeeks Walk() => LocationWeeks.Build(
        new[] { new RawLocationLink("Farm", "Forest"), new RawLocationLink("Farm", "Backwoods"), new RawLocationLink("Backwoods", "Mountain") },
        _ => false);

    private static EffortData Data(params RawArtifactSpot[] own) => new()
    {
        Objects = new Dictionary<string, RawObjectEntry>
        {
            [Boomerang.Substring(3)] = new("Arch", 0, 300, true, new string[0]),
            [Shield.Substring(3)] = new("Arch", 0, 300, true, new string[0]),
            ["102"] = new("asdf", 0, 0, false, new string[0]),
        },
        ObjectArtifactSpots = own,
    };

    [Fact]
    public void An_Artifact_Nothing_Else_Places_Is_Dug_From_Its_Own_Spots()
    {
        var composer = new EffortComposer(
            Data(new RawArtifactSpot("Forest", Boomerang, 0.05), new RawArtifactSpot("Custom_Nowhere", Boomerang, 0.5)),
            new Dictionary<string, ItemAvailability>(), hasKitchen: false, locationWeeks: Walk());

        ItemEffort? effort = composer.Derive(Boomerang);

        Assert.NotNull(effort);
        Assert.Equal(AvailabilityWeeks.ArtifactWeek, effort!.EarliestWeek);
        Assert.Contains("ArtifactSpotChances", effort.Basis);
        Assert.Contains("Forest at 0.05", effort.Basis);   // the undatable map's 0.5 is not read
    }

    [Fact]
    public void Spots_Only_On_Undatable_Maps_Or_No_Walk_Place_Nothing()
    {
        EffortData data = Data(new RawArtifactSpot("Custom_Nowhere", Shield, 0.2));
        Assert.Null(new EffortComposer(data, new Dictionary<string, ItemAvailability>(), false, locationWeeks: Walk()).Derive(Shield));
        Assert.Null(new EffortComposer(Data(new RawArtifactSpot("Forest", Shield, 0.2)),
            new Dictionary<string, ItemAvailability>(), false).Derive(Shield));
    }

    [Fact]
    public void A_Non_Artifact_With_Spot_Chances_Is_Left_Alone()
    {
        var composer = new EffortComposer(Data(new RawArtifactSpot("Forest", "(O)102", 0.05)),
            new Dictionary<string, ItemAvailability>(), false, locationWeeks: Walk());
        Assert.Null(composer.Derive("(O)102"));
    }
}
