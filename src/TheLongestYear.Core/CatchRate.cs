using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>How well an item bites: a fact table worked out once in the lexicon's cold build and
/// stored with its answers (Obtainability.FishCatchRates makes it), read by the fish ask sizes.
/// <see cref="Weekly"/> is, per season (Spring..Winter), how many a bundle may build an ask on: the
/// best ten-hour day times seven, rain weighted, or a rainy day times the season's rainy days when
/// that is more (a player after a rain fish waits for rain); 0 = a single ask (under
/// two a week, a legendary, or not caught that season).
/// <see cref="BestDay"/> is the expected catches of the best full day of the year: under
/// two a catch is luck, not a dependable route.</summary>
public sealed record CatchRate(IReadOnlyList<double> Weekly, double BestDay);
