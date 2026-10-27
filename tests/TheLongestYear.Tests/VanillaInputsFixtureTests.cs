using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using TheLongestYear.Core.Obtainability;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>The vanilla inputs fixture must hold only the unmodded game's data: a mod that edited
/// a data asset while the dump ran would leak its rows in.</summary>
public class VanillaInputsFixtureTests
{
    /// <summary>Vanilla ids of item types <see cref="VanillaItemIds"/> does not bake: the wallpaper and
    /// flooring sets of Data/AdditionalWallpaperFlooring ("MoreWalls:24", "MoreFloors:8"; the numbered
    /// ones are the base wallpapers.png/floors.png) and the two Data/Mannequins entries.</summary>
    private static readonly string[] UnbakedVanillaLocalPrefixes = { "MoreWalls:", "MoreFloors:" };
    private static readonly IReadOnlySet<string> UnbakedVanillaIds =
        new HashSet<string>(StringComparer.Ordinal) { "(M)MannequinFemale", "(M)MannequinMale" };
    private static readonly string[] WallpaperFlooringTypes = { "(WP)", "(FL)" };

    /// <summary>Vanilla item queries <see cref="ItemQueries.IsQuery"/> does not know yet: the Lost and
    /// Found shop's LOST_UNIQUE_ITEMS (ItemQueryResolver.cs 313).</summary>
    private static readonly IReadOnlySet<string> UnknownVanillaQueries =
        new HashSet<string>(StringComparer.Ordinal) { "LOST_UNIQUE_ITEMS" };

    /// <summary>The local part of every baked vanilla id: an unqualified shop ItemId ("AbigailsBow",
    /// "RetroTV") is looked up across every item type, as the game does.</summary>
    private static readonly IReadOnlySet<string> VanillaLocalIds =
        new HashSet<string>(VanillaItemIds.All.Select(id => id[(id.IndexOf(')') + 1)..]), StringComparer.Ordinal);

    [Fact]
    public void FixtureHasNoModRows()
    {
        ObtainabilityInputs inputs = VanillaInputsFixture.Load();

        Assert.NotEmpty(inputs.Objects);
        Assert.NotEmpty(inputs.Shops);

        string[] modShopItems = inputs.Shops
            .Where(r => !ItemQueries.IsQuery(r.ItemId) && !UnknownVanillaQueries.Contains(r.ItemId) && !IsCategory(r.ItemId) && !IsVanillaShopItem(r.ItemId))
            .Select(r => $"{r.ShopId}: {r.ItemId}")
            .ToArray();
        Assert.Empty(modShopItems);

        string[] modObjects = inputs.Objects.Keys.Where(id => !VanillaItemIds.All.Contains(id)).ToArray();
        Assert.Empty(modObjects);
    }

    private static bool IsVanillaShopItem(string itemId)
    {
        if (VanillaItemIds.Contains(itemId) || UnbakedVanillaIds.Contains(itemId)) return true;
        bool qualified = itemId.StartsWith('(');
        if (!qualified && VanillaLocalIds.Contains(itemId)) return true;
        string local = itemId;
        if (qualified)
        {
            string? type = WallpaperFlooringTypes.FirstOrDefault(t => itemId.StartsWith(t, StringComparison.Ordinal));
            if (type == null) return false;
            local = itemId[type.Length..];
            if (int.TryParse(local, out _)) return true;
        }
        return UnbakedVanillaLocalPrefixes.Any(p => local.StartsWith(p, StringComparison.Ordinal) && int.TryParse(local[p.Length..], out _));
    }

    /// <summary>A negative category number ("-75"), which the game reads as "any item of this category".</summary>
    private static bool IsCategory(string itemId) => int.TryParse(itemId, out int n) && n < 0;
}
