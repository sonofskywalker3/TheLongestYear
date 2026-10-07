using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Why a strike was postponed.</summary>
public enum PostponeCause
{
    /// <summary>Something else owns tonight's overnight slot (<see cref="StrikeSlot.Decide"/>).</summary>
    SlotTaken,

    /// <summary>Its scene is due but cannot show tonight's pick (asked at pick time).</summary>
    CannotStage,

    /// <summary>Settled by the net with its scene never staged: setUp failed or threw, another mod
    /// replaced the event, a fail or restart night left the slot alone, or no pickFarmEvent ran.</summary>
    NeverStaged,
}

/// <summary>Strikes waiting for a free night (designer, 2026-10-07: "Queue it for the next free
/// night"). A strike postponed because another event owned the overnight slot is queued by kind (a
/// staging failure is not: see <see cref="Queues"/>); nothing about it is kept, so the
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
    /// <summary>Does a strike postponed for this reason go on the queue? Only a slot collision does
    /// (review I1, ruling 2026-10-07). A scene that cannot stage would fail the same way again, so
    /// queuing it would fire it ahead of the roll every night and starve the darkness.</summary>
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
