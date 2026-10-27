using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TheLongestYear.Core.Obtainability;

namespace TheLongestYear.Core.Lexicon;

/// <summary>A stable key and a 64-bit hash for every raw input row, so a cached answer can tell the
/// data it was computed from apart from data that has since changed, and say which mod owns a changed row.
/// A row's hash is FNV-1a 64 over the UTF-8 of its <see cref="LexiconJson.Options"/> JSON (records
/// serialise their properties in declaration order, so the text is deterministic).</summary>
public static class RowHashes
{
    public const string Vanilla = "vanilla";
    private const string MetaType = "meta";
    private const string PierreKey = "meta|pierre_year2";
    private const ulong FnvOffset = 14695981039346656037UL;
    private const ulong FnvPrime = 1099511628211UL;
    private static readonly Regex QualifiedId = new(@"\([A-Za-z]+\)[^\s|,]+", RegexOptions.Compiled);
    private static readonly char[] PrefixEnd = { '.', '_' };

    public static IReadOnlyDictionary<string, ulong> Of(ObtainabilityInputs inputs, bool pierreYear2Owned)
        => Of(Rows(inputs, pierreYear2Owned));

    /// <summary>The hashes of rows already listed by <see cref="Rows"/>.</summary>
    internal static IReadOnlyDictionary<string, ulong> Of(IReadOnlyList<(string Key, object Row)> listed)
    {
        var rows = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (var (key, row) in listed) rows[key] = Hash(row);
        return rows;
    }

    /// <summary>Every input row under its key (the rows <see cref="Of"/> hashes), in list order.</summary>
    public static IReadOnlyList<(string Key, object Row)> Rows(ObtainabilityInputs inputs, bool pierreYear2Owned)
    {
        if (inputs is null) throw new ArgumentNullException(nameof(inputs));
        var rows = new List<(string Key, object Row)>();
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        // First occurrence keeps the plain key; a repeat gets |1, |2... in list order, so no row is dropped.
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        void Put(string key, object row)
        {
            seen.TryGetValue(key, out int dupes);
            seen[key] = dupes + 1;
            rows.Add((dupes == 0 ? key : key + "|" + dupes, row));
        }
        // Rows that repeat under one key get a list-order suffix.
        void PutN(string key, object row)
        {
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
            rows.Add((key + "|" + n, row));
        }
        static string S(Season? s) => s?.ToString() ?? "";

        foreach (var (id, o) in inputs.Objects) Put("obj|" + id, o);
        foreach (var (id, f) in inputs.Festivals) Put("fest|" + id, f);
        foreach (var r in inputs.Forage) PutN($"forage|{r.Location}|{r.ItemId}|{S(r.Season)}|{r.Condition}", r);
        foreach (var r in inputs.LocationFish) PutN($"fish|{r.Location}|{r.ItemId}|{S(r.Season)}|{r.Condition}", r);
        foreach (var (id, r) in inputs.FishRows) Put("fishrow|" + id, r);
        foreach (var r in inputs.ArtifactSpots) PutN($"spot|{r.Location}|{r.ItemId}", r);
        foreach (var r in inputs.Garbage) PutN($"garbage|{r.CanId}|{r.ItemId}", r);
        foreach (var r in inputs.Shops) PutN($"shop|{r.ShopId}|{r.ItemId}|{r.Condition}", r);
        foreach (var r in inputs.MonsterDrops) PutN($"drop|{r.Monster}|{r.ItemId}", r);
        foreach (var r in inputs.Crops) Put("crop|" + r.SeedId, r);
        foreach (var r in inputs.FruitTrees) Put("tree|" + r.SaplingId, r);
        foreach (var r in inputs.Machines) PutN("machine|" + r.MachineId, r);
        foreach (var r in inputs.Recipes) Put("recipe|" + r.Name, r);
        foreach (var r in inputs.Animals) Put("animal|" + r.AnimalId, r);
        foreach (var r in inputs.Ponds) Put("pond|" + r.Id, r);
        foreach (var r in inputs.TapItems) PutN($"tap|{r.TreeId}|{r.ItemId}", r);
        foreach (var r in inputs.GeodeDrops) PutN($"geode|{r.GeodeId}|{r.ItemId}", r);
        foreach (string id in inputs.GeodesUsingDefaultTable) Put("geodedefault|" + id, id);
        foreach (var r in inputs.SlayerQuests) Put("slayer|" + r.Id, r);
        foreach (var (name, days) in inputs.Buildings) Put("building|" + name, days);
        foreach (var (recipe, episode) in inputs.CookingChannel) Put("tv|" + recipe, episode);
        Put(PierreKey, pierreYear2Owned);
        return rows;
    }

    public static string Key(IReadOnlyDictionary<string, ulong> rows, IReadOnlyList<string> layers, string gameVersion, string modVersion)
    {
        var lines = rows.Select(p => $"{p.Key}={p.Value:x16}").ToList();
        lines.Add("layers=" + string.Join(",", layers));
        lines.Add("game=" + gameVersion);
        lines.Add("mod=" + modVersion);
        lines.Sort(StringComparer.Ordinal);
        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", lines)));
        return Convert.ToHexString(digest).ToLowerInvariant();
    }

    public static (IReadOnlyList<string> Added, IReadOnlyList<string> Removed, IReadOnlyList<string> Changed) Diff(
        IReadOnlyDictionary<string, ulong> baseRows, IReadOnlyDictionary<string, ulong> liveRows)
    {
        var added = liveRows.Keys.Where(k => !baseRows.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var removed = baseRows.Keys.Where(k => !liveRows.ContainsKey(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
        var changed = liveRows.Where(p => baseRows.TryGetValue(p.Key, out ulong h) && h != p.Value)
            .Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();
        return (added, removed, changed);
    }

    /// <summary>Best-guess attribution of a row for a report: "vanilla", or the mod prefix of the
    /// first non-vanilla item id in the key (the part before its first "." or "_", or the whole id
    /// when it has neither). A key with no item ids is vanilla when its first segment has no "." or
    /// "_". A mod shop id with a query stock ("shop|Mod_Shop|ALL_ITEMS...") is owned by the shop's
    /// prefix, since the shop id is the only mod-specific part. A heuristic, not a guarantee: a mod
    /// row under a vanilla-looking key reads as vanilla.</summary>
    public static string Owner(string rowKey)
    {
        string[] parts = rowKey.Split('|');
        if (parts[0] == MetaType) return Vanilla;
        var ids = parts.Skip(1).SelectMany(p => QualifiedId.Matches(p).Select(m => m.Value)).ToList();
        if (ids.Count == 0)
        {
            string first = parts.Length > 1 ? parts[1] : "";
            if (first.Length == 0 || ItemQueries.IsQuery(first)) return Vanilla;
            int end = first.IndexOfAny(PrefixEnd);
            return end < 0 ? Vanilla : first[..end];
        }
        string? foreign = ids.FirstOrDefault(id => !VanillaItemIds.All.Contains(id) && !ItemQueries.IsQuery(id));
        if (foreign == null) return Vanilla;
        string bare = BundleParsing.StripQualifier(foreign);
        int cut = bare.IndexOfAny(PrefixEnd);
        return cut < 0 ? bare : bare[..cut];
    }

    private static ulong Hash(object row)
    {
        ulong h = FnvOffset;
        foreach (byte b in Encoding.UTF8.GetBytes(JsonSerializer.Serialize(row, row.GetType(), LexiconJson.Options)))
            h = (h ^ b) * FnvPrime;
        return h;
    }
}
