using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BookKeepTests
{
    [Fact]
    public void Table_has_nineteen_books_totalling_6850_jp()
    {
        Assert.Equal(19, BookKeepTable.Entries.Count);
        Assert.Equal(6850, BookKeepTable.Entries.Sum(e => e.Cost));
        Assert.Equal(19, BookKeepTable.Entries.Select(e => e.StatKey).Distinct().Count());
        Assert.All(BookKeepTable.Entries, e => Assert.StartsWith("Book_", e.StatKey));
    }

    [Theory]
    [InlineData("Book_PriceCatalogue", "keep_book_pricecatalogue", 150)]
    [InlineData("Book_Woodcutting", "keep_book_woodcutting", 350)]
    [InlineData("Book_Bombs", "keep_book_bombs", 500)]
    [InlineData("Book_Defense", "keep_book_defense", 600)]
    [InlineData("Book_Speed", "keep_book_speed", 750)]
    [InlineData("Book_Speed2", "keep_book_speed2", 750)]
    public void Bands_price_each_book(string statKey, string id, long cost)
    {
        BookKeep e = BookKeepTable.Entries.Single(x => x.StatKey == statKey);
        Assert.Equal(id, e.UpgradeId);
        Assert.Equal(cost, e.Cost);
    }

    [Fact]
    public void Only_speed_two_has_a_prerequisite()
    {
        Assert.Equal("keep_book_speed",
            BookKeepTable.Entries.Single(e => e.StatKey == "Book_Speed2").PrerequisiteId);
        Assert.All(BookKeepTable.Entries.Where(e => e.StatKey != "Book_Speed2"),
            e => Assert.Null(e.PrerequisiteId));
    }

    [Fact]
    public void Catalog_carries_every_book_as_a_reach_gated_carryover_row()
    {
        foreach (BookKeep e in BookKeepTable.Entries)
        {
            UpgradeDefinition? def = UpgradeCatalog.TryGet(e.UpgradeId);
            Assert.NotNull(def);
            Assert.Equal(UpgradeCategory.Carryover, def!.Category);
            Assert.Equal(e.Cost, def.Cost);
            Assert.Equal(e.PrerequisiteId, def.PrerequisiteId);
            Assert.Equal($"book:{e.StatKey}", def.RunReachRequirement);
        }
    }
}
