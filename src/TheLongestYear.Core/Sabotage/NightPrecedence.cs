using System;

namespace TheLongestYear.Core.Sabotage;

/// <summary>Where tonight's one strike came from.</summary>
public enum NightSource
{
    /// <summary>No strike tonight.</summary>
    None,

    /// <summary>The guaranteed Winter tamper (spec 2.6).</summary>
    Guaranteed,

    /// <summary>A debug arm (<c>tly_sabotage arm</c>).</summary>
    Armed,

    /// <summary>A strike postponed by a slot collision, queued for the next free night.</summary>
    Queued,

    /// <summary>The every-loop guarantee forcing a kind that has not struck by day 15.</summary>
    Owed,

    /// <summary>The ordinary nightly roll.</summary>
    Roll,
}

/// <summary>Tonight's pick: where it came from and what it is.</summary>
public readonly record struct NightChoice(NightSource Source, DarknessEvent? Event);

/// <summary>Who takes tonight's one strike (review M2): the guaranteed Winter tamper, then a debug
/// arm, then a queued strike, then the every-loop guarantee, then the roll. Each source is a lazy
/// question asked in exactly this order and only when every source before it came back empty,
/// because asking can consume an arm or spend the night's random stream (a reversion plan). The
/// dice are thrown right after the guaranteed attempt, before the arm, as they always were, so a
/// night rolls the same stream it did before the queue existed.</summary>
public static class NightPrecedence
{
    /// <param name="guaranteedNight">Tonight is a guaranteed Winter tamper night.</param>
    /// <param name="guaranteedTakes">Try the guaranteed tamper; true when it took the night (it is
    /// waiting for its scene or has landed), false when it had nothing fair and the night goes on.</param>
    /// <param name="dice">Throw the night's dice: true for a strike.</param>
    /// <param name="armed">The armed kind that can act, consuming every arm.</param>
    /// <param name="queued">The oldest queued kind that can act.</param>
    /// <param name="owed">The every-loop guarantee's kind that can act.</param>
    /// <param name="roll">The season's even pick among the kinds that can act.</param>
    public static NightChoice Choose(
        bool guaranteedNight, Func<bool> guaranteedTakes, Func<bool> dice,
        Func<DarknessEvent?> armed, Func<DarknessEvent?> queued, Func<DarknessEvent?> owed,
        Func<DarknessEvent?> roll)
    {
        if (guaranteedTakes is null) throw new ArgumentNullException(nameof(guaranteedTakes));
        if (dice is null) throw new ArgumentNullException(nameof(dice));
        if (armed is null) throw new ArgumentNullException(nameof(armed));
        if (queued is null) throw new ArgumentNullException(nameof(queued));
        if (owed is null) throw new ArgumentNullException(nameof(owed));
        if (roll is null) throw new ArgumentNullException(nameof(roll));

        if (guaranteedNight && guaranteedTakes()) return new NightChoice(NightSource.Guaranteed, DarknessEvent.Tampering);
        bool strike = dice();
        if (armed() is DarknessEvent a) return new NightChoice(NightSource.Armed, a);
        if (queued() is DarknessEvent q) return new NightChoice(NightSource.Queued, q);
        if (owed() is DarknessEvent o) return new NightChoice(NightSource.Owed, o);
        if (!strike) return new NightChoice(NightSource.None, null);
        return roll() is DarknessEvent r ? new NightChoice(NightSource.Roll, r) : new NightChoice(NightSource.None, null);
    }
}
