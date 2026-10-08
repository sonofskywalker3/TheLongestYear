using TheLongestYear.Core.Sabotage;

namespace TheLongestYear.Core;

/// <summary>Which of the mod's porch scenes a Farm arrival starts.</summary>
public enum FarmArrivalScene { None, SeasonTurn, Tamper }

/// <summary>When the season-turn porch scene plays (Jeff, 2026-10-08, TODO "Winter save pass" item
/// 16: "It's weird it happens and then I get out of bed"). Not on waking: the Continue morning runs
/// the normal day start (the planning hub opens in the farmhouse as on any week-start morning) and
/// the scene is owed. It plays the first time the farmer then arrives on the Farm by any route, staged
/// at the porch and returning him to where he arrived, the same trigger and staging as the Junimos'
/// tamper scene (<see cref="TamperPorchRule"/>). It stays owed until it has started, across a save
/// too, so a day spent without stepping on the Farm still owes it.</summary>
public static class SeasonTurnArrival
{
    /// <summary>The stored value for an owed scene. A newer gate's scene replaces an older one never
    /// seen: a Summer scene in Fall would tell the wrong turn.</summary>
    public static string Owe(SeasonTurnKind kind) => SeasonTurn.SeenName(kind);

    /// <summary>The scene a stored value owes, or none (empty, or a value no build knows).</summary>
    public static SeasonTurnKind? Owed(string? stored)
        => !string.IsNullOrEmpty(stored) && SeasonTurn.TryParse(stored, out SeasonTurnKind kind) ? kind : null;

    /// <summary>The scene this arrival starts. The season turn goes first; a tamper scene owed on the
    /// same arrival waits for the next one (the turn's own return to the arrival tile counts). A
    /// season turn also waits while the Year One Ending is armed, which owns the next arrival. Every
    /// other condition is the tamper scene's: the local player, on the Farm, nothing else up, a porch
    /// known.</summary>
    public static FarmArrivalScene Pick(
        SeasonTurnKind? turnOwed, bool tamperOwed, bool endingArmed,
        string? enteredLocationName, bool isLocalPlayer, bool busy, (int X, int Y)? porch)
    {
        bool turnWaiting = turnOwed != null && !endingArmed;
        if (turnWaiting && TamperPorchRule.ShouldStart(true, enteredLocationName, isLocalPlayer, busy, porch))
            return FarmArrivalScene.SeasonTurn;
        if (TamperPorchRule.ShouldStart(tamperOwed, enteredLocationName, isLocalPlayer, busy, porch))
            return FarmArrivalScene.Tamper;
        return FarmArrivalScene.None;
    }
}
