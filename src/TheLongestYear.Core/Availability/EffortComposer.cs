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
    private readonly IReadOnlyList<PoolItem> _artifacts;
    private readonly IReadOnlyList<PoolItem> _books;
    private readonly Dictionary<string, ItemEffort?> _memo = new(StringComparer.Ordinal);
    private readonly HashSet<string> _visiting = new(StringComparer.Ordinal);

    public EffortComposer(EffortData data, IReadOnlyDictionary<string, ItemAvailability> seasonDerived, bool hasKitchen,
        IReadOnlyList<PoolItem>? saplings = null, IReadOnlyList<PoolItem>? artifacts = null,
        IReadOnlyList<PoolItem>? books = null)
    {
        _artifacts = artifacts ?? Array.Empty<PoolItem>();
        _books = books ?? Array.Empty<PoolItem>();
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

    /// <summary>Every rule that claims the id is asked, and the EARLIEST week wins (then the lower
    /// effort). First-claim-wins put Wood at week 5 (Recycling Machine) and Red Mushroom at week 9
    /// (Mushroom Box) because the machine rules ran before the natural ones (sim G, 2026-08-28).
    /// The rule order is kept only as the tiebreak.</summary>
    public ItemEffort? Derive(string qualifiedId)
    {
        ItemEffort? best = null;
        foreach (ItemEffort? raw in new[]
        {
            FishingTrashAvailability.Derive(qualifiedId),
            ShopAvailability.Derive(qualifiedId),
            MineralNodeAvailability.Derive(qualifiedId),
            GeodeAvailability.Derive(qualifiedId, _data.GeodeDrops),
            MonsterDropAvailability.Derive(qualifiedId, _data.MonsterDrops),
            ArtifactAvailability.Derive(qualifiedId, _data.ArtifactSpots),
            AnimalProductAvailability.Derive(qualifiedId, _data.Animals, _data.Buildings),
            CropForageAvailability.DeriveCrop(qualifiedId, _data.Crops),
            CropForageAvailability.DeriveForage(qualifiedId, _data.ForageSpawns),
            CropForageAvailability.DeriveSapling(qualifiedId, _saplings),
            TapperAvailability.Derive(qualifiedId, _data),
            PoolArtifact(qualifiedId),
            PoolBook(qualifiedId),
            ArtisanAvailability.Derive(qualifiedId, _data, EffortOf, WeekOf),
            FishPondAvailability.Derive(qualifiedId, _data, EffortOf, WeekOf),
            CookedDishAvailability.Derive(qualifiedId, _data, EffortOf, _hasKitchen, WeekOf),
        })
        {
            ItemEffort? candidate = raw;
            if (candidate == null) continue;
            if (AvailabilityWeeks.LateFloors.TryGetValue(qualifiedId, out (int Week, string Note) late)
                && (candidate.EarliestWeek ?? 0) < late.Week)
                candidate = new ItemEffort(candidate.Effort, $"{candidate.Basis}; late floor: {late.Note}, week {late.Week} (for Jeff to confirm)",
                    late.Week, AvailabilityWeeks.SeasonOf(late.Week));
            bool better = best == null
                || (candidate.EarliestWeek ?? int.MaxValue) < (best.EarliestWeek ?? int.MaxValue)
                || (candidate.EarliestWeek == best.EarliestWeek && candidate.Effort < best.Effort);
            if (better) best = candidate;
        }
        return best;
    }

    private const int PoolArtifactEffort = 4;

    /// <summary>An artifact the catalog's own pool lists but the spot data does not (five on the
    /// 2026-08-28 boards: Ancient Doll, Anchor, Bone Flute, Golden Relic, Prehistoric Handaxe):
    /// dig spots exist from day 1, so week 1 at a middling effort.</summary>
    private ItemEffort? PoolArtifact(string qualifiedId)
        => _artifacts.Any(a => a.ItemId == qualifiedId)
            ? new ItemEffort(PoolArtifactEffort, $"artifact (catalog pool, no spot row), week {AvailabilityWeeks.ArtifactWeek}, effort {PoolArtifactEffort}",
                AvailabilityWeeks.ArtifactWeek, Season.Spring)
            : null;

    private const int PoolBookEffort = 5;

    private ItemEffort? PoolBook(string qualifiedId)
        => _books.Any(b => b.ItemId == qualifiedId) && AvailabilityWeeks.BookWeeks.TryGetValue(qualifiedId, out int week)
            ? new ItemEffort(PoolBookEffort, $"book (catalog pool), year-1 route, week {week}, effort {PoolBookEffort}",
                week, AvailabilityWeeks.SeasonOf(week))
            : null;

    /// <summary>Every Data/Objects id a rule claims and Phase 1 did not, plus the table ids that
    /// are not objects (hats, weapons: Adventurer's Guild rewards), in ordinal order.</summary>
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
        foreach (string id in AvailabilityWeeks.GuildRewardWeeks.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            if (result.ContainsKey(id) || _seasonDerived.ContainsKey(id)) continue;
            ItemEffort? effort = ShopAvailability.Derive(id);
            if (effort != null) result[id] = effort;
        }
        return result;
    }
}
