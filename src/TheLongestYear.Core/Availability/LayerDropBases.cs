using System;
using System.Collections.Generic;
using System.Linq;

namespace TheLongestYear.Core.Availability;

/// <summary>The quantity basis of each item a supported mod's layer adds monster drops for
/// (<see cref="RawMonsterDrop.FromLayer"/>), worked out in the cold build and stored with the
/// availability answers (Jeff, 2026-11-20: measure, then let the ask sizing read the layer's drops).
/// The rule is the one the vanilla monster table follows (tools/fish-sim/gen_tables.py and
/// <see cref="QuantityBasisTables.MonsterDropCaps"/>): 60 kills a day for a week of the best monster
/// (a third of that in the Skull Cavern) times the chance per kill, capped at twice what clearing
/// floors measured (<see cref="QuantityBasisTables.MonsterWeeklyKills"/>, rounded up, at least 2),
/// the best monster per item, nothing under 2, at most a stack. A data drop of the same item from
/// the same monster adds to the layer's (the table sums a monster's chances per item). A seasonal
/// row counts only in its season.</summary>
public static class LayerDropBases
{
    private const double KillsPerWeek = 60 * 7;
    private const double SkullKillsPerWeek = KillsPerWeek / 3;
    private const double CapFactor = 2;
    private const double MinBasis = 2;
    private const double MaxBasis = 99;
    private const int Seasons = 4;

    public static IReadOnlyDictionary<string, double[]> Build(IReadOnlyList<RawMonsterDrop> drops)
    {
        if (drops == null) throw new ArgumentNullException(nameof(drops));
        var result = new Dictionary<string, double[]>(StringComparer.Ordinal);
        var layered = drops.Where(d => d.FromLayer == true)
            .Select(d => (d.MonsterName, Id: BundleParsing.NormalizeItemId(d.ItemId)))
            .Distinct()
            .OrderBy(p => p.Id, StringComparer.Ordinal).ThenBy(p => p.MonsterName, StringComparer.Ordinal);
        foreach ((string monster, string id) in layered)
        {
            int? area = MonsterDropAvailability.SpawnAreaFor(monster);
            if (area == null) continue;   // a monster the board cannot place has no farming spot
            double kills = area.Value >= MineAreas.SkullCavern ? SkullKillsPerWeek : KillsPerWeek;
            bool measured = QuantityBasisTables.MonsterWeeklyKills.TryGetValue(monster, out double cleared);
            var rows = drops.Where(d => d.MonsterName == monster && BundleParsing.NormalizeItemId(d.ItemId) == id).ToList();
            for (int s = 0; s < Seasons; s++)
            {
                double chance = rows.Where(d => d.Season == null || (int)d.Season.Value == s).Sum(d => d.Chance);
                if (chance <= 0) continue;
                double basis = kills * chance;
                if (measured) basis = Math.Min(basis, Math.Max(MinBasis, Math.Ceiling(CapFactor * cleared * chance)));
                if (basis < MinBasis) continue;
                if (!result.TryGetValue(id, out double[]? bySeason)) result[id] = bySeason = new double[Seasons];
                bySeason[s] = Math.Max(bySeason[s], Math.Min(MaxBasis, Math.Round(basis, 1)));
            }
        }
        return result;
    }
}
