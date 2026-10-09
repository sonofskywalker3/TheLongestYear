using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One villager's interaction signals for a single day, read from the live friendship data.</summary>
public sealed record VillagerDaySignals(string Npc, bool Talked, int Gifts, int HeartEvents);

/// <summary>Nightly rollup of interaction into <see cref="MetaState.VillagerFamiliarity"/> (deja-vu
/// dialogue spec 2026-08-27). Pure; the glue gathers the signals from Game1.</summary>
public static class FamiliarityRollup
{
    public const int TalkPoints = 1;
    public const int GiftPoints = 3;
    public const int HeartEventPoints = 10;

    /// <summary>Festival interactions the nightly rollup cannot see (deja-vu phase 2, ruling 3): dancing
    /// with a villager counts like a heart event, the Winter Star gift like a gift (vanilla gives it
    /// with updateGiftLimitInfo off, so GiftsToday never counts it).</summary>
    public const int DancePartnerPoints = HeartEventPoints;
    public const int WinterStarGiftPoints = GiftPoints;

    /// <summary>Add festival points straight to meta. Returns the villager's new total.</summary>
    public static int AddBonus(MetaState meta, string npc, int points)
    {
        if (meta == null || string.IsNullOrEmpty(npc) || points <= 0) return 0;
        meta.VillagerFamiliarity.TryGetValue(npc, out int current);
        return meta.VillagerFamiliarity[npc] = current + points;
    }

    /// <summary>Adds each villager's points for the day. Returns the total added. A villager with
    /// zero points gets no entry, so the dictionary only lists people the player has dealt with.</summary>
    public static int Apply(MetaState meta, IEnumerable<VillagerDaySignals> signals)
    {
        int total = 0;
        foreach (VillagerDaySignals s in signals)
        {
            int points = (s.Talked ? TalkPoints : 0) + s.Gifts * GiftPoints + s.HeartEvents * HeartEventPoints;
            if (points <= 0) continue;
            meta.VillagerFamiliarity.TryGetValue(s.Npc, out int current);
            meta.VillagerFamiliarity[s.Npc] = current + points;
            total += points;
        }
        return total;
    }
}
