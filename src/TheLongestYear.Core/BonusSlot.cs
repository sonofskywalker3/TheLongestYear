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

    /// <summary>True once the player has actually deposited into this exact slot this week.
    /// Required alongside the live CC flag before the goal counts: vanilla blanket-sets every
    /// ingredient flag in a bundle the moment the bundle completes (JunimoNoteMenu.cs:1009-1011),
    /// so in an n-of-m bundle the flag alone credits slots nobody filled (@ggrace67, 2026-08-26).
    /// Recorded by DonationService.OnItemDonated, which only ever sees real deposits.</summary>
    public bool Deposited { get; set; }

    /// <summary>True when the day-28 gate demands this line this season (rule A, tier 1). Set by
    /// SlotPoolBuilder; the sampler draws due lines before filler.</summary>
    public bool Due { get; set; }

    /// <summary>True once this goal's share of the weekly bonus has been paid (rule D). The
    /// idempotency guard against paying a goal twice across a save and reload.</summary>
    public bool Paid { get; set; }
}
