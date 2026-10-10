using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>What the player did at one festival in one loop (deja-vu phase 2, spec 2026-10-09). The run
/// log holds this loop's; meta holds the latest from earlier loops, merged at the rewind.</summary>
public sealed class FestivalMemory
{
    public const int Never = -1;

    /// <summary>Festival id, one of <see cref="FestivalIds"/>.</summary>
    public string Festival { get; set; } = "";

    /// <summary>RunNumber of the loop the player last entered it; -1 = never.</summary>
    public int AttendedRun { get; set; } = Never;

    /// <summary>RunNumber the outcome fields came from; -1 = no outcome yet.</summary>
    public int OutcomeRun { get; set; } = Never;

    /// <summary>One of <see cref="FestivalOutcome"/>; "" = none.</summary>
    public string Outcome { get; set; } = "";

    /// <summary>Qualified item id (Luau ingredient, Winter Star gift); "" = none.</summary>
    public string ItemId { get; set; } = "";

    public int ItemQuality { get; set; }

    /// <summary>Villager internal name (dance partner, secret friend); "" = none.</summary>
    public string Npc { get; set; } = "";

    /// <summary>Eggs, fish or grange points; 0 = none.</summary>
    public int Score { get; set; }

    /// <summary>RunNumber of the last loop a memory of this festival was heard; -1 = never.</summary>
    public int HeardRun { get; set; } = Never;

    public bool HasOutcome => !string.IsNullOrEmpty(Outcome);
    public bool Attended => AttendedRun >= 0;
}

/// <summary>One shared moment with one villager in one loop (a Flower Dance partner, a secret friend).
/// Kept for every loop, never replaced.</summary>
public sealed class BondMemory
{
    public string Npc { get; set; } = "";
    public int Run { get; set; }

    /// <summary><see cref="FestivalOutcome.Danced"/> or a Winter Star gift outcome.</summary>
    public string Outcome { get; set; } = "";
    public string ItemId { get; set; } = "";
    public int ItemQuality { get; set; }
}

/// <summary>The eight festivals TLY remembers, by their Data/Festivals key.</summary>
public static class FestivalIds
{
    public const string EggFestival = "spring13";
    public const string FlowerDance = "spring24";
    public const string Luau = "summer11";
    public const string Jellies = "summer28";
    public const string Fair = "fall16";
    public const string SpiritsEve = "fall27";
    public const string IceFestival = "winter8";
    public const string WinterStar = "winter25";

    /// <summary>Vanilla festival event ids are "festival_&lt;key&gt;" (Event.cs 11487).</summary>
    public const string EventIdPrefix = "festival_";

    public static readonly IReadOnlyList<string> All = new[]
    {
        EggFestival, FlowerDance, Luau, Jellies, Fair, SpiritsEve, IceFestival, WinterStar,
    };

    /// <summary>The festivals whose "before" lines are about a contest, pulled when it starts.</summary>
    public static readonly IReadOnlyCollection<string> WithContest = new HashSet<string>(StringComparer.Ordinal)
    {
        EggFestival, Luau, Fair, IceFestival,
    };

    private static readonly HashSet<string> Tracked = new(All, StringComparer.Ordinal);

    public static bool IsTracked(string id) => id != null && Tracked.Contains(id);

    /// <summary>"festival_spring13" or "spring13" to "spring13"; null for anything empty.</summary>
    public static string? FromEventId(string? eventId)
    {
        if (string.IsNullOrEmpty(eventId)) return null;
        string id = eventId.StartsWith(EventIdPrefix, StringComparison.Ordinal)
            ? eventId.Substring(EventIdPrefix.Length)
            : eventId;
        return id.Length == 0 ? null : id;
    }

    /// <summary>The setUpPlayerControlSequence ids that open each festival's free roam (Event.cs 10947).</summary>
    public static readonly IReadOnlyDictionary<string, string> ByControlSequence = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["eggFestival"] = EggFestival,
        ["flowerFestival"] = FlowerDance,
        ["luau"] = Luau,
        ["jellies"] = Jellies,
        ["fair"] = Fair,
        ["halloween"] = SpiritsEve,
        ["iceFestival"] = IceFestival,
        ["christmas"] = WinterStar,
    };
}

/// <summary>Outcome names stored in <see cref="FestivalMemory.Outcome"/> and <see cref="BondMemory.Outcome"/>.</summary>
public static class FestivalOutcome
{
    public const string EggWon = "EggWon";
    public const string EggLost = "EggLost";
    public const string Danced = "Danced";
    public const string NoPartner = "NoPartner";
    public const string LuauToxic = "LuauToxic";
    public const string LuauBad = "LuauBad";
    public const string LuauOkay = "LuauOkay";
    public const string LuauGood = "LuauGood";
    public const string LuauBest = "LuauBest";
    public const string LuauEmpty = "LuauEmpty";
    public const string LuauShorts = "LuauShorts";
    public const string GrangeFirst = "GrangeFirst";
    public const string GrangeSecond = "GrangeSecond";
    public const string GrangeThird = "GrangeThird";
    public const string GrangeLost = "GrangeLost";
    public const string GrangeShorts = "GrangeShorts";
    public const string GrangeNone = "GrangeNone";
    public const string Pumpkin = "Pumpkin";
    public const string IceWon = "IceWon";
    public const string IceLost = "IceLost";
    public const string GiftLoved = "GiftLoved";
    public const string GiftLiked = "GiftLiked";
    public const string GiftNeutral = "GiftNeutral";
    public const string GiftDisliked = "GiftDisliked";
    public const string GiftHated = "GiftHated";

    public static bool IsGift(string outcome)
        => outcome == GiftLoved || outcome == GiftLiked || outcome == GiftNeutral
        || outcome == GiftDisliked || outcome == GiftHated;
}

/// <summary>Memory names: the middle of every festmem.&lt;memory&gt;.&lt;npc&gt;.&lt;n&gt; key.</summary>
public static class FestivalMemoryKeys
{
    public const string EggHuntWon = "egghunt.won";
    public const string EggHuntLost = "egghunt.lost";
    public const string DanceAttended = "dance.attended";
    public const string DanceAgain = "dance.again";
    public const string DanceOther = "dance.other";
    public const string LuauBad = "luau.bad";
    public const string LuauGood = "luau.good";
    public const string LuauShorts = "luau.shorts";
    public const string JelliesAttended = "jellies.attended";
    public const string FairWon = "fair.won";
    public const string FairLost = "fair.lost";
    public const string FairShorts = "fair.shorts";
    public const string SpiritsPumpkin = "spirits.pumpkin";
    public const string IceFishWon = "icefish.won";
    public const string IceFishLost = "icefish.lost";
    public const string WinterStarAgain = "winterstar.again";
    public const string WinterStarSeen = "winterstar.seen";
    public const string WinterStarSeenLiked = "winterstar.seen.liked";
    public const string WinterStarSeenDisliked = "winterstar.seen.disliked";
}
