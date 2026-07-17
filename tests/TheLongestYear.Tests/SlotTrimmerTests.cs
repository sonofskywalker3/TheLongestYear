using System;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SlotTrimmerTests
{
    private static BundleSpec Spec(int slotCount, int numberOfSlots, int pickCount)
    {
        var slots = Enumerable.Range(0, slotCount)
            .Select(i => new BundleSlotSpec((100 + i).ToString(), 1, 0)).ToList();
        return new BundleSpec("Pantry", 0, "T", "T", "O 495 30", 0, numberOfSlots, slots, pickCount);
    }

    [Fact]
    public void PickCountDefault_MinusOne_NoTrim()
    {
        var spec = new BundleSpec("Pantry", 0, "T", "T", "O 495 30", 0, 2,
            new[] { new BundleSlotSpec("24", 1, 0) });
        Assert.Equal(-1, spec.PickCount);
        Assert.Same(spec, SlotTrimmer.Trim(spec, new Random(1)));
    }

    [Fact]
    public void Trim_ReducesToPickCount_PreservesOriginalOrder_NoDuplicates()
    {
        var trimmed = SlotTrimmer.Trim(Spec(slotCount: 8, numberOfSlots: 3, pickCount: 4), new Random(42));
        Assert.Equal(4, trimmed.Slots.Count);
        var ids = trimmed.Slots.Select(s => int.Parse(s.ItemId)).ToList();
        Assert.Equal(ids.OrderBy(x => x).ToList(), ids);      // original ascending order kept
        Assert.Equal(4, ids.Distinct().Count());
    }

    [Fact]
    public void Trim_ClampsNumberOfSlotsToTrimmedCount()
    {
        var trimmed = SlotTrimmer.Trim(Spec(slotCount: 6, numberOfSlots: 5, pickCount: 3), new Random(7));
        Assert.Equal(3, trimmed.Slots.Count);
        Assert.Equal(3, trimmed.NumberOfSlots);
    }

    [Fact]
    public void Trim_Deterministic_SameRngSeedSameResult()
    {
        var a = SlotTrimmer.Trim(Spec(10, 3, 5), new Random(99)).Slots.Select(s => s.ItemId);
        var b = SlotTrimmer.Trim(Spec(10, 3, 5), new Random(99)).Slots.Select(s => s.ItemId);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Trim_PickCountAtOrAboveSlotCount_NoTrim()
    {
        var spec = Spec(4, 3, 4);
        Assert.Same(spec, SlotTrimmer.Trim(spec, new Random(1)));
        var spec2 = Spec(4, 3, 9);
        Assert.Same(spec2, SlotTrimmer.Trim(spec2, new Random(1)));
    }
}
