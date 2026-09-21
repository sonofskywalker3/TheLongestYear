using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Which season turn a morning is, and what its Junimo scene needs
/// (spec 2026-09-07-season-turn-beats). Summer is the Spring-to-Summer turn, and so on.</summary>
public enum SeasonTurnKind { Summer, Fall, Winter }

public static class SeasonTurn
{
    public const string KeyPrefix = "event.turn.";

    public static SeasonTurnKind? ForSeasonStart(Season season) => season switch
    {
        Season.Summer => SeasonTurnKind.Summer,
        Season.Fall => SeasonTurnKind.Fall,
        Season.Winter => SeasonTurnKind.Winter,
        _ => null,
    };

    /// <summary>Two at the hopeful turn, three at the uneasy one, four at the alarmed one.</summary>
    public static int JunimoCount(SeasonTurnKind kind) => kind switch
    {
        SeasonTurnKind.Summer => 2,
        SeasonTurnKind.Fall => 3,
        _ => 4,
    };

    /// <summary>The scene's lines in order: which Junimo (0 = the green voice) says which i18n key.
    /// Only the Summer closer changes once the save has been rewound (spec 2026-09-21).</summary>
    public static IReadOnlyList<(int Junimo, string Key)> Lines(SeasonTurnKind kind, bool rewound) => kind switch
    {
        SeasonTurnKind.Summer => new[] { (0, KeyPrefix + "summer-1"), (1, KeyPrefix + "summer-2"), (0, KeyPrefix + (rewound ? "summer-3-again" : "summer-3")) },
        SeasonTurnKind.Fall => new[] { (0, KeyPrefix + "fall-1"), (1, KeyPrefix + "fall-2"), (2, KeyPrefix + "fall-3") },
        _ => new[] { (0, KeyPrefix + "winter-1"), (1, KeyPrefix + "winter-2"), (2, KeyPrefix + "winter-3"), (0, KeyPrefix + "winter-4") },
    };

    /// <summary>Every key any variant can return, for the i18n guard.</summary>
    public static IReadOnlyList<string> AllLineKeys { get; } = new[] { false, true }
        .SelectMany(r => Enum.GetValues<SeasonTurnKind>().SelectMany(k => Lines(k, r).Select(l => l.Key)))
        .Distinct().ToArray();

    public static string SeenName(SeasonTurnKind kind) => kind.ToString();

    /// <summary>A turn the save has already watched once may be skipped.</summary>
    public static bool IsSkippable(SeasonTurnKind kind, IReadOnlySet<string> seen)
        => seen != null && seen.Contains(SeenName(kind));

    public static bool TryParse(string text, out SeasonTurnKind kind)
        => Enum.TryParse(text, ignoreCase: true, out kind) && Enum.IsDefined(typeof(SeasonTurnKind), kind);
}
