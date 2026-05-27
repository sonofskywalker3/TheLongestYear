using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Snapshot of in-run peak achievements read from the live <c>Farmer</c> just before the
/// reset wipes them. The in-run cap side of cap-not-grant: an owned <c>keep_iron_hoe</c>
/// upgrade only restores Iron next run if <see cref="ToolTiers"/>["hoe"] reached 2 (Steel)
/// or higher this run. (Mine elevator's peak lives on <c>RunState.PeakMineFloor</c> and
/// stays there because it has to survive the reset itself.)
///
/// Pure data — no game refs. The mod-side <c>WorldResetService</c> populates this from
/// <c>Game1.player</c> before clearing the player.
/// </summary>
public sealed class PlayerSnapshot
{
    /// <summary>Tool kind slug → highest <c>Tool.UpgradeLevel</c> the player held this run.
    /// Same slug set as <c>RunBaseline.ToolTiers</c>. Missing key = 0.</summary>
    public IReadOnlyDictionary<string, int> ToolTiers { get; init; }
        = new Dictionary<string, int>();

    /// <summary>Vanilla skill-index (0..4) → highest level the player reached this run.
    /// Missing key = 0.</summary>
    public IReadOnlyDictionary<int, int> SkillLevels { get; init; }
        = new Dictionary<int, int>();

    /// <summary>Convenience: zero-everything snapshot (used in tests and for first-ever
    /// reset where there's no meaningful "peak" yet).</summary>
    public static PlayerSnapshot Empty { get; } = new PlayerSnapshot();
}
