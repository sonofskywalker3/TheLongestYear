using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>What a supported mod's layer says that its game data cannot: routes the data does not
/// describe (<see cref="ExtraSources"/>, by qualified item id, injected as direct sources before the
/// build), year rulings for the Traveling Cart's year 2 slot, and availability week rulings. Written by
/// hand in <c>assets/lexicon/layers/&lt;UniqueId&gt;.inputs.json</c>; <see cref="CheckedVersion"/> is the
/// mod version the layer was mapped against.</summary>
public sealed record LayerInputs(
    string UniqueId, string CheckedVersion,
    IReadOnlyDictionary<string, IReadOnlyList<ObtainSource>> ExtraSources,
    IReadOnlyDictionary<string, YearRuling> YearRulings,
    IReadOnlyDictionary<string, int> WeekRulings, string Notes)
{
    /// <summary>The first 8 hex digits of the SHA-256 of the file text this was read from (empty when
    /// made in code); part of the cache key, so editing a layer file does not reuse a stale entry.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TextHash { get; init; } = "";

    /// <summary>Fishing rows the game data does not hold (a fish a mod hands out in code), written as
    /// Data/Locations fish rows are read (<see cref="LocationSpawn"/>). They join the build's fish rows,
    /// so they give routes and catch rates (<see cref="FishCatchRates"/>) as a data row would.</summary>
    public IReadOnlyList<LocationSpawn> FishSpawns { get; init; } = Array.Empty<LocationSpawn>();

    /// <summary>Shop lines whose item the mod re-picks every day (a Content Patcher <c>{{Random: a, b, c}}</c>
    /// in the line's ItemId), so the data holds a different one of them each day. The live line is read as
    /// all of them, a random pick, before the data is hashed or built: the answers match on any day.</summary>
    public IReadOnlyList<RandomShopItem> RandomShopItems { get; init; } = Array.Empty<RandomShopItem>();

    /// <summary>Per qualified item id, the week (<see cref="YearTwoWeeks.First"/> to <see cref="YearTwoWeeks.Last"/>)
    /// and effort (0 to <see cref="YearTwoWeeks.MaxEffort"/>) a year-2 route gives it, with the reason.</summary>
    public IReadOnlyDictionary<string, YearTwoRuling> YearTwoRulings { get; init; }
        = new Dictionary<string, YearTwoRuling>(StringComparer.Ordinal);

    /// <summary>Maps (by location name) whose data routes are flagged as year 2 or Ginger Island.</summary>
    public IReadOnlyDictionary<string, YearTwoFlag> YearTwoMaps { get; init; }
        = new Dictionary<string, YearTwoFlag>(StringComparer.Ordinal);

    /// <summary>Shops (by shop id) whose data routes are flagged as year 2 or Ginger Island.</summary>
    public IReadOnlyDictionary<string, YearTwoFlag> YearTwoShops { get; init; }
        = new Dictionary<string, YearTwoFlag>(StringComparer.Ordinal);
}

/// <summary>What a year-2 map or shop route is flagged as.</summary>
public enum YearTwoFlag { YearTwo, GingerIsland }

/// <summary>A hand-ruled year-2 availability: <see cref="Week"/>, <see cref="Effort"/> and why (<see cref="Basis"/>).</summary>
public sealed record YearTwoRuling(int Week, int Effort, string Basis);

/// <summary>The bounds of a <see cref="YearTwoRuling"/>.</summary>
public static class YearTwoWeeks
{
    public const int First = 17;
    public const int Last = 32;
    public const int MaxEffort = 12;
}

/// <summary>A shop line that offers one of <see cref="ItemIds"/>, picked by the mod each day: the
/// Data/Shops line of <see cref="ShopId"/> whose item is one of them and, when given, whose Condition
/// reads <see cref="Condition"/> exactly as the game holds it.</summary>
public sealed record RandomShopItem(string ShopId, string? Condition, IReadOnlyList<string> ItemIds);

/// <summary>A layer's answers: what changes over the answers composed without it. <see cref="Mods"/>
/// is one id for a single mod's layer (<c>&lt;A&gt;.answers.json</c>) or several for the interaction
/// of those mods (<c>&lt;A&gt;+&lt;B&gt;.answers.json</c>, ids sorted ordinally).
/// <see cref="CheckedVersions"/> reads <c>"A@1.2.0"</c> or <c>"A@1.2.0+B@3.0.0"</c>.
/// <see cref="VariantRowHashes"/> are, per variant name, the input rows the layer adds or changes in
/// that save state (laid over vanilla's rows for the same variant, like <see cref="AnswerSet.RowHashesFor"/>),
/// rewritten whole each time that variant is written. <see cref="Deltas"/> holds, per variant name, the delta over the composed answers below
/// it (<see cref="LayerSet"/> says how it is laid on); an empty map means the layer was mapped and
/// changes no answer in any variant. Written by <c>tly_lexicon write &lt;UniqueID&gt;</c>.</summary>
public sealed record LayerAnswers(
    IReadOnlyList<string> Mods, string CheckedVersions,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, ulong>> VariantRowHashes,
    IReadOnlyDictionary<string, AnswerVariant> Deltas)
{
    /// <summary>See <see cref="LayerInputs.TextHash"/>.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string TextHash { get; init; } = "";

    /// <summary>Per variant name, what the layer takes away from the answers below it (a mod that
    /// deletes a vanilla row, as Cornucopia deletes the trellis Grape crop): applied after that
    /// variant's delta. A file with none leaves it out or empty.</summary>
    public IReadOnlyDictionary<string, LayerRemovals> Removals { get; init; }
        = new Dictionary<string, LayerRemovals>(StringComparer.Ordinal);
}

/// <summary>The keys a layer removes from the variant below it: input <see cref="Rows"/> (row hash
/// keys), and the <see cref="AnswerVariant"/> entries of the same names. Availability answers are not
/// listed: a variant fills them only as they are computed, so a missing one says nothing.</summary>
public sealed record LayerRemovals(
    IReadOnlyList<string> Rows, IReadOnlyList<string> Obtain, IReadOnlyList<string> YearSort,
    IReadOnlyList<string> Unresolved, IReadOnlyList<string> MapWeeks, IReadOnlyList<string> UnreachedMaps,
    IReadOnlyList<string> ShopWeeks, IReadOnlyList<string> CatchRates)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsEmpty => Rows.Count == 0 && Obtain.Count == 0 && YearSort.Count == 0 && Unresolved.Count == 0
                           && MapWeeks.Count == 0 && UnreachedMaps.Count == 0 && ShopWeeks.Count == 0 && CatchRates.Count == 0;
}

/// <summary>The layer files' names and formats. Both are <see cref="LexiconJson"/> text (sorted keys,
/// enums as strings, day tables as run-length strings); an answers file pools its deltas' day tables
/// per variant like an answer set, an inputs file is small and carries them inline.</summary>
public static class LayerFile
{
    public const string FolderRelativePath = "assets/lexicon/layers";
    public const string InputsSuffix = ".inputs.json";
    public const string AnswersSuffix = ".answers.json";
    /// <summary>Joins the mod ids of an interaction layer, in its name and in the composed set's layers.</summary>
    public const string Joiner = "+";
    private const string DeltasKey = "Deltas";

    public static string Folder(string modFolder) => Path.Combine(modFolder, "assets", "lexicon", "layers");

    /// <summary>The layer's name: its mod ids sorted ordinally and joined with <see cref="Joiner"/>.</summary>
    public static string Name(IEnumerable<string> mods)
        => string.Join(Joiner, mods.OrderBy(m => m, StringComparer.Ordinal));

    public static string InputsFileName(string uniqueId) => uniqueId + InputsSuffix;

    public static string AnswersFileName(IEnumerable<string> mods) => Name(mods) + AnswersSuffix;

    public static string WriteInputs(LayerInputs inputs) => LexiconJson.WriteDocument(inputs, null);

    /// <summary>A hand-written inputs file may leave out a map it has nothing for; it reads as empty.
    /// A null inside one (<c>"ExtraSources": {"(O)x": null}</c>, a null source) is refused with a
    /// <see cref="System.Text.Json.JsonException"/> naming it.</summary>
    public static LayerInputs ReadInputs(string json)
    {
        LayerInputs read = LexiconJson.ReadDocument<LayerInputs>(json, null);
        if (string.IsNullOrWhiteSpace(read.UniqueId)) throw new System.Text.Json.JsonException("A layer inputs file has no UniqueId.");
        if (read.ExtraSources != null)
            foreach (var (id, sources) in read.ExtraSources)
                if (sources == null || sources.Any(src => src == null))
                    throw new System.Text.Json.JsonException($"ExtraSources {id} is null or holds a null source.");
        RequireSourceMembers(json);
        if (read.FishSpawns != null && read.FishSpawns.Any(row => row == null || string.IsNullOrWhiteSpace(row.Location) || string.IsNullOrWhiteSpace(row.ItemId)))
            throw new System.Text.Json.JsonException("FishSpawns holds a null row or a row with no Location or ItemId.");
        if (read.RandomShopItems != null && read.RandomShopItems.Any(r => r == null || string.IsNullOrWhiteSpace(r.ShopId) || r.ItemIds == null || r.ItemIds.Count == 0))
            throw new System.Text.Json.JsonException("RandomShopItems holds a null entry or one with no ShopId or ItemIds.");
        if (read.YearTwoRulings != null)
            foreach (var (id, ruling) in read.YearTwoRulings)
            {
                if (ruling == null) throw new System.Text.Json.JsonException($"YearTwoRulings {id} is null.");
                if (ruling.Week < YearTwoWeeks.First || ruling.Week > YearTwoWeeks.Last)
                    throw new System.Text.Json.JsonException($"YearTwoRulings {id}: Week {ruling.Week} is outside {YearTwoWeeks.First} to {YearTwoWeeks.Last}.");
                if (ruling.Effort < 0 || ruling.Effort > YearTwoWeeks.MaxEffort)
                    throw new System.Text.Json.JsonException($"YearTwoRulings {id}: Effort {ruling.Effort} is outside 0 to {YearTwoWeeks.MaxEffort}.");
                if (string.IsNullOrWhiteSpace(ruling.Basis))
                    throw new System.Text.Json.JsonException($"YearTwoRulings {id}: Basis is null or blank.");
            }
        RequireFlags(json, "YearTwoMaps");
        RequireFlags(json, "YearTwoShops");
        return read with
        {
            CheckedVersion = read.CheckedVersion ?? "",
            YearTwoRulings = ToOrdinal(read.YearTwoRulings),
            YearTwoMaps = ToOrdinal(read.YearTwoMaps),
            YearTwoShops = ToOrdinal(read.YearTwoShops),
            ExtraSources = read.ExtraSources == null
                ? new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal)
                : read.ExtraSources.ToDictionary(
                    p => p.Key,
                    p => (IReadOnlyList<ObtainSource>)p.Value.Select(WithDefaults).ToList(),
                    StringComparer.Ordinal),
            YearRulings = read.YearRulings ?? new Dictionary<string, YearRuling>(StringComparer.Ordinal),
            WeekRulings = read.WeekRulings ?? new Dictionary<string, int>(StringComparer.Ordinal),
            Notes = read.Notes ?? "",
            FishSpawns = read.FishSpawns ?? Array.Empty<LocationSpawn>(),
            RandomShopItems = read.RandomShopItems ?? Array.Empty<RandomShopItem>(),
            TextHash = HashText(json),
        };
    }

    private static IReadOnlyDictionary<string, T> ToOrdinal<T>(IReadOnlyDictionary<string, T>? map)
        => map == null
            ? new Dictionary<string, T>(StringComparer.Ordinal)
            : map.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

    /// <summary>A null flag would read as the first enum value, so it is refused naming the entry.</summary>
    private static void RequireFlags(string json, string section)
    {
        if (System.Text.Json.Nodes.JsonNode.Parse(json)?[section] is not System.Text.Json.Nodes.JsonObject flags) return;
        foreach (var (id, flag) in flags)
            if (flag == null)
                throw new System.Text.Json.JsonException($"{section} {id} is null.");
    }

    private static readonly string[] RequiredSourceMembers = { "Kind", "Lands", "Detail" };

    /// <summary>A source that names no Kind, Lands or Detail cannot be read as intended (a missing Kind
    /// would silently be the first one), so the file is refused naming the item and member.</summary>
    private static void RequireSourceMembers(string json)
    {
        if (System.Text.Json.Nodes.JsonNode.Parse(json)?["ExtraSources"] is not System.Text.Json.Nodes.JsonObject extra) return;
        foreach (var (id, list) in extra)
            if (list is System.Text.Json.Nodes.JsonArray sources)
                foreach (var source in sources)
                    if (source is System.Text.Json.Nodes.JsonObject o)
                        foreach (string member in RequiredSourceMembers)
                            if (o[member] == null)
                                throw new System.Text.Json.JsonException($"ExtraSources {id} holds a source with no {member}.");
    }

    /// <summary>A hand-written source may leave out Conditions, Setup and Inputs; they read as none.</summary>
    private static ObtainSource WithDefaults(ObtainSource src)
        => src with
        {
            Conditions = src.Conditions ?? ObtainConditions.None,
            Setup = src.Setup ?? Array.Empty<SetupStep>(),
            Inputs = src.Inputs ?? Array.Empty<IReadOnlyList<string>>(),
        };

    private const int TextHashChars = 8;

    /// <summary>The first <see cref="TextHashChars"/> lower-case hex digits of the SHA-256 of the UTF-8 text.</summary>
    public static string HashText(string text)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..TextHashChars].ToLowerInvariant();

    public static string WriteAnswers(LayerAnswers answers) => LexiconJson.WriteDocument(answers, DeltasKey);

    public static LayerAnswers ReadAnswers(string json)
    {
        LayerAnswers read = LexiconJson.ReadDocument<LayerAnswers>(json, DeltasKey);
        if (read.Mods == null || read.Mods.Count == 0) throw new System.Text.Json.JsonException("A layer answers file names no Mods.");
        if (read.Mods.Any(string.IsNullOrWhiteSpace)) throw new System.Text.Json.JsonException("A layer answers file names a null or empty mod.");
        if (read.VariantRowHashes != null)
            foreach (var (name, rows) in read.VariantRowHashes)
                if (rows == null) throw new System.Text.Json.JsonException($"VariantRowHashes {name} is null.");
        if (read.Deltas != null)
            foreach (var (name, delta) in read.Deltas)
                if (NullMember(delta) is string member) throw new System.Text.Json.JsonException($"Deltas {name}: {member} is null.");
        if (read.Removals != null)
            foreach (var (name, removal) in read.Removals)
                if (removal == null || removal.Rows == null || removal.Obtain == null || removal.YearSort == null
                    || removal.Unresolved == null || removal.MapWeeks == null || removal.UnreachedMaps == null || removal.ShopWeeks == null || removal.CatchRates == null)
                    throw new System.Text.Json.JsonException($"Removals {name} is null or holds a null list.");
        return read with
        {
            CheckedVersions = read.CheckedVersions ?? "",
            VariantRowHashes = read.VariantRowHashes ?? new Dictionary<string, IReadOnlyDictionary<string, ulong>>(StringComparer.Ordinal),
            Deltas = read.Deltas ?? new Dictionary<string, AnswerVariant>(StringComparer.Ordinal),
            Removals = read.Removals ?? new Dictionary<string, LayerRemovals>(StringComparer.Ordinal),
            TextHash = HashText(json),
        };
    }

    /// <summary>The first member of a read delta that is null (or holds a null), or null when none is.</summary>
    private static string? NullMember(AnswerVariant? delta)
    {
        if (delta == null) return "the variant";
        if (delta.Obtain == null || delta.Obtain.Values.Any(v => v == null || v.Any(src => src == null))) return "Obtain";
        if (delta.YearSort == null || delta.YearSort.Values.Any(v => v == null || v.Any(src => src == null))) return "YearSort";
        if (delta.Unresolved == null) return "Unresolved";
        if (delta.MapWeeks == null || delta.MapWeeks.Values.Any(v => v == null)) return "MapWeeks";
        if (delta.UnreachedMaps == null) return "UnreachedMaps";
        if (delta.ShopWeeks == null || delta.ShopWeeks.Values.Any(v => v == null)) return "ShopWeeks";
        if (delta.Availability == null || delta.Availability.Values.Any(v => v == null)) return "Availability";
        if (delta.CatchRates == null || delta.CatchRates.Values.Any(v => v?.Weekly == null)) return "CatchRates";
        if (delta.YearTwo == null || delta.YearTwo.Values.Any(v => v == null)) return "YearTwo";
        return null;
    }

    /// <summary>Every layer file in <paramref name="folder"/>, in ordinal file-name order (the compose
    /// order of single-mod deltas). A missing folder has none. A file that cannot be read or parsed is
    /// reported to <paramref name="warn"/> and skipped, so one bad layer does not cost the others; a
    /// folder that cannot be listed is reported and has none.</summary>
    public static (IReadOnlyList<LayerInputs> Inputs, IReadOnlyList<LayerAnswers> Answers) ReadFolder(string folder, Action<string> warn)
    {
        var inputs = new List<LayerInputs>();
        var answers = new List<LayerAnswers>();
        if (!Directory.Exists(folder)) return (inputs, answers);
        string[] files;
        try
        {
            files = Directory.GetFiles(folder);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            warn($"Lexicon layer folder {folder} could not be listed ({e.GetType().Name}: {e.Message}); no layers applied.");
            return (inputs, answers);
        }
        foreach (string path in files.OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal))
        {
            string name = Path.GetFileName(path);
            bool isInputs = name.EndsWith(InputsSuffix, StringComparison.Ordinal);
            bool isAnswers = name.EndsWith(AnswersSuffix, StringComparison.Ordinal);
            if (!isInputs && !isAnswers) continue;
            try
            {
                string json = File.ReadAllText(path);
                if (isInputs) inputs.Add(ReadInputs(json));
                else answers.Add(ReadAnswers(json));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                warn($"Lexicon layer file {name} could not be read ({e.GetType().Name}: {e.Message}); skipping it.");
            }
        }
        return (inputs, answers);
    }
}
