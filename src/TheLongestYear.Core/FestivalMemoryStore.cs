using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Writes this loop's festival log and merges it into meta at the rewind (deja-vu phase 2,
/// spec "Commit at the rewind"). Pure. The log lives in RunState, so a memory is always about an
/// earlier loop: meta is not touched until the rewind.</summary>
public static class FestivalMemoryStore
{
    private static FestivalMemory Entry(RunState run, string festival)
    {
        run.FestivalLog ??= new();
        if (!run.FestivalLog.TryGetValue(festival, out FestivalMemory m))
            run.FestivalLog[festival] = m = new FestivalMemory { Festival = festival };
        return m;
    }

    /// <summary>The player entered the festival this loop. Idempotent.</summary>
    public static void RecordAttendance(RunState run, string festival)
    {
        if (run == null || string.IsNullOrEmpty(festival)) return;
        Entry(run, festival).AttendedRun = run.RunNumber;
    }

    /// <summary>The festival's result this loop. Replaces any earlier result the same loop.</summary>
    public static void RecordOutcome(RunState run, string festival, string outcome, string itemId = "",
        int quality = 0, string npc = "", int score = 0)
    {
        if (run == null || string.IsNullOrEmpty(festival)) return;
        FestivalMemory m = Entry(run, festival);
        m.AttendedRun = run.RunNumber;
        m.OutcomeRun = run.RunNumber;
        m.Outcome = outcome ?? "";
        m.ItemId = itemId ?? "";
        m.ItemQuality = quality;
        m.Npc = npc ?? "";
        m.Score = score;
    }

    /// <summary>A memory of this festival was heard this loop. Returns false when it already was.</summary>
    public static bool MarkHeard(RunState run, string festival)
    {
        if (run == null || string.IsNullOrEmpty(festival)) return false;
        run.FestivalMemoryHeard ??= new();
        if (run.FestivalMemoryHeard.Contains(festival)) return false;
        run.FestivalMemoryHeard.Add(festival);
        return true;
    }

    /// <summary>One memory per festival per loop: true once one has been heard.</summary>
    public static bool IsSpent(RunState run, string festival)
        => run?.FestivalMemoryHeard != null && run.FestivalMemoryHeard.Contains(festival);

    /// <summary>The egg hunt guarantee: the latest Egg Festival record is a win nobody has remembered
    /// yet. A memory heard in loop N is about an outcome from before N, so a win recorded in the same
    /// loop it was heard (HeardRun == OutcomeRun) is still unremembered.</summary>
    public static bool EggGuaranteeArmed(MetaState meta)
    {
        if (meta?.FestivalMemories == null) return false;
        if (!meta.FestivalMemories.TryGetValue(FestivalIds.EggFestival, out FestivalMemory m)) return false;
        return m.Outcome == FestivalOutcome.EggWon && m.OutcomeRun >= 0 && m.HeardRun <= m.OutcomeRun;
    }

    /// <summary>Merge this loop's log into meta. Runs once per rewind, before RunState.BeginNewRun.</summary>
    public static void Commit(MetaState meta, RunState run)
    {
        if (meta == null || run == null) return;
        meta.FestivalMemories ??= new();
        meta.DancePartners ??= new();
        meta.WinterStarRecipients ??= new();

        // 1. Heard stamps first, so an outcome merged below can re-arm the egg guarantee.
        foreach (string festival in run.FestivalMemoryHeard ?? new List<string>())
        {
            if (!FestivalIds.IsTracked(festival)) continue;
            Meta(meta, festival).HeardRun = run.RunNumber;
        }

        foreach (KeyValuePair<string, FestivalMemory> kv in run.FestivalLog ?? new Dictionary<string, FestivalMemory>())
        {
            if (!FestivalIds.IsTracked(kv.Key)) continue;
            FestivalMemory log = kv.Value;
            FestivalMemory target = null;
            // 2. Attendance.
            if (log.Attended)
                (target = Meta(meta, kv.Key)).AttendedRun = log.AttendedRun;
            // 3. Outcome: every outcome field together, so a silent result replaces a speakable one.
            if (log.HasOutcome)
            {
                target ??= Meta(meta, kv.Key);
                target.Outcome = log.Outcome;
                target.OutcomeRun = log.OutcomeRun;
                target.ItemId = log.ItemId ?? "";
                target.ItemQuality = log.ItemQuality;
                target.Npc = log.Npc ?? "";
                target.Score = log.Score;
                // 4. Bonds: appended, one per loop, never merged.
                if (kv.Key == FestivalIds.FlowerDance && log.Outcome == FestivalOutcome.Danced && !string.IsNullOrEmpty(log.Npc))
                    meta.DancePartners.Add(Bond(log, run.RunNumber));
                else if (kv.Key == FestivalIds.WinterStar && FestivalOutcome.IsGift(log.Outcome) && !string.IsNullOrEmpty(log.Npc))
                    meta.WinterStarRecipients.Add(Bond(log, run.RunNumber));
            }
        }
    }

    private static FestivalMemory Meta(MetaState meta, string festival)
    {
        if (!meta.FestivalMemories.TryGetValue(festival, out FestivalMemory m))
            meta.FestivalMemories[festival] = m = new FestivalMemory { Festival = festival };
        return m;
    }

    private static BondMemory Bond(FestivalMemory log, int runNumber) => new()
    {
        Npc = log.Npc,
        Run = runNumber,
        Outcome = log.Outcome,
        ItemId = log.ItemId ?? "",
        ItemQuality = log.ItemQuality,
    };
}

/// <summary>Queries over every loop's dance partners and secret friends (never fade, ruling 3).</summary>
public static class FestivalBonds
{
    public static bool IsPastPartner(MetaState meta, string npc)
        => meta?.DancePartners != null && meta.DancePartners.Exists(b => string.Equals(b.Npc, npc, StringComparison.Ordinal));

    public static bool IsPastRecipient(MetaState meta, string npc)
        => meta?.WinterStarRecipients != null && meta.WinterStarRecipients.Exists(b => string.Equals(b.Npc, npc, StringComparison.Ordinal));

    /// <summary>That villager's most recent secret gift from the player, or null.</summary>
    public static BondMemory LatestGift(MetaState meta, string npc)
    {
        if (meta?.WinterStarRecipients == null) return null;
        for (int i = meta.WinterStarRecipients.Count - 1; i >= 0; i--)
            if (string.Equals(meta.WinterStarRecipients[i].Npc, npc, StringComparison.Ordinal))
                return meta.WinterStarRecipients[i];
        return null;
    }
}
