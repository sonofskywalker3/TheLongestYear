using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core;

/// <summary>Keeps other mods' items off a TLY Custom board (spec
/// 2026-10-08-custom-board-vanilla-only). Every function here takes the vanilla id set as a
/// parameter; null means "no filter" and returns the input untouched, which is how the Normal and
/// Remixed paths keep exactly today's behavior.
///
/// Templates (the Data/Bundles and Data/RandomBundles candidates the engine picks from) are
/// filtered slot by slot: a slot naming a non-vanilla item is dropped, a non-vanilla reward is
/// swapped for vanilla's own reward at that key, a candidate left with no slots is dropped, and a
/// position left with no candidates takes vanilla's own standard bundle for that key. A candidate
/// that names only vanilla items and a vanilla reward comes back as the SAME instance, so an
/// unmodded game generates exactly the board it generated before this filter existed.</summary>
public static class VanillaOnlyBoard
{
    private const string MoneySlotId = "-1";
    private const char RewardSeparator = ' ';
    private const int RewardTypeField = 0;
    private const int RewardIdField = 1;
    private const int RewardMinFields = 2;

    /// <summary>The "C" (clothing) reward code: numeric ids at or above this are shirts, below
    /// are pants (decompile, Utility.getItemFromStandardTextDescription).</summary>
    private const int ShirtIdFloor = 1000;
    private const string ClothingCode = "C";
    private const string ShirtPrefix = "(S)";
    private const string PantsPrefix = "(P)";

    /// <summary>Reward for a key vanilla's standard board does not have (a position another mod
    /// added): Speed-Gro x20, Spring Crops' own reward. Only reached with a non-vanilla reward on
    /// a mod-added key.</summary>
    public const string FallbackReward = "O 465 20";

    /// <summary>The reward type codes the game's bundle reward reader accepts, to the qualifier
    /// each creates (decompile, Utility.getItemFromStandardTextDescription). "C" is handled apart.</summary>
    private static readonly IReadOnlyDictionary<string, string> RewardQualifiers =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["O"] = "(O)", ["Object"] = "(O)", ["R"] = "(O)", ["Ring"] = "(O)",
            ["BL"] = "(O)", ["Blueprint"] = "(O)",
            ["BO"] = "(BC)", ["BigObject"] = "(BC)", ["BBL"] = "(BC)", ["BBl"] = "(BC)", ["BigBlueprint"] = "(BC)",
            ["F"] = "(F)", ["Furniture"] = "(F)",
            ["H"] = "(H)", ["Hat"] = "(H)",
            ["W"] = "(W)", ["Weapon"] = "(W)",
            ["B"] = "(B)", ["Boot"] = "(B)",
        };

    /// <summary>The qualified id a bundle reward string ("O 465 20", "BO 10 1") gives, or null
    /// when the string names no item the reward reader knows.</summary>
    public static string? RewardItemId(string? reward)
    {
        if (string.IsNullOrWhiteSpace(reward)) return null;
        string[] parts = reward.Split(RewardSeparator, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < RewardMinFields) return null;
        string type = parts[RewardTypeField];
        string id = parts[RewardIdField];
        if (type == ClothingCode)
        {
            if (int.TryParse(id, out int n))
                return (n >= ShirtIdFloor ? ShirtPrefix : PantsPrefix) + id;
            return id.StartsWith("(", StringComparison.Ordinal) ? id : null;
        }
        return RewardQualifiers.TryGetValue(type, out string? qualifier) ? qualifier + id : null;
    }

    /// <summary>Whether a reward gives only a vanilla item. An empty reward gives nothing, so it
    /// passes (vanilla's own The Missing has none). A reward the reader cannot name does not.</summary>
    public static bool IsVanillaReward(string? reward, IReadOnlySet<string> vanillaIds)
    {
        if (string.IsNullOrWhiteSpace(reward)) return true;
        string? id = RewardItemId(reward);
        return id != null && vanillaIds.Contains(id);
    }

    /// <summary>Whether one ingredient ref is allowed: a money slot or a category ref ("any
    /// fish") is not an item and always passes; anything else must be in the vanilla set.</summary>
    public static bool IsVanillaIngredient(string? itemRef, IReadOnlySet<string> vanillaIds)
    {
        if (string.IsNullOrEmpty(itemRef)) return false;
        if (itemRef == MoneySlotId || BundleParsing.IsCategoryRef(itemRef)) return true;
        return vanillaIds.Contains(BundleParsing.NormalizeItemId(itemRef));
    }

    /// <summary>Vanilla's own reward at this key, else <see cref="FallbackReward"/>.</summary>
    public static string VanillaRewardFor(string room, int index, IReadOnlyDictionary<string, string> vanillaBoard)
    {
        if (vanillaBoard.TryGetValue($"{room}/{index}", out string? value))
        {
            string[] fields = value.Split('/');
            if (fields.Length > 1) return fields[1];
        }
        return FallbackReward;
    }

    /// <summary>One template with its non-vanilla slots dropped and a non-vanilla reward swapped
    /// for vanilla's own (see the class doc). Returns the SAME instance when nothing had to change,
    /// and null when every slot named a non-vanilla item (nothing of the bundle is left).</summary>
    public static BundleSpec? Strip(
        BundleSpec spec, IReadOnlySet<string> vanillaIds, IReadOnlyDictionary<string, string> vanillaBoard)
    {
        if (spec == null) throw new ArgumentNullException(nameof(spec));
        List<BundleSlotSpec> kept = spec.Slots.Where(s => IsVanillaIngredient(s.ItemId, vanillaIds)).ToList();
        bool rewardOk = IsVanillaReward(spec.RewardField, vanillaIds);
        if (kept.Count == spec.Slots.Count && rewardOk)
            return spec;
        if (kept.Count == 0 && spec.Slots.Count > 0)
            return null;

        string reward = rewardOk ? spec.RewardField : VanillaRewardFor(spec.Room, spec.Index, vanillaBoard);
        if (!IsVanillaReward(reward, vanillaIds))
            reward = FallbackReward;
        return spec with
        {
            Slots = kept,
            RewardField = reward,
            NumberOfSlots = Math.Min(spec.NumberOfSlots, kept.Count),
            PickCount = spec.PickCount < 0 ? spec.PickCount : Math.Min(spec.PickCount, kept.Count),
        };
    }

    /// <summary>The engine's room pools with every template filtered (see the class doc).
    /// <paramref name="vanillaIds"/> null returns <paramref name="pools"/> itself. Room and position
    /// order are kept, so the picker's streams line up with an unfiltered pool of the same shape.
    /// <paramref name="changed"/> counts the templates that were stripped, dropped or replaced.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> FilterRoomPools(
        IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> pools,
        IReadOnlySet<string>? vanillaIds,
        IReadOnlyDictionary<string, string> vanillaBoard,
        out int changed)
    {
        changed = 0;
        if (vanillaIds == null) return pools;
        if (pools == null) throw new ArgumentNullException(nameof(pools));
        if (vanillaBoard == null) throw new ArgumentNullException(nameof(vanillaBoard));

        var result = new Dictionary<string, IReadOnlyList<IReadOnlyList<BundleSpec>>>(StringComparer.Ordinal);
        foreach (KeyValuePair<string, IReadOnlyList<IReadOnlyList<BundleSpec>>> room in pools)
        {
            var positions = new List<IReadOnlyList<BundleSpec>>(room.Value.Count);
            foreach (IReadOnlyList<BundleSpec> candidates in room.Value)
            {
                var kept = new List<BundleSpec>(candidates.Count);
                foreach (BundleSpec candidate in candidates)
                {
                    BundleSpec? stripped = Strip(candidate, vanillaIds, vanillaBoard);
                    if (!ReferenceEquals(stripped, candidate)) changed++;
                    if (stripped != null) kept.Add(stripped);
                }

                if (kept.Count == 0 && candidates.Count > 0)
                {
                    // Every candidate here named only other mods' items: vanilla's own bundle at
                    // this key, or no position at all when vanilla has none (a mod-added key).
                    BundleSpec first = candidates[0];
                    if (vanillaBoard.TryGetValue($"{first.Room}/{first.Index}", out string? value))
                    {
                        BundleSpec? fallback = Strip(BundleParsing.ToSpec($"{first.Room}/{first.Index}", value), vanillaIds, vanillaBoard);
                        if (fallback != null) kept.Add(fallback);
                    }
                }

                if (kept.Count > 0)
                    positions.Add(kept);
            }
            if (positions.Count > 0)
                result[room.Key] = positions;
        }
        return result;
    }
}
