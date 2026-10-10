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

    /// <summary>Season list for one spawn entry: an explicit Season wins; otherwise any
    /// season names found in the Condition string (best-effort GameStateQuery token scan);
    /// otherwise empty = any season. Note: negated GSQ season clauses (containing '!')
    /// cannot be token-scanned safely, so any negation means "no season signal".</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(Season? season, string? condition)
        => SeasonsFromSpawn(season, condition, null, null);

    /// <summary>As above, plus the passive-festival rule: a row on a festival-only map
    /// (<see cref="BuiltInFestivalLocations"/>) or conditioned on
    /// <c>IS_PASSIVE_FESTIVAL_OPEN &lt;id&gt;</c> is only reachable in that festival's season,
    /// looked up in <paramref name="festivalSeasons"/> (Data/PassiveFestivals, so modded
    /// festivals count) with <see cref="BuiltInFestivalSeasons"/> as the fallback. Explicit
    /// seasons and season tokens still win; an unknown festival is no signal.</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(
        Season? season, string? condition, string? location,
        IReadOnlyDictionary<string, Season>? festivalSeasons = null)
    {
        if (season != null)
            return new[] { season.Value };

        string[] tokens = (condition ?? "").Split(ConditionSeparators, StringSplitOptions.RemoveEmptyEntries);
        bool negated = condition != null && condition.Contains('!');
        if (!negated)
        {
            var found = new List<Season>();
            foreach (string token in tokens)
            {
                // IsDefined: Enum.TryParse accepts any integer ("TIME 0600 1800" would read as
                // two nonsense seasons); only a season NAME is a signal.
                if (Enum.TryParse(token, ignoreCase: true, out Season s)
                    && Enum.IsDefined(typeof(Season), s) && !found.Contains(s))
                    found.Add(s);
            }
            if (found.Count > 0)
                return found;
        }

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
}
