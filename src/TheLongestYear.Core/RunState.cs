using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Mutable per-run progress for one loop attempt. Persisted as per-save data and committed only with
/// the game's own save (see MetaStore) so it cannot be save-scummed. Lists (not sets) keep JSON
/// round-tripping simple; membership helpers enforce uniqueness.
/// </summary>
public sealed class RunState
{
    /// <summary>Per-run RNG seed (used by ChampionService + BonusItemSampler).
    /// Stored so reload reproduces this week's offer and bonus samples.</summary>
    public int Seed { get; set; }

    /// <summary>1-based attempt counter (loop number), for logging and the narrative layer.</summary>
    public int RunNumber { get; set; } = 1;

    public Season Season { get; set; } = Season.Spring;

    public int DayOfMonth { get; set; } = 1;

    /// <summary>Cumulative donated item ids this run (consumed-on-donate; quantities/JP land in Plan 04).</summary>
    public List<string> DonatedItemIds { get; set; } = new();

    /// <summary>
    /// The active week's bonus-item sample (qualified ids) for the championed theme. Populated
    /// at championing time from <see cref="BonusItemSampler"/>; donating any of these earns the
    /// 1.5× ChampionBonusMultiplier. Cleared on <see cref="BeginNewMonth"/> and
    /// <see cref="BeginNewRun"/> so a fresh month/run starts with no active bonuses.
    /// </summary>
    public List<string> CurrentWeekBonusItems { get; set; } = new();

    /// <summary>Themes championed in the current month (cleared each month). The 5th is never championed.</summary>
    public List<Theme> ChampionedThemesThisMonth { get; set; } = new();

    /// <summary>The theme championed this week, whose bonus/liability are active. Null between weeks.</summary>
    public Theme? CurrentChampion { get; set; }

    /// <summary>
    /// Pre-pick for the FIRST week of the upcoming month, set on day 28's Sunday-night planning
    /// hub. <see cref="BeginNewMonth"/> consumes this (if present) to seed the new month's
    /// <see cref="CurrentChampion"/> so the day-28 pick survives the cross-season boundary.
    /// Null on a fresh run / after consumption.
    /// </summary>
    public Theme? NextMonthChampion { get; set; }

    /// <summary>The week-of-year for which the planning hub last presented an offer (-1 = never).
    /// Used so a re-trigger mid-week (e.g. via hotkey) is a no-op — the hub only opens at week-start.</summary>
    public int OfferPresentedWeek { get; set; } = -1;

    /// <summary>Bundle indices whose completion JP bonus has already been awarded this run.</summary>
    public List<int> AwardedBundleCompletions { get; set; } = new();

    /// <summary>Room/area numbers whose completion JP bonus has already been awarded this run.</summary>
    public List<int> AwardedRoomCompletions { get; set; } = new();

    /// <summary>
    /// Vault bundle indices paid this run (vanilla 1.6: 34=2500g, 35=5000g, 36=10000g, 37=25000g).
    /// Each season's gate requires the bundle of matching tier to be paid by day 28; missing it
    /// fails the run. The keep_bus_unlocked Buildings upgrade auto-satisfies all four.
    /// </summary>
    public List<int> VaultBundlesPaid { get; set; } = new();

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

    /// <summary>Advance to a new month: change season, reset to day 1, clear championing. Donations
    /// persist. If <see cref="NextMonthChampion"/> was set (Sunday-night day-28 pre-pick), apply
    /// it as the new month's week-1 champion before clearing.</summary>
    public void BeginNewMonth(Season season)
    {
        Season = season;
        DayOfMonth = 1;
        ChampionedThemesThisMonth.Clear();
        CurrentChampion = null;
        CurrentWeekBonusItems.Clear();

        // Consume the day-28 pre-pick (if any). The controller still needs to call
        // PopulateBonusItemsForCurrentChampion AFTER this so the new month's bonus list
        // matches the new season — see RunController.OnDayStarted.
        if (NextMonthChampion.HasValue)
        {
            Champion(NextMonthChampion.Value);
            NextMonthChampion = null;
        }
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
        NextMonthChampion = null;
        AwardedBundleCompletions.Clear();
        AwardedRoomCompletions.Clear();
        VaultBundlesPaid.Clear();
        CurrentWeekBonusItems.Clear();
        OfferPresentedWeek = -1;
    }
}
