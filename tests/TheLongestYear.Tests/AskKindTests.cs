using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class AskKindTests
{
    private static readonly Dictionary<string, RawObjectEntry> Objects = new()
    {
        ["145"] = new RawObjectEntry("Fish", -4, 100, false, Array.Empty<string>()),                          // Sunfish
        ["CaveJelly"] = new RawObjectEntry("Basic", 0, 100, false, new[] { "fish_nonfish", "counts_as_fish_catch" }),
        ["24"] = new RawObjectEntry("Basic", -75, 35, false, Array.Empty<string>()),                          // Parsnip
        ["296"] = new RawObjectEntry("Basic", -79, 5, false, new[] { "forage_item" }),                        // Salmonberry
        ["16"] = new RawObjectEntry("Basic", -81, 40, false, Array.Empty<string>()),                          // Wild Horseradish
        ["430"] = new RawObjectEntry("Basic", 0, 625, false, Array.Empty<string>()),                          // Truffle
        ["334"] = new RawObjectEntry("Basic", -15, 60, false, Array.Empty<string>()),                         // Copper Bar
        ["176"] = new RawObjectEntry("Basic", -5, 50, false, Array.Empty<string>()),                          // Egg
        ["194"] = new RawObjectEntry("Cooking", -7, 35, false, Array.Empty<string>()),                        // Fried Egg
        ["DriedFruit"] = new RawObjectEntry("Basic", -26, 10, false, Array.Empty<string>()),
        ["388"] = new RawObjectEntry("Basic", -16, 2, false, Array.Empty<string>()),                          // Wood
    };

    private static AskKind Kind(string itemRef) => AskKinds.Classify(itemRef, id => Objects.GetValueOrDefault(id));

    [Theory]
    [InlineData("145", AskKind.Fish)]
    [InlineData("(O)CaveJelly", AskKind.Fish)]
    [InlineData("24", AskKind.Crops)]
    [InlineData("296", AskKind.Forage)]
    [InlineData("16", AskKind.Forage)]
    [InlineData("430", AskKind.Forage)]
    [InlineData("334", AskKind.Minerals)]
    [InlineData("176", AskKind.AnimalProducts)]
    [InlineData("194", AskKind.CookedDishes)]
    [InlineData("388", AskKind.Other)]
    [InlineData("(BC)10", AskKind.Other)]
    [InlineData("999", AskKind.Other)]
    public void An_item_ask_classifies_by_its_object_row(string itemRef, AskKind expected)
        => Assert.Equal(expected, Kind(itemRef));

    [Theory]
    [InlineData("-4", AskKind.Fish)]
    [InlineData("-75", AskKind.Crops)]
    [InlineData("-81", AskKind.Forage)]
    [InlineData("-12", AskKind.Minerals)]
    [InlineData("-6", AskKind.AnimalProducts)]
    [InlineData("-26", AskKind.ArtisanGoods)]
    [InlineData("-16", AskKind.Other)]
    public void A_category_slot_classifies_by_its_category(string itemRef, AskKind expected)
        => Assert.Equal(expected, Kind(itemRef));

    [Fact]
    public void A_money_slot_is_gold()
        => Assert.Equal(AskKind.Gold, Kind(AskKinds.MoneyRef));

    [Fact]
    public void A_flavored_slot_counts_as_its_output()
        => Assert.Equal(AskKind.ArtisanGoods, Kind(FlavoredSlotRules.WrittenIdFor(FlavoredSlotRules.DriedFruit)));
}
