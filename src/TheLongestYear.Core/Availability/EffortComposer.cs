using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Runs the Phase 2 effort rules in a fixed order for one id and takes the first that
/// claims it: mine nodes, geodes, monster drops, artifacts, animal products, artisan goods, fish
/// ponds, cooked dishes, crops, forage. Season-derived ids (fish, crab-pot, metals) keep their
/// Phase 1 effort. Results are memoised; an id being resolved reads as unknown to itself, which
/// breaks recipe cycles (a machine whose input is its own output).</summary>
public sealed class EffortComposer
{
    private readonly EffortData _data;
    private readonly IReadOnlyDictionary<string, ItemAvailability> _seasonDerived;
    private readonly bool _hasKitchen;
    private readonly Dictionary<string, ItemEffort?> _memo = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visiting = new(StringComparer.Ordinal);

    public EffortComposer(EffortData data, IReadOnlyDictionary<string, ItemAvailability> seasonDerived, bool hasKitchen)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _seasonDerived = seasonDerived ?? throw new ArgumentNullException(nameof(seasonDerived));
        _hasKitchen = hasKitchen;
    }

    /// <summary>Effort for any id the model can place, or null. This is the resolver the
    /// recursive rules (artisan inputs, dish ingredients, pond fish) call.</summary>
    public int? EffortOf(string qualifiedId)
    {
        if (qualifiedId == null) return null;
        if (_seasonDerived.TryGetValue(qualifiedId, out ItemAvailability? season))
            return season.Effort;
        if (_memo.TryGetValue(qualifiedId, out ItemEffort? memo))
            return memo?.Effort;
        if (!_visiting.Add(qualifiedId))
            return null;
        try
        {
            ItemEffort? derived = Derive(qualifiedId);
            _memo[qualifiedId] = derived;
            return derived?.Effort;
        }
        finally
        {
            _visiting.Remove(qualifiedId);
        }
    }

    public ItemEffort? Derive(string qualifiedId)
        => MineralNodeAvailability.Derive(qualifiedId)
           ?? GeodeAvailability.Derive(qualifiedId, _data.GeodeDrops)
           ?? MonsterDropAvailability.Derive(qualifiedId, _data.MonsterDrops)
           ?? ArtifactAvailability.Derive(qualifiedId, _data.ArtifactSpots)
           ?? AnimalProductAvailability.Derive(qualifiedId, _data.Animals, _data.Buildings)
           ?? ArtisanAvailability.Derive(qualifiedId, _data, EffortOf)
           ?? FishPondAvailability.Derive(qualifiedId, _data, EffortOf)
           ?? CookedDishAvailability.Derive(qualifiedId, _data, EffortOf, _hasKitchen)
           ?? CropForageAvailability.DeriveCrop(qualifiedId, _data.Crops)
           ?? CropForageAvailability.DeriveForage(qualifiedId, _data.ForageSpawns);

    /// <summary>Every Data/Objects id a rule claims and Phase 1 did not, in ordinal order.</summary>
    public IReadOnlyDictionary<string, ItemEffort> DeriveAll()
    {
        var result = new Dictionary<string, ItemEffort>(StringComparer.Ordinal);
        foreach (string bare in _data.Objects.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            string id = BundleParsing.NormalizeItemId(bare);
            if (_seasonDerived.ContainsKey(id)) continue;
            EffortOf(id);
            if (_memo.TryGetValue(id, out ItemEffort? effort) && effort != null)
                result[id] = effort;
        }
        return result;
    }
}
