using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Keeps the load-time board repair stable (spec 2026-10-08-custom-board-vanilla-only,
/// addendum 3, 0.19.9). The repair re-draws an unreachable ask; two things made the draw change
/// between loads of the same loop: its seed was the run seed, which a new game only assigns AFTER
/// the first repair has run, and it walked the board in the live dictionary's insertion order, so
/// the board-wide no-repeat set filled in a different order. And on a save with a stored board of
/// record (TLY Custom always; Normal/Remixed with Tech's Cross-Mod Bundles) the repaired item never
/// reached the stored copy, so the next restore put the unreachable ask back and the repair drew
/// again on every load.</summary>
public static class BoardRepairStability
{
    /// <summary>Keeps the repair's stream apart from the engine's generation stream on the same basis.</summary>
    private const int RepairSalt = 0x5EED_0B0A;

    private const int IngredientFieldIndex = 2;

    /// <summary>The repair seed: the board's own seed basis (farmer id + bundle seed loop), the same
    /// on every load of one loop and for a held board, and independent of the run seed.</summary>
    public static int Seed(ulong farmerId, int bundleSeedLoop)
        => unchecked(BundleEngineSeed.For(farmerId, bundleSeedLoop) ^ RepairSalt);

    /// <summary>The order the repair walks the board in: ordinal by key, whatever order the live
    /// dictionary was filled in.</summary>
    public static IReadOnlyList<string> ScanOrder(IEnumerable<string> keys)
        => keys.OrderBy(k => k, StringComparer.Ordinal).ToList();

    /// <summary>Writes a repaired ingredient field into the stored board of record, keeping that
    /// value's other fields. Returns false (and changes nothing) when there is no stored board, the
    /// key is not on it, or the stored value has no ingredient field.</summary>
    public static bool MirrorIngredients(IDictionary<string, string>? stored, string key, string ingredients)
    {
        if (stored == null || !stored.TryGetValue(key, out string? value) || value == null)
            return false;
        string[] fields = value.Split('/');
        if (fields.Length <= IngredientFieldIndex)
            return false;
        fields[IngredientFieldIndex] = ingredients;
        stored[key] = string.Join("/", fields);
        return true;
    }
}
