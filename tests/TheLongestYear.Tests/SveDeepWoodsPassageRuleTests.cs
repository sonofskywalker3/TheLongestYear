using System;
using System.Collections.Generic;
using TheLongestYear.Core;
using Xunit;

namespace TheLongestYear.Tests;

/// <summary>Stardew Valley Expanded ships a map patch that joins Deep Woods' passage to its larger
/// Secret Woods, keyed on the id Deep Woods had under its first author. Today's Deep Woods has
/// another id, so with both installed the passage was a sealed pocket (walked in game 2026-11-06).
/// The Longest Year applies SVE's patch itself, only in that case.</summary>
public class SveDeepWoodsPassageRuleTests
{
    private static Func<string, bool> Loaded(params string[] ids) => new HashSet<string>(ids).Contains;

    [Fact]
    public void Both_mods_under_todays_ids_need_the_patch()
        => Assert.True(SveDeepWoodsPassageRule.Applies(Loaded(SveUnlocks.ModId, SveDeepWoodsPassageRule.DeepWoodsId)));

    [Fact]
    public void Either_mod_alone_does_not()
    {
        Assert.False(SveDeepWoodsPassageRule.Applies(Loaded(SveUnlocks.ModId)));
        Assert.False(SveDeepWoodsPassageRule.Applies(Loaded(SveDeepWoodsPassageRule.DeepWoodsId)));
        Assert.False(SveDeepWoodsPassageRule.Applies(Loaded()));
    }

    [Fact]
    public void The_old_id_means_sve_patches_the_map_itself()
        => Assert.False(SveDeepWoodsPassageRule.Applies(
            Loaded(SveUnlocks.ModId, SveDeepWoodsPassageRule.DeepWoodsId, SveDeepWoodsPassageRule.OldDeepWoodsId)));
}
