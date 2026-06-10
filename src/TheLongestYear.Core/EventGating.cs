using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>Whether a candidate vanilla event should be allowed to run or suppressed this tick.</summary>
public enum EventGatingDecision
{
    Allow,
    Suppress
}

/// <summary>
/// The curated event-id sets that drive <see cref="EventGatingPolicy"/>. Real vanilla ids are
/// sourced from the <c>tly_dumpevents</c> runtime audit (the ids live in compiled Data/Events
/// content, not in code) and wired into <see cref="Default"/> — never guessed.
/// </summary>
public sealed class EventGatingTables
{
    /// <summary>Spring is season index 0; the early-event hold only applies during Spring.</summary>
    public const int SpringSeasonIndex = 0;

    private readonly HashSet<string> _replayable;
    private readonly HashSet<string> _holdUntilSpring5;
    private readonly HashSet<string> _furnace;

    /// <summary>Calendar day a held early event is released on (inclusive). Default Spring 5.</summary>
    public int HoldThresholdDay { get; }

    public EventGatingTables(
        IEnumerable<string> replayable,
        IEnumerable<string> holdUntilSpring5,
        IEnumerable<string> furnace,
        int holdThresholdDay = 5)
    {
        _replayable = new HashSet<string>(replayable, StringComparer.Ordinal);
        _holdUntilSpring5 = new HashSet<string>(holdUntilSpring5, StringComparer.Ordinal);
        _furnace = new HashSet<string>(furnace, StringComparer.Ordinal);
        HoldThresholdDay = holdThresholdDay;
    }

    /// <summary>Events excluded from the cross-loop seen re-seed so they can fire again each loop.</summary>
    public IReadOnlyCollection<string> ReplayableEventIds => _replayable;

    public bool IsReplayable(string eventId) => _replayable.Contains(eventId);
    public bool IsHeldUntilSpring5(string eventId) => _holdUntilSpring5.Contains(eventId);
    public bool IsFurnaceTeach(string eventId) => _furnace.Contains(eventId);

    /// <summary>Event-script commands that GRANT a run-wipe-able unlock. An event whose script runs
    /// any of these re-teaches/re-unlocks something <c>FarmerReset</c> clears, so it must re-fire each
    /// loop. "mailReceived" is the vanilla alias of "addMailReceived" (Event.cs DefaultCommands).</summary>
    private static readonly string[] GrantCommandTokens =
        { "addCraftingRecipe", "addCookingRecipe", "addMailReceived", "mailReceived", "addQuest" };

    /// <summary>The grant command this script runs (for diagnostics), or null if none. Event scripts
    /// are "/"-delimited command segments; a grant is detected when a segment STARTS WITH a token
    /// (followed by a space or end-of-segment), so a token appearing inside <c>speak</c> dialogue text
    /// is ignored. Limitation: only "/"-segments are scanned, so a grant nested in a <c>quickQuestion</c>
    /// dialogue-choice branch (branches are "\"-delimited, not "/") is not detected — no vanilla unlock
    /// teach uses that shape; revisit if a choice-gated mod teach is reported as not replaying.</summary>
    public static string? MatchedGrantToken(string? script)
    {
        if (string.IsNullOrEmpty(script))
            return null;
        foreach (string segment in script.Split('/'))
        {
            string s = segment.TrimStart();
            foreach (string token in GrantCommandTokens)
            {
                if (s.Length < token.Length)
                    continue;
                if (!s.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (s.Length == token.Length || s[token.Length] == ' ')
                    return token;
            }
        }
        return null;
    }

    /// <summary>True if the event script grants a run-wipe-able unlock (see <see cref="MatchedGrantToken"/>).</summary>
    public static bool ScriptGrantsUnlock(string? script) => MatchedGrantToken(script) != null;

    /// <summary>The event ids that should be REPLAYABLE each loop: every scanned event whose script
    /// grants an unlock, MINUS the explicit <paramref name="exclude"/> set (narrative-suppressed +
    /// relationship events), UNION the always-replayable <paramref name="baseReplayableIds"/> (the
    /// vanilla furnace/cave ids — never excluded). Pure; the runtime scanner feeds it loaded content.</summary>
    public static HashSet<string> CollectReplayableIds(
        IEnumerable<(string id, string script)> events,
        IEnumerable<string> baseReplayableIds,
        ISet<string>? exclude)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string id, string script) in events)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            if (exclude != null && exclude.Contains(id))
                continue;
            if (ScriptGrantsUnlock(script))
                result.Add(id);
        }
        if (baseReplayableIds != null)
            foreach (string id in baseReplayableIds)
                result.Add(id);
        return result;
    }

    // Real vanilla ids, confirmed via the tly_dumpevents audit (2026-06-03), both in
    // Data/Events/Farm:
    //   992553 — Clint teaches the Furnace recipe ("…you've been bringing copper ore…").
    //   65     — Demetrius' cave choice (mushrooms vs fruit bats).
    private const string FurnaceTeachEventId = "992553";
    private const string DemetriusCaveEventId = "65";

    /// <summary>The live tables, wired with the audited vanilla ids. The furnace teach is replayable
    /// (excluded from the seen re-seed) but suppressed while the recipe is already known this run.
    /// The Demetrius cave scene plays ONCE (held to Spring 5); from loop 2 on it stays seen
    /// (event-hygiene pass 2026-06-10) and the per-loop mushrooms-vs-bats re-choice is offered by
    /// the lightweight <c>CaveChoicePrompt</c> on cave entry instead of replaying the cutscene.</summary>
    public static EventGatingTables Default { get; } = new EventGatingTables(
        replayable: new[] { FurnaceTeachEventId },
        holdUntilSpring5: new[] { DemetriusCaveEventId },
        furnace: new[] { FurnaceTeachEventId });
}

/// <summary>
/// Pure decision for whether a candidate event should run this tick, layered on top of vanilla's
/// own seen-check + preconditions. Two rules:
///   • Hold a jarring early event until Spring <see cref="EventGatingTables.HoldThresholdDay"/>.
///   • Suppress an unlock-teach scene (furnace) while that unlock is already known this run.
/// Everything else defers to vanilla.
/// </summary>
public static class EventGatingPolicy
{
    public static EventGatingDecision Decide(
        string eventId, int seasonIndex, int dayOfMonth, bool furnaceKnownThisRun,
        EventGatingTables tables)
    {
        if (string.IsNullOrEmpty(eventId))
            return EventGatingDecision.Allow;

        // Hold jarring early events: suppress only during Spring days before the threshold. Once
        // it's Spring 5+ (or any later season — a loop always starts at Spring 1), it's released.
        if (tables.IsHeldUntilSpring5(eventId)
            && seasonIndex == EventGatingTables.SpringSeasonIndex
            && dayOfMonth < tables.HoldThresholdDay)
            return EventGatingDecision.Suppress;

        // Furnace teach scene: only needed when the recipe isn't already in hand this run.
        if (tables.IsFurnaceTeach(eventId) && furnaceKnownThisRun)
            return EventGatingDecision.Suppress;

        return EventGatingDecision.Allow;
    }
}
