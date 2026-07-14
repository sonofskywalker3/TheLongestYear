namespace TheLongestYear.Core;

/// <summary>Deterministic per-loop seed for bundle generation: stable for a given
/// (save, loop) pair so reloading or replaying a reset regenerates the IDENTICAL set —
/// no bundle-reroll save-scum (spec 2026-07-14 engine, locked rule). Uses the repo's
/// XOR + prime-salt idiom (see BonusSlotSampler/WeatherScheduler), not HashCode.Combine.</summary>
public static class BundleEngineSeed
{
    private const int LoopSaltPrime = unchecked((int)0x9E3779B1); // golden-ratio prime, as WeatherScheduler

    public static int For(ulong uniqueGameId, int completedResets) =>
        unchecked((int)uniqueGameId ^ ((int)(uniqueGameId >> 32)) ^ (completedResets * LoopSaltPrime));
}
