using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class KeepSuggestionsTests
{
    private const string Sunfish = "(O)145";
    private const string Sturgeon = "(O)698";
    private const string Parsnip = "(O)24";
    private const string Salmonberry = "(O)296";
    private const string CopperBar = "(O)334";
    private const string FriedEgg = "(O)194";
    private const string Wood = "(O)388";

    private static readonly Dictionary<string, AskKind> Kinds = new()
    {
        [Sunfish] = AskKind.Fish,
        [Sturgeon] = AskKind.Fish,
        [Parsnip] = AskKind.Crops,
        [YearTwoCrops.Garlic] = AskKind.Crops,
        [Salmonberry] = AskKind.Forage,
        [CopperBar] = AskKind.Minerals,
        [FriedEgg] = AskKind.CookedDishes,
        [Wood] = AskKind.Other,
        ["-4"] = AskKind.Fish,
        [AskKinds.MoneyRef] = AskKind.Gold,
    };

    private static AskKind KindOf(string itemRef)
        => Kinds.TryGetValue(itemRef, out AskKind k) ? k
            : Kinds.TryGetValue(BundleParsing.NormalizeItemId(itemRef), out k) ? k : AskKind.Other;

    private static bool RareFish(string id) => id == Sturgeon;

    private static BundleProgress Bundle(int index, params (string Item, bool Filled)[] slots)
        => Bundle(index, slots.Length, slots);

    private static BundleProgress Bundle(int index, int required, params (string Item, bool Filled)[] slots)
        => new(index, "Pantry", slots.Select(s => new BundleIngredient(s.Item, 1, 0)).ToList(),
            slots.Select(s => s.Filled).ToList(), required);

    private static IReadOnlyList<KeepSuggestion> Suggest(IReadOnlyList<BundleProgress> board, MetaState? meta = null, bool held = false)
        => KeepSuggestions.For(board, held, meta ?? new MetaState(), KindOf, RareFish, _ => true);

    [Fact]
    public void Counts_the_undonated_slots_of_unfinished_bundles_by_kind()
    {
        var board = new[]
        {
            Bundle(0, 2, (Sunfish, false), (Sunfish, false), (Parsnip, true)),   // unfinished: 2 fish
            Bundle(1, (Parsnip, true), (Parsnip, true)),                          // finished: nothing
            Bundle(2, (Wood, false), (FriedEgg, false)),                          // wood is Other
        };
        IReadOnlyDictionary<AskKind, int> counts = KeepSuggestions.CountByKind(board, heldBoard: false, KindOf);
        Assert.Equal(new Dictionary<AskKind, int> { [AskKind.Fish] = 2, [AskKind.CookedDishes] = 1 }, counts);
    }

    [Fact]
    public void A_held_board_counts_every_slot_with_empty_progress()
    {
        var board = new[] { Bundle(0, (Parsnip, true), (Parsnip, true)) };
        Assert.Equal(2, KeepSuggestions.CountByKind(board, heldBoard: true, KindOf)[AskKind.Crops]);
    }

    [Fact]
    public void Ranks_by_count_and_breaks_ties_by_catalog_order()
    {
        var board = new[]
        {
            Bundle(0, (Sunfish, false), (Sunfish, false), (Sunfish, false)),
            Bundle(1, (Salmonberry, false)),
            Bundle(2, (CopperBar, false)),
        };
        // Fish (3): Quick Bite, then the Bamboo Pole; Minerals (1) ties Forage (1): Coal Vein's
        // catalog row comes before Forager's Eye, but only three fit.
        Assert.Equal(new[] { "quick_bite_1", "keep_fishing_rod_0", "coal_vein_1" },
            Suggest(board).Select(s => s.UpgradeId));
    }

    [Fact]
    public void A_pinned_chain_is_skipped_and_an_owned_tier_moves_to_the_next()
    {
        var board = new[] { Bundle(0, (Sunfish, false), (Sunfish, false)) };
        var meta = new MetaState();
        meta.OwnedUpgrades.Add("quick_bite_1");
        meta.PinnedUpgrades.Add("keep_fishing_rod_0");

        IReadOnlyList<KeepSuggestion> picks = Suggest(board, meta);

        Assert.Equal(new[] { "quick_bite_2" }, picks.Select(s => s.UpgradeId));
        Assert.Equal(new KeepSuggestion("quick_bite_2", AskKind.Fish, 2), picks[0]);
    }

    [Fact]
    public void Item_specific_keeps_need_those_items_on_the_board()
    {
        var generic = new[] { Bundle(0, (Sunfish, false), (Parsnip, false)) };
        var specific = new[] { Bundle(0, (Sturgeon, false), (YearTwoCrops.Garlic, false)) };

        Assert.DoesNotContain(Suggest(generic), s => s.UpgradeId is "fortune_rare_fish" or "cult_garlic" or "pierre_year2_seeds");
        var meta = new MetaState();
        // Own the generic fish and crop chains so the specific ones surface.
        meta.OwnedUpgrades.AddRange(new[] { "quick_bite_1", "quick_bite_2", "quick_bite_3", "quick_bite_4", "quick_bite_5",
            "keep_fishing_rod_0", "keep_fishing_rod_1", "keep_fishing_rod_2",
            "green_thumb_1", "green_thumb_2", "green_thumb_3", "green_thumb_4", "green_thumb_5" });
        // One ask each, so catalog order decides.
        Assert.Equal(new[] { "cult_garlic", "pierre_year2_seeds", "fortune_rare_fish" },
            Suggest(specific, meta).Select(s => s.UpgradeId));
    }

    [Fact]
    public void An_unpaid_vault_bundle_suggests_the_gold_keeps()
        => Assert.Equal(new[] { "starter_gold_1", "shop_discount_1" },
            Suggest(new[] { Bundle(23, (AskKinds.MoneyRef, false)) }).Select(s => s.UpgradeId));

    [Fact]
    public void Suggested_rows_follow_the_pinned_rows_in_suggestion_order()
    {
        var rows = new[] { "pinned", "a", "s2", "b", "s1" };
        var rank = new Dictionary<string, int> { ["s1"] = 0, ["s2"] = 1 };
        List<string> sorted = KeepSuggestions.AfterPinned(rows, r => r == "pinned", rank.ContainsKey,
            r => rank.TryGetValue(r, out int i) ? i : -1);
        Assert.Equal(new[] { "pinned", "s1", "s2", "a", "b" }, sorted);
    }
}
