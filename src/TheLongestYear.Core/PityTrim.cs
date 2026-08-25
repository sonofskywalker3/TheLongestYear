namespace TheLongestYear.Core;

/// <summary>Reshuffle-path season pity (spec 2026-08-25 section 3): trim <see cref="Units"/>
/// hardness units from the slot pools of bundles that feed <see cref="Season"/>'s gate.</summary>
public sealed record PityTrim(Season Season, int Units);
