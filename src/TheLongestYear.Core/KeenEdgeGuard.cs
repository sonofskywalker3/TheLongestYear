using System;

namespace TheLongestYear.Core;

/// <summary>
/// The temporary-raise state machine behind Keen Edge. A wrapped call raises a weapon's damage range,
/// and its finalizer puts it back. A call nested inside another wrapped call does nothing, so the range
/// is never raised twice. Only the call that took ownership clears the guard, whether or not it raised
/// anything (a weapon whose real Ruby already covers the tier raises nothing).
/// </summary>
public sealed class KeenEdgeGuard
{
    public readonly record struct State(bool Owner, int Min, int Max);

    private bool _active;

    /// <summary>Compute the extra damage and apply it, unless another wrapped call is already running.</summary>
    public State Begin(Func<(int Min, int Max)> extra, Action<int, int> add)
    {
        if (_active) return default;
        var (min, max) = extra();
        _active = true;
        if (min != 0 || max != 0) add(min, max);
        return new State(true, min, max);
    }

    /// <summary>Undo what <see cref="Begin"/> added and release the guard, if this call owned it.</summary>
    public void End(State state, Action<int, int> remove)
    {
        if (!state.Owner) return;
        _active = false;
        if (state.Min != 0 || state.Max != 0) remove(state.Min, state.Max);
    }
}
