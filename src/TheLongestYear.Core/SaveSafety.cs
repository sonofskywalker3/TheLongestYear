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

/// <summary>Asks a real XmlSerializer whether it can write one value.</summary>
public static class SerializerProbe
{
    public enum Verdict
    {
        /// <summary>The serializer wrote it.</summary>
        Writable,
        /// <summary>The serializer does not know the value's type ("The type ... was not expected").</summary>
        TypeRejected,
        /// <summary>The write failed for some other reason; nothing can be concluded about the type.</summary>
        ProbeFailed,
    }

    /// <summary>Write <paramref name="value"/> to a discarded stream. Serializing only reads the
    /// value, the same reads the save itself makes a moment later; nothing is moved or changed.</summary>
    public static Verdict Probe(XmlSerializer serializer, object value, out Exception? error)
    {
        error = null;
        try
        {
            serializer.Serialize(System.IO.Stream.Null, value);
            return Verdict.Writable;
        }
        catch (Exception ex)
        {
            error = ex;
            return Classify(ex, value.GetType());
        }
    }

    /// <summary>The serializer wraps an unknown type as InvalidOperationException ("There was an
    /// error generating the XML document") around InvalidOperationException ("The type X was not
    /// expected..."). Only that shape, naming this type, counts as a rejection of the type.</summary>
    public static Verdict Classify(Exception ex, Type type)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
        {
            if (e is InvalidOperationException
                && e.Message.Contains(type.FullName ?? type.Name, StringComparison.Ordinal)
                && e.Message.Contains("not expected", StringComparison.OrdinalIgnoreCase))
                return Verdict.TypeRejected;
        }
        return Verdict.ProbeFailed;
    }
}

/// <summary>What the reset does with the folder the next save will write.</summary>
public static class NewSaveFolderPlan
{
    public enum Action
    {
        /// <summary>No folder yet: make it, and pin the save name so the save writes into it.</summary>
        CreateAndPin,
        /// <summary>The folder exists but holds no save (one we made before a crash): use it and pin.</summary>
        AdoptAndPin,
        /// <summary>The folder holds someone's save: leave it, and let the game pick a free name itself.</summary>
        LeaveToGame,
    }

    /// <param name="folderExists">The folder &lt;farm&gt;_&lt;id&gt; exists.</param>
    /// <param name="holdsSaveFile">It holds a save file of its own name (what the load menu lists).</param>
    public static Action Decide(bool folderExists, bool holdsSaveFile)
        => !folderExists ? Action.CreateAndPin
           : holdsSaveFile ? Action.LeaveToGame
           : Action.AdoptAndPin;
}