using System;

namespace TheLongestYear.Core;

/// <summary>What kind of thing an item is, read from the game's own Data/Objects Category and
/// Type. The activity themes match goal lines by kind anywhere on the board.</summary>
public enum ItemKind { Other, Gem, Mineral, MonsterLoot, Artifact, ArtisanGood, Cooking, Egg, Milk, AnimalProduct }

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
    private const string ArchType = "Arch";

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
}
