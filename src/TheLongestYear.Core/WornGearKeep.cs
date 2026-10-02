namespace TheLongestYear.Core;

/// <summary>Keep Worn Gear (spec 2026-10-01, promised to sarahwinchester97): the boots, both rings
/// and the trinket(s) worn at the rewind stay worn. The Farmer object survives the in-place reset,
/// so FarmerReset just leaves the slots alone and the very instances (Combined Rings, tailored boots,
/// trinket stats) carry over. Only what is worn: spare rings and boots still need stash slots.</summary>
public static class WornGearKeep
{
    public const string UpgradeId = "keep_worn_gear";
    public const long Cost = 1000;

    /// <summary>The stat that shows the trinket slot (vanilla sets it to 1 at the Combat mastery
    /// claim). StatResetRules wipes it each loop; a kept trinket needs it back or it is worn but
    /// invisible and cannot be taken off.</summary>
    public const string TrinketSlotsStat = "trinketSlots";
}
