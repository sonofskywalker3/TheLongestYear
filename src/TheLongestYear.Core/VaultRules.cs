namespace TheLongestYear.Core;

/// <summary>How this season's vault gate is currently satisfied — for UI display.</summary>
public enum VaultGateStatus
{
    /// <summary>Neither paid this run nor covered by the keep upgrade — the gate will fail.</summary>
    Unpaid,
    /// <summary>The matching vault bundle has been paid this run.</summary>
    PaidThisRun,
    /// <summary>The keep_bus_unlocked upgrade is owned, so every season's gate is auto-satisfied.</summary>
    KeptViaUpgrade
}

/// <summary>
/// Maps a season to the vanilla 1.6 Vault bundle index it gates against.
/// The keep_bus_unlocked Buildings upgrade short-circuits this gate (bus stays restored across
/// runs, so vault payments aren't needed again).
///
/// Vanilla indices (Data/Bundles "Vault/N"):
///   34 = 2,500g · 35 = 5,000g · 36 = 10,000g · 37 = 25,000g
/// Total = 42,500g for full bus restoration over the year.
/// </summary>
public static class VaultRules
{
    /// <summary>Upgrade id that, when owned, satisfies the vault gate every season.</summary>
    public const string KeepBusUnlockedId = "keep_bus_unlocked";

    public const int Vault2500   = 34;
    public const int Vault5000   = 35;
    public const int Vault10000  = 36;
    public const int Vault25000  = 37;

    /// <summary>Which vault bundle gates the given season's monthly checkpoint.</summary>
    public static int BundleIndexForSeason(Season season) => season switch
    {
        Season.Spring => Vault2500,
        Season.Summer => Vault5000,
        Season.Fall   => Vault10000,
        Season.Winter => Vault25000,
        _ => -1
    };

    /// <summary>The gold price of the season's vault bundle (2,500 / 5,000 / 10,000 / 25,000g).</summary>
    public static int GoldCostForSeason(Season season) => season switch
    {
        Season.Spring => 2500,
        Season.Summer => 5000,
        Season.Fall   => 10000,
        Season.Winter => 25000,
        _ => 0
    };

    /// <summary>Classifies how (or whether) this season's vault gate is satisfied, for the
    /// green-journal display. The keep upgrade takes precedence over a per-run payment.</summary>
    public static VaultGateStatus DescribeGate(Season season, RunState run, MetaState meta)
    {
        if (meta.HasUpgrade(KeepBusUnlockedId))
            return VaultGateStatus.KeptViaUpgrade;
        return run.VaultBundlesPaid.Contains(BundleIndexForSeason(season))
            ? VaultGateStatus.PaidThisRun
            : VaultGateStatus.Unpaid;
    }

    /// <summary>True if the player has satisfied this season's vault gate (paid the bundle this run,
    /// or owns the keep_bus_unlocked meta upgrade).</summary>
    public static bool IsVaultGateSatisfied(Season season, RunState run, MetaState meta)
    {
        if (meta.HasUpgrade(KeepBusUnlockedId))
            return true;
        return run.VaultBundlesPaid.Contains(BundleIndexForSeason(season));
    }
}
