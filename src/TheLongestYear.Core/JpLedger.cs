namespace TheLongestYear.Core;

/// <summary>Where a loop's Junimo Points came from, as the loop recap groups them.</summary>
public enum JpSource
{
    /// <summary>Item donations, bundle completion bonuses and Vault payments.</summary>
    Bundles,
    Rooms,
    WeeklyThemes,
    ShrineGoals,
    Checkpoints,
}

/// <summary>JP earned this loop, by source (spec 2026-11-10 shrine planning, amendment F). Only
/// earnings are recorded: refunds, respec hand-overs and debug grants are not. Cleared only by
/// <see cref="RunState.BeginNewRun"/>.</summary>
public sealed class JpLedger
{
    public long Bundles { get; set; }
    public long Rooms { get; set; }
    public long WeeklyThemes { get; set; }
    public long ShrineGoals { get; set; }
    public long Checkpoints { get; set; }

    /// <summary>The ledger started part way through the loop (a save from before the ledger), so
    /// it covers only what was earned since the update.</summary>
    public bool SinceUpdate { get; set; }

    public long Of(JpSource source) => source switch
    {
        JpSource.Bundles => Bundles,
        JpSource.Rooms => Rooms,
        JpSource.WeeklyThemes => WeeklyThemes,
        JpSource.ShrineGoals => ShrineGoals,
        JpSource.Checkpoints => Checkpoints,
        _ => 0,
    };

    public long Total() => Bundles + Rooms + WeeklyThemes + ShrineGoals + Checkpoints;

    public void Add(JpSource source, long amount)
    {
        if (amount <= 0) return;
        switch (source)
        {
            case JpSource.Bundles: Bundles += amount; break;
            case JpSource.Rooms: Rooms += amount; break;
            case JpSource.WeeklyThemes: WeeklyThemes += amount; break;
            case JpSource.ShrineGoals: ShrineGoals += amount; break;
            case JpSource.Checkpoints: Checkpoints += amount; break;
        }
    }

    public JpLedger Copy() => new()
    {
        Bundles = Bundles,
        Rooms = Rooms,
        WeeklyThemes = WeeklyThemes,
        ShrineGoals = ShrineGoals,
        Checkpoints = Checkpoints,
        SinceUpdate = SinceUpdate,
    };
}

public sealed partial class RunState
{
    /// <summary>This loop's JP earnings by source. Null on a save from before the ledger: the
    /// first credit then starts a ledger marked <see cref="JpLedger.SinceUpdate"/>. A brand-new
    /// run state gets a full ledger from <see cref="StartJpLedger"/> or <see cref="BeginNewRun"/>.</summary>
    public JpLedger? JpEarned { get; set; }

    /// <summary>Nights this loop a blight strike actually landed (crops or chests).</summary>
    public int LoopBlightNights { get; set; }

    /// <summary>A full ledger from the start of a loop.</summary>
    public void StartJpLedger() => JpEarned = new JpLedger();

    /// <summary>Record JP the player just earned. Nothing for zero or less.</summary>
    public void RecordJpEarned(JpSource source, long amount)
    {
        if (amount <= 0) return;
        (JpEarned ??= new JpLedger { SinceUpdate = true }).Add(source, amount);
    }

    /// <summary>What the recap reads: a copy of the ledger, or an empty "since the update" one.</summary>
    public JpLedger JpEarnedSnapshot() => JpEarned?.Copy() ?? new JpLedger { SinceUpdate = true };
}
