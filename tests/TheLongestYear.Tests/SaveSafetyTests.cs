using System;
using System.Collections.Generic;
using System.Xml.Serialization;
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

    [Fact]
    public void Ordinary_save_keeps_its_day_start()
    {
        var latch = new PostSaveDayStartLatch();
        Assert.False(latch.Gate());
        Assert.False(latch.DayStartArrived());
        Assert.Equal(PostSaveDayStartLatch.Outcome.Idle, latch.Settle());
    }

    [Fact]
    public void Reset_save_day_start_is_switched_off_once_and_back_on_after()
    {
        var latch = new PostSaveDayStartLatch();
        latch.Arm();
        Assert.True(latch.Gate());          // Saved: switch the day start off
        Assert.False(latch.Gate());         // only once
        Assert.Equal(PostSaveDayStartLatch.Outcome.Dropped, latch.Settle());

        // The next real morning is not touched.
        Assert.False(latch.Gate());
        Assert.False(latch.DayStartArrived());
        Assert.Equal(PostSaveDayStartLatch.Outcome.Idle, latch.Settle());
    }

    [Fact]
    public void Day_start_that_gets_past_the_gate_is_reported_once()
    {
        var latch = new PostSaveDayStartLatch();
        latch.Arm();
        latch.Gate();
        Assert.True(latch.DayStartArrived());
        Assert.False(latch.DayStartArrived());   // a later morning is not a leak
        Assert.Equal(PostSaveDayStartLatch.Outcome.Leaked, latch.Settle());
        Assert.False(latch.Armed);
        Assert.False(latch.Gated);
    }

    [Fact]
    public void Save_that_never_raised_saved_switches_nothing_off()
    {
        var latch = new PostSaveDayStartLatch();
        latch.Arm();
        Assert.Equal(PostSaveDayStartLatch.Outcome.NoSaved, latch.Settle());
        Assert.False(latch.Gate());
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
    public void Serializer_writes_registered_types_and_what_they_bring_in()
    {
        var serializer = new XmlSerializer(typeof(ProbeRoot), new[] { typeof(RegisteredMonster) });

        Assert.Equal(SerializerProbe.Verdict.Writable, SerializerProbe.Probe(serializer, new RegisteredMonster(), out _));
        // Brought in by an XmlInclude on the registered type's unregistered base.
        Assert.Equal(SerializerProbe.Verdict.Writable, SerializerProbe.Probe(serializer, new SiblingMonster(), out _));
        Assert.Equal(SerializerProbe.Verdict.Writable, SerializerProbe.Probe(serializer, new ProbeRoot(), out _));
    }

    [Fact]
    public void Serializer_rejects_an_unknown_type()
    {
        var serializer = new XmlSerializer(typeof(ProbeRoot), new[] { typeof(RegisteredMonster) });

        Assert.Equal(SerializerProbe.Verdict.TypeRejected, SerializerProbe.Probe(serializer, new UnregisteredMonster(), out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Other_write_failures_say_nothing_about_the_type()
    {
        var serializer = new XmlSerializer(typeof(ProbeRoot), new[] { typeof(ThrowingMonster) });

        Assert.Equal(SerializerProbe.Verdict.ProbeFailed, SerializerProbe.Probe(serializer, new ThrowingMonster(), out _));
    }

    [Fact]
    public void Save_folder_holding_someone_elses_save_is_left_to_the_game()
    {
        Assert.Equal(NewSaveFolderPlan.Action.CreateAndPin, NewSaveFolderPlan.Decide(folderExists: false, holdsSaveFile: false));
        Assert.Equal(NewSaveFolderPlan.Action.AdoptAndPin, NewSaveFolderPlan.Decide(folderExists: true, holdsSaveFile: false));
        Assert.Equal(NewSaveFolderPlan.Action.LeaveToGame, NewSaveFolderPlan.Decide(folderExists: true, holdsSaveFile: true));
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

public class ProbeRoot { public int Hearts { get; set; } }

[XmlInclude(typeof(SiblingMonster))]
public class BaseOfRegistered : ProbeRoot { }

public class RegisteredMonster : BaseOfRegistered { }

public class SiblingMonster : ProbeRoot { }

public class UnregisteredMonster : ProbeRoot { }

public class ThrowingMonster : ProbeRoot
{
    public string Broken { get => throw new ArgumentException("mod state not ready"); set { } }
}