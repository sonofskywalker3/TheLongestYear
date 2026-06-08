using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Mutable per-run progress for one loop attempt. Persisted as per-save data and committed only with
/// the game's own save (see MetaStore) so it cannot be save-scummed. Lists (not sets) keep JSON
/// round-tripping simple; membership helpers enforce uniqueness.
/// </summary>
public sealed class RunState
{
    /// <summary>Per-run RNG seed (used by SelectionService + BonusItemSampler).
    /// Stored so reload reproduces this week's offer and bonus samples.</summary>
    public int Seed { get; set; }

    /// <summary>1-based attempt counter (loop number), for logging and the narrative layer.</summary>
    public int RunNumber { get; set; } = 1;

    public Season Season { get; set; } = Season.Spring;

    public int DayOfMonth { get; set; } = 1;

    /// <summary>Cumulative donated item ids this run (consumed-on-donate; quantities/JP land in Plan 04).</summary>
    public List<string> DonatedItemIds { get; set; } = new();

    /// <summary>
    /// Donated item ids for the CURRENT week only. Cleared on every theme <see cref="Select"/>,
    /// <see cref="BeginNewMonth"/>, and <see cref="BeginNewRun"/>. The weekly theme quest's checklist
    /// reads this (not the cumulative <see cref="DonatedItemIds"/>) so a re-sampled bonus item that was
    /// already donated in an EARLIER week doesn't auto-tick the box — or auto-complete the quest for a
    /// free JP bonus + drawback lift — without a real donation this week.
    /// </summary>
    public List<string> DonatedThisWeekIds { get; set; } = new();

    /// <summary>
    /// The active week's bonus-item sample (qualified ids) for the selected theme. Populated at
    /// selection time from <see cref="BonusItemSampler"/>; donating any of these earns the 1.5×
    /// SelectionBonusMultiplier. Cleared on <see cref="BeginNewMonth"/> and <see cref="BeginNewRun"/>
    /// so a fresh month/run starts with no active bonuses.
    /// </summary>
    public List<string> CurrentWeekBonusItems { get; set; } = new();

    /// <summary>Themes already selected this month (cleared each month). The 5th is never selected.</summary>
    public List<Theme> SelectedThemesThisMonth { get; set; } = new();

    /// <summary>The theme selected this week, whose bonus/liability are active. Null between weeks.</summary>
    public Theme? CurrentSelection { get; set; }

    /// <summary>
    /// Pre-pick for the FIRST week of the upcoming month, set on day 28's Sunday-night planning
    /// hub. <see cref="BeginNewMonth"/> consumes this (if present) to seed the new month's
    /// <see cref="CurrentSelection"/> so the day-28 pick survives the cross-season boundary.
    /// Null on a fresh run / after consumption.
    /// </summary>
    public Theme? NextMonthSelection { get; set; }

    /// <summary>The week-of-year for which the planning hub last presented an offer (-1 = never).
    /// Used so a re-trigger mid-week is a no-op — the hub only opens once per target week.</summary>
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

    /// <summary>Deepest mine floor reached this run. Used by RunBaseline to cap the
    /// restored mine elevator floor on reset (cap-not-grant). Updated by
    /// PeakMineFloorTracker (mod-side) on Player.Warped into a MineShaft.</summary>
    public int PeakMineFloor { get; set; }

    /// <summary>True when this week's theme quest has been completed (all 4 bonus items
    /// donated) and the liability is lifted for the rest of the week. Reset on theme select,
    /// month transition, and run reset. Persisted via MetaStore so a save+reload mid-week
    /// keeps the lifted state.</summary>
    public bool LiabilitySuppressedThisWeek { get; set; }

    /// <summary>Record having reached the given floor this run. Idempotent for shallower
    /// floors — only deeper reaches update the peak.</summary>
    public void RecordMineFloor(int floor)
    {
        if (floor > PeakMineFloor)
            PeakMineFloor = floor;
    }

    public int WeekOfYear => Calendar.WeekOfYear((int)Season, DayOfMonth);

    public int WeekInMonth => Calendar.WeekInMonth(DayOfMonth);

    public bool IsSelected(Theme theme) => SelectedThemesThisMonth.Contains(theme);

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

    /// <summary>Record a vault bundle as paid this run; returns false if it was already recorded
    /// (keeps <see cref="VaultBundlesPaid"/> deduped so the count maxes at 4).</summary>
    public bool TryMarkVaultBundlePaid(int bundleIndex)
    {
        if (VaultBundlesPaid.Contains(bundleIndex))
            return false;
        VaultBundlesPaid.Add(bundleIndex);
        return true;
    }

    /// <summary>Add a donated item id; idempotent so re-donating the same id is a no-op in the ledger.</summary>
    public void RecordDonation(string itemId)
    {
        if (!DonatedItemIds.Contains(itemId))
            DonatedItemIds.Add(itemId);
        if (!DonatedThisWeekIds.Contains(itemId))
            DonatedThisWeekIds.Add(itemId);
    }

    /// <summary>Add a donated id to the cumulative ledger ONLY (not the weekly bonus-suppression
    /// list), idempotent. Used by the day-end CC reconcile (<c>ItemDonationSync</c>), which unions
    /// the run's full historical deposits from vanilla state and must not pollute this-week
    /// tracking with items donated in earlier weeks.</summary>
    public void RecordCumulativeDonation(string itemId)
    {
        if (!DonatedItemIds.Contains(itemId))
            DonatedItemIds.Add(itemId);
    }

    /// <summary>The donation ledger as a set, for the gate evaluator.</summary>
    public ISet<string> DonatedSet() => new HashSet<string>(DonatedItemIds);

    /// <summary>Select a theme for this week: set current and add to the month's selections set.
    /// Also clears <see cref="LiabilitySuppressedThisWeek"/> — a fresh pick must always start
    /// with the liability active, otherwise the player could keep cycling themes to skip
    /// drawbacks entirely.</summary>
    public void Select(Theme theme)
    {
        CurrentSelection = theme;
        if (!SelectedThemesThisMonth.Contains(theme))
            SelectedThemesThisMonth.Add(theme);
        LiabilitySuppressedThisWeek = false;
        // A fresh week's quest must start from zero donations this week (the cumulative ledger persists).
        DonatedThisWeekIds.Clear();
    }

    /// <summary>Advance to a new month: change season, reset to day 1, clear selections. Donations
    /// persist. If <see cref="NextMonthSelection"/> was set (Sunday-night day-28 pre-pick), apply
    /// it as the new month's week-1 selection before clearing.</summary>
    public void BeginNewMonth(Season season)
    {
        Season = season;
        DayOfMonth = 1;
        SelectedThemesThisMonth.Clear();
        CurrentSelection = null;
        CurrentWeekBonusItems.Clear();
        LiabilitySuppressedThisWeek = false;
        DonatedThisWeekIds.Clear();

        // Consume the day-28 pre-pick (if any). The controller still needs to call
        // PopulateBonusItemsForCurrentSelection AFTER this so the new month's bonus list
        // matches the new season — see RunController.OnDayStarted.
        if (NextMonthSelection.HasValue)
        {
            Select(NextMonthSelection.Value);
            NextMonthSelection = null;
        }
    }

    /// <summary>Start a fresh loop attempt: reset to Spring 1, wipe ledger + selections, set the new seed.</summary>
    public void BeginNewRun(int seed)
    {
        RunNumber += 1;
        Seed = seed;
        Season = Season.Spring;
        DayOfMonth = 1;
        DonatedItemIds.Clear();
        DonatedThisWeekIds.Clear();
        SelectedThemesThisMonth.Clear();
        CurrentSelection = null;
        NextMonthSelection = null;
        AwardedBundleCompletions.Clear();
        AwardedRoomCompletions.Clear();
        VaultBundlesPaid.Clear();
        CurrentWeekBonusItems.Clear();
        OfferPresentedWeek = -1;
        PeakMineFloor = 0;
        LiabilitySuppressedThisWeek = false;
    }
}
