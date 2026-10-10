using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

public static partial class ItemPoolBuilder
{
    /// <summary>Built-in structural exclusions: Ginger Island / Qi-gated content, which is
    /// post-CC and never year-1 obtainable (Nexus 1122358, 2026-08-24 — engine bundles
    /// rolled these on fresh saves). Location markers can't catch them (crops come from
    /// Data/Crops, which has no location; category pools scan all of Data/Objects), and
    /// they must NOT live only in the tuning defaults: an existing config.json overrides
    /// serialized lists wholesale, so config-default-only excludes silently vanish on
    /// every install that has saved a config. Ids verified against the game's Data/Objects.</summary>
    public static readonly IReadOnlySet<string> BuiltInExcludedItemIds = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)69",  // Banana Sapling
        "(O)835", // Mango Sapling
        "(O)889", // Qi Fruit          — Qi challenge crop (Data/Crops lists all four seasons)
        "(O)832", // Pineapple         — island crop
        "(O)830", // Taro Root         — island crop
        "(O)831", // Taro Tuber        — island seed (Golden Coconut geode drop)
        "(O)833", // Pineapple Seeds   — island seed (Golden Coconut geode drop)
        "(O)91",  // Banana            — island fruit tree
        "(O)834", // Mango             — island fruit tree
        "(O)829", // Ginger            — island forage (also a Golden Coconut drop)
        "(O)851", // Magma Cap         — Volcano forage
        "(O)909", // Radioactive Ore   — island-only (metals pool)
        "(O)910", // Radioactive Bar   — island-only (metals pool)
        "(O)848", // Cinder Shard      — Volcano-only (metals pool)
        "(O)852", // Dragon Tooth      — Volcano-only (Golden Coconut drop)
        "(O)820", // Fossilized Skull  — Golden Coconut drop (island fossil)
        "(O)903", // Ginger Ale        — island dish (cooking pool)
        "(O)904", // Banana Pudding    — island dish
        "(O)905", // Mango Sticky Rice — island dish
        "(O)906", // Poi               — island dish
        "(O)907", // Tropical Curry    — island dish
        "(O)873", // Piña Colada       — island resort drink
        "(O)795", // Void Salmon       — Witch's Swamp only, behind the Dark Talisman quest (post-CC)
        // The five Extended Family fish hang off Mr. Qi's post-CC order (0.16.176 dropped their spawn
        // rows), but a recipe bundle that draws by colour or tag from ALL objects (Dye) never sees a
        // spawn row: board 129 of the 2026-09-04 sweep asked for Son of Crimsonfish. Ban the ids.
        "(O)898", // Son of Crimsonfish
        "(O)899", // Ms. Angler
        "(O)900", // Legend II
        "(O)901", // Radioactive Carp
        "(O)902", // Glacierfish Jr.
        "(O)733", // Shrimp Cocktail   — Queen of Sauce episode 32 (week 16, the year-2 pair of Winter
                  //                     28's Sunday). The Sneak Peek Boost airs year-2 episodes on
                  //                     Wednesday, and there is no Wednesday after Winter 28 inside the
                  //                     run, so no route reaches it (Jeff, 2026-09-21). It is a vanilla
                  //                     Chef's Bundle item, so the ban is what keeps the recipe's own
                  //                     bundle from re-offering it — see AvailabilityWeeks.YearTwoLastReachableEpisode.
        "(O)928", // Golden Egg        — Golden Chickens need Perfection (or Qi's Walnut Room shop), so a
                  //                     one-year loop can never see one. Category Egg (-5) and, unlike Void
                  //                     Egg and Ostrich Egg, NOT flagged ExcludeFromRandomSale, so the vet let
                  //                     it into the Chef's / Animal recipe buckets (Nexus 1127469, gazumbrado:
                  //                     "2 golden eggs which are perfection locked").
        "(O)MysticSyrup", // Mystic Tree only; its seed is the Foraging Mastery recipe (Jeff, 2026-09-30)
    };

    /// <summary>Built-in excluded location markers, merged with the config list by
    /// <see cref="IsExcludedLocation"/> (same config-override rationale as
    /// <see cref="BuiltInExcludedItemIds"/>). BugLand = Mutant Bug Lair: behind the Dark
    /// Talisman quest, which is itself post-CC — never year-1 content. WitchSwamp is behind
    /// the same quest, so Void Salmon is out too (0.12.18; the 2026-08-24 "hard but fair"
    /// ruling assumed the swamp was reachable in year 1, which it is not).
    ///
    /// Island (Ginger Island, and SVE's Custom_DinoIsland event maps), FableReef and
    /// CrimsonBadlands (SVE) used to be config defaults only, so a saved marker list without them
    /// let island fish into the pools (player report 2026-10, paigefromabook: Weatherman's asked
    /// for a Stingray, which is caught only in the Pirate Cove). Checked against the live
    /// Data/Locations (91 keys) and SVE's LocationsData: "Island" matches only the Ginger Island
    /// maps and the two DinoIsland maps, none of them year-1 places.</summary>
    public static readonly IReadOnlyList<string> BuiltInExcludedLocationMarkers =
        new[] { "BugLand", "WitchSwamp", "Island", "FableReef", "CrimsonBadlands" };

    /// <summary>Data/Locations keys that are not places anyone fishes or forages, matched
    /// EXACTLY (case-insensitive) rather than by substring so a modded "Temple" or
    /// "DefaultFarm" map stays in. "Temp" is the Festival of Ice contest map: its rows mix
    /// river and ocean fish (Red Mullet next to Bream) and carry no season, so treating it
    /// as a habitat leaked ocean fish into Lake Fish, river fish into Ocean Fish, and marked
    /// river fish catchable year-round (player report, 2026-08-28). "fishingGame" is the
    /// Fair minigame; "Default" is the trash / Joja Cola table every water shares.</summary>
    public static readonly IReadOnlySet<string> BuiltInNonHabitatLocationKeys =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Default", "Temp", "fishingGame" };

    /// <summary>The vet minus the ExcludeFromRandomSale rule, for Night Market fish (see
    /// BuildFishPools): the flag keeps them out of random shop stock, it does not mean they
    /// cannot be caught.</summary>
    private static bool VetsIgnoringRandomSale(
        string bareId, string qualifiedId,
        IReadOnlyDictionary<string, RawObjectEntry> objects, HashSet<string> excluded)
    {
        if (BuiltInExcludedItemIds.Contains(qualifiedId) || excluded.Contains(qualifiedId))
            return false;
        if (!objects.TryGetValue(bareId, out RawObjectEntry? obj))
            return false;
        if (string.Equals(obj.Type, QuestType, StringComparison.OrdinalIgnoreCase))
            return false;
        return true;
    }

    /// <summary>Structural + configured vetting. False = never offer this item. A
    /// PoolAdditions.VetExceptions id skips the ExcludeFromRandomSale check: those are the
    /// curated mine fish and legendaries, wanted despite the flag (spec 2026-08-28-obtainable-board,
    /// section 3).</summary>
    private static bool IsAnimalProduct(RawObjectEntry obj)
        => ItemKindClassifier.From(obj.Category, obj.Type) is ItemKind.Egg or ItemKind.Milk or ItemKind.AnimalProduct;

    private static bool Vets(
        string bareId, string qualifiedId,
        IReadOnlyDictionary<string, RawObjectEntry> objects, HashSet<string> excluded)
    {
        if (BuiltInExcludedItemIds.Contains(qualifiedId) || excluded.Contains(qualifiedId))
            return false;
        if (!objects.TryGetValue(bareId, out RawObjectEntry? obj))
            return false; // unknown to Data/Objects — can't price/vet it, drop it
        if (string.Equals(obj.Type, QuestType, StringComparison.OrdinalIgnoreCase))
            return false;
        if (obj.ExcludeFromRandomSale && !PoolAdditions.VetExceptions.Contains(qualifiedId))
            return false;
        return true;
    }

    /// <summary>True when a Data/Locations key is a built-in non-habitat key
    /// (<see cref="BuiltInNonHabitatLocationKeys"/>, exact match) or matches any
    /// excluded-location marker — built-in (<see cref="BuiltInExcludedLocationMarkers"/>)
    /// or configured — (case-insensitive substring): such locations never feed the pools.</summary>
    public static bool IsExcludedLocation(string locationKey, IReadOnlyList<string> markers)
    {
        if (BuiltInNonHabitatLocationKeys.Contains(locationKey))
            return true;
        foreach (string marker in BuiltInExcludedLocationMarkers.Concat(markers))
        {
            if (!string.IsNullOrEmpty(marker)
                && locationKey.Contains(marker, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
