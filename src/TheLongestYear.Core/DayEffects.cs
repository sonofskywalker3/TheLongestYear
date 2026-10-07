namespace TheLongestYear.Core;

/// <summary>Today's wildcard twist as a process-wide channel, like ActiveEffectsProvider: the
/// day start sets it from RunState, patches read it through <see cref="Has"/>. Never the source
/// of truth (RunState is); cleared on a reset and when TLY goes dormant.</summary>
public static class DayEffects
{
    private static string? _today;

    public static string? Today => _today;

    public static void Set(string? twistId) => _today = twistId;

    public static void Clear() => _today = null;

    public static bool Has(string id) => RunActivation.IsActive && _today == id;
}
