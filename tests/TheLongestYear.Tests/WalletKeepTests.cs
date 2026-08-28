using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class WalletKeepTests
{
    [Fact]
    public void Table_has_eighteen_rows_totalling_6950_jp()
    {
        Assert.Equal(18, WalletKeepTable.Entries.Count);
        Assert.Equal(6950, WalletKeepTable.Entries.Sum(e => e.Cost));
        Assert.Equal(18, WalletKeepTable.Entries.Select(e => e.UpgradeId).Distinct().Count());
        Assert.Equal(11, WalletKeepTable.Entries.Count(e => e.UpgradeId.StartsWith("keep_wallet_")));
        Assert.Equal(7, WalletKeepTable.Entries.Count(e => e.Kind == WalletKeepKind.Stardrop));
    }

    [Theory]
    [InlineData("keep_wallet_dwarvish", 150, "mail:HasDwarvishTranslationGuide")]
    [InlineData("keep_wallet_bearsknowledge", 150, "event:2120303")]
    [InlineData("keep_wallet_springonion", 150, "event:3910979")]
    [InlineData("keep_wallet_rustykey", 350, "mail:HasRustyKey")]
    [InlineData("keep_wallet_skullkey", 750, "mail:HasSkullKey")]
    [InlineData("keep_stardrop_fair", 500, "mail:CF_Fair")]
    [InlineData("keep_stardrop_mines", 500, "stardrop_mines")]
    [InlineData("keep_stardrop_museum", 500, "mail:museumComplete")]
    public void Rows_have_the_spec_price_and_reach(string id, long cost, string reach)
    {
        WalletKeep e = WalletKeepTable.Entries.Single(x => x.UpgradeId == id);
        Assert.Equal(cost, e.Cost);
        Assert.Equal(reach, e.Reach);
    }

    [Fact]
    public void Only_the_wizard_chain_has_prerequisites()
    {
        Assert.Equal("keep_wallet_rustykey",
            WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_wallet_darktalisman").PrerequisiteId);
        Assert.Equal("keep_wallet_darktalisman",
            WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_wallet_magicink").PrerequisiteId);
        Assert.All(WalletKeepTable.Entries.Where(e =>
                e.UpgradeId != "keep_wallet_darktalisman" && e.UpgradeId != "keep_wallet_magicink"),
            e => Assert.Null(e.PrerequisiteId));
    }

    [Fact]
    public void Skull_key_keeps_the_door_too_and_mines_stardrop_keeps_cf_mines()
    {
        var skull = WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_wallet_skullkey");
        Assert.Equal(new[] { "HasSkullKey", "HasUnlockedSkullDoor" }, skull.MailFlags);
        var mines = WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_stardrop_mines");
        Assert.Equal(new[] { "CF_Mines" }, mines.MailFlags);
        var bear = WalletKeepTable.Entries.Single(e => e.UpgradeId == "keep_wallet_bearsknowledge");
        Assert.Empty(bear.MailFlags);
        Assert.Equal("2120303", bear.EventId);
    }

    [Fact]
    public void Catalog_carries_every_row_as_a_reach_gated_carryover_row()
    {
        foreach (WalletKeep e in WalletKeepTable.Entries)
        {
            UpgradeDefinition? def = UpgradeCatalog.TryGet(e.UpgradeId);
            Assert.NotNull(def);
            Assert.Equal(UpgradeCategory.Carryover, def!.Category);
            Assert.Equal(e.Cost, def.Cost);
            Assert.Equal(e.PrerequisiteId, def.PrerequisiteId);
            Assert.Equal(e.Reach, def.RunReachRequirement);
        }
    }

    [Theory]
    [InlineData("mail:HasSkullKey", "mail", "HasSkullKey")]
    [InlineData("event:2120303", "event", "2120303")]
    public void Keyed_reach_forms_parse_with_threshold_one(string raw, string metric, string key)
    {
        RunReachRequirement? r = RunReachRequirement.Parse(raw);
        Assert.NotNull(r);
        Assert.Equal(metric, r!.Metric);
        Assert.Equal(key, r.Key);
        Assert.Equal(1, r.Threshold);
    }

    [Fact]
    public void Bare_stardrop_mines_reach_parses_with_threshold_one()
    {
        RunReachRequirement? r = RunReachRequirement.Parse("stardrop_mines");
        Assert.NotNull(r);
        Assert.Equal("stardrop_mines", r!.Metric);
        Assert.Null(r.Key);
        Assert.Equal(1, r.Threshold);
    }

    [Fact]
    public void Builder_maps_owned_rows_to_flags_events_and_stardrop_count()
    {
        var meta = new MetaState { OwnedUpgrades =
            { "keep_wallet_skullkey", "keep_wallet_bearsknowledge", "keep_stardrop_fair", "keep_stardrop_mines" } };
        RunBaseline b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(new[] { "HasSkullKey", "HasUnlockedSkullDoor", "CF_Fair", "CF_Mines" }, b.KeptMailFlags);
        Assert.Equal(new[] { "2120303" }, b.KeptEventIds);
        Assert.Equal(2, b.KeptStardropCount);
    }

    [Fact]
    public void Builder_leaves_everything_empty_when_nothing_is_owned()
    {
        RunBaseline b = RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Empty(b.KeptMailFlags);
        Assert.Empty(b.KeptEventIds);
        Assert.Equal(0, b.KeptStardropCount);
    }

    [Fact]
    public void Power_granting_events_are_replayable_so_an_unbought_power_is_earned_again()
    {
        Assert.True(EventGatingTables.Default.IsReplayable(WalletKeepTable.BearEventId));
        Assert.True(EventGatingTables.Default.IsReplayable(WalletKeepTable.SpringOnionEventId));
        Assert.False(EventGatingTables.Default.IsHeldUntilSpring5(WalletKeepTable.BearEventId));
        Assert.False(EventGatingTables.Default.IsFurnaceTeach(WalletKeepTable.BearEventId));
    }
}
