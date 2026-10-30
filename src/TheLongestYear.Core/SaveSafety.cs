using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace TheLongestYear.Core;

/// <summary>When the full save right after a loop reset may open.</summary>
public static class ResetSaveGate
{
    /// <summary>The reset rebuilds the world with loadForNewGame, so SMAPI owes every mod a
    /// SaveLoaded and a DayStarted for the new world, and raises them a tick or two later. SMAPI
    /// also raises a DayStarted after every save. A save opened before the reload finishes gives
    /// mods two day starts with the save between them, and a mod that reloads its per-save data on
    /// DayStarted (Farm Type Manager) forgets everything it spawned on the first one: those objects
    /// were never removed at day end and broke the next night's save (TLY 1.0.0, two players with
    /// SVE). Waiting for the reload puts the save after the mods' day start, like any mid-day save.</summary>
    public static bool CanOpen(bool menuOpen, bool eventUp, bool minigameUp, bool worldReloadFinished)
        => worldReloadFinished && !menuOpen && !eventUp && !minigameUp;
}

/// <summary>Waiting for the reload was not enough on its own: SMAPI raises DayStarted right after
/// every Saved, so the post-reset save still gave mods a second day start on Spring 1. Farm Type
/// Manager reloads its records on DayStarted and respawns everything again, losing track of what it
/// had already placed; a ghost it no longer knew about stayed in the world and broke the next
/// night's save (SylviaStormsinger, TLY 1.0.27, Seven Deadly Sins pack). The post-reset save is not
/// a new day, so that one DayStarted is dropped.
///
/// The steps: armed when the save's Saving runs; on its Saved the caller switches the day start
/// off (<see cref="Gate"/>); SMAPI raises nothing for a day start with no listeners, so the first
/// tick after finds the latch still armed and gated, which means the drop held, and the caller
/// switches the day start back on (<see cref="Settle"/>). If a day start reaches our own handler
/// while armed (<see cref="DayStartArrived"/>), the drop failed and the caller says so.</summary>
public sealed class PostSaveDayStartLatch
{
    /// <summary>How the latch ended at <see cref="Settle"/>.</summary>
    public enum Outcome
    {
        /// <summary>Never armed: an ordinary save, nothing to do.</summary>
        Idle,
        /// <summary>Gated and no day start got through: the drop worked. Switch the day start back on.</summary>
        Dropped,
        /// <summary>A day start reached our handler while armed. Switch it back on if it was switched off; the leak was already reported.</summary>
        Leaked,
        /// <summary>Armed, but no Saved came (the save menu closed without one). Nothing was switched off.</summary>
        NoSaved,
    }

    public bool Armed { get; private set; }

    /// <summary>The caller switched the day start off and owes a switch back on.</summary>
    public bool Gated { get; private set; }

    /// <summary>A day start arrived while armed (after the gate, or with no gate).</summary>
    private bool _leaked;

    public void Arm() => Armed = true;

    /// <summary>The save's Saved: true once per <see cref="Arm"/>, and the caller switches the
    /// day start off now. The following DayStarted is raised in the same step.</summary>
    public bool Gate()
    {
        if (!Armed || Gated) return false;
        Gated = true;
        return true;
    }

    /// <summary>Our own DayStarted handler: true if this day start should have been dropped and
    /// was not. That day start has already run for every mod, so the latch is spent.</summary>
    public bool DayStartArrived()
    {
        if (!Armed) return false;
        Armed = false;
        _leaked = true;
        return true;
    }

    /// <summary>The first tick after the save (or leaving the save): reports what happened and
    /// resets the latch. The caller switches the day start back on for Dropped and Leaked.</summary>
    public Outcome Settle()
    {
        Outcome outcome =
            _leaked ? Outcome.Leaked
            : !Armed ? Outcome.Idle
            : Gated ? Outcome.Dropped
            : Outcome.NoSaved;
        Armed = false;
        Gated = false;
        _leaked = false;
        return outcome;
    }
}

/// <summary>Which placed objects the save cannot write.</summary>
public static class UnsaveableObjects
{
    /// <summary>The keys of the entries whose value the save serializer cannot write. Values of a
    /// game type are trusted without a probe; any other type is probed once with
    /// <paramref name="canWrite"/> and the verdict kept in <paramref name="verdicts"/>, so a
    /// thousand copies of one mod object cost one probe.</summary>
    public static List<TKey> Pick<TKey, TValue>(
        IEnumerable<KeyValuePair<TKey, TValue>> entries,
        Func<Type, bool> isGameType,
        Func<TValue, bool> canWrite,
        IDictionary<Type, bool> verdicts)
    {
        var picked = new List<TKey>();
        foreach (var pair in entries)
        {
            if (pair.Value is null) continue;
            Type type = pair.Value.GetType();
            if (isGameType(type)) continue;
            if (!verdicts.TryGetValue(type, out bool writable))
            {
                writable = canWrite(pair.Value);
                verdicts[type] = writable;
            }
            if (!writable) picked.Add(pair.Key);
        }
        return picked;
    }
}

/// <summary>Which types a mod has taught the save serializer to write.</summary>
public static class SerializerKnownTypes
{
    /// <summary>The types the save serializer knows beyond the game's own: every type registered
    /// with it (SpaceCore's serializer types), plus every type those name with [XmlInclude],
    /// followed through. The serializer accepts a value only if its exact runtime type is known,
    /// so a subclass of a known type is not covered; and the game's own types never name a mod's
    /// type, so for a type from outside the game this set is the whole answer.</summary>
    public static HashSet<Type> Closure(IEnumerable<Type> registered)
    {
        var known = new HashSet<Type>();
        var pending = new Stack<Type>(registered);
        while (pending.Count > 0)
        {
            Type type = pending.Pop();
            if (type == null || !known.Add(type)) continue;
            foreach (XmlIncludeAttribute include in type.GetCustomAttributes(typeof(XmlIncludeAttribute), inherit: false))
                if (include.Type != null) pending.Push(include.Type);
        }
        return known;
    }
}