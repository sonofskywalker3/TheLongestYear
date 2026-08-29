namespace TheLongestYear.Core;

/// <summary>One filled Community Center slot in the run's donation ledger, keyed by the vanilla
/// bundle index and the ingredient's position in that bundle's Data/Bundles line (category slots
/// included in the numbering, so the index lines up with the board's bool[]). ItemId is the
/// normalized qualified id for display and id-level asks. Plain POCO for save serialization.</summary>
public sealed class DonatedSlot
{
    public int BundleIndex { get; set; }
    public int IngredientIndex { get; set; }
    public string ItemId { get; set; } = "";
}
