namespace TheLongestYear.Core;

/// <summary>
/// Vault (bus-repair) gate rules. Each season requires a cumulative, tier-agnostic minimum number
/// of vanilla 1.6 Vault money bundles paid THIS run: at least the season ordinal (Spring 1,
/// Summer 2, Fall 3, Winter 4). Paying all four in Spring pre-satisfies every season. The
/// keep_bus_unlocked Buildings upgrade short-circuits the gate (bus stays restored across runs).
///
/// Vanilla indices (Data/Bundles "Vault/N"):
///   34 = 2,500g · 35 = 5,000g · 36 = 10,000g · 37 = 25,000g  (42,500g total)
/// </summary>
public static class VaultRules
{
    /// <summary>Upgrade id that, when owned, satisfies the vault gate every season.</summary>
    public const string KeepBusUnlockedId = "keep_bus_unlocked";

    public const int Vault2500   = 34;
    public const int Vault5000   = 35;
    public const int Vault10000  = 36;
    public const int Vault25000  = 37;

    /// <summary>The four vanilla vault bundle indices, low tier to high.</summary>
    public static readonly int[] VaultIndices = { Vault2500, Vault5000, Vault10000, Vault25000 };

    /// <summary>1-based count of vault bundles required by the given season's day-28 checkpoint
    /// (Spring 1 … Winter 4).</summary>
    public static int SeasonOrdinal(Season season) => (int)season + 1;

    /// <summary>True if <paramref name="index"/> is one of the four vault bundle indices.</summary>
    public static bool IsVaultIndex(int index) => index >= Vault2500 && index <= Vault25000;

    /// <summary>The gold price of a given vault bundle index (drives the JP scaling).</summary>
    public static int GoldForIndex(int index) => index switch
    {
        Vault2500  => 2500,
        Vault5000  => 5000,
        Vault10000 => 10000,
        Vault25000 => 25000,
        _ => 0
    };

    /// <summary>Number of distinct vault bundles paid this run.</summary>
    public static int PaidCount(RunState run) => run.VaultBundlesPaid.Count;

    /// <summary>Which vault bundle gates a season's monthly checkpoint. Kept for the tly_payvault
    /// debug command (resolves a season name to an index); the live gate is count-based and does
    /// NOT use this.</summary>
    public static int BundleIndexForSeason(Season season) => season switch
    {
        Season.Spring => Vault2500,
        Season.Summer => Vault5000,
        Season.Fall   => Vault10000,
        Season.Winter => Vault25000,
        _ => -1
    };

    /// <summary>True if the player has satisfied this season's vault gate: owns keep_bus_unlocked,
    /// or has paid at least <see cref="SeasonOrdinal"/> vault bundles this run (any tiers).</summary>
    public static bool IsVaultGateSatisfied(Season season, RunState run, MetaState meta)
    {
        if (meta.HasUpgrade(KeepBusUnlockedId))
            return true;
        return run.VaultBundlesPaid.Count >= SeasonOrdinal(season);
    }
}
