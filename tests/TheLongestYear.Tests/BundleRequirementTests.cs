using System.Linq;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

public class BundleRequirementTests
{
    // ===== KIND 1: Seasonal =====

    [Fact]
    public void Seasonal_bundle_not_due_before_its_season()
    {
        // Summer bundle checked at Spring 28 -> not yet due, passes with empty donations.
        var b = BundleRequirement.CreateSeasonal("Summer Foraging", Theme.Foraging,
            new[] { "Grape", "SpiceBerry", "SweetPea" }, Season.Summer);
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Spring, TestLedger.Empty()));
    }

    [Fact]
    public void Seasonal_bundle_must_stay_complete_in_later_seasons()
    {
        // Spring bundle checked at Winter 28 -> well past due; empty donations fails.
        var b = BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging,
            new[] { "Horseradish", "Daffodil", "Leek", "Dandelion" }, Season.Spring);
        Assert.False(b.IsSatisfiedAtSeasonEnd(Season.Winter, TestLedger.Empty()));
    }

    [Fact]
    public void Seasonal_bundle_fails_when_in_season_and_missing_an_item()
    {
        var b = BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging,
            new[] { "Horseradish", "Daffodil", "Leek", "Dandelion" }, Season.Spring);
        var donated = TestLedger.Fill(b, "Horseradish", "Daffodil", "Leek");
        Assert.False(b.IsSatisfiedAtSeasonEnd(Season.Spring, donated));
    }

    [Fact]
    public void Seasonal_bundle_passes_when_all_donated_in_its_season()
    {
        var b = BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging,
            new[] { "Horseradish", "Daffodil", "Leek", "Dandelion" }, Season.Spring);
        var donated = TestLedger.Fill(b, "Horseradish", "Daffodil", "Leek", "Dandelion");
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Spring, donated));
    }

    // ===== KIND 2: PerItem =====

    [Fact]
    public void PerItem_bundle_passes_when_no_items_due_yet()
    {
        var b = BundleRequirement.CreatePerItem("Blacksmiths", Theme.Mining,
            new Dictionary<string, Season>
            {
                ["Copper"] = Season.Spring,
                ["Iron"] = Season.Summer,
                ["Gold"] = Season.Fall
            });
        // At Spring 28 only Copper is due. If Copper donated -> pass.
        var donated = TestLedger.Fill(b, "Copper");
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Spring, donated));
    }

    [Fact]
    public void PerItem_bundle_fails_when_earlier_pinned_item_undonated()
    {
        var b = BundleRequirement.CreatePerItem("Blacksmiths", Theme.Mining,
            new Dictionary<string, Season>
            {
                ["Copper"] = Season.Spring,
                ["Iron"] = Season.Summer,
                ["Gold"] = Season.Fall
            });
        // At Summer 28 both Copper and Iron are due. Missing Copper -> fail.
        var donated = TestLedger.Fill(b, "Iron");
        Assert.False(b.IsSatisfiedAtSeasonEnd(Season.Summer, donated));
    }

    [Fact]
    public void PerItem_bundle_passes_at_fall_when_all_pinned_through_fall_donated()
    {
        var b = BundleRequirement.CreatePerItem("Blacksmiths", Theme.Mining,
            new Dictionary<string, Season>
            {
                ["Copper"] = Season.Spring,
                ["Iron"] = Season.Summer,
                ["Gold"] = Season.Fall
            });
        var donated = TestLedger.Fill(b, "Copper", "Iron", "Gold");
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Fall, donated));
    }

    // ===== KIND 3: Percentage =====

    [Fact]
    public void Percentage_bundle_quota_per_season()
    {
        // Artisan-style: X=6, Y=12, quotas {1, 2, 4, 6}.
        var ingredients = new List<string>();
        for (int i = 0; i < 12; i++) ingredients.Add($"art-{i}");
        var b = BundleRequirement.CreatePercentage("Artisan", Theme.Farming,
            ingredients, numberOfSlots: 6, cumulativeRequiredBySeason: new[] { 1, 2, 4, 6 });

        var donated = TestLedger.Fill(b, "art-0");
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Spring, donated));    // 1/1 ok
        Assert.False(b.IsSatisfiedAtSeasonEnd(Season.Summer, donated));   // need 2

        donated = TestLedger.Fill(b, "art-0", "art-1");
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Summer, donated));    // 2/2 ok
        Assert.False(b.IsSatisfiedAtSeasonEnd(Season.Fall, donated));     // need 4

        donated = TestLedger.Fill(b, "art-0", "art-1", "art-2", "art-3");
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Fall, donated));      // 4/4 ok
        Assert.False(b.IsSatisfiedAtSeasonEnd(Season.Winter, donated));   // need 6

        donated = TestLedger.Fill(b, "art-0", "art-1", "art-2", "art-3", "art-4", "art-5");
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Winter, donated));    // 6/6 ok
    }

    [Fact]
    public void Percentage_bundle_zero_quota_is_trivially_met()
    {
        // Adventurer-style: 0 by Spring, 1 by Summer.
        var b = BundleRequirement.CreatePercentage("Adventurer", Theme.Mining,
            new[] { "Slime", "Bat", "Solar", "Void", "Bug" },
            numberOfSlots: 2,
            cumulativeRequiredBySeason: new[] { 0, 1, 2, 2 });
        Assert.True(b.IsSatisfiedAtSeasonEnd(Season.Spring, TestLedger.Empty()));
    }

    [Fact]
    public void Full_completion_checks_X_donations()
    {
        var b = BundleRequirement.CreatePercentage("Chef", Theme.Mixed,
            new[] { "a", "b", "c", "d", "e", "f", "g" }, numberOfSlots: 6,
            cumulativeRequiredBySeason: new[] { 1, 2, 4, 6 });
        var donated = TestLedger.Fill(b, "a", "b", "c", "d", "e");
        Assert.False(b.IsFullyComplete(donated));
        donated = TestLedger.Fill(b, "a", "b", "c", "d", "e", "f");
        Assert.True(b.IsFullyComplete(donated));
    }

    // ===== InPlayItemsFor =====

    [Fact]
    public void Seasonal_in_play_items_only_during_its_season()
    {
        var b = BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging,
            new[] { "Horseradish", "Daffodil" }, Season.Spring);
        Assert.Equal(2, System.Linq.Enumerable.Count(b.InPlayItemsFor(Season.Spring, _ => true)));
        Assert.Empty(b.InPlayItemsFor(Season.Summer, _ => true));
    }

    [Fact]
    public void Due_items_follow_the_gate_per_kind()
    {
        var perItem = BundleRequirement.CreatePerItem("Blacksmiths", Theme.Mining,
            new Dictionary<string, Season> { ["Copper"] = Season.Spring, ["Iron"] = Season.Summer });
        Assert.Equal(new[] { "Copper" }, perItem.DueItemsFor(Season.Spring, _ => true).ToArray());
        Assert.Equal(new[] { "Iron" }, perItem.DueItemsFor(Season.Summer, _ => true).ToArray());
        Assert.Empty(perItem.DueItemsFor(Season.Fall, _ => true));

        var seasonal = BundleRequirement.CreateSeasonal("Spring Crops", Theme.Farming, new[] { "A", "B" }, Season.Spring);
        Assert.Equal(new[] { "A", "B" }, seasonal.DueItemsFor(Season.Spring, _ => true).ToArray());
        Assert.Empty(seasonal.DueItemsFor(Season.Summer, _ => true));

        var pct = BundleRequirement.CreatePercentage("Crab Pot", Theme.Fishing, new[] { "X", "Y", "Z" }, 3, new[] { 1, 1, 2, 3 });
        Assert.Equal(new[] { "X", "Y", "Z" }, pct.DueItemsFor(Season.Spring, _ => true).ToArray());
        Assert.Empty(pct.DueItemsFor(Season.Summer, _ => true));
        Assert.Equal(new[] { "Y" }, pct.DueItemsFor(Season.Fall, id => id == "Y").ToArray());
    }

    /// <summary>Real-play simulation 2026-08-28: PerItem goals used to be only the items DUE this
    /// season, which left the Mixed theme with one goal all Spring and none in weeks 3 and 4.
    /// Every obtainable ingredient is a candidate now; the deadline only drives the gate.</summary>
    [Fact]
    public void PerItem_in_play_items_are_every_obtainable_ingredient_whatever_their_deadline()
    {
        var b = BundleRequirement.CreatePerItem("Blacksmiths", Theme.Mining,
            new Dictionary<string, Season>
            {
                ["Copper"] = Season.Spring,
                ["Iron"] = Season.Summer
            });
        Assert.Equal(new[] { "Copper", "Iron" }, System.Linq.Enumerable.ToArray(b.InPlayItemsFor(Season.Spring, _ => true)));
        Assert.Equal(new[] { "Copper", "Iron" }, System.Linq.Enumerable.ToArray(b.InPlayItemsFor(Season.Fall, _ => true)));
        Assert.Equal(new[] { "Iron" }, System.Linq.Enumerable.ToArray(b.InPlayItemsFor(Season.Fall, id => id == "Iron")));
    }

    /// <summary>Bundle-loop audit 2026-08-29: Lake Fish pinned Sturgeon (Summer/Winter) to Fall
    /// and Rainbow Trout (Summer) to Winter, and the Fishing theme offered both as goals in
    /// seasons they cannot be caught. A PerItem deadline says when the item is DUE, not when it
    /// exists, so the goal pool must apply the same obtainability predicate Percentage does.</summary>
    [Fact]
    public void PerItem_in_play_items_also_pass_the_obtainability_predicate()
    {
        var b = BundleRequirement.CreatePerItem("Lake Fish", Theme.Fishing,
            new Dictionary<string, Season>
            {
                ["Sturgeon"] = Season.Fall,
                ["Walleye"] = Season.Fall,
            });
        var inPlay = System.Linq.Enumerable.ToArray(b.InPlayItemsFor(Season.Fall, id => id == "Walleye"));
        Assert.Equal(new[] { "Walleye" }, inPlay);
    }

    [Fact]
    public void Percentage_in_play_items_filtered_by_predicate()
    {
        var b = BundleRequirement.CreatePercentage("Artisan", Theme.Farming,
            new[] { "Honey", "Wine", "Cloth" }, numberOfSlots: 2,
            cumulativeRequiredBySeason: new[] { 0, 1, 1, 2 });
        // Only Honey passes the predicate.
        var inPlay = System.Linq.Enumerable.ToArray(b.InPlayItemsFor(Season.Summer, id => id == "Honey"));
        Assert.Equal(new[] { "Honey" }, inPlay);
    }

    [Fact]
    public void Percentage_in_play_items_excluded_when_season_quota_is_zero()
    {
        // 2026-05-28 reversal of the prior decision: zero-quota Percentage bundles return an
        // empty in-play set so their ingredients don't pollute the bonus pool. Rarity-only
        // weighting (Common×8 vs VeryRare×1) can't keep deep-mine essences out of Spring
        // Mining when essences are priced as Common (Solar 40g) / Uncommon (Void 50g) —
        // playtest confirmed Solar+Void Essence appeared in Spring W1 Mining bonus. A
        // non-zero quota means the bundle is on the critical path this season, so its items
        // are fair game.
        var b = BundleRequirement.CreatePercentage("Adventurer", Theme.Mining,
            new[] { "solar-essence", "void-essence", "bat-wing" },
            numberOfSlots: 2,
            cumulativeRequiredBySeason: new[] { 0, 1, 2, 2 });
        Assert.Empty(b.InPlayItemsFor(Season.Spring, _ => true)); // quota 0 -> excluded
        Assert.Equal(3, System.Linq.Enumerable.Count(b.InPlayItemsFor(Season.Summer, _ => true))); // quota 1 -> included
    }

    // ===== Per-slot ledger (spec 2026-08-29-per-slot-ledger) =====

    private static BundleRequirement Construction(int bundleIndex) => BundleRequirement.CreatePerItem(
        "Construction", Theme.Foraging,
        new[] { "(O)388", "(O)390", "(O)709" },
        new Dictionary<string, Season> { ["(O)388"] = Season.Spring, ["(O)390"] = Season.Spring, ["(O)709"] = Season.Summer },
        bundleIndex: bundleIndex,
        slots: new[] { new BundleSlot(0, "(O)388"), new BundleSlot(1, "(O)388"), new BundleSlot(2, "(O)390"), new BundleSlot(3, "(O)709") });

    [Fact]
    public void Slots_default_to_one_per_ingredient_in_order_and_BundleIndex_to_minus_one()
    {
        var b = BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging, new[] { "A", "B" }, Season.Spring);
        Assert.Equal(-1, b.BundleIndex);
        Assert.Equal(new[] { new BundleSlot(0, "A"), new BundleSlot(1, "B") }, b.Slots);
    }

    [Fact]
    public void Supplied_slots_set_NumberOfSlots_for_PerItem_and_Seasonal()
    {
        Assert.Equal(4, Construction(1).NumberOfSlots);
        var seasonal = BundleRequirement.CreateSeasonal("S", Theme.Foraging, new[] { "A" }, Season.Spring,
            bundleIndex: 2, slots: new[] { new BundleSlot(0, "A"), new BundleSlot(1, "A") });
        Assert.Equal(2, seasonal.NumberOfSlots);
    }

    [Fact]
    public void One_deposit_credits_one_bundle_not_every_bundle_listing_the_id()
    {
        var foraging = BundleRequirement.CreateSeasonal("Spring Foraging", Theme.Foraging, new[] { "(O)296", "(O)16" }, Season.Spring, bundleIndex: 1);
        var childrens = BundleRequirement.CreateSeasonal("Children's", Theme.Foraging, new[] { "(O)296", "(O)18" }, Season.Spring, bundleIndex: 2);
        SlotLedger ledger = TestLedger.Fill((foraging, "(O)296"), (foraging, "(O)16"));
        Assert.True(foraging.IsSatisfiedAtSeasonEnd(Season.Spring, ledger));
        Assert.False(childrens.IsSatisfiedAtSeasonEnd(Season.Spring, ledger));
        Assert.Equal(2, ledger.FilledCount(1));
        Assert.Equal(0, ledger.FilledCount(2));
    }

    [Fact]
    public void A_doubled_id_needs_both_slots_filled()
    {
        var construction = Construction(13);
        var ledger = new SlotLedger();
        ledger.Add(13, 0, "(O)388");
        ledger.Add(13, 2, "(O)390");
        ledger.Add(13, 3, "(O)709");
        Assert.False(construction.IsFullyComplete(ledger));            // 3 of 4 slots
        Assert.False(construction.IsSatisfiedAtSeasonEnd(Season.Spring, ledger));  // Wood slot 1 is pinned Spring too
        var missing = construction.MissingForSeason(Season.Spring, ledger);
        Assert.Equal(1, missing.Count);
        Assert.Equal(new[] { "(O)388" }, missing.ItemIds);
        ledger.Add(13, 1, "(O)388");
        Assert.True(construction.IsFullyComplete(ledger));
        Assert.True(construction.IsSatisfiedAtSeasonEnd(Season.Winter, ledger));
    }

    [Fact]
    public void Percentage_counts_only_its_own_bundles_slots()
    {
        var crab = BundleRequirement.CreatePercentage("Crab Pot", Theme.Fishing, new[] { "A", "B", "C", "D" }, 3, new[] { 1, 2, 3, 3 }, bundleIndex: 4);
        var other = BundleRequirement.CreateSeasonal("Other", Theme.Fishing, new[] { "A", "B" }, Season.Spring, bundleIndex: 5);
        SlotLedger ledger = TestLedger.Fill((other, "A"), (other, "B"));
        Assert.False(crab.IsSatisfiedAtSeasonEnd(Season.Spring, ledger));
        var missing = crab.MissingForSeason(Season.Summer, ledger);
        Assert.Equal(2, missing.Count);
        Assert.Equal(new[] { "A", "B", "C", "D" }, missing.ItemIds);
    }

    [Fact]
    public void MissingForSeason_count_zero_matches_IsSatisfiedAtSeasonEnd_for_every_kind()
    {
        var seasonal = BundleRequirement.CreateSeasonal("S", Theme.Foraging, new[] { "A", "B" }, Season.Summer, bundleIndex: 1);
        var perItem = BundleRequirement.CreatePerItem("P", Theme.Mining, new Dictionary<string, Season> { ["X"] = Season.Spring, ["Y"] = Season.Fall }, bundleIndex: 2);
        var pct = BundleRequirement.CreatePercentage("Q", Theme.Farming, new[] { "M", "N", "O" }, 2, new[] { 1, 1, 2, 2 }, bundleIndex: 3);
        SlotLedger ledger = TestLedger.Fill((seasonal, "A"), (perItem, "X"), (pct, "M"));
        foreach (Season s in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
        {
            foreach (BundleRequirement r in new[] { seasonal, perItem, pct })
                Assert.Equal(r.IsSatisfiedAtSeasonEnd(s, ledger), r.MissingForSeason(s, ledger).Count == 0);
        }
        Assert.Equal(0, seasonal.MissingForSeason(Season.Spring, ledger).Count);   // not due yet
        Assert.Equal(1, seasonal.MissingForSeason(Season.Summer, ledger).Count);
        Assert.Equal(1, perItem.MissingForSeason(Season.Fall, ledger).Count);
        Assert.Equal(1, pct.MissingForSeason(Season.Fall, ledger).Count);
    }
}
