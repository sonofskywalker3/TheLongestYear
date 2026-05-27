namespace TheLongestYear.Core;

/// <summary>
/// Thread-local static accessor that exposes the current week's active bonus and liability
/// modifier ids to Harmony patches without requiring the patch layer to import RunState
/// or MetaStore directly. Set by RunController on selection; cleared on run start and load.
/// Null values mean "no active effect" (safe default — patches skip when null).
/// </summary>
public static class ActiveEffectsProvider
{
    private static string? _bonusId;
    private static string? _liabilityId;

    /// <summary>Id of the active bonus this week, or null if no selection has been made.</summary>
    public static string? BonusId => _bonusId;

    /// <summary>Id of the active liability this week, or null if no selection has been made.</summary>
    public static string? LiabilityId => _liabilityId;

    /// <summary>Register the active effects for the current week.</summary>
    public static void Set(string? bonusId, string? liabilityId)
    {
        _bonusId = bonusId;
        _liabilityId = liabilityId;
    }

    /// <summary>Clear effects (no selection active — start of a new run or before first pick).</summary>
    public static void Clear()
    {
        _bonusId = null;
        _liabilityId = null;
    }

    /// <summary>Returns true when the active bonus matches <paramref name="id"/>.</summary>
    public static bool ActiveBonus(string id) => _bonusId != null && _bonusId == id;

    /// <summary>Returns true when the active liability matches <paramref name="id"/>.</summary>
    public static bool ActiveLiability(string id) => _liabilityId != null && _liabilityId == id;
}
