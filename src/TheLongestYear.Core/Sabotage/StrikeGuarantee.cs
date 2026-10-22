using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Every kind of strike lands at least once every loop (spec 2026-09-21). A kind has a
/// debut season; if it has not struck by <see cref="ForceFromDay"/> of that season it is forced on
/// the first night it can act. Never overrides "cannot act" (warded, nothing stored, nothing
/// fair, capped): the caller's canAct decides that. Tampering has its own Winter rule in NightRoll.</summary>
public static class StrikeGuarantee
{
    /// <summary>The first half of week 3: two weeks left to pivot (Jeff, 2026-09-21).</summary>
    public const int ForceFromDay = 15;

    private static Season? DebutSeason(DarknessEvent e) => e switch
    {
        DarknessEvent.CropBlight => Season.Summer,
        DarknessEvent.ChestBlight => Season.Summer,
        DarknessEvent.Reversion => Season.Fall,
        _ => null,
    };

    /// <summary>The kinds still owed tonight, in NightRoll's option order.</summary>
    public static IReadOnlyList<DarknessEvent> Owed(Season season, int dayOfMonth, IReadOnlyCollection<string> struck)
    {
        if (struck is null) throw new ArgumentNullException(nameof(struck));
        if (dayOfMonth < ForceFromDay) return Array.Empty<DarknessEvent>();
        return NightRoll.Options(season)
            .Where(e => DebutSeason(e) == season && !struck.Contains(e.ToString()))
            .ToArray();
    }

    /// <summary>The owed kind to force tonight, or null when none is owed or none can act.</summary>
    public static DarknessEvent? ForcedTonight(Season season, int dayOfMonth, IReadOnlyCollection<string> struck, Func<DarknessEvent, bool> canAct)
    {
        if (canAct is null) throw new ArgumentNullException(nameof(canAct));
        foreach (DarknessEvent e in Owed(season, dayOfMonth, struck))
            if (canAct(e)) return e;
        return null;
    }
}
