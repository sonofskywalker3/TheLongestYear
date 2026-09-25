using System.Collections.Generic;

namespace TheLongestYear.Core;

/// <summary>
/// No animal keep may be bought past the room of the kept buildings (Jeff, 2026-09-25: "you
/// shouldn't allow purchases that will overflow your max space in a kept building"). A
/// "Start with" row or a Herd Book tier is refused when, after the buy, the animals that must
/// live in its family (Coop or Barn) would outnumber the highest kept building of that family.
/// Only further buys are refused: a save already over the limit keeps everything it owns.
/// </summary>
public static class AnimalCapacityRule
{
    /// <summary>Vanilla Data/Buildings MaxOccupants per kept tier (Coop / Big / Deluxe).</summary>
    private const int SmallCapacity = 4;
    private const int BigCapacity = 8;
    private const int DeluxeCapacity = 12;

    /// <summary>Keep id to the room it gives, highest first per family.</summary>
    private static readonly (string KeepId, string Family, int Capacity)[] KeptBuildings =
    {
        ("keep_deluxe_coop", AnimalHousing.CoopFamily, DeluxeCapacity),
        ("keep_big_coop", AnimalHousing.CoopFamily, BigCapacity),
        ("keep_coop", AnimalHousing.CoopFamily, SmallCapacity),
        ("keep_deluxe_barn", AnimalHousing.BarnFamily, DeluxeCapacity),
        ("keep_big_barn", AnimalHousing.BarnFamily, BigCapacity),
        ("keep_barn", AnimalHousing.BarnFamily, SmallCapacity),
    };

    /// <summary>The kept building room for a family: the highest kept tier's MaxOccupants, 0 if
    /// none of the family is kept.</summary>
    public static int Capacity(MetaState meta, string family)
    {
        foreach (var (keepId, keepFamily, capacity) in KeptBuildings)
            if (keepFamily == family && meta.HasUpgrade(keepId))
                return capacity;
        return 0;
    }

    /// <summary>Animals that must live in a family: owned "Start with" rows housed there plus the
    /// owned Herd Book slots (the free first Chicken slot included) whose kind lives there.</summary>
    public static int Demand(MetaState meta, string family)
    {
        int demand = 0;
        foreach (string startId in RunBaselineBuilder.StartingAnimalIds)
            if (meta.HasUpgrade(startId) && FamilyOf(startId) == family)
                demand++;
        int herdTier = meta.HighestKeptTier(UpgradeCatalog.HerdBookPrefix, UpgradeCatalog.HerdBookMaxTier);
        foreach (HerdSlotKind kind in HerdSlotRules.SlotsFor(herdTier))
            if (AnimalHousing.Chain(HerdSlotRules.RequiredHousing(kind)).Family == family)
                demand++;
        return demand;
    }

    /// <summary>The housing family one more animal lands in when this upgrade is bought, or null
    /// when the upgrade adds no animal (every row but start_* and herdbook_N).</summary>
    public static string? FamilyOf(string upgradeId)
    {
        string? housing = RunBaselineBuilder.StartingAnimalHousing(upgradeId);
        if (housing != null)
            return AnimalHousing.Chain(housing).Family;
        if (upgradeId.StartsWith(UpgradeCatalog.HerdBookPrefix, System.StringComparison.Ordinal)
            && int.TryParse(upgradeId.Substring(UpgradeCatalog.HerdBookPrefix.Length), out int tier)
            && tier >= 1 && tier <= UpgradeCatalog.HerdBookMaxTier)
            return AnimalHousing.Chain(HerdSlotRules.RequiredHousing(HerdSlotRules.KindAt(tier))).Family;
        return null;
    }

    /// <summary>True when buying <paramref name="upgradeId"/> would put more animals in its family
    /// than the kept buildings hold. False for rows that add no animal.</summary>
    public static bool WouldOverflow(MetaState meta, string upgradeId)
    {
        string? family = FamilyOf(upgradeId);
        if (family == null)
            return false;
        return Demand(meta, family) + 1 > Capacity(meta, family);
    }

    /// <summary>The short player-facing reason a row is refused ("Needs a bigger coop"), or null
    /// when the room is there.</summary>
    public static string? BlockReason(MetaState meta, string upgradeId)
    {
        if (!WouldOverflow(meta, upgradeId))
            return null;
        return FamilyOf(upgradeId) == AnimalHousing.CoopFamily
            ? Strings.Get("shrine.no-room.coop")
            : Strings.Get("shrine.no-room.barn");
    }
}
