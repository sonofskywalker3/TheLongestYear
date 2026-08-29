namespace TheLongestYear.Core;

/// <summary>Which week the availability model answers with (spec 2026-08-28-obtainable-board,
/// section 1). Pacing: Easy and Normal, gates and goals use the pacing week. HardGates: Hard,
/// gates use the hard week, cards stay on pacing. HardAll: Extreme, both use the hard week.</summary>
public enum WeekMode { Pacing, HardGates, HardAll }
