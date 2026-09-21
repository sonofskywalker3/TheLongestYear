using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Spec 2026-09-21: the first strike of each kind in a loop plays its scene; a scene can be
/// skipped once that kind has ever played on the save.</summary>
public class StrikeScenesTests
{
    [Fact]
    public void A_kind_not_yet_played_this_loop_is_due()
        => Assert.True(StrikeScenes.IsDue(DarknessEvent.CropBlight, new string[0]));

    [Fact]
    public void A_kind_already_played_this_loop_is_not_due()
        => Assert.False(StrikeScenes.IsDue(DarknessEvent.CropBlight, new[] { "CropBlight" }));

    [Fact]
    public void Crows_playing_does_not_use_up_the_thief()
        => Assert.True(StrikeScenes.IsDue(DarknessEvent.ChestBlight, new[] { "CropBlight" }));

    [Fact]
    public void The_first_time_ever_cannot_be_skipped()
        => Assert.False(StrikeScenes.IsSkippable(DarknessEvent.Tampering, new string[0]));

    [Fact]
    public void Once_seen_on_the_save_it_can_be_skipped()
        => Assert.True(StrikeScenes.IsSkippable(DarknessEvent.Tampering, new[] { "Tampering" }));

    [Fact]
    public void MarkPlayed_records_the_loop_and_the_save()
    {
        var run = new RunState(); var meta = new MetaState();
        StrikeScenes.MarkPlayed(DarknessEvent.Reversion, run, meta);
        Assert.Contains("Reversion", run.StrikeScenesPlayed);
        Assert.Contains("Reversion", meta.StrikeScenesSeen);
    }
}
