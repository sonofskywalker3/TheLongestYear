using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Nexus bug 1137151 (ShadowedAciexox, 2026-09-20): an Artisan slot asking for dried
/// fruit showed only "Dried", and a smoked fish slot only "Smoked", so there was no way to tell
/// what to hand in. Root cause is vanilla's: a Community Center bundle's ingredients parse as
/// (id, stack, quality) triples (Bundle.cs:133), with no field for the preserved item, so the
/// slot's flavor is always null and the menu falls back to the base display name — and the base
/// Name of these three objects is literally "Dried" / "Smoked".</summary>
public class FlavorlessBundleSlotsTests
{
    [Theory]
    [InlineData("(O)DriedFruit", "bundle-slot.any-dried-fruit")]
    [InlineData("(O)DriedMushrooms", "bundle-slot.any-dried-mushrooms")]
    [InlineData("(O)SmokedFish", "bundle-slot.any-smoked-fish")]
    public void The_three_flavorless_ids_get_a_label(string itemId, string key)
        => Assert.Equal(key, FlavorlessBundleSlots.LabelKeyFor(itemId));

    /// <summary>The menu hands us an item's unqualified ItemId, the pools hold qualified ids;
    /// both spellings have to land on the same rule.</summary>
    [Theory]
    [InlineData("DriedFruit", "bundle-slot.any-dried-fruit")]
    [InlineData("SmokedFish", "bundle-slot.any-smoked-fish")]
    public void An_unqualified_id_is_recognised_too(string itemId, string key)
        => Assert.Equal(key, FlavorlessBundleSlots.LabelKeyFor(itemId));

    /// <summary>Wine, Jelly and the rest are flavored goods too, but their base name already
    /// reads as a thing ("Wine"), so they are not relabelled.</summary>
    [Theory]
    [InlineData("(O)348")]
    [InlineData("(O)344")]
    [InlineData("(O)812")]
    [InlineData("(O)24")]
    [InlineData("")]
    [InlineData(null)]
    public void Everything_else_is_left_alone(string? itemId)
        => Assert.Null(FlavorlessBundleSlots.LabelKeyFor(itemId));
}
