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
    /// the Condition's season queries allow (<see cref="SeasonClauses.Read"/>); otherwise
    /// empty = any season. An impossible row also comes back empty here; the pool builders use
    /// <see cref="ReadSpawnSeasons"/>, which tells the two apart and drops impossible rows.</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(Season? season, string? condition)
        => SeasonsFromSpawn(season, condition, null, null);

    /// <summary>As above, plus the passive-festival rule (see <see cref="ReadSpawnSeasons"/>).</summary>
    public static IReadOnlyList<Season> SeasonsFromSpawn(
        Season? season, string? condition, string? location,
        IReadOnlyDictionary<string, Season>? festivalSeasons = null)
        => ReadSpawnSeasons(season, condition, location, festivalSeasons).Seasons;

    /// <summary>The Data/Locations entry every map inherits rows from: its rows run at many maps,
    /// so "Here" in them is no one location.</summary>
    public const string SharedDefaultLocation = "Default";

    /// <summary>True when a row's season clauses rule out every season, so the row never spawns
    /// and the pool builders drop it (the caller logs these).</summary>
    public static bool IsSeasonImpossible(Season? season, string? condition, string? location = null,
        IReadOnlyDictionary<string, SeasonSource>? locationSeasons = null)
        => ReadSpawnSeasons(season, condition, location, null, locationSeasons).Verdict == SeasonVerdict.Impossible;

    /// <summary>Whose season the row's location follows. The shared Default entry is always
    /// Unknown. With no table (pools and rows built by hand in tests) any other location reads as
    /// Calendar; with a table (the game's pool build always passes one), a location missing from it
    /// is Unknown, never assumed to be the calendar.</summary>
    public static SeasonSource SeasonSourceOf(string? location, IReadOnlyDictionary<string, SeasonSource>? locationSeasons)
    {
        if (string.Equals(location, SharedDefaultLocation, StringComparison.OrdinalIgnoreCase))
            return SeasonSource.Unknown;
        if (locationSeasons == null) return SeasonSource.Calendar;
        if (string.IsNullOrEmpty(location)) return SeasonSource.Unknown;
        return locationSeasons.TryGetValue(location, out SeasonSource source) ? source : SeasonSource.Unknown;
    }

    /// <summary>The season verdict for one spawn entry.
    /// <list type="bullet">
    /// <item>The explicit Season field: the game compares it with the LOCATION's season
    /// (GameLocation.GetSeason), so it is a calendar season only at a <see cref="SeasonSource.Calendar"/>
    /// location; elsewhere it is no signal (a safe superset).</item>
    /// <item>Otherwise the Condition's season queries decide (<see cref="SeasonClauses.Read"/>): these
    /// seasons, or impossible (the row never fires). LOCATION_SEASON Here / Target narrows only at a
    /// Calendar location: Here is the map being fished, and the Default entry's rows run at many.</item>
    /// <item>With no season signal, the passive-festival rule: a row on a festival-only map
    /// (<see cref="BuiltInFestivalLocations"/>) or conditioned on a top-level
    /// <c>IS_PASSIVE_FESTIVAL_OPEN &lt;id&gt;</c> is only reachable in that festival's season,
    /// looked up in <paramref name="festivalSeasons"/> (Data/PassiveFestivals, so modded festivals
    /// count) with <see cref="BuiltInFestivalSeasons"/> as the fallback; an unknown festival is no
    /// signal.</item>
    /// </list>
    /// Player reports 2026-10-29: Rainbow Trout and Squid were asked for in Spring weeks 1-2. A map
    /// mod's row such as "LOCATION_SEASON Here summer, !WEATHER Here rain" used to read as any
    /// season, because a '!' anywhere in the string meant "no season signal", and one any-season
    /// row makes the whole fish any-season.</summary>
    /// <param name="locationSeasons">Each Data/Locations key's <see cref="SeasonSource"/>
    /// (see <see cref="SeasonSourceOf"/>).</param>
    public static SeasonReading ReadSpawnSeasons(
        Season? season, string? condition, string? location,
        IReadOnlyDictionary<string, Season>? festivalSeasons = null,
        IReadOnlyDictionary<string, SeasonSource>? locationSeasons = null)
    {
        bool calendar = SeasonSourceOf(location, locationSeasons) == SeasonSource.Calendar;
        if (season != null && calendar)
            return new SeasonReading(SeasonVerdict.Seasons, new[] { season.Value });

        SeasonReading fromCondition = SeasonClauses.Read(condition, locationHasOwnSeason: !calendar);
        if (fromCondition.Verdict != SeasonVerdict.Unknown)
            return fromCondition;

        string? festival = null;
        if (!string.IsNullOrEmpty(location) && BuiltInFestivalLocations.TryGetValue(location, out string? byLocation))
            festival = byLocation;
        // Only a top-level, non-negated clause: a festival inside ANY is one alternative, not a gate.
        festival ??= SeasonClauses.PassiveFestivalsRequired(condition).FirstOrDefault();
        if (festival != null)
        {
            if (festivalSeasons != null && festivalSeasons.TryGetValue(festival, out Season fromData))
                return new SeasonReading(SeasonVerdict.Seasons, new[] { fromData });
            if (BuiltInFestivalSeasons.TryGetValue(festival, out Season builtIn))
                return new SeasonReading(SeasonVerdict.Seasons, new[] { builtIn });
        }
        return SeasonReading.Unknown;
    }
}
