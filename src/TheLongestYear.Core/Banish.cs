using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>What a banish keeps off the next board: one bundle (by its base name) or one item
/// (by its qualified id).</summary>
public enum BanishKind
{
    Bundle,
    Item,
}

/// <summary>One banish bought at the shrine (Nexus request, RosemaryBagels 2026-10-27). Persisted in
/// <see cref="MetaState.PendingBanishes"/> until the reset that builds the next board, then in
/// <see cref="MetaState.BoardBanishes"/> for as long as that board is live.</summary>
public sealed class BanishPick
{
    public BanishKind Kind { get; set; }

    /// <summary>The bundle's base name (no " II" suffix) or the item's qualified id.</summary>
    public string Key { get; set; } = "";

    /// <summary>The name the player picked from, kept for the refund notice (the item or bundle
    /// may not exist on the next board to look the name up from).</summary>
    public string Label { get; set; } = "";

    /// <summary>JP actually charged, so a refund pays back exactly this.</summary>
    public long Paid { get; set; }

    public bool SameTarget(BanishPick other)
        => other != null && other.Kind == Kind && string.Equals(other.Key, Key, StringComparison.Ordinal);
}

/// <summary>The banish price ladder. Each banish that has been honoured (kept off a real board)
/// raises the next one's price; a refunded banish does not count. Same shape as the Gifts of the
/// Junimos ladder, at a smaller scale: one bundle or item matters less than a room's reward, and
/// a banish lasts one loop rather than forever.</summary>
public static class BanishLadder
{
    /// <summary>Base prices by honoured banishes so far; the last step repeats.</summary>
    public static readonly IReadOnlyList<long> Costs = new long[] { 100, 200, 350, 550, 800 };

    /// <summary>The most banishes one board can take.</summary>
    public const int MaxPerLoop = 2;

    /// <summary>The next banish's price at the run's shrine price factor (rounded away from zero,
    /// like <see cref="UpgradePricing"/>).</summary>
    public static long CostFor(int honouredSoFar, double factor = 1.0)
    {
        int index = Math.Clamp(honouredSoFar, 0, Costs.Count - 1);
        long cost = Costs[index];
        if (factor == 1.0) return cost;
        long scaled = (long)Math.Round(cost * factor, MidpointRounding.AwayFromZero);
        return scaled < 0 ? 0 : scaled;
    }

    /// <summary>The ladder step counts banishes already honoured plus the ones bought for the next
    /// board, so buying two in one visit charges the first and second steps.</summary>
    public static long NextCost(MetaState state, double factor = 1.0)
        => CostFor(state.BanishesHonoured + state.PendingBanishes.Count, factor);
}

/// <summary>Buying, cancelling and resolving banishes. Pure: mutates <see cref="MetaState"/> only.</summary>
public static class BanishRules
{
    public enum BuyResult
    {
        Bought,
        NotEnoughJp,
        LimitReached,
        AlreadyBanished,
        Invalid,
    }

    /// <summary>Whether the shrine offers a banish at all: only a TLY Custom board is built by the
    /// mod (Normal and Remixed are the game's own boards), and a board the player chose to hold
    /// tonight is not rebuilt, so there is nothing to keep anything off.</summary>
    public static bool IsOfferable(MetaState state)
    {
        string source = BundleSourceNames.ForSave(state.ChosenBundleSource, state.BundleSource, state.VanillaBundleType);
        if (source != BundleSourceNames.Engine) return false;
        return !IsHoldingTonight(state);
    }

    /// <summary>True after the Fail-night "keep current bundles" answer, before the reset.</summary>
    public static bool IsHoldingTonight(MetaState state)
        => state.HoldChoiceMadeForReset && state.ConsecutiveHolds > 0;

    public static BuyResult Buy(MetaState state, BanishKind kind, string key, string label, double factor = 1.0)
    {
        if (string.IsNullOrWhiteSpace(key)) return BuyResult.Invalid;
        if (state.PendingBanishes.Count >= BanishLadder.MaxPerLoop) return BuyResult.LimitReached;
        var pick = new BanishPick { Kind = kind, Key = key, Label = string.IsNullOrEmpty(label) ? key : label };
        if (state.PendingBanishes.Any(p => p.SameTarget(pick))) return BuyResult.AlreadyBanished;
        long cost = BanishLadder.NextCost(state, factor);
        if (state.JunimoPoints < cost) return BuyResult.NotEnoughJp;
        state.JunimoPoints -= cost;
        pick.Paid = cost;
        state.PendingBanishes.Add(pick);
        return BuyResult.Bought;
    }

    /// <summary>Take back a banish bought for the next board (the loop has not reset yet), refunding
    /// what was paid. The banishes still pending are then re-priced to the ladder steps they now hold,
    /// and any overpayment comes back too, so the ones held never cost more than the ladder for that
    /// many (buy 100 + 200, take back the first, rebuy: 300 in all). A re-price never charges more.
    /// Returns the total refund, 0 when there is no such banish.</summary>
    public static long Cancel(MetaState state, int index, double factor = 1.0)
    {
        if (index < 0 || index >= state.PendingBanishes.Count) return 0;
        BanishPick pick = state.PendingBanishes[index];
        state.PendingBanishes.RemoveAt(index);
        long refund = pick.Paid;
        for (int i = 0; i < state.PendingBanishes.Count; i++)
        {
            BanishPick held = state.PendingBanishes[i];
            long step = BanishLadder.CostFor(state.BanishesHonoured + i, factor);
            if (step >= held.Paid) continue;
            refund += held.Paid - step;
            held.Paid = step;
        }
        state.JunimoPoints += refund;
        return refund;
    }

    /// <summary>The reset's banish step, whichever board it builds.
    /// <list type="bullet">
    /// <item>A held board (<paramref name="holdingBoard"/>) is rebuilt from the banishes it was built
    /// under; everything pending is refunded.</item>
    /// <item>Nothing pending: the board is built with no banish, and last loop's banishes expire.</item>
    /// <item>Otherwise <see cref="BanishResolver.Resolve{T}"/> decides, and the result is applied.</item>
    /// </list>
    /// Returns the build to write, and the banishes refunded at this reset.</summary>
    public static (T Build, IReadOnlyList<BanishPick> Refunded) ApplyAtReset<T>(
        MetaState state, bool holdingBoard,
        Func<IReadOnlyList<BanishPick>, (BanishBoardOutcome Outcome, T Build)> generate)
    {
        if (holdingBoard)
        {
            T held = generate(state.BoardBanishes.ToList()).Build;
            return (held, RefundAllPending(state));
        }
        if (state.PendingBanishes.Count == 0)
        {
            T plain = generate(Array.Empty<BanishPick>()).Build;
            ApplyResolution(state, new BanishResolution(Array.Empty<BanishPick>(), Array.Empty<BanishPick>()));
            return (plain, Array.Empty<BanishPick>());
        }
        (BanishResolution resolution, T build) = BanishResolver.Resolve(state.PendingBanishes.ToList(), generate);
        ApplyResolution(state, resolution);
        return (build, resolution.Refused);
    }

    /// <summary>The reset's half when this reset does NOT build a new board (a held board, or a
    /// Normal / Remixed board): every pending banish is refunded and the live board's own banishes
    /// stay with it (a held Custom board is re-derived from the same inputs).</summary>
    public static IReadOnlyList<BanishPick> RefundAllPending(MetaState state)
    {
        var refunded = state.PendingBanishes.ToList();
        foreach (BanishPick pick in refunded)
            state.JunimoPoints += pick.Paid;
        state.PendingBanishes.Clear();
        QueueNotice(state, refunded);
        return refunded;
    }

    /// <summary>The reset's half when it builds a new board: the honoured banishes become the new
    /// board's banishes (and climb the ladder), the refused ones are refunded and queued for the
    /// morning notice. Last loop's board banishes expire here: they are replaced, not added to.</summary>
    /// <remarks>The prices paid are first handed to the honoured banishes cheapest first, and the
    /// rest go with the refused ones. So when an EARLIER banish is refused and a later one honoured,
    /// the honoured one ends up on the first ladder step and the refund is the higher price: the
    /// player pays the ladder for the number actually honoured (100, not 200, for one), the same rule
    /// as the take-back re-price in <see cref="Cancel"/>. The total JP moved is unchanged.</remarks>
    public static void ApplyResolution(MetaState state, BanishResolution resolution)
    {
        List<long> prices = resolution.Honoured.Concat(resolution.Refused).Select(p => p.Paid).OrderBy(p => p).ToList();
        int next = 0;
        foreach (BanishPick pick in resolution.Honoured) pick.Paid = prices[next++];
        foreach (BanishPick pick in resolution.Refused) pick.Paid = prices[next++];
        state.BoardBanishes = resolution.Honoured.ToList();
        state.BanishesHonoured += resolution.Honoured.Count;
        foreach (BanishPick pick in resolution.Refused)
            state.JunimoPoints += pick.Paid;
        state.PendingBanishes.Clear();
        QueueNotice(state, resolution.Refused);
    }

    private static void QueueNotice(MetaState state, IReadOnlyList<BanishPick> refunded)
    {
        if (refunded.Count == 0) return;
        state.BanishRefundNotice ??= new List<BanishPick>();
        state.BanishRefundNotice.AddRange(refunded);
    }

    /// <summary>The refund notice waiting to be shown, left in place. Null when there is none.
    /// The glue clears it with <see cref="TakeNotice"/> only once the message is on screen, so a
    /// quit before then shows it again on the next load.</summary>
    public static IReadOnlyList<BanishPick>? PeekNotice(MetaState state)
        => state.BanishRefundNotice is { Count: > 0 } notice ? notice : null;

    /// <summary>The refund notice to show, cleared so it shows once. Null when there is none.</summary>
    public static IReadOnlyList<BanishPick>? TakeNotice(MetaState state)
    {
        List<BanishPick>? notice = state.BanishRefundNotice;
        state.BanishRefundNotice = null;
        return notice is { Count: > 0 } ? notice : null;
    }

    /// <summary>The item ids a set of banishes keeps out of the item pools, joined onto the save's
    /// own exclusions (year-two crops). Returns <paramref name="baseExcluded"/> unchanged when no
    /// item is banished, so a board with no banishes is built from exactly the same inputs as before.</summary>
    public static IReadOnlySet<string>? ExcludedIds(IReadOnlySet<string>? baseExcluded, IReadOnlyList<BanishPick>? banishes)
    {
        if (banishes == null || !banishes.Any(b => b.Kind == BanishKind.Item)) return baseExcluded;
        var set = baseExcluded != null ? new HashSet<string>(baseExcluded, StringComparer.Ordinal) : new HashSet<string>(StringComparer.Ordinal);
        foreach (BanishPick pick in banishes.Where(b => b.Kind == BanishKind.Item))
            set.Add(pick.Key);
        return set;
    }

    /// <summary>The bundle names a set of banishes keeps off the board.</summary>
    public static IReadOnlySet<string> BundleNames(IReadOnlyList<BanishPick>? banishes)
        => banishes == null
            ? new HashSet<string>(StringComparer.Ordinal)
            : new HashSet<string>(banishes.Where(b => b.Kind == BanishKind.Bundle).Select(b => b.Key), StringComparer.Ordinal);

    /// <summary>Drops every banished bundle from each room position's candidates. A position whose
    /// only candidates are banished keeps them all (a position is never left empty); the resolver
    /// then finds the banished bundle on the board and refuses the banish. Positions with nothing
    /// banished come back as the same instance, so the room's roll is unchanged without a banish.</summary>
    public static IReadOnlyList<IReadOnlyList<BundleSpec>> WithoutBanishedBundles(
        IReadOnlyList<IReadOnlyList<BundleSpec>> positions, IReadOnlySet<string> bannedNames)
    {
        if (bannedNames.Count == 0) return positions;
        return positions
            .Select(candidates =>
            {
                List<BundleSpec> kept = candidates.Where(c => !bannedNames.Contains(c.Name)).ToList();
                return kept.Count > 0 && kept.Count < candidates.Count ? (IReadOnlyList<BundleSpec>)kept : candidates;
            })
            .ToList();
    }
}

/// <summary>The outcome of building one candidate board, as far as the banish check needs it.</summary>
public sealed class BanishBoardOutcome
{
    public BanishBoardOutcome(IReadOnlyList<BundleSpec> bundles, int keptVanillaFallbacks)
    {
        Bundles = bundles;
        KeptVanillaFallbacks = keptVanillaFallbacks;
    }

    public IReadOnlyList<BundleSpec> Bundles { get; }

    /// <summary>Bundles whose pool could not fill every slot and kept their template's own slots.</summary>
    public int KeptVanillaFallbacks { get; }
}

public sealed class BanishResolution
{
    public BanishResolution(IReadOnlyList<BanishPick> honoured, IReadOnlyList<BanishPick> refused)
    {
        Honoured = honoured;
        Refused = refused;
    }

    public IReadOnlyList<BanishPick> Honoured { get; }
    public IReadOnlyList<BanishPick> Refused { get; }
}

/// <summary>Decides which banishes the next board can honour. A banish is honoured only when the
/// board built without the bundle or item still fills every slot: it does not ask for the banished
/// thing anywhere outside the pass-through rooms, it has as many bundles as the board built without
/// any banish, and no more of its bundles fell back to their template slots. Banishes are tried in
/// the order bought, each on top of the ones already honoured.</summary>
public static class BanishResolver
{
    /// <summary>Rooms the engine copies from the game untouched (the Vault, the Abandoned Joja
    /// Mart): their asks are not the loop's board, so they are neither offered nor checked.</summary>
    public static readonly IReadOnlySet<string> PassThroughRooms =
        new HashSet<string>(new[] { "Vault", "Abandoned Joja Mart" }, StringComparer.Ordinal);

    /// <param name="generate">Builds the board under a set of banishes and returns its outcome
    /// plus whatever the caller wants to keep from that build (the engine and its set).</param>
    /// <returns>The resolution and the build that matches it (the last honoured build, or the
    /// banish-free build when nothing was honoured).</returns>
    public static (BanishResolution Resolution, T Build) Resolve<T>(
        IReadOnlyList<BanishPick> pending,
        Func<IReadOnlyList<BanishPick>, (BanishBoardOutcome Outcome, T Build)> generate)
    {
        (BanishBoardOutcome baseline, T baselineBuild) = generate(Array.Empty<BanishPick>());
        var honoured = new List<BanishPick>();
        var refused = new List<BanishPick>();
        T current = baselineBuild;
        foreach (BanishPick pick in pending)
        {
            var attempt = new List<BanishPick>(honoured) { pick };
            (BanishBoardOutcome outcome, T build) = generate(attempt);
            if (Honours(outcome, attempt, baseline))
            {
                honoured.Add(pick);
                current = build;
            }
            else
            {
                refused.Add(pick);
            }
        }
        return (new BanishResolution(honoured, refused), current);
    }

    public static bool Honours(BanishBoardOutcome outcome, IReadOnlyList<BanishPick> banishes, BanishBoardOutcome baseline)
    {
        if (outcome.Bundles.Count < baseline.Bundles.Count) return false;
        if (outcome.KeptVanillaFallbacks > baseline.KeptVanillaFallbacks) return false;
        foreach (BanishPick pick in banishes)
            if (Appears(outcome.Bundles, pick)) return false;
        return true;
    }

    /// <summary>Whether a banished bundle or item is still on the board (pass-through rooms aside).</summary>
    public static bool Appears(IReadOnlyList<BundleSpec> bundles, BanishPick pick)
    {
        foreach (BundleSpec bundle in bundles)
        {
            if (PassThroughRooms.Contains(bundle.Room)) continue;
            if (pick.Kind == BanishKind.Bundle)
            {
                if (string.Equals(BanishCandidates.BaseName(bundle.Name), pick.Key, StringComparison.Ordinal))
                    return true;
                continue;
            }
            foreach (BundleSlotSpec slot in bundle.Slots)
                if (string.Equals(SlotItemId(slot.ItemId), pick.Key, StringComparison.Ordinal))
                    return true;
        }
        return false;
    }

    /// <summary>A slot's qualified item id, or null for a money or category slot.</summary>
    internal static string? SlotItemId(string itemRef)
    {
        if (string.IsNullOrEmpty(itemRef) || itemRef == "-1" || BundleParsing.IsCategoryRef(itemRef)) return null;
        return BundleParsing.NormalizeItemId(itemRef);
    }
}
