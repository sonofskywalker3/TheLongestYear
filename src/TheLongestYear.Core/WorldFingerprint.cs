using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// A flat snapshot of baseline-relevant world/player scalars, captured by the mod after a reset.
/// Comparing two post-reset fingerprints (the two-loop leak test) reveals any state that leaks
/// between runs — the classic in-place-reset failure mode (spec §12).
/// </summary>
public sealed class WorldFingerprint
{
    public int Year { get; set; }
    public Season Season { get; set; }
    public int DayOfMonth { get; set; }
    public int Money { get; set; }
    public int Stamina { get; set; }
    public int InventoryItemCount { get; set; }
    public int TotalSkillXp { get; set; }
    public int CropCount { get; set; }

    /// <summary>
    /// Total placed objects across all locations. Captured for logging but NOT used in <see cref="Diff"/>:
    /// vanilla world-gen + mods (FTM/SVE) spawn objects non-deterministically, so it is not a leak signal.
    /// </summary>
    public int PlacedObjectCount { get; set; }

    public int BuildingCount { get; set; }
    public int CompletedBundleCount { get; set; }
    public int FriendshipCount { get; set; }
    public int MailReceivedCount { get; set; }
    public int EventsSeenCount { get; set; }
    public int LowestMineLevel { get; set; }

    /// <summary>Field-by-field differences vs <paramref name="other"/>; empty means a clean match.</summary>
    public IReadOnlyList<string> Diff(WorldFingerprint other)
    {
        var diffs = new List<string>();
        void Cmp(string name, object a, object b)
        {
            if (!Equals(a, b)) diffs.Add($"{name}: {a} -> {b}");
        }

        Cmp(nameof(Year), Year, other.Year);
        Cmp(nameof(Season), Season, other.Season);
        Cmp(nameof(DayOfMonth), DayOfMonth, other.DayOfMonth);
        Cmp(nameof(Money), Money, other.Money);
        Cmp(nameof(Stamina), Stamina, other.Stamina);
        Cmp(nameof(InventoryItemCount), InventoryItemCount, other.InventoryItemCount);
        Cmp(nameof(TotalSkillXp), TotalSkillXp, other.TotalSkillXp);
        Cmp(nameof(CropCount), CropCount, other.CropCount);
        // PlacedObjectCount intentionally excluded — non-deterministic world-gen/mod spawning (observed
        // bouncing +3/-9/-5 between clean resets), so comparing it produces false positives, not leaks.
        Cmp(nameof(BuildingCount), BuildingCount, other.BuildingCount);
        Cmp(nameof(CompletedBundleCount), CompletedBundleCount, other.CompletedBundleCount);
        Cmp(nameof(FriendshipCount), FriendshipCount, other.FriendshipCount);
        Cmp(nameof(MailReceivedCount), MailReceivedCount, other.MailReceivedCount);
        Cmp(nameof(EventsSeenCount), EventsSeenCount, other.EventsSeenCount);
        Cmp(nameof(LowestMineLevel), LowestMineLevel, other.LowestMineLevel);
        return diffs;
    }

    public bool Matches(WorldFingerprint other) => Diff(other).Count == 0;
}
