using System;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What kind of thing an item is, read from the game's own Data/Objects Category and
/// Type. The activity themes match goal lines by kind anywhere on the board.</summary>
public enum ItemKind
{
    Other, Gem, Mineral, MonsterLoot, Artifact, ArtisanGood, Cooking, Egg, Milk, AnimalProduct,
    Resource, Seed, Sapling, Book, Trophy, Totem, Essence,
}

public static class ItemKindClassifier
{
    private const int GemCategory = -2;
    private const int MineralCategory = -12;
    private const int MonsterLootCategory = -28;
    private const int ArtisanCategory = -26;
    private const int CookingCategory = -7;
    private const int EggCategory = -5;
    private const int MilkCategory = -6;
    private const int AnimalProductCategory = -18;
    private const int ResourceCategory = -16;
    private const int SeedCategory = -74;
    private const string ArchType = "Arch";
    private const string BookType = "Book";
    private const string BookItemTag = "book_item";
    private const string SaplingNameFragment = "Sapling";
    private const string TotemNameSuffix = " Totem";
    private const string EssenceNameSuffix = " Essence";

    public static ItemKind From(int category, string? type)
    {
        if (string.Equals(type, ArchType, StringComparison.OrdinalIgnoreCase)) return ItemKind.Artifact;
        return category switch
        {
            GemCategory => ItemKind.Gem,
            MineralCategory => ItemKind.Mineral,
            MonsterLootCategory => ItemKind.MonsterLoot,
            ArtisanCategory => ItemKind.ArtisanGood,
            CookingCategory => ItemKind.Cooking,
            EggCategory => ItemKind.Egg,
            MilkCategory => ItemKind.Milk,
            AnimalProductCategory => ItemKind.AnimalProduct,
            _ => ItemKind.Other,
        };
    }

    /// <summary>Richer classification using the full Data/Objects row: Arch type -> Artifact;
    /// Book type/tag -> Book; a Gil's Trophies id -> Trophy; category -16 -> Resource; category
    /// -74 -> Sapling (name contains "Sapling") or Seed; then the existing category switch
    /// (<see cref="From(int, string?)"/>, unchanged so ThemeDomains/weekly-theme matching keeps
    /// working). Name-suffix Totem/Essence checks only apply as a LAST-RESORT fallback when
    /// nothing above matched: Totems have no dedicated category (Data/Objects category 0), but
    /// Solar/Void Essence are already correctly categorized MonsterLoot (-28) and must stay
    /// there rather than being reclassified by their name.</summary>
    public static ItemKind From(string bareId, RawObjectEntry obj)
    {
        if (string.Equals(obj.Type, ArchType, StringComparison.OrdinalIgnoreCase))
            return ItemKind.Artifact;
        if (string.Equals(obj.Type, BookType, StringComparison.OrdinalIgnoreCase)
            || (obj.ContextTags != null && obj.ContextTags.Contains(BookItemTag)))
            return ItemKind.Book;

        string qualifiedId = BundleParsing.NormalizeItemId(bareId);
        if (AuthoredBundleCatalog.GilTrophies.Contains(qualifiedId))
            return ItemKind.Trophy;

        if (obj.Category == ResourceCategory)
            return ItemKind.Resource;
        if (obj.Category == SeedCategory)
        {
            return !string.IsNullOrEmpty(obj.Name)
                && obj.Name.Contains(SaplingNameFragment, StringComparison.OrdinalIgnoreCase)
                ? ItemKind.Sapling
                : ItemKind.Seed;
        }

        ItemKind byCategory = From(obj.Category, obj.Type);
        if (byCategory != ItemKind.Other)
            return byCategory;

        if (!string.IsNullOrEmpty(obj.Name))
        {
            if (obj.Name.EndsWith(TotemNameSuffix, StringComparison.Ordinal))
                return ItemKind.Totem;
            if (obj.Name.EndsWith(EssenceNameSuffix, StringComparison.Ordinal))
                return ItemKind.Essence;
        }
        return ItemKind.Other;
    }
}
