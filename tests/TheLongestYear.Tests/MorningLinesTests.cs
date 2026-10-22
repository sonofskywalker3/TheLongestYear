using TheLongestYear.Core;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-08: the darkness's morning messages move to vanilla's corner box, and
/// the thief steals (nothing spoils), so his box lists what he took with counts.</summary>
public class MorningLinesTests
{
    private static StolenStack S(string id, string name, int count, int category = 0)
        => new() { ItemId = id, Name = name, Count = count, Category = category };

    [Theory]
    [InlineData("(O)24", "Parsnip", "1 Parsnip")]
    [InlineData("(O)348", "Wine", "1 bottle of Wine")]
    [InlineData("(O)303", "Pale Ale", "1 glass of Pale Ale")]
    [InlineData("(O)216", "Bread", "1 loaf of Bread")]
    [InlineData("(O)388", "Wood", "1 Wood")]
    public void One_of_a_thing_is_counted_with_its_singular_container(string id, string name, string expected)
        => Assert.Equal(expected, AskPhrases.Counted(1, id, name, ItemPluralsTests.VanillaPlural));

    [Theory]
    [InlineData("(O)24", "Parsnip", "3 Parsnips")]
    [InlineData("(O)348", "Wine", "3 bottles of Wine")]
    [InlineData("(O)388", "Wood", "3 Wood")]
    public void More_than_one_reads_as_the_ask(string id, string name, string expected)
        => Assert.Equal(expected, AskPhrases.Counted(3, id, name, ItemPluralsTests.VanillaPlural));

    [Fact]
    public void A_fish_keeps_its_word_when_counted()
        => Assert.Equal("2 Pike", AskPhrases.Counted(2, "(O)144", "Pike", ItemPluralsTests.VanillaPlural, -4));

    [Fact]
    public void Units_of_one_item_merge_and_the_biggest_comes_first()
    {
        var taken = new[] { S("(O)66", "Amethyst", 1), S("(O)24", "Parsnip", 1), S("(O)24", "Parsnip", 1), S("(O)66", "Amethyst", 1), S("(O)24", "Parsnip", 1), S("(O)348", "Wine", 1) };
        List<StolenStack> merged = MorningLines.Merge(taken);
        Assert.Equal(new[] { "Parsnip", "Amethyst", "Wine" }, merged.Select(m => m.Name));
        Assert.Equal(new[] { 3, 2, 1 }, merged.Select(m => m.Count));
    }

    [Fact]
    public void A_flavoured_good_stays_apart_from_another_flavour()
    {
        List<StolenStack> merged = MorningLines.Merge(new[] { S("(O)348", "Blueberry Wine", 1), S("(O)348", "Wine", 1) });
        Assert.Equal(2, merged.Count);
    }

    [Fact]
    public void The_list_names_five_kinds_and_counts_the_rest()
    {
        var merged = MorningLines.Merge(new[]
        {
            S("(O)24", "Parsnip", 6), S("(O)66", "Amethyst", 5), S("(O)348", "Wine", 4),
            S("(O)388", "Wood", 3), S("(O)330", "Clay", 3), S("(O)390", "Stone", 2), S("(O)92", "Sap", 1),
        });
        (List<string> named, int other) = MorningLines.StolenPhrases(merged, ItemPluralsTests.VanillaPlural);
        Assert.Equal(new[] { "6 Parsnips", "5 Amethysts", "4 bottles of Wine", "3 Wood", "3 Clay" }, named);
        Assert.Equal(3, other);
    }

    [Theory]
    [InlineData(new[] { "1 Parsnip" }, "1 Parsnip")]
    [InlineData(new[] { "1 Parsnip", "2 Wood" }, "1 Parsnip and 2 Wood")]
    [InlineData(new[] { "1 Parsnip", "2 Wood", "3 other things" }, "1 Parsnip, 2 Wood and 3 other things")]
    public void The_list_reads_as_English(string[] parts, string expected)
        => Assert.Equal(expected, MorningLines.JoinList(parts));

    [Fact]
    public void Units_add_up_what_was_taken()
        => Assert.Equal(5, MorningLines.Units(new[] { S("a", "A", 2), S("b", "B", 3) }));

    [Theory]
    [InlineData(1, MorningLines.VanillaBoxMs)]
    [InlineData(3, MorningLines.VanillaBoxMs)]
    [InlineData(5, MorningLines.VanillaBoxMs + 2 * MorningLines.ExtraLineMs)]
    public void A_long_box_stays_up_longer(int lines, int ms)
        => Assert.Equal(ms, MorningLines.BoxDurationMs(lines));

    [Fact]
    public void An_old_save_report_with_spoiled_and_missing_reads_back_with_nothing_stolen()
    {
        var report = System.Text.Json.JsonSerializer.Deserialize<SabotageReport>("{\"Kind\":0,\"Count\":2,\"Spoiled\":3,\"Missing\":1}")!;
        Assert.Equal(2, report.Count);
        Assert.Empty(report.Stolen);
    }

    [Fact]
    public void A_thief_report_round_trips_its_list()
    {
        var report = new SabotageReport { Kind = SabotageKind.Blight, Stolen = new() { S("(O)24", "Parsnip", 3) } };
        var back = System.Text.Json.JsonSerializer.Deserialize<SabotageReport>(System.Text.Json.JsonSerializer.Serialize(report))!;
        Assert.Equal("Parsnip", Assert.Single(back.Stolen).Name);
        Assert.Equal(3, back.Stolen[0].Count);
    }
}

/// <summary>Designer, 2026-10-08: the hall's morning box names what came undone, no count.</summary>
public class RevertedCalloutTests
{
    private static (string, bool) Item(int stack, string name, string id, int category = AskPhrases.NoCategory)
        => MorningLines.RevertedItem(stack, name, id, flavored: false, ItemPluralsTests.VanillaPlural, category);

    [Fact] public void One_is_the_bare_name() => Assert.Equal(("Parsnip", false), Item(1, "Parsnip", "(O)24"));
    [Fact] public void Several_is_the_bare_plural_with_are() => Assert.Equal(("Parsnips", true), Item(5, "Parsnip", "(O)24"));
    [Fact] public void A_mass_noun_stays_singular_with_is() => Assert.Equal(("Wild Honey", false), Item(3, "Wild Honey", "(O)340"));
    [Fact] public void A_container_good_has_no_container_phrase() => Assert.Equal(("Wine", false), Item(3, "Wine", "(O)348"));
    [Fact] public void A_fish_keeps_its_word_and_takes_are()
        => Assert.Equal(("Pike", true), Item(3, "Pike", "(O)144", FlavoredSlotRules.FishCategory));
    [Fact] public void A_cucumber_takes_its_plural()
        => Assert.Equal(("Sea Cucumbers", true), Item(3, "Sea Cucumber", "(O)154", FlavoredSlotRules.FishCategory));

    [Theory]
    [InlineData("Spring Crops", false)]
    [InlineData("Abigail's Bundle", true)]
    [InlineData("Odd bundle ", true)]
    [InlineData("", false)]
    public void A_label_that_says_bundle_is_not_doubled(string label, bool says)
        => Assert.Equal(says, MorningLines.LabelSaysBundle(label));

    private const string Value = "Spring Crops/O 465 20/24 1 0 188 5 0 190 1 0/0/4/0/Spring Crops Display";

    [Fact] public void The_slot_stack_is_read_off_the_board()
    {
        Assert.Equal(1, BundleDataTamper.StackAt(Value, 0));
        Assert.Equal(5, BundleDataTamper.StackAt(Value, 1));
        Assert.Equal(0, BundleDataTamper.StackAt(Value, 3));
    }

    [Fact] public void The_label_is_the_display_name_field() => Assert.Equal("Spring Crops Display", BundleDataTamper.LabelOf(Value));
    [Fact] public void No_display_name_falls_back_to_the_name() => Assert.Equal("Spring Crops", BundleDataTamper.LabelOf("Spring Crops/O 465 20/24 1 0/0/4/0"));
}
