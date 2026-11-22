using System.Collections.Generic;
using System.Text;

namespace TheLongestYear.Core;

/// <summary>What the screen reader should say after one look at the open menu.</summary>
public enum NarrationCue
{
    /// <summary>Nothing changed.</summary>
    None,
    /// <summary>A different menu is up: say its intro, then its focused item.</summary>
    MenuOpened,
    /// <summary>Same menu, the focus moved or its content changed: say the focused item.</summary>
    FocusChanged,
}

/// <summary>Remembers the last menu and focus the screen reader spoke, so a menu polled every
/// tick is spoken once per change.</summary>
public sealed class NarrationTracker
{
    private object? _menu;
    private object? _focus;

    /// <param name="menu">The menu to narrate, or null when none is up.</param>
    /// <param name="focusKey">A cheap value that changes whenever the focused item's text would.</param>
    public NarrationCue Observe(object? menu, object? focusKey)
    {
        if (menu == null)
        {
            _menu = null;
            _focus = null;
            return NarrationCue.None;
        }
        if (!ReferenceEquals(menu, _menu))
        {
            _menu = menu;
            _focus = focusKey;
            return NarrationCue.MenuOpened;
        }
        if (!Equals(focusKey, _focus))
        {
            _focus = focusKey;
            return NarrationCue.FocusChanged;
        }
        return NarrationCue.None;
    }
}

/// <summary>Builds spoken text from the pieces a menu draws.</summary>
public static class NarrationText
{
    private const string SentenceEnds = ".!?:";

    /// <summary>Joins the non-empty parts into one line, each ending as a sentence so the screen
    /// reader pauses between them. Each line of a part is its own sentence (a tooltip's name,
    /// description and cost), so pass text that has not been wrapped to a width.</summary>
    public static string Sentences(IEnumerable<string?> parts)
    {
        var text = new StringBuilder();
        foreach (string? raw in parts)
        {
            foreach (string line in (raw ?? "").Split('\n'))
            {
                string part = line.Trim();
                if (part.Length == 0) continue;
                if (text.Length > 0) text.Append(' ');
                text.Append(part);
                if (SentenceEnds.IndexOf(part[part.Length - 1]) < 0) text.Append('.');
            }
        }
        return text.ToString();
    }

    /// <inheritdoc cref="Sentences(IEnumerable{string})"/>
    public static string Sentences(params string?[] parts) => Sentences((IEnumerable<string?>)parts);
}
