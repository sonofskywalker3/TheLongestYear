namespace TheLongestYear.Core;

/// <summary>
/// One sampled Community Center bundle slot — a weekly theme goal. Identified by
/// (BundleIndex, IngredientIndex) against the live CC slot state; ItemId/Stack/Quality/BundleName
/// are display copies captured at sampling time. Plain get/set POCO so MetaStore's JSON
/// round-trips it (same pattern as StashItemRecord).
/// </summary>
public sealed class BonusSlot
{
    public int BundleIndex { get; set; }
    public int IngredientIndex { get; set; }
    public string ItemId { get; set; } = "";
    public int Stack { get; set; } = 1;
    public int Quality { get; set; }
    public string BundleName { get; set; } = "";
}
