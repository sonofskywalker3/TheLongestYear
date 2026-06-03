namespace TheLongestYear.Core;

/// <summary>
/// Captured state of the player's horse + stable for the "Keep Horse" upgrade (id <c>early_horse</c>).
/// Persisted in <see cref="MetaState.HorseState"/> so the stable is rebuilt at the SAME tile each
/// loop and the horse keeps its name and hat. Null when the player hasn't built a stable yet — the
/// upgrade is pure carry-over (no auto-build), so a fresh run has no horse until the player builds
/// one, after which it persists where they put it.
/// </summary>
/// <param name="StableTileX">Stable building tile X on the Farm.</param>
/// <param name="StableTileY">Stable building tile Y on the Farm.</param>
/// <param name="HorseName">Player-given horse name, restored verbatim.</param>
/// <param name="HatQualifiedItemId">Qualified item id of the horse's hat, or null if bare-headed.</param>
public sealed record HorseSnapshot(
    int StableTileX,
    int StableTileY,
    string HorseName,
    string HatQualifiedItemId);
