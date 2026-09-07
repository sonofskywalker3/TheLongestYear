using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One villager's interaction signals for a single day, read from the live friendship data.
/// <paramref name="BirthdayGift"/>: a gift today on the villager's birthday. <paramref name="HeartEventIds"/>:
/// the relationship event ids first seen today.</summary>
public sealed record VillagerDaySignals(string Npc, bool Talked, int Gifts, int HeartEvents,
    bool BirthdayGift = false, IReadOnlyList<string>? HeartEventIds = null);

/// <summary>Nightly rollup of interaction into <see cref="MetaState.VillagerFamiliarity"/> (deja-vu
/// dialogue spec 2026-08-27) and <see cref="MetaState.VillagerMemory"/> (ending spec 2026-09-06).
/// Pure; the glue gathers the signals from Game1.</summary>
public static class FamiliarityRollup
{
    public const int TalkPoints = 1;
    public const int GiftPoints = 3;
    public const int HeartEventPoints = 10;

    /// <summary>Adds each villager's points and counts for the day. Returns the total points added.
    /// A villager with zero points gets no entry, so the dictionaries only list people the player
    /// has dealt with.</summary>
    public static int Apply(MetaState meta, IEnumerable<VillagerDaySignals> signals, int loopNumber)
    {
        int total = 0;
        foreach (VillagerDaySignals s in signals)
        {
            int points = (s.Talked ? TalkPoints : 0) + s.Gifts * GiftPoints + s.HeartEvents * HeartEventPoints;
            if (points <= 0) continue;
            meta.VillagerFamiliarity.TryGetValue(s.Npc, out int current);
            meta.VillagerFamiliarity[s.Npc] = current + points;
            total += points;

            if (!meta.VillagerMemory.TryGetValue(s.Npc, out VillagerMemory? mem))
                meta.VillagerMemory[s.Npc] = mem = new VillagerMemory();
            if (s.Talked) mem.Talks++;
            mem.Gifts += s.Gifts;
            mem.HeartEvents += s.HeartEvents;
            VillagerMemory.AddLoop(mem.Loops, loopNumber);
            if (s.Gifts > 0) VillagerMemory.AddLoop(mem.GiftLoops, loopNumber);
            if (s.BirthdayGift && s.Gifts > 0)
            {
                mem.BirthdayGifts++;
                VillagerMemory.AddLoop(mem.BirthdayGiftLoops, loopNumber);
            }
            foreach (string id in s.HeartEventIds ?? System.Array.Empty<string>())
            {
                if (!mem.HeartEventLoops.TryGetValue(id, out List<int>? loops))
                    mem.HeartEventLoops[id] = loops = new List<int>();
                VillagerMemory.AddLoop(loops, loopNumber);
            }
        }
        return total;
    }
}
