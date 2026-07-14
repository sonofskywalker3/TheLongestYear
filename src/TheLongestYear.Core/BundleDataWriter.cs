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
        string ingredients = string.Join(" ",
            spec.Slots.Select(s => $"{s.ItemId} {s.Stack} {s.Quality}"));
        return $"{spec.Name}/{spec.RewardField}/{ingredients}/{spec.Color}/{spec.NumberOfSlots}//{spec.DisplayName}";
    }
}
