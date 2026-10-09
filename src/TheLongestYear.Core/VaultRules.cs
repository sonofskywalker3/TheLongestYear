using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// Vault (bus-repair) gate rules. Each season requires a cumulative, tier-agnostic minimum number
/// of vanilla 1.6 Vault money bundles paid THIS run: at least the season ordinal (Spring 1,
/// Summer 2, Fall 3, Winter 4). Paying all four in Spring pre-satisfies every season. The
/// keep_bus_unlocked Buildings upgrade short-circuits the gate (bus stays restored across runs).
///
/// Canonical NON-REMIXED indices (Data/Bundles "Vault/N", unmodded 1.6.15):
///   23 = 2,500g · 24 = 5,000g · 25 = 10,000g · 26 = 25,000g  (42,500g total)
/// (These were 34 to 37 until 2026-10-09, a pre-1.6 numbering; VaultRulesTests now checks them
/// against VanillaBundleBoard.Standard.)
/// Remixed-bundle saves renumber these, so the live indices/gold are resolved at runtime from the
/// save's own bundle data by <c>TheLongestYear.Integration.VaultBundleMap</c>; the constants below
/// are the canonical layout + the fallback that map uses when bundle data is unavailable. The gate
/// itself (<see cref="IsVaultGateSatisfied"/>) is count-based, so it is index-agnostic.
/// </summary>
public static class VaultRules
{
    /// <summary>Upgrade id that, when owned, satisfies the vault gate every season.</summary>
    public const string KeepBusUnlockedId = "keep_bus_unlocked";

    public const int Vault2500   = 23;
    public const int Vault5000   = 24;
    public const int Vault10000  = 25;
    public const int Vault25000  = 26;

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

    /// <summary>The Vault bundles a board has unless the bundle-count dial changed it (Easy 3,
    /// Hard 5, Extreme 6 on a TLY Custom board; see <see cref="VaultBundleCount"/>).</summary>
    public const int StandardVaultCount = 4;

    /// <summary>Vault bundles this season's checkpoint asks for on a board with
    /// <paramref name="vaultCount"/> of them: the season ordinal, but never more than the board has
    /// (an Easy board has three, so Winter asks for three). A count of 0 or less means unknown and
    /// asks for the ordinal.</summary>
    public static int RequiredPaid(Season season, int vaultCount)
        => vaultCount > 0 ? System.Math.Min(SeasonOrdinal(season), vaultCount) : SeasonOrdinal(season);

    /// <summary>The reach metric of keep_bus_unlocked: a bare yes/no flag ("bus"), met by
    /// <see cref="IsBusRepaired"/>.</summary>
    public const string BusReachMetric = "bus";

    /// <summary>Keep Bus Unlocked's reach (Jeff, 2026-10-09): met once EVERY Vault bundle on this
    /// board is paid this run, or the Vault room is repaired, whichever comes first. Replaces the
    /// old "4 paid" count, which opened the gift before the bus on a Hard or Extreme board (5 or 6
    /// Vault bundles) and needed an Easy special case (3). An empty board list is never "all
    /// paid"; only <paramref name="vaultRoomComplete"/> can meet it then.</summary>
    public static bool IsBusRepaired(IEnumerable<int> paidThisRun, IReadOnlyCollection<int> boardVaultIndices, bool vaultRoomComplete)
    {
        if (vaultRoomComplete)
            return true;
        if (boardVaultIndices == null || boardVaultIndices.Count == 0)
            return false;
        var paid = new HashSet<int>(paidThisRun ?? System.Array.Empty<int>());
        foreach (int index in boardVaultIndices)
            if (!paid.Contains(index))
                return false;
        return true;
    }

    /// <summary>Number of distinct vault bundles paid this run.</summary>
    public static int PaidCount(RunState run) => run.VaultBundlesPaid.Count;

    /// <summary>True if the player has satisfied this season's vault gate: owns keep_bus_unlocked,
    /// or has paid at least <see cref="RequiredPaid"/> vault bundles this run (any tiers).
    /// <paramref name="vaultCount"/> is the live board's Vault bundle count.</summary>
    public static bool IsVaultGateSatisfied(Season season, RunState run, MetaState meta, int vaultCount = StandardVaultCount)
    {
        if (meta.HasUpgrade(KeepBusUnlockedId))
            return true;
        return run.VaultBundlesPaid.Count >= RequiredPaid(season, vaultCount);
    }
}
