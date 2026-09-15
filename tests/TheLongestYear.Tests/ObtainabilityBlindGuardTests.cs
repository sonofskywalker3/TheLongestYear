using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace TheLongestYear.Tests;

/// <summary>Phase 1 of the obtainability model is built BLIND (spec 2026-09-14): it must come up with
/// its answers from game data alone, so it can be compared honestly against the existing model. This
/// fails if any blind file is missing, or names the existing model, its tables or its builders, in code
/// or comments, in any letter case.</summary>
public class ObtainabilityBlindGuardTests
{
    private static readonly string[] Forbidden =
    {
        "ItemAvailabilityModel", "ItemAvailability", "ItemEffort", "AvailabilityWeeks",
        "Core.Availability", "DefaultItemSeasonPins", "QuantityBasisTables", "ItemPoolBuilder",
        "GameDataPools", "GameEffortData", "LegendaryFishRules", "LocationGating", "MineAreas",
        "ItemAvailabilityBuilder", "BundleGenerationTuning", "EffortData", "PacingWeek", "HardWeek",
        "SeasonPins", "UnlockWeeks", "BasisByDeadline",
    };

    private static string SrcRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    /// <summary>Every file that must be blind, relative to src/.</summary>
    private static readonly string[] BlindFiles =
    {
        "TheLongestYear.Core/Obtainability/WeekMask.cs",
        "TheLongestYear.Core/Obtainability/DayTable.cs",
        "TheLongestYear.Core/Obtainability/ObtainTypes.cs",
        "TheLongestYear.Core/Obtainability/ObtainabilityModel.cs",
        "TheLongestYear.Core/Obtainability/ObtainabilityInputs.cs",
        "TheLongestYear.Core/Obtainability/ConditionSeasons.cs",
        "TheLongestYear.Core/Obtainability/ItemQueries.cs",
        "TheLongestYear.Core/Obtainability/SpawnSources.cs",
        "TheLongestYear.Core/Obtainability/ShopSources.cs",
        "TheLongestYear.Core/Obtainability/MineSources.cs",
        "TheLongestYear.Core/Obtainability/CodeSources.cs",
        "TheLongestYear.Core/Obtainability/GrowSources.cs",
        "TheLongestYear.Core/Obtainability/MadeSources.cs",
        "TheLongestYear.Core/Obtainability/ObtainabilityBuilder.cs",
        "TheLongestYear.Core/Obtainability/ObtainabilityText.cs",
        "TheLongestYear/Loop/GameObtainabilityData.cs",
    };

    private static string PathOf(string relative) => Path.Combine(SrcRoot, relative.Replace('/', Path.DirectorySeparatorChar));

    [Fact]
    public void Every_blind_file_exists()
        => Assert.Empty(BlindFiles.Where(f => !File.Exists(PathOf(f))));

    [Fact]
    public void Nothing_else_hides_in_the_blind_folder_unlisted()
    {
        string folder = Path.Combine(SrcRoot, "TheLongestYear.Core", "Obtainability");
        var listed = BlindFiles.Select(PathOf).Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unlisted = Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFullPath).Where(f => !listed.Contains(f)).Select(Path.GetFileName).ToList();
        Assert.Empty(unlisted);
    }

    [Fact]
    public void No_blind_file_names_the_existing_model()
    {
        var hits = new List<string>();
        foreach (string file in BlindFiles.Select(PathOf).Where(File.Exists))
        {
            string text = File.ReadAllText(file);
            foreach (string word in Forbidden)
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b", RegexOptions.IgnoreCase))
                    hits.Add($"{Path.GetFileName(file)}: {word}");
        }
        Assert.Empty(hits);
    }
}
