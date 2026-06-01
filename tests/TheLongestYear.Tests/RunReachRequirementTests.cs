using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class RunReachRequirementTests
{
    [Theory]
    [InlineData("tool:watering_can:2", "tool", "watering_can", 2)]
    [InlineData("skill:fishing:5", "skill", "fishing", 5)]
    [InlineData("rod:3", "rod", null, 3)]
    [InlineData("backpack:1", "backpack", null, 1)]
    [InlineData("mine:60", "mine", null, 60)]
    [InlineData("mastery:4", "mastery", null, 4)]
    [InlineData("scythe:golden", "scythe", "golden", 1)]
    public void Parse_extracts_metric_key_threshold(string raw, string metric, string? key, int threshold)
    {
        RunReachRequirement? r = RunReachRequirement.Parse(raw);
        Assert.NotNull(r);
        Assert.Equal(metric, r!.Metric);
        Assert.Equal(key, r.Key);
        Assert.Equal(threshold, r.Threshold);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("garbage")]
    [InlineData("tool::2")]
    [InlineData("mine:notanumber")]
    public void Parse_returns_null_for_empty_or_malformed(string? raw)
        => Assert.Null(RunReachRequirement.Parse(raw));

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(2, 2, true)]
    [InlineData(2, 3, false)]
    public void IsMet_is_actual_geq_threshold(int actual, int threshold, bool expected)
    {
        var r = RunReachRequirement.Parse($"mine:{threshold}")!;
        Assert.Equal(expected, r.IsMet(actual));
    }
}
