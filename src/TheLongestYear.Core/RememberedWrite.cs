using System;

namespace TheLongestYear.Core;

/// <summary>
/// Remembers one value this mod wrote onto a game object, so the write can be undone later without
/// clobbering a value someone else set in the meantime. Used by FestivalTimeFlow for the Spirit's
/// Eve clock (GameLocation.ExtraMillisecondsPerInGameMinute): the undo only fires while the object
/// still holds our value, and taking the undo always drops the reference, so a discarded festival
/// map is not kept alive by a static.
/// </summary>
public sealed class RememberedWrite<T> where T : class
{
    /// <summary>The object last written, or null when nothing is remembered.</summary>
    public T? Target { get; private set; }

    /// <summary>The value written onto <see cref="Target"/>.</summary>
    public int Value { get; private set; }

    /// <summary>Record a write of <paramref name="value"/> onto <paramref name="target"/>.</summary>
    public void Remember(T target, int value)
    {
        Target = target;
        Value = value;
    }

    /// <summary>
    /// Forget the remembered write and return the object the caller should put back to its
    /// neutral value: the target if it still holds our value, else null (someone else changed it,
    /// leave it alone). Always clears the reference.
    /// </summary>
    public T? TakeForUndo(Func<T, int> currentValue)
    {
        T? target = Target;
        int value = Value;
        Target = null;
        Value = 0;
        if (target == null) return null;
        return currentValue(target) == value ? target : null;
    }
}
