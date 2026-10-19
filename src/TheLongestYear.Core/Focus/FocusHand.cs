using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>One theme's dealt hand and what became of it. A plain get/set shape so the MetaStore
/// JSON round-trips it.</summary>
public sealed class FocusHand
{
    public FocusTheme Theme { get; set; }

    /// <summary>The chain ids dealt (at most three; empty when no chain of the theme was feasible).</summary>
    public List<string> Chains { get; set; } = new();

    /// <summary>The chain the player picked, or null until they pick. Final once set.</summary>
    public string? Chosen { get; set; }

    public bool Bought { get; set; }

    public string? BoughtUpgradeId { get; set; }
}
