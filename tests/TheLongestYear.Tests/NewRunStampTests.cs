using TheLongestYear.Core;

namespace TheLongestYear.Tests;

/// <summary>The creation-time save must already carry the new-game choices, so a farm quit before
/// its first night reloads with the board and mod-items setting the player picked.</summary>
public class NewRunStampTests
{
    [Theory]
    [InlineData(BundleSourceNames.Engine, BundleSourceNames.Engine, BundleSourceNames.VanillaTypeDefault)]
    [InlineData(BundleSourceNames.Normal, BundleSourceNames.LegacyVanilla, BundleSourceNames.VanillaTypeDefault)]
    [InlineData(BundleSourceNames.Remixed, BundleSourceNames.LegacyVanilla, BundleSourceNames.VanillaTypeRemixed)]
    public void Bundle_choice_is_stamped(string chosen, string source, string vanillaType)
    {
        var state = new MetaState();
        NewRunStamp.ApplyBundleChoice(state, chosen);
        Assert.Equal(chosen, state.ChosenBundleSource);
        Assert.Equal(source, state.BundleSource);
        Assert.Equal(vanillaType, state.VanillaBundleType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Marker_carries_the_run_flag_the_bundle_choice_and_the_title_default_for_mod_items(bool titleDefault)
    {
        MetaState marker = NewRunStamp.Marker(BundleSourceNames.Normal, titleDefault);
        Assert.True(marker.IsLongestYearRun);
        Assert.Equal(BundleSourceNames.Normal, marker.ChosenBundleSource);
        Assert.Equal(titleDefault, marker.AllowModItemsInCustomBundles);
    }
}
