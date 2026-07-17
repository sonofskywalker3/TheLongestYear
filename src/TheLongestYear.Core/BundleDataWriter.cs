using System;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Constructs vanilla BundleData dictionary entries from <see cref="BundleSpec"/>s —
/// the write-side inverse of <see cref="BundleParsing"/>. Format (BundleParsing.cs doc):
/// key = "Room/index"; value = name/reward/ingredients/color/numberOfSlots/sprite/displayName,
/// ingredients = space-separated "id stack quality" triples. The sprite field is left empty
/// (vanilla derives the sheet index from color/index when absent, matching remix output).</summary>
public static class BundleDataWriter
{
    public static string Key(BundleSpec spec) => $"{spec.Room}/{spec.Index}";

    public static string Value(BundleSpec spec)
    {
        if (spec.RewardField != null && spec.RewardField.Contains('/'))
            throw new ArgumentException(
                $"BundleSpec '{spec.Name}' has a '/' in RewardField '{spec.RewardField}' — a slashed " +
                "reward would shift every later field of the slash-delimited BundleData value.",
                nameof(spec));

        string name = Sanitize(spec.Name);
        string displayName = Sanitize(spec.DisplayName);
        string ingredients = string.Join(" ",
            spec.Slots.Select(s => $"{s.ItemId} {s.Stack} {s.Quality}"));
        return $"{name}/{spec.RewardField}/{ingredients}/{spec.Color}/{spec.NumberOfSlots}//{displayName}";
    }

    /// <summary>'/' is the BundleData field delimiter; a name containing it (possible for
    /// mod-authored RandomBundles variants) would corrupt the written value. Downstream
    /// name-matching (SlotPoolBuilder, quota tables) sees the SANITIZED name via the
    /// round-trip, so matching stays consistent (review-carried slash-guard requirement).</summary>
    private static string Sanitize(string text) => text?.Replace('/', '-') ?? "";
}
