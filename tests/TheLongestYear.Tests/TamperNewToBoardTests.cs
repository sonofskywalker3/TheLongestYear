using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using TheLongestYear.Core.Sabotage;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Designer, 2026-10-07: "they should skip anything on the board, it's something new." A
/// tamper's replacement is never an item any slot on the board asks for, filled or open, in any
/// flavour, on top of the bar on tainted items.</summary>
public class TamperNewToBoardTests
{
    private const string Potato = "(O)192";
    private const string Beer = "(O)346";
    private const string Wine = "(O)348";
    private const string Dried = "(O)DriedFruit";
    private const int Seeds = 80;

    private static BundleRequirement Bundle(string name, Theme theme, int index, int slots, params string[] ids)
        => BundleRequirement.CreatePercentage(name, theme, ids, slots, new[] { 0, 0, 0, slots }, bundleIndex: index);

    private static List<TamperCandidate> Pool()
        => new()
        {
            new(Beer, Theme.Farming, 5),
            new(Wine, Theme.Farming, 5),
            new(Potato, Theme.Farming, 5),
            new(Dried, Theme.Farming, 5),
            new("(O)428", Theme.Farming, 5),
        };

    [Fact]
    public void An_item_asked_anywhere_on_the_board_is_never_the_replacement()
    {
        var artisan = Bundle("Artisan", Theme.Farming, 1, 2, "(O)24", "(O)188");
        var brewer = Bundle("Brewer's", Theme.Mixed, 2, 2, Beer, "(O)262");        // open slot elsewhere
        var crops = Bundle("Fall Crops", Theme.Farming, 3, 2, "192", "(O)276");     // bare spelling
        var board = new[] { artisan, brewer, crops };
        var target = new TamperTarget(artisan, 0, "(O)24");
        for (int seed = 0; seed < Seeds; seed++)
        {
            TamperCandidate? pick = TamperRule.PickReplacement(target, 5, Pool(), new Random(seed), null, board);
            Assert.NotNull(pick);
            Assert.NotEqual(Beer, pick!.ItemId);
            Assert.NotEqual(Potato, pick.ItemId);
        }
    }

    [Fact]
    public void A_filled_slot_still_keeps_its_item_off_the_replacement_list()
    {
        // The board list is every slot's ask; whether the slot is filled does not matter.
        var artisan = Bundle("Artisan", Theme.Farming, 1, 1, "(O)24");
        var cellar = Bundle("Cellar", Theme.Farming, 2, 1, Wine);
        var target = new TamperTarget(artisan, 0, "(O)24");
        for (int seed = 0; seed < Seeds; seed++)
            Assert.NotEqual(Wine, TamperRule.PickReplacement(target, 5, Pool(), new Random(seed), null, new[] { artisan, cellar })!.ItemId);
    }

    [Fact]
    public void A_flavoured_ask_on_the_board_bars_every_flavour_of_that_good()
    {
        // A Dried Apples slot: a Dried Fruit replacement is written unflavoured ("any"), so it is
        // the same good the board already asks for.
        var artisan = Bundle("Artisan", Theme.Farming, 1, 1, "(O)24");
        var brewer = Bundle("Brewer's", Theme.Farming, 2, 1, "DriedFruit");
        var target = new TamperTarget(artisan, 0, "(O)24");
        for (int seed = 0; seed < Seeds; seed++)
            Assert.NotEqual(Dried, TamperRule.PickReplacement(target, 5, Pool(), new Random(seed), null, new[] { artisan, brewer })!.ItemId);
    }

    [Fact]
    public void The_board_and_the_tainted_bar_apply_together()
    {
        var artisan = Bundle("Artisan", Theme.Farming, 1, 1, "(O)24");
        var brewer = Bundle("Brewer's", Theme.Farming, 2, 3, Beer, Wine, Potato);
        var tainted = new[] { new TamperRecord { OldItemId = Dried, OldFlavor = "613" } };
        var target = new TamperTarget(artisan, 0, "(O)24");
        for (int seed = 0; seed < Seeds; seed++)
            Assert.Equal("(O)428", TamperRule.PickReplacement(target, 5, Pool(), new Random(seed), tainted, new[] { artisan, brewer })!.ItemId);
    }

    [Fact]
    public void A_pool_of_nothing_but_board_items_leaves_no_replacement()
    {
        var artisan = Bundle("Artisan", Theme.Farming, 1, 1, "(O)24");
        var brewer = Bundle("Brewer's", Theme.Farming, 2, 2, Beer, Wine);
        var target = new TamperTarget(artisan, 0, "(O)24");
        var pool = new List<TamperCandidate> { new(Beer, Theme.Farming, 5), new(Wine, Theme.Farming, 5) };
        Assert.Null(TamperRule.PickReplacement(target, 5, pool, new Random(1), null, new[] { artisan, brewer }));
        // Without the board the same pool still gives a pick: the bar is what emptied it.
        Assert.NotNull(TamperRule.PickReplacement(target, 5, pool, new Random(1)));
    }

    [Fact]
    public void Across_many_seeds_and_boards_the_replacement_is_always_new_to_the_board()
    {
        string[] universe = { Beer, Wine, Potato, Dried, "(O)428", "(O)24", "(O)188", "(O)262", "(O)276", "(O)400", "(O)254", "(O)256" };
        var candidates = new List<TamperCandidate>();
        foreach (string id in universe) candidates.Add(new TamperCandidate(id, Theme.Farming, id.Length));
        for (int seed = 0; seed < 300; seed++)
        {
            var rng = new Random(seed);
            var askedIds = new List<string>();
            var bundles = new List<BundleRequirement>();
            for (int b = 0; b < 3; b++)
            {
                var ids = new List<string>();
                int n = 1 + rng.Next(3);
                for (int s = 0; s < n; s++) ids.Add(universe[rng.Next(universe.Length)]);
                askedIds.AddRange(ids);
                bundles.Add(Bundle("B" + b, Theme.Farming, b + 1, ids.Count, ids.ToArray()));
            }
            var target = new TamperTarget(bundles[0], 0, bundles[0].Slots[0].ItemId);
            TamperCandidate? pick = TamperRule.PickReplacement(target, 5, candidates, new Random(seed), null, bundles);
            if (pick == null)
            {
                foreach (string id in universe) Assert.Contains(id, askedIds);
                continue;
            }
            Assert.DoesNotContain(pick.ItemId, askedIds);
        }
    }
}
