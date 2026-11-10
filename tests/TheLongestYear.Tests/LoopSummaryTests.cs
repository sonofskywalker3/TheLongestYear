using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class LoopSummaryTests
{
    public LoopSummaryTests(I18nFixture fixture) => _ = fixture;

    private static BundleProgress Bundle(int index, int required, params bool[] filled)
    {
        var ingredients = new List<BundleIngredient>();
        foreach (bool _ in filled) ingredients.Add(new BundleIngredient("(O)24", 1, 0));
        return new BundleProgress(index, "Pantry", ingredients, filled, required);
    }

    [Fact]
    public void Build_snapshots_the_loop_and_later_earnings_do_not_change_it()
    {
        var run = new RunState { RunNumber = 4, LoopBlightNights = 2 };
        run.StartJpLedger();
        run.RecordJpEarned(JpSource.Bundles, 300);
        run.RecordJpEarned(JpSource.Rooms, 100);
        run.Tampers.Add(new TamperRecord());
        var board = new[] { Bundle(0, 2, true, true, false), Bundle(1, 3, true, false, false, false) };

        LoopSummary summary = LoopSummary.Build(run, board, heldBoard: false, new KeepSuggestion[0]);
        run.RecordJpEarned(JpSource.Bundles, 999);

        Assert.Equal(4, summary.LoopNumber);
        Assert.Equal(400, summary.Earned.Total());
        Assert.Equal((1, 2, 2), (summary.BundlesDone, summary.BundlesTotal, summary.DonationsStillNeeded));
        Assert.Equal((1, 2), (summary.Tampers, summary.BlightNights));
    }

    [Theory]
    [InlineData(false, 5, "You missed 5 fish asks last loop.")]
    [InlineData(false, 1, "You missed 1 fish ask last loop.")]
    [InlineData(true, 5, "The board you're keeping asks for 5 crops.")]
    [InlineData(true, 1, "The board you're keeping asks for 1 crop.")]
    public void The_reason_line_reads_as_the_spec_words_it(bool held, int count, string expected)
    {
        AskKind kind = held ? AskKind.Crops : AskKind.Fish;
        Assert.Equal(expected, LoopRecapText.Reason(new KeepSuggestion("quick_bite_1", kind, count), held));
    }

    [Fact]
    public void A_partial_first_loop_says_since_the_update()
    {
        var run = new RunState();
        run.RecordJpEarned(JpSource.Checkpoints, 50);
        LoopSummary summary = LoopSummary.Build(run, new BundleProgress[0], heldBoard: false, new KeepSuggestion[0]);
        Assert.Equal("Junimo Points earned: 50 (since the update)", LoopRecapText.JpLine(summary));
    }
}
