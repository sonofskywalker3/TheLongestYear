using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class SaveSafetyTests
{
    [Fact]
    public void Reset_save_waits_for_the_reload_the_reset_started()
    {
        // The reset rebuilds the world (loadForNewGame), so SMAPI owes every mod a SaveLoaded and
        // a DayStarted. Saving before they run makes mods see two day starts around the save.
        Assert.False(ResetSaveGate.CanOpen(menuOpen: false, eventUp: false, minigameUp: false, worldReloadFinished: false));
        Assert.True(ResetSaveGate.CanOpen(menuOpen: false, eventUp: false, minigameUp: false, worldReloadFinished: true));
    }

    [Theory]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    public void Reset_save_waits_for_a_clear_screen(bool menuOpen, bool eventUp, bool minigameUp)
    {
        Assert.False(ResetSaveGate.CanOpen(menuOpen, eventUp, minigameUp, worldReloadFinished: true));
    }

    private class GameThing { }
    private sealed class ModThing : GameThing { }
    private sealed class OtherModThing : GameThing { }

    [Fact]
    public void Only_foreign_types_the_serializer_rejects_are_set_aside()
    {
        var entries = new List<KeyValuePair<int, GameThing>>
        {
            new(1, new GameThing()),
            new(2, new ModThing()),
            new(3, new OtherModThing()),
            new(4, new ModThing()),
        };
        var probed = new List<Type>();

        List<int> picked = UnsaveableObjects.Pick(
            entries,
            isGameType: t => t == typeof(GameThing),
            canWrite: v => { probed.Add(v.GetType()); return v is OtherModThing; },
            verdicts: new Dictionary<Type, bool>());

        Assert.Equal(new[] { 2, 4 }, picked);
        // Game types are never probed, and each foreign type is probed once.
        Assert.Equal(new[] { typeof(ModThing), typeof(OtherModThing) }, probed);
    }

    [Fact]
    public void Null_values_are_left_alone()
    {
        var entries = new List<KeyValuePair<int, GameThing?>> { new(1, null) };

        List<int> picked = UnsaveableObjects.Pick(
            entries,
            isGameType: _ => false,
            canWrite: _ => false,
            verdicts: new Dictionary<Type, bool>());

        Assert.Empty(picked);
    }
}
