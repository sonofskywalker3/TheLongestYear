namespace TheLongestYear.Core;

/// <summary>What a Mixed Seeds planting becomes when another mod picks the seed too.
/// <para>
/// Vanilla rolls the seed when the bag is used (Crop.ResolveSeedId), and this mod's Cultivation
/// upgrades and Year-Two Seeds boost act on that roll. Item Extensions picks a seed of its own from a
/// data pool and puts it in place of the roll as the seed goes into the ground, keeping vanilla's
/// roll only as one slot of the pool. With Cornucopia's 6 seeds a season in that pool, vanilla's
/// roll survives 1 time in 7: Fall's Artichoke Seeds fell from 25% to under 4%, and the upgrades
/// and the boost to a seventh of what they say. Every Fall ask for Artichoke, and both upgrades,
/// are built on the odds vanilla gives.
/// </para>
/// <para>
/// So the roll made when the bag is used stands in three cases: this mod's upgrade or boost changed
/// it, it came up Artichoke Seeds, or the other mod picked its "vanilla roll" slot (which would
/// otherwise roll a second time). In every other case the other mod's pick is planted.
/// </para></summary>
public static class MixedSeedsOutcome
{
    public const string MixedSeedsId = "770";
    /// <summary>Artichoke Seeds, unqualified as the roll returns it.</summary>
    public const string ArtichokeSeedsId = "489";

    /// <param name="incoming">The seed about to be planted, after any other mod changed it.</param>
    /// <param name="rolled">The seed vanilla's roll gave when the bag was used, after this mod's
    /// upgrade or boost.</param>
    /// <param name="substituted">Whether this mod's upgrade or boost changed that roll.</param>
    public static string Final(string incoming, string rolled, bool substituted)
        => substituted || rolled == ArtichokeSeedsId || incoming == MixedSeedsId ? rolled : incoming;
}
