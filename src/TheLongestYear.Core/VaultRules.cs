namespace TheLongestYear.Core;

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

    /// <summary>True if the player has satisfied this season's vault gate (paid the bundle this run,
    /// or owns the keep_bus_unlocked meta upgrade).</summary>
    public static bool IsVaultGateSatisfied(Season season, RunState run, MetaState meta)
    {
        if (meta.HasUpgrade(KeepBusUnlockedId))
            return true;
        return run.VaultBundlesPaid.Contains(BundleIndexForSeason(season));
    }
}
