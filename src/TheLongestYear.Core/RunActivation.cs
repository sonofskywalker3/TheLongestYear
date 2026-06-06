namespace TheLongestYear.Core;

/// <summary>
/// Master runtime gate: true only while The Longest Year is active on the currently-loaded
/// save. Set true by <c>ModEntry.OnSaveLoaded</c> after it confirms the save was started as a
/// TLY run; set false when a non-TLY save loads and when the player returns to the title.
///
/// Every gameplay Harmony patch, HUD draw, and per-tick handler consults this, so loading a
/// normal vanilla save with the mod installed does nothing at all — no effects, no HUD, no
/// reset loop. Defaults to false so nothing acts before a save has been confirmed.
/// </summary>
public static class RunActivation
{
    /// <summary>True while TLY is active on the loaded save.</summary>
    public static bool IsActive { get; private set; }

    /// <summary>Mark TLY active for the loaded TLY save.</summary>
    public static void Activate() => IsActive = true;

    /// <summary>Mark TLY dormant — a non-TLY save loaded, or the player returned to title.</summary>
    public static void Deactivate() => IsActive = false;
}
