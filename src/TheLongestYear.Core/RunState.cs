using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Mutable per-run progress for one loop attempt. Persisted as per-save data and committed only with
/// the game's own save (see MetaStore) so it cannot be save-scummed. Lists (not sets) keep JSON
/// round-tripping simple; membership helpers enforce uniqueness.
/// </summary>
public sealed class RunState
{
    /// <summary>Seed used to generate this run's YearPlan; stored so a reload reproduces the plan.</summary>
    public int Seed { get; set; }

    /// <summary>1-based attempt counter (loop number), for logging and the narrative layer.</summary>
    public int RunNumber { get; set; } = 1;

    public Season Season { get; set; } = Season.Spring;

    public int DayOfMonth { get; set; } = 1;

    /// <summary>Cumulative donated item ids this run (consumed-on-donate; quantities/JP land in Plan 04).</summary>
    public List<string> DonatedItemIds { get; set; } = new();

    /// <summary>Themes championed in the current month (cleared each month). The 5th is never championed.</summary>
    public List<Theme> ChampionedThemesThisMonth { get; set; } = new();

    /// <summary>The theme championed this week, whose bonus/liability are active. Null between weeks.</summary>
    public Theme? CurrentChampion { get; set; }

    /// <summary>Bundle indices whose completion JP bonus has already been awarded this run.</summary>
    public List<int> AwardedBundleCompletions { get; set; } = new();

    /// <summary>Room/area numbers whose completion JP bonus has already been awarded this run.</summary>
    public List<int> AwardedRoomCompletions { get; set; } = new();

    public int WeekOfYear => Calendar.WeekOfYear((int)Season, DayOfMonth);

    public int WeekInMonth => Calendar.WeekInMonth(DayOfMonth);

    public bool IsChampioned(Theme theme) => ChampionedThemesThisMonth.Contains(theme);

    /// <summary>Record a bundle-completion award; returns false if it was already awarded this run.</summary>
    public bool TryMarkBundleAwarded(int bundleIndex)
    {
        if (AwardedBundleCompletions.Contains(bundleIndex))
            return false;
        AwardedBundleCompletions.Add(bundleIndex);
        return true;
    }

    /// <summary>Record a room-completion award; returns false if it was already awarded this run.</summary>
    public bool TryMarkRoomAwarded(int area)
    {
        if (AwardedRoomCompletions.Contains(area))
            return false;
        AwardedRoomCompletions.Add(area);
        return true;
    }

    /// <summary>Add a donated item id; idempotent so re-donating the same id is a no-op in the ledger.</summary>
    public void RecordDonation(string itemId)
    {
        if (!DonatedItemIds.Contains(itemId))
            DonatedItemIds.Add(itemId);
    }

    /// <summary>The donation ledger as a set, for Contract.IsSatisfiedBy.</summary>
    public ISet<string> DonatedSet() => new HashSet<string>(DonatedItemIds);

    /// <summary>Champion a theme for this week: set current and add to the month's championed set.</summary>
    public void Champion(Theme theme)
    {
        CurrentChampion = theme;
        if (!ChampionedThemesThisMonth.Contains(theme))
            ChampionedThemesThisMonth.Add(theme);
    }

    /// <summary>Advance to a new month: change season, reset to day 1, clear championing. Donations persist.</summary>
    public void BeginNewMonth(Season season)
    {
        Season = season;
        DayOfMonth = 1;
        ChampionedThemesThisMonth.Clear();
        CurrentChampion = null;
    }

    /// <summary>Start a fresh loop attempt: reset to Spring 1, wipe ledger + championing, set the new seed.</summary>
    public void BeginNewRun(int seed)
    {
        RunNumber += 1;
        Seed = seed;
        Season = Season.Spring;
        DayOfMonth = 1;
        DonatedItemIds.Clear();
        ChampionedThemesThisMonth.Clear();
        CurrentChampion = null;
        AwardedBundleCompletions.Clear();
        AwardedRoomCompletions.Clear();
    }
}
