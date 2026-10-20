using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Why no card this week can ask for an unmet season-gate line. Player report (0.19.97):
/// a one-card week, then no planning hub, then a failed gate, and nothing in the log said which
/// lines the themes had run out of or why.</summary>
public enum UnaskableReason
{
    /// <summary>A theme on this week's offer asks for it.</summary>
    Askable,
    /// <summary>No theme's pool holds it and the item is out of season or before its goal week.</summary>
    NotObtainableThisWeek,
    /// <summary>No theme's pool holds it and the availability model never placed the item.</summary>
    Unreachable,
    /// <summary>The item is obtainable, but no theme's pool holds the line (quality before its
    /// week, or no theme matches it).</summary>
    NoThemeCovers,
    /// <summary>A theme's pool holds it, but that theme's weekly list left it out (caps or budget).</summary>
    LeftOutOfList,
    /// <summary>Only themes already picked this month would ask for it, and none made the offer.</summary>
    ThemesPickedThisMonth,
    /// <summary>An unpicked theme would ask for it, but that theme was not dealt.</summary>
    ThemeNotDealt,
}

/// <summary>The no-theme week's HUD line: the gate is met, one item is due, or several are.</summary>
public enum NoThemeWeekMessage
{
    GateMet,
    OneItemDue,
    ItemsDue,
}

/// <summary>A line's reason plus the themes it names (empty when no theme is involved).</summary>
public sealed record GateLineVerdict(UnaskableReason Reason, IReadOnlyList<Theme> Themes);

/// <summary>Pure helpers for the short-offer Warn log and the no-theme week's HUD message.</summary>
public static class GateLineDiagnosis
{
    private const int OneItem = 1;

    /// <summary>Classify one unmet gate line. <paramref name="poolThemes"/> hold the line in their
    /// open pool, <paramref name="sampleThemes"/> would ask for it this week; a pooled line skips
    /// the obtainability signal (a stretch line is pooled before its model week).</summary>
    public static GateLineVerdict Classify(
        GoalBlock block,
        IReadOnlyCollection<Theme> poolThemes, IReadOnlyCollection<Theme> sampleThemes,
        IReadOnlyCollection<Theme> offer, IReadOnlyCollection<Theme> pickedThisMonth)
    {
        poolThemes ??= Array.Empty<Theme>();
        sampleThemes ??= Array.Empty<Theme>();
        offer ??= Array.Empty<Theme>();
        pickedThisMonth ??= Array.Empty<Theme>();

        if (sampleThemes.Any(offer.Contains))
            return Verdict(UnaskableReason.Askable, sampleThemes.Where(offer.Contains));
        if (poolThemes.Count == 0)
            return block switch
            {
                GoalBlock.NotPlaced => Verdict(UnaskableReason.Unreachable),
                GoalBlock.OutOfSeason or GoalBlock.NotYet => Verdict(UnaskableReason.NotObtainableThisWeek),
                _ => Verdict(UnaskableReason.NoThemeCovers),
            };
        if (sampleThemes.Count == 0)
            return Verdict(UnaskableReason.LeftOutOfList, poolThemes);
        List<Theme> unpicked = sampleThemes.Where(t => !pickedThisMonth.Contains(t)).ToList();
        return unpicked.Count == 0
            ? Verdict(UnaskableReason.ThemesPickedThisMonth, sampleThemes)
            : Verdict(UnaskableReason.ThemeNotDealt, unpicked);
    }

    /// <summary>The log text for a verdict (English, log only).</summary>
    public static string Describe(GateLineVerdict verdict)
    {
        if (verdict == null) throw new ArgumentNullException(nameof(verdict));
        string themes = string.Join(", ", verdict.Themes ?? Array.Empty<Theme>());
        return verdict.Reason switch
        {
            UnaskableReason.Askable => $"asked by {themes}",
            UnaskableReason.NotObtainableThisWeek => "not obtainable this week (out of season or before its goal week)",
            UnaskableReason.Unreachable => "unreachable (the availability model never placed it)",
            UnaskableReason.NoThemeCovers => "no theme covers it",
            UnaskableReason.LeftOutOfList => $"in the pool of {themes}, but left out of this week's list (caps or budget)",
            UnaskableReason.ThemesPickedThisMonth => $"only {themes} could ask for it, already picked this month and not dealt again",
            UnaskableReason.ThemeNotDealt => $"{themes} could ask for it but was not dealt this week",
            _ => verdict.Reason.ToString(),
        };
    }

    /// <summary>How many more items the season gate needs before its day 28: the sum of every
    /// bundle's <see cref="BundleRequirement.MissingForSeason"/> count.</summary>
    public static int ItemsDue(IEnumerable<BundleRequirement> requirements, Season season, SlotLedger ledger)
    {
        if (ledger == null) throw new ArgumentNullException(nameof(ledger));
        int due = 0;
        foreach (BundleRequirement req in requirements ?? Array.Empty<BundleRequirement>())
            due += req.MissingForSeason(season, ledger).Count;
        return due;
    }

    /// <summary>Which HUD line a week with no theme at all shows, from the items still due.</summary>
    public static NoThemeWeekMessage NoThemeWeekMessageFor(int itemsDue) => itemsDue switch
    {
        <= 0 => NoThemeWeekMessage.GateMet,
        OneItem => NoThemeWeekMessage.OneItemDue,
        _ => NoThemeWeekMessage.ItemsDue,
    };

    private static GateLineVerdict Verdict(UnaskableReason reason, IEnumerable<Theme>? themes = null)
        => new(reason, (themes ?? Array.Empty<Theme>()).Distinct().OrderBy(t => (int)t).ToList());
}
