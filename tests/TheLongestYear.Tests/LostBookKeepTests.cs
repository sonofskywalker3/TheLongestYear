using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Keep Lost Books (Jeff, 2026-09-29; tanky24u asked on Nexus): found Lost Books stay found
/// through a rewind, so artifact spots stop turning up books already read.</summary>
public class LostBookKeepTests
{
    [Fact]
    public void Keeps_every_book_read_marker_and_the_found_flag_only()
    {
        var mail = new[] { "lb_1", "lb_12", "lostBookFound", "ccBoilerRoom", "lb", "Robin" };
        Assert.Equal(new[] { "lb_1", "lb_12", "lostBookFound" }, LostBookKeep.MailToKeep(mail).OrderBy(x => x));
    }

    [Fact]
    public void Keep_Lost_Books_costs_100_JP()
        => Assert.Equal(100L, UpgradeCatalog.All.Single(u => u.Id == LostBookKeep.UpgradeId).Cost);
}
