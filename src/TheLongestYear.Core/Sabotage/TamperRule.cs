using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>One item the tamper roll may swap in: its id, room theme and effort, already
/// filtered by the glue to things the availability model places in Winter.</summary>
public sealed record TamperCandidate(string ItemId, Theme Theme, int Effort);

/// <summary>An unfilled slot the darkness may rewrite, with the bundle it sits in. <see cref="Flavor"/>
/// is the input the slot names (a Dried Fruit's fruit), null when it names none.</summary>
public sealed record TamperTarget(BundleRequirement Bundle, int IngredientIndex, string ItemId, string? Flavor = null)
{
    private readonly IReadOnlyList<int>? _slots;

    /// <summary>Every open slot of this exact item in <see cref="Bundle"/>, which a tamper rewrites
    /// together (Jeff, 2026-10-07: Construction's double Wood is one target over both slots).
    /// <see cref="IngredientIndex"/> is the first of them. Defaults to that one slot.</summary>
    public IReadOnlyList<int> IngredientIndices
    {
        get => _slots ?? new[] { IngredientIndex };
        init => _slots = value;
    }
}

/// <summary>Winter's unavoidable front: pick an unfilled slot (the ones whose item the player is
/// holding first, that is the sting) and a replacement of similar effort the bundle does not
/// already ask for.
///
/// Only an item ONE bundle asks for may be the target (Jeff, 2026-10-07: "only let it pick an
/// item that only appears in 1 bundle"; per bundle, so Construction's double Wood counts). The
/// Junimos then say the darkness tainted all of it, every open slot of it in that bundle is
/// rewritten, and no slot is left asking for the tainted thing. The item is the exact item: its id
/// and the flavour the slot names, so Dried Apples and Dried Cucumbers are two items.</summary>
public static class TamperRule
{
    /// <summary>The items a tamper may rewrite, one target per exact item: an open slot in an
    /// unfinished item-room bundle whose item no OTHER bundle asks for, filled or open. The bundle
    /// may ask for it in several slots (Construction's double Wood); the target then carries every
    /// open slot of it in that bundle, and the filled ones stay. A slot that names no flavour takes
    /// every flavour of its item, so it overlaps the flavoured ones: an item is a target only when
    /// every slot overlapping it, on the whole board, is the same exact item in the same bundle.
    /// <paramref name="flavorOf"/> gives a slot's flavour from the board's flavour map (bundle
    /// index, slot index); null means no slot names one.</summary>
    public static IReadOnlyList<TamperTarget> Targets(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, Func<int, int, string?>? flavorOf = null)
    {
        if (ledger is null) throw new ArgumentNullException(nameof(ledger));
        if (requirements is null) throw new ArgumentNullException(nameof(requirements));
        var result = new List<TamperTarget>();
        foreach (BundleRequirement req in requirements)
        {
            if (req.BundleIndex < 0) continue;
            if (!ReversionRule.IsItemRoomTheme(req.Theme)) continue;
            if (req.IsFullyComplete(ledger)) continue;
            var grouped = new HashSet<string>(StringComparer.Ordinal);
            foreach (BundleSlot slot in req.Slots)
            {
                if (ledger.IsFilled(req.BundleIndex, slot.IngredientIndex)) continue;
                string? flavor = FlavorOf(req, slot, flavorOf);
                string id = BundleParsing.NormalizeItemId(slot.ItemId ?? "");
                if (!grouped.Add(id + "|" + (flavor == null ? "" : BundleParsing.StripQualifier(flavor)))) continue;
                List<int>? open = OpenSlotsIfOnlyThisBundle(ledger, requirements, req, id, flavor, flavorOf);
                if (open == null || open.Count == 0) continue;
                result.Add(new TamperTarget(req, open[0], slot.ItemId!, flavor) { IngredientIndices = open });
            }
        }
        return result;
    }

    /// <summary>The open slots of this exact item in <paramref name="bundle"/>, or null when any
    /// slot elsewhere on the board overlaps it (another bundle, or another flavour spelling in this
    /// one).</summary>
    private static List<int>? OpenSlotsIfOnlyThisBundle(
        SlotLedger ledger, IReadOnlyList<BundleRequirement> requirements, BundleRequirement bundle,
        string id, string? flavor, Func<int, int, string?>? flavorOf)
    {
        var open = new List<int>();
        foreach (BundleRequirement req in requirements)
            foreach (BundleSlot slot in req.Slots)
            {
                if (!string.Equals(BundleParsing.NormalizeItemId(slot.ItemId ?? ""), id, StringComparison.Ordinal)) continue;
                string? other = FlavorOf(req, slot, flavorOf);
                if (!SameFlavorOrAny(flavor, other)) continue;
                if (req.BundleIndex != bundle.BundleIndex || !SameExactFlavor(flavor, other)) return null;
                if (!ledger.IsFilled(req.BundleIndex, slot.IngredientIndex)) open.Add(slot.IngredientIndex);
            }
        return open;
    }

    /// <summary>Both name no flavour, or both name the same one.</summary>
    private static bool SameExactFlavor(string? a, string? b)
        => (a == null && b == null)
           || (a != null && b != null && string.Equals(BundleParsing.StripQualifier(a), BundleParsing.StripQualifier(b), StringComparison.Ordinal));

    /// <summary>A target's item is asked in exactly this many slots.</summary>
    private const int SingleSlot = 1;

    /// <summary>How many slots on the board ask for this exact item, filled or open, in any
    /// bundle. A slot that names no flavour takes every flavour of its item, so it overlaps every
    /// flavoured slot of the same id, and they count as asking for the same thing.</summary>
    public static int SlotsAsking(
        IReadOnlyList<BundleRequirement> requirements, string itemId, string? flavor, Func<int, int, string?>? flavorOf)
    {
        if (requirements is null) throw new ArgumentNullException(nameof(requirements));
        string id = BundleParsing.NormalizeItemId(itemId ?? "");
        int n = 0;
        foreach (BundleRequirement req in requirements)
            foreach (BundleSlot slot in req.Slots)
            {
                if (!string.Equals(BundleParsing.NormalizeItemId(slot.ItemId ?? ""), id, StringComparison.Ordinal)) continue;
                if (SameFlavorOrAny(flavor, FlavorOf(req, slot, flavorOf))) n++;
            }
        return n;
    }

    /// <summary>How many different bundles ask for this exact item, filled or open, counting the
    /// overlap of an any-flavour slot as <see cref="SlotsAsking"/> does. One is the tamper's bar.</summary>
    public static int BundlesAsking(
        IReadOnlyList<BundleRequirement> requirements, string itemId, string? flavor, Func<int, int, string?>? flavorOf)
    {
        if (requirements is null) throw new ArgumentNullException(nameof(requirements));
        string id = BundleParsing.NormalizeItemId(itemId ?? "");
        int n = 0;
        foreach (BundleRequirement req in requirements)
            foreach (BundleSlot slot in req.Slots)
            {
                if (!string.Equals(BundleParsing.NormalizeItemId(slot.ItemId ?? ""), id, StringComparison.Ordinal)) continue;
                if (!SameFlavorOrAny(flavor, FlavorOf(req, slot, flavorOf))) continue;
                n++;
                break;
            }
        return n;
    }

    /// <summary>The flavour this slot names, read the way FlavoredSlotPatch applies it: only on an
    /// id that takes a flavour, so a stale map entry under a plain item names nothing.</summary>
    private static string? FlavorOf(BundleRequirement req, BundleSlot slot, Func<int, int, string?>? flavorOf)
    {
        if (flavorOf == null || req.BundleIndex < 0 || !FlavoredSlotRules.IsFlavored(slot.ItemId)) return null;
        string? f = flavorOf(req.BundleIndex, slot.IngredientIndex);
        return string.IsNullOrEmpty(f) ? null : f;
    }

    /// <summary>Both name the same flavour, or either names none (and so takes any).</summary>
    private static bool SameFlavorOrAny(string? a, string? b)
        => a == null || b == null
           || string.Equals(BundleParsing.StripQualifier(a), BundleParsing.StripQualifier(b), StringComparison.Ordinal);

    /// <summary>A rewritten slot forgets the flavour it named, or FlavoredSlotPatch would pin the
    /// old fruit onto a new flavoured item ("Smoked Apple"). True when an entry was removed.</summary>
    public static bool ClearFlavor(IDictionary<string, string>? flavors, int bundleIndex, int ingredientIndex)
        => flavors != null && flavors.Remove(FlavoredSlotPass.KeyFor(bundleIndex, ingredientIndex));

    /// <summary>Slots whose item the player holds come first; within a tier the order is random.</summary>
    public static TamperTarget? PickTarget(
        IReadOnlyList<TamperTarget> targets, Func<string, bool> playerHolds, Random rng)
    {
        if (targets is null) throw new ArgumentNullException(nameof(targets));
        if (playerHolds is null) throw new ArgumentNullException(nameof(playerHolds));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (targets.Count == 0) return null;
        var held = targets.Where(t => playerHolds(t.ItemId)).ToList();
        var pool = held.Count > 0 ? held : targets.ToList();
        return pool[rng.Next(pool.Count)];
    }

    /// <summary>The most the hall may ask for: legendary fish are always one (LegendaryFishRules,
    /// the rule QuantityAskPass already applies and the old tamper path skipped); otherwise the
    /// board's own ceiling, <see cref="AskBands.Ceiling"/> of the item's basis by Winter 28; 0 when
    /// the item has no basis, which <see cref="Stack"/> turns into an ask of one.</summary>
    public static int MaxCount(string itemId, double? basisByWinter)
    {
        if (LegendaryFishRules.IsLegendary(itemId)) return 1;
        if (basisByWinter is not double basis || basis <= 0) return 0;
        return (int)Math.Ceiling(basis * AskBands.Ceiling);
    }

    /// <summary>How many of the replacement the hall asks for (Jeff, 2026-09-09). Start from the
    /// item's normal max count, divide by the Winter weeks already passed (week 3 halves it, week
    /// 4 thirds it; weeks 1 and 2 keep it whole), then roll a 10%-wide slice of what is left by
    /// difficulty: Easy 0 to 10%, Normal 10 to 20%, Hard 20 to 30%, Extreme 30 to 40%. Never
    /// below one. An item with no known max count asks for one.</summary>
    public static int Stack(int maxCount, int weekOfWinter, DifficultyStep step, Random rng)
    {
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        if (maxCount <= 0) return 1;
        int weeksPassed = Math.Max(1, weekOfWinter - 1);
        double remainder = (double)maxCount / weeksPassed;
        double low = (int)step * SabotageTuning.TamperSliceWidth;
        double high = low + SabotageTuning.TamperSliceWidth;
        double fraction = low + rng.NextDouble() * (high - low);
        return Math.Max(1, (int)Math.Round(remainder * fraction, MidpointRounding.AwayFromZero));
    }

    /// <summary>The replacement: same room theme (the Bulletin Board's Mixed takes any), not
    /// already in the bundle, not asked by any slot on <paramref name="board"/>, never an item the
    /// darkness already tainted this loop, then the closest few in effort to the original, one at
    /// random. Null when nothing is left.
    ///
    /// The board bar (designer, 2026-10-07: "they should skip anything on the board, it's something
    /// new"): every slot of every bundle in the requirements counts, filled or open, and an id
    /// counts in every flavour, since the replacement is written unflavoured and would take any of
    /// them. Null <paramref name="board"/> skips the bar (tests of the other rules).
    ///
    /// The tainted bar covers every flavour of a tainted id (fix round 1, 2026-10-07): a
    /// replacement is written unflavoured, so an "Any Dried Fruit" ask would take the tainted
    /// Dried Apples, and a later "Bring us 3 Potatoes" would ask for the very thing glowing as
    /// tainted.</summary>
    public static TamperCandidate? PickReplacement(
        TamperTarget target, int originalEffort, IReadOnlyList<TamperCandidate> candidates, Random rng,
        IReadOnlyList<TamperRecord>? tainted = null, IReadOnlyList<BundleRequirement>? board = null)
    {
        if (target is null) throw new ArgumentNullException(nameof(target));
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (rng is null) throw new ArgumentNullException(nameof(rng));
        var inBundle = new HashSet<string>(target.Bundle.Ingredients, StringComparer.Ordinal);
        var taintedIds = new HashSet<string>(StringComparer.Ordinal);
        if (tainted != null)
            foreach (TamperRecord record in tainted)
                if (!string.IsNullOrEmpty(record.OldItemId))
                    taintedIds.Add(BundleParsing.NormalizeItemId(record.OldItemId));
        var onBoard = new HashSet<string>(StringComparer.Ordinal);
        if (board != null)
            foreach (BundleRequirement req in board)
                foreach (BundleSlot slot in req.Slots)
                    if (!string.IsNullOrEmpty(slot.ItemId))
                        onBoard.Add(BundleParsing.NormalizeItemId(slot.ItemId));
        List<TamperCandidate> eligible = candidates
            .Where(c => !inBundle.Contains(c.ItemId))
            .Where(c => !taintedIds.Contains(BundleParsing.NormalizeItemId(c.ItemId)))
            .Where(c => !onBoard.Contains(BundleParsing.NormalizeItemId(c.ItemId)))
            .Where(c => target.Bundle.Theme == Theme.Mixed || c.Theme == target.Bundle.Theme)
            .OrderBy(c => Math.Abs(c.Effort - originalEffort))
            .ThenBy(c => c.ItemId, StringComparer.Ordinal)
            .Take(SabotageTuning.TamperCandidatePool)
            .ToList();
        return eligible.Count == 0 ? null : eligible[rng.Next(eligible.Count)];
    }
    /// <summary>The week's goal lines follow a tamper (designer, 2026-10-08): a goal that points at
    /// a rewritten slot now names the new item, stack and quality, so the weekly quest and the hub
    /// show and count what the board asks for. Before this the tamper dropped the goal from the
    /// first list only and never refreshed the quest text, so the checklist kept asking for the
    /// tainted item ("Super Cucumbers" after the Ocean Fish slot asked for Red Snapper).
    ///
    /// The line's discount is cleared (<see cref="BonusSlot.OriginalStack"/> 0): the week-end revert
    /// would otherwise put the OLD item's full ask back onto the new item's slot. Stretch and route
    /// tags described the old item and go too. Returns how many goal lines changed.</summary>
    public static int FollowGoals(
        IEnumerable<IList<BonusSlot>?> goalLists, int bundleIndex, IReadOnlyCollection<int> slots,
        string newItemId, int stack, int quality)
    {
        if (goalLists is null) throw new ArgumentNullException(nameof(goalLists));
        if (slots is null) throw new ArgumentNullException(nameof(slots));
        int changed = 0;
        foreach (IList<BonusSlot>? list in goalLists)
        {
            if (list is null) continue;
            foreach (BonusSlot goal in list)
            {
                if (goal.BundleIndex != bundleIndex || !slots.Contains(goal.IngredientIndex)) continue;
                goal.ItemId = newItemId;
                goal.Stack = stack;
                goal.Quality = quality;
                goal.OriginalStack = 0;
                goal.Stretch = false;
                goal.RouteTag = null;
                changed++;
            }
        }
        return changed;
    }
}
