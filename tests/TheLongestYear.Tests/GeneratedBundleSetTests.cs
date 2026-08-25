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

    /// <summary>Spec 2026-08-25 section 4: the season filter keeps Fall crops out of the
    /// Spring-named bundle, and the clamp drops a Spring quota that only non-Spring items
    /// could satisfy to zero. Rolls a Spring-named crop bundle (Spring ingredients only) and a
    /// derived-quota percentage bundle whose five ingredients are all Fall-or-later, builds
    /// the manifest with the derived pins, and verifies the clamp engaged: Spring required = 0.</summary>
    [Fact]
    public void SpringGate_NeverRequires_FallOrWinterOnlyProduce()
    {
        var pools = ItemPoolBuilder.Build(
            new[]
            {
                new RawCropEntry("24", new[] { Season.Spring }),
                new RawCropEntry("188", new[] { Season.Spring }),
                new RawCropEntry("190", new[] { Season.Spring }),
                new RawCropEntry("192", new[] { Season.Spring }),
                new RawCropEntry("270", new[] { Season.Summer, Season.Fall }),  // Corn
                new RawCropEntry("276", new[] { Season.Fall }),                  // Pumpkin
                new RawCropEntry("278", new[] { Season.Fall }),                  // Bok Choy
                new RawCropEntry("280", new[] { Season.Fall }),                  // Yam
                new RawCropEntry("282", new[] { Season.Fall }),                  // Cranberries
            },
            GatedItemVettingTests.Objects(("24", GatedItemVettingTests.Obj()), ("188", GatedItemVettingTests.Obj()),
                ("190", GatedItemVettingTests.Obj()), ("192", GatedItemVettingTests.Obj()),
                ("270", GatedItemVettingTests.Obj()), ("276", GatedItemVettingTests.Obj()),
                ("278", GatedItemVettingTests.Obj()), ("280", GatedItemVettingTests.Obj()),
                ("282", GatedItemVettingTests.Obj())),
            new List<RawSpawnEntry>(), new List<RawSpawnEntry>(),
            new HashSet<string>(), new List<RawMonsterDropEntry>(),
            new List<RawFruitTreeEntry>(), new List<RawGeodeDropEntry>(), new BundleGenerationTuning());

        var springSpec = new BundleSpec("Pantry", 0, "Spring Crops", "Spring Crops", "O 495 30", 0, 4,
            new[] { "(O)24", "(O)188", "(O)190", "(O)192" }.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());
        var filledSpring = BundleSlotFiller.Fill(springSpec, new DomainMatch(PoolDomain.SeasonalCrops, Season.Spring),
            pools, new BundleGenerationTuning(), new Random(11));
        Assert.All(filledSpring.Slots, s => Assert.DoesNotContain(s.ItemId, pools.DerivedSeasonPins.Keys));

        var anySpec = new BundleSpec("Pantry", 1, "Totally Unknown Bundle", "Totally Unknown Bundle", "O 495 30", 0, 4,
            new[] { "(O)270", "(O)276", "(O)278", "(O)280", "(O)282" }.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());
        var set = new GeneratedBundleSet(new[] { filledSpring, anySpec });
        var reqs = set.BuildRequirements(pools.DerivedSeasonPins, GameplayConfig.DefaultBundleQuotas);

        // The non-Spring bundle must have its Spring quota clamped to 0 because all five ingredients are unobtainable in Spring.
        var unknown = Assert.Single(reqs, r => r.Name == "Totally Unknown Bundle");
        Assert.Equal(0, unknown.CumulativeRequiredBySeason![0]);
        Assert.True(unknown.CumulativeRequiredBySeason[3] >= 4);

        // Donating only the Spring-obtainable items must satisfy every bundle's Spring gate.
        var springOnly = new HashSet<string>(
            filledSpring.Slots.Select(s => s.ItemId).Concat(new[] { "(O)24" }), StringComparer.Ordinal);
        Assert.All(reqs, r => Assert.True(r.IsSatisfiedAtSeasonEnd(Season.Spring, springOnly),
            $"{r.Name} demands Fall/Winter-only produce in Spring"));
    }
}
