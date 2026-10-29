using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BanishTests
{
    private static BundleSpec Bundle(string room, int index, string name, params string[] items)
        => new(room, index, name, name, "O 1 1", 0, items.Length,
            items.Select(id => new BundleSlotSpec(id, 1, 0)).ToList());

    private static BanishPick Item(string id, long paid = 100) => new() { Kind = BanishKind.Item, Key = id, Label = id, Paid = paid };
    private static BanishPick BundleBanish(string name, long paid = 100) => new() { Kind = BanishKind.Bundle, Key = name, Label = name, Paid = paid };

    // ---------- ladder ----------

    [Theory]
    [InlineData(0, 100)]
    [InlineData(1, 200)]
    [InlineData(2, 350)]
    [InlineData(3, 550)]
    [InlineData(4, 800)]
    [InlineData(9, 800)]
    [InlineData(-1, 100)]
    public void Ladder_climbs_then_holds_at_the_top(int honoured, long expected)
        => Assert.Equal(expected, BanishLadder.CostFor(honoured));

    [Fact]
    public void Ladder_applies_the_shrine_price_factor()
    {
        Assert.Equal(150, BanishLadder.CostFor(0, 1.5));
        Assert.Equal(175, BanishLadder.CostFor(2, 0.5));
    }

    [Fact]
    public void Second_banish_in_one_visit_costs_the_next_step()
    {
        var s = new MetaState { JunimoPoints = 1000, BanishesHonoured = 1 };
        Assert.Equal(200, BanishLadder.NextCost(s));
        BanishRules.Buy(s, BanishKind.Item, "(O)24", "Parsnip");
        Assert.Equal(350, BanishLadder.NextCost(s));
        Assert.Equal(800, s.JunimoPoints);
    }

    // ---------- buying ----------

    [Fact]
    public void Buy_charges_and_records_what_was_paid()
    {
        var s = new MetaState { JunimoPoints = 150 };
        Assert.Equal(BanishRules.BuyResult.Bought, BanishRules.Buy(s, BanishKind.Bundle, "Spring Foraging", "Spring Foraging"));
        Assert.Equal(50, s.JunimoPoints);
        Assert.Single(s.PendingBanishes);
        Assert.Equal(100, s.PendingBanishes[0].Paid);
    }

    [Fact]
    public void At_most_two_banishes_per_loop()
    {
        var s = new MetaState { JunimoPoints = 10_000 };
        BanishRules.Buy(s, BanishKind.Item, "(O)1", "a");
        BanishRules.Buy(s, BanishKind.Item, "(O)2", "b");
        Assert.Equal(BanishRules.BuyResult.LimitReached, BanishRules.Buy(s, BanishKind.Item, "(O)3", "c"));
        Assert.Equal(2, s.PendingBanishes.Count);
    }

    [Fact]
    public void Same_target_cannot_be_banished_twice()
    {
        var s = new MetaState { JunimoPoints = 10_000 };
        BanishRules.Buy(s, BanishKind.Item, "(O)1", "a");
        Assert.Equal(BanishRules.BuyResult.AlreadyBanished, BanishRules.Buy(s, BanishKind.Item, "(O)1", "a"));
    }

    [Fact]
    public void Not_enough_jp_changes_nothing()
    {
        var s = new MetaState { JunimoPoints = 99 };
        Assert.Equal(BanishRules.BuyResult.NotEnoughJp, BanishRules.Buy(s, BanishKind.Item, "(O)1", "a"));
        Assert.Equal(99, s.JunimoPoints);
        Assert.Empty(s.PendingBanishes);
    }

    [Fact]
    public void Cancel_refunds_what_was_paid()
    {
        var s = new MetaState { JunimoPoints = 300 };
        BanishRules.Buy(s, BanishKind.Item, "(O)1", "a");
        BanishRules.Buy(s, BanishKind.Item, "(O)2", "b");
        Assert.Equal(0, s.JunimoPoints);
        Assert.Equal(200, BanishRules.Cancel(s, 1));
        Assert.Equal(200, s.JunimoPoints);
        Assert.Equal(0, BanishRules.Cancel(s, 5));
        Assert.Single(s.PendingBanishes);
    }

    [Fact]
    public void Offered_only_on_a_custom_board_that_is_not_held_tonight()
    {
        Assert.True(BanishRules.IsOfferable(new MetaState { BundleSource = BundleSourceNames.Engine }));
        Assert.False(BanishRules.IsOfferable(new MetaState { ChosenBundleSource = BundleSourceNames.Normal }));
        Assert.False(BanishRules.IsOfferable(new MetaState { ChosenBundleSource = BundleSourceNames.Remixed }));
        Assert.False(BanishRules.IsOfferable(new MetaState { HoldChoiceMadeForReset = true, ConsecutiveHolds = 1 }));
        // A reshuffle answer still builds a new board.
        Assert.True(BanishRules.IsOfferable(new MetaState { HoldChoiceMadeForReset = true, ConsecutiveHolds = 0 }));
    }

    // ---------- exclusion applied in generation ----------

    [Fact]
    public void Banished_bundle_leaves_every_position_with_an_alternative()
    {
        var a = Bundle("Pantry", 0, "Spring Foraging", "(O)16");
        var b = Bundle("Pantry", 0, "Orchard", "(O)613");
        var c = Bundle("Pantry", 1, "Spring Foraging", "(O)16");
        var d = Bundle("Pantry", 1, "Quality Crops", "(O)24");
        var positions = new List<IReadOnlyList<BundleSpec>> { new[] { a, b }, new[] { c, d } };

        var filtered = BanishRules.WithoutBanishedBundles(positions, new HashSet<string> { "Spring Foraging" });

        Assert.Equal(new[] { "Orchard" }, filtered[0].Select(x => x.Name));
        Assert.Equal(new[] { "Quality Crops" }, filtered[1].Select(x => x.Name));
    }

    [Fact]
    public void A_position_is_never_left_empty()
    {
        var only = Bundle("Pantry", 0, "Spring Foraging", "(O)16");
        var positions = new List<IReadOnlyList<BundleSpec>> { new[] { only } };
        var filtered = BanishRules.WithoutBanishedBundles(positions, new HashSet<string> { "Spring Foraging" });
        Assert.Same(positions[0], filtered[0]);
    }

    [Fact]
    public void No_banish_leaves_the_positions_untouched()
    {
        var positions = new List<IReadOnlyList<BundleSpec>> { new[] { Bundle("Pantry", 0, "Orchard", "(O)613") } };
        Assert.Same(positions, BanishRules.WithoutBanishedBundles(positions, new HashSet<string>()));
    }

    [Fact]
    public void Banished_items_join_the_saves_own_exclusions()
    {
        IReadOnlySet<string> baseSet = new HashSet<string> { "(O)Garlic" };
        IReadOnlySet<string>? joined = BanishRules.ExcludedIds(baseSet, new[] { Item("(O)24"), BundleBanish("Orchard") });
        Assert.Equal(new[] { "(O)24", "(O)Garlic" }, joined!.OrderBy(x => x));
        Assert.Same(baseSet, BanishRules.ExcludedIds(baseSet, new[] { BundleBanish("Orchard") }));
        Assert.Same(baseSet, BanishRules.ExcludedIds(baseSet, null));
        Assert.Equal(new[] { "Orchard" }, BanishRules.BundleNames(new[] { Item("(O)24"), BundleBanish("Orchard") }));
    }

    // ---------- "not enough to fill" fallback ----------

    /// <summary>A fake generator: the board it builds depends on which banishes it was given.</summary>
    private static (BanishBoardOutcome, string) Board(IReadOnlyList<BanishPick> banishes, Func<IReadOnlyList<BanishPick>, BanishBoardOutcome> make)
        => (make(banishes), string.Join("+", banishes.Select(b => b.Key)));

    [Fact]
    public void Item_absent_from_the_new_board_is_honoured()
    {
        var pending = new[] { Item("(O)24") };
        (BanishResolution r, string build) = BanishResolver.Resolve(pending, banishes => Board(banishes, bs =>
            new BanishBoardOutcome(new[] { Bundle("Pantry", 0, "Spring Crops", bs.Count > 0 ? "(O)192" : "(O)24") }, 0)));
        Assert.Single(r.Honoured);
        Assert.Empty(r.Refused);
        Assert.Equal("(O)24", build);
    }

    [Fact]
    public void Item_still_on_the_board_is_refused_and_the_plain_board_is_kept()
    {
        var pending = new[] { Item("(O)24") };
        (BanishResolution r, string build) = BanishResolver.Resolve(pending, banishes => Board(banishes, _ =>
            new BanishBoardOutcome(new[] { Bundle("Pantry", 0, "Spring Crops", "(O)24") }, 0)));
        Assert.Empty(r.Honoured);
        Assert.Single(r.Refused);
        Assert.Equal("", build);
    }

    [Fact]
    public void A_banish_that_makes_a_bundle_fall_back_to_its_template_is_refused()
    {
        var pending = new[] { Item("(O)24") };
        (BanishResolution r, _) = BanishResolver.Resolve(pending, banishes => Board(banishes, bs =>
            new BanishBoardOutcome(new[] { Bundle("Pantry", 0, "Spring Crops", "(O)192") }, bs.Count)));
        Assert.Single(r.Refused);
    }

    [Fact]
    public void A_banish_that_leaves_the_board_short_a_bundle_is_refused()
    {
        var pending = new[] { BundleBanish("Orchard") };
        (BanishResolution r, _) = BanishResolver.Resolve(pending, banishes => Board(banishes, bs =>
            new BanishBoardOutcome(
                bs.Count == 0
                    ? new[] { Bundle("Pantry", 0, "Orchard", "(O)613"), Bundle("Pantry", 1, "Quality Crops", "(O)24") }
                    : new[] { Bundle("Pantry", 1, "Quality Crops", "(O)24") }, 0)));
        Assert.Single(r.Refused);
    }

    [Fact]
    public void Banished_bundle_still_on_the_board_under_a_suffix_is_refused()
    {
        var pending = new[] { BundleBanish("Orchard") };
        (BanishResolution r, _) = BanishResolver.Resolve(pending, banishes => Board(banishes, _ =>
            new BanishBoardOutcome(new[] { Bundle("Pantry", 0, "Orchard II", "(O)613") }, 0)));
        Assert.Single(r.Refused);
    }

    [Fact]
    public void Pass_through_rooms_do_not_count_against_a_banish()
    {
        var board = new[] { Bundle("Abandoned Joja Mart", 36, "The Missing", "(O)74"), Bundle("Pantry", 0, "Orchard", "(O)613") };
        Assert.False(BanishResolver.Appears(board, Item("(O)74")));
        Assert.True(BanishResolver.Appears(board, Item("(O)613")));
    }

    [Fact]
    public void Banishes_are_tried_in_order_each_on_top_of_the_honoured_ones()
    {
        // The second banish fits alone but not with the first; the first is honoured, so it is refused.
        var pending = new[] { Item("(O)1"), Item("(O)2") };
        var calls = new List<string>();
        (BanishResolution r, string build) = BanishResolver.Resolve(pending, banishes =>
        {
            calls.Add(string.Join("+", banishes.Select(b => b.Key)));
            int fallbacks = banishes.Count == 2 ? 1 : 0;
            return Board(banishes, _ => new BanishBoardOutcome(new[] { Bundle("Pantry", 0, "X", "(O)9") }, fallbacks));
        });
        Assert.Equal(new[] { "", "(O)1", "(O)1+(O)2" }, calls);
        Assert.Equal(new[] { "(O)1" }, r.Honoured.Select(p => p.Key));
        Assert.Equal(new[] { "(O)2" }, r.Refused.Select(p => p.Key));
        Assert.Equal("(O)1", build);
    }

    // ---------- reset bookkeeping and expiry ----------

    [Fact]
    public void Reset_honours_refunds_and_queues_one_notice()
    {
        var s = new MetaState { JunimoPoints = 0 };
        var kept = Item("(O)1", 100);
        var refused = Item("(O)2", 200);
        s.PendingBanishes.AddRange(new[] { kept, refused });

        BanishRules.ApplyResolution(s, new BanishResolution(new[] { kept }, new[] { refused }));

        Assert.Equal(200, s.JunimoPoints);
        Assert.Empty(s.PendingBanishes);
        Assert.Equal(new[] { "(O)1" }, s.BoardBanishes.Select(p => p.Key));
        Assert.Equal(1, s.BanishesHonoured);
        IReadOnlyList<BanishPick>? notice = BanishRules.TakeNotice(s);
        Assert.Equal(new[] { "(O)2" }, notice!.Select(p => p.Key));
        Assert.Null(BanishRules.TakeNotice(s));
    }

    [Fact]
    public void A_banish_expires_after_the_loop_it_applied_to()
    {
        var s = new MetaState();
        s.BoardBanishes.Add(Item("(O)1"));
        // The next reset builds a new board with nothing pending.
        BanishRules.ApplyResolution(s, new BanishResolution(Array.Empty<BanishPick>(), Array.Empty<BanishPick>()));
        Assert.Empty(s.BoardBanishes);
        Assert.Null(BanishRules.TakeNotice(s));
    }

    [Fact]
    public void A_held_board_keeps_its_banishes_and_refunds_the_new_ones()
    {
        var s = new MetaState { JunimoPoints = 0 };
        s.BoardBanishes.Add(Item("(O)1"));
        s.PendingBanishes.Add(Item("(O)2", 350));

        IReadOnlyList<BanishPick> refunded = BanishRules.RefundAllPending(s);

        Assert.Single(refunded);
        Assert.Equal(350, s.JunimoPoints);
        Assert.Empty(s.PendingBanishes);
        Assert.Equal(new[] { "(O)1" }, s.BoardBanishes.Select(p => p.Key));
        Assert.Equal(0, s.BanishesHonoured);
        Assert.NotNull(BanishRules.TakeNotice(s));
    }

    [Fact]
    public void Refunded_banishes_do_not_climb_the_ladder()
    {
        var s = new MetaState();
        var refused = Item("(O)2", 100);
        s.PendingBanishes.Add(refused);
        BanishRules.ApplyResolution(s, new BanishResolution(Array.Empty<BanishPick>(), new[] { refused }));
        Assert.Equal(100, BanishLadder.NextCost(s));
    }

    // ---------- the pick list ----------

    [Fact]
    public void Pick_list_reads_this_loops_board()
    {
        var board = new Dictionary<string, string>
        {
            ["Pantry/0"] = "Spring Crops/O 465 20/24 1 0 188 1 0/0/2",
            ["Pantry/1"] = "Orchard/O 1 1/613 1 0 634 1 0/0/2",
            ["Pantry/2"] = "Orchard II/O 1 1/635 1 0/0/1//Orchard II",
            ["Crafts Room/13"] = "Spring Foraging/O 1 1/16 1 0 -5 1 0/0/2",
            ["Vault/23"] = "2,500g/O 220 3/-1 2500 2500/4/5",
            ["Abandoned Joja Mart/36"] = "The Missing/ /74 1 0/6/1",
        };

        Assert.Equal(new[] { "Spring Foraging", "Orchard" }, BanishCandidates.Bundles(board).Select(b => b.Name));
        Assert.Equal("Orchard", BanishCandidates.Bundles(board)[1].DisplayName);
        Assert.Equal(new[] { "(O)16", "(O)188", "(O)24", "(O)613", "(O)634", "(O)635" },
            BanishCandidates.Items(board).OrderBy(x => x, StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("Orchard II", "Orchard")]
    [InlineData("Orchard IV", "Orchard")]
    [InlineData("Orchard", "Orchard")]
    [InlineData("Chef's", "Chef's")]
    public void Base_name_drops_the_duplicate_suffix(string name, string expected)
        => Assert.Equal(expected, BanishCandidates.BaseName(name));
}
