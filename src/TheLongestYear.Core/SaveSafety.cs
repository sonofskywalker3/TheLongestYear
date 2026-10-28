using System;
using System.Collections.Generic;

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
/// a new day, so that one DayStarted is dropped: armed when the save's Saving runs, spent by the
/// DayStarted SMAPI raises with its Saved.</summary>
public sealed class PostSaveDayStartLatch
{
    public bool Armed { get; private set; }

    public void Arm() => Armed = true;

    public void Disarm() => Armed = false;

    /// <summary>True once per <see cref="Arm"/>: the caller drops that DayStarted.</summary>
    public bool TryConsume()
    {
        if (!Armed) return false;
        Armed = false;
        return true;
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
