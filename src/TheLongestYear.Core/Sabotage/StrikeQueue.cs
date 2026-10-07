using System;
using System.Collections.Generic;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Strikes waiting for a free night (designer, 2026-10-07: "Queue it for the next free
/// night"). A strike postponed because its scene could not have the night (another event owned the
/// overnight slot, or its scene could not stage) is queued by kind; nothing about it is kept, so the
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
