using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Part B (spec 2026-09-15) owes a proof that board generation, gates, goals and pacing
/// never read the obtainability model or the darkness: byte-identical tly_genbundles and
/// tly_gatecheck across seeds. This is the static half; the live diff is in the plan's Task 9.</summary>
public class BoardPathsNeverReadTheModelTests
{
    private static readonly string[] BoardFiles =
    {
        "TheLongestYear.Core/ItemPoolBuilder.cs", "TheLongestYear.Core/BundleSlotFiller.cs",
        "TheLongestYear.Core/BoardRequirements.cs", "TheLongestYear.Core/GateEvaluator.cs",
        "TheLongestYear.Core/GoalObtainability.cs", "TheLongestYear.Core/BundleDeadlines.cs",
        "TheLongestYear.Core/QuantityAskPass.cs", "TheLongestYear.Core/AuthoredBundleComposer.cs",
        "TheLongestYear.Core/BundleGenerationTuning.cs", "TheLongestYear.Core/BonusItemSampler.cs",
        "TheLongestYear.Core/BonusSlotSampler.cs",
    };

    private static readonly string[] Forbidden = { "Obtainability", "FairnessRule", "SaveSnapshot", "NightRoll", "Sabotage" };

    private static string SrcRoot =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));

    [Fact]
    public void Board_files_never_name_the_model_or_the_darkness()
    {
        var hits = new List<string>();
        foreach (string relative in BoardFiles)
        {
            string path = Path.Combine(SrcRoot, relative.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) { hits.Add($"{relative}: missing (update the list)"); continue; }
            string text = File.ReadAllText(path);
            foreach (string word in Forbidden)
                if (Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b"))
                    hits.Add($"{relative}: {word}");
        }
        Assert.Empty(hits);
    }
}
