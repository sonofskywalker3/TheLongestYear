using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

[Collection("i18n")]
public class BanishTextTests
{
    public BanishTextTests(I18nFixture fixture) => I18nFixture.InstallGlobalProvider();

    private static BanishPick Pick(string label, long paid = 100) => new() { Kind = BanishKind.Item, Key = label, Label = label, Paid = paid };

    [Fact]
    public void Refund_notice_names_every_banish_through_the_translations()
    {
        Assert.Equal("The Junimos couldn't keep Parsnip off the board this loop, so 100 JP was returned.",
            BanishText.RefundNotice(new[] { Pick("Parsnip") }));
        Assert.Equal("The Junimos couldn't keep Parsnip and Orchard off the board this loop, so 300 JP was returned.",
            BanishText.RefundNotice(new[] { Pick("Parsnip"), Pick("Orchard", 200) }));
        Assert.Equal("The Junimos couldn't keep Parsnip, Orchard and Potato off the board this loop, so 300 JP was returned.",
            BanishText.RefundNotice(new[] { Pick("Parsnip"), Pick("Orchard"), Pick("Potato") }));
    }
}
