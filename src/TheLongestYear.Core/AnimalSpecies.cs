using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Maps a vanilla <c>FarmAnimal.type</c> to the species name the Start-with keeps gate on
/// (<c>species:Chicken</c>, <c>species:VoidChicken</c>, <c>species:Cow</c>...). Vanilla splits a
/// chicken into White/Brown/Blue and a cow into White/Brown, and spells the special chickens with a
/// space, so recording the raw type never matched a gate (spec 2026-09-25, section 1). Unknown types
/// (other animals, modded ones) pass through unchanged.</summary>
public static class AnimalSpecies
{
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["White Chicken"] = "Chicken",
            ["Brown Chicken"] = "Chicken",
            ["Blue Chicken"] = "Chicken",
            ["Void Chicken"] = "VoidChicken",
            ["Golden Chicken"] = "GoldenChicken",
            ["White Cow"] = "Cow",
            ["Brown Cow"] = "Cow",
            // The FarmAnimal constructor renames "Dairy Cow" to "Brown Cow"; old data may still say it.
            ["Dairy Cow"] = "Cow",
        };

    /// <summary>The gate species for a vanilla type; empty for a blank type.</summary>
    public static string Normalize(string? vanillaType)
    {
        if (string.IsNullOrWhiteSpace(vanillaType))
            return string.Empty;
        string trimmed = vanillaType.Trim();
        return Aliases.TryGetValue(trimmed, out string? species) ? species : trimmed;
    }

    /// <summary>True when a recorded name (normalized or a raw vanilla name an old save stored)
    /// is the required species. Case-insensitive, like the old gate.</summary>
    public static bool Matches(string recorded, string required)
        => string.Equals(Normalize(recorded), Normalize(required), StringComparison.OrdinalIgnoreCase);

    /// <summary>Adds the normalized species of <paramref name="vanillaType"/> to
    /// <paramref name="everOwned"/> unless an entry already matches it. True when it added one.</summary>
    public static bool Record(List<string> everOwned, string? vanillaType)
    {
        string species = Normalize(vanillaType);
        if (species.Length == 0 || everOwned.Any(owned => Matches(owned, species)))
            return false;
        everOwned.Add(species);
        return true;
    }
}
