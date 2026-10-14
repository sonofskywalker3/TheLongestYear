namespace TheLongestYear.Core;

/// <summary>The one-time intro quests' ids and the <see cref="MetaState.DismissedIndicators"/> keys
/// that stop them coming back, in one place (they used to be bare literals in five files).</summary>
public static class IntroQuestIds
{
    /// <summary>"Find the Junimo Stash" intro quest.</summary>
    public const string StashQuest = "tly.-9003";

    /// <summary>"Visit the planning shrine" intro quest.</summary>
    public const string ShrineQuest = "tly.-9005";

    /// <summary>Set the first time the stash chest is opened; the stash quest never re-adds after.</summary>
    public const string StashDismissed = "tly.stash";

    /// <summary>Set the first time the planning shrine is used; the shrine quest never re-adds after.</summary>
    public const string ShrineDismissed = "tly.shrine";

    /// <summary>LEGACY: the fireplace (Bundle Log) intro quest. No longer created since commit 060a54a
    /// dropped the book and board quests; still completed on first open so old saves that carry it
    /// clear it.</summary>
    public const string LegacyFireplaceQuest = "tly.-9004";

    /// <summary>LEGACY: dismissal keys from the dropped book and board quests. Still written on close,
    /// never read; kept so old saves and new ones carry the same set.</summary>
    public const string LegacyFireplaceDismissed = "tly.fireplace";
    public const string LegacyCookbookDismissed = "tly.cookbook";
    public const string LegacyCraftbookDismissed = "tly.craftbook";
}
