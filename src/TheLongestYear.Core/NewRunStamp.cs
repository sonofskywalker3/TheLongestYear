namespace TheLongestYear.Core;

/// <summary>The new-game choices a Longest Year save carries from the moment it is created. The
/// game writes a new farm once at character creation; a farm quit before its first night reloads
/// from that write, so the choices must be in it (bundle source, mod items), not only in memory
/// until the first night's save.</summary>
public static class NewRunStamp
{
    /// <summary>Stamp the Advanced Options bundle choice: TLY Custom -> Engine; Normal/Remixed ->
    /// Vanilla plus the vanilla type, so every reset regenerates the same kind of board.</summary>
    public static void ApplyBundleChoice(MetaState state, string chosenSource)
    {
        state.BundleSource = BundleSourceNames.IsVanilla(chosenSource)
            ? BundleSourceNames.LegacyVanilla : BundleSourceNames.Engine;
        state.VanillaBundleType = BundleSourceNames.VanillaTypeFor(chosenSource) ?? BundleSourceNames.VanillaTypeDefault;
        state.ChosenBundleSource = chosenSource;
    }

    /// <summary>The meta written into the creation-time save.</summary>
    public static MetaState Marker(string chosenSource, bool allowModItemsTitleDefault)
    {
        var state = new MetaState
        {
            IsLongestYearRun = true,
            AllowModItemsInCustomBundles = CustomBoardModItems.Initial(isNewSave: true, allowModItemsTitleDefault),
        };
        ApplyBundleChoice(state, chosenSource);
        return state;
    }
}
