using System;

namespace TheLongestYear.Core.Sabotage;

/// <summary>What vanilla's <c>pickFarmEvent</c> came back with tonight, as the strike sees it.</summary>
public enum OvernightEvent
{
    /// <summary>Nothing: the slot is empty, though vanilla still reads it again for a wedding,
    /// another mod's farm event override and a personal farm event.</summary>
    None,

    /// <summary>A random event that is rolled fresh every night (fairy, witch, meteorite, owl,
    /// capsule, dogs, barn birth): it gives way and comes round again.</summary>
    Random,

    /// <summary>Anything else (a WorldChangeEvent such as the bus repair, the day-31 earthquake, the
    /// raccoon windstorm, anything unrecognised): it keeps its night.</summary>
    Scripted,
}

/// <summary>What happens to a waiting strike when the overnight slot is decided.</summary>
public enum StrikeSlotVerdict
{
    /// <summary>A fail or restart night: nothing overnight runs, and the slot is left exactly as it is.</summary>
    LeaveAlone,

    /// <summary>Something else owns the slot: the strike does not land tonight, effect and scene both.</summary>
    Postpone,

    /// <summary>The scene takes the slot and lands the strike at its beat.</summary>
    PlayScene,
}

/// <summary>Who gets tonight's overnight slot when a strike with a scene is waiting (spec
/// 2026-09-21, corrected by Jeff 2026-10-07: "we don't delay scenes without delaying the effect of
/// them, that's stupid"). A strike never lands without its scene: when the slot belongs to
/// something else the strike is postponed, and the night is as if no strike happened (nothing is
/// recorded, so the week's chance, the cap slot and the tamper spacing stay unspent and the
/// every-loop guarantee still owes the kind). The normal roll and the guarantee bring it back on a
/// later night. A strike with no scene by design (its kind already played this loop, or a target
/// the scene cannot show) never reaches this: it lands at the night pass.</summary>
public static class StrikeSlot
{
    /// <param name="failNight">The morning rewinds (Fail or a voluntary restart).</param>
    /// <param name="wildcardTwist">A Wildcard night_event night: the player was told that morning
    /// that something will happen on the farm, so the twist owns the slot.</param>
    /// <param name="picked">What vanilla's pick came back with.</param>
    /// <param name="wedding">A wedding is today.</param>
    /// <param name="farmEventOverride">Another mod queued <c>Game1.farmEventOverride</c>.</param>
    /// <param name="personalEventOwns">Would vanilla play a personal farm event that cannot simply
    /// come round again (a birth, a couple's birth, a pregnancy question)? Asked only of an empty
    /// slot, because vanilla only reads it then (Game1.cs:8134).</param>
    public static StrikeSlotVerdict Decide(
        bool failNight, bool wildcardTwist, OvernightEvent picked, bool wedding, bool farmEventOverride, Func<bool> personalEventOwns)
    {
        if (personalEventOwns is null) throw new ArgumentNullException(nameof(personalEventOwns));
        if (failNight) return StrikeSlotVerdict.LeaveAlone;
        if (wildcardTwist) return StrikeSlotVerdict.Postpone;
        if (picked == OvernightEvent.Scripted) return StrikeSlotVerdict.Postpone;
        if (picked == OvernightEvent.None && (wedding || farmEventOverride || personalEventOwns()))
            return StrikeSlotVerdict.Postpone;
        return StrikeSlotVerdict.PlayScene;
    }
}

/// <summary>What the save, morning or next-night net does with a strike still waiting.</summary>
public enum StrikeNetAction
{
    /// <summary>It has landed or been postponed already.</summary>
    None,

    /// <summary>Its scene staged (so tonight is spent on it) but ended before the beat: land it.</summary>
    Land,

    /// <summary>Its scene never staged: a collision, a scene that could not stage or threw in setUp,
    /// another mod replacing the event after the slot was decided, or a night with no
    /// <c>pickFarmEvent</c> at all (DaysPlayed 1). Postpone it like any collision.</summary>
    Postpone,
}

/// <summary>One strike's life, pure (review C1, Jeff 2026-10-07: a strike never lands without its
/// scene). It is committed (the run records it) only when its scene has actually staged, or when it
/// lands with no scene by design. Only a committed strike may apply; only an uncommitted one may be
/// postponed; each happens at most once.</summary>
public sealed class StrikeLifecycle
{
    public bool Committed { get; private set; }
    public bool Applied { get; private set; }
    public bool Postponed { get; private set; }

    /// <summary>The scene staged. True the first time, when the run must record the strike.</summary>
    public bool Commit()
    {
        if (Committed || Postponed) return false;
        Committed = true;
        return true;
    }

    /// <summary>May the effect run now? True once, and only for a committed, unpostponed strike.</summary>
    public bool BeginApply()
    {
        if (!Committed || Applied || Postponed) return false;
        Applied = true;
        return true;
    }

    /// <summary>A strike with no scene by design: commit (if not yet) and apply now.</summary>
    public bool LandNow(out bool newlyCommitted)
    {
        newlyCommitted = Commit();
        return BeginApply();
    }

    /// <summary>Drop it unapplied. Only a strike that never committed can be postponed: once its
    /// scene staged, tonight is spent on it.</summary>
    public bool Postpone()
    {
        if (Committed || Postponed) return false;
        Postponed = true;
        return true;
    }

    public StrikeNetAction AtNet()
        => Applied || Postponed ? StrikeNetAction.None
            : Committed ? StrikeNetAction.Land
            : StrikeNetAction.Postpone;
}

/// <summary>The guaranteed Winter tamper's flags through a strike's life (review I1). Done when it
/// commits; carried past week 1 when postponed; the carry is dropped only when it lands, and a
/// failed apply makes it owed again (with the carry kept).</summary>
public static class GuaranteedTamper
{
    public static void OnPostponed(RunState run)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        run.GuaranteedTamperPostponed = true;
    }

    public static void OnCommitted(RunState run)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        run.GuaranteedTamperDone = true;
    }

    public static void OnApplied(RunState run, bool landed)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (landed) run.GuaranteedTamperPostponed = false;
        else run.GuaranteedTamperDone = false;
    }
}

/// <summary>What a strike spends once it is committed to tonight: the week's chance drop, the
/// front's cap or spacing, and the every-loop guarantee's record. Committed means it landed with no
/// scene by design, or its scene took the overnight slot; a postponed strike records nothing.</summary>
public static class StrikeLedger
{
    public static SabotageKind KindOf(DarknessEvent e) => e switch
    {
        DarknessEvent.Reversion => SabotageKind.Reversion,
        DarknessEvent.Tampering => SabotageKind.Tampering,
        _ => SabotageKind.Blight,
    };

    public static void Record(RunState run, DarknessEvent e, int weekOfYear, Season season, int dayOfYear)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        NightRoll.RecordStrike(run, weekOfYear, season);
        SabotageSchedule.RecordStrike(KindOf(e), run, weekOfYear, dayOfYear);
        (run.StruckEvents ??= new()).Add(e.ToString());
    }
}
