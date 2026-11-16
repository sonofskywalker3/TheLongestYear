using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TheLongestYear.Core.Lexicon;

/// <summary>The ids of every game data entry the shipped answers were built from: vanilla's, and
/// each active supported-mod layer's. A reader that is given one skips every entry that is not in it,
/// so content from a mod with no layer is never read (spec 2026-11-12-ignore-unsupported-mods).
/// <para>An id is <c>asset|key</c> for a top-level entry (<c>Data/Locations|Forest</c>) and
/// <c>asset|key|path</c> for a row inside one, the path being the list's member name and the row's
/// own <c>Id</c> (<c>Data/Locations|Forest|Fish/(O)136</c>), repeated for a list inside a row
/// (<c>Data/Machines|(BC)12|OutputRules/Default/OutputItem/Wine</c>). A row with no <c>Id</c> is
/// named by a digest of its own plain values (<c>~9f3a...</c>), so a row a mod inserts or changes
/// never takes a known row's name, whatever its place in the list.</para></summary>
public sealed class KnownIds
{
    public const char Separator = '|';
    public const char PathSeparator = '/';

    private readonly HashSet<string> _ids;

    public KnownIds(IEnumerable<string> ids) => _ids = new HashSet<string>(ids, StringComparer.Ordinal);

    public int Count => _ids.Count;

    public bool Has(string id) => _ids.Contains(id);

    public bool HasEntry(string asset, string key) => _ids.Contains(EntryId(asset, key));

    public static string EntryId(string asset, string key) => asset + Separator + key;

    public static string RowId(string entryId, string path) => entryId + Separator + path;

    /// <summary>This set with <paramref name="more"/> added (a layer's ids over vanilla's).</summary>
    public KnownIds With(IEnumerable<string> more) => new(_ids.Concat(more));

    /// <summary>The ids in ordinal order, as a file holds them.</summary>
    public IReadOnlyList<string> Sorted() => _ids.OrderBy(i => i, StringComparer.Ordinal).ToList();
}

/// <summary>Walks one game data asset by reflection, to list its ids (<see cref="Ids"/>) or to give a
/// copy holding only known ones (<see cref="Filter{T}"/>). It knows no game type: a top-level
/// dictionary is keyed by its own keys, a top-level list by each element's <c>Id</c>, any other object
/// is one entry with an empty key; inside an entry, every list whose elements carry a string
/// <c>Id</c> is a list of rows, and every string-keyed dictionary of objects is walked by key.
/// The game's own objects are never changed: an entry that loses a row is shallow-copied first.</summary>
public static class AssetIds
{
    private const string IdMember = "Id";
    private const string NoKey = "";
    private static readonly ConcurrentDictionary<Type, Shape> Shapes = new();
    private static readonly MethodInfo Clone =
        typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic)!;

    /// <summary>Every id in <paramref name="data"/>, entries and rows alike.</summary>
    public static IEnumerable<string> Ids(string asset, object? data)
    {
        foreach (var (key, entry) in Entries(data))
        {
            string entryId = KnownIds.EntryId(asset, key);
            yield return entryId;
            foreach (string path in RowPaths(entry, ""))
                yield return KnownIds.RowId(entryId, path);
        }
    }

    /// <summary>A copy of <paramref name="data"/> without the entries and rows <paramref name="known"/>
    /// does not hold; <paramref name="data"/> itself when <paramref name="known"/> is null or nothing
    /// is dropped from a non-dictionary. <paramref name="dropped"/> gets each id left out.</summary>
    public static T Filter<T>(string asset, T data, KnownIds? known, Action<string>? dropped = null) where T : class
    {
        if (known == null || data == null) return data;
        if (data is IDictionary dictionary && IsStringKeyed(data.GetType()))
        {
            var result = (IDictionary)Activator.CreateInstance(data.GetType())!;
            foreach (DictionaryEntry pair in dictionary)
            {
                string entryId = KnownIds.EntryId(asset, (string)pair.Key);
                if (!known.Has(entryId)) { dropped?.Invoke(entryId); continue; }
                result[pair.Key] = FilterRows(pair.Value, entryId, "", known, dropped);
            }
            return (T)result;
        }
        if (data is IList list && RowIdOf(ElementType(data.GetType())) != null)
        {
            var result = (IList)Activator.CreateInstance(data.GetType())!;
            foreach (object? element in list)
            {
                string entryId = KnownIds.EntryId(asset, RowKey(element));
                if (!known.Has(entryId)) { dropped?.Invoke(entryId); continue; }
                result.Add(FilterRows(element, entryId, "", known, dropped));
            }
            return (T)result;
        }
        string single = KnownIds.EntryId(asset, NoKey);
        return (T)FilterRows(data, single, "", known, dropped)!;
    }

    private static IEnumerable<(string Key, object? Entry)> Entries(object? data)
    {
        if (data == null) yield break;
        if (data is IDictionary dictionary && IsStringKeyed(data.GetType()))
        {
            foreach (DictionaryEntry pair in dictionary) yield return ((string)pair.Key, pair.Value);
            yield break;
        }
        if (data is IList list && RowIdOf(ElementType(data.GetType())) != null)
        {
            foreach (object? element in list) yield return (RowKey(element), element);
            yield break;
        }
        yield return (NoKey, data);
    }

    private static IEnumerable<string> RowPaths(object? entry, string prefix)
    {
        if (entry == null) yield break;
        foreach (Member member in ShapeOf(entry.GetType()).Members)
        {
            object? value = member.Get(entry);
            if (value == null) continue;
            if (member.IsRowList)
            {
                foreach (object? row in (IEnumerable)value)
                {
                    string path = prefix + member.Name + KnownIds.PathSeparator + RowKey(row);
                    yield return path;
                    foreach (string inner in RowPaths(row, path + KnownIds.PathSeparator)) yield return inner;
                }
            }
            else
                foreach (DictionaryEntry pair in (IDictionary)value)
                {
                    string path = prefix + member.Name + KnownIds.PathSeparator + (string)pair.Key;
                    yield return path;
                    foreach (string inner in RowPaths(pair.Value, path + KnownIds.PathSeparator)) yield return inner;
                }
        }
    }

    private static object? FilterRows(object? entry, string entryId, string prefix, KnownIds known, Action<string>? dropped)
    {
        if (entry == null) return null;
        object? copy = null;
        foreach (Member member in ShapeOf(entry.GetType()).Members)
        {
            object? value = member.Get(entry);
            if (value == null) continue;
            object kept;
            bool changed = false;
            if (member.IsRowList)
            {
                var rows = (IList)Activator.CreateInstance(value.GetType())!;
                foreach (object? row in (IEnumerable)value)
                {
                    string path = prefix + member.Name + KnownIds.PathSeparator + RowKey(row);
                    string rowId = KnownIds.RowId(entryId, path);
                    if (!known.Has(rowId)) { dropped?.Invoke(rowId); changed = true; continue; }
                    object? filtered = FilterRows(row, entryId, path + KnownIds.PathSeparator, known, dropped);
                    changed |= !ReferenceEquals(filtered, row);
                    rows.Add(filtered);
                }
                kept = rows;
            }
            else
            {
                var entries = (IDictionary)Activator.CreateInstance(value.GetType())!;
                foreach (DictionaryEntry pair in (IDictionary)value)
                {
                    string path = prefix + member.Name + KnownIds.PathSeparator + (string)pair.Key;
                    string rowId = KnownIds.RowId(entryId, path);
                    if (!known.Has(rowId)) { dropped?.Invoke(rowId); changed = true; continue; }
                    object? filtered = FilterRows(pair.Value, entryId, path + KnownIds.PathSeparator, known, dropped);
                    changed |= !ReferenceEquals(filtered, pair.Value);
                    entries[pair.Key] = filtered;
                }
                kept = entries;
            }
            if (!changed) continue;
            copy ??= Clone.Invoke(entry, null);
            member.Set(copy!, kept);
        }
        return copy ?? entry;
    }

    private const string DigestPrefix = "~";
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;

    /// <summary>A row's id: its <c>Id</c>, or with none a digest of its plain values (strings,
    /// numbers, flags and enums, and lists of them such as required tags, by member name), which does
    /// not depend on its place in the list.</summary>
    public static string RowKey(object? row)
    {
        if (row == null) return DigestPrefix + "null";
        string? id = RowIdOf(row.GetType())?.Invoke(row);
        if (!string.IsNullOrEmpty(id)) return id;
        ulong hash = FnvOffset;
        foreach (Func<object, string> plain in PlainOf(row.GetType()))
            foreach (char c in plain(row))
                hash = (hash ^ c) * FnvPrime;
        return DigestPrefix + hash.ToString("x16");
    }

    private static readonly ConcurrentDictionary<Type, IReadOnlyList<Func<object, string>>> Plains = new();

    private static IReadOnlyList<Func<object, string>> PlainOf(Type type) => Plains.GetOrAdd(type, t =>
    {
        const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;
        static bool IsPlain(Type m) => m == typeof(string) || m.IsPrimitive || m.IsEnum || m == typeof(decimal)
            || (Nullable.GetUnderlyingType(m) is Type inner && (inner.IsPrimitive || inner.IsEnum));
        static bool IsPlainList(Type m) => m != typeof(string) && typeof(IEnumerable).IsAssignableFrom(m)
            && (m.IsArray ? m.GetElementType() : m.IsGenericType ? m.GetGenericArguments()[^1] : null) is Type element && IsPlain(element);
        static string One(object? value) => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        static string Text(string name, object? value)
            => name + "=" + (value is IEnumerable list and not string
                ? "[" + string.Join(",", list.Cast<object?>().Select(One)) + "]"
                : One(value)) + ";";
        var readers = new List<(string Name, Func<object, string> Read)>();
        foreach (FieldInfo field in t.GetFields(Public))
            if (IsPlain(field.FieldType) || IsPlainList(field.FieldType)) readers.Add((field.Name, o => Text(field.Name, field.GetValue(o))));
        foreach (PropertyInfo property in t.GetProperties(Public))
            if (property.CanRead && property.GetIndexParameters().Length == 0
                && (IsPlain(property.PropertyType) || IsPlainList(property.PropertyType)))
                readers.Add((property.Name, o => Text(property.Name, property.GetValue(o))));
        return readers.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => r.Read).ToList();
    });

    private static Shape ShapeOf(Type type) => Shapes.GetOrAdd(type, Shape.Of);

    private static bool IsStringKeyed(Type type)
        => DictionaryTypes(type) is { } types && types.Key == typeof(string);

    private static (Type Key, Type Value)? DictionaryTypes(Type type)
    {
        Type? found = type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDictionary<,>));
        return found == null ? null : (found.GetGenericArguments()[0], found.GetGenericArguments()[1]);
    }

    private static Type? ElementType(Type type)
        => type.GetInterfaces().Append(type)
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>))?.GetGenericArguments()[0];

    private static Func<object, string?>? RowIdOf(Type? type)
    {
        if (type == null || type == typeof(string) || type.IsValueType) return null;
        const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;
        if (type.GetProperty(IdMember, Public) is { CanRead: true } property && property.PropertyType == typeof(string))
            return o => (string?)property.GetValue(o);
        if (type.GetField(IdMember, Public) is { } field && field.FieldType == typeof(string))
            return o => (string?)field.GetValue(o);
        return null;
    }

    private static bool IsWalkable(Type? type) => type != null && type.IsClass && type != typeof(string);

    private sealed record Member(string Name, bool IsRowList, Func<object, object?> Get, Action<object, object> Set);

    private sealed record Shape(IReadOnlyList<Member> Members)
    {
        public static Shape Of(Type type)
        {
            var members = new List<Member>();
            const BindingFlags Public = BindingFlags.Instance | BindingFlags.Public;
            foreach (FieldInfo field in type.GetFields(Public))
                if (!field.IsInitOnly && Kind(field.FieldType) is bool rows)
                    members.Add(new Member(field.Name, rows, field.GetValue, field.SetValue));
            foreach (PropertyInfo property in type.GetProperties(Public))
                if (property.CanRead && property.CanWrite && property.GetIndexParameters().Length == 0
                    && Kind(property.PropertyType) is bool rows)
                    members.Add(new Member(property.Name, rows, property.GetValue, property.SetValue));
            return new Shape(members.OrderBy(m => m.Name, StringComparer.Ordinal).ToList());
        }

        /// <summary>True for a list of rows with an Id, false for a string-keyed dictionary of
        /// objects, null for anything else.</summary>
        private static bool? Kind(Type type)
        {
            if (type == typeof(string) || !type.IsClass || type.GetConstructor(Type.EmptyTypes) == null) return null;
            if (typeof(IList).IsAssignableFrom(type) && RowIdOf(ElementType(type)) != null) return true;
            if (typeof(IDictionary).IsAssignableFrom(type) && DictionaryTypes(type) is { } types
                && types.Key == typeof(string) && IsWalkable(types.Value))
                return false;
            return null;
        }
    }
}
