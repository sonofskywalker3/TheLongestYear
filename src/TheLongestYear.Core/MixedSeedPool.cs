using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core;

/// <summary>A seed this mod's own roll puts in place of vanilla's, and how often
/// (a Cultivation upgrade, the Year-Two Seeds boost).</summary>
public sealed record MixedSeedSubstitution(string SeedId, double Chance);

/// <summary>What a bag of Mixed Seeds becomes when another mod keeps a pool of seeds for it.
/// <para>
/// Item Extensions (mistyspring.ItemExtensions, Patches/Crop.cs GetFromFramework) builds a list for
/// each planting: every pool entry whose Condition holds where the bag is planted and whose crop
/// grows in the current season (the season is not asked indoors), once per point of Weight, then the
/// bag itself as the "vanilla roll" slot; it plants one of the list, each equally likely.
/// <see cref="MixedSeedsOutcome"/> then lets the roll made when the bag was used stand when this mod
/// changed it or it came up Artichoke Seeds.
/// </para>
/// <para>
/// Everything here is for a planting in the valley: outdoors on the farm, or in the greenhouse.
/// Ginger Island has its own entries and is year 2.
/// </para></summary>
public static class MixedSeedPool
{
    public const string MixedSeedsQualifiedId = "(O)770";
    /// <summary>Artichoke Seeds, qualified.</summary>
    public const string ArtichokeSeedsQualifiedId = "(O)489";
    /// <summary>The share of the entries whose Condition this reader cannot judge. They take their
    /// slots, so every other share is as small as it could be, and they give no route.</summary>
    public const string Unreadable = "?";

    /// <summary>A row the reader adds when Crops Anytime Anywhere is installed: Item Extensions then
    /// plants a pool seed outdoors whatever its crop's seasons. It takes no slot; being a row, it also
    /// keeps such a game from matching answers written without that mod.</summary>
    public const string AnySeasonKey = "$crops_anytime";

    private const string VanillaCropsKey = "$vanilla_crops";
    private const string VanillaFlowersKey = "$vanilla_flowers";
    private const string VanillaSeedsKey = "$vanilla_seeds";
    private const char KeyMarker = '$';
    private const string FarmName = "Farm";
    private const string GreenhouseName = "Greenhouse";
    private const string HereTarget = "Here";
    private const string AnyQuery = "ANY";

    private static readonly Season[] GrowingSeasons = { Season.Spring, Season.Summer, Season.Fall };

    /// <summary>Vanilla's roll by the season of the planting day (Crop.getRandomLowGradeCropForThisSeason):
    /// Spring Next(472, 476) with 473 read as 472, Summer one of four, Fall Next(487, 491).</summary>
    private static readonly IReadOnlyDictionary<Season, (string SeedId, double Chance)[]> VanillaRoll =
        new Dictionary<Season, (string, double)[]>
        {
            [Season.Spring] = new[] { ("(O)472", 0.5), ("(O)474", 0.25), ("(O)475", 0.25) },
            [Season.Summer] = new[] { ("(O)487", 0.25), ("(O)483", 0.25), ("(O)482", 0.25), ("(O)484", 0.25) },
            [Season.Fall] = new[] { ("(O)487", 0.25), ("(O)488", 0.25), ("(O)489", 0.25), ("(O)490", 0.25) },
        };

    /// <summary>What Item Extensions' <c>$vanilla_crops</c> key adds, each seed once.</summary>
    private static readonly IReadOnlyDictionary<Season, string[]> KeyCrops = new Dictionary<Season, string[]>
    {
        [Season.Spring] = new[] { "(O)472", "(O)474", "(O)475" },
        [Season.Summer] = new[] { "(O)487", "(O)483", "(O)482", "(O)484" },
        [Season.Fall] = new[] { "(O)487", "(O)488", "(O)489", "(O)490" },
    };

    /// <summary>What Item Extensions' <c>$vanilla_flowers</c> key adds, each seed once.</summary>
    private static readonly IReadOnlyDictionary<Season, string[]> KeyFlowers = new Dictionary<Season, string[]>
    {
        [Season.Spring] = new[] { "(O)427", "(O)429" },
        [Season.Summer] = new[] { "(O)455", "(O)453", "(O)431" },
        [Season.Fall] = new[] { "(O)431", "(O)425" },
    };

    /// <summary>The pool's own entries: every row but the bag's own slot.</summary>
    public static IEnumerable<MixedSeedRow> Entries(IReadOnlyList<MixedSeedRow> pool)
        => pool.Where(r => r.SeedId != MixedSeedsQualifiedId && r.SeedId != AnySeasonKey);

    /// <summary>How many slots the vanilla roll holds: the bag's own row, or one when the pool has none.</summary>
    public static int VanillaSlots(IReadOnlyList<MixedSeedRow> pool)
    {
        MixedSeedRow? own = pool.FirstOrDefault(r => r.SeedId == MixedSeedsQualifiedId);
        return own == null ? 1 : Math.Max(0, own.Weight);
    }

    /// <summary>Whether a pool entry's Condition holds for a planting in the valley in
    /// <paramref name="season"/>, outdoors on the farm or in the greenhouse. Null when a clause is one
    /// this reader does not know: such an entry is never a route. Reads TRUE, FALSE, SEASON,
    /// LOCATION_SEASON, LOCATION_IS_INDOORS, LOCATION_IS_OUTDOORS, LOCATION_NAME, a leading "!" and
    /// ANY; clauses are joined by commas outside double quotes, as the game splits them.</summary>
    public static bool? Admits(string? condition, Season season, bool indoors)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;
        bool? all = true;
        foreach (string clause in Split(condition, ','))
        {
            bool? one = Clause(clause, season, indoors);
            if (one == false) return false;
            if (one == null) all = null;
        }
        return all;
    }

    private static bool? Clause(string raw, Season season, bool indoors)
    {
        string clause = raw.Trim();
        if (clause.Length == 0) return true;
        if (clause[0] == '!')
        {
            bool? inner = Clause(clause.Substring(1), season, indoors);
            return inner == null ? null : !inner;
        }
        List<string> tokens = Split(clause, ' ').Where(t => t.Length > 0).ToList();
        if (tokens.Count == 0) return true;
        switch (tokens[0].ToUpperInvariant())
        {
            case "TRUE": return true;
            case "FALSE": return false;
            case "SEASON": return NamesSeason(tokens.Skip(1), season);
            case "LOCATION_SEASON": return IsHere(tokens) ? NamesSeason(tokens.Skip(2), season) : null;
            case "LOCATION_IS_INDOORS": return IsHere(tokens) ? indoors : null;
            case "LOCATION_IS_OUTDOORS": return IsHere(tokens) ? !indoors : null;
            case "LOCATION_NAME":
                return IsHere(tokens)
                    ? tokens.Skip(2).Any(n => n.Equals(indoors ? GreenhouseName : FarmName, StringComparison.OrdinalIgnoreCase))
                    : null;
            case AnyQuery:
            {
                bool? any = false;
                foreach (string alternative in tokens.Skip(1))
                {
                    bool? one = Admits(alternative, season, indoors);
                    if (one == true) return true;
                    if (one == null) any = null;
                }
                return any;
            }
            default: return null;
        }
    }

    private static bool IsHere(List<string> tokens)
        => tokens.Count > 1 && tokens[1].Equals(HereTarget, StringComparison.OrdinalIgnoreCase);

    private static bool NamesSeason(IEnumerable<string> names, Season season)
        => names.Any(n => n.Equals(season.ToString(), StringComparison.OrdinalIgnoreCase));

    /// <summary>Splits on <paramref name="separator"/> outside double quotes, as the game does
    /// (ArgUtility.SplitQuoteAware): the comma split keeps quotes and escaped quotes for the next
    /// stage (GameStateQuery.SplitRaw), the space split drops the quotes and turns an escaped quote
    /// into a plain one, so an ANY inside an ANY still reads. A quote left open runs to the end.</summary>
    private static List<string> Split(string text, char separator)
    {
        bool keep = separator == ',';
        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '\\' && i + 1 < text.Length && text[i + 1] == '"')
            {
                if (keep) current.Append(c);
                current.Append('"');
                i++;
                continue;
            }
            if (c == '"') { quoted = !quoted; if (keep) current.Append(c); continue; }
            if (c == separator && !quoted) { parts.Add(current.ToString()); current.Clear(); continue; }
            current.Append(c);
        }
        parts.Add(current.ToString());
        return parts;
    }

    /// <summary>The chance each seed goes into the ground from one bag planted in the valley in
    /// <paramref name="season"/>, by qualified seed id; <see cref="Unreadable"/> holds the share of
    /// entries whose Condition could not be judged. Empty for Winter outdoors, where nothing is planted.
    /// With an empty pool this is vanilla's roll with this mod's substitutions.</summary>
    /// <param name="cropSeasons">The seasons a seed's crop grows in (empty: any); null when the seed
    /// has no Data/Crops row, which Item Extensions skips.</param>
    /// <param name="substitutions">This mod's own swaps of the roll, in the order the patch makes
    /// them (a later one overrides an earlier one).</param>
    public static IReadOnlyDictionary<string, double> Shares(
        Season season, bool indoors, IReadOnlyList<MixedSeedRow> pool, Func<string, IReadOnlyList<Season>?> cropSeasons,
        IReadOnlyList<MixedSeedSubstitution>? substitutions = null)
    {
        if (pool == null) throw new ArgumentNullException(nameof(pool));
        if (cropSeasons == null) throw new ArgumentNullException(nameof(cropSeasons));
        var shares = new Dictionary<string, double>(StringComparer.Ordinal);
        if (season == Season.Winter && !indoors) return shares;
        void Add(string id, double chance)
        {
            if (chance <= 0) return;
            shares[id] = shares.TryGetValue(id, out double had) ? had + chance : chance;
        }

        // The roll made when the bag is used. Winter (the greenhouse) rolls one of the three pools.
        var roll = new Dictionary<string, double>(StringComparer.Ordinal);
        Season[] rollSeasons = season == Season.Winter ? GrowingSeasons : new[] { season };
        foreach (Season s in rollSeasons)
            foreach ((string id, double chance) in VanillaRoll[s])
                roll[id] = (roll.TryGetValue(id, out double had) ? had : 0) + chance / rollSeasons.Length;

        double untouched = 1.0;
        if (substitutions != null)
            for (int i = 0; i < substitutions.Count; i++)
            {
                double later = 1.0;
                for (int j = i + 1; j < substitutions.Count; j++) later *= 1.0 - substitutions[j].Chance;
                Add(substitutions[i].SeedId, substitutions[i].Chance * later);
                untouched *= 1.0 - substitutions[i].Chance;
            }

        roll.TryGetValue(ArtichokeSeedsQualifiedId, out double artichoke);
        Add(ArtichokeSeedsQualifiedId, untouched * artichoke);
        double open = untouched * (1.0 - artichoke);   // the other mod's pick is planted

        // The other mod's list for this planting.
        var slots = new Dictionary<string, double>(StringComparer.Ordinal);
        void Slot(string id, double count) => slots[id] = (slots.TryGetValue(id, out double had) ? had : 0) + count;
        bool any = false;
        bool anySeason = pool.Any(r => r.SeedId == AnySeasonKey);
        foreach (MixedSeedRow row in Entries(pool))
        {
            if (row.Weight <= 0) continue;
            bool? admitted = Admits(row.Condition, season, indoors);
            if (admitted == false) continue;
            if (row.SeedId.Length > 0 && row.SeedId[0] == KeyMarker)
            {
                // A key adds vanilla's seeds; indoors or in Winter the other mod picks one of the
                // three seasons' lists at random for each key, which is read here as a third of each
                // list: close, not exact, when the lists differ in length.
                Season[] keySeasons = indoors || season == Season.Winter ? GrowingSeasons : new[] { season };
                foreach (Season s in keySeasons)
                    foreach (string id in KeySeeds(row.SeedId, s))
                        Slot(admitted == null ? Unreadable : id, (double)row.Weight / keySeasons.Length);
                any = true;
                continue;
            }
            IReadOnlyList<Season>? grows = cropSeasons(row.SeedId);
            if (grows == null) continue;
            if (!indoors && !anySeason && grows.Count > 0 && !grows.Contains(season)) continue;
            Slot(admitted == null ? Unreadable : row.SeedId, row.Weight);
            any = true;
        }
        int vanillaSlots = any ? VanillaSlots(pool) : 1;
        double total = slots.Values.Sum() + vanillaSlots;
        if (total <= 0) return shares;
        foreach (var (id, count) in slots) Add(id, open * count / total);
        // The bag's own slot: the roll stands, and it is not Artichoke Seeds here.
        if (artichoke < 1.0)
            foreach (var (id, chance) in roll)
                if (id != ArtichokeSeedsQualifiedId)
                    Add(id, open * (vanillaSlots / total) * chance / (1.0 - artichoke));
        return shares;
    }

    private static IEnumerable<string> KeySeeds(string key, Season season) => key switch
    {
        VanillaCropsKey => KeyCrops[season],
        VanillaFlowersKey => KeyFlowers[season],
        VanillaSeedsKey => KeyFlowers[season].Concat(KeyCrops[season]),
        _ => Array.Empty<string>(),
    };

    /// <summary>The pool's own seeds a year-1 farm can get from a bag: each seed with a share above
    /// zero in Spring, Summer or Fall outdoors. Seeds the vanilla roll gives are not listed.</summary>
    public static IReadOnlySet<string> FarmSeeds(IReadOnlyList<MixedSeedRow> pool, Func<string, IReadOnlyList<Season>?> cropSeasons)
    {
        var seeds = new HashSet<string>(StringComparer.Ordinal);
        if (pool == null || pool.Count == 0) return seeds;
        var listed = new HashSet<string>(Entries(pool).Select(r => r.SeedId), StringComparer.Ordinal);
        foreach (Season season in GrowingSeasons)
            foreach (var (id, share) in Shares(season, indoors: false, pool, cropSeasons))
                if (share > 0 && listed.Contains(id)) seeds.Add(id);
        return seeds;
    }
}
