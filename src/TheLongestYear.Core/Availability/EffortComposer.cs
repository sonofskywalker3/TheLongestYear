using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>Runs the Phase 2 effort rules in a fixed order for one id and takes the first that
/// claims it: mine nodes, geodes, monster drops, artifacts, animal products, artisan goods, fish
/// ponds, cooked dishes, crops, forage, saplings. Season-derived ids (fish, crab-pot, metals)
/// keep their Phase 1 effort and week. Results are memoised; an id being resolved reads as
/// unknown to itself, which breaks recipe cycles (a machine whose input is its own output).
/// Since the even-year spec (2026-08-28) every rule also places the id in time
/// (ItemEffort.EarliestWeek); <see cref="WeekOf"/> is the resolver the recursive rules use.</summary>
public sealed class EffortComposer
{
    private readonly EffortData _data;
    private readonly IReadOnlyDictionary<string, ItemAvailability> _seasonDerived;
    private readonly bool _hasKitchen;
    private readonly IReadOnlyList<PoolItem> _saplings;
    private readonly Dictionary<string, ItemEffort?> _memo = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visiting = new(StringComparer.Ordinal);

    public EffortComposer(EffortData data, IReadOnlyDictionary<string, ItemAvailability> seasonDerived, bool hasKitchen,
        IReadOnlyList<PoolItem>? saplings = null)
    {
        _data = data ?? throw new ArgumentNullException(nameof(data));
        _seasonDerived = seasonDerived ?? throw new ArgumentNullException(nameof(seasonDerived));
        _hasKitchen = hasKitchen;
        _saplings = saplings ?? Array.Empty<PoolItem>();
    }

    /// <summary>Effort for any id the model can place, or null. This is the resolver the
    /// recursive rules (artisan inputs, dish ingredients, pond fish) call.</summary>
    public int? EffortOf(string qualifiedId)
    {
        if (qualifiedId == null) return null;
        if (_seasonDerived.TryGetValue(qualifiedId, out ItemAvailability? season))
            return season.Effort;
        return Resolve(qualifiedId)?.Effort;
    }

    /// <summary>First week for any id the model has placed, or null. Season-derived ids answer
    /// from Phase 1; everything else runs the rules once and is memoised.</summary>
    public int? WeekOf(string qualifiedId)
    {
        if (qualifiedId == null) return null;
        if (_seasonDerived.TryGetValue(qualifiedId, out ItemAvailability? season))
            return season.Week;
        return Resolve(qualifiedId)?.EarliestWeek;
    }

    private ItemEffort? Resolve(string qualifiedId)
    {
        if (_memo.TryGetValue(qualifiedId, out ItemEffort? memo))
            return memo;
        if (!_visiting.Add(qualifiedId))
            return null;
        try
        {
            ItemEffort? derived = Derive(qualifiedId);
            _memo[qualifiedId] = derived;
            return derived;
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
           ?? ArtisanAvailability.Derive(qualifiedId, _data, EffortOf, WeekOf)
           ?? FishPondAvailability.Derive(qualifiedId, _data, EffortOf, WeekOf)
           ?? CookedDishAvailability.Derive(qualifiedId, _data, EffortOf, _hasKitchen, WeekOf)
           ?? CropForageAvailability.DeriveCrop(qualifiedId, _data.Crops)
           ?? CropForageAvailability.DeriveForage(qualifiedId, _data.ForageSpawns)
           ?? CropForageAvailability.DeriveSapling(qualifiedId, _saplings);

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
