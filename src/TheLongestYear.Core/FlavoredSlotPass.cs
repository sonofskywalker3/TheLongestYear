using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Names the input on every flavored slot of one bundle, and re-rolls that slot's stack
/// against what the named input actually yields. Plan 2026-09-21-flavored-bundle-slots, step 3.
///
/// Runs after <see cref="QuantityAskPass"/>, which has already banded these ids off the
/// <see cref="QuantityBasisTables.Stations"/> throughput of 35 a week. That number is what five
/// dehydrators can MAKE, and it was a fair basis while the slot took any fruit. Once the slot
/// names one fruit the binding constraint is the fruit, not the machine: the dehydrator eats five
/// per dried good, so 18 Dried Apples means 90 apples. This pass replaces the stack with one
/// rolled off the named input's own supply (see <see cref="FlavoredSlotRules.BasisFor"/>).
///
/// The chosen inputs come back so the caller can persist them beside the board. They are NOT
/// re-derived at load: the board's own data is stored rather than regenerated for exactly this
/// reason (MetaState.WrittenBoard), because a data mod that shifts the pools would otherwise
/// change a slot's fruit underneath a run in progress.
///
/// Core-only: pools, week and deadline all arrive as arguments.</summary>
public static class FlavoredSlotPass
{
    /// <summary>Salt for the stack re-roll, distinct from the flavor pick's own stream so the
    /// stack a slot asks for cannot shift the fruit it names.</summary>
    private const int StackSalt = 0x2B1D;

    /// <param name="weekOf">The week a rule places an id at, or null for an id nothing placed.</param>
    /// <param name="deadlineFor">The season a slot's ask is due, as the classifier will give it.</param>
    /// <param name="flavors">Slot index to the input id that slot now names. Empty when the
    /// bundle has no flavored slot, or when nothing was reachable in time for one.</param>
    public static BundleSpec Apply(
        BundleSpec spec,
        int seed,
        DifficultyProfile profile,
        ItemPools pools,
        Func<string, int?> weekOf,
        Func<string, Season?> deadlineFor,
        out IReadOnlyDictionary<int, string> flavors)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        if (weekOf == null) throw new ArgumentNullException(nameof(weekOf));
        if (deadlineFor == null) throw new ArgumentNullException(nameof(deadlineFor));

        Dictionary<int, string>? chosen = null;
        List<BundleSlotSpec>? rewritten = null;

        for (int i = 0; i < spec.Slots.Count; i++)
        {
            BundleSlotSpec slot = spec.Slots[i];
            if (!FlavoredSlotRules.IsFlavored(slot.ItemId)) continue;

            string baseId = BundleParsing.NormalizeItemId(slot.ItemId);
            int deadlineWeek = deadlineFor(baseId) is Season due
                ? AvailabilityWeeks.LastWeekOf(due)
                : Calendar.WeeksPerYear;

            IReadOnlyList<string> candidates = FlavoredSlotRules.CandidatesFor(baseId, pools, weekOf, deadlineWeek);
            // Nothing reachable in time: leave the slot flavorless rather than name something the
            // player cannot get. It keeps its "Any ..." label and its throughput-based stack.
            if (FlavoredSlotRules.Pick(seed, spec.Index, i, candidates) is not string input) continue;

            chosen ??= new Dictionary<int, string>();
            chosen[i] = input;

            double? inputBasis = QuantityAskPass.BasisByDeadline(input, deadlineFor(input));
            // An input with no supply row of its own (a modded fruit) keeps the machine basis
            // rather than falling to 1: the ask stays honest about the machine's limit.
            double basis = FlavoredSlotRules.BasisFor(
                baseId, inputBasis ?? FlavoredSlotRules.StationThroughput * FlavoredSlotRules.InputRatioFor(baseId));

            var rng = new Random(seed ^ (spec.Index * StackSalt) ^ (i + 1));
            int stack = AskBands.Roll(basis, profile, rng);
            if (stack == slot.Stack) continue;

            rewritten ??= spec.Slots.ToList();
            rewritten[i] = slot with { Stack = stack };
        }

        flavors = chosen ?? (IReadOnlyDictionary<int, string>)EmptyFlavors;
        return rewritten == null ? spec : spec with { Slots = rewritten };
    }

    private static readonly Dictionary<int, string> EmptyFlavors = new();

    /// <summary>The persisted key for one flavored slot: the bundle index as the board data keys
    /// it, then the slot's position. Read back by the mod when it sets the flavor on the live
    /// bundle, so the two must agree on this spelling.</summary>
    public static string KeyFor(int bundleIndex, int slotIndex) => $"{bundleIndex}:{slotIndex}";
}
