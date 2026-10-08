using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>One requirement the darkness rewrote this loop. Plain POCO for save serialization;
/// kept for tly_sabotage status, the log and the aura, never read back to re-apply (the tampered
/// board lives in MetaState.WrittenBoard and the live BundleData).</summary>
public sealed class TamperRecord
{
    public int BundleIndex { get; set; }
    public int IngredientIndex { get; set; }
    public string BundleName { get; set; } = "";
    public string OldItemId { get; set; } = "";
    /// <summary>The flavour the old slot named (a Dried Fruit's fruit, a Smoked Fish's fish), or
    /// null for a slot that named none. Saves written before 2026-10-07 lack the field and read
    /// back null, which the aura treats as every copy of the item, exactly as it did then.</summary>
    public string? OldFlavor { get; set; }
    public string NewItemId { get; set; } = "";
    public int Stack { get; set; } = 1;
    public int DayOfYear { get; set; }
}

/// <summary>What the thief took of one item: its id, its display name when taken, its Object
/// category (a fish keeps the same word in the plural) and how many units. Plain POCO for save
/// serialization.</summary>
public sealed class StolenStack
{
    public string ItemId { get; set; } = "";
    public string Name { get; set; } = "";
    public int Category { get; set; }
    public int Count { get; set; }
}

/// <summary>A morning report queued by the night pass: shown in the morning message box on the
/// next OnDayStarted (<see cref="MorningLines"/>), then cleared. Plain POCO for save serialization.</summary>
public sealed class SabotageReport
{
    public SabotageKind Kind { get; set; }
    /// <summary>Blight: crops that withered. Reversion and tampering: 1.</summary>
    public int Count { get; set; }
    /// <summary>Blight: what the thief took, one entry per item kind (designer, 2026-10-08: a
    /// thief steals, nothing spoils). Empty for a crops-only night. Saves written before then
    /// carried Spoiled and Missing counts instead; those read back empty here.</summary>
    public List<StolenStack> Stolen { get; set; } = new();
    public string BundleName { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string OldItemId { get; set; } = "";
    /// <summary>Tampering: the flavour the old slot named. Reversion: the flavour the emptied slot
    /// names. Null when none (and on older saves).</summary>
    public string? OldFlavor { get; set; }
    /// <summary>Reversion: how many the emptied slot asks for (0 on saves written before
    /// 2026-10-08, read as one).</summary>
    public int Stack { get; set; }
    /// <summary>Reversion: the bundle's name as its menu shows it (the display name field). Empty
    /// on older saves, which fall back to <see cref="BundleName"/>.</summary>
    public string BundleLabel { get; set; } = "";
}
