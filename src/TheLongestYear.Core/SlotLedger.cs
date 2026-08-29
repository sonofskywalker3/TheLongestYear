using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Read view of the per-slot donation ledger for the gate, the page and the sims.
/// A slot is filled or not; counts are per bundle. <see cref="ItemIds"/> is the distinct id set
/// for logging and any id-level question (never for progress counting).</summary>
public sealed class SlotLedger
{
    private readonly List<DonatedSlot> _entries = new();
    private readonly HashSet<(int Bundle, int Slot)> _filled = new();
    private readonly Dictionary<int, int> _countByBundle = new();
    private readonly HashSet<string> _ids = new(System.StringComparer.Ordinal);

    public SlotLedger() { }

    public SlotLedger(IEnumerable<DonatedSlot> slots)
    {
        if (slots == null) return;
        foreach (DonatedSlot s in slots)
            Add(s.BundleIndex, s.IngredientIndex, s.ItemId);
    }

    public IReadOnlyList<DonatedSlot> Entries => _entries;
    public IReadOnlySet<string> ItemIds => _ids;
    public int Count => _entries.Count;

    public bool IsFilled(int bundleIndex, int ingredientIndex) => _filled.Contains((bundleIndex, ingredientIndex));

    public int FilledCount(int bundleIndex) => _countByBundle.TryGetValue(bundleIndex, out int n) ? n : 0;

    /// <summary>Record a filled slot. False when it was already in the ledger.</summary>
    public bool Add(int bundleIndex, int ingredientIndex, string itemId)
    {
        if (!_filled.Add((bundleIndex, ingredientIndex))) return false;
        _entries.Add(new DonatedSlot { BundleIndex = bundleIndex, IngredientIndex = ingredientIndex, ItemId = itemId ?? "" });
        _countByBundle[bundleIndex] = FilledCount(bundleIndex) + 1;
        if (!string.IsNullOrEmpty(itemId)) _ids.Add(itemId);
        return true;
    }
}
