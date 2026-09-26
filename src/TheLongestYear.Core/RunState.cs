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

    /// <summary>The day-28 outcome the gate queued tonight and the morning has not resolved yet
    /// (Junimo scene, then rewind / next season / win screen). Persisted because the night save
    /// already writes the NEXT month's date: a quit before the scene resolves must replay it on
    /// load, not roll the run into a season the player never earned (Nexus report gmastern1,
    /// 2026-09-12). Cleared when the scene ends.</summary>
    public Day28.Day28Branch PendingDay28 { get; set; } = Day28.Day28Branch.None;

    /// <summary>True when a load should run the month rollover itself: the calendar moved on
    /// since the run-state was saved AND no day-28 outcome is waiting to decide what happens
    /// instead. A pending Fail rewinds rather than advances, so it must never roll over.</summary>
    public bool OwesMonthRolloverOnLoad(Season calendarSeason)
        => PendingDay28 == Day28.Day28Branch.None && calendarSeason != Season;

    /// <summary>
    /// LEGACY (pre-slot ledger, 2026-08-29): the old id-only donation ledger. Kept ONLY so saves
    /// from older versions deserialize. Never read and never written by current code; cleared on
    /// <see cref="BeginNewRun"/>. The ledger is <see cref="DonatedSlots"/>.
    /// </summary>
    public List<string> DonatedItemIds { get; set; } = new();

    /// <summary>The run's donation ledger: every Community Center slot the board shows filled, one
    /// entry per slot (spec 2026-08-29-per-slot-ledger). Mirrored from the board by
    /// ItemDonationSync on load, before the Season Goals page and before the day-end gate, and kept
    /// current in between by the live DonationObserver.</summary>
    public List<DonatedSlot> DonatedSlots { get; set; } = new();

    /// <summary>
    /// LEGACY (pre-slot redesign, 2026-07-09): the old id-only weekly bonus sample. Kept ONLY so
    /// mid-week saves from older versions deserialize and RunController can detect + migrate them
    /// (non-empty here + empty CurrentWeekBonusSlots → one-time re-sample). Never populated by
    /// current code — only cleared.
    /// </summary>
    public List<string> CurrentWeekBonusItems { get; set; } = new();

    /// <summary>
    /// The active week's sampled goal slots. Populated at selection time by
    /// RunController.PopulateBonusSlotsForCurrentSelection; a goal ticks when its exact CC slot
    /// flips complete (live state is the source of truth). Completing a sampled slot earns the
    /// 1.5× SelectionBonusMultiplier. Cleared on Select/BeginNewMonth/BeginNewRun.
    /// </summary>
    public List<BonusSlot> CurrentWeekBonusSlots { get; set; } = new();

    /// <summary>Themes already selected this month (cleared each month): four picks from the
    /// eight themes, so at least four are never selected in a given month.</summary>
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

    /// <summary>Deja-vu dialogue: villagers who already said their line this loop (one each).</summary>
    public List<string> DejaVuShownTo { get; set; } = new();

    /// <summary>Deja-vu dialogue: days-played stamp of the last line anywhere in town (-1 = none).</summary>
    public int DejaVuLastDay { get; set; } = -1;

    /// <summary>Deja-vu rollup: Farmer.eventsSeen as of the last rollup, so tonight's new heart
    /// events can be counted by difference.</summary>
    public List<string> EventsSeenAtDayStart { get; set; } = new();

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

    /// <summary>The day (Game1.Date.TotalDays) whose Traveling Cart selection is remembered in
    /// <see cref="CartStockIds"/>; -1 = none. Makes the cart cap per DAY: buying an item leaves
    /// a gap instead of pulling the next item up (Nexus post, lexihope, 2026-08-25).</summary>
    public int CartStockDay { get; set; } = -1;

    /// <summary>Qualified item ids the cart exposed first on <see cref="CartStockDay"/>.</summary>
    public List<string> CartStockIds { get; set; } = new();

    /// <summary>Festival whose main event (Egg Hunt, ice fishing, Flower Dance, grange judging) has
    /// already been played, paired with <see cref="FestivalMainEventDay"/>. See FestivalMainEvent:
    /// TLY festivals do not end the day, so without this the player can walk out and back in and run
    /// the main event again (Jeff, 2026-08-26: three Egg Hunts in one day on emmalution stream).</summary>
    public string FestivalMainEventId { get; set; } = "";

    /// <summary>The day (Game1.Date.TotalDays) <see cref="FestivalMainEventId"/> was played on;
    /// -1 = none. Keying on the day means the stamp expires at the next sunrise on its own.</summary>
    public int FestivalMainEventDay { get; set; } = -1;

    /// <summary>True when this week's theme quest has been completed (every sampled goal slot
    /// donated; the goal count varies by season, see BonusItemSampler.DefaultMaxCountBySeason)
    /// and the liability is lifted for the rest of the week. Also set by the empty-pool
    /// auto-lift when no goal slots could be sampled this week (see
    /// RunController.ApplyEmptyPoolLiftIfNeeded). The weekly JP is paid per goal and guarded by
    /// BonusSlot.Paid (rule D), not by this flag. Reset on theme select, month transition, and
    /// run reset. Persisted via MetaStore so a save+reload mid-week keeps the lifted state.</summary>
    public bool LiabilitySuppressedThisWeek { get; set; }

    /// <summary>Every boost bought this run, active or expired-but-not-yet-pruned (the Active tab
    /// shows "expires tonight" from the entry). Pruned on DayStarted, cleared by BeginNewRun.
    /// Spec 2026-08-29 shrine tabs + JP Boosts, section 1.2.</summary>
    public List<ActiveBoost> ActiveBoosts { get; set; } = new();

    /// <summary>Crash Course: levels bought this loop per skill index (cap 2). Earned level =
    /// current level minus this; a bought level is never keepable.</summary>
    public Dictionary<int, int> SkillLevelsBoughtThisLoop { get; set; } = new();

    /// <summary>Crash Course: levels bought this loop across all skills (the n in 3^(n-1)).</summary>
    public int SkillLevelsBoughtTotal { get; set; }

    /// <summary>Rain Dance / Storm Call: the day of year the override applies to, and the weather.</summary>
    public int WeatherOverrideDay { get; set; } = -1;
    public string? WeatherOverride { get; set; }

    /// <summary>Legacy (0.16.117 to 0.16.158, never released): migrated into ActiveBoosts by
    /// BoostState.MigrateLegacy on load.</summary>
    public int YearTwoSeedsWeek { get; set; } = -1;
    public int SneakPeekSeason { get; set; } = -1;

    /// <summary>Animals owed a second product today (Kitchen bonus animal_double_product).
    /// Written by the night's FarmAnimal.dayUpdate, consumed when the product is collected,
    /// cleared on DayEnding (before the next night's update) and on a run reset.</summary>
    public List<DoubleProduceRecord> DoubleProduceToday { get; set; } = new();

    /// <summary>Year One Ending (spec 2026-09-06): set at bedtime on the night the board completes.
    /// The EndingEventDriver plays the ending the first time the player steps onto the farm the next
    /// morning, then clears it. Survives a quit overnight.</summary>
    public bool EndingArmed { get; set; }

    // ---- Darkness pushback (spec 2026-09-09) ----
    /// <summary>Week of the year the blight counter below belongs to; -1 until blight first strikes.</summary>
    public int BlightWeek { get; set; } = -1;
    /// <summary>Nights blight has struck in <see cref="BlightWeek"/>.</summary>
    public int BlightNightsThisWeek { get; set; }
    /// <summary>Week of the year a donation last came undone; -1 until it happens.</summary>
    public int LastReversionWeek { get; set; } = -1;
    /// <summary>Days of the year (Winter) a requirement was rewritten this loop.</summary>
    public List<int> TamperDays { get; set; } = new();
    /// <summary>Every rewrite this loop, for tly_sabotage status and the log.</summary>
    public List<Sabotage.TamperRecord> Tampers { get; set; } = new();
    /// <summary>Morning HUD lines the night pass queued; drained by the next OnDayStarted.</summary>
    public List<Sabotage.SabotageReport> PendingSabotageReports { get; set; } = new();

    // ---- Darkness rework (spec 2026-09-15 Part B) ----
    /// <summary>Week of the year <see cref="DarknessChance"/> belongs to; -1 until the first roll.</summary>
    public int DarknessChanceWeek { get; set; } = -1;
    /// <summary>The week's current strike chance, after this week's decay. Meaningless when
    /// <see cref="DarknessChanceWeek"/> is not the current week.</summary>
    public double DarknessChance { get; set; }
    /// <summary>This loop's one unmoderated reversion has fired (Hard and Extreme).</summary>
    public bool UnmoderatedReversionSpent { get; set; }
    /// <summary>This loop's one unmoderated tamper has fired (Hard and Extreme).</summary>
    public bool UnmoderatedTamperSpent { get; set; }
    /// <summary>This Winter's guaranteed week-1 tamper has landed.</summary>
    public bool GuaranteedTamperDone { get; set; }
    /// <summary>Names of the DarknessEvent values that have struck this loop (spec 2026-09-21: the
    /// guarantee and the once-per-loop scenes both read it).</summary>
    public HashSet<string> StruckEvents { get; set; } = new();
    /// <summary>DarknessEvent names whose overnight strike scene has already played this loop
    /// (spec 2026-09-21): a kind's scene is due only the first time it strikes in a loop.</summary>
    public HashSet<string> StrikeScenesPlayed { get; set; } = new();

    // ---- Morris's offer (spec 2026-09-25-joja-offer-design). Per loop: a rewind starts all four over. ----

    /// <summary>Day of year (1..112) the offer scene played this loop, -1 = not yet this loop.</summary>
    public int JojaSceneSeenDay { get; set; } = -1;

    /// <summary>This loop's "come see me" letter days (day of year), planned on the first morning
    /// that needs them and keeping only the days from that morning on (eight on a fresh loop,
    /// fewer on a run first planned mid-year). Entry i is letter i+1. Empty = not planned yet.</summary>
    public List<int> JojaLetterDays { get; set; } = new();

    /// <summary>How many "come see me" letters went out this loop.</summary>
    public int JojaLettersSent { get; set; }

    /// <summary>How many "make a decision" letters went out this loop.</summary>
    public int JojaDecisionLettersSent { get; set; }

    /// <summary>Record that an animal is owed a second product today. Idempotent per animal.</summary>
    public void RecordDoubleProduce(long animalId, string produceId)
    {
        DoubleProduceToday ??= new List<DoubleProduceRecord>();
        if (DoubleProduceToday.Exists(r => r.AnimalId == animalId)) return;
        DoubleProduceToday.Add(new DoubleProduceRecord { AnimalId = animalId, ProduceId = produceId ?? "" });
    }

    /// <summary>Take (and remove) the animal's owed product, if any.</summary>
    public bool TryTakeDoubleProduce(long animalId, out string produceId)
    {
        DoubleProduceRecord? record = DoubleProduceToday?.Find(r => r.AnimalId == animalId);
        if (record == null)
        {
            produceId = "";
            return false;
        }
        DoubleProduceToday!.Remove(record);
        produceId = record.ProduceId;
        return true;
    }

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

    /// <summary>Record a filled slot. Idempotent per (bundle, ingredient) pair; returns true when
    /// the slot was newly added. A repeated id in one bundle is two slots and two entries.</summary>
    public bool RecordDonation(int bundleIndex, int ingredientIndex, string itemId)
    {
        DonatedSlots ??= new List<DonatedSlot>();
        if (DonatedSlots.Exists(s => s.BundleIndex == bundleIndex && s.IngredientIndex == ingredientIndex))
            return false;
        DonatedSlots.Add(new DonatedSlot { BundleIndex = bundleIndex, IngredientIndex = ingredientIndex, ItemId = itemId ?? "" });
        return true;
    }

    /// <summary>The mirror write: the ledger becomes exactly the given slots (the board's state).</summary>
    public void ReplaceDonations(IEnumerable<DonatedSlot> slots)
    {
        var next = new List<DonatedSlot>();
        var seen = new HashSet<(int, int)>();
        foreach (DonatedSlot s in slots ?? System.Array.Empty<DonatedSlot>())
            if (seen.Add((s.BundleIndex, s.IngredientIndex)))
                next.Add(new DonatedSlot { BundleIndex = s.BundleIndex, IngredientIndex = s.IngredientIndex, ItemId = s.ItemId ?? "" });
        DonatedSlots = next;
    }

    /// <summary>The ledger as a read view for the gate, the page and the sims.</summary>
    public SlotLedger DonatedLedger() => new SlotLedger(DonatedSlots ?? new List<DonatedSlot>());

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
        // A fresh pick must start from zero goals — the previous week's sampled slots don't carry over.
        CurrentWeekBonusSlots.Clear();
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
        CurrentWeekBonusSlots.Clear();
        LiabilitySuppressedThisWeek = false;

        // Consume the day-28 pre-pick (if any). The controller still needs to call
        // PopulateBonusSlotsForCurrentSelection AFTER this so the new month's goal slots
        // match the new season — see RunController.OnDayStarted.
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
        (DonatedSlots ??= new()).Clear();
        SelectedThemesThisMonth.Clear();
        CurrentSelection = null;
        NextMonthSelection = null;
        AwardedBundleCompletions.Clear();
        AwardedRoomCompletions.Clear();
        VaultBundlesPaid.Clear();
        CurrentWeekBonusItems.Clear();
        CurrentWeekBonusSlots.Clear();
        OfferPresentedWeek = -1;
        PeakMineFloor = 0;
        CartStockDay = -1;
        // A rewind means the festival has not happened yet for this farmer: the calendar is back
        // at Spring 1, so loop 2 hits the same TotalDays and would inherit loop 1 stamp and
        // refuse the Egg Hunt forever. Once per DAY, not once per playthrough (Jeff, 2026-08-26).
        FestivalMainEventId = "";
        FestivalMainEventDay = -1;
        (CartStockIds ??= new()).Clear();
        (DoubleProduceToday ??= new()).Clear();
        LiabilitySuppressedThisWeek = false;
        ActiveBoosts.Clear();
        SkillLevelsBoughtThisLoop.Clear();
        SkillLevelsBoughtTotal = 0;
        WeatherOverrideDay = -1;
        WeatherOverride = null;
        YearTwoSeedsWeek = -1;
                SneakPeekSeason = -1;
        // A new run cannot have a win night pending: a reset taken while the ending was armed
        // (tly_reset the same night, or a fail-night reset racing a win) left the flag set for
        // the whole next loop, which forced every night sunny and, since 2026-09-09, silenced the
        // darkness (live run, 2026-09-09).
        EndingArmed = false;
        BlightWeek = -1;
        BlightNightsThisWeek = 0;
        LastReversionWeek = -1;
        (TamperDays ??= new()).Clear();
        (Tampers ??= new()).Clear();
        (PendingSabotageReports ??= new()).Clear();
        DarknessChanceWeek = -1;
        DarknessChance = 0.0;
        UnmoderatedReversionSpent = false;
        UnmoderatedTamperSpent = false;
        GuaranteedTamperDone = false;
        (StruckEvents ??= new()).Clear();
        (StrikeScenesPlayed ??= new()).Clear();
        JojaSceneSeenDay = -1;
        (JojaLetterDays ??= new()).Clear();
        JojaLettersSent = 0;
        JojaDecisionLettersSent = 0;
    }
}
