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

    public static JsonSerializerOptions Options { get; } = BuildOptions();

    public static string Write(AnswerSet s)
    {
        JsonNode? node = JsonSerializer.SerializeToNode(s, Options);
        var sb = new StringBuilder();
        Emit(sb, node, 0, compact: false);
        sb.Append('\n');
        return sb.ToString();
    }

    public static AnswerSet Read(string json)
        => JsonSerializer.Deserialize<AnswerSet>(json, Options)
           ?? throw new JsonException("The answer file deserialised to null.");

    private static JsonSerializerOptions BuildOptions()
    {
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
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
    /// (a source, a setup step) and every scalar array is written on one line.</summary>
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
        bool hasObjects = arr.Any(e => e is JsonObject);
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
