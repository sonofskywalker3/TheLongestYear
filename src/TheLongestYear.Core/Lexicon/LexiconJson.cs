using System;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>The answer file format: JSON with ordinally sorted keys at every level, one dictionary
/// entry per line, one source per line, enums as strings and day tables as run-length text.
/// The same set always writes the same bytes.</summary>
public static class LexiconJson
{
    private const string Indent = "  ";
    private const int MaxInlineScalars = 8;

    public static JsonSerializerOptions Options { get; } = BuildOptions();

    public static string Write(AnswerSet s)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(s, Options);
        PoolTables(node);
        var sb = new StringBuilder();
        Emit(sb, node, 0, compact: false);
        sb.Append('\n');
        return sb.ToString();
    }

    public static AnswerSet Read(string json)
    {
        JsonNode? node = JsonNode.Parse(json);
        ResolveTables(node);
        return node.Deserialize<AnswerSet>(Options)
               ?? throw new JsonException("The answer file deserialised to null.");
    }

    private const string TablesKey = "Tables";
    private static readonly string[] SourceTableKeys = { "Lands", "UndelayedLands" };
    private static readonly string[] SourceMapKeys = { "Obtain", "YearSort" };

    private static System.Collections.Generic.IEnumerable<JsonObject> Sources(JsonObject variant)
    {
        foreach (string mapKey in SourceMapKeys)
            if (variant[mapKey] is JsonObject map)
                foreach (var entry in map)
                    if (entry.Value is JsonArray list)
                        foreach (JsonNode? source in list)
                            if (source is JsonObject o) yield return o;
    }

    private static System.Collections.Generic.IEnumerable<JsonObject> Variants(JsonNode? root)
        => root?["Variants"] is JsonObject variants
            ? variants.Select(p => p.Value).OfType<JsonObject>()
            : Enumerable.Empty<JsonObject>();

    /// <summary>Per variant: replaces each source's run strings with an index into a sorted pool of the
    /// distinct strings, and writes the pool as the variant's <c>Tables</c>.</summary>
    private static void PoolTables(JsonNode? root)
    {
        foreach (JsonObject variant in Variants(root))
        {
            var sources = Sources(variant).ToList();
            var pool = sources.SelectMany(o => SourceTableKeys.Select(k => o[k]?.GetValue<string>()))
                .Where(t => t != null).Select(t => t!).Distinct().OrderBy(t => t, StringComparer.Ordinal).ToList();
            var index = pool.Select((t, i) => (t, i)).ToDictionary(p => p.t, p => p.i, StringComparer.Ordinal);
            foreach (JsonObject source in sources)
                foreach (string key in SourceTableKeys)
                    if (source[key]?.GetValue<string>() is string runs) source[key] = index[runs];
            variant[TablesKey] = new JsonArray(pool.Select(t => (JsonNode?)JsonValue.Create(t)).ToArray());
        }
    }

    private static void ResolveTables(JsonNode? root)
    {
        foreach (JsonObject variant in Variants(root))
        {
            if (variant[TablesKey] is not JsonArray tables) throw new JsonException("A variant has no Tables pool.");
            foreach (JsonObject source in Sources(variant))
                foreach (string key in SourceTableKeys)
                {
                    if (source[key] is not JsonValue v) continue;
                    if (!v.TryGetValue(out int i) || i < 0 || i >= tables.Count)
                        throw new JsonException($"Table index out of range in '{key}'.");
                    source[key] = tables[i]!.GetValue<string>();
                }
        }
    }

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // "+" and non-ASCII stay readable
        };
        options.Converters.Add(new JsonStringEnumConverter());
        options.Converters.Add(new DayTableConverter());
        return options;
    }

    private sealed class DayTableConverter : JsonConverter<DayTable>
    {
        public override DayTable Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            try { return DayTableRuns.Read(reader.GetString() ?? ""); }
            catch (FormatException e) { throw new JsonException(e.Message, e); }
        }

        public override void Write(Utf8JsonWriter writer, DayTable value, JsonSerializerOptions options)
            => writer.WriteStringValue(DayTableRuns.Write(value));
    }

    /// <summary>Objects are one entry per line with sorted keys; an object that is an array element
    /// (a source, a setup step) is written on one line, as is a short scalar array; a long one
    /// (the table pool) takes a line per element.</summary>
    private static void Emit(StringBuilder sb, JsonNode? node, int depth, bool compact)
    {
        switch (node)
        {
            case null:
                sb.Append("null");
                break;
            case JsonObject obj:
                EmitObject(sb, obj, depth, compact);
                break;
            case JsonArray arr:
                EmitArray(sb, arr, depth, compact);
                break;
            default:
                sb.Append(node.ToJsonString(Options));
                break;
        }
    }

    private static void EmitObject(StringBuilder sb, JsonObject obj, int depth, bool compact)
    {
        var entries = obj.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();
        if (entries.Count == 0) { sb.Append("{}"); return; }
        sb.Append('{');
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(compact ? ", " : ",");
            if (!compact) sb.Append('\n').Append(Pad(depth + 1));
            sb.Append(JsonSerializer.Serialize(entries[i].Key)).Append(": ");
            Emit(sb, entries[i].Value, depth + 1, compact);
        }
        if (!compact) sb.Append('\n').Append(Pad(depth));
        sb.Append('}');
    }

    private static void EmitArray(StringBuilder sb, JsonArray arr, int depth, bool compact)
    {
        if (arr.Count == 0) { sb.Append("[]"); return; }
        bool hasObjects = arr.Any(e => e is JsonObject) || arr.Count > MaxInlineScalars;
        sb.Append('[');
        if (!hasObjects || compact)
        {
            for (int i = 0; i < arr.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                Emit(sb, arr[i], depth + 1, compact: true);
            }
            sb.Append(']');
            return;
        }
        for (int i = 0; i < arr.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('\n').Append(Pad(depth + 1));
            Emit(sb, arr[i], depth + 1, compact: true);
        }
        sb.Append('\n').Append(Pad(depth)).Append(']');
    }

    private static string Pad(int depth) => string.Concat(Enumerable.Repeat(Indent, depth));
}
