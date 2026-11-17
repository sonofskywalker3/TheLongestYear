using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;

namespace TheLongestYear.Core.Lexicon;

/// <summary>Fields of data entries a supported mod's layer pins (<see cref="LayerInputs.DataFields"/>).
///
/// A mod can change one field of an entry once the player has seen an event (East Scarp makes
/// Starfruit regrow after a late Junimo scene, and raises the Bouquet's price after one of Aideen's).
/// The answers are kept for one reading of the data, so a save past the event would no longer match
/// them and would build its own. A pinned field is read as the layer's value whatever the game holds,
/// which is the entry as a new farm has it. The game's own objects are never changed: a pinned entry
/// is read from a copy.</summary>
public static class PinnedDataFields
{
    private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public;
    private static readonly MethodInfo Clone =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>A copy of a string-keyed data dictionary with each pinned entry replaced by a copy
    /// that holds the pinned values. An entry the game does not hold, and a field the entry's type
    /// does not have, change nothing. Returns <paramref name="live"/> itself when nothing applies.</summary>
    public static object Apply(object live, IReadOnlyDictionary<string, IReadOnlyDictionary<string, JsonElement>>? pins)
    {
        if (live == null) throw new ArgumentNullException(nameof(live));
        if (pins == null || pins.Count == 0 || live is not IDictionary entries) return live;
        Type type = live.GetType();
        if (!type.IsGenericType || type.GetGenericArguments()[0] != typeof(string)) return live;

        IDictionary? read = null;
        foreach (var (key, fields) in pins)
        {
            if (!entries.Contains(key) || entries[key] is not object entry || entry.GetType().IsValueType || entry is string) continue;
            object copy = Clone.Invoke(entry, null)!;
            bool changed = false;
            foreach (var (name, value) in fields)
                changed |= Set(copy, name, value);
            if (!changed) continue;
            if (read == null)
            {
                read = (IDictionary)Activator.CreateInstance(type)!;
                foreach (DictionaryEntry pair in entries) read[pair.Key] = pair.Value;
            }
            read[key] = copy;
        }
        return read ?? live;
    }

    private static bool Set(object entry, string name, JsonElement value)
    {
        Type type = entry.GetType();
        if (type.GetField(name, Members) is FieldInfo field)
        {
            field.SetValue(entry, value.Deserialize(field.FieldType));
            return true;
        }
        if (type.GetProperty(name, Members) is PropertyInfo { CanWrite: true } property)
        {
            property.SetValue(entry, value.Deserialize(property.PropertyType));
            return true;
        }
        return false;
    }
}
