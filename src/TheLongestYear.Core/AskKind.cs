using System;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>The kinds of board ask the shrine's suggestions group by (spec 2026-11-10 shrine
/// planning, amendment C). Its own small taxonomy, not <see cref="ItemKind"/>: an ask the table
/// has no keep for is <see cref="Other"/> and is never suggested.</summary>
public enum AskKind
{
    Other,
    Fish,
    Crops,
    Forage,
    Minerals,
    AnimalProducts,
    CookedDishes,
    ArtisanGoods,
    Gold,
}

/// <summary>Classifies one bundle ingredient by what it asks for: a category slot by its category,
/// an item by its Data/Objects row. A flavored slot is written as its output's id (Dried Fruit,
/// Smoked Fish, Jelly), so it classifies as that output.</summary>
public static class AskKinds
{
    /// <summary>The ingredient id vanilla uses for a gold payment (the Vault).</summary>
    public const string MoneyRef = "-1";

    private const int GemCategory = -2;
    private const int FishCategory = -4;
    private const int EggCategory = -5;
    private const int MilkCategory = -6;
    private const int CookingCategory = -7;
    private const int MineralCategory = -12;
    private const int MetalCategory = -15;          // ores, bars, coal
    private const int AnimalProductCategory = -18;
    private const int BeachFindCategory = -23;      // coral, sea urchin, nautilus shell
    private const int ArtisanCategory = -26;
    private const int SyrupCategory = -27;          // tapper goods
    private const int VegetableCategory = -75;
    private const int FruitCategory = -79;
    private const int FlowerCategory = -80;
    private const int ForageCategory = -81;
    private const string ForageItemTag = "forage_item";
    private const string ObjectQualifier = "(O)";

    /// <param name="itemRef">The ingredient id as the bundle data writes it.</param>
    /// <param name="objectOf">Data/Objects row for an unqualified object id, or null.</param>
    public static AskKind Classify(string itemRef, Func<string, RawObjectEntry?> objectOf)
    {
        if (string.IsNullOrEmpty(itemRef)) return AskKind.Other;
        if (itemRef == MoneyRef) return AskKind.Gold;
        if (BundleParsing.IsCategoryRef(itemRef))
            return ByCategory(int.Parse(itemRef));

        string qualified = BundleParsing.NormalizeItemId(itemRef);
        if (!qualified.StartsWith(ObjectQualifier, StringComparison.Ordinal)) return AskKind.Other;
        RawObjectEntry? obj = objectOf(BundleParsing.StripQualifier(qualified));
        if (obj == null) return AskKind.Other;
        return ByObject(qualified, obj);
    }

    private static AskKind ByObject(string qualifiedId, RawObjectEntry obj)
    {
        if (obj.Category == FishCategory || ItemPoolBuilder.IsJellyCatch(obj)) return AskKind.Fish;
        bool forageTagged = obj.ContextTags != null && obj.ContextTags.Contains(ForageItemTag);
        if (forageTagged) return AskKind.Forage;
        AskKind byCategory = ByCategory(obj.Category);
        if (byCategory != AskKind.Other) return byCategory;
        // Truffle and any other object the game itself counts as forage.
        return ItemPoolBuilder.IsForageCategory(obj, qualifiedId) ? AskKind.Forage : AskKind.Other;
    }

    private static AskKind ByCategory(int category) => category switch
    {
        FishCategory => AskKind.Fish,
        VegetableCategory or FruitCategory or FlowerCategory => AskKind.Crops,
        ForageCategory or BeachFindCategory => AskKind.Forage,
        GemCategory or MineralCategory or MetalCategory => AskKind.Minerals,
        EggCategory or MilkCategory or AnimalProductCategory => AskKind.AnimalProducts,
        CookingCategory => AskKind.CookedDishes,
        ArtisanCategory or SyrupCategory => AskKind.ArtisanGoods,
        _ => AskKind.Other,
    };
}
