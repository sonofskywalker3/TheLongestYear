using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>"Allow mod items in custom bundles" (spec 2026-10-08-custom-board-vanilla-only, addendum 1).</summary>
public class CustomBoardModItemsTests
{
    [Fact]
    public void Config_default_for_new_games_is_off()
        => Assert.False(new GameplayConfig().AllowModItemsInCustomBundles);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void New_save_takes_the_title_screen_default(bool titleDefault)
        => Assert.Equal(titleDefault, CustomBoardModItems.Initial(isNewSave: true, titleDefault));

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Existing_save_without_a_value_is_on_whatever_the_title_default(bool titleDefault)
        => Assert.True(CustomBoardModItems.Initial(isNewSave: false, titleDefault));

    [Fact]
    public void Missing_field_reads_as_on()
    {
        // A meta file written before this option has no such property at all.
        MetaState legacy = JsonSerializer.Deserialize<MetaState>("{\"JunimoPoints\":5}")!;
        Assert.Null(legacy.AllowModItemsInCustomBundles);
        Assert.Null(legacy.BoardAllowsModItems);
        Assert.True(legacy.ModItemsChosen());
        Assert.True(legacy.ModItemsOnBoard());
    }

    [Fact]
    public void Stored_off_survives_a_round_trip()
    {
        var state = new MetaState { AllowModItemsInCustomBundles = false, BoardAllowsModItems = false };
        MetaState restored = JsonSerializer.Deserialize<MetaState>(JsonSerializer.Serialize(state))!;
        Assert.False(restored.ModItemsChosen());
        Assert.False(restored.ModItemsOnBoard());
    }

    [Fact]
    public void Board_stamp_wins_over_a_mid_loop_toggle()
    {
        // Board built vanilla-only, then the player turned the option on mid-loop.
        var state = new MetaState { BoardAllowsModItems = false, AllowModItemsInCustomBundles = true };
        Assert.False(state.ModItemsOnBoard());
        Assert.True(state.ModItemsChosen());
        Assert.Equal(new[] { false, true }, CustomBoardModItems.ManifestTryOrder(state.BoardAllowsModItems, state.AllowModItemsInCustomBundles));
    }

    [Fact]
    public void Unstamped_board_tries_both_values_existing_save_value_first()
    {
        Assert.Equal(new[] { true, false }, CustomBoardModItems.ManifestTryOrder(null, null));
        Assert.Equal(new[] { false, true }, CustomBoardModItems.ManifestTryOrder(null, false));
    }

    [Fact]
    public void New_board_at_reset_takes_the_choice()
    {
        Assert.True(CustomBoardModItems.ForReset(holdingBoard: false, boardStamp: false, chosen: true));
        Assert.False(CustomBoardModItems.ForReset(holdingBoard: false, boardStamp: true, chosen: false));
        Assert.True(CustomBoardModItems.ForReset(holdingBoard: false, boardStamp: null, chosen: null));
    }

    [Fact]
    public void Held_board_at_reset_keeps_its_stamp()
    {
        Assert.False(CustomBoardModItems.ForReset(holdingBoard: true, boardStamp: false, chosen: true));
        Assert.True(CustomBoardModItems.ForReset(holdingBoard: true, boardStamp: true, chosen: false));
        Assert.False(CustomBoardModItems.ForReset(holdingBoard: true, boardStamp: null, chosen: false));
    }
}
