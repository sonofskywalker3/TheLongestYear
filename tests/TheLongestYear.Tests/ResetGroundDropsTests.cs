using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Items the rewind drops on the ground (Keep Farm Decor overflow and the like) are not in
/// the save, since ground debris is never saved. They are remembered in the meta until the first
/// night so a quit on Spring 1 before picking them up does not lose them.</summary>
public class ResetGroundDropsTests
{
    private static PendingGroundDrop Fence() => new("Farm", 60, 15, new StashItemRecord("(O)322", 7, 0));

    [Fact]
    public void A_drop_survives_a_meta_round_trip()
    {
        var meta = new MetaState();
        ResetGroundDrops.Remember(meta, new[] { Fence() });
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(meta);
        var back = Newtonsoft.Json.JsonConvert.DeserializeObject<MetaState>(json)!;
        PendingGroundDrop d = Assert.Single(back.PendingResetDrops);
        Assert.Equal(("Farm", 60, 15, "(O)322", 7), (d.Location, d.X, d.Y, d.Item.ItemId, d.Item.Quantity));
    }

    [Fact]
    public void Remember_replaces_an_older_list()
    {
        var meta = new MetaState();
        ResetGroundDrops.Remember(meta, new[] { Fence(), Fence() });
        ResetGroundDrops.Remember(meta, new[] { Fence() });
        Assert.Single(meta.PendingResetDrops);
    }

    [Fact]
    public void The_first_night_forgets_them()
    {
        var meta = new MetaState();
        ResetGroundDrops.Remember(meta, new[] { Fence() });
        ResetGroundDrops.ForgetAtNight(meta);
        Assert.Empty(meta.PendingResetDrops);
    }

    [Fact]
    public void Old_saves_load_with_none()
        => Assert.Empty(Newtonsoft.Json.JsonConvert.DeserializeObject<MetaState>("{}")!.PendingResetDrops);
}
