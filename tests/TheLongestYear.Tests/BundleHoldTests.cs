using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleHoldTests
{
    private static readonly long[] Curve = { 0, 50, 100, 200, 300 };

    [Fact]
    public void First_hold_is_free_and_pins_seed_loop_to_current()
    {
        var s = new MetaState { CompletedResets = 2, JunimoPoints = 10 };

        var result = BundleHold.Apply(s, keep: true, Curve);

        Assert.Equal(BundleHold.HoldResult.Kept, result);
        Assert.Equal(10, s.JunimoPoints);
        Assert.Equal(1, s.ConsecutiveHolds);
        Assert.Equal(2, s.BundleSeedLoop);
        Assert.Equal(2, s.EffectiveBundleSeedLoop);
    }

    [Fact]
    public void Second_hold_costs_fifty_and_keeps_seed_loop()
    {
        var s = new MetaState { CompletedResets = 3, JunimoPoints = 80, BundleSeedLoop = 2, ConsecutiveHolds = 1 };

        var result = BundleHold.Apply(s, keep: true, Curve);

        Assert.Equal(BundleHold.HoldResult.Kept, result);
        Assert.Equal(30, s.JunimoPoints);
        Assert.Equal(2, s.ConsecutiveHolds);
        Assert.Equal(2, s.BundleSeedLoop);
    }

    [Fact]
    public void NotEnoughJp_leaves_state_untouched()
    {
        var s = new MetaState { CompletedResets = 3, JunimoPoints = 20, BundleSeedLoop = 2, ConsecutiveHolds = 1 };

        var result = BundleHold.Apply(s, keep: true, Curve);

        Assert.Equal(BundleHold.HoldResult.NotEnoughJp, result);
        Assert.Equal(20, s.JunimoPoints);
        Assert.Equal(1, s.ConsecutiveHolds);
        Assert.Equal(2, s.BundleSeedLoop);
    }

    [Fact]
    public void Reshuffle_resets_counter_and_advances_seed_loop_to_upcoming()
    {
        var s = new MetaState { CompletedResets = 3, JunimoPoints = 500, BundleSeedLoop = 1, ConsecutiveHolds = 2 };

        var result = BundleHold.Apply(s, keep: false, Curve);

        Assert.Equal(BundleHold.HoldResult.Reshuffled, result);
        Assert.Equal(500, s.JunimoPoints);
        Assert.Equal(0, s.ConsecutiveHolds);
        Assert.Equal(4, s.BundleSeedLoop);   // CompletedResets + 1 = the loop PerformReset is about to create
    }

    [Fact]
    public void Reshuffle_on_legacy_save_advances_from_CompletedResets()
    {
        var s = new MetaState { CompletedResets = 3 };   // BundleSeedLoop = -1
        BundleHold.Apply(s, keep: false, Curve);
        Assert.Equal(4, s.BundleSeedLoop);
    }

    [Fact]
    public void NextCost_reads_the_curve_at_the_current_counter()
    {
        Assert.Equal(0, BundleHold.NextCost(new MetaState(), Curve));
        Assert.Equal(200, BundleHold.NextCost(new MetaState { ConsecutiveHolds = 3 }, Curve));
    }

    [Fact]
    public void Hold_after_reshuffle_is_free_again()
    {
        var s = new MetaState { CompletedResets = 5, JunimoPoints = 10, BundleSeedLoop = 1, ConsecutiveHolds = 3 };
        BundleHold.Apply(s, keep: false, Curve);
        s.CompletedResets = 6;   // PerformReset bumped it
        var result = BundleHold.Apply(s, keep: true, Curve);
        Assert.Equal(BundleHold.HoldResult.Kept, result);
        Assert.Equal(10, s.JunimoPoints);
        Assert.Equal(6, s.BundleSeedLoop);
    }

    [Fact]
    public void Null_curve_is_free()
    {
        Assert.Equal(0, BundleHold.NextCost(new MetaState(), null));

        var s = new MetaState { CompletedResets = 2, JunimoPoints = 10 };
        var result = BundleHold.Apply(s, keep: true, null);

        Assert.Equal(BundleHold.HoldResult.Kept, result);
        Assert.Equal(10, s.JunimoPoints);
    }

    [Fact]
    public void ConsumeChoiceAtReset_with_no_choice_snaps_seed_loop_and_zeroes_counter()
    {
        var s = new MetaState { CompletedResets = 3, BundleSeedLoop = 1, ConsecutiveHolds = 2, HoldChoiceMadeForReset = false };

        bool choiceMade = BundleHold.ConsumeChoiceAtReset(s);

        Assert.False(choiceMade);
        Assert.Equal(3, s.BundleSeedLoop);
        Assert.Equal(0, s.ConsecutiveHolds);
        Assert.False(s.HoldChoiceMadeForReset);
    }

    [Fact]
    public void ConsumeChoiceAtReset_with_a_choice_leaves_seed_loop_and_counter_untouched()
    {
        var s = new MetaState { CompletedResets = 3, BundleSeedLoop = 1, ConsecutiveHolds = 2, HoldChoiceMadeForReset = true };

        bool choiceMade = BundleHold.ConsumeChoiceAtReset(s);

        Assert.True(choiceMade);
        Assert.Equal(1, s.BundleSeedLoop);
        Assert.Equal(2, s.ConsecutiveHolds);
        Assert.False(s.HoldChoiceMadeForReset);
    }

    [Fact]
    public void ConsumeChoiceAtReset_without_a_choice_clears_the_board_trim_stamp()
    {
        var s = new MetaState { CompletedResets = 4, BoardTrimSeason = 0, BoardTrimSteps = 4 };
        bool made = BundleHold.ConsumeChoiceAtReset(s);
        Assert.False(made);
        Assert.Equal(-1, s.BoardTrimSeason);
        Assert.Equal(0, s.BoardTrimSteps);
    }

    [Fact]
    public void ConsumeChoiceAtReset_with_a_choice_keeps_the_board_trim_stamp()
    {
        var s = new MetaState { CompletedResets = 4, BoardTrimSeason = 0, BoardTrimSteps = 4, HoldChoiceMadeForReset = true };
        Assert.True(BundleHold.ConsumeChoiceAtReset(s));
        Assert.Equal(0, s.BoardTrimSeason);
        Assert.Equal(4, s.BoardTrimSteps);
    }

    [Fact]
    public void IsOfferable_is_true_for_Engine_and_false_for_Vanilla()
    {
        Assert.True(BundleHold.IsOfferable(BundleSourceNames.Engine));
        Assert.False(BundleHold.IsOfferable(BundleSourceNames.Vanilla));
    }

    [Fact]
    public void Both_answers_stamp_the_choice_flag_but_NotEnoughJp_does_not()
    {
        var kept = new MetaState { JunimoPoints = 0 };
        BundleHold.Apply(kept, keep: true, Curve);
        Assert.True(kept.HoldChoiceMadeForReset);

        var shuffled = new MetaState();
        BundleHold.Apply(shuffled, keep: false, Curve);
        Assert.True(shuffled.HoldChoiceMadeForReset);

        var broke = new MetaState { ConsecutiveHolds = 1, JunimoPoints = 0 };
        BundleHold.Apply(broke, keep: true, Curve);
        Assert.False(broke.HoldChoiceMadeForReset);
    }
}
