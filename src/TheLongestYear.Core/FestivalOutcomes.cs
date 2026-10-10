using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Classifies raw festival results (governor level, grange score, eggs, fish, gift taste) into
/// <see cref="FestivalOutcome"/> names, and outcomes into the memory a villager can speak. Pure.</summary>
public static class FestivalOutcomes
{
    /// <summary>Vanilla's single-player egg hunt win (Event.eggHuntWinner, numberOfEggsToWin).</summary>
    public const int EggsToWin = 9;

    /// <summary>Vanilla's ice fishing win (Event.iceFishingWinner, numberOfFishToWin).</summary>
    public const int FishToWin = 5;

    /// <summary>Grange bands from Event.interpretGrangeResults.</summary>
    public const int GrangeFirstScore = 90;
    public const int GrangeSecondScore = 75;
    public const int GrangeThirdScore = 60;

    /// <summary>The score vanilla's judgeGrange gives a display holding Lewis's shorts.</summary>
    public const int GrangeShortsScore = -666;

    /// <summary>NPC.gift_taste_* values (NPC.cs 72 to 82).</summary>
    public const int TasteLove = 0;
    public const int TasteLike = 2;
    public const int TasteDislike = 4;
    public const int TasteHate = 6;
    public const int TasteStardropTea = 7;

    private static readonly string[] LuauByLevel =
    {
        FestivalOutcome.LuauToxic, FestivalOutcome.LuauBad, FestivalOutcome.LuauOkay, FestivalOutcome.LuauGood,
        FestivalOutcome.LuauBest, FestivalOutcome.LuauEmpty, FestivalOutcome.LuauShorts,
    };

    /// <summary>Event.governorTaste's like level, 0 to 6 (switchEvent governorReaction&lt;N&gt;).</summary>
    public static string ClassifyLuau(int governorLevel)
        => governorLevel >= 0 && governorLevel < LuauByLevel.Length ? LuauByLevel[governorLevel] : FestivalOutcome.LuauEmpty;

    public static string ClassifyGrange(int score, bool emptyDisplay)
    {
        if (score == GrangeShortsScore) return FestivalOutcome.GrangeShorts;
        if (emptyDisplay) return FestivalOutcome.GrangeNone;
        if (score >= GrangeFirstScore) return FestivalOutcome.GrangeFirst;
        if (score >= GrangeSecondScore) return FestivalOutcome.GrangeSecond;
        if (score >= GrangeThirdScore) return FestivalOutcome.GrangeThird;
        return FestivalOutcome.GrangeLost;
    }

    public static string ClassifyEggHunt(int eggs) => eggs >= EggsToWin ? FestivalOutcome.EggWon : FestivalOutcome.EggLost;

    public static string ClassifyIceFishing(int fish) => fish >= FishToWin ? FestivalOutcome.IceWon : FestivalOutcome.IceLost;

    public static string ClassifyGift(int taste) => taste switch
    {
        TasteLove or TasteStardropTea => FestivalOutcome.GiftLoved,
        TasteLike => FestivalOutcome.GiftLiked,
        TasteDislike => FestivalOutcome.GiftDisliked,
        TasteHate => FestivalOutcome.GiftHated,
        _ => FestivalOutcome.GiftNeutral,
    };

    private static readonly Dictionary<string, string> MemoryByOutcome = new()
    {
        [FestivalOutcome.EggWon] = FestivalMemoryKeys.EggHuntWon,
        [FestivalOutcome.EggLost] = FestivalMemoryKeys.EggHuntLost,
        [FestivalOutcome.LuauToxic] = FestivalMemoryKeys.LuauBad,
        [FestivalOutcome.LuauBad] = FestivalMemoryKeys.LuauBad,
        [FestivalOutcome.LuauGood] = FestivalMemoryKeys.LuauGood,
        [FestivalOutcome.LuauBest] = FestivalMemoryKeys.LuauGood,
        [FestivalOutcome.LuauShorts] = FestivalMemoryKeys.LuauShorts,
        [FestivalOutcome.GrangeFirst] = FestivalMemoryKeys.FairWon,
        [FestivalOutcome.GrangeSecond] = FestivalMemoryKeys.FairLost,
        [FestivalOutcome.GrangeThird] = FestivalMemoryKeys.FairLost,
        [FestivalOutcome.GrangeLost] = FestivalMemoryKeys.FairLost,
        [FestivalOutcome.GrangeShorts] = FestivalMemoryKeys.FairShorts,
        [FestivalOutcome.Pumpkin] = FestivalMemoryKeys.SpiritsPumpkin,
        [FestivalOutcome.IceWon] = FestivalMemoryKeys.IceFishWon,
        [FestivalOutcome.IceLost] = FestivalMemoryKeys.IceFishLost,
    };

    /// <summary>The ordinary (non-bond) memory a festival record can be spoken as, or null when the
    /// outcome is not memorable. The Flower Dance and the Jellies speak from attendance alone; the
    /// Winter Star speaks only through its bonds.</summary>
    public static string? MemoryFor(FestivalMemory? record)
    {
        if (record == null) return null;
        switch (record.Festival)
        {
            case FestivalIds.FlowerDance: return record.Attended ? FestivalMemoryKeys.DanceAttended : null;
            case FestivalIds.Jellies: return record.Attended ? FestivalMemoryKeys.JelliesAttended : null;
            case FestivalIds.WinterStar: return null;
        }
        return record.HasOutcome && MemoryByOutcome.TryGetValue(record.Outcome, out string? memory) ? memory : null;
    }

    /// <summary>The memory a past secret friend speaks at the Winter Star, from the latest gift.</summary>
    public static string WinterStarSeenMemory(string giftOutcome) => giftOutcome switch
    {
        FestivalOutcome.GiftLoved or FestivalOutcome.GiftLiked => FestivalMemoryKeys.WinterStarSeenLiked,
        FestivalOutcome.GiftDisliked or FestivalOutcome.GiftHated => FestivalMemoryKeys.WinterStarSeenDisliked,
        _ => FestivalMemoryKeys.WinterStarSeen,
    };
}
