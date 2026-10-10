using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>A villager present at a festival, as the rules see him or her. The glue fills it from the
/// event's actors.</summary>
public sealed record FestivalActor(string Name, bool IsHost = false, bool IsSpouse = false, bool IsChild = false,
    bool CanSocialize = true, bool CanDance = false, bool IsSecretFriend = false);

/// <summary>A festival-day memory one villager will carry: push it, and it plays on the next talk.</summary>
public sealed record PlannedMemory(string Npc, string Memory, bool IsBond, bool Guaranteed, string ItemId);

/// <summary>Who remembers what at a festival (deja-vu phase 2, spec 2026-10-09, both rulings sections).
/// Pure: the glue supplies actors and rolls. No friendship gate; one memory per festival per loop; 20%
/// per villager, 50% for a past dance partner or secret friend, 100% for the egg hunt guarantee.</summary>
public static class FestivalMemoryRules
{
    public const int RollRange = 100;
    public const int Certain = 100;

    /// <summary>Salts for <see cref="StableRoll"/>, one per moment, so the same villager's rolls at
    /// different moments of the same festival are independent.</summary>
    public const string SaltFestivalDay = "day";
    public const string SaltAfter = "after";
    public const string SaltBond = "bond";
    public const string SaltLine = "line";

    /// <summary>Phase 2 rides on phase 1: both switches must be on.</summary>
    public static bool Active(GameplayConfig config)
        => config != null && config.EnableDejaVuDialogue && config.EnableDejaVuFestivalMemories;

    /// <summary>The festival-day memories to push as the festival opens. <paramref name="roll"/> gives
    /// each villager a value in [0,100); a hit is roll &lt; chance. <paramref name="force"/> (debug)
    /// skips the chance and the budget, never the speaker guards.</summary>
    public static IEnumerable<PlannedMemory> PlanFestivalDay(MetaState meta, RunState run, string festival,
        IEnumerable<FestivalActor> actors, GameplayConfig config, Func<string, int> roll, bool force)
    {
        if (!Active(config) || meta == null || run == null || actors == null) yield break;
        if (!force && FestivalMemoryStore.IsSpent(run, festival)) yield break;

        FestivalMemory? record = null;
        meta.FestivalMemories?.TryGetValue(festival, out record);
        string? ordinary = FestivalOutcomes.MemoryFor(record);
        bool guaranteed = festival == FestivalIds.EggFestival && ordinary == FestivalMemoryKeys.EggHuntWon
            && FestivalMemoryStore.EggGuaranteeArmed(meta);

        foreach (FestivalActor actor in actors)
        {
            if (actor == null || !CanSpeak(actor)) continue;
            if (festival == FestivalIds.WinterStar)
            {
                BondMemory? gift = FestivalBonds.LatestGift(meta, actor.Name);
                if (gift == null) continue;
                if (force || roll(actor.Name) < config.DejaVuFestivalBondChancePercent)
                    yield return new PlannedMemory(actor.Name, FestivalOutcomes.WinterStarSeenMemory(gift.Outcome),
                        IsBond: true, Guaranteed: false, gift.ItemId ?? "");
                continue;
            }
            // The Flower Dance's dancers get their memories through the dance hooks (dance.again,
            // dance.other): vanilla redraws their stack while the player has no partner.
            if (festival == FestivalIds.FlowerDance && actor.CanDance) continue;
            if (ordinary == null) continue;
            int chance = guaranteed ? Certain : config.DejaVuFestivalChancePercent;
            if (force || roll(actor.Name) < chance)
                yield return new PlannedMemory(actor.Name, ordinary, IsBond: false, guaranteed, record?.ItemId ?? "");
        }
    }

    private static bool CanSpeak(FestivalActor a)
        => a.CanSocialize && !a.IsHost && !a.IsSpouse && !a.IsChild && !a.IsSecretFriend;

    /// <summary>The post-result memory, or null: the result just now repeats the remembered one (won
    /// again, bad soup again). The speaker is whoever owns the line (<see cref="FestivalMemoryLines.AfterSpeakers"/>).</summary>
    public static string? PlanAfter(MetaState meta, RunState run, string festival, string outcomeNow,
        GameplayConfig config, int roll, bool force)
    {
        if (!Active(config) || meta == null || run == null) return null;
        if (!force && FestivalMemoryStore.IsSpent(run, festival)) return null;
        FestivalMemory? record = null;
        meta.FestivalMemories?.TryGetValue(festival, out record);
        string? past = FestivalOutcomes.MemoryFor(record);
        if (past == null) return null;
        string? now = FestivalOutcomes.MemoryFor(new FestivalMemory { Festival = festival, Outcome = outcomeNow ?? "" });
        if (now != past) return null;
        bool guaranteed = past == FestivalMemoryKeys.EggHuntWon && FestivalMemoryStore.EggGuaranteeArmed(meta);
        int chance = guaranteed ? Certain : config.DejaVuFestivalChancePercent;
        return force || roll < chance ? past : null;
    }

    /// <summary>A bond memory roll: a past dance partner (Flower Dance) or past secret friend (Winter
    /// Star) at 50%. Used for dance.again, dance.other and winterstar.again.</summary>
    public static bool BondHit(MetaState meta, RunState run, string festival, string npc, GameplayConfig config,
        int roll, bool force)
    {
        if (!Active(config) || meta == null || run == null || string.IsNullOrEmpty(npc)) return false;
        bool bonded = festival switch
        {
            FestivalIds.FlowerDance => FestivalBonds.IsPastPartner(meta, npc),
            FestivalIds.WinterStar => FestivalBonds.IsPastRecipient(meta, npc),
            _ => false,
        };
        if (!bonded) return false;
        if (force) return true;
        if (FestivalMemoryStore.IsSpent(run, festival)) return false;
        return roll < config.DejaVuFestivalBondChancePercent;
    }

    private const uint FnvOffset = 2166136261;
    private const uint FnvPrime = 16777619;

    /// <summary>A roll in [0,100) fixed for (run seed, run number, festival, salt, villager), so walking
    /// out and back in gives the same villagers the same lines. FNV-1a: string.GetHashCode is randomised
    /// per process and would reroll on every launch.</summary>
    public static int StableRoll(RunState run, string festival, string salt, string npc)
    {
        uint h = FnvOffset;
        string text = $"{run?.Seed}|{run?.RunNumber}|{festival}|{salt}|{npc}";
        foreach (char c in text)
        {
            h ^= c;
            h *= FnvPrime;
        }
        // Final avalanche so neighbouring names do not land on neighbouring values.
        h ^= h >> 15;
        h *= 0x2c1b3c6dU;
        h ^= h >> 12;
        return (int)(h % RollRange);
    }
}
