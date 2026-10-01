namespace TheLongestYear.Core;

/// <summary>Fires at most once per interval, so a repeated refusal (a player clicking the same
/// container at the stash again and again, or two hooks seeing one deposit) shows one HUD line.
/// The clock is passed in so the rule is testable.</summary>
public sealed class HudThrottle
{
    private readonly long _intervalMs;
    private long? _lastFiredMs;

    public HudThrottle(long intervalMs) => _intervalMs = intervalMs;

    /// <summary>True (and remembers the time) when nothing fired within the interval before nowMs.</summary>
    public bool TryFire(long nowMs)
    {
        if (_lastFiredMs.HasValue && nowMs - _lastFiredMs.Value < _intervalMs && nowMs >= _lastFiredMs.Value)
            return false;
        _lastFiredMs = nowMs;
        return true;
    }
}
