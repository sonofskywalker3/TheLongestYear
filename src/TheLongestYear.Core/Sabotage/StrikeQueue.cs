using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Why a strike was postponed.</summary>
public enum PostponeCause
{
    /// <summary>A conflict: something else owns tonight's overnight slot (<see cref="StrikeSlot.Decide"/>),
    /// or another mod replaced our scene before its setUp ran.</summary>
    SlotTaken,

    /// <summary>Settled by the net with its scene never handed the slot: a fail or restart night
    /// left the slot alone, or no pickFarmEvent ran. (A scene that cannot stage is not a postpone
    /// at all: the strike lands bare, <see cref="StrikeStaging"/>.)</summary>
    NeverStaged,
}

/// <summary>Strikes waiting for a free night (designer, 2026-10-07: "Queue it for the next free
/// night"). A strike postponed because another event owned the overnight slot is queued by kind (a
/// broken scene never postpones: it lands bare, <see cref="StrikeStaging"/>); nothing about it is kept, so the
/// night it fires plans it afresh and fairly. On every later night pass the first queued kind that
/// can act tonight fires instead of the normal roll and is that night's one strike, with its scene.
/// It honours everything the kind's own "can act" test does: caps, the spacing between tampers,
/// wards, quiet days, nothing fair to take. A queued kind that cannot act tonight stays queued and
/// the night rolls normally. It leaves the queue only when it commits (its scene staged, or it lands
/// with no scene by design); a night whose slot is taken again postpones it and it stays queued.
///
/// The guaranteed Winter tamper keeps its own carry (<see cref="RunState.GuaranteedTamperPostponed"/>),
/// which already fires it on the next free night and also lifts the week-1 reservation, so it is not
/// queued here. Persisted on the run, cleared at the loop reset.</summary>
public static class StrikeQueue
{
    /// <summary>Does a strike postponed for this reason go on the queue? Only a conflict does (Jeff,
    /// 2026-10-08). A scene that cannot stage is not postponed at all: it lands bare.</summary>
    public static bool Queues(PostponeCause cause) => cause == PostponeCause.SlotTaken;

    /// <summary>Queue a postponed strike's kind. A kind already waiting is not queued twice.</summary>
    public static void Enqueue(RunState run, DarknessEvent e)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        List<string> queue = run.QueuedStrikes ??= new();
        string name = e.ToString();
        if (!queue.Contains(name)) queue.Add(name);
    }

    /// <summary>Is this kind waiting for a free night?</summary>
    public static bool IsQueued(RunState run, DarknessEvent e)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        return run.QueuedStrikes != null && run.QueuedStrikes.Contains(e.ToString());
    }

    /// <summary>The queued strike that fires tonight: the oldest one that can act, or null when
    /// none is queued or none can act (then the night rolls normally). Unknown names (a save from
    /// a later version) are passed over.</summary>
    public static DarknessEvent? Tonight(RunState run, Func<DarknessEvent, bool> canAct)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        if (canAct is null) throw new ArgumentNullException(nameof(canAct));
        if (run.QueuedStrikes == null) return null;
        foreach (string name in run.QueuedStrikes)
            if (Enum.TryParse(name, out DarknessEvent e) && canAct(e)) return e;
        return null;
    }

    /// <summary>A strike of this kind committed: it is no longer waiting. True when one was queued.</summary>
    public static bool OnCommitted(RunState run, DarknessEvent e)
    {
        if (run is null) throw new ArgumentNullException(nameof(run));
        return run.QueuedStrikes != null && run.QueuedStrikes.Remove(e.ToString());
    }
}
