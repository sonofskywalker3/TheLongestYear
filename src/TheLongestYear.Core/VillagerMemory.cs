using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Year One Ending (spec 2026-09-06 §6): what the save actually remembers about one villager
/// across every loop, beside the familiarity score. Lists keep JSON round-tripping simple; the rollup
/// enforces uniqueness on the loop lists.</summary>
public sealed class VillagerMemory
{
    public int Talks { get; set; }
    public int Gifts { get; set; }
    public int BirthdayGifts { get; set; }
    public int HeartEvents { get; set; }
    /// <summary>Loop numbers (CompletedResets + 1) in which the player dealt with this villager.</summary>
    public List<int> Loops { get; set; } = new();
    public List<int> GiftLoops { get; set; } = new();
    public List<int> BirthdayGiftLoops { get; set; } = new();
    /// <summary>Heart event id to the loops it was seen in.</summary>
    public Dictionary<string, List<int>> HeartEventLoops { get; set; } = new();

    internal static void AddLoop(List<int> loops, int loop)
    {
        if (!loops.Contains(loop)) loops.Add(loop);
    }
}
