using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>One keep the shrine suggests for the next loop, and the asks behind it. A broad keep
/// counts every ask of its kind; an item-specific keep counts only the asks for its own items, and
/// <paramref name="Item"/> carries the game's display name when that is a single item (Garlic, Red
/// Cabbage). Null otherwise.</summary>
public sealed record KeepSuggestion(string UpgradeId, AskKind Kind, int Count, string? Item = null);

/// <summary>"Suggested" keeps (spec 2026-11-10 shrine planning, feature 1 and amendments C, D): count
/// the board's unfinished asks by <see cref="AskKind"/>, map each kind to keep chains through a
/// fixed table, take each chain's next buyable tier, skip pinned and owned chains, rank by the
/// count (ties by catalog order) and keep three, at most one per kind unless fewer than three
/// kinds have a candidate (Jeff, 2026-11-10). Item-specific keeps count only their own items'
/// asks. A lookup table, no guessing.</summary>
public static class KeepSuggestions
{
    public const int MaxSuggestions = 3;

    public const string FortuneRareFish = "fortune_rare_fish";

    /// <summary>Which asks a chain counts: every ask of its kind, or only the asks for its items.</summary>
    public enum Trigger { Always, RareFish, Garlic, RedCabbage, YearTwoSeeds }

    /// <summary>One table row: the kind it answers, the chain's tiers in buying order, and which
    /// asks it counts. A list of alternatives (the Start with rows) works the same way: the
    /// first unowned buyable entry is the one offered.</summary>
    public sealed record Chain(AskKind Kind, IReadOnlyList<string> Tiers, Trigger When = Trigger.Always);

    private static string[] Tiers(string prefix, int count)
        => Enumerable.Range(1, count).Select(t => prefix + t).ToArray();

    /// <summary>The kind to keep table (spec feature 1 table, amendment C), in the spec's order.</summary>
    public static readonly IReadOnlyList<Chain> Table = new[]
    {
        new Chain(AskKind.Fish, Tiers("quick_bite_", 5)),
        new Chain(AskKind.Fish, new[] { FortuneRareFish }, Trigger.RareFish),
        new Chain(AskKind.Fish, new[] { "keep_fishing_rod_0", "keep_fishing_rod_1", "keep_fishing_rod_2" }),
        new Chain(AskKind.Crops, Tiers("green_thumb_", 5)),
        new Chain(AskKind.Crops, new[] { YearTwoCrops.PierreUpgrade }, Trigger.YearTwoSeeds),
        new Chain(AskKind.Crops, new[] { YearTwoCrops.GarlicUpgrade }, Trigger.Garlic),
        new Chain(AskKind.Crops, new[] { YearTwoCrops.RedCabbageUpgrade }, Trigger.RedCabbage),
        new Chain(AskKind.Forage, Tiers("foragers_eye_", 5)),
        new Chain(AskKind.Minerals, Tiers("coal_vein_", 5)),
        new Chain(AskKind.Minerals, Tiers("keep_pickaxe_", 4)),
        new Chain(AskKind.Minerals, new[] { "keep_shortcuts" }),
        new Chain(AskKind.AnimalProducts, new[] { "keep_coop", "keep_big_coop", "keep_deluxe_coop" }),
        new Chain(AskKind.AnimalProducts, new[] { "keep_barn", "keep_big_barn", "keep_deluxe_barn" }),
        new Chain(AskKind.AnimalProducts, new[]
        {
            "start_chicken", "start_cow", "start_duck", "start_goat", "start_sheep", "start_pig",
            "start_rabbit", "start_void_chicken", "start_dinosaur", "start_ostrich",
        }),
        new Chain(AskKind.CookedDishes, Tiers("cookbook_", UpgradeCatalog.BookMaxTier)),
        new Chain(AskKind.CookedDishes, new[] { "keep_kitchen" }),
        new Chain(AskKind.ArtisanGoods, Tiers("craftbook_", UpgradeCatalog.BookMaxTier)),
        new Chain(AskKind.ArtisanGoods, new[] { "keep_garden_pot" }),
        new Chain(AskKind.Gold, Tiers("starter_gold_", 5)),
        new Chain(AskKind.Gold, Tiers("shop_discount_", 5)),
    };

    private static readonly IReadOnlySet<string> YearTwoSeedCrops =
        new HashSet<string>(StringComparer.Ordinal) { YearTwoCrops.Garlic, YearTwoCrops.RedCabbage, YearTwoCrops.Artichoke };

    private static readonly IReadOnlyDictionary<string, int> CatalogOrder =
        UpgradeCatalog.All.Select((d, i) => (d.Id, i)).ToDictionary(p => p.Id, p => p.i);

    /// <summary>The asks the suggestions count: on a held board every slot of every bundle (it
    /// comes back empty), otherwise the undonated slots of the unfinished bundles.</summary>
    public static IEnumerable<BundleIngredient> CountedAsks(IReadOnlyList<BundleProgress> board, bool heldBoard)
    {
        foreach (BundleProgress bundle in board)
        {
            if (!heldBoard && bundle.Done) continue;
            for (int i = 0; i < bundle.Ingredients.Count; i++)
                if (heldBoard || !bundle.Filled[i])
                    yield return bundle.Ingredients[i];
        }
    }

    /// <summary>Counted asks by kind (<see cref="AskKind.Other"/> left out).</summary>
    public static IReadOnlyDictionary<AskKind, int> CountByKind(
        IReadOnlyList<BundleProgress> board, bool heldBoard, Func<string, AskKind> kindOf)
    {
        var counts = new Dictionary<AskKind, int>();
        foreach (BundleIngredient ask in CountedAsks(board, heldBoard))
        {
            AskKind kind = kindOf(ask.ItemRef);
            if (kind == AskKind.Other) continue;
            counts[kind] = counts.TryGetValue(kind, out int n) ? n + 1 : 1;
        }
        return counts;
    }

    /// <summary>Up to <see cref="MaxSuggestions"/> keeps for the next loop, best first.</summary>
    /// <param name="kindOf">The ask kind of an ingredient id (<see cref="AskKinds.Classify"/>).</param>
    /// <param name="isRareFish">Whether an asked item is a rare fish (Fortune: Rare Fish's count).</param>
    /// <param name="reachMet">The shop's live reach check, so a suggestion is a row the shop shows.</param>
    /// <param name="itemName">The game's display name of a qualified item id (Garlic, Red Cabbage).</param>
    public static IReadOnlyList<KeepSuggestion> For(
        IReadOnlyList<BundleProgress> board, bool heldBoard, MetaState meta,
        Func<string, AskKind> kindOf, Func<string, bool> isRareFish, Func<string?, bool> reachMet,
        Func<string, string> itemName)
    {
        if (board == null) throw new ArgumentNullException(nameof(board));
        if (meta == null) throw new ArgumentNullException(nameof(meta));
        IReadOnlyDictionary<AskKind, int> counts = CountByKind(board, heldBoard, kindOf);
        List<string> asked = CountedAsks(board, heldBoard).Select(a => BundleParsing.NormalizeItemId(a.ItemRef)).ToList();

        var picks = new List<(KeepSuggestion Suggestion, int Order)>();
        foreach (Chain chain in Table)
        {
            int count = chain.When == Trigger.Always
                ? counts.TryGetValue(chain.Kind, out int n) ? n : 0
                : asked.Count(id => Counts(chain.When, id, kindOf, isRareFish));
            if (count <= 0) continue;
            UpgradeDefinition? next = NextBuyable(chain, meta, reachMet);
            if (next == null || PinnedKeeps.IsPinned(meta, next.Id)) continue;
            string? item = SingleItem(chain.When) is string id ? itemName(id) : null;
            picks.Add((new KeepSuggestion(next.Id, chain.Kind, count, item), CatalogOrder[next.Id]));
        }

        var roots = new HashSet<string>(StringComparer.Ordinal);
        var ranked = picks
            .OrderByDescending(p => p.Suggestion.Count)
            .ThenBy(p => p.Order)
            .Where(p => roots.Add(PinnedKeeps.ChainRoot(p.Suggestion.UpgradeId)))
            .ToList();
        return Varied(ranked.Select(p => p.Suggestion).ToList());
    }

    /// <summary>From <paramref name="ranked"/> (best first): the best of each kind, then, while
    /// places are left, the next best of kinds already used. Returned in ranked order.</summary>
    private static IReadOnlyList<KeepSuggestion> Varied(IReadOnlyList<KeepSuggestion> ranked)
    {
        var kinds = new HashSet<AskKind>();
        var chosen = new HashSet<int>();
        for (int i = 0; i < ranked.Count && chosen.Count < MaxSuggestions; i++)
            if (kinds.Add(ranked[i].Kind)) chosen.Add(i);
        for (int i = 0; i < ranked.Count && chosen.Count < MaxSuggestions; i++)
            chosen.Add(i);
        return ranked.Where((_, i) => chosen.Contains(i)).ToList();
    }

    /// <summary>The first tier of the chain that is not owned and that the shop would sell now.</summary>
    public static UpgradeDefinition? NextBuyable(Chain chain, MetaState meta, Func<string?, bool> reachMet)
    {
        foreach (string id in chain.Tiers)
        {
            UpgradeDefinition? def = UpgradeCatalog.TryGet(id);
            if (def == null || meta.HasUpgrade(id)) continue;
            if (KeepShopFilter.IsBuyable(def, meta, reachMet)) return def;
        }
        return null;
    }

    /// <summary>Whether an asked item counts for an item-specific chain.</summary>
    private static bool Counts(Trigger when, string id, Func<string, AskKind> kindOf, Func<string, bool> isRareFish)
        => when switch
        {
            Trigger.RareFish => kindOf(id) == AskKind.Fish && isRareFish(id),
            Trigger.Garlic => id == YearTwoCrops.Garlic,
            Trigger.RedCabbage => id == YearTwoCrops.RedCabbage,
            Trigger.YearTwoSeeds => YearTwoSeedCrops.Contains(id),
            _ => false,
        };

    /// <summary>The one item a chain names in its reason, or null.</summary>
    private static string? SingleItem(Trigger when) => when switch
    {
        Trigger.Garlic => YearTwoCrops.Garlic,
        Trigger.RedCabbage => YearTwoCrops.RedCabbage,
        _ => null,
    };

    /// <summary><paramref name="rows"/> (pinned rows already first) with the suggested ones moved
    /// right below the pinned rows, in suggestion order. Rows that are not suggested keep their order.</summary>
    public static List<T> AfterPinned<T>(IReadOnlyList<T> rows, Func<T, bool> isPinned, Func<T, bool> isSuggested,
        Func<T, int> suggestionRank)
    {
        int pinnedCount = 0;
        while (pinnedCount < rows.Count && isPinned(rows[pinnedCount])) pinnedCount++;
        var result = rows.Take(pinnedCount).ToList();
        var rest = rows.Skip(pinnedCount).ToList();
        result.AddRange(rest.Where(isSuggested).OrderBy(suggestionRank));
        result.AddRange(rest.Where(r => !isSuggested(r)));
        return result;
    }
}
