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
}

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

    /// <summary>A hand-written inputs file may leave out a map it has nothing for; it reads as empty.</summary>
    public static LayerInputs ReadInputs(string json)
    {
        LayerInputs read = LexiconJson.ReadDocument<LayerInputs>(json, null);
        if (string.IsNullOrWhiteSpace(read.UniqueId)) throw new System.Text.Json.JsonException("A layer inputs file has no UniqueId.");
        return read with
        {
            CheckedVersion = read.CheckedVersion ?? "",
            ExtraSources = read.ExtraSources ?? new Dictionary<string, IReadOnlyList<ObtainSource>>(StringComparer.Ordinal),
            YearRulings = read.YearRulings ?? new Dictionary<string, YearRuling>(StringComparer.Ordinal),
            WeekRulings = read.WeekRulings ?? new Dictionary<string, int>(StringComparer.Ordinal),
            Notes = read.Notes ?? "",
            TextHash = HashText(json),
        };
    }

    private const int TextHashChars = 8;

    /// <summary>The first <see cref="TextHashChars"/> lower-case hex digits of the SHA-256 of the UTF-8 text.</summary>
    public static string HashText(string text)
        => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..TextHashChars].ToLowerInvariant();

    public static string WriteAnswers(LayerAnswers answers) => LexiconJson.WriteDocument(answers, DeltasKey);

    public static LayerAnswers ReadAnswers(string json)
    {
        LayerAnswers read = LexiconJson.ReadDocument<LayerAnswers>(json, DeltasKey);
        if (read.Mods == null || read.Mods.Count == 0) throw new System.Text.Json.JsonException("A layer answers file names no Mods.");
        return read with
        {
            CheckedVersions = read.CheckedVersions ?? "",
            VariantRowHashes = read.VariantRowHashes ?? new Dictionary<string, IReadOnlyDictionary<string, ulong>>(StringComparer.Ordinal),
            Deltas = read.Deltas ?? new Dictionary<string, AnswerVariant>(StringComparer.Ordinal),
            TextHash = HashText(json),
        };
    }

    /// <summary>Every layer file in <paramref name="folder"/>, in ordinal file-name order (the compose
    /// order of single-mod deltas). A missing folder has none. A file that cannot be read or parsed is
    /// reported to <paramref name="warn"/> and skipped, so one bad layer does not cost the others.</summary>
    public static (IReadOnlyList<LayerInputs> Inputs, IReadOnlyList<LayerAnswers> Answers) ReadFolder(string folder, Action<string> warn)
    {
        var inputs = new List<LayerInputs>();
        var answers = new List<LayerAnswers>();
        if (!Directory.Exists(folder)) return (inputs, answers);
        foreach (string path in Directory.GetFiles(folder).OrderBy(p => Path.GetFileName(p), StringComparer.Ordinal))
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
