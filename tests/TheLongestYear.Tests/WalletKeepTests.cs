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
}
