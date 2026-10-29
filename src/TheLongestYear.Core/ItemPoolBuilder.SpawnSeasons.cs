using System;
using System.Collections.Generic;
using System.Linq;
using TheLongestYear.Core.Availability;

namespace TheLongestYear.Core;

public static partial class ItemPoolBuilder
{
    /// <summary>Maps that only exist while a passive festival runs, keyed to that festival's
    /// Data/PassiveFestivals id. The Night Market replaces the Beach with BeachNightMarket (that
    /// pair is in the festival data) and adds the Submarine, which the data does not mention:
    /// the game gates the market by date in code, so its spawn rows carry no season and read as
    /// all-year (player report 2026-08-28: a Sea Cucumber demanded before Summer 1).</summary>
    private static readonly IReadOnlyDictionary<string, string> BuiltInFestivalLocations =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Submarine"] = "NightMarket",
            ["BeachNightMarket"] = "NightMarket",
        };

    /// <summary>Vanilla passive-festival seasons, used only when the caller supplies no
    /// Data/PassiveFestivals table (hand-built pools) or the table lacks the id.</summary>
    private static readonly IReadOnlyDictionary<string, Season> BuiltInFestivalSeasons =
        new Dictionary<string, Season>(StringComparer.OrdinalIgnoreCase)
        {
            ["NightMarket"] = Season.Winter,
            ["SquidFest"] = Season.Winter,
            ["TroutDerby"] = Season.Summer,
            ["DesertFestival"] = Season.Spring,
        };

    private const string PassiveFestivalOpenQuery = "IS_PASSIVE_FESTIVAL_OPEN";
    private static readonly char[] ConditionSeparators = { ' ', ',' };
    private static readonly char[] ClauseSeparator = { ',' };

    /// <summary>Game state queries that gate a Data/Locations row on a special order being
    /// active. A row carrying one of these is only reachable while that order is running, and
    /// every order that matters here is Mr. Qi's: the Extended Family fish sit behind
    /// LEGENDARY_FAMILY and Qi Beans behind DROP_QI_BEANS, both of which need the Walnut Room on
    /// Ginger Island, i.e. post-Community-Center. A one-year loop can never see them.
    ///
    /// Found by Nexus bug 2026-08-30 (spenderg): a river fish bundle asked for Ms. Angler. Unlike
    /// the five vanilla legendaries, which Data/Objects flags ExcludeFromRandomSale so the vet
    /// drops them, all five Extended Family fish (898-902) carry ExcludeFromRandomSale=false, and
    /// their spawn maps are ordinary Town/Beach/Mountain/Forest/Sewer, so no location marker and
    /// no id list caught them.
    ///
    /// THE TRAP, and why this must be clause-aware and not a substring match: the vanilla
    /// legendaries are themselves written as NEGATED rows of the same query
    /// ("!PLAYER_SPECIAL_ORDER_RULE_ACTIVE Current LEGENDARY_FAMILY" is Angler's ONLY row, because
    /// vanilla swaps parent for child while the order runs). A naive Contains() would delete
    /// Legend, Crimsonfish, Angler, Glacierfish and Mutant Carp from the pool outright. A negated
    /// clause means the row is reachable WITHOUT the order, which is exactly what a loop has.</summary>
    private static readonly string[] SpecialOrderQueries =
    {
        "PLAYER_SPECIAL_ORDER_RULE_ACTIVE",
        "PLAYER_SPECIAL_ORDER_ACTIVE",
    };

    /// <summary>True when a spawn row can only fire while a special order is active, so a loop
    /// that never reaches Qi cannot get the item from it. See <see cref="SpecialOrderQueries"/>.
    /// A Condition is a comma-separated AND of clauses, so one non-negated clause is enough.</summary>
    public static bool IsSpecialOrderGated(string? condition)
    {
        if (string.IsNullOrEmpty(condition)) return false;
        foreach (string clause in condition.Split(ClauseSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = clause.Trim();
            if (trimmed.StartsWith('!')) continue;   // reachable without the order - see the trap above
            int space = trimmed.IndexOf(' ');
            string query = space < 0 ? trimmed : trimmed.Substring(0, space);
            foreach (string gated in SpecialOrderQueries)
                if (string.Equals(query, gated, StringComparison.OrdinalIgnoreCase))
                    return true;
        }
        return false;
    }

    /// <summary>Season list for one spawn entry: an explicit Season wins; otherwise the seasons
    /// the Condition allows, read clause by clause (<see cref="SeasonsFromCondition"/>);
    /// otherwise empty = any season.</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(Season? season, string? condition)
        => SeasonsFromSpawn(season, condition, null, null);

    /// <summary>As above, plus the passive-festival rule: a row on a festival-only map
    /// (<see cref="BuiltInFestivalLocations"/>) or conditioned on
    /// <c>IS_PASSIVE_FESTIVAL_OPEN &lt;id&gt;</c> is only reachable in that festival's season,
    /// looked up in <paramref name="festivalSeasons"/> (Data/PassiveFestivals, so modded
    /// festivals count) with <see cref="BuiltInFestivalSeasons"/> as the fallback. Explicit
    /// seasons and season clauses still win; an unknown festival is no signal.</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(
        Season? season, string? condition, string? location,
        IReadOnlyDictionary<string, Season>? festivalSeasons = null)
    {
        if (season != null)
            return new[] { season.Value };

        HashSet<Season>? allowed = SeasonsFromCondition(condition);
        if (allowed != null && allowed.Count > 0)
            return AllSeasons.Where(allowed.Contains).ToArray();

        string[] tokens = (condition ?? "").Split(ConditionSeparators, StringSplitOptions.RemoveEmptyEntries);
        string? festival = null;
        if (!string.IsNullOrEmpty(location) && BuiltInFestivalLocations.TryGetValue(location, out string? byLocation))
            festival = byLocation;
        for (int i = 0; festival == null && i + 1 < tokens.Length; i++)
        {
            if (string.Equals(tokens[i], PassiveFestivalOpenQuery, StringComparison.OrdinalIgnoreCase))
                festival = tokens[i + 1];
        }
        if (festival != null)
        {
            if (festivalSeasons != null && festivalSeasons.TryGetValue(festival, out Season fromData))
                return new[] { fromData };
            if (BuiltInFestivalSeasons.TryGetValue(festival, out Season builtIn))
                return new[] { builtIn };
        }
        return Array.Empty<Season>();
    }

    /// <summary>True when a row with no Season field has season clauses that rule out every season
    /// (e.g. "SEASON spring, !SEASON spring"). Such a row never fires in the game, but
    /// <see cref="SeasonsFromSpawn(Season?, string?, string?, IReadOnlyDictionary{string, Season}?)"/>
    /// reads it as any season, so the pool builder's caller logs it.</summary>
    public static bool HasContradictorySeasons(Season? season, string? condition)
        => season == null && SeasonsFromCondition(condition) is { Count: 0 };

    private static readonly Season[] AllSeasons ={ Season.Spring, Season.Summer, Season.Fall, Season.Winter };

    /// <summary>The seasons a Condition allows, or null when no clause says anything about seasons.
    /// A Condition is a comma-separated AND, so it is read one clause at a time:
    /// <list type="bullet">
    /// <item>a non-negated clause that names seasons narrows the set: SEASON / LOCATION_SEASON by
    /// their season arguments (a name or 0-3, as the game reads them), any other clause by its bare
    /// season NAMES only (SEASON_DAY spring 5; "YEAR 2" is not Fall); several such clauses
    /// intersect;</item>
    /// <item>a negated SEASON / LOCATION_SEASON clause removes the seasons it names;</item>
    /// <item>any other negated clause, and any ANY clause (its quoted OR does not split into
    /// clauses), says nothing about seasons and is skipped.</item>
    /// </list>
    /// Player reports 2026-10-29: Rainbow Trout and Squid were asked for in Spring weeks 1-2. A map
    /// mod's row such as "LOCATION_SEASON Here summer, !WEATHER Here rain" used to read as ANY
    /// season, because a '!' anywhere in the string meant "no season signal", and one any-season
    /// row makes the whole fish any-season. An empty set (contradictory clauses: a row that never
    /// fires) is returned as is; <see cref="SeasonsFromSpawn(Season?, string?, string?, IReadOnlyDictionary{string, Season}?)"/>
    /// treats it as no signal; <see cref="HasContradictorySeasons"/> lets the caller log those rows.</summary>
    internal static HashSet<Season>? SeasonsFromCondition(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return null;
        HashSet<Season>? allowed = null;
        foreach (string clause in SeasonClauses.Split(condition))
        {
            if (SeasonClauses.IsAnyQuery(clause)) continue;   // an OR of quoted sub-queries: no signal
            HashSet<Season>? named = SeasonClauses.SeasonsNamedBy(clause, out bool negated);
            if (negated)
            {
                if (named == null || named.Count == 0) continue;
                allowed ??= new HashSet<Season>(AllSeasons);
                allowed.ExceptWith(named);
                continue;
            }
            if (named == null)
            {
                named = new HashSet<Season>();
                foreach (string token in clause.Split(ConditionSeparators, StringSplitOptions.RemoveEmptyEntries))
                    if (SeasonClauses.TryParseSeasonName(token, out Season s))
                        named.Add(s);
            }
            if (named.Count == 0) continue;
            if (allowed == null) allowed = named;
            else allowed.IntersectWith(named);
        }
        return allowed;
    }
}
