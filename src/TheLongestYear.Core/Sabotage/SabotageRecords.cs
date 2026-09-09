namespace TheLongestYear.Core.Sabotage;

/// <summary>One requirement the darkness rewrote this loop. Plain POCO for save serialization;
/// kept for tly_sabotage status and the log, never read back to re-apply (the tampered board
/// lives in MetaState.WrittenBoard and the live BundleData).</summary>
public sealed class TamperRecord
{
    public int BundleIndex { get; set; }
    public int IngredientIndex { get; set; }
    public string BundleName { get; set; } = "";
    public string OldItemId { get; set; } = "";
    public string NewItemId { get; set; } = "";
    public int DayOfYear { get; set; }
}

/// <summary>A morning report queued by the night pass: shown as a HUD line on the next
/// OnDayStarted, then cleared. Plain POCO for save serialization.</summary>
public sealed class SabotageReport
{
    public SabotageKind Kind { get; set; }
    public int Count { get; set; }
    public string BundleName { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string OldItemId { get; set; } = "";
}
