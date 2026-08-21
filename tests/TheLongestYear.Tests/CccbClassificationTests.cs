using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Classification completeness against a real third-party board: Challenging Community
/// Center Bundles (Nexus 6361) `Vanilla` pack, v3.1.0 — 48 bundle strings that swap INTO the
/// game's own keys at DayStarted. Vanilla mode must classify every themed one (spec 2026-08-21
/// audit item 5). Fixture: <c>Fixtures/cccb_vanilla.json</c> (name → bundle string).</summary>
public class CccbClassificationTests
{
    private static Dictionary<string, string> LoadPack()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "cccb_vanilla.json");
        var options = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
        var root = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(File.ReadAllText(path), options)!;
        return root.Values.First();
    }

    private static readonly HashSet<string> VaultNames = new(StringComparer.Ordinal)
    {
        "Vault/23", "Vault/24", "Vault/25", "Vault/26",
    };

    [Fact]
    public void Every_non_vault_non_joja_bundle_classifies_with_a_sane_ramp()
    {
        var pack = LoadPack();
        Assert.True(pack.Count >= 40, $"fixture has only {pack.Count} entries");

        int classified = 0;
        foreach (KeyValuePair<string, string> kvp in pack)
        {
            if (VaultNames.Contains(kvp.Key) || kvp.Key == "The Missing") continue;

            ParsedBundle parsed = BundleParsing.Parse($"Room/{classified}", kvp.Value);
            BundleRequirement? req = BundleClassifier.Classify(parsed, Theme.Mixed,
                new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas);

            Assert.True(req != null, $"'{kvp.Key}' classified to null: {kvp.Value}");
            Assert.True(req!.Ingredients.Count > 0, $"'{kvp.Key}' has no ingredients");
            Assert.All(req.Ingredients, id => Assert.StartsWith("(", id));   // bare string ids get qualified
            if (req.CumulativeRequiredBySeason != null)
            {
                var ramp = req.CumulativeRequiredBySeason;
                for (int i = 1; i < ramp.Count; i++)
                    Assert.True(ramp[i] >= ramp[i - 1], $"'{kvp.Key}' ramp not monotone");
                Assert.True(ramp[^1] <= req.NumberOfSlots, $"'{kvp.Key}' winter quota exceeds X");
            }
            classified++;
        }
        Assert.True(classified >= 40, $"only {classified} classified");
    }

    [Fact]
    public void Quality_and_stack_asks_are_carried_per_ingredient()
    {
        var pack = LoadPack();
        // Quality Fish asks for gold (4) fish ×5.
        var parsed = BundleParsing.Parse("Fish Tank/10", pack["Quality Fish"]);
        var req = BundleClassifier.Classify(parsed, Theme.Fishing,
            new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas)!;
        Assert.All(req.Ingredients, id => Assert.Equal(4, req.IngredientQualities[id]));
        Assert.All(req.Ingredients, id => Assert.Equal(5, req.IngredientStacks[id]));
    }

    [Fact]
    public void Bare_string_item_ids_are_qualified_as_objects()
    {
        var pack = LoadPack();
        var parsed = BundleParsing.Parse("Crafts Room/19", pack["Forest"]);
        var req = BundleClassifier.Classify(parsed, Theme.Foraging,
            new Dictionary<string, Season>(), GameplayConfig.DefaultBundleQuotas)!;
        Assert.Contains("(O)Moss", req.Ingredients);
        Assert.Contains("(O)RiverJelly", req.Ingredients);
    }
}
