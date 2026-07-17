using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleDataWriterTests
{
    private static BundleSpec SpringCrops() => new(
        Room: "Pantry", Index: 0, Name: "Spring Crops", DisplayName: "Spring Crops",
        RewardField: "O 495 30", Color: 0, NumberOfSlots: 4,
        Slots: new[]
        {
            new BundleSlotSpec("24", 1, 0),
            new BundleSlotSpec("188", 1, 0),
            new BundleSlotSpec("190", 1, 0),
            new BundleSlotSpec("192", 1, 0),
        });

    [Fact]
    public void Key_IsRoomSlashIndex()
    {
        Assert.Equal("Pantry/0", BundleDataWriter.Key(SpringCrops()));
    }

    [Fact]
    public void Value_MatchesVanillaFieldLayout()
    {
        // name / reward / ingredients / color / numberOfSlots / sprite(empty) / displayName
        Assert.Equal(
            "Spring Crops/O 495 30/24 1 0 188 1 0 190 1 0 192 1 0/0/4//Spring Crops",
            BundleDataWriter.Value(SpringCrops()));
    }

    [Fact]
    public void RoundTrip_ThroughBundleParsing_PreservesEverythingParsed()
    {
        var spec = SpringCrops();
        var parsed = BundleParsing.Parse(BundleDataWriter.Key(spec), BundleDataWriter.Value(spec));
        Assert.Equal(spec.Room, parsed.Room);
        Assert.Equal(spec.Index, parsed.Index);
        Assert.Equal(spec.Name, parsed.Name);
        Assert.Equal(spec.NumberOfSlots, parsed.NumberOfSlots);
        Assert.Equal(spec.Slots.Count, parsed.Ingredients.Count);
        Assert.All(spec.Slots.Zip(parsed.Ingredients), pair =>
        {
            Assert.Equal(pair.First.ItemId, pair.Second.ItemRef);
            Assert.Equal(pair.First.Stack, pair.Second.Stack);
            Assert.Equal(pair.First.Quality, pair.Second.Quality);
        });
    }

    [Fact]
    public void Value_MoneyBundle_UsesMinusOneIngredient()
    {
        var vault = new BundleSpec("Vault", 23, "2,500g", "2,500g", "O 286 3", 4, 1,
            new[] { new BundleSlotSpec("-1", 2500, 0) });
        Assert.Equal("2,500g/O 286 3/-1 2500 0/4/1//2,500g", BundleDataWriter.Value(vault));
    }

    [Fact]
    public void Value_SlashInName_IsSanitizedToHyphen()
    {
        var spec = new BundleSpec("Pantry", 0, "Wine/Cheese", "Wine/Cheese", "O 495 30", 0, 1,
            new[] { new BundleSlotSpec("24", 1, 0) });
        string value = BundleDataWriter.Value(spec);
        Assert.Equal("Wine-Cheese/O 495 30/24 1 0/0/1//Wine-Cheese", value);
        // The written string must still parse into exactly the sanitized name.
        var parsed = BundleParsing.Parse(BundleDataWriter.Key(spec), value);
        Assert.Equal("Wine-Cheese", parsed.Name);
        Assert.Single(parsed.Ingredients);
    }

    [Fact]
    public void Value_SlashInRewardField_Throws()
    {
        var spec = new BundleSpec("Pantry", 0, "Fine", "Fine", "O 495/30", 0, 1,
            new[] { new BundleSlotSpec("24", 1, 0) });
        Assert.Throws<System.ArgumentException>(() => BundleDataWriter.Value(spec));
    }
}
