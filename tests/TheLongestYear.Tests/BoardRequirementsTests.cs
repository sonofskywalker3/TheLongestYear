using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BoardRequirementsTests
{
    private static BundleSpec Spec(string room, int index, string name, int slots, params string[] itemIds) =>
        new(room, index, name, name, "O 495 30", 0, slots,
            itemIds.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());

    private static GeneratedBundleSet Set() => new(new[]
    {
        Spec("Pantry", 0, "Quality Crops", 3, "(O)24", "(O)188", "(O)190", "(O)192"),
        Spec("Pantry", 1, "Totally Unknown Bundle", 2, "(O)24", "(O)188", "(O)190"),
        Spec("Fish Tank", 9, "Night Fishing", 3, "(O)800", "(O)132", "(O)155"),
        Spec("Vault", 23, "2,500g", 1, "-1"),
    });

    [Fact]
    public void Stored_board_rebuilds_the_same_requirements_as_the_generated_set()
    {
        GeneratedBundleSet set = Set();
        var pins = new Dictionary<string, Season>();
        IReadOnlyList<BundleRequirement> fromSet = set.BuildRequirements(pins, GameplayConfig.DefaultBundleQuotas);

        // Round-trip the way MetaState persists it: a plain dictionary of the written strings.
        var stored = new Dictionary<string, string>(set.ToBundleData());
        IReadOnlyList<BundleRequirement> fromStored = BoardRequirements.Build(stored, pins, GameplayConfig.DefaultBundleQuotas);

        Assert.Equal(fromSet.Select(r => r.Name), fromStored.Select(r => r.Name));
        Assert.Equal(fromSet.Select(r => r.Kind), fromStored.Select(r => r.Kind));
        Assert.Equal(fromSet.Select(r => r.BundleIndex), fromStored.Select(r => r.BundleIndex));
        Assert.Equal(fromSet.Select(r => string.Join(",", r.Ingredients)), fromStored.Select(r => string.Join(",", r.Ingredients)));
        Assert.DoesNotContain(fromStored, r => r.Name == "2,500g"); // Vault skipped, as before
    }

    [Fact]
    public void Season_pins_round_trip_through_their_stored_int_form()
    {
        var pins = new Dictionary<string, Season> { ["(O)800"] = Season.Fall, ["(O)24"] = Season.Spring };
        Dictionary<string, int> stored = BoardRequirements.PinsToStored(pins);
        Assert.Equal(pins, BoardRequirements.PinsFromStored(stored));
        Assert.Empty(BoardRequirements.PinsFromStored(null));
    }

    [Fact]
    public void RoomOf_strips_the_index()
    {
        Assert.Equal("Fish Tank", BoardRequirements.RoomOf("Fish Tank/9"));
        Assert.Equal("Vault", BoardRequirements.RoomOf("Vault"));
    }
}
