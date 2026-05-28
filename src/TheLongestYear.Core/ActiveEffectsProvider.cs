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
    private static bool _liabilitySuppressed;

    /// <summary>Id of the active bonus this week, or null if no selection has been made.</summary>
    public static string? BonusId => _bonusId;

    /// <summary>Id of the active liability this week, or null if no selection has been made.</summary>
    public static string? LiabilityId => _liabilityId;

    /// <summary>True when the weekly theme quest has been completed and the liability is lifted
    /// for the rest of the week. The bonus stays active either way.</summary>
    public static bool LiabilitySuppressed => _liabilitySuppressed;

    /// <summary>Register the active effects for the current week. Always clears the liability
    /// suppression flag — a fresh theme select must always start with the liability active
    /// (the player has to complete THIS week's quest to lift it).</summary>
    public static void Set(string? bonusId, string? liabilityId)
    {
        _bonusId = bonusId;
        _liabilityId = liabilityId;
        _liabilitySuppressed = false;
    }

    /// <summary>Clear effects (no selection active — start of a new run or before first pick).</summary>
    public static void Clear()
    {
        _bonusId = null;
        _liabilityId = null;
        _liabilitySuppressed = false;
    }

    /// <summary>Lift the active liability for the remaining days of the week. Called by
    /// <c>WeeklyThemeQuestService</c> on quest completion. Idempotent.</summary>
    public static void SuppressLiability()
    {
        _liabilitySuppressed = true;
    }

    /// <summary>Returns true when the active bonus matches <paramref name="id"/>. The bonus is
    /// never suppressed — it stays active for the whole week regardless of quest state.</summary>
    public static bool ActiveBonus(string id) => _bonusId != null && _bonusId == id;

    /// <summary>Returns true when the active liability matches <paramref name="id"/> AND the
    /// quest hasn't been completed yet. Once <see cref="SuppressLiability"/> is called, all
    /// liability checks short-circuit to false for the rest of the week.</summary>
    public static bool ActiveLiability(string id)
        => !_liabilitySuppressed && _liabilityId != null && _liabilityId == id;
}
