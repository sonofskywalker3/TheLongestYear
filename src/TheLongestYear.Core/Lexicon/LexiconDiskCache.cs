using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security;
using System.Text.Json;

namespace TheLongestYear.Core.Lexicon;

/// <summary>The lexicon's files, game-free: the shipped load and the disk cache
/// (<c>&lt;directory&gt;/&lt;key&gt;.json</c>, our own <see cref="LexiconJson"/> text, the
/// <c>maxFiles</c> most recently written or used entries kept). Nothing here throws on an
/// unreadable or unwritable file (a corrupt entry, a denied storage permission): it logs and misses,
/// so the load falls through to the next source.</summary>
public sealed class LexiconDiskCache
{
    private const string Extension = ".json";
    private const int KeyPrefixLength = 8;

    private readonly string _directory;
    private readonly int _max;
    private readonly Action<string> _warn;
    private readonly Action<string> _trace;
    private readonly Action<string> _touch;

    /// <param name="touch">Marks an entry as just used; <see cref="File.SetLastWriteTimeUtc"/> to now
    /// unless a test replaces it.</param>
    public LexiconDiskCache(string directory, int maxFiles, Action<string> warn, Action<string> trace, Action<string>? touch = null)
    {
        _touch = touch ?? (path => File.SetLastWriteTimeUtc(path, DateTime.UtcNow));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _max = maxFiles;
        _warn = warn ?? throw new ArgumentNullException(nameof(warn));
        _trace = trace ?? throw new ArgumentNullException(nameof(trace));
    }

    /// <summary>A failure to read a lexicon file that means "no answers here", not a bug.</summary>
    public static bool IsReadFailure(Exception e)
        => e is JsonException or IOException or FormatException or UnauthorizedAccessException
            or NotSupportedException or SecurityException;

    /// <summary>A failure to write a lexicon file; JSON and argument errors cover a value the format
    /// cannot hold (a non-finite number).</summary>
    private static bool IsWriteFailure(Exception e)
        => e is IOException or UnauthorizedAccessException or NotSupportedException or SecurityException
            or JsonException or ArgumentException;

    /// <summary>The shipped set <paramref name="load"/> returns, or null when it returns null or fails
    /// (one WARN either way).</summary>
    public static AnswerSet? LoadShipped(Func<AnswerSet?> load, Action<string> warn, Action<string> trace)
    {
        if (load == null) throw new ArgumentNullException(nameof(load));
        try
        {
            var timer = Stopwatch.StartNew();
            AnswerSet? shipped = load();
            if (shipped != null)
                trace($"Lexicon: shipped answers read in {timer.ElapsedMilliseconds} ms (game {shipped.GameVersion}, variants {string.Join(", ", shipped.Variants.Keys)}).");
            else
                warn($"Lexicon: no shipped answers at {ShippedLexicon.VanillaRelativePath}; every load builds or reads the cache.");
            return shipped;
        }
        catch (Exception e) when (IsReadFailure(e))
        {
            warn($"Lexicon: the shipped answers could not be read ({e.GetType().Name}: {e.Message}); ignoring them.");
            return null;
        }
    }

    /// <summary>The entry for <paramref name="key"/>, or null when there is none or it cannot be read
    /// (one WARN). A hit is touched so pruning keeps it; a failed touch is a TRACE and the entry is
    /// still served.</summary>
    public AnswerSet? Read(string key)
    {
        string path = PathFor(key);
        AnswerSet set;
        try
        {
            if (!File.Exists(path)) return null;
            set = LexiconJson.Read(File.ReadAllText(path));
        }
        catch (Exception e) when (IsReadFailure(e))
        {
            _warn($"Lexicon: the cache entry {Short(key)} could not be read ({e.GetType().Name}: {e.Message}); rebuilding.");
            return null;
        }
        try
        {
            _touch(path);
        }
        catch (Exception e) when (IsWriteFailure(e))
        {
            _trace($"Lexicon: could not mark the cache entry {Short(key)} as used ({e.GetType().Name}: {e.Message}).");
        }
        return set;
    }

    /// <summary>Writes the entry for <paramref name="key"/>, then prunes. A failed write is one WARN
    /// (the answers are still served); a failed prune is its own TRACE.</summary>
    public void Write(string key, AnswerSet set)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(PathFor(key), LexiconJson.Write(set));
        }
        catch (Exception e) when (IsWriteFailure(e))
        {
            _warn($"Lexicon: could not write the cache entry {Short(key)} ({e.GetType().Name}: {e.Message}).");
            return;
        }
        Prune();
    }

    private void Prune()
    {
        IEnumerable<FileInfo> stale;
        try
        {
            stale = new DirectoryInfo(_directory).GetFiles("*" + Extension)
                .OrderByDescending(f => f.LastWriteTimeUtc).Skip(_max).ToList();
        }
        catch (Exception e) when (IsWriteFailure(e))
        {
            _trace($"Lexicon: could not list the cache folder to prune it ({e.GetType().Name}: {e.Message}).");
            return;
        }
        foreach (FileInfo file in stale)
        {
            try
            {
                file.Delete();
            }
            catch (Exception e) when (IsWriteFailure(e))
            {
                _trace($"Lexicon: could not prune the old cache entry {file.Name} ({e.GetType().Name}: {e.Message}).");
            }
        }
    }

    private string PathFor(string key) => Path.Combine(_directory, key + Extension);

    private static string Short(string key) => key.Length > KeyPrefixLength ? key[..KeyPrefixLength] : key;
}
