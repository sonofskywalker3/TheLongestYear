using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>How well an item bites: a fact table worked out once in the lexicon's cold build and
/// stored with its answers (Obtainability.FishCatchRates makes it), read by the fish ask sizes.
/// <see cref="Weekly"/> is, per season (Spring..Winter), how many a bundle may build an ask on: the
/// best ten-hour day times seven, rain weighted, or a rainy day times the season's rainy days when
/// that is more (a player after a rain fish waits for rain); 0 = a single ask (under
/// two a week, a legendary, or not caught that season).
/// <see cref="BestDay"/> is the expected catches of the best full day of the year: under
/// two a catch is luck, not a dependable route.
/// <see cref="CatchLimit"/> is set when every row that offers the fish carries a CatchLimit: the game
/// counts that limit by fish, over all its rows (GameLocation.GetFishFromLocationData), so it is the most
/// a loop can catch. Such a fish has no weekly basis, and one limited to a single catch is asked for once
/// (OncePerLoopAsks). Null = no limit.</summary>
public sealed record CatchRate(IReadOnlyList<double> Weekly, double BestDay, int? CatchLimit = null);
