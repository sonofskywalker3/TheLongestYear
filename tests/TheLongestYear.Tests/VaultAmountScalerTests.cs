using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class VaultAmountScalerTests
{
    private static BundleSpec Money(int amount, string name) => new(
        "Vault", 23, name, name, "O 286 3", 4, 1,
        new[] { new BundleSlotSpec("-1", amount, 0) });

    [Theory]
    [InlineData(2500, "2,500g", 3125, "3,125g")]
    [InlineData(5000, "5,000g", 6250, "6,250g")]
    [InlineData(10000, "10,000g", 12500, "12,500g")]
    [InlineData(25000, "25,000g", 31250, "31,250g")]
    public void Scale_125Percent_AmountsAndNames(int amount, string name, int expectedAmount, string expectedName)
    {
        var scaled = VaultAmountScaler.Scale(Money(amount, name), 1.25);
        Assert.Equal(expectedAmount, scaled.Slots[0].Stack);
        Assert.Equal(expectedName, scaled.Name);
        Assert.Equal(expectedName, scaled.DisplayName);
    }

    [Fact]
    public void Scale_MultiplierOne_SameReference()
    {
        var spec = Money(2500, "2,500g");
        Assert.Same(spec, VaultAmountScaler.Scale(spec, 1.0));
    }

    [Fact]
    public void Scale_NonMoneyName_AmountScalesNameKept()
    {
        var scaled = VaultAmountScaler.Scale(Money(2500, "The Missing"), 1.25);
        Assert.Equal(3125, scaled.Slots[0].Stack);
        Assert.Equal("The Missing", scaled.Name);
    }
}
