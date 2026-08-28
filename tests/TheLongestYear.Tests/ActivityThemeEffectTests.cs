using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class MachineReadyTimeTests
{
    [Theory]
    [InlineData(200, 0.75, 150)] [InlineData(200, 1.25, 250)] [InlineData(1750, 0.75, 1310)] [InlineData(1750, 1.25, 2190)]
    [InlineData(10, 0.75, 10)] [InlineData(4, 1.25, 10)] [InlineData(0, 1.25, 0)] [InlineData(-1, 0.75, -1)]
    public void Scales_and_rounds_to_ten_minutes_with_a_ten_minute_floor(int minutes, double factor, int expected)
        => Assert.Equal(expected, MachineReadyTime.Scale(minutes, factor));
}

public class DoubleProduceTests
{
    [Fact]
    public void Double_produce_records_are_taken_once_and_wiped_by_a_new_run()
    {
        var run = new RunState();
        run.RecordDoubleProduce(7, "184");
        run.RecordDoubleProduce(7, "184");
        Assert.Single(run.DoubleProduceToday);
        Assert.True(run.TryTakeDoubleProduce(7, out string produce));
        Assert.Equal("184", produce);
        Assert.False(run.TryTakeDoubleProduce(7, out _));
        run.RecordDoubleProduce(8, "440");
        run.BeginNewRun(5);
        Assert.Empty(run.DoubleProduceToday);
    }
}
