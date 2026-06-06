namespace TheLongestYear.Core;

/// <summary>Tunable Junimo Point values. Serialized as part of the mod config.</summary>
public sealed class JpSettings
{
    public int CommonJp { get; set; } = 1;
    public int UncommonJp { get; set; } = 3;
    public int RareJp { get; set; } = 10;
    public int VeryRareJp { get; set; } = 25;

    /// <summary>
    /// Per-season JP multiplier (Spring..Winter). Spec 2026-05-26 replaces the linear
    /// WeekDepthStep ramp with explicit per-season tiers: roguelite-style acceleration
    /// gives late-season donations meaningfully more value than early-season ones.
    /// Bundle/room completion bonuses also scale by this multiplier.
    /// </summary>
    public double[] SeasonMultipliers { get; set; } = { 1.0, 1.5, 2.5, 4.0 };

    public int BundleCompletionBonus { get; set; } = 15;
    public int RoomCompletionBonus { get; set; } = 60;

    /// <summary>Bonus JP awarded when the player completes the weekly theme quest by
    /// donating every bonus item this week. Scales by season multiplier like the bundle/
    /// room completion bonuses.</summary>
    public int WeeklyQuestCompletionBonus { get; set; } = 30;

    /// <summary>Gold paid to the CC Vault per 1 JP awarded. Vault payments reward JP proportional
    /// to the gold sunk (2,500g→3, 5,000g→5, 10,000g→10, 25,000g→25 at the default rate). Unlike
    /// item/bundle JP this is NOT season-multiplied — gold's value is season-independent.</summary>
    public int VaultGoldPerJp { get; set; } = 1000;

    public int BaseFor(Rarity rarity) => rarity switch
    {
        Rarity.Common => CommonJp,
        Rarity.Uncommon => UncommonJp,
        Rarity.Rare => RareJp,
        Rarity.VeryRare => VeryRareJp,
        _ => 0
    };
}
