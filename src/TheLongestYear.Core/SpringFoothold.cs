using System;

namespace TheLongestYear.Core;

/// <summary>Spec 2026-08-28-even-year, rule 4: every re-rolled bundle keeps at least one item a
/// Spring gate may demand, so no bundle is invisible to Spring. The predicate must be the same
/// wherever the engine runs (reset, manifest re-check, diagnostics), because the board it
/// produces is compared byte for byte at save load; build it from the availability model and
/// nothing else.</summary>
public static class SpringFoothold
{
    /// <summary>How many of a bundle's picks must be Spring-gated: a quarter, at least one.</summary>
    public static int Needed(int picks) => Math.Max(1, (picks + Calendar.MonthsPerYear - 1) / Calendar.MonthsPerYear);

    public static bool IsSpring(ItemAvailabilityModel? model, string itemId)
        => model != null && itemId != null && model.IsPlaced(itemId) && model.For(itemId).Gate == Season.Spring;

    /// <summary>Null when there is no model (hand-built pools in tests, or a save before the model
    /// exists), which the filler reads as "no foothold rule".</summary>
    public static Func<string, bool>? Predicate(ItemAvailabilityModel? model)
        => model == null ? null : id => IsSpring(model, id);
}
