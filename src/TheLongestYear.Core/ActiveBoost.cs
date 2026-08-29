namespace TheLongestYear.Core;

/// <summary>One bought boost on the run record. Persisted in RunState (Newtonsoft via SMAPI's
/// Data API), so plain settable properties. Days are day-of-year (Calendar.DayOfYear).
/// Spec docs/superpowers/specs/2026-08-29-shrine-tabs-jp-boosts-design.md section 1.2.</summary>
public sealed class ActiveBoost
{
    public string Id { get; set; } = "";
    public int BoughtDay { get; set; }
    /// <summary>Last day of year the boost is active, inclusive. 112 for Loop rows.</summary>
    public int ExpiresAfterDay { get; set; }
    /// <summary>Crash Course only: the skill index bought (Farming 0, Fishing 1, Foraging 2, Mining 3, Combat 4).</summary>
    public int Skill { get; set; } = -1;

    public bool IsActiveOn(int dayOfYear) => BoughtDay <= dayOfYear && dayOfYear <= ExpiresAfterDay;
}
