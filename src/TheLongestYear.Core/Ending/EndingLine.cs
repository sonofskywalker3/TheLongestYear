using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Ending;

public enum EndingLineTier { BirthdayGift = 1, HeartEvent = 2, Gifts = 3, Talks = 4 }

/// <summary>Year One Ending (spec 2026-09-06 section 6): the speaker's middle sentence is the strongest fact
/// that is true of the save. Keys are i18n keys; the injector reads them through Strings.Get.</summary>
public static class EndingLine
{
    public const string OpenKey = "event.ending.crack.open";
    public const string CloseKey = "event.ending.crack.close";
    private const string MiddlePrefix = "event.ending.crack.tier";
    private const int LoopsNeeded = 2;

    /// <summary>Heart event id to the i18n key of its scene phrase ("the day we [scene]"). Ids and
    /// friendship points come from a live Data/Events dump (game install, 2026-09-06): each entry is
    /// the lowest-hearts event for that villager whose script is a clear, memorable scene. An id
    /// missing here falls through to the gifts tier.</summary>
    public static readonly IReadOnlyDictionary<string, string> SceneTable = new Dictionary<string, string>
    {
        ["1"]      = "event.ending.scene.abigail-2",   // Abigail, 500 pts (2 hearts), SeedShop: playing Journey of the Prairie King on her TV
        ["20"]     = "event.ending.scene.alex-2",      // Alex, 500 pts (2 hearts), Beach: throwing the football with the farmer
        ["39"]     = "event.ending.scene.elliott-2",   // Elliott, 500 pts (2 hearts), ElliottHouse: showing off his cabin and writing desk
        ["471942"] = "event.ending.scene.emily-2",     // Emily, 500 pts (2 hearts), HaleyHouse: the farmer wanders into her dream
        ["11"]     = "event.ending.scene.haley-2",     // Haley, 500 pts (2 hearts), HaleyHouse: Haley and Emily arguing over cleaning
        ["56"]     = "event.ending.scene.harvey-2",    // Harvey, 500 pts (2 hearts), JoshHouse: examining George at a check-up
        ["50"]     = "event.ending.scene.leah-2",      // Leah, 500 pts (2 hearts), LeahHouse: showing the farmer her sculpture
        ["6"]      = "event.ending.scene.maru-2",      // Maru, 500 pts (2 hearts), ScienceHouse: testing soil samples with Demetrius
        ["34"]     = "event.ending.scene.penny-2",     // Penny, 500 pts (2 hearts), Town: helping George reach a letter
        ["44"]     = "event.ending.scene.sam-2",       // Sam, 500 pts (2 hearts), SamHouse: a jam session with Sebastian
        ["384883"] = "event.ending.scene.sebastian-4", // Sebastian, 1000 pts (4 hearts), Mountain: showing off his motorcycle
        ["611944"] = "event.ending.scene.shane-2",     // Shane, 500 pts (2 hearts), Forest: a late night drink behind the ranch
    };

    /// <summary>Villagers whose voice would not survive the generic line get their own key set
    /// (suffix appended to the tier key).</summary>
    public static readonly IReadOnlySet<string> VoiceOverrides =
        new HashSet<string> { "Shane", "George", "Haley", "Abigail", "Wizard" };

    public static EndingLineTier Tier(VillagerMemory? mem, out string? sceneEventId)
    {
        sceneEventId = null;
        if (mem == null) return EndingLineTier.Talks;
        if (mem.BirthdayGiftLoops.Count >= LoopsNeeded) return EndingLineTier.BirthdayGift;
        string? scene = mem.HeartEventLoops
            .Where(kv => kv.Value.Count >= LoopsNeeded && SceneTable.ContainsKey(kv.Key))
            .Select(kv => kv.Key)
            .OrderBy(id => id, System.StringComparer.Ordinal)
            .FirstOrDefault();
        if (scene != null) { sceneEventId = scene; return EndingLineTier.HeartEvent; }
        if (mem.GiftLoops.Count >= LoopsNeeded) return EndingLineTier.Gifts;
        return EndingLineTier.Talks;
    }

    public static string MiddleKey(string npc, EndingLineTier tier)
        => VoiceOverrides.Contains(npc) ? $"{MiddlePrefix}{(int)tier}.{npc}" : $"{MiddlePrefix}{(int)tier}";

    public static string? SceneKey(string eventId)
        => SceneTable.TryGetValue(eventId, out string? key) ? key : null;
}
