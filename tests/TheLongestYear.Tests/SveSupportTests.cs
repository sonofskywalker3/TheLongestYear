using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Stardew Valley Expanded's unlocks rewind with the loop, with keeps sold (Jeff,
/// 2026-11-05): power scenes, story orders, the three mastery keeps and Keep Grandpa's Shed.</summary>
public class SveSupportTests
{
    [Theory]
    [InlineData("PLAYER_HAS_SEEN_EVENT Any 1000026", "1000026")]
    [InlineData("player_has_seen_event Current 5407917", "5407917")]
    [InlineData("SEASON spring, PLAYER_HAS_SEEN_EVENT Any 908072", "908072")]
    [InlineData("PLAYER_HAS_MAIL Current HasRustyKey", null)]
    [InlineData("!PLAYER_HAS_SEEN_EVENT Any 1000026", null)]
    [InlineData("PLAYER_HAS_SEEN_EVENT Any", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Power_scene_is_read_from_the_unlock_condition(string? condition, string? expected)
    {
        HashSet<string> ids = PowerEventMarkers.EventIds(new[] { condition });
        if (expected == null) Assert.Empty(ids);
        else Assert.Equal(new HashSet<string> { expected }, ids);
    }

    [Fact]
    public void The_rewound_set_holds_both_scenes_of_every_story_order_and_the_shed_scene()
    {
        Assert.Equal(17, SveUnlocks.StoryOrders.Count);
        foreach (SveStoryOrder order in SveUnlocks.StoryOrders)
        {
            Assert.Contains(order.TriggerEvent, SveUnlocks.RewoundEventIds);
            Assert.Contains(order.DoneEvent, SveUnlocks.RewoundEventIds);
        }
        Assert.Contains(SveUnlocks.ShedCompleteEvent, SveUnlocks.RewoundEventIds);
    }

    /// <summary>SVE's western forest leads into the Secret Woods past a large log that is placed only
    /// while the woods' first-visit scene is unseen. Carried over as seen, the log was gone from the
    /// second loop on and the woods were open on day 1.</summary>
    [Fact]
    public void The_secret_woods_first_visit_scene_rewinds_so_the_western_log_comes_back()
        => Assert.Contains("7134028", SveUnlocks.RewoundEventIds);

    [Fact]
    public void The_scene_before_an_order_scene_is_rewound_with_it()
    {
        // Clint's boulder order: the Skull Key gate sits on 8050107, the order scene on 8050108.
        var keys = new List<(string id, string key)>
        {
            ("8050107", "8050107/t 600 1500/H"),
            ("8050108", "8050108/e 8050107"),
            ("999", "999/e 111"),
        };
        var flagged = new HashSet<string> { "8050108" };

        int added = EventGatingTables.AddDirectPrerequisites(
            keys, new HashSet<string> { "8050108" }, flagged, exclude: new HashSet<string>());

        Assert.Equal(1, added);
        Assert.Contains("8050107", flagged);
        Assert.DoesNotContain("111", flagged);
    }

    [Fact]
    public void An_excluded_scene_is_never_pulled_in_as_a_prerequisite()
    {
        var keys = new List<(string id, string key)> { ("5", "5/e 191393") };
        var flagged = new HashSet<string>();

        EventGatingTables.AddDirectPrerequisites(
            keys, new HashSet<string> { "5" }, flagged, exclude: new HashSet<string> { "191393" });

        Assert.Empty(flagged);
    }

    [Fact]
    public void Story_orders_are_forgotten_like_town_orders()
    {
        var types = new Dictionary<string, string?>
        {
            ["Robin3"] = SveUnlocks.StoryOrderType,
            ["Clint2"] = SveUnlocks.StoryOrderType,
            ["QiChallenge2"] = "Qi",
        };

        List<string> forget = SpecialOrderResetRules.CompletedToForget(new[] { "Robin3", "Clint2", "QiChallenge2" }, types);

        Assert.Equal(new[] { "Robin3", "Clint2" }, forget);
    }

    [Fact]
    public void Keep_grandpas_shed_leaves_its_order_completed()
    {
        var types = new Dictionary<string, string?>
        {
            ["Robin3"] = SveUnlocks.StoryOrderType,
            ["Clint2"] = SveUnlocks.StoryOrderType,
        };
        IReadOnlySet<string> kept = SveUnlocks.KeptCompletedOrders(id => id == SveUnlocks.KeepShedId);

        List<string> forget = SpecialOrderResetRules.CompletedToForget(new[] { "Robin3", "Clint2" }, types, kept);

        Assert.Equal(new[] { "Clint2" }, forget);
        Assert.Empty(SveUnlocks.KeptCompletedOrders(_ => false));
    }

    [Fact]
    public void Keep_grandpas_shed_puts_back_its_scenes_and_mail()
    {
        var meta = new MetaState { OwnedUpgrades = { SveUnlocks.KeepShedId } };
        RunBaseline kept = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        RunBaseline plain = RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500);

        // Robin's inspection, her order scene, the completion scene and the first visit inside.
        Assert.Equal(new[] { "2554902", "2554903", SveUnlocks.ShedCompleteEvent, "2554907" }, kept.KeptEventIds);
        Assert.Contains("ShedRepaired", kept.KeptMailFlags);
        Assert.Empty(plain.KeptEventIds);
        Assert.Empty(plain.KeptMailFlags);
    }

    [Theory]
    [InlineData(WalletKeepTable.CheeseMasteryKeepId, 150, "event:1000026")]
    [InlineData(WalletKeepTable.CraftMasteryKeepId, 350, "event:1000083")]
    [InlineData(WalletKeepTable.BrewingMasteryKeepId, 350, "event:5407917")]
    public void Mastery_keeps_are_power_rows(string id, long cost, string reach)
    {
        WalletKeep keep = Assert.Single(WalletKeepTable.Entries, e => e.UpgradeId == id);
        Assert.Equal(WalletKeepKind.Event, keep.Kind);
        Assert.Equal(cost, keep.Cost);
        Assert.Equal(reach, keep.Reach);
        Assert.NotNull(RunReachRequirement.Parse(keep.Reach));

        var meta = new MetaState { OwnedUpgrades = { id } };
        RunBaseline b = RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500);
        Assert.Equal(new[] { keep.EventId! }, b.KeptEventIds);
    }

    [Fact]
    public void Sve_rows_are_offered_only_with_sve_installed()
    {
        string[] carryover =
        {
            WalletKeepTable.CheeseMasteryKeepId, WalletKeepTable.CraftMasteryKeepId, WalletKeepTable.BrewingMasteryKeepId,
        };
        var before = UpgradeCatalog.IsModLoaded;
        try
        {
            UpgradeCatalog.IsModLoaded = _ => false;
            foreach (string id in carryover)
                Assert.DoesNotContain(UpgradeCatalog.ByCategory(UpgradeCategory.Carryover), d => d.Id == id);
            Assert.DoesNotContain(UpgradeCatalog.ByCategory(UpgradeCategory.Buildings), d => d.Id == SveUnlocks.KeepShedId);

            UpgradeCatalog.IsModLoaded = id => id == SveUnlocks.ModId;
            foreach (string id in carryover)
                Assert.Contains(UpgradeCatalog.ByCategory(UpgradeCategory.Carryover), d => d.Id == id);
            UpgradeDefinition shed = Assert.Single(
                UpgradeCatalog.ByCategory(UpgradeCategory.Buildings), d => d.Id == SveUnlocks.KeepShedId);
            Assert.Equal(SveUnlocks.KeepShedCost, shed.Cost);
        }
        finally
        {
            UpgradeCatalog.IsModLoaded = before;
        }
    }
}
