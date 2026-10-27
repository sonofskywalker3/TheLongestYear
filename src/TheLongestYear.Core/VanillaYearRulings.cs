using System;
using System.Collections.Generic;

namespace TheLongestYear.Core;

public enum YearRuling { YearOne, YearTwo, NeverSell }

/// <summary>Hand-checked year rulings for vanilla items the obtainability model cannot read: routes
/// that live in game code rather than data (volcano and mine chests, Mr. Qi's and the volcano
/// dwarf's shops, event and quest rewards, tailoring), checked against the decompiled 1.6 source
/// (2026-10-27). <see cref="YearRuling.NeverSell"/> marks items no shop should offer: currencies and
/// placeholders. Mod items never appear here; they go by the model's rule alone.</summary>
public static class VanillaYearRulings
{
    private static readonly IReadOnlyDictionary<string, YearRuling> Rulings =
        new Dictionary<string, YearRuling>(StringComparer.Ordinal)
        {
        };

    public static YearRuling? Ruling(string qualifiedId)
        => Rulings.TryGetValue(qualifiedId, out YearRuling r) ? r : null;
}
