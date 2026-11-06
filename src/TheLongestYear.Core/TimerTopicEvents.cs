using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Event ids that start a conversation topic some other event's condition reads. Mods time things
/// this way: a hidden scene runs <c>addConversationTopic Stay 162</c>, and a later scene waits on
/// <c>A Stay</c> (topic no longer active) to fire. The rewind clears every active topic, so with
/// the starting scene re-seeded as seen the countdown read as already over and the whole chain
/// ran on the first days of the next loop (Defense Division's characters "leaving" right after a
/// rewind, Nexus posts, Celjhyn 2026-11-05). The starting scene must replay so the countdown
/// starts again; chain propagation then carries the scenes behind it.
///
/// A topic no event condition reads is villager small talk only, and its scene stays seen.
/// </summary>
public static class TimerTopicEvents
{
    public const string AddTopicCommand = "addConversationTopic";

    /// <summary>The one-letter spelling of "topic is not active". Case matters: lowercase "a" is
    /// a different precondition.</summary>
    private const string LegacyConditionName = "A";

    /// <summary>The long spellings of the "topic is / is not active" precondition, which the game
    /// matches without regard to case.</summary>
    private static readonly string[] LongConditionNames =
        { "ActiveDialogueEvent", "NotActiveDialogueEvent" };

    private const char Quote = '"';
    private const char SegmentSeparator = '/';

    private const char Negation = '!';

    /// <summary>Ids of the events whose script starts a topic that any event's key reads.</summary>
    public static HashSet<string> EventIds(IEnumerable<(string id, string key, string script)>? events)
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        if (events == null)
            return ids;

        var all = new List<(string id, string key, string script)>(events);
        var readTopics = new HashSet<string>(StringComparer.Ordinal);
        foreach ((string _, string key, string _) in all)
            foreach (string topic in TopicsRead(key))
                readTopics.Add(topic);
        if (readTopics.Count == 0)
            return ids;

        foreach ((string id, string _, string script) in all)
        {
            if (string.IsNullOrEmpty(id))
                continue;
            foreach (string topic in TopicsStarted(script))
                if (readTopics.Contains(topic))
                {
                    ids.Add(id);
                    break;
                }
        }
        return ids;
    }

    /// <summary>Topics an event key's conditions read, e.g. "99/e 5/A Stay" gives Stay.</summary>
    private static IEnumerable<string> TopicsRead(string? key)
    {
        foreach (string segment in Segments(key))
        {
            string[] args = ActionArgs.SplitBySpaceQuoteAware(segment.Trim());
            if (args.Length < 2 || string.IsNullOrWhiteSpace(args[1]))
                continue;
            string name = args[0].TrimStart(Negation);
            if (IsTopicCondition(name))
                yield return args[1];
        }
    }

    private static bool IsTopicCondition(string name)
    {
        if (string.Equals(name, LegacyConditionName, StringComparison.Ordinal))
            return true;
        foreach (string known in LongConditionNames)
            if (string.Equals(name, known, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    /// <summary>Topics an event script starts. Only a command at the start of a segment counts,
    /// so the word inside spoken text is ignored.</summary>
    private static IEnumerable<string> TopicsStarted(string? script)
    {
        foreach (string segment in Segments(script))
        {
            string[] args = ActionArgs.SplitBySpaceQuoteAware(segment.Trim());
            if (args.Length >= 2
                && !string.IsNullOrWhiteSpace(args[1])
                && string.Equals(args[0], AddTopicCommand, StringComparison.OrdinalIgnoreCase))
                yield return args[1];
        }
    }

    /// <summary>Splits on "/" the way the game does: a slash inside double quotes is text.</summary>
    private static IEnumerable<string> Segments(string? text)
    {
        if (string.IsNullOrEmpty(text))
            yield break;
        bool quoted = false;
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == Quote)
                quoted = !quoted;
            else if (c == SegmentSeparator && !quoted)
            {
                yield return text.Substring(start, i - start);
                start = i + 1;
            }
        }
        yield return text.Substring(start);
    }
}
