using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

public static partial class ItemPoolBuilder
{
    private const string ForageItemTag = "forage_item";
    private const string TruffleId = "(O)430";
    private static readonly int[] ForageCategories = { -79, -80, -81, -75, -23 };

    /// <summary>Vanilla's standard Dye bundle: Red Mushroom, Sea Urchin, Sunflower, Duck
    /// Feather, Aquamarine, Red Cabbage.</summary>
    private static readonly IReadOnlySet<string> VanillaDyeItems = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)420", "(O)397", "(O)421", "(O)444", "(O)62", "(O)266",
    };

    /// <summary>The six gems the mines' gem nodes drop (Emerald, Aquamarine, Ruby, Amethyst, Topaz,
    /// Jade) plus three common mine crystals (Quartz, Fire Quartz, Frozen Tear). Earth Crystal is
    /// copper, the game's orange, and Dye has no orange slot, so it is left out (Jeff, 2026-09-29).
    /// Diamond and Prismatic Shard are rare and stay out, as do geode minerals (Jeff, 2026-09-29).
    /// The deeper crystals still wait for their floors: the board's availability model places them.</summary>
    private static readonly IReadOnlySet<string> CommonGems = new HashSet<string>(StringComparer.Ordinal)
    {
        "(O)60", "(O)62", "(O)64", "(O)66", "(O)68", "(O)70",
        "(O)80", "(O)82", "(O)84",
    };

    /// <summary>Whether the Dye recipe may pick this object: a vanilla Dye item, a common gem, or
    /// something grown or gathered (the game's own forage test: crops, fruit, flowers, forage,
    /// beach finds).</summary>
    private static bool IsDyeCandidate(string qualifiedId, RawObjectEntry obj)
        => VanillaDyeItems.Contains(qualifiedId) || CommonGems.Contains(qualifiedId) || IsForageCategory(obj, qualifiedId);

    /// <summary>Mirrors StardewValley.Object.isForage(): the only objects the game gives
    /// forage quality to when picked up.</summary>
    public static bool IsForageCategory(RawObjectEntry obj, string qualifiedId)
        => Array.IndexOf(ForageCategories, obj.Category) >= 0
           || (obj.ContextTags != null && obj.ContextTags.Contains(ForageItemTag))
           || qualifiedId == TruffleId;

    private const string FishNonFishTag = "fish_nonfish";
    private const string CountsAsFishCatchTag = "counts_as_fish_catch";

    /// <summary>A jelly in Data/Objects terms: a rod catch the game counts as a fish catch
    /// ("counts_as_fish_catch") but marks as not a fish ("fish_nonfish"). In vanilla 1.6 that is
    /// exactly Sea, River and Cave Jelly; Seaweed and the algae are "fish_nonfish" only. A modded
    /// jelly that copies vanilla's tags is caught the same way (Jeff, 2026-10-05).</summary>
    public static bool IsJellyCatch(RawObjectEntry obj)
        => obj.ContextTags != null
           && obj.ContextTags.Contains(FishNonFishTag)
           && obj.ContextTags.Contains(CountsAsFishCatchTag);

    /// <summary>River/Sea/Cave Jelly are rod catches that never carry quality.</summary>
    public static bool IsJelly(string qualifiedId)
        => Unqualify(qualifiedId).EndsWith("Jelly", StringComparison.Ordinal);

    private static IReadOnlySet<string> BuildQualityEligibleIds(
        IReadOnlyList<RawCropEntry> crops,
        IReadOnlyDictionary<string, RawObjectEntry> objects,
        IReadOnlyList<RawSpawnEntry> forageSpawns,
        IReadOnlyList<RawSpawnEntry> fishSpawns,
        IReadOnlySet<string> trapFishIds,
        HashSet<string> excluded)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (RawCropEntry crop in crops)
        {
            if (string.IsNullOrEmpty(crop.HarvestItemId)) continue;
            if (crop.HarvestMaxQuality == 0) continue; // CropData clamps to base quality (e.g. Fiber)
            string bare = Unqualify(crop.HarvestItemId);
            string id = Qualify(bare);
            if (Vets(bare, id, objects, excluded)) result.Add(id);
        }
        foreach (RawSpawnEntry spawn in fishSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId) || IsSpecialOrderGated(spawn.Condition)) continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded)) continue;
            if (!objects.TryGetValue(bare, out RawObjectEntry? obj)
                || !string.Equals(obj.Type, FishType, StringComparison.OrdinalIgnoreCase)) continue;
            if (trapFishIds.Contains(bare) || IsJelly(id)) continue;
            result.Add(id);
        }
        foreach (RawSpawnEntry spawn in forageSpawns)
        {
            if (string.IsNullOrEmpty(spawn.ItemId) || IsSpecialOrderGated(spawn.Condition)) continue;
            string bare = Unqualify(spawn.ItemId);
            string id = Qualify(bare);
            if (!Vets(bare, id, objects, excluded)) continue;
            if (objects.TryGetValue(bare, out RawObjectEntry? obj) && IsForageCategory(obj, id))
                result.Add(id);
        }
        return result;
    }
}
