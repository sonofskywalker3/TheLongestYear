using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Sword and Sorcery's keep row: offered only with the mod, bought once the guild is built
/// this loop, and owned it puts the guild's mail back at the rewind.</summary>
public class SnsSupportTests
{
    [Fact]
    public void Keep_coastal_guild_shows_only_with_the_mod_needs_the_guild_and_keeps_its_mail()
    {
        var before = UpgradeCatalog.IsModLoaded;
        try
        {
            UpgradeCatalog.IsModLoaded = _ => false;
            Assert.DoesNotContain(UpgradeCatalog.ByCategory(UpgradeCategory.Buildings), d => d.Id == SnsUnlocks.KeepCoastalGuildId);
            UpgradeCatalog.IsModLoaded = id => id == SnsUnlocks.ModId;
            UpgradeDefinition row = Assert.Single(UpgradeCatalog.ByCategory(UpgradeCategory.Buildings), d => d.Id == SnsUnlocks.KeepCoastalGuildId);
            Assert.Equal(350L, row.Cost);
            RunReachRequirement? reach = RunReachRequirement.Parse(row.RunReachRequirement);
            Assert.Equal("mail", reach!.Metric);
            Assert.Equal("Mateo_SpecialOrders_BuildGuildMail", reach.Key);
        }
        finally
        {
            UpgradeCatalog.IsModLoaded = before;
        }

        var meta = new MetaState { OwnedUpgrades = { SnsUnlocks.KeepCoastalGuildId } };
        Assert.Contains("Mateo_SpecialOrders_BuildGuildMail", RunBaselineBuilder.Build(meta, new RunState(), PlayerSnapshot.Empty, 500).KeptMailFlags);
        Assert.DoesNotContain("Mateo_SpecialOrders_BuildGuildMail", RunBaselineBuilder.Build(new MetaState(), new RunState(), PlayerSnapshot.Empty, 500).KeptMailFlags);
    }

    private static readonly Dictionary<string, string?> SnsOrderTypes = new()
    {
        ["Mateo.SpecialOrders.BuildGuild"] = "SwordSorcery.Event",
        ["Mateo.SpecialOrders.AQ1"] = "SwordSorcery",
        ["QiChallenge2"] = "Qi",
    };

    [Theory]
    [InlineData(false, new[] { "Mateo.SpecialOrders.BuildGuild", "Mateo.SpecialOrders.AQ1" })]
    [InlineData(true, new[] { "Mateo.SpecialOrders.AQ1" })]
    public void Rewind_forgets_the_mods_orders_but_keeps_the_guild_order_done_with_the_keep(bool keepOwned, string[] forgotten)
    {
        IReadOnlySet<string> kept = SpecialOrderResetRules.KeptCompletedOrders(id => keepOwned && id == SnsUnlocks.KeepCoastalGuildId);

        Assert.Equal(forgotten, SpecialOrderResetRules.CompletedToForget(
            new[] { "Mateo.SpecialOrders.BuildGuild", "Mateo.SpecialOrders.AQ1", "QiChallenge2" }, SnsOrderTypes, kept));
        // A kept order missing from the record is put back, so the replayed scene cannot hand it out.
        Assert.Equal(keepOwned ? new[] { "Mateo.SpecialOrders.BuildGuild" } : new string[0],
            SpecialOrderResetRules.KeptToRecord(new[] { "QiChallenge2" }, kept));
        Assert.Empty(SpecialOrderResetRules.KeptToRecord(new[] { "Mateo.SpecialOrders.BuildGuild" }, kept));
    }
}
