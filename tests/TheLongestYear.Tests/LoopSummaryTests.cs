using System;
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

    [Theory]
    [InlineData(false, YearTwoCrops.GarlicUpgrade, 3, "Garlic", "You missed 3 Garlic asks last loop.")]
    [InlineData(false, YearTwoCrops.GarlicUpgrade, 1, "Garlic", "You missed 1 Garlic ask last loop.")]
    [InlineData(true, YearTwoCrops.RedCabbageUpgrade, 3, "Red Cabbage", "The board you're keeping asks for 3 Red Cabbage.")]
    [InlineData(false, YearTwoCrops.PierreUpgrade, 2, null, "You missed 2 year-two crop asks last loop.")]
    [InlineData(true, YearTwoCrops.PierreUpgrade, 1, null, "The board you're keeping asks for 1 year-two crop.")]
    [InlineData(true, YearTwoCrops.PierreUpgrade, 4, null, "The board you're keeping asks for 4 year-two crops.")]
    [InlineData(false, KeepSuggestions.FortuneRareFish, 2, null, "You missed 2 rare fish asks last loop.")]
    [InlineData(true, KeepSuggestions.FortuneRareFish, 1, null, "The board you're keeping asks for 1 rare fish.")]
    public void An_item_specific_reason_names_its_item(bool held, string upgradeId, int count, string? item, string expected)
        => Assert.Equal(expected, LoopRecapText.Reason(new KeepSuggestion(upgradeId, AskKind.Crops, count, item), held));

    [Theory]
    [InlineData(1, 1, "The darkness changed 1 ask and struck 1 night.")]
    [InlineData(3, 0, "The darkness changed 3 asks and struck 0 nights.")]
    [InlineData(0, 0, null)]
    public void The_darkness_line_counts_in_singular_and_plural(int tampers, int nights, string expected)
    {
        var run = new RunState { LoopBlightNights = nights };
        for (int i = 0; i < tampers; i++) run.Tampers.Add(new TamperRecord());
        LoopSummary summary = LoopSummary.Build(run, new BundleProgress[0], heldBoard: false, new KeepSuggestion[0]);
        Assert.Equal(expected, LoopRecapText.DarknessLine(summary));
    }

    [Fact]
    public void A_partial_first_loop_says_since_the_update()
    {
        var run = new RunState();
        run.RecordJpEarned(JpSource.Checkpoints, 50);
        LoopSummary summary = LoopSummary.Build(run, new BundleProgress[0], heldBoard: false, new KeepSuggestion[0]);
        Assert.Equal("Junimo Points earned: 50 (since the update)", LoopRecapText.JpLine(summary));
    }

    [Theory]
    [InlineData(1000, 1f, true)]      // fits as is
    [InlineData(800, 0.8f, true)]     // shrinks the text first
    [InlineData(600, 0.9f, false)]    // then drops the sources line, back at a larger scale
    [InlineData(100, 0.7f, false)]    // nothing fits: the floor, without the sources line
    public void Recap_fits_by_shrinking_text_then_dropping_the_sources_line(int available, float scale, bool sources)
    {
        // 1000 px at full scale with the sources line, 650 without; height follows the scale.
        int Body(float s, bool withSources) => (int)Math.Round((withSources ? 1000 : 650) * s);

        (float fitScale, bool fitSources) = LoopRecapText.Fit(Body, available);

        Assert.Equal(scale, fitScale, 3);
        Assert.Equal(sources, fitSources);
    }
}
