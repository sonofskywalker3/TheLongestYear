namespace TheLongestYear.Core;

/// <summary>
/// How a vanilla CC bundle gates against the seasonal checkpoints (spec 2026-05-26 round 7).
/// </summary>
public enum BundleKind
{
    /// <summary>
    /// X == Y, named after a season (e.g. Spring Foraging Bundle). All ingredients must be
    /// donated by the bundle's named-season day 28. Off-season days short-circuit (not yet due).
    /// </summary>
    Seasonal,

    /// <summary>
    /// X == Y, non-seasonal (e.g. Blacksmith's: Copper/Iron/Gold all required, but at different
    /// progression tiers). Each ingredient is pinned to a specific season; the gate fails when
    /// any pinned-by-this-or-earlier-season ingredient hasn't been donated.
    /// </summary>
    PerItem,

    /// <summary>
    /// X &lt; Y (e.g. Artisan: 6 of 12). The player has choice of which X to donate. The gate is a
    /// cumulative-count quota by season (e.g. {1,2,4,6} for Artisan): by Spring 28 at least 1
    /// of the 12 must be donated; by Summer 28 at least 2; by Fall 28 at least 4; by Winter day
    /// 28 (i.e. completion) at least 6.
    /// </summary>
    Percentage
}
