using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class GeneratedBundleSetTests
{
    private static BundleSpec Spec(string room, int index, string name, int slots, params string[] itemIds) =>
        new(room, index, name, name, "O 495 30", 0, slots,
            itemIds.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());

    [Fact]
    public void ToBundleData_EmitsOneEntryPerSpec_WriterKeysAndValues()
    {
        var set = new GeneratedBundleSet(new[]
        {
            Spec("Pantry", 0, "Spring Crops", 4, "24", "188", "190", "192"),
            Spec("CraftsRoom", 0, "Spring Foraging", 4, "16", "18", "20", "22"),
        });
        var data = set.ToBundleData();
        Assert.Equal(2, data.Count);
        Assert.True(data.ContainsKey("Pantry/0"));
        Assert.StartsWith("Spring Crops/", data["Pantry/0"]);
    }

    [Fact]
    public void BuildRequirements_ClassifiesEveryThemedBundle_NoneSkipped()
    {
        var set = new GeneratedBundleSet(new[]
        {
            Spec("Pantry", 0, "Quality Crops", 3, "(O)24", "(O)188", "(O)190", "(O)192"), // pick-3-of-4 with curated quota
            Spec("Pantry", 1, "Totally Unknown Bundle", 2, "(O)24", "(O)188", "(O)190"),  // pick-2-of-3, derived ramp
            Spec("Vault", 23, "2,500g", 1, "-1"),                                          // non-themed: skipped
        });
        var reqs = set.BuildRequirements(
            itemSeasonPins: new Dictionary<string, Season>(),
            bundleQuotas: GameplayConfig.DefaultBundleQuotas);
        Assert.Equal(2, reqs.Count); // Vault skipped, NOTHING else skipped — the engine's core guarantee
        Assert.Contains(reqs, r => r.Name == "Totally Unknown Bundle");
    }

    [Fact]
    public void ClampRamp_NeverDemandsMoreThanObtainableBySeason()
    {
        // 3 ingredients: two available from Spring (no pin), one pinned to Winter.
        var pins = new Dictionary<string, Season> { ["(O)412"] = Season.Winter };
        var ramp = GeneratedBundleSet.ClampRampForObtainability(
            cumulativeRamp: new[] { 1, 2, 3, 3 },
            ingredients: new[] { "(O)16", "(O)18", "(O)412" },
            numberOfSlots: 3,
            pins: pins);
        // By Fall only 2 slots are obtainable, so Fall clamps 3→2; Winter demands the full 3.
        Assert.Equal(new[] { 1, 2, 2, 3 }, ramp);
    }

    [Fact]
    public void ClampRamp_StaysMonotonic()
    {
        var pins = new Dictionary<string, Season>
        {
            ["(O)A"] = Season.Winter, ["(O)B"] = Season.Winter, ["(O)C"] = Season.Winter,
        };
        var ramp = GeneratedBundleSet.ClampRampForObtainability(
            new[] { 1, 2, 3, 3 }, new[] { "(O)A", "(O)B", "(O)C" }, 3, pins);
        Assert.Equal(new[] { 0, 0, 0, 3 }, ramp); // nothing obtainable before Winter
        for (int i = 1; i < 4; i++) Assert.True(ramp[i] >= ramp[i - 1]);
    }
}
