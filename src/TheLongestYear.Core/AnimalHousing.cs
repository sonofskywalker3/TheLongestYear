namespace TheLongestYear.Core;

/// <summary>Building type to (family, tier) for the kept-building chains. A Coop must never satisfy a
/// Cow and vice versa: the family segregates the chains, and tier comparison only happens within a
/// family. Silo is a one-tier family so the kept-building spot snapshot can key it (animals never
/// ask for it). Moved from WorldResetService.ChainInfo so the Herd Book placement can use it.</summary>
public static class AnimalHousing
{
    public const string CoopFamily = "coop";
    public const string BarnFamily = "barn";
    public const string SiloFamily = "silo";

    public static (string Family, int Tier) Chain(string? blueprint) => blueprint switch
    {
        "Coop" => (CoopFamily, 1),
        "Big Coop" => (CoopFamily, 2),
        "Deluxe Coop" => (CoopFamily, 3),
        "Barn" => (BarnFamily, 1),
        "Big Barn" => (BarnFamily, 2),
        "Deluxe Barn" => (BarnFamily, 3),
        "Silo" => (SiloFamily, 1),
        _ => ("", 0),
    };
}
