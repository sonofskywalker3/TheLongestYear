namespace TheLongestYear.Core;

/// <summary>"Allow mod items in custom bundles": whether a TLY Custom (Engine) board may ask for
/// and reward other mods' items (spec 2026-10-08-custom-board-vanilla-only, addendum 1). Per save,
/// shaped like BundleSource: <see cref="GameplayConfig.AllowModItemsInCustomBundles"/> is only the
/// value a NEW game starts with, <see cref="MetaState.AllowModItemsInCustomBundles"/> is the save's
/// choice (applied at its next reset), and <see cref="MetaState.BoardAllowsModItems"/> is what the
/// board on disk was generated with.</summary>
public static class CustomBoardModItems
{
    /// <summary>A save from before this option has no stored choice. Its boards were built with
    /// other mods' items allowed (before 0.19.2), so it keeps that behavior.</summary>
    public const bool ExistingSaveDefault = true;

    /// <summary>The choice to write when a save has none yet. A new game takes the title-screen
    /// default; anything else is an existing save and gets <see cref="ExistingSaveDefault"/>.</summary>
    public static bool Initial(bool isNewSave, bool titleDefault)
        => isNewSave ? titleDefault : ExistingSaveDefault;

    /// <summary>The save's choice, with a missing value read as an existing save.</summary>
    public static bool Chosen(bool? stored) => stored ?? ExistingSaveDefault;

    /// <summary>The value the board currently on disk was generated with. Unstamped boards (written
    /// before this field existed) fall back to the save's choice.</summary>
    public static bool OnBoard(bool? boardStamp, bool? chosen) => boardStamp ?? Chosen(chosen);

    /// <summary>The value a reset builds its board with. A held board (Fail-night keep) keeps the
    /// value it was built under, so keeping a board never changes it; a new board takes the choice.</summary>
    public static bool ForReset(bool holdingBoard, bool? boardStamp, bool? chosen)
        => holdingBoard ? OnBoard(boardStamp, chosen) : Chosen(chosen);

    /// <summary>The values the load-time seed re-derivation tries, in order. A stamped board tries
    /// only its stamp first; an unstamped one could come from either era (0.19.2/0.19.3 boards were
    /// vanilla-only, earlier ones allowed mod items), so both are tried and neither demotes a healthy
    /// board to the "foreign bundle data" path.</summary>
    public static bool[] ManifestTryOrder(bool? boardStamp, bool? chosen)
    {
        bool first = OnBoard(boardStamp, chosen);
        return new[] { first, !first };
    }
}
