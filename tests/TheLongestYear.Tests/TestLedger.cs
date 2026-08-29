using System.Collections.Generic;
using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>Builds a SlotLedger for tests by naming ids per requirement: every slot of the
/// requirement whose id is named is filled. Multi-bundle tests must give each requirement its own
/// bundleIndex (the factories default to -1, and two -1 bundles would share slots).</summary>
internal static class TestLedger
{
    public static SlotLedger Empty() => new SlotLedger();

    public static SlotLedger Fill(BundleRequirement req, params string[] ids)
    {
        var ledger = new SlotLedger();
        FillInto(ledger, req, ids);
        return ledger;
    }

    public static SlotLedger Fill(params (BundleRequirement Req, string Id)[] fills)
    {
        var ledger = new SlotLedger();
        foreach (var (req, id) in fills)
            FillInto(ledger, req, new[] { id });
        return ledger;
    }

    /// <summary>Fill every slot of every requirement whose id is named (ids shared across bundles
    /// fill a slot in each). Requirements need distinct bundle indexes.</summary>
    public static SlotLedger Fill(IEnumerable<BundleRequirement> reqs, params string[] ids)
    {
        var ledger = new SlotLedger();
        foreach (BundleRequirement req in reqs)
            FillInto(ledger, req, ids);
        return ledger;
    }

    private static void FillInto(SlotLedger ledger, BundleRequirement req, IEnumerable<string> ids)
    {
        var wanted = new HashSet<string>(ids, System.StringComparer.Ordinal);
        foreach (BundleSlot slot in req.Slots)
            if (wanted.Contains(slot.ItemId))
                ledger.Add(req.BundleIndex, slot.IngredientIndex, slot.ItemId);
    }
}
