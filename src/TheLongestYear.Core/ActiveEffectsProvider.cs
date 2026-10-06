using System;
using System.Collections.Generic;

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
    private static string? _bonusId2;
    private static string? _liabilityId2;
    private static bool _liabilitySuppressed2;

    /// <summary>Second entry, used only on a double theme week.</summary>
    public static string? SecondBonusId => _bonusId2;
    public static string? SecondLiabilityId => _liabilityId2;
    public static bool SecondLiabilitySuppressed => _liabilitySuppressed2;

    public static void SetSecond(string? bonusId, string? liabilityId)
    {
        _bonusId2 = bonusId;
        _liabilityId2 = liabilityId;
        _liabilitySuppressed2 = false;
    }

    public static void SuppressSecondLiability() => _liabilitySuppressed2 = true;

    private static void ClearSecond()
    {
        _bonusId2 = null;
        _liabilityId2 = null;
        _liabilitySuppressed2 = false;
    }

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
        ClearSecond();
    }

    /// <summary>Clear effects (no selection active — start of a new run or before first pick).</summary>
    public static void Clear()
    {
        _bonusId = null;
        _liabilityId = null;
        _liabilitySuppressed = false;
        ClearSecond();
    }

    /// <summary>Lift the active liability for the remaining days of the week. Called by
    /// <c>WeeklyThemeQuestService</c> on quest completion. Idempotent.</summary>
    public static void SuppressLiability()
    {
        _liabilitySuppressed = true;
    }

    /// <summary>Returns true when the active bonus matches <paramref name="id"/>. The bonus is
    /// never suppressed — it stays active for the whole week regardless of quest state. Always
    /// false while TLY is dormant (see <see cref="RunActivation"/>), so theme-effect patches
    /// no-op on a non-TLY save even if stale selection state lingers from a prior save.</summary>
    private static Func<IEnumerable<string>>? _boosts;

    /// <summary>Boost source: the modifier ids of every active boost, one per entry
    /// (spec 2026-08-29 shrine tabs + JP Boosts, section 1.4).</summary>
    public static void AttachBoosts(Func<IEnumerable<string>> activeModifierIds) => _boosts = activeModifierIds;

    public static void DetachBoosts() => _boosts = null;

    /// <summary>Independent rolls a patch should make for <paramref name="id"/>: 1 for the week's
    /// theme bonus plus 1 per active boost bound to it. 0 when the run is inactive. Ruling 3: two
    /// independent rolls, no cap.</summary>
    public static int BonusStacks(string id)
    {
        if (!RunActivation.IsActive) return 0;
        int n = _bonusId != null && _bonusId == id ? 1 : 0;
        if (_bonusId2 != null && _bonusId2 == id) n++;
        if (_boosts != null)
        {
            foreach (string m in _boosts())
                if (m == id) n++;
        }
        return n;
    }

    public static bool ActiveBonus(string id) => BonusStacks(id) > 0;

    /// <summary>Returns true when the active liability matches <paramref name="id"/> AND the
    /// quest hasn't been completed yet. Once <see cref="SuppressLiability"/> is called, all
    /// liability checks short-circuit to false for the rest of the week. Always false while
    /// TLY is dormant.</summary>
    public static bool ActiveLiability(string id)
        => RunActivation.IsActive
           && ((!_liabilitySuppressed && _liabilityId != null && _liabilityId == id)
               || (!_liabilitySuppressed2 && _liabilityId2 != null && _liabilityId2 == id));
}
