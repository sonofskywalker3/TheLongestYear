namespace TheLongestYear.Core;

/// <summary>One animal owed a second product today (Kitchen bonus animal_double_product). Plain
/// get/set POCO so MetaStore's JSON round-trips it (same pattern as BonusSlot).</summary>
public sealed class DoubleProduceRecord
{
    public long AnimalId { get; set; }
    public string ProduceId { get; set; } = "";
}
